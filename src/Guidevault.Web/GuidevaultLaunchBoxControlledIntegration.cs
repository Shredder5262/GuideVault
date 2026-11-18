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
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 100;
    public string StatusFilter { get; set; } = "pending";
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
    // GUIDEVAULT_MANUAL_IDENTITY_PAGINATION_V13
    // Manual-only dry run. Strong requires matching primary game/title identity AND
    // primary platform; metadata aliases cannot override different numbered games.
    // Read-only scanner for RELEASE-SPECIFIC MANUALS only. Cross-platform literature is
    // deliberately out of scope; do not copy these rules to magazines or strategy guides.
    public static object Preview(string platform, string matchType,
        IReadOnlyList<GuidevaultLaunchBoxGame> games, IReadOnlyList<LibraryItem> documents,
        string? launchBoxRoot, int page = 1, int pageSize = 100,
        IReadOnlyList<GuidevaultLaunchBoxMatch>? savedMatches = null, string statusFilter = "pending")
    {
        // GUIDEVAULT_MANUAL_BATCH_REVIEW_V17: filter saved decisions before paging.
        // Pending is the default queue; history is separately inspectable/undoable.
        statusFilter = (statusFilter ?? "pending").Trim().ToLowerInvariant();
        if (statusFilter is not ("pending" or "approved" or "rejected" or "metadata" or "all"))
            throw new ArgumentException("Invalid manual review queue.");
        if (!string.Equals(matchType, "Manual", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The controlled scanner currently supports Manuals only.");
        if (string.IsNullOrWhiteSpace(platform)) throw new ArgumentException("Select a LaunchBox platform.");
        if (page < 1 || pageSize < 25 || pageSize > 500)
            throw new ArgumentException("Page must be at least 1 and page size between 25 and 500.");

        var selectedGames = games.Where(g => string.Equals(g.Platform, platform, StringComparison.OrdinalIgnoreCase)).ToArray();
        var targetPlatform = NormalizePlatform(platform);
        if (targetPlatform.Length == 0) throw new ArgumentException("Select a specific LaunchBox platform, not an unknown or multi-platform category.");
        var manuals = documents.Where(i => string.Equals(i.Kind, "Manual", StringComparison.OrdinalIgnoreCase)).ToArray();
        var byName = new Dictionary<string, List<LibraryItem>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in manuals)
        {
            // GameTitle is more specific than a document's filename or display title.
            var rawName = !string.IsNullOrWhiteSpace(item.GameTitle)
                ? item.GameTitle : !string.IsNullOrWhiteSpace(item.ManualTitle) ? item.ManualTitle : item.Title;
            var name = NormalizeGameName(rawName, publication: true);
            if (name.Length < 2) continue;
            if (!byName.TryGetValue(name, out var list)) byName[name] = list = new List<LibraryItem>();
            list.Add(item);
        }

        var strongRows = new List<object>();
        var reviewRows = new List<object>();
        var excludedExamples = new List<object>();
        var identityExclusions = new List<object>();
        int strong = 0, review = 0, excluded = 0, excludedByIdentity = 0, withoutExactTitle = 0, withoutEligible = 0;
        int pendingPairs = 0, approvedPairs = 0, rejectedPairs = 0, metadataPairs = 0;
        foreach (var game in selectedGames.OrderBy(g => g.Title, StringComparer.OrdinalIgnoreCase))
        {
            var keys = new[] { game.Title, game.SortTitle }
                .Concat(game.AlternateNames ?? new List<string>())
                .Select(title => NormalizeGameName(title, publication: false))
                .Where(name => name.Length >= 2).Distinct(StringComparer.OrdinalIgnoreCase);
            var primaryGameName = NormalizeGameName(game.Title, publication: false);
            var candidates = keys.Where(byName.ContainsKey).SelectMany(key => byName[key])
                .GroupBy(i => i.Id, StringComparer.OrdinalIgnoreCase).Select(group => group.First()).ToArray();
            if (candidates.Length == 0) { withoutExactTitle++; withoutEligible++; continue; }

            bool eligible = false;
            foreach (var item in candidates)
            {
                var rawManualGameName = !string.IsNullOrWhiteSpace(item.GameTitle)
                    ? item.GameTitle : !string.IsNullOrWhiteSpace(item.ManualTitle) ? item.ManualTitle : item.Title;
                var primaryManualName = NormalizeGameName(rawManualGameName, publication: true);
                var displayedManualName = NormalizeGameName(item.Title, publication: true);
                var manualTitleName = NormalizeGameName(item.ManualTitle, publication: true);
                // A normalized key may have come from a stale AlternateNames/SortTitle or
                // inaccurate document GameTitle field. Neither may silently turn a sequel
                // into the same game. Also compare the *displayed* publication title.
                bool contradictorySequel = DifferentNumberedGame(primaryGameName, primaryManualName)
                    || DifferentNumberedGame(primaryGameName, displayedManualName)
                    || DifferentNumberedGame(primaryManualName, displayedManualName)
                    || DifferentNumberedGame(primaryGameName, manualTitleName)
                    || DifferentNumberedGame(primaryManualName, manualTitleName);
                if (contradictorySequel)
                {
                    excludedByIdentity++;
                    if (identityExclusions.Count < 20)
                        identityExclusions.Add(new { gameTitle = game.Title, publicationTitle = item.Title,
                            publicationGameTitle = rawManualGameName, reason = "Conflicting numbered game or manual title" });
                    continue;
                }
                bool samePrimaryGame = string.Equals(primaryGameName, primaryManualName,
                    StringComparison.OrdinalIgnoreCase);
                // Primary title must match to become Strong. Sort/alternate-name matches
                // remain visible, but require human verification.
                // Primary manual fields describe *which physical/platform edition* this is.
                // Other associated/covered platforms are weaker hints, never sufficient to
                // override a contradictory primary manual system (e.g. an Arcade 1942 manual).
                var primary = ManualPlatforms(new[] { item.System, item.PrimarySystem, item.PlatformFocus });
                var associated = ManualPlatforms((item.AssociatedPlatforms ?? Array.Empty<string>())
                    .Concat(item.CoveredPlatforms ?? Array.Empty<string>()));
                var declared = primary.Concat(associated).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                bool primaryIsTarget = primary.Contains(targetPlatform, StringComparer.OrdinalIgnoreCase);
                bool associatedIsTarget = associated.Contains(targetPlatform, StringComparer.OrdinalIgnoreCase);

                // Important: a manual explicitly for another platform is outside this scan,
                // even if one of its general associated-platform tags names this platform.
                // Do not present it as a conflict to be dispositioned.
                bool explicitlyOther = primary.Length > 0 && !primaryIsTarget;
                bool onlyOtherAssociated = primary.Length == 0 && associated.Length > 0 && !associatedIsTarget;
                if (explicitlyOther || onlyOtherAssociated)
                {
                    excluded++;
                    if (excludedExamples.Count < 12)
                        excludedExamples.Add(new { gameTitle = game.Title, publicationTitle = item.Title,
                            publicationSystem = string.Join(", ", declared),
                            reason = explicitlyOther ? "Manual declares a different primary platform" : "Only other platforms are identified" });
                    continue;
                }
                eligible = true;

                var yearConflict = int.TryParse(game.ReleaseYear, out var gameYear) &&
                    int.TryParse(item.GameReleaseYear, out var itemYear) && Math.Abs(gameYear - itemYear) > 1;
                var contradictoryPrimary = primaryIsTarget && primary.Any(p => !string.Equals(p, targetPlatform, StringComparison.OrdinalIgnoreCase));
                // Broad associated-platform tags cannot prove a release-specific manual.
                var verifiedPlatform = primaryIsTarget && !contradictoryPrimary;
                var strongCandidate = verifiedPlatform && !yearConflict && samePrimaryGame;
                var classification = strongCandidate ? "Strong candidate" : "Needs review";
                var reason = !samePrimaryGame
                    ? "Matched through LaunchBox sort/alternate name, not primary game title; verify the alias"
                    : contradictoryPrimary
                    ? "Conflicting primary manual platform fields; verify the edition"
                    : yearConflict
                        ? "Exact game title and target manual platform, but explicit game-release years differ"
                        : verifiedPlatform
                            ? "Exact game title and matching primary manual platform"
                            : associatedIsTarget
                                ? "Target platform appears only in associated/covered tags; verify manual's actual system"
                                : "Manual platform missing; verify the edition before linking";
                var savedStatus = savedMatches?.FirstOrDefault(m =>
                    string.Equals(m.LaunchBoxGameId, game.Id, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(m.GuideVaultItemId, item.Id, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(m.MatchType, "Manual", StringComparison.OrdinalIgnoreCase))?.MatchStatus ?? string.Empty;
                var isApproved = string.Equals(savedStatus, "Confirmed", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(savedStatus, "AutoMatched", StringComparison.OrdinalIgnoreCase);
                var isRejected = string.Equals(savedStatus, "Rejected", StringComparison.OrdinalIgnoreCase);
                var isMetadata = string.Equals(savedStatus, "NeedsMetadata", StringComparison.OrdinalIgnoreCase);
                if (isApproved) approvedPairs++;
                else if (isRejected) rejectedPairs++;
                else if (isMetadata) metadataPairs++;
                else pendingPairs++;
                var show = statusFilter switch
                {
                    "pending" => !isApproved && !isRejected && !isMetadata,
                    "approved" => isApproved,
                    "rejected" => isRejected,
                    "metadata" => isMetadata,
                    _ => true
                };
                if (!show) continue;
                if (strongCandidate) strong++; else review++;
                var row = new { gameId = game.Id, gameTitle = game.Title, gamePlatform = game.Platform,
                    gameBoxFrontUrl = "/api/integrations/launchbox/games/" + Uri.EscapeDataString(game.Id) + "/box-front",
                    publicationId = item.Id, publicationTitle = item.Title,
                    publicationSystem = string.Join(", ", declared), classification, reason,
                    matchBasis = samePrimaryGame ? "Primary game title" : "Sort/alternate name",
                    manualGameTitle = rawManualGameName,
                    savedStatus,
                    coverUrl = "/api/items/" + Uri.EscapeDataString(item.Id) + "/cover-thumb?w=360" };
                if (strongCandidate) strongRows.Add(row); else reviewRows.Add(row);
            }
            if (!eligible) withoutEligible++;
        }

        var includedCount = strong + review;
        var pageCount = Math.Max(1, (int)Math.Ceiling(includedCount / (double)pageSize));
        var actualPage = Math.Min(page, pageCount);
        var offset = (actualPage - 1) * pageSize;
        return new { platform, matchType = "Manual", dryRun = true, changedRecords = 0,
            games = selectedGames.Length, manualsInLibrary = manuals.Length,
            candidatePairs = includedCount, strongCandidates = strong, needsReview = review,
            statusFilter, pendingPairs, approvedPairs, rejectedPairs, metadataPairs,
            conflicts = 0, excludedByPlatform = excluded, excludedByIdentity,
            excludedExamples = excludedExamples.ToArray(), identityExclusions = identityExclusions.ToArray(),
            gamesWithNoExactTitleCandidate = withoutExactTitle,
            gamesWithNoEligibleCandidate = withoutEligible,
            artworkNote = GuidevaultLaunchBoxBoxFronts.AvailabilityNote(launchBoxRoot, platform),
            page = actualPage, pageSize, pageCount,
            firstRow = includedCount == 0 ? 0 : offset + 1,
            lastRow = Math.Min(includedCount, offset + pageSize),
            rows = strongRows.Concat(reviewRows).Skip(offset).Take(pageSize).ToArray(),
            note = "Manuals only: excluded wrong platforms and conflicting numbered games. Scanning never writes links. Pending hides saved decisions; review Approved/Rejected/Needs Metadata using the queue filter." };
    }

    // GUIDEVAULT_MANUAL_RECOVERY_V18: reuse the release-specific safety checks
    // for a reviewer-directed correction outside the exact-title candidate scan.
    public static string NormalizedPlatform(string? platform) => NormalizePlatform(platform);
    public static string[] PrimaryManualPlatforms(LibraryItem item) =>
        ManualPlatforms(new[] { item.System, item.PrimarySystem, item.PlatformFocus });

    public static string? CorrectionSafetyIssue(GuidevaultLaunchBoxGame game, LibraryItem item)
    {
        var target = NormalizePlatform(game.Platform);
        var primaries = PrimaryManualPlatforms(item);
        if (target.Length == 0 || primaries.Length == 0)
            return "The manual's primary platform is missing; correct its platform metadata before approving a manual correction.";
        if (!primaries.Contains(target, StringComparer.OrdinalIgnoreCase) || primaries.Any(p => p != target))
            return "The manual explicitly identifies another primary system; correct its platform metadata first.";
        var gameTitle = NormalizeGameName(game.Title, publication: false);
        var titles = new[] { item.Title, item.GameTitle, item.ManualTitle }
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => NormalizeGameName(t, publication: true))
            .Where(t => t.Length > 0).ToArray();
        if (titles.Any(title => DifferentNumberedGame(gameTitle, title)))
            return "A numbered sequel conflicts with the proposed game's identity. Correct source metadata rather than overriding it.";
        return null;
    }

    // A sequel number is part of a game's identity, not a fuzzy alias.
    // Compare a numbered title against the same series with a different number
    // or against the base title lacking a number. Do not confuse 1942 and 1943:
    // those titles are pure numbers and do not have an identifiable series stem.
    private static bool DifferentNumberedGame(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)
            || string.Equals(left, right, StringComparison.OrdinalIgnoreCase)) return false;
        static (string Stem, string Number)? NumberedTitle(string title)
        {
            var match = Regex.Match(title, @"^(?<stem>.+?)\s+(?<number>[1-9][0-9]{0,2})(?:\s|$)",
                RegexOptions.CultureInvariant);
            return match.Success
                ? (match.Groups["stem"].Value.Trim(), match.Groups["number"].Value)
                : null;
        }
        var a = NumberedTitle(left);
        var b = NumberedTitle(right);
        if (a is not null && b is not null
            && string.Equals(a.Value.Stem, b.Value.Stem, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(a.Value.Number, b.Value.Number, StringComparison.Ordinal)) return true;
        if (a is not null && string.Equals(a.Value.Stem, right, StringComparison.OrdinalIgnoreCase)) return true;
        if (b is not null && string.Equals(b.Value.Stem, left, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    // GUIDEVAULT_MANUAL_REVIEW_V14 — server-side revalidation of one scanned pair.
    // The scanner remains the only authority for what may be reviewed or approved.
    public static (bool Found, string Classification, string Reason) FindCandidate(
        string platform, string gameId, string publicationId,
        IReadOnlyList<GuidevaultLaunchBoxGame> games, IReadOnlyList<LibraryItem> documents,
        string? launchBoxRoot)
    {
        int pageCount = 1;
        for (var page = 1; page <= pageCount && page <= 100; page++)
        {
            using var json = JsonDocument.Parse(JsonSerializer.Serialize(
                Preview(platform, "Manual", games, documents, launchBoxRoot, page, 500)));
            pageCount = json.RootElement.GetProperty("pageCount").GetInt32();
            foreach (var row in json.RootElement.GetProperty("rows").EnumerateArray())
            {
                if (string.Equals(row.GetProperty("gameId").GetString(), gameId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(row.GetProperty("publicationId").GetString(), publicationId, StringComparison.OrdinalIgnoreCase))
                    return (true, row.GetProperty("classification").GetString() ?? string.Empty,
                        row.GetProperty("reason").GetString() ?? string.Empty);
            }
        }
        return (false, string.Empty, "The pair is not eligible for the selected platform's manual scan.");
    }

    private static string[] ManualPlatforms(IEnumerable<string?> values) => values
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .SelectMany(value => Regex.Split(value!, @"[,;|]"))
        .Select(NormalizePlatform)
        .Where(value => value.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    private static string NormalizePlatform(string? value)
    {
        var text = Regex.Replace((value ?? "").ToLowerInvariant(), @"[^a-z0-9]+", " ").Trim();
        return text switch
        {
            "nes" or "nintendo entertainment system" or "nintendo entertainment system nes" or "nes nintendo entertainment system" => "nintendo entertainment system",
            "famicom" or "family computer" or "nintendo family computer" => "nintendo family computer",
            "snes" or "super nes" or "super nintendo entertainment system" => "super nintendo entertainment system",
            "psx" or "ps1" or "sony playstation" or "playstation" => "sony playstation",
            "3do" or "3do interactive multiplayer" => "3do interactive multiplayer",
            "n64" or "nintendo 64" => "nintendo 64",
            "mega drive" or "sega mega drive" or "sega genesis" or "genesis" => "sega genesis",
            // Recognize common abbreviated shelf labels without conflating distinct generations.
            "gb" or "game boy" or "nintendo game boy" => "nintendo game boy",
            "gbc" or "game boy color" or "nintendo game boy color" => "nintendo game boy color",
            "gba" or "game boy advance" or "nintendo game boy advance" => "nintendo game boy advance",
            "nds" or "nintendo ds" => "nintendo ds",
            "3ds" or "nintendo 3ds" => "nintendo 3ds",
            "ps2" or "playstation 2" or "sony playstation 2" => "sony playstation 2",
            "ps3" or "playstation 3" or "sony playstation 3" => "sony playstation 3",
            "ps4" or "playstation 4" or "sony playstation 4" => "sony playstation 4",
            "psp" or "playstation portable" or "sony psp" => "sony psp",
            "dreamcast" or "sega dreamcast" => "sega dreamcast",
            "saturn" or "sega saturn" => "sega saturn",
            "master system" or "sega master system" => "sega master system",
            "xbox" or "microsoft xbox" => "microsoft xbox",
            "xbox 360" or "microsoft xbox 360" => "microsoft xbox 360",
            "multi platform" or "multiplatform" or "mixed" or "unknown" or "unsorted" or "manuals" or "manual" or "nintendo" or "sega" or "sony" or "console" or "all platforms" => "",
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
