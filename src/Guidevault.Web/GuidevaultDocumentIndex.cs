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
        var done = new List<int>(); var warnings = new List<int>(); double seconds=0;
        if(m is not null)
            for(int page=1;page<=m.TotalPages;page++)
            {
                token.ThrowIfCancellationRequested(); var p = await ReadPage(dir,page,token); if(p is null) continue;
                done.Add(page); seconds+=p.Seconds; if(p.Warning is not null) warnings.Add(page);
            }
        return new { revision=Revision, fingerprint=fp, totalPages=m?.TotalPages??0, indexedPages=done, warningPages=warnings, extractionSeconds=Math.Round(seconds,2) };
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
                stage?.Invoke("Tesseract OCR (90s timeout)");
                var tsv=await Run("tesseract",[input,"stdout","-l","eng","--psm",page==1?"11":"3","tsv"],token); extracted=ParseTsv(tsv);
            }
            if(extracted.Width<=0 || extracted.Height<=0) throw new InvalidOperationException("Extraction returned invalid page geometry.");
            var text=string.Join("\n",extracted.Lines.Select(l=>l.Text));
            var record=new PageRecord(page,extracted.Source,text,extracted.Width,extracted.Height,extracted.Units,extracted.Lines,Math.Round(timer.Elapsed.TotalSeconds,2),Usable(extracted)?null:"Little readable text; this may be an image-only page or poor recognition.",Revision);
            token.ThrowIfCancellationRequested(); if(Fingerprint(item)!=fp) throw new InvalidOperationException("Source changed during extraction. Page was not saved.");
            stage?.Invoke("Saving page index");await Write(PagePath(dir,page),record,token); return record;
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
    public static async Task<object> PublicationAsync(string root, LibraryItem item, IReadOnlyList<LibraryItem> library, CancellationToken token)
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
        void Add(string field,string value,int? page,string source,string proof)
        {
            value=Regex.Replace(value.Trim(),@"\s+"," ").Trim(' ',':',';','–','—');
            if (!Meaningful(value) || value.Length>120 || Key(value).Length==0) return;
            if (evidence.Any(e=>e.Field==field && EvidenceKey(field,e.Value)==EvidenceKey(field,value) && e.Page==page && e.Source==source)) return;
            evidence.Add(new(field,value,page,source,proof[..Math.Min(500,proof.Length)]));
        }
        const string months = @"January|February|March|April|May|June|July|August|September|October|November|December|Jan\.?|Feb\.?|Mar\.?|Apr\.?|Jun\.?|Jul\.?|Aug\.?|Sept?\.?|Oct\.?|Nov\.?|Dec\.?|Spring|Summer|Autumn|Fall|Winter";
        string datePattern = $@"(?:{months})(?:\s*[/–-]\s*(?:{months}))?\s+(?:19|20)\d{{2}}";
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
            if(page>1 && p.Height>0)
            foreach(var footer in p.Lines.Where(l=>l.Y>=p.Height*.89&&l.Text.Length<=120&&!(l.Confidence is double c&&c<45)))
            {
                var d=Regex.Match(footer.Text,datePattern,ci);if(!d.Success)continue;
                if(ContainsTitle(footer.Text,item.MagazineTitle)||Regex.IsMatch(footer.Text,@"^\s*(?:\d{1,4}\s+)?"+datePattern+@"(?:\s+\d{1,4})?\s*$",ci))footerDates.Add((d.Value,page,footer.Text));
            }
            // Publication-related correspondence, not arbitrary reader addresses or advertisements.
            // Restrict to opening/closing editorial pages; require an organizational address and publication context.
            if(page<=8 || page>=manifest.TotalPages-1)
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
            bool creditPage=Regex.IsMatch(p.Text,@"\b(?:editor[\s–—-]+in[\s–—-]+chief|ISSN|masthead|publication date|publisher\s*:|published\s+(?:by|monthly|weekly|quarterly|biweekly|bimonthly|annually)|all rights reserved|copyright)\b|©",ci);
            bool roles=Regex.IsMatch(p.Text,@"\b(?:editor[\s–—-]+in[\s–—-]+chief|managing editor|executive editor|art director)\b|\beditor\s*:",ci);
            bool masthead=creditPage && (titleOnPage || roles && Regex.IsMatch(p.Text,@"\b(?:publisher|published|ISSN|managing editor|art director)\b",ci));
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
                    const string publisherPattern=@"\bpublished(?:\s+(?:monthly|weekly|quarterly|annually|bimonthly|biweekly))?\s+by\s+([\p{L}][\p{L}\p{N} &'’.()-]{2,90}?)(?=\s+(?:at|located|ISSN|copyright|all rights|subscription)\b|[,;]|$)";
                    var pub=Regex.Match(text,publisherPattern,ci);
                    if(!pub.Success)pub=Regex.Match(joined,publisherPattern,ci);
                    if(pub.Success)Add("publisher",pub.Groups[1].Value.TrimEnd('.'),page,"Publication-linked publisher credit",text==joined||Regex.IsMatch(text,publisherPattern,ci)?text:joined);
                    // Copyright holder is not necessarily publisher; record the explicit copyright separately.
                    var rights=Regex.Match(text,@"(?:©|\(c\)|\bcopyright\b)\s*(?:(?:19|20)\d{2}\s*[,–-]?\s*)?([\p{L}][\p{L}\p{N} &'’.()-]{2,80}?)(?=\s+(?:all rights|copyright|ISSN|printed)\b|[,;]|$)",ci);
                    if(rights.Success && (masthead || titleLine) && Regex.IsMatch(rights.Groups[1].Value,@"\b(?:inc\.?|ltd\.?|limited|corporation|corp\.?|company|publishing|publications|press|media|of America)\b",ci))
                        Add("copyrightHolder",rights.Groups[1].Value.TrimEnd('.'),page,"Printed copyright statement (not publisher credit)",text);
                    Match("region",@"\b(?:printed|published) in (?:the )?(United States(?: of America)?|U\.?S\.?A\.?|United Kingdom|U\.?K\.?|Canada|Australia|Japan|Germany|France)\b","Printed publication location");
                    if(Regex.IsMatch(joined,@"\b(?:published by|publisher|editorial office|publication office|subscriptions)\b",ci) && Regex.IsMatch(joined,@"\b(?:AL|AK|AZ|AR|CA|CO|CT|DE|FL|GA|HI|ID|IL|IN|IA|KS|KY|LA|ME|MD|MA|MI|MN|MS|MO|MT|NE|NV|NH|NJ|NM|NY|NC|ND|OH|OK|OR|PA|RI|SC|SD|TN|TX|UT|VT|VA|WA|WV|WI|WY|DC)\s+\d{5}(?:-\d{4})?\b",RegexOptions.CultureInvariant))
                        Add("region","USA",page,"Publication address candidate",joined);
                    Match("frequency",@"\b(?:published|issued|appears)\s+(monthly|weekly|quarterly|annually|bimonthly|biweekly)\b","Printed publication frequency");
                }
                if((page==1 || masthead) && (nearCoverDate || titleLine || masthead || line.Y<p.Height*.25))
                {
                    var dedication=Regex.Match(text,@"\b(?:100\s*%\s*(?:Sega\s+)?|(?:official|dedicated|exclusively)\s+(?:magazine|newsletter|coverage|for|to|about|of)\s+(?:for\s+|to\s+|the\s+)*)("+platformPattern+@")\b",ci);
                    if(!dedication.Success)dedication=Regex.Match(text,@"\b("+platformPattern+@")\s+(?:owners['’]?\s+|gaming\s+)?(?:magazine|newsletter)\b",ci);
                    if(dedication.Success)Add("primarySystem",PlatformName(dedication.Groups[1].Value),page,"Explicit publication platform dedication",text);
                }
                if(!masthead) continue;
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
        if(indexed!=manifest.TotalPages)throw new InvalidOperationException($"Document index incomplete ({indexed}/{manifest.TotalPages}). Resume indexing first.");
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
        var peers=library.Where(x=>x.Id!=item.Id && x.Kind=="Magazine" && titleKey.Length>=4 && Key(x.MagazineTitle)==titleKey &&
            (!Meaningful(item.Region)||!Meaningful(x.Region)||Key(item.Region)==Key(x.Region)) && (!Meaningful(item.LanguageTag)||!Meaningful(x.LanguageTag)||Key(item.LanguageTag)==Key(x.LanguageTag))).ToArray();
        foreach(var field in fields.Where(f=>f.Shared))
        {
            string Value(LibraryItem x)=>field.Key switch {"publisher"=>x.Publisher,"region"=>x.Region,"languageTag"=>x.LanguageTag,"primarySystem"=>x.PrimarySystem,"platformFocus"=>x.PlatformFocus,"magazineCategory"=>x.MagazineCategory,_=>""};
            foreach(var group in peers.Where(x=>MetadataLockHelper.IsLocked(x.MetadataLocks,field.Key)||MetadataStatusHelper.Normalize(x.MetadataStatus) is MetadataStatusHelper.Reviewed or MetadataStatusHelper.Locked).Where(x=>Meaningful(Value(x))).GroupBy(x=>Key(Value(x))))
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
            if(e.Field=="publisher")
            {
                var core=PublisherBase(e.Value);var letters=Key(core);
                if(letters.Length is >=2 and <=6&&Regex.IsMatch(core,@"^[A-Z.\s-]+$"))
                {var matches=expanded.Where(x=>Initials(x)==letters).ToArray();if(matches.Length==1)return EvidenceKey("publisher",matches[0]);}
            }
            return EvidenceKey(e.Field,e.Value);
        }
        var report=fields.Select(f=>{
            var candidates=evidence.Where(e=>e.Field==f.Key).GroupBy(GroupKey).Select(g=>new {value=g.OrderByDescending(e=>f.Key=="publisher"&&e.Source=="Reviewed publication peer"&&expanded.Contains(e.Value)).First().Value,evidence=g.OrderBy(e=>e.Source.Contains("candidate",StringComparison.OrdinalIgnoreCase)||e.Source.Contains("inference",StringComparison.OrdinalIgnoreCase)?1:0).Take(8).ToArray(),support=g.All(e=>e.Source.Contains("candidate",StringComparison.OrdinalIgnoreCase)||e.Source.Contains("inference",StringComparison.OrdinalIgnoreCase))?"Indirect":"Direct or reviewed"}).ToArray();
            var status=candidates.Length>1?(f.Key=="coverPrice"?"Multiple price candidates":"Conflicting evidence"):candidates.Length==0?"No indexed evidence":!Meaningful(f.Current)?"Candidate":EvidenceKey(f.Key,f.Current)==EvidenceKey(f.Key,candidates[0].value)?"Matches current":"Differs from current";
            return new {field=f.Key,label=f.Label,current=f.Current,locked=MetadataLockHelper.IsLocked(item.MetadataLocks,f.Key),status,candidates};
        }).ToArray();
        return new {revision="v8.0-phase2.4",indexedPages=indexed,totalPages=manifest.TotalPages,relatedIssues=peers.Length,seconds=Math.Round(watch.Elapsed.TotalSeconds,2),fields=report,openingPages,platformSignals};
    }
    // Phase 3: read-only editorial analysis over the persistent page index.
    private sealed record EditorialTitle(string Title, string Platform, bool Alias);
    private sealed record EditorialHit(string Title, int Page, string Kind, string Evidence, string[] Platforms, bool Cover, bool Contents, double Height);
    private static readonly Regex EditorialWords = new(@"[\p{L}\p{N}]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex EditorialKinds = new(@"\b(?:reviews?|previews?|walkthroughs?|strateg(?:y|ies)|tips|hints|cheats?|codes|passwords?|interviews?|news|features?|spotlight|mailbag|letters|contest|competition|editorial|tech talk|scoreboard|high scores|buyers['’]? guide|special report|retrospective)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Advertising = new(@"\b(?:advertisement|advertising supplement|paid advertisement|order now|call toll.free|send check or money order|limited time offer|while supplies last|suggested retail price)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Merchandise = new(@"\b(?:t[ -]?shirts?|sweatshirts?|caps?|hats?|athletic bags?|order form|merchandise|shipping and handling|mail order)\b",RegexOptions.IgnoreCase|RegexOptions.Compiled);
    private static readonly Regex Price = new(@"(?:\$\s*\d+(?:[.,]\d{2})?|\b\d+\s*(?:USD|GBP)\b)",RegexOptions.IgnoreCase|RegexOptions.Compiled);
    private static readonly Regex GameplayCue = new(@"\b(?:to (?:beat|defeat|avoid|prevent|unlock|select|complete|control|kill|get|reach|win)|(?:press|hold) (?:the |button |down |up )|(?:sound test|extra lives|secret passage|hidden level|highest score)|how (?:do|can) (?:i|you)|(?:at the beginning of|when in) (?:stage|level|the main))\b",RegexOptions.IgnoreCase|RegexOptions.Compiled);
    private static readonly Regex CatalogCue = new(@"(?:\[\s*[_ ]?\]|\(\s*[_ ]?\)|\|_\]|\b(?:check (?:all|the)|which (?:of|games)|games (?:you own|do you own)|order form)\b)",RegexOptions.IgnoreCase|RegexOptions.Compiled);
    private static string NumberedToken(string s)=>s.ToLowerInvariant() switch {"ii"=>"2","iii"=>"3","iv"=>"4","v"=>"5","vi"=>"6","vii"=>"7","viii"=>"8","ix"=>"9","x"=>"10","xi"=>"11","xii"=>"12",_=>s.ToLowerInvariant()};
    private static string TitleKey(IEnumerable<string> words)=>string.Join(" ",words.Select((word,i)=>i==0?word:NumberedToken(word)));
    private static bool GenericPhrase(string raw)=>Regex.IsMatch(raw,@"^(?:basic|advanced|general|secret|special|helpful)\s+(?:strateg(?:y|ies)|tips|hints|codes|instructions|moves|features)$",RegexOptions.IgnoreCase);
    private static readonly Regex ContentsNumber = new(@"(?:\.{2,}|\s{2,})\s*\d{1,3}\s*$",RegexOptions.Compiled);
    private static readonly Regex LegalLine = new(@"\b(?:all rights reserved|registered trademark|copyright|ISSN|published (?:by|quarterly|monthly)|editor in chief|advertising materials)\b|©",RegexOptions.IgnoreCase|RegexOptions.Compiled);
    private static string[] EditorialTokens(string s) => EditorialWords.Matches(s).Select(m=>m.Value.ToLowerInvariant()).ToArray();
    private static bool SequelToken(string s) => int.TryParse(s,out _) || Regex.IsMatch(s,@"^(?:i|ii|iii|iv|v|vi|vii|viii|ix|x|xi|xii)$",RegexOptions.IgnoreCase);
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
    public static async Task<object> EditorialAsync(string root, LibraryItem item, IEnumerable<GuidevaultLaunchBoxGame> games, CancellationToken token)
    {
        var timer=Stopwatch.StartNew();var fp=Fingerprint(item);var dir=DirectoryFor(root,item,fp);
        var manifest=await ReadManifest(dir,fp,token)??throw new InvalidOperationException("Index this magazine first.");
        var pages=new List<PageRecord>();
        for(int n=1;n<=manifest.TotalPages;n++){token.ThrowIfCancellationRequested();var p=await ReadPage(dir,n,token);if(p is not null)pages.Add(p);}
        if(pages.Count!=manifest.TotalPages)throw new InvalidOperationException("Complete the document index before analyzing editorial content.");
        int.TryParse(Regex.Match(item.Year??"",@"\b(?:19|20)\d{2}\b").Value,out int year);
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
            if(EditorialTokens(shortTitle).Length>=2)names=names.Append(shortTitle);
            foreach(var name in names)
            {
                if(string.IsNullOrWhiteSpace(name))continue;var words=EditorialTokens(name);
                if(words.Length==0||words.Length>16||string.Concat(words).Length<3)continue;
                if(words.Length==1&&generic.Contains(words[0]))continue;
                var key=TitleKey(words);maxWords=Math.Max(maxWords,words.Length);
                if(!lookup.TryGetValue(key,out var list))lookup[key]=list=new();
                var entry=new EditorialTitle(g.Title.Trim(),g.Platform??"",!string.Equals(name,g.Title,StringComparison.OrdinalIgnoreCase));
                if(!list.Contains(entry))list.Add(entry);
            }
        }
        var resolved=new Dictionary<string,EditorialTitle[]>(StringComparer.Ordinal);
        foreach(var pair in lookup)
        {
            // Exact names take precedence over aliases; ambiguous distinct numbered titles are not merged.
            var direct=pair.Value.Where(x=>!x.Alias).ToArray();var options=direct.Length>0?direct:pair.Value.ToArray();
            if(options.Select(x=>TitleKey(EditorialTokens(x.Title))).Distinct(StringComparer.Ordinal).Count()!=1)continue;
            // A short alias must not silently expand to a different sequel number.
            if(options.All(x=>x.Alias)&&options.Any(x=>!EditorialTokens(x.Title).Where(SequelToken).Select(NumberedToken).SequenceEqual(pair.Key.Split(' ').Where(SequelToken).Select(NumberedToken))))continue;
            resolved[pair.Key]=options;
        }
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
            bool contents=!readerPage&&!masthead&&p.Page>1&&p.Page<=10&&(explicitContents||exactEntries>=4||pageRefs>=3&&exactEntries>=1||compactHeadings>=4&&exactEntries>=1);
            if(contents)contentsPages.Add(p.Page);
            bool merchandisePage=lines.Count(l=>Merchandise.IsMatch(l.Text)&&Price.IsMatch(l.Text))>=2 || (Merchandise.Matches(p.Text).Count>=3&&Price.Matches(p.Text).Count>=3);
            bool catalogPage=lines.Count(l=>CatalogCue.IsMatch(l.Text))>=5;
            bool promoPage=!contents&&!masthead&&p.Page<=4&&Regex.IsMatch(p.Text,@"\b(?:all rights reserved|trademarks? of|registered trademark)\b",RegexOptions.IgnoreCase)&&Regex.IsMatch(p.Text,@"\b(?:check out|exclusive|available now|coming soon|from the makers|only on|only for|you want a taste)\b",RegexOptions.IgnoreCase);
            bool advertisement=Advertising.Matches(p.Text).Select(m=>m.Value.ToLowerInvariant()).Distinct().Count()>=2 || merchandisePage||promoPage;
            if(advertisement)advertisingPages.Add(p.Page);
            string activeKind="Reference";int lastHeading=-10;
            var articleSubjects=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool assistancePage=!readerPage&&!contents&&!advertisement&&lines.Count(l=>GameplayCue.IsMatch(l.Text))>=2;
            for(int li=0;li<lines.Length;li++)
            {
                token.ThrowIfCancellationRequested();var l=lines[li];var text=l.Text.Trim();if(text.Length>2000||LegalLine.IsMatch(text))continue;
                int originalLength=text.Length;
                // Join a possible line wrap only in the same column and with plausible spacing.
                // Only matches beginning in the original line are accepted.
                string matchText=text;
                if(li+1<lines.Length&&!Regex.IsMatch(text,@"[.!?;:™®©]\s*$")&&p.Width>0&&p.Height>0)
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
                for(int j=0;j<words.Length;j++)if(Regex.IsMatch(matches[j].Value,@"^I[JLl]$")){ocrWords[j]="2";hasOcrRoman=true;}
                bool exactEntry=ExactEntry(text);int lineWordCount=EditorialTokens(text).Length;
                string SpanKey(string[] tokens,int start,int len)=>len==1?words[start]:words[start]==tokens[start]?string.Join(" ",tokens,start,len):words[start]+" "+string.Join(" ",tokens,start+1,len-1);
                for(int i=0;i<words.Length;i++)
                {
                    if(matches[i].Index>=originalLength)break;
                    for(int len=Math.Min(maxWords,words.Length-i);len>=1;len--)
                    {
                        string key=SpanKey(normalizedWords,i,len);
                        if(!resolved.TryGetValue(key,out var options))
                        {
                            // Bounded OCR Roman II variant, only for multi-word catalog matches.
                            if(len<3||!hasOcrRoman||!resolved.TryGetValue(SpanKey(ocrWords,i,len),out options))continue;
                        }
                        int end=i+len;int charEnd=matches[end-1].Index+matches[end-1].Length;
                        // Never accept a shorter franchise name when the printed text has a sequel suffix.
                        if(end<words.Length&&(matches[end].Index<originalLength||charEnd>originalLength)&&(SequelToken(matches[end].Value)||Regex.IsMatch(matches[end].Value,@"^I[JLl]$")))continue;
                        string raw=matchText.Substring(matches[i].Index,charEnd-matches[i].Index);
                        bool marked=Regex.IsMatch(matchText.Substring(charEnd),@"^\s*[™®©]");
                        bool titleCase=raw.Any(char.IsUpper);bool standalone=lineWordCount<=len+2;
                        bool prominent=height>=.035&&shortLine;
                        bool fullLine=lineWordCount==len;
                        bool startsTitle=matches[i].Index<=2;
                        if(GenericPhrase(raw)&&options.All(x=>x.Alias)&&!marked||!fullLine&&!marked&&Regex.IsMatch(raw,@"^(?:pick up|fire ball)$",RegexOptions.IgnoreCase)){i=end-1;break;}
                        // Short/common terms and aliases need independent title-like evidence, not a nearby gameplay keyword.
                        if(len==1&&!(fullLine&&(marked||prominent||contents)||marked&&startsTitle||marked&&Regex.IsMatch(text,@"\b(?:in|play|playing|game)\s+"+Regex.Escape(raw)+@"\b",RegexOptions.IgnoreCase))){i=end-1;break;}
                        if(len==1&&i>0&&Regex.IsMatch(words[i-1],@"^(?:easter|zombie|[a-z]+[i1l]{2})$",RegexOptions.IgnoreCase)){i=end-1;break;}
                        if(options.All(x=>x.Alias)&&!exactEntry&&!marked&&!prominent&&(!startsTitle||!titleCase)&&len<=2){i=end-1;break;}
                        string near=heading||p.Page==1?localKind:"Reference";
                        if(near=="Reference"&&contents&&standalone&&li+1<lines.Length&&Regex.IsMatch(lines[li+1].Text,@"^\s*(?:our|a|the)\s+(?:special\s+)?",RegexOptions.IgnoreCase))near=Coverage(lines[li+1].Text);
                        if(near=="Reference"&&contents&&li-lastHeading<=2)near=activeKind;
                        if(readerPage)near="Reader letters";
                        else if(!contents&&p.Page>1&&(GameplayCue.IsMatch(text)||(!catalogPage&&assistancePage&&marked)||(!catalogPage&&activeKind=="Gameplay assistance"&&li-lastHeading<=8&&marked)))near="Gameplay assistance";
                        else if(!contents&&p.Page>1&&near=="Reference"&&(activeKind is "Review" or "Preview" or "Feature")&&li-lastHeading<=5&&marked)near=activeKind;
                        // Single ordinary words require title typography or explicit editorial context, not raw frequency.
                        if(len==1&&(!titleCase&&!marked||!marked&&!standalone&&!prominent&&near=="Reference")){i=end-1;break;}
                        bool ad=promoPage||Advertising.IsMatch(text)||(Merchandise.IsMatch(text)&&(Price.IsMatch(text)||merchandisePage||standalone))||(advertisement&&!contents&&!readerPage&&!GameplayCue.IsMatch(text)&&near=="Reference");
                        string kind=ad?"Possible advertising":catalogPage&&near=="Reference"?"Catalog listing":near;
                        if(!ad&&contents&&exactEntry&&(kind is not "Review" and not "Preview" and not "Interview" and not "Gameplay assistance"))kind="Contents reference";
                        if(!ad&&kind=="Reference"&&contents&&standalone)kind="Contents reference";
                        if(!ad&&kind=="Reference"&&p.Page==1&&(marked||prominent||standalone))kind="Cover reference";
                        if(!ad&&kind=="Reference"&&prominent&&standalone)kind="Heading reference";
                        if(catalogPage&&options.All(x=>x.Alias)&&!marked&&near=="Reference"&&Regex.IsMatch(raw,@"\b(?:family|party|sports|action|adventure|arcade|board|card) games$",RegexOptions.IgnoreCase))
                        {i=end-1;break;}
                        var canonical=options.OrderBy(x=>x.Alias).ThenBy(x=>x.Title.Length).ThenBy(x=>x.Title,StringComparer.Ordinal).First().Title;
                        if(!ad&&!contents&&!readerPage&&!catalogPage&&p.Page>1&&exactEntry&&prominent&&len>=2)articleSubjects.Add(canonical);
                        if(!ad&&!contents&&!readerPage&&!catalogPage&&kind=="Reference"&&articleSubjects.Contains(canonical))kind="Feature";
                        if(!ad&&contents&&exactEntry&&headingKeys.Add(p.Page+":entry:"+canonical))headings.Add(new{page=p.Page,title=canonical,kind=kind,source="Contents candidate",printedPage=PrintedPage(text)});
                        var platforms=options.Select(x=>x.Platform).Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x=>x).ToArray();
                        hits.Add(new EditorialHit(canonical,p.Page,kind,charEnd>originalLength?matchText:text,platforms,p.Page==1,contents,height));i=end-1;break;
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
        bool Featured(EditorialHit h)=>h.Kind is "Review" or "Preview" or "Gameplay assistance" or "Feature" or "Interview" or "Cover reference" or "Heading reference" or "Contents reference";
        // Infer platform from editorial evidence before excluding incompatible incidental matches.
        // Keep explicit coverage of other platforms rather than forcing a single-platform magazine.
        var preliminary=hits.Where(h=>Featured(h)&&h.Platforms.Length==1&&h.Kind!="Cover reference")
            .GroupBy(h=>h.Platforms[0],StringComparer.OrdinalIgnoreCase)
            .Select(g=>new{platform=g.Key,count=g.Select(h=>h.Title).Distinct(StringComparer.OrdinalIgnoreCase).Count()})
            .OrderByDescending(g=>g.count).ToArray();
        string? dominant=preliminary.Length>0&&preliminary[0].count>=3&&(preliminary.Length==1||preliminary[0].count>=preliminary[1].count*3+1)?preliminary[0].platform:null;
        var excluded=new List<object>();
        if(dominant is not null)
        {
            var offPlatform=hits.GroupBy(h=>h.Title,StringComparer.OrdinalIgnoreCase).Where(g=>g.All(h=>h.Platforms.Length>0&&!h.Platforms.Contains(dominant,StringComparer.OrdinalIgnoreCase))&&g.All(h=>!Featured(h))).Select(g=>g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach(var g in hits.Where(h=>offPlatform.Contains(h.Title)).GroupBy(h=>h.Title,StringComparer.OrdinalIgnoreCase))excluded.Add(new{title=g.Key,reason="Incidental match outside inferred platform",pages=g.Select(h=>h.Page).Distinct().OrderBy(n=>n).ToArray()});
            hits.RemoveAll(h=>offPlatform.Contains(h.Title));
        }
        // A shorter title in a feature section can be a shorthand for a longer title named in the contents.
        // Do not merge numbered sequels or titles with independent review/preview/contents evidence.
        var explicitTitles=hits.Where(h=>h.Contents&&Featured(h)).GroupBy(h=>h.Title,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToArray();
        foreach(var shorter in hits.GroupBy(h=>h.Title,StringComparer.OrdinalIgnoreCase).ToArray())
        {
            var st=EditorialTokens(shorter.Key);if(st.Length<2||shorter.Any(h=>h.Contents||(h.Kind is "Review" or "Preview" or "Interview")))continue;
            var longer=explicitTitles.Where(h=>{var lt=EditorialTokens(h.Title);return lt.Length>st.Length&&lt.Take(st.Length).SequenceEqual(st)&&!lt.Skip(st.Length).Any(SequelToken)&&h.Platforms.Intersect(shorter.SelectMany(x=>x.Platforms),StringComparer.OrdinalIgnoreCase).Any();}).ToArray();
            if(longer.Length!=1)continue;
            var target=longer[0];
            foreach(var hit in shorter.Where(h=>h.Kind=="Heading reference"||h.Kind=="Reference"&&shorter.Any(x=>x.Page==h.Page&&x.Kind=="Heading reference")).ToArray())
            {
                hits.Remove(hit);hits.Add(hit with{Title=target.Title,Platforms=target.Platforms,Kind=hit.Kind=="Heading reference"?"Feature":hit.Kind});
            }
        }
        var groups=hits.GroupBy(h=>TitleKey(EditorialTokens(h.Title))).Select(g=>new{
            title=g.First().Title, featured=g.Any(Featured), editorialPages=g.Where(Featured).Select(h=>h.Page).Distinct().OrderBy(x=>x).ToArray(),
            pages=g.Select(h=>h.Page).Distinct().OrderBy(x=>x).ToArray(),cover=g.Any(h=>h.Cover&&h.Kind!="Possible advertising"),contents=g.Any(h=>h.Contents&&Featured(h)),
            platforms=g.SelectMany(h=>h.Platforms).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x=>x).ToArray(),
            kinds=g.Select(h=>h.Kind).Distinct().OrderBy(x=>x).ToArray(),evidence=g.OrderByDescending(Featured).ThenBy(h=>h.Page).GroupBy(h=>new{h.Page,h.Kind}).Select(x=>x.First()).Take(12).Select(h=>new{h.Page,h.Kind,h.Evidence}).ToArray()
        }).OrderByDescending(g=>g.featured).ThenByDescending(g=>g.cover).ThenByDescending(g=>g.contents).ThenByDescending(g=>g.editorialPages.Length).ThenBy(g=>g.title).ToArray();
        // No automatic cover winner: unreadable artwork/typography cannot be recovered from text alone.
        var coverCandidates=groups.Where(g=>g.cover||g.contents&&g.kinds.Contains("Review"))
            .OrderByDescending(g=>(g.contents&&g.kinds.Contains("Review")?5:0)+(g.cover?3:0)+(g.editorialPages.Any(n=>n>2)?2:0))
            .ThenByDescending(g=>g.editorialPages.Length).Take(20).Select(g=>new{g.title,source=g.cover?"Cover text": "Contents review only; cover unverified",g.editorialPages}).ToArray();
        var platformSignals=groups.Where(g=>g.featured&&g.platforms.Length==1).GroupBy(g=>g.platforms[0],StringComparer.OrdinalIgnoreCase).Select(g=>new{platform=g.Key,featuredTitles=g.Count(),titles=g.Select(x=>x.title).ToArray()}).OrderByDescending(g=>g.featuredTitles).ToArray();
        string? primary=platformSignals.Length>0&&platformSignals[0].featuredTitles>=2&&(platformSignals.Length==1||platformSignals[0].featuredTitles>=platformSignals[1].featuredTitles*2+1)?platformSignals[0].platform:null;
        if(Fingerprint(item)!=fp)throw new InvalidOperationException("Source changed during analysis. Re-index the document.");
        return new{revision="v8.0-phase3.2",excluded,indexedPages=pages.Count,totalPages=manifest.TotalPages,catalogGames=gameCount,contentsPages,advertisingPages,coverCandidates,primarySystemCandidate=primary,platformSignals,featuredCount=groups.Count(g=>g.featured),mentionedCount=groups.Count(g=>!g.featured&&g.kinds.Any(k=>k!="Possible advertising")),advertisingOnlyCount=groups.Count(g=>g.kinds.All(k=>k=="Possible advertising")),games=groups,headings,seconds=Math.Round(timer.Elapsed.TotalSeconds,2)};
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
        bool Equal(string field,JsonElement a,JsonElement b)=>a.ValueKind==JsonValueKind.String&&b.ValueKind==JsonValueKind.String?EvidenceKey(field,a.GetString())==EvidenceKey(field,b.GetString()):a.ValueKind==JsonValueKind.Array&&b.ValueKind==JsonValueKind.Array?a.EnumerateArray().Select(x=>x.GetString()).SequenceEqual(b.EnumerateArray().Select(x=>x.GetString()),StringComparer.OrdinalIgnoreCase):a.GetRawText()==b.GetRawText();
        void Offer(string field,object value,string reason,bool canReplace=true)
        {
            var after=JsonSerializer.SerializeToElement(value,Json); if(!Nonempty(after))return;
            var before=current.TryGetProperty(field,out var old)?old:JsonSerializer.SerializeToElement<object?>(null,Json);
            if(Equal(field,before,after))return;
            if(protectedItem){held.Add(field+": protected metadata (record status: "+MetadataStatusHelper.Normalize(item.MetadataStatus)+") — "+reason);return;}
            if(MetadataLockHelper.IsLocked(item.MetadataLocks,field)){held.Add(field+": protected metadata (field locked) — "+reason);return;}
            if((Nonempty(before)||before.ValueKind==JsonValueKind.String&&!string.IsNullOrWhiteSpace(before.GetString()))&&(!replace||manual||!canReplace)){held.Add(field+": existing value retained ("+(manual?"manual edit source":!replace?"replacement option disabled":"insufficient replacement evidence")+") — "+reason);return;}
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
            if(key=="pageCount"){if(int.TryParse(val,out var count))Offer(key,count,"Indexed digital page count",false);}
            else Offer(key,val,support+" — "+val,direct||key=="region"&&repeatedContact);
        }
        var gameRows=editorial.GetProperty("games").EnumerateArray().ToArray();
        var primary=S(editorial,"primarySystemCandidate");
        var relevant=gameRows; // Phase 3 already filters unsupported incidental matches; retain genuine cross-platform coverage.
        bool EditorialCoverage(JsonElement g)=>g.GetProperty("featured").GetBoolean()&&(A(g,"kinds").Any(k=>k is "Review" or "Preview" or "Feature" or "Gameplay assistance" or "Interview")||g.GetProperty("contents").GetBoolean()&&A(g,"kinds").Contains("Contents reference")&&g.GetProperty("evidence").EnumerateArray().Any(e=>!editorial.GetProperty("contentsPages").EnumerateArray().Any(p=>p.GetInt32()==e.GetProperty("page").GetInt32())&&S(e,"kind")=="Reference"));
        var featured=relevant.Where(EditorialCoverage).Select(g=>S(g,"title")).Where(Meaningful).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var mentioned=relevant.Where(g=>!featured.Contains(S(g,"title"),StringComparer.OrdinalIgnoreCase)&&A(g,"kinds").Any(k=>k is not "Possible advertising" and not "Catalog listing")).Select(g=>S(g,"title")).Where(Meaningful).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if(primary.Length>0)
        {
            if(!values.ContainsKey("primarySystem")&&!conflicting.Contains("primarySystem"))Offer("primarySystem",primary,"Platform inferred from unambiguous editorial game titles",false);
            if(!values.ContainsKey("platformFocus")&&!conflicting.Contains("platformFocus"))Offer("platformFocus",primary,"Platform inferred from editorial game titles",editorial.GetProperty("platformSignals").EnumerateArray().Any(p=>S(p,"platform")==primary&&p.GetProperty("featuredTitles").GetInt32()>=3));
            Offer("featuredPlatforms",new[]{primary}.Concat(relevant.Where(EditorialCoverage).Where(g=>A(g,"platforms").Length==1).SelectMany(g=>A(g,"platforms"))).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),"Dominant and other unambiguous editorial platforms");
        }
        else
        {
            var platforms=relevant.Where(EditorialCoverage).SelectMany(g=>A(g,"platforms").Length==1?A(g,"platforms"):Array.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            Offer("featuredPlatforms",platforms,"Unambiguous catalog platforms from editorial coverage");
        }
        Offer("featuredGames",featured,"Indexed editorial coverage; excludes cover-only, catalog-only and advertising-only matches");
        Offer("mentionedGames",mentioned,"Indexed non-advertising references; catalog-only matches retained in report");
        var covers=editorial.GetProperty("coverCandidates").EnumerateArray().ToArray();
        // A text mention alone does not establish the main cover story. Do not promote an unverified contents-only candidate.
        if(covers.Length>0)
        {
            var best=covers[0];var title=S(best,"title");
            var verifiedText=covers.Length==1&&S(best,"source")=="Cover text"&&featured.Contains(title,StringComparer.OrdinalIgnoreCase)&&relevant.Any(g=>S(g,"title")==title&&g.GetProperty("contents").GetBoolean()&&A(g,"kinds").Any(k=>k is "Review" or "Preview" or "Feature"));
            if(verifiedText)Offer("coverSubject",title,"Single cover-text candidate with explicit editorial contents support",false);
            else held.Add("coverSubject: primary cover not verified — "+string.Join(", ",covers.Select(g=>S(g,"title"))));
        }
        var kinds=relevant.Where(EditorialCoverage).SelectMany(g=>A(g,"kinds")).Where(k=>k is "Review" or "Preview" or "Feature" or "Gameplay assistance" or "Interview").Concat(editorial.GetProperty("headings").EnumerateArray().Where(h=>S(h,"source")=="Contents candidate").Select(h=>S(h,"kind"))).Where(Meaningful).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x=>x).ToArray();
        var departments=editorial.GetProperty("headings").EnumerateArray().Where(h=>S(h,"source")=="Contents candidate"&&(S(h,"kind") is "Reader letters" or "Technical department" or "Competition" or "High scores" or "Editorial")).Select(h=>Regex.Replace(S(h,"title"),@"^NEW!\s*|[_—–]+\s*$","",RegexOptions.IgnoreCase).Trim()).Where(t=>t.Length is >=5 and <=65&&!Regex.IsMatch(t,@"[.!?]$")&&EditorialTokens(t).Length<=9).Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToArray();
        Offer("specialFeatures",kinds.Concat(departments).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),"Editorial coverage and contents section types");
        if(featured.Length>0)
        {
            string publicationName=values.TryGetValue("magazineTitle",out var titleValue)?titleValue.ToString()??item.MagazineTitle:item.MagazineTitle;
            if(!Meaningful(publicationName))publicationName=item.Title;
            string issue=values.TryGetValue("issueNumber",out var issueValue)?issueValue.ToString()??"":item.IssueNumber;
            string date=values.TryGetValue("coverDate",out var dateValue)?dateValue.ToString()??"":item.CoverDate;
            string JoinNatural(IEnumerable<string> list){var a=list.ToArray();return a.Length<=1?string.Join("",a):string.Join(", ",a.Take(a.Length-1))+(a.Length>2?", and ":" and ")+a[^1];}
            var main=relevant.Where(EditorialCoverage).Where(g=>g.GetProperty("contents").GetBoolean()||A(g,"kinds").Contains("Review")||A(g,"kinds").Contains("Preview")).OrderByDescending(g=>A(g,"kinds").Contains("Review")).Select(g=>S(g,"title")).Distinct(StringComparer.OrdinalIgnoreCase).Take(6).ToArray();
            var other=featured.Except(main,StringComparer.OrdinalIgnoreCase).Take(6).ToArray();
            var summary=(Meaningful(date)?"The "+date+" issue of ":"This issue of ")+publicationName+(Meaningful(issue)?" (issue "+issue+")":"")+(primary.Length>0?" focuses on the "+primary+".":" features video game coverage.");
            if(main.Length>0)summary+=" Featured coverage includes "+JoinNatural(main)+".";
            if(other.Length>0)summary+=(main.Length>0?" Additional game coverage includes ":" Game coverage includes ")+JoinNatural(other)+".";
            if(departments.Length>0)summary+=" Regular sections include "+JoinNatural(departments)+".";
            else if(kinds.Length>0)summary+=" Contents include "+JoinNatural(kinds.Select(k=>k.ToLowerInvariant()))+".";
            Offer("summary",summary,"Deterministic issue summary from indexed editorial evidence");
        }
        if(Fingerprint(item)!=fp)throw new InvalidOperationException("Source changed during cataloging. Re-index first.");
        // Enforce alias locks too, using the existing metadata store filter. Report any removed change.
        var update=ItemMetadataJsonReader.Read(JsonSerializer.SerializeToElement(values,Json));
        update=MetadataLockHelper.FilterLockedFields(update,item.MetadataLocks);
        var filtered=JsonSerializer.SerializeToElement(update,Json);
        foreach(var c in changes.Where(c=>!filtered.TryGetProperty(c.Field,out var v)||v.ValueKind==JsonValueKind.Null).ToArray()){changes.Remove(c);held.Add(c.Field+": protected by field alias lock");}
        if(changes.Count>0&&!manual)update=update with{MetadataSource="Magazine document index v8.0 Phase 4.1"};
        return new CatalogPlan("v8.0-phase4.2",fp,update,changes,held,publication,editorial,Math.Round(clock.Elapsed.TotalSeconds,2));
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
        public object? Report { get; set; }
    }
    private static object Snapshot(IndexJob j)
    {
        var elapsed = Math.Max(.01, ((j.Finished ?? DateTimeOffset.UtcNow)-j.Started).TotalSeconds);
        return new { jobId=j.Id, state=j.State, message=j.Message, totalPages=j.Total, indexed=j.Indexed,
            processed=j.Processed, target=j.Target, force=j.Force, catalog=j.Catalog, workers=j.Workers,
            seconds=Math.Round(elapsed,1), pagesPerMinute=Math.Round(j.Processed*60/elapsed,1),
            remainingSeconds=j.Processed>0&&j.State=="running"? (double?)Math.Round(Math.Max(0,j.Target-j.Processed)*elapsed/j.Processed):null,
            active=j.Active.OrderBy(p=>p.Key).Select(p=>new{page=p.Key,stage=p.Value}).ToArray(),
            failed=j.Failed.OrderBy(p=>p.Key).Select(p=>new{page=p.Key,error=p.Value}).ToArray(), report=j.Report };
    }
    public static object? JobStatus(string id)
    {
        lock(JobLock) return Jobs.TryGetValue(id,out var j)?Snapshot(j):null;
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
                                    await IndexPageAsync(root,item,page,fp,force,ct,entries is null?null:entries[page-1],Stage);
                                    lock(JobLock){if(done.Add(page))j.Indexed++;j.Processed++;j.Failed.Remove(page);}
                                    break;
                                }
                                catch(Exception ex) when(ex is not OperationCanceledException)
                                {
                                    logger.LogWarning(ex,"Document index {Job} page {Page} attempt {Attempt} failed",j.Id,page,attempt);
                                    if(attempt==2){lock(JobLock){j.Failed[page]=ex.Message;j.Processed++;}}
                                    else {Stage("Retrying after extraction error");await Task.Delay(750,ct);}
                                }
                            }
                        }
                        finally{lock(JobLock)j.Active.Remove(page);}
                    });
                    token.ThrowIfCancellationRequested();
                    bool complete;lock(JobLock)complete=j.Failed.Count==0&&j.Indexed==j.Total;
                    if(Fingerprint(item)!=fp)throw new InvalidOperationException("Source changed during indexing; cataloging cancelled.");
                    if(complete&&catalog&&saveCatalog is not null)
                    {
                        lock(JobLock)j.Message="Interpreting and saving catalog metadata";
                        var report=await saveCatalog(token);lock(JobLock)j.Report=report;
                    }
                    lock(JobLock){j.State=complete?"complete":"incomplete";j.Message=complete?(catalog?"Cataloging complete":"Indexing complete"):"Failed pages retained in report. Resume to retry; cataloging not started.";}
                }
                catch(OperationCanceledException){lock(JobLock){j.State="stopped";j.Message="Stopped; completed page records retained. Check history if catalog saving had begun.";}}
                catch(Exception ex){logger.LogError(ex,"Document index job {Job} failed",j.Id);lock(JobLock){j.State="failed";j.Message=ex.Message;}}
                finally{lock(JobLock){j.Active.Clear();j.Finished=DateTimeOffset.UtcNow;}lease.Dispose();}
            });
            return Snapshot(j);
        }
    }
    // Use the same application assembly as a short-lived archive-only subprocess.
    // A blocked archive read is killed by RunProcess's timeout, not abandoned inside the web process.
    private static async Task<string> RunArchiveWorker(string mode,string path,string? key,string? output,CancellationToken token)
    {
        var executable=Environment.ProcessPath??throw new InvalidOperationException("Cannot locate archive worker executable.");
        var args=new List<string>();
        if(Path.GetFileNameWithoutExtension(executable).Equals("dotnet",StringComparison.OrdinalIgnoreCase))
            args.Add(typeof(GuidevaultDocumentIndex).Assembly.Location);
        args.Add("--gv-index-archive-worker");args.Add(mode);args.Add(path);
        if(key is not null)args.Add(key);if(output is not null)args.Add(output);
        return await Run(executable,args.ToArray(),token,60);
    }
    public static async Task RunArchiveWorkerCommand(string[] args)
    {
        if(args.Length==3&&args[1]=="list")
        {
            Console.Write(JsonSerializer.Serialize(ArchiveReader.GetImageEntryKeys(args[2]),Json));return;
        }
        if(args.Length!=5||args[1]!="extract")throw new InvalidOperationException("Invalid archive worker arguments.");
        var image=await ArchiveReader.GetImagePageByKeyAsync(args[2],args[3],40_000_000);
        if(image is null)throw new InvalidOperationException("Page image unavailable or unsupported.");
        await File.WriteAllBytesAsync(args[4],image.Value.Bytes);
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
