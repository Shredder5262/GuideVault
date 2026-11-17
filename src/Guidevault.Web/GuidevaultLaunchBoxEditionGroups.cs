using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

// Confirmed edition/variant relationships are intentionally separate from
// LaunchBox sync results. A resync must not discard a user's decisions.
public sealed class GuidevaultLaunchBoxEditionGroup
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public List<string> GameIds { get; set; } = new();
    public List<string> ShareKinds { get; set; } = new();
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class GuidevaultLaunchBoxEditionGroupRequest
{
    public string Title { get; set; } = string.Empty;
    public string PrimaryGameId { get; set; } = string.Empty;
    public List<string> RelatedGameIds { get; set; } = new();
    // Default: share strategy guides and curated magazine matches, not manuals.
    public List<string> ShareKinds { get; set; } = new() { "Strategy Guide", "Magazine" };
}

public sealed class GuidevaultLaunchBoxEditionCandidate
{
    public string GameId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public bool CrossPlatform { get; set; }
    public bool ScummVmVariant { get; set; }
    public int Score { get; set; }
}

public sealed class GuidevaultLaunchBoxEditionGroupsStore
{
    private readonly object _gate = new();
    private readonly string _path;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private List<GuidevaultLaunchBoxEditionGroup> _groups;
    private bool _loadFailed;

    public GuidevaultLaunchBoxEditionGroupsStore(string path)
    {
        _path = path;
        _groups = Load();
    }

    public List<GuidevaultLaunchBoxEditionGroup> All()
    {
        lock (_gate) return _groups.Select(Copy).ToList();
    }

    public GuidevaultLaunchBoxEditionGroup? ForGame(string gameId)
    {
        lock (_gate) return _groups.Where(g => g.GameIds.Any(id => Same(id, gameId)))
            .Select(Copy).FirstOrDefault();
    }

