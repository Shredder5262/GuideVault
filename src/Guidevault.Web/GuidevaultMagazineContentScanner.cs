using System.Diagnostics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

// Read-only magazine OCR and conservative metadata suggestions. No source or metadata writes.
public static class GuidevaultMagazineContentScanner
{
    public sealed record Suggestion(string Field, string Value, int Page, string Evidence, string Confidence);
    public sealed record ScanResult(int TotalPages, int Start, int Scanned, int ReadablePages, List<Suggestion> Suggestions, List<string> Warnings)
    {
        public string ScannerRevision => "v7.1.6";
        public int CachedPages { get; init; }
    }
    private static readonly SemaphoreSlim Gate = new(1, 1);
    // Process-local bounded OCR cache, guarded by Gate. No library files are written.
    private static readonly Dictionary<(string Path,long Size,long Modified,int Page),string> OcrCache = new();
    private static readonly Queue<(string Path,long Size,long Modified,int Page)> OcrOrder = new();
    private static long OcrCharacters;
    private const long MaxOcrCharacters = 16_000_000;
    private static void RememberOcr((string Path,long Size,long Modified,int Page) key,string value)
    {
        if (value.Length > MaxOcrCharacters || OcrCache.ContainsKey(key)) return;
        while (OcrOrder.Count > 0 && (OcrCharacters + value.Length > MaxOcrCharacters || OcrOrder.Count >= 512))
        {
            var expired=OcrOrder.Dequeue();
            if(OcrCache.Remove(expired,out var previous)) OcrCharacters-=previous.Length;
        }
        OcrCache[key]=value; OcrOrder.Enqueue(key); OcrCharacters+=value.Length;
    }
    private static readonly Regex Words = new(@"[\p{L}\p{N}]+", RegexOptions.Compiled);
    private static readonly Regex YearPattern = new(@"\b(?:19|20)\d{2}\b", RegexOptions.Compiled);
    private const string Months = "January|February|March|April|May|June|July|August|September|October|November|December|Jan|Feb|Mar|Apr|Jun|Jul|Aug|Sep|Sept|Oct|Nov|Dec";
    private static readonly (string Pattern, string Name)[] Platforms = [
        (@"\b(?:NES|(?<!Super )Nintendo Entertainment System)\b", "Nintendo Entertainment System"),
        (@"\b(?:SNES|Super Nintendo(?: Entertainment System)?)\b", "Super Nintendo Entertainment System"),
        (@"\b(?:Nintendo 64|N64)\b", "Nintendo 64"), (@"\bGame Boy Advance\b", "Nintendo Game Boy Advance"),
        (@"\bGame Boy Color\b", "Nintendo Game Boy Color"), (@"\bGame Boy\b(?!\s+(?:Advance|Color))", "Nintendo Game Boy"),
        (@"\b(?:Nintendo GameCube|GameCube)\b", "Nintendo GameCube"), (@"\bNintendo DS\b", "Nintendo DS"), (@"\bNintendo 3DS\b", "Nintendo 3DS"),
        (@"\bWii U\b", "Nintendo Wii U"), (@"\bWii\b(?!\s+U)", "Nintendo Wii"), (@"\bNintendo Switch\b", "Nintendo Switch"),
        (@"\b(?:Sega Genesis|Mega Drive|Genesis)\b", "Sega Genesis"), (@"\bSega Saturn\b", "Sega Saturn"), (@"\b(?:Sega Dreamcast|Dreamcast)\b", "Sega Dreamcast"),
        (@"\b(?:Sega CD|Mega[- ]CD)\b", "Sega CD"), (@"\b(?:Sega Game Gear|Game Gear)\b", "Sega Game Gear"), (@"\bSega Master System\b", "Sega Master System"),
        (@"\b(?:PlayStation 2|PS2)\b", "Sony PlayStation 2"), (@"\b(?:PlayStation 3|PS3)\b", "Sony PlayStation 3"), (@"\b(?:PlayStation 4|PS4)\b", "Sony PlayStation 4"),
        (@"\b(?:PlayStation 5|PS5)\b", "Sony PlayStation 5"), (@"\b(?:PlayStation Portable|PSP)\b", "Sony PSP"), (@"\b(?:PlayStation Vita|PS Vita)\b", "Sony PlayStation Vita"),
        (@"\b(?:PS1|PSX|PlayStation)\b(?!\s*(?:[2345]|Portable|Vita|Network|Store|Plus)\b)", "Sony PlayStation"),
        (@"\bXbox 360\b", "Microsoft Xbox 360"), (@"\bXbox One\b", "Microsoft Xbox One"), (@"\bXbox Series [XS]\b", "Microsoft Xbox Series X/S"),
        (@"\bXbox\b(?!\s+(?:360|One|Series|Live|Game Pass))", "Microsoft Xbox"), (@"\b(?:MS-DOS|DOS PC)\b", "MS-DOS"), (@"\b(?:Windows PC|PC Windows|PC CD-ROM)\b", "Windows"),
        (@"\b(?:TurboGrafx|PC Engine)[- ]?CD(?:-ROM)?\b", "NEC TurboGrafx-CD"), (@"\b(?:TurboGrafx(?:[- ]?16)?|TurboGratx|PC Engine)\b(?![- ]?CD\b)", "NEC TurboGrafx-16"), (@"\b(?:Atari )?Jaguar\b", "Atari Jaguar"), (@"\b(?:Sega )?32X\b", "Sega 32X"), (@"\b(?:Arcade)\b", "Arcade"), (@"\b3DO\b", "3DO Interactive Multiplayer")];

