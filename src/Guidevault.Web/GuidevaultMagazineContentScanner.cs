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
        public string ScannerRevision => "v3";
    }
    private static readonly SemaphoreSlim Gate = new(1, 1);
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
        (@"\b(?:Sega Genesis|Mega Drive)\b", "Sega Genesis"), (@"\bSega Saturn\b", "Sega Saturn"), (@"\b(?:Sega Dreamcast|Dreamcast)\b", "Sega Dreamcast"),
        (@"\b(?:Sega CD|Mega CD)\b", "Sega CD"), (@"\b(?:Sega Game Gear|Game Gear)\b", "Sega Game Gear"), (@"\bSega Master System\b", "Sega Master System"),
        (@"\b(?:PlayStation 2|PS2)\b", "Sony PlayStation 2"), (@"\b(?:PlayStation 3|PS3)\b", "Sony PlayStation 3"), (@"\b(?:PlayStation 4|PS4)\b", "Sony PlayStation 4"),
        (@"\b(?:PlayStation 5|PS5)\b", "Sony PlayStation 5"), (@"\b(?:PlayStation Portable|PSP)\b", "Sony PSP"), (@"\b(?:PlayStation Vita|PS Vita)\b", "Sony PlayStation Vita"),
        (@"\b(?:PS1|PSX|PlayStation)\b(?!\s*(?:[2345]|Portable|Vita|Network|Store|Plus)\b)", "Sony PlayStation"),
        (@"\bXbox 360\b", "Microsoft Xbox 360"), (@"\bXbox One\b", "Microsoft Xbox One"), (@"\bXbox Series [XS]\b", "Microsoft Xbox Series X/S"),
        (@"\bXbox\b(?!\s+(?:360|One|Series|Live|Game Pass))", "Microsoft Xbox"), (@"\b(?:MS-DOS|DOS PC)\b", "MS-DOS"), (@"\b(?:Windows PC|PC Windows|PC CD-ROM)\b", "Windows"),
        (@"\b(?:TurboGrafx-16|PC Engine)\b", "NEC TurboGrafx-16"), (@"\b(?:Atari )?Jaguar\b", "Atari Jaguar"), (@"\b(?:Sega )?32X\b", "Sega 32X"), (@"\b(?:Arcade)\b", "Arcade"), (@"\b3DO\b", "3DO Interactive Multiplayer")];

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
            for (int i = start; i < Math.Min(start + count, total); i++)
            {
                token.ThrowIfCancellationRequested();
                try
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
                    var layout = ParseLayout(await Run("tesseract", [input, "stdout", "-l", "eng", "--psm", i == 0 ? "11" : "3", "tsv"], token));
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
                        AnalyzePage(item, dictionary, text, layout, i + 1, result.Suggestions);
                        result = result with { ReadablePages = result.ReadablePages + 1 };
                    }
                    else result.Warnings.Add($"Page {i + 1}: little or no readable text.");
                }
                catch (Exception ex) when (ex is not OperationCanceledException) { result.Warnings.Add($"Page {i + 1}: {ex.Message}"); }
                result = result with { Scanned = result.Scanned + 1 };
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
        Add("volume",info.Volume,"Volume"); Add("year",info.Year,"Year"); Add("region",info.Country,"Country");
        var language = info.LanguageTag?.Trim(); if (!string.IsNullOrEmpty(language)) Add("languageTag",language.ToLowerInvariant() switch {"en" or "en-us" or "en-gb"=>"English", "fr"=>"French", "de"=>"German", "es"=>"Spanish", "ja"=>"Japanese", _=>language},"Language");
        if(string.IsNullOrWhiteSpace(item.Summary)) Add("summary",info.Summary,"Summary");
        foreach(var tag in (info.Tags ?? "").Split([',',';'],StringSplitOptions.RemoveEmptyEntries)) Add("tags",tag,"Tags");
        if(int.TryParse(info.Year,out int y)&&y>=1900&&y<=2099&&int.TryParse(info.Month,out int m)&&m>=1&&m<=12)
        {
            var date=new DateTime(y,m,1); Add("publicationDate",date.ToString("MMMM yyyy",CultureInfo.InvariantCulture),"Year/Month (archive date, not necessarily cover date)");
        }
    }
    private static int ParsePdfPages(string text) => int.TryParse(Regex.Match(text, @"(?im)^Pages:\s*(\d+)").Groups[1].Value, out int n) ? n : 0;
    private static int GetYear(string value) => int.TryParse(YearPattern.Match(value ?? "").Value, out int n) ? n : 0;
    private static string Normalize(string value) => string.Join(" ", Words.Matches(value ?? "").Select(m => m.Value.ToLowerInvariant()));
    // Exact catalog names outrank aliases. Alias matches preserve the printed name;
    // do not silently rename an article to a different regional or unrelated game.
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
            result[key] = new((Compact(owner) == Compact(printed) || (Normalize(owner).StartsWith(key + " ", StringComparison.Ordinal) && key.Split(' ').Length >= 2 && SameNumbers(Normalize(owner), key))) ? owner : printed, true);
        }
        // Punctuation-only spelling variants share the same display when unambiguous.
        foreach(var group in result.ToArray().GroupBy(kv=>Compact(kv.Key)))
        {
            if(group.Select(kv=>Compact(kv.Value.Display)).Distinct(StringComparer.Ordinal).Count()!=1) continue;
            var canonical=group.OrderBy(kv=>kv.Value.Alias).ThenBy(kv=>kv.Value.Display.Length).ThenBy(kv=>kv.Value.Display,StringComparer.Ordinal).First().Value.Display;
            foreach(var kv in group) result[kv.Key]=kv.Value with {Display=canonical};
        }
        return result;
        static bool Usable(string key) => key.Length >= 3 && key.Split(' ').Length <= 12;
    }
    private static void AnalyzePage(LibraryItem item, Dictionary<string, CatalogTitle> dictionary, string text, PageLayout layout, int page, List<Suggestion> suggestions)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string field, string value, string evidence, string confidence = "Review")
        {
            value = value.Trim(); evidence = Regex.Replace(evidence.Trim(), @"\s+", " ");
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
        bool IsLegal(string line) => Regex.IsMatch(line, @"\b(?:all rights reserved|(?:registered )?trademarks?|copyright|official seals?)\b|©", RegexOptions.IgnoreCase);
        bool NumberedLine(string line) => Regex.IsMatch(line, @"(?:^\s*\d{1,3}[.\s]+|\.{2,}\s*\d{1,3}\s*$|\s+\d{1,3}\s*$)");
        bool PlatformIndexLine(string line) => Regex.IsMatch(line, @"^\s*\d{1,3}[A-Za-z]?\s+", RegexOptions.IgnoreCase) &&
            (Platforms.Any(p => Regex.IsMatch(line, p.Pattern, RegexOptions.IgnoreCase)) || Regex.IsMatch(line, @"\b(?:Genesis|Saturn|PC|DOS)\b", RegexOptions.IgnoreCase));
        bool toc = page > 1 && (lines.Any(l => Regex.IsMatch(l, @"^(?:table of\s+)?contents\s*[:.!]?\s*$", RegexOptions.IgnoreCase)) || (lines.Count(PlatformIndexLine) >= 3 && lines.Any(l => Regex.IsMatch(l, @"\b(?:reviews|features|departments|previews)\b", RegexOptions.IgnoreCase))));
        bool likelyAd = !toc && lines.Any(IsLegal) && Regex.IsMatch(text, @"\b(?:order now|call toll.free|available at participating|ask your retailer|for a limited time|send check or money order|suggested retail price|visit your local retailer)\b", RegexOptions.IgnoreCase);
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
            foreach (Match volume in Regex.Matches(text, @"\b(?:volume|vol\.?)\s*(\d{1,3}|[IVXLCDM]{1,7})\b", RegexOptions.IgnoreCase))
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
            MatchField("publisher", @"^\s*(?:published by|publisher\s*:)\s*([^\r\n]{3,100})", text);
            MatchField("publisher", @"\bis published[^\r\n]{0,180}?\bby\s+([\p{L}][^,;\r\n]{2,80})", text);
            var masthead = Regex.Match(text, @"^\s*([\p{L}][\p{L}\p{N} &'.-]{2,70}?)\s*\(ISSN\b", RegexOptions.IgnoreCase|RegexOptions.Multiline);
            if(masthead.Success)
            {
                var title=masthead.Groups[1].Value;
                if(!string.IsNullOrWhiteSpace(item.MagazineTitle) && Normalize(title)==Normalize(item.MagazineTitle+" Magazine")) title=item.MagazineTitle;
                Add("magazineTitle",title,masthead.Value);
            }
            MatchField("writer", @"^\s*(?:editor[- ]in[- ]chief|editorial director|(?:managing |executive )?editor)\s*[:–-]?\s*([^\r\n]{3,80})", text);
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
        var heights = layout.Lines.Where(l => l.Confidence >= 45 && l.Height > 0).Select(l => l.Height).OrderBy(x => x).ToArray();
        double median = heights.Length > 0 ? heights[heights.Length / 2] : 0;
        bool prominent(LayoutLine l) => median > 0 && l.Height >= median * 1.3 && l.Confidence >= 50;
        var coverHeadings = new List<(string Game, LayoutLine Line)>();
        var section = "";
        var previous = (LayoutLine?)null;
        var coverRegion = toc; // Leading content before the first review/platform section may include the cover feature.
        var platformIndex = false;
        foreach (var l in layout.Lines)
        {
            string line = l.Text;
            if (IsLegal(line) || Regex.IsMatch(line, @"\b(?:grand prize|send (?:your )?entries|sweepstakes|win this|no purchase necessary|order now|(?:cd|poster) contest|third .* movie)\b", RegexOptions.IgnoreCase)) { previous = null; continue; }
            if (l.Height > 0 && l.Confidence < 38) { previous = null; continue; }
            var headingText = Regex.Replace(line, @"^\d{1,3}[A-Za-z]?[.\s]+|\.{2,}\s*\d{1,3}$", "").Trim();
            bool numbered = NumberedLine(line);
            bool platformHeading = PlatformIndexLine(line) || (numbered && Regex.IsMatch(headingText, @"^(?:Jaguar|32X|Genesis|PC|DOS|Saturn)\b", RegexOptions.IgnoreCase));
            bool genericSection = Regex.IsMatch(headingText, @"^(?:table of contents|contents|cover (?:story|feature)|features?|reviews?|previews?|departments?|news|letters|editorial|interviews?|walkthroughs?|strategy guides?|cheats?(?: and tips)?|tips(?: and tricks)?|codes|hardware)\s*[:.!]?\s*$", RegexOptions.IgnoreCase);
            if (genericSection)
            {
                section = headingText;
                if (Regex.IsMatch(section, @"^cover (?:story|feature)", RegexOptions.IgnoreCase)) coverRegion = true;
                else if (!Regex.IsMatch(section, @"contents", RegexOptions.IgnoreCase)) coverRegion = false;
                if (toc || prominent(l)) Add("specialFeatures", section, line, "Section heading");
                if (Regex.IsMatch(section, @"^(?:reviews?|previews?|features?|walkthroughs?|strategy guides?)", RegexOptions.IgnoreCase)) platformIndex = true;
                if (Regex.IsMatch(section, @"^(?:departments?|letters|news|cheats?|tips|codes|editorial)", RegexOptions.IgnoreCase)) platformIndex = false;
                previous = null; continue;
            }
            if (platformHeading) { if (!coverRegion || platformIndex) coverRegion = false; if (!Regex.IsMatch(section, @"^(?:departments?|letters|news|cheats?|tips|codes)", RegexOptions.IgnoreCase)) platformIndex = true; }
            bool departments = Regex.IsMatch(section, @"^(?:departments?|letters|news|cheats?|tips|codes|editorial)", RegexOptions.IgnoreCase);
            bool listContext = toc && !departments && (platformIndex || coverRegion || numbered);
            bool titleContext = listContext || (prominent(l) && Words.Matches(line).Count <= 12) || page == 1;
            bool gameContext = Regex.IsMatch(line, @"\b(?:game|review|preview|version|cheat|code|password|released?|play(?:ed|ing)?|for (?:the )?(?:SNES|NES|Genesis|Game Boy|3DO|Sega CD))\b", RegexOptions.IgnoreCase);
            var matched = FindGames(dictionary, line, titleContext, listContext, gameContext).ToList();
            if (listContext && previous is not null && previous.Block == l.Block && previous.Height > 0 && l.Y - previous.Y <= Math.Max(l.Height, previous.Height) * 2.5 && !platformHeading)
            {
                var joined = previous.Text + " " + line;
                foreach (var hit in FindGames(dictionary, joined, true, true, true)) if (!matched.Any(x => Compact(x.Game) == Compact(hit.Game))) matched.Add(hit);
            }
            foreach (var hit in matched)
            {
                string evidence = (previous is not null && previous.Block==l.Block && !Normalize(line).Contains(Normalize(hit.Game),StringComparison.Ordinal) ? previous.Text+" "+line : line) + Geometry(l, layout);
                if (page == 1 && titleContext && l.Confidence >= 50) coverHeadings.Add((hit.Game,l));
                else if (toc && coverRegion && Regex.IsMatch(line, @"\b(?:game for|in.depth|cover (?:story|feature))\b|:\s*[^:]+$", RegexOptions.IgnoreCase) && !hit.Fuzzy)
                { Add("coverSubject", hit.Game, evidence, "Contents cover evidence"); Add("featuredGames", hit.Game, evidence, "Contents cover evidence"); }
                else if (listContext || (page > 1 && prominent(l) && hit.Whole && !departments && !hit.Fuzzy)) Add("featuredGames", hit.Game, evidence, hit.Fuzzy ? "Possible OCR correction" : "Editorial heading");
                else if (page > 1) Add("mentionedGames", hit.Game, evidence, hit.Fuzzy ? "Possible OCR correction" : "Contextual title match");
            }
            if ((toc && !departments && (platformHeading || matched.Count > 0)) || (matched.Count > 0 && gameContext))
            {
                foreach (var (pattern, name) in Platforms) if (Regex.IsMatch(line, pattern, RegexOptions.IgnoreCase)) Add("featuredPlatforms", name, line + Geometry(l, layout), "Editorial platform evidence");
                if (toc && Regex.IsMatch(line, @"\bGenesis\b", RegexOptions.IgnoreCase)) Add("featuredPlatforms", "Sega Genesis", line + Geometry(l, layout), "Editorial platform evidence");
            }
            if (toc && departments && (numbered || (prominent(l) && Words.Matches(headingText).Count<=5)) && Words.Matches(headingText).Count <= 9 && !IsLegal(headingText)) Add("specialFeatures", headingText, line + Geometry(l, layout), "Department listing");
            if ((page <= 3 || toc) && Regex.IsMatch(line, @"\b(?:free|bonus|pull[- ]out|includes?|inside|exclusive)\s+(?:a\s+)?(?:poster|demo (?:disc|disk|CD)|disc|disk|insert|sticker|map|cover disc)\b", RegexOptions.IgnoreCase)) Add("includedExtras", line, line + Geometry(l, layout));
            previous = platformHeading ? null : l;
        }
        if (page == 1 && coverHeadings.Count > 0)
        {
            var candidates = coverHeadings.GroupBy(x => Compact(x.Game)).Select(g => g.OrderByDescending(x=>x.Line.Height).First()).OrderByDescending(x=>x.Line.Height).ToArray();
            var first = candidates[0];
            // Size is supporting evidence only. Never replace a known cover based on a tagline.
            if (prominent(first.Line) && (candidates.Length == 1 || first.Line.Height >= candidates[1].Line.Height * 1.6)) Add("coverSubject", first.Game, first.Line.Text + Geometry(first.Line, layout), "Visual cover candidate");
            else foreach (var c in candidates.Take(6)) Add("coverSubject", c.Game, c.Line.Text + Geometry(c.Line, layout), "Ambiguous cover heading");
        }
        // A cover subject need not be a game; use an explicit, non-game cover-story label only.
        if (toc && !suggestions.Any(x=>x.Page==page && x.Field=="coverSubject"))
            MatchField("coverSubject", @"^\s*cover (?:story|feature)\s*:\s*([^\r\n]{3,100})", text);
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
    private static int RomanOrNumber(string s)
    {
        if(int.TryParse(s,out int v))return v;
        int total=0,last=0; foreach(char c in s.ToUpperInvariant().Reverse()) {int n=c switch{'I'=>1,'V'=>5,'X'=>10,'L'=>50,'C'=>100,'D'=>500,'M'=>1000,_=>0}; if(n==0)return 0;total+=n<last?-n:n;last=Math.Max(last,n);}return total;
    }
    private static bool ValidUpc(string s)
    {
        if(s.Length is not (12 or 13)||!s.All(char.IsAsciiDigit)) return false;
        int sum=0;for(int i=s.Length-2,k=0;i>=0;i--,k++)sum+=(s[i]-'0')*(k%2==0?3:1);return (10-sum%10)%10==s[^1]-'0';
    }
    private sealed record GameHit(string Game,bool Whole,bool Fuzzy);
    private static IEnumerable<GameHit> FindGames(Dictionary<string,CatalogTitle> dictionary,string line,bool titleContext,bool listing,bool context)
    {
        var original=Words.Matches(line).Select(m=>m.Value).ToArray();var tokens=original.Select(x=>x.ToLowerInvariant()).ToArray();
        var whole=Normalize(Regex.Replace(line,@"^\d{1,3}[A-Za-z]?[.\s]+|\.{2,}\s*\d{1,3}$",""));
        var used=new HashSet<string>(StringComparer.Ordinal); 
        for(int i=0;i<tokens.Length;i++)
        {
            for(int n=Math.Min(12,tokens.Length-i);n>=1;n--)
            {
                string key=string.Join(" ",tokens,i,n);
                if(!dictionary.TryGetValue(key,out var hit))continue;
                bool entire=key==whole;
                bool sequel=i+n<tokens.Length&&Regex.IsMatch(tokens[i+n],@"^(?:[0-9]{1,4}|i{1,3}|iv|v|vi{1,3}|ix|x|xi{1,3})$");
                if(sequel || (i+n<tokens.Length && Regex.IsMatch(tokens[i+n],@"^(?:vs|versus)$")))continue;
                if(i+n<tokens.Length&&Regex.IsMatch(tokens[i+n],@"^(?:series|franchise|movie|film|saga|meat|contest|universe)$"))continue;
                if(n==1&&!((titleContext&&entire)||listing&&(i==0||i+n==tokens.Length)||context&&char.IsUpper(original[i][0])&&key.Length>=5))continue;
                if(!titleContext&&(!char.IsUpper(original[i][0])||(!context&&n<=2)))continue;
                if(!listing&&!entire&&!context&&n<=2&&!titleContext)continue;
                if(hit.Alias&&!titleContext&&!context)continue;
                if(used.Add(Compact(hit.Display)))yield return new(hit.Display,entire,false);
                i+=n-1;break;
            }
        }
        if(!listing)yield break;
        // Conservative one-character OCR recovery only for isolated contents entries.
        foreach(var part in line.Split([',',';'],StringSplitOptions.RemoveEmptyEntries))
        {
            var key=Normalize(part); if(key.Length<5||key.Length>70||key.Split(' ').Length>9||dictionary.ContainsKey(key)||key.Split(' ').Length<2)continue;
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
