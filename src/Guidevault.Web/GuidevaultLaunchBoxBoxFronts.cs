// GUIDEVAULT_LAUNCHBOX_BOXFRONTS_V1_1
// Strict, read-only LaunchBox game-cover resolver. Never uses machine-specific game paths as match keys.
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

public static class GuidevaultLaunchBoxBoxFronts
{
    private sealed record FolderIndex(DateTimeOffset ExpiresUtc,
        Dictionary<string, List<string>> FilesByTitle);
    private static readonly ConcurrentDictionary<string, FolderIndex> Indexes =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly object RebuildGate = new();
    private static readonly TimeSpan IndexLifetime = TimeSpan.FromMinutes(10);
    private static readonly HashSet<string> ImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp" };

    // The only source accepted is the configured, server-visible LaunchBox root.
    // Never interpret ApplicationPath, ROM paths, or absolute paths from synced XML as artwork paths.
    private static string? FrontFolder(string? root, string? platform)
    {
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(platform)) return null;
        if (platform is "." or ".." || platform.IndexOfAny(new[] { '/', '\\', ':', '\0' }) >= 0) return null;
        if (!Path.IsPathFullyQualified(root)) return null;
        var basePath = Path.GetFullPath(root);
        var folder = Path.Combine(basePath, "Images", platform, "Box - Front");
        return Directory.Exists(folder) ? folder : null;
    }

    public static string AvailabilityNote(string? root, string? platform)
    {
        if (string.IsNullOrWhiteSpace(root))
            return "LaunchBox box fronts are unavailable: set a read-only LaunchBox root under Settings → Integrations and expose its Images folder to the GuideVault container.";
        if (FrontFolder(root, platform) is null)
            return $"LaunchBox box fronts are unavailable: expected Images/{platform}/Box - Front inside the configured container-visible LaunchBox root. Confirm the network mount includes Images.";
        return "LaunchBox box fronts are resolved by game title from the configured Images/<Platform>/Box - Front folder. Individual missing images are labeled; no artwork files are changed.";
    }

    private static string NormalizeTitle(string? value)
    {
        var text = (value ?? string.Empty).Normalize(System.Text.NormalizationForm.FormKC).ToLowerInvariant();
        return Regex.Replace(text, @"[^a-z0-9]+", string.Empty);
    }

    private static FolderIndex BuildIndex(string folder)
    {
        var entries = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        try
        {
            // Regional subfolders (e.g. North America) are included, but symlinks are not followed.
            foreach (var file in Directory.EnumerateFiles(folder, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            }))
            {
                if (!ImageExtensions.Contains(Path.GetExtension(file))) continue;
                var stem = Path.GetFileNameWithoutExtension(file);
                // Standard LaunchBox image suffixes: e.g. "1942-01.jpg" or "1942-02.png".
                stem = Regex.Replace(stem, @"-\d{2,3}$", "", RegexOptions.CultureInvariant);
                var key = NormalizeTitle(stem);
                if (key.Length == 0) continue;
                if (!entries.TryGetValue(key, out var paths)) entries[key] = paths = new List<string>();
                paths.Add(file);
            }
        }
        catch (IOException) { /* Missing or disconnected network share; return no covers. */ }
        catch (UnauthorizedAccessException) { /* Read-only mount not accessible. */ }
        foreach (var list in entries.Values) list.Sort(StringComparer.OrdinalIgnoreCase);
        return new FolderIndex(DateTimeOffset.UtcNow.Add(IndexLifetime), entries);
    }

    private static FolderIndex GetIndex(string folder)
    {
        if (Indexes.TryGetValue(folder, out var current) && current.ExpiresUtc > DateTimeOffset.UtcNow)
            return current;
        lock (RebuildGate)
        {
            if (Indexes.TryGetValue(folder, out current) && current.ExpiresUtc > DateTimeOffset.UtcNow)
                return current;
            var fresh = BuildIndex(folder);
            Indexes[folder] = fresh;
            return fresh;
        }
    }

    public static string? Resolve(string? launchBoxRoot, GuidevaultLaunchBoxGame game)
    {
        var folder = FrontFolder(launchBoxRoot, game.Platform);
        if (folder is null) return null;
        var index = GetIndex(folder);
        var names = new[] { game.Title, game.SortTitle }
            .Concat(game.AlternateNames ?? Enumerable.Empty<string>());
        foreach (var key in names.Select(NormalizeTitle).Where(n => n.Length > 0).Distinct())
        {
            if (!index.FilesByTitle.TryGetValue(key, out var paths)) continue;
            // Prefer the game's region when there are several region-specific covers.
            var region = (game.Region ?? string.Empty).Trim();
            var selected = !string.IsNullOrWhiteSpace(region)
                ? paths.FirstOrDefault(path => path.Split(Path.DirectorySeparatorChar)
                    .Any(part => part.Equals(region, StringComparison.OrdinalIgnoreCase)))
                : null;
            selected ??= paths.FirstOrDefault();
            if (selected is not null && File.Exists(selected)) return selected;
        }
        return null;
    }

    public static string ContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png", ".webp" => "image/webp", ".gif" => "image/gif",
        ".bmp" => "image/bmp", _ => "image/jpeg"
    };
}
