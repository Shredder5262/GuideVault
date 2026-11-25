// GUIDEVAULT_STRATEGY_EVIDENCE_V20
// Read-only proposals. A strategy guide is a publication, not a per-game file.
// Explicit CoveredGames is game coverage, while platform lists describe applicability.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

public static class GuidevaultLaunchBoxStrategyScanner
{
    private static string Key(string? value)
    {
        var s = (value ?? "").ToLowerInvariant().Trim();
        s = Regex.Replace(s, @"\b(ii|iii|iv|v|vi|vii|viii|ix)\b", match => match.Value switch
        { "ii" => "2", "iii" => "3", "iv" => "4", "v" => "5", "vi" => "6", "vii" => "7", "viii" => "8", "ix" => "9", _ => match.Value });
        return Regex.Replace(Regex.Replace(s, @"[^a-z0-9]+", " ").Trim(), @"\s+", " ");
    }
    // CoverSubject can be a game, hardware, or a character; only an exact game-title
    // match qualifies. Explicit semicolon/pipe separators allow multiple cover games.
    public static IEnumerable<(string Field, string Title, int Priority)> MagazineTitles(LibraryItem item)
    {
        foreach (var title in Regex.Split(item.CoverSubject ?? "", @"[;|]"))
            if (!string.IsNullOrWhiteSpace(title)) yield return ("Cover Game / Subject", title.Trim(), 1);
        foreach (var title in item.FeaturedGames ?? Array.Empty<string>())
            if (!string.IsNullOrWhiteSpace(title)) yield return ("Featured Games", title, 2);
        foreach (var title in item.MentionedGames ?? Array.Empty<string>())
            if (!string.IsNullOrWhiteSpace(title)) yield return ("Mentioned Games", title, 3);
    }
    private static int Relevance(string field) => field == "Cover Game / Subject" ? 1 : field == "Featured Games" ? 2 : 3;
    public static string MagazineEvidence(GuidevaultLaunchBoxGame game, LibraryItem item)
    {
        var names = new[] { game.Title, game.SortTitle }.Concat(game.AlternateNames ?? new List<string>())
            .Select(Key).Where(k => k.Length >= 2).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return MagazineTitles(item).FirstOrDefault(t => names.Contains(Key(t.Title))).Field ?? "";
    }
    private static string[] Platforms(IEnumerable<string?> values) => values
        .Where(v => !string.IsNullOrWhiteSpace(v))
        .SelectMany(v => Regex.Split(v!, @"[,;|]"))
        .Select(GuidevaultLaunchBoxControlledManualScanner.NormalizedPlatform)
        .Where(v => v.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    // A title-only clue is always Needs Review, never sufficient for automatic coverage.
    private static string TitleHint(string title)
    {
        var text = (title ?? string.Empty).ToLowerInvariant();
        text = Regex.Replace(text,
            @"(?:\s*[-:]\s*)?(?:prima(?:'s)?\s+)?(?:the\s+)?(?:official\s+)?(?:strategy|game)\s+guide\s*$",
            "", RegexOptions.IgnoreCase);
        return Key(text);
    }
    // GUIDEVAULT_STRATEGY_PENDING_COUNTS_V23
    // Use the identical scanner/proposal rules as a normal Pending scan. This is
    // intentionally a read-only aggregate of game-to-guide pairs, NOT guidebooks.
    public static object PendingByPlatform(
        IReadOnlyList<GuidevaultLaunchBoxGame> games,
        IReadOnlyList<LibraryItem> documents,
        IReadOnlyList<GuidevaultLaunchBoxMatch> savedMatches, string matchType = "Strategy Guide")
    {
        var platforms = games.Select(g => g.Platform)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
        var rows = platforms.Select(platform =>
        {
            // Page one still returns the total across ALL pages.
            // A guide covering six games contributes six pairs when pending.
            var scan = System.Text.Json.JsonSerializer.SerializeToElement(
                Preview(platform, games, documents, 1, 25, savedMatches, "pending", matchType));
            return new { platform, pendingPairs = scan.GetProperty("pendingPairs").GetInt32(),
                relatedPairs = scan.GetProperty("relatedPairs").GetInt32() };
        }).ToArray();
        return new { kind = matchType, platforms = rows,
            definition = matchType == "Magazine" ? "Counts refer to game/issue pairs. Cover Game / Subject, Featured Games, then Mentioned Games determine relevance; Featured Platforms determines compatibility. Unlisted platforms require content verification." : "Pending means platform-supported proposals. Related means titles covered by guides that do not list this platform; these require version/content verification. Counts refer to game/guide pairs." };
    }

    public static object Preview(string platform,
        IReadOnlyList<GuidevaultLaunchBoxGame> games, IReadOnlyList<LibraryItem> documents,
        int page, int pageSize, IReadOnlyList<GuidevaultLaunchBoxMatch>? savedMatches,
        string? statusFilter = "pending", string matchType = "Strategy Guide")
    {
        if (matchType is not ("Strategy Guide" or "Magazine")) throw new ArgumentException("Unsupported publication type.");
        bool isMagazine = matchType == "Magazine";
        var coverageField = isMagazine ? "Featured Games" : "Covered Games";
        statusFilter = (statusFilter ?? "pending").Trim().ToLowerInvariant();
        if (statusFilter is not ("pending" or "related" or "approved" or "rejected" or "metadata" or "all"))
            throw new ArgumentException("Invalid publication queue filter.");
        if (page < 1 || pageSize < 25 || pageSize > 500)
            throw new ArgumentException("Page must be at least 1 and page size between 25 and 500.");
        var platformKey = GuidevaultLaunchBoxControlledManualScanner.NormalizedPlatform(platform);
        if (string.IsNullOrWhiteSpace(platformKey)) throw new ArgumentException("Select one specific LaunchBox platform.");
        var selected = games.Where(g => string.Equals(g.Platform, platform, StringComparison.OrdinalIgnoreCase))
            .OrderBy(g => g.Title, StringComparer.OrdinalIgnoreCase).ToArray();
        var guides = documents.Where(i => string.Equals(i.Kind, matchType, StringComparison.OrdinalIgnoreCase))
            .Where(i => !string.IsNullOrWhiteSpace(i.Id))
            .GroupBy(i => i.Id, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToArray();

        // One index entry per covered game. Guide titles alone are never treated as explicit coverage
        // for multi-game publications; GameTitle is allowed only if CoveredGames is absent.
        var byGame = new Dictionary<string, List<(LibraryItem Item, string Evidence, string RawTitle)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var guide in guides)
        {
            if (isMagazine)
            {
                foreach (var (field, title, priority) in MagazineTitles(guide))
                {
                    var key = Key(title);
                    if (key.Length < 2) continue;
                    if (!byGame.TryGetValue(key, out var entries)) byGame[key] = entries = new();
                    entries.Add((guide, field, title));
                }
                continue;
            }
            var explicitTitles = (guide.CoveredGames ?? Array.Empty<string>())
                .Where(v => !string.IsNullOrWhiteSpace(v)).ToArray();
            var titleOnly = explicitTitles.Length == 0 && string.IsNullOrWhiteSpace(guide.GameTitle);
            var titles = explicitTitles.Length > 0 ? explicitTitles
                : !string.IsNullOrWhiteSpace(guide.GameTitle) ? new[] { guide.GameTitle }
                : new[] { TitleHint(guide.Title) };
            foreach (var raw in titles)
            {
                var key = Key(raw);
                if (key.Length < 2) continue;
                if (!byGame.TryGetValue(key, out var list)) byGame[key] = list = new();
                list.Add((guide, explicitTitles.Length > 0 ? coverageField : titleOnly ? "Title only" : "Game Title", raw));
            }
        }
        var rows = new List<(int Priority, string GameTitle, string GuideTitle, object Row)>();
        var seenPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var savedByPair = (savedMatches ?? Array.Empty<GuidevaultLaunchBoxMatch>())
            .Where(m => string.Equals(m.MatchType, matchType, StringComparison.OrdinalIgnoreCase))
            .GroupBy(m => m.LaunchBoxGameId + "\u001f" + m.GuideVaultItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        int pending = 0, related = 0, approved = 0, rejected = 0, metadata = 0, strong = 0, review = 0;
        int gamesWithoutCandidate = 0;
        foreach (var game in selected)
        {
            var names = new[] { game.Title, game.SortTitle }.Concat(game.AlternateNames ?? new List<string>())
                .Select(Key).Where(k => k.Length >= 2).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var possibilities = names.Where(byGame.ContainsKey).SelectMany(k => byGame[k]);
            if (isMagazine) possibilities = possibilities.OrderBy(x => Relevance(x.Evidence))
                .ThenBy(x => Key(x.RawTitle) == Key(game.Title) ? 0 : 1);
            var found = false;
            foreach (var (item, evidence, rawMatchedValue) in possibilities)
            {
                var pairKey = game.Id + "\u001f" + item.Id;
                if (!seenPairs.Add(pairKey)) continue;
                found = true;
                var explicitPlatforms = isMagazine ? item.FeaturedPlatforms : item.CoveredPlatforms;
                // Magazine shelving and general focus are descriptive, not release evidence.
                // Only Featured Platforms determines the magazine's compatibility.
                var declared = isMagazine ? Platforms(explicitPlatforms ?? Array.Empty<string>()) :
                    Platforms(new[] { item.System, item.PrimarySystem, item.PlatformFocus }
                    .Concat(item.AssociatedPlatforms ?? Array.Empty<string>())
                    .Concat(explicitPlatforms ?? Array.Empty<string>()));
                var matchesPlatform = declared.Contains(platformKey, StringComparer.OrdinalIgnoreCase);
                var isCrossPlatform = declared.Length > 1;
                // The scanner's matching key may come from the LaunchBox primary name,
                // sort name or an alternate name. Show the exact value used to join.
                var nameSources = new List<(string Field, string Value)> { ("Title", game.Title) };
                if (!string.IsNullOrWhiteSpace(game.SortTitle)) nameSources.Add(("Sort Title", game.SortTitle));
                nameSources.AddRange((game.AlternateNames ?? new List<string>())
                    .Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => ("Alternate Name", n)));
                var rawTitleKey = evidence == "Title only" ? TitleHint(item.Title) : Key(rawMatchedValue);
                var matchedLaunchBox = nameSources.FirstOrDefault(n => Key(n.Value) == rawTitleKey);
                var matchedLaunchBoxField = matchedLaunchBox.Field ?? "Unknown";
                var matchedLaunchBoxValue = matchedLaunchBox.Value ?? game.Title;
                // Different fields mean different things: the shelving System isn't
                // automatically proof that a multi-platform guide covers that release.
                var primaryPlatform = Platforms(new[] { item.System, item.PrimarySystem, item.PlatformFocus });
                var associatedPlatform = Platforms(item.AssociatedPlatforms ?? Array.Empty<string>());
                var coveredPlatform = Platforms(explicitPlatforms ?? Array.Empty<string>());
                var platformBasis = coveredPlatform.Contains(platformKey, StringComparer.OrdinalIgnoreCase) ? (isMagazine ? "Featured Platforms" : "Covered Platforms") :
                    !isMagazine && associatedPlatform.Contains(platformKey, StringComparer.OrdinalIgnoreCase) ? "Associated Platforms" :
                    !isMagazine && primaryPlatform.Contains(platformKey, StringComparer.OrdinalIgnoreCase) ? "System / Primary System / Platform Focus" :
                    declared.Length == 0 ? "Not specified" : "No matching platform field";
                var strictTitle = Key(game.Title);
                var explicitGame = isMagazine ? Key(rawMatchedValue) == strictTitle :
                    (item.CoveredGames ?? Array.Empty<string>()).Any(t => Key(t) == strictTitle);
                // Strong is a *proposal*, never approval. Cross-platform metadata and
                // single-game GameTitle without explicit coverage always need review.
                bool isStrong = (isMagazine ? Relevance(evidence) < 3 : evidence == coverageField) && explicitGame && matchesPlatform
                    && !isCrossPlatform && (!isMagazine || coveredPlatform.Contains(platformKey, StringComparer.OrdinalIgnoreCase));
                // A title/coverage hit with a known incompatible platform is only
                // a candidate for review, never evidence of this particular release.
                var platformCompatibility = declared.Length == 0 ? "Unknown" :
                    matchesPlatform ? (isCrossPlatform ? "Multiple declared platforms" : "Target platform listed") :
                    "Target platform not listed";
                var isUnlistedRelease = declared.Length > 0 && !matchesPlatform;
                // A shared name is NOT proof of equivalent gameplay across releases.
                // Keep possibilities (e.g. arcade -> NES Donkey Kong) in a separate queue.
                string classification = isUnlistedRelease ? "Related release" : isStrong ? "Strong candidate" : "Needs review";
                string reason = evidence is "Game Title" or "Title only"
                    ? "Game-specific title clue only; verify the guide's actual coverage"
                    : !matchesPlatform && declared.Length > 0
                        ? "Matching title in Covered Games, but the guide does not list this platform. Different game or edition is possible; review actual guide contents before linking"
                        : declared.Length == 0
                            ? "Game explicitly covered; guide platform not documented"
                            : isCrossPlatform
                                ? "Game explicitly covered; verify which listed releases are applicable"
                                : !explicitGame
                                    ? "Covered through an alternate game name; verify identity"
                                    : "Explicit Covered Games entry and matching specific platform";
                if (isMagazine)
                    reason = evidence + ": " + (isUnlistedRelease ? "issue does not list this platform; verify release" :
                        evidence == "Mentioned Games" ? "brief mention; verify relevance and release before linking" :
                        isStrong ? "exact game title and one matching Featured Platforms entry" :
                        !explicitGame ? "alternate or sort title; verify identity and release" :
                        declared.Length == 0 ? "Featured Platforms missing; verify release" :
                        "multiple featured platforms; verify which release the article covers");
                savedByPair.TryGetValue(pairKey, out var prior);
                var savedStatus = prior?.MatchStatus ?? "";
                var isApproved = savedStatus is "Confirmed" or "AutoMatched";
                var isRejected = savedStatus == "Rejected";
                var isMetadata = savedStatus == "NeedsMetadata";
                if (isApproved) approved++; else if (isRejected) rejected++; else if (isMetadata) metadata++; else if (isUnlistedRelease) related++; else pending++;
                bool show = statusFilter switch
                {
                    "pending" => !isApproved && !isRejected && !isMetadata && !isUnlistedRelease,
                    "related" => !isApproved && !isRejected && !isMetadata && isUnlistedRelease,
                    "approved" => isApproved, "rejected" => isRejected, "metadata" => isMetadata, _ => true
                };
                if (!show) continue;
                if (isStrong) strong++; else review++;
                rows.Add((isMagazine ? Relevance(evidence) * 2 + (isStrong ? 0 : 1) : isStrong ? 0 : 1, game.Title, item.Title, new {
                    gameId = game.Id, gameTitle = game.Title, gamePlatform = game.Platform,
                    gameBoxFrontUrl = "/api/integrations/launchbox/games/" + Uri.EscapeDataString(game.Id) + "/box-front",
                    publicationId = item.Id, publicationTitle = item.Title, publicationKind = matchType, issueNumber = isMagazine ? item.IssueNumber : "", coverDate = isMagazine ? item.CoverDate : "", publicationSystem = string.Join(", ", declared),
                    classification, reason, matchBasis = evidence,
                    relevancePriority = isMagazine ? Relevance(evidence) : 0,
                    // Exact evidence on BOTH sides, not just "Covered Games".
                    launchBoxMatchField = matchedLaunchBoxField, launchBoxMatchedValue = matchedLaunchBoxValue,
                    guideMatchField = evidence, guideMatchedValue = rawMatchedValue,
                    guideGameTitle = isMagazine ? "" : item.GameTitle ?? "",
                    guidePrimaryPlatform = string.Join(", ", primaryPlatform),
                    guideCoveredPlatforms = string.Join(", ", coveredPlatform),
                    guideAssociatedPlatforms = string.Join(", ", associatedPlatform),
                    platformBasis, platformCompatibility,
                    evidenceProvenance = "GuideVault metadata (publication contents not verified)",
                    savedStatus, savedDecisionReason = prior?.MatchReason ?? "",
                    coverUrl = "/api/items/" + Uri.EscapeDataString(item.Id) + "/cover-thumb?w=360"
                }));
            }
            if (!found) gamesWithoutCandidate++;
        }
        var ordered = rows.OrderBy(r => r.Priority).ThenBy(r => r.GameTitle, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.GuideTitle, StringComparer.OrdinalIgnoreCase).ToArray();
        var pageCount = Math.Max(1, (int)Math.Ceiling(ordered.Length / (double)pageSize));
        page = Math.Min(page, pageCount);
        var first = (page - 1) * pageSize;
        return new {
            platform, matchType, dryRun = true, changedRecords = 0,
            games = selected.Length, strategyGuidesInLibrary = isMagazine ? 0 : guides.Length, publicationsInLibrary = guides.Length,
            candidatePairs = ordered.Length, strongCandidates = strong, needsReview = review,
            pendingPairs = pending, relatedPairs = related, approvedPairs = approved, rejectedPairs = rejected, metadataPairs = metadata,
            gamesWithNoEligibleCandidate = gamesWithoutCandidate,
            statusFilter, page, pageSize, pageCount,
            firstRow = ordered.Length == 0 ? 0 : first + 1,
            lastRow = Math.Min(ordered.Length, first + pageSize),
            rows = ordered.Skip(first).Take(pageSize).Select(r => r.Row).ToArray(),
            note = ""
        };
    }
}
