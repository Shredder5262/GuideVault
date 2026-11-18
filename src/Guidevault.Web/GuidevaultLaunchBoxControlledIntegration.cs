// GUIDEVAULT_LAUNCHBOX_CONTROLLED_V1
// Read-only, container-visible LaunchBox catalog source and safe manual proposal scanner.
// This file does not modify LaunchBox XML and never persists publication matches.
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

public sealed class GuidevaultLaunchBoxSourceSettings
{
    public string RootPath { get; set; } = string.Empty;
}

public sealed class GuidevaultLaunchBoxSourceSettingsStore
{
    private readonly string _path;
    private readonly object _gate = new();
    private GuidevaultLaunchBoxSourceSettings _settings;

    public GuidevaultLaunchBoxSourceSettingsStore(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        try
        {
            _settings = File.Exists(_path)
                ? JsonSerializer.Deserialize<GuidevaultLaunchBoxSourceSettings>(
                    File.ReadAllText(_path), new JsonSerializerOptions(JsonSerializerDefaults.Web))
                    ?? new GuidevaultLaunchBoxSourceSettings()
                : new GuidevaultLaunchBoxSourceSettings();
        }
        catch { _settings = new GuidevaultLaunchBoxSourceSettings(); }
    }

    public GuidevaultLaunchBoxSourceSettings Get()
    {
        lock (_gate) return new GuidevaultLaunchBoxSourceSettings { RootPath = _settings.RootPath };
    }

