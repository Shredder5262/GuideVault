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
    public static async Task<object> InitializeAsync(string root, LibraryItem item, CancellationToken token)
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
            else total=await ArchiveReader.GetImagePageCountAsync(item.Path);
            if(total<=0 || total>10000) throw new InvalidOperationException("No supported pages found, or document exceeds 10,000 pages.");
            if(Fingerprint(item)!=fp) throw new InvalidOperationException("Source changed during indexing. Restart indexing.");
            Directory.CreateDirectory(dir);
            await Write(Path.Combine(dir,"manifest.json"),new Manifest(fp,total,Revision),token);
        }
        return await StatusAsync(root,item,token);
    }
    public static async Task<PageRecord> IndexPageAsync(string root, LibraryItem item, int page, string? expected, bool force, CancellationToken token)
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
                try { var bbox=await Run("pdftotext",["-f",n,"-l",n,"-bbox-layout",item.Path,"-"],token); var candidate=ParsePdf(bbox); if(Usable(candidate)) extracted=candidate; }
                catch(Exception ex) when(ex is not OperationCanceledException) { /* fall back to raster OCR */ }
            }
            if(extracted is null)
            {
                string input;
                if(IsPdf(item))
                {
                    var prefix=Path.Combine(temp,"page"); await Run("pdftoppm",["-f",n,"-l",n,"-scale-to",page==1?"3000":"2200","-singlefile","-png",item.Path,prefix],token); input=prefix+".png";
                }
                else
                {
                    var image=await ArchiveReader.GetImagePageAsync(item.Path,page-1);
                    if(image is null || image.Value.Bytes.Length>40_000_000) throw new InvalidOperationException("Page image unavailable or exceeds 40 MB.");
                    input=Path.Combine(temp,"page.img"); await File.WriteAllBytesAsync(input,image.Value.Bytes,token);
                }
                var tsv=await Run("tesseract",[input,"stdout","-l","eng","--psm",page==1?"11":"3","tsv"],token); extracted=ParseTsv(tsv);
            }
            if(extracted.Width<=0 || extracted.Height<=0) throw new InvalidOperationException("Extraction returned invalid page geometry.");
            var text=string.Join("\n",extracted.Lines.Select(l=>l.Text));
            var record=new PageRecord(page,extracted.Source,text,extracted.Width,extracted.Height,extracted.Units,extracted.Lines,Math.Round(timer.Elapsed.TotalSeconds,2),Usable(extracted)?null:"Little readable text; this may be an image-only page or poor recognition.",Revision);
            token.ThrowIfCancellationRequested(); if(Fingerprint(item)!=fp) throw new InvalidOperationException("Source changed during extraction. Page was not saved.");
            await Write(PagePath(dir,page),record,token); return record;
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
            ("writer","Editor",item.Writer,false), ("region","Region",item.Region,true), ("languageTag","Language",item.LanguageTag,true),
            ("primarySystem","Primary system",item.PrimarySystem,true), ("platformFocus","Platform focus",item.PlatformFocus,true),
            ("magazineCategory","Magazine category",item.MagazineCategory,true), ("barcodeUpcIssn","ISSN / barcode",item.BarcodeUpcIssn,false),
            ("coverPrice","Cover price (index only)","",false), ("frequency","Publication frequency (index only)","",false),
            ("pageCount","Digital page count",item.PageCount>0?item.PageCount.ToString(CultureInfo.InvariantCulture):"",false)
        };
        var evidence = new List<PublicationEvidence>(); int indexed=0;
        var openingPages = new List<object>();
        var languageWords = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        var platformPages = new Dictionary<string,HashSet<int>>(StringComparer.OrdinalIgnoreCase);
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
                    Match("coverPrice",@"(?:^|\s)((?:US\s*\$|U\.S\.A\.\s*\$|USA\s*\$|CAN(?:ADA)?\s*\$|UK\s*£|£|\$|€)\s*\d{1,3}[.,]\d{2})(?!\d)","Cover price candidate");
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
                    // Copyright holder is not necessarily publisher; preserve as a separate lower-confidence candidate.
                    var rights=Regex.Match(text,@"(?:©|\(c\)|\bcopyright\b)\s*(?:(?:19|20)\d{2}\s*[,–-]?\s*)?([\p{L}][\p{L}\p{N} &'’.()-]{2,80}?)(?=\s+(?:all rights|copyright|ISSN|printed)\b|[,;]|$)",ci);
                    if(rights.Success && (masthead || titleLine) && Regex.IsMatch(rights.Groups[1].Value,@"\b(?:inc\.?|ltd\.?|limited|corporation|corp\.?|company|publishing|publications|press|media|of America)\b",ci))
                        Add("publisher",rights.Groups[1].Value.TrimEnd('.'),page,"Copyright holder candidate (not proof of publisher)",text);
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
        var report=fields.Select(f=>{
            var candidates=evidence.Where(e=>e.Field==f.Key).GroupBy(e=>EvidenceKey(f.Key,e.Value)).Select(g=>new {value=g.First().Value,evidence=g.OrderBy(e=>e.Source.Contains("candidate",StringComparison.OrdinalIgnoreCase)||e.Source.Contains("inference",StringComparison.OrdinalIgnoreCase)?1:0).Take(8).ToArray(),support=g.All(e=>e.Source.Contains("candidate",StringComparison.OrdinalIgnoreCase)||e.Source.Contains("inference",StringComparison.OrdinalIgnoreCase))?"Indirect":"Direct or reviewed"}).ToArray();
            var status=candidates.Length>1?"Conflicting evidence":candidates.Length==0?"No indexed evidence":!Meaningful(f.Current)?"Candidate":EvidenceKey(f.Key,f.Current)==EvidenceKey(f.Key,candidates[0].value)?"Matches current":"Differs from current";
            return new {field=f.Key,label=f.Label,current=f.Current,locked=MetadataLockHelper.IsLocked(item.MetadataLocks,f.Key),status,candidates};
        }).ToArray();
        return new {revision="v8.0-phase2.2",indexedPages=indexed,totalPages=manifest.TotalPages,relatedIssues=peers.Length,seconds=Math.Round(watch.Elapsed.TotalSeconds,2),fields=report,openingPages,platformSignals};
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
    private static async Task<string> Run(string executable,string[] args,CancellationToken token)
    {
        using var p=new Process{StartInfo=new ProcessStartInfo(executable){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true}};
        foreach(var a in args)p.StartInfo.ArgumentList.Add(a);p.StartInfo.Environment["OMP_THREAD_LIMIT"]="2";p.StartInfo.Environment["LC_ALL"]="C";
        p.Start(); using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(90));
        var stdout=p.StandardOutput.ReadToEndAsync(timeout.Token);var stderr=p.StandardError.ReadToEndAsync(timeout.Token);
        try { await p.WaitForExitAsync(timeout.Token);var output=await stdout;var error=await stderr;if(p.ExitCode!=0)throw new InvalidOperationException($"{executable} failed ({p.ExitCode}).");return output; }
        catch(OperationCanceledException){if(token.IsCancellationRequested)throw;throw new InvalidOperationException($"{executable} timed out.");}
        finally{try{if(!p.HasExited){p.Kill(true);await p.WaitForExitAsync();}}catch{}}
    }
}
