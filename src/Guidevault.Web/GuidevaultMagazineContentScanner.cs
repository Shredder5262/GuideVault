using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

// Read-only magazine OCR and conservative metadata suggestions. No source or metadata writes.
public static class GuidevaultMagazineContentScanner
{
    public sealed record Suggestion(string Field, string Value, int Page, string Evidence, string Confidence);
    public sealed record ScanResult(int TotalPages, int Start, int Scanned, int ReadablePages, List<Suggestion> Suggestions, List<string> Warnings);
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly Regex Words = new(@"[\p{L}\p{N}]+", RegexOptions.Compiled);
    private static readonly Regex YearPattern = new(@"\b(?:19|20)\d{2}\b", RegexOptions.Compiled);
    private const string Months = "January|February|March|April|May|June|July|August|September|October|November|December|Jan|Feb|Mar|Apr|Jun|Jul|Aug|Sep|Sept|Oct|Nov|Dec";
    private static readonly string[] Sections = ["Table of Contents", "Contents", "Reviews", "Previews", "Features", "News", "Letters", "Editorial", "Walkthroughs", "Strategy Guides", "Tips and Tricks", "Cheat Codes", "Classified Information", "Pak Watch", "Counselors Corner", "Now Playing", "Reader's Choice", "Top 10", "Hardware", "Interviews"];
    private static readonly (string Pattern, string Name)[] Platforms = [
        (@"\b(?:NES|Nintendo Entertainment System)\b", "Nintendo Entertainment System"),
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
        (@"\b(?:TurboGrafx-16|PC Engine)\b", "NEC TurboGrafx-16"), (@"\bAtari Jaguar\b", "Atari Jaguar"), (@"\b3DO\b", "3DO Interactive Multiplayer")];

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
            if (start == 0) result.Suggestions.Add(new("pageCount", total.ToString(), 0, "Actual page count from document reader.", "Measured"));
            var dictionary = BuildDictionary(games, GetYear(item.CoverDate + " " + item.PublicationDate + " " + item.Year));
            for (int i = start; i < Math.Min(start + count, total); i++)
            {
                token.ThrowIfCancellationRequested();
                string text = "";
                try
                {
                    if (pdf)
                    {
                        var page = (i + 1).ToString();
                        text = await Run("pdftotext", ["-f", page, "-l", page, "-layout", item.Path, "-"], token);
                        if (Words.Matches(text).Count < 12)
                        {
                            var prefix = Path.Combine(temp, "page");
                            await Run("pdftoppm", ["-f", page, "-l", page, "-scale-to", "2400", "-singlefile", "-png", item.Path, prefix], token);
                            text = await Run("tesseract", [prefix + ".png", "stdout", "-l", "eng", "--psm", i == 0 ? "11" : "3"], token);
                        }
                    }
                    else
                    {
                        var image = await ArchiveReader.GetImagePageAsync(item.Path, i);
                        if (image is null) throw new InvalidOperationException("Page image unavailable.");
                        if (image.Value.Bytes.Length > 40_000_000) throw new InvalidOperationException("Page exceeds the 40 MB OCR limit.");
                        var input = Path.Combine(temp, "page.img");
                        await File.WriteAllBytesAsync(input, image.Value.Bytes, token);
                        text = await Run("tesseract", [input, "stdout", "-l", "eng", "--psm", i == 0 ? "11" : "3"], token);
                    }
                    if (Words.Matches(text).Count >= 3)
                    {
                        AnalyzePage(item, dictionary, text, i + 1, result.Suggestions);
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

    private static int ParsePdfPages(string text) => int.TryParse(Regex.Match(text, @"(?im)^Pages:\s*(\d+)").Groups[1].Value, out int n) ? n : 0;
    private static int GetYear(string value) => int.TryParse(YearPattern.Match(value ?? "").Value, out int n) ? n : 0;
    private static string Normalize(string value) => string.Join(" ", Words.Matches(value ?? "").Select(m => m.Value.ToLowerInvariant()));
    private static Dictionary<string, string> BuildDictionary(IEnumerable<GuidevaultLaunchBoxGame> games, int year)
    {
        var names = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var game in games)
        {
            int release = GetYear(game.ReleaseYear);
            if (year > 0 && release > year + 2) continue;
            if (string.IsNullOrWhiteSpace(game.Title)) continue;
            foreach (var alias in new[] { game.Title }.Concat(game.AlternateNames ?? new()))
            {
                string key = Normalize(alias);
                if (key.Length < 4 || key.Split(' ').Length > 9) continue;
                if (!names.TryGetValue(key, out var values)) names[key] = values = new(StringComparer.OrdinalIgnoreCase);
                values.Add(game.Title);
            }
        }
        return names.Where(x => x.Value.Count == 1).ToDictionary(x => x.Key, x => x.Value.First(), StringComparer.Ordinal);
    }
    private static void AnalyzePage(LibraryItem item, Dictionary<string, string> dictionary, string text, int page, List<Suggestion> suggestions)
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
        bool toc = lines.Any(l => Regex.IsMatch(l, @"^(?:table of\s+)?contents\s*$", RegexOptions.IgnoreCase));
        if (page <= 8)
        {
            MatchField("issueNumber", @"\b(?:issue|no\.|number)\s*#?\s*(\d{1,4})\b", text);
            MatchField("volume", @"\b(?:volume|vol\.?)\s*(\d{1,3})\b", text);
            if (page <= 2)
            {
                var date = Regex.Match(text, $@"\b((?:{Months})(?:\s*[/–-]\s*(?:{Months}))?\s+(?:19|20)\d{{2}})\b", RegexOptions.IgnoreCase);
                if (date.Success) { Add("coverDate", date.Groups[1].Value, date.Value); Add("year", GetYear(date.Value).ToString(), date.Value); }
                var publication = (item.MagazineTitle ?? "").Trim();
                if (publication.Length > 0 && Normalize(text).Contains(Normalize(publication), StringComparison.Ordinal)) Add("magazineTitle", publication, "Existing publication name appears on page.");
            }
            MatchField("publisher", @"^\s*(?:published by|publisher\s*:)\s*([^\r\n]{3,100})", text);
            MatchField("writer", @"^\s*(?:editor-in-chief|editor in chief|editor\s*:)\s*([^\r\n]{3,100})", text);
            MatchField("region", @"\b(?:printed|published) in (?:the )?((?:United States(?: of America)?|USA|United Kingdom|Canada|Australia|Japan|Germany|France))\b", text);
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
        var coverGames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in lines)
        {
            var tokens = Normalize(line).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) continue;
            bool heading = toc && Regex.IsMatch(line, @"(?:^\d{1,3}\s+|\s+\d{1,3}$)");
            string headingText = Regex.Replace(line, @"^\d{1,3}\s+|\s+\d{1,3}$", "").Trim();
            string headingKey = Normalize(headingText);
            var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < tokens.Length; i++)
            {
                for (int n = Math.Min(9, tokens.Length - i); n >= 1; n--)
                {
                    string key = string.Join(" ", tokens, i, n);
                    if (n == 1 && key != headingKey) continue; // Avoid common-word titles in prose.
                    if (n == 1 && !(page == 1 || heading)) continue;
                    if (!dictionary.TryGetValue(key, out var game)) continue;
                    if (matched.Add(game))
                    {
                        if (page == 1 && key == headingKey) coverGames.Add(game);
                        else if (page > 1 && heading) Add("featuredGames", game, line, "Contents heading");
                        else Add("mentionedGames", game, line, "Text match");
                    }
                    i += n - 1; break;
                }
            }
            foreach (var (pattern, name) in Platforms)
                if (Regex.IsMatch(line, pattern, RegexOptions.IgnoreCase)) Add("featuredPlatforms", name, line, "Explicit text");
            foreach (var section in Sections)
                if (string.Equals(Normalize(headingText), Normalize(section), StringComparison.Ordinal)) Add("specialFeatures", section, line, "Heading");
            if (page <= 3 && Regex.IsMatch(line, @"\b(?:free poster|bonus poster|pull[- ]out poster|demo disc|bonus disc|free insert)\b", RegexOptions.IgnoreCase)) Add("includedExtras", line, line);
        }
        if (coverGames.Count == 1) Add("coverSubject", coverGames.First(), "Single exact cover heading matched to LaunchBox.");
        else if (coverGames.Count > 1) foreach (var game in coverGames) Add("mentionedGames", game, "Multiple titles on cover; cover subject ambiguous.");
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
