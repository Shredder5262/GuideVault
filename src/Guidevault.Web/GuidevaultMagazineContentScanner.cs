using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

// Read-only magazine OCR and conservative metadata suggestions. No source or metadata writes.
public static class GuidevaultMagazineContentScanner
{
    public sealed record Suggestion(string Field, string Value, int Page, string Evidence, string Confidence);
    public sealed record ScanResult(int TotalPages, int Start, int Scanned, int ReadablePages, List<Suggestion> Suggestions, List<string> Warnings)
    {
        public string ScannerRevision => "v4.7.1";
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

    public static async Task<ScanResult> ScanAsync(LibraryItem item, IEnumerable<GuidevaultLaunchBoxGame> games, int start, int count, CancellationToken token)
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
            var dictionary = BuildDictionary(games, GetYear(item.CoverDate + " " + item.PublicationDate + " " + item.Year));
            var sourceInfo = new FileInfo(item.Path);
            for (int i = start; i < Math.Min(start + count, total); i++)
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
                        await Run("pdftoppm", ["-f", page, "-l", page, "-scale-to", "2400", "-singlefile", "-png", item.Path, prefix], token);
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
                    if (Words.Matches(tsv).Count > 30) RememberOcr(cacheKey,tsv);
                    }
                    else result = result with { CachedPages = result.CachedPages + 1 };
                    var layout = ParseLayout(tsv);
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
                    if (Words.Matches(text).Count >= 3)
                    {
                        var excluded = ExcludedPageReason(text, i + 1, total);
                        if (excluded.Length > 0)
                        {
                            result.Warnings.Add($"Page {i+1}: {excluded}; excluded from current-issue metadata evidence.");
                            foreach (var line in layout.Lines)
                                foreach (var hit in FindGames(dictionary,line.Text,true,false,true).Where(h=>!h.Fuzzy))
                                    if (!result.Suggestions.Any(s=>s.Field=="excludedGame"&&s.Page==i+1&&ListingKey(s.Value)==ListingKey(hit.Game)))
                                        result.Suggestions.Add(new("excludedGame",hit.Game,i+1,line.Text,"Excluded page — not current-issue coverage"));
                        }
                        else AnalyzePage(item, dictionary, text, layout, i + 1, result.Suggestions);
                        result = result with { ReadablePages = result.ReadablePages + 1 };
                    }
                    else result.Warnings.Add($"Page {i + 1}: little or no readable text.");
                }
                catch (Exception ex) when (ex is not OperationCanceledException) { result.Warnings.Add($"Page {i + 1}: {ex.Message}"); }
                result = result with { Scanned = result.Scanned + 1 };
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
            if (dictionary.Count == 0) result.Warnings.Add("No usable LaunchBox game names; game coverage suggestions are unavailable.");
            return result;
        }
        finally { try { Directory.Delete(temp, true); } catch { } Gate.Release(); }
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
                    var local = layout.Lines.Skip(Math.Max(0, pi - 2)).Take(Math.Min(3, pi + 1))
                        .Where(l => l.Block == pubLine.Block && l.Paragraph == pubLine.Paragraph)
                        .Select(l => l.Text);
                    var context = string.Join(" ", local);
                    if (!(" " + Normalize(context) + " ").Contains(" " + publicationKey + " ", StringComparison.Ordinal)) continue;
                    if (IsLegal(pubLine.Text) && !Regex.IsMatch(context, @"\b(?:subscription|published (?:monthly|weekly|quarterly)|ISSN)\b", RegexOptions.IgnoreCase)) continue;
                    // A publisher name may wrap onto the next OCR line. Never propose a dangling
                    // fragment or replace a longer stored name with its shorter prefix.
                    var pub = PublisherName(pubLine.Text);
                    if (pub.Length == 0) continue;
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
            string line = l.Text;
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
                var exact=dictionary.TryGetValue(Normalize(title),out var direct)?new[]{direct.Display}:Array.Empty<string>();
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
        return key.Length>=3 && key.Length<=100 && key!=Normalize(publication??"") && !Regex.IsMatch(key,@"^(?:(?:issue|volume|vol|number|no)(?:\s+\d+)?|magazine|cover|cover story|cover feature|featured games?|features?|special edition|contents|table of contents|reviews?|previews?|news|unknown|unspecified|not identified|none|n a)$",RegexOptions.IgnoreCase);
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
        var lines=layout.Lines.Where(l=>l.Height>0 && l.Confidence>=48 && !ArticleNoise(l.Text)).ToArray();
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
    private static string Geometry(LayoutLine l, PageLayout p) => p.Width>0&&p.Height>0 ? $" [x={100*l.X/p.Width}%, y={100*l.Y/p.Height}%, OCR {l.Confidence:0}%]" : "";
    private static string Compact(string s) => Normalize(s).Replace(" ", "");
    private static string NormalizeCoverDate(string value)
    {
        return Regex.Replace(value, $@"\b(?:{Months})\b", m => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(m.Value.ToLowerInvariant()), RegexOptions.IgnoreCase);
    }
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
    private static IEnumerable<GameHit> FindGames(Dictionary<string,CatalogTitle> dictionary,string line,bool titleContext,bool listing,bool context)
    {
        var original=Words.Matches(line.Normalize(NormalizationForm.FormKC)).Select(m=>m.Value).ToArray();var tokens=original.Select(x=>x.ToLowerInvariant()).ToArray();
        var whole=Normalize(listing?Regex.Replace(line,@"^\d{1,3}[A-Za-z]?[.\s]+|\.{2,}\s*\d{1,3}$",""):line);
        var used=new HashSet<string>(StringComparer.Ordinal);
        var exactRanges = new List<string>();
        var parts=listing?line.Split([',',';'],StringSplitOptions.RemoveEmptyEntries):new[]{line};
        foreach(var part in parts)
        {
            var clean=part.Trim();
            var partKey=Normalize(clean);
            if(partKey.Length==0)continue;
            if(dictionary.TryGetValue(partKey,out var direct))
            {
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
                if(!dictionary.TryGetValue(key,out var hit))continue;
                bool entire=key==whole;
                if(!entire && exactRanges.Any(r=>r!=key && (" "+r+" ").Contains(" "+key+" ",StringComparison.Ordinal)))continue;
                bool sequel=i+n<tokens.Length&&Regex.IsMatch(tokens[i+n],@"^(?:[0-9]{1,4}|i{1,3}|l{2,3}|iv|v|vi{1,3}|ix|x|xi{1,3})$");
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
            var candidates=dictionary.Where(kv=>!kv.Value.Alias&&Math.Abs(kv.Key.Length-key.Length)<=1&&kv.Key[0]==key[0]&&SameNumbers(kv.Key,key)&&OneEdit(kv.Key,key)).Select(kv=>kv.Value.Display).Distinct(StringComparer.OrdinalIgnoreCase).Take(2).ToArray();
            if(candidates.Length==1&&used.Add(Compact(candidates[0])))yield return new(candidates[0],false,true);
        }
    }
    private static bool SameNumbers(string a,string b) => Regex.Matches(a,@"\b(?:\d+|i{1,3}|iv|v|vi{1,3}|ix|x)\b").Select(m=>m.Value).SequenceEqual(Regex.Matches(b,@"\b(?:\d+|i{1,3}|iv|v|vi{1,3}|ix|x)\b").Select(m=>m.Value));
    private static bool OneEdit(string a,string b)
    {
        if(Math.Abs(a.Length-b.Length)>1)return false;if(a.Length>b.Length)(a,b)=(b,a);
        int i=0,j=0,edits=0;while(i<a.Length&&j<b.Length){if(a[i]==b[j]){i++;j++;continue;}if(++edits>1)return false;if(a.Length==b.Length)i++;j++;}return edits+b.Length-j<=1;
    }
    private static async Task<string> Run(string executable, string[] args, CancellationToken token)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true } };
        foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
        process.StartInfo.Environment["OMP_THREAD_LIMIT"] = "2";
        process.Start();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(35));
        var output = process.StandardOutput.ReadToEndAsync(timeout.Token); var error = process.StandardError.ReadToEndAsync(timeout.Token);
        try { await process.WaitForExitAsync(timeout.Token); var stdout = await output; await error; if (process.ExitCode != 0) throw new InvalidOperationException($"{executable} returned exit code {process.ExitCode}."); return stdout; }
        catch (OperationCanceledException) { try { if (!process.HasExited) process.Kill(true); } catch { } if (token.IsCancellationRequested) throw; throw new InvalidOperationException($"{executable} timed out."); }
        finally { try { if (!process.HasExited) process.Kill(true); } catch { } }
    }
}
