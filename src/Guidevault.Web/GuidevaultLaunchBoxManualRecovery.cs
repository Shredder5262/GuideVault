// GUIDEVAULT_MANUAL_RECOVERY_V18 — read-only counts and correction-search helpers.
// Never edits LaunchBox XML, document metadata, or match decisions.
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class GuidevaultManualCorrectionRequest
{
    public string Platform { get; set; } = string.Empty;
    public string PublicationId { get; set; } = string.Empty;
    public string GameId { get; set; } = string.Empty;
    public string ReviewNote { get; set; } = string.Empty;
}

public static class GuidevaultLaunchBoxManualRecovery
{
    public static object UnmatchedByPlatform(GuidevaultLaunchBoxIntegrationData snapshot, IReadOnlyList<LibraryItem> items)
    {
        var platforms = snapshot.Games.Select(g => g.Platform)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
        var gamePlatforms = snapshot.Games
            .Where(g => !string.IsNullOrWhiteSpace(g.Id))
            .GroupBy(g => g.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Platform, StringComparer.OrdinalIgnoreCase);
        var approved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var match in snapshot.Matches)
        {
            if (!string.Equals(match.MatchType, "Manual", StringComparison.OrdinalIgnoreCase) ||
                !GuidevaultLaunchBoxMatcher.IsActiveMatchStatus(match.MatchStatus) ||
                !gamePlatforms.TryGetValue(match.LaunchBoxGameId, out var platform)) continue;
            approved.Add(GuidevaultLaunchBoxControlledManualScanner.NormalizedPlatform(platform) + "\u001f" + match.GuideVaultItemId);
        }
        var manuals = items.Where(i => string.Equals(i.Kind, "Manual", StringComparison.OrdinalIgnoreCase))
            .Where(i => !string.IsNullOrWhiteSpace(i.Id))
            .GroupBy(i => i.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First()).ToArray();
        var unknownCount = manuals.Count(i => GuidevaultLaunchBoxControlledManualScanner.PrimaryManualPlatforms(i).Length == 0);
        var tagged = manuals.Select(i => (Item: i, Platforms: GuidevaultLaunchBoxControlledManualScanner.PrimaryManualPlatforms(i))).ToArray();
        var rows = platforms.Select(platform =>
        {
            var key = GuidevaultLaunchBoxControlledManualScanner.NormalizedPlatform(platform);
            var eligible = tagged.Where(row => row.Platforms.Contains(key, StringComparer.OrdinalIgnoreCase)).ToArray();
            var linked = eligible.Count(row => approved.Contains(key + "\u001f" + row.Item.Id));
            // Pending means actionable candidates visible in the scanner, not every orphan document.
            // The previous unmatched count could be nonzero with an empty review queue.
            var preview = GuidevaultLaunchBoxControlledManualScanner.Preview(platform, "Manual",
                snapshot.Games, items, null, 1, 25, snapshot.Matches, "pending");
            var json = System.Text.Json.JsonSerializer.SerializeToElement(preview);
            var pending = json.GetProperty("pendingPairs").GetInt32();
            return new { platform, manualDocuments = eligible.Length, linkedManuals = linked,
                unmatchedManuals = eligible.Length - linked, pendingPairs = pending };
        }).ToArray();
        return new { kind = "Manual", unknownPlatformManuals = unknownCount, platforms = rows,
            definition = "Pending means undecided game/manual candidate pairs shown by a platform's manual scan. Unlinked manuals without eligible candidates are not counted as pending. Unlinked document totals remain available separately." };
    }

    public static object SearchManuals(string platform, string? query, IReadOnlyList<LibraryItem> items)
    {
        var normalized = GuidevaultLaunchBoxControlledManualScanner.NormalizedPlatform(platform);
        if (string.IsNullOrWhiteSpace(normalized)) throw new ArgumentException("Select a specific platform.");
        var q = (query ?? string.Empty).Trim();
        if (q.Length < 2 || q.Length > 100) throw new ArgumentException("Enter at least two characters of the manual title (maximum 100).");
        var result = items.Where(i => string.Equals(i.Kind, "Manual", StringComparison.OrdinalIgnoreCase) &&
                GuidevaultLaunchBoxControlledManualScanner.PrimaryManualPlatforms(i).Contains(normalized, StringComparer.OrdinalIgnoreCase) &&
                (i.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                 i.GameTitle.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                 i.ManualTitle.Contains(q, StringComparison.OrdinalIgnoreCase)))
            .GroupBy(i => i.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(i => i.Title, StringComparer.OrdinalIgnoreCase).Take(50)
            .Select(i => new { id = i.Id, title = i.Title, gameTitle = i.GameTitle,
                system = i.System, coverUrl = "/api/items/" + Uri.EscapeDataString(i.Id) + "/cover-thumb?w=360" })
            .ToArray();
        return new { platform, query = q, items = result };
    }
}
