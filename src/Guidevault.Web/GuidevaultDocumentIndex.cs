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
