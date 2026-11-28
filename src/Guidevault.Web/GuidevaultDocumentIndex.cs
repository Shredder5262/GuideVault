using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

// Phase 1: persistent extraction only. Never modifies library files or metadata.
public static class GuidevaultDocumentIndex
{
    private const string Revision = "v8.0-phase1";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private sealed record Manifest(string Fingerprint, int TotalPages, string Revision);
    public sealed record Line(string Text, double X, double Y, double Width, double Height, double? Confidence);
    public sealed record PageRecord(int Page, string Source, string Text, double Width, double Height, string Units, List<Line> Lines, double Seconds, string? Warning, string Revision);
    private sealed record PageFailure(int Page, string Error, DateTimeOffset When);
    private sealed class OcrRecoveryException(string message) : InvalidOperationException(message) { }
    private static string FailurePath(string dir,int page)=>Path.Combine(dir,$"failure-{page:D6}.json");
    private sealed record Extraction(string Source, double Width, double Height, string Units, List<Line> Lines);
    private static string Hash(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();
    private static string Fingerprint(LibraryItem item)
    {
        var info = new FileInfo(item.Path);
        if (!info.Exists) throw new InvalidOperationException("Source magazine is unavailable.");
        return Hash(JsonSerializer.Serialize(new { info.FullName, info.Length, Modified = info.LastWriteTimeUtc.Ticks, Revision }));
    }
    private static string DirectoryFor(string root, LibraryItem item, string fingerprint) => Path.Combine(root, "document-index", Revision, Hash(item.Id), fingerprint);
    private static string PagePath(string dir, int page) => Path.Combine(dir, $"page-{page:D6}.json");
    private static async Task<T?> Read<T>(string path, CancellationToken token) where T : class
    {
        if (!File.Exists(path)) return null;
        try { await using var stream = File.OpenRead(path); return await JsonSerializer.DeserializeAsync<T>(stream, Json, token); }
        catch (JsonException) { return null; }
        catch (FileNotFoundException) { return null; }
    }
    private static async Task Write<T>(string path, T value, CancellationToken token)
    {
        var tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { await using (var stream = File.Create(tmp)) await JsonSerializer.SerializeAsync(stream, value, Json, token); token.ThrowIfCancellationRequested(); File.Move(tmp, path, true); }
        finally { try { File.Delete(tmp); } catch { } }
    }
    private static async Task<Manifest?> ReadManifest(string dir, string fp, CancellationToken token)
    {
        var m = await Read<Manifest>(Path.Combine(dir,"manifest.json"), token);
        return m is not null && m.Fingerprint == fp && m.Revision == Revision && m.TotalPages > 0 ? m : null;
    }
    private static async Task<PageRecord?> ReadPage(string dir, int page, CancellationToken token)
    {
        var p = await Read<PageRecord>(PagePath(dir,page),token);
        return p is not null && p.Page == page && p.Revision == Revision && p.Lines is not null && p.Text is not null ? p : null;
    }
    public static async Task<object> StatusAsync(string root, LibraryItem item, CancellationToken token)
    {
        var fp = Fingerprint(item); var dir = DirectoryFor(root,item,fp); var m = await ReadManifest(dir,fp,token);
        var done = new List<int>(); var warnings = new List<int>(); var recovered = new List<int>(); var failures=new List<PageFailure>(); double seconds=0;
        if(m is not null)
            for(int page=1;page<=m.TotalPages;page++)
            {
                token.ThrowIfCancellationRequested(); var p = await ReadPage(dir,page,token);
                var failure=await Read<PageFailure>(FailurePath(dir,page),token);if(failure is not null)failures.Add(failure);
                if(p is null)continue;
                done.Add(page); seconds+=p.Seconds; if(p.Warning is not null) warnings.Add(page);if(p.Source.StartsWith("tesseract-recovery",StringComparison.Ordinal))recovered.Add(page);
            }
        return new { revision=Revision, fingerprint=fp, totalPages=m?.TotalPages??0, indexedPages=done, warningPages=warnings, recoveredPages=recovered, failedPages=failures, missingPages=m is null?Array.Empty<int>():Enumerable.Range(1,m.TotalPages).Except(done).ToArray(), extractionSeconds=Math.Round(seconds,2) };
    }
    public static async Task<object> InitializeAsync(string root, LibraryItem item, CancellationToken token, int? archiveCount=null)
    {
        var fp=Fingerprint(item); var dir=DirectoryFor(root,item,fp);
        if(await ReadManifest(dir,fp,token) is null)
        {
            int total;
            if(IsPdf(item))
            {
                var info=await Run("pdfinfo",[item.Path],token);
                if(!int.TryParse(Regex.Match(info,@"(?m)^Pages:\s*(\d+)").Groups[1].Value,out total)) total=0;
            }
            else total=archiveCount??(JsonSerializer.Deserialize<string[]>(await RunArchiveWorker("list",item.Path,null,null,token),Json)?.Length??0);
            if(total<=0 || total>10000) throw new InvalidOperationException("No supported pages found, or document exceeds 10,000 pages.");
            if(Fingerprint(item)!=fp) throw new InvalidOperationException("Source changed during indexing. Restart indexing.");
            Directory.CreateDirectory(dir);
            await Write(Path.Combine(dir,"manifest.json"),new Manifest(fp,total,Revision),token);
        }
        return await StatusAsync(root,item,token);
    }
    public static async Task<PageRecord> IndexPageAsync(string root, LibraryItem item, int page, string? expected, bool force, CancellationToken token, string? archiveEntry=null, Action<string>? stage=null)
    {
        var fp=Fingerprint(item); if(fp!=expected) throw new InvalidOperationException("Source changed. Start indexing again.");
        var dir=DirectoryFor(root,item,fp); var m=await ReadManifest(dir,fp,token);
        if(m is null || page<1 || page>m.TotalPages) throw new InvalidOperationException("Initialize the document index and select a valid page first.");
        if(!force && await ReadPage(dir,page,token) is PageRecord existing) return existing;
        var timer=Stopwatch.StartNew(); var temp=Path.Combine(Path.GetTempPath(),"gv-index-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            Extraction? extracted=null;
            var n=page.ToString(CultureInfo.InvariantCulture);
            if(IsPdf(item))
            {
                stage?.Invoke("Extracting PDF text");
                try { var bbox=await Run("pdftotext",["-f",n,"-l",n,"-bbox-layout",item.Path,"-"],token); var candidate=ParsePdf(bbox); if(Usable(candidate)) extracted=candidate; }
                catch(Exception ex) when(ex is not OperationCanceledException) { /* fall back to raster OCR */ }
            }
            if(extracted is null)
            {
                string input;
                if(IsPdf(item))
                {
                    stage?.Invoke("Rendering PDF page");
                    var prefix=Path.Combine(temp,"page"); await Run("pdftoppm",["-f",n,"-l",n,"-scale-to",page==1?"3000":"2200","-singlefile","-png",item.Path,prefix],token); input=prefix+".png";
                }
                else
                {
                    stage?.Invoke("Extracting archive image (60s timeout)");
                    if(archiveEntry is null){var keys=JsonSerializer.Deserialize<string[]>(await RunArchiveWorker("list",item.Path,null,null,token),Json)??[];if(page>keys.Length)throw new InvalidOperationException("Page not in archive manifest.");archiveEntry=keys[page-1];}
                    input=Path.Combine(temp,"page.img");await RunArchiveWorker("extract",item.Path,archiveEntry,input,token);
                    if(!File.Exists(input)||new FileInfo(input).Length>40_000_000)throw new InvalidOperationException("Page image unavailable or exceeds 40 MB.");
                }
                extracted=await RecognizePage(input,temp,page,token,stage);
            }
            if(extracted.Width<=0 || extracted.Height<=0) throw new InvalidOperationException("Extraction returned invalid page geometry.");
            var text=string.Join("\n",extracted.Lines.Select(l=>l.Text));
            var record=new PageRecord(page,extracted.Source,text,extracted.Width,extracted.Height,extracted.Units,extracted.Lines,Math.Round(timer.Elapsed.TotalSeconds,2),Usable(extracted)?null:"Little readable text; this may be an image-only page or poor recognition.",Revision);
            token.ThrowIfCancellationRequested(); if(Fingerprint(item)!=fp) throw new InvalidOperationException("Source changed during extraction. Page was not saved.");
            stage?.Invoke("Saving page index");await Write(PagePath(dir,page),record,token);
            try{File.Delete(FailurePath(dir,page));}catch(IOException){}catch(UnauthorizedAccessException){}
            stage?.Invoke($"Saved page index ({record.Width:0} x {record.Height:0} {record.Units}, {record.Source}, {record.Seconds:0.00}s)"); return record;
        }
        finally { try { Directory.Delete(temp,true); } catch { } }
    }
    public static async Task<PageRecord?> GetPageAsync(string root, LibraryItem item, int page, CancellationToken token)
    {
        var fp=Fingerprint(item); var dir=DirectoryFor(root,item,fp); var m=await ReadManifest(dir,fp,token);
        return m is null || page<1 || page>m.TotalPages ? null : await ReadPage(dir,page,token);
    }
    public static async Task<object> SearchAsync(string root, LibraryItem item, string query, CancellationToken token)
    {
        query=query.Trim(); if(query.Length<2 || query.Length>200) throw new InvalidOperationException("Search requires 2–200 characters.");
        var fp=Fingerprint(item); var dir=DirectoryFor(root,item,fp); var m=await ReadManifest(dir,fp,token);
        var hits=new List<object>(); int matching=0;
        if(m is not null) for(int i=1;i<=m.TotalPages;i++)
        {
            token.ThrowIfCancellationRequested(); var p=await ReadPage(dir,i,token); if(p is null) continue;
            var at=p.Text.IndexOf(query,StringComparison.OrdinalIgnoreCase); if(at<0)continue; matching++;
            if(hits.Count<100){int start=Math.Max(0,at-100); hits.Add(new {page=i,source=p.Source,snippet=p.Text.Substring(start,Math.Min(320,p.Text.Length-start))});}
        }
        return new {matchingPages=matching,hits};
    }
    // Phase 2 deliberately reads the Phase 1 index without changing its revision or contents.
    private static bool IncompletePublisher(string value)=>Regex.IsMatch(value.TrimEnd(' ','.'),@"\b(?:of|the|and|for|by|in|at|de|du|des|van|von|&)\s*$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)||value.TrimEnd().EndsWith("&",StringComparison.Ordinal);
    public sealed record PublicationEvidence(string Field, string Value, int? Page, string Source, string Evidence);
    private static string Key(string? value) => Regex.Replace((value ?? "").ToLowerInvariant(), @"[^\p{L}\p{N}]", "");
    private static bool Meaningful(string? value) => !string.IsNullOrWhiteSpace(value) && !Regex.IsMatch(value.Trim(), @"^(?:unknown|unspecified|none|n/?a|magazine|not specified|unreviewed|multi.platform)$", RegexOptions.IgnoreCase);
    private static string EvidenceKey(string field, string? value)
    {
        var key=Key(value);
        if(field=="issueNumber" || field=="volume") return int.TryParse(key,NumberStyles.None,CultureInfo.InvariantCulture,out int n)?n.ToString(CultureInfo.InvariantCulture):key;
        if(field=="publisher") return Key(Regex.Replace(value??"",@"[,\s]+(?:Inc\.?|Ltd\.?|Limited|Corp\.?|Corporation|LLC)\s*$","",RegexOptions.IgnoreCase));
        if(field=="region") return key switch {"usa" or "us" or "unitedstatesofamerica" or "unitedstates" => "unitedstates", "uk" or "unitedkingdom" or "greatbritain" => "unitedkingdom", _=>key};
        if(field=="languageTag") return key switch {"en" or "enus" or "engb" => "english", "fr"=>"french", "de"=>"german", "es"=>"spanish", "it"=>"italian", "pt"=>"portuguese", _=>key};
        if(field=="primarySystem") return key switch {"mastersystem"=>"segamastersystem", "gamegear"=>"segagamegear", "megadrive" or "segamegadrive" or "genesis"=>"segagenesis", "snes"=>"supernintendoentertainmentsystem", "supernintendo"=>"supernintendoentertainmentsystem", _=>key};
        return key;
    }
    private static bool ContainsTitle(string text,string title)
    {
        var words=Regex.Matches(title??"",@"[\p{L}\p{N}]+").Select(m=>Regex.Escape(m.Value)).ToArray();
        return words.Length>0 && Key(title).Length>=4 && Regex.IsMatch(text,@"(?<![\p{L}\p{N}])"+string.Join(@"[^\p{L}\p{N}]+",words)+@"(?![\p{L}\p{N}])",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
    }
    private static string PlatformName(string value) => EvidenceKey("primarySystem",value) switch {
        "segamastersystem"=>"Sega Master System", "segagamegear"=>"Sega Game Gear", "segagenesis"=>"Sega Genesis", "supernintendoentertainmentsystem"=>"Super Nintendo Entertainment System", _=>value.Trim() };
    private static bool Person(string value)
    {
        if (Regex.IsMatch(value, @"\b(?:editor|chief|publisher|publishing|director|staff|department|magazine|editorial|assistant|management|production|contact|copyright|office|subscription|design|team)\b", RegexOptions.IgnoreCase)) return false;
        return Regex.IsMatch(value, @"^[\p{Lu}][\p{L}\p{M}'’.-]+(?:\s+(?:[\p{Lu}]\.|[\p{Lu}][\p{L}\p{M}'’.-]+|de|van|von|da|del)){1,4}$");
    }
    public static async Task<object> PublicationAsync(string root, LibraryItem item, IReadOnlyList<LibraryItem> library, CancellationToken token, bool allowPartial=false)
    {
        var watch = Stopwatch.StartNew(); var fp = Fingerprint(item); var dir = DirectoryFor(root,item,fp);
        var manifest = await ReadManifest(dir,fp,token) ?? throw new InvalidOperationException("Index this magazine first.");
        var fields = new (string Key, string Label, string Current, bool Shared)[] {
            ("magazineTitle","Publication title",item.MagazineTitle,false), ("issueNumber","Issue number",item.IssueNumber,false),
            ("volume","Volume",item.Volume,false), ("coverDate","Cover date",item.CoverDate,false), ("year","Year",item.Year,false),
            ("publicationDate","Publication date",item.PublicationDate,false), ("publisher","Publisher",item.Publisher,true),
            ("copyrightHolder","Copyright holder (index only)","",false), ("writer","Editor",item.Writer,false), ("region","Region",item.Region,true), ("languageTag","Language",item.LanguageTag,true),
            ("primarySystem","Primary system",item.PrimarySystem,true), ("platformFocus","Platform focus",item.PlatformFocus,true),
            ("magazineCategory","Magazine category",item.MagazineCategory,true), ("barcodeUpcIssn","ISSN / barcode",item.BarcodeUpcIssn,false),
            ("coverPrice","Cover price (index only)","",false), ("frequency","Publication frequency (index only)","",false),
            ("pageCount","Digital page count",item.PageCount>0?item.PageCount.ToString(CultureInfo.InvariantCulture):"",false)
        };
        var evidence = new List<PublicationEvidence>(); int indexed=0;
        var openingPages = new List<object>();
        var languageWords = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        var platformPages = new Dictionary<string,HashSet<int>>(StringComparer.OrdinalIgnoreCase);
        var contactOrganizations = new List<(string Name,int Page,string Proof)>();
        var footerDates = new List<(string Date,int Page,string Proof)>();
        string? coverMarket=null;
        void Add(string field,string value,int? page,string source,string proof)
        {
            value=Regex.Replace(value.Trim(),@"\s+"," ").Trim(' ',':',';','–','—');
            if(field=="coverDate"||field=="publicationDate")value=Regex.Replace(value,@",\s*"," ");
            // Do not offer a clipped organization name as authoritative metadata.
            if(field=="publisher"&&IncompletePublisher(value))return;
            if(field=="copyrightHolder")value=Regex.Replace(value,@"^by\s+","",RegexOptions.IgnoreCase);
            if(field=="writer"&&value.Any(char.IsLetter)&&!value.Any(char.IsLower)&&Person(value))value=CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.ToLowerInvariant());
            if (!Meaningful(value) || value.Length>120 || Key(value).Length==0) return;
            if (evidence.Any(e=>e.Field==field && EvidenceKey(field,e.Value)==EvidenceKey(field,value) && e.Page==page && e.Source==source)) return;
            evidence.Add(new(field,value,page,source,proof[..Math.Min(500,proof.Length)]));
        }
        const string months = @"January|February|March|April|May|June|July|August|September|October|November|December|Jan\.?|Feb\.?|Mar\.?|Apr\.?|Jun\.?|Jul\.?|Aug\.?|Sept?\.?|Oct\.?|Nov\.?|Dec\.?|Spring|Summer|Autumn|Fall|Winter";
        string datePattern = $@"(?:{months})(?:\s*[/–-]\s*(?:{months}))?\s*[,]?\s+(?:19|20)\d{{2}}";
        const RegexOptions ci = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        string titleKey=Key(item.MagazineTitle);
        const string platformPattern = @"(?:Sega\s+Master System|Master System|Sega\s+Game Gear|Game Gear|Sega\s+Genesis|Genesis|Mega[ -]Drive|Super Nintendo(?: Entertainment System)?|SNES|Nintendo 64|Nintendo Entertainment System|Game Boy(?: Color| Advance)?|PlayStation(?:\s+[2345])?|Sega Saturn|Dreamcast|TurboGrafx[ -]16|Atari 2600|Commodore 64|Amiga)";
        string filename=Path.GetFileNameWithoutExtension(item.FileName??"");
        if(string.IsNullOrWhiteSpace(filename))filename=Path.GetFileNameWithoutExtension(item.Path);
        // File identity is useful, but is not independent printed evidence.
        if(ContainsTitle(filename,item.MagazineTitle))
        {
            Add("magazineTitle",item.MagazineTitle,null,"Filename identity candidate",filename);
            var issue=Regex.Match(filename,@"(?:\bissue\s*(?:number|no\.?|#)?\s*|#)\s*(\d{1,4})(?!\d)",ci);
            if(!issue.Success)
            {
                var titleWords=Regex.Matches(item.MagazineTitle,@"[\p{L}\p{N}]+").Select(m=>Regex.Escape(m.Value)).ToArray();
                issue=Regex.Match(filename,@"^"+string.Join(@"[^\p{L}\p{N}]+",titleWords)+@"[ _.-]+(\d{1,3})(?=$|[ _.-]|[([])",ci);
            }
            if(issue.Success && int.TryParse(issue.Groups[1].Value,out int fileIssue) && fileIssue>0 && fileIssue is not (>=1900 and <=2099)) Add("issueNumber",fileIssue.ToString(CultureInfo.InvariantCulture),null,"Filename identity candidate",filename);
            var fileDate=Regex.Match(filename,datePattern,ci);
            if(fileDate.Success){Add("coverDate",fileDate.Value,null,"Filename identity candidate",filename);Add("year",Regex.Match(fileDate.Value,@"(?:19|20)\d{2}").Value,null,"Filename identity candidate",filename);}
        }
        for (int page=1;page<=manifest.TotalPages;page++)
        {
            token.ThrowIfCancellationRequested(); var p=await ReadPage(dir,page,token); if(p is null) continue; indexed++;
            if(page<=3) openingPages.Add(new {page,source=p.Source,warning=p.Warning,text=p.Text[..Math.Min(3500,p.Text.Length)],truncated=p.Text.Length>3500});
            // Sample readable words across the issue; avoid inferring language from the OCR model setting.
            foreach(Match word in Regex.Matches(p.Text,@"\b[\p{L}]{2,20}\b").Take(400))
                languageWords[word.Value]=languageWords.GetValueOrDefault(word.Value)+1;
            foreach(Match platform in Regex.Matches(p.Text,@"\b"+platformPattern.Replace("|Genesis|","|")+@"\b",ci))
            {
                var value=PlatformName(platform.Value);
                if(!platformPages.TryGetValue(value,out var seen))platformPages[value]=seen=new HashSet<int>();
                seen.Add(page);
            }
            bool titleOnPage=ContainsTitle(p.Text,item.MagazineTitle);
            if(page==1)
            {
                var imprint=Regex.Match(p.Text,@"(?im)^([^\r\n]{0,100}"+datePattern+@"[^\r\n]{0,30})$");
                if(imprint.Success&&ContainsTitle(imprint.Value,item.MagazineTitle))
                {
                    var date=Regex.Match(imprint.Value,datePattern,ci);
                    if(date.Success){Add("coverDate",date.Value,page,"Printed cover imprint",imprint.Value);Add("year",Regex.Match(date.Value,@"(?:19|20)\d{2}").Value,page,"Printed cover imprint",imprint.Value);}
                }
                var guide=GuidePlatform(p.Text);
                var dedication=PrintedReviewPlatform(p.Text);
                if(dedication is not null)Add("primarySystem",dedication,page,"Explicit cover editorial dedication",p.Text[..Math.Min(500,p.Text.Length)]);
                foreach(Match barcode in Regex.Matches(p.Text,@"(?m)^\s*(\d)\s*(?:\r?\n|\s+)\s*(\d{6})[\s°·-]*(\d{6})\s*$"))
                {
                    var digits=barcode.Groups[1].Value+barcode.Groups[2].Value+barcode.Groups[3].Value;
                    if(Enumerable.Range(0,13).Sum(k=>(digits[k]-'0')*(k%2==0?1:3))%10==0)Add("barcodeUpcIssn",digits,page,"Checksum-valid printed EAN-13",barcode.Value);
                }
                if(guide is not null)Add("primarySystem",guide,page,"Explicit cover guide subject",p.Text[..Math.Min(500,p.Text.Length)]);
                var market=Regex.Match(p.Text,@"(?im)^\s*\$\s*\d{1,3}[.,]\d{2}\s*[^\r\n\d]{0,12}\$\s*\d{1,3}[.,]\d{2}\s*Canada\b");
                if(market.Success){coverMarket="USA";Add("region","USA",page,"North American cover pricing candidate",market.Value);}
                if(Regex.IsMatch(p.Text,@"\b(?:U\.?S\.?A?\.?|United States)\s*\$\s*\d",ci))
                {coverMarket="USA";Add("region","USA",page,"US-labelled cover price candidate",p.Text[..Math.Min(500,p.Text.Length)]);}
            }
            if(page>1 && p.Height>0)
            foreach(var footer in p.Lines.Where(l=>l.Y>=p.Height*.89&&l.Text.Length<=120&&!(l.Confidence is double c&&c<45)))
            {
                var d=Regex.Match(footer.Text,datePattern,ci);if(!d.Success)continue;
                if(ContainsTitle(footer.Text,item.MagazineTitle)||Regex.IsMatch(footer.Text,@"^\s*(?:\d{1,4}\s+)?"+datePattern+@"(?:\s+\d{1,4})?\s*$",ci))footerDates.Add((d.Value,page,footer.Text));
            }
            // Publication-related correspondence, not arbitrary reader addresses or advertisements.
            // Restrict to opening/closing editorial pages; require an organizational address and publication context.
            if((page<=8 || page>=manifest.TotalPages-1)&&!SubscriptionAdvertisement(p.Text))
            {
                const string organization=@"([\p{Lu}][\p{L}\p{N} &'’.\-]{1,75}?(?:,?\s+(?:Inc\.?|Ltd\.?|Limited|Corporation|Corp\.?|LLC|Company|Publications|Publishing|Press)))";
                const string usAddress=@"\b\d{1,6}\s+[^\n]{3,95},\s*[^\n,]{2,45},?\s+(?:AL|AK|AZ|AR|CA|CO|CT|DE|FL|GA|HI|ID|IL|IN|IA|KS|KY|LA|ME|MD|MA|MI|MN|MS|MO|MT|NE|NV|NH|NJ|NM|NY|NC|ND|OH|OK|OR|PA|RI|SC|SD|TN|TX|UT|VT|VA|WA|WV|WI|WY|DC)\s+\d{5}(?:-\d{4})?\b";
                var textLines=p.Text.Split('\n');
                for(int j=0;j<textLines.Length;j++)
                {
                    string addressLine=textLines[j].Trim();
                    if(!Regex.IsMatch(addressLine,usAddress,RegexOptions.CultureInvariant))continue;
                    var context=string.Join(" ",textLines.Skip(Math.Max(0,j-3)).Take(Math.Min(4,j+1)));
                    if(!Regex.IsMatch(context,@"\b(?:newsletter|magazine|editorial|publication|subscription|mailing label|change of address|mailbag|letters to the editor)\b",ci))continue;
                    var org=Regex.Match(addressLine,organization+@"\s*,\s*(?=\d{1,6}\s)",RegexOptions.CultureInvariant);
                    if(!org.Success)continue;
                    string name=org.Groups[1].Value.Trim();
                    if(name.Length<3)continue;
                    Add("region","USA",page,"Publication correspondence address candidate",context);
                    contactOrganizations.Add((name.TrimEnd('.'),page,context));
                }
                // Explicit editorial announcement of a change in issues per year. Avoid reader wishes or ads.
                foreach(Match m in Regex.Matches(p.Text,@"\bwe\s+(?:are\s+now\s+|are\s+|will\s+be\s+)?(?:expanding|increasing|changing)\s+from\s+(?:\d{1,2}|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve)\s+issues\s+(?:a|per|each)\s+year\s+to\s+(\d{1,2}|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve)\b",ci))
                {
                    var numbers=new[]{"","one","two","three","four","five","six","seven","eight","nine","ten","eleven","twelve"};
                    int n=int.TryParse(m.Groups[1].Value,out int parsed)?parsed:Array.IndexOf(numbers,m.Groups[1].Value.ToLowerInvariant());
                    if(n>0 && n<=52)Add("frequency",n.ToString(CultureInfo.InvariantCulture)+" issues per year",page,"Printed publication frequency announcement",m.Value);
                }
            }
            if(page>=2&&page<=8&&!SubscriptionAdvertisement(p.Text)&&Regex.IsMatch(p.Text,@"\b(?:this issue|the magazine|our readers|letters coming|editorial|welcome to)\b",ci))
            {
                foreach(Match signature in Regex.Matches(p.Text,@"(?m)^\s*([^\r\n]{3,65})\s*\r?\n[ \t]*(?:Editor|Editor[ -]+in[ -]+Chief)[ \t]*\r?\n[ \t]*([^\s@]+@[^\s@]+\.[A-Za-z]{2,})[ \t]*$"))
                {
                    var person=signature.Groups[1].Value.Trim();var email=signature.Groups[2].Value;
                    if(Person(person))
                    {
                        Add("writer",person,page,"Signed editorial credit",signature.Value);
                        if(email.EndsWith(".co.uk",StringComparison.OrdinalIgnoreCase))Add("region","UK",page,"Editorial contact region candidate",signature.Value);
                        if(Meaningful(item.Publisher)&&Key(item.Publisher.Split(' ')[0]).Length>=4&&email.Contains("@"+item.Publisher.Split(' ')[0]+".",StringComparison.OrdinalIgnoreCase))Add("publisher",item.Publisher,page,"Editorial contact publisher candidate",signature.Value);
                    }
                }
            }
            bool creditPage=Regex.IsMatch(p.Text,@"\b(?:editor[\s–—-]+in[\s–—-]+chief|ISSN|masthead|publication date|publisher\s*:|published\s+(?:by|monthly|weekly|quarterly|biweekly|bimonthly|annually)|all rights reserved|copyright)\b|©",ci);
            bool roles=Regex.IsMatch(p.Text,@"\b(?:editor[\s–—-]+in[\s–—-]+chief|managing editor|executive editor|art director)\b|\beditor\s*:",ci);
            bool masthead=!SubscriptionAdvertisement(p.Text)&&creditPage && (titleOnPage || roles && Regex.IsMatch(p.Text,@"\b(?:publisher|published|ISSN|managing editor|art director)\b",ci));
            if(masthead)
            {
                foreach(Match frequencyMatch in Regex.Matches(p.Text,@"\b(?:published|issued|appears)\s+(quar-\s*terly|month-\s*ly|week-\s*ly|annu-\s*ally)\b",ci))
                    Add("frequency",Regex.Replace(frequencyMatch.Groups[1].Value,@"-\s*",""),page,"Wrapped masthead frequency",frequencyMatch.Value);
            }
            if(page>8 && !creditPage)continue;
            var lines=p.Lines.Where(l=>!string.IsNullOrWhiteSpace(l.Text)).ToList();
            var coverDates=page==1?lines.Where(l=>Regex.IsMatch(l.Text,datePattern,ci) && !(l.Confidence is double c && c<45)).ToArray():Array.Empty<Line>();
            for(int i=0;i<lines.Count;i++)
            {
                var line=lines[i]; var text=line.Text.Trim(); if(text.Length>500) continue;
                if(line.Confidence is double conf && conf<45) continue;
                void Match(string field,string pattern, string? source=null)
                {
                    foreach(Match m in Regex.Matches(text,pattern,ci)) Add(field,m.Groups[1].Value,page,source??"Printed text",text);
                }
                bool nearCoverDate=page==1 && coverDates.Any(d=>Math.Abs(d.Y-line.Y)<=Math.Max(Math.Max(d.Height,line.Height)*2.0,p.Height*.015));
                bool titleLine=ContainsTitle(text,item.MagazineTitle);
                if(titleLine && ((page==1 && (Key(text)==titleKey || line.Y<p.Height*.30 || nearCoverDate)) || masthead && Key(text)==titleKey))
                    Add("magazineTitle",item.MagazineTitle,page,"Printed publication identity",text);
                if(page==1 && (nearCoverDate || line.Y<p.Height*.22 || line.Y>p.Height*.82))
                {
                    // A standalone #number near the cover date is a common issue marker, not an article number.
                    var marker=Regex.Match(text,@"^\s*#\s*(\d{1,4})\s*$",ci);
                    if(!marker.Success && nearCoverDate)marker=Regex.Match(text,datePattern+@"\s*[,–—-]?\s*#\s*(\d{1,4})\s*$",ci);
                    if(marker.Success && nearCoverDate && int.TryParse(marker.Groups[1].Value,out int coverIssue) && coverIssue>0 && coverIssue is not (>=1900 and <=2099))
                        Add("issueNumber",coverIssue.ToString(CultureInfo.InvariantCulture),page,"Issue marker near cover date",text+" / "+string.Join(" / ",coverDates.Take(2).Select(d=>d.Text)));
                    if(Regex.IsMatch(text,@"\b(?:the\s+)?[\p{L}\p{N} &'-]{0,60}\bnewsletter\b",ci) && (nearCoverDate || titleLine || Regex.IsMatch(text,@"\bofficial newsletter\b",ci)))
                        Add("magazineCategory","Newsletter",page,"Printed publication subtitle",text);
                    var date=Regex.Match(text,datePattern,ci);
                    if(date.Success && (nearCoverDate && (titleLine || Regex.IsMatch(text,@"\bnewsletter\b|\bissue\b|#\s*\d+",ci))))
                    {Add("coverDate",date.Value,page,"Printed cover imprint",text);Add("year",Regex.Match(date.Value,@"(?:19|20)\d{2}").Value,page,"Printed cover imprint",text);}
                }
                if (page==1 || masthead)
                {
                    foreach(Match m in Regex.Matches(text,@"\b(?:issue\s*(?:number|no\.?|#)?|vol(?:ume)?\.?\s*\d{1,3}\s*[,;]?\s*(?:no\.?|number))\s*#?\s*(\d{1,4})\b",ci))
                        if(int.TryParse(m.Groups[1].Value,out var num)&&num>0&&num is not (>=1900 and <=2099)) Add("issueNumber",num.ToString(CultureInfo.InvariantCulture),page,"Printed issue label",text);
                    Match("volume",@"\bvol(?:ume)?\.?\s*(\d{1,3}|[IVXLCDM]{1,7})\b");
                    if(page==1 && Regex.IsMatch(text,@"^(?:\s*(?:issue\s*#?\d+\s*[-–,:]?\s*)?"+datePattern+@"\s*[.!]?)$",ci))
                    {
                        var m=Regex.Match(text,datePattern,ci); Add("coverDate",m.Value,page,"Printed cover date",text); Add("year",Regex.Match(m.Value,@"(?:19|20)\d{2}").Value,page,"Printed cover date",text);
                    }
                    Match("coverDate",@"\bcover date\s*:\s*("+datePattern+")");
                    Match("publicationDate",@"\b(?:publication date|published on)\s*:?\s*("+datePattern+")");
                }
                if(page==1)
                    Match("coverPrice",@"(?:^|\s)((?:US\s*\$|U\.S\.A\.\s*\$|USA\s*\$|CAN(?:ADA)?\s*\$|U\.?K\.?\s*£|£|\$|€)\s*\d{1,3}[.,]\d{2})(?!\d)","Cover price candidate");
                // Publication credits can be on the back page or a colophon rather than a labeled masthead.
                // Join only physically adjacent lines with similar left alignment to avoid crossing columns.
                var following=Regex.IsMatch(text,@"\b(?:published|publisher|editorial office|publication office|subscriptions)\b",ci)?lines.Where(l=>l.Y>line.Y+line.Height*.45 && l.Y<=line.Y+Math.Max(line.Height*3.0,p.Height*.012) && Math.Abs(l.X-line.X)<=Math.Max(p.Width*.035,12) && !(l.Confidence is double c && c<50)).OrderBy(l=>l.Y).FirstOrDefault():null;
                string joined=following is null?text:text+" "+following.Text.Trim();
                bool linkedCredit=titleLine || ContainsTitle(joined,item.MagazineTitle) || masthead;
                if(linkedCredit && (page<=8 || creditPage))
                {
                    const string publisherPattern=@"\bpublished(?:\s+(?:monthly|weekly|quarterly|annually|bimonthly|biweekly|quar-\s*terly|month-\s*ly|week-\s*ly|annu-\s*ally))?\s+by\s+([\p{L}][\p{L}\p{N} &'’.()-]{2,90}?)(?=\s+(?:at|located|ISSN|copyright|all rights|subscription)\b|[,;]|$)";
                    var pub=Regex.Match(text,publisherPattern,ci);
                    if(!pub.Success||IncompletePublisher(pub.Groups[1].Value))pub=Regex.Match(joined,publisherPattern,ci);
                    if(!pub.Success&&masthead&&titleLine&&Regex.IsMatch(text,@"\bpublished\b",ci))
                    {
                        var wrapped=Regex.Match(p.Text,@"\bpublished(?:\s+(?:monthly|weekly|quarterly|annually|bimonthly|biweekly|quar-\s*terly|month-\s*ly|week-\s*ly|annu-\s*ally))?\s+by\s+([\p{L}][\p{L}\p{N} &'’.()-]{2,90}?)(?=\s+(?:at|located|ISSN|copyright|all rights|subscription)\b|[,;]|\r?\n|$)",ci);
                        if(wrapped.Success&&wrapped.Value.Length<=150)Add("publisher",wrapped.Groups[1].Value.TrimEnd('.'),page,"Wrapped masthead publisher credit",wrapped.Value);
                    }
                    if(pub.Success)Add("publisher",pub.Groups[1].Value.TrimEnd('.'),page,"Publication-linked publisher credit",text==joined||Regex.IsMatch(text,publisherPattern,ci)?text:joined);
                    // Copyright holder is not necessarily publisher; record the explicit copyright separately.
                    var rights=Regex.Match(text,@"(?:©|\(c\)|\bcopyright\b)\s*(?:(?:19|20)\d{2}\s*[,–-]?\s*)?([\p{L}][\p{L}\p{N} &'’.()-]{2,80}?)(?=\s+(?:all rights|copyright|ISSN|printed)\b|[,;]|$)",ci);
                    if(rights.Success && !CoverArtCredit.IsMatch(text) && !Regex.IsMatch(text,@"\blicensed from\b",ci) && (masthead || titleLine) && Regex.IsMatch(rights.Groups[1].Value,@"\b(?:inc\.?|ltd\.?|limited|corporation|corp\.?|company|publishing|publications|press|media|of America)\b",ci))
                        Add("copyrightHolder",rights.Groups[1].Value.TrimEnd('.'),page,"Printed copyright statement (not publisher credit)",text);
                    Match("region",@"\b(?:printed|published) in (?:the )?(United States(?: of America)?|U\.?S\.?A\.?|United Kingdom|U\.?K\.?|Canada|Australia|Japan|Germany|France)\b","Printed publication location");
                    if(Regex.IsMatch(joined,@"\b(?:published by|publisher|editorial office|publication office|subscriptions)\b",ci) && Regex.IsMatch(joined,@"\b(?:AL|AK|AZ|AR|CA|CO|CT|DE|FL|GA|HI|ID|IL|IN|IA|KS|KY|LA|ME|MD|MA|MI|MN|MS|MO|MT|NE|NV|NH|NJ|NM|NY|NC|ND|OH|OK|OR|PA|RI|SC|SD|TN|TX|UT|VT|VA|WA|WV|WI|WY|DC)\s+\d{5}(?:-\d{4})?\b",RegexOptions.CultureInvariant))
                        Add("region","USA",page,"Publication address candidate",joined);
                    var frequency=Regex.Match(joined,@"\b(?:published|issued|appears)\s+(monthly|weekly|quarterly|annually|bimonthly|biweekly|quar-\s*terly)\b",ci);
                    if(frequency.Success)Add("frequency",Regex.Replace(frequency.Groups[1].Value,@"-\s*", ""),page,"Printed publication frequency",joined);
                }
                if((page==1 || masthead) && (nearCoverDate || titleLine || masthead || line.Y<p.Height*.25))
                {
                    var dedication=Regex.Match(text,@"\b(?:100\s*%\s*(?:Sega\s+)?|(?:official|dedicated|exclusively)\s+(?:magazine|newsletter|coverage|for|to|about|of)\s+(?:for\s+|to\s+|the\s+)*)("+platformPattern+@")\b",ci);
                    if(!dedication.Success)dedication=Regex.Match(text,@"\b("+platformPattern+@")\s+(?:owners['’]?\s+|gaming\s+)?(?:magazine|newsletter)\b",ci);
                    if(dedication.Success)Add("primarySystem",PlatformName(dedication.Groups[1].Value),page,"Explicit publication platform dedication",text);
                }
                if(!masthead) continue;
                if(Regex.IsMatch(text,@"\bISSN\b",ci)&&Regex.IsMatch(text,@"\bvol(?:ume)?\.?\s*\d+",ci))
                {
                    var mastheadDate=Regex.Match(text,datePattern,ci);
                    if(mastheadDate.Success){Add("coverDate",mastheadDate.Value,page,"Printed masthead issue date",text);Add("year",Regex.Match(mastheadDate.Value,@"(?:19|20)\d{2}").Value,page,"Printed masthead issue date",text);}
                }
                Match("magazineTitle",@"^([\p{L}][\p{L}\p{N} &'’:.!-]{2,75}?)\s*\(\s*ISSN\b");
                Match("magazineTitle",@"^(?:magazine|publication) title\s*:\s*(.{3,100})$");
                Match("publisher",@"^publisher\s*[:–—-]\s*([\p{L}][\p{L}\p{N} &'’.()-]{2,80})$","Masthead credit");
                // Anchored complete role, never fall back to a partial Editor match on Editor-in-Chief.
                var editor=Regex.Match(text,@"^(?:editor[\s–—-]+in[\s–—-]+chief|editor)(?:\s*[:–—-]\s*|\s+)(.+)$",ci);
                if(editor.Success && Person(editor.Groups[1].Value.Trim())) Add("writer",editor.Groups[1].Value,page,"Masthead credit",text);
                if(Regex.IsMatch(text,@"^(?:editor[\s–—-]+in[\s–—-]+chief|editor)\s*[:–—-]?\s*$",ci))
                {
                    var next=lines.Where(l=>l.Y>=line.Y+line.Height*.6 && l.Y<=line.Y+line.Height*3.5 && Math.Abs(l.X-line.X)<=Math.Max(line.Width*.6,p.Width*.035)).OrderBy(l=>l.Y).FirstOrDefault();
                    if(next is not null && Person(next.Text.Trim()) && !(next.Confidence is double nc && nc<55)) Add("writer",next.Text,page,"Adjacent masthead credit",text+" / "+next.Text);
                }
                Match("region",@"\b(?:printed|published) in (?:the )?(United States(?: of America)?|USA|United Kingdom|Canada|Australia|Japan|Germany|France)\b");
                Match("languageTag",@"^language\s*:\s*(English|French|German|Spanish|Japanese|Italian|Portuguese|Dutch)\b");
                Match("platformFocus",@"^(?:platform focus|audience focus)\s*:\s*(.{3,100})$");
                Match("magazineCategory",@"^(?:magazine category|publication category)\s*:\s*(.{3,100})$");
                Match("frequency",@"\bpublished\s+(monthly|weekly|quarterly|annually|bimonthly|biweekly)\b");
                foreach(Match m in Regex.Matches(text,@"\bISSN\s*[:#]?\s*(\d{4}[- ]?\d{3}[\dX])\b",ci))
                {
                    var code=Regex.Replace(m.Groups[1].Value,@"[- ]","").ToUpperInvariant(); int sum=0;
                    for(int j=0;j<8;j++)sum+=(code[j]=='X'?10:code[j]-'0')*(8-j);
                    if(sum%11==0)Add("barcodeUpcIssn",code.Insert(4,"-"),page,"Checksum-valid printed ISSN",text);
                }
            }

        }
        if(indexed==0||!allowPartial&&indexed!=manifest.TotalPages)throw new InvalidOperationException($"Document index incomplete ({indexed}/{manifest.TotalPages}). Resume indexing first.");
        Add("pageCount",manifest.TotalPages.ToString(CultureInfo.InvariantCulture),null,"Document manifest","Number of indexed digital pages; not necessarily printed pagination.");
        // Language inference uses distinctive common words, not file names or the OCR engine language setting.
        var languageSets=new Dictionary<string,string[]> {
            ["English"]=["the","and","with","from","this","that","your","have","will","which","their","you","are","for"],
            ["French"]=["les","des","une","avec","dans","pour","vous","est","sur","nous","cette","sont"],
            ["German"]=["der","die","das","und","mit","ist","auf","nicht","eine","auch","den","sich"],
            ["Spanish"]=["los","las","una","para","con","que","del","por","como","esta","sus","juego"],
            ["Italian"]=["della","delle","gli","sono","una","che","con","nel","per","questo","anche","dei"],
            ["Portuguese"]=["uma","dos","das","com","para","que","não","está","mais","este","pela","você"]
        };
        var scores=languageSets.Select(l=>new {name=l.Key,hits=l.Value.Sum(w=>Math.Min(languageWords.GetValueOrDefault(w),20)),distinct=l.Value.Count(w=>languageWords.GetValueOrDefault(w)>0)}).OrderByDescending(l=>l.hits).ToArray();
        if(scores[0].hits>=25 && scores[0].distinct>=7 && scores[0].hits>=scores[1].hits*2.0)
            Add("languageTag",scores[0].name,null,"Indexed language inference",$"{scores[0].distinct} distinct common words; capped hits {scores[0].hits} vs {scores[1].hits} for {scores[1].name}. Not an explicit printed language label.");
        // Only suggest a correspondence organization as publisher when repeated or consistent with current metadata;
        // never promote it to a direct publishing credit. A direct printed publisher credit takes precedence.
        if(!evidence.Any(e=>e.Field=="publisher"))
        {
            foreach(var group in contactOrganizations.GroupBy(x=>EvidenceKey("publisher",x.Name)))
            {
                var contacts=group.ToArray();
                if(contacts.Select(x=>x.Page).Distinct().Count()<2 && EvidenceKey("publisher",item.Publisher)!=group.Key)continue;
                foreach(var c in contacts)Add("publisher",c.Name,c.Page,"Publication correspondence organization candidate (not proof of publisher)",c.Proof);
            }
        }
        foreach(var dates in footerDates.GroupBy(x=>Key(x.Date)).Where(g=>g.Select(x=>x.Page).Distinct().Count()>=3))
            foreach(var d in dates.Take(4)){Add("coverDate",d.Date,d.Page,"Repeated running issue date",d.Proof);Add("year",Regex.Match(d.Date,@"(?:19|20)\d{2}").Value,d.Page,"Repeated running issue date",d.Proof);}
        var explicitSystems=evidence.Where(e=>e.Field=="primarySystem" && e.Source=="Explicit publication platform dedication").GroupBy(e=>EvidenceKey("primarySystem",e.Value)).ToArray();
        if(explicitSystems.Length==1){var e=explicitSystems[0].First();Add("platformFocus",e.Value,e.Page,"Explicit publication platform dedication",e.Evidence);}
        // Platform mentions are coverage signals only; never infer exclusive dedication from game references.
        var platformSignals=platformPages.OrderByDescending(k=>k.Value.Count).Take(12).Select(k=>new {platform=k.Key,pages=k.Value.OrderBy(x=>x).ToArray()}).ToArray();
        // Reuse only explicitly reviewed or locked peers, exact publication title and compatible region/language.
        var printedRegions=evidence.Where(e=>e.Field=="region"&&!e.Source.Contains("candidate",StringComparison.OrdinalIgnoreCase)).Select(e=>EvidenceKey("region",e.Value)).Distinct().ToArray();
        var editionRegion=printedRegions.Length==1?printedRegions[0]:coverMarket is not null?EvidenceKey("region",coverMarket):EvidenceKey("region",item.Region);
        bool editionMismatch=Meaningful(item.Region)&&editionRegion.Length>0&&editionRegion!=EvidenceKey("region",item.Region);
        var peers=library.Where(x=>x.Id!=item.Id && x.Kind=="Magazine" && titleKey.Length>=4 && Key(x.MagazineTitle)==titleKey &&
            (editionRegion.Length==0||(!editionMismatch&&!Meaningful(x.Region))||EvidenceKey("region",x.Region)==editionRegion) && (!Meaningful(item.LanguageTag)||!Meaningful(x.LanguageTag)||EvidenceKey("languageTag",item.LanguageTag)==EvidenceKey("languageTag",x.LanguageTag))).ToArray();
        foreach(var field in fields.Where(f=>f.Shared))
        {
            string Value(LibraryItem x)=>field.Key switch {"publisher"=>x.Publisher,"region"=>x.Region,"languageTag"=>x.LanguageTag,"primarySystem"=>x.PrimarySystem,"platformFocus"=>x.PlatformFocus,"magazineCategory"=>x.MagazineCategory,_=>""};
            foreach(var group in peers.Where(x=>MetadataLockHelper.IsLocked(x.MetadataLocks,field.Key)||MetadataStatusHelper.Normalize(x.MetadataStatus) is MetadataStatusHelper.Reviewed or MetadataStatusHelper.Locked).Where(x=>Meaningful(Value(x))).GroupBy(x=>EvidenceKey(field.Key,Value(x))))
            {
                var samples=group.ToArray(); Add(field.Key,Value(samples[0]),null,"Reviewed publication peer",$"{samples.Length} related issue(s): "+string.Join(", ",samples.Take(4).Select(x=>x.Title+" #"+x.IssueNumber)));
            }
        }
        if(Fingerprint(item)!=fp)throw new InvalidOperationException("Source changed. Re-index the magazine first.");
        // Resolve a printed corporate initialism only when a unique expanded reviewed peer supports it.
        string PublisherBase(string name)=>Regex.Replace(name,@"[,\s]+(?:Inc\.?|Ltd\.?|Limited|Corp\.?|Corporation|LLC)\s*$","",RegexOptions.IgnoreCase).Trim();
        string Initials(string name)=>string.Concat(Regex.Matches(PublisherBase(name),@"[\p{L}]+(?:'[\p{L}]+)?").Select(m=>char.ToLowerInvariant(m.Value[0])));
        var expanded=evidence.Where(e=>e.Field=="publisher"&&e.Source=="Reviewed publication peer"&&Regex.Matches(PublisherBase(e.Value),@"[\p{L}]{2,}").Count>=2).Select(e=>e.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        string GroupKey(PublicationEvidence e)
        {
            if(e.Field=="magazineTitle"&&Meaningful(item.MagazineTitle))
            {
                var presented=Regex.Match(e.Value,@"^.{2,50}\s+presents\s*[:–—-]?\s*(.+)$",ci);
                if(presented.Success&&Key(presented.Groups[1].Value)==titleKey)return titleKey;
            }
            if(e.Field=="publisher")
            {
                var core=PublisherBase(e.Value);var letters=Key(core);
                if(letters.Length is >=2 and <=6&&Regex.IsMatch(core,@"^[A-Z.\s-]+$"))
                {var matches=expanded.Where(x=>Initials(x)==letters).ToArray();if(matches.Length==1)return EvidenceKey("publisher",matches[0]);}
            }
            return EvidenceKey(e.Field,e.Value);
        }
        var report=fields.Select(f=>{
            var candidates=evidence.Where(e=>e.Field==f.Key).GroupBy(GroupKey).Select(g=>new {value=g.OrderByDescending(e=>f.Key=="magazineTitle"&&Key(e.Value)==titleKey).ThenByDescending(e=>f.Key=="publisher"&&e.Source=="Reviewed publication peer"&&expanded.Contains(e.Value)).First().Value,evidence=g.OrderBy(e=>e.Source.Contains("candidate",StringComparison.OrdinalIgnoreCase)||e.Source.Contains("inference",StringComparison.OrdinalIgnoreCase)?1:0).Take(8).ToArray(),support=g.All(e=>e.Source.Contains("candidate",StringComparison.OrdinalIgnoreCase)||e.Source.Contains("inference",StringComparison.OrdinalIgnoreCase))?"Indirect":"Direct or reviewed"}).ToArray();
            var status=candidates.Length>1?(f.Key=="coverPrice"?"Multiple price candidates":"Conflicting evidence"):candidates.Length==0?"No indexed evidence":!Meaningful(f.Current)?"Candidate":EvidenceKey(f.Key,f.Current)==EvidenceKey(f.Key,candidates[0].value)&&(f.Key!="writer"||string.Equals(f.Current,candidates[0].value,StringComparison.Ordinal))?"Matches current":"Differs from current";
            return new {field=f.Key,label=f.Label,current=f.Current,locked=MetadataLockHelper.IsLocked(item.MetadataLocks,f.Key),status,candidates};
        }).ToArray();
        return new {revision="v8.0-phase2.8.1",indexedPages=indexed,totalPages=manifest.TotalPages,relatedIssues=peers.Length,editionMismatch,editionRegionCandidate=coverMarket,seconds=Math.Round(watch.Elapsed.TotalSeconds,2),fields=report,openingPages,platformSignals};
    }
    // Phase 3: read-only editorial analysis over the persistent page index.
    // Cover wording establishes a guide's focus, not exclusive publication identity.
    private static string? GuidePlatform(string text)
    {
        var m=Regex.Match(text,@"(?i)\b(?:guide\s+to|guide\s+for)\s+(?:over\s+|more\s+than\s+)?(?:\d{1,4}\s+)?(Genesis|Mega[ -]Drive|(?:Sega )?Master System|(?:Sega )?Game Gear|Super NES|SNES|Super Nintendo)\s+g(?:ames?\b|[.\r\n])");
        return m.Success?PlatformName(m.Groups[1].Value):null;
    }
    private static string? PrintedReviewPlatform(string text)
    {
        var m=Regex.Match(text,@"\b(?:every\s+new|all\s+the\s+latest|reviews?\s+of\s+(?:every\s+new\s+)?)(?:the\s+)?(Saturn|Sega Saturn|Nintendo Entertainment System|NES|Super NES|SNES|Genesis|Mega[ -]Drive|Master System|Game Gear)\s+games?\b",RegexOptions.IgnoreCase);
        if(!m.Success)return null;
        return m.Groups[1].Value.ToLowerInvariant() switch {"saturn" or "sega saturn"=>"Sega Saturn","nes"=>"Nintendo Entertainment System","super nes"=>"Super Nintendo Entertainment System",_=>PlatformName(m.Groups[1].Value)};
    }
    private static readonly Regex ReviewBody=new(@"\b(?:graphics|sound|playability|gameplay|lastability|verdict|rating|value for money|arcade conversion|reviewed|controls?|presentation)\b",RegexOptions.IgnoreCase|RegexOptions.Compiled);
    private static readonly Regex ArticlePreview=new(@"\b(?:eagerly awaited|forthcoming|upcoming|in development|due for release|scheduled for release|first look|sneak preview|previewed)\b",RegexOptions.IgnoreCase|RegexOptions.Compiled);
    private static readonly Regex InstructionBody=new(@"\b(?:press|hold|enter)\s+(?:the\s+)?(?:following|code|button|start|select|up|down|left|right|[abxy]\b)|\b(?:secret\s+(?:areas?|rooms?|passages?|doors?|warps?|levels?|items?|codes?|paths?|exits?|tricks?)|special moves|level codes|cheat menu|stage\s+\d+|passwords?|walkthrough|how to defeat)\b",RegexOptions.IgnoreCase|RegexOptions.Compiled);
    private static bool AcquisitionContext(string text)=>Regex.IsMatch(text,@"\b(?:going to get|going to buy|want to buy|picked up|buying|wish list|birthday present|favorite games?|favourite games?)\b",RegexOptions.IgnoreCase);
    private static bool SubscriptionAdvertisement(string text)=>Regex.IsMatch(text,@"\b(?:start my|introductory subscription|subscribe now|please include|make check or money order payable|mail to|reader service card)\b",RegexOptions.IgnoreCase)&&Regex.IsMatch(text,@"\b(?:subscription|subscribe)\b",RegexOptions.IgnoreCase)&&Regex.IsMatch(text,@"\b(?:name\s+address|city\s+state\s+zip|check or money order|reader service card|please include\s*\$)\b",RegexOptions.IgnoreCase);
    private static readonly Regex ProfileSpec=new(@"\b(?:size\s*:\s*\d+\s*(?:meg|mb|kb)|difficulty\s*:\s*\w+|(?:no\.? of )?players?\s*:\s*\d+|(?:rating|graphics|sound|playability|genre|type)\s*:\s*[\p{L}\d])",RegexOptions.IgnoreCase|RegexOptions.Compiled);
    private static bool ProfileSuffix(string text)=>Regex.IsMatch(text,@"^\s*[-–—]\s*[\p{Lu}\d][\p{Lu}\d &'’.]{1,50}(?:\s*[|.;~]|\s+\d{1,2}\s+[a-z]{1,3})?\s*$");
    private sealed record EditorialTitle(string Title, string Platform, bool Alias);
    private sealed record EditorialHit(string Title, int Page, string Kind, string Evidence, string[] Platforms, bool Cover, bool Contents, double Height, string[]? ExplicitPlatforms=null);
    private static readonly Regex CoverArtCredit = new(@"^\s*(?:front\s+)?cover\s+(?:art(?:work)?|illustration|image|photo(?:graph)?)\s*[:–—-]\s*",RegexOptions.IgnoreCase|RegexOptions.Compiled);
    private static readonly Regex PromoLegal = new(@"\b(?:trademarks?\s+of|registered\s+trademark|all\s+rights\s+reserved|licensed\s+(?:by|from))\b",RegexOptions.IgnoreCase|RegexOptions.Compiled);
    private static readonly Regex PromoCopy = new(@"\b(?:order now|available now|coming soon|from the makers|check out|exclusive|only (?:on|for)|choose from over|bonus points|your best shot|game screens from|suggested retail price|call (?:now|toll.free)|you want a taste)\b",RegexOptions.IgnoreCase|RegexOptions.Compiled);
    private static readonly Regex StrongGameplay = new(@"\b(?:secret\s+(?:cheat\s+)?codes?|cheat\s+codes?|passwords?|god\s+mode|extra\s+(?:weapons|ammo|lives)|fatalit(?:y|ies)|walkthrough|stage\s+select|level\s+select|button\s+combinations?|sound\s+test|hidden\s+(?:levels?|characters?)|strategy\s+guide)\b",RegexOptions.IgnoreCase|RegexOptions.Compiled);
    private static bool ContextualGameplay(string text,string raw)
    {
        if(AcquisitionContext(text))return false;
        var t=Regex.Escape(raw);
        if(Regex.IsMatch(text,t+@"\s+(?:is\s+(?:riddled|packed|filled)\s+with|contains)\s+secret\b",RegexOptions.IgnoreCase))return true;
        return Regex.IsMatch(text,@"\b(?:start|load|play|master|complete|finish|defeat|beat|set up|finished|mastered)\s+(?:the\s+(?:game|[\w-]+\s+version)\s+(?:of\s+)?)?"+t+@"(?![\p{L}\p{N}])",RegexOptions.IgnoreCase) && (StrongGameplay.IsMatch(text)||GameplayCue.IsMatch(text)||Regex.IsMatch(text,@"\b(?:mastered|master|finished|finish)\b",RegexOptions.IgnoreCase))
            ||Regex.IsMatch(text,@"\b(?:find your way through|navigate|walkthrough of|guide to|codes for|tips for|passwords for)\s+(?:the world of\s+)?"+t+@"(?![\p{L}\p{N}])",RegexOptions.IgnoreCase)
            ||Regex.IsMatch(text,t+@"\s+(?:map\b|(?:gives?|offers?|has|contains)\s+(?:you\s+)?(?:secret\s+)?(?:cheat\s+)?(?:codes?|passwords?|cheats?)\b)",RegexOptions.IgnoreCase)
            ||Regex.IsMatch(text,t+@"\s*[.,;]?\s*(?:to\s+set\s+it\s+up|go\s+to\s+the\s+password)\b",RegexOptions.IgnoreCase);
    }
    private static string GameplayDetail(string text)
    {
        if(Regex.IsMatch(text,@"\b(?:walkthrough|find your way|navigate|map)\b",RegexOptions.IgnoreCase))return "Walkthroughs and maps";
        if(Regex.IsMatch(text,@"\bpasswords?\b",RegexOptions.IgnoreCase))return "Passwords";
        if(StrongGameplay.IsMatch(text))return "Cheats and codes";
        return "Gameplay tips and strategies";
    }
    private static bool NonGameContext(string text,string raw,int start,int end)
    {
        string left=text[..start],right=text[end..];
        if(Regex.IsMatch(raw,@"^(?:great deal|my way|rip[ -]off|roller coaster|track and field|hang[ -]on)$",RegexOptions.IgnoreCase)&&
            (Regex.IsMatch(left,@"\b(?:a|the|coming|takes|your|of|and)\s*$",RegexOptions.IgnoreCase)||Regex.IsMatch(right,@"^\s+(?:a second|of dedication|levels?|areas?)\b",RegexOptions.IgnoreCase)||!raw.Any(char.IsUpper)))return true;
        if(Regex.IsMatch(raw,@"^(?:alien invasion|checkered flag|high[ -]speed|mini[ -]putt|miniature golf|magic sword|grand master|the castle)$",RegexOptions.IgnoreCase)&&
           (Regex.IsMatch(left,@"\b(?:an?|the|retrieve|through|of|this)\s*$",RegexOptions.IgnoreCase)||Regex.IsMatch(right,@"^\s+(?:Meio|cycle racing|course|of King)\b",RegexOptions.IgnoreCase)||!raw.Any(char.IsUpper)))return true;
        if(Regex.IsMatch(raw,@"^mickey mouse$",RegexOptions.IgnoreCase)&&(Regex.IsMatch(right,@"^\s+in the castle of illusion",RegexOptions.IgnoreCase)||Regex.IsMatch(left,@"\bstarring\s*$",RegexOptions.IgnoreCase)))return true;
        if(Regex.IsMatch(raw,@"^moon crystal$",RegexOptions.IgnoreCase)&&Regex.IsMatch(left,@"[’']s\s*$",RegexOptions.IgnoreCase))return true;
        if(Regex.IsMatch(raw,@"^the ninja$",RegexOptions.IgnoreCase)&&!Regex.IsMatch(raw,@"^The Ninja$|^THE NINJA$")&&!Regex.IsMatch(left,@"\b(?:game|play|playing|called|titled)\s*$",RegexOptions.IgnoreCase)&&!Regex.IsMatch(right,@"^\s*[™®]"))return true;
        return Regex.IsMatch(left,@"\b(?:before the|at the|after the|during the)\s*$",RegexOptions.IgnoreCase)&&Regex.IsMatch(raw,@"^(?:tip[ -]off|kick[ -]off)$",RegexOptions.IgnoreCase)
            ||Regex.IsMatch(left,@"\b(?:you(?:'|’)re|you are|was|were|is)\s*$",RegexOptions.IgnoreCase)&&Regex.IsMatch(raw,@"^(?:out of control|out of time)$",RegexOptions.IgnoreCase)
            ||Regex.IsMatch(raw,@"^(?:lock on|the shadow|the ninja|life force|moon crystal|one[ -]on[ -]one|james bond)$",RegexOptions.IgnoreCase)&&
                (Regex.IsMatch(right,@"^\s+(?:the\s+door|uppercut|kick|punch|smoke|fatality|combat|movie|film)\b",RegexOptions.IgnoreCase)||Regex.IsMatch(left,@"\b(?:pick up|nabbing|collect|get|fifth|fourth|third|second|first)\s+(?:the\s+)?(?:[\w'’“”""]+\s+){0,2}$",RegexOptions.IgnoreCase)&&Regex.IsMatch(raw,@"crystal$",RegexOptions.IgnoreCase)
                 ||Regex.IsMatch(raw,@"^moon crystal$",RegexOptions.IgnoreCase)&&Regex.IsMatch(left,@"\b(?:the|a)\s+(?:[“”""']?\w+[“”""']?\s+)?$",RegexOptions.IgnoreCase));
    }
    private static string[] EditorialPlatformContext(string text,string[] candidates)
    {
        var explicitNames=new List<string>();
        foreach(var (pattern, name) in new (string,string)[]{(@"\b(?:SNES|Super NES|Super Nintendo(?: Entertainment System)?)\b","Super Nintendo Entertainment System"),(@"\b(?:Sega Genesis|Genesis|Mega[ -]Drive)\b","Sega Genesis"),(@"\b(?:Sega CD|Mega[ -]CD)\b","Sega CD"),(@"\b(?:Sega )?Game Gear\b","Sega Game Gear"),(@"\b(?:Nintendo )?Game Boy\b(?!\s+(?:Color|Advance))","Nintendo Game Boy"),(@"\b(?:Sega )?Master System\b","Sega Master System"),(@"\b3DO\b","3DO Interactive Multiplayer"),(@"\b(?:Atari )?Jaguar\b","Atari Jaguar"),(@"(?<!Super )\bNES\b|\bNintendo Entertainment System\b","Nintendo Entertainment System"),(@"\bSega Saturn\b","Sega Saturn"),(@"\bSega 32X\b","Sega 32X")})
            if(Regex.IsMatch(text,pattern,RegexOptions.IgnoreCase))explicitNames.Add(name);
        return candidates.Where(x=>explicitNames.Contains(x,StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
    private static readonly Regex EditorialWords = new(@"[\p{L}\p{N}]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex EditorialKinds = new(@"\b(?:reviews?|previews?|walkthroughs?|strateg(?:y|ies)|tips|hints|cheats?|codes|passwords?|interviews?|news|features?|spotlight|mailbag|letters|contest|competition|editorial|tech talk|scoreboard|high scores|buyers['’]? guide|special report|retrospective)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Advertising = new(@"\b(?:advertisement|advertising supplement|paid advertisement|order now|call toll.free|send check or money order|limited time offer|while supplies last|suggested retail price)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Merchandise = new(@"\b(?:t[ -]?shirts?|sweatshirts?|caps?|hats?|athletic bags?|order form|merchandise|shipping and handling|mail order)\b",RegexOptions.IgnoreCase|RegexOptions.Compiled);
    private static readonly Regex Price = new(@"(?:\$\s*\d+(?:[.,]\d{2})?|\b\d+\s*(?:USD|GBP)\b)",RegexOptions.IgnoreCase|RegexOptions.Compiled);
    private static readonly Regex GameplayCue = new(@"\b(?:to (?:beat|defeat|avoid|prevent|unlock|select|complete|control|kill|get|reach|win)|(?:press|hold) (?:the |button |down |up )|(?:sound test|extra lives|secret passage|hidden level|highest score)|how (?:do|can) (?:i|you)|(?:at the beginning of|when in) (?:stage|level|the main))\b",RegexOptions.IgnoreCase|RegexOptions.Compiled);
    private static readonly Regex CatalogCue = new(@"(?:\[\s*[_ ]?\]|\(\s*[_ ]?\)|\|_\]|\b(?:check (?:all|the)|which (?:of|games)|games (?:you own|do you own)|order form)\b)",RegexOptions.IgnoreCase|RegexOptions.Compiled);
    private static string NumberedToken(string s)=>s.ToLowerInvariant() switch {"i"=>"1","ii"=>"2","iii"=>"3","iv"=>"4","v"=>"5","vi"=>"6","vii"=>"7","viii"=>"8","ix"=>"9","x"=>"10","xi"=>"11","xii"=>"12","xiii"=>"13","xiv"=>"14","xv"=>"15","xvi"=>"16",_=>s.ToLowerInvariant()};
    // Only repair ambiguous OCR glyphs inside an otherwise catalog-confirmed numbered title.
    private static string? OcrSequelToken(string raw)
    {
        if(!Regex.IsMatch(raw,@"^[VvXx]?[IiLl1Jj]{2,4}$")||!Regex.IsMatch(raw,@"[lL1Jj]")||!raw.Any(char.IsUpper)||raw.All(char.IsDigit))return null;
        var repaired=Regex.Replace(raw.ToUpperInvariant(),@"[L1J]","I");
        return Regex.IsMatch(repaired,@"^(?:II|III|VII|VIII|XII|XIII)$")?NumberedToken(repaired):null;
    }
    private static string MergeDimensionToken(string key)=>Regex.Replace(key,@"\b([23]) d\b","$1d");
    private static string TitleKey(IEnumerable<string> words)=>MergeDimensionToken(string.Join(" ",words.Select((word,i)=>i==0?word:NumberedToken(word))));
    private static bool GenericPhrase(string raw)=>Regex.IsMatch(raw,@"^(?:basic|advanced|general|secret|special|helpful)\s+(?:strateg(?:y|ies)|tips|hints|codes|instructions|moves|features)$",RegexOptions.IgnoreCase);
    private static readonly Regex ContentsNumber = new(@"(?:\.{2,}|\s{2,})\s*\d{1,3}\s*$",RegexOptions.Compiled);
    private static readonly Regex LegalLine = new(@"\b(?:all rights reserved|registered trademark|copyright|ISSN|published (?:by|quarterly|monthly)|editor in chief|advertising materials)\b|©",RegexOptions.IgnoreCase|RegexOptions.Compiled);
    private static string[] EditorialTokens(string s) => EditorialWords.Matches(s).Select(m=>m.Value.ToLowerInvariant()).ToArray();
    private static bool SequelToken(string s) => int.TryParse(s,out _) || Regex.IsMatch(s,@"^(?:i|ii|iii|iv|v|vi|vii|viii|ix|x|xi|xii|xiii|xiv|xv|xvi)$",RegexOptions.IgnoreCase);
    private static string Coverage(string text)
    {
        if(Regex.IsMatch(text,@"\b(?:walkthroughs?|strateg(?:y|ies)|tips|hints|cheats?|codes|passwords?|advice from the experts)\b",RegexOptions.IgnoreCase))return "Gameplay assistance";
        if(Regex.IsMatch(text,@"\breviews?\b",RegexOptions.IgnoreCase))return "Review";
        if(Regex.IsMatch(text,@"\bpreviews?\b",RegexOptions.IgnoreCase))return "Preview";
        if(Regex.IsMatch(text,@"\binterviews?\b",RegexOptions.IgnoreCase))return "Interview";
        if(Regex.IsMatch(text,@"\b(?:mailbag|letters|dear)\b",RegexOptions.IgnoreCase))return "Reader letters";
        if(Regex.IsMatch(text,@"\b(?:contest|competition)\b",RegexOptions.IgnoreCase))return "Competition";
        if(Regex.IsMatch(text,@"\b(?:tech talk|technical)\b",RegexOptions.IgnoreCase))return "Technical department";
        if(Regex.IsMatch(text,@"\b(?:editorial|letter from)\b",RegexOptions.IgnoreCase))return "Editorial";
        if(Regex.IsMatch(text,@"\bnews\b",RegexOptions.IgnoreCase))return "News";
        if(Regex.IsMatch(text,@"\b(?:features?|spotlight|special report|retrospective)\b",RegexOptions.IgnoreCase))return "Feature";
        if(Regex.IsMatch(text,@"\b(?:scoreboard|high scores)\b",RegexOptions.IgnoreCase))return "High scores";
        return "Reference";
    }
    // Numbered contents cards: a page-number/title row followed by a nearby platform row.
    // A compact title key tolerates letter-spaced typography; it does not perform fuzzy matching.
    private sealed record ContentsCard(int ContentsPage,int PrintedPage,string PrintedTitle,string[] Platforms,string? Title,string Source,bool Department);
    private static string ContentsCompact(string text)=>string.Concat(text.Where(char.IsLetterOrDigit)).ToLowerInvariant();
    private static readonly (string Label,string Platform)[] ContentsPlatformLabels=new (string,string)[]{
        ("supernintendoentertainmentsystem","Super Nintendo Entertainment System"),("supernintendo","Super Nintendo Entertainment System"),("supernes","Super Nintendo Entertainment System"),("snes","Super Nintendo Entertainment System"),
        ("nintendoentertainmentsystem","Nintendo Entertainment System"),("nes","Nintendo Entertainment System"),("segagenesis","Sega Genesis"),("genesis","Sega Genesis"),("megadrive","Sega Genesis"),
        ("segacd","Sega CD"),("megacd","Sega CD"),("segagamegear","Sega Game Gear"),("gamegear","Sega Game Gear"),("nintendogameboy","Nintendo Game Boy"),("gameboy","Nintendo Game Boy"),
        ("atarijaguar","Atari Jaguar"),("jaguar","Atari Jaguar"),("3dointeractivemultiplayer","3DO Interactive Multiplayer"),("3do","3DO Interactive Multiplayer"),("3d0","3DO Interactive Multiplayer"),
        ("segamastersystem","Sega Master System"),("mastersystem","Sega Master System"),("segasaturn","Sega Saturn"),("saturn","Sega Saturn"),("sega32x","Sega 32X"),("32x","Sega 32X"),
        ("playstation","Sony PlayStation"),("ps1","Sony PlayStation"),("nintendo64","Nintendo 64"),("n64","Nintendo 64"),("arcade","Arcade"),("multiplesystems","*"),("multiplatform","*")};
    private static string[] ContentsPlatforms(string text)
    {
        string rest=ContentsCompact(text);var names=new List<string>();
        if(rest.Length==0)return [];
        while(rest.Length>0)
        {
            var match=ContentsPlatformLabels.Where(x=>rest.StartsWith(x.Label,StringComparison.Ordinal)).OrderByDescending(x=>x.Label.Length).FirstOrDefault();
            if(string.IsNullOrEmpty(match.Label))return [];
            names.Add(match.Platform);rest=rest[match.Label.Length..];
        }
        return names.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
    private static Dictionary<string,List<EditorialTitle>> ContentsTitleLookup(Dictionary<string,List<EditorialTitle>> lookup)
    {
        var result=new Dictionary<string,List<EditorialTitle>>(StringComparer.Ordinal);
        void Add(string text,EditorialTitle title){var key=ContentsCompact(text);if(key.Length<3)return;if(!result.TryGetValue(key,out var list))result[key]=list=new();if(!list.Contains(title))list.Add(title);}
        foreach(var pair in lookup)foreach(var title in pair.Value){Add(pair.Key,title);Add(title.Title,title);}
        foreach(var title in lookup.Values.SelectMany(x=>x).Distinct())
        {
            int colon=title.Title.IndexOf(':');if(colon<1)continue;
            // In a numbered, platform-qualified contents entry an unambiguous subtitle/base is usable.
            var stem=title.Title[..colon].Trim();if(stem.Length>=4)Add(stem,title with{Alias=true});
            var suffix=title.Title[(colon+1)..].Trim();var stemWords=EditorialTokens(stem);
            var number=stemWords.LastOrDefault(SequelToken);
            // Preserve the sequel digit in abbreviated names such as C2: Judgment Clay.
            if(number is not null&&stemWords.Length>=2&&suffix.Length>=5)
            {
                string n=NumberedToken(number);string initial=stemWords[0][..1];
                Add(initial+n+suffix,title with{Alias=true});
                Add(string.Concat(stemWords.Where(w=>!SequelToken(w)).Select(w=>w[..1]))+n+suffix,title with{Alias=true});
            }
        }
        return result;
    }
    private static EditorialTitle[] ResolveContentsTitle(string text,string[] platforms,Dictionary<string,List<EditorialTitle>> lookup)
    {
        var keys=new[]{ContentsCompact(text),ContentsCompact(TitleKey(EditorialTokens(text)))}.Distinct();
        var found=keys.Where(lookup.ContainsKey).SelectMany(k=>lookup[k]).Where(t=>platforms.Any(p=>EvidenceKey("primarySystem",p)==EvidenceKey("primarySystem",t.Platform))).Distinct().ToArray();
        found=found.GroupBy(t=>EvidenceKey("primarySystem",t.Platform)).SelectMany(g=>g.Any(t=>!t.Alias)?g.Where(t=>!t.Alias):g).ToArray();
        if(found.Select(t=>TitleKey(EditorialTokens(t.Title))).Distinct(StringComparer.Ordinal).Count()!=1)return [];
        return found;
    }
    private static Line[] ContentsRows(IEnumerable<Line> source,double height)
    {
        if(height<=0)return [];
        var lines=source.Where(l=>l.Height>0&&l.Width>0&&l.Text.Trim().Length is >0 and <=180).OrderBy(l=>l.Y+l.Height/2).ThenBy(l=>l.X).ToArray();
        var bands=new List<List<Line>>();
        foreach(var l in lines)
        {
            double center=l.Y+l.Height/2;
            var band=bands.LastOrDefault(b=>Math.Abs(b.Average(x=>x.Y+x.Height/2)-center)<=height*.0105);
            if(band is null)bands.Add(new(){l});else band.Add(l);
        }
        var rows=new List<Line>();
        foreach(var band in bands)
        {
            var chunk=new List<Line>();
            void Flush(){if(chunk.Count==0)return;double x=chunk.Min(l=>l.X),y=chunk.Min(l=>l.Y);rows.Add(new Line(string.Join(" ",chunk.Select(l=>l.Text.Trim())),x,y,chunk.Max(l=>l.X+l.Width)-x,chunk.Max(l=>l.Y+l.Height)-y,null));chunk.Clear();}
            foreach(var l in band.OrderBy(l=>l.X))
            {
                if(chunk.Count>0&&(l.X-chunk.Max(z=>z.X+z.Width)>height*.075||Regex.IsMatch(l.Text.Trim(),@"^\d{1,3}$")&&chunk.Any(z=>Regex.IsMatch(z.Text.Trim(),@"^\d{1,3}\b"))))Flush();
                chunk.Add(l);
            }
            Flush();
        }
        return rows.OrderBy(l=>l.Y).ThenBy(l=>l.X).ToArray();
    }
    private static List<ContentsCard> ReadContentsCards(int page,IEnumerable<Line> lines,double height,int totalPages,Dictionary<string,List<EditorialTitle>> lookup,string source)
    {
        var rows=ContentsRows(lines,height);var cards=new List<ContentsCard>();
        foreach(var row in rows)
        {
            var m=Regex.Match(row.Text,@"^\s*[\W_]*(\d{1,3})(?:\s*[.\-–—:|)]*\s+|\s*[.\-–—:|)]+\s*|(?=\p{L}))([\p{L}\p{N}][^\r\n]{1,100})\s*$");
            if(!m.Success||!int.TryParse(m.Groups[1].Value,out int number)||number<=page||number>totalPages+8)continue;
            string raw=m.Groups[2].Value.Trim();
            // The nearest aligned platform label must be immediately below the title, not on a later card.
            var candidates=rows.Where(r=>r.Y+r.Height/2>row.Y+row.Height/2+height*.008&&r.Y-row.Y<=height*.075&&r.X<row.X+row.Width+height*.015&&r.X+r.Width>row.X+height*.015)
                .Select(r=>new{Line=r,Platforms=ContentsPlatforms(r.Text)}).Where(r=>r.Platforms.Length>0).OrderBy(r=>r.Line.Y).ToArray();
            if(candidates.Length==0)continue;
            var first=candidates[0];
            // Some covers/contents print each platform in a separate horizontal badge.
            var platforms=candidates.Where(r=>Math.Abs(r.Line.Y+r.Line.Height/2-first.Line.Y-first.Line.Height/2)<=height*.0105).SelectMany(r=>r.Platforms).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if(platforms.Contains("*")&&platforms.Length!=1)continue;
            bool department=Regex.IsMatch(ContentsCompact(raw),@"^(?:tipcollection|tipscollection|cheatcollection|codecollection|readerletters|mailbag|editorial|news|highscores|contents|index|gameindex|tipsindex|codeindex)$",RegexOptions.IgnoreCase);
            var matches=department||platforms.Contains("*")?Array.Empty<EditorialTitle>():ResolveContentsTitle(raw,platforms,lookup);
            string? title=matches.OrderBy(t=>t.Alias).ThenBy(t=>t.Title.Length).ThenBy(t=>t.Title,StringComparer.Ordinal).FirstOrDefault()?.Title;
            // Do not assign a platform for which the catalog title was not resolved.
            if(title is not null&&platforms.Any(p=>!matches.Any(t=>EvidenceKey("primarySystem",p)==EvidenceKey("primarySystem",t.Platform))))title=null;
            cards.Add(new(page,number,raw,platforms,title,source,department));
        }
        return cards.GroupBy(c=>c.PrintedPage).Where(g=>g.Select(c=>ContentsCompact(c.PrintedTitle)).Distinct().Count()==1).Select(g=>g.OrderByDescending(c=>c.Title is not null).First()).OrderBy(c=>c.PrintedPage).ToList();
    }
    private static bool StrongContents(IReadOnlyCollection<ContentsCard> cards)=>cards.Count>=3&&cards.Where(c=>c.Title is not null&&!c.Department).Select(c=>c.Title).Distinct(StringComparer.OrdinalIgnoreCase).Count()>=3&&cards.Count(c=>c.Title is not null||c.Department)*2>=cards.Count;
    public static async Task<object> EditorialAsync(string root, LibraryItem item, IEnumerable<GuidevaultLaunchBoxGame> games, CancellationToken token, bool allowPartial=false)
    {
        var timer=Stopwatch.StartNew();var fp=Fingerprint(item);var dir=DirectoryFor(root,item,fp);
        var manifest=await ReadManifest(dir,fp,token)??throw new InvalidOperationException("Index this magazine first.");
        var pages=new List<PageRecord>();
        for(int n=1;n<=manifest.TotalPages;n++){token.ThrowIfCancellationRequested();var p=await ReadPage(dir,n,token);if(p is not null)pages.Add(p);}
        if(pages.Count==0||!allowPartial&&pages.Count!=manifest.TotalPages)throw new InvalidOperationException("Complete the document index before analyzing editorial content.");
        int.TryParse(Regex.Match(item.Year??"",@"\b(?:19|20)\d{2}\b").Value,out int year);
        string coverText=pages.FirstOrDefault(p=>p.Page==1)?.Text??"";
        string? guidePlatform=GuidePlatform(coverText);
        string? reviewPlatform=PrintedReviewPlatform(coverText);
        var earlySystems=pages.Where(p=>p.Page<=8&&!SubscriptionAdvertisement(p.Text)).SelectMany(p=>EditorialPlatformContext(p.Text,new[]{"Nintendo Entertainment System","Super Nintendo Entertainment System","Sega Genesis","Sega Saturn","Sega Game Gear","Sega Master System","Nintendo Game Boy","Sega CD","3DO Interactive Multiplayer"})).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        string? issueScope=reviewPlatform??(guidePlatform is null&&earlySystems.Length==1&& !Regex.IsMatch(item.PlatformFocus??"",@"multi[ -]?platform",RegexOptions.IgnoreCase)&&Meaningful(item.PlatformFocus)&&earlySystems[0].StartsWith(item.PlatformFocus,StringComparison.OrdinalIgnoreCase)?earlySystems[0]:null);
        var lookup=new Dictionary<string,List<EditorialTitle>>(StringComparer.Ordinal);int gameCount=0;int maxWords=1;
        var generic=new HashSet<string>("a an the and or it is in on up down out go no yes one two three new all next last first game games magazine challenge review preview news feature features tips hints code codes password passwords action adventure sports racing golf tennis soccer football baseball basketball hockey boxing pool chess checkers pinball poker casino music video computer system master world time power team spot star space war battle combat control super special final force speed drive race test play player home life risk trouble champion championship olympics pro ace target score winner magic monster hero quest".Split(' '),StringComparer.OrdinalIgnoreCase);
        foreach(var g in games)
        {
            token.ThrowIfCancellationRequested();
            int.TryParse(Regex.Match(g.ReleaseYear??"",@"\b(?:19|20)\d{2}\b").Value,out int release);
            if(year>0&&release>year+2||string.IsNullOrWhiteSpace(g.Title))continue;
            gameCount++;
            // Subtitle abbreviations are allowed only if the base contains at least two words;
            // ambiguity and sequel tokens are checked below, just like catalog aliases.
            var colon=g.Title.IndexOf(':');
            var shortTitle=colon>0?g.Title.Substring(0,colon).Trim():"";
            var names=new[]{g.Title}.Concat(g.AlternateNames??new List<string>());
            if(EditorialTokens(shortTitle).Length>=2||EditorialTokens(shortTitle).Length==1&&shortTitle.Length>=5&&issueScope is not null&&EvidenceKey("primarySystem",g.Platform)==EvidenceKey("primarySystem",issueScope))names=names.Append(shortTitle);
            foreach(var name in names)
            {
                if(string.IsNullOrWhiteSpace(name))continue;var words=EditorialTokens(name);
                if(words.Length==0||words.Length>16||string.Concat(words).Length<3)continue;
                if(words.Length==1&&generic.Contains(words[0]))continue;
                var key=TitleKey(words);maxWords=Math.Max(maxWords,words.Length+1);
                if(!lookup.TryGetValue(key,out var list))lookup[key]=list=new();
                var entry=new EditorialTitle(g.Title.Trim(),g.Platform??"",!string.Equals(name,g.Title,StringComparison.OrdinalIgnoreCase));
                if(!list.Contains(entry))list.Add(entry);
            }
        }
        var resolved=new Dictionary<string,EditorialTitle[]>(StringComparer.Ordinal);
        foreach(var pair in lookup)
        {
            // Exact names take precedence over aliases; ambiguous distinct numbered titles are not merged.
            var scoped=issueScope is null?Array.Empty<EditorialTitle>():pair.Value.Where(x=>EvidenceKey("primarySystem",x.Platform)==EvidenceKey("primarySystem",issueScope)).ToArray();
            var pool=scoped.Length>0?scoped:pair.Value.ToArray();
            var direct=pool.Where(x=>!x.Alias).ToArray();var options=direct.Length>0?direct:pool;
            if(options.Select(x=>TitleKey(EditorialTokens(x.Title))).Distinct(StringComparer.Ordinal).Count()!=1)continue;
            // A short alias must not silently expand to a different sequel number.
            if(options.All(x=>x.Alias)&&options.Any(x=>!EditorialTokens(x.Title).Where(SequelToken).Select(NumberedToken).SequenceEqual(pair.Key.Split(' ').Where(SequelToken).Select(NumberedToken))))continue;
            var selectedKey=TitleKey(EditorialTokens(options[0].Title));
            resolved[pair.Key]=pair.Value.Where(x=>TitleKey(EditorialTokens(x.Title))==selectedKey).ToArray();
        }
        // First read the publisher's numbered, platform-qualified contents cards from the existing index.
        // Vision is bounded to three early-page overview attempts, cached by source fingerprint and page.
        var tocLookup=ContentsTitleLookup(lookup);
        var tocDiagnostics=new List<object>();var structuredContents=new List<ContentsCard>();
        var earlyPages=pages.Where(p=>p.Page>=2&&p.Page<=10&&!SubscriptionAdvertisement(p.Text)).ToArray();
        var indexedCards=earlyPages.ToDictionary(p=>p.Page,p=>ReadContentsCards(p.Page,p.Lines,p.Height,manifest.TotalPages,tocLookup,"Indexed contents layout"));
        foreach(var p in earlyPages)
        {
            var cards=indexedCards[p.Page];
            if(StrongContents(cards))structuredContents.AddRange(cards);
            if(cards.Count>0)tocDiagnostics.Add(new{page=p.Page,source="Index",status=StrongContents(cards)?"Accepted layout":"Insufficient resolved entries",entries=cards});
        }
        // Avoid extra image recognition for a conventional, already readable contents page.
        bool conventionalContents=earlyPages.Any(p=>p.Lines.Count(l=>PrintedPage(l.Text) is int n&&n>p.Page&&n<=manifest.TotalPages+8&&resolved.ContainsKey(TitleKey(EditorialTokens(Regex.Replace(l.Text,@"(?:\.{2,}|\s{3,}|\bpage\s+)\s*\d{1,4}\s*$","",RegexOptions.IgnoreCase)))))>=3);
        if(structuredContents.Count==0&&!conventionalContents)
        {
            var candidates=earlyPages.Where(p=>p.Page>=4||indexedCards[p.Page].Count>=2||Regex.IsMatch(p.Text,@"\b(?:table of contents|in this issue|contents)\b",RegexOptions.IgnoreCase))
                .Select(p=>new{Page=p,Score=indexedCards[p.Page].Count*15+p.Lines.Count(l=>ContentsPlatforms(l.Text).Length>0)*4+(Regex.IsMatch(p.Text,@"\b(?:table of contents|in this issue|contents)\b",RegexOptions.IgnoreCase)?12:0)+(p.Page is >=4 and <=6?8:p.Page==7?4:0)})
                .OrderByDescending(x=>x.Score).ThenBy(x=>x.Page.Page).Take(3).ToArray();
            foreach(var candidate in candidates)
            {
                token.ThrowIfCancellationRequested();var p=candidate.Page;
                var vision=await CoverVisionAsync(dir,item,token,p.Page);
                var cards=ReadContentsCards(p.Page,vision.Lines,vision.Height,manifest.TotalPages,tocLookup,"Vision contents layout");
                tocDiagnostics.Add(new{page=p.Page,source="Vision",status=vision.Status,entries=cards,regions=vision.Lines.Take(120).Select(l=>new{l.Text,l.X,l.Y,l.Width,l.Height}).ToArray()});
                if(StrongContents(cards)){structuredContents.AddRange(cards);break;}
            }
        }
        if(conventionalContents&&structuredContents.Count==0)tocDiagnostics.Add(new{source="Index",status="Conventional numbered contents detected; existing parser retained"});
        // A conflicting page number cannot be used as authoritative evidence. If no reliable
        // structured contents are found, the existing recognition remains the fallback.
        structuredContents=structuredContents.GroupBy(c=>c.PrintedPage).Where(g=>g.Select(c=>ContentsCompact(c.PrintedTitle)).Distinct().Count()==1).Select(g=>g.First()).OrderBy(c=>c.PrintedPage).ToList();
        bool contentsAuthoritative=StrongContents(structuredContents);
        if(!contentsAuthoritative)structuredContents.Clear();
        var structuredPages=structuredContents.Select(c=>c.ContentsPage).ToHashSet();
        // Derive page structure before matching. Guide runs are bounded by adjacent specification-bearing pages.
        var profileSeed=pages.Where(p=>p.Page>1&&ProfileSpec.Matches(p.Text).Count>=2&&p.Lines.Count(l=>Regex.IsMatch(l.Text,@"[\p{Lu}\d][\p{Lu}\d &'’:.!]{2,80}\s*[-–—]\s*[\p{Lu}][\p{Lu} &'’.]{2,50}"))>=2).Select(p=>p.Page).ToHashSet();
        var structures=new List<object>();
        var pageKinds=new Dictionary<int,string>();
        var articleRegions=new List<object>();
        var contentsLinks=new List<(string Title,int ContentsPage,int PrintedPage)>();
        var contentsMapping=new List<object>();
        var profilePages=new List<int>();
        var hits=new List<EditorialHit>();var headings=new List<object>();var headingKeys=new HashSet<string>();var contentsPages=new List<int>();var advertisingPages=new List<int>();
        bool Margin(PageRecord p,Line l)=>p.Height>0&&(l.Y<=p.Height*.06||l.Y>=p.Height*.91)&&l.Text.Length<=110;
        string MarginKey(string text)=>Key(Regex.Replace(text,@"\b\d{1,4}\b",""));
        var repeatedMargins=pages.SelectMany(p=>p.Lines.Where(l=>Margin(p,l)).Select(l=>new{p.Page,key=MarginKey(l.Text)})).Where(x=>x.key.Length>=5).GroupBy(x=>x.key).Where(g=>g.Select(x=>x.Page).Distinct().Count()>=3).Select(g=>g.Key).ToHashSet();
        bool ExactEntry(string text)
        {
            var words=EditorialTokens(Regex.Replace(text,@"(?:\.{2,}|\s{2,})\s*\d{1,3}\s*$",""));
            return words.Length>0&&(words.Length>=2||words[0].Length>=5&&Regex.IsMatch(text,@"[™®]|^[\p{Lu}]{5,}\s*[:.!]?\s*$"))&&resolved.ContainsKey(TitleKey(words));
        }
        foreach(var p in pages)
        {
            token.ThrowIfCancellationRequested();
            var lines=p.Lines.Where(l=>!string.IsNullOrWhiteSpace(l.Text)&&!(Margin(p,l)&&repeatedMargins.Contains(MarginKey(l.Text)))).ToArray();
            bool readerPage=Regex.Matches(p.Text,@"(?im)^\s*Dear\s+").Count>=2;
            bool masthead=Regex.IsMatch(p.Text,@"\b(?:editor[ -]+in[ -]+chief|ISSN|published quarterly|published monthly)\b",RegexOptions.IgnoreCase);
            int exactEntries=lines.Count(l=>l.Text.Length<=100&&ExactEntry(l.Text));
            int pageRefs=lines.Count(l=>ContentsNumber.IsMatch(l.Text));
            int compactHeadings=lines.Count(l=>l.Text.Length<=90&&EditorialKinds.IsMatch(l.Text)&&!Regex.IsMatch(l.Text,@"[.!?]\s*$")&&EditorialTokens(l.Text).Length<=12);
            bool explicitContents=lines.Any(l=>Regex.IsMatch(l.Text,@"^\s*(?:table of contents|contents|in this issue|inside this issue)\s*[:.!]?\s*$",RegexOptions.IgnoreCase));
            int instructionLines=lines.Count(l=>InstructionBody.IsMatch(l.Text));
            int proseLines=lines.Count(l=>EditorialTokens(l.Text).Length>=12);
            bool featureList=lines.Any(l=>Regex.IsMatch(l.Text,@"^\s*(?:features|reviews|previews|regulars)\s*[:.!]?\s*$",RegexOptions.IgnoreCase));
            bool contents=structuredPages.Contains(p.Page)||!readerPage&&!masthead&&p.Page>1&&p.Page<=10&&(explicitContents||instructionLines<2&&(pageRefs>=3&&exactEntries>=1||featureList&&exactEntries>=1&&(pageRefs>=1||proseLines<8)||exactEntries>=4&&proseLines<5));
            if(contents)contentsPages.Add(p.Page);
            bool merchandisePage=lines.Count(l=>Merchandise.IsMatch(l.Text)&&Price.IsMatch(l.Text))>=2 || (Merchandise.Matches(p.Text).Count>=3&&Price.Matches(p.Text).Count>=3);
            bool catalogPage=lines.Count(l=>CatalogCue.IsMatch(l.Text))>=5;
            bool subscriptionAd=SubscriptionAdvertisement(p.Text);
            int specCount=ProfileSpec.Matches(p.Text).Count;
            int publisherHeadings=lines.Count(l=>Regex.IsMatch(l.Text,@"[\p{Lu}\d][\p{Lu}\d &'’:.!]{2,80}\s*[-–—]\s*[\p{Lu}][\p{Lu} &'’.]{2,50}\s*[|.]?\s*$"));
            bool adjacentGuide=guidePlatform is not null&&profileSeed.Any(n=>Math.Abs(n-p.Page)<=2);
            bool profilePage=p.Page>1&&!masthead&&!contents&&!readerPage&&!catalogPage&&!subscriptionAd&&(publisherHeadings>=2&&specCount>=2||adjacentGuide&&publisherHeadings>=1&&specCount>=1);
            if(profilePage)profilePages.Add(p.Page);
            bool promoPage=!contents&&!masthead&&p.Page>1&&(p.Page<=8||p.Page>=manifest.TotalPages-1)&&PromoCopy.IsMatch(p.Text)&&(PromoLegal.IsMatch(p.Text)||Regex.IsMatch(p.Text,@"\bgame screens from\b",RegexOptions.IgnoreCase)&&p.Text.Count(c=>c is '™' or '®')>=3);
            bool promotionalContinuation=!contents&&!masthead&&p.Page>1&&p.Page<=8&&advertisingPages.Contains(p.Page-1)&&PromoCopy.IsMatch(p.Text)&&!Regex.IsMatch(p.Text,@"\b(?:editorial|table of contents|reader letters)\b",RegexOptions.IgnoreCase)&&lines.Count(l=>StrongGameplay.IsMatch(l.Text))==0;
            bool advertisement=!structuredPages.Contains(p.Page)&&(subscriptionAd||Advertising.Matches(p.Text).Select(m=>m.Value.ToLowerInvariant()).Distinct().Count()>=2 || merchandisePage||promoPage||promotionalContinuation);
            if(advertisement)advertisingPages.Add(p.Page);
            string activeKind="Reference";int lastHeading=-10;
            var articleSubjects=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool assistancePage=!readerPage&&!contents&&!advertisement&&(instructionLines>=2||lines.Count(l=>GameplayCue.IsMatch(l.Text))>=2);
            bool reviewPage=!readerPage&&!contents&&!advertisement&&!profilePage&&ReviewBody.Matches(p.Text).Select(m=>m.Value.ToLowerInvariant()).Distinct().Count()>=3;
            string pageKind=p.Page==1?"Cover":structuredPages.Contains(p.Page)?"Contents":advertisement?"Advertising":masthead?"Masthead":readerPage?"Reader letters":contents?"Contents":profilePage?"Game profiles":reviewPage?"Review":assistancePage?"Gameplay assistance":catalogPage?"Catalog":"Unclassified";
            pageKinds[p.Page]=pageKind;
            structures.Add(new{page=p.Page,kind=pageKind,exactEntries,pageRefs,specCount,instructionLines});
            for(int li=0;li<lines.Length;li++)
            {
                token.ThrowIfCancellationRequested();var l=lines[li];var text=l.Text.Trim();
                bool coverCredit=masthead&&CoverArtCredit.IsMatch(text);
                if(text.Length>2000||!coverCredit&&LegalLine.IsMatch(text))continue;
                if(coverCredit)text=Regex.Split(CoverArtCredit.Replace(text,""),@"[©®™]|\blicensed from\b|\bcopyright\b",RegexOptions.IgnoreCase)[0].Trim();
                if(text.Length==0)continue;
                int originalLength=text.Length;
                // Join a possible line wrap only in the same column and with plausible spacing.
                // Only matches beginning in the original line are accepted.
                string matchText=text;
                if(!coverCredit&&li+1<lines.Length&&!Regex.IsMatch(text,@"[.!?;:™®©]\s*$")&&p.Width>0&&p.Height>0)
                {
                    var next=lines[li+1]; double delta=next.Y-l.Y;
                    if(Math.Abs(next.X-l.X)<=p.Width*.07&&delta>0&&delta<=Math.Max(l.Height,next.Height)*3&&next.Text.Length<=250)
                        matchText=text+" "+next.Text.Trim();
                }
                double height=p.Height>0?l.Height/p.Height:0;
                var localKind=Coverage(text);bool shortLine=text.Length<=145;
                bool heading=shortLine&&localKind!="Reference"&&!masthead&&(contents&&EditorialTokens(text).Length<=14||height>=.025&&EditorialTokens(text).Length<=12||Regex.IsMatch(text,@"^[\p{Lu}\d][\p{Lu}\d &’':-]{2,70}$"));
                if(heading&&!advertisement&&!Advertising.IsMatch(text)&&(!readerPage||localKind=="Reader letters")&&!Regex.IsMatch(text,@"^Dear\s+|^letters\s+we",RegexOptions.IgnoreCase))
                {
                    activeKind=localKind;lastHeading=li;
                    if(headingKeys.Add(p.Page+":"+text.ToLowerInvariant()))headings.Add(new{page=p.Page,title=text,kind=localKind,source=contents?"Contents candidate":"Heading candidate",printedPage=PrintedPage(text)});
                }
                var matches=EditorialWords.Matches(matchText);var words=EditorialTokens(matchText);
                var normalizedWords=words.Select(NumberedToken).ToArray();
                var ocrWords=normalizedWords.ToArray();bool hasOcrRoman=false;
                for(int j=0;j<words.Length;j++){var corrected=OcrSequelToken(matches[j].Value);if(corrected is not null){ocrWords[j]=corrected;hasOcrRoman=true;}}
                bool exactEntry=ExactEntry(text);int lineWordCount=EditorialTokens(text).Length;
                string SpanKey(string[] tokens,int start,int len)=>MergeDimensionToken(len==1?words[start]:words[start]==tokens[start]?string.Join(" ",tokens,start,len):words[start]+" "+string.Join(" ",tokens,start+1,len-1));
                for(int i=0;i<words.Length;i++)
                {
                    if(matches[i].Index>=originalLength)break;
                    for(int len=Math.Min(maxWords,words.Length-i);len>=1;len--)
                    {
                        string key=SpanKey(normalizedWords,i,len);
                        if(!resolved.TryGetValue(key,out var options))
                        {
                            // Bounded OCR Roman II variant, only for multi-word catalog matches.
                            if(len<2||!hasOcrRoman||!resolved.TryGetValue(SpanKey(ocrWords,i,len),out options))continue;
                        }
                        int end=i+len;int charEnd=matches[end-1].Index+matches[end-1].Length;
                        // Never accept a shorter franchise name when the printed text has a sequel suffix.
                        if(end<words.Length&&(matches[end].Index<originalLength||charEnd>originalLength)&&(SequelToken(matches[end].Value)||OcrSequelToken(matches[end].Value) is not null))continue;
                        string raw=matchText.Substring(matches[i].Index,charEnd-matches[i].Index);
                        // Do not promote a partial artwork credit to a different game. Allow an explicit artist attribution after a complete title.
                        if(coverCredit&&(i!=0||end<words.Length&&!Regex.IsMatch(matchText.Substring(charEnd),@"^\s*[,–—-]?\s*(?:by|courtesy of|illustrated by)\b",RegexOptions.IgnoreCase))){i=end-1;break;}
                        bool marked=coverCredit||Regex.IsMatch(matchText.Substring(charEnd),@"^\s*[™®©]");
                        bool titleCase=raw.Any(char.IsUpper);bool standalone=lineWordCount<=len+2;
                        bool prominent=height>=.035&&shortLine;
                        bool fullLine=lineWordCount==len;
                        bool startsTitle=matches[i].Index<=2;
                        // A rejected longest match consumes the span; do not manufacture a shorter game inside it.
                        if(!coverCredit&&NonGameContext(matchText,raw,matches[i].Index,charEnd)){i=end-1;break;}
                        bool specificGameplay=!coverCredit&&!masthead&&!catalogPage&&p.Page>1&&!AcquisitionContext(matchText)&&ContextualGameplay(matchText,raw)&&!Regex.IsMatch(matchText.Substring(charEnd),@"^\s+(?:creators?|developers?|designers?|movie|film)\b",RegexOptions.IgnoreCase);
                        if(!coverCredit&&!marked&&!prominent&&!fullLine&&!specificGameplay&&!titleCase&&options.All(x=>x.Alias)){i=end-1;break;}
                        if(GenericPhrase(raw)&&options.All(x=>x.Alias)&&!marked||!fullLine&&!marked&&Regex.IsMatch(raw,@"^(?:pick up|fire ball)$",RegexOptions.IgnoreCase)){i=end-1;break;}
                        // Short/common terms and aliases need independent title-like evidence, not a nearby gameplay keyword.
                        if(len==1&&!(fullLine&&(marked||prominent||contents)||marked&&startsTitle||marked&&Regex.IsMatch(text,@"\b(?:in|play|playing|game)\s+"+Regex.Escape(raw)+@"\b",RegexOptions.IgnoreCase))){i=end-1;break;}
                        if(len==1&&i>0&&Regex.IsMatch(words[i-1],@"^(?:easter|zombie|[a-z]+[i1l]{2})$",RegexOptions.IgnoreCase)){i=end-1;break;}
                        if(options.All(x=>x.Alias)&&!exactEntry&&!marked&&!prominent&&!specificGameplay&&(!startsTitle||!titleCase)&&len<=3){i=end-1;break;}
                        string near=heading||p.Page==1?localKind:"Reference";
                        if(near=="Reference"&&contents&&standalone&&li+1<lines.Length&&Regex.IsMatch(lines[li+1].Text,@"^\s*(?:our|a|the)\s+(?:special\s+)?",RegexOptions.IgnoreCase))near=Coverage(lines[li+1].Text);
                        if(near=="Reference"&&contents&&li-lastHeading<=2)near=activeKind;
                        if(readerPage)near="Reader letters";
                        else if(!contents&&p.Page>1&&!masthead&&!catalogPage&&!AcquisitionContext(matchText)&&(specificGameplay||GameplayCue.IsMatch(text)&& (marked||standalone||exactEntry)||(!catalogPage&&assistancePage&&marked)||(!catalogPage&&activeKind=="Gameplay assistance"&&li-lastHeading<=8&&marked)))near="Gameplay assistance";
                        else if(!contents&&p.Page>1&&near=="Reference"&&(activeKind is "Review" or "Preview" or "Feature")&&li-lastHeading<=5&&marked)near=activeKind;
                        // Single ordinary words require title typography or explicit editorial context, not raw frequency.
                        if(len==1&&(!titleCase&&!marked||!marked&&!standalone&&!prominent&&near=="Reference")){i=end-1;break;}
                        bool ad=!coverCredit&&(subscriptionAd||promoPage||promotionalContinuation||Advertising.IsMatch(text)||(Merchandise.IsMatch(text)&&(Price.IsMatch(text)||merchandisePage||standalone))||(advertisement&&!contents&&!readerPage&&!GameplayCue.IsMatch(text)&&near=="Reference"));
                        string kind=coverCredit?"Cover artwork credit":ad?"Possible advertising":catalogPage&&near=="Reference"?"Catalog listing":near;
                        bool profileEntry=!ad&&profilePage&&charEnd<=originalLength&&(ProfileSuffix(text[charEnd..])||Regex.IsMatch(text[charEnd..],@"^\s*[-–—]\s*[\p{Lu}][\p{Lu}\d &'’.]{2,45}(?:\s+[|;]|\s*$)"))&&raw.Any(char.IsUpper)&&!raw.Any(char.IsLower)&&
                            (startsTitle||Regex.IsMatch(text[..matches[i].Index],@"^\s*(?:[\W_]*|Size\s*:\s*\d+\s*(?:Meg|MB|KB)\s*)$",RegexOptions.IgnoreCase)||guidePlatform is not null&&len>=2&&Regex.IsMatch(text[..matches[i].Index],@"^\s*[\p{L}]{1,4}\s*[=»><|~_]+\s*$"));
                        if(profileEntry&&guidePlatform is not null&&guidePlatform.StartsWith("Sega ",StringComparison.OrdinalIgnoreCase)&&!options.Any(x=>(x.Platform??"").StartsWith("Sega ",StringComparison.OrdinalIgnoreCase)))profileEntry=false;
                        if(profileEntry)kind="Game profile";
                        if(!ad&&contents&&exactEntry&&(kind is not "Review" and not "Preview" and not "Interview" and not "Gameplay assistance"))kind="Contents reference";
                        if(!ad&&kind=="Reference"&&contents&&standalone)kind="Contents reference";
                        if(!ad&&kind=="Reference"&&p.Page==1&&(marked||prominent||standalone))kind="Cover reference";
                        if(!ad&&kind=="Reference"&&prominent&&standalone)kind="Heading reference";
                        if(!ad&&!coverCredit&&!masthead&&!readerPage&&!catalogPage&&!contents&&p.Page>1)
                        {
                            bool titleAnchor=(exactEntry||standalone&&startsTitle&&prominent)&&!AcquisitionContext(matchText);
                            bool gameSentence=Regex.IsMatch(matchText,Regex.Escape(raw)+@"[”""’']?\s+(?:is|has|offers|features|delivers|takes|gives|plays)\b",RegexOptions.IgnoreCase)&&!AcquisitionContext(matchText);
                            bool compatibleAnchor=issueScope is null||options.Any(x=>EvidenceKey("primarySystem",x.Platform)==EvidenceKey("primarySystem",issueScope));
                            if((kind is "Reference" or "Heading reference")&&(titleAnchor||gameSentence)&&ArticlePreview.IsMatch(matchText))kind="Preview";
                            else if((kind is "Reference" or "Heading reference")&&(titleAnchor&&compatibleAnchor||gameSentence)&&reviewPage)kind="Review";
                            else if(kind=="Heading reference"&&assistancePage&&exactEntry&&instructionLines>=1)kind="Gameplay assistance";
                            else if(kind=="Reference"&&specificGameplay)kind="Gameplay assistance";
                        }
                        if(catalogPage&&options.All(x=>x.Alias)&&!marked&&near=="Reference"&&Regex.IsMatch(raw,@"\b(?:family|party|sports|action|adventure|arcade|board|card) games$",RegexOptions.IgnoreCase))
                        {i=end-1;break;}
                        var canonical=options.OrderBy(x=>x.Alias).ThenBy(x=>x.Title.Length).ThenBy(x=>x.Title,StringComparer.Ordinal).First().Title;
                        if(!ad&&!contents&&!readerPage&&!catalogPage&&p.Page>1&&exactEntry&&prominent&&len>=2)articleSubjects.Add(canonical);
                        if(!ad&&!contents&&!readerPage&&!catalogPage&&kind=="Reference"&&articleSubjects.Contains(canonical))kind="Feature";
                        if(!ad&&contents&&headingKeys.Add(p.Page+":entry:"+canonical)&&(exactEntry||startsTitle&&standalone))
                        {
                            int? printed=PrintedPage(text);
                            if(printed is null&&charEnd<originalLength&&startsTitle&&Regex.IsMatch(text[charEnd..],@"^\s*[_.–—-]*\s*\d{1,3}\s*[.]*$"))
                            {
                                var number=Regex.Match(text[charEnd..],@"\d{1,3}");
                                if(number.Success&&int.TryParse(number.Value,out int pn)&&pn>0)printed=pn;
                            }
                            headings.Add(new{page=p.Page,title=canonical,kind=kind,source="Contents candidate",printedPage=printed});
                            if(printed is int printedNumber)contentsLinks.Add((canonical,p.Page,printedNumber));
                        }
                        var platforms=options.Select(x=>x.Platform).Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x=>x).ToArray();
                        var explicitPlatforms=!ad&&!coverCredit&&p.Page>1&&!masthead?EditorialPlatformContext(charEnd>originalLength?matchText:text,platforms):Array.Empty<string>();
                        if(profileEntry&&explicitPlatforms.Length==0&&guidePlatform is not null)
                        {
                            var pageSystems=EditorialPlatformContext(p.Text,new[]{"Sega Genesis","Sega Master System","Sega Game Gear","Sega CD","Super Nintendo Entertainment System","Nintendo Game Boy"});
                            if(pageSystems.Length==1&&platforms.Contains(pageSystems[0],StringComparer.OrdinalIgnoreCase))explicitPlatforms=pageSystems;
                            else if(platforms.Contains(guidePlatform,StringComparer.OrdinalIgnoreCase))explicitPlatforms=new[]{guidePlatform};
                        }
                        if(explicitPlatforms.Length==0&&issueScope is not null&&!ad&&!coverCredit&&!masthead&&!readerPage&&!catalogPage&&p.Page>1&&platforms.Contains(issueScope,StringComparer.OrdinalIgnoreCase)&&(kind is "Review" or "Preview" or "Feature" or "Gameplay assistance" or "Game profile"))explicitPlatforms=new[]{issueScope};
                        if(explicitPlatforms.Length>0)platforms=explicitPlatforms;
                        hits.Add(new EditorialHit(canonical,p.Page,kind,coverCredit?l.Text:charEnd>originalLength?matchText:text,platforms,p.Page==1||coverCredit,contents,height,explicitPlatforms));i=end-1;break;
                    }
                }
            }
        }
        // A prominent exact game heading may anchor an adjacent article page, but only if that page
        // independently contains the same title and an explicit continuation marker.
        var continuationPages=pages.Where(p=>Regex.IsMatch(p.Text,@"(?im)^\s*(?:continued(?:\s+from\s+page\s+\d+)?|cont[.’']?d)\s*[.:]?\s*$")).Select(p=>p.Page).ToHashSet();
        var articleAnchors=hits.Where(h=>h.Page>1&&!h.Contents&&h.Height>=.035&&(h.Kind is "Heading reference" or "Feature" or "Review" or "Preview")).ToArray();
        for(int i=0;i<hits.Count;i++)
        {
            var h=hits[i];if(h.Kind!="Reference"||h.Contents||!continuationPages.Contains(h.Page)||advertisingPages.Contains(h.Page))continue;
            if(articleAnchors.Any(a=>a.Page==h.Page-1&&string.Equals(a.Title,h.Title,StringComparison.OrdinalIgnoreCase)))hits[i]=h with{Kind="Feature"};
        }
        foreach(var card in structuredContents.Where(c=>c.Title is not null&&!c.Department))contentsLinks.Add((card.Title!,card.ContentsPage,card.PrintedPage));
        var offsetVotes=contentsLinks.SelectMany(link=>hits.Where(h=>h.Page>link.ContentsPage&&!h.Contents&&!h.Cover&&h.Title==link.Title&&h.Kind is not ("Possible advertising" or "Reader letters" or "Catalog listing")&&Math.Abs(h.Page-link.PrintedPage)<=8).Select(h=>new{link.Title,Offset=h.Page-link.PrintedPage})).Distinct().GroupBy(x=>x.Offset).Select(g=>new{offset=g.Key,count=g.Select(x=>x.Title).Distinct().Count()}).OrderByDescending(x=>x.count).ToArray();
        int? printedOffset=offsetVotes.Length>0&&offsetVotes[0].count>=2&&(offsetVotes.Length==1||offsetVotes[0].count>offsetVotes[1].count)?offsetVotes[0].offset:null;
        foreach(var link in contentsLinks.Distinct())
        {
            int? target=printedOffset is int off?link.PrintedPage+off:null;
            var matching=target is int pageNumber?hits.Where(h=>h.Title==link.Title&&h.Page==pageNumber&&!h.Cover&&!h.Contents&&h.Kind is not ("Possible advertising" or "Reader letters" or "Catalog listing")).ToArray():Array.Empty<EditorialHit>();
            bool confirmed=matching.Length>0&&target is int targetPage&&pageKinds.TryGetValue(targetPage,out var targetKind)&&targetKind is not ("Advertising" or "Catalog" or "Masthead" or "Reader letters" or "Contents");
            contentsMapping.Add(new{title=link.Title,contentsPage=link.ContentsPage,printedPage=link.PrintedPage,digitalPage=target,confirmed});
            if(confirmed)
            foreach(var h in matching.Where(h=>h.Kind is "Reference" or "Heading reference"))
            {
                int ix=hits.IndexOf(h);if(ix>=0)hits[ix]=h with{Kind="Feature",ExplicitPlatforms=issueScope is not null&&h.Platforms.Contains(issueScope,StringComparer.OrdinalIgnoreCase)?new[]{issueScope}:h.ExplicitPlatforms};
            }
        }
        var contextualTitles=hits.Where(h=>h.Contents||h.Cover).Select(h=>h.Title).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(var g in hits.Where(h=>h.Page>1&&!h.Contents&&!h.Cover&&h.Kind!="Possible advertising"&&h.Kind!="Reader letters").GroupBy(h=>h.Title,StringComparer.OrdinalIgnoreCase).ToArray())
        {
            var rows=g.ToArray();
            foreach(var h in rows)
            {
                if(h.Kind is not ("Reference" or "Heading reference"))continue;
                if(!pageKinds.TryGetValue(h.Page,out var pk)||pk is "Catalog" or "Advertising" or "Masthead" or "Reader letters" or "Contents")continue;
                bool headingAnchor=h.Kind=="Heading reference"&&h.Height>=.035;
                bool sameTitleSentence=Regex.IsMatch(h.Evidence,@"\b(?:has the action|is riddled with secret|is packed with|eagerly awaited|offers a|features a|gives you)\b",RegexOptions.IgnoreCase)&&!AcquisitionContext(h.Evidence);
                bool nearby=rows.Any(x=>x.Page!=h.Page&&Math.Abs(x.Page-h.Page)<=2&&(x.Kind is "Review" or "Preview" or "Feature" or "Gameplay assistance" or "Heading reference"));
                string? kind=ArticlePreview.IsMatch(h.Evidence)&&sameTitleSentence?"Preview":pk=="Review"&&(headingAnchor||sameTitleSentence)?"Review":sameTitleSentence&&InstructionBody.IsMatch(h.Evidence)?"Gameplay assistance":(headingAnchor||sameTitleSentence)&&contextualTitles.Contains(h.Title)&&(nearby||sameTitleSentence)?"Feature":null;
                if(kind is null)continue;
                if(issueScope is not null&&h.Platforms.Length>0&&!h.Platforms.Contains(issueScope,StringComparer.OrdinalIgnoreCase)&&!contextualTitles.Contains(h.Title))continue;
                int ix=hits.IndexOf(h);if(ix<0)continue;
                var platform=h.ExplicitPlatforms is {Length:>0}?h.ExplicitPlatforms:issueScope is not null&&h.Platforms.Contains(issueScope,StringComparer.OrdinalIgnoreCase)?new[]{issueScope}:null;
                hits[ix]=h with{Kind=kind,ExplicitPlatforms=platform};
            }
        }
        // Reconcile an incomplete franchise heading only when the same page explicitly
        // distinguishes the original from a single numbered sequel and gives that sequel instructions.
        // Do not transfer other pages or independent coverage of the original.
        var articleIdentityCorrections=new List<object>();
        foreach(var pageGroup in hits.Where(h=>h.Page>1&&!h.Cover&&!h.Contents).GroupBy(h=>h.Page).ToArray())
        {
            if(!pageKinds.TryGetValue(pageGroup.Key,out var articlePageKind)||articlePageKind!="Gameplay assistance")continue;
            var rows=pageGroup.ToArray();
            foreach(var shortHit in rows.Where(h=>h.Kind is "Gameplay assistance" or "Heading reference"))
            {
                var stem=EditorialTokens(shortHit.Title);
                if(stem.Length<2)continue;
                string shortPattern=Regex.Escape(shortHit.Title);
                // Only a title-like heading, not a sentence describing actual gameplay.
                if(!Regex.IsMatch(shortHit.Evidence,@"^\s*"+shortPattern+@"\s*[™®©]?\s*(?:[\p{L}]{1,3})?\s*[\W_]*$",RegexOptions.IgnoreCase))continue;
                if(!rows.Any(h=>h.Title.Equals(shortHit.Title,StringComparison.OrdinalIgnoreCase)&&h.Kind=="Reference"&&Regex.IsMatch(h.Evidence,@"\boriginal\s+"+shortPattern+@"(?![\p{L}\p{N}])(?!\s+(?:[0-9]+|[IVXLCDM]+)\b)",RegexOptions.IgnoreCase)))continue;
                var sequelHits=rows.Where(h=>h.Kind=="Reference"&&!AcquisitionContext(h.Evidence)&&InstructionBody.IsMatch(h.Evidence)).Where(h=>
                {
                    var longer=EditorialTokens(h.Title);
                    return longer.Length>stem.Length&&longer.Take(stem.Length).SequenceEqual(stem)&&SequelToken(longer[stem.Length])&&
                        Regex.IsMatch(h.Evidence,@"(?<![\p{L}\p{N}])"+Regex.Escape(h.Title)+@"(?:\s+(?:fanatic|players?|fans?))?\s*[,;:—–-]\s*(?:press|hold|enter)\b",RegexOptions.IgnoreCase);
                }).ToArray();
                if(sequelHits.Select(h=>h.Title).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=1)continue;
                var sequel=sequelHits[0];
                int shortIndex=hits.IndexOf(shortHit),longIndex=hits.IndexOf(sequel);
                if(shortIndex<0||longIndex<0)continue;
                hits[shortIndex]=shortHit with{Kind="Reference",ExplicitPlatforms=null};
                hits[longIndex]=sequel with{Kind="Gameplay assistance"};
                articleIdentityCorrections.Add(new{page=pageGroup.Key,original=shortHit.Title,article=sequel.Title,heading=shortHit.Evidence,instruction=sequel.Evidence,reason="Explicit sequel instruction and original-game comparison on the same page"});
            }
        }
        // The corrected page must not retain a second article association through another hit
        // for the same damaged original heading. Preserve genuine original-game references.
        foreach(var correction in articleIdentityCorrections.Select(c=>JsonSerializer.SerializeToElement(c,Json)))
        {
            int correctedPage=correction.GetProperty("page").GetInt32();string originalTitle=correction.GetProperty("original").GetString()??"";
            for(int i=0;i<hits.Count;i++)
            {
                var h=hits[i];if(h.Page==correctedPage&&h.Title.Equals(originalTitle,StringComparison.OrdinalIgnoreCase)&&!h.Cover&&!h.Contents&&(h.Kind is "Gameplay assistance" or "Heading reference" or "Feature"))hits[i]=h with{Kind="Reference",ExplicitPlatforms=null};
            }
        }
        // Report evidence-supported article ranges; merge only consecutive pages with qualifying evidence.
        foreach(var g in hits.Where(h=>h.Page>1&&!h.Contents&&!h.Cover&&(h.Kind is "Review" or "Preview" or "Feature" or "Gameplay assistance" or "Game profile")).GroupBy(h=>h.Title,StringComparer.OrdinalIgnoreCase))
        {
            var ordered=g.Select(h=>h.Page).Distinct().OrderBy(n=>n).ToArray();
            int start=ordered[0],end=start;
            foreach(int n in ordered.Skip(1)){if(n==end+1){end=n;continue;}articleRegions.Add(new{title=g.Key,startPage=start,endPage=end});start=end=n;}
            articleRegions.Add(new{title=g.Key,startPage=start,endPage=end});
        }
        bool Featured(EditorialHit h)=>h.Kind is "Review" or "Preview" or "Gameplay assistance" or "Feature" or "Interview" or "Game profile" or "Vision cover reference" or "Cover reference" or "Heading reference" or "Contents reference";
        // A wrapped title may be matched again on its second line. Suppress only the shorter
        // occurrence when a longer title on the same page contains that exact evidence span.
        var excluded=new List<object>();
        string WordSequence(string text)=>" "+string.Join(" ",EditorialTokens(text))+" ";
        foreach(var pageHits in hits.GroupBy(h=>h.Page).ToArray())
        {
            var rows=pageHits.ToArray();
            foreach(var shortHit in rows)
            {
                var shortTitle=WordSequence(shortHit.Title);var shortEvidence=WordSequence(shortHit.Evidence);
                if(rows.Any(longHit=>longHit.Title.Length>shortHit.Title.Length&&WordSequence(longHit.Title).Contains(shortTitle,StringComparison.Ordinal)&&WordSequence(longHit.Evidence).Contains(shortEvidence,StringComparison.Ordinal)))
                {
                    hits.Remove(shortHit);excluded.Add(new{title=shortHit.Title,reason="Overlapping occurrence of a longer game title",pages=new[]{shortHit.Page}});
                }
            }
        }
        // Infer platform from editorial evidence before excluding incompatible incidental matches.
        // Keep explicit coverage of other platforms rather than forcing a single-platform magazine.
        var preliminary=hits.Where(h=>Featured(h)&&h.Platforms.Length==1&&!h.Cover&&!h.Contents)
            .GroupBy(h=>h.Platforms[0],StringComparer.OrdinalIgnoreCase)
            .Select(g=>new{platform=g.Key,count=g.Select(h=>h.Title).Distinct(StringComparer.OrdinalIgnoreCase).Count()})
            .OrderByDescending(g=>g.count).ToArray();
        string? dominant=preliminary.Length>0&&preliminary[0].count>=3&&(preliminary.Length==1||preliminary[0].count>=preliminary[1].count*3+1)?preliminary[0].platform:null;
        if(dominant is not null&&guidePlatform is null&&!Regex.IsMatch(item.PlatformFocus??"",@"multi[ -]?platform",RegexOptions.IgnoreCase))
        {
            var offPlatform=hits.GroupBy(h=>h.Title,StringComparer.OrdinalIgnoreCase).Where(g=>g.All(h=>h.Platforms.Length>0&&!h.Platforms.Contains(dominant,StringComparer.OrdinalIgnoreCase))&&g.All(h=>!Featured(h))).Select(g=>g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach(var g in hits.Where(h=>offPlatform.Contains(h.Title)).GroupBy(h=>h.Title,StringComparer.OrdinalIgnoreCase))excluded.Add(new{title=g.Key,reason="Incidental match outside inferred platform",pages=g.Select(h=>h.Page).Distinct().OrderBy(n=>n).ToArray()});
            hits.RemoveAll(h=>offPlatform.Contains(h.Title));
        }
        // A shorter title in a feature section can be a shorthand for a longer title named in the contents.
        // Do not merge numbered sequels or titles with independent review/preview/contents evidence.
        if(issueScope is not null)
        {
            var unsupported=hits.GroupBy(h=>h.Title,StringComparer.OrdinalIgnoreCase).Where(g=>g.All(h=>h.Platforms.Length>0&&!h.Platforms.Contains(issueScope,StringComparer.OrdinalIgnoreCase))&&g.All(h=>h.Kind is "Heading reference" or "Contents reference" or "Reference" or "Reader letters")&&g.Any(h=>h.Kind=="Heading reference")&&!g.Any(h=>h.Kind=="Reference"&&h.Evidence.Contains("™"))).Select(g=>g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach(var h in hits.Where(h=>unsupported.Contains(h.Title)&&(h.Kind is "Heading reference" or "Contents reference")).ToArray())
            {hits.Remove(h);excluded.Add(new{title=h.Title,reason="Unsupported off-platform heading (not confirmed article)",pages=new[]{h.Page}});}
        }
        var explicitTitles=hits.Where(h=>h.Contents&&Featured(h)).GroupBy(h=>h.Title,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToArray();
        foreach(var shorter in hits.GroupBy(h=>h.Title,StringComparer.OrdinalIgnoreCase).ToArray())
        {
            var st=EditorialTokens(shorter.Key);if(st.Length<2||shorter.Any(h=>h.Contents||(h.Kind is "Review" or "Preview" or "Interview")))continue;
            var longer=explicitTitles.Where(h=>{var lt=EditorialTokens(h.Title);return lt.Length>st.Length&&lt.Take(st.Length).SequenceEqual(st)&&!lt.Skip(st.Length).Any(SequelToken)&&h.Platforms.Intersect(shorter.SelectMany(x=>x.Platforms),StringComparer.OrdinalIgnoreCase).Any();}).ToArray();
            if(longer.Length!=1)continue;
            var target=longer[0];
            foreach(var hit in shorter.Where(h=>h.Kind=="Heading reference"||h.Kind=="Reference"&&shorter.Any(x=>x.Page==h.Page&&x.Kind=="Heading reference")).ToArray())
            {
                hits.Remove(hit);hits.Add(hit with{Title=target.Title,Platforms=target.Platforms,ExplicitPlatforms=null,Kind=hit.Kind=="Heading reference"?"Feature":hit.Kind});
            }
        }
        bool InteriorCoverage(EditorialHit h)=>h.Page>1&&!h.Contents&&(h.Kind is "Review" or "Preview" or "Feature" or "Gameplay assistance" or "Interview" or "Game profile");
        string[] SupportedPlatforms(IEnumerable<EditorialHit> group)
        {
            if(contentsAuthoritative)
            {
                var key=TitleKey(EditorialTokens(group.First().Title));
                return structuredContents.Where(c=>c.Title is not null&&TitleKey(EditorialTokens(c.Title))==key).SelectMany(c=>c.Platforms).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x=>x).ToArray();
            }
            var coverage=group.Where(InteriorCoverage).ToArray();
            return coverage.SelectMany(h=>h.ExplicitPlatforms is {Length:>0}?h.ExplicitPlatforms:h.Platforms.Length==1?h.Platforms:Array.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x=>x).ToArray();
        }
        if(guidePlatform is not null&&guidePlatform.StartsWith("Sega ",StringComparison.OrdinalIgnoreCase))
        {
            var offBrand=hits.GroupBy(h=>h.Title,StringComparer.OrdinalIgnoreCase).Where(g=>g.All(h=>!Featured(h)&&h.Platforms.Length>0&&h.Platforms.All(p=>!p.StartsWith("Sega ",StringComparison.OrdinalIgnoreCase)))).Select(g=>g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach(var g in hits.Where(h=>offBrand.Contains(h.Title)).GroupBy(h=>h.Title,StringComparer.OrdinalIgnoreCase))excluded.Add(new{title=g.Key,reason="Unsupported incidental match outside printed Sega guide focus",pages=g.Select(h=>h.Page).Distinct().OrderBy(n=>n).ToArray()});
            hits.RemoveAll(h=>offBrand.Contains(h.Title));
        }
        string visionStatus="Not needed";
        var visionDiagnostics=new List<object>();
        if(hits.Where(h=>h.Kind=="Cover artwork credit").Select(h=>h.Title).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=1)
        {
            var vision=await CoverVisionAsync(dir,item,token);visionStatus=vision.Status;
            if(vision.Lines.Length>0&&vision.Height>0)
            {
                foreach(var vl in vision.Lines)
                {
                    var titleWords=EditorialTokens(vl.Text);double relativeHeight=vl.Height/vision.Height;
                    var matchesFound=new List<EditorialTitle>();
                    if(relativeHeight>=.018)
                        for(int j=0;j<titleWords.Length;j++)for(int n=Math.Min(maxWords,titleWords.Length-j);n>=1;n--)
                        {
                            if(n==1&&titleWords[j].Length<5)continue;
                            if(!resolved.TryGetValue(TitleKey(titleWords.Skip(j).Take(n)),out var choices))continue;
                            if(j+n<titleWords.Length&&SequelToken(titleWords[j+n]))continue;
                            matchesFound.Add(choices[0]);j+=n-1;break;
                        }
                    var accepted=new List<string>();
                    foreach(var choice in matchesFound.DistinctBy(x=>x.Title,StringComparer.OrdinalIgnoreCase))
                    {
                        if(!hits.Any(h=>TitleKey(EditorialTokens(h.Title))==TitleKey(EditorialTokens(choice.Title))&&h.Page>1&&h.Kind!="Possible advertising"))continue;
                        var catalogPlatforms=hits.Where(h=>h.Title==choice.Title).SelectMany(h=>h.Platforms).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                        hits.Add(new EditorialHit(choice.Title,1,"Vision cover reference",vl.Text,catalogPlatforms,true,false,relativeHeight));accepted.Add(choice.Title);
                    }
                    if(visionDiagnostics.Count<150)visionDiagnostics.Add(new{text=vl.Text,height=Math.Round(relativeHeight,4),matches=matchesFound.Select(x=>x.Title).Distinct().ToArray(),accepted,reason=accepted.Count>0?"Candidate headline only":relativeHeight<.018?"Below prominence threshold":matchesFound.Count==0?"No resolved title span":"No corroborating interior title"});
                }
            }
        }
        // The validated contents cards establish major features. Other evidence remains in the
        // report as secondary coverage and must not elevate unrelated tips or reader references.
        foreach(var card in structuredContents)
        {
            if(card.Department)
            {
                headings.Add(new{page=card.ContentsPage,title=card.PrintedTitle,kind=Coverage(card.PrintedTitle),source="Contents candidate",printedPage=(int?)card.PrintedPage});
                continue;
            }
            if(card.Title is null)continue;
            hits.Add(new EditorialHit(card.Title,card.ContentsPage,"Contents main feature",card.PrintedPage.ToString(CultureInfo.InvariantCulture)+" "+card.PrintedTitle+" — "+string.Join(" / ",card.Platforms),card.Platforms,false,true,0,card.Platforms));
        }
        bool MainFeatured(EditorialHit h)=>contentsAuthoritative?h.Kind=="Contents main feature":Featured(h);
        var mainArticleRanges=structuredContents.Select((c,i)=>new{title=c.Title,printedTitle=c.PrintedTitle,contentsPage=c.ContentsPage,printedStart=c.PrintedPage,printedEnd=i+1<structuredContents.Count?(int?)(structuredContents[i+1].PrintedPage-1):null,platforms=c.Platforms,department=c.Department,source=c.Source}).ToArray();
        var groups=hits.GroupBy(h=>TitleKey(EditorialTokens(h.Title))).Select(g=>new{
            title=g.First().Title, featured=g.Any(MainFeatured), editorialPages=g.Where(h=>MainFeatured(h)||contentsAuthoritative&&g.Any(MainFeatured)&&InteriorCoverage(h)).Select(h=>h.Page).Distinct().OrderBy(x=>x).ToArray(),
            pages=g.Select(h=>h.Page).Distinct().OrderBy(x=>x).ToArray(),cover=g.Any(h=>h.Cover&&h.Kind!="Possible advertising"),contents=g.Any(h=>h.Contents&&MainFeatured(h)),
            platforms=SupportedPlatforms(g).Length>0?SupportedPlatforms(g):g.SelectMany(h=>h.Platforms).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x=>x).ToArray(),
            supportedPlatforms=SupportedPlatforms(g),
            kinds=g.Select(h=>h.Kind).Distinct().OrderBy(x=>x).ToArray(),evidence=g.OrderByDescending(MainFeatured).ThenByDescending(Featured).ThenBy(h=>h.Page).GroupBy(h=>new{h.Page,h.Kind}).Select(x=>x.First()).Take(12).Select(h=>new{h.Page,h.Kind,h.Evidence,detail=h.Kind=="Gameplay assistance"?GameplayDetail(h.Evidence):null}).ToArray()
        }).OrderByDescending(g=>g.featured).ThenByDescending(g=>g.cover).ThenByDescending(g=>g.contents).ThenByDescending(g=>g.editorialPages.Length).ThenBy(g=>g.title).ToArray();
        // No automatic cover winner: unreadable artwork/typography cannot be recovered from text alone.
        var coverCandidates=groups.Where(g=>g.cover||g.contents&&g.kinds.Contains("Review"))
            .OrderByDescending(g=>(g.kinds.Contains("Cover artwork credit")?100:0)+(g.contents&&g.kinds.Contains("Review")?5:0)+(g.cover?3:0)+(g.editorialPages.Any(n=>n>2)?2:0))
            .ThenByDescending(g=>g.editorialPages.Length).Take(20).Select(g=>new{g.title,source=g.kinds.Contains("Cover artwork credit")?"Printed cover artwork credit":g.kinds.Contains("Vision cover reference")?"Vision cover headline (not artwork verification)":g.cover?"Cover text": "Contents review only; cover unverified",g.editorialPages}).ToArray();
        var platformSignals=groups.SelectMany(g=>g.supportedPlatforms.Select(platform=>new{platform,g.title})).GroupBy(g=>g.platform,StringComparer.OrdinalIgnoreCase).Select(g=>new{platform=g.Key,featuredTitles=g.Count(),titles=g.Select(x=>x.title).ToArray()}).OrderByDescending(g=>g.featuredTitles).ToArray();
        string? primary=guidePlatform??reviewPlatform??(!Regex.IsMatch(item.PlatformFocus??"",@"multi[ -]?platform",RegexOptions.IgnoreCase)&&platformSignals.Length>0&&platformSignals[0].featuredTitles>=2&&(platformSignals.Length==1||platformSignals[0].featuredTitles>=platformSignals[1].featuredTitles*2+1)?platformSignals[0].platform:null);
        if(Fingerprint(item)!=fp)throw new InvalidOperationException("Source changed during analysis. Re-index the document.");
        return new{revision="v8.0-phase3.7",contentsAuthoritative,mainArticleRanges,tocDiagnostics,articleIdentityCorrections,excluded,indexedPages=pages.Count,totalPages=manifest.TotalPages,catalogGames=gameCount,guidePlatform,issueScope,profilePages,structures,articleRegions,contentsMapping,printedOffset,visionStatus,visionDiagnostics,contentsPages,advertisingPages,coverCandidates,primarySystemCandidate=primary,platformSignals,featuredCount=groups.Count(g=>g.featured),mentionedCount=groups.Count(g=>!g.featured&&g.kinds.Any(k=>k!="Possible advertising")),advertisingOnlyCount=groups.Count(g=>g.kinds.All(k=>k=="Possible advertising")),games=groups,headings,seconds=Math.Round(timer.Elapsed.TotalSeconds,2)};
    }
    private sealed record CoverVisionResult(string Status,double Height,Line[] Lines);
    private static async Task<CoverVisionResult> CoverVisionAsync(string dir,LibraryItem item,CancellationToken token,int page=1)
    {
        string cache=Path.Combine(dir,page==1?"cover-vision-phase45.json":$"contents-vision-phase47-{page:D6}.json");
        var cached=await Read<CoverVisionResult>(cache,token);if(cached is not null)return cached with{Status="Cached: "+cached.Status};
        const string python="/opt/guidevault-vision/bin/python",script="/opt/guidevault-vision/cover_ocr.py";
        if(!File.Exists(python)||!File.Exists(script))return new("Existing vision runtime unavailable",0,Array.Empty<Line>());
        if(Environment.GetEnvironmentVariable("GUIDEVAULT_INDEX_COVER_VISION")=="0")return new("Disabled by configuration",0,Array.Empty<Line>());
        string temp=Path.Combine(Path.GetTempPath(),"gv-index-cover-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
        CoverVisionResult result;
        try
        {
            string input;
            if(IsPdf(item)){var prefix=Path.Combine(temp,"cover");string number=page.ToString(CultureInfo.InvariantCulture);await Run("pdftoppm",["-f",number,"-l",number,"-scale-to","1800","-singlefile","-png",item.Path,prefix],token,30);input=prefix+".png";}
            else{var keys=JsonSerializer.Deserialize<string[]>(await RunArchiveWorker("list",item.Path,null,null,token),Json)??[];if(page<1||page>keys.Length)throw new InvalidOperationException("Requested vision page unavailable");input=Path.Combine(temp,"cover.img");await RunArchiveWorker("extract",item.Path,keys[page-1],input,token);}
            if(!File.Exists(input)||new FileInfo(input).Length>40_000_000)throw new InvalidOperationException("Cover unavailable or too large");
            // Existing single-image invocation performs one overview pass, not deep tiles or semantic inference.
            var raw=await Run(python,[script,input],token,90);
            using var doc=JsonDocument.Parse(raw);var v=doc.RootElement;
            var regions=v.GetProperty("regions").EnumerateArray().Take(800).Select(x=>new Line(x.GetProperty("text").GetString()??"",x.GetProperty("x").GetDouble(),x.GetProperty("y").GetDouble(),x.GetProperty("width").GetDouble(),x.GetProperty("height").GetDouble(),null)).ToArray();
            result=new("Overview recognition (candidate evidence only)",v.GetProperty("height").GetDouble(),regions);
        }
        catch(Exception ex) when(ex is not OperationCanceledException){result=new("Page vision fallback failed: "+ex.Message,0,Array.Empty<Line>());}
        finally{try{Directory.Delete(temp,true);}catch{}}
        await Write(cache,result,token);return result;
    }
    private static int? PrintedPage(string text)
    {
        var m=Regex.Match(text,@"(?:\.{2,}|\s{3,}|\bpage\s+)\s*(\d{1,4})\s*$",RegexOptions.IgnoreCase);
        return m.Success&&int.TryParse(m.Groups[1].Value,out int n)&&n>0?n:null;
    }

    // Phase 4: derive a conservative save plan from the existing read-only reports.
    public sealed record CatalogChange(string Field, JsonElement Before, JsonElement After, string Reason);
    public sealed record CatalogPlan(string Revision, string Fingerprint, ItemMetadataUpdate Update, List<CatalogChange> Changes, List<string> Held, JsonElement Publication, JsonElement Editorial, double Seconds);
    public static string CatalogFingerprint(LibraryItem item) => Fingerprint(item);
    public static async Task<CatalogPlan> CatalogAsync(string root, LibraryItem item, IReadOnlyList<LibraryItem> library, IEnumerable<GuidevaultLaunchBoxGame> games, bool replace, CancellationToken token)
    {
        var clock=Stopwatch.StartNew(); var fp=Fingerprint(item);
        var publication=JsonSerializer.SerializeToElement(await PublicationAsync(root,item,library,token),Json);
        var editorial=JsonSerializer.SerializeToElement(await EditorialAsync(root,item,games,token),Json);
        var changes=new List<CatalogChange>(); var held=new List<string>(); var values=new Dictionary<string,object>();
        var protectedItem=MetadataStatusHelper.Normalize(item.MetadataStatus) is MetadataStatusHelper.Reviewed or MetadataStatusHelper.Locked or MetadataStatusHelper.ManualOnly;
        bool manual=(item.MetadataSource??"").Contains("Manual edit",StringComparison.OrdinalIgnoreCase);
        var current=JsonSerializer.SerializeToElement(item,Json);
        string S(JsonElement e,string name)=>e.TryGetProperty(name,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()??"":"";
        string[] A(JsonElement e,string name)=>e.TryGetProperty(name,out var v)&&v.ValueKind==JsonValueKind.Array?v.EnumerateArray().Where(x=>x.ValueKind==JsonValueKind.String).Select(x=>x.GetString()??"").Where(x=>x.Length>0).ToArray():[];
        bool Nonempty(JsonElement e)=>e.ValueKind switch {JsonValueKind.String=>Meaningful(e.GetString()),JsonValueKind.Array=>e.GetArrayLength()>0,JsonValueKind.Number=>e.TryGetInt32(out var n)&&n>0,_=>false};
        bool Equal(string field,JsonElement a,JsonElement b)=>a.ValueKind==JsonValueKind.String&&b.ValueKind==JsonValueKind.String?(field=="writer"?string.Equals(a.GetString(),b.GetString(),StringComparison.Ordinal):EvidenceKey(field,a.GetString())==EvidenceKey(field,b.GetString())):a.ValueKind==JsonValueKind.Array&&b.ValueKind==JsonValueKind.Array?a.EnumerateArray().Select(x=>x.GetString()).SequenceEqual(b.EnumerateArray().Select(x=>x.GetString()),StringComparer.OrdinalIgnoreCase):a.GetRawText()==b.GetRawText();
        // Preserve existing list values and order. Only append genuinely new candidates.
        // Keep publication platform aliases equivalent, without conflating different game titles.
        string ListKey(string field,string value)=>field=="featuredPlatforms"?EvidenceKey("primarySystem",value):value.Trim();
        void Enrich(string field,IEnumerable<string> candidates,string reason)
        {
            var existing=A(current,field);
            var seen=new HashSet<string>(existing.Select(x=>ListKey(field,x)),StringComparer.OrdinalIgnoreCase);
            // A game already featured should not be added as a mere mention. Existing overlap is retained.
            if(field=="mentionedGames")foreach(var title in A(current,"featuredGames"))seen.Add(ListKey(field,title));
            var additions=candidates.Where(Meaningful).Where(x=>seen.Add(ListKey(field,x))).ToArray();
            if(additions.Length==0)return;
            Offer(field,existing.Concat(additions).ToArray(),"Add "+additions.Length.ToString(CultureInfo.InvariantCulture)+" new candidate(s); preserve existing entries — "+reason);
        }
        void Offer(string field,object value,string reason,bool canReplace=true)
        {
            var after=JsonSerializer.SerializeToElement(value,Json); if(!Nonempty(after))return;
            var before=current.TryGetProperty(field,out var old)?old:JsonSerializer.SerializeToElement<object?>(null,Json);
            if(Equal(field,before,after))return;
            if(protectedItem){held.Add(field+": protected metadata (record status: "+MetadataStatusHelper.Normalize(item.MetadataStatus)+") — "+reason);return;}
            if(MetadataLockHelper.IsLocked(item.MetadataLocks,field)){held.Add(field+": protected metadata (field locked) — "+reason);return;}
            bool dateEnrichment=field=="coverDate"&&canReplace&&before.ValueKind==JsonValueKind.String&&Regex.IsMatch(before.GetString()??"",@"^(?:19|20)\d{2}$")&&after.ValueKind==JsonValueKind.String&&Regex.IsMatch(after.GetString()??"",@"^[\p{L}]+\s+"+Regex.Escape(before.GetString()??"")+"$");
            bool emptyPlaceholder=before.ValueKind==JsonValueKind.String&&Regex.IsMatch(before.GetString()??"",@"^\s*(?:unknown|unspecified|not specified|n/?a)\s*$",RegexOptions.IgnoreCase);
            if((Nonempty(before)||before.ValueKind==JsonValueKind.String&&!string.IsNullOrWhiteSpace(before.GetString()))&&(manual||!canReplace||!replace&&!dateEnrichment&&!emptyPlaceholder)){held.Add(field+": existing value retained ("+(manual?"manual edit source":!replace?"replacement option disabled":"insufficient replacement evidence")+") — "+reason);return;}
            values[field]=value;changes.Add(new(field,before,after,reason));
        }
        var conflicting=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var permitted=new HashSet<string>(StringComparer.OrdinalIgnoreCase){"magazineTitle","issueNumber","volume","coverDate","year","publicationDate","publisher","writer","region","languageTag","primarySystem","platformFocus","magazineCategory","barcodeUpcIssn","pageCount"};
        foreach(var field in publication.GetProperty("fields").EnumerateArray())
        {
            var key=S(field,"field");if(!permitted.Contains(key))continue;
            var candidates=field.GetProperty("candidates");if(candidates.GetArrayLength()!=1){if(candidates.GetArrayLength()>1){conflicting.Add(key);held.Add(key+": conflicting publication evidence");}continue;}
            var c=candidates[0];var val=S(c,"value");var support=S(c,"support");bool direct=support=="Direct or reviewed";
            // Indirect evidence never replaces a populated field; language and repeated publication contacts may fill blanks.
            var ev=c.GetProperty("evidence").EnumerateArray().ToArray();
            bool repeatedContact=ev.Where(e=>S(e,"source").Contains("correspondence",StringComparison.OrdinalIgnoreCase)&&e.GetProperty("page").ValueKind==JsonValueKind.Number).Select(e=>e.GetProperty("page").GetInt32()).Distinct().Count()>=2;
            bool indirectAllowed=key=="languageTag"||(key is "publisher" or "region")&&repeatedContact;
            if(!direct&&!indirectAllowed){held.Add(key+": indirect evidence only — "+val);continue;}
            // Compound publishing credits can be truncated by OCR; preserve the evidence, not the incomplete name.
            if(key=="publisher"&&Regex.IsMatch(val,@"\b(?:in conjunction with|in association with|in cooperation with|in partnership with|together with|on behalf of)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))
            {held.Add(key+": compound publishing credit retained for review — "+val);continue;}
            if(key=="pageCount"){if(int.TryParse(val,out var count))Offer(key,count,"Indexed digital page count",false);}
            else Offer(key,val,support+" — "+val,direct||key=="region"&&repeatedContact);
        }
        var gameRows=editorial.GetProperty("games").EnumerateArray().ToArray();
        var primary=S(editorial,"primarySystemCandidate");
        if(publication.TryGetProperty("editionMismatch",out var mismatch)&&mismatch.ValueKind==JsonValueKind.True)held.Add("Publication identity: cover/printed market differs from stored region; verify edition and publisher before replacing identity fields.");
        var relevant=gameRows; // Phase 3 already filters unsupported incidental matches; retain genuine cross-platform coverage.
        bool contentsAuthoritative=editorial.GetProperty("contentsAuthoritative").GetBoolean();
        bool EditorialCoverage(JsonElement g)=>contentsAuthoritative?A(g,"kinds").Contains("Contents main feature"):g.GetProperty("featured").GetBoolean()&&(A(g,"kinds").Any(k=>k is "Review" or "Preview" or "Feature" or "Gameplay assistance" or "Interview" or "Game profile")||g.GetProperty("contents").GetBoolean()&&A(g,"kinds").Contains("Contents reference")&&g.GetProperty("evidence").EnumerateArray().Any(e=>!editorial.GetProperty("contentsPages").EnumerateArray().Any(p=>p.GetInt32()==e.GetProperty("page").GetInt32())&&S(e,"kind")=="Reference"));
        var mainRanges=editorial.GetProperty("mainArticleRanges").EnumerateArray().ToArray();
        var featured=(contentsAuthoritative?mainRanges.Select(g=>S(g,"title")):relevant.Where(EditorialCoverage).Select(g=>S(g,"title"))).Where(Meaningful).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if(contentsAuthoritative)
        {
            foreach(var entry in mainRanges.Where(g=>S(g,"title").Length==0&&!g.GetProperty("department").GetBoolean()))held.Add("Featured games: unresolved contents entry — "+S(entry,"printedTitle")+" (printed page "+entry.GetProperty("printedStart").GetInt32()+")");
        }
        else if(editorial.GetProperty("contentsPages").GetArrayLength()==0)held.Add("Editorial structure: no validated contents page; using previous recognition fallback.");
        var mentioned=relevant.Where(g=>!featured.Contains(S(g,"title"),StringComparer.OrdinalIgnoreCase)&&A(g,"kinds").Any(k=>k is not "Possible advertising" and not "Catalog listing" and not "Cover artwork credit")).Select(g=>S(g,"title")).Where(Meaningful).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if(primary.Length>0)
        {
            if(!values.ContainsKey("primarySystem")&&!conflicting.Contains("primarySystem"))Offer("primarySystem",primary,"Platform inferred from unambiguous editorial game titles",false);
            if(S(editorial,"guidePlatform").Length==0&&!values.ContainsKey("platformFocus")&&!conflicting.Contains("platformFocus"))Offer("platformFocus",primary,"Platform inferred from editorial game titles",editorial.GetProperty("platformSignals").EnumerateArray().Any(p=>S(p,"platform")==primary&&p.GetProperty("featuredTitles").GetInt32()>=3));
            var supported=new[]{primary}.Concat(relevant.Where(EditorialCoverage).SelectMany(g=>A(g,"supportedPlatforms"))).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            Enrich("featuredPlatforms",supported,"Supported editorial platforms");
        }
        else
        {
            var platforms=relevant.Where(EditorialCoverage).SelectMany(g=>A(g,"supportedPlatforms")).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            Enrich("featuredPlatforms",platforms,"Explicit article platforms and unambiguous catalog platforms from editorial coverage");
        }
        Enrich("featuredGames",featured,contentsAuthoritative?"Publisher contents: numbered main articles with explicit platform labels":"Indexed editorial coverage; excludes cover-only, catalog-only and advertising-only matches");
        Enrich("mentionedGames",mentioned,"Indexed non-advertising references; catalog-only matches retained in report");
        var covers=editorial.GetProperty("coverCandidates").EnumerateArray().ToArray();
        // A text mention alone does not establish the main cover story. Do not promote an unverified contents-only candidate.
        if(covers.Length>0)
        {
            var best=covers[0];var title=S(best,"title");
            var verifiedText=covers.Length==1&&S(best,"source")=="Cover text"&&featured.Contains(title,StringComparer.OrdinalIgnoreCase)&&relevant.Any(g=>S(g,"title")==title&&g.GetProperty("contents").GetBoolean()&&A(g,"kinds").Any(k=>k is "Review" or "Preview" or "Feature"));
            var artwork=covers.Where(c=>S(c,"source")=="Printed cover artwork credit").ToArray();
            if(artwork.Length==1)Offer("coverSubject",S(artwork[0],"title"),"Explicit printed cover artwork credit (not visual verification)");
            else if(verifiedText)Offer("coverSubject",title,"Single cover-text candidate with explicit editorial contents support",false);
            else held.Add("coverSubject: primary cover not verified — "+string.Join(", ",covers.Select(g=>S(g,"title"))));
        }
        if(covers.Length==0)held.Add("coverSubject: no supported cover candidate — "+S(editorial,"visionStatus"));
        var kinds=relevant.Where(EditorialCoverage).SelectMany(g=>A(g,"kinds")).Where(k=>k is "Review" or "Preview" or "Feature" or "Gameplay assistance" or "Interview" or "Game profile").Concat(editorial.GetProperty("headings").EnumerateArray().Where(h=>S(h,"source")=="Contents candidate").Select(h=>S(h,"kind"))).Where(Meaningful).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x=>x).ToArray();
        var departments=editorial.GetProperty("headings").EnumerateArray().Where(h=>S(h,"source")=="Contents candidate"&&(S(h,"kind") is "Reader letters" or "Technical department" or "Competition" or "High scores" or "Editorial")).Select(h=>Regex.Replace(S(h,"title"),@"^NEW!\s*|[_—–]+\s*$","",RegexOptions.IgnoreCase).Trim()).Where(t=>t.Length is >=5 and <=65&&!Regex.IsMatch(t,@"[.!?]$")&&EditorialTokens(t).Length<=9).Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToArray();
        var gameplayDetails=relevant.Where(EditorialCoverage).SelectMany(g=>g.GetProperty("evidence").EnumerateArray()).Where(e=>S(e,"kind")=="Gameplay assistance").Select(e=>S(e,"detail")).Where(Meaningful).Distinct(StringComparer.OrdinalIgnoreCase);
        Enrich("specialFeatures",kinds.Concat(gameplayDetails).Concat(departments).Concat(mainRanges.Where(g=>g.GetProperty("department").GetBoolean()).Select(g=>S(g,"printedTitle"))).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),"Editorial coverage and contents section types");
        if(featured.Length>0)
        {
            string publicationName=values.TryGetValue("magazineTitle",out var titleValue)?titleValue.ToString()??item.MagazineTitle:item.MagazineTitle;
            if(!Meaningful(publicationName))publicationName=item.Title;
            string issue=values.TryGetValue("issueNumber",out var issueValue)?issueValue.ToString()??"":item.IssueNumber;
            string date=values.TryGetValue("coverDate",out var dateValue)?dateValue.ToString()??"":item.CoverDate;
            string JoinNatural(IEnumerable<string> list){var a=list.ToArray();return a.Length<=1?string.Join("",a):string.Join(", ",a.Take(a.Length-1))+(a.Length>2?", and ":" and ")+a[^1];}
            var main=relevant.Where(EditorialCoverage).Where(g=>g.GetProperty("cover").GetBoolean()||g.GetProperty("contents").GetBoolean()||A(g,"kinds").Contains("Review")||A(g,"kinds").Contains("Preview")||A(g,"kinds").Contains("Gameplay assistance")).OrderByDescending(g=>g.GetProperty("cover").GetBoolean()).ThenByDescending(g=>g.GetProperty("contents").GetBoolean()).ThenByDescending(g=>g.GetProperty("editorialPages").GetArrayLength()).ThenByDescending(g=>A(g,"kinds").Contains("Review")).Select(g=>S(g,"title")).Distinct(StringComparer.OrdinalIgnoreCase).Take(6).ToArray();
            var artworkTitles=covers.Where(c=>S(c,"source")=="Printed cover artwork credit").Select(c=>S(c,"title")).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var descriptions=gameplayDetails.Concat(kinds.Where(k=>k!="Gameplay assistance"||!gameplayDetails.Any())).Select(k=>k.ToLowerInvariant()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var contentTitles=(contentsAuthoritative?featured:main.Concat(featured.Except(main,StringComparer.OrdinalIgnoreCase))).Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToArray();
            var summary=(Meaningful(date)?"The "+date+" issue of ":"This issue of ")+publicationName+(Meaningful(issue)?" (issue "+issue+")":"");
            if(artworkTitles.Length==0&&S(editorial,"guidePlatform").Length>0)
            {
                int profiles=relevant.Count(g=>A(g,"kinds").Contains("Game profile"));
                summary+=" is a guide to "+S(editorial,"guidePlatform")+" games"+(profiles>0?", including "+profiles.ToString(CultureInfo.InvariantCulture)+" identified game profiles":"")+".";
            }
            else if(artworkTitles.Length==1)summary+=" features "+artworkTitles[0]+" cover artwork"+(descriptions.Length>0?", with "+JoinNatural(descriptions):"")+".";
            else if(descriptions.Length>0)summary+=" includes "+JoinNatural(descriptions)+(primary.Length>0?" for the "+primary:"")+".";
            else summary+=(primary.Length>0?" focuses on the "+primary:" covers video games")+".";
            if(contentTitles.Length>0)summary+=" Game coverage includes "+JoinNatural(contentTitles)+".";
            if(departments.Length>0)summary+=" Regular sections include "+JoinNatural(departments)+".";
            Offer("summary",summary,"Fill empty summary from indexed editorial evidence; existing summary retained",false);
        }
        if(Fingerprint(item)!=fp)throw new InvalidOperationException("Source changed during cataloging. Re-index first.");
        // Enforce alias locks too, using the existing metadata store filter. Report any removed change.
        var update=ItemMetadataJsonReader.Read(JsonSerializer.SerializeToElement(values,Json));
        update=MetadataLockHelper.FilterLockedFields(update,item.MetadataLocks);
        var filtered=JsonSerializer.SerializeToElement(update,Json);
        foreach(var c in changes.Where(c=>!filtered.TryGetProperty(c.Field,out var v)||v.ValueKind==JsonValueKind.Null).ToArray()){changes.Remove(c);held.Add(c.Field+": protected by field alias lock");}
        if(changes.Count>0&&!manual)update=update with{MetadataSource="Magazine document index v8.0 Phase 4.7"};
        return new CatalogPlan("v8.0-phase4.7",fp,update,changes,held,publication,editorial,Math.Round(clock.Elapsed.TotalSeconds,2));
    }
    public static async Task SaveCatalogReportAsync(string root,LibraryItem item,CatalogPlan plan,CancellationToken token)
    {
        var fp=Fingerprint(item);if(fp!=plan.Fingerprint)throw new InvalidOperationException("Source changed before saving catalog report.");
        var dir=DirectoryFor(root,item,fp);await Write(Path.Combine(dir,"catalog-report.json"),plan,token);
    }

    // Phase 4.2: bounded server-owned jobs. Saved page records remain the durable resume checkpoint.
    private static readonly object JobLock = new();
    private static readonly Dictionary<string, IndexJob> Jobs = new(StringComparer.Ordinal);
    private sealed class IndexJob
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public string ItemId { get; init; } = "";
        public bool Force { get; init; }
        public bool Catalog { get; init; }
        public int Workers { get; init; }
        public CancellationTokenSource Stop { get; } = new();
        public DateTimeOffset Started { get; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? Finished { get; set; }
        public string State { get; set; } = "running";
        public string Message { get; set; } = "Preparing document";
        public int Total { get; set; }
        public int Indexed { get; set; }
        public int Processed { get; set; }
        public int Target { get; set; }
        public Dictionary<int,string> Active { get; } = new();
        public Dictionary<int,string> Failed { get; } = new();
        public HashSet<int> Recovered { get; } = new();
        public int[] Missing { get; set; } = [];
        public object? Report { get; set; }
        public Func<string[],object>? ApplyPreview { get; set; }
    }
    private static object Snapshot(IndexJob j)
    {
        var elapsed = Math.Max(.01, ((j.Finished ?? DateTimeOffset.UtcNow)-j.Started).TotalSeconds);
        return new { jobId=j.Id, state=j.State, message=j.Message, totalPages=j.Total, indexed=j.Indexed,
            processed=j.Processed, target=j.Target, force=j.Force, catalog=j.Catalog, workers=j.Workers,
            seconds=Math.Round(elapsed,1), pagesPerMinute=Math.Round(j.Processed*60/elapsed,1),
            remainingSeconds=j.Processed>0&&j.State=="running"? (double?)Math.Round(Math.Max(0,j.Target-j.Processed)*elapsed/j.Processed):null,
            active=j.Active.OrderBy(p=>p.Key).Select(p=>new{page=p.Key,stage=p.Value}).ToArray(),
            failed=j.Failed.OrderBy(p=>p.Key).Select(p=>new{page=p.Key,error=p.Value}).ToArray(), recovered=j.Recovered.OrderBy(p=>p).ToArray(), missing=j.Missing, report=j.Report };
    }
    public static object? JobStatus(string id)
    {
        lock(JobLock) return Jobs.TryGetValue(id,out var j)?Snapshot(j):null;
    }
    public static void RegisterCatalogPreview(string id,Func<string[],object> apply)
    {
        lock(JobLock)
        {
            if(!Jobs.TryGetValue(id,out var job)||job.State!="running"||job.Stop.IsCancellationRequested)throw new OperationCanceledException();
            job.ApplyPreview=apply;
        }
    }
    public static object ApplyCatalogPreview(string id,string jobId,string[] fields)
    {
        lock(JobLock)
        {
            if(!Jobs.TryGetValue(id,out var job)||job.Id!=jobId||job.State!="complete"||job.ApplyPreview is null)throw new InvalidOperationException("Scan preview expired or was already applied. Scan magazine again.");
            if(fields.Length==0)throw new InvalidOperationException("Select at least one suggestion.");
            var result=job.ApplyPreview(fields);
            job.ApplyPreview=null;job.Report=result;job.Message="Selected suggestions applied";
            return result;
        }
    }
    public static CatalogPlan SelectCatalogChanges(CatalogPlan plan,string[] fields,LibraryItem item)
    {
        var wanted=fields.ToHashSet(StringComparer.Ordinal);
        var changes=plan.Changes.Where(c=>wanted.Contains(c.Field)).ToList();
        if(changes.Count==0||changes.Count!=wanted.Count)throw new InvalidOperationException("Selection is not part of this scan preview.");
        var values=changes.ToDictionary(c=>c.Field,c=>c.After,StringComparer.Ordinal);
        var update=MetadataLockHelper.FilterLockedFields(ItemMetadataJsonReader.Read(JsonSerializer.SerializeToElement(values,Json)),item.MetadataLocks);
        var filtered=JsonSerializer.SerializeToElement(update,Json);
        if(changes.Any(c=>!filtered.TryGetProperty(c.Field,out var v)||v.ValueKind==JsonValueKind.Null))throw new InvalidOperationException("Metadata locks changed. Scan magazine again.");
        update=update with{MetadataSource=plan.Update.MetadataSource};
        return plan with{Update=update,Changes=changes,Held=plan.Held.Concat(plan.Changes.Where(c=>!wanted.Contains(c.Field)).Select(c=>c.Field+": not selected")).ToList()};
    }
    public static object? StopJob(string id)
    {
        lock(JobLock)
        {
            if(!Jobs.TryGetValue(id,out var j))return null;
            if(j.State=="running"){j.Message="Stopping after cancellation";j.Stop.Cancel();}
            return Snapshot(j);
        }
    }
    public static object StartJob(string root,LibraryItem item,bool force,bool catalog,int workers,IDisposable lease,
        Func<CancellationToken,Task<object>>? saveCatalog,CancellationToken shutdown,ILogger logger)
    {
        lock(JobLock)
        {
            if(Jobs.TryGetValue(item.Id,out var old)&&old.State=="running"){lease.Dispose();return Snapshot(old);}
            // Limit retained in-memory results; page checkpoints and catalog reports live on disk.
            foreach(var key in Jobs.Where(x=>x.Value.Finished.HasValue).OrderByDescending(x=>x.Value.Finished).Skip(15).Select(x=>x.Key).ToArray())
                Jobs.Remove(key);
            var j=new IndexJob{ItemId=item.Id,Force=force,Catalog=catalog,Workers=Math.Clamp(workers,1,4)};
            Jobs[item.Id]=j;
            _=Task.Run(async()=>
            {
                using var linked=CancellationTokenSource.CreateLinkedTokenSource(j.Stop.Token,shutdown);
                var token=linked.Token;
                try
                {
                    logger.LogInformation("Document index job {Job} started for {Item}, workers {Workers}",j.Id,item.Id,j.Workers);
                    var fp=Fingerprint(item);var dir=DirectoryFor(root,item,fp);
                    string[]? entries=null;
                    if(!IsPdf(item))
                    {
                        lock(JobLock)j.Message="Reading archive page manifest";
                        var output=await RunArchiveWorker("list",item.Path,null,null,token);
                        entries=JsonSerializer.Deserialize<string[]>(output,Json)??[];
                    }
                    await InitializeAsync(root,item,token,entries?.Length);
                    var m=await ReadManifest(dir,fp,token)??throw new InvalidOperationException("Document manifest unavailable.");
                    if(entries is not null&&entries.Length!=m.TotalPages)throw new InvalidOperationException("Archive page count differs from the saved index. Source may have changed.");
                    var done=new HashSet<int>();
                    for(int p=1;p<=m.TotalPages;p++)if(await ReadPage(dir,p,token)is not null)done.Add(p);
                    var todo=Enumerable.Range(1,m.TotalPages).Where(p=>force||!done.Contains(p)).ToArray();
                    lock(JobLock){j.Total=m.TotalPages;j.Indexed=done.Count;j.Target=todo.Length;j.Message="Indexing missing pages";}
                    await Parallel.ForEachAsync(todo,new ParallelOptions{MaxDegreeOfParallelism=j.Workers,CancellationToken=token},async(page,ct)=>
                    {
                        void Stage(string value){lock(JobLock)j.Active[page]=value;logger.LogInformation("Document index {Job} page {Page}: {Stage}",j.Id,page,value);}
                        try
                        {
                            for(int attempt=1;attempt<=2;attempt++)
                            {
                                ct.ThrowIfCancellationRequested();
                                try
                                {
                                    Stage($"Attempt {attempt}: preparing");
                                    var record=await IndexPageAsync(root,item,page,fp,force,ct,entries is null?null:entries[page-1],Stage);
                                    lock(JobLock){if(done.Add(page))j.Indexed++;j.Processed++;j.Failed.Remove(page);if(record.Source.StartsWith("tesseract-recovery",StringComparison.Ordinal))j.Recovered.Add(page);}
                                    break;
                                }
                                catch(Exception ex) when(ex is not OperationCanceledException)
                                {
                                    logger.LogWarning(ex,"Document index {Job} page {Page} attempt {Attempt} failed",j.Id,page,attempt);
                                    if(attempt==2||ex is OcrRecoveryException)
                                    {
                                        lock(JobLock){j.Failed[page]=ex.Message;j.Processed++;}
                                        try{await Write(FailurePath(dir,page),new PageFailure(page,ex.Message,DateTimeOffset.UtcNow),ct);}catch(Exception writeError) when(writeError is not OperationCanceledException){logger.LogWarning(writeError,"Could not persist page {Page} failure",page);}
                                        break;
                                    }
                                    else {Stage("Retrying after extraction error");await Task.Delay(750,ct);}
                                }
                            }
                        }
                        finally{lock(JobLock)j.Active.Remove(page);}
                    });
                    token.ThrowIfCancellationRequested();
                    var missing=new List<int>();
                    for(int p=1;p<=m.TotalPages;p++)if(await ReadPage(dir,p,token)is null)missing.Add(p);
                    bool complete;lock(JobLock){j.Missing=missing.ToArray();j.Indexed=m.TotalPages-missing.Count;complete=j.Failed.Count==0&&missing.Count==0;}
                    logger.LogInformation("Document index job {Job}: verified {Indexed}/{Total}, missing [{Missing}], failed [{Failed}], recovered [{Recovered}]",j.Id,j.Indexed,j.Total,string.Join(",",missing),string.Join(",",j.Failed.Keys),string.Join(",",j.Recovered));
                    if(Fingerprint(item)!=fp)throw new InvalidOperationException("Source changed during indexing; cataloging cancelled.");
                    if(complete&&catalog&&saveCatalog is not null)
                    {
                        lock(JobLock)j.Message="Analyzing magazine metadata";
                        var report=await saveCatalog(token);lock(JobLock){if(j.ApplyPreview is not null)token.ThrowIfCancellationRequested();j.Report=report;}
                    }
                    lock(JobLock){j.State=complete?"complete":"incomplete";j.Message=complete?(catalog?(j.ApplyPreview is not null?"Scan complete; suggestions ready":"Cataloging complete"):"Indexing complete"):"Failed pages retained in report. Resume to retry; cataloging not started.";}
                }
                catch(OperationCanceledException){lock(JobLock){j.State="stopped";j.Message="Stopped; completed page records retained. Check history if catalog saving had begun.";}}
                catch(Exception ex){logger.LogError(ex,"Document index job {Job} failed",j.Id);lock(JobLock){j.State="failed";j.Message=ex.Message;}}
                finally{lock(JobLock){j.Active.Clear();j.Finished=DateTimeOffset.UtcNow;}lease.Dispose();logger.LogInformation("Document index job {Job} finished: {State}, {Indexed}/{Total} indexed, {Seconds:0.0}s. {Message}",j.Id,j.State,j.Indexed,j.Total,(j.Finished.Value-j.Started).TotalSeconds,j.Message);}
            });
            return Snapshot(j);
        }
    }
    // Use the same application assembly as a short-lived archive-only subprocess.
    // A blocked archive read is killed by RunProcess's timeout, not abandoned inside the web process.
    private static async Task<string> RunArchiveWorker(string mode,string path,string? key,string? output,CancellationToken token,int seconds=60)
    {
        var executable=Environment.ProcessPath??throw new InvalidOperationException("Cannot locate archive worker executable.");
        var args=new List<string>();
        if(Path.GetFileNameWithoutExtension(executable).Equals("dotnet",StringComparison.OrdinalIgnoreCase))
            args.Add(typeof(GuidevaultDocumentIndex).Assembly.Location);
        args.Add("--gv-index-archive-worker");args.Add(mode);args.Add(path);
        if(key is not null)args.Add(key);if(output is not null)args.Add(output);
        return await Run(executable,args.ToArray(),token,seconds);
    }
    public static async Task RunArchiveWorkerCommand(string[] args)
    {
        if(args.Length==3&&args[1]=="list")
        {
            Console.Write(JsonSerializer.Serialize(ArchiveReader.GetImageEntryKeys(args[2]),Json));return;
        }
        if(args.Length==5&&args[1]=="prepare")
        {
            if(!int.TryParse(args[3],out var edge)||edge is < 1000 or > 3000)throw new InvalidOperationException("Invalid OCR recovery image size.");
            var info=Image.Identify(args[2]);
            if(info.Width<=0||info.Height<=0||(long)info.Width*info.Height>80_000_000)throw new InvalidOperationException($"Recovery image dimensions {info.Width} x {info.Height} exceed safe decode limits.");
            using var recoveryImage=Image.Load<L8>(args[2]);
            var scale=Math.Min(1d,edge/(double)Math.Max(recoveryImage.Width,recoveryImage.Height));
            int width=Math.Max(1,(int)Math.Round(recoveryImage.Width*scale)),height=Math.Max(1,(int)Math.Round(recoveryImage.Height*scale));
            if(width!=recoveryImage.Width||height!=recoveryImage.Height)recoveryImage.Mutate(x=>x.Resize(width,height));
            await recoveryImage.SaveAsPngAsync(args[4]);
            Console.Write($"{info.Width} x {info.Height} -> {width} x {height}");return;
        }
        if(args.Length!=5||args[1]!="extract")throw new InvalidOperationException("Invalid archive worker arguments.");
        var image=await ArchiveReader.GetImagePageByKeyAsync(args[2],args[3],40_000_000);
        if(image is null)throw new InvalidOperationException("Page image unavailable or unsupported.");
        await File.WriteAllBytesAsync(args[4],image.Value.Bytes);
    }

    // Recovery uses a different input and segmentation; all temporary images stay in the page workspace.
    private static async Task<Extraction> RecognizePage(string input,string temp,int page,CancellationToken token,Action<string>? stage)
    {
        var errors=new List<string>();
        async Task<Extraction> Ocr(string path,string psm,int seconds,string source)
        {
            var tsv=await Run("tesseract",[path,"stdout","-l","eng","--psm",psm,"tsv"],token,seconds);
            var result=ParseTsv(tsv) with {Source=source};
            if(result.Width<=0||result.Height<=0)throw new InvalidOperationException("OCR returned invalid page geometry.");
            return result;
        }
        try{stage?.Invoke("Tesseract standard OCR (90s timeout)");return await Ocr(input,page==1?"11":"3",90,"tesseract-eng");}
        catch(Exception ex) when(ex is not OperationCanceledException){errors.Add("Standard: "+ex.Message);stage?.Invoke("Standard OCR failed: "+ex.Message);}
        foreach(var mode in new[]{(Edge:2400,Psm:page==1?"11":"3",Seconds:45),(Edge:1600,Psm:page==1?"6":"11",Seconds:30)})
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var optimized=Path.Combine(temp,$"recovery-{mode.Edge}.png");
                stage?.Invoke($"Preparing grayscale recovery image (max edge {mode.Edge}px, 30s timeout)");
                var geometry=await RunArchiveWorker("prepare",input,mode.Edge.ToString(CultureInfo.InvariantCulture),optimized,token,30);
                stage?.Invoke($"Recovery OCR {geometry.Trim()} PSM {mode.Psm} ({mode.Seconds}s timeout)");
                var recovered=await Ocr(optimized,mode.Psm,mode.Seconds,$"tesseract-recovery-{mode.Edge}-psm{mode.Psm}");
                if(!Usable(recovered))throw new InvalidOperationException("Recovery returned insufficient readable text; page remains unresolved.");
                return recovered;
            }
            catch(Exception ex) when(ex is not OperationCanceledException){errors.Add($"Recovery {mode.Edge}: {ex.Message}");stage?.Invoke(errors[^1]);}
        }
        throw new OcrRecoveryException(string.Join(" | ",errors));
    }

    private static bool IsPdf(LibraryItem item) => Path.GetExtension(item.Path).Equals(".pdf",StringComparison.OrdinalIgnoreCase);
    private static bool Usable(Extraction p) => Regex.Matches(string.Join(" ",p.Lines.Select(l=>l.Text)),@"\p{L}{2,}").Count>=12;
    private static double Number(string? s) => double.TryParse(s,NumberStyles.Float,CultureInfo.InvariantCulture,out var d)&&double.IsFinite(d)?d:0;
    private static Extraction ParsePdf(string xml)
    {
        using var reader=XmlReader.Create(new StringReader(xml),new XmlReaderSettings{DtdProcessing=DtdProcessing.Ignore,XmlResolver=null});
        var doc=XDocument.Load(reader); var page=doc.Descendants().First(e=>e.Name.LocalName=="page");
        double width=Number((string?)page.Attribute("width")),height=Number((string?)page.Attribute("height")); var lines=new List<Line>();
        foreach(var l in page.Descendants().Where(e=>e.Name.LocalName=="line"))
        {
            var text=string.Join(" ",l.Descendants().Where(e=>e.Name.LocalName=="word").Select(e=>e.Value)); if(string.IsNullOrWhiteSpace(text))continue;
            var x=Number((string?)l.Attribute("xMin"));var y=Number((string?)l.Attribute("yMin"));
            lines.Add(new Line(text,x,y,Number((string?)l.Attribute("xMax"))-x,Number((string?)l.Attribute("yMax"))-y,null));
        }
        return new Extraction("pdf-text",width,height,"points",lines);
    }
    private static Extraction ParseTsv(string tsv)
    {
        double width=0,height=0; var groups=new Dictionary<string,List<Line>>();
        foreach(var row in tsv.Split('\n').Skip(1))
        {
            var p=row.TrimEnd('\r').Split('\t',12); if(p.Length!=12)continue;
            if(p[0]=="1"){width=Number(p[8]);height=Number(p[9]);continue;}
            if(p[0]!="5"||string.IsNullOrWhiteSpace(p[11])||Number(p[10])<0)continue;
            var key=string.Join(":",p.Skip(1).Take(4)); if(!groups.TryGetValue(key,out var g))groups[key]=g=new();
            g.Add(new Line(p[11].Trim(),Number(p[6]),Number(p[7]),Number(p[8]),Number(p[9]),Number(p[10])));
        }
        var lines=groups.Values.Select(g=>{double x=g.Min(l=>l.X),y=g.Min(l=>l.Y);return new Line(string.Join(" ",g.Select(l=>l.Text)),x,y,g.Max(l=>l.X+l.Width)-x,g.Max(l=>l.Y+l.Height)-y,g.Average(l=>l.Confidence));}).ToList();
        return new Extraction("tesseract-eng",width,height,"pixels",lines);
    }
    private static async Task<string> Run(string executable,string[] args,CancellationToken token,int seconds=90)
    {
        using var p=new Process{StartInfo=new ProcessStartInfo(executable){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true}};
        foreach(var a in args)p.StartInfo.ArgumentList.Add(a);p.StartInfo.Environment["OMP_THREAD_LIMIT"]="2";p.StartInfo.Environment["LC_ALL"]="C";
        p.Start(); using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(seconds));
        var stdout=p.StandardOutput.ReadToEndAsync(timeout.Token);var stderr=p.StandardError.ReadToEndAsync(timeout.Token);
        try { await p.WaitForExitAsync(timeout.Token);var output=await stdout;var error=await stderr;if(p.ExitCode!=0)throw new InvalidOperationException($"{Path.GetFileName(executable)} failed ({p.ExitCode}): {error.Trim()[..Math.Min(800,error.Trim().Length)]}");return output; }
        catch(OperationCanceledException){if(token.IsCancellationRequested)throw;throw new InvalidOperationException($"{executable} timed out.");}
        finally{try{if(!p.HasExited){p.Kill(true);await p.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));}}catch{}}
    }
}