    public (bool Success, string Error, GuidevaultLaunchBoxEditionGroup? Group) Create(
        GuidevaultLaunchBoxEditionGroupRequest? request, IReadOnlyList<GuidevaultLaunchBoxGame> games)
    {
        if (request is null) return (false, "Request is required.", null);
        var ids = new[] { request.PrimaryGameId }.Concat(request.RelatedGameIds ?? new List<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (ids.Count < 2 || ids.Count > 32)
            return (false, "Choose between 2 and 32 distinct LaunchBox game IDs.", null);
        var knownGames = games.Where(g => ids.Contains(g.Id, StringComparer.OrdinalIgnoreCase)).ToList();
        if (knownGames.Count != ids.Count)
            return (false, "Every game ID must exist in the most recent LaunchBox sync.", null);

        var kinds = (request.ShareKinds ?? new List<string>())
            .Select(NormalizeKind).Where(kind => kind.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (kinds.Count == 0) return (false, "Specify at least one share kind.", null);
        if ((request.ShareKinds ?? new List<string>()).Any(kind => NormalizeKind(kind).Length == 0))
            return (false, "ShareKinds permits only Strategy Guide, Magazine, and Manual.", null);

        lock (_gate)
        {
            if (_loadFailed) return (false, "Saved edition-group data could not be read. Repair the JSON or restore a backup before editing groups.", null);
            var existing = _groups.FirstOrDefault(g => g.GameIds.Any(id => ids.Contains(id, StringComparer.OrdinalIgnoreCase)));
            if (existing is not null)
                return (false, "One or more games already belong to group " + existing.Id + ". Delete or edit that group before creating another.", null);
            var group = new GuidevaultLaunchBoxEditionGroup
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = string.IsNullOrWhiteSpace(request.Title) ? knownGames[0].Title : request.Title.Trim(),
                GameIds = ids,
                ShareKinds = kinds,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            var next = _groups.Select(Copy).Append(group).ToList();
            Save(next);
            _groups = next;
            return (true, string.Empty, Copy(group));
        }
    }

    public bool Delete(string id)
    {
        lock (_gate)
        {
            if (_loadFailed) return false;
            if (!_groups.Any(group => Same(group.Id, id))) return false;
            var next = _groups.Where(group => !Same(group.Id, id)).Select(Copy).ToList();
            Save(next);
            _groups = next;
            return true;
        }
    }

    public static List<GuidevaultLaunchBoxEditionCandidate> Candidates(
        GuidevaultLaunchBoxGame source, IReadOnlyList<GuidevaultLaunchBoxGame> games)
    {
        var sourceTitle = CanonicalTitle(source.Title);
        var sourcePlatform = PlatformKey(source.Platform);
        if (sourceTitle.Length < 4) return new();
        var candidates = new List<GuidevaultLaunchBoxEditionCandidate>();
        foreach (var other in games)
        {
            if (Same(other.Id, source.Id)) continue;
            var otherTitle = CanonicalTitle(other.Title);
            if (!Same(otherTitle, sourceTitle)) continue;
            bool crossPlatform = !Same(PlatformKey(other.Platform), sourcePlatform);
            bool scummVm = LooksLikeScummVm(other) || LooksLikeScummVm(source);
            var score = crossPlatform ? (scummVm ? 65 : 50) : (Same(other.Title, source.Title) ? 96 : 92);
            candidates.Add(new GuidevaultLaunchBoxEditionCandidate
            {
                GameId = other.Id, Title = other.Title, Platform = other.Platform,
                CrossPlatform = crossPlatform, ScummVmVariant = scummVm, Score = score,
                Reason = crossPlatform ? "Related title, different platform; manual review required."
                    : "Related title and matching platform; confirm edition compatibility."
            });
        }
        return candidates.OrderByDescending(c => c.Score).ThenBy(c => c.Title, StringComparer.OrdinalIgnoreCase).Take(60).ToList();
    }

    private static string CanonicalTitle(string? title)
    {
        var value = (title ?? string.Empty).Trim().ToLowerInvariant();
        // Strip only recognized edition suffixes, never arbitrary subtitles such
        // as 'Remake', 'Part II', or game-sequel numbers.
        value = Regex.Replace(value,
            @"\s*(?:[:\-]\s*)?(?:dual\s*shock\s*(?:ver\.?|version|edition)?|director['’]?s\s*cut|special\s*edition|enhanced\s*edition)\s*$",
            string.Empty, RegexOptions.IgnoreCase);
        value = Regex.Replace(value, @"\s*[\(\[]\s*(?:disc|disk|cd)\s*(?:[1-9]|i{1,3}|iv|v)\s*[\)\]]\s*$", string.Empty,
            RegexOptions.IgnoreCase);
        value = Regex.Replace(value, @"[^a-z0-9]+", " ").Trim();
        // Roman numeral sequel aliases are candidate hints only, not links.
        // Final Fantasy VIII and Final Fantasy 8 may be proposed for review.
        return Regex.Replace(value, @"\b(?:ii|iii|iv|v|vi|vii|viii|ix|x)\b", match => match.Value switch
        {
            "ii" => "2", "iii" => "3", "iv" => "4", "v" => "5", "vi" => "6",
            "vii" => "7", "viii" => "8", "ix" => "9", "x" => "10",
            _ => match.Value
        });
    }

    private static string PlatformKey(string? platform) => Regex.Replace(
        (platform ?? string.Empty).ToLowerInvariant(), @"[^a-z0-9]+", " ").Trim();

    private static bool LooksLikeScummVm(GuidevaultLaunchBoxGame game) =>
        (game.Platform ?? string.Empty).Contains("scummvm", StringComparison.OrdinalIgnoreCase) ||
        (game.ApplicationPath ?? string.Empty).Contains("scummvm", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeKind(string? kind) => (kind ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "strategy guide" or "strategy guides" or "guide" or "guides" => "Strategy Guide",
        "magazine" or "magazines" => "Magazine",
        "manual" or "manuals" => "Manual",
        _ => string.Empty
    };

    private List<GuidevaultLaunchBoxEditionGroup> Load()
    {
        try
        {
            if (!File.Exists(_path)) return new();
            return (JsonSerializer.Deserialize<List<GuidevaultLaunchBoxEditionGroup>>(File.ReadAllText(_path), _json) ?? new())
                .Where(group => !string.IsNullOrWhiteSpace(group.Id) && group.GameIds is { Count: >= 2 })
                .Select(Copy).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Fail closed and refuse future writes rather than replacing corrupt data.
            _loadFailed = true;
            return new();
        }
    }

    private void Save(List<GuidevaultLaunchBoxEditionGroup> groups)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var tmp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(tmp, JsonSerializer.Serialize(groups, _json));
            File.Move(tmp, _path, true);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    private static GuidevaultLaunchBoxEditionGroup Copy(GuidevaultLaunchBoxEditionGroup group) => new()
    {
        Id = group.Id,
        Title = group.Title,
        GameIds = (group.GameIds ?? new()).ToList(),
        ShareKinds = (group.ShareKinds ?? new()).ToList(),
        UpdatedAt = group.UpdatedAt
    };

    private static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