    public GuidevaultLaunchBoxSourceSettings Save(GuidevaultLaunchBoxSourceSettings input)
    {
        var root = (input.RootPath ?? string.Empty).Trim();
        if (root.Length > 1024 || (root.Length > 0 && !Path.IsPathFullyQualified(root)))
            throw new ArgumentException("Enter an absolute path accessible inside the GuideVault server/container, such as /launchbox.");
        // Never guess/convert a remote Windows path on a Linux Docker host.
        lock (_gate)
        {
            var updated = new GuidevaultLaunchBoxSourceSettings { RootPath = root };
            GuidevaultAtomicFile.WriteAllText(_path,
                JsonSerializer.Serialize(updated, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
            _settings = updated;
            return GetUnsafeCopy();
        }
    }

    private GuidevaultLaunchBoxSourceSettings GetUnsafeCopy() =>
        new() { RootPath = _settings.RootPath };
}

public sealed class GuidevaultLaunchBoxScopedRequest
{
    public string Platform { get; set; } = string.Empty;
    public string MatchType { get; set; } = "Manual";
    public string ExpectedSha256 { get; set; } = string.Empty;
}

public static class GuidevaultLaunchBoxCatalogReader
{
    private static string PlatformsFolder(string root)
    {
        if (string.IsNullOrWhiteSpace(root)) throw new InvalidOperationException("Set the LaunchBox root path in Settings → Integrations first.");
        if (!Path.IsPathFullyQualified(root)) throw new InvalidOperationException("LaunchBox root must be a server/container-visible absolute path.");
        var full = Path.GetFullPath(root);
        var folder = Path.Combine(full, "Data", "Platforms");
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException($"LaunchBox Data/Platforms not found at: {folder}. Mount the LaunchBox SMB share read-only inside the Docker container.");
        return folder;
    }

    public static object Inspect(string root)
    {
        var folder = PlatformsFolder(root);
        var platforms = Directory.EnumerateFiles(folder, "*.xml", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToArray();
        return new { ok = platforms.Length > 0, rootPath = root, platformCount = platforms.Length, platforms,
            note = "Only catalog XML is read; LaunchBox is never modified. Paths inside game XML are not used as match keys." };
    }

    private static string GetPlatformFile(string root, string platform)
    {
        if (string.IsNullOrWhiteSpace(platform)) throw new ArgumentException("Select one LaunchBox platform.");
        var folder = PlatformsFolder(root);
        // Resolve by filename from the directory, never by concatenating untrusted input.
        var found = Directory.EnumerateFiles(folder, "*.xml", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(file => string.Equals(Path.GetFileNameWithoutExtension(file), platform, StringComparison.OrdinalIgnoreCase));
        if (found is null) throw new FileNotFoundException($"Platform XML not found for {platform}.");
        return found;
    }

    public static (List<GuidevaultLaunchBoxGame> Games, string Sha256, string Filename) ReadPlatform(string root, string platform)
    {
        var file = GetPlatformFile(root, platform);
        var bytes = File.ReadAllBytes(file);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 80_000_000
        });
        var document = XDocument.Load(reader, LoadOptions.None);
        static string Field(XElement element, string name) => element.Elements()
            .FirstOrDefault(x => string.Equals(x.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))?.Value.Trim() ?? string.Empty;
        var games = new List<GuidevaultLaunchBoxGame>();
        foreach (var element in document.Descendants().Where(x => x.Name.LocalName.Equals("Game", StringComparison.OrdinalIgnoreCase)))
        {
            var id = Field(element, "ID");
            var title = Field(element, "Title");
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(id)) continue;
            var releaseDate = Field(element, "ReleaseDate");
            var releaseYear = releaseDate.Length >= 4 && int.TryParse(releaseDate[..4], NumberStyles.None, CultureInfo.InvariantCulture, out var year)
                ? year.ToString(CultureInfo.InvariantCulture) : string.Empty;
            games.Add(new GuidevaultLaunchBoxGame
            {
                Id = id, Title = title,
                SortTitle = Field(element, "SortTitle"),
                Platform = string.IsNullOrWhiteSpace(Field(element, "Platform")) ? platform : Field(element, "Platform"),
                Region = Field(element, "Region"), ReleaseYear = releaseYear,
                Developer = Field(element, "Developer"), Publisher = Field(element, "Publisher"),
                Series = Field(element, "Series"),
                DatabaseId = Field(element, "DatabaseID"),
                ApplicationPath = Field(element, "ApplicationPath"),
                ManualPath = Field(element, "ManualPath"),
                Source = "LaunchBox XML"
            });
        }
        if (games.Count == 0) throw new InvalidDataException("No valid game IDs/titles were found. Import refused; check this platform XML before continuing.");
        var duplicate = games.GroupBy(g => g.Id, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null) throw new InvalidDataException($"Duplicate LaunchBox game ID {duplicate.Key} in XML; import refused.");
        if (games.Any(g => !string.Equals(g.Platform, platform, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Some games declare a platform different from the selected XML filename; import refused until corrected.");
        return (games, hash, Path.GetFileName(file));
    }

    public static object Preview(string root, string platform, GuidevaultLaunchBoxIntegrationData current)
    {
        var (incoming, hash, filename) = ReadPlatform(root, platform);
        var old = current.Games.Where(g => string.Equals(g.Platform, platform, StringComparison.OrdinalIgnoreCase)).ToList();
        var oldIds = old.Select(g => g.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var newIds = incoming.Select(g => g.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var removed = old.Where(g => !newIds.Contains(g.Id)).ToArray();
        var added = incoming.Where(g => !oldIds.Contains(g.Id)).ToArray();
        var removedNames = removed.Select(g => $"{g.Platform}|{g.Title}" ).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var replacedIds = added.Where(g => removedNames.Contains($"{g.Platform}|{g.Title}")).ToArray();
        return new
        {
            platform, filename, sha256 = hash,
            imported = false, count = incoming.Count, previousCount = old.Count,
            additions = added.Length, removals = removed.Length,
            possibleChangedIds = replacedIds.Length,
            examplesAdded = added.Take(8).Select(g => new { g.Title, g.Id }),
            examplesRemoved = removed.Take(8).Select(g => new { g.Title, g.Id }),
            warning = replacedIds.Length > 0
                ? "Possible ID changes detected. Confirm the synchronized LaunchBox XML retains original IDs before importing."
                : "No document associations will be created. Check counts and changes before importing."
        };
    }
}

public static class GuidevaultLaunchBoxControlledManualScanner
{
    // A strict read-only first pass, not a replacement for a complete identity/edition engine.
    // Never auto-approves, persists or restores associations. Returns evidence to a human.
    public static object Preview(string platform, string matchType,
        IReadOnlyList<GuidevaultLaunchBoxGame> games, IReadOnlyList<LibraryItem> documents)
    {
        if (!string.Equals(matchType, "Manual", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The first controlled scanner supports Manuals only. Guides and magazines require separate coverage rules.");
        if (string.IsNullOrWhiteSpace(platform)) throw new ArgumentException("Select a LaunchBox platform.");
        var selectedGames = games.Where(g => string.Equals(g.Platform, platform, StringComparison.OrdinalIgnoreCase)).ToArray();
        var manuals = documents.Where(i => string.Equals(i.Kind, "Manual", StringComparison.OrdinalIgnoreCase)).ToArray();
        var byName = new Dictionary<string, List<LibraryItem>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in manuals)
        {
            // Explicit GameTitle is authoritative for discovering candidates; don't let a
            // contradictory publication title override a populated GameTitle.
            var rawName = !string.IsNullOrWhiteSpace(item.GameTitle)
                ? item.GameTitle : !string.IsNullOrWhiteSpace(item.ManualTitle) ? item.ManualTitle : item.Title;
            var name = NormalizeGameName(rawName, publication: true);
            if (name.Length < 2) continue;
            if (!byName.TryGetValue(name, out var list)) byName[name] = list = new List<LibraryItem>();
            list.Add(item);
        }
        var rows = new List<object>();
        int strong = 0, review = 0, conflict = 0, unmatched = 0;
        foreach (var game in selectedGames.OrderBy(g => g.Title, StringComparer.OrdinalIgnoreCase))
        {
            var keys = new[] { game.Title, game.SortTitle }
                .Concat(game.AlternateNames ?? new List<string>())
                .Select(title => NormalizeGameName(title, publication: false))
                .Where(name => name.Length >= 2).Distinct(StringComparer.OrdinalIgnoreCase);
            var candidates = keys.Where(byName.ContainsKey).SelectMany(key => byName[key])
                .GroupBy(i => i.Id, StringComparer.OrdinalIgnoreCase).Select(group => group.First()).ToArray();
            if (candidates.Length == 0) { unmatched++; continue; }
            foreach (var item in candidates)
            {
                var namedPlatforms = new[] { item.System, item.PrimarySystem, item.PlatformFocus }
                    .Concat(item.AssociatedPlatforms ?? Array.Empty<string>())
                    .Concat(item.CoveredPlatforms ?? Array.Empty<string>())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(NormalizePlatform).Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                var gamePlatform = NormalizePlatform(game.Platform);
                var hasPlatform = namedPlatforms.Contains(gamePlatform, StringComparer.OrdinalIgnoreCase);
                var containsKnownOther = namedPlatforms.Any(p => !string.Equals(p, gamePlatform, StringComparison.OrdinalIgnoreCase));
                var yearConflict = int.TryParse(game.ReleaseYear, out var gameYear) &&
                    int.TryParse(item.GameReleaseYear, out var itemYear) && Math.Abs(gameYear - itemYear) > 1;
                var classification = yearConflict || (!hasPlatform && containsKnownOther)
                    ? "Conflict" : hasPlatform ? "Strong candidate" : "Needs review";
                var reason = yearConflict ? "Explicit game-release years conflict" :
                    !hasPlatform && containsKnownOther ? "Publication names another platform; verify edition" :
                    hasPlatform ? "Exact game-title identity and matching platform metadata" :
                    "Exact game-title identity, but platform metadata is missing";
                if (classification == "Conflict") conflict++;
                else if (classification == "Strong candidate") strong++;
                else review++;
                rows.Add(new { gameId = game.Id, gameTitle = game.Title, gamePlatform = game.Platform,
                    publicationId = item.Id, publicationTitle = item.Title,
                    publicationSystem = string.Join(", ", namedPlatforms),
                    classification, reason,
                    coverUrl = "/api/items/" + Uri.EscapeDataString(item.Id) + "/cover-thumb?w=160" });
            }
        }
        return new { platform, matchType = "Manual", dryRun = true, changedRecords = 0,
            games = selectedGames.Length, manualsInLibrary = manuals.Length,
            candidatePairs = rows.Count, strongCandidates = strong, needsReview = review,
            conflicts = conflict, gamesWithNoExactTitleCandidate = unmatched,
            truncated = rows.Count > 500, rows = rows.Take(500).ToArray(),
            note = "Strict exact-title discovery. No links are written. Review platform/year conflicts and metadata gaps; approval is not enabled in this first update." };
    }

    private static string NormalizePlatform(string? value)
    {
        var text = Regex.Replace((value ?? "").ToLowerInvariant(), @"[^a-z0-9]+", " ").Trim();
        return text switch
        {
            "nes" or "famicom" or "nintendo entertainment system" => "nintendo entertainment system",
            "snes" or "super nes" or "super nintendo entertainment system" => "super nintendo entertainment system",
            "psx" or "ps1" or "sony playstation" or "playstation" => "sony playstation",
            "3do" or "3do interactive multiplayer" => "3do interactive multiplayer",
            "n64" or "nintendo 64" => "nintendo 64",
            "mega drive" or "sega mega drive" or "sega genesis" or "genesis" => "sega genesis",
            "multi platform" or "multiplatform" or "mixed" or "unknown" or "unsorted" or "manuals" or "manual" => "",
            _ => text
        };
    }

    private static string NormalizeGameName(string? name, bool publication)
    {
        var text = (name ?? string.Empty).Trim().ToLowerInvariant();
        if (publication)
            text = Regex.Replace(text, @"\s*[-:]?\s*(instruction(?:s)?(?:\s+booklet)?|instruction\s+manual|manual|owner'?s\s+manual)\s*$", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"[^a-z0-9]+", " ").Trim();
        text = Regex.Replace(text, @"\b(ii|iii|iv|v|vi|vii|viii|ix)\b", match => match.Value switch
        {
            "ii" => "2", "iii" => "3", "iv" => "4", "v" => "5", "vi" => "6", "vii" => "7", "viii" => "8", "ix" => "9", _ => match.Value
        });
        return Regex.Replace(text, @"\s+", " ").Trim();
    }
}