    public static async Task<ScanResult> ScanAsync(LibraryItem item, IEnumerable<GuidevaultLaunchBoxGame> games, int start, int count, CancellationToken token, bool vision = false, string? engine = null)
    {
        if (!await Gate.WaitAsync(0, token)) throw new InvalidOperationException("Another magazine scan is running. Retry after it finishes.");
        string temp = Path.Combine(Path.GetTempPath(), "guidevault-magscan-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(temp);
            bool pdf = string.Equals(item.Format, "PDF", StringComparison.OrdinalIgnoreCase);
            int total = pdf ? ParsePdfPages(await Run("pdfinfo", [item.Path], token)) : await ArchiveReader.GetImagePageCountAsync(item.Path);
            if (total <= 0) throw new InvalidOperationException("No readable pages were found.");
            start = Math.Clamp(start, 0, total); count = Math.Clamp(count, 1, 3);
            var result = new ScanResult(total, start, 0, 0, new(), new());
            if (start == 0)
            {
                result.Suggestions.Add(new("pageCount", total.ToString(), 0, "Document reader page/image count; printed page numbering or double-page scans may differ.", "Measured"));
                if (!pdf) await AddArchiveMetadata(item, result.Suggestions);
            }
            var catalog = games.ToArray();
            var year = GetYear(item.CoverDate + " " + item.PublicationDate + " " + item.Year);
            var dictionary = BuildDictionary(catalog, year);
            var focus = new[] { item.PrimarySystem, item.PlatformFocus }.Where(v => !string.IsNullOrWhiteSpace(v)).ToArray();
            var local = catalog.Where(g => focus.Any(v => Normalize(v) == Normalize(g.Platform))).ToArray();
            var coverDictionary = local.Length > 0 ? BuildDictionary(local, year) : dictionary;
            var sourceInfo = new FileInfo(item.Path);
            bool fullVision = engine is "full" or "vision";
            bool visionOnly = engine == "vision";
            bool balanced = engine == "balanced";
            var refinePages = new List<int>();
            // A fixed early-page window bounds vision cost across independent HTTP chunks.
            // Cover always receives deep recognition; at most three interior pages receive one pass each.
            if (balanced && start == 0) refinePages.Add(0);
            if (fullVision || balanced)
            {
                const string script = "/opt/guidevault-vision/cover_ocr.py";
                const string protocol = "guidevault-vision-deep-v7.1.3";
                if (!File.Exists(script) || !(await File.ReadAllTextAsync(script, token)).Contains(protocol, StringComparison.Ordinal))
                    throw new InvalidOperationException("Incompatible vision runtime: rebuild and deploy the matching v7.1.3 Docker image. No magazine recognition was started.");
                var verified = await Run("/opt/guidevault-vision/bin/python", [script, "--self-test"], token);
                if (!string.Equals(verified.Trim(), protocol, StringComparison.Ordinal))
                    throw new InvalidOperationException("Vision runtime compatibility self-test failed. Rebuild and deploy the matching Docker image.");
            }
            for (int i = start; !visionOnly && i < Math.Min(start + count, total); i++)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    var cacheKey=(sourceInfo.FullName,sourceInfo.Length,sourceInfo.LastWriteTimeUtc.Ticks,i);
                    if (!OcrCache.TryGetValue(cacheKey,out var tsv))
                    {
                    string input;
                    if (pdf)
                    {
                        var page = (i + 1).ToString();
                        var prefix = Path.Combine(temp, "page");
                        await Run("pdftoppm", ["-f", page, "-l", page, "-scale-to", i == 0 ? "3200" : "2400", "-singlefile", "-png", item.Path, prefix], token);
                        input = prefix + ".png";
                    }
                    else
                    {
                        var image = await ArchiveReader.GetImagePageAsync(item.Path, i);
                        if (image is null) throw new InvalidOperationException("Page image unavailable.");
                        if (image.Value.Bytes.Length > 40_000_000) throw new InvalidOperationException("Page exceeds the 40 MB OCR limit.");
                        input = Path.Combine(temp, "page.img");
                        await File.WriteAllBytesAsync(input, image.Value.Bytes, token);
                    }
                    tsv = await Run("tesseract", [input, "stdout", "-l", "eng", "--psm", i == 0 ? "11" : "3", "tsv"], token);
                    bool completeOcr = true;
                    if (i == 0)
                    {
                        foreach (var mode in new[] { "3", "6", "12" })
                        {
                            try { tsv += "\n\f\n" + await Run("tesseract", [input, "stdout", "-l", "eng", "--psm", mode, "tsv"], token); }
                            catch (Exception ex) when (ex is not OperationCanceledException) { completeOcr = false; result.Warnings.Add($"Cover OCR mode {mode}: {ex.Message}"); }
                        }
                    }
                    if(i==0)
                    {
                        try
                        {
                            using var cover=await Image.LoadAsync(input,token);
                            // Overlapping cover regions, independent of publication or game identity.
                            foreach(var region in new[]{(X:0.0,Y:0.55,W:1.0,H:0.45),(X:0.30,Y:0.55,W:0.70,H:0.45),(X:0.0,Y:0.15,W:0.70,H:0.55),(X:0.35,Y:0.15,W:0.65,H:0.55)})
                            {
                                int x=(int)(cover.Width*region.X),y=(int)(cover.Height*region.Y);
                                int w=Math.Max(1,Math.Min(cover.Width-x,(int)(cover.Width*region.W))),h=Math.Max(1,Math.Min(cover.Height-y,(int)(cover.Height*region.H)));
                                foreach(int variant in new[]{0,1,2})
                                {
                                    using var crop=cover.Clone(c=>c.Crop(new Rectangle(x,y,w,h)).Grayscale());
                                    if(variant==1)crop.Mutate(c=>c.Contrast(1.5f).Invert());
                                    if(variant==2)crop.Mutate(c=>c.BinaryThreshold(0.65f).Invert());
                                    var cropPath=Path.Combine(temp,"cover-region.png");
                                    await crop.SaveAsPngAsync(cropPath,token);
                                    var regional=await Run("tesseract",[cropPath,"stdout","-l","eng","--psm","11","tsv"],token);
                                    tsv += "\u001d" + RemapCoverTsv(regional,x,y,cover.Width,cover.Height);
                                }
                            }
                        }
                        catch(Exception ex) when(ex is not OperationCanceledException){completeOcr=false;result.Warnings.Add($"Cover region OCR: {ex.Message}");}
                    }
                    if(i<4)
                    {
                        try
                        {
                            using var image=await Image.LoadAsync(input,token);
                            int w=Math.Max(1,image.Width/8);
                            foreach(int x in new[]{0,image.Width-w})
                            foreach(int angle in new[]{90,270})
                            {
                                using var margin=image.Clone(c=>c.Crop(new Rectangle(x,0,w,image.Height)).Rotate(angle));
                                var marginPath=Path.Combine(temp,"margin.png");
                                await margin.SaveAsPngAsync(marginPath,token);
                                tsv += "\u001e" + await Run("tesseract",[marginPath,"stdout","-l","eng","--psm","11"],token);
                            }
                        }
                        catch(Exception ex) when(ex is not OperationCanceledException){completeOcr=false;result.Warnings.Add($"Page {i+1} margin OCR: {ex.Message}");}
                    }
                    if (completeOcr && Words.Matches(tsv).Count > 30) RememberOcr(cacheKey,tsv);
                    }
                    else result = result with { CachedPages = result.CachedPages + 1 };
                    var sections=tsv.Split('\u001e');
                    var coverSections=sections[0].Split('\u001d');
                    var passes = coverSections[0].Split('\f', StringSplitOptions.RemoveEmptyEntries).Select(x => ParseLayout(x.TrimStart('\r','\n'))).ToArray();
                    var layout = passes.OrderByDescending(p => p.Lines.Sum(l => Words.Matches(l.Text).Count * Math.Max(0,l.Confidence))).First();
                    string text = string.Join("\n", layout.Lines.Select(l => l.Text));
                    if (pdf && Words.Matches(text).Count < 12)
                    {
                        var page = (i + 1).ToString();
                        var fallback = await Run("pdftotext", ["-f", page, "-l", page, "-layout", item.Path, "-"], token);
                        if (Words.Matches(fallback).Count > Words.Matches(text).Count)
                        {
                            text = fallback;
                            layout = new PageLayout(0, 0, text.Split('\n').Where(x => !string.IsNullOrWhiteSpace(x)).Select((x,j) => new LayoutLine(x.Trim(), j, 0, 0, 0, 0, 0, 0)).ToList());
                            result.Warnings.Add($"Page {i+1}: embedded text fallback; no reliable geometry.");
                        }
                    }
                    if (i == 0) AnalyzeCover(item, coverDictionary, passes.Concat(coverSections.Skip(1).Select(ParseLayout)).ToArray(), result.Suggestions);
                    if (Words.Matches(text).Count >= 3)
                    {
                        var excluded = ExcludedPageReason(text, i + 1, total);
                        if (balanced && i > 0 && i <= 3 && excluded.Length == 0)
                        {
                            int words = Words.Matches(text).Count;
                            var confidence = layout.Lines.Where(l => l.Confidence >= 0).Select(l => l.Confidence).DefaultIfEmpty(0).Average();
                            bool contents = Regex.IsMatch(text, @"\b(?:table of contents|contents|in this issue|inside this issue)\b", RegexOptions.IgnoreCase);
                            if (words < 60 || confidence < 65 || contents) refinePages.Add(i);
                        }
                        if (excluded.Length > 0)
                        {
                            result.Warnings.Add($"Page {i+1}: {excluded}; excluded from current-issue metadata evidence.");
                            foreach (var line in layout.Lines)
                                foreach (var hit in FindGames(dictionary,line.Text,true,false,true).Where(h=>!h.Fuzzy))
                                    if (!result.Suggestions.Any(s=>s.Field=="excludedGame"&&s.Page==i+1&&ListingKey(s.Value)==ListingKey(hit.Game)))
                                        result.Suggestions.Add(new("excludedGame",hit.Game,i+1,line.Text,"Excluded page — not current-issue coverage"));
                        }
                        else
                        {
                            AnalyzePage(item, dictionary, text, layout, i + 1, result.Suggestions);
                            if(i<4) foreach(var source in passes.Select(p=>string.Join("\n",p.Lines.Select(l=>l.Text))).Concat(sections.Skip(1)))
                                AddPrintedPeriod(item,source,i+1,result.Suggestions);
                        }
                        result = result with { ReadablePages = result.ReadablePages + 1 };
                    }
                    else
                    {
                        result.Warnings.Add($"Page {i + 1}: little or no readable text.");
                        if (balanced && i > 0 && i <= 3) refinePages.Add(i);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    result.Warnings.Add($"Page {i + 1}: {ex.Message}");
                    if (balanced && i > 0 && i <= 3) refinePages.Add(i);
                }
                result = result with { Scanned = result.Scanned + 1 };
            }
            if (fullVision || (balanced && refinePages.Count > 0))
            {
                var batch = new List<(int Index,string? Json,bool Cached)>();
                if (fullVision)
                    batch = await ReadVisionBatch(item, sourceInfo, pdf, temp, start, Math.Min(start + count, total), result.Warnings, token);
                else
                {
                    if (refinePages.Contains(0))
                        batch.AddRange(await ReadVisionBatch(item, sourceInfo, pdf, temp, 0, 1, result.Warnings, token));
                    var interior = refinePages.Where(i=>i>0).Distinct().ToArray();
                    if (interior.Length > 0)
                        batch.AddRange(await ReadVisionBatch(item, sourceInfo, pdf, temp, start, Math.Min(start + count, total), result.Warnings, token, false, interior));
                }
                if (fullVision && batch.Count > 0 && batch.All(p => p.Json is null))
                    throw new InvalidOperationException("All vision pages in this batch failed. No metadata suggestions returned for this batch. " + string.Join(" | ", result.Warnings.TakeLast(3)));
                foreach(var (index, json, cached) in batch)
                {
                    token.ThrowIfCancellationRequested();
                    if(visionOnly) result = result with { Scanned = result.Scanned + 1, CachedPages = result.CachedPages + (cached ? 1 : 0) };
                    if(json is null) continue;
                    try
                    {
                        if(index==0) AnalyzeVisionCover(item,coverDictionary,json,result.Suggestions);
                        if(AnalyzeVisionPage(item,index==0?coverDictionary:dictionary,json,index+1,total,result.Suggestions,result.Warnings) && visionOnly)
                            result = result with { ReadablePages = result.ReadablePages + 1 };
                    }
                    catch(Exception ex) when(ex is not OperationCanceledException) { result.Warnings.Add($"Page {index+1}: vision analysis failed: {ex.Message}"); }
                }
            }
            if (vision && !fullVision && !balanced && start == 0)
            {
                try
                {
                    var visionKey=(sourceInfo.FullName,sourceInfo.Length,sourceInfo.LastWriteTimeUtc.Ticks,-1);
                    if(!OcrCache.TryGetValue(visionKey,out var visionJson))
                    {
                        string input;
                        if(pdf)
                        {
                            var prefix=Path.Combine(temp,"vision-cover");
                            await Run("pdftoppm",["-f","1","-l","1","-scale-to","1800","-singlefile","-png",item.Path,prefix],token);
                            input=prefix+".png";
                        }
                        else
                        {
                            var image=await ArchiveReader.GetImagePageAsync(item.Path,0);
                            if(image is null||image.Value.Bytes.Length>40_000_000)throw new InvalidOperationException("Cover unavailable or too large.");
                            input=Path.Combine(temp,"vision-cover.img");
                            await File.WriteAllBytesAsync(input,image.Value.Bytes,token);
                        }
                        visionJson=await Run("/opt/guidevault-vision/bin/python",["/opt/guidevault-vision/cover_ocr.py",input],token,600);
                        AnalyzeVisionCover(item,coverDictionary,visionJson,result.Suggestions);
                        RememberOcr(visionKey,visionJson);
                    }
                    else
                    {
                        result.Suggestions.Add(new("coverDiagnostic","Florence-2",1,"Reused process-local cover result","Vision cache hit"));
                        AnalyzeVisionCover(item,coverDictionary,visionJson,result.Suggestions);
                    }
                }
                catch(Exception ex) when(ex is not OperationCanceledException)
                {
                    result.Warnings.Add($"Experimental vision failed; standard OCR retained: {ex.Message}");
                }
            }
            // Offer canonical replacements for previously saved abbreviations only when
            // the same canonical game is independently detected in this scan chunk.
            var observedGames = result.Suggestions.Where(s => s.Field == "featuredGames" || s.Field == "mentionedGames" || s.Field == "coverSubject").ToArray();
            var storedNames = (item.FeaturedGames ?? Array.Empty<string>()).Concat(item.MentionedGames ?? Array.Empty<string>()).Append(item.CoverSubject ?? "");
            foreach (var stored in storedNames.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!dictionary.TryGetValue(Normalize(stored), out var resolved) || !resolved.Alias || Normalize(stored) == Normalize(resolved.Display)) continue;
                if (resolved.Display.IndexOfAny([',',';','|']) >= 0) continue;
                var evidence = observedGames.FirstOrDefault(s => Normalize(s.Value) == Normalize(resolved.Display) && s.Confidence != "Possible OCR correction");
                if (evidence is not null) result.Suggestions.Add(new("catalogAlias", resolved.Display, evidence.Page, stored, "Catalog alternate name — normalize existing title (review)"));
            }
            foreach(var stored in storedNames.Where(v=>!string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var prefix=Normalize(stored);
                if(!BareFranchise(dictionary,prefix))continue;
                var numbered=observedGames.Where(g=>Normalize(g.Value).StartsWith(prefix+" ",StringComparison.Ordinal)&&SequelNumber(Normalize(g.Value).Split(' ')[prefix.Split(' ').Length])>0).Select(g=>g.Value).Distinct(StringComparer.OrdinalIgnoreCase).Take(6).ToArray();
                if(numbered.Length>0)result.Warnings.Add($"Stored title '{stored}' may be an original game or a series reference; numbered coverage found: {string.Join(", ",numbered)}. Review independently; no automatic replacement or removal.");
            }
            var publicationWords=Normalize((item.MagazineTitle??"").Replace("+"," plus")).Split(' ',StringSplitOptions.RemoveEmptyEntries);
            bool PublicationNoise(string value)
            {
                var k=Normalize(value); if(k.Length==0)return true;
                if(k==Normalize(item.MagazineTitle??""))return true;
                if(k.Contains(' '))return false;
                return k.Length>=4 && publicationWords.Any(w=>w.Length>=5 && (w==k || (k.Length>=5 && w.EndsWith(k,StringComparison.Ordinal) && w.Length-k.Length<=3)));
            }
            result.Suggestions.RemoveAll(s => (s.Field is "featuredGames" or "mentionedGames" or "articleCandidate" or "unresolvedGame") && PublicationNoise(s.Value));
            for(int k=0;k<result.Suggestions.Count;k++)
            {
                var hit=result.Suggestions[k];
                if((hit.Field is "featuredGames" or "mentionedGames" or "articleCandidate") && Regex.IsMatch(Normalize(hit.Value),@"^(?:family games|sega game|video game|video games|game|games)$",RegexOptions.IgnoreCase))
                    result.Suggestions[k]=hit with {Field="unresolvedGame",Confidence="Generic phrase — verify actual game title"};
            }
            result.Suggestions.RemoveAll(s => s.Field == "coverSubject" && !ValidCoverSubject(s.Value,item.MagazineTitle));
            result.Suggestions.RemoveAll(s => s.Field == "publisher" && MissingPublisher(s.Value));
            if(Normalize((item.MagazineTitle??"").Replace("+"," plus"))=="saturn plus")
                for(int k=0;k<result.Suggestions.Count;k++)
                {
                    var p=result.Suggestions[k];
                    if(p.Field=="publisher" && Normalize(p.Value)=="paragon")result.Suggestions[k]=p with { Value="Paragon Publishing", Evidence=p.Evidence+" | Publication-specific publisher alias" };
                }
            if ((MissingPublisher(item.Publisher) || (Normalize((item.MagazineTitle??"").Replace("+"," plus"))=="saturn plus" && Normalize(item.Publisher??"")=="paragon")) && !result.Suggestions.Any(s => s.Field == "publisher"))
            {
                var publication = Normalize((item.MagazineTitle ?? "").Replace("+", " plus"));
                var issueYear = GetYear(item.CoverDate + " " + item.PublicationDate + " " + item.Year);
                // Explicit series identity and publication era; no guesses for similarly named magazines.
                if (publication == "saturn plus" && issueYear >= 1995 && issueYear <= 1997)
                    result.Suggestions.Add(new("publisher", "Paragon Publishing", 0, "Publication reference: UK Games Magazines, Saturn Plus Issue Three (ukgamesmags.com/issues/saturn-plus-issue-three); verify against issue credits.", "Publication reference — verify"));
            }
            if (dictionary.Count == 0) result.Warnings.Add("No usable LaunchBox game names; game coverage suggestions are unavailable.");
            return result;
        }
        finally { try { Directory.Delete(temp, true); } catch { } Gate.Release(); }
    }

    // Cache only extracted regions, never source images. Source identity and pipeline version invalidate stale results.
    private static async Task<List<(int Index,string? Json,bool Cached)>> ReadVisionBatch(LibraryItem item,FileInfo source,bool pdf,string temp,int start,int end,List<string> warnings,CancellationToken token,bool deep=true,int[]? selected=null)
    {
        var output=new List<(int Index,string? Json,bool Cached)>();
        var pending=new List<(int Index,string Input,string Cache)>();
        var cacheRoot=Path.Combine(Environment.GetEnvironmentVariable("GUIDEVAULT_DATA")??"/data","vision-page-cache",deep?"v7.1.2":"v7.1.3-balanced");
        bool cacheEnabled=true;
        try { Directory.CreateDirectory(cacheRoot); } catch(Exception ex) { cacheEnabled=false; warnings.Add("Vision disk cache unavailable: "+ex.Message); }
        for(int i=start;i<end;i++)
        {
            token.ThrowIfCancellationRequested();
            if(selected is not null && !selected.Contains(i)) continue;
            var identity=$"{source.FullName}\n{source.Length}\n{source.LastWriteTimeUtc.Ticks}\n{i}\n{(deep?"florence-base-00921df-adaptive-1800-1024-v712":"florence-base-00921df-overview-1800-768-v713")}\n{Environment.GetEnvironmentVariable("GUIDEVAULT_VISION_RETRY_DEPTH")??"1"}";
            var key=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
            var cachePath=Path.Combine(cacheRoot,key+".json");
            try
            {
                if(cacheEnabled && File.Exists(cachePath) && new FileInfo(cachePath).Length<2_000_000)
                {
                    try
                    {
                        var cached=await File.ReadAllTextAsync(cachePath,token);
                        using var check=System.Text.Json.JsonDocument.Parse(cached);
                        if(check.RootElement.TryGetProperty("regions",out var regions)&&regions.ValueKind==System.Text.Json.JsonValueKind.Array && check.RootElement.TryGetProperty("truncated",out var incompleteCache) && incompleteCache.ValueKind==System.Text.Json.JsonValueKind.False)
                        {output.Add((i,cached,true));continue;}
                    }
                    catch(Exception ex) when(ex is not OperationCanceledException) { warnings.Add($"Page {i+1}: invalid vision cache; rescanning."); }
                }
                string input;
                if(pdf)
                {
                    var prefix=Path.Combine(temp,$"vision-{i}");var page=(i+1).ToString(CultureInfo.InvariantCulture);
                    await Run("pdftoppm",["-f",page,"-l",page,"-scale-to","2400","-singlefile","-png",item.Path,prefix],token);
                    input=prefix+".png";
                }
                else
                {
                    var image=await ArchiveReader.GetImagePageAsync(item.Path,i);
                    if(image is null || image.Value.Bytes.Length>40_000_000)throw new InvalidOperationException("Page unavailable or exceeds 40 MB limit.");
                    input=Path.Combine(temp,$"vision-{i}.img");await File.WriteAllBytesAsync(input,image.Value.Bytes,token);
                }
                pending.Add((i,input,cachePath));
            }
            catch(Exception ex) when(ex is not OperationCanceledException) { warnings.Add($"Page {i+1}: vision input failed: {ex.Message}");output.Add((i,null,false)); }
        }
        if(pending.Count>0)
        {
            try
            {
                var args=new[]{"/opt/guidevault-vision/cover_ocr.py",deep?"--deep-batch":"--batch"}.Concat(pending.Select(p=>p.Input)).ToArray();
                var raw=await Run("/opt/guidevault-vision/bin/python",args,token,2400);
                using var doc=System.Text.Json.JsonDocument.Parse(raw);
                var pages=doc.RootElement.EnumerateArray().ToArray();
                if(pages.Length!=pending.Count)throw new InvalidOperationException("Vision batch result count mismatch.");
                for(int j=0;j<pending.Count;j++)
                {
                    var p=pending[j];var page=pages[j];
                    if(!page.TryGetProperty("regions",out var regions)||regions.ValueKind!=System.Text.Json.JsonValueKind.Array)
                    {warnings.Add($"Page {p.Index+1}: vision failed: {page.GetRawText()}");output.Add((p.Index,null,false));continue;}
                    var json=page.GetRawText();output.Add((p.Index,json,false));
                    if(cacheEnabled && json.Length<2_000_000 && regions.GetArrayLength()>0 && page.TryGetProperty("truncated",out var incomplete) && incomplete.ValueKind==System.Text.Json.JsonValueKind.False)
                    {
                        try { var tmp=p.Cache+".tmp";await File.WriteAllTextAsync(tmp,json,token);File.Move(tmp,p.Cache,true); }
                        catch(Exception ex) when(ex is not OperationCanceledException) { warnings.Add($"Page {p.Index+1}: vision cache write failed: {ex.Message}"); }
                    }
                }
            }
            catch(Exception ex) when(ex is not OperationCanceledException)
            {
                warnings.Add("Vision batch failed: "+ex.Message);
                foreach(var p in pending.Where(p=>!output.Any(o=>o.Index==p.Index)))output.Add((p.Index,null,false));
            }
        }
        return output.OrderBy(p=>p.Index).ToList();
    }

    private static bool AnalyzeVisionPage(LibraryItem item,Dictionary<string,CatalogTitle> dictionary,string json,int page,int total,List<Suggestion> suggestions,List<string> warnings)
    {
        using var doc=System.Text.Json.JsonDocument.Parse(json);var root=doc.RootElement;
        int width=root.GetProperty("width").GetInt32(),height=root.GetProperty("height").GetInt32();
        if(width<=0||height<=0)throw new InvalidOperationException("Invalid vision dimensions.");
        // 101 is an internal eligibility sentinel for legacy geometry heuristics, NOT calibrated model confidence.
        // Geometry() explicitly labels these lines as vision without a percentage.
        var lines=root.GetProperty("regions").EnumerateArray().Take(800).Select(r=>new LayoutLine(r.GetProperty("text").GetString()??"",0,0,r.GetProperty("x").GetInt32(),r.GetProperty("y").GetInt32(),r.GetProperty("width").GetInt32(),r.GetProperty("height").GetInt32(),101)).Where(l=>l.Height>0&&l.Width>0&&!string.IsNullOrWhiteSpace(l.Text)).OrderBy(l=>l.Y).ThenBy(l=>l.X).ToList();
        var text=string.Join("\n",lines.Select(l=>l.Text));
        if(Words.Matches(text).Count<3){warnings.Add($"Page {page}: little or no vision text.");return false;}
        var excluded=ExcludedPageReason(text,page,total);
        if(excluded.Length>0){warnings.Add($"Page {page}: vision {excluded}; excluded from current-issue evidence.");return true;}
        var found=new List<Suggestion>();
        if(root.TryGetProperty("truncated",out var cut) && cut.ValueKind==System.Text.Json.JsonValueKind.True)
            warnings.Add($"Page {page}: vision output reached its limit; coverage may be incomplete. Result not cached.");
        var passes=root.TryGetProperty("passes",out var passCount)?passCount.GetInt32():1;
        var retries=root.TryGetProperty("retries",out var retryCount)?retryCount.GetInt32():0;
        var recovered=root.TryGetProperty("recovered",out var recoveryCount)?recoveryCount.GetInt32():0;
        var unresolved=root.TryGetProperty("unresolved",out var unresolvedCount)?unresolvedCount.GetInt32():0;
        suggestions.Add(new("pageVisionDiagnostic",$"Page {page}",page,$"{lines.Count} regions; {passes} passes; {retries} retry tiles; {recovered} recovered limits; {unresolved} unresolved; {root.GetProperty("seconds").GetDouble():F1}s; peak process RSS {root.GetProperty("peakMb").GetDouble():F0} MiB","Vision page recognition"));
        AnalyzePage(item,dictionary,text,new PageLayout(width,height,lines),page,found);
        // A cover teaser with a printed page reference is evidence of dedicated editorial coverage.
        // Restrict matching to nearby aligned lines; a page number elsewhere is not corroboration.
        if(page==1)
        {
            foreach(var reference in lines.Where(l=>Regex.IsMatch(l.Text,@"\b(?:see\s+)?pages?\s+\d{1,3}(?:\s*[-–—]\s*\d{1,3})?\b",RegexOptions.IgnoreCase)))
            {
                var nearby=lines.Where(l=>l.Y>=reference.Y-height*.13 && l.Y<=reference.Y+reference.Height && Math.Min(l.X+l.Width,reference.X+reference.Width)>Math.Max(l.X,reference.X)).OrderBy(l=>l.Y).ThenBy(l=>l.X).ToArray();
                var joined=string.Join(" ",nearby.Select(l=>l.Text));
                if(!Regex.IsMatch(joined,@"\b(?:review|preview|guide|tips|master|latest|walkthrough|solution|details)\b",RegexOptions.IgnoreCase))continue;
                foreach(var hit in FindGames(dictionary,joined,true,false,true).Where(h=>!h.Fuzzy))
                    if(ValidCoverSubject(hit.Game,item.MagazineTitle) && (Words.Matches(hit.Game).Count>1 || nearby.Any(l=>ListingKey(l.Text)==ListingKey(hit.Game)) || Regex.IsMatch(joined,@"\b(?:latest on|review of|guide to|guide for|tips for|and)\s+"+Regex.Escape(hit.Game)+@"\b",RegexOptions.IgnoreCase)) && !found.Any(x=>x.Field=="featuredGames"&&ListingKey(x.Value)==ListingKey(hit.Game)))
                        found.Add(new("featuredGames",hit.Game,page,joined[..Math.Min(joined.Length,420)],"Cover editorial page reference — verify"));
            }
            foreach(var (pattern,label) in new[]{(@"\b(?:mailbag|reader letters)\b","Reader letters"),(@"\b(?:customer\s+service\s+department|sega spotlight)\b","Customer service spotlight"),(@"\b(?:art contest|reader contest)\b","Reader contests"),(@"\b(?:play guide|gameplay guide|walkthrough|solution|strategy guide)\b","Strategy guides"),(@"\b(?:reviewed|reviews?)\b","Reviews"),(@"\b(?:preview|first look)\b","Previews")})
                if(Regex.IsMatch(text,pattern,RegexOptions.IgnoreCase))found.Add(new("specialFeatures",label,page,Regex.Match(text,pattern,RegexOptions.IgnoreCase).Value,"Cover editorial evidence"));
        }
        AddPrintedPeriod(item,text,page,found);
        if(page==1)
        {
            // Seasonal dates are valid printed issue periods; not actual release dates.
            foreach(Match m in Regex.Matches(text,@"\b(?:Spring|Summer|Autumn|Fall|Winter)[,\s]+(?:19|20)\d{2}\b",RegexOptions.IgnoreCase))
            {
                var date=Regex.Replace(m.Value,@"[,\s]+"," ");
                found.Add(new("coverDate",date,page,m.Value,"Printed publication period"));
                found.Add(new("year",GetYear(date).ToString(CultureInfo.InvariantCulture),page,m.Value,"Printed publication period"));
                // A seasonal dateline followed by #N is an explicit cover issue number.
                var issue=Regex.Match(text,@"\b"+Regex.Escape(m.Value)+@"\s*#\s*(\d{1,3})\b",RegexOptions.IgnoreCase);
                if(issue.Success && int.TryParse(issue.Groups[1].Value,out int issueNumber) && issueNumber>0)
                    found.Add(new("issueNumber",issueNumber.ToString(CultureInfo.InvariantCulture),page,issue.Value,"Explicit cover issue number"));
            }
        }
        foreach(var hit in found)
        {
            // Cover subject ranking is handled by the dedicated vision cover analyzer, not generic text matching.
            if(page==1 && hit.Field=="coverSubject")continue;
            if(hit.Field is "featuredGames" or "mentionedGames" or "articleCandidate" or "unresolvedGame")
            {
                if(!ValidCoverSubject(hit.Value,item.MagazineTitle)||Regex.IsMatch(Normalize(hit.Value),@"^(?:sega game|video game|game|games|challenge|victory|soccer|grand prix|kung fu)$",RegexOptions.IgnoreCase))
                {
                    // Generic words can be real games: retain as manual-review evidence rather than assert coverage.
                    if(!suggestions.Any(s=>s.Field=="unresolvedGame"&&s.Page==page&&ListingKey(s.Value)==ListingKey(hit.Value)))
                        suggestions.Add(hit with {Field="unresolvedGame",Evidence="Florence-2: "+hit.Evidence,Confidence="Ambiguous short or generic title — verify"});
                    continue;
                }
                if(hit.Confidence=="Possible OCR correction") { suggestions.Add(hit with {Field="unresolvedGame",Evidence="Florence-2: "+hit.Evidence,Confidence="Possible recognition correction — verify"});continue; }
            }
            if(!suggestions.Any(s=>s.Field==hit.Field&&s.Page==page&&ListingKey(s.Value)==ListingKey(hit.Value)))
                suggestions.Add(hit with {Evidence="Florence-2 region evidence (confidence unavailable): "+hit.Evidence});
        }
        return true;
    }

    private static void AddPrintedPeriod(LibraryItem item,string text,int page,List<Suggestion> suggestions)
    {
        foreach(var line in text.Split('\n'))
        {
            var pub=Normalize((item.MagazineTitle??"").Replace("+"," plus"));
            var context=Normalize(line.Replace("+"," plus"));
            if(page!=1 && !Regex.IsMatch(line,@"\bmagazine\b",RegexOptions.IgnoreCase) && (pub.Length<3 || !context.Contains(pub,StringComparison.Ordinal)))continue;
            var m=Regex.Match(line,$@"\b(?<a>{Months})\s*[/–-]\s*(?<b>{Months})\s+(?<y>(?:19|20)\d{{2}})\s*[/–-]\s*(?<z>(?:(?:19|20)\d{{2}}|\d{{2}}))\b",RegexOptions.IgnoreCase);
            if(!m.Success)continue;
            int y=int.Parse(m.Groups["y"].Value), z=int.Parse(m.Groups["z"].Value);
            if(z<100)z=y/100*100+z;
            if(z<y)z+=100;
            if(z!=y+1)continue;
            int Month(string v)=>Array.FindIndex(CultureInfo.InvariantCulture.DateTimeFormat.MonthNames,n=>n.Length>0&&n.StartsWith(v,StringComparison.OrdinalIgnoreCase))+1;
            int a=Month(m.Groups["a"].Value), b=Month(m.Groups["b"].Value);
            if(a==0||b==0||a<=b)continue;
            var value=$"{CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(a)} {y} – {CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(b)} {z}";
            foreach(var pair in new[]{(Field:"coverDate",Value:value),(Field:"year",Value:y.ToString(CultureInfo.InvariantCulture))})
                if(!suggestions.Any(s=>s.Field==pair.Field&&s.Value==pair.Value&&s.Confidence=="Printed publication period"))
                    suggestions.Add(new(pair.Field,pair.Value,page,line.Trim(),"Printed publication period"));
        }
    }

    private static async Task AddArchiveMetadata(LibraryItem item, List<Suggestion> suggestions)
    {
        var info = await ArchiveReader.GetComicInfoAsync(item.Path);
        if (info is null) return;
        void Add(string field,string? value,string tag)
        {
            if(!string.IsNullOrWhiteSpace(value) && value.Length<=2000) suggestions.Add(new(field,value.Trim(),0,"Embedded ComicInfo.xml: "+tag+" (archive metadata; verify against print).","Archive metadata"));
        }
        Add("name",info.Title,"Title"); Add("magazineTitle",info.Series,"Series"); Add("publisher",info.Publisher,"Publisher");
        if(string.IsNullOrWhiteSpace(item.IssueNumber)) Add("issueNumber",info.Number,"Number");
        var archiveVolume = RomanOrNumber(info.Volume ?? ""); if (archiveVolume > 0 && archiveVolume < 1000) Add("volume",archiveVolume.ToString(CultureInfo.InvariantCulture),"Volume"); Add("year",info.Year,"Year"); Add("region",info.Country,"Country");
        var language = CanonicalLanguage(info.LanguageTag); if (language.Length > 0) Add("languageTag",language,"Language");
        if(string.IsNullOrWhiteSpace(item.Summary)) Add("summary",info.Summary,"Summary");
        foreach(var tag in (info.Tags ?? "").Split([',',';'],StringSplitOptions.RemoveEmptyEntries)) Add("tags",tag,"Tags");
        if(int.TryParse(info.Year,out int y)&&y>=1900&&y<=2099&&int.TryParse(info.Month,out int m)&&m>=1&&m<=12)
        {
            var date=new DateTime(y,m,1); Add("publicationDate",date.ToString("MMMM yyyy",CultureInfo.InvariantCulture),"Year/Month (archive date, not necessarily cover date)");
        }
    }
    private static int ParsePdfPages(string text) => int.TryParse(Regex.Match(text, @"(?im)^Pages:\s*(\d+)").Groups[1].Value, out int n) ? n : 0;
    private static int GetYear(string value) => int.TryParse(YearPattern.Match(value ?? "").Value, out int n) ? n : 0;
    private static string Normalize(string value) => string.Join(" ", Words.Matches((value ?? "").Normalize(NormalizationForm.FormKC)).Select(m => m.Value.ToLowerInvariant()));
    // Exact catalog names outrank aliases. Unambiguous full-title expansions use the
    // catalog title; unrelated or ambiguous regional aliases retain printed wording for review.
    private sealed record CatalogTitle(string Display, bool Alias);
    private static Dictionary<string, CatalogTitle> BuildDictionary(IEnumerable<GuidevaultLaunchBoxGame> games, int year)
    {
        var exact = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var aliases = new Dictionary<string, List<(string Alias, string Owner)>>(StringComparer.Ordinal);
        foreach (var game in games)
        {
            int release = GetYear(game.ReleaseYear);
            if (year > 0 && release > year + 2) continue;
            if (string.IsNullOrWhiteSpace(game.Title)) continue;
            string title = game.Title.Trim();
            string key = Normalize(title);
            if (Usable(key))
            {
                if (!exact.TryGetValue(key, out var values)) exact[key] = values = new(StringComparer.OrdinalIgnoreCase);
                values.Add(title);
            }
            foreach (var alias in game.AlternateNames ?? new())
            {
                string aliasKey = Normalize(alias);
                if (!Usable(aliasKey) || aliasKey == key) continue;
                if (!aliases.TryGetValue(aliasKey, out var values)) aliases[aliasKey] = values = new();
                values.Add((alias.Trim(), title));
            }
        }
        var result = new Dictionary<string, CatalogTitle>(StringComparer.Ordinal);
        foreach (var (key, values) in exact)
            if (values.Count == 1) result[key] = new(values.First(), false);
            else if (values.Select(Compact).Distinct(StringComparer.Ordinal).Count() == 1) result[key] = new(values.OrderBy(v => v.Length).ThenBy(v => v, StringComparer.Ordinal).First(), false);
        foreach (var (key, values) in aliases)
        {
            // Even an ambiguous exact name must never fall through to an alias.
            if (exact.ContainsKey(key) || values.Select(v => v.Owner).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 1) continue;
            var owner = values[0].Owner;
            var printed = values.Select(v => v.Alias).OrderBy(v => v, StringComparer.Ordinal).First();
            // A contiguous abbreviation can occur at either end or inside the full name.
            // Require a known alternate name, not a fuzzy substring match, and preserve sequel numbers.
            var ownerKey = Normalize(owner);
            bool expansion = SameNumbers(ownerKey, key) && (" " + ownerKey + " ").Contains(" " + key + " ", StringComparison.Ordinal);
            result[key] = new((Compact(owner) == Compact(printed) || expansion) ? owner : printed, true);
        }
        // Punctuation-only spelling variants share the same display when unambiguous.
        foreach(var group in result.ToArray().GroupBy(kv=>Compact(kv.Key)))
        {
            if(group.Select(kv=>Compact(kv.Value.Display)).Distinct(StringComparer.Ordinal).Count()!=1) continue;
            var canonical=group.OrderBy(kv=>kv.Value.Alias).ThenBy(kv=>kv.Value.Display.Length).ThenBy(kv=>kv.Value.Display,StringComparer.Ordinal).First().Value.Display;
            foreach(var kv in group) result[kv.Key]=kv.Value with {Display=canonical};
        }
        foreach(var group in result.ToArray().GroupBy(kv=>ListingKey(kv.Key)))
        {
            if(group.Select(kv=>ListingKey(kv.Value.Display)).Distinct(StringComparer.Ordinal).Count()!=1) continue;
            var best=group.OrderBy(kv=>kv.Value.Alias).ThenBy(kv=>kv.Value.Display.Length).ThenBy(kv=>kv.Value.Display,StringComparer.Ordinal).First().Value;
            result["\0"+group.Key]=best;
        }
        // Only unambiguous, complete numbered title equivalents may share a lookup.
        // Internal keys are not actual titles and must never participate in fuzzy matching.
        var titles = result.Where(kv=>!kv.Key.StartsWith("\0",StringComparison.Ordinal)).ToArray();
        foreach(var group in titles.Where(kv=>NumberedTitleKey(kv.Key)!=kv.Key || kv.Key.Split(' ').Skip(1).Any(t=>SequelNumber(t)>0)).GroupBy(kv=>NumberedTitleKey(kv.Key)))
        {
            if(group.Select(kv=>NumberedTitleKey(kv.Value.Display)).Distinct(StringComparer.Ordinal).Count()!=1 || group.Any(kv=>!SameNumbers(kv.Key,kv.Value.Display)))continue;
            var displays=group.Where(kv=>!kv.Value.Alias).Select(kv=>kv.Value.Display).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if(displays.Length==0)displays=group.Select(kv=>kv.Value.Display).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if(displays.Length==1)result["\0number:"+group.Key]=new(displays[0],true);
        }
        // A bare franchise name in prose is not proof of the original game.
        foreach(var kv in titles)
        {
            var parts=kv.Key.Split(' ');
            for(int i=1;i<parts.Length;i++)
                if(SequelNumber(parts[i])>0)
                {
                    var prefix=string.Join(" ",parts,0,i);
                    result["\0family:"+prefix]=new(prefix,true);
                }
        }
        return result;
        static bool Usable(string key) => key.Length >= 3 && key.Split(' ').Length <= 12;
    }
    private static void AnalyzePage(LibraryItem item, Dictionary<string, CatalogTitle> dictionary, string text, PageLayout layout, int page, List<Suggestion> suggestions)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string field, string value, string evidence, string confidence = "Review")
        {
            value = value.Trim(); if (field == "coverDate") value = NormalizeCoverDate(value); evidence = Regex.Replace(evidence.Trim(), @"\s+", " ");
            if (value.Length == 0 || value.Length > 500 || !seen.Add(field + "\0" + value)) return;
            suggestions.Add(new(field, value, page, evidence[..Math.Min(220, evidence.Length)], confidence));
        }
        void MatchField(string field, string pattern, string source)
        {
            var match = Regex.Match(source, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (match.Success) Add(field, match.Groups[1].Value, match.Value);
        }
        var lines = text.Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0 && x.Length < 600).Take(1200).ToArray();
        // OCR may lose the Contents heading, particularly on multi-column layouts.
        bool IsLegal(string line) => Regex.IsMatch(line, @"\b(?:all rights[. ]+reserved|(?:registered )?trademarks?|copyright|official seals?|licensed (?:by|from|to)|used under license|registered quality standards|patent pending)\b|©", RegexOptions.IgnoreCase);
        bool NumberedLine(string line) => Regex.IsMatch(line, @"(?:^\s*\d{1,3}[.\s]+|\.{2,}\s*\d{1,3}\s*$|\s+\d{1,3}\s*$)");
        bool PlatformIndexLine(string line) => Regex.IsMatch(line, @"^\s*\d{1,3}[A-Za-z]?\s+", RegexOptions.IgnoreCase) &&
            (Platforms.Any(p => Regex.IsMatch(line, p.Pattern, RegexOptions.IgnoreCase)) || Regex.IsMatch(line, @"\b(?:Genesis|Saturn|PC|DOS)\b", RegexOptions.IgnoreCase));
        bool toc = page > 1 && (lines.Any(l => Regex.IsMatch(l, @"^(?:table of\s+)?contents\s*[:.!]?\s*$", RegexOptions.IgnoreCase)) || (lines.Count(PlatformIndexLine) >= 3 && lines.Any(l => Regex.IsMatch(l, @"\b(?:reviews|features|departments|previews)\b", RegexOptions.IgnoreCase))) || (lines.Count(l => Regex.IsMatch(l, @"(?:^\s*\d{1,3}\s+[A-Z][\p{L}]|\.{2,}\s*\d{1,3}\s*$)")) >= 5 && lines.Any(l=>Regex.IsMatch(l,@"^\s*(?:reviews|features|departments|previews|in this issue)\s*[:.!]?\s*$",RegexOptions.IgnoreCase))));
        // Numbered title listings and review/solution descriptions can identify an index
        // even when graphical section labels are missing from OCR.
        bool graphicalIndex = page > 1 && page <= 16 && LooksLikeGraphicalIndex(layout, text);
        bool centeredIndex = page > 1 && page <= 16 && LooksLikeCenteredIndex(layout);
        toc = toc || graphicalIndex || centeredIndex;
        bool retailPage = !toc && RetailMarkers(text) >= 2;
        bool otherIssuePage = !toc && Regex.IsMatch(text, @"\b(?:back issues?|previous issues?|past issues?|next issue|order (?:this|your|an) issue)\b", RegexOptions.IgnoreCase) && (Regex.Matches(text, @"\b(?:issue|no\.)\s*#?\d{1,4}\b", RegexOptions.IgnoreCase).Count >= 2 || RetailMarkers(text) >= 1);
        bool likelyAd = retailPage || otherIssuePage || !toc && lines.Any(IsLegal) && Regex.IsMatch(text, @"\b(?:order now|call toll.free|available at participating|ask your retailer|for a limited time|send check or money order|suggested retail price|visit your local retailer)\b", RegexOptions.IgnoreCase);
        if (page <= 8 || Regex.IsMatch(text, @"\b(?:published by|is published|editor.in.chief|ISSN|masthead)\b", RegexOptions.IgnoreCase))
        {
            // An isolated publication year is never a reliable issue number.
            if (page <= 4 || Regex.IsMatch(text, @"\b(?:masthead|published by|editor.in.chief)\b", RegexOptions.IgnoreCase))
            {
                foreach (Match issue in Regex.Matches(text, @"\b(?:issue\s*(?:no\.?|number|#)?|(?:vol\.?\s*\d{1,3}\s*[,;]?\s*no\.?))\s*#?\s*(\d{1,4})\b", RegexOptions.IgnoreCase))
                {
                    if (int.TryParse(issue.Groups[1].Value, out int number) && number > 0 && (number < 1900 || number > 2099))
                        Add("issueNumber", issue.Groups[1].Value, issue.Value + " (printed number; verify overall issue sequence)", "Printed number");
                }
            }
            foreach (Match volume in Regex.Matches(text, @"\b(?:volume|vol\.?)\s*(\d{1,3}|[IVXLCDM]{1,7})(?![\p{L}\p{N}])", RegexOptions.IgnoreCase))
            { var v = RomanOrNumber(volume.Groups[1].Value); if (v > 0 && v < 1000) Add("volume", v.ToString(), volume.Value); }
            if (page <= 2)
            {
                var date = Regex.Match(text, $@"\b((?:{Months})(?:\s*[/–-]\s*(?:{Months}))?\s+(?:19|20)\d{{2}})\b", RegexOptions.IgnoreCase);
                if (date.Success) { Add("coverDate", date.Groups[1].Value, date.Value); Add("year", GetYear(date.Value).ToString(), date.Value); }
                var publication = (item.MagazineTitle ?? "").Trim();
                if (publication.Length > 0 && Normalize(text).Contains(Normalize(publication), StringComparison.Ordinal)) Add("magazineTitle", publication, "Existing publication name appears on page.");
            }
            if (page <= 3 || toc)
            {
                foreach (Match date in Regex.Matches(text, $@"\b((?:{Months})(?:\s*[/–-]\s*(?:{Months}))?\s+(?:(?:19|20)\d{{2}}|['’‘]\d{{2}}))\b", RegexOptions.IgnoreCase))
                {
                    var value = Regex.Replace(date.Groups[1].Value, "['’‘](\\d{2})$", m => (int.Parse(m.Groups[1].Value) >= 70 ? "19" : "20") + m.Groups[1].Value);
                    Add("coverDate", value, date.Value); var y = GetYear(value); if (y > 0) Add("year", y.ToString(), date.Value);
                }
            }
            // The subject of a game advertisement can also be "Published by".
            // Require the actual publication name in the same local OCR context.
            var publicationKey = Normalize(item.MagazineTitle ?? "");
            if (publicationKey.Length > 2 && !likelyAd)
            {
                for (int pi = 0; pi < layout.Lines.Count; pi++)
                {
                    var pubLine = layout.Lines[pi];
                    var local = layout.Lines.Skip(Math.Max(0, pi - 3)).Take(7)
                        .Where(l => l.Block == pubLine.Block && l.Paragraph == pubLine.Paragraph)
                        .Select(l => l.Text);
                    var context = string.Join(" ", local);
                    if (!(" " + Normalize(context) + " ").Contains(" " + publicationKey + " ", StringComparison.Ordinal)) continue;
                    if (IsLegal(pubLine.Text) && !Regex.IsMatch(context, @"\b(?:subscription|published (?:monthly|weekly|quarterly)|ISSN)\b", RegexOptions.IgnoreCase)) continue;
                    // A publisher name may wrap onto the next OCR line. Never propose a dangling
                    // fragment or replace a longer stored name with its shorter prefix.
                    var pub = PublisherName(pubLine.Text);
                    if (MissingPublisher(pub)) continue;
                    if (IncompletePublisher(pub))
                    {
                        if (pi + 1 >= layout.Lines.Count) continue;
                        var next = layout.Lines[pi + 1];
                        if (!PublisherContinuation(pubLine, next, layout) || IsLegal(next.Text)) continue;
                        var continuation = Regex.Split(next.Text.Trim(), @"[,;]|\s+(?:ISSN|ISBN|editor|telephone|tel|fax|address|subscription)\b", RegexOptions.IgnoreCase)[0].Trim();
                        if (!Regex.IsMatch(continuation, @"^[\p{L}][\p{L}\p{M}\p{N} &'’().\-]{0,49}$") ||
                            Regex.IsMatch(continuation, @"^(?:editor|editorial|publisher|published|subscription|address|telephone|fax|printed|copyright|all rights|www|http|contact|contents|reviews)\b", RegexOptions.IgnoreCase)) continue;
                        pub += " " + continuation;
                        context += " " + next.Text;
                    }
                    if (pub.Length > 80 || IncompletePublisher(pub) || ShorterPublisherPrefix(pub, item.Publisher)) continue;
                    Add("publisher", pub, context, "Publication-linked print");
                }
            }
            var masthead = Regex.Match(text, @"^\s*([\p{L}][\p{L}\p{N} &'.-]{2,70}?)\s*\(ISSN\b", RegexOptions.IgnoreCase|RegexOptions.Multiline);
            if(masthead.Success)
            {
                var title=masthead.Groups[1].Value;
                if(!string.IsNullOrWhiteSpace(item.MagazineTitle) && Normalize(title)==Normalize(item.MagazineTitle+" Magazine")) title=item.MagazineTitle;
                Add("magazineTitle",title,masthead.Value);
            }
            foreach (Match editor in Regex.Matches(text, @"(?im)^[ \t]*(?:editor[- ]in[- ]chief|editorial director|(?:managing |executive )?editor)\b[ \t]*[:–-]?[ \t]*([^\r\n]{3,80})"))
            {
                var person = editor.Groups[1].Value.Trim();
                if (LooksLikePerson(person)) Add("writer", person, editor.Value);
            }
            MatchField("region", @"\b(?:printed|published) in (?:the )?((?:United States(?: of America)?|USA|United Kingdom|Canada|Australia|Japan|Germany|France))\b", text);
            if (Regex.IsMatch(text, @"\b(?:postmaster|published|publisher)\b", RegexOptions.IgnoreCase) && Regex.IsMatch(text, @"\b(?:[A-Z]{2}\s+\d{5}(?:-\d{4})?|United States|U\.S\.A\.)\b")) Add("region", "US", "Publisher/postal address resembles a US address; verify country.", "Inferred");
            MatchField("languageTag", @"^\s*language\s*:\s*(English|French|German|Spanish|Japanese|Italian|Portuguese)\b", text);
            MatchField("magazineTitle", @"^\s*(?:magazine title|publication title)\s*:\s*([^\r\n]{3,100})", text);
            MatchField("magazineCategory", @"^\s*(?:magazine category|publication category)\s*:\s*([^\r\n]{3,100})", text);
            MatchField("platformFocus", @"^\s*(?:platform focus|audience focus)\s*:\s*([^\r\n]{3,100})", text);
            MatchField("publicationDate", $@"\b(?:publication date|published on)\s*:?\s*((?:{Months})\s+(?:\d{{1,2}},?\s+)?(?:19|20)\d{{2}})\b", text);
            foreach (Match match in Regex.Matches(text, @"\bISSN\s*[:#]?\s*(\d{4}[- ]?\d{3}[\dX])\b", RegexOptions.IgnoreCase))
            {
                string code = match.Groups[1].Value.Replace("-", "").Replace(" ", "").ToUpperInvariant();
                int sum = 0; for (int j = 0; j < 8; j++) sum += (code[j] == 'X' ? 10 : code[j] - '0') * (8 - j);
                if (sum % 11 == 0) Add("barcodeUpcIssn", code.Insert(4, "-"), match.Value, "Checksum valid");
            }
            foreach (Match match in Regex.Matches(text, @"\b(?:UPC|EAN|barcode)\s*[:#]?\s*(\d{12,13})\b", RegexOptions.IgnoreCase))
            {
                string code = match.Groups[1].Value; int sum = 0;
                for (int j = code.Length - 2, k = 0; j >= 0; j--, k++) sum += (code[j] - '0') * (k % 2 == 0 ? 3 : 1);
                if ((10 - sum % 10) % 10 == code[^1] - '0') Add("barcodeUpcIssn", code, match.Value, "Checksum valid");
            }
        }
        if (page == 1 && layout.Height > 0)
        {
            foreach (var group in layout.Lines.Where(l => l.Y > layout.Height * .70 && Regex.IsMatch(l.Text, @"^[0-9\s-]{5,20}$")).GroupBy(l => l.Y / Math.Max(1, layout.Height / 55)))
            {
                var code = Regex.Replace(string.Join("", group.OrderBy(l => l.X).Select(l => l.Text)), @"\D", "");
                if (ValidUpc(code)) Add("barcodeUpcIssn", code, "Cover barcode-area digits: " + code, "Review barcode");
            }
        }
        if (page == 1 || toc)
        {
            var english = Regex.Matches(text, @"\b(?:the|and|with|from|for|of|this|your|reviews|game|published)\b", RegexOptions.IgnoreCase).Count;
            if (english >= 12) Add("languageTag", "English", "Repeated English editorial words; language inferred, not guaranteed.", "Inferred");
        }
        if (likelyAd) return;
        if(page==1)
        {
            foreach(var line in layout.Lines)
            {
                if(!Regex.IsMatch(line.Text,@"\b(?:for|dedicated to)\b.{0,65}\b(?:owners|gamers|players|users)\b",RegexOptions.IgnoreCase))continue;
                foreach(var (pattern,name) in Platforms)
                    if(name!="Arcade"&&Regex.IsMatch(line.Text,pattern,RegexOptions.IgnoreCase))
                    { Add("platformFocus",name,line.Text+Geometry(line,layout),"Explicit cover audience"); Add("featuredPlatforms",name,line.Text+Geometry(line,layout),"Explicit cover audience"); }
            }
        }
        if (page > 1 && page <= 12) AnalyzeEditorialIntroduction(layout,dictionary,page,suggestions);
        if (toc && !centeredIndex) AnalyzeNumberedFeatures(layout,dictionary,page,suggestions);
        // A cover story can be indexed even when the artwork/logo has no readable text.
        // Never assume that the first feature or largest OCR line is the artwork subject.
        if (toc || (page > 1 && page <= 12 && lines.Count(NumberedLine) >= 2 && lines.Any(l=>Regex.IsMatch(l,@"^\s*(?:index|in this issue|on the cover|cover story|cover feature)\b",RegexOptions.IgnoreCase))))
        {
            var explicitLabel = @"(?:cover\s+(?:story|feature|game)|on\s+the\s+cover|this\s+(?:month|issue)['’]s\s+cover)";
            var weakLabel = @"(?:featured\s+games?|main\s+feature|special\s+feature)";
            var labelPattern = @"^\s*(?:\d{1,3}[.\s]+)?(?<label>"+explicitLabel+"|"+weakLabel+@")\b\s*[:\-–—]?\s*(?<tail>.*)$";
            foreach(int ci in Enumerable.Range(0,layout.Lines.Count).OrderBy(i=>Regex.IsMatch(layout.Lines[i].Text,@"^\s*(?:\d{1,3}[.\s]+)?"+explicitLabel+@"\b",RegexOptions.IgnoreCase)?0:1))
            {
                var heading=layout.Lines[ci];
                var match=Regex.Match(heading.Text,labelPattern,RegexOptions.IgnoreCase);
                if(!match.Success || IsLegal(heading.Text) || (heading.Height>0&&heading.Confidence<45)) continue;
                bool explicitCover=Regex.IsMatch(match.Groups["label"].Value,"^"+explicitLabel+"$",RegexOptions.IgnoreCase);
                var fragments=new List<(string Text,LayoutLine Line)>();
                var tail=match.Groups["tail"].Value.Trim();
                if(tail.Length>0) fragments.Add((tail,heading));
                // Follow nearby title lines, not unrelated columns or the following department.
                for(int ni=ci+1;ni<Math.Min(ci+3,layout.Lines.Count);ni++)
                {
                    var next=layout.Lines[ni];
                    if(Regex.IsMatch(next.Text,labelPattern,RegexOptions.IgnoreCase)||Regex.IsMatch(next.Text,@"^\s*(?:reviews?|previews?|features?|solutions?|walkthroughs?|departments?|news|contents|letters|editorial|cheats?|tips)\s*[:.!]?\s*$",RegexOptions.IgnoreCase)||IsLegal(next.Text)) break;
                    if(heading.Height>0 && next.Height>0 && (next.Y<heading.Y || next.Y-heading.Y>Math.Max(heading.Height,next.Height)*4 || Math.Abs(next.X-heading.X)>Math.Max(heading.Height,next.Height)*5)) break;
                    if(next.Height>0&&next.Confidence<45) break;
                    fragments.Add((next.Text,next));
                    if(fragments.Count>=2) break;
                }
                if(fragments.Count==2) fragments.Insert(0,(fragments[0].Text+" "+fragments[1].Text,fragments[0].Line));
                bool identified=false;
                foreach(var fragment in fragments)
                {
                    var clean=Regex.Replace(fragment.Text,@"^\s*\d{1,3}[.\s]+|\.{2,}\s*\d{1,3}\s*$|\s+\(page\s+\d{1,3}\)\s*$","",RegexOptions.IgnoreCase).Trim(' ',':','-','–','—');
                    if(!ValidCoverSubject(clean,item.MagazineTitle)) continue;
                    var hits=FindGames(dictionary,clean,true,true,true).Where(h=>!h.Fuzzy&&h.Whole&&ValidCoverSubject(h.Game,item.MagazineTitle)).Select(h=>h.Game).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                    if(hits.Length>0)
                    {
                        foreach(var game in hits) Add("coverSubject",game,heading.Text+" | "+fragment.Text+Geometry(fragment.Line,layout),explicitCover?"Explicit contents cover story":"Contents featured game — cover unverified");
                        identified=true; break;
                    }
                }
                // A printed non-game subject is eligible only under an explicit cover label.
                // Use the inline text or first adjacent line, not a concatenated paragraph.
                if(explicitCover && !identified && fragments.Count>0)
                {
                    var fragment=fragments.Count==3?fragments[1]:fragments[0];
                    var clean=Regex.Replace(fragment.Text,@"^\s*\d{1,3}[.\s]+|\.{2,}\s*\d{1,3}\s*$","").Trim(' ',':','-','–','—');
                    if(ValidCoverSubject(clean,item.MagazineTitle) && LooksLikeUnresolvedTitle(clean) && !Regex.IsMatch(clean,@"\b(?:page|see|turn to|read about|this month|in this issue)\b",RegexOptions.IgnoreCase))
                        Add("coverSubject",clean,heading.Text+" | "+fragment.Text+Geometry(fragment.Line,layout),"Explicit cover subject — verify printed title");
                }
            }
        }
        var heights = layout.Lines.Where(l => l.Confidence >= 45 && l.Height > 0).Select(l => l.Height).OrderBy(x => x).ToArray();
        double median = heights.Length > 0 ? heights[heights.Length / 2] : 0;
        bool prominent(LayoutLine l) => median > 0 && l.Height >= median * 1.3 && l.Confidence >= 50;
        var coverHeadings = new List<(string Game, LayoutLine Line)>();
        var section = "";
        var previous = (LayoutLine?)null;
        if(centeredIndex) { AnalyzeCenteredIndex(layout,dictionary,page,suggestions); return; }
        var coverRegion = toc; // Leading contents entries are not automatically cover stories.
        var platformIndex = false;
        foreach (var l in layout.Lines)
        {
            string line = SequelLineText(dictionary,l,layout);
            if (IsLegal(line) || RetailMarkers(line) >= 2 || Regex.IsMatch(line, @"\b(?:grand prize|send (?:your )?entries|sweepstakes|win this|no purchase necessary|order now|(?:cd|poster) contest|third .* movie)\b", RegexOptions.IgnoreCase)) { previous = null; continue; }
            if (l.Height > 0 && l.Confidence < 38) { previous = null; continue; }
            var headingText = Regex.Replace(line, @"^\d{1,3}[A-Za-z]?[.\s]+|\.{2,}\s*\d{1,3}$", "").Trim();
            bool numbered = NumberedLine(line);
            bool platformHeading = PlatformIndexLine(line) || (numbered && Regex.IsMatch(headingText, @"^(?:Jaguar|32X|Genesis|PC|DOS|Saturn)\b", RegexOptions.IgnoreCase));
            bool genericSection = Regex.IsMatch(headingText, @"^(?:table of contents|contents|cover (?:story|feature)|features?|reviews?|previews?|departments?|news|letters|editorial|interviews?|walkthroughs?|solutions?|strategy guides?|cheats?(?: and tips)?|tips(?: and tricks)?|codes|hardware)\s*[:.!]?\s*$", RegexOptions.IgnoreCase);
            if (genericSection)
            {
                section = headingText;
                if (Regex.IsMatch(section, @"^cover (?:story|feature)", RegexOptions.IgnoreCase)) coverRegion = true;
                else if (!Regex.IsMatch(section, @"contents", RegexOptions.IgnoreCase)) coverRegion = false;
                if (toc || prominent(l)) Add("specialFeatures", section, line, "Section heading");
                if (Regex.IsMatch(section, @"^(?:reviews?|previews?|features?|walkthroughs?|solutions?|strategy guides?)", RegexOptions.IgnoreCase)) platformIndex = true;
                if (Regex.IsMatch(section, @"^(?:departments?|letters|news|cheats?|tips|codes|editorial)", RegexOptions.IgnoreCase)) platformIndex = false;
                previous = null; continue;
            }
            if (platformHeading) { if (!coverRegion || platformIndex) coverRegion = false; if (!Regex.IsMatch(section, @"^(?:departments?|letters|news|cheats?|tips|codes)", RegexOptions.IgnoreCase)) platformIndex = true; }
            bool departments = Regex.IsMatch(section, @"^(?:departments?|letters|news|cheats?|tips|codes|editorial)", RegexOptions.IgnoreCase);
            bool listContext = toc && !departments && (platformIndex || coverRegion || numbered);
            // Explicit review/preview statements in introductory editorial text are
            // useful even when the magazine has no recognizable contents heading.
            bool explicitCoverage = page > 1 && page <= 8 && !departments && !otherIssuePage &&
                Regex.IsMatch(line, @"\b(?:reviews?|previews?|walkthroughs?|features?)\s+(?:of|for|on)\s+", RegexOptions.IgnoreCase) &&
                !Regex.IsMatch(line, @"\b(?:next|last|previous|back|past|upcoming)\s+issue\b", RegexOptions.IgnoreCase);
            var coverageText = explicitCoverage ? Regex.Split(line, @"\b(?:reviews?|previews?|walkthroughs?|features?)\s+(?:of|for|on)\s+", RegexOptions.IgnoreCase).Last() : "";
            var coverageHits = explicitCoverage ? FindGames(dictionary, coverageText, true, true, true).Where(h=>!h.Fuzzy).Select(h=>Compact(h.Game)).ToHashSet(StringComparer.Ordinal) : new HashSet<string>(StringComparer.Ordinal);
            bool titleContext = listContext || (prominent(l) && Words.Matches(line).Count <= 12) || page == 1;
            bool gameContext = Regex.IsMatch(line, @"\b(?:game|review|preview|version|cheat|code|password|released?|play(?:ed|ing)?|for (?:the )?(?:SNES|NES|Genesis|Game Boy|3DO|Sega CD))\b", RegexOptions.IgnoreCase);
            var matched = FindGames(dictionary, line, titleContext, listContext, gameContext).ToList();
            // A platform-labelled list is evidence of a game reference, not a dedicated article.
            var labelled = Platforms.Any(p => Regex.IsMatch(line, @"^\s*(?:[_\-–]\s*)?(?:" + p.Pattern + @")\s*:", RegexOptions.IgnoreCase));
            if (labelled && line.Contains(':'))
            {
                var entries = line[(line.IndexOf(':') + 1)..];
                foreach (var hit in FindGames(dictionary, entries, true, true, true).Where(h=>!h.Fuzzy && h.Whole))
                    if (!matched.Any(x=>ListingKey(x.Game)==ListingKey(hit.Game))) matched.Add(hit);
                var entryParts = entries.Split([',',';'], StringSplitOptions.RemoveEmptyEntries).SelectMany(part => FindGames(dictionary,part.Trim(),true,true,true).Any(h=>h.Whole&&!h.Fuzzy) ? new[]{part} : Regex.Split(part,@"\s+and\s+",RegexOptions.IgnoreCase));
                foreach(var rawEntry in entryParts)
                {
                    var entry=rawEntry.Trim().Trim('.', '"', '“', '”', '’', ':');
                    if(!LooksLikeUnresolvedTitle(entry)) continue;
                    var exactHits=FindGames(dictionary,entry,true,true,true).Where(h=>h.Whole&&!h.Fuzzy).ToArray();
                    if(exactHits.Length>0) { foreach(var hit in exactHits) if(!matched.Any(x=>ListingKey(x.Game)==ListingKey(hit.Game))) matched.Add(hit); }
                    else if(!matched.Any(x=>ListingKey(x.Game)==ListingKey(entry))) Add("unresolvedGame",entry,line+Geometry(l,layout),"Unresolved platform-labelled entry — verify printed title");
                }
            }
            if(page>1 && !departments && !listContext)
            {
                foreach(Match quoted in Regex.Matches(line,@"[“""]([^“”""\r\n]{4,75})[”""]"))
                {
                    var printed=quoted.Groups[1].Value.Trim();
                    if(!LooksLikeUnresolvedTitle(printed))continue;
                    foreach(var hit in FindGames(dictionary,printed,true,true,true).Where(h=>h.Whole&&!h.Fuzzy))
                        if(!matched.Any(x=>ListingKey(x.Game)==ListingKey(hit.Game)))matched.Add(hit);
                }
                // OCR often preserves a closing quotation mark but drops the opening one.
                var quotedTail=Regex.Match(line,@"^\s*([A-Z][\p{L}\p{N} &'’:\-]{3,65})[”""](?:[.!?,; ]|$)");
                if(quotedTail.Success)
                {
                    var printed=quotedTail.Groups[1].Value.Trim();
                    if(LooksLikeUnresolvedTitle(printed))
                    {
                        var quotedHits=FindGames(dictionary,printed,true,true,true).Where(h=>h.Whole&&!h.Fuzzy).ToArray();
                        foreach(var hit in quotedHits)if(!matched.Any(x=>ListingKey(x.Game)==ListingKey(hit.Game)))matched.Add(hit);
                        if(quotedHits.Length==0 && gameContext)Add("unresolvedGame",printed,line+Geometry(l,layout),"Unresolved quoted game-context title — verify printed title");
                    }
                }
            }
            if (explicitCoverage)
                foreach (var hit in FindGames(dictionary, coverageText, true, true, true).Where(h=>!h.Fuzzy))
                    if (!matched.Any(x=>Compact(x.Game)==Compact(hit.Game))) matched.Add(hit);
            if (departments && Regex.IsMatch(line,@"\b(?:presents|includes exclusive codes|hot news from)\b",RegexOptions.IgnoreCase)) matched.Clear();
            if (page > 1 && !listContext && !gameContext)
                matched.RemoveAll(h=>Words.Matches(h.Game).Count==1 && !(prominent(l) && l.Y<layout.Height*.60 && Regex.IsMatch(text,@"\b(?:review|preview|rating|graphics|sound|gameplay|publisher|developer|walkthrough)\b",RegexOptions.IgnoreCase)));
            string joinedEvidence = "";
            // Adjacent lines in a column may be different OCR blocks. Join only a plausible
            // title continuation; do not join independent headings or cross columns.
            if (listContext && previous is not null && previous.Height > 0 && l.Height > 0 && l.Y > previous.Y && l.Y - previous.Y <= Math.Max(l.Height, previous.Height) * 2.8 && Math.Abs(l.X-previous.X) <= Math.Max(l.Height,previous.Height)*2 && !platformHeading && !previous.Text.TrimEnd().EndsWith(','))
            {
                var joined = previous.Text + " " + line;
                var tail = previous.Text.Split([',',';']).Last().Trim();
                var head = line.Split([',',';']).First().Trim();
                if (tail.Length > 0 && head.Length > 0 && !IsLegal(joined))
                {
                    var full = FindGames(dictionary, tail + " " + head, true, true, true).Where(h=>h.Whole&&!h.Fuzzy).ToArray();
                    foreach (var hit in full)
                    {
                        matched.RemoveAll(x=>Compact(hit.Game)!=Compact(x.Game)&&Normalize(hit.Game).Contains(Normalize(x.Game),StringComparison.Ordinal));
                        if(!matched.Any(x=>Compact(x.Game)==Compact(hit.Game))) {matched.Add(hit);joinedEvidence=joined;}
                    }
                }
            }
            if (listContext && platformIndex && !coverRegion && !platformHeading)
            {
                foreach (var part in headingText.Split([',',';'],StringSplitOptions.RemoveEmptyEntries))
                {
                    var candidate=part.Trim().TrimEnd('.');
                    if (!LooksLikeUnresolvedTitle(candidate)) continue;
                    if (FindGames(dictionary,candidate,true,true,true).Any(h=>h.Whole || h.Fuzzy)) continue;
                    if (matched.Any(h=>Normalize(h.Game)==Normalize(candidate))) continue;
                    Add("unresolvedGame",candidate,line+Geometry(l,layout),"Unresolved contents entry — verify title and coverage");
                }
            }
            if (matched.Count==0 && (page==1 || listContext) && prominent(l))
            {
                var printed=headingText.Trim();
                var titleKey=Normalize(RepairRomanGlyphs(printed));
                int lastSpace=titleKey.LastIndexOf(' ');
                if(lastSpace>0 && BareFranchise(dictionary,titleKey[..lastSpace]) && SequelNumber(titleKey[(lastSpace+1)..])>0 && !TryNumberedTitle(dictionary,printed,out _))
                    Add("unresolvedGame",printed,line+Geometry(l,layout),"Numbered printed heading — catalog unresolved; review full title");
            }
            foreach (var hit in matched)
            {
                string evidence = (joinedEvidence.Length>0 && !Normalize(line).Contains(Normalize(hit.Game),StringComparison.Ordinal) ? joinedEvidence : line) + Geometry(l, layout);
                if (page == 1 && titleContext && l.Confidence >= 50 && ValidCoverSubject(hit.Game,item.MagazineTitle)) coverHeadings.Add((hit.Game,l));
                else if (toc && coverRegion && Regex.IsMatch(line, @"\b(?:game for|in.depth)\b", RegexOptions.IgnoreCase) && !hit.Fuzzy && ValidCoverSubject(hit.Game,item.MagazineTitle))
                { Add("coverSubject", hit.Game, evidence, "Contents main feature — cover unverified"); Add("featuredGames", hit.Game, evidence, "Contents main feature"); }
                else if (explicitCoverage && coverageHits.Contains(Compact(hit.Game))) Add("featuredGames", hit.Game, evidence, "Explicit review/feature reference");
                else if (listContext || (page > 1 && prominent(l) && hit.Whole && !departments && !hit.Fuzzy && !(Words.Matches(line).Count<=3 && line.TrimEnd().EndsWith('!')) && l.Y < layout.Height*.60 && Regex.IsMatch(text,@"\b(?:review|preview|rating|graphics|sound|gameplay|publisher|developer|walkthrough)\b",RegexOptions.IgnoreCase))) Add("featuredGames", hit.Game, evidence, hit.Fuzzy ? "Possible OCR correction" : "Editorial heading");
                else if (page > 1) Add("mentionedGames", hit.Game, evidence, hit.Fuzzy ? "Possible OCR correction" : "Contextual title match");
            }
            bool explicitPlatformLabel = matched.Count > 0 && Platforms.Any(p => Regex.IsMatch(line, @"^\s*(?:[_\-–]\s*)?(?:" + p.Pattern + @")\s*:", RegexOptions.IgnoreCase));
            if ((toc && !departments && (platformHeading || matched.Count > 0)) || (matched.Count > 0 && (gameContext || explicitPlatformLabel)))
            {
                foreach (var (pattern, name) in Platforms)
                {
                    if (!Regex.IsMatch(line, pattern, RegexOptions.IgnoreCase)) continue;
                    if (name == "Arcade" && !ArcadePlatformEvidence(line, toc)) continue;
                    Add("featuredPlatforms", name, line + Geometry(l, layout), "Editorial platform evidence");
                }
                if (toc && Regex.IsMatch(line, @"\bGenesis\b", RegexOptions.IgnoreCase)) Add("featuredPlatforms", "Sega Genesis", line + Geometry(l, layout), "Editorial platform evidence");
            }
            if (toc && departments && numbered && Words.Matches(headingText).Count <= 6 && !IsLegal(headingText) && !Regex.IsMatch(headingText,@"\b(?:presents|includes?|exclusive codes|hot news from)\b",RegexOptions.IgnoreCase)) Add("specialFeatures", headingText, line + Geometry(l, layout), "Department listing");
            if(toc && !platformHeading && matched.Count==0 && numbered && prominent(l) && Words.Matches(headingText).Count is >=2 and <=6 && Regex.IsMatch(headingText,@"\b(?:letters?|editor|mail|news|poll|scores?|talk|corner|access|tips|codes|shop|community|reader|hardware|interview|preview|review)\b",RegexOptions.IgnoreCase) && !Regex.IsMatch(headingText,@"\b(?:includes?|presents|new from|available)\b",RegexOptions.IgnoreCase))
                Add("specialFeatures",headingText,line+Geometry(l,layout),"Possible contents department — review heading");
            if ((page <= 3 || toc) && Regex.IsMatch(line, @"\b(?:free|bonus|pull[- ]out|includes?|inside|exclusive)\s+(?:a\s+)?(?:poster|demo (?:disc|disk|CD)|disc|disk|insert|sticker|map|cover disc)\b", RegexOptions.IgnoreCase)) Add("includedExtras", line, line + Geometry(l, layout));
            previous = platformHeading ? null : l;
        }
        if (page > 1 && !toc && layout.Width > 0 && layout.Height > 0)
            AnalyzeArticleRegions(layout,dictionary,page,suggestions);
        if (page == 1 && coverHeadings.Count > 0)
        {
            var candidates = coverHeadings.GroupBy(x => Compact(x.Game)).Select(g => g.OrderByDescending(x=>x.Line.Height).First()).OrderByDescending(x=>x.Line.Height).ToArray();
            var first = candidates[0];
            // Size is supporting evidence only. Never replace a known cover based on a tagline.
            if (prominent(first.Line) && (candidates.Length == 1 || first.Line.Height >= candidates[1].Line.Height * 1.6)) Add("coverSubject", first.Game, first.Line.Text + Geometry(first.Line, layout), "Visual cover candidate");
            else foreach (var c in candidates.Take(6)) Add("coverSubject", c.Game, c.Line.Text + Geometry(c.Line, layout), "Ambiguous cover heading");
        }

    }
    // Cover passes remain separate: never concatenate unrelated OCR blocks across passes.
    private static void AnalyzeCover(LibraryItem item, Dictionary<string,CatalogTitle> dictionary, PageLayout[] passes, List<Suggestion> suggestions)
    {
        var candidates = new List<(string Name, string Printed, string Evidence, double Size, bool Editorial, bool Resolved)>();
        void Diagnostic(string name,string evidence,string status) => suggestions.Add(new("coverDiagnostic",name,1,evidence,status));
        int passNumber=0;
        foreach(var pass in passes)
        {
            passNumber++;
            Diagnostic($"OCR pass {passNumber}", $"Image {pass.Width} x {pass.Height}; lines {pass.Lines.Count}", "Raw cover OCR");
            foreach(var line in pass.Lines.Take(200))
                Diagnostic($"Pass {passNumber}: {line.Text}", Geometry(line,pass)+$" | box {line.X},{line.Y},{line.Width},{line.Height} | confidence {line.Confidence:F1}", "OCR line");
        }

        // Explicit cover numbering is collected across all passes, even if the
        // selected general OCR pass did not contain the issue label.
        var printedIssues=passes.SelectMany(p=>p.Lines).Where(l=>l.Confidence>=50)
            .SelectMany(l=>Regex.Matches(l.Text,@"\bissue\s*(?:no\.?|number|#)?\s*#?\s*(\d{1,4})(?!\d)",RegexOptions.IgnoreCase).Cast<Match>().Select(m=>(Number:m.Groups[1].Value,Text:l.Text)))
            .Where(m=>int.TryParse(m.Number,out int n)&&n>0&&(n<1900||n>2099)).GroupBy(m=>m.Number).ToArray();
        if(printedIssues.Length==1)suggestions.Add(new("issueNumber",printedIssues[0].Key,1,printedIssues[0].First().Text,"Explicit cover issue number"));
        string Clean(string value) => Regex.Replace(value.Trim(), @"\s+(?:reviewed!?|reviews?!?|previewed!?|previews?!?|solution|walkthrough|strategy guide|exclusive maps)\s*[!.:]*$", "", RegexOptions.IgnoreCase).Trim(' ',':','!','—','-');
        string? Resolve(string value)
        {
            if (TryNumberedTitle(dictionary,value,out var numbered)) return numbered.Display;
            var key=Normalize(value);
            if(dictionary.TryGetValue(key,out var exact)) return exact.Display;
            if(dictionary.TryGetValue("\0"+ListingKey(value),out var equivalent)) return equivalent.Display;
            return null;
        }
        bool TitleLike(string value) => ValidCoverSubject(value,item.MagazineTitle) && LooksLikeUnresolvedTitle(value) && !ArticleNoise(value)
            && !Regex.IsMatch(value,@"\b(?:inside|essential|every|new|game[s]?|issue|solution|walkthrough|maps|definitive|reviewed|previewed|unofficial|official|edition of)\b",RegexOptions.IgnoreCase);
        foreach(var pass in passes.Where(p=>p.Width>0&&p.Height>0))
        {
            var lines=pass.Lines.Where(l=>l.Height>0&&l.Confidence>=35).ToArray();
            if(lines.Length==0)continue;
            AnalyzeExplicitCoverCallouts(item,dictionary,lines.Where(l=>l.Confidence>=55).ToArray(),pass.Height,suggestions,"OCR");
            double median=lines.Select(l=>(double)l.Height).OrderBy(h=>h).ElementAt(lines.Length/2);
            bool Nearby(LayoutLine a,LayoutLine b) => b.Y>a.Y && b.Y-(a.Y+a.Height)<=Math.Max(a.Height,b.Height)*1.5 &&
                (Math.Min(a.X+a.Width,b.X+b.Width)-Math.Max(a.X,b.X)>Math.Min(a.Width,b.Width)*.15 || Math.Abs(a.X-b.X)<=Math.Max(a.Height,b.Height)*1.2);
            void Candidate(string printed, LayoutLine l, bool editorial, double size)
            {
                printed=Clean(printed);
                var name=Resolve(printed);
                if(!ValidCoverSubject(printed,item.MagazineTitle) || (name is not null && !ValidCoverSubject(name,item.MagazineTitle)) || (!editorial&&name is null) || (name is null&&!TitleLike(printed)))return;
                // Preserve an explicit unresolved subtitle instead of reducing it to a franchise.
                bool strategy = lines.Any(n => n.Y >= l.Y - l.Height && n.Y <= l.Y + Math.Max(l.Height, size)*4 &&
                    Math.Min(n.X+n.Width,l.X+l.Width)-Math.Max(n.X,l.X)>0 &&
                    Regex.IsMatch(n.Text,@"\b(?:solution|walk[ -]?through|exclusive maps|strategy guide|play guide)\b",RegexOptions.IgnoreCase));
                candidates.Add((name??printed,printed,printed+Geometry(l,pass)+(strategy?" | Nearby strategy headline":""),size>0?size/pass.Height*(strategy?1.5:1):0,editorial,name is not null));
            }
            // A stacked REVIEWED / INSIDE badge can sit above a narrow review column.
            // Only promote complete catalog entries in a contiguous small-print column.
            foreach(var header in lines.Where(l=>Regex.IsMatch(l.Text,@"\b(?:reviewed|review)\b",RegexOptions.IgnoreCase)))
            {
                var inside=lines.FirstOrDefault(n=>n.Y>=header.Y&&n.Y-header.Y<pass.Height*.09&&Regex.IsMatch(n.Text,@"^\s*inside\s*$",RegexOptions.IgnoreCase));
                if(inside is null&&!Regex.IsMatch(header.Text,@"\breviewed\s+inside\b",RegexOptions.IgnoreCase))continue;
                int start=(inside??header).Y+(inside??header).Height;
                var small=lines.Where(n=>n.Y>=start&&n.Y-start<pass.Height*.42&&n.Height<=pass.Height*.022&&n.Confidence>=75&&n.X<header.X+header.Width).OrderBy(n=>n.Y).ToArray();
                foreach(var seed in small.Where(n=>n.Y-start<pass.Height*.09).Take(4))
                {
                    var column=small.Where(n=>n.Y>=seed.Y&&Math.Abs(n.X-seed.X)<=pass.Width*.018).OrderBy(n=>n.Y).ToArray();
                    var run=new List<LayoutLine>();
                    foreach(var entry in column)
                    {
                        if(run.Count>0&&entry.Y-(run[^1].Y+run[^1].Height)>pass.Height*.022)break;
                        if(Resolve(Clean(entry.Text)) is null&&!TitleLike(Clean(entry.Text)))break;
                        run.Add(entry);
                    }
                    if(run.Count>=3)foreach(var entry in run.Take(30))Candidate(entry.Text,entry,true,0);
                }
            }
            foreach(var l in lines)
            {
                string printed=Clean(l.Text);
                bool labelled=Regex.IsMatch(l.Text,@"\b(?:reviewed|previewed|solution|walkthrough|strategy guide)\s*[!:.]*$",RegexOptions.IgnoreCase);
                if(Resolve(printed) is not null && (labelled || l.Height>=median*1.15)) Candidate(printed,l,labelled,l.Height);
                // Join stacked logos even when the first word is much smaller than the second.
                foreach(var next in lines.Where(n=>Nearby(l,n)).OrderBy(n=>n.Y).Take(4))
                {
                    string joined=Clean(l.Text+" "+next.Text);
                    if(Resolve(joined) is not null && (Resolve(printed) is null || Resolve(Clean(next.Text)) is null) && Math.Max(l.Height,next.Height)>=median*1.15)
                        Candidate(joined,l,labelled||Regex.IsMatch(next.Text,@"\b(?:reviewed|solution|walkthrough)\b",RegexOptions.IgnoreCase),Math.Max(l.Height,next.Height));
                }
                var header=Regex.Match(l.Text,@"^\s*(?:reviewed(?:\s+inside)?|reviews?|previewed|previews?|featured games?)\s*[:!]?\s*(?<tail>.*)$",RegexOptions.IgnoreCase);
                if(!header.Success)continue;
                if(header.Groups["tail"].Value.Trim().Length>0 && Normalize(header.Groups["tail"].Value)!="inside") Candidate(header.Groups["tail"].Value,l,true,0);
                // Follow the heading's column, not global Tesseract reading order.
                var list=lines.Where(n=>n.Y>l.Y && n.Y-l.Y<pass.Height*.46 && n.X>=l.X-l.Height*1.5 && n.X<l.X+Math.Max(l.Width,l.Height*7) && n.Height<=Math.Max(l.Height*1.5,median*1.6)).OrderBy(n=>n.Y).ToArray();
                var prev=l;
                for(int j=0;j<Math.Min(30,list.Length);j++)
                {
                    var entry=list[j];
                    if(entry.Y-(prev.Y+prev.Height)>Math.Max(prev.Height,entry.Height)*2.8)break;
                    string value=entry.Text.Trim();
                    if(j+1<list.Length && Nearby(entry,list[j+1]))
                    {
                        var continuation=list[j+1];
                        string joined=value+" "+continuation.Text.Trim();
                        if((value.EndsWith(':') || Regex.IsMatch(continuation.Text,@"\b(?:edition|version)\b",RegexOptions.IgnoreCase) || (Resolve(value) is null&&Resolve(joined) is not null)) && (Resolve(joined) is not null || TitleLike(joined)))
                        { value=joined;prev=continuation;j++; }
                        else prev=entry;
                    }
                    else prev=entry;
                    if(!TitleLike(Clean(value)) && Resolve(Clean(value)) is null)break;
                    Candidate(value,entry,true,0);
                }
            }
        }
        // Suppress shorter versions when the same printed headline has a longer title.
        var distinct=candidates.Where(c=>!candidates.Any(d=>Normalize(d.Printed).StartsWith(Normalize(c.Printed)+" ",StringComparison.Ordinal)&&Normalize(d.Name)!=Normalize(c.Name))).GroupBy(c=>ListingKey(c.Name)).Select(g=>g.OrderByDescending(c=>c.Editorial).ThenByDescending(c=>c.Size).First()).ToArray();
        void Add(string field,string name,string evidence,string confidence)
        {
            if(!suggestions.Any(s=>s.Field==field&&s.Page==1&&ListingKey(s.Value)==ListingKey(name)&&s.Confidence==confidence)) suggestions.Add(new(field,name,1,evidence,confidence));
        }
        foreach(var c in distinct.Where(c=>c.Editorial))
            Add("featuredGames",c.Name,"Cover editorial: "+c.Evidence,c.Resolved?"Cover editorial listing":"Cover editorial listing — catalog unresolved");
        // Reconnect a large partial logo to a complete title printed elsewhere on the cover.
        // A featured title alone is not sufficient: require a unique word and local strategy text.
        var fullTitles = suggestions.Where(x=>x.Page==1 && x.Field=="featuredGames").Select(x=>x.Value)
            .Concat(distinct.Where(x=>x.Editorial).Select(x=>x.Name)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var pass in passes.Where(p=>p.Width>0&&p.Height>0))
        {
            var lines=pass.Lines.Where(l=>l.Height>0&&l.Confidence>=35).ToArray();
            foreach(var l in lines.Where(l=>l.Height>=pass.Height*.035))
            {
                var word=Normalize(Clean(l.Text));
                if(word.Length<5 || word.Contains(' '))continue;
                var matches=fullTitles.Where(t=>Normalize(t).Split(' ').Contains(word) && Normalize(t).Contains(' ')).ToArray();
                bool strategy=lines.Any(n=>n.Y>=l.Y-l.Height && n.Y<=l.Y+l.Height*4 &&
                    Math.Min(n.X+n.Width,l.X+l.Width)>Math.Max(n.X,l.X) && Regex.IsMatch(n.Text,@"\b(?:solution|walk[ -]?through|exclusive maps|strategy guide|play guide)\b",RegexOptions.IgnoreCase));
                Diagnostic(word,Geometry(l,pass)+$" | local matches: {string.Join(", ",matches)} | strategy context: {strategy}", "Prominent fragment");
                if(strategy && !Regex.IsMatch(word,@"^(?:solution|walkthrough|exclusive|strategy|reviewed|previewed|definitive|magazine)$",RegexOptions.IgnoreCase))
                    suggestions.Add(new("coverFragment",word,1,l.Text+Geometry(l,pass),"Prominent fragment with nearby strategy text"));
                if(matches.Length!=1 || !ValidCoverSubject(matches[0],item.MagazineTitle) || !strategy)continue;
                candidates.Add((matches[0],matches[0],l.Text+Geometry(l,pass)+" | Full title independently detected on cover | Nearby strategy headline",(double)l.Height/pass.Height*1.5,true,true));
            }
        }
        // Recover a damaged two-word logo only when two distinct title tokens
        // are independently supported in one spatial cluster near a strategy label.
        // Review-list membership alone never establishes the main cover subject.
        foreach(var pass in passes.Where(p=>p.Width>0&&p.Height>0))
        {
            var lines=pass.Lines.Where(l=>l.Height>0&&l.Confidence>=55).ToArray();
            foreach(var strategy in lines.Where(l=>Regex.IsMatch(l.Text,@"\b(?:solution|walk[ -]?through|strategy guide|exclusive maps|play guide)\b",RegexOptions.IgnoreCase)))
            {
                var nearby=lines.Where(l=>l!=strategy && l.Y<strategy.Y && strategy.Y-l.Y<=pass.Height*.25 && l.Height>=pass.Height*.022 && l.X+l.Width>=strategy.X-pass.Width*.35 && l.X<=strategy.X+strategy.Width+pass.Width*.05).ToArray();
                bool Fragment(string fragment,string word)=>fragment.Length>=3 && fragment.Length<=word.Length && (word.StartsWith(fragment,StringComparison.Ordinal)||word.EndsWith(fragment,StringComparison.Ordinal));
                var matches=new List<(string Title,LayoutLine A,LayoutLine B)>();
                foreach(var title in fullTitles.Where(t=>ValidCoverSubject(t,item.MagazineTitle)))
                {
                    var words=Normalize(title).Split(' ');
                    if(words.Length!=2||words.Any(w=>w.Length<4))continue;
                    foreach(var a in nearby)
                    foreach(var b in nearby.Where(l=>l!=a))
                    {
                        var fa=Normalize(a.Text);var fb=Normalize(b.Text);
                        // One token may be a short prefix only if the other is a
                        // distinctive >=3-character fragment and both boxes are prominent.
                        bool shortPrefix=fb.Length==2 && words[1].Length>=5 && words[1].StartsWith(fb,StringComparison.Ordinal) && b.Height>=pass.Height*.035;
                        if(!Fragment(fa,words[0])||!(Fragment(fb,words[1])||shortPrefix))continue;
                        if(Math.Abs(a.Y-b.Y)>pass.Height*.13 || Math.Abs(a.X+a.Width/2.0-(b.X+b.Width/2.0))>pass.Width*.55)continue;
                        if(a.Height<pass.Height*.025||b.Height<pass.Height*.035)continue;
                        matches.Add((title,a,b));
                    }
                }
                var unique=matches.GroupBy(m=>ListingKey(m.Title)).ToArray();
                if(unique.Length!=1)continue;
                var m=unique[0].OrderByDescending(m=>m.A.Height+m.B.Height).First();
                var detail=$"Corroborated logo fragments '{m.A.Text}'{Geometry(m.A,pass)} + '{m.B.Text}'{Geometry(m.B,pass)} | strategy label '{strategy.Text}' | complete title independently printed on cover";
                candidates.Add((m.Title,m.Title,detail,(double)Math.Max(m.A.Height,m.B.Height)/pass.Height*1.5,true,true));
                Diagnostic(m.Title,detail,"Two-token cover fragment match");
            }
        }
        var ranked=candidates.Where(c=>c.Resolved&&c.Size>0&&(distinct.Any(d=>ListingKey(d.Name)==ListingKey(c.Name)) || fullTitles.Any(t=>ListingKey(t)==ListingKey(c.Name)))).GroupBy(c=>ListingKey(c.Name)).Select(g=>g.OrderByDescending(c=>c.Size).First()).OrderByDescending(c=>c.Size).ToArray();
        foreach(var c in candidates.Take(200)) Diagnostic(c.Name,c.Evidence+$" | normalized size {c.Size:F4} | editorial {c.Editorial} | catalog resolved {c.Resolved}","Cover candidate");
        Diagnostic("Cover ranking",string.Join("; ",ranked.Select(c=>$"{c.Name}: {c.Size:F4}")),ranked.Length==0?"No ranked candidate":ranked.Length==1||ranked[0].Size>=ranked[1].Size*1.6?"Dominant candidate":"Ambiguous size ranking");
        if(ranked.Length>0 && (ranked.Length==1 || ranked[0].Size>=ranked[1].Size*1.6))
        {
            var primary=ranked[0];
            Add("coverSubject",primary.Name,"Dominant cover headline: "+primary.Evidence,"Dominant cover headline — verify");
            Add("featuredGames",primary.Name,"Dominant cover headline: "+primary.Evidence,"Cover story — verify");
        }
    }
    private static string ExcludedPageReason(string text,int page,int total)
    {
        if (page >= Math.Max(2,total-2) && Regex.IsMatch(text,@"\b(?:retromags|preservation project|scanned by|digitized by)\b",RegexOptions.IgnoreCase) &&
            Regex.IsMatch(text,@"\b(?:scan(?:ned|ning|s)?|archive|preserv(?:e|ation)|download|digitiz(?:ed|ation)|magazines)\b",RegexOptions.IgnoreCase)) return "Archive preservation notice";
        int prices=Regex.Matches(text,@"(?:£|\$|€)\s*\d{1,5}[.,]\d{2}\b").Count;
        int promos=Regex.Matches(text,@"\b(?:playable\s+demo|cover\s*[- ]?disc|inside (?:we|is|you)|(?:full|exclusive)\s+(?:review|preview|solution)|(?:we|we've)\s+(?:review|preview)|previous issues?)\b",RegexOptions.IgnoreCase).Count;
        bool order=Regex.IsMatch(text,@"\b(?:back\s+issues?|past\s+issues?|previous\s+issues?|order\s+form|order\s+(?:your|these|past|back)\s+issues?)\b",RegexOptions.IgnoreCase);
        // A contents entry called Back Issues is not itself an order form. Require corroboration.
        if ((prices>=5 && promos>=3) || (order && prices>=3 && promos>=1) || (order && prices>=2 && Regex.IsMatch(text,@"\b(?:cheque|postal order|postage|payment|credit card|order form)\b",RegexOptions.IgnoreCase)))
            return "Probable back-issue/order catalogue (prices and promotional listings)";
        return "";
    }
    // A second general index layout: centered page numbers above uppercase headings.
    // Infer columns from repeated heading centers, never from publication names or fixed offsets.
    private static bool CenteredIndexHeading(LayoutLine l)
    {
        var letters=l.Text.Where(char.IsLetter).ToArray();
        return l.Height>0 && ((l.Confidence>=30 && (IndexSection(l.Text)||IndexDepartment(l.Text))) ||
            (l.Confidence>=35 && letters.Length>=4 && letters.Count(char.IsUpper)>=letters.Length*.80 && Words.Matches(l.Text).Count<=12));
    }
    private static bool IndexSection(string value) => Regex.IsMatch(value.Trim(),@"^(?:table of contents|contents|features?|reviews?|previews?|columns?|departments?|regulars?|solutions?|walkthroughs?|strategy guides?|news|letters|editorial|cheats?|tips|codes|hardware|directory|index)\s*[:.!]?\s*$",RegexOptions.IgnoreCase);
    private static bool IndexDepartment(string value) => Regex.IsMatch(value.Trim(),@"^(?:editor['’]?s notes|letter from the editor|editorial|gamebytes|directory|(?:pc |industry |game )?news|back issues?|order form|subscriptions?)\s*[:.!]?\s*$",RegexOptions.IgnoreCase);
    private static List<List<LayoutLine>> CenteredIndexColumns(PageLayout layout)
    {
        var groups=new List<List<LayoutLine>>();
        if(layout.Width<=0||layout.Height<=0)return groups;
        double Center(LayoutLine l)=>l.X+l.Width/2.0;
        foreach(var l in layout.Lines.Where(CenteredIndexHeading).OrderBy(Center))
        {
            var group=groups.LastOrDefault();
            if(group is null||Math.Abs(group.Average(Center)-Center(l))>Math.Max(10,layout.Width*.025))groups.Add(new List<LayoutLine>{l});
            else group.Add(l);
        }
        return groups.Where(g=>g.Count>=3).ToList();
    }
    private static bool LooksLikeCenteredIndex(PageLayout layout)
    {
        var columns=CenteredIndexColumns(layout);
        if(columns.Count<2||columns.Sum(g=>g.Count)<12)return false;
        int sections=columns.Sum(g=>g.Count(l=>IndexSection(l.Text)));
        int numbers=layout.Lines.Count(l=>l.Height>0&&Regex.IsMatch(l.Text.Trim(),@"^\d{1,3}$")&&columns.Any(g=>Math.Abs(g.Average(h=>h.X+h.Width/2.0)-(l.X+l.Width/2.0))<=Math.Max(10,layout.Width*.035)&&g.Any(h=>h.Y>l.Y&&h.Y-l.Y<=Math.Max(h.Height,l.Height)*3)));
        return sections>=2&&numbers>=4;
    }
    private static string IndexTitleKey(string value)
    {
        string key=Normalize(value);
        // Only complete-title equivalents: ordinal spelling and leading/sort articles.
        key=Regex.Replace(key,@"^(?:the|a|an)\s+|\s+(?:the|a|an)$", "");
        var ordinals=new Dictionary<string,string>(StringComparer.Ordinal) {
            ["first"]="1st",["second"]="2nd",["third"]="3rd",["fourth"]="4th",["fifth"]="5th",["sixth"]="6th",["seventh"]="7th",["eighth"]="8th",["ninth"]="9th",["tenth"]="10th"
        };
        key=Regex.Replace(key,@"\b(?:first|second|third|fourth|fifth|sixth|seventh|eighth|ninth|tenth)\b",m=>ordinals[m.Value]);
        key=Regex.Replace(key,@"\b(\d+)\s+(st|nd|rd|th)\b", "$1$2");
        return ListingKey(key);
    }
    private static void AnalyzeCenteredIndex(PageLayout layout,Dictionary<string,CatalogTitle> dictionary,int page,List<Suggestion> suggestions)
    {
        var columns=CenteredIndexColumns(layout);
        // Index resolution must not depend on finding every printed string as an exact catalog key.
        // Only retain unambiguous full-title equivalents, preserving the catalog display spelling.
        var indexNames=dictionary.Where(kv=>!kv.Key.StartsWith("\0",StringComparison.Ordinal))
            .SelectMany(kv=>new[]{(Key:IndexTitleKey(kv.Key),Title:kv.Value.Display),(Key:IndexTitleKey(kv.Value.Display),Title:kv.Value.Display)})
            .Where(x=>x.Key.Length>=3).GroupBy(x=>x.Key).ToDictionary(g=>g.Key,g=>g.Select(x=>x.Title).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),StringComparer.Ordinal);
        string section="";
        void Add(string field,string value,string evidence,string confidence)
        {
            if(value.Length==0||suggestions.Any(s=>s.Page==page&&s.Field==field&&ListingKey(s.Value)==ListingKey(value)))return;
            suggestions.Add(new(field,value,page,evidence,confidence));
        }
        foreach(var column in columns)
        {
            var headings=column.OrderBy(l=>l.Y).ToArray();
            double center=column.Average(l=>l.X+l.Width/2.0);
            for(int i=0;i<headings.Length;i++)
            {
                var first=headings[i];
                if(IndexSection(first.Text)) {section=first.Text.Trim();Add("specialFeatures",section,first.Text+Geometry(first,layout),"Centered index section");continue;}
                if(IndexDepartment(first.Text)){Add("specialFeatures",first.Text.Trim(),first.Text+Geometry(first,layout),"Centered index department");continue;}
                var fragments=new List<LayoutLine>{first};
                // Adjacent uppercase lines of comparable size form one heading. A normal
                // gap between entries (or a small page-number line) terminates the heading.
                while(i+1<headings.Length)
                {
                    var prev=fragments[^1];var next=headings[i+1];
                    if(IndexSection(next.Text)||IndexDepartment(next.Text)||next.Y-prev.Y>Math.Max(prev.Height,next.Height)*1.85||next.Y<=prev.Y||next.Height>prev.Height*1.6||prev.Height>next.Height*1.6)break;
                    if(layout.Lines.Any(l=>l.Y>prev.Y&&l.Y<next.Y&&Math.Abs(l.X+l.Width/2.0-center)<=Math.Max(10,layout.Width*.035)&&Regex.IsMatch(l.Text.Trim(),@"^\d{1,3}$")))break;
                    fragments.Add(next);i++;
                }
                string printed=string.Join(" ",fragments.Select(l=>l.Text)).Trim(' ','.',',','‘','’','"');
                if(!Regex.IsMatch(section,@"^(?:features?|reviews?|previews?|solutions?|walkthroughs?|strategy guides?|columns?)$",RegexOptions.IgnoreCase))continue;
                bool columnFeature=Regex.IsMatch(section,@"^columns?$",RegexOptions.IgnoreCase);
                // Editorial wrappers are not part of the game title. Keep the original
                // complete heading in evidence and never substitute a partial game hit.
                string title=Regex.Replace(printed,@"^.*?\b(?:the making of|sneak preview\s*:|(?:game |exclusive )?preview\s*:|review\s*:|walkthrough\s*:|solution\s*:)\s*", "",RegexOptions.IgnoreCase).Trim(' ','.',',','‘','’','"');
                title=Regex.Replace(title,@"(?<=\s)[‘’](?=\p{L})"," ").Trim();
                // Complete heading matching only. A match to one word inside a long
                // heading must never replace that heading's actual title.
                var exact=TryNumberedTitle(dictionary,title,out var direct)?new[]{direct.Display}:Array.Empty<string>();
                if(exact.Length==0&&dictionary.TryGetValue("\0"+ListingKey(title),out var spelling))exact=new[]{spelling.Display};
                if(exact.Length==0&&indexNames.TryGetValue(IndexTitleKey(title),out var equivalents))exact=equivalents;
                bool fuzzy=false;
                if(exact.Length==0&&title.IndexOfAny([',',';'])<0){exact=FindGames(dictionary,title,true,true,true).Where(h=>h.Fuzzy).Select(h=>h.Game).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();fuzzy=exact.Length>0;}
                if(!columnFeature)
                {
                    string status=exact.Length==1?(fuzzy?"Possible OCR correction: ":"Catalog match: ")+exact[0]:exact.Length>1?"Ambiguous catalog title: "+string.Join(" / ",exact.Take(5)):"No complete catalog match";
                    Add("indexEntry",title,printed+Geometry(first,layout),status);
                    bool mainFeature=Regex.IsMatch(section,@"^features?$",RegexOptions.IgnoreCase)&&Regex.IsMatch(printed,@"\bthe making of\b",RegexOptions.IgnoreCase);
                    // A major feature is not proof of cover artwork. Surface a weak,
                    // unchecked cover proposal only; explicit cover evidence still wins.
                    if(mainFeature&&exact.Length==1&&!fuzzy&&fragments.All(l=>l.Confidence>=40))
                        Add("coverSubject",exact[0],printed+Geometry(first,layout),"Contents main feature — cover unverified");
                }
                if(exact.Length==1&&!columnFeature&&!fuzzy&&fragments.All(l=>l.Confidence>=40))
                {
                    Add("featuredGames",exact[0],printed+Geometry(first,layout),"Centered index full heading — review");continue;
                }
                if(columnFeature)
                {
                    Add("specialFeatures",printed,printed+Geometry(first,layout),"Centered index column heading");
                    // A column title may not be the game. Inspect the short description
                    // and surface catalog matches as possible coverage, not automatic features.
                    int bottom=i+1<headings.Length?headings[i+1].Y:first.Y+Math.Max(first.Height*7,layout.Height/12);
                    var body=layout.Lines.Where(l=>l.Y>fragments[^1].Y&&l.Y<bottom&&!CenteredIndexHeading(l)&&Math.Abs(l.X+l.Width/2.0-center)<layout.Width*.065&&Words.Matches(l.Text).Count>=2).OrderBy(l=>l.Y).Select(l=>l.Text);
                    string description=string.Join(" ",body);
                    foreach(var hit in FindGames(dictionary,description,false,false,true).Where(h=>!h.Fuzzy&&Words.Matches(h.Game).Count>=2))Add("articleCandidate",hit.Game,printed+" — "+description+Geometry(first,layout),"Centered index column description — verify coverage");
                    continue;
                }
                // Editorial coverage and catalog identity are independent. A legible
                // complete heading in a recognized feature/review index is useful
                // metadata even when no catalog title resolves. Never substitute a
                // fuzzy match, choose among ambiguous titles, or auto-save it.
                bool plausiblePrintedTitle=LooksLikeUnresolvedTitle(title)||(title.Length>=3&&title.Length<=85&&Words.Matches(title).Count==1&&title.All(char.IsLetter)&&!IndexSection(title)&&!IndexDepartment(title));
                bool clearPrintedTitle=exact.Length==0 && !fuzzy && plausiblePrintedTitle && fragments.All(l=>l.Confidence>=65)
                    && title.IndexOfAny([',',';','|'])<0 && ValidCoverSubject(title,"") && !IndexSection(title) && !IndexDepartment(title);
                if(clearPrintedTitle)
                {
                    // Preserve printed spelling; do not fabricate canonical capitalization or identity.
                    Add("featuredGames",title,printed+Geometry(first,layout),"Printed index feature — catalog unresolved; review before applying");
                    Add("unresolvedGame",title,printed+Geometry(first,layout),"Catalog unresolved — printed index supports featured coverage");
                    if(Regex.IsMatch(section,@"^features?$",RegexOptions.IgnoreCase)&&Regex.IsMatch(printed,@"\bthe making of\b",RegexOptions.IgnoreCase))
                        Add("coverSubject",title,printed+Geometry(first,layout),"Contents main feature — cover unverified");
                }
                else if(exact.Length==1)Add("unresolvedGame",fuzzy?title:exact[0],printed+Geometry(first,layout),fuzzy?"Possible OCR correction — verify printed title and catalog candidate":"Low-confidence centered index title — verify full title");
                else if(plausiblePrintedTitle)
                    Add("unresolvedGame",title,printed+Geometry(first,layout),"Unresolved centered index title — verify full title");
            }
        }
    }
    private static bool LooksLikeGraphicalIndex(PageLayout layout,string text)
    {
        if(layout.Width<=0||layout.Height<=0) return false;
        int references=layout.Lines.Count(l=>Regex.IsMatch(l.Text,@"(?:\.{1,}\s*\d{1,3}|\s+\d{1,3})\s*[.!]?\s*$") && Words.Matches(l.Text).Count<=9 && Regex.IsMatch(l.Text,@"\p{L}"));
        int descriptions=Regex.Matches(text,@"\b(?:review|preview|full solution|fighting guide|mapped solution|walkthrough|turn to page|guide)\b",RegexOptions.IgnoreCase).Count;
        int sections=layout.Lines.Count(l=>Regex.IsMatch(l.Text,@"^\s*(?:reviews?|previews?|solutions?|walkthroughs?|features?|regulars?|departments?|contents)\s*[.!:]?\s*$",RegexOptions.IgnoreCase));
        return (references>=3 && descriptions>=2) || (references>=2 && sections>=2);
    }
    private static void AnalyzeNumberedFeatures(PageLayout layout,Dictionary<string,CatalogTitle> dictionary,int page,List<Suggestion> suggestions)
    {
        if(layout.Width<=0||layout.Height<=0)return;
        var heights=layout.Lines.Where(l=>l.Height>0&&l.Confidence>=45).Select(l=>l.Height).OrderBy(v=>v).ToArray();
        if(heights.Length==0)return;
        int median=heights[heights.Length/2];
        foreach(var last in layout.Lines.Where(l=>l.Height>=median*1.2&&l.Confidence>=40))
        {
            var m=Regex.Match(last.Text,@"^(?<title>.*?)(?:\s*\.{1,}\s*|\s+)(?<page>\d{1,3})\s*[.!]?\s*$");
            if(!m.Success)continue;
            string suffix=m.Groups["title"].Value.Trim(' ','.',':',';','?','|');
            if(suffix.Length==0||Words.Matches(suffix).Count>9)continue;
            // Connect headings by geometry, not OCR traversal order (columns may interleave).
            var prev=layout.Lines.Where(l=>l!=last&&l.Height>0&&l.Confidence>=40&&l.Y<last.Y&&last.Y-l.Y<=Math.Max(last.Height,l.Height)*2.6&&Math.Abs(l.X-last.X)<=Math.Max(last.Height,l.Height)*2&&l.Height>=last.Height*.6&&l.Height<=last.Height*1.7&&Words.Matches(l.Text).Count<=8&&!Regex.IsMatch(l.Text,@"\d{1,3}\s*$"))
                .OrderBy(l=>last.Y-l.Y).FirstOrDefault();
            var candidates=new List<string>();
            if(prev is not null)candidates.Add(prev.Text.Trim(' ','.',':',';','?','|')+" "+suffix);
            candidates.Add(suffix);
            foreach(var printed in candidates)
            {
                if(Regex.IsMatch(printed,@"\b(?:back issues?|disc info|regulars?|departments?|contents|letters|editorial|reviews?|previews?|solutions?|walkthroughs?|order form|subscriptions?)\b",RegexOptions.IgnoreCase))continue;
                var hits=FindGames(dictionary,printed,true,true,true).Where(h=>h.Whole&&!h.Fuzzy).ToArray();
                if(hits.Length!=1)continue;
                string title=hits[0].Game;
                if(!suggestions.Any(s=>s.Field=="featuredGames"&&s.Page==page&&ListingKey(s.Value)==ListingKey(title)))
                    suggestions.Add(new("featuredGames",title,page,printed+" (printed page "+m.Groups["page"].Value+")"+Geometry(last,layout),"Numbered contents feature — review"));
                break;
            }
            // A complete printed heading that cannot resolve remains a review-only candidate.
            if(!candidates.Any(c=>FindGames(dictionary,c,true,true,true).Any(h=>h.Whole&&!h.Fuzzy)))
            {
                var printed=candidates.FirstOrDefault(c=>LooksLikeUnresolvedTitle(c)&&!Regex.IsMatch(c,@"\b(?:back issues?|disc info|regulars?|solutions?|walkthroughs?|order form|subscriptions?)\b",RegexOptions.IgnoreCase));
                if(printed is not null&&!suggestions.Any(s=>s.Field=="unresolvedGame"&&s.Page==page&&ListingKey(s.Value)==ListingKey(printed)))
                    suggestions.Add(new("unresolvedGame",printed,page,printed+Geometry(last,layout),"Unresolved numbered contents feature — verify title"));
            }
        }
    }
    private static void AnalyzeEditorialIntroduction(PageLayout layout,Dictionary<string,CatalogTitle> dictionary,int page,List<Suggestion> suggestions)
    {
        // OCR often splits multi-word titles over body lines. Rejoin only within the same paragraph.
        foreach(var block in layout.Lines.GroupBy(l=>(l.Block,l.Paragraph)))
        {
            var ordered=block.OrderBy(l=>l.Y).ThenBy(l=>l.X).ToArray();
            if(ordered.Length<2 || ordered.Any(l=>l.Height<=0))continue;
            var text=string.Join(" ",ordered.Select(l=>l.Text));
            if(text.Length>2200||ArticleNoise(text)||Regex.IsMatch(text,@"\b(?:next|previous|back|past)\s+issues?\b",RegexOptions.IgnoreCase))continue;
            bool intro=Regex.IsMatch(text,@"\b(?:inside this issue|in this issue|this month|this issue|this edition)\b",RegexOptions.IgnoreCase);
            bool coverage=Regex.IsMatch(text,@"\b(?:review|preview|sneak (?:look|peek)|full solutions?|(?:fighting|strategy|game) guide|walkthrough|playab[li]e demo)\b",RegexOptions.IgnoreCase);
            if(!intro||!coverage)continue;
            // Sentence-level evidence prevents unrelated sidebar or masthead names being promoted.
            bool previousDemo=false;
            foreach(var sentence in Regex.Split(text,@"(?<=[.!?])\s+"))
            {
                bool demo=Regex.IsMatch(sentence,@"\bplayab[li]e demo\b",RegexOptions.IgnoreCase);
                bool continued=previousDemo&&Regex.IsMatch(sentence,@"^\s*(?:this time|this month|it(?:['’]s| is)|the demo)\b",RegexOptions.IgnoreCase);
                previousDemo=demo;
                if((!continued&&!Regex.IsMatch(sentence,@"\b(?:review|preview|sneak (?:look|peek)|full solutions?|(?:fighting|strategy|game) guide|walkthrough|playab[li]e demo)\b",RegexOptions.IgnoreCase))||ArticleNoise(sentence))continue;
                foreach(var hit in FindGames(dictionary,sentence,true,false,true).Where(h=>!h.Fuzzy))
                {
                    if(Words.Matches(hit.Game).Count==1&&!Regex.IsMatch(sentence,@"\b(?:review|preview|guide|solution)\s+(?:of|for|to|on)\s+"+Regex.Escape(hit.Game)+@"\b",RegexOptions.IgnoreCase))continue;
                    if(!suggestions.Any(s=>s.Field=="featuredGames"&&s.Page==page&&ListingKey(s.Value)==ListingKey(hit.Game)))
                        suggestions.Add(new("featuredGames",hit.Game,page,sentence[..Math.Min(350,sentence.Length)],"Current-issue editorial introduction — review"));
                }
            }
        }
    }
    private static bool ValidCoverSubject(string value,string? publication)
    {
        var key=Normalize(value);
        return Regex.IsMatch(key,@"\p{L}") && !Regex.IsMatch(key,$@"^(?:(?:{Months})\s+)?(?:19|20)\d{{2}}(?:\s+(?:19|20)\d{{2}})?$",RegexOptions.IgnoreCase) && key.Length>=3 && key.Length<=100 && key!=Normalize(publication??"") && !Regex.IsMatch(key,@"^(?:(?:issue|volume|vol|number|no)(?:\s+\d+)?|magazine|cover|cover story|cover feature|featured games?|features?|special edition|contents|table of contents|reviews?|previews?|news|unknown|unspecified|not identified|none|n a)$",RegexOptions.IgnoreCase);
    }
    private static bool LooksLikeUnresolvedTitle(string value)
    {
        if(value.Length<4 || value.Length>85 || !char.IsLetter(value[0])) return false;
        var words=Words.Matches(value).Select(m=>m.Value).ToArray();
        if(words.Length<1 || words.Length>9 || !words.Any(w=>char.IsUpper(w[0]))) return false;
        if(Regex.IsMatch(value,@"\b(?:contents|reviews?|previews?|features?|departments?|magazine|publisher|presents|includes?|exclusive|subscribe|editor|copyright|news|tips|codes|page|coming soon|coming next|available|and more|the latest)\b",RegexOptions.IgnoreCase)) return false;
        if(Platforms.Any(p=>Regex.IsMatch(value,p.Pattern,RegexOptions.IgnoreCase))) return false;
        return words.Length==1 ? words[0].Length>=5 : words.Count(w=>char.IsUpper(w[0])||char.IsDigit(w[0])||Regex.IsMatch(w,@"^(?:a|an|the|of|and|in|on|to|for|vs)$",RegexOptions.IgnoreCase))>=words.Length-1;
    }
    private static bool ArticleNoise(string text) => Regex.IsMatch(text,@"\b(?:copyright|all rights[. ]+reserved|trademarks?|licensed (?:by|from|to)|official seals?|order now|mail order|special offer|grand prize|sweepstakes|send (?:your )?entries|no purchase necessary|back issues?|previous issues?|next issue|price list|third .* movie)\b|©",RegexOptions.IgnoreCase) || RetailMarkers(text)>=2;
    private static int EditorialSignals(string text)
    {
        // Independent evidence categories; a repeated game name alone never establishes a feature.
        string[] signals=[@"\b(?:graphics|animation|visuals|sprites|scrolling|sound(?:track| effects)?|music)\b",@"\b(?:gameplay|controls?|controller|joystick|difficulty|challenge|playability|responsive)\b",@"\b(?:levels?|stages?|rounds?|opponents?|characters?|weapons?|missions?|courses?|teams?)\b",@"\b(?:review|rating|rated|score|verdict|recommended|overall|pros and cons)\b",@"\b(?:you (?:can|must|have to|will)|players? (?:can|must|choose|control)|the player|game (?:offers|features|provides|has|contains))\b", @"\b(?:select|choose|move|jump|shoot|pass|tackle|drive|swing|aim|steer|defend|attack|race|score)\w*\b", @"\b(?:excellent|impressive|enjoyable|frustrating|realistic|challenging|addictive|unfortunately|however|worth playing)\b"];
        return signals.Count(p=>Regex.IsMatch(text,p,RegexOptions.IgnoreCase));
    }
    private static void AnalyzeArticleRegions(PageLayout layout, Dictionary<string,CatalogTitle> dictionary,int page,List<Suggestion> suggestions)
    {
        // Each anchor gets a column-local region, with neighboring blocks included only if aligned.
        // These are hypotheses, not reconstructed articles; page breaks are joined in the scan review layer.
        var lines=layout.Lines.Where(l=>l.Height>0 && l.Confidence>=48 && !ArticleNoise(l.Text)).Select(l=>l with {Text=SequelLineText(dictionary,l,layout)}).ToArray();
        if(lines.Length==0) return;
        var pageText=string.Join(" ",lines.Select(l=>l.Text));
        if(Regex.IsMatch(pageText,@"\b(?:back issues?|mail order|price list|order form)\b",RegexOptions.IgnoreCase) && RetailMarkers(pageText)>=1) return;
        var heights=lines.Select(l=>l.Height).OrderBy(h=>h).ToArray();
        double median=heights[heights.Length/2];
        var hits=new Dictionary<string,List<(GameHit Hit,LayoutLine Line)>>(StringComparer.Ordinal);
        foreach(var line in lines)
        {
            bool headline=line.Height>=median*1.30 && Words.Matches(line.Text).Count<=12;
            bool context=EditorialSignals(line.Text)>0 || Regex.IsMatch(line.Text,@"\b(?:game|review|preview|version|released?|play(?:ed|ing)?)\b",RegexOptions.IgnoreCase);
            foreach(var hit in FindGames(dictionary,line.Text,headline,false,context).Where(h=>!h.Fuzzy))
            {
                var key=ListingKey(hit.Game); if(!hits.TryGetValue(key,out var list)) hits[key]=list=new();
                list.Add((hit,line));
            }
        }
        foreach(var (key,occurrences) in hits)
        {
            foreach(var occurrence in occurrences.OrderByDescending(o=>o.Line.Height).Take(4))
            {
                var anchor=occurrence.Line;
                // Join neighboring OCR blocks in the same column, stopping at large
                // vertical gaps, competing whole-title headings and section boundaries.
                var boundaries=lines.Where(l=>l != anchor && (Regex.IsMatch(l.Text,@"^\s*(?:contents|news|letters|advertisement|cheats?|tips(?: and tricks)?|passwords?|high scores?|release schedule|order form)\s*[:.!]?\s*$",RegexOptions.IgnoreCase) || (l.Height>=median*1.30 && hits.Any(kv=>kv.Key!=key && kv.Value.Any(o=>o.Line==l && o.Hit.Whole))))).ToHashSet();
                bool Boundary(LayoutLine l)=>boundaries.Contains(l);
                bool Aligned(LayoutLine a,LayoutLine b)
                {
                    int overlap=Math.Min(a.X+a.Width,b.X+b.Width)-Math.Max(a.X,b.X);
                    return overlap>=Math.Min(a.Width,b.Width)*.45 && (Math.Abs(a.X-b.X)<=layout.Width*.10 || a.Block==b.Block);
                }
                var connected=new HashSet<LayoutLine>{anchor};
                var pending=new Queue<LayoutLine>(); pending.Enqueue(anchor);
                while(pending.Count>0 && connected.Count<90)
                {
                    var a=pending.Dequeue();
                    foreach(var b in lines)
                    {
                        if(connected.Contains(b)||Boundary(b)||Math.Abs(b.Y-anchor.Y)>layout.Height*.38||!Aligned(a,b)) continue;
                        int gap=Math.Max(0,Math.Max(a.Y,b.Y)-Math.Min(a.Y+a.Height,b.Y+b.Height));
                        if(gap>Math.Max(median*2.8,Math.Max(a.Height,b.Height)*1.2)) continue;
                        // Do not cross a heading or a clearly different section in this column.
                        if(boundaries.Any(l=>l.Y>Math.Min(a.Y,b.Y)&&l.Y<Math.Max(a.Y,b.Y)&&Aligned(a,l))) continue;
                        connected.Add(b);pending.Enqueue(b);
                        if(connected.Count>=90) break;
                    }
                }
                bool SameRegion(LayoutLine l)=>connected.Contains(l);
                var region=connected.OrderBy(l=>l.Y).ThenBy(l=>l.X).ToArray();
                var context=string.Join(" ",region.Select(l=>l.Text));
                if(region.Length<3 || ArticleNoise(context) || Regex.IsMatch(context,@"\b(?:cheat codes?|passwords?|game genie|high scores?|release schedule|coming next issue|top \d+|letters to the editor|readers? poll)\b",RegexOptions.IgnoreCase)) continue;
                int words=Words.Matches(context).Count, signals=EditorialSignals(context);
                int repeats=occurrences.Select(o=>o.Line).Distinct().Count(SameRegion);
                bool heading=occurrence.Hit.Whole && anchor.Height>=median*1.30;
                int rivals=hits.Count(kv=>kv.Key!=key && kv.Value.Any(o=>SameRegion(o.Line)));
                bool focused=rivals<=1 && (heading||repeats>=2||rivals==0);
                if(!focused||words<30||signals<2) continue;
                var title=occurrence.Hit.Game;
                var detail=$"{anchor.Text}{Geometry(anchor,layout)}; connected article region: {words} words, {signals} editorial cues, {repeats} title lines, {rivals} other titles.";
                // A focused body paragraph can establish substantial coverage even if
                // a stylized headline was missed. Weak regions are only review evidence.
                bool substantial=(heading&&words>=45&&signals>=2)||(repeats>=2&&words>=55&&signals>=3)||(rivals==0&&words>=90&&signals>=3);
                // A short exclamatory heading can be an ordinary phrase rather than a game title.
                bool ambiguousHeadline=occurrence.Hit.Whole && Words.Matches(anchor.Text).Count<=3 && anchor.Text.TrimEnd().EndsWith('!') && repeats<2;
                if(ambiguousHeadline)substantial=false;
                if(substantial && !suggestions.Any(x=>x.Page==page&&x.Field=="featuredGames"&&ListingKey(x.Value)==key)) suggestions.Add(new("featuredGames",title,page,detail,"Connected article region — review substantial coverage"));
                else if(!suggestions.Any(x=>x.Page==page&&x.Field=="articleCandidate"&&ListingKey(x.Value)==key)) suggestions.Add(new("articleCandidate",title,page,detail,"Possible article coverage — review context"));
                // Platform association is local and independent of the magazine-wide list.
                foreach(var (pattern,name) in Platforms)
                {
                    var platformLine=region.FirstOrDefault(l=>Regex.IsMatch(l.Text,pattern,RegexOptions.IgnoreCase) && (name!="Arcade"||ArcadePlatformEvidence(l.Text,false)) && (Regex.IsMatch(l.Text,@"\b(?:platform|version|for|system|cartridge)\b|[:—]",RegexOptions.IgnoreCase)||l.Text.Trim().Equals(name,StringComparison.OrdinalIgnoreCase)));
                    if(platformLine is null) continue;
                    if(!suggestions.Any(x=>x.Page==page&&x.Field=="gamePlatformEvidence"&&ListingKey(x.Value)==key&&x.Evidence.StartsWith(name+" — ",StringComparison.Ordinal))) suggestions.Add(new("gamePlatformEvidence",title,page,name+" — "+platformLine.Text+Geometry(platformLine,layout),"Local article platform candidate"));
                    if(!suggestions.Any(x=>x.Page==page&&x.Field=="featuredPlatforms"&&x.Value==name)) suggestions.Add(new("featuredPlatforms",name,page,platformLine.Text+Geometry(platformLine,layout),"Local editorial platform evidence"));
                }
            }
        }
    }
    private sealed record LayoutLine(string Text, int Block, int Paragraph, int X, int Y, int Width, int Height, double Confidence);
    private sealed record PageLayout(int Width, int Height, List<LayoutLine> Lines);
    // Explicit editorial callouts may print the game in small type, independently of logo size.
    private static void AnalyzeExplicitCoverCallouts(LibraryItem item, Dictionary<string,CatalogTitle> dictionary, LayoutLine[] lines, int height, List<Suggestion> suggestions, string source)
    {
        if(height<=0)return;
        const string pattern=@"^\s*(?:(?<emphasis>massive|huge|major|exclusive|complete|full|in[- ]depth|special|world exclusive)\s+)?(?<title>.+?)\s+(?<kind>preview|review|solution|walk[ -]?through|strategy guide|play guide)\s*[!:.]*$";
        void Check(string text, LayoutLine first, LayoutLine last)
        {
            var m=Regex.Match(text,pattern,RegexOptions.IgnoreCase);
            if(!m.Success || ArticleNoise(text))return;
            var title=m.Groups["title"].Value.Trim(' ',':','!','—','-');
            if(!dictionary.TryGetValue(Normalize(title),out var match)&&!dictionary.TryGetValue("\0"+ListingKey(title),out match))return;
            if(!ValidCoverSubject(match.Display,item.MagazineTitle))return;
            var publication=Normalize(item.MagazineTitle??"");
            if(publication.Split(' ',StringSplitOptions.RemoveEmptyEntries).Contains(Normalize(match.Display)))return;
            var kind=m.Groups["kind"].Value.ToLowerInvariant();
            var evidence=$"{source} explicit cover callout '{text}' | editorial kind: {kind} | box {first.X},{first.Y},{first.Width},{first.Height}";
            void Add(string field,string value,string confidence)
            {
                if(!suggestions.Any(x=>x.Field==field&&x.Page==1&&ListingKey(x.Value)==ListingKey(value)&&x.Confidence==confidence))suggestions.Add(new(field,value,1,evidence,confidence));
            }
            Add("featuredGames",match.Display,"Explicit cover editorial callout");
            Add("coverSubject",match.Display,"Explicit cover editorial callout");
            // Only an emphatic, reasonably visible callout can outrank ordinary cover candidates.
            if(m.Groups["emphasis"].Success && Math.Max(first.Height,last.Height)>=height*.012)
                Add("coverSubject",match.Display,"Explicit prominent cover callout");
            Add("specialFeatures",kind=="preview"?"Previews":kind=="review"?"Reviews":"Strategy guides","Cover editorial evidence");
            Add("coverDiagnostic",match.Display,"Explicit editorial callout recognized");
        }
        foreach(var first in lines)
        {
            Check(first.Text,first,first);
            // Build up to three adjacent lines in the same column, never across OCR passes.
            var current=first;var text=first.Text;
            for(int step=0;step<2;step++)
            {
                var next=lines.Where(n=>n.Y>current.Y && n.Y-(current.Y+current.Height)>=-current.Height*.35 && n.Y-(current.Y+current.Height)<=Math.Max(current.Height,n.Height)*1.1 &&
                    (Math.Min(current.X+current.Width,n.X+n.Width)-Math.Max(current.X,n.X)>Math.Min(current.Width,n.Width)*.25 || Math.Abs(current.X-n.X)<=Math.Max(current.Height,n.Height)*.6))
                    .OrderBy(n=>n.Y).ThenBy(n=>Math.Abs(n.X-current.X)).FirstOrDefault();
                if(next is null)break;
                text+=" "+next.Text;Check(text,first,next);current=next;
            }
        }
    }

    // Model regions are evidence, not calibrated OCR confidence. Do not feed them
    // into the Tesseract confidence gates or overwrite existing metadata.
    private static void AnalyzeVisionCover(LibraryItem item, Dictionary<string,CatalogTitle> dictionary, string json, List<Suggestion> suggestions)
    {
        using var doc=System.Text.Json.JsonDocument.Parse(json);
        var root=doc.RootElement;
        int width=root.GetProperty("width").GetInt32(),height=root.GetProperty("height").GetInt32();
        if(width<=0||height<=0)throw new InvalidOperationException("Invalid vision dimensions.");
        var lines=root.GetProperty("regions").EnumerateArray().Take(800).Select(r=>new LayoutLine(r.GetProperty("text").GetString()??"",0,0,r.GetProperty("x").GetInt32(),r.GetProperty("y").GetInt32(),r.GetProperty("width").GetInt32(),r.GetProperty("height").GetInt32(),0)).Where(l=>l.Height>0&&l.Width>0&&!string.IsNullOrWhiteSpace(l.Text)).ToArray();
        void Log(string value,string evidence,string status)=>suggestions.Add(new("coverDiagnostic",value,1,evidence,status));
        Log("Florence-2 Base",$"{lines.Length} regions; {root.GetProperty("seconds").GetDouble():F1}s; peak process RSS {root.GetProperty("peakMb").GetDouble():F0} MiB; geometry {width}x{height}","Vision runtime");
        foreach(var l in lines)Log(l.Text,$"box {l.X},{l.Y},{l.Width},{l.Height}; model confidence not available","Vision region");
        if(root.TryGetProperty("alternatives",out var alternatives) && alternatives.ValueKind==System.Text.Json.JsonValueKind.Array)
            foreach(var a in alternatives.EnumerateArray().Take(200))
                Log(a.GetProperty("text").GetString()??"",$"Preferred: {a.GetProperty("chosen").GetString()} | box {a.GetProperty("x")},{a.GetProperty("y")},{a.GetProperty("width")},{a.GetProperty("height")}","Vision overlapping alternative");
        if(lines.Length==0)return;
        AnalyzeExplicitCoverCallouts(item,dictionary,lines,height,suggestions,"Florence-2");
        // A top headline linked to a publication subtitle is a masthead, not a game.
        var mastheads=lines.Where(l=>l.Y<height*.16 && l.Width>width*.35 && l.Height>height*.035 &&
            lines.Any(n=>n.Y>=l.Y+l.Height-height*.03 && n.Y<=l.Y+l.Height+height*.09 &&
                Math.Min(l.X+l.Width,n.X+n.Width)>Math.Max(l.X,n.X) &&
                Regex.IsMatch(n.Text,@"\b(?:newsletter|magazine|journal|fanzine)\b",RegexOptions.IgnoreCase))).ToArray();
        var mastheadKeys=new HashSet<string>();
        foreach(var l in mastheads)
        {
            mastheadKeys.Add(ListingKey(l.Text));
            if(dictionary.TryGetValue(Normalize(l.Text),out var resolvedMasthead)||dictionary.TryGetValue("\0"+ListingKey(l.Text),out resolvedMasthead))mastheadKeys.Add(ListingKey(resolvedMasthead.Display));
            Log(l.Text,$"box {l.X},{l.Y},{l.Width},{l.Height} | publication subtitle below masthead","Publication masthead excluded");
        }
        foreach(var previous in suggestions.Where(x=>x.Page==1 && mastheadKeys.Contains(ListingKey(x.Value)) &&
            (x.Field=="coverSubject" || (x.Field=="featuredGames" && x.Confidence=="Cover story — verify"))).ToArray())
        {
            Log(previous.Value,previous.Evidence,"Publication masthead excluded");
            suggestions.Remove(previous);
        }
        var confirmed=suggestions.Where(s=>s.Field=="featuredGames"&&s.Page==1).Select(s=>ListingKey(s.Value)).ToHashSet();
        var candidates=new List<(string Name,double Size,string Evidence,bool Strategy,bool Editorial)>();
        var median=lines.Select(l=>l.Height).OrderBy(v=>v).ElementAt(lines.Length/2);
        void Candidate(string printed,LayoutLine first,LayoutLine last)
        {
            printed=Regex.Replace(printed.Trim(),@"\s+(?:solution|reviewed|review|walkthrough|strategy guide)[!:.]*$","",RegexOptions.IgnoreCase);
            if(!dictionary.TryGetValue(Normalize(printed),out var match)&&!dictionary.TryGetValue("\0"+ListingKey(printed),out match))return;
            if(!ValidCoverSubject(match.Display,item.MagazineTitle)||mastheads.Contains(first)||mastheads.Contains(last))return;
            if(mastheadKeys.Contains(ListingKey(match.Display)) && first.Y<height*.25){Log(match.Display,printed,"Publication masthead excluded");return;}
            double size=(double)Math.Max(first.Height,last.Height)/height;
            var evidence=$"Florence-2 region '{printed}' | box {first.X},{first.Y},{first.Width},{first.Height} | normalized height {size:F3}";
            int left=Math.Min(first.X,last.X), right=Math.Max(first.X+first.Width,last.X+last.Width);
            int bottom=Math.Max(first.Y+first.Height,last.Y+last.Height);
            bool NearbyLabel(LayoutLine n) => n!=first && n!=last && n.Y>=bottom-height*.025 && n.Y<=bottom+height*.12 && Math.Min(right,n.X+n.Width)>Math.Max(left,n.X);
            bool strategy=lines.Any(n=>NearbyLabel(n)&&Regex.IsMatch(n.Text,@"\b(?:solution|walk[ -]?through|strategy guide|play guide|exclusive maps)\b",RegexOptions.IgnoreCase));
            bool teaser=lines.Any(n=>NearbyLabel(n)&&Regex.IsMatch(n.Text,@"\b(?:new pics|new pictures|first look)\b",RegexOptions.IgnoreCase));
            bool editorial=lines.Any(n=>NearbyLabel(n)&&Regex.IsMatch(n.Text,@"\b(?:is here|has arrived|exclusive(?: review| preview)?|first look|full review|in depth|see pages?\s+\d+(?:\s*[-–—]\s*\d+)?(?: for details)?)\b",RegexOptions.IgnoreCase));
            // A prominent exact catalog match plus local editorial copy is independent of Tesseract success.
            bool independent=confirmed.Contains(ListingKey(match.Display));
            evidence+=$" | strategy {strategy}; editorial {editorial}; teaser {teaser}; independent cover OCR {independent}";
            bool pageReference=lines.Any(n=>NearbyLabel(n)&&Regex.IsMatch(n.Text,@"\bsee pages?\s+\d+(?:\s*[-–—]\s*\d+)?\b",RegexOptions.IgnoreCase));
            var leadIn=string.Join(" ",lines.Where(n=>n.Y>=first.Y-height*.11 && n.Y<first.Y && Math.Min(right,n.X+n.Width)>Math.Max(left,n.X)).OrderBy(n=>n.Y).Select(n=>n.Text));
            if(pageReference && Regex.IsMatch(leadIn,@"\bmaster\s+the\b",RegexOptions.IgnoreCase)){strategy=true; evidence+=" | aligned instructional lead-in: "+leadIn;}
            if(size<.035||Math.Max(first.Height,last.Height)<median*1.5||(!independent&&!strategy&&!(editorial&&(size>=.06||pageReference)))){Log(match.Display,evidence,"Vision candidate — insufficient independent or prominence evidence");return;}
            double score=size*(strategy?1.5:editorial?1.35:1.0)*(teaser&&!editorial?.70:1.0);
            candidates.Add((match.Display,score,evidence,strategy,editorial));Log(match.Display,evidence,"Vision candidate — corroborated");
        }
        foreach(var l in lines)
        {
            Candidate(l.Text,l,l);
            foreach(var n in lines.Where(n=>n!=l&&n.Y>l.Y&&n.Y-(l.Y+l.Height)<=Math.Max(l.Height,n.Height)*1.5&&Math.Min(l.X+l.Width,n.X+n.Width)>Math.Max(l.X,n.X)).OrderBy(n=>n.Y).Take(4))
                Candidate(l.Text+" "+n.Text,l,n);
        }
        var ranked=candidates.GroupBy(c=>ListingKey(c.Name)).Select(g=>g.OrderByDescending(c=>c.Size).First()).OrderByDescending(c=>c.Size).ToArray();
        if(ranked.Length==0 || (ranked.Length>1&&ranked[0].Size<ranked[1].Size*1.6)){Log("Vision ranking",string.Join("; ",ranked.Select(c=>$"{c.Name}: {c.Size:F3}")),"Vision inconclusive — retain OCR");return;}
        var best=ranked[0];
        // A unique, prominent vision title with local strategy evidence can supersede a weaker OCR-only callout.
        // Keep previous OCR evidence in diagnostics and retain manual approval.
        if(best.Strategy)
        {
            foreach(var previous in suggestions.Where(x=>x.Field=="coverSubject"&&x.Page==1&&x.Confidence=="Dominant cover headline — verify"&&ListingKey(x.Value)!=ListingKey(best.Name)).ToArray())
            {
                Log(previous.Value,previous.Evidence,"OCR headline superseded by dominant vision strategy evidence");
                suggestions.Remove(previous);
            }
        }
        suggestions.Add(new("coverSubject",best.Name,1,best.Evidence,"Dominant cover headline — verify"));
        suggestions.Add(new("featuredGames",best.Name,1,best.Evidence,"Cover story — verify"));
        if(best.Strategy) suggestions.Add(new("specialFeatures","Strategy guides",1,best.Evidence,"Cover strategy evidence — verify"));
    }

    private static string RemapCoverTsv(string tsv,int offsetX,int offsetY,int width,int height)
    {
        var rows=tsv.Split('\n');
        for(int i=1;i<rows.Length;i++)
        {
            var p=rows[i].TrimEnd('\r').Split('\t',12);
            if(p.Length!=12||!int.TryParse(p[6],out int x)||!int.TryParse(p[7],out int y))continue;
            if(p[0]=="1"){p[6]="0";p[7]="0";p[8]=width.ToString(CultureInfo.InvariantCulture);p[9]=height.ToString(CultureInfo.InvariantCulture);}
            else {p[6]=(x+offsetX).ToString(CultureInfo.InvariantCulture);p[7]=(y+offsetY).ToString(CultureInfo.InvariantCulture);}
            rows[i]=string.Join("\t",p);
        }
        return string.Join("\n",rows);
    }
    private static PageLayout ParseLayout(string tsv)
    {
        var groups = new Dictionary<(int,int,int), List<(string Text,int X,int Y,int W,int H,double Conf)>>();
        int width=0,height=0;
        foreach (var row in tsv.Split('\n').Skip(1))
        {
            var p=row.TrimEnd('\r').Split('\t',12); if(p.Length!=12) continue;
            if(!int.TryParse(p[6],out int x)||!int.TryParse(p[7],out int y)||!int.TryParse(p[8],out int w)||!int.TryParse(p[9],out int h)) continue;
            if(p[0]=="1") {width=w;height=h;continue;}
            if(p[0]!="5"||string.IsNullOrWhiteSpace(p[11])||!double.TryParse(p[10],NumberStyles.Float,CultureInfo.InvariantCulture,out double conf)||conf<0) continue;
            if(!int.TryParse(p[2],out int b)||!int.TryParse(p[3],out int par)||!int.TryParse(p[4],out int n)) continue;
            if(!groups.TryGetValue((b,par,n),out var words)) groups[(b,par,n)]=words=new();
            words.Add((p[11].Trim(),x,y,w,h,conf));
        }
        var lines=groups.Select(g=>{var a=g.Value; int x=a.Min(t=>t.X),y=a.Min(t=>t.Y);return new LayoutLine(string.Join(" ",a.Select(t=>t.Text)),g.Key.Item1,g.Key.Item2,x,y,a.Max(t=>t.X+t.W)-x,a.Select(t=>t.H).OrderBy(v=>v).ElementAt(a.Count/2),a.Average(t=>t.Conf));}).ToList();
        return new(width,height,lines);
    }
    private static string Geometry(LayoutLine l, PageLayout p) => p.Width>0&&p.Height>0 ? $" [x={100*l.X/p.Width}%, y={100*l.Y/p.Height}%, {(l.Confidence==101?"vision; confidence unavailable":$"OCR {l.Confidence:0}%")}]" : "";
    private static string Compact(string s) => Normalize(s).Replace(" ", "");
    private static string NormalizeCoverDate(string value)
    {
        return Regex.Replace(value, $@"\b(?:{Months})\b", m => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(m.Value.ToLowerInvariant()), RegexOptions.IgnoreCase);
    }
    private static bool MissingPublisher(string? value) => Regex.IsMatch(Normalize(value ?? ""), @"^(?:|unknown|unspecified|not specified|not identified|none|n a)$", RegexOptions.IgnoreCase);
    private static string PublisherName(string line)
    {
        var match = Regex.Match(line, @"\b(?:is published[^\r\n]{0,180}?\bby|published by|publisher[ \t]*:)[ \t]*[:–-]?[ \t]*([\p{L}][^,;\r\n]{0,80})", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : "";
    }
    private static bool IncompletePublisher(string value) =>
        Regex.IsMatch(value.Trim(), @"(?:\b(?:of|the|and|for|de|del|du|des|la|le|van|von|di|da)\s*|[&/–-])$", RegexOptions.IgnoreCase);
    private static bool ShorterPublisherPrefix(string proposed, string? existing)
    {
        var a = Normalize(proposed); var b = Normalize(existing ?? "");
        return a.Length > 0 && b.StartsWith(a + " ", StringComparison.Ordinal);
    }
    private static bool PublisherContinuation(LayoutLine first, LayoutLine next, PageLayout layout)
    {
        // No geometry means no reliable continuation: preserve the existing value instead.
        if (layout.Width <= 0 || layout.Height <= 0 || first.Height <= 0 || next.Height <= 0 ||
            first.Block != next.Block || first.Paragraph != next.Paragraph || next.Confidence < 55) return false;
        int h = Math.Max(first.Height, next.Height);
        return next.Y > first.Y && next.Y - first.Y <= 3 * h &&
            Math.Abs(first.X - next.X) <= Math.Max(2 * h, layout.Width / 50) &&
            next.Height <= 2 * first.Height && first.Height <= 2 * next.Height;
    }
    private static bool LooksLikePerson(string value)
    {
        if (Regex.IsMatch(value, @"\b(?:design|staff|team|department|production|editorial|editor|publisher|advertising|art director|layout|photography|marketing|office|contact)\b", RegexOptions.IgnoreCase)) return false;
        return Regex.IsMatch(value, @"^[\p{L}][\p{L}.'’\-]*(?:[ \t]+[\p{L}][\p{L}.'’\-]*){1,4}$");
    }
    private static int RetailMarkers(string text)
    {
        string[] markers = [@"\b(?:special offer|mail order|order now|credit cards? accepted|call now|toll.free|free delivery|postage and packing|send cheque|send check)\b", @"\b(?:opening hours|open (?:mon|tue|wed|thu|fri|sat|sun)|mon\s*[-–]\s*fri|sat\s*[-–]\s*sun|hours\s*:)\b", @"(?:£|\$|€)\s*\d{1,5}[.,]\d{2}\b", @"\b(?:tel(?:ephone)?|fax)\s*[:.]?\s*\+?[0-9(]", @"\b(?:retailer|stockists?|catalogue|price list|trade in|trade-in)\b"];
        return markers.Count(p => Regex.IsMatch(text, p, RegexOptions.IgnoreCase));
    }
    private static bool ArcadePlatformEvidence(string line, bool toc)
    {
        // The platform label must have its own context, not be a word in a game title.
        return Regex.IsMatch(line, @"(?:^|[—–:])\s*Arcade\s*$|\b(?:for (?:the )?arcade|in (?:the )?arcades|arcade (?:version|release|machine|hardware|cabinet|gameplay))\b", RegexOptions.IgnoreCase) ||
            (toc && Regex.IsMatch(line, @"^\s*\d{1,3}[. \t]+Arcade\s*$", RegexOptions.IgnoreCase));
    }
    private static string CanonicalLanguage(string? raw)
    {
        var value = (raw ?? "").Trim();
        if (value.Length == 0 || Regex.IsMatch(value, @"^(?:ISO|ISO[- ]?639.*|tag|code|unknown|und|none|n/a)$", RegexOptions.IgnoreCase)) return "";
        // Only accept a recognized language code or name. Do not treat a label as a language.
        var neutral = CultureInfo.GetCultures(CultureTypes.NeutralCultures).Where(c => c.Name.Length > 0);
        var candidate = neutral.FirstOrDefault(c => string.Equals(c.Name,value,StringComparison.OrdinalIgnoreCase) || string.Equals(c.TwoLetterISOLanguageName,value,StringComparison.OrdinalIgnoreCase) || string.Equals(c.ThreeLetterISOLanguageName,value,StringComparison.OrdinalIgnoreCase) || string.Equals(c.EnglishName,value,StringComparison.OrdinalIgnoreCase));
        if (candidate is not null) return candidate.EnglishName;
        var parts = value.Replace('_','-').Split('-');
        if (parts.Length > 1 && parts.All(p => p.Length > 0 && p.All(char.IsAsciiLetterOrDigit)))
        {
            var code = parts[0];
            candidate = neutral.FirstOrDefault(c => string.Equals(c.Name,code,StringComparison.OrdinalIgnoreCase));
            if (candidate is not null) return candidate.EnglishName;
        }
        return "";
    }
    private static int RomanOrNumber(string s)
    {
        s = s.Trim();
        if(int.TryParse(s,NumberStyles.None,CultureInfo.InvariantCulture,out int v))return v;
        // OCR commonly reads an uppercase I as lowercase l. Only correct a run of I/l glyphs;
        // a true uppercase L remains 50. Reject invalid subtractive Roman forms.
        if(Regex.IsMatch(s,@"^[Iil]+$") && s.Any(c=>c=='I'||c=='i')) s=s.Replace('l','I');
        s=s.ToUpperInvariant();
        if(s.Length==0 || !Regex.IsMatch(s,@"^M{0,3}(?:CM|CD|D?C{0,3})(?:XC|XL|L?X{0,3})(?:IX|IV|V?I{0,3})$"))return 0;
        int total=0,last=0; foreach(char c in s.Reverse()) {int n=c switch{'I'=>1,'V'=>5,'X'=>10,'L'=>50,'C'=>100,'D'=>500,'M'=>1000,_=>0}; if(n==0)return 0;total+=n<last?-n:n;last=Math.Max(last,n);}return total;
    }
    private static bool ValidUpc(string s)
    {
        if(s.Length is not (12 or 13)||!s.All(char.IsAsciiDigit)) return false;
        int sum=0;for(int i=s.Length-2,k=0;i>=0;i--,k++)sum+=(s[i]-'0')*(k%2==0?3:1);return (10-sum%10)%10==s[^1]-'0';
    }
    private sealed record GameHit(string Game,bool Whole,bool Fuzzy);
    private static string ListingKey(string value)
    {
        // Normalize conjunctions and hyphens only for complete printed entries.
        return Compact(Regex.Replace(value, @"\band\b|&", " ", RegexOptions.IgnoreCase));
    }
    private static int SequelNumber(string token)
    {
        if(int.TryParse(token,NumberStyles.None,CultureInfo.InvariantCulture,out int value))return value>0?value:0;
        if(!Regex.IsMatch(token,@"^[ivxl]+$",RegexOptions.IgnoreCase))return 0;
        value=RomanOrNumber(token);
        return value is >=1 and <=39?value:0;
    }
    private static string NumberedTitleKey(string value)
    {
        var tokens=Normalize(value).Split(' ',StringSplitOptions.RemoveEmptyEntries);
        for(int i=1;i<tokens.Length;i++)
        {
            int number=SequelNumber(tokens[i]);
            if(number>0)tokens[i]=number.ToString(CultureInfo.InvariantCulture);
        }
        return string.Join(" ",tokens);
    }
    private static string RepairRomanGlyphs(string value)
    {
        // Uppercase Roman typography only; retain raw OCR separately as evidence.
        // A catalog lookup must still verify the resulting complete title.
        value=Regex.Replace(value,@"\b([VX])([Il1]{1,3})\b",m=>m.Groups[1].Value+new string('I',m.Groups[2].Length));
        return Regex.Replace(value,@"\b([VX])(?:[ \t]+[I1l]){1,3}\b|\bI(?:[ \t]+I){1,2}\b",m=>Regex.Replace(m.Value,@"[ \t]"," ").Replace(" ","").Replace('1','I').Replace('l','I'));
    }
    private static bool TryNumberedTitle(Dictionary<string,CatalogTitle> dictionary,string text,out CatalogTitle hit)
    {
        var key=Normalize(text);
        if(dictionary.TryGetValue(key,out var exact)) {hit=exact;return true;}
        var repaired=Normalize(RepairRomanGlyphs(text));
        if(repaired!=key && dictionary.TryGetValue(repaired,out var repairedHit)) {hit=repairedHit;return true;}
        if(dictionary.TryGetValue("\0number:"+NumberedTitleKey(repaired),out var numbered)) {hit=numbered;return true;}
        hit=null!;return false;
    }
    private static bool TryNumberedKey(Dictionary<string,CatalogTitle> dictionary,string key,out CatalogTitle hit)
    {
        if(dictionary.TryGetValue(key,out var direct)){hit=direct;return true;}
        var tokens=key.Split(' ');
        if(tokens.Skip(1).Any(t=>t.Length>0&&(char.IsAsciiDigit(t[0]) || (t.Length<=7 && t.All(c=>c is 'i' or 'v' or 'x' or 'l')))) && dictionary.TryGetValue("\0number:"+NumberedTitleKey(key),out var numbered)){hit=numbered;return true;}
        hit=null!;return false;
    }
    private static bool BareFranchise(Dictionary<string,CatalogTitle> dictionary,string key) => dictionary.ContainsKey("\0family:"+key);
    private static string SequelLineText(Dictionary<string,CatalogTitle> dictionary,LayoutLine first,PageLayout layout)
    {
        if(first.Height<=0 || first.Confidence<40 || !BareFranchise(dictionary,Normalize(first.Text)))return first.Text;
        foreach(var next in layout.Lines.Where(l=>l.Y>first.Y&&l.Height>0&&l.Confidence>=40).OrderBy(l=>l.Y))
        {
            if(next.Y-first.Y>Math.Max(first.Height,next.Height)*2.6)continue;
            if(next.Height<first.Height*.55 || next.Height>first.Height*1.8)continue;
            if(Math.Abs(first.X-next.X)>Math.Max(first.Height,next.Height)*1.5 && Math.Abs(first.X+first.Width/2.0-next.X-next.Width/2.0)>Math.Max(first.Height,next.Height)*1.5)continue;
            var numeral=RepairRomanGlyphs(next.Text.Trim());
            if(!Regex.IsMatch(numeral,@"^[IVXL]+$") || SequelNumber(numeral)==0)continue;
            var joined=first.Text+" "+next.Text;
            if(TryNumberedTitle(dictionary,joined,out var hit)&&SameNumbers(RepairRomanGlyphs(joined),hit.Display))return joined;
        }
        return first.Text;
    }
    private static IEnumerable<GameHit> FindGames(Dictionary<string,CatalogTitle> dictionary,string line,bool titleContext,bool listing,bool context)
    {
        var matchingLine=RepairRomanGlyphs(line.Normalize(NormalizationForm.FormKC));
        var original=Words.Matches(matchingLine).Select(m=>m.Value).ToArray();var tokens=original.Select(x=>x.ToLowerInvariant()).ToArray();
        var whole=Normalize(listing?Regex.Replace(matchingLine,@"^\d{1,3}[A-Za-z]?[.\s]+|\.{2,}\s*\d{1,3}$",""):matchingLine);
        var used=new HashSet<string>(StringComparer.Ordinal);
        var exactRanges = new List<string>();
        var parts=listing?line.Split([',',';'],StringSplitOptions.RemoveEmptyEntries):new[]{line};
        foreach(var part in parts)
        {
            var clean=part.Trim();
            var partKey=Normalize(clean);
            if(partKey.Length==0)continue;
            if(TryNumberedTitle(dictionary,clean,out var direct))
            {
                if(BareFranchise(dictionary,partKey)&&!listing&&!titleContext)continue;
                if((listing||titleContext||context)&&used.Add(Compact(direct.Display))) {exactRanges.Add(partKey);yield return new(direct.Display,true,false);}
                continue;
            }
            // Complete listing entries can use unambiguous punctuation/conjunction variants.
            if(listing && partKey.Length>=5 && partKey.Split(' ').Length>=2)
            {
                if(dictionary.TryGetValue("\0"+ListingKey(clean),out var spelling))
                {
                    if(used.Add(Compact(spelling.Display))) {exactRanges.Add(partKey);yield return new(spelling.Display,true,false);}
                    continue;
                }
            }
        }
        for(int i=0;i<tokens.Length;i++)
        {
            for(int n=Math.Min(12,tokens.Length-i);n>=1;n--)
            {
                string key=string.Join(" ",tokens,i,n);
                if(!TryNumberedKey(dictionary,key,out var hit))continue;
                bool entire=key==whole;
                if(BareFranchise(dictionary,key)&&(!entire||(!titleContext&&!listing)))continue;
                if(!entire && exactRanges.Any(r=>r!=key && (" "+r+" ").Contains(" "+key+" ",StringComparison.Ordinal)))continue;
                bool sequel=i+n<tokens.Length&&(SequelNumber(tokens[i+n])>0||Regex.IsMatch(tokens[i+n],@"^(?:[0-9]{1,4}|l{2,3})$"));
                if(sequel || (i+n<tokens.Length && Regex.IsMatch(tokens[i+n],@"^(?:vs|versus|series|franchise|movie|film|saga|meat|contest|universe)$")))continue;
                // A single common word or character name in prose is insufficient evidence.
                // Single-word games remain eligible as complete headings or exact listing entries.
                bool explicitNamedTitle = listing && i+n==tokens.Length && Regex.IsMatch(line,@"\b(?:cover (?:story|feature)|game for[^:]{0,45})\s*:\s*[^:]+$",RegexOptions.IgnoreCase);
                if(n==1&&!entire&&!explicitNamedTitle)continue;
                if(n==1&&!titleContext&&!explicitNamedTitle)continue;
                if(!titleContext&&(!char.IsUpper(original[i][0])||(!context&&n<=2)))continue;
                if(!listing&&!entire&&!context&&n<=2&&!titleContext)continue;
                if(!listing&&!entire&&n<=2&&Enumerable.Range(i,n).Any(j=>!char.IsUpper(original[j][0])&&!char.IsAsciiDigit(original[j][0])))continue;
                if(hit.Alias&&!titleContext&&!context)continue;
                if(used.Add(Compact(hit.Display)))yield return new(hit.Display,entire,false);
                i+=n-1;break;
            }
        }
        if(!listing)yield break;
        // Conservative one-character OCR recovery only for isolated contents entries.
        foreach(var part in parts)
        {
            var key=Normalize(part); if(exactRanges.Contains(key)||key.Length<5||key.Length>70||key.Split(' ').Length>9||dictionary.ContainsKey(key)||key.Split(' ').Length<2)continue;
            var candidates=dictionary.Where(kv=>!kv.Key.StartsWith("\0",StringComparison.Ordinal)&&!kv.Value.Alias&&Math.Abs(kv.Key.Length-key.Length)<=1&&kv.Key[0]==key[0]&&SameNumbers(kv.Key,key)&&OneEdit(kv.Key,key)).Select(kv=>kv.Value.Display).Distinct(StringComparer.OrdinalIgnoreCase).Take(2).ToArray();
            if(candidates.Length==1&&used.Add(Compact(candidates[0])))yield return new(candidates[0],false,true);
        }
    }
    private static bool SameNumbers(string a,string b) => Normalize(a).Split(' ').Select(SequelNumber).Where(n=>n>0).SequenceEqual(Normalize(b).Split(' ').Select(SequelNumber).Where(n=>n>0));
    private static bool OneEdit(string a,string b)
    {
        if(Math.Abs(a.Length-b.Length)>1)return false;if(a.Length>b.Length)(a,b)=(b,a);
        int i=0,j=0,edits=0;while(i<a.Length&&j<b.Length){if(a[i]==b[j]){i++;j++;continue;}if(++edits>1)return false;if(a.Length==b.Length)i++;j++;}return edits+b.Length-j<=1;
    }
    private static async Task<string> Run(string executable, string[] args, CancellationToken token, int timeoutSeconds = 35)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true } };
        foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
        process.StartInfo.Environment["OMP_THREAD_LIMIT"] = "2";
        process.Start();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        var output = process.StandardOutput.ReadToEndAsync(timeout.Token); var error = process.StandardError.ReadToEndAsync(timeout.Token);
        try { await process.WaitForExitAsync(timeout.Token); var stdout = await output; var stderr = await error; if (process.ExitCode != 0) throw new InvalidOperationException($"{executable} returned exit code {process.ExitCode}." + (timeoutSeconds>35 ? " "+stderr.Substring(Math.Max(0,stderr.Length-1800)) : "")); return stdout; }
        catch (OperationCanceledException) { try { if (!process.HasExited) process.Kill(true); } catch { } if (token.IsCancellationRequested) throw; throw new InvalidOperationException($"{executable} timed out."); }
        finally { try { if (!process.HasExited) process.Kill(true); } catch { } }
    }
}
