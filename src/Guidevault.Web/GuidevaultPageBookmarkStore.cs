using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

// GuideVault is the authoritative store. Bookmarks are scoped to an authenticated
// GuideVault user AND an actual GuideVault item ID (not a LaunchBox game ID).
// Written to data/config so GuideVault's existing backup/restore includes them.
public sealed class GuidevaultPageBookmarkStore
{
    private readonly object _gate = new();
    private readonly string _file;
    private Dictionary<string, Dictionary<string, GuidevaultPageBookmarkDocument>> _users;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public GuidevaultPageBookmarkStore(string file)
    {
        _file = file;
        _users = new(StringComparer.Ordinal);
        try
        {
            if (File.Exists(_file))
                _users = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, GuidevaultPageBookmarkDocument>>>(
                    File.ReadAllText(_file), _json) ?? new(StringComparer.Ordinal);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("GuideVault bookmark file is corrupted; refusing to overwrite it.", ex);
        }
    }

    public GuidevaultPageBookmarkDocument Get(string userId, string itemId, string itemTitle)
    {
        lock (_gate) return Snapshot(userId, itemId, itemTitle);
    }

    public GuidevaultPageBookmarkDocument Set(string userId, string itemId, string itemTitle,
        GuidevaultPageBookmarkChange change)
    {
        if (change.Page is < 1 or > 100000) throw new ArgumentOutOfRangeException(nameof(change.Page));
        lock (_gate)
        {
            var document = Snapshot(userId, itemId, itemTitle);
            var match = document.Pages.FirstOrDefault(x => x.Page == change.Page);
            if (match is null)
            {
                document.Pages.Add(new GuidevaultPageBookmarkEntry
                {
                    Page = change.Page,
                    DisplayMode = change.DisplayMode is 1 or 2 ? change.DisplayMode : 2,
                    SavedAt = DateTimeOffset.UtcNow,
                    Note = Truncate(change.Note, 1000)
                });
            }
            else if (change.Note != null)
            {
                match.Note = Truncate(change.Note, 1000);
            }
            document.Title = itemTitle;
            document.UpdatedAt = DateTimeOffset.UtcNow;
            document.Pages = document.Pages.OrderBy(x => x.Page).ToList();
            if (!_users.TryGetValue(userId, out var items)) _users[userId] = items = new();
            items[itemId] = document;
            Save();
            return Snapshot(userId, itemId, itemTitle);
        }
    }

    public GuidevaultPageBookmarkDocument Remove(string userId, string itemId, string itemTitle, int page)
    {
        if (page is < 1 or > 100000) throw new ArgumentOutOfRangeException(nameof(page));
        lock (_gate)
        {
            var document = Snapshot(userId, itemId, itemTitle);
            document.Pages.RemoveAll(entry => entry.Page == page);
            document.UpdatedAt = DateTimeOffset.UtcNow;
            if (!_users.TryGetValue(userId, out var items)) _users[userId] = items = new();
            if (document.Pages.Count == 0) items.Remove(itemId);
            else items[itemId] = document;
            Save();
            return Snapshot(userId, itemId, itemTitle);
        }
    }

    private GuidevaultPageBookmarkDocument Snapshot(string userId, string itemId, string itemTitle)
    {
        if (_users.TryGetValue(userId, out var items) && items.TryGetValue(itemId, out var stored))
            return new GuidevaultPageBookmarkDocument
            {
                ItemId = itemId, Title = stored.Title,
                UpdatedAt = stored.UpdatedAt,
                Pages = stored.Pages.Select(p => new GuidevaultPageBookmarkEntry
                {
                    Page = p.Page, DisplayMode = p.DisplayMode, SavedAt = p.SavedAt, Note = p.Note
                }).OrderBy(p => p.Page).ToList()
            };
        return new() { ItemId = itemId, Title = itemTitle, Pages = new() };
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        string temp = _file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(_users, _json));
            File.Move(temp, _file, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private static string Truncate(string? text, int length)
        => text is null ? "" : text.Trim().Substring(0, Math.Min(text.Trim().Length, length));
}

public sealed class GuidevaultPageBookmarkDocument
{
    public string ItemId { get; set; } = "";
    public string Title { get; set; } = "";
    public List<GuidevaultPageBookmarkEntry> Pages { get; set; } = new();
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class GuidevaultPageBookmarkEntry
{
    public int Page { get; set; }
    public int DisplayMode { get; set; } = 2;
    public string Note { get; set; } = "";
    public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class GuidevaultPageBookmarkChange
{
    public int Page { get; set; }
    public int DisplayMode { get; set; } = 2;
    public string? Note { get; set; }
}
