// GUIDEVAULT_STRATEGY_SCOPED_V19
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
    public static object Preview(string platform,
        IReadOnlyList<GuidevaultLaunchBoxGame> games, IReadOnlyList<LibraryItem> documents,
        int page, int pageSize, IReadOnlyList<GuidevaultLaunchBoxMatch>? savedMatches,
        string? statusFilter = "pending")
    {
        statusFilter = (statusFilter ?? "pending").Trim().ToLowerInvariant();
        if (statusFilter is not ("pending" or "approved" or "rejected" or "metadata" or "all"))
            throw new ArgumentException("Invalid strategy guide queue filter.");
        if (page < 1 || pageSize < 25 || pageSize > 500)
            throw new ArgumentException("Page must be at least 1 and page size between 25 and 500.");
        var platformKey = GuidevaultLaunchBoxControlledManualScanner.NormalizedPlatform(platform);
        if (string.IsNullOrWhiteSpace(platformKey)) throw new ArgumentException("Select one specific LaunchBox platform.");
        var selected = games.Where(g => string.Equals(g.Platform, platform, StringComparison.OrdinalIgnoreCase))
            .OrderBy(g => g.Title, StringComparer.OrdinalIgnoreCase).ToArray();
        var guides = documents.Where(i => string.Equals(i.Kind, "Strategy Guide", StringComparison.OrdinalIgnoreCase))
            .Where(i => !string.IsNullOrWhiteSpace(i.Id))
            .GroupBy(i => i.Id, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToArray();

        // One index entry per covered game. Guide titles alone are never treated as explicit coverage
        // for multi-game publications; GameTitle is allowed only if CoveredGames is absent.
        var byGame = new Dictionary<string, List<(LibraryItem Item, string Evidence)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var guide in guides)
        {
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
                list.Add((guide, explicitTitles.Length > 0 ? "Covered Games" : titleOnly ? "Title only" : "Game Title"));
            }
        }
        var rows = new List<(int Priority, string GameTitle, string GuideTitle, object Row)>();
        var seenPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int pending = 0, approved = 0, rejected = 0, metadata = 0, strong = 0, review = 0;
        int gamesWithoutCandidate = 0;
        foreach (var game in selected)
        {
            var names = new[] { game.Title, game.SortTitle }.Concat(game.AlternateNames ?? new List<string>())
                .Select(Key).Where(k => k.Length >= 2).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var possibilities = names.Where(byGame.ContainsKey).SelectMany(k => byGame[k]);
            var found = false;
            foreach (var (item, evidence) in possibilities)
            {
                var pairKey = game.Id + "\u001f" + item.Id;
                if (!seenPairs.Add(pairKey)) continue;
                found = true;
                var declared = Platforms(new[] { item.System, item.PrimarySystem, item.PlatformFocus }
                    .Concat(item.AssociatedPlatforms ?? Array.Empty<string>())
                    .Concat(item.CoveredPlatforms ?? Array.Empty<string>()));
                var matchesPlatform = declared.Contains(platformKey, StringComparer.OrdinalIgnoreCase);
                var isCrossPlatform = declared.Length > 1;
                var strictTitle = Key(game.Title);
                var explicitGame = (item.CoveredGames ?? Array.Empty<string>())
                    .Any(t => Key(t) == strictTitle);
                // Strong is a *proposal*, never approval. Cross-platform metadata and
                // single-game GameTitle without explicit coverage always need review.
                bool isStrong = evidence == "Covered Games" && explicitGame && matchesPlatform
                    && !isCrossPlatform;
                string classification = isStrong ? "Strong candidate" : "Needs review";
                string reason = evidence is "Game Title" or "Title only"
                    ? "Game-specific title clue only; verify the guide's actual coverage"
                    : !matchesPlatform && declared.Length > 0
                        ? "Game explicitly covered; listed platforms differ — verify cross-platform applicability"
                        : declared.Length == 0
                            ? "Game explicitly covered; guide platform not documented"
                            : isCrossPlatform
                                ? "Game explicitly covered; verify which listed releases are applicable"
                                : !explicitGame
                                    ? "Covered through an alternate game name; verify identity"
                                    : "Explicit Covered Games entry and matching specific platform";
                var prior = savedMatches?.FirstOrDefault(m =>
                    string.Equals(m.LaunchBoxGameId, game.Id, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(m.GuideVaultItemId, item.Id, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(m.MatchType, "Strategy Guide", StringComparison.OrdinalIgnoreCase));
                var savedStatus = prior?.MatchStatus ?? "";
                var isApproved = savedStatus is "Confirmed" or "AutoMatched";
                var isRejected = savedStatus == "Rejected";
                var isMetadata = savedStatus == "NeedsMetadata";
                if (isApproved) approved++; else if (isRejected) rejected++; else if (isMetadata) metadata++; else pending++;
                bool show = statusFilter switch
                {
                    "pending" => !isApproved && !isRejected && !isMetadata,
                    "approved" => isApproved, "rejected" => isRejected, "metadata" => isMetadata, _ => true
                };
                if (!show) continue;
                if (isStrong) strong++; else review++;
                rows.Add((isStrong ? 0 : 1, game.Title, item.Title, new {
                    gameId = game.Id, gameTitle = game.Title, gamePlatform = game.Platform,
                    gameBoxFrontUrl = "/api/integrations/launchbox/games/" + Uri.EscapeDataString(game.Id) + "/box-front",
                    publicationId = item.Id, publicationTitle = item.Title, publicationSystem = string.Join(", ", declared),
                    classification, reason, matchBasis = evidence,
                    savedStatus, coverUrl = "/api/items/" + Uri.EscapeDataString(item.Id) + "/cover-thumb?w=360"
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
            platform, matchType = "Strategy Guide", dryRun = true, changedRecords = 0,
            games = selected.Length, strategyGuidesInLibrary = guides.Length,
            candidatePairs = ordered.Length, strongCandidates = strong, needsReview = review,
            pendingPairs = pending, approvedPairs = approved, rejectedPairs = rejected, metadataPairs = metadata,
            gamesWithNoEligibleCandidate = gamesWithoutCandidate,
            statusFilter, page, pageSize, pageCount,
            firstRow = ordered.Length == 0 ? 0 : first + 1,
            lastRow = Math.Min(ordered.Length, first + pageSize),
            rows = ordered.Skip(first).Take(pageSize).Select(r => r.Row).ToArray(),
            note = "Strategy guides: explicit Covered Games can produce multiple game relationships. Platform compatibility is reviewed separately. No guide links are written."
        };
    }
}
