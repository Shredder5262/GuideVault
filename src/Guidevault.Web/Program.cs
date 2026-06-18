using System.Collections.Concurrent;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Net;
using System.Net.Mail;
using System.Net.Http.Headers;
using SharpCompress.Readers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
const string GuidevaultVersion = "0.9.217";
var app = builder.Build();
var metadataJsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
var options = app.Configuration.GetSection("Guidevault").Get<GuidevaultOptions>() ?? new GuidevaultOptions();
var contentRoot = app.Environment.ContentRootPath;
var configPath = Path.Combine(contentRoot, "data", "config", "library.settings.json");
var metadataPath = Path.Combine(contentRoot, "data", "config", "metadata.overrides.json");
var coverOverridePath = Path.Combine(contentRoot, "data", "config", "cover.overrides.json");
var fileIdentityPath = Path.Combine(contentRoot, "data", "config", "file.identity-map.json");
var opdsSettingsPath = Path.Combine(contentRoot, "data", "config", "opds.settings.json");
var serverSettingsPath = Path.Combine(contentRoot, "data", "config", "server.settings.json");
var emailSettingsPath = Path.Combine(contentRoot, "data", "config", "email.settings.json");
var emailHistoryPath = Path.Combine(contentRoot, "data", "config", "email.history.json");
var usersSettingsPath = Path.Combine(contentRoot, "data", "config", "users.settings.json");
var itemReviewsPath = Path.Combine(contentRoot, "data", "config", "item.reviews.json");
var taskSettingsPath = Path.Combine(contentRoot, "data", "config", "task.settings.json");
var customizeSettingsPath = Path.Combine(contentRoot, "data", "config", "customize.settings.json");
var deviceHistoryPath = Path.Combine(contentRoot, "data", "config", "device.history.json");
var systemInfoPath = Path.Combine(contentRoot, "data", "config", "system.info.json");
var systemEventsPath = Path.Combine(contentRoot, "data", "config", "system.events.json");
var webRoot = app.Environment.WebRootPath ?? Path.Combine(contentRoot, "wwwroot");
var readerBackgroundsPath = Path.Combine(webRoot, "assets", "backgrounds");
var legacyReaderBackgroundsPath = Path.Combine(webRoot, "backgrounds");
Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);

var savedSettings = LibrarySettingsStore.Load(configPath);
var loadedSettings = savedSettings ?? new LibrarySettings();
loadedSettings.Libraries ??= new List<LibraryDefinition>();
if (savedSettings is null && loadedSettings.Libraries.Count == 0)
{
    var initialLibraryPath = ResolvePath(contentRoot, loadedSettings.LibraryPath ?? options.LibraryPath);
    loadedSettings.Libraries.Add(new LibraryDefinition("Manuals", "Mixed", new List<string> { initialLibraryPath }, DateTimeOffset.MinValue));
    loadedSettings.LibraryPath = initialLibraryPath;
    LibrarySettingsStore.Save(configPath, loadedSettings);
}
loadedSettings = loadedSettings.Normalize(contentRoot, options.LibraryPath);
LibrarySettingsStore.Save(configPath, loadedSettings);

var metadataStore = new MetadataStore(metadataPath);
var coverOverrideStore = new ItemCoverOverrideStore(coverOverridePath);
var fileIdentityStore = new FileIdentityStore(fileIdentityPath);
var opdsStore = new OpdsSettingsStore(opdsSettingsPath);
var serverSettingsStore = new GuidevaultServerSettingsStore(serverSettingsPath, contentRoot);
var emailSettingsStore = new GuidevaultEmailSettingsStore(emailSettingsPath);
var emailHistoryStore = new GuidevaultEmailHistoryStore(emailHistoryPath);
var usersStore = new GuidevaultUsersStore(usersSettingsPath);
var itemReviewStore = new GuidevaultItemReviewStore(itemReviewsPath);
var taskSettingsStore = new GuidevaultTaskSettingsStore(taskSettingsPath);
var customizeSettingsStore = new GuidevaultCustomizeSettingsStore(customizeSettingsPath);
var deviceStore = new DeviceHistoryStore(deviceHistoryPath);
var systemInfoStore = new SystemInfoStore(systemInfoPath, GuidevaultVersion);
var systemEventStore = new GuidevaultSystemEventStore(systemEventsPath);
var indexCachePath = Path.Combine(contentRoot, "data", "cache", "library-index.json");
var coverCachePath = Path.Combine(contentRoot, "data", "cache", "covers");
var coverThumbnailCachePath = Path.Combine(contentRoot, "data", "cache", "cover-thumbs");
ArchiveReader.ConfigureCoverCache(coverCachePath, coverThumbnailCachePath);
var taskMonitor = new TaskMonitor();
var fileConversionJobs = new FileConversionJobStore();
var cache = new LibraryCache(loadedSettings.Libraries, metadataStore, fileIdentityStore, indexCachePath, taskMonitor);
var updateChecker = new StableUpdateChecker(options.Updates, GuidevaultVersion);
var categoryCoverPrewarmGate = new SemaphoreSlim(1, 1);
var homeAssistantConnector = new GuidevaultHomeAssistantConnector(serverSettingsStore, app.Logger, GetReaderBackgroundCatalog);


ReaderBackgroundCatalog GetReaderBackgroundCatalog()
{
    var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp"
    };

    var activeBackgroundsPath = Directory.Exists(readerBackgroundsPath)
        ? readerBackgroundsPath
        : legacyReaderBackgroundsPath;
    var backgroundUrlBase = Directory.Exists(readerBackgroundsPath)
        ? "/assets/backgrounds"
        : "/backgrounds";

    var backgroundFiles = Directory.Exists(activeBackgroundsPath)
        ? Directory.EnumerateFiles(activeBackgroundsPath)
        : Enumerable.Empty<string>();

    var backgrounds = backgroundFiles
        .Where(file => allowedExtensions.Contains(Path.GetExtension(file)))
        .Select(file =>
        {
            var fileName = Path.GetFileName(file);
            var baseName = Path.GetFileNameWithoutExtension(fileName)
                .Replace('-', ' ')
                .Replace('_', ' ')
                .Trim();
            var displayName = fileName.ToLowerInvariant() switch
            {
                "adventuremap.png" => "Adventure Map",
                "bathroom.png" => "Bathroom",
                "gamerbedroom.png" => "Gamer Bedroom",
                "librarydesk.png" => "Library Desk",
                "livingroom.png" => "Living Room",
                "schoolbus.png" => "School Bus",
                "spacehud.png" => "Space HUD",
                "warriorhud.png" => "Warrior HUD",
                "wood.png" => "Wood",
                _ => string.IsNullOrWhiteSpace(baseName)
                    ? fileName
                    : System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(baseName.ToLowerInvariant())
            };
            return new ReaderBackgroundInfo
            {
                Name = fileName,
                DisplayName = displayName,
                Url = $"{backgroundUrlBase}/{Uri.EscapeDataString(fileName)}"
            };
        })
        .OrderBy(bg => bg.DisplayName, StringComparer.OrdinalIgnoreCase)
        .ToList();

    var defaultName = backgrounds.FirstOrDefault(bg => bg.Name.Equals("librarydesk.png", StringComparison.OrdinalIgnoreCase))?.Name
        ?? backgrounds.FirstOrDefault()?.Name
        ?? string.Empty;

    return new ReaderBackgroundCatalog
    {
        Folder = activeBackgroundsPath,
        DefaultName = defaultName,
        Backgrounds = backgrounds
    };
}

void RecordSystemEvent(string category, string title, string message = "", string source = "server", string? itemId = null, string? itemTitle = null)
{
    try
    {
        systemEventStore.Record(new GuidevaultSystemEventRecord
        {
            Category = category,
            Title = title,
            Message = message,
            Source = source,
            ItemId = itemId ?? string.Empty,
            ItemTitle = itemTitle ?? string.Empty
        });
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Unable to write Guidevault system event.");
    }
}

RecordSystemEvent("System", "Guidevault started", $"Guidevault {GuidevaultVersion} server initialized.", "server");
// Do not create configured user library folders here. Guidevault scans existing folders in place.
// Creating missing folders can hide typo/path mistakes and make libraries appear empty.
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/favicon.ico", () => Results.Redirect("/assets/favicon.ico", permanent: false));

app.MapGet("/api/health", () => Results.Ok(new
{
    app = "Guidevault",
    version = GuidevaultVersion,
    libraryPaths = cache.LibraryPaths,
    dockerReady = true
}));

app.MapGet("/api/devices", () => Results.Ok(deviceStore.GetSnapshot()));

app.MapGet("/api/system/info", () => Results.Ok(systemInfoStore.GetSnapshot()));

app.MapGet("/api/system/events", (int? limit) => Results.Ok(systemEventStore.GetEvents(limit ?? 100)));

app.MapGet("/api/system/performance", () =>
{
    var process = Process.GetCurrentProcess();
    var gcInfo = GC.GetGCMemoryInfo();
    var archive = ArchiveReader.GetDiagnostics();
    var tasks = taskMonitor.RecentTasks();
    return Results.Ok(new
    {
        capturedAt = DateTimeOffset.UtcNow,
        process = new
        {
            workingSetBytes = process.WorkingSet64,
            privateMemoryBytes = process.PrivateMemorySize64,
            virtualMemoryBytes = process.VirtualMemorySize64,
            threadCount = process.Threads.Count,
            processorTime = process.TotalProcessorTime.ToString()
        },
        dotnet = new
        {
            totalManagedMemoryBytes = GC.GetTotalMemory(false),
            heapSizeBytes = gcInfo.HeapSizeBytes,
            memoryLoadBytes = gcInfo.MemoryLoadBytes,
            highMemoryLoadThresholdBytes = gcInfo.HighMemoryLoadThresholdBytes,
            generation0Collections = GC.CollectionCount(0),
            generation1Collections = GC.CollectionCount(1),
            generation2Collections = GC.CollectionCount(2),
            isServerGc = System.Runtime.GCSettings.IsServerGC
        },
        library = new
        {
            cachedItemCount = cache.CachedItemCount,
            libraryCount = cache.Libraries.Count,
            libraryPathCount = cache.LibraryPaths.Count,
            lastScan = cache.LastScanStats
        },
        tasks = new
        {
            activeCount = tasks.Count(t => t.Status == "running" || t.Status == "queued"),
            recentCount = tasks.Count
        },
        archive
    });
});

app.MapPost("/api/system/performance/trim", () =>
{
    ArchiveReader.ClearMemoryCaches();
    GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
    GC.WaitForPendingFinalizers();
    GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
    return Results.Ok(new { trimmed = true, capturedAt = DateTimeOffset.UtcNow });
});

app.MapGet("/api/system/update-check", async (bool force = false) => Results.Ok(await updateChecker.CheckAsync(force)));

app.MapGet("/api/server/settings", () => Results.Ok(serverSettingsStore.GetSnapshot()));

app.MapPut("/api/server/settings", (GuidevaultServerSettings payload) =>
{
    var saved = serverSettingsStore.Update(payload ?? new GuidevaultServerSettings());
    RecordSystemEvent("System", "Server settings updated", "Server settings were saved from the web UI.", "api");
    return Results.Ok(saved);
});


app.MapGet("/api/home-assistant/status", () => Results.Ok(homeAssistantConnector.GetStatusSnapshot(cache.GetItemsSnapshot().Count)));

app.MapPost("/api/home-assistant/status", async (GuidevaultHomeAssistantReaderStatus payload) =>
{
    var result = await homeAssistantConnector.UpdateReaderStatusAsync(payload ?? new GuidevaultHomeAssistantReaderStatus(), cache.GetItemsSnapshot().Count);
    if (!string.IsNullOrWhiteSpace(payload?.EventType))
    {
        var title = string.IsNullOrWhiteSpace(payload.ItemTitle) ? "Guidevault reader state updated" : $"Guidevault reader: {payload.ItemTitle}";
        RecordSystemEvent("Home Assistant", title, payload.EventType ?? string.Empty, "home-assistant", payload.ItemId, payload.ItemTitle);
    }
    return Results.Ok(result);
});

app.MapPost("/api/home-assistant/test", async () =>
{
    var result = await homeAssistantConnector.TestAsync(cache.GetItemsSnapshot().Count);
    RecordSystemEvent("Home Assistant", result.Success ? "Home Assistant connection verified" : "Home Assistant connection failed", result.Message, "api");
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});

app.MapPost("/api/home-assistant/command", (HttpRequest request, GuidevaultHomeAssistantCommandRequest payload) =>
{
    var settings = serverSettingsStore.GetSnapshot();
    if (!settings.HomeAssistantCommandEnabled) return Results.BadRequest(new { error = "Home Assistant command intake is disabled." });
    if (!GuidevaultHomeAssistantConnector.IsCommandAuthorized(request, settings)) return Results.Unauthorized();

    var command = homeAssistantConnector.EnqueueCommand(payload ?? new GuidevaultHomeAssistantCommandRequest());
    RecordSystemEvent("Home Assistant", $"Home Assistant command queued: {command.Action}", command.ItemTitle, "home-assistant", command.ItemId, command.ItemTitle);
    return Results.Ok(new { queued = true, command });
});

app.MapGet("/api/home-assistant/commands", (long? after) => Results.Ok(new
{
    enabled = serverSettingsStore.GetSnapshot().HomeAssistantCommandEnabled,
    commands = homeAssistantConnector.GetCommands(after ?? 0)
}));

app.MapPost("/api/server/backup", () =>
{
    try
    {
        var backup = serverSettingsStore.CreateLibraryBackup(new[]
        {
            configPath,
            metadataPath,
            opdsSettingsPath,
            deviceHistoryPath,
            systemInfoPath,
            emailSettingsPath,
            emailHistoryPath,
            usersSettingsPath,
            taskSettingsPath,
            indexCachePath
        });
        return Results.Ok(backup);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = $"Backup failed: {ex.Message}" });
    }
});

app.MapGet("/api/email/settings", () => Results.Ok(emailSettingsStore.GetClientSettings()));

app.MapPut("/api/email/settings", (GuidevaultEmailSettings payload) =>
{
    var saved = emailSettingsStore.Update(payload ?? new GuidevaultEmailSettings());
    RecordSystemEvent("Email", "Email settings updated", "Email delivery settings were saved.", "api");
    return Results.Ok(saved);
});

app.MapGet("/api/email/history", () => Results.Ok(emailHistoryStore.GetHistory()));

app.MapPost("/api/email/test", (GuidevaultEmailTestRequest payload) =>
{
    var email = emailSettingsStore.GetSettings();
    if (payload is null || string.IsNullOrWhiteSpace(payload.To))
        return Results.BadRequest(new { error = "A test recipient email address is required." });
    if (!email.IsConfigured)
    {
        emailHistoryStore.Record(new GuidevaultEmailHistoryRecord
        {
            Type = "Test",
            To = payload.To?.Trim() ?? string.Empty,
            Subject = string.IsNullOrWhiteSpace(payload.Subject) ? "Guidevault test email" : payload.Subject.Trim(),
            TemplateName = email.ActiveTemplateName,
            Status = "Blocked",
            Message = "Email settings are incomplete."
        });
        return Results.BadRequest(new { error = "Email settings are incomplete." });
    }

    try
    {
        var subject = string.IsNullOrWhiteSpace(payload.Subject) ? email.ActiveTemplateSubject : payload.Subject!.Trim();
        var body = string.IsNullOrWhiteSpace(payload.Body) ? email.ActiveTemplateBody : payload.Body!;
        emailSettingsStore.SendTest(email, payload.To!.Trim(), subject, body);
        var record = emailHistoryStore.Record(new GuidevaultEmailHistoryRecord
        {
            Type = "Test",
            To = payload.To!.Trim(),
            Subject = subject,
            TemplateName = email.ActiveTemplateName,
            Status = "Sent",
            Message = "Test email sent."
        });
        RecordSystemEvent("Email", "Test email sent", $"Test email sent to {payload.To!.Trim()}.", "api");
        return Results.Ok(new { sent = true, record, history = emailHistoryStore.GetHistory() });
    }
    catch (Exception ex)
    {
        var record = emailHistoryStore.Record(new GuidevaultEmailHistoryRecord
        {
            Type = "Test",
            To = payload.To?.Trim() ?? string.Empty,
            Subject = string.IsNullOrWhiteSpace(payload.Subject) ? email.ActiveTemplateSubject : payload.Subject!.Trim(),
            TemplateName = email.ActiveTemplateName,
            Status = "Failed",
            Message = ex.Message
        });
        RecordSystemEvent("Email", "Test email failed", ex.Message, "api");
        return Results.BadRequest(new { error = ex.Message, record, history = emailHistoryStore.GetHistory() });
    }
});

app.MapGet("/api/users", () => Results.Ok(new
{
    users = usersStore.GetUsers(),
    libraries = cache.Libraries.Select(l => l.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n).ToArray(),
    permissions = GuidevaultUsersStore.DefaultPermissions
}));

app.MapPost("/api/users/invite", (HttpRequest request, GuidevaultUserInviteRequest payload) =>
{
    if (payload is null || string.IsNullOrWhiteSpace(payload.Email))
        return Results.BadRequest(new { error = "Email is required." });

    var result = usersStore.Invite(payload);
    var email = emailSettingsStore.GetSettings();
    var sent = false;
    var emailMessage = string.Empty;
    var subject = email.ActiveTemplateSubject;
    if (email.IsConfigured)
    {
        try
        {
            var inviteUrl = BuildAbsoluteUrl(request, "/");
            emailSettingsStore.SendInvite(email, result.User, inviteUrl);
            sent = true;
            emailHistoryStore.Record(new GuidevaultEmailHistoryRecord
            {
                Type = "Invite",
                To = result.User.Email,
                Subject = subject,
                TemplateName = email.ActiveTemplateName,
                Status = "Sent",
                Message = "Invite email sent."
            });
        }
        catch (Exception ex)
        {
            emailMessage = ex.Message;
            emailHistoryStore.Record(new GuidevaultEmailHistoryRecord
            {
                Type = "Invite",
                To = result.User.Email,
                Subject = subject,
                TemplateName = email.ActiveTemplateName,
                Status = "Failed",
                Message = ex.Message
            });
        }
    }
    else
    {
        emailMessage = "Email is not configured. The user was saved as a pending invite.";
        emailHistoryStore.Record(new GuidevaultEmailHistoryRecord
        {
            Type = "Invite",
            To = result.User.Email,
            Subject = subject,
            TemplateName = email.ActiveTemplateName,
            Status = "Pending",
            Message = emailMessage
        });
    }

    return Results.Ok(new { result.User, users = usersStore.GetUsers(), emailSent = sent, emailMessage });
});

app.MapGet("/api/tasks/settings", () => Results.Ok(taskSettingsStore.GetSettings()));

app.MapPut("/api/tasks/settings", (GuidevaultTaskScheduleSettings payload) =>
{
    var saved = taskSettingsStore.Update(payload ?? new GuidevaultTaskScheduleSettings());
    return Results.Ok(saved);
});

app.MapGet("/api/customize/settings", () => Results.Ok(customizeSettingsStore.GetSettings()));

app.MapPut("/api/customize/settings", (GuidevaultCustomizeSettings payload) =>
{
    var saved = customizeSettingsStore.SaveSettings(payload ?? new GuidevaultCustomizeSettings());
    return Results.Ok(saved);
});

app.MapPost("/api/devices/heartbeat", (HttpRequest request, ClientDeviceHeartbeat payload) =>
{
    var device = deviceStore.RecordWebClient(request, payload ?? new ClientDeviceHeartbeat());
    RecordSystemEvent("Connection", "Client connection", $"{device.DisplayName} connected from {device.IpAddress}.", "api");
    return Results.Ok(new { device, snapshot = deviceStore.GetSnapshot() });
});

app.MapPost("/api/devices/email", (EmailDeviceUpsert payload) =>
{
    if (payload is null || string.IsNullOrWhiteSpace(payload.Name) || string.IsNullOrWhiteSpace(payload.Email))
        return Results.BadRequest(new { error = "Name and email are required." });
    var snapshot = deviceStore.UpsertEmailDevice(payload);
    RecordSystemEvent("Connection", "Email device saved", $"Saved email device {payload.Name}.", "api");
    return Results.Ok(snapshot);
});

app.MapDelete("/api/devices/email/{id}", (string id) =>
{
    var snapshot = deviceStore.DeleteEmailDevice(id);
    return snapshot is null ? Results.NotFound(new { error = "Email device not found." }) : Results.Ok(snapshot);
});

app.MapDelete("/api/devices/clients/{id}", (string id) =>
{
    var snapshot = deviceStore.DeleteClientDevice(id);
    return snapshot is null ? Results.NotFound(new { error = "Client device not found." }) : Results.Ok(snapshot);
});

app.MapPatch("/api/devices/clients/{id}", (string id, ClientDeviceNameUpdate payload) =>
{
    if (payload is null || string.IsNullOrWhiteSpace(payload.DisplayName))
        return Results.BadRequest(new { error = "Display name is required." });
    var snapshot = deviceStore.RenameClientDevice(id, payload.DisplayName);
    return snapshot is null ? Results.NotFound(new { error = "Client device not found." }) : Results.Ok(snapshot);
});

app.MapPost("/api/devices/clients/clear-stale", (int days) =>
{
    var thresholdDays = days <= 0 ? 30 : days;
    return Results.Ok(deviceStore.ClearStaleClientDevices(thresholdDays));
});

app.MapGet("/api/library", () => Results.Ok(cache.GetItemsSnapshot()));

app.MapPost("/api/library/prewarm-covers", (string? kind, int? limit) =>
{
    var requestedLimit = Math.Clamp(limit ?? 220, 20, 420);
    var itemsToWarm = BuildCategoryPreviewCoverItems(cache.GetItemsSnapshot(), kind, requestedLimit);
    if (itemsToWarm.Count == 0) return Results.Ok(new { queued = false, count = 0 });

    _ = Task.Run(async () =>
    {
        if (!await categoryCoverPrewarmGate.WaitAsync(0)) return;
        try
        {
            foreach (var item in itemsToWarm)
            {
                if (string.IsNullOrWhiteSpace(item.Path) || !File.Exists(item.Path)) continue;
                if (string.Equals(item.Format, "PDF", StringComparison.OrdinalIgnoreCase)) continue;
                try { await ArchiveReader.GetCoverThumbnailAsync(item.Path, 360, coverOverrideStore.Get(item.Id)?.EntryKey); }
                catch { /* best-effort warmup only */ }
            }
        }
        finally
        {
            categoryCoverPrewarmGate.Release();
        }
    });

    return Results.Ok(new { queued = true, count = itemsToWarm.Count });
});

app.MapGet("/api/startup/status", () =>
{
    var runningTasks = taskMonitor.RecentTasks()
        .Where(t => string.Equals(t.Status, "running", StringComparison.OrdinalIgnoreCase) || string.Equals(t.Status, "queued", StringComparison.OrdinalIgnoreCase))
        .Select(t => new
        {
            t.Id,
            t.Kind,
            t.Title,
            t.Status,
            t.Message,
            t.ProgressPercent,
            t.UpdatedAt
        })
        .ToArray();
    var cachedCount = cache.CachedItemCount;
    var status = runningTasks.Length > 0 ? "warming" : cachedCount > 0 ? "ready" : "empty";
    var message = runningTasks.FirstOrDefault()?.Message
        ?? (cachedCount > 0
            ? $"Preparing library from {cachedCount:n0} indexed item(s)..."
            : "No indexed library items are available yet. Run a library scan to build the index.");

    return Results.Ok(new
    {
        status,
        message,
        itemCount = cachedCount,
        libraryCount = cache.Libraries.Count,
        libraryPathCount = cache.LibraryPaths.Count,
        runningTasks,
        generatedAt = DateTimeOffset.UtcNow
    });
});

// Fast web bootstrap endpoint: returns a small, shelf-oriented subset so the browser can
// paint the first view without downloading the entire library index. OPDS intentionally
// continues to use the full cache path below so its feed structure is unchanged.
app.MapGet("/api/library/initial", (int? limit) => Results.Ok(BuildLibraryInitialPayload(cache.GetItemsSnapshot(), limit)));

// Chunked endpoint used by the web UI background loader. This avoids sending and
// parsing the entire library index as one large JSON response during login.
app.MapGet("/api/library/chunk", (int? offset, int? limit) =>
{
    var source = cache.GetItemsSnapshot();
    var safeOffset = Math.Max(0, offset ?? 0);
    var safeLimit = Math.Clamp(limit ?? 220, 60, 500);
    var total = source.Count;
    var items = source.Skip(safeOffset).Take(safeLimit).ToArray();
    var nextOffset = safeOffset + items.Length;
    return Results.Ok(new
    {
        items,
        offset = safeOffset,
        limit = safeLimit,
        nextOffset,
        totalCount = total,
        hasMore = nextOffset < total,
        counts = safeOffset == 0 ? BuildLibraryCounts(source) : null,
        generatedAt = DateTimeOffset.UtcNow
    });
});

// Lightweight paged endpoint for browser-facing library views. The legacy /api/library
// endpoint remains full-index for compatibility and internal consumers.
app.MapGet("/api/library/page", (int? page, int? pageSize, string? kind, string? q, string? sort) =>
{
    var source = cache.GetItemsSnapshot();
    var filtered = FilterLibraryItems(source, kind, q);
    var ordered = SortLibraryItems(filtered, sort);
    var safePageSize = Math.Clamp(pageSize ?? 100, 12, 250);
    var safePage = Math.Max(1, page ?? 1);
    var total = ordered.Count;
    var items = ordered.Skip((safePage - 1) * safePageSize).Take(safePageSize).ToArray();
    return Results.Ok(new
    {
        items,
        page = safePage,
        pageSize = safePageSize,
        total,
        totalPages = (int)Math.Ceiling(total / (double)safePageSize),
        hasMore = safePage * safePageSize < total,
        counts = BuildLibraryCounts(source)
    });
});

app.MapGet("/api/tasks", () => Results.Ok(new { tasks = taskMonitor.RecentTasks() }));
app.MapPost("/api/tasks/clear", () => Results.Ok(new { cleared = taskMonitor.ClearNonRunning(), tasks = taskMonitor.RecentTasks() }));


app.MapGet("/api/reader/backgrounds", () => Results.Ok(GetReaderBackgroundCatalog()));


app.MapPost("/api/library/rescan", () =>
{
    var task = taskMonitor.Start("library-scan", "Library scan", "Library scan queued.");
    _ = Task.Run(async () =>
    {
        try
        {
            taskMonitor.Update(task.Id, "Starting library scan...", 2);
            var items = await cache.RescanAsync(task.Id);
            taskMonitor.Complete(task.Id, $"Scan complete: {items.Count} item(s) indexed.", 100);
        }
        catch (Exception ex)
        {
            taskMonitor.Fail(task.Id, $"Scan failed: {ex.Message}");
        }
    });
    return Results.Ok(new
    {
        taskId = task.Id,
        id = task.Id,
        kind = task.Kind,
        title = task.Title,
        status = task.Status,
        message = "Library scan queued.",
        progressPercent = task.ProgressPercent,
        updatedAt = task.UpdatedAt
    });
});

app.MapPost("/api/library/cleanup", () =>
{
    var task = taskMonitor.Start("library-cleanup", "Library cleanup", "Library cleanup queued.");
    _ = Task.Run(async () =>
    {
        try
        {
            taskMonitor.Update(task.Id, "Starting safe library cleanup...", 2);
            // Use the fast index reconciliation path. Deep archive validation was
            // too easy to stall/error on large or network-hosted libraries, so this
            // cleanup pass removes stale/untracked items and refreshes changed files
            // without trying to open every archive.
            var items = await cache.RescanAsync(task.Id, "scan");
            taskMonitor.Complete(task.Id, $"Cleanup complete: {items.Count} item(s) indexed.", 100);
        }
        catch (Exception ex)
        {
            taskMonitor.Fail(task.Id, $"Cleanup failed: {ex.Message}");
        }
    });
    return Results.Ok(new
    {
        taskId = task.Id,
        id = task.Id,
        kind = task.Kind,
        title = task.Title,
        status = task.Status,
        message = "Library cleanup queued.",
        progressPercent = task.ProgressPercent,
        updatedAt = task.UpdatedAt
    });
});

app.MapPost("/api/library/enrich-metadata", () =>
{
    var task = taskMonitor.Start("library-enrichment", "Fast metadata enrichment", "Fast metadata enrichment queued.");
    _ = Task.Run(async () =>
    {
        try
        {
            taskMonitor.Update(task.Id, "Starting fast Guidevault JSON metadata enrichment...", 2);
            var items = await cache.RescanAsync(task.Id, "metadata");
            taskMonitor.Complete(task.Id, $"Fast metadata enrichment complete: {items.Count} item(s) reconciled.", 100);
        }
        catch (Exception ex)
        {
            taskMonitor.Fail(task.Id, $"Fast metadata enrichment failed: {ex.Message}");
        }
    });
    return Results.Ok(new
    {
        taskId = task.Id,
        id = task.Id,
        kind = task.Kind,
        title = task.Title,
        status = task.Status,
        message = "Fast metadata enrichment queued.",
        progressPercent = task.ProgressPercent,
        updatedAt = task.UpdatedAt
    });
});

app.MapPost("/api/library/enrich-comicinfo", () =>
{
    var task = taskMonitor.Start("library-enrichment", "Legacy ComicInfo import", "Legacy ComicInfo import queued.");
    _ = Task.Run(async () =>
    {
        try
        {
            taskMonitor.Update(task.Id, "Starting slower legacy ComicInfo metadata import...", 2);
            var items = await cache.RescanAsync(task.Id, "comicinfo");
            taskMonitor.Complete(task.Id, $"Legacy ComicInfo import complete: {items.Count} item(s) reconciled.", 100);
        }
        catch (Exception ex)
        {
            taskMonitor.Fail(task.Id, $"Legacy ComicInfo import failed: {ex.Message}");
        }
    });
    return Results.Ok(new
    {
        taskId = task.Id,
        id = task.Id,
        kind = task.Kind,
        title = task.Title,
        status = task.Status,
        message = "Legacy ComicInfo import queued.",
        progressPercent = task.ProgressPercent,
        updatedAt = task.UpdatedAt
    });
});

app.MapGet("/api/library/debug", async () =>
{
    var items = await cache.GetItemsAsync();
    return Results.Ok(new
    {
        libraryPaths = cache.LibraryPaths,
        libraries = cache.Libraries,
        exists = cache.LibraryPaths.Select(p => new { path = p, exists = Directory.Exists(p) }).ToArray(),
        supported = ArchiveReader.SupportedExtensions.OrderBy(x => x).ToArray(),
        discoveredItems = items.Count,
        sampleFiles = cache.LibraryPaths.Where(Directory.Exists).SelectMany(root => LibraryCache.SafeEnumerateFiles(root).Take(10).Select(p => new { root, file = Path.GetRelativePath(root, p) })).Take(50).ToArray()
    });
});

app.MapGet("/api/settings/library", () => Results.Ok(new
{
    libraryPath = cache.LibraryPaths.FirstOrDefault() ?? string.Empty,
    libraryPaths = cache.LibraryPaths,
    libraries = cache.Libraries,
    exists = cache.LibraryPaths.Select(p => new { path = p, exists = Directory.Exists(p) }).ToArray(),
    supported = ArchiveReader.SupportedExtensions.OrderBy(x => x).ToArray(),
    note = "Guidevault builds its libraries by scanning files in place. Files are not uploaded or copied into the app."
}));


app.MapGet("/api/server/directories", (string? path) =>
{
    var requested = string.IsNullOrWhiteSpace(path) ? DefaultBrowsePath(contentRoot) : path.Trim();
    string current;
    try
    {
        current = Path.IsPathRooted(requested)
            ? Path.GetFullPath(requested)
            : Path.GetFullPath(Path.Combine(contentRoot, requested));
    }
    catch
    {
        current = DefaultBrowsePath(contentRoot);
    }

    if (!Directory.Exists(current))
    {
        current = Directory.Exists("/app/data") ? "/app/data" : contentRoot;
    }

    try
    {
        var info = new DirectoryInfo(current);
        var entries = info.EnumerateDirectories()
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .Take(250)
            .Select(d => new
            {
                name = d.Name,
                path = d.FullName.Replace('\\', '/'),
                modifiedAt = d.LastWriteTimeUtc
            })
            .ToArray();

        var roots = BrowseRoots(contentRoot)
            .Select(root => new { label = root.Label, path = root.Path.Replace('\\', '/') })
            .ToArray();

        return Results.Ok(new
        {
            currentPath = info.FullName.Replace('\\', '/'),
            parentPath = info.Parent?.FullName.Replace('\\', '/') ?? string.Empty,
            roots,
            entries,
            note = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "Browsing Windows server folders. Docker containers will show Linux-style mounted folders here."
                : "Browsing Linux/container folders. Mounted Docker volumes appear here as normal folders."
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = $"Unable to browse folder: {ex.Message}" });
    }
});

app.MapPost("/api/settings/library", (LibrarySettings settings) =>
{
    if (settings is null || string.IsNullOrWhiteSpace(settings.LibraryPath))
        return Results.BadRequest(new { error = "A library root folder path is required." });

    var newPath = ResolvePath(contentRoot, settings.LibraryPath.Trim());
    if (!Directory.Exists(newPath))
        return Results.BadRequest(new { error = $"Folder not found from inside the Guidevault server/container: {newPath}. In Docker, paste the mounted container path, such as /library, /library/Manuals, or /library/Strategy Guides. Windows host paths only work if they are mounted into the container." });

    var updated = new LibrarySettings
    {
        LibraryPath = newPath,
        Libraries = new List<LibraryDefinition> { new("Manuals", "Mixed", new List<string> { newPath }, DateTimeOffset.MinValue) }
    }.Normalize(contentRoot, options.LibraryPath);
    cache.SetLibraries(updated.Libraries);
    LibrarySettingsStore.Save(configPath, updated);
    RecordSystemEvent("Library", "Library path updated", $"Library path saved: {newPath}", "api");
    var task = taskMonitor.Start("library-scan", "Library scan", "Scanning updated library folder...");
    _ = Task.Run(async () =>
    {
        try
        {
            var items = await cache.RescanAsync(task.Id);
            taskMonitor.Complete(task.Id, $"Scan complete: {items.Count} item(s) indexed.", 100);
        }
        catch (Exception ex)
        {
            taskMonitor.Fail(task.Id, $"Scan failed: {ex.Message}");
        }
    });
    return Results.Ok(new { libraryPath = newPath, libraries = cache.Libraries, taskId = task.Id, message = "Library saved. Scan started." });
});

app.MapPost("/api/settings/libraries", (JsonElement payload) =>
{
    var operation = payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("operation", out var operationElement)
        ? operationElement.GetString() ?? string.Empty
        : string.Empty;
    var removeLibraryOperation = operation.Equals("remove-library", StringComparison.OrdinalIgnoreCase);
    var parsedLibraries = LibrarySettingsJson.ParseLibraries(payload, contentRoot);

    var missingFolders = parsedLibraries
        .SelectMany(l => l.Folders ?? new List<string>())
        .Where(f => !Directory.Exists(f))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
    if (missingFolders.Length > 0)
        return Results.BadRequest(new
        {
            error = "One or more library folders were not found from inside the Guidevault server/container. Paste the server path that Guidevault can see, such as /library/Strategy Guides, not an unmapped Windows host path. Guidevault scans files in place and will not create folders automatically.",
            missingFolders
        });

    var updated = new LibrarySettings
    {
        LibraryPath = parsedLibraries.FirstOrDefault()?.Folders.FirstOrDefault(),
        Libraries = parsedLibraries
    };

    cache.SetLibraries(updated.Libraries);
    LibrarySettingsStore.Save(configPath, updated);
    RecordSystemEvent("Library", removeLibraryOperation || parsedLibraries.Count == 0 ? "Library removed" : "Libraries updated", removeLibraryOperation || parsedLibraries.Count == 0 ? "Library entries were removed and reindexing started." : $"Saved {parsedLibraries.Count} library definition(s) and started a scan.", "api");
    var emptyLibraryList = parsedLibraries.Count == 0;
    var removalActivity = removeLibraryOperation || emptyLibraryList;
    var task = taskMonitor.Start(
        removalActivity ? "library-removal" : "library-scan",
        removalActivity ? "Library removal" : "Library scan",
        removalActivity ? "Removing library and clearing indexed items..." : "Scanning updated libraries...");
    _ = Task.Run(async () =>
    {
        try
        {
            var items = await cache.RescanAsync(task.Id, removalActivity ? "removal" : "scan");
            taskMonitor.Complete(task.Id,
                removalActivity
                    ? "Library removed. Indexed items cleared."
                    : $"Scan complete: {items.Count} item(s) indexed.",
                100);
        }
        catch (Exception ex)
        {
            taskMonitor.Fail(task.Id, removalActivity ? $"Library removal failed: {ex.Message}" : $"Scan failed: {ex.Message}");
        }
    });
    return Results.Ok(new
    {
        libraries = cache.Libraries,
        taskId = task.Id,
        message = removalActivity ? "Library removal started. Clearing indexed items." : "Libraries saved. Scan started."
    });
});

app.MapGet("/api/items/{id}", async (string id) =>
{
    var item = (await cache.GetItemsAsync()).FirstOrDefault(i => i.Id == id);
    return item is null ? Results.NotFound() : Results.Ok(item);
});

app.MapGet("/api/items/{id}/reviews", (string id) =>
{
    if (string.IsNullOrWhiteSpace(id))
        return Results.BadRequest(new { error = "Item id is required." });

    return Results.Ok(new
    {
        itemId = id,
        reviews = itemReviewStore.GetPublicForItem(id)
    });
});

app.MapPut("/api/items/{id}/reviews", (string id, GuidevaultReviewRequest payload) =>
{
    if (string.IsNullOrWhiteSpace(id))
        return Results.BadRequest(new { error = "Item id is required." });

    if (payload is null)
        return Results.BadRequest(new { error = "Review payload is required." });

    var saved = itemReviewStore.Upsert(id, payload);
    return Results.Ok(new
    {
        review = saved,
        reviews = itemReviewStore.GetPublicForItem(id)
    });
});

app.MapDelete("/api/reviews/{reviewId}", (string reviewId, string? user) =>
{
    if (string.IsNullOrWhiteSpace(reviewId))
        return Results.BadRequest(new { error = "Review id is required." });

    var deleted = itemReviewStore.Delete(reviewId, user);
    return Results.Ok(new { deleted });
});


app.MapGet("/api/openlibrary/search", async (string? q, string? secondary, string? isbn, string? title, string? gameTitle, string? publisher, string? year, int? limit) =>
{
    try
    {
        var results = await OpenLibraryMetadataClient.SearchAsync(q, secondary, isbn, title, gameTitle, publisher, year, limit ?? 16);
        RecordSystemEvent("API", "Open Library search", $"Open Library search returned {results.Count} result(s).", "api");
        return Results.Ok(new
        {
            provider = "Open Library",
            searchMode = "title-first",
            results
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = $"Open Library search failed: {ex.Message}" });
    }
});

app.MapPost("/api/openlibrary/resolve", async (JsonElement payload) =>
{
    try
    {
        var result = await OpenLibraryMetadataClient.ResolveAsync(payload);
        RecordSystemEvent("API", "Open Library metadata resolved", "Open Library metadata result was loaded for comparison.", "api");
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = $"Open Library metadata lookup failed: {ex.Message}" });
    }
});

app.MapGet("/api/igdb/status", async () =>
{
    try
    {
        var settings = serverSettingsStore.GetSnapshot();
        var status = await IgdbGameMetadataClient.TestCredentialsAsync(settings.IgdbClientId, settings.IgdbClientSecret);
        return Results.Ok(status);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { ok = false, error = ex.Message });
    }
});

app.MapGet("/api/igdb/search", async (string? q, string? platform, string? year, int? limit) =>
{
    try
    {
        var settings = serverSettingsStore.GetSnapshot();
        var results = await IgdbGameMetadataClient.SearchAsync(q, platform, year, limit ?? 16, settings.IgdbClientId, settings.IgdbClientSecret);
        RecordSystemEvent("API", "IGDB search", $"IGDB search returned {results.Count} result(s).", "api");
        return Results.Ok(new
        {
            provider = "IGDB",
            searchMode = "game-title-first",
            results
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = $"IGDB game metadata search failed: {ex.Message}" });
    }
});

app.MapPost("/api/igdb/resolve", async (JsonElement payload) =>
{
    try
    {
        var settings = serverSettingsStore.GetSnapshot();
        var result = await IgdbGameMetadataClient.ResolveAsync(payload, settings.IgdbClientId, settings.IgdbClientSecret);
        RecordSystemEvent("API", "IGDB metadata resolved", "IGDB metadata result was loaded for comparison.", "api");
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = $"IGDB game metadata lookup failed: {ex.Message}" });
    }
});


app.MapGet("/api/esrb/search", async (string? q, string? platform, int? limit) =>
{
    try
    {
        var results = await EsrbRatingMetadataClient.SearchAsync(q, platform, limit ?? 12);
        RecordSystemEvent("API", "ESRB search", $"ESRB search returned {results.Count} result(s).", "api");
        return Results.Ok(new
        {
            provider = "ESRB",
            searchMode = "game-title-first",
            results
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = $"ESRB rating search failed: {ex.Message}" });
    }
});

app.MapPost("/api/esrb/resolve", async (JsonElement payload) =>
{
    try
    {
        var result = await EsrbRatingMetadataClient.ResolveAsync(payload);
        RecordSystemEvent("API", "ESRB rating resolved", "ESRB rating result was loaded for comparison.", "api");
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = $"ESRB rating lookup failed: {ex.Message}" });
    }
});

app.MapPut("/api/items/{id}/metadata", (string id, JsonElement payload) =>
{
    if (string.IsNullOrWhiteSpace(id))
        return Results.BadRequest(new { error = "Item id is required." });

    // Metadata save must be a lightweight text write. Do not call GetItemsAsync()
    // here: that can wait behind a scan/cleanup task or trigger a cold library
    // rebuild, making a simple metadata save feel like it is hanging.
    var update = ItemMetadataJsonReader.Read(payload);
    var effectiveUpdate = metadataStore.PrepareIncomingOverride(id, update, MetadataPayloadOptions.OverwriteLockedFields(payload));
    metadataStore.MergeOverride(id, effectiveUpdate);

    // If the item is already loaded, update the active/persisted cache immediately
    // and return the full updated item. If it is not loaded, still return success;
    // the saved override will be applied the next time the item is indexed/loaded.
    var cached = cache.TryGetCachedItem(id);
    RecordSystemEvent("Metadata", "Metadata updated", cached is not null ? $"Updated metadata for {cached.Title}." : $"Updated metadata override for item {id}.", "api", id, cached?.Title);
    if (cached is not null)
    {
        var updated = metadataStore.ApplyOverride(cached);
        updated = MetadataStore.ApplyUpdateSnapshot(updated, effectiveUpdate);
        cache.ReplaceCachedItem(updated, persist: false);
        return Results.Ok(updated);
    }

    return Results.Ok(new
    {
        id,
        saved = true,
        message = "Metadata saved. The saved values will apply when this item is loaded."
    });
});

app.MapPost("/api/items/metadata/bulk", (JsonElement payload) =>
{
    try
    {
        var requests = BulkMetadataJsonReader.Read(payload);
        if (requests.Count == 0)
            return Results.BadRequest(new { error = "No metadata updates were supplied." });

        var effectiveUpdates = requests
            .Where(request => !string.IsNullOrWhiteSpace(request.Id))
            .Select(request => new BulkMetadataUpdateRequest(
                request.Id.Trim(),
                metadataStore.PrepareIncomingOverride(request.Id.Trim(), request.Update, request.OverwriteLockedFields),
                request.OverwriteLockedFields))
            .ToArray();

        if (effectiveUpdates.Length == 0)
            return Results.BadRequest(new { error = "No valid metadata updates were supplied." });

        metadataStore.MergeOverrides(effectiveUpdates);

        var updateCache = true;
        if (payload.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in payload.EnumerateObject())
            {
                if (!property.Name.Equals("updateCache", StringComparison.OrdinalIgnoreCase)) continue;
                if (property.Value.ValueKind == JsonValueKind.False) updateCache = false;
                else if (property.Value.ValueKind == JsonValueKind.True) updateCache = true;
                else if (property.Value.ValueKind == JsonValueKind.String && bool.TryParse(property.Value.GetString(), out var parsed)) updateCache = parsed;
                break;
            }
        }

        var cacheUpdated = 0;
        var missingIds = new List<string>();

        if (updateCache)
        {
            var cachedItems = cache.GetItemsSnapshot()
                .Where(item => !string.IsNullOrWhiteSpace(item.Id))
                .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            var updatedItems = new List<LibraryItem>();

            foreach (var request in effectiveUpdates)
            {
                if (!cachedItems.TryGetValue(request.Id, out var cached))
                {
                    missingIds.Add(request.Id);
                    continue;
                }

                var updated = metadataStore.ApplyOverride(cached);
                updated = MetadataStore.ApplyUpdateSnapshot(updated, request.Update);
                updatedItems.Add(updated);
            }

            cacheUpdated = cache.ReplaceCachedItems(updatedItems, persist: false);
        }

        RecordSystemEvent("Metadata", "Bulk metadata updated", $"Saved {effectiveUpdates.Length} metadata row(s).", "api");

        // Do not return the updated LibraryItem list here. Large JSON imports can update
        // thousands of rows, and echoing all rows back can create a huge response and make
        // the browser/app look like it crashed. The client applies saved rows locally and
        // refreshes the server cache once after all chunks finish.
        return Results.Ok(new
        {
            processed = requests.Count,
            saved = effectiveUpdates.Length,
            cacheUpdated,
            cacheRefreshDeferred = !updateCache,
            missingCount = missingIds.Count,
            missingIds = missingIds.Take(25).ToArray(),
            message = $"Saved {effectiveUpdates.Length} metadata row(s) in one bulk write."
        });
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Bulk metadata save failed.");
        return Results.BadRequest(new { error = $"Bulk metadata save failed: {ex.Message}" });
    }
});

app.MapPost("/api/items/metadata/refresh-cache", (JsonElement payload) =>
{
    try
    {
        var ids = MetadataIdListJsonReader.Read(payload);
        if (ids.Count == 0)
            return Results.BadRequest(new { error = "No item ids were supplied for metadata cache refresh." });

        var cacheUpdated = cache.RefreshCachedMetadataOverrides(ids, persist: false);
        return Results.Ok(new
        {
            requested = ids.Count,
            cacheUpdated,
            message = $"Refreshed cached metadata overrides for {cacheUpdated} row(s)."
        });
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Metadata cache refresh failed.");
        return Results.BadRequest(new { error = $"Metadata cache refresh failed: {ex.Message}" });
    }
});

app.MapPost("/api/items/{id}/metadata/native-export", async (string id, JsonElement payload) =>
{
    if (string.IsNullOrWhiteSpace(id))
        return Results.BadRequest(new { error = "Item id is required." });

    var update = ItemMetadataJsonReader.Read(payload);
    var effectiveUpdate = metadataStore.PrepareIncomingOverride(id, update, true);
    metadataStore.MergeOverride(id, effectiveUpdate);

    var cached = cache.TryGetCachedItem(id);
    if (cached is null)
    {
        if (GuidevaultLibraryIoGate.IsBusy)
            return Results.Conflict(new { error = GuidevaultLibraryIoGate.BusyMessage });

        cached = (await cache.GetItemsAsync()).FirstOrDefault(i => i.Id == id);
    }

    if (cached is null)
        return Results.NotFound(new { error = "Item not found." });

    if (!GuidevaultLibraryIoGate.TryBeginArchiveWrite(out var archiveWriteLease, out var busyMessage) || archiveWriteLease is null)
        return Results.Conflict(new { error = busyMessage });

    try
    {
        var updated = metadataStore.ApplyOverride(cached);
        updated = MetadataStore.ApplyUpdateSnapshot(updated, effectiveUpdate);
        cache.ReplaceCachedItem(updated, persist: false);

        var exportDocument = GuidevaultNativeMetadata.BuildExport(updated, effectiveUpdate, payload);
        var exportJson = JsonSerializer.Serialize(exportDocument, GuidevaultNativeMetadata.JsonOptions);
        var writeResult = await ArchiveReader.WriteGuidevaultMetadataAsync(updated.Path, updated.Kind, exportJson);
        if (!writeResult.Success)
            return Results.BadRequest(new { error = writeResult.Message });

        RecordSystemEvent("Metadata", "Guidevault JSON written", $"Wrote Guidevault JSON metadata to {updated.Title}.", "api", id, updated.Title);

        return Results.Ok(new
        {
            item = updated,
            metadataFileName = writeResult.MetadataFileName,
            writtenArchivePath = writeResult.WrittenArchivePath,
            writtenArchiveFileName = Path.GetFileName(writeResult.WrittenArchivePath),
            createdPackage = writeResult.CreatedPackage,
            originalArchivePath = writeResult.OriginalArchivePath,
            message = writeResult.Message
        });
    }
    finally
    {
        archiveWriteLease.Dispose();
    }
});


app.MapPost("/api/items/metadata/native-export/bulk", async (JsonElement payload) =>
{
    try
    {
        var ids = MetadataIdListJsonReader.Read(payload);
        if (ids.Count == 0)
            return Results.BadRequest(new { error = "No item ids were supplied for metadata write-back." });

        if (!GuidevaultLibraryIoGate.TryBeginArchiveWrite(out var archiveWriteLease, out var busyMessage) || archiveWriteLease is null)
            return Results.Conflict(new { error = busyMessage });

        try
        {
            var snapshot = cache.GetItemsSnapshot()
                .Where(item => !string.IsNullOrWhiteSpace(item.Id))
                .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            var results = new List<object>();
            var updatedItems = new List<LibraryItem>();
            var written = 0;
            var failed = 0;

            foreach (var id in ids)
            {
                var cached = cache.TryGetCachedItem(id) ?? (snapshot.TryGetValue(id, out var item) ? item : null);
                if (cached is null)
                {
                    failed++;
                    results.Add(new { id, success = false, message = "Item was not found in the active library cache." });
                    continue;
                }

                var updated = metadataStore.ApplyOverride(cached);
                cache.ReplaceCachedItem(updated, persist: false);
                updatedItems.Add(updated);

                var exportDocument = GuidevaultNativeMetadata.BuildExport(updated, new ItemMetadataUpdate());
                var exportJson = JsonSerializer.Serialize(exportDocument, GuidevaultNativeMetadata.JsonOptions);
                var writeResult = await ArchiveReader.WriteGuidevaultMetadataAsync(updated.Path, updated.Kind, exportJson);
                if (writeResult.Success) written++; else failed++;

                results.Add(new
                {
                    id = updated.Id,
                    title = updated.Title,
                    kind = updated.Kind,
                    fileName = updated.FileName,
                    success = writeResult.Success,
                    metadataFileName = writeResult.MetadataFileName,
                    writtenArchivePath = writeResult.WrittenArchivePath,
                    writtenArchiveFileName = string.IsNullOrWhiteSpace(writeResult.WrittenArchivePath) ? string.Empty : Path.GetFileName(writeResult.WrittenArchivePath),
                    createdPackage = writeResult.CreatedPackage,
                    originalArchivePath = writeResult.OriginalArchivePath,
                    message = writeResult.Message
                });
            }

            if (updatedItems.Count > 0) cache.ReplaceCachedItems(updatedItems, persist: false);
            RecordSystemEvent("Metadata", "Bulk Guidevault JSON write-back", $"Wrote Guidevault JSON metadata to {written} file(s); {failed} failed.", "api");

            return Results.Ok(new
            {
                requested = ids.Count,
                written,
                failed,
                results,
                message = failed == 0
                    ? $"Wrote Guidevault JSON metadata to {written} selected file(s)."
                    : $"Wrote Guidevault JSON metadata to {written} file(s); {failed} failed."
            });
        }
        finally
        {
            archiveWriteLease.Dispose();
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Bulk native metadata write-back failed.");
        return Results.BadRequest(new { error = $"Bulk native metadata write-back failed: {ex.Message}" });
    }
});

app.MapPost("/api/items/files/organize/preview", (JsonElement payload) =>
{
    try
    {
        var ids = MetadataIdListJsonReader.Read(payload);
        if (ids.Count == 0)
            return Results.BadRequest(new { error = "No item ids were supplied for file organization preview." });

        var snapshot = cache.GetItemsSnapshot();
        var plans = BuildFileOrganizationPlans(ids, payload, snapshot, metadataStore, cache.LibraryPaths, apply: false);
        var ready = plans.Count(plan => plan.Status.Equals("Ready", StringComparison.OrdinalIgnoreCase));
        return Results.Ok(new
        {
            requested = ids.Count,
            readyToApply = ready,
            unchanged = plans.Count(plan => plan.Status.Equals("Unchanged", StringComparison.OrdinalIgnoreCase)),
            blocked = plans.Count(plan => !plan.Status.Equals("Ready", StringComparison.OrdinalIgnoreCase) && !plan.Status.Equals("Unchanged", StringComparison.OrdinalIgnoreCase)),
            results = plans,
            message = ready > 0
                ? $"Previewed {plans.Count} file(s). {ready} file(s) are ready to move/rename."
                : $"Previewed {plans.Count} file(s). No file moves are currently ready."
        });
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "File organization preview failed.");
        return Results.BadRequest(new { error = $"File organization preview failed: {ex.Message}" });
    }
});

app.MapPost("/api/items/files/organize/apply", (JsonElement payload) =>
{
    try
    {
        var ids = MetadataIdListJsonReader.Read(payload);
        if (ids.Count == 0)
            return Results.BadRequest(new { error = "No item ids were supplied for file organization." });

        if (!GuidevaultLibraryIoGate.TryBeginArchiveWrite(out var archiveWriteLease, out var busyMessage) || archiveWriteLease is null)
            return Results.Conflict(new { error = busyMessage });

        try
        {
            var snapshot = cache.GetItemsSnapshot();
            var plans = BuildFileOrganizationPlans(ids, payload, snapshot, metadataStore, cache.LibraryPaths, apply: true);
            var movedItems = new List<LibraryItem>();
            var results = new List<FileOrganizationPlan>();
            var moved = 0;
            var failed = 0;

            foreach (var plan in plans)
            {
                if (!plan.Status.Equals("Ready", StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(plan);
                    if (!plan.Status.Equals("Unchanged", StringComparison.OrdinalIgnoreCase)) failed++;
                    continue;
                }

                try
                {
                    var sourcePath = Path.GetFullPath(plan.CurrentPath);
                    var destinationPath = Path.GetFullPath(plan.ProposedPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? Directory.GetCurrentDirectory());
                    File.Move(sourcePath, destinationPath);
                    ArchiveReader.ClearCoverCacheForPath(sourcePath);
                    ArchiveReader.ClearCoverCacheForPath(destinationPath);

                    var updated = snapshot.FirstOrDefault(item => string.Equals(item.Id, plan.Id, StringComparison.OrdinalIgnoreCase));
                    if (updated is not null)
                    {
                        // Preserve the existing GuideVault item identity and current metadata before changing the path.
                        // Without this, a later scan can infer metadata from the newly generated filename/folder and
                        // make the renamed file look like a brand-new entry.
                        updated = metadataStore.ApplyOverride(updated);
                        metadataStore.MergeOverride(updated.Id, CreateMetadataSnapshotForMove(updated));

                        var info = new FileInfo(destinationPath);
                        updated = updated with
                        {
                            Path = destinationPath,
                            RelativePath = BuildRelativeLibraryPath(destinationPath, cache.LibraryPaths, updated.RelativePath),
                            FileName = info.Name,
                            SizeBytes = info.Length,
                            // Keep the original library-added timestamp. A rename/move is not a new library add.
                            Added = updated.Added,
                            Modified = info.LastWriteTimeUtc
                        };
                        fileIdentityStore.RememberRename(sourcePath, destinationPath, updated.Id);
                        movedItems.Add(updated);
                    }

                    moved++;
                    results.Add(plan with { Status = "Moved", Message = "File moved and Guidevault cache updated." });
                }
                catch (Exception ex)
                {
                    failed++;
                    results.Add(plan with { Status = "Failed", Message = ex.Message });
                }
            }

            if (movedItems.Count > 0) cache.ReplaceCachedItems(movedItems, persist: true);
            RecordSystemEvent("Files", "Files moved or renamed", $"Moved/renamed {moved} file(s); {failed} failed.", "api");

            return Results.Ok(new
            {
                requested = ids.Count,
                moved,
                failed,
                items = movedItems,
                results,
                message = failed == 0
                    ? $"Moved/renamed {moved} file(s)."
                    : $"Moved/renamed {moved} file(s); {failed} file(s) were blocked or failed."
            });
        }
        finally
        {
            archiveWriteLease.Dispose();
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "File organization apply failed.");
        return Results.BadRequest(new { error = $"File organization apply failed: {ex.Message}" });
    }
});


app.MapPost("/api/items/files/convert", (JsonElement payload) =>
{
    static string ReadString(JsonElement json, string name)
    {
        if (json.ValueKind != JsonValueKind.Object) return string.Empty;
        if (json.TryGetProperty(name, out var direct) && direct.ValueKind == JsonValueKind.String) return direct.GetString()?.Trim() ?? string.Empty;
        foreach (var property in json.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.String)
                return property.Value.GetString()?.Trim() ?? string.Empty;
        }
        return string.Empty;
    }

    try
    {
        var ids = MetadataIdListJsonReader.Read(payload)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (ids.Count == 0)
            return Results.BadRequest(new { error = "No item ids were supplied for file conversion." });

        var targetFormat = ReadString(payload, "targetFormat").ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(targetFormat))
            return Results.BadRequest(new { error = "Choose a target format before converting files." });

        if (targetFormat is not ("cbz" or "pdf"))
            return Results.BadRequest(new { error = $"Unsupported conversion target: {targetFormat}." });

        var targetLabel = targetFormat.ToUpperInvariant();
        var task = taskMonitor.Start("file-conversion", "File conversion", $"Queued {targetLabel} conversion for {ids.Count} file(s).");
        fileConversionJobs.Start(task.Id, ids.Count, targetLabel);

        _ = Task.Run(async () =>
        {
            var results = new List<object>();
            var converted = 0;
            var failed = 0;

            try
            {
                taskMonitor.Update(task.Id, $"Preparing {targetLabel} conversion for {ids.Count} file(s)...", 2);
                fileConversionJobs.Update(task.Id, "running", $"Preparing {targetLabel} conversion...", ids.Count, converted, failed, results);

                if (!GuidevaultLibraryIoGate.TryBeginArchiveWrite(out var archiveWriteLease, out var busyMessage) || archiveWriteLease is null)
                {
                    taskMonitor.Fail(task.Id, busyMessage);
                    fileConversionJobs.Update(task.Id, "failed", busyMessage, ids.Count, converted, ids.Count, results);
                    return;
                }

                try
                {
                    var snapshot = cache.GetItemsSnapshot()
                        .Where(item => !string.IsNullOrWhiteSpace(item.Id))
                        .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

                    for (var index = 0; index < ids.Count; index++)
                    {
                        var id = ids[index];
                        var item = cache.TryGetCachedItem(id) ?? (snapshot.TryGetValue(id, out var cached) ? cached : null);
                        var itemNumber = index + 1;
                        var baseProgress = 5 + (int)Math.Round((index / Math.Max(1d, ids.Count)) * 88d);

                        if (item is null)
                        {
                            failed++;
                            results.Add(new { id, success = false, message = "Item was not found in the active library cache." });
                            taskMonitor.Update(task.Id, $"Skipped missing item {itemNumber} of {ids.Count}.", Math.Min(96, baseProgress));
                            fileConversionJobs.Update(task.Id, "running", $"Skipped missing item {itemNumber} of {ids.Count}.", ids.Count, converted, failed, results);
                            continue;
                        }

                        var updated = metadataStore.ApplyOverride(item);
                        taskMonitor.Update(task.Id, $"Converting {itemNumber} of {ids.Count}: {updated.Title}", Math.Min(96, baseProgress));
                        fileConversionJobs.Update(task.Id, "running", $"Converting {itemNumber} of {ids.Count}: {updated.Title}", ids.Count, converted, failed, results);

                        var exportDocument = GuidevaultNativeMetadata.BuildExport(updated, new ItemMetadataUpdate());
                        var exportJson = JsonSerializer.Serialize(exportDocument, GuidevaultNativeMetadata.JsonOptions);
                        var result = await ArchiveReader.ConvertArchiveAsync(updated.Path, updated.Kind, exportJson, targetFormat);
                        if (result.Success) converted++; else failed++;
                        results.Add(new
                        {
                            id = updated.Id,
                            title = updated.Title,
                            kind = updated.Kind,
                            fileName = updated.FileName,
                            success = result.Success,
                            sourceFileName = result.SourceFileName,
                            sourceFormat = result.SourceFormat,
                            sourceBytes = result.SourceBytes,
                            outputPath = result.OutputPath,
                            outputFileName = result.OutputFileName,
                            targetFormat = result.TargetFormat,
                            outputBytes = result.OutputBytes,
                            createdPackage = result.CreatedPackage,
                            message = result.Message
                        });

                        var progress = 5 + (int)Math.Round((itemNumber / Math.Max(1d, ids.Count)) * 90d);
                        taskMonitor.Update(task.Id, $"Converted {converted} file(s); {failed} failed.", Math.Min(98, progress));
                        fileConversionJobs.Update(task.Id, "running", $"Converted {converted} file(s); {failed} failed.", ids.Count, converted, failed, results);
                    }

                    var finalMessage = failed == 0
                        ? $"Converted {converted} selected file(s)."
                        : $"Converted {converted} file(s); {failed} failed.";
                    RecordSystemEvent("Files", "File format conversion", $"Converted {converted} file(s); {failed} failed.", "api");
                    taskMonitor.Complete(task.Id, finalMessage, 100);
                    fileConversionJobs.Update(task.Id, failed == 0 ? "completed" : "completed", finalMessage, ids.Count, converted, failed, results);
                }
                finally
                {
                    archiveWriteLease.Dispose();
                }
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "File conversion failed.");
                failed = Math.Max(failed, ids.Count - converted);
                var message = $"File conversion failed: {ex.Message}";
                taskMonitor.Fail(task.Id, message);
                fileConversionJobs.Update(task.Id, "failed", message, ids.Count, converted, failed, results);
            }
        });

        return Results.Ok(new
        {
            taskId = task.Id,
            id = task.Id,
            kind = task.Kind,
            title = task.Title,
            status = task.Status,
            message = $"Started {targetLabel} conversion for {ids.Count} selected file(s). Track progress in Tasks.",
            progressPercent = task.ProgressPercent,
            updatedAt = task.UpdatedAt
        });
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "File conversion start failed.");
        return Results.BadRequest(new { error = $"File conversion start failed: {ex.Message}" });
    }
});

app.MapGet("/api/items/files/convert/{taskId}", (string taskId) =>
{
    var job = fileConversionJobs.Get(taskId);
    return job is null ? Results.NotFound(new { error = "Conversion job was not found." }) : Results.Ok(job);
});

app.MapPost("/api/items/{id}/metadata/enrich-native", async (string id) =>
{
    if (string.IsNullOrWhiteSpace(id))
        return Results.BadRequest(new { error = "Item id is required." });

    var cached = cache.TryGetCachedItem(id);
    if (cached is null)
    {
        if (GuidevaultLibraryIoGate.IsBusy)
            return Results.Conflict(new { error = GuidevaultLibraryIoGate.BusyMessage });

        cached = cache.GetItemsSnapshot().FirstOrDefault(i => i.Id == id)
            ?? (await cache.GetItemsAsync()).FirstOrDefault(i => i.Id == id);
    }

    if (cached is null)
        return Results.NotFound(new { error = "Item not found." });

    if (string.IsNullOrWhiteSpace(cached.Path) || !File.Exists(cached.Path))
        return Results.BadRequest(new { error = "The source file could not be found. Refresh the library before enriching this file." });

    if (cached.Format.Equals("PDF", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { error = "Single-file native metadata enrichment is available for CBZ/CBR packages. PDF metadata packages are written as ZIP exports instead." });

    if (!GuidevaultLibraryIoGate.TryBeginArchiveWrite(out var archiveReadLease, out var busyMessage) || archiveReadLease is null)
        return Results.Conflict(new { error = busyMessage });

    try
    {
        var nativeUpdate = await ArchiveReader.GetGuidevaultMetadataAsync(cached.Path);
        if (nativeUpdate is null)
            return Results.NotFound(new { error = "No Guidevault JSON metadata file was found inside this package." });

        var normalizedUpdate = nativeUpdate with
        {
            MetadataSource = string.IsNullOrWhiteSpace(nativeUpdate.MetadataSource) ? "Guidevault JSON" : nativeUpdate.MetadataSource
        };

        var effectiveUpdate = metadataStore.PrepareIncomingOverride(id, normalizedUpdate, false);
        metadataStore.MergeOverride(id, effectiveUpdate);
        var updated = metadataStore.ApplyOverride(cached);
        updated = MetadataStore.ApplyUpdateSnapshot(updated, effectiveUpdate);
        cache.ReplaceCachedItem(updated, persist: true);

        return Results.Ok(new
        {
            item = updated,
            message = "Read Guidevault JSON metadata from this package and updated the Guidevault index/database entry."
        });
    }
    finally
    {
        archiveReadLease.Dispose();
    }
});

app.MapPost("/api/items/{id}/file/rename-to-suggested", async (string id, JsonElement payload) =>
{
    if (string.IsNullOrWhiteSpace(id))
        return Results.BadRequest(new { error = "Item id is required." });

    var update = ItemMetadataJsonReader.Read(payload);
    var effectiveUpdate = metadataStore.PrepareIncomingOverride(id, update, true);
    metadataStore.MergeOverride(id, effectiveUpdate);

    var cached = cache.TryGetCachedItem(id);
    if (cached is null)
    {
        if (GuidevaultLibraryIoGate.IsBusy)
            return Results.Conflict(new { error = GuidevaultLibraryIoGate.BusyMessage });

        cached = (await cache.GetItemsAsync()).FirstOrDefault(i => i.Id == id);
    }

    if (cached is null)
        return Results.NotFound(new { error = "Item not found." });

    if (string.IsNullOrWhiteSpace(cached.Path) || !File.Exists(cached.Path))
        return Results.BadRequest(new { error = "The source file could not be found. Refresh the library before renaming." });

    var updated = metadataStore.ApplyOverride(cached);
    updated = MetadataStore.ApplyUpdateSnapshot(updated, effectiveUpdate);

    var suggestedFileName = GuidevaultNativeMetadata.BuildSuggestedFileName(updated, effectiveUpdate, payload);
    var safeFileName = GuidevaultNativeMetadata.SanitizeSuggestedFileName(suggestedFileName, updated.FileName);
    if (string.IsNullOrWhiteSpace(safeFileName))
        return Results.BadRequest(new { error = "Unable to generate a safe suggested filename from this metadata." });

    if (GuidevaultNativeMetadata.IsLikelyDosShortFileName(safeFileName))
        return Results.BadRequest(new { error = $"Refusing to rename the source file to {safeFileName} because it looks like a Windows 8.3 short-name alias. Save the metadata fields first, confirm the Filename tab preview shows a real title, then try again." });

    var sourcePath = Path.GetFullPath(updated.Path);
    var sourceDirectory = Path.GetDirectoryName(sourcePath);
    if (string.IsNullOrWhiteSpace(sourceDirectory))
        return Results.BadRequest(new { error = "Unable to resolve the source file directory." });

    var destinationPath = Path.GetFullPath(Path.Combine(sourceDirectory, safeFileName));
    if (!string.Equals(Path.GetDirectoryName(destinationPath), sourceDirectory, StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { error = "Suggested filename must stay in the current source folder." });

    var oldFileName = Path.GetFileName(sourcePath);
    if (string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
    {
        cache.ReplaceCachedItem(updated, persist: true);
        fileIdentityStore.RememberRename(sourcePath, destinationPath, id);
        return Results.Ok(new
        {
            item = updated,
            oldFileName,
            newFileName = oldFileName,
            oldPath = sourcePath,
            newPath = destinationPath,
            renamed = false,
            message = "The source file already matches the suggested filename."
        });
    }

    if (File.Exists(destinationPath))
        return Results.BadRequest(new { error = $"A file named {Path.GetFileName(destinationPath)} already exists in this folder." });

    if (!GuidevaultLibraryIoGate.TryBeginArchiveWrite(out var archiveWriteLease, out var busyMessage) || archiveWriteLease is null)
        return Results.Conflict(new { error = busyMessage });

    try
    {
        File.Move(sourcePath, destinationPath);
        ArchiveReader.ClearCoverCacheForPath(sourcePath);
        ArchiveReader.ClearCoverCacheForPath(destinationPath);

        var info = new FileInfo(destinationPath);
        var renamedItem = updated with
        {
            Path = destinationPath,
            RelativePath = BuildRelativeLibraryPath(destinationPath, cache.LibraryPaths, updated.RelativePath),
            FileName = info.Name,
            SizeBytes = info.Length,
            Added = info.CreationTimeUtc,
            Modified = info.LastWriteTimeUtc
        };

        fileIdentityStore.RememberRename(sourcePath, destinationPath, id);
        cache.ReplaceCachedItem(renamedItem, persist: true);

        return Results.Ok(new
        {
            item = renamedItem,
            oldFileName,
            newFileName = info.Name,
            oldPath = sourcePath,
            newPath = destinationPath,
            renamed = true,
            message = $"Renamed source file to {info.Name} and updated the Guidevault index."
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = $"Unable to rename source file: {ex.Message}" });
    }
    finally
    {
        archiveWriteLease.Dispose();
    }
});

app.MapPost("/api/items/{id}/strategy-platforms/resolve", async (string id) =>
{
    var item = (await cache.GetItemsAsync()).FirstOrDefault(i => i.Id == id);
    if (item is null) return Results.NotFound(new { error = "Item not found." });
    if (!string.Equals(item.Kind, "Strategy Guide", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { error = "Platform lookup is only available for strategy guides." });

    var resolution = await StrategyGuidePlatformResolver.ResolveAsync(item);
    var associated = resolution.AssociatedPlatforms.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    var detected = associated.Length > 0 ? associated[0] : "Unsorted Strategy Guides";
    var tags = item.Tags
        .Where(t => !string.IsNullOrWhiteSpace(t) && !t.Equals("Unsorted Strategy Guides", StringComparison.OrdinalIgnoreCase))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    var platformUpdate = new ItemMetadataUpdate(
        System: detected,
        Category: detected,
        Tags: tags,
        Rating: BlankToNull(resolution.Rating),
        Asin: BlankToNull(resolution.Asin),
        Isbn10: BlankToNull(resolution.Isbn10),
        Isbn13: BlankToNull(resolution.Isbn13),
        LanguageTag: BlankToNull(resolution.LanguageTag),
        AssociatedPlatforms: associated,
        PlatformMatchTitle: BlankToNull(resolution.CandidateGameTitle),
        PlatformResolverSource: resolution.Source,
        PlatformResolverConfidence: resolution.Confidence,
        MetadataSource: "Strategy platform resolver");
    metadataStore.MergeOverride(id, metadataStore.PrepareIncomingOverride(id, platformUpdate, false));

    cache.Invalidate();
    var updated = (await cache.GetItemsAsync()).FirstOrDefault(i => i.Id == id);
    return updated is null
        ? Results.NotFound(new { error = "Item not found after lookup." })
        : Results.Ok(new { item = updated, resolution });
});


app.MapDelete("/api/items/{id}", (string id) =>
{
    if (string.IsNullOrWhiteSpace(id))
        return Results.BadRequest(new { error = "Item id is required." });

    // Guidevault scans files in place. "Remove from Library" should only remove
    // the current indexed entry and clear its Guidevault metadata override. It
    // must NOT create a permanent removed/ignore marker, because a later
    // refresh/rescan is expected to find the source file again and re-infer its
    // metadata.
    //
    // Keep this instant: do not call GetItemsAsync() here because that can wait
    // behind a scan or cold-load the full index.
    var cachedForRemoval = cache.TryGetCachedItem(id) ?? cache.GetItemsSnapshot().FirstOrDefault(i => string.Equals(i.Id, id, StringComparison.OrdinalIgnoreCase));
    if (cachedForRemoval is not null)
        ArchiveReader.ClearCacheForPath(cachedForRemoval.Path);

    metadataStore.RemoveOverride(id);
    coverOverrideStore.Remove(id);
    cache.RemoveCachedItem(id, persist: false);
    return Results.Ok(new { removedId = id, mode = "removed-from-index", rescanWillRediscover = true, pathPreserved = true, readerCacheCleared = cachedForRemoval is not null });
});

app.MapGet("/api/items/{id}/cover", async (string id, HttpResponse response) =>
{
    var item = cache.TryGetCachedItem(id) ?? (await cache.GetItemsAsync()).FirstOrDefault(i => i.Id == id);
    if (item is null) return Results.NotFound();
    if (!File.Exists(item.Path))
    {
        response.Headers.CacheControl = "no-store";
        return Results.NotFound();
    }
    response.Headers.CacheControl = "public, max-age=604800, immutable";
    if (item.Format == "PDF") return Results.Redirect("/assets/pdf-cover.svg");

    var coverOverride = coverOverrideStore.Get(item.Id);
    var image = await ArchiveReader.GetCachedCoverImageAsync(item.Path, coverOverride?.EntryKey);
    if (image is null)
    {
        if (GuidevaultLibraryIoGate.IsBusy)
        {
            response.Headers.CacheControl = "no-store";
            response.Headers["Retry-After"] = "15";
            return Results.StatusCode(503);
        }

        image = await ArchiveReader.GetCoverImageAsync(item.Path, coverOverride?.EntryKey);
    }
    if (image is null) return Results.Redirect("/assets/missing-cover.svg");

    return Results.File(
        image.Value.Bytes,
        image.Value.ContentType,
        fileDownloadName: null,
        lastModified: item.Modified,
        entityTag: new Microsoft.Net.Http.Headers.EntityTagHeaderValue($"\"{item.Id}-{item.Modified.ToUnixTimeSeconds()}\""),
        enableRangeProcessing: true);
});

app.MapGet("/api/items/{id}/cover-thumb", async (string id, int? w, HttpResponse response) =>
{
    var item = cache.TryGetCachedItem(id) ?? (await cache.GetItemsAsync()).FirstOrDefault(i => i.Id == id);
    if (item is null) return Results.NotFound();
    if (!File.Exists(item.Path))
    {
        response.Headers.CacheControl = "no-store";
        return Results.NotFound();
    }
    response.Headers.CacheControl = "public, max-age=604800, immutable";
    if (item.Format == "PDF") return Results.Redirect("/assets/pdf-cover.svg");

    var width = Math.Clamp(w ?? 360, 120, 720);
    var coverOverride = coverOverrideStore.Get(item.Id);
    var image = await ArchiveReader.GetCachedCoverThumbnailAsync(item.Path, width, coverOverride?.EntryKey);
    if (image is null)
    {
        if (GuidevaultLibraryIoGate.IsBusy)
        {
            response.Headers.CacheControl = "no-store";
            response.Headers["Retry-After"] = "15";
            return Results.StatusCode(503);
        }

        image = await ArchiveReader.GetCoverThumbnailAsync(item.Path, width, coverOverride?.EntryKey);
    }
    if (image is null) return Results.Redirect("/assets/missing-cover.svg");

    return Results.File(
        image.Value.Bytes,
        image.Value.ContentType,
        fileDownloadName: null,
        lastModified: item.Modified,
        entityTag: new Microsoft.Net.Http.Headers.EntityTagHeaderValue($"\"{item.Id}-{item.Modified.ToUnixTimeSeconds()}-thumb-{width}\""),
        enableRangeProcessing: true);
});

app.MapGet("/api/items/{id}/pages", async (string id) =>
{
    var item = (await cache.GetItemsAsync()).FirstOrDefault(i => i.Id == id);
    if (item is null) return Results.NotFound();
    RecordSystemEvent("Reader", "Book opened", $"Opened {item.Title} in the reader.", "api", item.Id, item.Title);

    var sourceVersion = ArchiveReader.GetArchiveVersionStamp(item.Path);
    var escapedId = Uri.EscapeDataString(item.Id);

    if (item.Format == "PDF")
    {
        return Results.Ok(new
        {
            item.Id,
            item.PageCount,
            format = item.Format,
            sourceVersion,
            pdfUrl = $"/api/items/{escapedId}/file?v={sourceVersion}"
        });
    }

    var pageCount = await ArchiveReader.GetImagePageCountAsync(item.Path);
    return Results.Ok(new
    {
        item.Id,
        pageCount,
        format = item.Format,
        sourceVersion,
        pages = Enumerable.Range(0, pageCount).Select(i => new
        {
            index = i,
            imageUrl = $"/api/items/{escapedId}/page/{i}?v={sourceVersion}"
        })
    });
});

app.MapGet("/api/items/{id}/page/{page:int}", async (string id, int page, HttpResponse response) =>
{
    var item = (await cache.GetItemsAsync()).FirstOrDefault(i => i.Id == id);
    if (item is null) return Results.NotFound();
    if (item.Format == "PDF") return Results.BadRequest(new { error = "PDF pages are handled through the browser PDF viewer in this prototype." });

    var sourceVersion = ArchiveReader.GetArchiveVersionStamp(item.Path);
    response.Headers.CacheControl = "public, max-age=604800, immutable";
    response.Headers.ETag = $"\"{item.Id}-{sourceVersion}-page-{page}\"";

    var image = await ArchiveReader.GetImagePageAsync(item.Path, page);
    return image is null
        ? Results.NotFound()
        : Results.File(image.Value.Bytes, image.Value.ContentType, lastModified: item.Modified, entityTag: new Microsoft.Net.Http.Headers.EntityTagHeaderValue($"\"{item.Id}-{sourceVersion}-page-{page}\""), enableRangeProcessing: true);
});

app.MapGet("/api/items/{id}/page/{page:int}/thumb", async (string id, int page, int? w, HttpResponse response) =>
{
    var item = cache.TryGetCachedItem(id) ?? (await cache.GetItemsAsync()).FirstOrDefault(i => i.Id == id);
    if (item is null) return Results.NotFound();
    if (item.Format == "PDF") return Results.BadRequest(new { error = "PDF pages are handled through the browser PDF viewer in this prototype." });
    if (GuidevaultLibraryIoGate.IsBusy)
    {
        response.Headers.CacheControl = "no-store";
        response.Headers["Retry-After"] = "15";
        return Results.StatusCode(503);
    }

    var sourceVersion = ArchiveReader.GetArchiveVersionStamp(item.Path);
    response.Headers.CacheControl = "public, max-age=604800, immutable";
    response.Headers.ETag = $"\"{item.Id}-{sourceVersion}-page-{page}-thumb\"";

    var width = Math.Clamp(w ?? 180, 80, 360);
    var image = await ArchiveReader.GetImagePageThumbnailAsync(item.Path, page, width);
    return image is null
        ? Results.NotFound()
        : Results.File(image.Value.Bytes, image.Value.ContentType, lastModified: item.Modified, entityTag: new Microsoft.Net.Http.Headers.EntityTagHeaderValue($"\"{item.Id}-{sourceVersion}-page-{page}-thumb-{width}\""), enableRangeProcessing: true);
});

app.MapGet("/api/items/{id}/cover-options", async (string id) =>
{
    var item = cache.TryGetCachedItem(id) ?? (await cache.GetItemsAsync()).FirstOrDefault(i => i.Id == id);
    if (item is null) return Results.NotFound(new { error = "Item not found." });
    if (item.Format == "PDF") return Results.BadRequest(new { error = "PDF covers use the browser/PDF cover placeholder and do not expose archive image pages." });
    if (GuidevaultLibraryIoGate.IsBusy) return Results.Conflict(new { error = "A library scan or file operation is currently running. Load cover pages after that task finishes so the archive reader does not compete with the scan." });

    var entries = ArchiveReader.GetImageEntryKeys(item.Path);
    var selected = coverOverrideStore.Get(item.Id);
    var selectedIndex = -1;
    if (selected is not null)
    {
        selectedIndex = Array.FindIndex(entries, entry => string.Equals(entry, selected.EntryKey, StringComparison.OrdinalIgnoreCase));
    }

    var archiveExtension = Path.GetExtension(item.Path);
    var canDeletePages = archiveExtension.Equals(".cbz", StringComparison.OrdinalIgnoreCase) || archiveExtension.Equals(".zip", StringComparison.OrdinalIgnoreCase);
    var archiveStamp = ArchiveReader.GetArchiveVersionStamp(item.Path);

    return Results.Ok(new
    {
        itemId = item.Id,
        itemTitle = item.Title,
        pageCount = entries.Length,
        canDeletePages,
        deletePageHint = canDeletePages
            ? "Deleting a page rewrites the source CBZ/ZIP archive after confirmation."
            : "Page deletion is available only for writable CBZ/ZIP archives. CBR/RAR archives are preview-only.",
        hasManualOverride = selected is not null,
        selectedEntryKey = selected?.EntryKey ?? string.Empty,
        selectedIndex,
        selectedUpdatedAt = selected?.UpdatedAt,
        entries = entries.Select((entry, index) => new
        {
            index,
            entryKey = entry,
            fileName = Path.GetFileName(entry),
            folder = Path.GetDirectoryName(entry)?.Replace('\\', '/') ?? string.Empty,
            imageUrl = $"/api/items/{Uri.EscapeDataString(item.Id)}/page/{index}?v={archiveStamp}",
            thumbnailUrl = $"/api/items/{Uri.EscapeDataString(item.Id)}/page/{index}/thumb?w=180&v={archiveStamp}",
            isSelected = selected is not null && string.Equals(entry, selected.EntryKey, StringComparison.OrdinalIgnoreCase)
        })
    });
});

app.MapPut("/api/items/{id}/cover-selection", async (string id, JsonElement payload) =>
{
    var item = cache.TryGetCachedItem(id) ?? (await cache.GetItemsAsync()).FirstOrDefault(i => i.Id == id);
    if (item is null) return Results.NotFound(new { error = "Item not found." });
    if (item.Format == "PDF") return Results.BadRequest(new { error = "PDF cover selection is not supported." });

    var entries = ArchiveReader.GetImageEntryKeys(item.Path);
    if (entries.Length == 0) return Results.BadRequest(new { error = "No image pages were found in this archive." });

    string? entryKey = null;
    if (payload.ValueKind == JsonValueKind.Object)
    {
        if (payload.TryGetProperty("entryKey", out var entryProp) && entryProp.ValueKind == JsonValueKind.String)
            entryKey = entryProp.GetString();
        if (string.IsNullOrWhiteSpace(entryKey) && payload.TryGetProperty("pageIndex", out var pageProp) && pageProp.TryGetInt32(out var pageIndex) && pageIndex >= 0 && pageIndex < entries.Length)
            entryKey = entries[pageIndex];
    }

    entryKey = (entryKey ?? string.Empty).Replace('\\', '/').Trim();
    var selectedIndex = Array.FindIndex(entries, entry => string.Equals(entry, entryKey, StringComparison.OrdinalIgnoreCase));
    if (selectedIndex < 0)
        return Results.BadRequest(new { error = "Selected cover page was not found in this archive." });

    var record = coverOverrideStore.Set(item.Id, entries[selectedIndex], selectedIndex, Path.GetFileName(entries[selectedIndex]));
    ArchiveReader.ClearCoverCacheForPath(item.Path);
    RecordSystemEvent("Metadata", "Cover override saved", $"Set manual cover page for {item.Title} to {record.DisplayName}.", "api", item.Id, item.Title);
    return Results.Ok(new
    {
        itemId = item.Id,
        itemTitle = item.Title,
        hasManualOverride = true,
        selectedEntryKey = record.EntryKey,
        selectedIndex = record.PageIndex,
        selectedFileName = record.DisplayName,
        selectedUpdatedAt = record.UpdatedAt,
        message = $"Cover set to page {record.PageIndex + 1}: {record.DisplayName}."
    });
});

app.MapDelete("/api/items/{id}/cover-selection", async (string id) =>
{
    var item = cache.TryGetCachedItem(id) ?? (await cache.GetItemsAsync()).FirstOrDefault(i => i.Id == id);
    if (item is null) return Results.NotFound(new { error = "Item not found." });

    coverOverrideStore.Remove(item.Id);
    ArchiveReader.ClearCoverCacheForPath(item.Path);
    RecordSystemEvent("Metadata", "Cover override cleared", $"Returned {item.Title} to automatic cover selection.", "api", item.Id, item.Title);
    return Results.Ok(new { itemId = item.Id, hasManualOverride = false, message = "Manual cover selection cleared. Guidevault will use automatic cover detection again." });
});

app.MapDelete("/api/items/{id}/archive-page", async (string id, [FromBody] JsonElement payload) =>
{
    var item = cache.TryGetCachedItem(id) ?? (await cache.GetItemsAsync()).FirstOrDefault(i => i.Id == id);
    if (item is null) return Results.NotFound(new { error = "Item not found." });
    if (item.Format == "PDF") return Results.BadRequest(new { error = "PDF page deletion is not supported." });

    var extension = Path.GetExtension(item.Path);
    if (!extension.Equals(".cbz", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
    {
        return Results.BadRequest(new { error = "Page deletion is currently supported only for writable CBZ/ZIP archives. CBR/RAR archives can be previewed, but Guidevault will not rewrite them." });
    }

    if (!File.Exists(item.Path)) return Results.NotFound(new { error = "Source archive was not found." });

    var entries = ArchiveReader.GetImageEntryKeys(item.Path);
    if (entries.Length == 0) return Results.BadRequest(new { error = "No image pages were found in this archive." });

    string? entryKey = null;
    if (payload.ValueKind == JsonValueKind.Object)
    {
        if (payload.TryGetProperty("entryKey", out var entryProp) && entryProp.ValueKind == JsonValueKind.String)
            entryKey = entryProp.GetString();
        if (string.IsNullOrWhiteSpace(entryKey) && payload.TryGetProperty("pageIndex", out var pageProp) && pageProp.TryGetInt32(out var pageIndex) && pageIndex >= 0 && pageIndex < entries.Length)
            entryKey = entries[pageIndex];
    }

    entryKey = (entryKey ?? string.Empty).Replace('\\', '/').Trim();
    var selectedIndex = Array.FindIndex(entries, entry => string.Equals(entry, entryKey, StringComparison.OrdinalIgnoreCase));
    if (selectedIndex < 0)
        return Results.BadRequest(new { error = "Selected page was not found in this archive." });

    var selectedEntry = entries[selectedIndex];
    var selectedFileName = Path.GetFileName(selectedEntry);

    try
    {
        var deleted = ArchiveReader.DeleteImageEntry(item.Path, selectedEntry);
        if (!deleted) return Results.BadRequest(new { error = "Selected page could not be deleted from the archive." });

        var manual = coverOverrideStore.Get(item.Id);
        var removedManualOverride = manual is not null && string.Equals(manual.EntryKey, selectedEntry, StringComparison.OrdinalIgnoreCase);
        if (removedManualOverride) coverOverrideStore.Remove(item.Id);

        ArchiveReader.ClearCoverCacheForPath(item.Path);
        cache.Invalidate();
        RecordSystemEvent("Metadata", "Archive page deleted", $"Deleted page {selectedIndex + 1} ({selectedFileName}) from {item.Title}.", "api", item.Id, item.Title);

        return Results.Ok(new
        {
            itemId = item.Id,
            itemTitle = item.Title,
            deletedEntryKey = selectedEntry,
            deletedIndex = selectedIndex,
            deletedFileName = selectedFileName,
            removedManualOverride,
            selectedUpdatedAt = DateTimeOffset.UtcNow,
            message = $"Deleted page {selectedIndex + 1}: {selectedFileName}."
        });
    }
    catch (InvalidDataException ex)
    {
        return Results.BadRequest(new { error = $"The archive could not be updated as a ZIP/CBZ file: {ex.Message}" });
    }
    catch (UnauthorizedAccessException)
    {
        return Results.BadRequest(new { error = "Guidevault does not have permission to modify this archive." });
    }
    catch (IOException ex)
    {
        return Results.BadRequest(new { error = $"The archive could not be modified: {ex.Message}" });
    }
});

app.MapGet("/api/items/{id}/file", async (string id) =>
{
    var item = (await cache.GetItemsAsync()).FirstOrDefault(i => i.Id == id);
    if (item is null) return Results.NotFound();
    RecordSystemEvent("Reader", "File opened", $"Opened source file for {item.Title}.", "api", item.Id, item.Title);
    return Results.File(item.Path, contentType: item.ContentType, enableRangeProcessing: true);
});

app.MapGet("/api/opds/settings", (HttpRequest request) => Results.Ok(opdsStore.GetClientSettings(DefaultOpdsConnectionUrl(request))));

app.MapPut("/api/opds/settings", (HttpRequest request, OpdsSettingsUpdate payload) =>
{
    var saved = opdsStore.UpdateSettings(payload?.ConnectionUrl, payload?.SelectedKeyId, payload?.Enabled, DefaultOpdsConnectionUrl(request));
    return Results.Ok(saved);
});

app.MapPost("/api/opds/keys", (HttpRequest request, OpdsKeyCreateRequest payload) =>
{
    var saved = opdsStore.CreateKey(payload?.Name, DefaultOpdsConnectionUrl(request));
    return Results.Ok(saved);
});

app.MapPost("/api/opds/keys/{id}/rotate", (HttpRequest request, string id) =>
{
    var saved = opdsStore.RotateKey(id, DefaultOpdsConnectionUrl(request));
    return saved is null ? Results.NotFound(new { error = "Authorization key not found." }) : Results.Ok(saved);
});

app.MapDelete("/api/opds/keys/{id}", (HttpRequest request, string id) =>
{
    var saved = opdsStore.DeleteKey(id, DefaultOpdsConnectionUrl(request));
    return saved is null ? Results.NotFound(new { error = "Authorization key not found." }) : Results.Ok(saved);
});

app.MapGet("/api/settings/opds-status", (HttpRequest request) => Results.Ok(new
{
    status = opdsStore.IsEnabled ? "Available" : "Disabled",
    mode = "opds-authenticated-feed",
    opdsUrl = opdsStore.GetClientSettings(DefaultOpdsConnectionUrl(request)).ConnectionUrl,
    note = "OPDS feeds are now served by Guidevault. Generate an OPDS auth key under Server > OPDS / Auth Keys, then copy the masked OPDS URL into a third-party reader."
}));

app.MapGet("/opds", async (HttpRequest request) => await BuildOpdsRootCatalog(request, cache, opdsStore, deviceStore));
app.MapGet("/opds/v1", async (HttpRequest request) => await BuildOpdsRootCatalog(request, cache, opdsStore, deviceStore));
app.MapGet("/opds/v1/{**rest}", (HttpRequest request, string? rest) => RedirectOpdsV1Request(request, rest));

app.MapGet("/opds/all", async (HttpRequest request) =>
{
    var auth = AuthorizeOpdsRequest(request, opdsStore, deviceStore);
    if (!auth.Success) return auth.Response!;
    var items = (await cache.GetItemsAsync()).OrderBy(i => DisplayItemTitle(i), StringComparer.OrdinalIgnoreCase).ToArray();
    return OpdsXml(OpdsAcquisitionCatalog(request, auth.Secret, "Guidevault - All Items", "All indexed Guidevault items.", items));
});

app.MapGet("/opds/recent", async (HttpRequest request) =>
{
    var auth = AuthorizeOpdsRequest(request, opdsStore, deviceStore);
    if (!auth.Success) return auth.Response!;
    var items = (await cache.GetItemsAsync()).OrderByDescending(i => i.Added).ThenBy(i => DisplayItemTitle(i), StringComparer.OrdinalIgnoreCase).ToArray();
    return OpdsXml(OpdsAcquisitionCatalog(request, auth.Secret, "Guidevault - Recently Added", "The most recently indexed Guidevault items.", items));
});

app.MapGet("/opds/kind/{kind}", async (HttpRequest request, string kind) =>
{
    var auth = AuthorizeOpdsRequest(request, opdsStore, deviceStore);
    if (!auth.Success) return auth.Response!;
    var normalizedKind = Uri.UnescapeDataString(kind ?? string.Empty);
    var items = (await cache.GetItemsAsync())
        .Where(i => KindEquals(i, normalizedKind))
        .ToArray();

    if (ShouldOpdsGroupKind(normalizedKind))
    {
        var entries = items
            .GroupBy(i => OpdsKindGroupBucket(i, normalizedKind), StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => OpdsGroupSortKey(g.Key, normalizedKind), StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => OpdsNavEntry(
                g.Key,
                $"{g.Count()} item(s).",
                $"/opds/kind/{Uri.EscapeDataString(normalizedKind)}/{Uri.EscapeDataString(g.Key)}",
                "navigation"))
            .ToArray();

        return OpdsXml(OpdsNavigationCatalog(
            request,
            auth.Secret,
            $"Guidevault - {OpdsKindPluralTitle(normalizedKind)}",
            $"Guidevault - {OpdsKindPluralTitle(normalizedKind)}",
            OpdsKindGroupedDescription(normalizedKind),
            entries),
            "navigation");
    }

    var ordered = items
        .OrderBy(i => DisplayItemTitle(i), StringComparer.OrdinalIgnoreCase)
        .ToArray();
    return OpdsXml(OpdsAcquisitionCatalog(request, auth.Secret, $"Guidevault - {normalizedKind}", $"Items marked as {normalizedKind}.", ordered));
});

app.MapGet("/opds/kind/{kind}/{group}", async (HttpRequest request, string kind, string group) =>
{
    var auth = AuthorizeOpdsRequest(request, opdsStore, deviceStore);
    if (!auth.Success) return auth.Response!;
    var normalizedKind = Uri.UnescapeDataString(kind ?? string.Empty);
    var decodedGroup = Uri.UnescapeDataString(group ?? string.Empty);
    var items = (await cache.GetItemsAsync())
        .Where(i => KindEquals(i, normalizedKind))
        .Where(i => string.Equals(OpdsKindGroupBucket(i, normalizedKind), decodedGroup, StringComparison.OrdinalIgnoreCase))
        .OrderBy(i => OpdsKindItemSortValue(i, normalizedKind))
        .ThenBy(i => DisplayItemTitle(i), StringComparer.OrdinalIgnoreCase)
        .ToArray();

    return OpdsXml(OpdsAcquisitionCatalog(
        request,
        auth.Secret,
        $"Guidevault - {decodedGroup}",
        $"{OpdsKindPluralTitle(normalizedKind)} in {decodedGroup}.",
        items));
});

app.MapGet("/opds/categories", async (HttpRequest request) =>
{
    var auth = AuthorizeOpdsRequest(request, opdsStore, deviceStore);
    if (!auth.Success) return auth.Response!;
    var items = await cache.GetItemsAsync();
    var entries = items
        .SelectMany(item => CategoryBuckets(item).Select(category => new { Category = category, Item = item }))
        .GroupBy(x => x.Category, StringComparer.OrdinalIgnoreCase)
        .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
        .Select(g => OpdsNavEntry(g.Key, $"{g.Select(x => x.Item.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()} item(s).", $"/opds/category/{Uri.EscapeDataString(g.Key)}"))
        .ToArray();
    return OpdsXml(OpdsNavigationCatalog(request, auth.Secret, "Guidevault Categories", "Guidevault - Categories", "Browse your library by preferred or associated platform.", entries), "navigation");
});

app.MapGet("/opds/category/{category}", async (HttpRequest request, string category) =>
{
    var auth = AuthorizeOpdsRequest(request, opdsStore, deviceStore);
    if (!auth.Success) return auth.Response!;
    var decoded = Uri.UnescapeDataString(category ?? string.Empty);
    var items = (await cache.GetItemsAsync())
        .Where(i => CategoryBuckets(i).Any(bucket => string.Equals(bucket, decoded, StringComparison.OrdinalIgnoreCase)))
        .OrderBy(i => DisplayItemTitle(i), StringComparer.OrdinalIgnoreCase)
        .ToArray();
    return OpdsXml(OpdsAcquisitionCatalog(request, auth.Secret, $"Guidevault - {decoded}", $"Items in {decoded}.", items));
});

app.MapGet("/opds/series", async (HttpRequest request) =>
{
    var auth = AuthorizeOpdsRequest(request, opdsStore, deviceStore);
    if (!auth.Success) return auth.Response!;
    var items = await cache.GetItemsAsync();
    var entries = items
        .Where(i => !string.IsNullOrWhiteSpace(i.Series))
        .GroupBy(i => i.Series.Trim(), StringComparer.OrdinalIgnoreCase)
        .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
        .Select(g => OpdsNavEntry(g.Key, $"{g.Count()} item(s).", $"/opds/series/{Uri.EscapeDataString(g.Key)}"))
        .ToArray();
    return OpdsXml(OpdsNavigationCatalog(request, auth.Secret, "Guidevault Series", "Guidevault - Series", "Browse magazines, guides, and manuals by series.", entries), "navigation");
});

app.MapGet("/opds/series/{series}", async (HttpRequest request, string series) =>
{
    var auth = AuthorizeOpdsRequest(request, opdsStore, deviceStore);
    if (!auth.Success) return auth.Response!;
    var decoded = Uri.UnescapeDataString(series ?? string.Empty);
    var items = (await cache.GetItemsAsync())
        .Where(i => string.Equals((i.Series ?? string.Empty).Trim(), decoded, StringComparison.OrdinalIgnoreCase))
        .OrderBy(i => IssueSortValue(i))
        .ThenBy(i => DisplayItemTitle(i), StringComparer.OrdinalIgnoreCase)
        .ToArray();
    return OpdsXml(OpdsAcquisitionCatalog(request, auth.Secret, $"Guidevault - {decoded}", $"Items in the {decoded} series.", items));
});

app.MapGet("/opds/search", async (HttpRequest request) =>
{
    var auth = AuthorizeOpdsRequest(request, opdsStore, deviceStore);
    if (!auth.Success) return auth.Response!;
    var query = request.Query.TryGetValue("q", out var q) ? q.ToString().Trim() : string.Empty;
    var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var items = (await cache.GetItemsAsync())
        .Where(i => terms.Length == 0 || terms.All(t => OpdsSearchText(i).Contains(t, StringComparison.OrdinalIgnoreCase)))
        .OrderBy(i => DisplayItemTitle(i), StringComparer.OrdinalIgnoreCase)
        .ToArray();
    return OpdsXml(OpdsAcquisitionCatalog(request, auth.Secret, $"Guidevault Search - {query}", string.IsNullOrWhiteSpace(query) ? "Search Guidevault." : $"Search results for {query}.", items));
});

app.MapGet("/opds/items/{id}", async (HttpRequest request, string id) =>
{
    var auth = AuthorizeOpdsRequest(request, opdsStore, deviceStore);
    if (!auth.Success) return auth.Response!;
    var item = cache.TryGetCachedItem(id) ?? (await cache.GetItemsAsync()).FirstOrDefault(i => i.Id == id);
    if (item is null) return Results.NotFound(new { error = "Item not found." });
    return OpdsXml(OpdsItemDetailsCatalog(request, auth.Secret, item));
});

app.MapGet("/opds/items/{id}/details", async (HttpRequest request, string id) =>
{
    var auth = AuthorizeOpdsRequest(request, opdsStore, deviceStore);
    if (!auth.Success) return auth.Response!;
    var item = cache.TryGetCachedItem(id) ?? (await cache.GetItemsAsync()).FirstOrDefault(i => i.Id == id);
    if (item is null) return Results.NotFound(new { error = "Item not found." });
    return OpdsHtmlItemDetails(request, auth.Secret, item);
});

app.MapGet("/opds/items/{id}/download", async (HttpRequest request, string id) =>
{
    var auth = AuthorizeOpdsRequest(request, opdsStore, deviceStore);
    if (!auth.Success) return auth.Response!;
    var item = cache.TryGetCachedItem(id) ?? (await cache.GetItemsAsync()).FirstOrDefault(i => i.Id == id);
    if (item is null) return Results.NotFound(new { error = "Item not found." });
    return Results.File(item.Path, contentType: item.ContentType, fileDownloadName: item.FileName, enableRangeProcessing: true);
});

app.MapGet("/opds/items/{id}/cover", async (HttpRequest request, string id) =>
{
    var auth = AuthorizeOpdsRequest(request, opdsStore, deviceStore);
    if (!auth.Success) return auth.Response!;
    var item = cache.TryGetCachedItem(id) ?? (await cache.GetItemsAsync()).FirstOrDefault(i => i.Id == id);
    if (item is null) return Results.NotFound(new { error = "Item not found." });
    if (item.Format == "PDF") return Results.Redirect("/assets/pdf-cover.svg");

    var coverOverride = coverOverrideStore.Get(item.Id);
    var image = await ArchiveReader.GetCachedCoverImageAsync(item.Path, coverOverride?.EntryKey);
    if (image is null)
    {
        if (GuidevaultLibraryIoGate.IsBusy) return Results.StatusCode(503);
        image = await ArchiveReader.GetCoverImageAsync(item.Path, coverOverride?.EntryKey);
    }
    return image is null
        ? Results.Redirect("/assets/missing-cover.svg")
        : Results.File(image.Value.Bytes, image.Value.ContentType, enableRangeProcessing: true);
});

app.MapFallbackToFile("index.html");
app.Run();


static object BuildLibraryInitialPayload(IReadOnlyList<LibraryItem> source, int? requestedLimit)
{
    var limit = Math.Clamp(requestedLimit ?? 320, 60, 800);
    var items = source ?? Array.Empty<LibraryItem>();
    var selected = new Dictionary<string, LibraryItem>(StringComparer.OrdinalIgnoreCase);
    var totalCount = 0;
    var manualCount = 0;
    var strategyGuideCount = 0;
    var magazineCount = 0;
    var manuals = new List<LibraryItem>();
    var guides = new List<LibraryItem>();
    var magazines = new List<LibraryItem>();
    var unsortedGuides = new List<LibraryItem>();
    var multiPlatformGuides = new List<LibraryItem>();
    var largest = new List<LibraryItem>();

    foreach (var item in items)
    {
        if (item is null) continue;
        totalCount++;
        largest.Add(item);
        if (KindEquals(item, "Manual"))
        {
            manualCount++;
            manuals.Add(item);
        }
        else if (KindEquals(item, "Strategy Guide"))
        {
            strategyGuideCount++;
            guides.Add(item);
            if (string.IsNullOrWhiteSpace(CategoryOrSystem(item))) unsortedGuides.Add(item);
            if ((item.AssociatedPlatforms?.Distinct(StringComparer.OrdinalIgnoreCase).Count() ?? 0) > 1) multiPlatformGuides.Add(item);
        }
        else if (KindEquals(item, "Magazine"))
        {
            magazineCount++;
            magazines.Add(item);
        }
    }

    void AddItems(IEnumerable<LibraryItem> candidates, int take)
    {
        foreach (var item in candidates.Take(take))
        {
            if (item is null || string.IsNullOrWhiteSpace(item.Id)) continue;
            if (!selected.ContainsKey(item.Id)) selected[item.Id] = item;
            if (selected.Count >= limit) return;
        }
    }

    static DateTimeOffset RecentStamp(LibraryItem item) => item.Modified > DateTimeOffset.MinValue ? item.Modified : item.Added;
    static int CompareTitle(LibraryItem a, LibraryItem b) => StringComparer.OrdinalIgnoreCase.Compare(DisplayItemTitle(a), DisplayItemTitle(b));
    static int CompareRecent(LibraryItem a, LibraryItem b)
    {
        var recent = RecentStamp(b).CompareTo(RecentStamp(a));
        return recent != 0 ? recent : CompareTitle(a, b);
    }
    static int CompareIssue(LibraryItem a, LibraryItem b)
    {
        var issue = IssueSortValue(a).CompareTo(IssueSortValue(b));
        return issue != 0 ? issue : CompareTitle(a, b);
    }
    static int CompareSize(LibraryItem a, LibraryItem b)
    {
        var size = b.SizeBytes.CompareTo(a.SizeBytes);
        return size != 0 ? size : CompareTitle(a, b);
    }

    AddItems(TakeBestLibraryItems(items, 96, CompareRecent), 96);
    AddItems(TakeBestLibraryItems(manuals, 72, CompareTitle), 72);
    AddItems(TakeBestLibraryItems(guides, 72, CompareTitle), 72);
    AddItems(TakeBestLibraryItems(magazines, 72, CompareIssue), 72);
    AddItems(TakeBestLibraryItems(unsortedGuides, 48, CompareTitle), 48);
    AddItems(TakeBestLibraryItems(multiPlatformGuides, 48, CompareTitle), 48);
    AddItems(TakeBestLibraryItems(largest, 48, CompareSize), 48);

    return new
    {
        items = selected.Values.ToArray(),
        isPartial = selected.Count < totalCount,
        totalCount,
        counts = BuildLibraryCountsPayload(totalCount, manualCount, strategyGuideCount, magazineCount),
        generatedAt = DateTimeOffset.UtcNow,
        message = selected.Count < totalCount
            ? $"Fast startup loaded {selected.Count:n0} of {totalCount:n0} indexed item(s); full library is loading in the background."
            : $"Loaded {totalCount:n0} indexed item(s)."
    };
}

static IReadOnlyList<LibraryItem> TakeBestLibraryItems(IEnumerable<LibraryItem> source, int take, Comparison<LibraryItem> comparison)
{
    var limit = Math.Max(0, take);
    if (limit == 0) return Array.Empty<LibraryItem>();
    var best = new List<LibraryItem>(limit);
    foreach (var item in source ?? Enumerable.Empty<LibraryItem>())
    {
        if (item is null) continue;
        if (best.Count < limit)
        {
            best.Add(item);
            continue;
        }

        var worstIndex = 0;
        for (var i = 1; i < best.Count; i++)
        {
            if (comparison(best[i], best[worstIndex]) > 0) worstIndex = i;
        }

        if (comparison(item, best[worstIndex]) < 0) best[worstIndex] = item;
    }

    best.Sort(comparison);
    return best;
}

static object BuildLibraryCountsPayload(int all, int manual, int strategyGuide, int magazine) => new
{
    all,
    manual,
    strategyGuide,
    magazine
};

static object BuildLibraryCounts(IEnumerable<LibraryItem> source)
{
    var all = 0;
    var manual = 0;
    var strategyGuide = 0;
    var magazine = 0;
    foreach (var item in source ?? Array.Empty<LibraryItem>())
    {
        if (item is null) continue;
        all++;
        if (KindEquals(item, "Manual")) manual++;
        else if (KindEquals(item, "Strategy Guide")) strategyGuide++;
        else if (KindEquals(item, "Magazine")) magazine++;
    }
    return BuildLibraryCountsPayload(all, manual, strategyGuide, magazine);
}

static IReadOnlyList<LibraryItem> BuildCategoryPreviewCoverItems(IReadOnlyList<LibraryItem> source, string? kind, int limit)
{
    var targetKinds = string.IsNullOrWhiteSpace(kind)
        ? new[] { "Manual", "Strategy Guide", "Magazine" }
        : new[] { kind.Trim() };
    var selected = new Dictionary<string, LibraryItem>(StringComparer.OrdinalIgnoreCase);

    foreach (var targetKind in targetKinds)
    {
        var kindItems = (source ?? Array.Empty<LibraryItem>())
            .Where(item => item is not null && KindEquals(item, targetKind) && item.HasReadablePages && !string.Equals(item.Format, "PDF", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (kindItems.Length == 0) continue;

        var groups = kindItems
            .SelectMany(item => CategoryBuckets(item).Select(category => new { Category = category, Item = item }))
            .GroupBy(x => x.Category, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var previewItems = string.Equals(targetKind, "Manual", StringComparison.OrdinalIgnoreCase)
                ? group.Select(x => x.Item).OrderBy(DisplayItemTitle, StringComparer.OrdinalIgnoreCase).Take(4)
                : group.Select(x => x.Item).OrderBy(IssueSortValue).ThenBy(DisplayItemTitle, StringComparer.OrdinalIgnoreCase).Take(4);

            foreach (var item in previewItems)
            {
                if (string.IsNullOrWhiteSpace(item.Id) || selected.ContainsKey(item.Id)) continue;
                selected[item.Id] = item;
                if (selected.Count >= limit) return selected.Values.ToArray();
            }
        }
    }

    return selected.Values.ToArray();
}

static List<LibraryItem> FilterLibraryItems(IEnumerable<LibraryItem> source, string? kind, string? q)
{
    var query = source ?? Array.Empty<LibraryItem>();
    var cleanKind = (kind ?? string.Empty).Trim();
    if (!string.IsNullOrWhiteSpace(cleanKind) && !cleanKind.Equals("All Content", StringComparison.OrdinalIgnoreCase))
        query = query.Where(i => KindEquals(i, cleanKind));

    var term = (q ?? string.Empty).Trim();
    if (!string.IsNullOrWhiteSpace(term))
    {
        query = query.Where(i => LibraryItemSearchText(i).Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    return query.ToList();
}

static List<LibraryItem> SortLibraryItems(IEnumerable<LibraryItem> source, string? sort)
{
    var mode = (sort ?? "recent").Trim().ToLowerInvariant();
    var query = source ?? Array.Empty<LibraryItem>();
    return mode switch
    {
        "title" => query.OrderBy(DisplayItemTitle, StringComparer.OrdinalIgnoreCase).ToList(),
        "issue" or "sequence" => query.OrderBy(i => IssueSortValue(i)).ThenBy(DisplayItemTitle, StringComparer.OrdinalIgnoreCase).ToList(),
        "kind" => query.OrderBy(i => i.Kind ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenBy(CategoryOrSystem, StringComparer.OrdinalIgnoreCase).ThenBy(DisplayItemTitle, StringComparer.OrdinalIgnoreCase).ToList(),
        "category" => query.OrderBy(CategoryOrSystem, StringComparer.OrdinalIgnoreCase).ThenBy(i => IssueSortValue(i)).ThenBy(DisplayItemTitle, StringComparer.OrdinalIgnoreCase).ToList(),
        "largest" or "size" => query.OrderByDescending(i => i.SizeBytes).ThenBy(DisplayItemTitle, StringComparer.OrdinalIgnoreCase).ToList(),
        _ => query.OrderByDescending(i => i.Modified > DateTimeOffset.MinValue ? i.Modified : i.Added).ThenBy(DisplayItemTitle, StringComparer.OrdinalIgnoreCase).ToList()
    };
}

static string LibraryItemSearchText(LibraryItem item) => string.Join(' ', new[]
{
    item.Title, item.FileName, item.RelativePath, item.Kind, item.System, item.Category, item.Publisher,
    item.Year, item.Series, item.MagazineTitle, item.GameTitle, item.Franchise, item.Developer,
    item.GamePublisher, item.Genre, item.Summary, item.Notes,
    string.Join(' ', item.Tags ?? Array.Empty<string>()),
    string.Join(' ', item.AssociatedPlatforms ?? Array.Empty<string>()),
    string.Join(' ', item.FeaturedGames ?? Array.Empty<string>()),
    string.Join(' ', item.FeaturedPlatforms ?? Array.Empty<string>()),
    string.Join(' ', item.CoveredGames ?? Array.Empty<string>()),
    string.Join(' ', item.CoveredPlatforms ?? Array.Empty<string>())
});

static string DefaultOpdsConnectionUrl(HttpRequest request) => BuildAbsoluteUrl(request, "/opds");

static string BuildAbsoluteUrl(HttpRequest request, string pathAndQuery)
{
    var path = pathAndQuery.StartsWith("/", StringComparison.Ordinal) ? pathAndQuery : "/" + pathAndQuery;
    var pathBase = request.PathBase.HasValue ? request.PathBase.Value : string.Empty;
    return $"{request.Scheme}://{request.Host}{pathBase}{path}";
}

static string AppendOpdsAuth(HttpRequest request, string path, string secret)
{
    var adjustedPath = EnsureOpdsPageSizeQuery(request, path);
    var joiner = adjustedPath.Contains('?', StringComparison.Ordinal) ? "&" : "?";
    return BuildAbsoluteUrl(request, $"{adjustedPath}{joiner}auth={Uri.EscapeDataString(secret)}");
}

static string EnsureOpdsPageSizeQuery(HttpRequest request, string path)
{
    if (PathHasOpdsPagingQuery(path)) return path;

    var requestedPageSize = FirstPositiveQueryInt(request, "pageSize")
        ?? FirstPositiveQueryInt(request, "limit")
        ?? FirstPositiveQueryInt(request, "count");
    if (requestedPageSize is null) return path;

    var pageSize = NormalizeOpdsPageSize(requestedPageSize.Value);
    var joiner = path.Contains('?', StringComparison.Ordinal) ? "&" : "?";
    return $"{path}{joiner}pageSize={pageSize}";
}

static bool PathHasOpdsPagingQuery(string path)
{
    var question = path.IndexOf('?');
    if (question < 0 || question >= path.Length - 1) return false;

    var query = path[(question + 1)..];
    foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        var separator = part.IndexOf('=');
        var key = separator >= 0 ? part[..separator] : part;
        key = Uri.UnescapeDataString(key).Trim();
        if (IsOpdsPagingQueryKey(key)) return true;
    }

    return false;
}

static IResult RedirectOpdsV1Request(HttpRequest request, string? rest)
{
    var suffix = string.IsNullOrWhiteSpace(rest) ? string.Empty : "/" + rest.TrimStart('/');
    return Results.Redirect(BuildAbsoluteUrl(request, $"/opds{suffix}{request.QueryString}"), permanent: false);
}

static async Task<IResult> BuildOpdsRootCatalog(HttpRequest request, LibraryCache cache, OpdsSettingsStore opdsStore, DeviceHistoryStore deviceStore)
{
    var auth = AuthorizeOpdsRequest(request, opdsStore, deviceStore);
    if (!auth.Success) return auth.Response!;

    var items = await cache.GetItemsAsync();
    return OpdsXml(OpdsNavigationCatalog(
        request,
        auth.Secret,
        "Guidevault",
        "Guidevault OPDS Catalog",
        "Browse your local Guidevault library through an authenticated OPDS feed.",
        new[]
        {
            OpdsNavEntry("All Items", "Every indexed manual, strategy guide, and magazine.", "/opds/all"),
            OpdsNavEntry("Recently Added", "Newest indexed items first.", "/opds/recent"),
            OpdsNavEntry("Manuals", $"{items.Count(i => KindEquals(i, "Manual"))} manual(s).", "/opds/kind/Manual"),
            OpdsNavEntry("Strategy Guides", $"{items.Count(i => KindEquals(i, "Strategy Guide"))} strategy guide(s).", "/opds/kind/Strategy%20Guide"),
            OpdsNavEntry("Magazines", $"{items.Count(i => KindEquals(i, "Magazine"))} magazine(s).", "/opds/kind/Magazine"),
            OpdsNavEntry("Categories", "Browse by detected category or system.", "/opds/categories"),
            OpdsNavEntry("Series", "Browse by magazine or guide series.", "/opds/series")
        }),
        "navigation");
}

static OpdsAuthResult AuthorizeOpdsRequest(HttpRequest request, OpdsSettingsStore store, DeviceHistoryStore deviceStore)
{
    if (!store.IsEnabled)
        return new OpdsAuthResult(false, string.Empty, null, Results.StatusCode(StatusCodes.Status503ServiceUnavailable));

    var secret = ExtractOpdsSecret(request);
    if (string.IsNullOrWhiteSpace(secret))
    {
        return new OpdsAuthResult(false, string.Empty, null, Results.Unauthorized());
    }

    var key = store.ValidateKey(secret.Trim(), touch: true);
    if (key is null)
        return new OpdsAuthResult(false, secret.Trim(), null, Results.Unauthorized());

    deviceStore.RecordOpdsClient(request, key);
    return new OpdsAuthResult(true, secret.Trim(), key, null);
}

static string ExtractOpdsSecret(HttpRequest request)
{
    foreach (var name in new[] { "auth", "key", "token" })
    {
        if (request.Query.TryGetValue(name, out var values))
        {
            var value = values.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
    }

    var authorization = request.Headers.Authorization.FirstOrDefault();
    if (string.IsNullOrWhiteSpace(authorization)) return string.Empty;

    const string bearerPrefix = "Bearer ";
    if (authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
    {
        return authorization[bearerPrefix.Length..].Trim();
    }

    const string basicPrefix = "Basic ";
    if (authorization.StartsWith(basicPrefix, StringComparison.OrdinalIgnoreCase))
    {
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(authorization[basicPrefix.Length..].Trim()));
            var separator = decoded.IndexOf(':');
            return separator >= 0 ? decoded[(separator + 1)..].Trim() : decoded.Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    return string.Empty;
}

static IResult OpdsXml(XDocument document, string kind = "acquisition")
{
    var profileKind = string.Equals(kind, "navigation", StringComparison.OrdinalIgnoreCase) ? "navigation" : "acquisition";
    return Results.Content(
        document.ToString(SaveOptions.DisableFormatting),
        $"application/atom+xml;profile=opds-catalog;kind={profileKind};charset=utf-8",
        Encoding.UTF8);
}

static XDocument OpdsNavigationCatalog(HttpRequest request, string secret, string id, string title, string subtitle, IEnumerable<OpdsNavigationEntry> entries)
{
    XNamespace atom = "http://www.w3.org/2005/Atom";
    var updated = DateTimeOffset.UtcNow.ToString("O");
    return new XDocument(
        new XElement(atom + "feed",
            new XAttribute(XNamespace.Xmlns + "atom", atom.NamespaceName),
            new XElement(atom + "id", $"urn:guidevault:opds:{id}"),
            new XElement(atom + "title", title),
            new XElement(atom + "updated", updated),
            new XElement(atom + "author", new XElement(atom + "name", "Guidevault")),
            new XElement(atom + "subtitle", subtitle),
            new XElement(atom + "link", new XAttribute("rel", "self"), new XAttribute("type", "application/atom+xml;profile=opds-catalog;kind=navigation"), new XAttribute("href", AppendOpdsAuth(request, request.Path.Value ?? "/opds", secret))),
            new XElement(atom + "link", new XAttribute("rel", "start"), new XAttribute("type", "application/atom+xml;profile=opds-catalog;kind=navigation"), new XAttribute("href", AppendOpdsAuth(request, "/opds", secret))),
            new XElement(atom + "link", new XAttribute("rel", "search"), new XAttribute("type", "application/atom+xml;profile=opds-catalog;kind=acquisition"), new XAttribute("href", AppendOpdsAuth(request, "/opds/search?q={searchTerms}", secret))),
            entries.Select(entry => new XElement(atom + "entry",
                new XElement(atom + "title", entry.Title),
                new XElement(atom + "id", $"urn:guidevault:opds:{entry.Href}"),
                new XElement(atom + "updated", updated),
                new XElement(atom + "content", new XAttribute("type", "text"), entry.Description),
                new XElement(atom + "link", new XAttribute("rel", "subsection"), new XAttribute("type", entry.Kind == "navigation" ? "application/atom+xml;profile=opds-catalog;kind=navigation" : "application/atom+xml;profile=opds-catalog;kind=acquisition"), new XAttribute("href", AppendOpdsAuth(request, entry.Href, secret)))))));
}

static XDocument OpdsAcquisitionCatalog(HttpRequest request, string secret, string title, string subtitle, IEnumerable<LibraryItem> items)
{
    XNamespace atom = "http://www.w3.org/2005/Atom";
    XNamespace dc = "http://purl.org/dc/terms/";
    XNamespace opds = "http://opds-spec.org/2010/catalog";
    XNamespace openSearch = "http://a9.com/-/spec/opensearch/1.1/";
    var allItems = items.ToArray();
    var paging = ResolveOpdsPaging(request, allItems.Length);
    var itemArray = allItems.Skip(paging.Skip).Take(paging.PageSize).ToArray();
    var updated = allItems.Select(i => i.Modified).DefaultIfEmpty(DateTimeOffset.UtcNow).Max().ToString("O");
    var feedPath = request.Path.Value ?? "/opds/all";
    var rangeText = paging.Total == 0
        ? "No matching items."
        : $"Showing {paging.StartIndex}-{paging.EndIndex} of {paging.Total} item(s).";

    return new XDocument(
        new XElement(atom + "feed",
            new XAttribute(XNamespace.Xmlns + "atom", atom.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "dc", dc.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "opds", opds.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "openSearch", openSearch.NamespaceName),
            new XElement(atom + "id", $"urn:guidevault:opds:{feedPath}"),
            new XElement(atom + "title", title),
            new XElement(atom + "updated", updated),
            new XElement(atom + "author", new XElement(atom + "name", "Guidevault")),
            new XElement(atom + "subtitle", $"{subtitle} {rangeText} Page size: {paging.PageSize}."),
            new XElement(openSearch + "totalResults", paging.Total),
            new XElement(openSearch + "itemsPerPage", paging.PageSize),
            new XElement(openSearch + "startIndex", paging.StartIndex),
            new XElement(atom + "link", new XAttribute("rel", "self"), new XAttribute("type", "application/atom+xml;profile=opds-catalog;kind=acquisition"), new XAttribute("href", AppendOpdsAuth(request, BuildOpdsPagedPath(request, paging.Page, paging.PageSize), secret))),
            new XElement(atom + "link", new XAttribute("rel", "start"), new XAttribute("type", "application/atom+xml;profile=opds-catalog;kind=navigation"), new XAttribute("href", AppendOpdsAuth(request, "/opds", secret))),
            new XElement(atom + "link", new XAttribute("rel", "first"), new XAttribute("type", "application/atom+xml;profile=opds-catalog;kind=acquisition"), new XAttribute("href", AppendOpdsAuth(request, BuildOpdsPagedPath(request, 1, paging.PageSize), secret))),
            paging.Page > 1 ? new XElement(atom + "link", new XAttribute("rel", "previous"), new XAttribute("type", "application/atom+xml;profile=opds-catalog;kind=acquisition"), new XAttribute("href", AppendOpdsAuth(request, BuildOpdsPagedPath(request, paging.Page - 1, paging.PageSize), secret))) : null,
            paging.Page < paging.TotalPages ? new XElement(atom + "link", new XAttribute("rel", "next"), new XAttribute("type", "application/atom+xml;profile=opds-catalog;kind=acquisition"), new XAttribute("href", AppendOpdsAuth(request, BuildOpdsPagedPath(request, paging.Page + 1, paging.PageSize), secret))) : null,
            new XElement(atom + "link", new XAttribute("rel", "last"), new XAttribute("type", "application/atom+xml;profile=opds-catalog;kind=acquisition"), new XAttribute("href", AppendOpdsAuth(request, BuildOpdsPagedPath(request, paging.TotalPages, paging.PageSize), secret))),
            OpdsPageSizeLinks(request, secret, paging),
            // OPDS item lists should be terminal acquisition entries, not an extra
            // single-item submenu. Readers can now open/download from the item
            // entry itself instead of navigating through /opds/items/{id} first.
            itemArray.Select(item => OpdsItemEntry(request, secret, item, includeDetailLink: false, includeAcquisitionLinks: true, includeWebLinks: true, fullDetails: true))));
}

static XDocument OpdsItemDetailsCatalog(HttpRequest request, string secret, LibraryItem item)
{
    XNamespace atom = "http://www.w3.org/2005/Atom";
    XNamespace dc = "http://purl.org/dc/terms/";
    XNamespace opds = "http://opds-spec.org/2010/catalog";
    var title = DisplayItemTitle(item);
    var updated = item.Modified.ToString("O");

    return new XDocument(
        new XElement(atom + "feed",
            new XAttribute(XNamespace.Xmlns + "atom", atom.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "dc", dc.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "opds", opds.NamespaceName),
            new XElement(atom + "id", $"urn:guidevault:opds:item:{item.Id}"),
            new XElement(atom + "title", title),
            new XElement(atom + "updated", updated),
            new XElement(atom + "author", new XElement(atom + "name", "Guidevault")),
            new XElement(atom + "subtitle", "Item details. Use the available links to read in Guidevault, open the web detail page, or download the file."),
            new XElement(atom + "link", new XAttribute("rel", "self"), new XAttribute("type", "application/atom+xml;profile=opds-catalog;kind=acquisition"), new XAttribute("href", AppendOpdsAuth(request, $"/opds/items/{Uri.EscapeDataString(item.Id)}", secret))),
            new XElement(atom + "link", new XAttribute("rel", "start"), new XAttribute("type", "application/atom+xml;profile=opds-catalog;kind=navigation"), new XAttribute("href", AppendOpdsAuth(request, "/opds", secret))),
            OpdsItemEntry(request, secret, item, includeDetailLink: false, includeAcquisitionLinks: true, includeWebLinks: true, fullDetails: true)));
}

static XElement OpdsItemEntry(
    HttpRequest request,
    string secret,
    LibraryItem item,
    bool includeDetailLink = true,
    bool includeAcquisitionLinks = true,
    bool includeWebLinks = false,
    bool fullDetails = false)
{
    XNamespace atom = "http://www.w3.org/2005/Atom";
    XNamespace dc = "http://purl.org/dc/terms/";
    var title = DisplayItemTitle(item);
    var summary = string.IsNullOrWhiteSpace(item.Summary) ? OpdsItemDescription(item) : item.Summary.Trim();
    var encodedId = Uri.EscapeDataString(item.Id);
    var detailHref = AppendOpdsAuth(request, $"/opds/items/{encodedId}", secret);
    var htmlDetailHref = AppendOpdsAuth(request, $"/opds/items/{encodedId}/details", secret);
    var downloadHref = AppendOpdsAuth(request, $"/opds/items/{encodedId}/download", secret);
    var coverHref = AppendOpdsAuth(request, $"/opds/items/{encodedId}/cover", secret);
    var webReaderHref = BuildAbsoluteUrl(request, $"/?read={encodedId}");
    var webDetailHref = BuildAbsoluteUrl(request, $"/?detail={encodedId}");
    var contentType = string.IsNullOrWhiteSpace(item.ContentType) ? "application/octet-stream" : item.ContentType;

    return new XElement(atom + "entry",
        new XElement(atom + "title", title),
        new XElement(atom + "id", $"urn:guidevault:item:{item.Id}"),
        new XElement(atom + "updated", item.Modified.ToString("O")),
        new XElement(atom + "author", new XElement(atom + "name", string.IsNullOrWhiteSpace(item.Publisher) ? "Guidevault" : item.Publisher)),
        new XElement(atom + "summary", summary),
        new XElement(atom + "content", new XAttribute("type", "text"), fullDetails ? OpdsItemFullDescription(item) : OpdsItemDescription(item)),
        string.IsNullOrWhiteSpace(item.Year) ? null : new XElement(dc + "issued", item.Year),
        string.IsNullOrWhiteSpace(item.Publisher) ? null : new XElement(dc + "publisher", item.Publisher),
        string.IsNullOrWhiteSpace(item.LanguageTag) ? null : new XElement(dc + "language", item.LanguageTag),
        new XElement(atom + "category", new XAttribute("term", item.Kind), new XAttribute("label", item.Kind)),
        includeDetailLink ? new XElement(atom + "link", new XAttribute("rel", "subsection"), new XAttribute("title", "Details"), new XAttribute("type", "application/atom+xml;profile=opds-catalog;kind=acquisition"), new XAttribute("href", detailHref)) : null,
        includeWebLinks ? new XElement(atom + "link", new XAttribute("rel", "alternate"), new XAttribute("title", "Details page"), new XAttribute("type", "text/html"), new XAttribute("href", htmlDetailHref)) : null,
        includeWebLinks ? new XElement(atom + "link", new XAttribute("rel", "alternate"), new XAttribute("title", "Open in Guidevault"), new XAttribute("type", "text/html"), new XAttribute("href", webDetailHref)) : null,
        includeWebLinks ? new XElement(atom + "link", new XAttribute("rel", "alternate"), new XAttribute("title", "Read in Guidevault"), new XAttribute("type", "text/html"), new XAttribute("href", webReaderHref)) : null,
        includeAcquisitionLinks ? new XElement(atom + "link", new XAttribute("rel", "http://opds-spec.org/acquisition"), new XAttribute("title", "Download"), new XAttribute("type", contentType), new XAttribute("href", downloadHref)) : null,
        new XElement(atom + "link", new XAttribute("rel", "http://opds-spec.org/image"), new XAttribute("type", "image/*"), new XAttribute("href", coverHref)),
        new XElement(atom + "link", new XAttribute("rel", "http://opds-spec.org/image/thumbnail"), new XAttribute("type", "image/*"), new XAttribute("href", coverHref)));
}

static OpdsPaging ResolveOpdsPaging(HttpRequest request, int total)
{
    var requestedPage = FirstPositiveQueryInt(request, "page") ?? 1;
    var requestedPageSize = FirstPositiveQueryInt(request, "pageSize")
        ?? FirstPositiveQueryInt(request, "limit")
        ?? FirstPositiveQueryInt(request, "count")
        ?? 50;
    var pageSize = NormalizeOpdsPageSize(requestedPageSize);
    var totalPages = Math.Max(1, (int)Math.Ceiling(Math.Max(0, total) / (double)pageSize));
    var page = Math.Clamp(requestedPage, 1, totalPages);
    var skip = Math.Max(0, (page - 1) * pageSize);
    var startIndex = total == 0 ? 0 : skip + 1;
    var endIndex = total == 0 ? 0 : Math.Min(total, skip + pageSize);
    return new OpdsPaging(page, pageSize, total, totalPages, skip, startIndex, endIndex);
}

static int NormalizeOpdsPageSize(int value)
{
    if (value <= 20) return 20;
    if (value <= 50) return 50;
    return 100;
}

static int? FirstPositiveQueryInt(HttpRequest request, string name)
{
    if (!request.Query.TryGetValue(name, out var values)) return null;
    foreach (var value in values)
    {
        if (int.TryParse(value, out var parsed) && parsed > 0) return parsed;
    }
    return null;
}

static string BuildOpdsPagedPath(HttpRequest request, int page, int pageSize)
{
    var path = request.Path.Value ?? "/opds/all";
    var query = new List<string>();
    foreach (var pair in request.Query)
    {
        if (IsOpdsAuthQueryKey(pair.Key) || IsOpdsPagingQueryKey(pair.Key)) continue;
        foreach (var value in pair.Value)
        {
            query.Add($"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(value ?? string.Empty)}");
        }
    }

    query.Add($"page={page}");
    query.Add($"pageSize={pageSize}");
    return query.Count == 0 ? path : $"{path}?{string.Join("&", query)}";
}

static bool IsOpdsAuthQueryKey(string key) =>
    string.Equals(key, "auth", StringComparison.OrdinalIgnoreCase)
    || string.Equals(key, "key", StringComparison.OrdinalIgnoreCase)
    || string.Equals(key, "token", StringComparison.OrdinalIgnoreCase);

static bool IsOpdsPagingQueryKey(string key) =>
    string.Equals(key, "page", StringComparison.OrdinalIgnoreCase)
    || string.Equals(key, "pageSize", StringComparison.OrdinalIgnoreCase)
    || string.Equals(key, "limit", StringComparison.OrdinalIgnoreCase)
    || string.Equals(key, "count", StringComparison.OrdinalIgnoreCase);

static IEnumerable<XElement> OpdsPageSizeLinks(HttpRequest request, string secret, OpdsPaging paging)
{
    XNamespace atom = "http://www.w3.org/2005/Atom";
    foreach (var size in new[] { 20, 50, 100 })
    {
        yield return new XElement(atom + "link",
            new XAttribute("rel", "alternate"),
            new XAttribute("title", $"Show {size} items per page"),
            new XAttribute("type", "application/atom+xml;profile=opds-catalog;kind=acquisition"),
            new XAttribute("href", AppendOpdsAuth(request, BuildOpdsPagedPath(request, 1, size), secret)));
    }
}

static IResult OpdsHtmlItemDetails(HttpRequest request, string secret, LibraryItem item)
{
    var encodedId = Uri.EscapeDataString(item.Id);
    var title = DisplayItemTitle(item);
    var coverHref = AppendOpdsAuth(request, $"/opds/items/{encodedId}/cover", secret);
    var downloadHref = AppendOpdsAuth(request, $"/opds/items/{encodedId}/download", secret);
    var readHref = BuildAbsoluteUrl(request, $"/?read={encodedId}");
    var webDetailHref = BuildAbsoluteUrl(request, $"/?detail={encodedId}");
    var rows = string.Join("", OpdsItemDetailRows(item).Select(row => $"<div class=\"gv-opds-row\"><dt>{Html(row.Label)}</dt><dd>{Html(row.Value)}</dd></div>"));
    var summary = Html(string.IsNullOrWhiteSpace(item.Summary) ? OpdsItemDescription(item) : item.Summary.Trim());
    var body = $$$"""
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>{{{Html(title)}}} - Guidevault OPDS Details</title>
  <style>
    :root{color-scheme:dark;background:#08111f;color:#e8eef9;font-family:Inter,Segoe UI,Arial,sans-serif;}
    body{margin:0;min-height:100vh;background:radial-gradient(circle at top left,#1f4472 0,#08111f 42%,#040812 100%);}
    .gv-opds-shell{max-width:1040px;margin:0 auto;padding:32px 20px;}
    .gv-opds-card{display:grid;grid-template-columns:minmax(180px,260px) 1fr;gap:28px;background:rgba(7,16,31,.82);border:1px solid rgba(129,168,216,.28);border-radius:24px;box-shadow:0 24px 80px rgba(0,0,0,.36);padding:24px;}
    .gv-opds-cover{width:100%;border-radius:18px;background:#0d1728;box-shadow:0 18px 48px rgba(0,0,0,.4);}
    h1{margin:0 0 8px;font-size:clamp(1.6rem,4vw,2.6rem);line-height:1.05;}
    .gv-opds-sub{color:#9fb2ca;margin:0 0 18px;}
    .gv-opds-actions{display:flex;flex-wrap:wrap;gap:10px;margin:20px 0;}
    .gv-opds-button{display:inline-flex;align-items:center;justify-content:center;border-radius:999px;padding:10px 16px;text-decoration:none;font-weight:700;background:#2f8df5;color:white;border:1px solid rgba(255,255,255,.16);}
    .gv-opds-button.secondary{background:rgba(255,255,255,.08);color:#e8eef9;}
    dl{display:grid;grid-template-columns:1fr 1fr;gap:10px;margin:18px 0 0;}
    .gv-opds-row{border:1px solid rgba(129,168,216,.18);background:rgba(255,255,255,.045);border-radius:14px;padding:10px 12px;}
    dt{font-size:.73rem;text-transform:uppercase;letter-spacing:.08em;color:#88a5c9;margin-bottom:5px;}dd{margin:0;color:#f5f8ff;}
    @media(max-width:720px){.gv-opds-card{grid-template-columns:1fr;}dl{grid-template-columns:1fr}.gv-opds-cover{max-width:260px}}
  </style>
</head>
<body>
  <main class="gv-opds-shell">
    <section class="gv-opds-card">
      <img class="gv-opds-cover" src="{{{Html(coverHref)}}}" alt="{{{Html(title)}}} cover" />
      <div>
        <h1>{{{Html(title)}}}</h1>
        <p class="gv-opds-sub">{{{summary}}}</p>
        <div class="gv-opds-actions">
          <a class="gv-opds-button" href="{{{Html(readHref)}}}">Read in Guidevault</a>
          <a class="gv-opds-button secondary" href="{{{Html(webDetailHref)}}}">Open Guidevault Details</a>
          <a class="gv-opds-button secondary" href="{{{Html(downloadHref)}}}">Download File</a>
        </div>
        <dl>{{{rows}}}</dl>
      </div>
    </section>
  </main>
</body>
</html>
""";
    return Results.Content(body, "text/html; charset=utf-8", Encoding.UTF8);
}

static string Html(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

static IEnumerable<(string Label, string Value)> OpdsItemDetailRows(LibraryItem item)
{
    void AddIf(List<(string Label, string Value)> rows, string label, string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(text)) rows.Add((label, text));
    }

    var rows = new List<(string Label, string Value)>();
    AddIf(rows, "Type", item.Kind);
    AddIf(rows, "Format", item.Format);
    AddIf(rows, "Preferred Platform", CategoryOrSystem(item));
    AddIf(rows, "Associated Platforms", JoinValues(item.AssociatedPlatforms));
    AddIf(rows, "Series", item.Series);
    AddIf(rows, "Issue", item.IssueNumber);
    AddIf(rows, "Publisher", item.Publisher);
    AddIf(rows, "Year", item.Year);
    AddIf(rows, "Language", item.LanguageTag);
    AddIf(rows, "Region", item.Region);
    AddIf(rows, "Rating", item.Rating);
    AddIf(rows, "ISBN-13", item.Isbn13);
    AddIf(rows, "ISBN-10", item.Isbn10);
    AddIf(rows, "ISSN / Barcode / UPC", item.BarcodeUpcIssn);
    AddIf(rows, "Page Count", item.PageCount > 0 ? item.PageCount.ToString() : string.Empty);
    AddIf(rows, "File", item.FileName);
    AddIf(rows, "Size", FormatOpdsBytes(item.SizeBytes));
    AddIf(rows, "Associated Game", item.GameTitle);
    AddIf(rows, "Guide Type", item.GuideType);
    AddIf(rows, "Edition", item.Edition);
    AddIf(rows, "Featured Games", JoinValues(item.FeaturedGames));
    AddIf(rows, "Featured Platforms", JoinValues(item.FeaturedPlatforms));
    AddIf(rows, "Covered Games", JoinValues(item.CoveredGames ?? Array.Empty<string>()));
    AddIf(rows, "Covered Platforms", JoinValues(item.CoveredPlatforms ?? Array.Empty<string>()));
    return rows;
}

static string OpdsItemFullDescription(LibraryItem item) =>
    string.Join(Environment.NewLine, OpdsItemDetailRows(item).Select(row => $"{row.Label}: {row.Value}"));

static string JoinValues(IEnumerable<string>? values) =>
    string.Join(", ", (values ?? Array.Empty<string>()).Select(v => (v ?? string.Empty).Trim()).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase));

static string FormatOpdsBytes(long bytes)
{
    if (bytes <= 0) return string.Empty;
    string[] units = ["B", "KB", "MB", "GB", "TB"];
    var value = (double)bytes;
    var unit = 0;
    while (value >= 1024 && unit < units.Length - 1)
    {
        value /= 1024;
        unit++;
    }
    return $"{value:0.#} {units[unit]}";
}

static OpdsNavigationEntry OpdsNavEntry(string title, string description, string href, string kind = "acquisition") => new(title, description, href, kind);

static bool KindEquals(LibraryItem item, string kind) => string.Equals(item.Kind ?? string.Empty, kind ?? string.Empty, StringComparison.OrdinalIgnoreCase);

static string CategoryOrSystem(LibraryItem item)
{
    if (!string.IsNullOrWhiteSpace(item.Category) && !IsMultiPlatformBucket(item.Category)) return item.Category.Trim();
    if (!string.IsNullOrWhiteSpace(item.System) && !IsMultiPlatformBucket(item.System)) return item.System.Trim();
    if (!string.IsNullOrWhiteSpace(item.PrimarySystem) && !IsMultiPlatformBucket(item.PrimarySystem)) return item.PrimarySystem.Trim();
    return "Unsorted";
}

static string[] CategoryBuckets(LibraryItem item)
{
    var buckets = new List<string>();
    void Add(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text) || text == "\u2014" || IsMultiPlatformBucket(text)) return;
        if (!buckets.Any(existing => string.Equals(existing, text, StringComparison.OrdinalIgnoreCase))) buckets.Add(text);
    }

    if (string.Equals(item.Kind, "Strategy Guide", StringComparison.OrdinalIgnoreCase))
    {
        foreach (var platform in item.AssociatedPlatforms ?? []) Add(platform);
        if (buckets.Count == 0) Add(CategoryOrSystem(item));
        return buckets.Count > 0 ? buckets.ToArray() : ["Unsorted Strategy Guides"];
    }

    Add(CategoryOrSystem(item));
    return buckets.Count > 0 ? buckets.ToArray() : ["Unsorted"];
}

static bool IsMultiPlatformBucket(string? value) =>
    Regex.IsMatch(value ?? string.Empty, @"^multi[-\s]*platform(?: strategy guides?)?$", RegexOptions.IgnoreCase);

static bool ShouldOpdsGroupKind(string? kind) =>
    string.Equals(kind, "Magazine", StringComparison.OrdinalIgnoreCase)
    || string.Equals(kind, "Manual", StringComparison.OrdinalIgnoreCase);

static string OpdsFirstNonEmpty(params string?[] values) =>
    values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

static string OpdsKindPluralTitle(string? kind)
{
    if (string.Equals(kind, "Magazine", StringComparison.OrdinalIgnoreCase)) return "Magazines";
    if (string.Equals(kind, "Manual", StringComparison.OrdinalIgnoreCase)) return "Manuals";
    if (string.Equals(kind, "Strategy Guide", StringComparison.OrdinalIgnoreCase)) return "Strategy Guides";
    return string.IsNullOrWhiteSpace(kind) ? "Items" : kind.Trim();
}

static string OpdsKindGroupedDescription(string? kind)
{
    if (string.Equals(kind, "Magazine", StringComparison.OrdinalIgnoreCase))
        return "Browse magazine issues by official magazine title before opening an issue list.";
    if (string.Equals(kind, "Manual", StringComparison.OrdinalIgnoreCase))
        return "Browse manuals by system or platform before opening a manual list.";
    return "Browse grouped Guidevault items.";
}

static string OpdsKindGroupBucket(LibraryItem item, string? kind)
{
    if (string.Equals(kind, "Magazine", StringComparison.OrdinalIgnoreCase))
    {
        var title = OpdsFirstNonEmpty(item.MagazineTitle, item.Series);
        if (!string.IsNullOrWhiteSpace(title)) return title.Trim();
        return "Unsorted Magazines";
    }

    if (string.Equals(kind, "Manual", StringComparison.OrdinalIgnoreCase))
    {
        foreach (var platform in item.AssociatedPlatforms ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(platform) && !IsMultiPlatformBucket(platform)) return platform.Trim();
        }

        var system = OpdsFirstNonEmpty(item.PrimarySystem, item.Category, item.System);
        if (!string.IsNullOrWhiteSpace(system) && !IsMultiPlatformBucket(system)) return system.Trim();
        return "Unsorted Manuals";
    }

    return CategoryOrSystem(item);
}

static string OpdsGroupSortKey(string group, string? kind)
{
    if (group.StartsWith("Unsorted", StringComparison.OrdinalIgnoreCase)) return "zzzzzz " + group;
    return group;
}

static double OpdsKindItemSortValue(LibraryItem item, string? kind)
{
    if (string.Equals(kind, "Magazine", StringComparison.OrdinalIgnoreCase)) return IssueSortValue(item);
    return 0;
}

static string DisplayItemTitle(LibraryItem item)
{
    if (string.Equals(item.Kind, "Magazine", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(item.IssueNumber))
    {
        var publicationTitle = OpdsFirstNonEmpty(item.MagazineTitle, item.Series);
        if (!string.IsNullOrWhiteSpace(publicationTitle)) return $"{publicationTitle.Trim()} #{item.IssueNumber.Trim()}";
    }

    return string.IsNullOrWhiteSpace(item.Title) ? item.FileName : item.Title.Trim();
}

static string OpdsItemDescription(LibraryItem item)
{
    var parts = new[]
    {
        item.Kind,
        item.Format,
        CategoryOrSystem(item),
        item.Series,
        item.Publisher,
        item.Year,
        item.PageCount > 0 ? $"{item.PageCount} page(s)" : string.Empty
    }.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase);
    return string.Join(" \u2022 ", parts);
}

static string OpdsSearchText(LibraryItem item) => string.Join(" ", new[]
{
    item.Title,
    item.FileName,
    item.Kind,
    item.Format,
    item.System,
    item.Category,
    item.Series,
    item.Publisher,
    item.Year,
    item.Summary,
    item.GameTitle,
    item.Franchise,
    item.MagazineTitle,
    item.PrimarySystem,
    item.PlatformFocus,
    string.Join(" ", item.Tags ?? Array.Empty<string>()),
    string.Join(" ", item.FeaturedGames ?? Array.Empty<string>()),
    string.Join(" ", item.CoveredGames ?? Array.Empty<string>())
});

static double IssueSortValue(LibraryItem item)
{
    var raw = item.IssueNumber ?? string.Empty;
    var match = Regex.Match(raw, @"\d+(\.\d+)?");
    return match.Success && double.TryParse(match.Value, out var value) ? value : double.MaxValue;
}

static string? BlankToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();


static string DefaultBrowsePath(string contentRoot)
{
    if (Directory.Exists("/app/data/library")) return "/app/data/library";
    if (Directory.Exists("/app/data")) return "/app/data";
    var localData = Path.Combine(contentRoot, "data", "library");
    if (Directory.Exists(localData)) return localData;
    return contentRoot;
}

static IEnumerable<BrowseRoot> BrowseRoots(string contentRoot)
{
    var candidates = new List<BrowseRoot>
    {
        new("Library", "/app/data/library"),
        new("Guidevault data", "/app/data"),
        new("Data", "/data"),
        new("Library mount", "/library"),
        new("Libraries", "/libraries"),
        new("Mounted volumes", "/mnt"),
        new("Media", "/media"),
        new("Local app data", Path.Combine(contentRoot, "data", "library")),
        new("App root", contentRoot)
    };

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        candidates.AddRange(Directory.GetLogicalDrives().Select(drive => new BrowseRoot(drive.TrimEnd('\\'), drive)));
    }
    else
    {
        candidates.Add(new BrowseRoot("Filesystem root", "/"));
    }

    return candidates
        .Where(root => !string.IsNullOrWhiteSpace(root.Path) && Directory.Exists(root.Path))
        .GroupBy(root => Path.GetFullPath(root.Path), StringComparer.OrdinalIgnoreCase)
        .Select(g => g.First());
}

static string ResolvePath(string contentRoot, string path)
{
    var envPath = Environment.GetEnvironmentVariable("GUIDEVAULT_LIBRARY_PATH");
    if (!string.IsNullOrWhiteSpace(envPath)) return Path.GetFullPath(envPath);
    if (Path.IsPathRooted(path)) return Path.GetFullPath(path);
    return Path.GetFullPath(Path.Combine(contentRoot, path));
}


static List<FileOrganizationPlan> BuildFileOrganizationPlans(IEnumerable<string> ids, JsonElement payload, IReadOnlyList<LibraryItem> snapshot, MetadataStore metadataStore, IEnumerable<string> libraryPaths, bool apply)
{
    var idSet = ids.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var itemMap = snapshot
        .Where(item => !string.IsNullOrWhiteSpace(item.Id))
        .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    var plans = new List<FileOrganizationPlan>();

    foreach (var id in idSet)
    {
        if (!itemMap.TryGetValue(id, out var sourceItem))
        {
            plans.Add(new FileOrganizationPlan(id, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, "Missing", "Item was not found in the active library cache."));
            continue;
        }

        var item = metadataStore.ApplyOverride(sourceItem);
        var currentPath = string.IsNullOrWhiteSpace(item.Path) ? string.Empty : Path.GetFullPath(item.Path);
        if (string.IsNullOrWhiteSpace(currentPath) || !File.Exists(currentPath))
        {
            plans.Add(new FileOrganizationPlan(item.Id, item.Kind, item.Title, item.FileName, currentPath, string.Empty, item.RelativePath, "Missing", "Source file could not be found."));
            continue;
        }

        if (!TryResolveLibraryRoot(currentPath, libraryPaths, out var libraryRoot))
        {
            plans.Add(new FileOrganizationPlan(item.Id, item.Kind, item.Title, item.FileName, currentPath, string.Empty, item.RelativePath, "Blocked", "Unable to resolve the current library root for this file."));
            continue;
        }

        var template = FileOrganizationTemplateForKind(payload, item.Kind);
        var relativePath = BuildOrganizationRelativePath(item, template);
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            plans.Add(new FileOrganizationPlan(item.Id, item.Kind, item.Title, item.FileName, currentPath, string.Empty, item.RelativePath, "Blocked", "Template produced an empty path."));
            continue;
        }

        var destinationPath = Path.GetFullPath(Path.Combine(libraryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsPathInsideRoot(destinationPath, libraryRoot))
        {
            plans.Add(new FileOrganizationPlan(item.Id, item.Kind, item.Title, item.FileName, currentPath, destinationPath, relativePath, "Blocked", "Template tried to move the file outside its current library root."));
            continue;
        }

        var proposedFileName = Path.GetFileName(destinationPath);
        if (GuidevaultNativeMetadata.IsLikelyDosShortFileName(proposedFileName))
        {
            plans.Add(new FileOrganizationPlan(item.Id, item.Kind, item.Title, item.FileName, currentPath, destinationPath, relativePath, "Blocked", "Proposed filename looks like a Windows 8.3 short-name alias. Adjust the template or metadata before applying."));
            continue;
        }

        if (string.Equals(currentPath, destinationPath, StringComparison.OrdinalIgnoreCase))
        {
            plans.Add(new FileOrganizationPlan(item.Id, item.Kind, item.Title, item.FileName, currentPath, destinationPath, relativePath, "Unchanged", "File already matches this organization template."));
            continue;
        }

        if (File.Exists(destinationPath))
        {
            plans.Add(new FileOrganizationPlan(item.Id, item.Kind, item.Title, item.FileName, currentPath, destinationPath, relativePath, "Conflict", "A file already exists at the proposed destination."));
            continue;
        }

        plans.Add(new FileOrganizationPlan(item.Id, item.Kind, item.Title, item.FileName, currentPath, destinationPath, relativePath, "Ready", apply ? "Ready to apply." : "Ready to move/rename."));
    }

    return plans;
}

static string FileOrganizationTemplateForKind(JsonElement payload, string kind)
{
    var defaults = kind.Equals("Manual", StringComparison.OrdinalIgnoreCase)
        ? "Manuals/{Platform}/{GameTitle}/{Title} - Manual{Extension}"
        : kind.Equals("Magazine", StringComparison.OrdinalIgnoreCase)
            ? "Magazines/{MagazineSeries}/{Year}/{MagazineSeries} - {IssuePart}{Extension}"
            : "Strategy Guides/{Platform}/{GameTitle}/{Title}{Extension}";

    if (payload.ValueKind != JsonValueKind.Object) return defaults;
    if (!TryGetJsonProperty(payload, "templates", out var templates) || templates.ValueKind != JsonValueKind.Object) return defaults;

    var key = kind.Equals("Manual", StringComparison.OrdinalIgnoreCase)
        ? "manual"
        : kind.Equals("Magazine", StringComparison.OrdinalIgnoreCase)
            ? "magazine"
            : "strategyGuide";

    if (TryGetJsonProperty(templates, key, out var value) && value.ValueKind == JsonValueKind.String)
    {
        var text = value.GetString();
        if (!string.IsNullOrWhiteSpace(text)) return text!;
    }

    return defaults;
}

static string BuildOrganizationRelativePath(LibraryItem item, string template)
{
    var ext = NormalizeOrganizationExtensionToken(Path.GetExtension(item.FileName));
    if (string.IsNullOrWhiteSpace(ext)) ext = NormalizeOrganizationExtensionToken(Path.GetExtension(item.Path));

    var cleanTitle = CleanOrganizationTitle(FirstNonBlank(item.Title, item.ManualTitle, item.MagazineTitle, item.GameTitle, Path.GetFileNameWithoutExtension(item.FileName)), item);
    var cleanManualTitle = CleanOrganizationTitle(FirstNonBlank(item.ManualTitle, item.Title, item.GameTitle), item);
    var cleanStrategyTitle = CleanOrganizationTitle(FirstNonBlank(item.Title, item.GameTitle, Path.GetFileNameWithoutExtension(item.FileName)), item);
    var cleanMagazineTitle = CleanOrganizationTitle(FirstNonBlank(item.MagazineTitle, item.Series, item.Title), item);

    var issueNumber = FirstMeaningfulOrganizationValue(item.IssueNumber);
    var volumeNumber = FirstMeaningfulOrganizationValue(item.Volume);
    var edition = FirstMeaningfulOrganizationValue(item.Edition);
    var guideType = FirstMeaningfulOrganizationValue(item.GuideType);

    var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["ContentType"] = item.Kind.Equals("Manual", StringComparison.OrdinalIgnoreCase) ? "Manuals" : item.Kind.Equals("Magazine", StringComparison.OrdinalIgnoreCase) ? "Magazines" : "Strategy Guides",
        ["Kind"] = item.Kind,
        ["Title"] = cleanTitle,
        ["GameTitle"] = FirstNonBlank(item.GameTitle, cleanTitle, cleanManualTitle),
        ["Platform"] = FirstNonBlank(item.Category, item.System, item.PrimarySystem, item.AssociatedPlatforms?.FirstOrDefault() ?? string.Empty, "Unsorted"),
        ["PreferredPlatform"] = FirstNonBlank(item.Category, item.System, item.PrimarySystem, "Unsorted"),
        ["MagazineSeries"] = FirstNonBlank(cleanMagazineTitle, item.Series, "Unsorted Magazines"),
        ["Series"] = FirstNonBlank(item.Series, item.Franchise),
        ["IssueNumber"] = issueNumber,
        ["IssuePart"] = LabeledOrganizationValue(issueNumber, "Issue "),
        ["Number"] = issueNumber,
        ["NumberPart"] = LabeledOrganizationValue(issueNumber, "No. "),
        ["Volume"] = volumeNumber,
        ["VolumeNumber"] = volumeNumber,
        ["VolumePart"] = LabeledOrganizationValue(volumeNumber, "Vol. "),
        ["Edition"] = edition,
        ["EditionPart"] = edition,
        ["GuideType"] = guideType,
        ["GuideTypePart"] = guideType,
        ["CoveredGames"] = string.Join(", ", item.CoveredGames ?? Array.Empty<string>()),
        ["Year"] = FirstMeaningfulOrganizationValue(item.Year, ExtractYear(item.PublicationDate), ExtractYear(item.CoverDate), ExtractYear(item.GameReleaseYear)),
        ["Month"] = FirstMeaningfulOrganizationValue(ExtractMonth(item.CoverDate), ExtractMonth(item.PublicationDate)),
        ["PublicationDate"] = FirstMeaningfulOrganizationValue(item.PublicationDate, item.CoverDate, item.Year),
        ["Publisher"] = FirstMeaningfulOrganizationValue(item.Publisher),
        ["Writer"] = FirstMeaningfulOrganizationValue(item.Writer),
        ["ISBN"] = FirstMeaningfulOrganizationValue(item.Isbn13, item.Isbn10),
        ["ISBN10"] = FirstMeaningfulOrganizationValue(item.Isbn10),
        ["ISBN13"] = FirstMeaningfulOrganizationValue(item.Isbn13),
        ["ASIN"] = FirstMeaningfulOrganizationValue(item.Asin),
        ["ManualTitle"] = cleanManualTitle,
        ["ManualType"] = FirstMeaningfulOrganizationValue(item.ManualType),
        ["StrategyGuideTitle"] = cleanStrategyTitle,
        ["MagazineTitle"] = cleanMagazineTitle,
        ["CoverDate"] = FirstMeaningfulOrganizationValue(item.CoverDate),
        ["Region"] = item.Region ?? string.Empty,
        ["Language"] = item.LanguageTag ?? string.Empty,
        ["Extension"] = ext
    };

    var rendered = Regex.Replace(template ?? string.Empty, "\\{([A-Za-z0-9_]+)\\}", match =>
    {
        var tokenName = match.Groups[1].Value;
        if (!values.TryGetValue(tokenName, out var value)) return string.Empty;
        return tokenName.Equals("Extension", StringComparison.OrdinalIgnoreCase)
            ? NormalizeOrganizationExtensionToken(value)
            : NormalizeOrganizationTokenValue(value);
    });
    rendered = CleanupOrganizationRenderedTemplate(rendered.Replace('\\', '/'));
    var parts = rendered.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(SanitizePathPart)
        .Where(part => !string.IsNullOrWhiteSpace(part))
        .ToList();

    if (parts.Count == 0) return string.Empty;
    var fileName = EnsureOrganizationFileExtension(parts[^1], ext);
    parts[^1] = SanitizeFileName(fileName, ext);
    return string.Join('/', parts);
}

static string CleanOrganizationTitle(string? value, LibraryItem item)
{
    var text = value ?? string.Empty;
    if (string.IsNullOrWhiteSpace(text)) return string.Empty;

    text = Path.GetFileNameWithoutExtension(text.Trim());
    text = NormalizeOrganizationTokenValue(text);
    text = StripGeneratedOrganizationSuffixes(text);

    if (string.IsNullOrWhiteSpace(text))
    {
        var fallback = FirstMeaningfulOrganizationValue(item.GameTitle, item.ManualTitle, item.MagazineTitle, item.Series, Path.GetFileNameWithoutExtension(item.FileName));
        return StripGeneratedOrganizationSuffixes(NormalizeOrganizationTokenValue(fallback));
    }

    return text;
}

static string StripGeneratedOrganizationSuffixes(string? value)
{
    var text = value ?? string.Empty;
    if (string.IsNullOrWhiteSpace(text)) return string.Empty;

    for (var i = 0; i < 8; i++)
    {
        var before = text;
        text = Regex.Replace(text, @"\s+-\s+(?:Manuals?|Strategy\s+Guides?|Magazines?)(?:\s*-+\s*(?:[0-9Xx-]{10,17}|(?:19|20)\d{2}|Unknown|Unsorted|N/?A|None))*\s*$", string.Empty, RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\s+-+\s*(?:[0-9Xx-]{10,17}|(?:19|20)\d{2}|Unknown|Unsorted|N/?A|None)\s*$", string.Empty, RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\s+-\s+-\s*", " - ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\s+--\s+", " - ", RegexOptions.IgnoreCase);
        text = text.Trim(' ', '.', '-');
        if (string.Equals(before, text, StringComparison.Ordinal)) break;
    }

    return text;
}

static string CleanupOrganizationRenderedTemplate(string value)
{
    var text = value ?? string.Empty;
    text = Regex.Replace(text, @"\s+", " ");
    text = Regex.Replace(text, @"\s+-\s*(?:-\s*)+", " - ");
    text = Regex.Replace(text, @"\b(?:Issue|No\.?|Number|Vol\.?|Volume|Edition)\s*(?=(?:/|$|\.[A-Za-z0-9]{1,8}|\s+-\s))", string.Empty, RegexOptions.IgnoreCase);
    text = Regex.Replace(text, @"\s+-\s*(?=(?:/|$|\.[A-Za-z0-9]{1,8}))", string.Empty);
    text = Regex.Replace(text, @"(?:\s+-\s*)+(?=(?:\.[A-Za-z0-9]{1,8})?$)", string.Empty);
    text = Regex.Replace(text, @"/\s+-\s*", "/");
    text = Regex.Replace(text, @"\s+-\s*/", "/");
    text = Regex.Replace(text, @"/{2,}", "/");
    return text.Trim(' ', '/', '-');
}

static bool IsPlaceholderOrganizationValue(string? value)
{
    var text = (value ?? string.Empty).Trim();
    if (string.IsNullOrWhiteSpace(text)) return true;
    var normalized = Regex.Replace(text, @"[\s._-]+", string.Empty).ToUpperInvariant();
    return normalized is "UNKNOWN" or "UNSORTED" or "NA" or "N/A" or "NONE" or "NULL" or "TBD";
}

static string FirstMeaningfulOrganizationValue(params string?[] values)
{
    foreach (var value in values)
    {
        if (!IsPlaceholderOrganizationValue(value)) return value!.Trim();
    }
    return string.Empty;
}


static string LabeledOrganizationValue(string? value, string label)
{
    var meaningful = FirstMeaningfulOrganizationValue(value);
    return string.IsNullOrWhiteSpace(meaningful) ? string.Empty : $"{label}{meaningful}";
}

static string NormalizeOrganizationTokenValue(string? value)
{
    var text = value ?? string.Empty;
    if (string.IsNullOrWhiteSpace(text)) return string.Empty;

    // GuideVault often runs inside a Linux container while organizing files on Windows bind mounts.
    // Use a Windows-safe character set here instead of the container OS invalid-character list so
    // characters like ':' cannot create invalid or host-mangled filenames on Windows-backed volumes.
    text = Regex.Replace(text, @"[\\/:*?""<>|]+", " - ");
    text = Regex.Replace(text, "[\x00-\x1F]+", " ");
    text = Regex.Replace(text, @"\s+", " ").Trim();
    text = Regex.Replace(text, @"\s*-\s*(?:-\s*)+", " - ");
    return text.Trim(' ', '.', '-');
}

static string NormalizeOrganizationExtensionToken(string? value)
{
    var ext = (value ?? string.Empty).Trim();
    if (string.IsNullOrWhiteSpace(ext)) return string.Empty;
    ext = Regex.Replace(ext, @"[^A-Za-z0-9.]+", string.Empty);
    if (string.IsNullOrWhiteSpace(ext)) return string.Empty;
    ext = ext.TrimStart('.');
    return string.IsNullOrWhiteSpace(ext) ? string.Empty : $".{ext}";
}

static string SanitizePathPart(string value)
{
    var cleaned = NormalizeOrganizationTokenValue(value);
    cleaned = Regex.Replace(cleaned, @"\s+-\s+-\s+", " - ");
    cleaned = Regex.Replace(cleaned, @"(\s+-\s*)+(?=\.[A-Za-z0-9]{1,8}$)", string.Empty);
    cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim(' ', '.', '-');
    return string.IsNullOrWhiteSpace(cleaned) ? "Unsorted" : cleaned;
}

static string EnsureOrganizationFileExtension(string value, string? preferredExtension)
{
    var cleaned = value ?? string.Empty;
    var ext = NormalizeOrganizationExtensionToken(preferredExtension);
    if (string.IsNullOrWhiteSpace(ext)) ext = ".cbz";

    // Magazine templates often end with values such as "Vol. 3" or "No. 12".
    // Path.GetExtension treats the last dotted label as an extension, which can prevent the real
    // source extension from being appended. Only trust known GuideVault package extensions here.
    var currentExt = NormalizeOrganizationExtensionToken(Path.GetExtension(cleaned));
    if (HasKnownOrganizationFileExtension(currentExt)) return cleaned;

    return cleaned.TrimEnd('.') + ext;
}

static bool HasKnownOrganizationFileExtension(string? value)
{
    var ext = NormalizeOrganizationExtensionToken(value);
    return ext.Equals(".cbz", StringComparison.OrdinalIgnoreCase)
        || ext.Equals(".cbr", StringComparison.OrdinalIgnoreCase)
        || ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
        || ext.Equals(".zip", StringComparison.OrdinalIgnoreCase);
}

static string SanitizeFileName(string value, string? preferredExtension = ".cbz")
{
    var cleaned = SanitizePathPart(value);
    return EnsureOrganizationFileExtension(cleaned, preferredExtension);
}

static bool TryResolveLibraryRoot(string filePath, IEnumerable<string> libraryPaths, out string root)
{
    root = string.Empty;
    var normalized = Path.GetFullPath(filePath);
    foreach (var candidate in libraryPaths.Where(p => !string.IsNullOrWhiteSpace(p)).Select(Path.GetFullPath).OrderByDescending(p => p.Length))
    {
        var withSep = candidate.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (normalized.StartsWith(withSep, StringComparison.OrdinalIgnoreCase))
        {
            root = candidate;
            return true;
        }
    }
    return false;
}

static bool IsPathInsideRoot(string path, string root)
{
    var normalizedPath = Path.GetFullPath(path);
    var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
    return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
}

static string FirstNonBlank(params string?[] values)
    => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

static string ExtractYear(string? value)
{
    var match = Regex.Match(value ?? string.Empty, "\\b(19|20)\\d{2}\\b");
    return match.Success ? match.Value : string.Empty;
}

static string ExtractMonth(string? value)
{
    var match = Regex.Match(value ?? string.Empty, "\\b(January|February|March|April|May|June|July|August|September|October|November|December|Jan|Feb|Mar|Apr|Jun|Jul|Aug|Sep|Sept|Oct|Nov|Dec)\\b", RegexOptions.IgnoreCase);
    return match.Success ? match.Value : string.Empty;
}

static bool TryGetJsonProperty(JsonElement json, string camelName, out JsonElement value)
{
    if (json.ValueKind == JsonValueKind.Object)
    {
        if (json.TryGetProperty(camelName, out value)) return true;
        foreach (var property in json.EnumerateObject())
        {
            if (property.Name.Equals(camelName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }
    }
    value = default;
    return false;
}

static ItemMetadataUpdate CreateMetadataSnapshotForMove(LibraryItem item)
    => new(
        Title: item.Title,
        Kind: item.Kind,
        System: item.System,
        Category: item.Category,
        Publisher: item.Publisher,
        Year: item.Year,
        Tags: item.Tags,
        Summary: item.Summary,
        Series: item.Series,
        Writer: item.Writer,
        IssueNumber: item.IssueNumber,
        Rating: item.Rating,
        WebLink: item.WebLink,
        Asin: item.Asin,
        Isbn10: item.Isbn10,
        Isbn13: item.Isbn13,
        LanguageTag: item.LanguageTag,
        AssociatedPlatforms: item.AssociatedPlatforms,
        PlatformMatchTitle: item.PlatformMatchTitle,
        PlatformResolverSource: item.PlatformResolverSource,
        PlatformResolverConfidence: item.PlatformResolverConfidence,
        MagazineTitle: item.MagazineTitle,
        Volume: item.Volume,
        CoverDate: item.CoverDate,
        PublicationDate: item.PublicationDate,
        Region: item.Region,
        PlatformFocus: item.PlatformFocus,
        PrimarySystem: item.PrimarySystem,
        MagazineCategory: item.MagazineCategory,
        CoverSubject: item.CoverSubject,
        BarcodeUpcIssn: item.BarcodeUpcIssn,
        FeaturedGames: item.FeaturedGames,
        FeaturedPlatforms: item.FeaturedPlatforms,
        SpecialFeatures: item.SpecialFeatures,
        IncludedExtras: item.IncludedExtras,
        GameTitle: item.GameTitle,
        GuideType: item.GuideType,
        Edition: item.Edition,
        Franchise: item.Franchise,
        Developer: item.Developer,
        GamePublisher: item.GamePublisher,
        GameReleaseYear: item.GameReleaseYear,
        Genre: item.Genre,
        CoveredGames: item.CoveredGames,
        CoveredPlatforms: item.CoveredPlatforms,
        GuideTopics: item.GuideTopics,
        CharactersCovered: item.CharactersCovered,
        LocationsCovered: item.LocationsCovered,
        ManualTitle: item.ManualTitle,
        ManualType: item.ManualType,
        IncludedSections: item.IncludedSections,
        ControlScheme: item.ControlScheme,
        ItemsCovered: item.ItemsCovered,
        WarrantySupport: item.WarrantySupport,
        PageCount: item.PageCount > 0 ? item.PageCount : null,
        MetadataSource: string.IsNullOrWhiteSpace(item.MetadataSource) ? "GuideVault file organization" : item.MetadataSource,
        MetadataStatus: item.MetadataStatus,
        Notes: item.Notes,
        MetadataLocks: item.MetadataLocks);

static string BuildRelativeLibraryPath(string fullPath, IEnumerable<string> libraryPaths, string fallbackRelativePath)
{
    try
    {
        var normalizedFull = Path.GetFullPath(fullPath);
        foreach (var root in libraryPaths.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            var normalizedRoot = Path.GetFullPath(root);
            var rootWithSep = normalizedRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (normalizedFull.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase))
                return Path.GetRelativePath(normalizedRoot, normalizedFull).Replace('\\', '/');
        }
    }
    catch { }

    var folder = Path.GetDirectoryName((fallbackRelativePath ?? string.Empty).Replace('\\', '/'))?.Replace('\\', '/').Trim('/');
    return string.IsNullOrWhiteSpace(folder)
        ? Path.GetFileName(fullPath)
        : $"{folder}/{Path.GetFileName(fullPath)}";
}

record BrowseRoot(string Label, string Path);

record FileOrganizationPlan(
    string Id,
    string Kind,
    string Title,
    string FileName,
    string CurrentPath,
    string ProposedPath,
    string RelativePath,
    string Status,
    string Message);



public sealed class DeviceHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly object _gate = new();
    private readonly string _path;
    private DeviceHistoryData _data;

    public DeviceHistoryStore(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        _data = Load();
        Save();
    }

    public DeviceHistorySnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return Snapshot();
        }
    }

    public ClientDeviceRecord RecordWebClient(HttpRequest request, ClientDeviceHeartbeat payload)
    {
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            var ua = TrimTo(payload.UserAgent, 1000) ?? request.Headers.UserAgent.FirstOrDefault() ?? string.Empty;
            var browserName = Clean(payload.BrowserName) ?? DetectBrowser(ua);
            var browserVersion = Clean(payload.BrowserVersion) ?? DetectBrowserVersion(ua, browserName);
            var platform = Clean(payload.Platform) ?? DetectPlatform(ua);
            var screen = Clean(payload.Screen) ?? string.Empty;
            var username = Clean(payload.Username) ?? string.Empty;
            var fingerprint = string.Join('|', "web", browserName, browserVersion, platform, screen, username, HashText(ua));
            var id = DeviceId(fingerprint);
            var displayName = Clean(payload.DisplayName) ?? $"{browserName} on {platform}";
            var device = _data.ClientDevices.FirstOrDefault(d => d.Id == id);
            if (device is null)
            {
                device = new ClientDeviceRecord
                {
                    Id = id,
                    DisplayName = displayName,
                    ClientType = "Web App",
                    FirstSeen = now
                };
                _data.ClientDevices.Add(device);
            }

            device.DisplayName = string.IsNullOrWhiteSpace(device.CustomName) ? displayName : device.CustomName;
            device.ClientType = "Web App";
            device.Username = username;
            device.Email = Clean(payload.Email) ?? string.Empty;
            device.BrowserName = browserName;
            device.BrowserVersion = browserVersion;
            device.Platform = platform;
            device.Screen = screen;
            device.AppVersion = Clean(payload.AppVersion) ?? GuidevaultBuildInfo.Version;
            device.UserAgent = ua;
            device.IpAddress = RequestIp(request);
            device.LastPath = Clean(payload.LastPath) ?? request.Path.Value ?? "/";
            device.LastSeen = now;
            device.TotalRequests = Math.Max(0, device.TotalRequests) + 1;
            Save();
            return device;
        }
    }

    public ClientDeviceRecord RecordOpdsClient(HttpRequest request, OpdsAuthKey key)
    {
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            var ua = request.Headers.UserAgent.FirstOrDefault() ?? string.Empty;
            var browserName = DetectBrowser(ua);
            var platform = DetectPlatform(ua);
            var keyName = string.IsNullOrWhiteSpace(key.Name) ? "Auth Key" : key.Name.Trim();
            var ip = RequestIp(request);
            var fingerprint = string.Join('|', "opds", key.Id, platform, HashText(ua), ip);
            var id = DeviceId(fingerprint);
            var displayName = $"OPDS Client on {platform}";
            var device = _data.ClientDevices.FirstOrDefault(d => d.Id == id);
            if (device is null)
            {
                device = new ClientDeviceRecord
                {
                    Id = id,
                    DisplayName = displayName,
                    ClientType = "OPDS Client",
                    FirstSeen = now
                };
                _data.ClientDevices.Add(device);
            }

            device.DisplayName = string.IsNullOrWhiteSpace(device.CustomName) ? displayName : device.CustomName;
            device.ClientType = "OPDS Client";
            device.Username = keyName;
            device.AuthKeyId = key.Id;
            device.AuthKeyName = keyName;
            device.BrowserName = browserName;
            device.BrowserVersion = DetectBrowserVersion(ua, browserName);
            device.Platform = platform;
            device.Screen = string.Empty;
            device.AppVersion = string.Empty;
            device.UserAgent = ua;
            device.IpAddress = ip;
            device.LastPath = request.Path.Value ?? "/opds";
            device.LastSeen = now;
            device.TotalRequests = Math.Max(0, device.TotalRequests) + 1;
            Save();
            return device;
        }
    }

    public DeviceHistorySnapshot UpsertEmailDevice(EmailDeviceUpsert payload)
    {
        payload ??= new EmailDeviceUpsert();
        var payloadId = (payload.Id ?? string.Empty).Trim();
        var payloadName = (payload.Name ?? string.Empty).Trim();
        var payloadEmail = (payload.Email ?? string.Empty).Trim();
        var payloadPlatform = (payload.Platform ?? string.Empty).Trim();

        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            var id = string.IsNullOrWhiteSpace(payloadId) ? $"email-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Guid.NewGuid():N}"[..40] : payloadId;
            var device = _data.EmailDevices.FirstOrDefault(d => d.Id == id);
            if (device is null)
            {
                device = new EmailDeviceRecord { Id = id, CreatedAt = now };
                _data.EmailDevices.Add(device);
            }
            device.Name = payloadName;
            device.Email = payloadEmail;
            device.Platform = string.IsNullOrWhiteSpace(payloadPlatform) ? "Email" : payloadPlatform;
            device.UpdatedAt = now;
            Save();
            return Snapshot();
        }
    }

    public DeviceHistorySnapshot? DeleteEmailDevice(string id)
    {
        lock (_gate)
        {
            var device = _data.EmailDevices.FirstOrDefault(d => d.Id == id);
            if (device is null) return null;
            _data.EmailDevices.Remove(device);
            Save();
            return Snapshot();
        }
    }

    public DeviceHistorySnapshot? RenameClientDevice(string id, string? displayName)
    {
        lock (_gate)
        {
            var device = _data.ClientDevices.FirstOrDefault(d => d.Id == id);
            if (device is null) return null;
            var name = Clean(displayName);
            if (name is null) return Snapshot();
            device.CustomName = TrimTo(name, 80);
            device.DisplayName = device.CustomName;
            Save();
            return Snapshot();
        }
    }

    public DeviceHistorySnapshot? DeleteClientDevice(string id)
    {
        lock (_gate)
        {
            var device = _data.ClientDevices.FirstOrDefault(d => d.Id == id);
            if (device is null) return null;
            _data.ClientDevices.Remove(device);
            Save();
            return Snapshot();
        }
    }

    public DeviceHistorySnapshot ClearStaleClientDevices(int days)
    {
        lock (_gate)
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(-Math.Clamp(days, 1, 3650));
            _data.ClientDevices.RemoveAll(d => d.LastSeen < cutoff);
            Save();
            return Snapshot();
        }
    }

    private DeviceHistorySnapshot Snapshot()
    {
        _data.EmailDevices ??= new List<EmailDeviceRecord>();
        _data.ClientDevices ??= new List<ClientDeviceRecord>();
        var now = DateTimeOffset.UtcNow;
        foreach (var device in _data.ClientDevices)
            device.IsActive = device.LastSeen >= now.AddMinutes(-10);

        return new DeviceHistorySnapshot
        {
            EmailDevices = _data.EmailDevices.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            ClientDevices = _data.ClientDevices.OrderByDescending(d => d.IsActive).ThenByDescending(d => d.LastSeen).ToList(),
            GeneratedAt = now
        };
    }

    private DeviceHistoryData Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var loaded = JsonSerializer.Deserialize<DeviceHistoryData>(File.ReadAllText(_path), JsonOptions);
                if (loaded is not null)
                {
                    loaded.EmailDevices ??= new List<EmailDeviceRecord>();
                    loaded.ClientDevices ??= new List<ClientDeviceRecord>();
                    return loaded;
                }
            }
        }
        catch
        {
            // Prefer a clean device history file over blocking app startup.
        }
        return new DeviceHistoryData();
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(_data, JsonOptions));
    }

    private static string? Clean(string? value)
    {
        var text = value?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string TrimTo(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var text = value.Trim();
        return text.Length <= max ? text : text[..max];
    }

    private static string RequestIp(HttpRequest request)
    {
        var forwarded = request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded)) return forwarded.Split(',')[0].Trim();
        return request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
    }

    private static string DeviceId(string fingerprint) => "dev-" + HashText(fingerprint)[..18];

    private static string HashText(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string DetectPlatform(string userAgent)
    {
        var ua = userAgent ?? string.Empty;
        if (ua.Contains("Android", StringComparison.OrdinalIgnoreCase)) return "Android";
        if (ua.Contains("iPhone", StringComparison.OrdinalIgnoreCase) || ua.Contains("iPad", StringComparison.OrdinalIgnoreCase) || ua.Contains("iOS", StringComparison.OrdinalIgnoreCase)) return "iOS";
        if (ua.Contains("Windows", StringComparison.OrdinalIgnoreCase)) return "Windows";
        if (ua.Contains("Macintosh", StringComparison.OrdinalIgnoreCase) || ua.Contains("Mac OS", StringComparison.OrdinalIgnoreCase)) return "macOS";
        if (ua.Contains("Linux", StringComparison.OrdinalIgnoreCase)) return "Linux";
        return "Unknown";
    }

    private static string DetectBrowser(string userAgent)
    {
        var ua = userAgent ?? string.Empty;
        if (ua.Contains("OPDS", StringComparison.OrdinalIgnoreCase)) return "OPDS";
        if (ua.Contains("Edg/", StringComparison.OrdinalIgnoreCase)) return "Edge";
        if (ua.Contains("Chrome/", StringComparison.OrdinalIgnoreCase) || ua.Contains("CriOS/", StringComparison.OrdinalIgnoreCase)) return "Chrome";
        if (ua.Contains("Firefox/", StringComparison.OrdinalIgnoreCase) || ua.Contains("FxiOS/", StringComparison.OrdinalIgnoreCase)) return "Firefox";
        if (ua.Contains("Safari/", StringComparison.OrdinalIgnoreCase)) return "Safari";
        return string.IsNullOrWhiteSpace(ua) ? "Unknown" : "Client";
    }

    private static string DetectBrowserVersion(string userAgent, string browserName)
    {
        var ua = userAgent ?? string.Empty;
        var token = browserName switch
        {
            "Edge" => "Edg/",
            "Chrome" => ua.Contains("CriOS/", StringComparison.OrdinalIgnoreCase) ? "CriOS/" : "Chrome/",
            "Firefox" => ua.Contains("FxiOS/", StringComparison.OrdinalIgnoreCase) ? "FxiOS/" : "Firefox/",
            "Safari" => "Version/",
            _ => string.Empty
        };
        if (string.IsNullOrWhiteSpace(token)) return string.Empty;
        var index = ua.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return string.Empty;
        var start = index + token.Length;
        var end = start;
        while (end < ua.Length && (char.IsDigit(ua[end]) || ua[end] == '.')) end++;
        return end > start ? ua[start..end] : string.Empty;
    }
}

public sealed class DeviceHistoryData
{
    public List<EmailDeviceRecord> EmailDevices { get; set; } = new();
    public List<ClientDeviceRecord> ClientDevices { get; set; } = new();
}

public sealed class DeviceHistorySnapshot
{
    public List<EmailDeviceRecord> EmailDevices { get; set; } = new();
    public List<ClientDeviceRecord> ClientDevices { get; set; } = new();
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class EmailDeviceRecord
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ClientDeviceRecord
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string CustomName { get; set; } = string.Empty;
    public string ClientType { get; set; } = string.Empty;
    public string BrowserName { get; set; } = string.Empty;
    public string BrowserVersion { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string Screen { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string AuthKeyId { get; set; } = string.Empty;
    public string AuthKeyName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public string LastPath { get; set; } = string.Empty;
    public DateTimeOffset FirstSeen { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeen { get; set; } = DateTimeOffset.UtcNow;
    public int TotalRequests { get; set; }
    public bool IsActive { get; set; }
}

public sealed class ClientDeviceNameUpdate
{
    public string? DisplayName { get; set; }
}

public sealed class ClientDeviceHeartbeat
{
    public string? DisplayName { get; set; }
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? BrowserName { get; set; }
    public string? BrowserVersion { get; set; }
    public string? Platform { get; set; }
    public string? Screen { get; set; }
    public string? AppVersion { get; set; }
    public string? UserAgent { get; set; }
    public string? LastPath { get; set; }
}

public sealed class EmailDeviceUpsert
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Platform { get; set; }
}



public sealed class SystemInfoStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly object _gate = new();
    private readonly string _path;
    private readonly string _currentVersion;
    private SystemInfoRecord _record;

    public SystemInfoStore(string path, string currentVersion)
    {
        _path = path;
        _currentVersion = string.IsNullOrWhiteSpace(currentVersion) ? "0.0.0" : currentVersion.Trim();
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        _record = LoadOrCreate();
        Save();
    }

    public SystemInfoRecord GetSnapshot()
    {
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(_record.Version)) _record.Version = _currentVersion;
            if (string.IsNullOrWhiteSpace(_record.FirstInstallVersion)) _record.FirstInstallVersion = _currentVersion;
            if (string.IsNullOrWhiteSpace(_record.InstallId)) _record.InstallId = GenerateInstallId();
            if (_record.FirstInstallDate == default) _record.FirstInstallDate = DateTimeOffset.UtcNow;
            _record.Version = _currentVersion;
            Save();
            return _record.Clone();
        }
    }

    private SystemInfoRecord LoadOrCreate()
    {
        try
        {
            if (File.Exists(_path))
            {
                var raw = File.ReadAllText(_path);
                var loaded = JsonSerializer.Deserialize<SystemInfoRecord>(raw, JsonOptions);
                if (loaded is not null) return Normalize(loaded);
            }
        }
        catch
        {
            // If the local system info file is corrupt, regenerate only the app identity metadata.
        }
        return Normalize(new SystemInfoRecord());
    }

    private SystemInfoRecord Normalize(SystemInfoRecord record)
    {
        record.Version = _currentVersion;
        if (string.IsNullOrWhiteSpace(record.FirstInstallVersion)) record.FirstInstallVersion = _currentVersion;
        if (record.FirstInstallDate == default) record.FirstInstallDate = DateTimeOffset.UtcNow;
        if (string.IsNullOrWhiteSpace(record.InstallId)) record.InstallId = GenerateInstallId();
        return record;
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(_record, JsonOptions));
    }

    private static string GenerateInstallId()
    {
        Span<byte> bytes = stackalloc byte[4];
        RandomNumberGenerator.Fill(bytes);
        return $"GV-{DateTimeOffset.UtcNow:yyyyMMdd}-{Convert.ToHexString(bytes)}";
    }
}

public sealed class SystemInfoRecord
{
    public string AppName { get; set; } = "Guidevault";
    public string Version { get; set; } = string.Empty;
    public string FirstInstallVersion { get; set; } = string.Empty;
    public DateTimeOffset FirstInstallDate { get; set; } = DateTimeOffset.UtcNow;
    public string InstallId { get; set; } = string.Empty;
    public string RuntimeMode { get; set; } = "Local self-hosted web app";
    public string SupportedFiles { get; set; } = "CBZ, CBR, PDF";

    public SystemInfoRecord Clone() => new()
    {
        AppName = AppName,
        Version = Version,
        FirstInstallVersion = FirstInstallVersion,
        FirstInstallDate = FirstInstallDate,
        InstallId = InstallId,
        RuntimeMode = RuntimeMode,
        SupportedFiles = SupportedFiles
    };
}


public sealed class StableUpdateChecker
{
    private const string DefaultStableFeedUrl = "https://api.github.com/repos/Shredder5262/GuideVault/tags";
    private const string DefaultTagFeedUrl = "https://api.github.com/repos/Shredder5262/GuideVault/tags";
    private const string DefaultReleaseUrl = "https://github.com/Shredder5262/GuideVault/releases";
    private const string DefaultPackageUrl = "https://github.com/Shredder5262/GuideVault/pkgs/container/guidevault";
    private static readonly HttpClient Http = CreateHttpClient();
    private readonly UpdateOptions _options;
    private readonly string _currentVersion;
    private readonly object _gate = new();
    private StableUpdateResult? _cached;
    private DateTimeOffset _cachedAt = DateTimeOffset.MinValue;

    public StableUpdateChecker(UpdateOptions? options, string currentVersion)
    {
        _options = options ?? new UpdateOptions();
        _currentVersion = string.IsNullOrWhiteSpace(currentVersion) ? "0.0.0" : currentVersion.Trim();
    }

    public async Task<StableUpdateResult> CheckAsync(bool force = false)
    {
        var feedUrl = FirstNonEmpty(
            Environment.GetEnvironmentVariable("GUIDEVAULT_STABLE_UPDATE_FEED_URL"),
            Environment.GetEnvironmentVariable("GUIDEVAULT_UPDATE_FEED_URL"),
            Environment.GetEnvironmentVariable("GUIDEVAULT_RELEASE_API_URL"),
            _options.StableFeedUrl,
            DefaultStableFeedUrl);
        var channel = FirstNonEmpty(Environment.GetEnvironmentVariable("GUIDEVAULT_UPDATE_CHANNEL"), _options.Channel, "stable");
        var currentImage = FirstNonEmpty(Environment.GetEnvironmentVariable("GUIDEVAULT_IMAGE"), _options.CurrentImage, "ghcr.io/shredder5262/guidevault:latest");
        var releaseUrlFallback = FirstNonEmpty(
            Environment.GetEnvironmentVariable("GUIDEVAULT_RELEASE_URL"),
            Environment.GetEnvironmentVariable("GUIDEVAULT_RELEASE_PATH"),
            _options.ReleaseUrl,
            _options.ReleasePath,
            DefaultReleaseUrl);
        var packageUrl = FirstNonEmpty(
            Environment.GetEnvironmentVariable("GUIDEVAULT_PACKAGE_URL"),
            _options.PackageUrl,
            DefaultPackageUrl);
        var cacheMinutes = Math.Clamp(_options.CheckCacheMinutes <= 0 ? 15 : _options.CheckCacheMinutes, 1, 240);

        lock (_gate)
        {
            if (!force && _cached is not null && DateTimeOffset.UtcNow - _cachedAt < TimeSpan.FromMinutes(cacheMinutes))
                return _cached with { Forced = false };
        }

        StableUpdateResult result;
        try
        {
            var fetched = await FetchUpdateJsonAsync(feedUrl, force);
            using var doc = fetched.Document;
            var feedUrlUsed = fetched.Url;
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                root = root.GetArrayLength() > 0 ? root[0] : throw new InvalidOperationException("The update feed returned an empty release/tag list.");
            }
            var latestVersion = ReadString(root, "version", "latestVersion", "stableVersion", "tag", "tag_name", "name");
            var image = ReadString(root, "image", "containerImage", "dockerImage") ?? currentImage;
            var releaseUrl = FirstNonEmpty(
                ReadString(root, "url", "releaseUrl", "releasePath", "htmlUrl", "html_url"),
                BuildReleaseUrlForVersion(latestVersion),
                releaseUrlFallback);
            var publishedAt = ReadDate(root, "publishedAt", "published", "published_at", "date", "created_at");
            var notes = ReadStringArray(root, "notes", "releaseNotes", "changes", "body", "description");
            var feedChannel = ReadString(root, "channel") ?? channel;
            var available = IsNewerVersion(latestVersion, _currentVersion);
            result = new StableUpdateResult
            {
                Configured = true,
                Channel = feedChannel,
                CurrentVersion = _currentVersion,
                CurrentImage = currentImage,
                LatestVersion = NormalizeVersion(latestVersion),
                LatestImage = image ?? string.Empty,
                FeedUrl = feedUrlUsed,
                ReleaseUrl = releaseUrl,
                ReleasePath = releaseUrl,
                PackageUrl = packageUrl,
                PublishedAt = publishedAt,
                Notes = notes,
                UpdateAvailable = available,
                Forced = force,
                CheckedAt = DateTimeOffset.UtcNow,
                Status = available ? "available" : "current",
                Message = available
                    ? $"Guidevault {NormalizeVersion(latestVersion)} is available on the {feedChannel} channel."
                    : $"Guidevault is current for the configured {feedChannel} release path."
            };
        }
        catch (Exception ex)
        {
            result = new StableUpdateResult
            {
                Configured = true,
                Channel = channel,
                CurrentVersion = _currentVersion,
                CurrentImage = currentImage,
                LatestVersion = _currentVersion,
                LatestImage = currentImage,
                FeedUrl = feedUrl,
                ReleaseUrl = releaseUrlFallback,
                ReleasePath = releaseUrlFallback,
                PackageUrl = packageUrl,
                Forced = force,
                CheckedAt = DateTimeOffset.UtcNow,
                Status = "unverified",
                Message = $"Could not verify the latest stable version right now. Configured release and package paths are shown below. Details: {ex.Message}"
            };
        }

        lock (_gate)
        {
            _cached = result;
            _cachedAt = DateTimeOffset.UtcNow;
        }
        return result;
    }

    private static async Task<(JsonDocument Document, string Url)> FetchUpdateJsonAsync(string feedUrl, bool force)
    {
        var first = await SendUpdateRequestAsync(feedUrl, force);
        if (first.IsSuccessStatusCode)
        {
            await using var stream = await first.Content.ReadAsStreamAsync();
            return (await JsonDocument.ParseAsync(stream), feedUrl);
        }

        var firstStatus = (int)first.StatusCode;
        first.Dispose();

        if (!string.Equals(feedUrl, DefaultTagFeedUrl, StringComparison.OrdinalIgnoreCase))
        {
            var fallback = await SendUpdateRequestAsync(DefaultTagFeedUrl, force);
            if (fallback.IsSuccessStatusCode)
            {
                await using var stream = await fallback.Content.ReadAsStreamAsync();
                return (await JsonDocument.ParseAsync(stream), DefaultTagFeedUrl);
            }

            var fallbackStatus = (int)fallback.StatusCode;
            fallback.Dispose();
            throw new HttpRequestException($"Release feed returned HTTP {firstStatus}; tag feed returned HTTP {fallbackStatus}.");
        }

        throw new HttpRequestException($"Update feed returned HTTP {firstStatus}.");
    }

    private static Task<HttpResponseMessage> SendUpdateRequestAsync(string url, bool force)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = force };
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        return Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("GuidevaultUpdateChecker/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return client;
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;

    private static string? ReadString(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var value)) continue;
            if (value.ValueKind == JsonValueKind.String) return value.GetString();
            if (value.ValueKind != JsonValueKind.Null && value.ValueKind != JsonValueKind.Undefined) return value.ToString();
        }
        return null;
    }

    private static DateTimeOffset? ReadDate(JsonElement root, params string[] names)
    {
        var raw = ReadString(root, names);
        return DateTimeOffset.TryParse(raw, out var value) ? value : null;
    }

    private static string[] ReadStringArray(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var value)) continue;
            if (value.ValueKind == JsonValueKind.Array)
            {
                return value.EnumerateArray()
                    .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => item!.Trim())
                    .Take(12)
                    .ToArray();
            }
            if (value.ValueKind == JsonValueKind.String)
            {
                return value.GetString()?
                    .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Where(line => !line.StartsWith("#", StringComparison.Ordinal))
                    .Take(12)
                    .ToArray() ?? [];
            }
        }
        return [];
    }

    private static string BuildReleaseUrlForVersion(string? value)
    {
        var tag = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(tag)) return string.Empty;
        return $"https://github.com/Shredder5262/GuideVault/releases/tag/{Uri.EscapeDataString(tag)}";
    }

    private static string NormalizeVersion(string? value)
    {
        var text = Regex.Match(value ?? string.Empty, @"\d+(?:\.\d+){0,3}").Value;
        return string.IsNullOrWhiteSpace(text) ? (value ?? string.Empty).Trim() : text;
    }

    private static bool IsNewerVersion(string? latest, string current)
    {
        var latestParts = VersionParts(latest);
        var currentParts = VersionParts(current);
        var length = Math.Max(latestParts.Length, currentParts.Length);
        for (var i = 0; i < length; i++)
        {
            var l = i < latestParts.Length ? latestParts[i] : 0;
            var c = i < currentParts.Length ? currentParts[i] : 0;
            if (l > c) return true;
            if (l < c) return false;
        }
        return false;
    }

    private static int[] VersionParts(string? value)
    {
        var text = Regex.Match(value ?? string.Empty, @"\d+(?:\.\d+){0,3}").Value;
        if (string.IsNullOrWhiteSpace(text)) return [];
        return text.Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => int.TryParse(part, out var n) ? n : 0)
            .ToArray();
    }
}

public sealed record StableUpdateResult
{
    public bool Configured { get; set; }
    public string Channel { get; set; } = "stable";
    public string CurrentVersion { get; set; } = string.Empty;
    public string CurrentImage { get; set; } = string.Empty;
    public string LatestVersion { get; set; } = string.Empty;
    public string LatestImage { get; set; } = string.Empty;
    public string FeedUrl { get; set; } = string.Empty;
    public string ReleaseUrl { get; set; } = string.Empty;
    public string ReleasePath { get; set; } = string.Empty;
    public string PackageUrl { get; set; } = string.Empty;
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset? CheckedAt { get; set; }
    public string[] Notes { get; set; } = [];
    public bool UpdateAvailable { get; set; }
    public bool Forced { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}


public sealed class GuidevaultServerSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly object _gate = new();
    private readonly string _path;
    private readonly string _contentRoot;
    private GuidevaultServerSettings _settings;

    public GuidevaultServerSettingsStore(string path, string contentRoot)
    {
        _path = path;
        _contentRoot = contentRoot;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        _settings = Normalize(Load());
        Save();
    }

    public GuidevaultServerSettings GetSnapshot()
    {
        lock (_gate) return CloneForClient(_settings);
    }

    public GuidevaultServerSettings Update(GuidevaultServerSettings payload)
    {
        lock (_gate)
        {
            _settings = Normalize(payload);
            Save();
            return CloneForClient(_settings);
        }
    }

    public GuidevaultBackupResult CreateLibraryBackup(IEnumerable<string> sourceFiles)
    {
        lock (_gate)
        {
            _settings = Normalize(_settings);
            var backupDir = ResolveSettingPath(_contentRoot, _settings.BackupDirectory);
            Directory.CreateDirectory(backupDir);
            var fileName = $"guidevault-library-backup-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.zip";
            var fullPath = Path.Combine(backupDir, fileName);
            using (var stream = File.Open(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                var manifest = JsonSerializer.Serialize(new
                {
                    app = "Guidevault",
                    createdAt = DateTimeOffset.UtcNow,
                    note = "Library configuration and index backup. Source media files are not copied."
                }, JsonOptions);
                var manifestEntry = zip.CreateEntry("manifest.json", CompressionLevel.Optimal);
                using (var writer = new StreamWriter(manifestEntry.Open(), Encoding.UTF8)) writer.Write(manifest);

                foreach (var file in sourceFiles.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var name = Path.GetFileName(file);
                    var folder = Path.GetFileName(Path.GetDirectoryName(file) ?? string.Empty);
                    var entryName = string.Equals(name, "library-index.json", StringComparison.OrdinalIgnoreCase)
                        ? "cache/library-index.json"
                        : $"config/{name}";
                    if (string.Equals(folder, "cache", StringComparison.OrdinalIgnoreCase)) entryName = $"cache/{name}";
                    zip.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
                }
            }
            var info = new FileInfo(fullPath);
            return new GuidevaultBackupResult(fileName, info.FullName, info.Length, DateTimeOffset.UtcNow, _settings.BackupDirectory);
        }
    }

    private GuidevaultServerSettings Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<GuidevaultServerSettings>(File.ReadAllText(_path), JsonOptions) ?? new GuidevaultServerSettings();
        }
        catch { }
        return new GuidevaultServerSettings();
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(_settings, JsonOptions));
    }

    private GuidevaultServerSettings Normalize(GuidevaultServerSettings value)
    {
        value ??= new GuidevaultServerSettings();
        value.HostName = Clean(value.HostName, "http://localhost:5478");
        value.BaseUrl = NormalizeBaseUrl(value.BaseUrl);
        value.IpAddresses = Clean(value.IpAddresses, string.Empty);
        value.Port = value.Port is >= 1 and <= 65535 ? value.Port : 5478;
        value.LoggingLevel = NormalizeLogLevel(value.LoggingLevel);
        value.BackupDirectory = NormalizePathValue(value.BackupDirectory, "data/backups");
        value.BookmarksDirectory = NormalizePathValue(value.BookmarksDirectory, "data/bookmarks");
        value.IgdbClientId = Clean(value.IgdbClientId, string.Empty);
        value.IgdbClientSecret = Clean(value.IgdbClientSecret, string.Empty);
        value.HomeAssistantEnabled = value.HomeAssistantEnabled;
        value.HomeAssistantUrl = NormalizeHomeAssistantUrl(value.HomeAssistantUrl);
        value.HomeAssistantLongLivedAccessToken = Clean(value.HomeAssistantLongLivedAccessToken, string.Empty);
        value.HomeAssistantEntityPrefix = NormalizeHomeAssistantEntityPrefix(value.HomeAssistantEntityPrefix);
        value.HomeAssistantPushStateEnabled = value.HomeAssistantPushStateEnabled;
        value.HomeAssistantPushEventsEnabled = value.HomeAssistantPushEventsEnabled;
        value.HomeAssistantCommandEnabled = value.HomeAssistantCommandEnabled;
        value.HomeAssistantCommandToken = NormalizeHomeAssistantCommandToken(value.HomeAssistantCommandToken);
        return value;
    }

    private static GuidevaultServerSettings CloneForClient(GuidevaultServerSettings value) => new()
    {
        HostName = value.HostName,
        BaseUrl = value.BaseUrl,
        IpAddresses = value.IpAddresses,
        Port = value.Port,
        LoggingLevel = value.LoggingLevel,
        BackupDirectory = value.BackupDirectory,
        BookmarksDirectory = value.BookmarksDirectory,
        IgdbClientId = value.IgdbClientId,
        IgdbClientSecret = value.IgdbClientSecret,
        HomeAssistantEnabled = value.HomeAssistantEnabled,
        HomeAssistantUrl = value.HomeAssistantUrl,
        HomeAssistantLongLivedAccessToken = value.HomeAssistantLongLivedAccessToken,
        HomeAssistantEntityPrefix = value.HomeAssistantEntityPrefix,
        HomeAssistantPushStateEnabled = value.HomeAssistantPushStateEnabled,
        HomeAssistantPushEventsEnabled = value.HomeAssistantPushEventsEnabled,
        HomeAssistantCommandEnabled = value.HomeAssistantCommandEnabled,
        HomeAssistantCommandToken = value.HomeAssistantCommandToken
    };

    private static string Clean(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    private static string NormalizeBaseUrl(string? value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? "/" : value.Trim();
        if (!text.StartsWith('/')) text = "/" + text;
        return text == "/" || text == "//" ? "/" : text.TrimEnd('/');
    }
    private static string NormalizeLogLevel(string? value)
    {
        var text = Clean(value, "Information");
        var allowed = new[] { "Trace", "Debug", "Information", "Warning", "Error", "Critical", "None" };
        return allowed.FirstOrDefault(x => string.Equals(x, text, StringComparison.OrdinalIgnoreCase)) ?? "Information";
    }
    private static string NormalizePathValue(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().Replace('\\', '/');
    private static string NormalizeHomeAssistantUrl(string? value)
    {
        var text = Clean(value, string.Empty).Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        if (!text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            text = $"http://{text}";

        // Users often paste the REST API root after testing Home Assistant manually.
        // Store only the Home Assistant base URL so Guidevault can append /api/... consistently.
        if (text.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
            text = text[..^4].TrimEnd('/');

        return text;
    }
    private static string NormalizeHomeAssistantEntityPrefix(string? value)
    {
        var text = Clean(value, "guidevault").ToLowerInvariant();
        text = Regex.Replace(text, "[^a-z0-9_]+", "_").Trim('_');
        return string.IsNullOrWhiteSpace(text) ? "guidevault" : text;
    }
    private static string NormalizeHomeAssistantCommandToken(string? value)
    {
        var text = Clean(value, string.Empty);
        if (!string.IsNullOrWhiteSpace(text) && text.Length >= 16) return text;
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
    }
    private static string ResolveSettingPath(string contentRoot, string value)
    {
        var path = string.IsNullOrWhiteSpace(value) ? "data/backups" : value.Trim();
        return Path.IsPathRooted(path) || path.StartsWith(@"\\") || Regex.IsMatch(path, @"^[A-Za-z]:[\\/].*")
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(contentRoot, path));
    }
}

public sealed class GuidevaultServerSettings
{
    public string HostName { get; set; } = "http://localhost:5478";
    public string BaseUrl { get; set; } = "/";
    public string IpAddresses { get; set; } = string.Empty;
    public int Port { get; set; } = 5478;
    public string LoggingLevel { get; set; } = "Information";
    public string BackupDirectory { get; set; } = "data/backups";
    public string BookmarksDirectory { get; set; } = "data/bookmarks";
    public string IgdbClientId { get; set; } = string.Empty;
    public string IgdbClientSecret { get; set; } = string.Empty;
    public bool HomeAssistantEnabled { get; set; } = false;
    public string HomeAssistantUrl { get; set; } = string.Empty;
    public string HomeAssistantLongLivedAccessToken { get; set; } = string.Empty;
    public string HomeAssistantEntityPrefix { get; set; } = "guidevault";
    public bool HomeAssistantPushStateEnabled { get; set; } = true;
    public bool HomeAssistantPushEventsEnabled { get; set; } = true;
    public bool HomeAssistantCommandEnabled { get; set; } = true;
    public string HomeAssistantCommandToken { get; set; } = string.Empty;
}

public sealed record GuidevaultBackupResult(string FileName, string Path, long SizeBytes, DateTimeOffset CreatedAt, string BackupDirectory);



public sealed class ReaderBackgroundCatalog
{
    public static ReaderBackgroundCatalog Empty { get; } = new();
    public string Folder { get; set; } = string.Empty;
    public string DefaultName { get; set; } = string.Empty;
    public IReadOnlyList<ReaderBackgroundInfo> Backgrounds { get; set; } = Array.Empty<ReaderBackgroundInfo>();
}

public sealed class ReaderBackgroundInfo
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public sealed class GuidevaultHomeAssistantConnector
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = false };
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(8) };
    private readonly GuidevaultServerSettingsStore _settingsStore;
    private readonly ILogger _logger;
    private readonly Func<ReaderBackgroundCatalog> _readerBackgroundCatalog;
    private readonly GuidevaultHomeAssistantCommandStore _commands = new();
    private readonly object _statusGate = new();
    private GuidevaultHomeAssistantReaderStatus _readerStatus = new();

    public GuidevaultHomeAssistantConnector(GuidevaultServerSettingsStore settingsStore, ILogger logger, Func<ReaderBackgroundCatalog>? readerBackgroundCatalog = null)
    {
        _settingsStore = settingsStore;
        _logger = logger;
        _readerBackgroundCatalog = readerBackgroundCatalog ?? (() => ReaderBackgroundCatalog.Empty);
    }

    public GuidevaultHomeAssistantStatusSnapshot GetStatusSnapshot(int libraryItemCount)
    {
        var settings = _settingsStore.GetSnapshot();
        GuidevaultHomeAssistantReaderStatus reader;
        lock (_statusGate) reader = _readerStatus.Clone();
        var backgroundCatalog = GetReaderBackgroundCatalogSafe();
        var enrichedReader = EnrichReaderStatusWithBackgroundCatalog(reader, backgroundCatalog);
        return new GuidevaultHomeAssistantStatusSnapshot
        {
            Enabled = settings.HomeAssistantEnabled,
            PushStateEnabled = settings.HomeAssistantPushStateEnabled,
            PushEventsEnabled = settings.HomeAssistantPushEventsEnabled,
            CommandEnabled = settings.HomeAssistantCommandEnabled,
            EntityPrefix = settings.HomeAssistantEntityPrefix,
            CommandToken = settings.HomeAssistantCommandToken,
            Reader = enrichedReader,
            LibraryItemCount = libraryItemCount,
            CommandQueueDepth = _commands.Count,
            AvailableBackgrounds = backgroundCatalog.Backgrounds,
            DefaultBackground = backgroundCatalog.DefaultName,
            BackgroundFolder = backgroundCatalog.Folder
        };
    }

    public async Task<GuidevaultHomeAssistantPublishResult> TestAsync(int libraryItemCount)
    {
        var settings = _settingsStore.GetSnapshot();
        if (!settings.HomeAssistantEnabled) return new(false, false, "Home Assistant integration is disabled.");
        if (string.IsNullOrWhiteSpace(settings.HomeAssistantUrl)) return new(false, false, "Home Assistant URL is blank.");
        if (string.IsNullOrWhiteSpace(settings.HomeAssistantLongLivedAccessToken)) return new(false, false, "Home Assistant long-lived access token is blank.");

        try
        {
            var probe = await ProbeHomeAssistantApiAsync(settings);
            if (!probe.Success)
            {
                return new(false, false, probe.Message);
            }

            var status = GetStatusSnapshot(libraryItemCount).Reader;
            status.EventType = "connection_test";
            status.Message = "Guidevault connection test";
            var publish = await PublishStatusAsync(settings, status, libraryItemCount, force: true);
            if (!publish.Success) return publish;
            return new(true, true, "Home Assistant connection verified and Guidevault test sensors were published.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Home Assistant connection test failed.");
            return new(false, false, $"Home Assistant connection failed: {ex.Message}");
        }
    }

    public async Task<GuidevaultHomeAssistantPublishResult> UpdateReaderStatusAsync(GuidevaultHomeAssistantReaderStatus payload, int libraryItemCount)
    {
        var backgroundCatalog = GetReaderBackgroundCatalogSafe();
        var normalized = NormalizeReaderStatus(payload, backgroundCatalog);
        lock (_statusGate) _readerStatus = normalized.Clone();

        var settings = _settingsStore.GetSnapshot();
        if (!settings.HomeAssistantEnabled || !settings.HomeAssistantPushStateEnabled)
            return new(true, false, "Reader status saved locally; Home Assistant publishing is disabled.");

        return await PublishStatusAsync(settings, normalized, libraryItemCount, force: false);
    }

    public GuidevaultHomeAssistantCommand EnqueueCommand(GuidevaultHomeAssistantCommandRequest request)
    {
        var normalized = NormalizeCommandRequest(request);
        return _commands.Enqueue(normalized);
    }

    public IReadOnlyList<GuidevaultHomeAssistantCommand> GetCommands(long after) => _commands.GetAfter(after);

    public static bool IsCommandAuthorized(HttpRequest request, GuidevaultServerSettings settings)
    {
        var configured = settings.HomeAssistantCommandToken?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(configured)) return false;

        string supplied = string.Empty;
        if (request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var text = authHeader.ToString().Trim();
            if (text.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) supplied = text[7..].Trim();
        }
        if (string.IsNullOrWhiteSpace(supplied) && request.Headers.TryGetValue("X-Guidevault-Token", out var customHeader)) supplied = customHeader.ToString().Trim();
        if (string.IsNullOrWhiteSpace(supplied) && request.Query.TryGetValue("token", out var queryToken)) supplied = queryToken.ToString().Trim();
        return SlowEquals(configured, supplied);
    }

    private async Task<GuidevaultHomeAssistantPublishResult> PublishStatusAsync(GuidevaultServerSettings settings, GuidevaultHomeAssistantReaderStatus status, int libraryItemCount, bool force)
    {
        if (string.IsNullOrWhiteSpace(settings.HomeAssistantUrl)) return new(false, false, "Home Assistant URL is blank.");
        if (string.IsNullOrWhiteSpace(settings.HomeAssistantLongLivedAccessToken)) return new(false, false, "Home Assistant access token is blank.");

        try
        {
            var prefix = SanitizeEntityPrefix(settings.HomeAssistantEntityPrefix);
            var activeState = status.ReaderActive ? "reading" : "idle";
            var titleState = TruncateState(status.ItemTitle, status.ReaderActive ? "Open" : "None");
            var pageState = status.ReaderActive && status.Page > 0 ? status.Page.ToString() : "0";
            var backgroundCatalog = GetReaderBackgroundCatalogSafe();
            status = EnrichReaderStatusWithBackgroundCatalog(status, backgroundCatalog);
            var attributes = BuildReaderAttributes(status, libraryItemCount, backgroundCatalog);

            await PublishStateAsync(settings, $"sensor.{prefix}_reader", activeState, Merge(attributes, new Dictionary<string, object?>
            {
                ["friendly_name"] = "Guidevault Reader",
                ["icon"] = status.ReaderActive ? "mdi:book-open-page-variant" : "mdi:book-open-blank-variant"
            }));
            await PublishStateAsync(settings, $"sensor.{prefix}_current_item", titleState, Merge(attributes, new Dictionary<string, object?>
            {
                ["friendly_name"] = "Guidevault Current Item",
                ["icon"] = "mdi:book-open"
            }));
            await PublishStateAsync(settings, $"sensor.{prefix}_page", pageState, Merge(attributes, new Dictionary<string, object?>
            {
                ["friendly_name"] = "Guidevault Reader Page",
                ["unit_of_measurement"] = "page",
                ["icon"] = "mdi:file-document-outline"
            }));
            var backgroundState = TruncateState(status.BackgroundDisplayName, string.IsNullOrWhiteSpace(status.Background) ? "Default Gradient" : status.Background);
            await PublishStateAsync(settings, $"sensor.{prefix}_background", backgroundState, Merge(attributes, new Dictionary<string, object?>
            {
                ["friendly_name"] = "Guidevault Reader Background",
                ["icon"] = "mdi:image-filter-hdr"
            }));
            await PublishStateAsync(settings, $"binary_sensor.{prefix}_fullscreen", status.Fullscreen ? "on" : "off", Merge(attributes, new Dictionary<string, object?>
            {
                ["friendly_name"] = "Guidevault Reader Fullscreen",
                ["device_class"] = "running",
                ["icon"] = status.Fullscreen ? "mdi:fullscreen" : "mdi:fullscreen-exit"
            }));
            await PublishStateAsync(settings, $"sensor.{prefix}_library_items", libraryItemCount.ToString(), new Dictionary<string, object?>
            {
                ["friendly_name"] = "Guidevault Library Items",
                ["unit_of_measurement"] = "items",
                ["icon"] = "mdi:bookshelf"
            });

            if (settings.HomeAssistantPushEventsEnabled && (!string.IsNullOrWhiteSpace(status.EventType) || force))
            {
                await FireEventAsync(settings, "guidevault_event", Merge(attributes, new Dictionary<string, object?>
                {
                    ["event_type"] = string.IsNullOrWhiteSpace(status.EventType) ? "reader_status" : status.EventType,
                    ["message"] = status.Message ?? string.Empty
                }));
            }

            return new(true, true, "Published Guidevault state to Home Assistant.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to publish Guidevault state to Home Assistant.");
            return new(false, false, $"Unable to publish to Home Assistant: {ex.Message}");
        }
    }

    private static Dictionary<string, object?> BuildReaderAttributes(GuidevaultHomeAssistantReaderStatus status, int libraryItemCount, ReaderBackgroundCatalog backgroundCatalog) => new(StringComparer.OrdinalIgnoreCase)
    {
        ["reader_active"] = status.ReaderActive,
        ["view"] = status.View,
        ["item_id"] = status.ItemId,
        ["item_title"] = status.ItemTitle,
        ["item_kind"] = status.ItemKind,
        ["page"] = status.Page,
        ["page_count"] = status.PageCount,
        ["progress_percent"] = status.ProgressPercent,
        ["zoom"] = status.Zoom,
        ["display_mode"] = status.DisplayMode,
        ["transition_mode"] = status.TransitionMode,
        ["fullscreen"] = status.Fullscreen,
        ["background"] = status.Background,
        ["background_name"] = status.BackgroundName,
        ["background_display_name"] = status.BackgroundDisplayName,
        ["background_brightness"] = status.BackgroundBrightness,
        ["default_background"] = backgroundCatalog.DefaultName,
        ["available_backgrounds"] = backgroundCatalog.Backgrounds.Select(bg => bg.Name).ToArray(),
        ["available_background_options"] = backgroundCatalog.Backgrounds.Select(bg => new Dictionary<string, string>
        {
            ["name"] = bg.Name,
            ["display_name"] = bg.DisplayName,
            ["url"] = bg.Url
        }).ToArray(),
        ["library_item_count"] = libraryItemCount,
        ["updated_at"] = DateTimeOffset.UtcNow.ToString("O")
    };

    private ReaderBackgroundCatalog GetReaderBackgroundCatalogSafe()
    {
        try
        {
            return _readerBackgroundCatalog() ?? ReaderBackgroundCatalog.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to read Guidevault reader backgrounds for Home Assistant.");
            return ReaderBackgroundCatalog.Empty;
        }
    }

    private static GuidevaultHomeAssistantReaderStatus EnrichReaderStatusWithBackgroundCatalog(GuidevaultHomeAssistantReaderStatus status, ReaderBackgroundCatalog backgroundCatalog)
    {
        status ??= new GuidevaultHomeAssistantReaderStatus();
        backgroundCatalog ??= ReaderBackgroundCatalog.Empty;
        var clone = status.Clone();
        var background = CleanFirst(clone.Background, clone.BackgroundName, clone.ReaderBackground);
        var matched = backgroundCatalog.Backgrounds.FirstOrDefault(bg => bg.Name.Equals(background, StringComparison.OrdinalIgnoreCase))
            ?? backgroundCatalog.Backgrounds.FirstOrDefault(bg => bg.DisplayName.Equals(background, StringComparison.OrdinalIgnoreCase));
        clone.Background = matched?.Name ?? background;
        clone.BackgroundName = clone.Background;
        clone.BackgroundDisplayName = matched?.DisplayName
            ?? (!string.IsNullOrWhiteSpace(clone.Background) ? clone.Background : "Default Gradient");
        clone.BackgroundBrightness = Math.Clamp(clone.BackgroundBrightness <= 0 ? 72 : clone.BackgroundBrightness, 15, 100);
        clone.AvailableBackgrounds = backgroundCatalog.Backgrounds;
        return clone;
    }

    private GuidevaultHomeAssistantReaderStatus NormalizeReaderStatus(GuidevaultHomeAssistantReaderStatus value, ReaderBackgroundCatalog backgroundCatalog)
    {
        value ??= new GuidevaultHomeAssistantReaderStatus();
        var pageCount = Math.Max(0, value.PageCount);
        var page = pageCount > 0 ? Math.Clamp(value.Page, 1, pageCount) : Math.Max(0, value.Page);
        var progress = pageCount > 1 && page > 0 ? Math.Round(((page - 1) / (double)(pageCount - 1)) * 100.0, 2) : (pageCount == 1 ? 100 : 0);
        var background = CleanFirst(value.Background, value.BackgroundName, value.ReaderBackground);
        var backgroundBrightness = value.BackgroundBrightness > 0 ? value.BackgroundBrightness : value.Brightness;
        var normalized = new GuidevaultHomeAssistantReaderStatus
        {
            ReaderActive = value.ReaderActive,
            View = Clean(value.View, value.ReaderActive ? "reader" : "library"),
            ItemId = Clean(value.ItemId, string.Empty),
            ItemTitle = Clean(value.ItemTitle, string.Empty),
            ItemKind = Clean(value.ItemKind, string.Empty),
            Page = page,
            PageCount = pageCount,
            ProgressPercent = value.ProgressPercent > 0 ? Math.Round(Math.Clamp(value.ProgressPercent, 0, 100), 2) : progress,
            Zoom = Math.Clamp(value.Zoom <= 0 ? 100 : value.Zoom, 50, 250),
            DisplayMode = Clean(value.DisplayMode, string.Empty),
            TransitionMode = Clean(value.TransitionMode, string.Empty),
            Fullscreen = value.Fullscreen || value.IsFullscreen || value.ReaderFullscreen,
            Background = background,
            BackgroundName = background,
            BackgroundBrightness = Math.Clamp(backgroundBrightness <= 0 ? 72 : backgroundBrightness, 15, 100),
            EventType = Clean(value.EventType, string.Empty),
            Message = Clean(value.Message, string.Empty),
            UpdatedAt = DateTimeOffset.UtcNow
        };
        return EnrichReaderStatusWithBackgroundCatalog(normalized, backgroundCatalog);
    }

    private static GuidevaultHomeAssistantCommandRequest NormalizeCommandRequest(GuidevaultHomeAssistantCommandRequest request)
    {
        request ??= new GuidevaultHomeAssistantCommandRequest();
        var action = NormalizeAction(request.Action);
        var impliedItemKind = ItemKindFromOpenAction(request.Action);
        return new GuidevaultHomeAssistantCommandRequest
        {
            Action = action,
            ItemId = Clean(request.ItemId, string.Empty),
            ItemTitle = Clean(request.ItemTitle, string.Empty),
            ItemKind = NormalizeHomeAssistantItemKind(request.ItemKind, request.Kind, request.ContentType, request.ItemType, impliedItemKind),
            Query = Clean(request.Query, string.Empty),
            IssueNumber = CleanFirst(request.IssueNumber, request.Issue, request.MagazineIssueNumber, request.IssueNo, request.Number),
            Volume = CleanFirst(request.Volume, request.MagazineVolume, request.Vol),
            Page = Math.Max(0, request.Page),
            Zoom = request.Zoom,
            DisplayMode = Clean(request.DisplayMode, string.Empty),
            Background = CleanFirst(request.Background, request.BackgroundName, request.ReaderBackground, request.BackgroundDisplayName, request.Name),
            BackgroundBrightness = request.BackgroundBrightness > 0
                ? request.BackgroundBrightness
                : (request.ReaderBackgroundBrightness > 0 ? request.ReaderBackgroundBrightness : request.Brightness),
            Message = Clean(request.Message, string.Empty)
        };
    }

    private static string NormalizeHomeAssistantItemKind(params string?[] values)
    {
        foreach (var value in values)
        {
            var raw = Clean(value, string.Empty);
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var text = raw.Trim().ToLowerInvariant().Replace('-', ' ').Replace('_', ' ');
            text = Regex.Replace(text, "\\s+", " ").Trim();
            return text switch
            {
                "manual" or "manuals" or "game manual" or "game manuals" => "Manual",
                "strategy" or "strategy guide" or "strategy guides" or "guide" or "guides" or "walkthrough" or "walkthroughs" => "Strategy Guide",
                "magazine" or "magazines" or "issue" or "issues" => "Magazine",
                _ when string.Equals(raw.Trim(), "Strategy Guide", StringComparison.OrdinalIgnoreCase) => "Strategy Guide",
                _ when string.Equals(raw.Trim(), "Manual", StringComparison.OrdinalIgnoreCase) => "Manual",
                _ when string.Equals(raw.Trim(), "Magazine", StringComparison.OrdinalIgnoreCase) => "Magazine",
                _ => raw.Trim()
            };
        }
        return string.Empty;
    }

    private static string NormalizeAction(string? action)
    {
        var text = Clean(action, "status").Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
        return text switch
        {
            "next" => "next_page",
            "previous" => "previous_page",
            "prev" => "previous_page",
            "open_item" => "open",
            "open_guide" or "open_manual" or "open_manuals" or "open_strategy" or "open_strategy_guide" or "open_strategy_guides" or "open_magazine" or "open_magazines" => "open",
            "jump" => "set_page",
            "page" => "set_page",
            "zoom" => "set_zoom",
            "background" or "reader_background" or "set_reader_background" or "reader_background_set" => "set_background",
            "next_background" or "background_next" or "toggle_background" or "cycle_background" => "next_background",
            "previous_background" or "prev_background" or "background_previous" or "background_prev" => "previous_background",
            "background_brightness" or "brightness" or "reader_background_brightness" or "set_reader_background_brightness" => "set_background_brightness",
            "fullscreen_toggle" or "toggle_full_screen" or "toggle_fullscreen" or "full_screen_toggle" => "toggle_fullscreen",
            "fullscreen_on" or "enter_full_screen" or "enter_fullscreen" or "full_screen" => "fullscreen",
            "fullscreen_off" or "exit_full_screen" => "exit_fullscreen",
            "close" => "close_reader",
            _ => text
        };
    }

    private static string ItemKindFromOpenAction(string? action)
    {
        var text = Clean(action, string.Empty).Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
        return text switch
        {
            "open_manual" or "open_manuals" => "Manual",
            "open_strategy" or "open_strategy_guide" or "open_strategy_guides" or "open_guide" => "Strategy Guide",
            "open_magazine" or "open_magazines" => "Magazine",
            _ => string.Empty
        };
    }

    private static async Task<GuidevaultHomeAssistantProbeResult> ProbeHomeAssistantApiAsync(GuidevaultServerSettings settings)
    {
        var primary = await ProbeHomeAssistantApiPathAsync(settings, "/api/");
        if (primary.Success) return primary;

        // Defensive fallback for proxies that normalize away the trailing slash.
        if (primary.StatusCode == 404)
        {
            var fallback = await ProbeHomeAssistantApiPathAsync(settings, "/api");
            if (fallback.Success) return fallback;
        }

        var baseUrl = settings.HomeAssistantUrl?.Trim() ?? string.Empty;
        if (primary.StatusCode == 404)
        {
            return new(false, primary.StatusCode, $"Home Assistant API returned 404 at {baseUrl}/api/. Guidevault reached the server, but not the Home Assistant REST API. Use the Home Assistant base URL only, for example http://homeassistant.local:8123 or http://192.168.1.217:8123.");
        }

        return primary;
    }

    private static async Task<GuidevaultHomeAssistantProbeResult> ProbeHomeAssistantApiPathAsync(GuidevaultServerSettings settings, string path)
    {
        using var apiRequest = BuildHomeAssistantRequest(HttpMethod.Get, settings, path);
        using var apiResponse = await Client.SendAsync(apiRequest);
        var apiBody = await apiResponse.Content.ReadAsStringAsync();
        if (apiResponse.IsSuccessStatusCode)
            return new(true, (int)apiResponse.StatusCode, "Home Assistant API is reachable.");

        var detail = string.IsNullOrWhiteSpace(apiBody) ? apiResponse.ReasonPhrase ?? "No response body" : apiBody;
        return new(false, (int)apiResponse.StatusCode, $"Home Assistant API returned {(int)apiResponse.StatusCode}: {detail}");
    }

    private static async Task PublishStateAsync(GuidevaultServerSettings settings, string entityId, string state, Dictionary<string, object?> attributes)
    {
        var body = new { state = TruncateState(state, "unknown"), attributes };
        using var request = BuildHomeAssistantRequest(HttpMethod.Post, settings, $"/api/states/{entityId}", body);
        using var response = await Client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var text = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Home Assistant state publish failed for {entityId}: {(int)response.StatusCode} {text}");
        }
    }

    private static async Task FireEventAsync(GuidevaultServerSettings settings, string eventType, Dictionary<string, object?> eventData)
    {
        using var request = BuildHomeAssistantRequest(HttpMethod.Post, settings, $"/api/events/{eventType}", eventData);
        using var response = await Client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var text = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Home Assistant event publish failed: {(int)response.StatusCode} {text}");
        }
    }

    private static HttpRequestMessage BuildHomeAssistantRequest(HttpMethod method, GuidevaultServerSettings settings, string path, object? body = null)
    {
        var baseUrl = (settings.HomeAssistantUrl ?? string.Empty).Trim().TrimEnd('/');
        var request = new HttpRequestMessage(method, baseUrl + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.HomeAssistantLongLivedAccessToken?.Trim() ?? string.Empty);
        if (body is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");
        }
        return request;
    }

    private static Dictionary<string, object?> Merge(Dictionary<string, object?> a, Dictionary<string, object?> b)
    {
        var merged = new Dictionary<string, object?>(a, StringComparer.OrdinalIgnoreCase);
        foreach (var kv in b) merged[kv.Key] = kv.Value;
        return merged;
    }

    private static string SanitizeEntityPrefix(string? prefix)
    {
        var text = Clean(prefix, "guidevault").ToLowerInvariant();
        text = Regex.Replace(text, "[^a-z0-9_]+", "_").Trim('_');
        return string.IsNullOrWhiteSpace(text) ? "guidevault" : text;
    }

    private static string TruncateState(string? value, string fallback)
    {
        var text = Clean(value, fallback);
        return text.Length <= 240 ? text : text[..240];
    }

    private static string Clean(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string CleanFirst(params string?[] values)
    {
        foreach (var value in values)
        {
            var cleaned = Clean(value, string.Empty);
            if (!string.IsNullOrWhiteSpace(cleaned)) return cleaned;
        }
        return string.Empty;
    }

    private static bool SlowEquals(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
        var left = Encoding.UTF8.GetBytes(a);
        var right = Encoding.UTF8.GetBytes(b);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }
}

public sealed class GuidevaultHomeAssistantCommandStore
{
    private readonly object _gate = new();
    private readonly List<GuidevaultHomeAssistantCommand> _commands = new();
    private long _nextId = 0;

    public int Count { get { lock (_gate) return _commands.Count; } }

    public GuidevaultHomeAssistantCommand Enqueue(GuidevaultHomeAssistantCommandRequest request)
    {
        lock (_gate)
        {
            var command = new GuidevaultHomeAssistantCommand
            {
                Id = ++_nextId,
                Action = request.Action,
                ItemId = request.ItemId,
                ItemTitle = request.ItemTitle,
                ItemKind = request.ItemKind,
                Query = request.Query,
                IssueNumber = request.IssueNumber,
                Volume = request.Volume,
                Page = request.Page,
                Zoom = request.Zoom,
                DisplayMode = request.DisplayMode,
                Background = request.Background,
                BackgroundBrightness = request.BackgroundBrightness,
                Message = request.Message,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _commands.Add(command);
            if (_commands.Count > 100) _commands.RemoveRange(0, _commands.Count - 100);
            return command;
        }
    }

    public IReadOnlyList<GuidevaultHomeAssistantCommand> GetAfter(long after)
    {
        lock (_gate) return _commands.Where(c => c.Id > after).OrderBy(c => c.Id).ToList();
    }
}

public sealed class GuidevaultHomeAssistantReaderStatus
{
    public bool ReaderActive { get; set; }
    public string View { get; set; } = "library";
    public string ItemId { get; set; } = string.Empty;
    public string ItemTitle { get; set; } = string.Empty;
    public string ItemKind { get; set; } = string.Empty;
    public int Page { get; set; }
    public int PageCount { get; set; }
    public double ProgressPercent { get; set; }
    public int Zoom { get; set; } = 100;
    public string DisplayMode { get; set; } = string.Empty;
    public string TransitionMode { get; set; } = string.Empty;
    public bool Fullscreen { get; set; }
    public bool IsFullscreen { get; set; }
    public bool ReaderFullscreen { get; set; }
    public string Background { get; set; } = string.Empty;
    public string BackgroundName { get; set; } = string.Empty;
    public string ReaderBackground { get; set; } = string.Empty;
    public string BackgroundDisplayName { get; set; } = string.Empty;
    public int BackgroundBrightness { get; set; } = 72;
    public int Brightness { get; set; }
    public IReadOnlyList<ReaderBackgroundInfo> AvailableBackgrounds { get; set; } = Array.Empty<ReaderBackgroundInfo>();
    public string EventType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public GuidevaultHomeAssistantReaderStatus Clone() => new()
    {
        ReaderActive = ReaderActive,
        View = View,
        ItemId = ItemId,
        ItemTitle = ItemTitle,
        ItemKind = ItemKind,
        Page = Page,
        PageCount = PageCount,
        ProgressPercent = ProgressPercent,
        Zoom = Zoom,
        DisplayMode = DisplayMode,
        TransitionMode = TransitionMode,
        Fullscreen = Fullscreen,
        IsFullscreen = IsFullscreen,
        ReaderFullscreen = ReaderFullscreen,
        Background = Background,
        BackgroundName = BackgroundName,
        ReaderBackground = ReaderBackground,
        BackgroundDisplayName = BackgroundDisplayName,
        BackgroundBrightness = BackgroundBrightness,
        Brightness = Brightness,
        AvailableBackgrounds = AvailableBackgrounds,
        EventType = EventType,
        Message = Message,
        UpdatedAt = UpdatedAt
    };
}

public sealed class GuidevaultHomeAssistantCommandRequest
{
    public string Action { get; set; } = "status";
    public string ItemId { get; set; } = string.Empty;
    public string ItemTitle { get; set; } = string.Empty;
    public string ItemKind { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public string IssueNumber { get; set; } = string.Empty;
    public string Issue { get; set; } = string.Empty;
    public string MagazineIssueNumber { get; set; } = string.Empty;
    public string IssueNo { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public string Volume { get; set; } = string.Empty;
    public string MagazineVolume { get; set; } = string.Empty;
    public string Vol { get; set; } = string.Empty;
    public int Page { get; set; }
    public int Zoom { get; set; }
    public string DisplayMode { get; set; } = string.Empty;
    public string Background { get; set; } = string.Empty;
    public string BackgroundName { get; set; } = string.Empty;
    public string ReaderBackground { get; set; } = string.Empty;
    public string BackgroundDisplayName { get; set; } = string.Empty;
    public int BackgroundBrightness { get; set; }
    public int ReaderBackgroundBrightness { get; set; }
    public int Brightness { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class GuidevaultHomeAssistantCommand
{
    public long Id { get; set; }
    public string Action { get; set; } = "status";
    public string ItemId { get; set; } = string.Empty;
    public string ItemTitle { get; set; } = string.Empty;
    public string ItemKind { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public string IssueNumber { get; set; } = string.Empty;
    public string Volume { get; set; } = string.Empty;
    public int Page { get; set; }
    public int Zoom { get; set; }
    public string DisplayMode { get; set; } = string.Empty;
    public string Background { get; set; } = string.Empty;
    public int BackgroundBrightness { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class GuidevaultHomeAssistantStatusSnapshot
{
    public bool Enabled { get; set; }
    public bool PushStateEnabled { get; set; }
    public bool PushEventsEnabled { get; set; }
    public bool CommandEnabled { get; set; }
    public string EntityPrefix { get; set; } = "guidevault";
    public string CommandToken { get; set; } = string.Empty;
    public GuidevaultHomeAssistantReaderStatus Reader { get; set; } = new();
    public int LibraryItemCount { get; set; }
    public int CommandQueueDepth { get; set; }
    public IReadOnlyList<ReaderBackgroundInfo> AvailableBackgrounds { get; set; } = Array.Empty<ReaderBackgroundInfo>();
    public string DefaultBackground { get; set; } = string.Empty;
    public string BackgroundFolder { get; set; } = string.Empty;
}

public sealed record GuidevaultHomeAssistantProbeResult(bool Success, int StatusCode, string Message);
public sealed record GuidevaultHomeAssistantPublishResult(bool Success, bool Published, string Message);

public sealed class GuidevaultEmailSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly object _gate = new();
    private readonly string _path;
    private GuidevaultEmailSettings _settings;

    public GuidevaultEmailSettingsStore(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        _settings = Normalize(Load(), null);
        Save();
    }

    public GuidevaultEmailSettings GetSettings()
    {
        lock (_gate) return Clone(_settings, includePassword: true);
    }

    public GuidevaultEmailSettings GetClientSettings()
    {
        lock (_gate) return Clone(_settings, includePassword: false);
    }

    public GuidevaultEmailSettings Update(GuidevaultEmailSettings payload)
    {
        lock (_gate)
        {
            _settings = Normalize(payload, _settings);
            Save();
            return Clone(_settings, includePassword: false);
        }
    }

    public void SendInvite(GuidevaultEmailSettings settings, GuidevaultUserRecord user, string inviteUrl)
    {
        if (!settings.IsConfigured) throw new InvalidOperationException("Email settings are incomplete.");
        var body = RenderInviteTemplate(settings.ActiveTemplateBody, user, inviteUrl);
        SendMessage(settings, user.Email, user.DisplayName, settings.ActiveTemplateSubject, body);
    }

    public void SendTest(GuidevaultEmailSettings settings, string to, string subject, string body)
    {
        if (!settings.IsConfigured) throw new InvalidOperationException("Email settings are incomplete.");
        SendMessage(settings, to, string.Empty, subject, body);
    }

    private static void SendMessage(GuidevaultEmailSettings settings, string to, string displayName, string subject, string body)
    {
        var provider = string.IsNullOrWhiteSpace(settings.Provider) ? "transactional-resend" : settings.Provider.Trim().ToLowerInvariant();
        if (provider == "smtp")
        {
            SendSmtpMessage(settings, to, displayName, subject, body);
            return;
        }
        if (provider == "transactional-sendgrid")
        {
            SendSendGridMessage(settings, to, displayName, subject, body);
            return;
        }
        SendResendMessage(settings, to, displayName, subject, body);
    }

    private static void SendSmtpMessage(GuidevaultEmailSettings settings, string to, string displayName, string subject, string body)
    {
        using var message = new MailMessage();
        message.From = new MailAddress(settings.SenderAddress, string.IsNullOrWhiteSpace(settings.DisplayName) ? "Guidevault" : settings.DisplayName);
        message.To.Add(string.IsNullOrWhiteSpace(displayName) ? new MailAddress(to) : new MailAddress(to, displayName));
        message.Subject = string.IsNullOrWhiteSpace(subject) ? "Guidevault email" : subject.Trim();
        message.Body = string.IsNullOrWhiteSpace(body) ? "This is a Guidevault email." : body;
        message.IsBodyHtml = LooksLikeHtml(message.Body);
        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Credentials = new NetworkCredential(settings.Username, settings.Password)
        };
        client.Send(message);
    }

    private static void SendResendMessage(GuidevaultEmailSettings settings, string to, string displayName, string subject, string body)
    {
        var endpoint = string.IsNullOrWhiteSpace(settings.ApiEndpoint) ? "https://api.resend.com/emails" : settings.ApiEndpoint.Trim();
        var payload = new Dictionary<string, object?>
        {
            ["from"] = FormatFromAddress(settings),
            ["to"] = new[] { to },
            ["subject"] = string.IsNullOrWhiteSpace(subject) ? "Guidevault email" : subject.Trim()
        };
        if (LooksLikeHtml(body)) payload["html"] = string.IsNullOrWhiteSpace(body) ? "<p>This is a Guidevault email.</p>" : body;
        else payload["text"] = string.IsNullOrWhiteSpace(body) ? "This is a Guidevault email." : body;
        PostJsonWithBearer(endpoint, settings.ApiKey, payload, "Resend");
    }

    private static void SendSendGridMessage(GuidevaultEmailSettings settings, string to, string displayName, string subject, string body)
    {
        var endpoint = string.IsNullOrWhiteSpace(settings.ApiEndpoint) ? "https://api.sendgrid.com/v3/mail/send" : settings.ApiEndpoint.Trim();
        var contentType = LooksLikeHtml(body) ? "text/html" : "text/plain";
        var payload = new
        {
            personalizations = new[] { new { to = new[] { new { email = to, name = string.IsNullOrWhiteSpace(displayName) ? null : displayName } } } },
            from = new { email = settings.SenderAddress, name = string.IsNullOrWhiteSpace(settings.DisplayName) ? "Guidevault" : settings.DisplayName },
            subject = string.IsNullOrWhiteSpace(subject) ? "Guidevault email" : subject.Trim(),
            content = new[] { new { type = contentType, value = string.IsNullOrWhiteSpace(body) ? "This is a Guidevault email." : body } }
        };
        PostJsonWithBearer(endpoint, settings.ApiKey, payload, "SendGrid");
    }

    private static void PostJsonWithBearer(string endpoint, string apiKey, object payload, string providerName)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) throw new InvalidOperationException($"{providerName} API key is required.");
        using var http = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        using var response = http.Send(request);
        if (!response.IsSuccessStatusCode)
        {
            var detail = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            throw new InvalidOperationException($"{providerName} send failed ({(int)response.StatusCode}): {detail}");
        }
    }

    private static string FormatFromAddress(GuidevaultEmailSettings settings)
    {
        var sender = string.IsNullOrWhiteSpace(settings.SenderAddress) ? "guidevault@example.com" : settings.SenderAddress.Trim();
        var name = string.IsNullOrWhiteSpace(settings.DisplayName) ? "Guidevault" : settings.DisplayName.Trim();
        return string.IsNullOrWhiteSpace(name) ? sender : $"{name} <{sender}>";
    }

    private static string RenderInviteTemplate(string template, GuidevaultUserRecord user, string inviteUrl)
    {
        var permissions = string.Join(", ", user.Permissions ?? Array.Empty<string>());
        var body = string.IsNullOrWhiteSpace(template) ? GuidevaultEmailSettings.DefaultTemplateBody : template;
        return body
            .Replace("{{appName}}", "Guidevault", StringComparison.OrdinalIgnoreCase)
            .Replace("{{displayName}}", user.DisplayName ?? user.Email, StringComparison.OrdinalIgnoreCase)
            .Replace("{{email}}", user.Email ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{{inviteUrl}}", inviteUrl, StringComparison.OrdinalIgnoreCase)
            .Replace("{{permissions}}", permissions, StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeHtml(string value) => value.Contains('<') && value.Contains('>');

    private GuidevaultEmailSettings Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<GuidevaultEmailSettings>(File.ReadAllText(_path), JsonOptions) ?? new GuidevaultEmailSettings();
        }
        catch { }
        return new GuidevaultEmailSettings();
    }

    private void Save() => File.WriteAllText(_path, JsonSerializer.Serialize(_settings, JsonOptions));

    private static GuidevaultEmailSettings Normalize(GuidevaultEmailSettings value, GuidevaultEmailSettings? existing)
    {
        value ??= new GuidevaultEmailSettings();
        value.Provider = Clean(value.Provider);
        if (string.IsNullOrWhiteSpace(value.Provider)) value.Provider = !string.IsNullOrWhiteSpace(value.Host) || !string.IsNullOrWhiteSpace(value.Username) ? "smtp" : "transactional-resend";
        value.HostName = Clean(value.HostName);
        value.SenderAddress = Clean(value.SenderAddress);
        value.DisplayName = Clean(value.DisplayName);
        value.Host = Clean(value.Host);
        value.Port = value.Port is >= 1 and <= 65535 ? value.Port : 587;
        value.Username = Clean(value.Username);
        value.ApiEndpoint = Clean(value.ApiEndpoint);
        value.ApiKey = NormalizeSecret(value.ApiKey, existing?.ApiKey);
        if (string.IsNullOrWhiteSpace(value.ApiEndpoint)) value.ApiEndpoint = value.Provider switch
        {
            "transactional-sendgrid" => "https://api.sendgrid.com/v3/mail/send",
            "transactional-resend" => "https://api.resend.com/emails",
            _ => string.Empty
        };
        var password = value.Password ?? string.Empty;
        if ((string.IsNullOrWhiteSpace(password) || password.All(c => c == '*')) && existing is not null)
            value.Password = existing.Password;
        else
            value.Password = password;
        value.SizeLimitMb = value.SizeLimitMb is >= 1 and <= 2048 ? value.SizeLimitMb : 25;
        value.SelectedTemplateId = Clean(value.SelectedTemplateId);
        value.TemplateName = Clean(value.TemplateName);
        value.TemplateSubject = string.IsNullOrWhiteSpace(value.TemplateSubject) ? GuidevaultEmailSettings.DefaultTemplateSubject : value.TemplateSubject.Trim();
        value.TemplateBody = string.IsNullOrWhiteSpace(value.TemplateBody) ? GuidevaultEmailSettings.DefaultTemplateBody : value.TemplateBody;
        return value;
    }

    private static GuidevaultEmailSettings Clone(GuidevaultEmailSettings value, bool includePassword) => new()
    {
        Provider = value.Provider,
        HostName = value.HostName,
        SenderAddress = value.SenderAddress,
        DisplayName = value.DisplayName,
        Host = value.Host,
        Port = value.Port,
        UseSsl = value.UseSsl,
        Username = value.Username,
        Password = includePassword ? value.Password : (string.IsNullOrWhiteSpace(value.Password) ? string.Empty : "********"),
        ApiKey = includePassword ? value.ApiKey : (string.IsNullOrWhiteSpace(value.ApiKey) ? string.Empty : "********"),
        ApiEndpoint = value.ApiEndpoint,
        SizeLimitMb = value.SizeLimitMb,
        CustomizedTemplates = value.CustomizedTemplates,
        SelectedTemplateId = value.SelectedTemplateId,
        TemplateName = value.TemplateName,
        TemplateSubject = value.TemplateSubject,
        TemplateBody = value.TemplateBody
    };

    private static string NormalizeSecret(string? incoming, string? existing)
    {
        var value = incoming ?? string.Empty;
        if ((string.IsNullOrWhiteSpace(value) || value.All(c => c == '*')) && !string.IsNullOrWhiteSpace(existing))
            return existing;
        return value;
    }

    private static string Clean(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}

public sealed class GuidevaultEmailSettings
{
    public string Provider { get; set; } = "transactional-resend";
    public string HostName { get; set; } = string.Empty;
    public string SenderAddress { get; set; } = string.Empty;
    public string DisplayName { get; set; } = "Guidevault";
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiEndpoint { get; set; } = string.Empty;
    public int SizeLimitMb { get; set; } = 25;
    public bool CustomizedTemplates { get; set; }
    public string SelectedTemplateId { get; set; } = "guidevault-default";
    public string TemplateName { get; set; } = "Guidevault Invite";
    public string TemplateSubject { get; set; } = DefaultTemplateSubject;
    public string TemplateBody { get; set; } = DefaultTemplateBody;
    public static string DefaultTemplateSubject => "Guidevault invite";
    public static string DefaultTemplateBody => "<h2>You have been invited to {{appName}}</h2><p>Hello {{displayName}},</p><p>You can open your Guidevault library here:</p><p><a href=\"{{inviteUrl}}\">{{inviteUrl}}</a></p><p>Permissions: {{permissions}}</p>";
    public string ActiveTemplateName => string.IsNullOrWhiteSpace(TemplateName) ? "Guidevault Invite" : TemplateName;
    public string ActiveTemplateSubject => string.IsNullOrWhiteSpace(TemplateSubject) ? DefaultTemplateSubject : TemplateSubject;
    public string ActiveTemplateBody => string.IsNullOrWhiteSpace(TemplateBody) ? DefaultTemplateBody : TemplateBody;
    public bool IsSmtp => string.Equals(Provider, "smtp", StringComparison.OrdinalIgnoreCase);
    public bool IsConfigured => !string.IsNullOrWhiteSpace(SenderAddress) && (IsSmtp
        ? !string.IsNullOrWhiteSpace(Host) && Port > 0 && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password)
        : !string.IsNullOrWhiteSpace(ApiKey));
}

public sealed class GuidevaultEmailTestRequest
{
    public string? To { get; set; }
    public string? Subject { get; set; }
    public string? Body { get; set; }
}

public sealed class GuidevaultEmailHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly object _gate = new();
    private readonly string _path;
    private List<GuidevaultEmailHistoryRecord> _records;

    public GuidevaultEmailHistoryStore(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        _records = Load();
        Save();
    }

    public IReadOnlyList<GuidevaultEmailHistoryRecord> GetHistory()
    {
        lock (_gate) return _records.OrderByDescending(r => r.SentAt).Take(250).Select(Clone).ToArray();
    }

    public GuidevaultEmailHistoryRecord Record(GuidevaultEmailHistoryRecord record)
    {
        lock (_gate)
        {
            record.Id = string.IsNullOrWhiteSpace(record.Id) ? Guid.NewGuid().ToString("N")[..12] : record.Id;
            record.SentAt = record.SentAt == default ? DateTimeOffset.UtcNow : record.SentAt;
            record.To = Clean(record.To);
            record.Type = Clean(record.Type, "Email");
            record.Subject = Clean(record.Subject, "Guidevault email");
            record.TemplateName = Clean(record.TemplateName, "Guidevault Invite");
            record.Status = Clean(record.Status, "Logged");
            record.Message = Clean(record.Message);
            _records.Insert(0, record);
            if (_records.Count > 500) _records = _records.Take(500).ToList();
            Save();
            return Clone(record);
        }
    }

    private List<GuidevaultEmailHistoryRecord> Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<List<GuidevaultEmailHistoryRecord>>(File.ReadAllText(_path), JsonOptions) ?? new List<GuidevaultEmailHistoryRecord>();
        }
        catch { }
        return new List<GuidevaultEmailHistoryRecord>();
    }

    private void Save() => File.WriteAllText(_path, JsonSerializer.Serialize(_records, JsonOptions));
    private static GuidevaultEmailHistoryRecord Clone(GuidevaultEmailHistoryRecord record) => new()
    {
        Id = record.Id,
        Type = record.Type,
        To = record.To,
        Subject = record.Subject,
        TemplateName = record.TemplateName,
        Status = record.Status,
        Message = record.Message,
        SentAt = record.SentAt
    };
    private static string Clean(string? value, string fallback = "") => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}

public sealed class GuidevaultEmailHistoryRecord
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = "Email";
    public string To { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string Status { get; set; } = "Logged";
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class GuidevaultSystemEventStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly object _gate = new();
    private readonly string _path;
    private List<GuidevaultSystemEventRecord> _records;

    public GuidevaultSystemEventStore(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        _records = Load();
        Save();
    }

    public IReadOnlyList<GuidevaultSystemEventRecord> GetEvents(int limit = 100)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        lock (_gate) return _records.OrderByDescending(r => r.CreatedAt).Take(safeLimit).Select(Clone).ToArray();
    }

    public GuidevaultSystemEventRecord Record(GuidevaultSystemEventRecord record)
    {
        lock (_gate)
        {
            record.Id = string.IsNullOrWhiteSpace(record.Id) ? Guid.NewGuid().ToString("N")[..12] : record.Id;
            record.CreatedAt = record.CreatedAt == default ? DateTimeOffset.UtcNow : record.CreatedAt;
            record.Category = Clean(record.Category, "System");
            record.Title = Clean(record.Title, "Guidevault event");
            record.Message = Clean(record.Message);
            record.Source = Clean(record.Source, "server");
            record.ItemId = Clean(record.ItemId);
            record.ItemTitle = Clean(record.ItemTitle);
            _records.Insert(0, record);
            if (_records.Count > 500) _records = _records.Take(500).ToList();
            Save();
            return Clone(record);
        }
    }

    private List<GuidevaultSystemEventRecord> Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<List<GuidevaultSystemEventRecord>>(File.ReadAllText(_path), JsonOptions) ?? new List<GuidevaultSystemEventRecord>();
        }
        catch { }
        return new List<GuidevaultSystemEventRecord>();
    }

    private void Save() => File.WriteAllText(_path, JsonSerializer.Serialize(_records, JsonOptions));
    private static GuidevaultSystemEventRecord Clone(GuidevaultSystemEventRecord record) => new()
    {
        Id = record.Id,
        Category = record.Category,
        Title = record.Title,
        Message = record.Message,
        Source = record.Source,
        ItemId = record.ItemId,
        ItemTitle = record.ItemTitle,
        CreatedAt = record.CreatedAt
    };
    private static string Clean(string? value, string fallback = "") => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}

public sealed class GuidevaultSystemEventRecord
{
    public string Id { get; set; } = string.Empty;
    public string Category { get; set; } = "System";
    public string Title { get; set; } = "Guidevault event";
    public string Message { get; set; } = string.Empty;
    public string Source { get; set; } = "server";
    public string ItemId { get; set; } = string.Empty;
    public string ItemTitle { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}


public sealed class GuidevaultItemReviewStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly object _gate = new();
    private readonly string _path;
    private GuidevaultItemReviewSettings _settings;

    public GuidevaultItemReviewStore(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        _settings = Load();
        _settings.Reviews ??= new List<GuidevaultReviewRecord>();
        Save();
    }

    public IReadOnlyList<GuidevaultReviewRecord> GetPublicForItem(string itemId)
    {
        var cleanItemId = Clean(itemId);
        lock (_gate)
        {
            return _settings.Reviews
                .Where(r => string.Equals(r.ItemId, cleanItemId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(NormalizeVisibility(r.Visibility), "public", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(r => r.UpdatedAt)
                .Select(Clone)
                .ToArray();
        }
    }

    public GuidevaultReviewRecord Upsert(string itemId, GuidevaultReviewRequest payload)
    {
        var cleanItemId = Clean(itemId);
        var user = Clean(payload.User, "local user").ToLowerInvariant();
        var now = DateTimeOffset.UtcNow;
        lock (_gate)
        {
            var review = _settings.Reviews.FirstOrDefault(r => !string.IsNullOrWhiteSpace(payload.Id) && string.Equals(r.Id, payload.Id.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? _settings.Reviews.FirstOrDefault(r => string.Equals(r.ItemId, cleanItemId, StringComparison.OrdinalIgnoreCase) && string.Equals(r.User, user, StringComparison.OrdinalIgnoreCase));

            if (review is null)
            {
                review = new GuidevaultReviewRecord
                {
                    Id = string.IsNullOrWhiteSpace(payload.Id) ? ("review-" + Guid.NewGuid().ToString("N"))[..32] : payload.Id.Trim(),
                    ItemId = cleanItemId,
                    User = user,
                    CreatedAt = now
                };
                _settings.Reviews.Add(review);
            }

            review.ItemId = cleanItemId;
            review.User = user;
            review.UserDisplayName = Clean(payload.UserDisplayName, DisplayNameFromUser(user));
            review.AvatarDataUrl = Limit(Clean(payload.AvatarDataUrl), 300_000);
            review.Title = Limit(Clean(payload.Title), 240);
            review.Kind = Limit(Clean(payload.Kind), 80);
            review.Rating = Math.Clamp(payload.Rating, 1, 5);
            review.Text = Limit(Clean(payload.Text), 2000);
            review.Visibility = NormalizeVisibility(payload.Visibility);
            review.UpdatedAt = now;
            Save();
            return Clone(review);
        }
    }

    public bool Delete(string reviewId, string? user)
    {
        var id = Clean(reviewId);
        var userKey = Clean(user).ToLowerInvariant();
        lock (_gate)
        {
            var before = _settings.Reviews.Count;
            _settings.Reviews = _settings.Reviews
                .Where(r => !string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrWhiteSpace(userKey) && !string.Equals(r.User, userKey, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            var deleted = _settings.Reviews.Count != before;
            if (deleted) Save();
            return deleted;
        }
    }

    private GuidevaultItemReviewSettings Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<GuidevaultItemReviewSettings>(File.ReadAllText(_path), JsonOptions) ?? new GuidevaultItemReviewSettings();
        }
        catch { }
        return new GuidevaultItemReviewSettings();
    }

    private void Save() => File.WriteAllText(_path, JsonSerializer.Serialize(_settings, JsonOptions));
    private static string Clean(string? value, string fallback = "") => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    private static string Limit(string value, int max) => value.Length <= max ? value : value[..max];
    private static string NormalizeVisibility(string? value) => string.Equals(value, "public", StringComparison.OrdinalIgnoreCase) ? "public" : "private";
    private static string DisplayNameFromUser(string user) => string.IsNullOrWhiteSpace(user) ? "Guidevault user" : user.Split('@')[0];
    private static GuidevaultReviewRecord Clone(GuidevaultReviewRecord review) => new()
    {
        Id = review.Id,
        ItemId = review.ItemId,
        User = review.User,
        UserDisplayName = review.UserDisplayName,
        AvatarDataUrl = review.AvatarDataUrl,
        Title = review.Title,
        Kind = review.Kind,
        Rating = review.Rating,
        Text = review.Text,
        Visibility = NormalizeVisibility(review.Visibility),
        CreatedAt = review.CreatedAt,
        UpdatedAt = review.UpdatedAt
    };
}

public sealed class GuidevaultItemReviewSettings
{
    public List<GuidevaultReviewRecord> Reviews { get; set; } = new();
}

public sealed class GuidevaultReviewRecord
{
    public string Id { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string UserDisplayName { get; set; } = string.Empty;
    public string AvatarDataUrl { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public int Rating { get; set; } = 5;
    public string Text { get; set; } = string.Empty;
    public string Visibility { get; set; } = "private";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class GuidevaultReviewRequest
{
    public string? Id { get; set; }
    public string? User { get; set; }
    public string? UserDisplayName { get; set; }
    public string? AvatarDataUrl { get; set; }
    public string? Title { get; set; }
    public string? Kind { get; set; }
    public int Rating { get; set; } = 5;
    public string? Text { get; set; }
    public string? Visibility { get; set; }
}

public sealed class GuidevaultUsersStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public static readonly string[] DefaultPermissions = ["Login", "Bookmark", "Download", "Read Only", "Change Password", "Change Restriction", "Promote", "Admin"];
    private readonly object _gate = new();
    private readonly string _path;
    private GuidevaultUsersSettings _settings;

    public GuidevaultUsersStore(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        _settings = Load();
        _settings.Users ??= new List<GuidevaultUserRecord>();
        Save();
    }

    public IReadOnlyList<GuidevaultUserRecord> GetUsers()
    {
        lock (_gate) return _settings.Users.OrderBy(u => u.Email, StringComparer.OrdinalIgnoreCase).Select(Clone).ToArray();
    }

    public GuidevaultUserInviteResult Invite(GuidevaultUserInviteRequest payload)
    {
        lock (_gate)
        {
            var email = (payload.Email ?? string.Empty).Trim();
            var user = _settings.Users.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));
            var now = DateTimeOffset.UtcNow;
            if (user is null)
            {
                user = new GuidevaultUserRecord { Id = Guid.NewGuid().ToString("N")[..12], CreatedAt = now };
                _settings.Users.Add(user);
            }
            user.Email = email;
            user.DisplayName = string.IsNullOrWhiteSpace(payload.DisplayName) ? email.Split('@')[0] : payload.DisplayName.Trim();
            user.Role = string.IsNullOrWhiteSpace(payload.Role) ? "Reader" : payload.Role.Trim();
            user.Libraries = (payload.Libraries ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            user.Permissions = NormalizePermissions(payload.Permissions);
            user.AgeRatingRestriction = string.IsNullOrWhiteSpace(payload.AgeRatingRestriction) ? "No Restriction" : payload.AgeRatingRestriction.Trim();
            user.IncludeUnknowns = payload.IncludeUnknowns;
            user.Status = "Invited";
            user.InvitedAt = now;
            user.UpdatedAt = now;
            Save();
            return new GuidevaultUserInviteResult(Clone(user));
        }
    }

    private GuidevaultUsersSettings Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<GuidevaultUsersSettings>(File.ReadAllText(_path), JsonOptions) ?? new GuidevaultUsersSettings();
        }
        catch { }
        return new GuidevaultUsersSettings();
    }

    private void Save() => File.WriteAllText(_path, JsonSerializer.Serialize(_settings, JsonOptions));
    private static string[] NormalizePermissions(IEnumerable<string>? values)
    {
        var selected = (values ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return selected.Length > 0 ? selected : ["Login", "Bookmark", "Read Only"];
    }
    private static GuidevaultUserRecord Clone(GuidevaultUserRecord user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        DisplayName = user.DisplayName,
        Role = user.Role,
        Libraries = user.Libraries ?? Array.Empty<string>(),
        Permissions = user.Permissions ?? Array.Empty<string>(),
        AgeRatingRestriction = user.AgeRatingRestriction,
        IncludeUnknowns = user.IncludeUnknowns,
        Status = user.Status,
        CreatedAt = user.CreatedAt,
        InvitedAt = user.InvitedAt,
        UpdatedAt = user.UpdatedAt
    };
}

public sealed class GuidevaultUsersSettings
{
    public List<GuidevaultUserRecord> Users { get; set; } = new();
}

public sealed class GuidevaultUserRecord
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = "Reader";
    public string[] Libraries { get; set; } = Array.Empty<string>();
    public string[] Permissions { get; set; } = Array.Empty<string>();
    public string AgeRatingRestriction { get; set; } = "No Restriction";
    public bool IncludeUnknowns { get; set; }
    public string Status { get; set; } = "Invited";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? InvitedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class GuidevaultUserInviteRequest
{
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public string? Role { get; set; }
    public string[] Libraries { get; set; } = Array.Empty<string>();
    public string[] Permissions { get; set; } = Array.Empty<string>();
    public string? AgeRatingRestriction { get; set; }
    public bool IncludeUnknowns { get; set; }
}

public sealed record GuidevaultUserInviteResult(GuidevaultUserRecord User);

public sealed class GuidevaultTaskSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly object _gate = new();
    private readonly string _path;
    private GuidevaultTaskScheduleSettings _settings;

    public GuidevaultTaskSettingsStore(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        _settings = Normalize(Load());
        Save();
    }

    public GuidevaultTaskScheduleSettings GetSettings()
    {
        lock (_gate) return Clone(_settings);
    }

    public GuidevaultTaskScheduleSettings Update(GuidevaultTaskScheduleSettings payload)
    {
        lock (_gate)
        {
            _settings = Normalize(payload);
            Save();
            return Clone(_settings);
        }
    }

    private GuidevaultTaskScheduleSettings Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<GuidevaultTaskScheduleSettings>(File.ReadAllText(_path), JsonOptions) ?? new GuidevaultTaskScheduleSettings();
        }
        catch { }
        return new GuidevaultTaskScheduleSettings();
    }

    private void Save() => File.WriteAllText(_path, JsonSerializer.Serialize(_settings, JsonOptions));
    private static GuidevaultTaskScheduleSettings Normalize(GuidevaultTaskScheduleSettings value)
    {
        value ??= new GuidevaultTaskScheduleSettings();
        value.LibraryScan = NormalizeSchedule(value.LibraryScan, "Daily");
        value.GuidevaultBackup = NormalizeSchedule(value.GuidevaultBackup, "Daily");
        value.Cleanup = NormalizeSchedule(value.Cleanup, "Daily");
        value.ReadingListSync = NormalizeSchedule(value.ReadingListSync, "Custom (0 4 * * *)");
        return value;
    }
    private static GuidevaultTaskScheduleSettings Clone(GuidevaultTaskScheduleSettings value) => new()
    {
        LibraryScan = value.LibraryScan,
        GuidevaultBackup = value.GuidevaultBackup,
        Cleanup = value.Cleanup,
        ReadingListSync = value.ReadingListSync
    };
    private static string NormalizeSchedule(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}

public sealed class GuidevaultTaskScheduleSettings
{
    public string LibraryScan { get; set; } = "Daily";
    public string GuidevaultBackup { get; set; } = "Daily";
    public string Cleanup { get; set; } = "Daily";
    public string ReadingListSync { get; set; } = "Custom (0 4 * * *)";
}

public sealed record OpdsAuthResult(bool Success, string Secret, OpdsAuthKey? Key, IResult? Response);
public sealed record OpdsNavigationEntry(string Title, string Description, string Href, string Kind = "acquisition");
public sealed record OpdsPaging(int Page, int PageSize, int Total, int TotalPages, int Skip, int StartIndex, int EndIndex);
public sealed record OpdsKeyCreateRequest(string? Name);
public sealed record OpdsSettingsUpdate(string? ConnectionUrl, string? SelectedKeyId, bool? Enabled);


public sealed class GuidevaultCustomizeSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _path;
    private readonly object _gate = new();
    private GuidevaultCustomizeSettings _settings;

    public GuidevaultCustomizeSettingsStore(string path)
    {
        _path = path;
        _settings = Normalize(Load());
        Save();
    }

    public GuidevaultCustomizeSettings GetSettings()
    {
        lock (_gate) return Clone(_settings);
    }

    public GuidevaultCustomizeSettings SaveSettings(GuidevaultCustomizeSettings payload)
    {
        lock (_gate)
        {
            _settings = Normalize(payload);
            Save();
            return Clone(_settings);
        }
    }

    private GuidevaultCustomizeSettings Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<GuidevaultCustomizeSettings>(File.ReadAllText(_path), JsonOptions) ?? new GuidevaultCustomizeSettings();
        }
        catch
        {
            // Fall through to defaults rather than blocking app startup.
        }
        return new GuidevaultCustomizeSettings();
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(_settings, JsonOptions));
    }

    private static GuidevaultCustomizeSettings Normalize(GuidevaultCustomizeSettings? value)
    {
        value ??= new GuidevaultCustomizeSettings();
        var activeTab = string.Equals(value.ActiveTab, "side-nav", StringComparison.OrdinalIgnoreCase) ? "side-nav" : "home";
        var shelves = (value.HomeShelves ?? new List<string>())
            .Select(v => (v ?? string.Empty).Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (shelves.Count == 0) shelves.Add("recently-added");

        var items = (value.SideNav?.CustomItems ?? new List<GuidevaultCustomSideNavItem>())
            .Select((item, index) => NormalizeItem(item, index))
            .Where(item => !string.IsNullOrWhiteSpace(item.Label) && !string.IsNullOrWhiteSpace(item.Value))
            .GroupBy(item => $"{item.Type}::{item.Label}::{item.Value}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        return new GuidevaultCustomizeSettings
        {
            ActiveTab = activeTab,
            HomeShelves = shelves,
            SideNav = new GuidevaultCustomizeSideNavSettings { CustomItems = items }
        };
    }

    private static GuidevaultCustomSideNavItem NormalizeItem(GuidevaultCustomSideNavItem? item, int index)
    {
        item ??= new GuidevaultCustomSideNavItem();
        var type = (item.Type ?? string.Empty).Trim().ToLowerInvariant();
        if (type is not ("series" or "kind" or "category" or "publisher" or "list" or "search")) type = "series";
        var kindScope = (item.KindScope ?? item.Scope ?? "all").Trim();
        if (kindScope is not ("Manual" or "Strategy Guide" or "Magazine")) kindScope = "all";
        var matchMode = string.Equals(item.MatchMode, "exact", StringComparison.OrdinalIgnoreCase) ? "exact" : "contains";
        var sortMode = (item.SortMode ?? "default").Trim().ToLowerInvariant();
        if (sortMode is not ("default" or "title" or "sequence" or "recent")) sortMode = "default";
        var label = (item.Label ?? item.Value ?? $"Shortcut {index + 1}").Trim();
        var value = (item.Value ?? label).Trim();
        var icon = (item.Icon ?? item.IconPreset ?? string.Empty).Trim();
        if (icon.Length > 4) icon = icon[..4];
        return new GuidevaultCustomSideNavItem
        {
            Id = string.IsNullOrWhiteSpace(item.Id) ? $"custom-nav-{Guid.NewGuid():N}" : item.Id.Trim(),
            Label = label,
            Type = type,
            Value = value,
            KindScope = kindScope,
            MatchMode = matchMode,
            SortMode = sortMode,
            Icon = icon
        };
    }

    private static GuidevaultCustomizeSettings Clone(GuidevaultCustomizeSettings value) => Normalize(new GuidevaultCustomizeSettings
    {
        ActiveTab = value.ActiveTab,
        HomeShelves = value.HomeShelves?.ToList() ?? new List<string>(),
        SideNav = new GuidevaultCustomizeSideNavSettings
        {
            CustomItems = value.SideNav?.CustomItems?.Select(item => new GuidevaultCustomSideNavItem
            {
                Id = item.Id,
                Label = item.Label,
                Type = item.Type,
                Value = item.Value,
                KindScope = item.KindScope,
                Scope = item.Scope,
                MatchMode = item.MatchMode,
                SortMode = item.SortMode,
                Icon = item.Icon,
                IconPreset = item.IconPreset
            }).ToList() ?? new List<GuidevaultCustomSideNavItem>()
        }
    });
}

public sealed class GuidevaultCustomizeSettings
{
    public string ActiveTab { get; set; } = "home";
    public List<string> HomeShelves { get; set; } = new() { "recently-added" };
    public GuidevaultCustomizeSideNavSettings SideNav { get; set; } = new();
}

public sealed class GuidevaultCustomizeSideNavSettings
{
    public List<GuidevaultCustomSideNavItem> CustomItems { get; set; } = new();
}

public sealed class GuidevaultCustomSideNavItem
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = "series";
    public string Value { get; set; } = string.Empty;
    public string KindScope { get; set; } = "all";
    public string? Scope { get; set; }
    public string MatchMode { get; set; } = "contains";
    public string SortMode { get; set; } = "default";
    public string Icon { get; set; } = string.Empty;
    public string? IconPreset { get; set; }
}

public sealed class OpdsSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly object _gate = new();
    private readonly string _path;
    private OpdsSettings _settings;

    public OpdsSettingsStore(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        _settings = Load();
        Save();
    }

    public OpdsClientSettings GetClientSettings(string defaultConnectionUrl)
    {
        lock (_gate)
        {
            EnsureDefaults(defaultConnectionUrl);
            return ToClientSettings(defaultConnectionUrl);
        }
    }

    public bool IsEnabled => _settings.Enabled;

    public OpdsClientSettings UpdateSettings(string? connectionUrl, string? selectedKeyId, bool? enabled, string defaultConnectionUrl)
    {
        lock (_gate)
        {
            EnsureDefaults(defaultConnectionUrl);
            if (!string.IsNullOrWhiteSpace(connectionUrl))
                _settings.ConnectionUrl = StripAuthQuery(connectionUrl.Trim());
            if (enabled is not null)
                _settings.Enabled = enabled.Value;

            var selected = string.IsNullOrWhiteSpace(selectedKeyId) ? string.Empty : selectedKeyId.Trim();
            _settings.SelectedKeyId = _settings.Keys.Any(k => k.Id == selected) ? selected : _settings.Keys.FirstOrDefault()?.Id ?? string.Empty;
            Save();
            return ToClientSettings(defaultConnectionUrl);
        }
    }

    public OpdsClientSettings CreateKey(string? name, string defaultConnectionUrl)
    {
        lock (_gate)
        {
            EnsureDefaults(defaultConnectionUrl);
            var trimmed = string.IsNullOrWhiteSpace(name) ? $"OPDS {_settings.Keys.Count + 1}" : name.Trim();
            var key = new OpdsAuthKey
            {
                Id = $"opds-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Guid.NewGuid():N}"[..37],
                Name = trimmed,
                Secret = GenerateSecret(),
                CreatedAt = DateTimeOffset.UtcNow
            };
            _settings.Keys.Add(key);
            _settings.SelectedKeyId = key.Id;
            Save();
            return ToClientSettings(defaultConnectionUrl);
        }
    }

    public OpdsClientSettings? RotateKey(string id, string defaultConnectionUrl)
    {
        lock (_gate)
        {
            EnsureDefaults(defaultConnectionUrl);
            var key = _settings.Keys.FirstOrDefault(k => k.Id == id);
            if (key is null) return null;
            key.Secret = GenerateSecret();
            key.RotatedAt = DateTimeOffset.UtcNow;
            _settings.SelectedKeyId = key.Id;
            Save();
            return ToClientSettings(defaultConnectionUrl);
        }
    }

    public OpdsClientSettings? DeleteKey(string id, string defaultConnectionUrl)
    {
        lock (_gate)
        {
            EnsureDefaults(defaultConnectionUrl);
            var key = _settings.Keys.FirstOrDefault(k => k.Id == id);
            if (key is null) return null;
            _settings.Keys.Remove(key);
            if (_settings.SelectedKeyId == id)
                _settings.SelectedKeyId = _settings.Keys.FirstOrDefault()?.Id ?? string.Empty;
            Save();
            return ToClientSettings(defaultConnectionUrl);
        }
    }

    public OpdsAuthKey? ValidateKey(string secret, bool touch)
    {
        lock (_gate)
        {
            var key = _settings.Keys.FirstOrDefault(k => !string.IsNullOrWhiteSpace(k.Secret) && SecretEquals(k.Secret, secret));
            if (key is null) return null;
            if (key.ExpiresAt is not null && key.ExpiresAt.Value < DateTimeOffset.UtcNow) return null;
            if (touch)
            {
                key.LastAccessed = DateTimeOffset.UtcNow;
                Save();
            }
            return key;
        }
    }

    private OpdsSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var loaded = JsonSerializer.Deserialize<OpdsSettings>(File.ReadAllText(_path), JsonOptions);
                if (loaded is not null)
                {
                    loaded.Keys ??= new List<OpdsAuthKey>();
                    return loaded;
                }
            }
        }
        catch
        {
            // Fall through to a clean settings file rather than blocking app startup.
        }
        return new OpdsSettings();
    }

    private void EnsureDefaults(string defaultConnectionUrl)
    {
        _settings.Keys ??= new List<OpdsAuthKey>();
        if (string.IsNullOrWhiteSpace(_settings.ConnectionUrl))
            _settings.ConnectionUrl = defaultConnectionUrl;
        if (string.IsNullOrWhiteSpace(_settings.SelectedKeyId) || !_settings.Keys.Any(k => k.Id == _settings.SelectedKeyId))
            _settings.SelectedKeyId = _settings.Keys.FirstOrDefault()?.Id ?? string.Empty;
    }

    private OpdsClientSettings ToClientSettings(string defaultConnectionUrl)
    {
        EnsureDefaults(defaultConnectionUrl);
        return new OpdsClientSettings
        {
            Enabled = _settings.Enabled,
            ConnectionUrl = string.IsNullOrWhiteSpace(_settings.ConnectionUrl) ? defaultConnectionUrl : _settings.ConnectionUrl!,
            SelectedKeyId = _settings.SelectedKeyId ?? string.Empty,
            Keys = _settings.Keys.Select(k => new OpdsClientKey
            {
                Id = k.Id,
                Name = k.Name,
                Secret = k.Secret,
                CreatedAt = k.CreatedAt,
                RotatedAt = k.RotatedAt,
                ExpiresAt = k.ExpiresAt,
                LastAccessed = k.LastAccessed
            }).ToList()
        };
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(_settings, JsonOptions));
    }

    private static bool SecretEquals(string expected, string actual)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual);
        return expectedBytes.Length == actualBytes.Length && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private static string GenerateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return "GV-OPDS-" + Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string StripAuthQuery(string url)
    {
        foreach (var marker in new[] { "auth=", "key=", "token=" })
        {
            var index = url.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0) continue;
            var queryStart = url.LastIndexOf('?', index);
            if (queryStart >= 0) return url[..queryStart];
        }
        return url;
    }
}

public sealed class OpdsSettings
{
    public bool Enabled { get; set; } = true;
    public string? ConnectionUrl { get; set; }
    public string? SelectedKeyId { get; set; }
    public List<OpdsAuthKey> Keys { get; set; } = new();
}

public sealed class OpdsAuthKey
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RotatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? LastAccessed { get; set; }
}

public sealed class OpdsClientSettings
{
    public bool Enabled { get; set; } = true;
    public string ConnectionUrl { get; set; } = string.Empty;
    public string SelectedKeyId { get; set; } = string.Empty;
    public List<OpdsClientKey> Keys { get; set; } = new();
}

public sealed class OpdsClientKey
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RotatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? LastAccessed { get; set; }
}

public sealed class GuidevaultOptions
{
    public string LibraryPath { get; set; } = "data/library";
    public string DatabasePath { get; set; } = "data/guidevault.json";
    public string BrandName { get; set; } = "Guidevault";
    public bool EnableOpdsPlaceholder { get; set; } = true;
    public UpdateOptions Updates { get; set; } = new();
}

public sealed class UpdateOptions
{
    public string StableFeedUrl { get; set; } = "https://api.github.com/repos/Shredder5262/GuideVault/tags";
    public string ReleaseUrl { get; set; } = "https://github.com/Shredder5262/GuideVault/releases";
    public string ReleasePath { get; set; } = "https://github.com/Shredder5262/GuideVault/releases";
    public string PackageUrl { get; set; } = "https://github.com/Shredder5262/GuideVault/pkgs/container/guidevault";
    public string Channel { get; set; } = "stable";
    public string CurrentImage { get; set; } = "ghcr.io/shredder5262/guidevault:latest";
    public int CheckCacheMinutes { get; set; } = 15;
}

public sealed class LibrarySettings
{
    public string? LibraryPath { get; set; }
    public List<LibraryDefinition> Libraries { get; set; } = new();

    public LibrarySettings Normalize(string contentRoot, string fallbackPath)
    {
        Libraries ??= new List<LibraryDefinition>();
        if (Libraries.Count == 0 && !string.IsNullOrWhiteSpace(LibraryPath))
            Libraries.Add(new LibraryDefinition("Manuals", "Mixed", new List<string> { LibraryPath! }, DateTimeOffset.MinValue));
        if (Libraries.Count == 0)
        {
            LibraryPath = null;
            return this;
        }

        var cleaned = Libraries
            .Where(l => l is not null && (l.Folders?.Count ?? 0) > 0)
            .Select((l, i) => l.Normalize(contentRoot, i))
            .Where(l => l.Folders.Count > 0)
            .ToList();

        Libraries = LibraryDefinitionDeduper.Deduplicate(cleaned);
        LibraryPath = Libraries.FirstOrDefault()?.Folders.FirstOrDefault();
        return this;
    }
}

public sealed record LibraryDefinition(string Name, string Type, List<string> Folders, DateTimeOffset LastScanned)
{
    public LibraryDefinition Normalize(string contentRoot, int index)
    {
        var name = string.IsNullOrWhiteSpace(Name) ? $"Library {index + 1}" : Name.Trim();
        var folders = (Folders ?? new List<string>())
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Select(f => ResolveLibraryPath(contentRoot, f.Trim()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var type = NormalizeType(Type, name, folders);
        return this with { Name = name, Type = type, Folders = folders };
    }

    public static string NormalizeType(string? type, string? name = null, IEnumerable<string>? folders = null)
    {
        var explicitType = CanonicalType(type);
        if (!string.Equals(explicitType, "Mixed", StringComparison.OrdinalIgnoreCase)) return explicitType;

        var hint = string.Join(' ', new[] { name ?? string.Empty }.Concat(folders ?? Array.Empty<string>())).ToLowerInvariant();
        if (Regex.IsMatch(hint, @"\b(magazine|magazines|issues|publication|publications)\b", RegexOptions.IgnoreCase)) return "Magazines";
        if (Regex.IsMatch(hint, @"\b(strategy\s*guides?|walkthroughs?|solution\s*guides?|hint\s*books?)\b", RegexOptions.IgnoreCase)) return "Strategy Guides";
        if (Regex.IsMatch(hint, @"\b(manuals?|instruction\s*booklets?|game\s*program\s*instructions?)\b", RegexOptions.IgnoreCase)) return "Manuals";
        return "Mixed";
    }

    private static string CanonicalType(string? type)
    {
        var value = (type ?? string.Empty).Trim().ToLowerInvariant();
        return value switch
        {
            "manual" or "manuals" => "Manuals",
            "strategy guide" or "strategy guides" or "guide" or "guides" => "Strategy Guides",
            "magazine" or "magazines" => "Magazines",
            _ => "Mixed"
        };
    }

    private static string ResolveLibraryPath(string contentRoot, string path)
    {
        if (Path.IsPathRooted(path) || path.StartsWith(@"\\") || System.Text.RegularExpressions.Regex.IsMatch(path, @"^[A-Za-z]:[\\/].*"))
            return Path.GetFullPath(path);
        return Path.GetFullPath(Path.Combine(contentRoot, path));
    }
}

public static class LibraryDefinitionDeduper
{
    public static List<LibraryDefinition> Deduplicate(IEnumerable<LibraryDefinition> libraries)
    {
        var results = new List<LibraryDefinition>();
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var library in libraries ?? Enumerable.Empty<LibraryDefinition>())
        {
            if (library is null) continue;
            var folders = (library.Folders ?? new List<string>())
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Select(NormalizeFolder)
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (folders.Count == 0) continue;

            var normalized = library with { Folders = folders };
            var key = string.Join("|", folders.OrderBy(f => f, StringComparer.OrdinalIgnoreCase));
            if (seen.TryGetValue(key, out var existingIndex))
            {
                // Treat adding the same folder again as editing the existing library.
                // The later entry wins so a user can correct the type/name without
                // indexing the same physical books twice.
                results[existingIndex] = normalized;
            }
            else
            {
                seen[key] = results.Count;
                results.Add(normalized);
            }
        }

        return results;
    }

    public static string NormalizeFolder(string path)
    {
        try
        {
            return Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return (path ?? string.Empty).Trim().TrimEnd('/', '\\');
        }
    }
}

public static class LibrarySettingsJson
{
    public static List<LibraryDefinition> ParseLibraries(JsonElement payload, string contentRoot)
    {
        var librariesElement = TryGet(payload, "libraries", "Libraries");
        if (librariesElement.ValueKind != JsonValueKind.Array)
            return new List<LibraryDefinition>();

        var results = new List<LibraryDefinition>();
        var index = 0;
        foreach (var lib in librariesElement.EnumerateArray())
        {
            var name = GetString(lib, "name", "Name");
            var type = GetString(lib, "type", "Type");
            var folders = new List<string>();

            var folder = GetString(lib, "folder", "Folder");
            if (!string.IsNullOrWhiteSpace(folder)) folders.Add(folder.Trim());

            var foldersElement = TryGet(lib, "folders", "Folders");
            if (foldersElement.ValueKind == JsonValueKind.Array)
            {
                folders.AddRange(foldersElement.EnumerateArray()
                    .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : null)
                    .Where(x => !string.IsNullOrWhiteSpace(x))!
                    .Select(x => x!.Trim()));
            }

            folders = folders
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(f => ResolveLibraryPath(contentRoot, f))
                .ToList();

            if (folders.Count == 0)
                continue;

            name = string.IsNullOrWhiteSpace(name) ? $"Library {index + 1}" : name.Trim();
            type = LibraryDefinition.NormalizeType(type, name, folders);
            results.Add(new LibraryDefinition(name, type, folders, DateTimeOffset.UtcNow));
            index++;
        }

        return LibraryDefinitionDeduper.Deduplicate(results);
    }

    private static JsonElement TryGet(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value))
                return value;
        }
        return default;
    }

    private static string? GetString(JsonElement element, params string[] names)
    {
        var value = TryGet(element, names);
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static string ResolveLibraryPath(string contentRoot, string path)
    {
        if (Path.IsPathRooted(path) || path.StartsWith(@"\\") || System.Text.RegularExpressions.Regex.IsMatch(path, @"^[A-Za-z]:[\\/].*"))
            return Path.GetFullPath(path);
        return Path.GetFullPath(Path.Combine(contentRoot, path));
    }
}

public static class LibrarySettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static LibrarySettings? Load(string path)
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<LibrarySettings>(File.ReadAllText(path)) : null;
        }
        catch
        {
            return null;
        }
    }

    public static void Save(string path, LibrarySettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(settings, JsonOptions));
    }
}


public sealed record ItemCoverOverrideRecord(
    string ItemId,
    string EntryKey,
    int PageIndex,
    string DisplayName,
    DateTimeOffset UpdatedAt,
    string Source = "manual");

public sealed class ItemCoverOverrideStore
{
    private readonly string _path;
    private readonly object _gate = new();
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private Dictionary<string, ItemCoverOverrideRecord> _overrides;

    public ItemCoverOverrideStore(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        _overrides = Load();
    }

    public ItemCoverOverrideRecord? Get(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        lock (_gate)
        {
            return _overrides.TryGetValue(id.Trim(), out var record) ? record : null;
        }
    }

    public ItemCoverOverrideRecord Set(string id, string entryKey, int pageIndex, string? displayName = null)
    {
        var normalizedId = (id ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedId)) throw new ArgumentException("Item id is required.", nameof(id));
        var normalizedEntry = (entryKey ?? string.Empty).Replace('\\', '/').Trim();
        if (string.IsNullOrWhiteSpace(normalizedEntry)) throw new ArgumentException("Cover entry is required.", nameof(entryKey));

        var record = new ItemCoverOverrideRecord(
            normalizedId,
            normalizedEntry,
            Math.Max(0, pageIndex),
            string.IsNullOrWhiteSpace(displayName) ? Path.GetFileName(normalizedEntry) : displayName.Trim(),
            DateTimeOffset.UtcNow,
            "manual");

        lock (_gate)
        {
            _overrides[normalizedId] = record;
            Persist();
        }

        return record;
    }

    public void Remove(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        lock (_gate)
        {
            if (_overrides.Remove(id.Trim())) Persist();
        }
    }

    private Dictionary<string, ItemCoverOverrideRecord> Load()
    {
        try
        {
            if (!File.Exists(_path)) return new(StringComparer.OrdinalIgnoreCase);
            var loaded = JsonSerializer.Deserialize<Dictionary<string, ItemCoverOverrideRecord>>(File.ReadAllText(_path), _jsonOptions)
                ?? new Dictionary<string, ItemCoverOverrideRecord>(StringComparer.OrdinalIgnoreCase);
            return new Dictionary<string, ItemCoverOverrideRecord>(loaded, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, ItemCoverOverrideRecord>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void Persist() => File.WriteAllText(_path, JsonSerializer.Serialize(_overrides, _jsonOptions));
}

public sealed record LibraryItem(
    string Id,
    string Title,
    string Kind,
    string Format,
    string Path,
    string RelativePath,
    string FileName,
    long SizeBytes,
    DateTimeOffset Added,
    DateTimeOffset Modified,
    int PageCount,
    string System,
    string Category,
    string Publisher,
    string Year,
    string ContentType,
    string[] Tags,
    string Summary,
    string Series,
    string Writer,
    string IssueNumber,
    string Rating,
    string WebLink,
    string Asin,
    string Isbn10,
    string Isbn13,
    string LanguageTag,
    string[] AssociatedPlatforms,
    string PlatformMatchTitle,
    string PlatformResolverSource,
    double PlatformResolverConfidence,
    string MagazineTitle,
    string Volume,
    string CoverDate,
    string PublicationDate,
    string Region,
    string PlatformFocus,
    string PrimarySystem,
    string MagazineCategory,
    string CoverSubject,
    string[] FeaturedGames,
    string[] FeaturedPlatforms,
    string[] SpecialFeatures,
    string[] IncludedExtras,
    string LibraryName,
    string LibraryType,
    string Notes,
    string ValidationStatus = "ok",
    string ValidationMessage = "",
    bool HasReadablePages = true,
    string GameTitle = "",
    string GuideType = "",
    string Edition = "",
    string Franchise = "",
    string Developer = "",
    string GamePublisher = "",
    string GameReleaseYear = "",
    string Genre = "",
    string[]? CoveredGames = null,
    string[]? CoveredPlatforms = null,
    string[]? GuideTopics = null,
    string[]? CharactersCovered = null,
    string[]? LocationsCovered = null,
    string ManualTitle = "",
    string ManualType = "",
    string[]? IncludedSections = null,
    string ControlScheme = "",
    string[]? ItemsCovered = null,
    string WarrantySupport = "",
    string MetadataSource = "",
    string BarcodeUpcIssn = "",
    string MetadataStatus = "Unreviewed",
    Dictionary<string, MetadataFieldLock>? MetadataLocks = null);

public sealed record MetadataFieldLock(
    bool Locked = true,
    DateTimeOffset? LockedAt = null,
    string? LockedBy = null,
    string? Reason = null,
    string? Source = null);


public sealed record ItemMetadataUpdate(
    string? Title = null,
    string? Kind = null,
    string? System = null,
    string? Category = null,
    string? Publisher = null,
    string? Year = null,
    string[]? Tags = null,
    string? Summary = null,
    string? Series = null,
    string? Writer = null,
    string? IssueNumber = null,
    string? Rating = null,
    string? WebLink = null,
    string? Asin = null,
    string? Isbn10 = null,
    string? Isbn13 = null,
    string? LanguageTag = null,
    string[]? AssociatedPlatforms = null,
    string? PlatformMatchTitle = null,
    string? PlatformResolverSource = null,
    double? PlatformResolverConfidence = null,
    string? MagazineTitle = null,
    string? Volume = null,
    string? CoverDate = null,
    string? PublicationDate = null,
    string? Region = null,
    string? PlatformFocus = null,
    string? PrimarySystem = null,
    string? MagazineCategory = null,
    string? CoverSubject = null,
    string? BarcodeUpcIssn = null,
    string[]? FeaturedGames = null,
    string[]? FeaturedPlatforms = null,
    string[]? SpecialFeatures = null,
    string[]? IncludedExtras = null,
    string? GameTitle = null,
    string? GuideType = null,
    string? Edition = null,
    string? Franchise = null,
    string? Developer = null,
    string? GamePublisher = null,
    string? GameReleaseYear = null,
    string? Genre = null,
    string[]? CoveredGames = null,
    string[]? CoveredPlatforms = null,
    string[]? GuideTopics = null,
    string[]? CharactersCovered = null,
    string[]? LocationsCovered = null,
    string? ManualTitle = null,
    string? ManualType = null,
    string[]? IncludedSections = null,
    string? ControlScheme = null,
    string[]? ItemsCovered = null,
    string? WarrantySupport = null,
    int? PageCount = null,
    string? MetadataSource = null,
    string? MetadataStatus = null,
    string? Notes = null,
    bool? Removed = null,
    Dictionary<string, MetadataFieldLock>? MetadataLocks = null);

public static class ItemMetadataJsonReader
{
    public static ItemMetadataUpdate Read(JsonElement payload)
    {
        var isbn10 = GetString(payload, "isbn10");
        var isbn13 = GetString(payload, "isbn13");
        var combinedIsbn = GetString(payload, "isbn");
        if ((!HasText(isbn10) || !HasText(isbn13)) && HasText(combinedIsbn))
        {
            var split = SplitIsbn(combinedIsbn);
            if (!HasText(isbn10)) isbn10 = split.Isbn10;
            if (!HasText(isbn13)) isbn13 = split.Isbn13;
        }

        var title = FirstText(GetString(payload, "title"), GetString(payload, "strategyGuideTitle"), GetString(payload, "name"));
        var language = FirstText(GetString(payload, "languageTag"), GetString(payload, "language"));
        var edition = FirstText(GetString(payload, "edition"), GetString(payload, "editionType"));
        var franchise = FirstText(GetString(payload, "franchise"), GetString(payload, "gameFranchise"));
        var developer = FirstText(GetString(payload, "developer"), GetString(payload, "gameDeveloper"));
        var coverSubject = FirstText(GetString(payload, "coverSubject"), GetString(payload, "coverStory"));
        var issueNumber = FirstText(GetString(payload, "issueNumber"), GetString(payload, "issue"));

        return new ItemMetadataUpdate(
            Title: title,
            Kind: GetString(payload, "kind"),
            System: GetString(payload, "system"),
            Category: GetString(payload, "category"),
            Publisher: GetString(payload, "publisher"),
            Year: GetString(payload, "year"),
            Tags: GetStringArray(payload, "tags"),
            Summary: GetString(payload, "summary"),
            Series: GetString(payload, "series"),
            Writer: GetString(payload, "writer"),
            IssueNumber: issueNumber,
            Rating: GetString(payload, "rating"),
            WebLink: GetString(payload, "webLink"),
            Asin: GetString(payload, "asin"),
            Isbn10: isbn10,
            Isbn13: isbn13,
            LanguageTag: language,
            AssociatedPlatforms: GetStringArray(payload, "associatedPlatforms"),
            PlatformMatchTitle: GetString(payload, "platformMatchTitle"),
            PlatformResolverSource: GetString(payload, "platformResolverSource"),
            PlatformResolverConfidence: GetDouble(payload, "platformResolverConfidence"),
            MagazineTitle: GetString(payload, "magazineTitle"),
            Volume: GetString(payload, "volume"),
            CoverDate: GetString(payload, "coverDate"),
            PublicationDate: GetString(payload, "publicationDate"),
            Region: GetString(payload, "region"),
            PlatformFocus: GetString(payload, "platformFocus"),
            PrimarySystem: GetString(payload, "primarySystem"),
            MagazineCategory: GetString(payload, "magazineCategory"),
            CoverSubject: coverSubject,
            BarcodeUpcIssn: FirstText(GetString(payload, "barcodeUpcIssn"), GetString(payload, "barcode"), GetString(payload, "upc"), GetString(payload, "issn")),
            FeaturedGames: GetStringArray(payload, "featuredGames"),
            FeaturedPlatforms: GetStringArray(payload, "featuredPlatforms"),
            SpecialFeatures: GetStringArray(payload, "specialFeatures"),
            IncludedExtras: GetStringArray(payload, "includedExtras"),
            GameTitle: GetString(payload, "gameTitle"),
            GuideType: GetString(payload, "guideType"),
            Edition: edition,
            Franchise: franchise,
            Developer: developer,
            GamePublisher: GetString(payload, "gamePublisher"),
            GameReleaseYear: GetString(payload, "gameReleaseYear"),
            Genre: GetString(payload, "genre"),
            CoveredGames: GetStringArray(payload, "coveredGames"),
            CoveredPlatforms: GetStringArray(payload, "coveredPlatforms"),
            GuideTopics: GetStringArray(payload, "guideTopics"),
            CharactersCovered: GetStringArray(payload, "charactersCovered"),
            LocationsCovered: GetStringArray(payload, "locationsCovered"),
            ManualTitle: GetString(payload, "manualTitle"),
            ManualType: GetString(payload, "manualType"),
            IncludedSections: GetStringArray(payload, "includedSections"),
            ControlScheme: GetString(payload, "controlScheme"),
            ItemsCovered: GetStringArray(payload, "itemsCovered"),
            WarrantySupport: GetString(payload, "warrantySupport"),
            PageCount: FirstInt(GetInt(payload, "pageCount"), GetInt(payload, "metadataPageCount")),
            MetadataSource: GetString(payload, "metadataSource"),
            MetadataStatus: MetadataStatusHelper.NormalizeOrNull(FirstText(GetString(payload, "metadataStatus"), GetString(payload, "metadataReviewStatus"), GetString(payload, "reviewStatus"))),
            Notes: GetString(payload, "notes"),
            Removed: GetBool(payload, "removed"),
            MetadataLocks: GetMetadataLocks(payload, "metadataLocks"));
    }

    private static Dictionary<string, MetadataFieldLock>? GetMetadataLocks(JsonElement json, string camelName)
    {
        if (!TryGetProperty(json, camelName, out var value)) return null;
        var locks = new Dictionary<string, MetadataFieldLock>(StringComparer.OrdinalIgnoreCase);
        if (value.ValueKind != JsonValueKind.Object) return locks;

        foreach (var property in value.EnumerateObject())
        {
            var key = property.Name?.Trim();
            if (string.IsNullOrWhiteSpace(key)) continue;
            var entry = property.Value;
            if (entry.ValueKind == JsonValueKind.False)
            {
                locks[key] = new MetadataFieldLock(Locked: false);
                continue;
            }
            if (entry.ValueKind == JsonValueKind.True)
            {
                locks[key] = new MetadataFieldLock(Locked: true, LockedAt: DateTimeOffset.UtcNow, Source: "import");
                continue;
            }
            if (entry.ValueKind != JsonValueKind.Object) continue;

            var locked = true;
            if (TryGetProperty(entry, "locked", out var lockedElement))
            {
                locked = lockedElement.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.String when bool.TryParse(lockedElement.GetString(), out var parsed) => parsed,
                    _ => true
                };
            }

            DateTimeOffset? lockedAt = null;
            if (TryGetProperty(entry, "lockedAt", out var lockedAtElement)
                && lockedAtElement.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(lockedAtElement.GetString(), out var parsedDate))
            {
                lockedAt = parsedDate;
            }

            locks[key] = new MetadataFieldLock(
                Locked: locked,
                LockedAt: lockedAt ?? (locked ? DateTimeOffset.UtcNow : null),
                LockedBy: GetString(entry, "lockedBy"),
                Reason: GetString(entry, "reason"),
                Source: GetString(entry, "source"));
        }

        return locks;
    }

    private static string? GetString(JsonElement json, string camelName)
    {
        if (!TryGetProperty(json, camelName, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.Array => string.Join(", ", value.EnumerateArray()
                .Select(v => v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString())
                .Where(HasText)
                .Select(v => v!.Trim())),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => string.Empty,
            _ => value.ToString()
        };
    }

    private static string[]? GetStringArray(JsonElement json, string camelName)
    {
        if (!TryGetProperty(json, camelName, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Array)
        {
            return value.EnumerateArray()
                .Select(v => v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString())
                .Where(HasText)
                .Select(v => v!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString()?.Split(new[] { ',', ';', '|' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? Array.Empty<string>();
        }
        return Array.Empty<string>();
    }

    private static int? GetInt(JsonElement json, string camelName)
    {
        if (!TryGetProperty(json, camelName, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return Math.Max(0, number);
        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed)) return Math.Max(0, parsed);
        return null;
    }

    private static int? FirstInt(params int?[] values)
        => values.FirstOrDefault(v => v.HasValue);

    private static bool? GetBool(JsonElement json, string camelName)
    {
        if (!TryGetProperty(json, camelName, out var value)) return null;
        if (value.ValueKind == JsonValueKind.True) return true;
        if (value.ValueKind == JsonValueKind.False) return false;
        if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed)) return parsed;
        return null;
    }

    private static double? GetDouble(JsonElement json, string camelName)
    {
        if (!TryGetProperty(json, camelName, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number)) return number;
        if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), out var parsed)) return parsed;
        return null;
    }

    private static bool TryGetProperty(JsonElement json, string camelName, out JsonElement value)
    {
        if (json.ValueKind == JsonValueKind.Object)
        {
            if (json.TryGetProperty(camelName, out value)) return true;
            var pascal = char.ToUpperInvariant(camelName[0]) + camelName[1..];
            if (json.TryGetProperty(pascal, out value)) return true;
            foreach (var property in json.EnumerateObject())
            {
                if (property.Name.Equals(camelName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }
        value = default;
        return false;
    }

    private static string? FirstText(params string?[] values)
        => values.FirstOrDefault(HasText)?.Trim();

    private static (string Isbn10, string Isbn13) SplitIsbn(string? value)
    {
        var parts = (value ?? string.Empty)
            .Split(new[] { ',', ';', '|', '/' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => new string(v.Where(c => char.IsDigit(c) || c == 'X' || c == 'x').ToArray()).ToUpperInvariant())
            .Where(HasText)
            .ToArray();
        var isbn10 = parts.FirstOrDefault(v => v.Length == 10) ?? string.Empty;
        var isbn13 = parts.FirstOrDefault(v => v.Length == 13) ?? string.Empty;
        return (isbn10, isbn13);
    }

    private static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);
}

public sealed record BulkMetadataUpdateRequest(string Id, ItemMetadataUpdate Update, bool OverwriteLockedFields = false);

public static class BulkMetadataJsonReader
{
    public static List<BulkMetadataUpdateRequest> Read(JsonElement payload)
    {
        var overwriteLockedFields = GetBool(payload, "overwriteLockedFields") ?? false;
        var updates = ResolveUpdatesArray(payload);
        if (updates.ValueKind != JsonValueKind.Array) return new List<BulkMetadataUpdateRequest>();

        var results = new List<BulkMetadataUpdateRequest>();
        foreach (var entry in updates.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object) continue;

            var id = FirstText(GetString(entry, "id"), GetString(entry, "itemId"), GetString(entry, "Id"));
            if (string.IsNullOrWhiteSpace(id)) continue;

            var rowOverwriteLockedFields = GetBool(entry, "overwriteLockedFields") ?? overwriteLockedFields;
            var metadataPayload = ResolveMetadataPayload(entry);
            if (metadataPayload.ValueKind != JsonValueKind.Object) continue;

            results.Add(new BulkMetadataUpdateRequest(id.Trim(), ItemMetadataJsonReader.Read(metadataPayload), rowOverwriteLockedFields));
        }

        return results;
    }

    private static JsonElement ResolveUpdatesArray(JsonElement payload)
    {
        if (payload.ValueKind == JsonValueKind.Array) return payload;
        if (payload.ValueKind == JsonValueKind.Object)
        {
            if (TryGetProperty(payload, "updates", out var updates) && updates.ValueKind == JsonValueKind.Array) return updates;
            if (TryGetProperty(payload, "items", out var items) && items.ValueKind == JsonValueKind.Array) return items;
        }
        return default;
    }

    private static JsonElement ResolveMetadataPayload(JsonElement entry)
    {
        if (TryGetProperty(entry, "payload", out var payload) && payload.ValueKind == JsonValueKind.Object) return payload;
        if (TryGetProperty(entry, "metadata", out var metadata) && metadata.ValueKind == JsonValueKind.Object) return metadata;
        return entry;
    }

    private static string? GetString(JsonElement json, string camelName)
    {
        if (!TryGetProperty(json, camelName, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static bool? GetBool(JsonElement json, string camelName)
    {
        if (!TryGetProperty(json, camelName, out var value)) return null;
        if (value.ValueKind == JsonValueKind.True) return true;
        if (value.ValueKind == JsonValueKind.False) return false;
        if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed)) return parsed;
        return null;
    }

    private static bool TryGetProperty(JsonElement json, string camelName, out JsonElement value)
    {
        if (json.ValueKind == JsonValueKind.Object)
        {
            if (json.TryGetProperty(camelName, out value)) return true;
            if (!string.IsNullOrWhiteSpace(camelName))
            {
                var pascal = char.ToUpperInvariant(camelName[0]) + camelName[1..];
                if (json.TryGetProperty(pascal, out value)) return true;
            }
            foreach (var property in json.EnumerateObject())
            {
                if (property.Name.Equals(camelName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }
        value = default;
        return false;
    }

    private static string? FirstText(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
}

public static class MetadataIdListJsonReader
{
    public static List<string> Read(JsonElement payload)
    {
        var ids = new List<string>();

        void AddId(JsonElement value)
        {
            var id = value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.ToString(),
                _ => null
            };
            if (!string.IsNullOrWhiteSpace(id)) ids.Add(id.Trim());
        }

        if (payload.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in payload.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.Object)
                {
                    if (TryGetProperty(entry, "id", out var idValue) || TryGetProperty(entry, "itemId", out idValue) || TryGetProperty(entry, "Id", out idValue))
                        AddId(idValue);
                }
                else
                {
                    AddId(entry);
                }
            }
        }
        else if (payload.ValueKind == JsonValueKind.Object)
        {
            if (TryGetProperty(payload, "ids", out var idsElement) && idsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in idsElement.EnumerateArray()) AddId(entry);
            }
            else if (TryGetProperty(payload, "updates", out var updates) && updates.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in updates.EnumerateArray())
                {
                    if (entry.ValueKind == JsonValueKind.Object && (TryGetProperty(entry, "id", out var idValue) || TryGetProperty(entry, "itemId", out idValue) || TryGetProperty(entry, "Id", out idValue)))
                        AddId(idValue);
                }
            }
        }

        return ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool TryGetProperty(JsonElement json, string camelName, out JsonElement value)
    {
        if (json.ValueKind == JsonValueKind.Object)
        {
            if (json.TryGetProperty(camelName, out value)) return true;
            foreach (var property in json.EnumerateObject())
            {
                if (property.Name.Equals(camelName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }
        value = default;
        return false;
    }
}

public sealed record FileIdentityRecord(string ItemId, string Path, string PreviousPath, DateTimeOffset RenamedAt);

public sealed class FileIdentityStore
{
    private readonly string _path;
    private readonly object _gate = new();
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private Dictionary<string, FileIdentityRecord> _records;

    public FileIdentityStore(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        _records = Load();
    }

    public string? GetItemId(string path)
    {
        var key = NormalizePath(path);
        if (string.IsNullOrWhiteSpace(key)) return null;
        lock (_gate)
        {
            return _records.TryGetValue(key, out var record) && !string.IsNullOrWhiteSpace(record.ItemId)
                ? record.ItemId
                : null;
        }
    }

    public void RememberRename(string oldPath, string newPath, string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || string.IsNullOrWhiteSpace(newPath)) return;
        var oldKey = NormalizePath(oldPath);
        var newKey = NormalizePath(newPath);
        if (string.IsNullOrWhiteSpace(newKey)) return;
        lock (_gate)
        {
            if (!string.IsNullOrWhiteSpace(oldKey) && !string.Equals(oldKey, newKey, StringComparison.OrdinalIgnoreCase))
                _records.Remove(oldKey);
            _records[newKey] = new FileIdentityRecord(itemId, newPath, oldPath, DateTimeOffset.UtcNow);
            Persist();
        }
    }

    private Dictionary<string, FileIdentityRecord> Load()
    {
        try
        {
            if (!File.Exists(_path)) return new(StringComparer.OrdinalIgnoreCase);
            var records = JsonSerializer.Deserialize<Dictionary<string, FileIdentityRecord>>(File.ReadAllText(_path), _jsonOptions)
                ?? new Dictionary<string, FileIdentityRecord>(StringComparer.OrdinalIgnoreCase);
            return new Dictionary<string, FileIdentityRecord>(records, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, FileIdentityRecord>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void Persist()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(_records, _jsonOptions));
        }
        catch
        {
            // File identity mapping is best-effort. The active cache is still updated immediately.
        }
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return string.Empty;
        }
    }
}




public static class MetadataStatusHelper
{
    public const string Unreviewed = "Unreviewed";
    public const string NeedsReview = "Needs Review";
    public const string Reviewed = "Reviewed";
    public const string Locked = "Locked";
    public const string FailedLookup = "Failed Lookup";
    public const string ManualOnly = "Manual Only";

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["unreviewed"] = Unreviewed,
        ["unscanned"] = Unreviewed,
        ["new"] = Unreviewed,
        ["needsreview"] = NeedsReview,
        ["needs review"] = NeedsReview,
        ["review"] = NeedsReview,
        ["needs-review"] = NeedsReview,
        ["partial"] = NeedsReview,
        ["partial match"] = NeedsReview,
        ["reviewed"] = Reviewed,
        ["verified"] = Reviewed,
        ["complete"] = Reviewed,
        ["locked"] = Locked,
        ["protected"] = Locked,
        ["failedlookup"] = FailedLookup,
        ["failed lookup"] = FailedLookup,
        ["lookup failed"] = FailedLookup,
        ["no match"] = FailedLookup,
        ["no-match"] = FailedLookup,
        ["manualonly"] = ManualOnly,
        ["manual only"] = ManualOnly,
        ["manual-only"] = ManualOnly,
        ["manual"] = ManualOnly
    };

    public static string Normalize(string? value, string fallback = Unreviewed)
        => NormalizeOrNull(value) ?? (string.IsNullOrWhiteSpace(fallback) ? Unreviewed : fallback.Trim());

    public static string? NormalizeOrNull(string? value)
    {
        var text = value?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (Aliases.TryGetValue(text, out var match)) return match;
        var compact = Regex.Replace(text.ToLowerInvariant(), @"[^a-z0-9]+", string.Empty);
        return Aliases.TryGetValue(compact, out match) ? match : text;
    }
}

public static class MetadataPayloadOptions
{
    public static bool OverwriteLockedFields(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object) return false;
        if (!TryGet(payload, "overwriteLockedFields", out var value)) return false;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => false
        };
    }

    private static bool TryGet(JsonElement json, string name, out JsonElement value)
    {
        if (json.TryGetProperty(name, out value)) return true;
        var pascal = char.ToUpperInvariant(name[0]) + name[1..];
        if (json.TryGetProperty(pascal, out value)) return true;
        foreach (var property in json.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }
        value = default;
        return false;
    }
}

public static class MetadataLockHelper
{
    public static Dictionary<string, MetadataFieldLock>? NormalizeLocks(Dictionary<string, MetadataFieldLock>? locks)
    {
        if (locks is null) return null;
        var normalized = new Dictionary<string, MetadataFieldLock>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in locks)
        {
            var key = pair.Key?.Trim();
            if (string.IsNullOrWhiteSpace(key)) continue;
            var entry = pair.Value;
            if (!entry.Locked) continue;
            normalized[key] = entry with
            {
                Locked = true,
                LockedAt = entry.LockedAt ?? DateTimeOffset.UtcNow,
                Source = string.IsNullOrWhiteSpace(entry.Source) ? "manual" : entry.Source
            };
        }
        return normalized;
    }

    public static Dictionary<string, MetadataFieldLock>? MergeLocks(Dictionary<string, MetadataFieldLock>? existing, Dictionary<string, MetadataFieldLock>? incoming)
    {
        if (incoming is null) return NormalizeLocks(existing);
        var merged = NormalizeLocks(existing) ?? new Dictionary<string, MetadataFieldLock>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in incoming)
        {
            var key = pair.Key?.Trim();
            if (string.IsNullOrWhiteSpace(key)) continue;
            if (!pair.Value.Locked)
            {
                merged.Remove(key);
                continue;
            }
            merged[key] = pair.Value with
            {
                Locked = true,
                LockedAt = pair.Value.LockedAt ?? DateTimeOffset.UtcNow,
                Source = string.IsNullOrWhiteSpace(pair.Value.Source) ? "manual" : pair.Value.Source
            };
        }
        return merged;
    }

    public static bool IsLocked(Dictionary<string, MetadataFieldLock>? locks, params string[] keys)
    {
        if (locks is null || locks.Count == 0) return false;
        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key)) continue;
            if (locks.TryGetValue(key, out var fieldLock) && fieldLock.Locked) return true;
        }
        return false;
    }

    public static bool ShouldRespectLocks(ItemMetadataUpdate update)
    {
        var source = update.MetadataSource?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(source)) return false;
        return !source.Contains("Manual edit", StringComparison.OrdinalIgnoreCase);
    }

    public static ItemMetadataUpdate FilterLockedFields(ItemMetadataUpdate update, Dictionary<string, MetadataFieldLock>? locks)
    {
        if (locks is null || locks.Count == 0) return update;
        string? S(string key, string? value, params string[] aliases) => value is null || !IsLocked(locks, new[] { key }.Concat(aliases).ToArray()) ? value : null;
        string[]? A(string key, string[]? value, params string[] aliases) => value is null || !IsLocked(locks, new[] { key }.Concat(aliases).ToArray()) ? value : null;
        int? I(string key, int? value, params string[] aliases) => value is null || !IsLocked(locks, new[] { key }.Concat(aliases).ToArray()) ? value : null;
        double? D(string key, double? value, params string[] aliases) => value is null || !IsLocked(locks, new[] { key }.Concat(aliases).ToArray()) ? value : null;

        return new ItemMetadataUpdate(
            Title: S("title", update.Title, "strategyGuideTitle", "manualTitle", "magazineTitle", "name"),
            Kind: S("kind", update.Kind),
            System: S("preferredPlatform", update.System, "system", "category"),
            Category: S("preferredPlatform", update.Category, "category", "system"),
            Publisher: S("publisher", update.Publisher),
            Year: S("year", update.Year, "publishYear"),
            Tags: A("tags", update.Tags),
            Summary: S("summary", update.Summary, "description"),
            Series: S("series", update.Series),
            Writer: S("writer", update.Writer, "authorWriter", "editor"),
            IssueNumber: S("issueNumber", update.IssueNumber, "issue"),
            Rating: S("rating", update.Rating, "esrb"),
            WebLink: S("webLink", update.WebLink),
            Asin: S("asin", update.Asin),
            Isbn10: S("isbn10", update.Isbn10, "isbn"),
            Isbn13: S("isbn13", update.Isbn13, "isbn"),
            LanguageTag: S("languageTag", update.LanguageTag, "language"),
            AssociatedPlatforms: A("associatedPlatforms", update.AssociatedPlatforms),
            PlatformMatchTitle: S("platformMatchTitle", update.PlatformMatchTitle),
            PlatformResolverSource: S("platformResolverSource", update.PlatformResolverSource),
            PlatformResolverConfidence: D("platformResolverConfidence", update.PlatformResolverConfidence),
            MagazineTitle: S("magazineTitle", update.MagazineTitle),
            Volume: S("volume", update.Volume),
            CoverDate: S("coverDate", update.CoverDate),
            PublicationDate: S("publicationDate", update.PublicationDate),
            Region: S("region", update.Region),
            PlatformFocus: S("platformFocus", update.PlatformFocus),
            PrimarySystem: S("primarySystem", update.PrimarySystem),
            MagazineCategory: S("magazineCategory", update.MagazineCategory),
            CoverSubject: S("coverSubject", update.CoverSubject, "coverStory"),
            BarcodeUpcIssn: S("barcodeUpcIssn", update.BarcodeUpcIssn, "barcode", "upc", "issn"),
            FeaturedGames: A("featuredGames", update.FeaturedGames),
            FeaturedPlatforms: A("featuredPlatforms", update.FeaturedPlatforms),
            SpecialFeatures: A("specialFeatures", update.SpecialFeatures, "sections"),
            IncludedExtras: A("includedExtras", update.IncludedExtras, "physicalExtras", "insertDetails"),
            GameTitle: S("gameTitle", update.GameTitle),
            GuideType: S("guideType", update.GuideType),
            Edition: S("edition", update.Edition, "editionType"),
            Franchise: S("franchise", update.Franchise, "gameFranchise"),
            Developer: S("developer", update.Developer, "gameDeveloper"),
            GamePublisher: S("gamePublisher", update.GamePublisher),
            GameReleaseYear: S("gameReleaseYear", update.GameReleaseYear),
            Genre: S("genre", update.Genre),
            CoveredGames: A("coveredGames", update.CoveredGames),
            CoveredPlatforms: A("coveredPlatforms", update.CoveredPlatforms),
            GuideTopics: A("guideTopics", update.GuideTopics),
            CharactersCovered: A("charactersCovered", update.CharactersCovered),
            LocationsCovered: A("locationsCovered", update.LocationsCovered),
            ManualTitle: S("manualTitle", update.ManualTitle),
            ManualType: S("manualType", update.ManualType),
            IncludedSections: A("includedSections", update.IncludedSections),
            ControlScheme: S("controlScheme", update.ControlScheme),
            ItemsCovered: A("itemsCovered", update.ItemsCovered),
            WarrantySupport: S("warrantySupport", update.WarrantySupport),
            PageCount: I("pageCount", update.PageCount, "metadataPageCount"),
            MetadataSource: update.MetadataSource,
            MetadataStatus: update.MetadataStatus,
            Notes: S("notes", update.Notes),
            Removed: update.Removed,
            MetadataLocks: update.MetadataLocks);
    }
}

public sealed class MetadataStore
{
    private readonly string _path;
    private readonly object _gate = new();
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private Dictionary<string, ItemMetadataUpdate> _overrides;

    public MetadataStore(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        _overrides = Load();
    }

    public LibraryItem ApplyOverride(LibraryItem item)
    {
        lock (_gate)
        {
            if (!_overrides.TryGetValue(item.Id, out var o)) return item;
            var kind = First(o.Kind, item.Kind);
            var category = First(o.Category, item.Category);
            var system = First(o.System, item.System);
            return item with
            {
                Title = First(o.Title, item.Title),
                Kind = kind,
                System = system,
                Category = category,
                Publisher = First(o.Publisher, item.Publisher),
                Year = First(o.Year, item.Year),
                Tags = o.Tags is { Length: > 0 } ? o.Tags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() : item.Tags,
                Summary = First(o.Summary, item.Summary),
                Series = First(o.Series, item.Series),
                Writer = First(o.Writer, item.Writer),
                IssueNumber = kind == "Magazine" ? First(o.IssueNumber, item.IssueNumber) : string.Empty,
                Rating = First(o.Rating, item.Rating),
                WebLink = First(o.WebLink, item.WebLink),
                Asin = First(o.Asin, item.Asin),
                Isbn10 = First(o.Isbn10, item.Isbn10),
                Isbn13 = First(o.Isbn13, item.Isbn13),
                LanguageTag = First(o.LanguageTag, item.LanguageTag),
                AssociatedPlatforms = o.AssociatedPlatforms is not null ? CleanDistinct(o.AssociatedPlatforms) : item.AssociatedPlatforms,
                PlatformMatchTitle = First(o.PlatformMatchTitle, item.PlatformMatchTitle),
                PlatformResolverSource = First(o.PlatformResolverSource, item.PlatformResolverSource),
                PlatformResolverConfidence = o.PlatformResolverConfidence ?? item.PlatformResolverConfidence,
                MagazineTitle = kind == "Magazine" ? First(o.MagazineTitle, item.MagazineTitle) : string.Empty,
                Volume = kind == "Magazine" ? First(o.Volume, item.Volume) : string.Empty,
                CoverDate = kind == "Magazine" ? First(o.CoverDate, item.CoverDate) : string.Empty,
                PublicationDate = (kind == "Magazine" || kind == "Strategy Guide" || kind == "Manual") ? First(o.PublicationDate, item.PublicationDate) : string.Empty,
                Region = (kind == "Magazine" || kind == "Strategy Guide" || kind == "Manual") ? First(o.Region, item.Region) : string.Empty,
                PlatformFocus = kind == "Magazine" ? First(o.PlatformFocus, item.PlatformFocus) : string.Empty,
                PrimarySystem = kind == "Magazine" ? First(o.PrimarySystem, item.PrimarySystem) : string.Empty,
                MagazineCategory = kind == "Magazine" ? First(o.MagazineCategory, item.MagazineCategory) : string.Empty,
                CoverSubject = kind == "Magazine" ? First(o.CoverSubject, item.CoverSubject) : string.Empty,
                BarcodeUpcIssn = kind == "Magazine" ? First(o.BarcodeUpcIssn, item.BarcodeUpcIssn) : string.Empty,
                FeaturedGames = kind == "Magazine" && o.FeaturedGames is not null ? CleanDistinct(o.FeaturedGames) : item.FeaturedGames,
                FeaturedPlatforms = kind == "Magazine" && o.FeaturedPlatforms is not null ? CleanDistinct(o.FeaturedPlatforms) : item.FeaturedPlatforms,
                SpecialFeatures = (kind == "Magazine" || kind == "Strategy Guide" || kind == "Manual") && o.SpecialFeatures is not null ? CleanDistinct(o.SpecialFeatures) : item.SpecialFeatures,
                IncludedExtras = (kind == "Magazine" || kind == "Strategy Guide" || kind == "Manual") && o.IncludedExtras is not null ? CleanDistinct(o.IncludedExtras) : item.IncludedExtras,
                GameTitle = (kind == "Strategy Guide" || kind == "Manual") ? First(o.GameTitle, item.GameTitle) : string.Empty,
                GuideType = kind == "Strategy Guide" ? First(o.GuideType, item.GuideType) : string.Empty,
                Edition = kind == "Strategy Guide" ? First(o.Edition, item.Edition) : string.Empty,
                Franchise = (kind == "Strategy Guide" || kind == "Manual") ? First(o.Franchise, item.Franchise) : string.Empty,
                Developer = (kind == "Strategy Guide" || kind == "Manual") ? First(o.Developer, item.Developer) : string.Empty,
                GamePublisher = (kind == "Strategy Guide" || kind == "Manual") ? First(o.GamePublisher, item.GamePublisher) : string.Empty,
                GameReleaseYear = (kind == "Strategy Guide" || kind == "Manual") ? First(o.GameReleaseYear, item.GameReleaseYear) : string.Empty,
                Genre = (kind == "Strategy Guide" || kind == "Manual") ? First(o.Genre, item.Genre) : string.Empty,
                CoveredGames = kind == "Strategy Guide" && o.CoveredGames is not null ? CleanDistinct(o.CoveredGames) : (item.CoveredGames ?? []),
                CoveredPlatforms = kind == "Strategy Guide" && o.CoveredPlatforms is not null ? CleanDistinct(o.CoveredPlatforms) : (item.CoveredPlatforms ?? []),
                GuideTopics = kind == "Strategy Guide" && o.GuideTopics is not null ? CleanDistinct(o.GuideTopics) : (item.GuideTopics ?? []),
                CharactersCovered = (kind == "Strategy Guide" || kind == "Manual") && o.CharactersCovered is not null ? CleanDistinct(o.CharactersCovered) : (item.CharactersCovered ?? []),
                LocationsCovered = kind == "Strategy Guide" && o.LocationsCovered is not null ? CleanDistinct(o.LocationsCovered) : (item.LocationsCovered ?? []),
                ManualTitle = kind == "Manual" ? First(o.ManualTitle, item.ManualTitle) : string.Empty,
                ManualType = kind == "Manual" ? First(o.ManualType, item.ManualType) : string.Empty,
                IncludedSections = kind == "Manual" && o.IncludedSections is not null ? CleanDistinct(o.IncludedSections) : (item.IncludedSections ?? []),
                ControlScheme = kind == "Manual" ? First(o.ControlScheme, item.ControlScheme) : string.Empty,
                ItemsCovered = kind == "Manual" && o.ItemsCovered is not null ? CleanDistinct(o.ItemsCovered) : (item.ItemsCovered ?? []),
                WarrantySupport = kind == "Manual" ? First(o.WarrantySupport, item.WarrantySupport) : string.Empty,
                PageCount = o.PageCount.HasValue && o.PageCount.Value > 0 ? o.PageCount.Value : item.PageCount,
                MetadataSource = First(o.MetadataSource, item.MetadataSource),
                MetadataStatus = MetadataStatusHelper.Normalize(o.MetadataStatus, item.MetadataStatus),
                Notes = First(o.Notes, item.Notes),
                MetadataLocks = MetadataLockHelper.NormalizeLocks(o.MetadataLocks ?? item.MetadataLocks)
            };
        }
    }

    public static LibraryItem ApplyUpdateSnapshot(LibraryItem item, ItemMetadataUpdate update)
    {
        var kind = string.IsNullOrWhiteSpace(update.Kind) ? item.Kind : update.Kind.Trim();
        var category = string.IsNullOrWhiteSpace(update.Category) ? item.Category : update.Category.Trim();
        var system = string.IsNullOrWhiteSpace(update.System) ? item.System : update.System.Trim();
        return item with
        {
            Title = Keep(update.Title, item.Title),
            Kind = kind,
            System = system,
            Category = category,
            Publisher = Keep(update.Publisher, item.Publisher),
            Year = Keep(update.Year, item.Year),
            Tags = update.Tags is not null ? CleanDistinct(update.Tags) : item.Tags,
            Summary = Keep(update.Summary, item.Summary),
            Series = Keep(update.Series, item.Series),
            Writer = Keep(update.Writer, item.Writer),
            IssueNumber = kind == "Magazine" ? Keep(update.IssueNumber, item.IssueNumber) : item.IssueNumber,
            Rating = Keep(update.Rating, item.Rating),
            WebLink = Keep(update.WebLink, item.WebLink),
            Asin = Keep(update.Asin, item.Asin),
            Isbn10 = Keep(update.Isbn10, item.Isbn10),
            Isbn13 = Keep(update.Isbn13, item.Isbn13),
            LanguageTag = Keep(update.LanguageTag, item.LanguageTag),
            AssociatedPlatforms = update.AssociatedPlatforms is not null ? CleanDistinct(update.AssociatedPlatforms) : item.AssociatedPlatforms,
            PlatformMatchTitle = Keep(update.PlatformMatchTitle, item.PlatformMatchTitle),
            PlatformResolverSource = Keep(update.PlatformResolverSource, item.PlatformResolverSource),
            PlatformResolverConfidence = update.PlatformResolverConfidence ?? item.PlatformResolverConfidence,
            MagazineTitle = kind == "Magazine" ? Keep(update.MagazineTitle, item.MagazineTitle) : item.MagazineTitle,
            Volume = kind == "Magazine" ? Keep(update.Volume, item.Volume) : item.Volume,
            CoverDate = kind == "Magazine" ? Keep(update.CoverDate, item.CoverDate) : item.CoverDate,
            PublicationDate = (kind == "Magazine" || kind == "Strategy Guide" || kind == "Manual") ? Keep(update.PublicationDate, item.PublicationDate) : item.PublicationDate,
            Region = (kind == "Magazine" || kind == "Strategy Guide" || kind == "Manual") ? Keep(update.Region, item.Region) : item.Region,
            PlatformFocus = kind == "Magazine" ? Keep(update.PlatformFocus, item.PlatformFocus) : item.PlatformFocus,
            PrimarySystem = kind == "Magazine" ? Keep(update.PrimarySystem, item.PrimarySystem) : item.PrimarySystem,
            MagazineCategory = kind == "Magazine" ? Keep(update.MagazineCategory, item.MagazineCategory) : item.MagazineCategory,
            CoverSubject = kind == "Magazine" ? Keep(update.CoverSubject, item.CoverSubject) : item.CoverSubject,
            BarcodeUpcIssn = kind == "Magazine" ? Keep(update.BarcodeUpcIssn, item.BarcodeUpcIssn) : item.BarcodeUpcIssn,
            FeaturedGames = kind == "Magazine" && update.FeaturedGames is not null ? CleanDistinct(update.FeaturedGames) : item.FeaturedGames,
            FeaturedPlatforms = kind == "Magazine" && update.FeaturedPlatforms is not null ? CleanDistinct(update.FeaturedPlatforms) : item.FeaturedPlatforms,
            SpecialFeatures = (kind == "Magazine" || kind == "Strategy Guide" || kind == "Manual") && update.SpecialFeatures is not null ? CleanDistinct(update.SpecialFeatures) : item.SpecialFeatures,
            IncludedExtras = (kind == "Magazine" || kind == "Strategy Guide" || kind == "Manual") && update.IncludedExtras is not null ? CleanDistinct(update.IncludedExtras) : item.IncludedExtras,
            GameTitle = (kind == "Strategy Guide" || kind == "Manual") ? Keep(update.GameTitle, item.GameTitle) : item.GameTitle,
            GuideType = kind == "Strategy Guide" ? Keep(update.GuideType, item.GuideType) : item.GuideType,
            Edition = kind == "Strategy Guide" ? Keep(update.Edition, item.Edition) : item.Edition,
            Franchise = (kind == "Strategy Guide" || kind == "Manual") ? Keep(update.Franchise, item.Franchise) : item.Franchise,
            Developer = (kind == "Strategy Guide" || kind == "Manual") ? Keep(update.Developer, item.Developer) : item.Developer,
            GamePublisher = (kind == "Strategy Guide" || kind == "Manual") ? Keep(update.GamePublisher, item.GamePublisher) : item.GamePublisher,
            GameReleaseYear = (kind == "Strategy Guide" || kind == "Manual") ? Keep(update.GameReleaseYear, item.GameReleaseYear) : item.GameReleaseYear,
            Genre = (kind == "Strategy Guide" || kind == "Manual") ? Keep(update.Genre, item.Genre) : item.Genre,
            CoveredGames = kind == "Strategy Guide" && update.CoveredGames is not null ? CleanDistinct(update.CoveredGames) : item.CoveredGames,
            CoveredPlatforms = kind == "Strategy Guide" && update.CoveredPlatforms is not null ? CleanDistinct(update.CoveredPlatforms) : item.CoveredPlatforms,
            GuideTopics = kind == "Strategy Guide" && update.GuideTopics is not null ? CleanDistinct(update.GuideTopics) : item.GuideTopics,
            CharactersCovered = (kind == "Strategy Guide" || kind == "Manual") && update.CharactersCovered is not null ? CleanDistinct(update.CharactersCovered) : item.CharactersCovered,
            LocationsCovered = kind == "Strategy Guide" && update.LocationsCovered is not null ? CleanDistinct(update.LocationsCovered) : item.LocationsCovered,
            ManualTitle = kind == "Manual" ? Keep(update.ManualTitle, item.ManualTitle) : item.ManualTitle,
            ManualType = kind == "Manual" ? Keep(update.ManualType, item.ManualType) : item.ManualType,
            IncludedSections = kind == "Manual" && update.IncludedSections is not null ? CleanDistinct(update.IncludedSections) : item.IncludedSections,
            ControlScheme = kind == "Manual" ? Keep(update.ControlScheme, item.ControlScheme) : item.ControlScheme,
            ItemsCovered = kind == "Manual" && update.ItemsCovered is not null ? CleanDistinct(update.ItemsCovered) : item.ItemsCovered,
            WarrantySupport = kind == "Manual" ? Keep(update.WarrantySupport, item.WarrantySupport) : item.WarrantySupport,
            PageCount = update.PageCount.HasValue && update.PageCount.Value > 0 ? update.PageCount.Value : item.PageCount,
            MetadataSource = Keep(update.MetadataSource, item.MetadataSource),
            MetadataStatus = MetadataStatusHelper.Normalize(update.MetadataStatus, item.MetadataStatus),
            Notes = Keep(update.Notes, item.Notes),
            MetadataLocks = MetadataLockHelper.MergeLocks(item.MetadataLocks, update.MetadataLocks)
        };
    }

    public ItemMetadataUpdate PrepareIncomingOverride(string id, ItemMetadataUpdate update, bool overwriteLockedFields = false)
    {
        lock (_gate)
        {
            _overrides.TryGetValue(id, out var existing);
            if (!overwriteLockedFields && MetadataLockHelper.ShouldRespectLocks(update))
                return MetadataLockHelper.FilterLockedFields(update, existing?.MetadataLocks);
            return update;
        }
    }

    private static string Keep(string? candidate, string fallback) => string.IsNullOrWhiteSpace(candidate) ? fallback : candidate.Trim();

    public void SaveOverride(string id, ItemMetadataUpdate update)
    {
        lock (_gate)
        {
            _overrides[id] = update;
            Persist();
        }
    }

    public void RemoveOverride(string id)
    {
        lock (_gate)
        {
            if (_overrides.Remove(id)) Persist();
        }
    }

    public bool IsRemoved(string id)
    {
        lock (_gate)
        {
            return _overrides.TryGetValue(id, out var update) && update.Removed == true;
        }
    }

    public void MarkRemoved(string id)
    {
        lock (_gate)
        {
            _overrides[id] = new ItemMetadataUpdate(Removed: true);
            Persist();
        }
    }

    private Dictionary<string, ItemMetadataUpdate> Load()
    {
        try
        {
            return File.Exists(_path)
                ? JsonSerializer.Deserialize<Dictionary<string, ItemMetadataUpdate>>(File.ReadAllText(_path), _jsonOptions) ?? new()
                : new();
        }
        catch { return new(); }
    }

    private void Persist() => File.WriteAllText(_path, JsonSerializer.Serialize(_overrides, _jsonOptions));
    private static string First(string? candidate, string fallback) => string.IsNullOrWhiteSpace(candidate) ? fallback : candidate.Trim();
    private static string[] CleanDistinct(IEnumerable<string> values) => values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public void MergeOverride(string id, ItemMetadataUpdate update)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        lock (_gate)
        {
            _overrides.TryGetValue(id, out var existing);
            _overrides[id] = MergeMetadataUpdate(existing, update);
            Persist();
        }
    }

    public void MergeOverrides(IEnumerable<BulkMetadataUpdateRequest> updates)
    {
        if (updates is null) return;
        lock (_gate)
        {
            var changed = false;
            foreach (var request in updates)
            {
                if (string.IsNullOrWhiteSpace(request.Id)) continue;
                _overrides.TryGetValue(request.Id, out var existing);
                _overrides[request.Id] = MergeMetadataUpdate(existing, request.Update);
                changed = true;
            }

            if (changed) Persist();
        }
    }

    private static ItemMetadataUpdate MergeMetadataUpdate(ItemMetadataUpdate? existing, ItemMetadataUpdate update)
        => new(
            Title: update.Title ?? existing?.Title,
            Kind: update.Kind ?? existing?.Kind,
            System: update.System ?? existing?.System,
            Category: update.Category ?? existing?.Category,
            Publisher: update.Publisher ?? existing?.Publisher,
            Year: update.Year ?? existing?.Year,
            Tags: update.Tags ?? existing?.Tags,
            Summary: update.Summary ?? existing?.Summary,
            Series: update.Series ?? existing?.Series,
            Writer: update.Writer ?? existing?.Writer,
            IssueNumber: update.IssueNumber ?? existing?.IssueNumber,
            Rating: update.Rating ?? existing?.Rating,
            WebLink: update.WebLink ?? existing?.WebLink,
            Asin: update.Asin ?? existing?.Asin,
            Isbn10: update.Isbn10 ?? existing?.Isbn10,
            Isbn13: update.Isbn13 ?? existing?.Isbn13,
            LanguageTag: update.LanguageTag ?? existing?.LanguageTag,
            AssociatedPlatforms: update.AssociatedPlatforms ?? existing?.AssociatedPlatforms,
            PlatformMatchTitle: update.PlatformMatchTitle ?? existing?.PlatformMatchTitle,
            PlatformResolverSource: update.PlatformResolverSource ?? existing?.PlatformResolverSource,
            PlatformResolverConfidence: update.PlatformResolverConfidence ?? existing?.PlatformResolverConfidence,
            MagazineTitle: update.MagazineTitle ?? existing?.MagazineTitle,
            Volume: update.Volume ?? existing?.Volume,
            CoverDate: update.CoverDate ?? existing?.CoverDate,
            PublicationDate: update.PublicationDate ?? existing?.PublicationDate,
            Region: update.Region ?? existing?.Region,
            PlatformFocus: update.PlatformFocus ?? existing?.PlatformFocus,
            PrimarySystem: update.PrimarySystem ?? existing?.PrimarySystem,
            MagazineCategory: update.MagazineCategory ?? existing?.MagazineCategory,
            CoverSubject: update.CoverSubject ?? existing?.CoverSubject,
            BarcodeUpcIssn: update.BarcodeUpcIssn ?? existing?.BarcodeUpcIssn,
            FeaturedGames: update.FeaturedGames ?? existing?.FeaturedGames,
            FeaturedPlatforms: update.FeaturedPlatforms ?? existing?.FeaturedPlatforms,
            SpecialFeatures: update.SpecialFeatures ?? existing?.SpecialFeatures,
            IncludedExtras: update.IncludedExtras ?? existing?.IncludedExtras,
            GameTitle: update.GameTitle ?? existing?.GameTitle,
            GuideType: update.GuideType ?? existing?.GuideType,
            Edition: update.Edition ?? existing?.Edition,
            Franchise: update.Franchise ?? existing?.Franchise,
            Developer: update.Developer ?? existing?.Developer,
            GamePublisher: update.GamePublisher ?? existing?.GamePublisher,
            GameReleaseYear: update.GameReleaseYear ?? existing?.GameReleaseYear,
            Genre: update.Genre ?? existing?.Genre,
            CoveredGames: update.CoveredGames ?? existing?.CoveredGames,
            CoveredPlatforms: update.CoveredPlatforms ?? existing?.CoveredPlatforms,
            GuideTopics: update.GuideTopics ?? existing?.GuideTopics,
            CharactersCovered: update.CharactersCovered ?? existing?.CharactersCovered,
            LocationsCovered: update.LocationsCovered ?? existing?.LocationsCovered,
            ManualTitle: update.ManualTitle ?? existing?.ManualTitle,
            ManualType: update.ManualType ?? existing?.ManualType,
            IncludedSections: update.IncludedSections ?? existing?.IncludedSections,
            ControlScheme: update.ControlScheme ?? existing?.ControlScheme,
            ItemsCovered: update.ItemsCovered ?? existing?.ItemsCovered,
            WarrantySupport: update.WarrantySupport ?? existing?.WarrantySupport,
            PageCount: update.PageCount ?? existing?.PageCount,
            MetadataSource: update.MetadataSource ?? existing?.MetadataSource,
            MetadataStatus: update.MetadataStatus ?? existing?.MetadataStatus,
            Notes: update.Notes ?? existing?.Notes,
            Removed: update.Removed ?? existing?.Removed,
            MetadataLocks: MetadataLockHelper.MergeLocks(existing?.MetadataLocks, update.MetadataLocks));
}



public sealed record FileConversionJob(
    string TaskId,
    string Status,
    string Message,
    int Requested,
    int Converted,
    int Failed,
    IReadOnlyList<object> Results,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed class FileConversionJobStore
{
    private readonly ConcurrentDictionary<string, FileConversionJob> _jobs = new(StringComparer.OrdinalIgnoreCase);

    public FileConversionJob Start(string taskId, int requested, string targetFormat)
    {
        var now = DateTimeOffset.UtcNow;
        var job = new FileConversionJob(taskId, "running", $"Queued {targetFormat} conversion for {requested} file(s).", requested, 0, 0, Array.Empty<object>(), now, now);
        _jobs[taskId] = job;
        return job;
    }

    public void Update(string taskId, string status, string message, int requested, int converted, int failed, IReadOnlyList<object> results)
    {
        var now = DateTimeOffset.UtcNow;
        var created = _jobs.TryGetValue(taskId, out var existing) ? existing.CreatedAt : now;
        _jobs[taskId] = new FileConversionJob(taskId, status, message, requested, converted, failed, results.ToArray(), created, now);
        Prune();
    }

    public FileConversionJob? Get(string taskId)
    {
        Prune();
        return string.IsNullOrWhiteSpace(taskId) ? null : _jobs.TryGetValue(taskId, out var job) ? job : null;
    }

    private void Prune()
    {
        var cutoff = DateTimeOffset.UtcNow.AddHours(-2);
        foreach (var stale in _jobs.Where(kv => kv.Value.UpdatedAt < cutoff && !kv.Value.Status.Equals("running", StringComparison.OrdinalIgnoreCase)).Select(kv => kv.Key).ToArray())
            _jobs.TryRemove(stale, out _);
    }
}

public sealed record GuidevaultTask(string Id, string Kind, string Title, string Status, string Message, int ProgressPercent, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed class TaskMonitor
{
    private readonly ConcurrentDictionary<string, GuidevaultTask> _tasks = new();

    public GuidevaultTask Start(string kind, string title, string message)
    {
        var now = DateTimeOffset.UtcNow;
        var task = new GuidevaultTask(Guid.NewGuid().ToString("N")[..12], kind, title, "running", message, 0, now, now);
        _tasks[task.Id] = task;
        return task;
    }

    public void Update(string id, string message, int progressPercent)
    {
        if (!_tasks.TryGetValue(id, out var task)) return;
        _tasks[id] = task with
        {
            Status = "running",
            Message = message,
            ProgressPercent = Math.Clamp(progressPercent, 0, 99),
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Complete(string id, string message, int progressPercent = 100)
    {
        if (!_tasks.TryGetValue(id, out var task)) return;
        _tasks[id] = task with
        {
            Status = "completed",
            Message = message,
            ProgressPercent = Math.Clamp(progressPercent, 0, 100),
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Fail(string id, string message)
    {
        if (!_tasks.TryGetValue(id, out var task)) return;
        _tasks[id] = task with
        {
            Status = "failed",
            Message = message,
            ProgressPercent = 100,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public int ClearNonRunning()
    {
        var removed = 0;
        foreach (var stale in _tasks.Where(kv => kv.Value.Status != "running" && kv.Value.Status != "queued").Select(kv => kv.Key).ToArray())
        {
            if (_tasks.TryRemove(stale, out _)) removed++;
        }
        return removed;
    }

    public IReadOnlyList<GuidevaultTask> RecentTasks()
    {
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-30);
        foreach (var stale in _tasks.Where(kv => kv.Value.UpdatedAt < cutoff && kv.Value.Status != "running").Select(kv => kv.Key).ToArray())
            _tasks.TryRemove(stale, out _);

        return _tasks.Values
            .OrderByDescending(t => t.Status == "running")
            .ThenByDescending(t => t.UpdatedAt)
            .Take(20)
            .ToArray();
    }
}

public sealed class LibraryCache
{
    private IReadOnlyList<LibraryItem>? _items;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<LibraryDefinition> _libraries;
    private readonly MetadataStore _metadataStore;
    private readonly FileIdentityStore _identityStore;
    private readonly string _cachePath;
    private readonly TaskMonitor _taskMonitor;
    private LibraryScanStats _lastScanStats = LibraryScanStats.Empty;
    private static readonly JsonSerializerOptions CacheJsonOptions = new() { WriteIndented = false };

    public LibraryCache(List<LibraryDefinition> libraries, MetadataStore metadataStore, FileIdentityStore identityStore, string cachePath, TaskMonitor taskMonitor)
    {
        _libraries = libraries;
        _metadataStore = metadataStore;
        _identityStore = identityStore;
        _cachePath = cachePath;
        _taskMonitor = taskMonitor;
        _items = LoadPersistedCache();
    }

    public IReadOnlyList<LibraryDefinition> Libraries => _libraries;
    public IReadOnlyList<string> LibraryPaths => _libraries.SelectMany(l => l.Folders).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    public int CachedItemCount => _items?.Count ?? 0;
    public LibraryScanStats LastScanStats => _lastScanStats;

    public LibraryItem? TryGetCachedItem(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        return _items?.FirstOrDefault(i => string.Equals(i.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    public void ReplaceCachedItem(LibraryItem updated, bool persist = true)
    {
        if (_items is null || updated is null) return;
        var list = _items.ToList();
        var index = list.FindIndex(i => string.Equals(i.Id, updated.Id, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return;
        list[index] = updated;
        _items = list;
        if (persist) SavePersistedCache(_items);
    }

    public int ReplaceCachedItems(IEnumerable<LibraryItem> updatedItems, bool persist = true)
    {
        if (_items is null || updatedItems is null) return 0;
        var updates = updatedItems
            .Where(item => item is not null && !string.IsNullOrWhiteSpace(item.Id))
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        if (updates.Count == 0) return 0;

        var changed = 0;
        var list = _items
            .Select(item =>
            {
                if (!updates.TryGetValue(item.Id, out var updated)) return item;
                changed++;
                return updated;
            })
            .ToList();

        if (changed == 0) return 0;
        _items = list;
        if (persist) SavePersistedCache(_items);
        return changed;
    }

    public int RefreshCachedMetadataOverrides(IEnumerable<string> ids, bool persist = true)
    {
        if (_items is null || ids is null) return 0;
        var idSet = ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (idSet.Count == 0) return 0;

        var changed = 0;
        var list = _items
            .Select(item =>
            {
                if (string.IsNullOrWhiteSpace(item.Id) || !idSet.Contains(item.Id)) return item;
                changed++;
                return _metadataStore.ApplyOverride(item);
            })
            .ToList();

        if (changed == 0) return 0;
        _items = list;
        if (persist) SavePersistedCache(_items);
        return changed;
    }

    public void RemoveCachedItem(string id, bool persist = true)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        var list = (_items ?? LoadPersistedCache() ?? Array.Empty<LibraryItem>())
            .Where(i => !string.Equals(i.Id, id, StringComparison.OrdinalIgnoreCase))
            .ToList();
        _items = list;
        if (persist) SavePersistedCache(_items);
    }

    private bool ItemBelongsToConfiguredLibrary(LibraryItem item)
    {
        if (_libraries.Count == 0) return false;
        var itemPath = (item.Path ?? string.Empty).Replace('\\', '/');
        foreach (var library in _libraries)
        {
            if (!string.IsNullOrWhiteSpace(library.Name) && string.Equals(item.LibraryName, library.Name, StringComparison.OrdinalIgnoreCase))
                return true;

            foreach (var folder in library.Folders ?? new List<string>())
            {
                if (string.IsNullOrWhiteSpace(folder)) continue;
                var root = Path.GetFullPath(folder).Replace('\\', '/').TrimEnd('/');
                if (itemPath.StartsWith(root + '/', StringComparison.OrdinalIgnoreCase) || string.Equals(itemPath, root, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    public void SetLibraries(List<LibraryDefinition> libraries)
    {
        _libraries = libraries;
        ArchiveReader.ClearCache();

        // Keep the existing index readable while a background scan rebuilds.
        // This prevents actions like Open Reader from blocking behind a long scan.
        // If libraries were removed, immediately prune their items from the active cache.
        if (_items is not null)
        {
            _items = _items
                .Where(ItemBelongsToConfiguredLibrary)
                .Select(_metadataStore.ApplyOverride)
                .ToList();
            SavePersistedCache(_items);
        }
    }

    public void Invalidate()
    {
        _items = null;
        ArchiveReader.ClearCache();
    }

    public IReadOnlyList<LibraryItem> GetItemsSnapshot()
    {
        if (_items is not null) return _items;
        var persisted = LoadPersistedCache();
        if (persisted is not null)
        {
            _items = persisted;
            return _items;
        }
        return Array.Empty<LibraryItem>();
    }

    public async Task<IReadOnlyList<LibraryItem>> GetItemsAsync()
    {
        if (_items is not null) return _items;
        await _lock.WaitAsync();
        try
        {
            if (_items is not null) return _items;
            using var libraryIoLease = await GuidevaultLibraryIoGate.BeginLibraryScanAsync();
            _items = await ScanAsync();
            SavePersistedCache(_items);
            return _items;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<LibraryItem>> RescanAsync(string taskId, string activity = "scan")
    {
        if (_lock.CurrentCount == 0)
            _taskMonitor.Update(taskId, "Waiting for the current library operation to finish...", 1);

        await _lock.WaitAsync();
        try
        {
            if (GuidevaultLibraryIoGate.IsBusy)
                _taskMonitor.Update(taskId, "Waiting for the current file write/rename operation to finish before scanning...", 1);

            using var libraryIoLease = await GuidevaultLibraryIoGate.BeginLibraryScanAsync();

            // Keep the existing item index available while the scan rebuilds. Archive
            // entry/cover memory caches are intentionally not blown away here because
            // repeated add/remove/cleanup operations can otherwise thrash network archives.
            _items = await ScanAsync(taskId, activity);
            SavePersistedCache(_items);
            return _items;
        }
        finally
        {
            _lock.Release();
        }
    }

    private IReadOnlyList<LibraryItem>? LoadPersistedCache()
    {
        try
        {
            if (!File.Exists(_cachePath)) return null;
            var json = File.ReadAllText(_cachePath);
            var cached = JsonSerializer.Deserialize<List<LibraryItem>>(json, CacheJsonOptions);
            if (cached is null || cached.Count == 0) return null;
            var validRoots = new HashSet<string>(LibraryPaths.Select(p => Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), StringComparer.OrdinalIgnoreCase);
            var cacheInvalidated = false;
            var stillRelevant = cached
                .Where(item =>
                {
                    if (string.IsNullOrWhiteSpace(item.Path))
                    {
                        cacheInvalidated = true;
                        return false;
                    }

                    string itemPath;
                    try { itemPath = Path.GetFullPath(item.Path); }
                    catch
                    {
                        cacheInvalidated = true;
                        return false;
                    }

                    var belongsToConfiguredRoot = validRoots.Count == 0 || validRoots.Any(root =>
                        itemPath.Equals(root, StringComparison.OrdinalIgnoreCase)
                        || itemPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                        || itemPath.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
                    if (!belongsToConfiguredRoot)
                    {
                        cacheInvalidated = true;
                        return false;
                    }

                    if (!File.Exists(itemPath))
                    {
                        cacheInvalidated = true;
                        return false;
                    }

                    return true;
                })
                .Select(_metadataStore.ApplyOverride)
                .GroupBy(i => NormalizeFilePathKey(i.Path), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(i => LibraryTypeSpecificity(i.LibraryType)).ThenByDescending(i => i.Modified).First())
                .GroupBy(i => i.Id, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            // If files were renamed/moved outside GuideVault, the persisted cache can
            // point at old paths.  Do not keep serving those stale rows with missing
            // covers; force a real scan so the renamed files are rediscovered.
            return !cacheInvalidated && stillRelevant.Count > 0 ? stillRelevant : null;
        }
        catch
        {
            return null;
        }
    }

    private void SavePersistedCache(IReadOnlyList<LibraryItem> items)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
            File.WriteAllText(_cachePath, JsonSerializer.Serialize(items, CacheJsonOptions));
        }
        catch
        {
            // Disk cache is a startup optimization; never fail a scan because it could not persist.
        }
    }

    private static string NormalizeFilePathKey(string path)
    {
        try
        {
            return Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return (path ?? string.Empty).Trim().TrimEnd('/', '\\');
        }
    }

    private static bool PathsEqual(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
        try { return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase); }
        catch { return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase); }
    }

    private static bool CachedItemMatchesFile(LibraryItem item, FileInfo info)
    {
        try
        {
            if (!string.Equals(Path.GetFullPath(item.Path), Path.GetFullPath(info.FullName), StringComparison.OrdinalIgnoreCase)) return false;
            return CachedItemLooksLikeSamePhysicalFile(item, info);
        }
        catch
        {
            return false;
        }
    }

    private static bool CachedItemLooksLikeSamePhysicalFile(LibraryItem item, FileInfo info)
    {
        try
        {
            if (item.SizeBytes != info.Length) return false;
            var itemExt = Path.GetExtension(item.FileName);
            if (string.IsNullOrWhiteSpace(itemExt)) itemExt = Path.GetExtension(item.Path);
            if (!string.Equals(itemExt, info.Extension, StringComparison.OrdinalIgnoreCase)) return false;
            var cachedModified = item.Modified.UtcDateTime;
            return Math.Abs((cachedModified - info.LastWriteTimeUtc).TotalSeconds) < 2;
        }
        catch
        {
            return false;
        }
    }

    private static string MovedFileSignature(long sizeBytes, DateTime modifiedUtc, string? extension)
    {
        if (sizeBytes <= 0) return string.Empty;
        var normalizedExt = (extension ?? string.Empty).Trim().ToLowerInvariant();
        var unixSeconds = new DateTimeOffset(DateTime.SpecifyKind(modifiedUtc, DateTimeKind.Utc)).ToUnixTimeSeconds();
        return $"{sizeBytes}:{unixSeconds}:{normalizedExt}";
    }

    private static LibraryItem RefreshCachedItemForLibrary(LibraryItem cached, FileInfo info, string relativePath, LibraryDefinition library)
        => cached with
        {
            Path = info.FullName,
            RelativePath = relativePath,
            FileName = info.Name,
            SizeBytes = info.Length,
            Added = cached.Added,
            Modified = info.LastWriteTimeUtc,
            LibraryName = library.Name,
            LibraryType = library.Type
        };

    private static bool ShouldDeepValidateCachedItem(LibraryItem cached, string libraryType)
    {
        if (!string.Equals(libraryType, "Magazines", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(cached.Kind, "Magazine", StringComparison.OrdinalIgnoreCase)) return false;
        return !string.Equals(cached.ValidationStatus, "ok", StringComparison.OrdinalIgnoreCase)
            || !cached.HasReadablePages
            || !string.IsNullOrWhiteSpace(cached.ValidationMessage);
    }

    private static bool ShouldEnrichMetadata(LibraryItem cached, bool includeComicInfoFallback)
    {
        if (!cached.Format.Equals("CBZ", StringComparison.OrdinalIgnoreCase)
            && !cached.Format.Equals("CBR", StringComparison.OrdinalIgnoreCase)) return false;

        var source = cached.MetadataSource ?? string.Empty;

        // Fast Guidevault JSON enrichment is an explicit user action. Always let it
        // re-check packages, even when the existing cache already says Guidevault
        // JSON, because newer native metadata may contain fields that an older
        // reader did not understand yet.
        if (!includeComicInfoFallback) return true;

        // Legacy ComicInfo import should remain a fallback and should not override
        // already-native Guidevault JSON metadata.
        if (source.Contains("Guidevault JSON", StringComparison.OrdinalIgnoreCase)) return false;
        if (source.Contains("ComicInfo", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    private static int LibraryTypeSpecificity(string? libraryType)
    {
        var value = (libraryType ?? string.Empty).Trim().ToLowerInvariant();
        return value switch
        {
            "manual" or "manuals" or "strategy guide" or "strategy guides" or "guide" or "guides" or "magazine" or "magazines" => 2,
            "mixed" or "" => 0,
            _ => 1
        };
    }

    private static bool ShouldRefreshPathInference(LibraryItem cached, string libraryType)
    {
        static bool IsUnsortedBucket(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return true;
            var trimmed = value.Trim();
            return trimmed.Equals("Unsorted", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("Unsorted ", StringComparison.OrdinalIgnoreCase);
        }

        if (IsUnsortedBucket(cached.System) || IsUnsortedBucket(cached.Category)) return true;

        var expectedKind = (libraryType ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "manual" or "manuals" => "Manual",
            "strategy guide" or "strategy guides" or "guide" or "guides" => "Strategy Guide",
            "magazine" or "magazines" => "Magazine",
            _ => string.Empty
        };
        if (!string.IsNullOrWhiteSpace(expectedKind) && !string.Equals(cached.Kind, expectedKind, StringComparison.OrdinalIgnoreCase)) return true;

        if (string.Equals(cached.Kind, "Strategy Guide", StringComparison.OrdinalIgnoreCase)
            && (cached.AssociatedPlatforms is null || cached.AssociatedPlatforms.Length == 0)) return true;

        return false;
    }

    private async Task<IReadOnlyList<LibraryItem>> ScanAsync(string? taskId = null, string activity = "scan")
    {
        var scanTimer = Stopwatch.StartNew();
        var scanStartedAt = DateTimeOffset.UtcNow;
        var guidevaultJsonMetadataActivity = activity.Equals("metadata", StringComparison.OrdinalIgnoreCase);
        var legacyComicInfoActivity = activity.Equals("comicinfo", StringComparison.OrdinalIgnoreCase)
            || activity.Equals("legacy-comicinfo", StringComparison.OrdinalIgnoreCase)
            || activity.Equals("enrichment", StringComparison.OrdinalIgnoreCase)
            || activity.Equals("enrich", StringComparison.OrdinalIgnoreCase);
        var metadataActivity = guidevaultJsonMetadataActivity || legacyComicInfoActivity;
        var scanRoots = _libraries.SelectMany(l => l.Folders.Select(f => new { Library = l, Folder = f }))
            .Where(x => !string.IsNullOrWhiteSpace(x.Folder))
            .ToList();

        var candidates = new List<(LibraryDefinition Library, string Root, string File)>();
        var discoveredRoots = 0;
        foreach (var root in scanRoots)
        {
            discoveredRoots++;
            if (!Directory.Exists(root.Folder))
            {
                if (!string.IsNullOrWhiteSpace(taskId))
                    _taskMonitor.Update(taskId, $"Skipping missing library folder: {root.Folder}", Math.Min(4, discoveredRoots));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(taskId))
                _taskMonitor.Update(taskId, $"Discovering files in {root.Library.Name}...", 3);

            var before = candidates.Count;
            // Keep discovery streaming. Sorting by LastWriteTime calls into the
            // filesystem for every candidate and is especially expensive on network
            // shares, so sort only after FileInfo metadata is already collected.
            var files = SafeEnumerateFiles(root.Folder)
                .Where(f => !f.Replace('\\', '/').Contains("/.guidevault_deleted/", StringComparison.OrdinalIgnoreCase))
                .Where(f => ArchiveReader.SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));

            foreach (var file in files)
                candidates.Add((root.Library, root.Folder, file));

            if (!string.IsNullOrWhiteSpace(taskId))
                _taskMonitor.Update(taskId, $"Discovered {candidates.Count - before} supported file(s) in {root.Library.Name}.", Math.Min(4, 2 + discoveredRoots));
        }

        // A file can be discovered by more than one configured library when roots overlap
        // (for example a broad literature folder plus a narrower Strategy Guides folder).
        // Keep one owner per physical file so a Manual library and a Strategy Guide library
        // cannot fight over the same cached item. The most-specific folder wins; explicit
        // typed libraries beat Mixed libraries when roots are equally specific.
        candidates = candidates
            .GroupBy(c => NormalizeFilePathKey(c.File), StringComparer.OrdinalIgnoreCase)
            .Select(g => g
                .OrderByDescending(c => Path.GetFullPath(c.Root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length)
                .ThenByDescending(c => LibraryTypeSpecificity(c.Library.Type))
                .ThenBy(c => c.Library.Name, StringComparer.OrdinalIgnoreCase)
                .First())
            .ToList();

        var removalActivity = activity.Equals("removal", StringComparison.OrdinalIgnoreCase);
        var cleanupActivity = activity.Equals("cleanup", StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(taskId))
            _taskMonitor.Update(taskId,
                removalActivity
                    ? $"Removing deleted library items and reconciling the remaining index ({candidates.Count} supported file(s) left)..."
                    : metadataActivity
                        ? legacyComicInfoActivity
                            ? $"Importing legacy ComicInfo metadata for {candidates.Count} supported file(s). This is the slower deep pass..."
                            : $"Fast-enriching Guidevault JSON metadata for {candidates.Count} supported file(s)..."
                        : cleanupActivity
                            ? $"Reconciling {candidates.Count} supported file(s). Validating only new, changed, or previously failed magazine entries..."
                            : $"Fast-indexing {candidates.Count} supported file(s) against the cached index...",
                candidates.Count == 0 ? 100 : 4);

        var previousItems = (_items ?? LoadPersistedCache() ?? Array.Empty<LibraryItem>())
            .GroupBy(i => i.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // A rename/move must not make GuideVault treat the archive as a brand-new item.
        // The explicit identity map should handle app-driven moves, but this recovery map
        // lets scans repair items moved before the map was written or after a container restart.
        var movedFileRecoveryMap = previousItems.Values
            .Where(item => !string.IsNullOrWhiteSpace(item.Id) && !string.IsNullOrWhiteSpace(item.Path) && item.SizeBytes > 0)
            .Where(item =>
            {
                try { return !File.Exists(item.Path); }
                catch { return false; }
            })
            .Select(item => new { Key = MovedFileSignature(item.SizeBytes, item.Modified.UtcDateTime, Path.GetExtension(item.FileName)), Item = item })
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
            .GroupBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.First().Item, StringComparer.OrdinalIgnoreCase);

        var bag = new ConcurrentBag<LibraryItem>();
        var processed = 0;
        var parsed = 0;
        var reused = 0;
        var skippedUnreadable = 0;
        var metadataDeferred = 0;
        var metadataEnriched = 0;
        var total = Math.Max(1, candidates.Count);
        // Normal scans should behave like a fast indexer and avoid opening every
        // archive. Metadata enrichment is explicit, slower, and intentionally lower
        // concurrency so it does not make the app feel stuck.
        var parallelism = metadataActivity
            ? Math.Max(1, Math.Min(Environment.ProcessorCount, legacyComicInfoActivity ? 2 : 3))
            : cleanupActivity
                ? Math.Max(1, Math.Min(Environment.ProcessorCount, 2))
                // Normal scans mostly perform directory/FileInfo checks. On Docker
                // bind mounts and network shares, high concurrency can make this
                // crawl by hammering the host filesystem. Keep it modest and leave
                // archive metadata reads to explicit metadata enrichment.
                : Math.Max(1, Math.Min(Environment.ProcessorCount, 2));

        await Parallel.ForEachAsync(candidates, new ParallelOptions { MaxDegreeOfParallelism = parallelism }, async (candidate, cancellationToken) =>
        {
            try
            {
                var info = new FileInfo(candidate.File);
                if (!info.Exists) return;

                LibraryItem? cached = null;
                var id = _identityStore.GetItemId(candidate.File);
                if (string.IsNullOrWhiteSpace(id))
                {
                    var recoveryKey = MovedFileSignature(info.Length, info.LastWriteTimeUtc, info.Extension);
                    if (movedFileRecoveryMap.TryGetValue(recoveryKey, out var recovered) && recovered is not null)
                    {
                        id = recovered.Id;
                        cached = recovered;
                        _identityStore.RememberRename(recovered.Path, candidate.File, recovered.Id);
                    }
                }
                id ??= StableId(candidate.File);
                if (_metadataStore.IsRemoved(id)) return;

                var ext = info.Extension.ToLowerInvariant();
                var format = ext.TrimStart('.').ToUpperInvariant();
                var relativePath = Path.GetRelativePath(candidate.Root, candidate.File);

                if (cached is null) previousItems.TryGetValue(id, out cached);
                var hasMatchingCache = cached is not null && CachedItemMatchesFile(cached, info);
                var hasMovedMatchingCache = !hasMatchingCache && cached is not null && CachedItemLooksLikeSamePhysicalFile(cached, info);
                if (cached is not null && !hasMatchingCache && PathsEqual(cached.Path, info.FullName))
                    ArchiveReader.ClearCacheForPath(info.FullName);
                if ((hasMatchingCache || hasMovedMatchingCache) && cached is not null)
                {
                    if (hasMovedMatchingCache) _identityStore.RememberRename(cached.Path, info.FullName, cached.Id);
                    var refreshed = RefreshCachedItemForLibrary(cached, info, relativePath, candidate.Library);
                    var needsValidation = cleanupActivity && ShouldDeepValidateCachedItem(refreshed, candidate.Library.Type);
                    var needsMetadataEnrichment = metadataActivity && ShouldEnrichMetadata(refreshed, legacyComicInfoActivity);
                    // Platform detection improvements need a chance to repair already-cached
                    // Unsorted items even when the underlying CBZ/CBR file has not changed.
                    var needsPathInferenceRefresh = !metadataActivity && !cleanupActivity && ShouldRefreshPathInference(refreshed, candidate.Library.Type);
                    if (!needsValidation && !needsMetadataEnrichment && !needsPathInferenceRefresh)
                    {
                        Interlocked.Increment(ref reused);
                        bag.Add(_metadataStore.ApplyOverride(refreshed));
                        return;
                    }
                }

                Interlocked.Increment(ref parsed);
                var inferred = MetadataInferer.FromFile(info, relativePath, candidate.Library.Type);
                ItemMetadataUpdate? guidevaultMetadata = null;
                // Fast library scans should not open archive payloads. On Windows-hosted
                // Docker bind mounts, opening even a handful of large CBZ/CBR files can
                // stall the whole scan. Guidevault JSON still takes precedence during
                // explicit metadata enrichment and for metadata written through the app,
                // because the saved override/cache is already updated immediately.
                var canReadGuidevaultMetadata = metadataActivity
                    && format != "PDF"
                    && (ext.Equals(".cbz", StringComparison.OrdinalIgnoreCase)
                        || ext.Equals(".cbr", StringComparison.OrdinalIgnoreCase));
                if (canReadGuidevaultMetadata)
                {
                    try
                    {
                        guidevaultMetadata = await Task.Run(() => ArchiveReader.GetGuidevaultMetadataAsync(candidate.File), cancellationToken)
                            .WaitAsync(TimeSpan.FromMilliseconds(1000), cancellationToken);
                        if (guidevaultMetadata is not null) Interlocked.Increment(ref metadataEnriched);
                    }
                    catch
                    {
                        guidevaultMetadata = null;
                    }
                }
                else if (!metadataActivity && format != "PDF" && ext.Equals(".cbz", StringComparison.OrdinalIgnoreCase) && !hasMatchingCache)
                {
                    Interlocked.Increment(ref metadataDeferred);
                }
                // Keep normal scans responsive. ComicInfo parsing is the slow path on
                // network shares and malformed archives, so the normal rescan defers it.
                // Use the Metadata Enrichment action when deeper ComicInfo import is wanted.
                ComicInfoMetadata? comicInfo = null;
                var canReadComicInfo = format != "PDF" && ext.Equals(".cbz", StringComparison.OrdinalIgnoreCase);
                var shouldReadComicInfo = canReadComicInfo && legacyComicInfoActivity && guidevaultMetadata is null;
                if (shouldReadComicInfo)
                {
                    try
                    {
                        comicInfo = await Task.Run(() => ArchiveReader.GetComicInfoAsync(candidate.File), cancellationToken)
                            .WaitAsync(TimeSpan.FromMilliseconds(1500), cancellationToken);
                        if (comicInfo is not null) Interlocked.Increment(ref metadataEnriched);
                    }
                    catch
                    {
                        comicInfo = null;
                    }
                }
                else if (canReadComicInfo && !hasMatchingCache && metadataActivity)
                {
                    Interlocked.Increment(ref metadataDeferred);
                }
                var merged = MetadataInferer.ApplyComicInfo(inferred, comicInfo);
                var pageCount = cached?.PageCount ?? 0;
                var validationStatus = cached?.ValidationStatus ?? "ok";
                var validationMessage = cached?.ValidationMessage ?? string.Empty;
                var hasReadablePages = cached?.HasReadablePages ?? true;

                var shouldValidateArchive = cleanupActivity
                    && format != "PDF"
                    && (string.Equals(merged.Kind, "Magazine", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(candidate.Library.Type, "Magazines", StringComparison.OrdinalIgnoreCase));
                if (shouldValidateArchive)
                {
                    ArchiveValidationResult validation;
                    try
                    {
                        validation = await Task.Run(() => ArchiveReader.ValidateArchiveAsync(candidate.File), cancellationToken)
                            .WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
                    }
                    catch (TimeoutException)
                    {
                        validation = new ArchiveValidationResult(false, "Validation timed out while opening the archive.", 0);
                    }
                    catch (Exception ex)
                    {
                        validation = new ArchiveValidationResult(false, $"Archive validation failed: {ex.Message}", 0);
                    }

                    if (!validation.IsReadable)
                    {
                        ArchiveReader.ClearCoverCacheForPath(candidate.File);
                        Interlocked.Increment(ref skippedUnreadable);
                        return;
                    }

                    pageCount = validation.PageCount;
                    validationStatus = "ok";
                    validationMessage = validation.Message;
                    hasReadablePages = validation.PageCount > 0;
                }

                var item = new LibraryItem(
                    Id: id,
                    Title: merged.Title,
                    Kind: merged.Kind,
                    Format: format,
                    Path: candidate.File,
                    RelativePath: relativePath,
                    FileName: info.Name,
                    SizeBytes: info.Length,
                    Added: info.CreationTimeUtc,
                    Modified: info.LastWriteTimeUtc,
                    PageCount: pageCount,
                    System: merged.System,
                    Category: merged.Category,
                    Publisher: merged.Publisher,
                    Year: merged.Year,
                    ContentType: format == "PDF" ? "application/pdf" : "application/octet-stream",
                    Tags: merged.Tags,
                    Summary: merged.Summary,
                    Series: merged.Series,
                    Writer: merged.Writer,
                    IssueNumber: merged.IssueNumber,
                    Rating: merged.Rating,
                    WebLink: merged.WebLink,
                    Asin: merged.Asin,
                    Isbn10: merged.Isbn10,
                    Isbn13: merged.Isbn13,
                    LanguageTag: merged.LanguageTag,
                    AssociatedPlatforms: merged.AssociatedPlatforms ?? [],
                    PlatformMatchTitle: merged.PlatformMatchTitle,
                    PlatformResolverSource: merged.PlatformResolverSource,
                    PlatformResolverConfidence: merged.PlatformResolverConfidence,
                    MagazineTitle: merged.MagazineTitle,
                    Volume: merged.Volume,
                    CoverDate: merged.CoverDate,
                    PublicationDate: merged.PublicationDate,
                    Region: merged.Region,
                    PlatformFocus: merged.PlatformFocus,
                    PrimarySystem: merged.PrimarySystem,
                    MagazineCategory: merged.MagazineCategory,
                    CoverSubject: merged.CoverSubject,
                    FeaturedGames: merged.FeaturedGames ?? [],
                    FeaturedPlatforms: merged.FeaturedPlatforms ?? [],
                    SpecialFeatures: merged.SpecialFeatures ?? [],
                    IncludedExtras: merged.IncludedExtras ?? [],
                    LibraryName: candidate.Library.Name,
                    LibraryType: candidate.Library.Type,
                    Notes: cached?.Notes ?? string.Empty,
                    ValidationStatus: validationStatus,
                    ValidationMessage: validationMessage,
                    HasReadablePages: hasReadablePages,
                    GameTitle: merged.GameTitle,
                    GuideType: merged.GuideType,
                    Edition: merged.Edition,
                    Franchise: merged.Franchise,
                    Developer: merged.Developer,
                    GamePublisher: merged.GamePublisher,
                    GameReleaseYear: merged.GameReleaseYear,
                    Genre: merged.Genre,
                    CoveredGames: merged.CoveredGames ?? [],
                    CoveredPlatforms: merged.CoveredPlatforms ?? [],
                    GuideTopics: merged.GuideTopics ?? [],
                    CharactersCovered: merged.CharactersCovered ?? [],
                    LocationsCovered: merged.LocationsCovered ?? [],
                    MetadataSource: merged.MetadataSource,
                    MetadataStatus: MetadataStatusHelper.Unreviewed);
                if (guidevaultMetadata is not null)
                {
                    item = MetadataStore.ApplyUpdateSnapshot(item, guidevaultMetadata with
                    {
                        MetadataSource = string.IsNullOrWhiteSpace(guidevaultMetadata.MetadataSource) ? "Guidevault JSON" : guidevaultMetadata.MetadataSource
                    });
                }
                bag.Add(_metadataStore.ApplyOverride(item));
            }
            catch
            {
                // Keep one unreadable/corrupt file from blanking the whole library.
                Interlocked.Increment(ref skippedUnreadable);
            }
            finally
            {
                var done = Interlocked.Increment(ref processed);
                if (!string.IsNullOrWhiteSpace(taskId) && (done == total || done % 25 == 0))
                {
                    var pct = 5 + (int)Math.Round(done / (double)total * 90);
                    var progressMessage = removalActivity
                        ? $"Reconciling remaining library index... {done}/{total} ({reused} cached, {parsed} parsed)"
                        : metadataActivity
                            ? legacyComicInfoActivity
                                ? $"Importing legacy ComicInfo metadata... {done}/{total} ({reused} already current, {metadataEnriched} enriched, {parsed} checked)"
                                : $"Fast-enriching Guidevault JSON metadata... {done}/{total} ({reused} already current, {metadataEnriched} enriched, {parsed} checked)"
                            : cleanupActivity
                                ? $"Validating changed magazines and indexing files... {done}/{total} ({reused} cached, {parsed} parsed, {skippedUnreadable} skipped)"
                                : $"Fast-indexing library files... {done}/{total} ({reused} cached, {parsed} new/changed, {metadataDeferred} archive metadata deferred)";
                    _taskMonitor.Update(taskId, progressMessage, pct);
                }
            }
        });

        if (!string.IsNullOrWhiteSpace(taskId))
            _taskMonitor.Update(taskId,
                removalActivity
                    ? $"Finalizing library removal. Reused {reused} cached item(s), parsed {parsed} changed item(s)..."
                    : metadataActivity
                        ? legacyComicInfoActivity
                            ? $"Finalizing legacy ComicInfo import. Enriched {metadataEnriched} item(s), reused {reused} already-current item(s)..."
                            : $"Finalizing fast metadata enrichment. Enriched {metadataEnriched} item(s), reused {reused} already-current item(s)..."
                        : cleanupActivity
                            ? $"Finalizing cleanup. Reused {reused} cached item(s), parsed {parsed} changed item(s), skipped {skippedUnreadable} unreadable/stale magazine file(s)..."
                            : $"Finalizing fast library index. Reused {reused} cached item(s), indexed {parsed} new/changed item(s), deferred {metadataDeferred} archive metadata import(s)...",
                98);

        var result = bag
            .GroupBy(i => NormalizeFilePathKey(i.Path), StringComparer.OrdinalIgnoreCase)
            .Select(g => g
                .OrderByDescending(i => LibraryTypeSpecificity(i.LibraryType))
                .ThenByDescending(i => i.Modified)
                .First())
            .GroupBy(i => i.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderByDescending(i => i.Modified)
            .ToList();

        scanTimer.Stop();
        _lastScanStats = new LibraryScanStats(
            StartedAt: scanStartedAt,
            CompletedAt: DateTimeOffset.UtcNow,
            Activity: metadataActivity ? (legacyComicInfoActivity ? "comicinfo" : "metadata") : activity,
            TotalCandidates: candidates.Count,
            Reused: reused,
            Parsed: parsed,
            MetadataDeferred: metadataDeferred,
            MetadataEnriched: metadataEnriched,
            SkippedUnreadable: skippedUnreadable,
            ElapsedMs: scanTimer.ElapsedMilliseconds,
            Message: metadataActivity
                ? legacyComicInfoActivity
                    ? $"Legacy ComicInfo import checked {candidates.Count} file(s), enriched {metadataEnriched}, reused {reused}."
                    : $"Fast metadata enrichment checked {candidates.Count} file(s), enriched {metadataEnriched}, reused {reused}."
                : $"Fast index checked {candidates.Count} file(s), reused {reused}, indexed {parsed}, deferred {metadataDeferred} archive metadata import(s)."
        );

        return result;
    }

    public static IEnumerable<string> SafeEnumerateFiles(string root)
    {
        var pending = new Stack<string>();
        if (Directory.Exists(root)) pending.Push(root);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            IEnumerable<string> files = [];
            IEnumerable<string> dirs = [];

            try { files = Directory.EnumerateFiles(current); }
            catch { }

            foreach (var file in files) yield return file;

            try { dirs = Directory.EnumerateDirectories(current); }
            catch { }

            foreach (var dir in dirs)
            {
                var normalized = dir.Replace('\\', '/');
                if (normalized.Contains("/.guidevault_deleted", StringComparison.OrdinalIgnoreCase)) continue;
                pending.Push(dir);
            }
        }
    }

    private static DateTime SafeGetLastWriteTimeUtc(string path)
    {
        try { return File.GetLastWriteTimeUtc(path); }
        catch { return DateTime.MinValue; }
    }

    private static string StableId(string path)
    {
        var bytes = SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(path.ToLowerInvariant()));
        return Convert.ToHexString(bytes)[..12].ToLowerInvariant();
    }
}

public sealed record LibraryScanStats(
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string Activity,
    int TotalCandidates,
    int Reused,
    int Parsed,
    int MetadataDeferred,
    int MetadataEnriched,
    int SkippedUnreadable,
    long ElapsedMs,
    string Message)
{
    public static LibraryScanStats Empty { get; } = new(null, null, string.Empty, 0, 0, 0, 0, 0, 0, 0, "No library scan has completed in this app session.");
}

public sealed record InferredMetadata(
    string Title,
    string Kind,
    string System,
    string Category,
    string Publisher,
    string Year,
    string[] Tags,
    string Summary,
    string Series,
    string Writer,
    string IssueNumber,
    string Rating,
    string WebLink,
    string Asin,
    string Isbn10,
    string Isbn13,
    string LanguageTag,
    string[]? AssociatedPlatforms = null,
    string PlatformMatchTitle = "",
    string PlatformResolverSource = "",
    double PlatformResolverConfidence = 0,
    string MagazineTitle = "",
    string Volume = "",
    string CoverDate = "",
    string PublicationDate = "",
    string Region = "",
    string PlatformFocus = "",
    string PrimarySystem = "",
    string MagazineCategory = "",
    string CoverSubject = "",
    string[]? FeaturedGames = null,
    string[]? FeaturedPlatforms = null,
    string[]? SpecialFeatures = null,
    string[]? IncludedExtras = null,
    string GameTitle = "",
    string GuideType = "",
    string Edition = "",
    string Franchise = "",
    string Developer = "",
    string GamePublisher = "",
    string GameReleaseYear = "",
    string Genre = "",
    string[]? CoveredGames = null,
    string[]? CoveredPlatforms = null,
    string[]? GuideTopics = null,
    string[]? CharactersCovered = null,
    string[]? LocationsCovered = null,
    string MetadataSource = "");

public sealed record StrategyGuideBookFields(string Asin, string Isbn10, string Isbn13, string LanguageTag);

public sealed record ComicInfoMetadata(
    string Title,
    string Series,
    string Number,
    string Writer,
    string Publisher,
    string Year,
    string Summary,
    string Genre,
    string Tags,
    string Rating,
    string WebLink,
    string Information,
    string Asin,
    string Isbn10,
    string Isbn13,
    string LanguageTag,
    string Volume = "",
    string Month = "",
    string Day = "",
    string Country = "");

public static class MetadataInferer
{
    public static InferredMetadata FromFile(FileInfo file, string relativePath, string libraryType = "Mixed")
    {
        var name = Path.GetFileNameWithoutExtension(file.Name)
            .Replace('_', ' ')
            .Replace('.', ' ')
            .Replace("  ", " ")
            .Trim();

        var lowerName = name.ToLowerInvariant();
        var lowerPath = relativePath.Replace('\\', '/').ToLowerInvariant();
        var kind = KindFromLibraryType(libraryType) ?? DetectKind(lowerName, lowerPath);
        var system = DetectSystem(lowerName, lowerPath);
        var category = DetectCategory(kind, system, relativePath, lowerName, lowerPath);
        var publisher = DetectPublisher(lowerName, lowerPath);

        var year = System.Text.RegularExpressions.Regex.Match(name, "(19|20)\\d{2}").Value;
        if (string.IsNullOrWhiteSpace(year)) year = "Unknown";

        var tags = new List<string> { kind, category };
        if (kind == "Magazine") tags.Add("Magazine");
        if (kind == "Strategy Guide") tags.Add("Walkthrough");
        if (kind == "Manual") tags.Add("Instruction Booklet");
        var associatedPlatforms = kind == "Strategy Guide" && !IsUnsorted(system) ? new[] { system } : [];
        var platformMatchTitle = kind == "Strategy Guide" ? DeriveStrategyGuideGameTitle(name) : string.Empty;
        var strategyGameTitle = kind == "Strategy Guide" ? FirstNonEmpty(platformMatchTitle, DeriveStrategyGuideGameTitle(name), name) : string.Empty;
        var manualGameTitle = kind == "Manual" ? CleanManualGameTitle(name) : string.Empty;
        var gameTitle = kind == "Strategy Guide" ? strategyGameTitle : manualGameTitle;
        var gameReleaseYear = kind == "Manual" && !string.Equals(year, "Unknown", StringComparison.OrdinalIgnoreCase) ? year : string.Empty;
        var gamePublisher = kind == "Manual" && !string.Equals(publisher, "Unknown", StringComparison.OrdinalIgnoreCase) ? publisher : string.Empty;
        var strategyGuideType = kind == "Strategy Guide" ? DetectGuideType(name, relativePath) : string.Empty;
        var strategyTopics = kind == "Strategy Guide" ? ExtractStrategyGuideTopics($"{name} {relativePath}") : [];
        var strategySpecialFeatures = kind == "Strategy Guide" ? ExtractSpecialFeatures($"{name} {relativePath}") : [];
        var strategyIncludedExtras = kind == "Strategy Guide" ? ExtractIncludedExtras($"{name} {relativePath}") : [];
        var strategyCoveredPlatforms = kind == "Strategy Guide" ? associatedPlatforms : [];
        var strategyCoveredGames = kind == "Strategy Guide" && !string.IsNullOrWhiteSpace(strategyGameTitle) ? new[] { strategyGameTitle } : Array.Empty<string>();

        var magazineTitle = string.Empty;
        var volume = string.Empty;
        var coverDate = string.Empty;
        var publicationDate = string.Empty;
        var region = string.Empty;
        var platformFocus = string.Empty;
        var primarySystem = string.Empty;
        var magazineCategory = string.Empty;
        var coverSubject = string.Empty;
        string[] featuredPlatforms = [];
        string[] specialFeatures = [];
        string[] includedExtras = [];
        if (kind == "Magazine")
        {
            magazineTitle = DetectMagazineSeries(lowerName, lowerPath);
            if (magazineTitle == "Unsorted Magazines") magazineTitle = ParentFolderName(relativePath);
            volume = ExtractFieldFromText(name, "Volume", "Vol", "V");
            var fileIssue = ExtractIssueNumber(name);
            var date = ExtractMagazineDate(name);
            coverDate = date;
            publicationDate = date;
            region = DetectRegion(name, relativePath);
            featuredPlatforms = DetectSystemsFromText($"{name} {relativePath}");
            platformFocus = featuredPlatforms.Length > 1 ? "Multi-platform" : featuredPlatforms.FirstOrDefault() ?? (IsUnsorted(system) ? string.Empty : system);
            primarySystem = featuredPlatforms.FirstOrDefault() ?? (IsUnsorted(system) ? string.Empty : system);
            magazineCategory = DetectMagazineCategory(name, relativePath, magazineTitle);
            specialFeatures = ExtractSpecialFeatures($"{name} {relativePath}");
            includedExtras = ExtractIncludedExtras($"{name} {relativePath}");
            coverSubject = ExtractCoverSubject(name, magazineTitle, fileIssue, date);
            return new InferredMetadata(
                Title: TitleCaseFallback(magazineTitle, name, fileIssue, date),
                Kind: kind,
                System: string.IsNullOrWhiteSpace(magazineTitle) ? system : magazineTitle,
                Category: string.IsNullOrWhiteSpace(magazineTitle) ? category : magazineTitle,
                Publisher: publisher,
                Year: year,
                Tags: tags.Distinct().ToArray(),
                Summary: string.Empty,
                Series: magazineTitle,
                Writer: string.Empty,
                IssueNumber: fileIssue,
                Rating: string.Empty,
                WebLink: string.Empty,
                Asin: string.Empty,
                Isbn10: string.Empty,
                Isbn13: string.Empty,
                LanguageTag: string.Empty,
                AssociatedPlatforms: associatedPlatforms,
                PlatformMatchTitle: platformMatchTitle,
                PlatformResolverSource: associatedPlatforms.Length > 0 ? "Folder/File name" : string.Empty,
                PlatformResolverConfidence: associatedPlatforms.Length > 0 ? 0.62 : 0,
                MagazineTitle: magazineTitle,
                Volume: volume,
                CoverDate: coverDate,
                PublicationDate: publicationDate,
                Region: region,
                PlatformFocus: platformFocus,
                PrimarySystem: primarySystem,
                MagazineCategory: magazineCategory,
                CoverSubject: coverSubject,
                FeaturedGames: [],
                FeaturedPlatforms: featuredPlatforms,
                SpecialFeatures: specialFeatures,
                IncludedExtras: includedExtras);
        }

        return new InferredMetadata(
            Title: name,
            Kind: kind,
            System: system,
            Category: category,
            Publisher: publisher,
            Year: year,
            Tags: tags.Distinct().ToArray(),
            Summary: string.Empty,
            Series: kind == "Strategy Guide" ? strategyGameTitle : string.Empty,
            Writer: string.Empty,
            IssueNumber: string.Empty,
            Rating: string.Empty,
            WebLink: string.Empty,
            Asin: string.Empty,
            Isbn10: string.Empty,
            Isbn13: string.Empty,
            LanguageTag: string.Empty,
            PublicationDate: kind == "Strategy Guide" ? year : string.Empty,
            Region: kind == "Strategy Guide" ? DetectRegion(name, relativePath) : string.Empty,
            AssociatedPlatforms: associatedPlatforms,
            PlatformMatchTitle: platformMatchTitle,
            PlatformResolverSource: associatedPlatforms.Length > 0 ? "Folder/File name" : string.Empty,
            PlatformResolverConfidence: associatedPlatforms.Length > 0 ? 0.62 : 0,
            GameTitle: gameTitle,
            GuideType: strategyGuideType,
            Franchise: kind == "Strategy Guide" ? strategyGameTitle : string.Empty,
            GamePublisher: gamePublisher,
            GameReleaseYear: gameReleaseYear,
            Genre: string.Empty,
            CoveredGames: strategyCoveredGames,
            CoveredPlatforms: strategyCoveredPlatforms,
            GuideTopics: strategyTopics,
            SpecialFeatures: strategySpecialFeatures,
            IncludedExtras: strategyIncludedExtras,
            MetadataSource: "Filename / folder path");
    }

    public static InferredMetadata ApplyComicInfo(InferredMetadata inferred, ComicInfoMetadata? comicInfo)
    {
        if (comicInfo is null) return inferred;

        var title = FirstNonEmpty(comicInfo.Title, inferred.Title);
        var publisher = FirstNonEmpty(comicInfo.Publisher, inferred.Publisher);
        var year = FirstNonEmpty(comicInfo.Year, inferred.Year);
        var summary = FirstNonEmpty(comicInfo.Summary, inferred.Summary);
        var series = FirstNonEmpty(comicInfo.Series, inferred.Series);
        var writer = FirstNonEmpty(comicInfo.Writer, inferred.Writer);
        var issueNumber = inferred.Kind == "Magazine" ? FirstNonEmpty(comicInfo.Number, inferred.IssueNumber) : string.Empty;
        var rating = FirstNonEmpty(comicInfo.Rating, inferred.Rating);
        var webLink = FirstNonEmpty(comicInfo.WebLink, inferred.WebLink);

        var parsedBookFields = ExtractStrategyGuideBookFields(comicInfo.Information, comicInfo.Tags, comicInfo.Summary);
        var asin = FirstNonEmpty(comicInfo.Asin, parsedBookFields.Asin, inferred.Asin);
        var isbn10 = FirstNonEmpty(comicInfo.Isbn10, parsedBookFields.Isbn10, inferred.Isbn10);
        var isbn13 = FirstNonEmpty(comicInfo.Isbn13, parsedBookFields.Isbn13, inferred.Isbn13);
        var languageTag = FirstNonEmpty(comicInfo.LanguageTag, parsedBookFields.LanguageTag, inferred.LanguageTag);

        var tagList = new List<string>(inferred.Tags ?? []);
        AddSplitTags(tagList, comicInfo.Genre);
        AddSplitTags(tagList, comicInfo.Tags);
        if (!string.IsNullOrWhiteSpace(series)) tagList.Add(series);
        if (!string.IsNullOrWhiteSpace(writer)) tagList.Add(writer);

        var detectedSystem = inferred.System;
        var category = inferred.Category;
        var associatedPlatforms = inferred.AssociatedPlatforms ?? [];
        var platformMatchTitle = inferred.PlatformMatchTitle;
        var platformResolverSource = inferred.PlatformResolverSource;
        var platformResolverConfidence = inferred.PlatformResolverConfidence;
        var magazineTitle = inferred.MagazineTitle;
        var volume = inferred.Volume;
        var coverDate = inferred.CoverDate;
        var publicationDate = inferred.PublicationDate;
        var region = inferred.Region;
        var platformFocus = inferred.PlatformFocus;
        var primarySystem = inferred.PrimarySystem;
        var magazineCategory = inferred.MagazineCategory;
        var coverSubject = inferred.CoverSubject;
        var featuredGames = inferred.FeaturedGames ?? [];
        var featuredPlatforms = inferred.FeaturedPlatforms ?? [];
        var specialFeatures = inferred.SpecialFeatures ?? [];
        var includedExtras = inferred.IncludedExtras ?? [];
        var gameTitle = inferred.GameTitle;
        var guideType = inferred.GuideType;
        var edition = inferred.Edition;
        var franchise = inferred.Franchise;
        var developer = inferred.Developer;
        var gamePublisher = inferred.GamePublisher;
        var gameReleaseYear = inferred.GameReleaseYear;
        var genre = inferred.Genre;
        var coveredGames = inferred.CoveredGames ?? [];
        var coveredPlatforms = inferred.CoveredPlatforms ?? [];
        var guideTopics = inferred.GuideTopics ?? [];
        var charactersCovered = inferred.CharactersCovered ?? [];
        var locationsCovered = inferred.LocationsCovered ?? [];
        var metadataSource = inferred.MetadataSource;

        if (inferred.Kind == "Manual")
        {
            // For manuals, the ComicInfo Series tag is commonly used as the platform/system bucket.
            if (!string.IsNullOrWhiteSpace(series))
            {
                detectedSystem = series;
                category = series;
            }
            var manualTextBlock = $"{comicInfo.Tags} {comicInfo.Genre} {comicInfo.Information} {comicInfo.Summary} {title} {inferred.Title}";
            gameTitle = FirstNonEmpty(inferred.GameTitle, ExtractFieldFromText(manualTextBlock, "Game Title", "Title"), title, inferred.Title);
            franchise = FirstNonEmpty(inferred.Franchise, ExtractFieldFromText(manualTextBlock, "Franchise", "Game Franchise"));
            developer = FirstNonEmpty(inferred.Developer, ExtractFieldFromText(manualTextBlock, "Developer", "Game Developer"));
            gamePublisher = FirstNonEmpty(inferred.GamePublisher, ExtractFieldFromText(manualTextBlock, "Game Publisher"), publisher);
            gameReleaseYear = FirstNonEmpty(inferred.GameReleaseYear, ExtractFieldFromText(manualTextBlock, "Game Release Year", "Release Year"), comicInfo.Year, inferred.Year == "Unknown" ? string.Empty : inferred.Year);
            genre = FirstNonEmpty(comicInfo.Genre, inferred.Genre);
            metadataSource = "ComicInfo / filename";
        }
        else if (inferred.Kind == "Strategy Guide")
        {
            // For strategy guides, Series is usually the guide/game series, not the platform.
            // Prefer platform/system values found in ComicInfo Tags/Genre/Information, then folder/file inference.
            var tagSystems = DetectSystemsFromText($"{comicInfo.Tags} {comicInfo.Genre} {comicInfo.Information}");
            if (tagSystems.Length > 0)
            {
                associatedPlatforms = tagSystems;
                detectedSystem = tagSystems[0];
                platformResolverSource = "ComicInfo Tags/Information";
                platformResolverConfidence = tagSystems.Length > 1 ? 0.92 : 0.88;
            }
            else if (!IsUnsorted(inferred.System))
            {
                associatedPlatforms = [inferred.System];
                detectedSystem = inferred.System;
                platformResolverSource = "Folder/File name";
                platformResolverConfidence = 0.62;
            }
            else
            {
                associatedPlatforms = [];
                detectedSystem = "Unsorted Strategy Guides";
            }
            category = detectedSystem;
            platformMatchTitle = FirstNonEmpty(series, DeriveStrategyGuideGameTitle(title, inferred.Title), inferred.PlatformMatchTitle);
            gameTitle = FirstNonEmpty(inferred.GameTitle, platformMatchTitle, DeriveStrategyGuideGameTitle(title, inferred.Title));
            guideType = FirstNonEmpty(inferred.GuideType, DetectGuideType($"{title} {comicInfo.Tags}", comicInfo.Information));
            franchise = FirstNonEmpty(inferred.Franchise, series, gameTitle);
            genre = FirstNonEmpty(comicInfo.Genre, inferred.Genre);
            coveredGames = CleanDistinctStrings(coveredGames.Concat(!string.IsNullOrWhiteSpace(gameTitle) ? new[] { gameTitle } : Array.Empty<string>()));
            coveredPlatforms = CleanDistinctStrings(coveredPlatforms.Concat(associatedPlatforms));
            guideTopics = CleanDistinctStrings(guideTopics.Concat(ExtractStrategyGuideTopics($"{comicInfo.Tags} {comicInfo.Genre} {comicInfo.Information} {comicInfo.Summary} {title}")));
            specialFeatures = CleanDistinctStrings((specialFeatures ?? []).Concat(ExtractSpecialFeatures($"{comicInfo.Tags} {comicInfo.Information} {comicInfo.Summary} {title}")));
            includedExtras = CleanDistinctStrings((includedExtras ?? []).Concat(ExtractIncludedExtras($"{comicInfo.Tags} {comicInfo.Information} {comicInfo.Summary} {title}")));
            edition = FirstNonEmpty(inferred.Edition, ExtractFieldFromText($"{comicInfo.Information} {comicInfo.Tags}", "Edition"));
            developer = FirstNonEmpty(inferred.Developer, ExtractFieldFromText($"{comicInfo.Information} {comicInfo.Tags}", "Developer", "Game Developer"));
            gamePublisher = FirstNonEmpty(inferred.GamePublisher, ExtractFieldFromText($"{comicInfo.Information} {comicInfo.Tags}", "Game Publisher", "Developer Publisher"));
            gameReleaseYear = FirstNonEmpty(inferred.GameReleaseYear, ExtractFieldFromText($"{comicInfo.Information} {comicInfo.Tags}", "Game Release Year", "Release Year"));
            publicationDate = FirstNonEmpty(inferred.PublicationDate, FormatMagazineDate(comicInfo.Year, comicInfo.Month, comicInfo.Day), inferred.Year);
            region = FirstNonEmpty(inferred.Region, comicInfo.Country);
            metadataSource = "ComicInfo / filename";
        }
        else if (inferred.Kind == "Magazine")
        {
            magazineTitle = FirstNonEmpty(series, inferred.MagazineTitle, detectedSystem, inferred.Title);
            if (!string.IsNullOrWhiteSpace(magazineTitle))
            {
                detectedSystem = magazineTitle;
                category = magazineTitle;
            }
            volume = FirstNonEmpty(comicInfo.Volume, inferred.Volume);
            var comicDate = FormatMagazineDate(comicInfo.Year, comicInfo.Month, comicInfo.Day);
            coverDate = FirstNonEmpty(comicDate, inferred.CoverDate);
            publicationDate = FirstNonEmpty(comicDate, inferred.PublicationDate);
            region = FirstNonEmpty(comicInfo.Country, inferred.Region, DetectRegion($"{comicInfo.Tags} {comicInfo.Information}", string.Empty));
            var textBlock = $"{comicInfo.Tags} {comicInfo.Genre} {comicInfo.Information} {comicInfo.Summary} {inferred.Title}";
            featuredPlatforms = CleanDistinctStrings(featuredPlatforms.Concat(DetectSystemsFromText(textBlock)));
            platformFocus = FirstNonEmpty(inferred.PlatformFocus, featuredPlatforms.Length > 1 ? "Multi-platform" : featuredPlatforms.FirstOrDefault() ?? string.Empty);
            primarySystem = FirstNonEmpty(inferred.PrimarySystem, featuredPlatforms.FirstOrDefault() ?? string.Empty);
            magazineCategory = FirstNonEmpty(inferred.MagazineCategory, DetectMagazineCategory(textBlock, string.Empty, magazineTitle));
            coverSubject = FirstNonEmpty(inferred.CoverSubject, ExtractCoverSubject(title, magazineTitle, issueNumber, coverDate));
            specialFeatures = CleanDistinctStrings(specialFeatures.Concat(ExtractSpecialFeatures(textBlock)));
            includedExtras = CleanDistinctStrings(includedExtras.Concat(ExtractIncludedExtras(textBlock)));
            featuredGames = CleanDistinctStrings(featuredGames.Concat(ExtractFeaturedGames(textBlock)));
        }

        return inferred with
        {
            Title = title,
            Publisher = publisher,
            Year = year,
            System = detectedSystem,
            Category = category,
            Tags = tagList.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            Summary = summary,
            Series = series,
            Writer = writer,
            IssueNumber = issueNumber,
            Rating = rating,
            WebLink = webLink,
            Asin = asin,
            Isbn10 = isbn10,
            Isbn13 = isbn13,
            LanguageTag = languageTag,
            AssociatedPlatforms = associatedPlatforms,
            PlatformMatchTitle = platformMatchTitle,
            PlatformResolverSource = platformResolverSource,
            PlatformResolverConfidence = platformResolverConfidence,
            MagazineTitle = inferred.Kind == "Magazine" ? magazineTitle : string.Empty,
            Volume = inferred.Kind == "Magazine" ? volume : string.Empty,
            CoverDate = inferred.Kind == "Magazine" ? coverDate : string.Empty,
            PublicationDate = inferred.Kind == "Magazine" || inferred.Kind == "Strategy Guide" ? publicationDate : string.Empty,
            Region = inferred.Kind == "Magazine" || inferred.Kind == "Strategy Guide" ? region : string.Empty,
            PlatformFocus = inferred.Kind == "Magazine" ? platformFocus : string.Empty,
            PrimarySystem = inferred.Kind == "Magazine" ? primarySystem : string.Empty,
            MagazineCategory = inferred.Kind == "Magazine" ? magazineCategory : string.Empty,
            CoverSubject = inferred.Kind == "Magazine" ? coverSubject : string.Empty,
            FeaturedGames = inferred.Kind == "Magazine" ? featuredGames : [],
            FeaturedPlatforms = inferred.Kind == "Magazine" ? featuredPlatforms : [],
            SpecialFeatures = inferred.Kind == "Magazine" ? specialFeatures : [],
            IncludedExtras = inferred.Kind == "Magazine" || inferred.Kind == "Strategy Guide" || inferred.Kind == "Manual" ? includedExtras : [],
            GameTitle = inferred.Kind == "Strategy Guide" || inferred.Kind == "Manual" ? gameTitle : string.Empty,
            GuideType = inferred.Kind == "Strategy Guide" ? guideType : string.Empty,
            Edition = inferred.Kind == "Strategy Guide" ? edition : string.Empty,
            Franchise = inferred.Kind == "Strategy Guide" || inferred.Kind == "Manual" ? franchise : string.Empty,
            Developer = inferred.Kind == "Strategy Guide" || inferred.Kind == "Manual" ? developer : string.Empty,
            GamePublisher = inferred.Kind == "Strategy Guide" || inferred.Kind == "Manual" ? gamePublisher : string.Empty,
            GameReleaseYear = inferred.Kind == "Strategy Guide" || inferred.Kind == "Manual" ? gameReleaseYear : string.Empty,
            Genre = inferred.Kind == "Strategy Guide" || inferred.Kind == "Manual" ? genre : string.Empty,
            CoveredGames = inferred.Kind == "Strategy Guide" ? coveredGames : [],
            CoveredPlatforms = inferred.Kind == "Strategy Guide" ? coveredPlatforms : [],
            GuideTopics = inferred.Kind == "Strategy Guide" ? guideTopics : [],
            CharactersCovered = inferred.Kind == "Strategy Guide" ? charactersCovered : [],
            LocationsCovered = inferred.Kind == "Strategy Guide" ? locationsCovered : [],
            MetadataSource = metadataSource
        };
    }


    private static string CleanManualGameTitle(string value)
    {
        var text = FirstNonEmpty(value).Trim();
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        text = Regex.Replace(text, @"\b(?:instruction\s*)?manuals?\b", " ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\binstruction\s*book(?:let)?\b", " ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bbooklet\b", " ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\b(?:usa|us|eu|europe|japan|jp|rev\s*[a-z0-9]+)\b", " ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\((?:usa|us|eu|europe|japan|jp|rev[^)]*)\)", " ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"[\-_]+", " ");
        text = Regex.Replace(text, @"\s+", " ").Trim(' ', '-', '_', '.');
        return string.IsNullOrWhiteSpace(text) ? FirstNonEmpty(value).Trim() : text;
    }

    private static string DetectGuideType(string name, string context)
    {
        var text = $"{name} {context}";
        if (ContainsAnyToken(text, @"\bcollectors?\b", @"\bcollector'?s\b")) return "Collector's Guide";
        if (ContainsAnyToken(text, @"\bofficial\b", @"\bprima\b", @"\bprima\s+games\b", @"\bbrady\b", @"\bbradygames\b", @"\bbrady\s+games\b", @"\bversus\b")) return "Official Guide";
        if (ContainsAnyToken(text, @"\bwalkthroughs?\b")) return "Walkthrough";
        if (ContainsAnyToken(text, @"\btips?\b", @"\bcheats?\b", @"\bcodes?\b")) return "Tips & Cheats";
        if (ContainsAnyToken(text, @"\batlas\b")) return "Atlas";
        if (ContainsAnyToken(text, @"\bcompendium\b")) return "Compendium";
        return "Strategy Guide";
    }

    private static string[] ExtractStrategyGuideTopics(string value)
    {
        var text = value ?? string.Empty;
        var lower = text.ToLowerInvariant();
        var topics = new List<string>();
        void AddIf(string needle, string label)
        {
            if (lower.Contains(needle)) topics.Add(label);
        }

        AddIf("walkthrough", "Walkthrough");
        AddIf("strategy", "Strategy");
        AddIf("secret", "Secrets");
        AddIf("cheat", "Cheats");
        AddIf("code", "Codes");
        AddIf("map", "Maps");
        AddIf("atlas", "Maps");
        AddIf("boss", "Bosses");
        AddIf("bestiary", "Bestiary");
        AddIf("enemy", "Enemies");
        AddIf("weapon", "Weapons");
        AddIf("armor", "Armor");
        AddIf("item", "Items");
        AddIf("quest", "Quests");
        AddIf("side quest", "Side Quests");
        AddIf("character", "Characters");
        AddIf("build", "Character Builds");
        AddIf("level", "Leveling");
        AddIf("mission", "Missions");
        AddIf("scenario", "Scenarios");
        return topics.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;

    private static string[] CleanDistinctStrings(IEnumerable<string?> values)
        => values.Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string ParentFolderName(string relativePath)
    {
        var normalized = (relativePath ?? string.Empty).Replace('\\', '/');
        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > 1 ? parts[^2] : string.Empty;
    }

    private static string TitleCaseFallback(string magazineTitle, string fileNameTitle, string issueNumber, string coverDate)
    {
        if (string.IsNullOrWhiteSpace(magazineTitle)) return fileNameTitle;
        var suffix = new List<string>();
        if (!string.IsNullOrWhiteSpace(issueNumber)) suffix.Add($"Issue #{issueNumber}");
        if (!string.IsNullOrWhiteSpace(coverDate)) suffix.Add(coverDate);
        return suffix.Count == 0 ? magazineTitle : $"{magazineTitle} \u2014 {string.Join(" \u2022 ", suffix)}";
    }

    private static string ExtractIssueNumber(string value)
    {
        value ??= string.Empty;
        var patterns = new[]
        {
            @"(?i)(?:issue|iss|no\.?|number|#)\s*([0-9]{1,4}[a-z]?)",
            @"(?i)\b([0-9]{1,4})\b"
        };
        foreach (var pattern in patterns)
        {
            foreach (Match match in Regex.Matches(value, pattern))
            {
                var candidate = match.Groups[1].Value.Trim();
                if (Regex.IsMatch(candidate, @"^(19|20)\d{2}$")) continue;
                return candidate;
            }
        }
        return string.Empty;
    }

    private static string ExtractFieldFromText(string value, params string[] labels)
    {
        value ??= string.Empty;
        foreach (var label in labels)
        {
            var match = Regex.Match(value, $@"(?i)\b{Regex.Escape(label)}\.?\s*[:#-]?\s*([0-9A-Za-z]+)");
            if (match.Success) return match.Groups[1].Value.Trim();
        }
        return string.Empty;
    }

    private static string ExtractMagazineDate(string value)
    {
        value ??= string.Empty;
        var month = @"(?:Jan(?:uary)?|Feb(?:ruary)?|Mar(?:ch)?|Apr(?:il)?|May|Jun(?:e)?|Jul(?:y)?|Aug(?:ust)?|Sep(?:t(?:ember)?)?|Oct(?:ober)?|Nov(?:ember)?|Dec(?:ember)?)";
        var monthYear = Regex.Match(value, $@"(?i)\b({month})[\s._-]+((?:19|20)\d{{2}})\b");
        if (monthYear.Success) return $"{NormalizeMonth(monthYear.Groups[1].Value)} {monthYear.Groups[2].Value}";
        var yearMonth = Regex.Match(value, $@"(?i)\b((?:19|20)\d{{2}})[\s._-]+({month})\b");
        if (yearMonth.Success) return $"{NormalizeMonth(yearMonth.Groups[2].Value)} {yearMonth.Groups[1].Value}";
        var yearOnly = Regex.Match(value, @"\b(19|20)\d{2}\b");
        return yearOnly.Success ? yearOnly.Value : string.Empty;
    }

    private static string FormatMagazineDate(string year, string month, string day)
    {
        year = (year ?? string.Empty).Trim();
        month = (month ?? string.Empty).Trim();
        day = (day ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(year)) return string.Empty;
        if (string.IsNullOrWhiteSpace(month)) return year;
        var monthName = NormalizeMonth(month);
        if (string.IsNullOrWhiteSpace(monthName)) return year;
        return string.IsNullOrWhiteSpace(day) ? $"{monthName} {year}" : $"{monthName} {day}, {year}";
    }

    private static string NormalizeMonth(string value)
    {
        value = (value ?? string.Empty).Trim();
        if (int.TryParse(value, out var n) && n >= 1 && n <= 12)
            return System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(n);
        var months = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.MonthNames.Where(m => !string.IsNullOrWhiteSpace(m)).ToArray();
        return months.FirstOrDefault(m => m.StartsWith(value, StringComparison.OrdinalIgnoreCase)) ?? value;
    }

    private static string DetectRegion(string value, string relativePath)
    {
        var haystack = $"{value} {relativePath}".ToLowerInvariant();
        if (Regex.IsMatch(haystack, @"\b(us|usa|united states|ntsc-u)\b")) return "US";
        if (Regex.IsMatch(haystack, @"\b(uk|united kingdom|gb|england)\b")) return "UK";
        if (Regex.IsMatch(haystack, @"\b(japan|jp|jpn)\b")) return "Japan";
        if (Regex.IsMatch(haystack, @"\b(europe|eu|pal)\b")) return "Europe";
        if (Regex.IsMatch(haystack, @"\b(australia|au)\b")) return "Australia";
        return string.Empty;
    }

    private static string DetectMagazineCategory(string name, string relativePath, string magazineTitle)
    {
        var haystack = $"{name} {relativePath} {magazineTitle}".ToLowerInvariant();
        if (haystack.Contains("official")) return "Official video game magazine";
        if (haystack.Contains("cheat") || haystack.Contains("codes")) return "Cheats and codes";
        if (haystack.Contains("pc gamer") || haystack.Contains("computer gaming") || haystack.Contains("pc gaming")) return "PC gaming magazine";
        if (haystack.Contains("retro")) return "Retro gaming magazine";
        if (haystack.Contains("arcade")) return "Arcade magazine";
        if (haystack.Contains("import")) return "Import gaming magazine";
        return "Video game magazine";
    }

    private static string ExtractCoverSubject(string title, string magazineTitle, string issueNumber, string coverDate)
    {
        var value = (title ?? string.Empty).Trim();
        foreach (var token in new[] { magazineTitle, issueNumber, coverDate })
        {
            if (!string.IsNullOrWhiteSpace(token))
                value = Regex.Replace(value, Regex.Escape(token), string.Empty, RegexOptions.IgnoreCase).Trim();
        }
        value = Regex.Replace(value, @"(?i)\b(issue|iss|no\.?|number|vol\.?|volume|#)\b\s*[:#-]?\s*[0-9A-Za-z]+", string.Empty).Trim(' ', '-', '_', '.', '\u2022');
        return value.Length >= 4 ? value : string.Empty;
    }

    private static string[] ExtractSpecialFeatures(string value)
    {
        value = (value ?? string.Empty).ToLowerInvariant();
        var features = new List<string>();
        void AddIf(string key, string label) { if (value.Contains(key)) features.Add(label); }
        AddIf("e3", "E3 coverage");
        AddIf("buyer", "Buyer\u2019s guide");
        AddIf("holiday", "Holiday guide");
        AddIf("preview", "Previews");
        AddIf("review", "Reviews");
        AddIf("walkthrough", "Walkthroughs");
        AddIf("cheat", "Cheats");
        AddIf("codes", "Codes");
        AddIf("hardware", "Hardware guide");
        AddIf("import", "Import coverage");
        return CleanDistinctStrings(features);
    }

    private static string[] ExtractIncludedExtras(string value)
    {
        value = (value ?? string.Empty).ToLowerInvariant();
        var extras = new List<string>();
        void AddIf(string key, string label) { if (value.Contains(key)) extras.Add(label); }
        AddIf("demo disc", "Demo disc");
        AddIf("demo cd", "Demo CD");
        AddIf("cd-rom", "CD-ROM");
        AddIf("poster", "Poster");
        AddIf("map", "Map");
        AddIf("insert", "Insert");
        AddIf("supplement", "Supplement");
        AddIf("trading card", "Trading cards");
        return CleanDistinctStrings(extras);
    }

    private static string[] ExtractFeaturedGames(string value)
    {
        value ??= string.Empty;
        var matches = Regex.Matches(value, @"(?i)(?:featured games?|games covered|cover games?|cover subject)\s*[:=-]\s*([^\.\n\r]+)");
        return CleanDistinctStrings(matches.Cast<Match>().SelectMany(m => m.Groups[1].Value.Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));
    }

    private static void AddSplitTags(List<string> tags, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        foreach (var tag in value.Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            tags.Add(tag);
    }

    private static string? KindFromLibraryType(string? libraryType)
    {
        var value = (libraryType ?? string.Empty).Trim().ToLowerInvariant();
        return value switch
        {
            "manual" or "manuals" => "Manual",
            "strategy guide" or "strategy guides" or "guide" or "guides" => "Strategy Guide",
            "magazine" or "magazines" => "Magazine",
            _ => null
        };
    }

    private static bool ContainsAnyToken(string haystack, params string[] patterns)
    {
        var normalized = Regex.Replace(haystack, @"[^a-z0-9]+", " ");
        return patterns.Any(pattern => Regex.IsMatch(normalized, pattern, RegexOptions.IgnoreCase));
    }

    private static bool IsUnsorted(string value)
        => string.IsNullOrWhiteSpace(value) || value.Equals("Unsorted", StringComparison.OrdinalIgnoreCase) || value.StartsWith("Unsorted ", StringComparison.OrdinalIgnoreCase);

    private static string DetectKind(string lowerName, string lowerPath)
    {
        var haystack = lowerPath + " " + lowerName;
        if (ContainsAnyToken(haystack,
            @"\bmagazines?\b",
            @"\bretro\s+gamer\b",
            @"\bnintendo\s+power\b",
            @"\bgamepro\b",
            @"\begm\b",
            @"\belectronic\s+gaming\s+monthly\b"))
        {
            return "Magazine";
        }

        if (ContainsAnyToken(haystack,
            @"\bstrategy\s+guides?\b",
            @"\bguides?\b",
            @"\bwalkthroughs?\b",
            @"\bbradygames\b",
            @"\bbrady\s+games\b",
            @"\bprima\b",
            @"\bprima\s+games\b"))
        {
            return "Strategy Guide";
        }

        return "Manual";
    }

    private static string DetectSystem(string lowerName, string lowerPath)
        => DetectSystemFromText(lowerPath + " " + lowerName);

    public static string DetectSystemFromText(string value)
    {
        var systems = DetectSystemsFromText(value);
        return systems.Length > 0 ? systems[0] : "Unsorted";
    }

    public static string[] DetectSystemsFromText(string value)
    {
        var haystack = value ?? string.Empty;
        var matches = new List<string>();
        void AddIf(string name, params string[] patterns)
        {
            if (ContainsAnyToken(haystack, patterns)) matches.Add(name);
        }

        // Long/specific names first prevents broad aliases like "GB" or "Xbox" from swallowing variants.
        // Keep this list intentionally broad for retro literature libraries, where the platform often only appears in the folder/file name.
        AddIf("Amstrad GX4000", @"\b(amstrad gx4000|gx4000)\b");
        AddIf("Amstrad CPC", @"\b(amstrad cpc|cpc 464|cpc464|cpc 6128|cpc6128)\b");
        AddIf("Atari 2600", @"\b(atari 2600|vcs)\b");
        AddIf("Atari 5200", @"\b(atari 5200)\b");
        AddIf("Atari 7800", @"\b(atari 7800)\b");
        AddIf("Atari Lynx", @"\b(atari lynx|lynx)\b");
        AddIf("Atari Jaguar", @"\b(atari jaguar|jaguar cd|jaguar)\b");
        AddIf("Atari ST", @"\b(atari st)\b");
        AddIf("Atari 8-bit", @"\b(atari 400|atari 800|atari xl|atari xe|atari 8 bit|atari 8-bit)\b");
        AddIf("ColecoVision", @"\b(colecovision|coleco vision)\b");
        AddIf("Intellivision", @"\b(intellivision|mattel intellivision)\b");
        AddIf("Magnavox Odyssey 2", @"\b(odyssey 2|odyssey2|philips videopac|videopac)\b");
        AddIf("Vectrex", @"\b(vectrex)\b");
        AddIf("Daphne Laserdisc Arcade", @"\b(daphne|laserdisc|laser disc|laser game|lasergame)\b");
        AddIf("Philips CD-i", @"\b(cd i|cd-i|philips cdi|philips cd i)\b");
        AddIf("Neo Geo AES", @"\b(neo geo aes|neogeo aes)\b");
        AddIf("Neo Geo CD", @"\b(neo geo cd|neogeo cd)\b");
        AddIf("Neo Geo Pocket Color", @"\b(neo geo pocket color|ngpc)\b");
        AddIf("Neo Geo Pocket", @"\b(neo geo pocket|ngp)\b");
        AddIf("Commodore Amiga", @"\b(amiga|commodore amiga)\b");
        AddIf("Commodore 64", @"\b(commodore 64|c64)\b");
        AddIf("Commodore VIC-20", @"\b(vic 20|vic-20)\b");
        AddIf("MSX", @"\b(msx|msx2)\b");
        AddIf("ZX Spectrum", @"\b(zx spectrum|spectrum)\b");
        AddIf("Sharp X68000", @"\b(x68000|sharp x68000)\b");
        AddIf("FM Towns", @"\b(fm towns|fmtowns)\b");
        AddIf("Apple II", @"\b(apple ii|apple 2|apple iie|apple iic)\b");
        AddIf("Nintendo Switch", @"\b(switch|nintendo switch)\b");
        AddIf("Nintendo Wii U", @"\b(wii u|wiiu)\b");
        AddIf("Nintendo Wii", @"\b(wii)\b");
        AddIf("Nintendo GameCube", @"\b(gamecube|game cube|gcn)\b");
        AddIf("Nintendo 64", @"\b(n64|nintendo 64)\b");
        AddIf("Super Nintendo Entertainment System", @"\b(snes|super nintendo|super famicom)\b");
        AddIf("Nintendo Entertainment System", @"\b(nes|nintendo entertainment system|famicom)\b");
        AddIf("Nintendo Game Boy Advance", @"\b(game boy advance|gba)\b");
        AddIf("Nintendo Game Boy Color", @"\b(game boy color|gbc)\b");
        AddIf("Nintendo Game Boy", @"\b(game boy|gb)\b");
        AddIf("Nintendo DS", @"\b(nintendo ds|nds|ds)\b");
        AddIf("Nintendo 3DS", @"\b(3ds|nintendo 3ds)\b");
        AddIf("Sega Genesis", @"\b(genesis|mega drive)\b");
        AddIf("Sega Master System", @"\b(master system|sms)\b");
        AddIf("Sega Game Gear", @"\b(game gear)\b");
        AddIf("Sega Saturn", @"\b(saturn|sega saturn)\b");
        AddIf("Sega Dreamcast", @"\b(dreamcast)\b");
        AddIf("Sony Playstation 5", @"\b(ps5|playstation 5|sony playstation 5)\b");
        AddIf("Sony Playstation 4", @"\b(ps4|playstation 4|sony playstation 4)\b");
        AddIf("Sony Playstation 3", @"\b(ps3|playstation 3|sony playstation 3)\b");
        AddIf("Sony Playstation 2", @"\b(ps2|playstation 2|sony playstation 2)\b");
        AddIf("Sony Playstation", @"\b(ps1|psx|playstation|sony playstation)\b");
        AddIf("Sony PSP", @"\b(psp|playstation portable)\b");
        AddIf("Sony Playstation Vita", @"\b(vita|playstation vita|ps vita|sony playstation vita)\b");
        AddIf("Microsoft Xbox Series X S", @"\b(xbox series|series x|series s)\b");
        AddIf("Microsoft Xbox One", @"\b(xbox one)\b");
        AddIf("Microsoft Xbox 360", @"\b(xbox 360)\b");
        AddIf("Microsoft Xbox", @"\b(original xbox|xbox)\b");
        AddIf("NEC TurboGrafx 16", @"\b(turbografx|turbo grafx|pc engine)\b");
        AddIf("3DO Interactive Multiplayer", @"\b(3do)\b");
        AddIf("Arcade", @"\b(arcade|mame)\b");
        AddIf("Apple iOS", @"\b(ios|iphone|ipad)\b");
        AddIf("Android", @"\b(android)\b");
        AddIf("Apple Mac OS", @"\b(mac os|macos|os x)\b");
        AddIf("Linux", @"\b(linux)\b");
        AddIf("MS-DOS", @"\b(ms[-\s]?dos|dos)\b");
        AddIf("Windows", @"\b(pc|windows|microsoft windows)\b");

        return matches.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static string NormalizePlatformName(string value)
    {
        var detected = DetectSystemFromText(value);
        if (!IsUnsorted(detected)) return detected;
        var cleaned = (value ?? string.Empty).Trim();
        if (cleaned.Equals("GameCube", StringComparison.OrdinalIgnoreCase)) return "Nintendo GameCube";
        if (cleaned.Equals("Xbox", StringComparison.OrdinalIgnoreCase)) return "Microsoft Xbox";
        if (cleaned.Equals("PlayStation", StringComparison.OrdinalIgnoreCase)) return "Sony Playstation";
        if (cleaned.Equals("PlayStation 2", StringComparison.OrdinalIgnoreCase) || cleaned.Equals("PS2", StringComparison.OrdinalIgnoreCase)) return "Sony Playstation 2";
        if (cleaned.Equals("PlayStation 3", StringComparison.OrdinalIgnoreCase) || cleaned.Equals("PS3", StringComparison.OrdinalIgnoreCase)) return "Sony Playstation 3";
        if (cleaned.Equals("PlayStation 4", StringComparison.OrdinalIgnoreCase) || cleaned.Equals("PS4", StringComparison.OrdinalIgnoreCase)) return "Sony Playstation 4";
        if (cleaned.Equals("PlayStation 5", StringComparison.OrdinalIgnoreCase) || cleaned.Equals("PS5", StringComparison.OrdinalIgnoreCase)) return "Sony Playstation 5";
        if (cleaned.Equals("Microsoft Windows", StringComparison.OrdinalIgnoreCase) || cleaned.Equals("Personal computer", StringComparison.OrdinalIgnoreCase)) return "Windows";
        return cleaned;
    }

    public static string DeriveStrategyGuideGameTitle(params string[] values)
    {
        var value = FirstNonEmpty(values);
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        value = Regex.Replace(value, @"\([^)]*(strategy guide|prima|bradygames|official|unauthorized|walkthrough|guide)[^)]*\)", " ", RegexOptions.IgnoreCase);
        value = Regex.Replace(value, @"\b(prima|bradygames|brady games|versus books|official|complete|unauthorized|authorized|signature series|limited edition|collector'?s edition|player'?s choice)\b", " ", RegexOptions.IgnoreCase);
        value = Regex.Replace(value, @"\b(strategy guide|official strategy guide|player'?s guide|game guide|guidebook|walkthrough|manual|companion)\b", " ", RegexOptions.IgnoreCase);
        value = Regex.Replace(value, @"\b(for|on)\s+(nintendo|playstation|xbox|windows|pc|gamecube|wii|switch|ds|3ds|ps[1-5]|psp|vita)\b.*$", " ", RegexOptions.IgnoreCase);
        value = Regex.Replace(value, @"\s+", " ").Trim(' ', '-', ':', '\u2013', '\u2014');
        return value;
    }

    private static string DetectCategory(string kind, string system, string relativePath, string lowerName, string lowerPath)
    {
        if (kind == "Magazine")
        {
            var series = DetectMagazineSeries(lowerName, lowerPath);
            if (series != "Unsorted Magazines") return series;
            var folderSeries = FirstFolderAfter(relativePath, "magazines") ?? FirstFolderAfter(relativePath, "magazine");
            return string.IsNullOrWhiteSpace(folderSeries) ? "Unsorted Magazines" : folderSeries;
        }

        if (kind == "Strategy Guide")
        {
            return IsUnsorted(system) ? "Unsorted Strategy Guides" : system;
        }

        return system == "Unsorted" ? "Unsorted Manuals" : system;
    }

    public static StrategyGuideBookFields ExtractStrategyGuideBookFields(params string[] values)
    {
        var text = string.Join("\n", values.Where(v => !string.IsNullOrWhiteSpace(v)));
        if (string.IsNullOrWhiteSpace(text)) return new StrategyGuideBookFields(string.Empty, string.Empty, string.Empty, string.Empty);

        static string CleanBookCode(string value) => Regex.Replace(value ?? string.Empty, @"[^0-9Xx]", string.Empty).ToUpperInvariant();
        static string CleanLanguage(string value) => (value ?? string.Empty).Trim().Replace('_', '-');

        var asin = Regex.Match(text, @"\bASIN\s*[:#-]?\s*([A-Z0-9]{10})\b", RegexOptions.IgnoreCase).Groups[1].Value.ToUpperInvariant();
        var isbn13 = CleanBookCode(Regex.Match(text, @"\bISBN(?:[-\s]?13)?\s*[:#-]?\s*((?:97[89])[0-9Xx\-\s]{10,20})", RegexOptions.IgnoreCase).Groups[1].Value);
        var isbn10 = CleanBookCode(Regex.Match(text, @"\bISBN(?:[-\s]?10)?\s*[:#-]?\s*([0-9Xx][0-9Xx\-\s]{8,18}[0-9Xx])", RegexOptions.IgnoreCase).Groups[1].Value);

        if (!string.IsNullOrWhiteSpace(isbn10) && isbn10.Length == 13 && isbn10.StartsWith("97"))
        {
            if (string.IsNullOrWhiteSpace(isbn13)) isbn13 = isbn10;
            isbn10 = string.Empty;
        }
        if (!string.IsNullOrWhiteSpace(isbn13) && isbn13.Length == 10)
        {
            if (string.IsNullOrWhiteSpace(isbn10)) isbn10 = isbn13;
            isbn13 = string.Empty;
        }

        var language = CleanLanguage(Regex.Match(text, @"\bLanguage(?:\s*(?:ISO|Tag|Code))?\s*[:#-]?\s*([A-Za-z]{2,3}(?:[-_][A-Za-z0-9]{2,8})?)\b", RegexOptions.IgnoreCase).Groups[1].Value);
        return new StrategyGuideBookFields(asin, isbn10, isbn13, language);
    }

    private static string DetectMagazineSeries(string lowerName, string lowerPath)
    {
        var haystack = lowerPath + " " + lowerName;
        return haystack.Contains("nintendo power") ? "Nintendo Power"
            : haystack.Contains("electronic gaming monthly") || haystack.Contains("egm") ? "Electronic Gaming Monthly"
            : haystack.Contains("gamepro") ? "GamePro"
            : haystack.Contains("retro gamer") ? "Retro Gamer"
            : haystack.Contains("official xbox magazine") ? "Official Xbox Magazine"
            : haystack.Contains("official playstation magazine") ? "Official PlayStation Magazine"
            : haystack.Contains("pc gamer") ? "PC Gamer"
            : "Unsorted Magazines";
    }

    private static string? FirstFolderAfter(string relativePath, string marker)
    {
        var parts = relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length - 1; i++)
        {
            var normalized = parts[i].Trim().ToLowerInvariant();
            if (normalized == marker || normalized == marker.Replace(" ", ""))
            {
                return CleanFolderName(parts[i + 1]);
            }
        }
        return null;
    }

    private static string CleanFolderName(string value)
    {
        return value.Replace('_', ' ').Replace('.', ' ').Trim();
    }

    private static string DetectPublisher(string lowerName, string lowerPath)
    {
        var haystack = lowerPath + " " + lowerName;
        return ContainsAnyToken(haystack, @"\bnintendo\s+power\b") ? "Nintendo of America"
            : ContainsAnyToken(haystack, @"\bretro\s+gamer\b") ? "Future Publishing"
            : ContainsAnyToken(haystack, @"\bbrady\b", @"\bbradygames\b", @"\bbrady\s+games\b") ? "BradyGames"
            : ContainsAnyToken(haystack, @"\bprima\b", @"\bprima\s+games\b") ? "Prima"
            : ContainsAnyToken(haystack, @"\bgamepro\b") ? "IDG"
            : "Unknown";
    }
}


public sealed record StrategyPlatformResolution(string CandidateGameTitle, string[] AssociatedPlatforms, string DetectedSystem, string Source, double Confidence, string Message, string Asin = "", string Isbn10 = "", string Isbn13 = "", string LanguageTag = "", string Rating = "");
public sealed record StrategyGuideBookMetadata(string Title, string Asin, string Isbn10, string Isbn13, string LanguageTag);

public static class StrategyGuidePlatformResolver
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public static async Task<StrategyPlatformResolution> ResolveAsync(LibraryItem item)
    {
        var localPlatforms = MetadataInferer.DetectSystemsFromText($"{item.System} {item.Category} {string.Join(" ", item.Tags ?? Array.Empty<string>())} {item.Summary} {item.RelativePath} {item.FileName}");
        var book = await TryGetOpenLibraryBookAsync(item);
        var candidate = MetadataInferer.DeriveStrategyGuideGameTitle(book.Title, item.PlatformMatchTitle, item.Series, item.Title, item.FileName);
        if (string.IsNullOrWhiteSpace(candidate)) candidate = item.Title;

        var online = await TryResolveFromWikidataAsync(candidate);
        if (online.AssociatedPlatforms.Length > 0)
        {
            return online with
            {
                CandidateGameTitle = candidate,
                Asin = FirstNonEmpty(book.Asin, online.Asin, item.Asin),
                Isbn10 = FirstNonEmpty(book.Isbn10, online.Isbn10, item.Isbn10),
                Isbn13 = FirstNonEmpty(book.Isbn13, online.Isbn13, item.Isbn13),
                LanguageTag = FirstNonEmpty(book.LanguageTag, online.LanguageTag, item.LanguageTag),
                Rating = FirstNonEmpty(online.Rating, item.Rating),
                Message = online.AssociatedPlatforms.Length > 1
                    ? $"Found {online.AssociatedPlatforms.Length} likely platforms for {candidate}."
                    : $"Found one likely platform for {candidate}."
            };
        }

        if (localPlatforms.Length > 0)
        {
            var detected = localPlatforms[0];
            return new StrategyPlatformResolution(
                candidate,
                localPlatforms,
                detected,
                "Local metadata/folder detection",
                0.64,
                "No online platform match was found; kept the local platform detection.",
                Asin: FirstNonEmpty(book.Asin, item.Asin),
                Isbn10: FirstNonEmpty(book.Isbn10, item.Isbn10),
                Isbn13: FirstNonEmpty(book.Isbn13, item.Isbn13),
                LanguageTag: FirstNonEmpty(book.LanguageTag, item.LanguageTag),
                Rating: item.Rating);
        }

        return new StrategyPlatformResolution(
            candidate,
            [],
            "Unsorted Strategy Guides",
            "No confident platform source",
            0,
            "No platform could be detected. Kept this under Unsorted Strategy Guides.",
            Asin: FirstNonEmpty(book.Asin, item.Asin),
            Isbn10: FirstNonEmpty(book.Isbn10, item.Isbn10),
            Isbn13: FirstNonEmpty(book.Isbn13, item.Isbn13),
            LanguageTag: FirstNonEmpty(book.LanguageTag, item.LanguageTag),
            Rating: item.Rating);
    }

    private static async Task<StrategyGuideBookMetadata> TryGetOpenLibraryBookAsync(LibraryItem item)
    {
        var empty = new StrategyGuideBookMetadata(string.Empty, item.Asin, item.Isbn10, item.Isbn13, item.LanguageTag);
        var isbn = FirstNonEmpty(item.Isbn13, item.Isbn10);
        try
        {
            using var client = CreateHttpClient();
            if (!string.IsNullOrWhiteSpace(isbn))
            {
                using var cts = new CancellationTokenSource(Timeout);
                var url = $"https://openlibrary.org/isbn/{Uri.EscapeDataString(isbn)}.json";
                var json = await client.GetStringAsync(url, cts.Token);
                using var doc = JsonDocument.Parse(json);
                var meta = BookMetadataFromOpenLibraryElement(doc.RootElement);
                if (!IsBookMetadataEmpty(meta)) return MergeBookMetadata(meta, empty);
            }

            var searchQuery = FirstNonEmpty(isbn, item.Title, item.FileName);
            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                using var cts = new CancellationTokenSource(Timeout);
                var url = "https://openlibrary.org/search.json?limit=1&fields=title,subtitle,isbn,language,id_amazon&q=" + Uri.EscapeDataString(searchQuery);
                var json = await client.GetStringAsync(url, cts.Token);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("docs", out var docs) && docs.ValueKind == JsonValueKind.Array)
                {
                    var first = docs.EnumerateArray().FirstOrDefault();
                    if (first.ValueKind == JsonValueKind.Object)
                    {
                        var meta = BookMetadataFromOpenLibraryElement(first);
                        if (!IsBookMetadataEmpty(meta)) return MergeBookMetadata(meta, empty);
                    }
                }
            }
        }
        catch
        {
            // Book metadata is best-effort and should never block platform lookup.
        }

        return empty;
    }

    private static StrategyGuideBookMetadata BookMetadataFromOpenLibraryElement(JsonElement root)
    {
        var title = GetString(root, "title");
        var subtitle = GetString(root, "subtitle");
        var fullTitle = FirstNonEmpty($"{title} {subtitle}".Trim(), title);
        var isbn10 = FirstArrayString(root, "isbn_10");
        var isbn13 = FirstArrayString(root, "isbn_13");
        if (string.IsNullOrWhiteSpace(isbn10) || string.IsNullOrWhiteSpace(isbn13))
        {
            var anyIsbn = AllArrayStrings(root, "isbn");
            if (string.IsNullOrWhiteSpace(isbn13)) isbn13 = anyIsbn.FirstOrDefault(v => Regex.Replace(v, @"[^0-9Xx]", "").Length == 13) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(isbn10)) isbn10 = anyIsbn.FirstOrDefault(v => Regex.Replace(v, @"[^0-9Xx]", "").Length == 10) ?? string.Empty;
        }
        var asin = FirstArrayString(root, "id_amazon");
        var language = FirstArrayString(root, "language");
        if (string.IsNullOrWhiteSpace(language) && root.TryGetProperty("languages", out var languages) && languages.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in languages.EnumerateArray())
            {
                var key = GetString(item, "key");
                if (string.IsNullOrWhiteSpace(key)) continue;
                language = key.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? key;
                break;
            }
        }
        return new StrategyGuideBookMetadata(fullTitle, asin, CleanBookCode(isbn10), CleanBookCode(isbn13), CleanLanguageTag(language));
    }

    private static StrategyGuideBookMetadata MergeBookMetadata(StrategyGuideBookMetadata primary, StrategyGuideBookMetadata fallback)
        => new(
            FirstNonEmpty(primary.Title, fallback.Title),
            FirstNonEmpty(primary.Asin, fallback.Asin),
            FirstNonEmpty(primary.Isbn10, fallback.Isbn10),
            FirstNonEmpty(primary.Isbn13, fallback.Isbn13),
            FirstNonEmpty(primary.LanguageTag, fallback.LanguageTag));

    private static bool IsBookMetadataEmpty(StrategyGuideBookMetadata value)
        => string.IsNullOrWhiteSpace(value.Title)
           && string.IsNullOrWhiteSpace(value.Asin)
           && string.IsNullOrWhiteSpace(value.Isbn10)
           && string.IsNullOrWhiteSpace(value.Isbn13)
           && string.IsNullOrWhiteSpace(value.LanguageTag);

    private static string FirstArrayString(JsonElement element, string property)
        => AllArrayStrings(element, property).FirstOrDefault() ?? string.Empty;

    private static string[] AllArrayStrings(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return [];
        if (value.ValueKind == JsonValueKind.String) return [value.GetString() ?? string.Empty];
        if (value.ValueKind != JsonValueKind.Array) return [];
        return value.EnumerateArray()
            .Select(v => v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToArray();
    }

    private static string CleanBookCode(string value)
        => Regex.Replace(value ?? string.Empty, @"[^0-9Xx]", string.Empty).ToUpperInvariant();

    private static string CleanLanguageTag(string value)
        => (value ?? string.Empty).Trim().Trim('/').Replace('_', '-');

    private static async Task<StrategyPlatformResolution> TryResolveFromWikidataAsync(string candidateTitle)
    {
        if (string.IsNullOrWhiteSpace(candidateTitle))
            return new StrategyPlatformResolution(string.Empty, [], "Unsorted Strategy Guides", "Wikidata", 0, "No candidate game title was available for lookup.");

        try
        {
            using var client = CreateHttpClient();
            var queries = new[]
            {
                candidateTitle,
                $"{candidateTitle} video game",
                Regex.Replace(candidateTitle, @"\b(the|a|an)\b", " ", RegexOptions.IgnoreCase).Trim()
            }.Where(q => !string.IsNullOrWhiteSpace(q)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

            foreach (var query in queries)
            {
                var candidates = await SearchWikidataAsync(client, query);
                foreach (var candidate in candidates)
                {
                    var platforms = await GetWikidataPlatformsAsync(client, candidate.Id);
                    if (platforms.Length == 0) continue;
                    var normalized = platforms.Select(MetadataInferer.NormalizePlatformName)
                        .Where(p => !string.IsNullOrWhiteSpace(p) && !p.Equals("Unsorted", StringComparison.OrdinalIgnoreCase))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    if (normalized.Length == 0) continue;

                    var rating = (await GetWikidataClaimLabelsAsync(client, candidate.Id, "P852")).FirstOrDefault() ?? string.Empty;
                    var labelMatch = NormalizeForCompare(candidate.Label).Equals(NormalizeForCompare(candidateTitle), StringComparison.OrdinalIgnoreCase);
                    var confidence = labelMatch ? 0.82 : 0.72;
                    var detected = normalized[0];
                    return new StrategyPlatformResolution(candidate.Label, normalized, detected, $"Wikidata: {candidate.Label}", confidence, string.Empty, Rating: rating);
                }
            }
        }
        catch
        {
            // Online resolution is best-effort and should never break local scanning.
        }

        return new StrategyPlatformResolution(candidateTitle, [], "Unsorted Strategy Guides", "Wikidata", 0, "No Wikidata platform match was found.");
    }

    private sealed record WikidataSearchCandidate(string Id, string Label, string Description);

    private static async Task<WikidataSearchCandidate[]> SearchWikidataAsync(HttpClient client, string query)
    {
        using var cts = new CancellationTokenSource(Timeout);
        var url = "https://www.wikidata.org/w/api.php?action=wbsearchentities&type=item&language=en&format=json&limit=8&search=" + Uri.EscapeDataString(query);
        var json = await client.GetStringAsync(url, cts.Token);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("search", out var search) || search.ValueKind != JsonValueKind.Array) return [];
        var results = new List<WikidataSearchCandidate>();
        foreach (var item in search.EnumerateArray())
        {
            var id = GetString(item, "id");
            var label = GetString(item, "label");
            var description = GetString(item, "description");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(label)) continue;
            var desc = description.ToLowerInvariant();
            // Prefer game entities, but don't exclude all entries here because some games have sparse descriptions.
            if (desc.Contains("video game") || desc.Contains("computer game") || desc.Contains("game series") || results.Count < 3)
                results.Add(new WikidataSearchCandidate(id, label, description));
        }
        return results.ToArray();
    }

    private static Task<string[]> GetWikidataPlatformsAsync(HttpClient client, string entityId)
        => GetWikidataClaimLabelsAsync(client, entityId, "P400");

    private static async Task<string[]> GetWikidataClaimLabelsAsync(HttpClient client, string entityId, string propertyId)
    {
        using var cts = new CancellationTokenSource(Timeout);
        var url = $"https://www.wikidata.org/w/api.php?action=wbgetentities&ids={Uri.EscapeDataString(entityId)}&props=claims&format=json";
        var json = await client.GetStringAsync(url, cts.Token);
        using var doc = JsonDocument.Parse(json);
        var ids = new List<string>();
        if (!doc.RootElement.TryGetProperty("entities", out var entities)) return [];
        if (!entities.TryGetProperty(entityId, out var entity)) return [];
        if (!entity.TryGetProperty("claims", out var claims)) return [];
        if (!claims.TryGetProperty(propertyId, out var propertyClaims) || propertyClaims.ValueKind != JsonValueKind.Array) return [];
        foreach (var claim in propertyClaims.EnumerateArray())
        {
            if (!claim.TryGetProperty("mainsnak", out var mainsnak)) continue;
            if (!mainsnak.TryGetProperty("datavalue", out var datavalue)) continue;
            if (!datavalue.TryGetProperty("value", out var value)) continue;
            var id = GetString(value, "id");
            if (!string.IsNullOrWhiteSpace(id)) ids.Add(id);
        }
        return await GetWikidataLabelsAsync(client, ids.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static async Task<string[]> GetWikidataLabelsAsync(HttpClient client, string[] ids)
    {
        if (ids.Length == 0) return [];
        using var cts = new CancellationTokenSource(Timeout);
        var joined = string.Join('|', ids);
        var url = $"https://www.wikidata.org/w/api.php?action=wbgetentities&ids={Uri.EscapeDataString(joined)}&props=labels&languages=en&format=json";
        var json = await client.GetStringAsync(url, cts.Token);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("entities", out var entities)) return [];
        var labels = new List<string>();
        foreach (var id in ids)
        {
            if (!entities.TryGetProperty(id, out var entity)) continue;
            if (!entity.TryGetProperty("labels", out var labelRoot)) continue;
            if (!labelRoot.TryGetProperty("en", out var en)) continue;
            var value = GetString(en, "value");
            if (!string.IsNullOrWhiteSpace(value)) labels.Add(value);
        }
        return labels.ToArray();
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = Timeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Guidevault/0.9.30 (+local strategy guide platform resolver)");
        return client;
    }

    private static string GetString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;


    private static string DetectGuideType(string name, string context)
    {
        var text = Regex.Replace($"{name} {context}", @"[^a-zA-Z0-9]+", " ");
        static bool Has(string value, params string[] patterns) => patterns.Any(pattern => Regex.IsMatch(value, pattern, RegexOptions.IgnoreCase));
        if (Has(text, @"\bcollectors?\b", @"\bcollector'?s\b")) return "Collector's Guide";
        if (Has(text, @"\bofficial\b", @"\bprima\b", @"\bprima\s+games\b", @"\bbrady\b", @"\bbradygames\b", @"\bbrady\s+games\b", @"\bversus\b")) return "Official Guide";
        if (Has(text, @"\bwalkthroughs?\b")) return "Walkthrough";
        if (Has(text, @"\btips?\b", @"\bcheats?\b", @"\bcodes?\b")) return "Tips & Cheats";
        if (Has(text, @"\batlas\b")) return "Atlas";
        if (Has(text, @"\bcompendium\b")) return "Compendium";
        return "Strategy Guide";
    }

    private static string[] ExtractStrategyGuideTopics(string value)
    {
        var text = value ?? string.Empty;
        var lower = text.ToLowerInvariant();
        var topics = new List<string>();
        void AddIf(string needle, string label)
        {
            if (lower.Contains(needle)) topics.Add(label);
        }

        AddIf("walkthrough", "Walkthrough");
        AddIf("strategy", "Strategy");
        AddIf("secret", "Secrets");
        AddIf("cheat", "Cheats");
        AddIf("code", "Codes");
        AddIf("map", "Maps");
        AddIf("atlas", "Maps");
        AddIf("boss", "Bosses");
        AddIf("bestiary", "Bestiary");
        AddIf("enemy", "Enemies");
        AddIf("weapon", "Weapons");
        AddIf("armor", "Armor");
        AddIf("item", "Items");
        AddIf("quest", "Quests");
        AddIf("side quest", "Side Quests");
        AddIf("character", "Characters");
        AddIf("build", "Character Builds");
        AddIf("level", "Leveling");
        AddIf("mission", "Missions");
        AddIf("scenario", "Scenarios");
        return topics.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;

    private static string NormalizeForCompare(string value)
        => Regex.Replace((value ?? string.Empty).ToLowerInvariant(), @"[^a-z0-9]+", " ").Trim();
}

public static class GuidevaultNativeMetadata
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static object BuildExport(LibraryItem item, ItemMetadataUpdate update, JsonElement? submittedMetadata = null)
    {
        if (submittedMetadata.HasValue && submittedMetadata.Value.ValueKind == JsonValueKind.Object)
            return BuildExportFromSubmittedMetadata(item, update, submittedMetadata.Value);

        var metadata = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["title"] = First(update.Title, item.Title),
            ["kind"] = First(update.Kind, item.Kind),
            ["category"] = First(update.Category, item.Category),
            ["preferredPlatform"] = First(update.Category, item.Category),
            ["system"] = First(update.System, item.System),
            ["associatedPlatforms"] = update.AssociatedPlatforms ?? item.AssociatedPlatforms,
            ["series"] = First(update.Series, item.Series),
            ["issueNumber"] = First(update.IssueNumber, item.IssueNumber),
            ["publisher"] = First(update.Publisher, item.Publisher),
            ["year"] = First(update.Year, item.Year),
            ["pageCount"] = update.PageCount.HasValue && update.PageCount.Value > 0 ? update.PageCount.Value : item.PageCount,
            ["metadataPageCount"] = update.PageCount.HasValue && update.PageCount.Value > 0 ? update.PageCount.Value : item.PageCount,
            ["writer"] = First(update.Writer, item.Writer),
            ["rating"] = First(update.Rating, item.Rating),
            ["language"] = First(update.LanguageTag, item.LanguageTag),
            ["languageTag"] = First(update.LanguageTag, item.LanguageTag),
            ["summary"] = First(update.Summary, item.Summary),
            ["tags"] = update.Tags ?? item.Tags,
            ["notes"] = First(update.Notes, item.Notes),
            ["metadataStatus"] = MetadataStatusHelper.Normalize(update.MetadataStatus, item.MetadataStatus),
            ["metadataSource"] = "Guidevault JSON"
        };
        var exportLocks = MetadataLockHelper.MergeLocks(item.MetadataLocks, update.MetadataLocks);
        if (exportLocks is not null && exportLocks.Count > 0)
            metadata["metadataLocks"] = exportLocks;

        var kind = First(update.Kind, item.Kind);
        if (kind.Equals("Magazine", StringComparison.OrdinalIgnoreCase))
        {
            Put(metadata, "magazineTitle", First(update.MagazineTitle, item.MagazineTitle));
            Put(metadata, "volume", First(update.Volume, item.Volume));
            Put(metadata, "coverDate", First(update.CoverDate, item.CoverDate));
            Put(metadata, "publicationDate", First(update.PublicationDate, item.PublicationDate));
            Put(metadata, "region", First(update.Region, item.Region));
            Put(metadata, "platformFocus", First(update.PlatformFocus, item.PlatformFocus));
            Put(metadata, "primarySystem", First(update.PrimarySystem, item.PrimarySystem));
            Put(metadata, "magazineCategory", First(update.MagazineCategory, item.MagazineCategory));
            Put(metadata, "coverSubject", First(update.CoverSubject, item.CoverSubject));
            Put(metadata, "barcodeUpcIssn", First(update.BarcodeUpcIssn, item.BarcodeUpcIssn));
            Put(metadata, "featuredGames", update.FeaturedGames ?? item.FeaturedGames);
            Put(metadata, "featuredPlatforms", update.FeaturedPlatforms ?? item.FeaturedPlatforms);
            Put(metadata, "specialFeatures", update.SpecialFeatures ?? item.SpecialFeatures);
            Put(metadata, "includedExtras", update.IncludedExtras ?? item.IncludedExtras);
        }
        else if (kind.Equals("Manual", StringComparison.OrdinalIgnoreCase))
        {
            Put(metadata, "manualTitle", First(update.ManualTitle, item.ManualTitle));
            Put(metadata, "manualType", First(update.ManualType, item.ManualType));
            Put(metadata, "gameTitle", First(update.GameTitle, item.GameTitle));
            Put(metadata, "publicationDate", First(update.PublicationDate, item.PublicationDate));
            Put(metadata, "region", First(update.Region, item.Region));
            Put(metadata, "franchise", First(update.Franchise, item.Franchise));
            Put(metadata, "gameFranchise", First(update.Franchise, item.Franchise));
            Put(metadata, "developer", First(update.Developer, item.Developer));
            Put(metadata, "gameDeveloper", First(update.Developer, item.Developer));
            Put(metadata, "gamePublisher", First(update.GamePublisher, item.GamePublisher));
            Put(metadata, "gameReleaseYear", First(update.GameReleaseYear, item.GameReleaseYear));
            Put(metadata, "genre", First(update.Genre, item.Genre));
            Put(metadata, "includedSections", update.IncludedSections ?? item.IncludedSections ?? []);
            Put(metadata, "includedExtras", update.IncludedExtras ?? item.IncludedExtras);
            Put(metadata, "controlScheme", First(update.ControlScheme, item.ControlScheme));
            Put(metadata, "charactersCovered", update.CharactersCovered ?? item.CharactersCovered ?? []);
            Put(metadata, "itemsCovered", update.ItemsCovered ?? item.ItemsCovered ?? []);
            Put(metadata, "warrantySupport", First(update.WarrantySupport, item.WarrantySupport));
        }
        else
        {
            Put(metadata, "strategyGuideTitle", First(update.Title, item.Title));
            Put(metadata, "gameTitle", First(update.GameTitle, item.GameTitle));
            Put(metadata, "isbn", JoinNonEmpty(First(update.Isbn10, item.Isbn10), First(update.Isbn13, item.Isbn13)));
            Put(metadata, "isbn10", First(update.Isbn10, item.Isbn10));
            Put(metadata, "isbn13", First(update.Isbn13, item.Isbn13));
            Put(metadata, "guideType", First(update.GuideType, item.GuideType));
            Put(metadata, "editionType", First(update.Edition, item.Edition));
            Put(metadata, "edition", First(update.Edition, item.Edition));
            Put(metadata, "franchise", First(update.Franchise, item.Franchise));
            Put(metadata, "gameFranchise", First(update.Franchise, item.Franchise));
            Put(metadata, "publicationDate", First(update.PublicationDate, item.PublicationDate));
            Put(metadata, "region", First(update.Region, item.Region));
            Put(metadata, "developer", First(update.Developer, item.Developer));
            Put(metadata, "gameDeveloper", First(update.Developer, item.Developer));
            Put(metadata, "gamePublisher", First(update.GamePublisher, item.GamePublisher));
            Put(metadata, "gameReleaseYear", First(update.GameReleaseYear, item.GameReleaseYear));
            Put(metadata, "genre", First(update.Genre, item.Genre));
            Put(metadata, "coveredGames", update.CoveredGames ?? item.CoveredGames ?? []);
            Put(metadata, "coveredPlatforms", update.CoveredPlatforms ?? item.CoveredPlatforms ?? []);
            Put(metadata, "guideTopics", update.GuideTopics ?? item.GuideTopics ?? []);
            Put(metadata, "specialFeatures", update.SpecialFeatures ?? item.SpecialFeatures);
            Put(metadata, "includedExtras", update.IncludedExtras ?? item.IncludedExtras);
            Put(metadata, "charactersCovered", update.CharactersCovered ?? item.CharactersCovered ?? []);
            Put(metadata, "locationsCovered", update.LocationsCovered ?? item.LocationsCovered ?? []);
        }

        var fileSizeBytes = item.SizeBytes;
        var exportedItem = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["suggestedFileName"] = SuggestedFileName(item, metadata),
            ["fileSizeBytes"] = fileSizeBytes,
            ["format"] = item.Format,
            ["kind"] = kind,
            ["metadataStatus"] = metadata.TryGetValue("metadataStatus", out var ms) ? ms : MetadataStatusHelper.Normalize(update.MetadataStatus, item.MetadataStatus),
            ["pageCount"] = metadata.TryGetValue("pageCount", out var pc) ? pc : item.PageCount,
            ["metadata"] = metadata
        };

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["schema"] = "guidevault.item-metadata.v1",
            ["exportedAt"] = DateTimeOffset.UtcNow,
            ["guidevaultVersion"] = GuidevaultBuildInfo.Version,
            ["exportScope"] = "item",
            ["item"] = exportedItem
        };
    }

    private static object BuildExportFromSubmittedMetadata(LibraryItem item, ItemMetadataUpdate update, JsonElement submittedMetadata)
    {
        var metadata = JsonSerializer.Deserialize<Dictionary<string, object?>>(submittedMetadata.GetRawText(), JsonOptions)
            ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        metadata = new Dictionary<string, object?>(metadata, StringComparer.OrdinalIgnoreCase);

        void PutIfMissing(string key, object? value)
        {
            if (value is null) return;
            if (metadata.TryGetValue(key, out var existing) && !IsEmptyValue(existing)) return;
            metadata[key] = value;
        }

        var kind = First(update.Kind, item.Kind);
        var pageCount = update.PageCount.HasValue && update.PageCount.Value > 0 ? update.PageCount.Value : item.PageCount;
        PutIfMissing("title", First(update.Title, item.Title));
        PutIfMissing("kind", kind);
        PutIfMissing("category", First(update.Category, item.Category));
        PutIfMissing("preferredPlatform", First(update.Category, item.Category));
        PutIfMissing("system", First(update.System, item.System));
        PutIfMissing("publisher", First(update.Publisher, item.Publisher));
        PutIfMissing("year", First(update.Year, item.Year));
        PutIfMissing("languageTag", First(update.LanguageTag, item.LanguageTag));
        PutIfMissing("metadataStatus", MetadataStatusHelper.Normalize(update.MetadataStatus, item.MetadataStatus));
        PutIfMissing("tags", update.Tags ?? item.Tags);
        if (pageCount > 0)
        {
            metadata["pageCount"] = pageCount;
            metadata["metadataPageCount"] = pageCount;
        }
        metadata["metadataSource"] = "Guidevault JSON";
        var exportLocks = MetadataLockHelper.MergeLocks(item.MetadataLocks, update.MetadataLocks);
        if (exportLocks is not null && exportLocks.Count > 0 && !metadata.ContainsKey("metadataLocks"))
            metadata["metadataLocks"] = exportLocks;
        PutIfMissing("gameFranchise", First(update.Franchise, item.Franchise));
        PutIfMissing("franchise", First(update.Franchise, item.Franchise));
        PutIfMissing("gameDeveloper", First(update.Developer, item.Developer));
        PutIfMissing("developer", First(update.Developer, item.Developer));
        PutIfMissing("gamePublisher", First(update.GamePublisher, item.GamePublisher));
        PutIfMissing("strategyGuideTitle", First(update.Title, item.Title));
        PutIfMissing("manualTitle", First(update.ManualTitle, item.ManualTitle));
        PutIfMissing("magazineTitle", First(update.MagazineTitle, item.MagazineTitle));
        PutIfMissing("barcodeUpcIssn", First(update.BarcodeUpcIssn, item.BarcodeUpcIssn));

        var exportedItem = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["suggestedFileName"] = SuggestedFileName(item, metadata),
            ["fileSizeBytes"] = item.SizeBytes,
            ["format"] = item.Format,
            ["kind"] = kind,
            ["metadataStatus"] = metadata.TryGetValue("metadataStatus", out var ms2) ? ms2 : MetadataStatusHelper.Normalize(update.MetadataStatus, item.MetadataStatus),
            ["metadata"] = metadata
        };
        if (pageCount > 0) exportedItem["pageCount"] = pageCount;

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["schema"] = "guidevault.item-metadata.v1",
            ["exportedAt"] = DateTimeOffset.UtcNow,
            ["guidevaultVersion"] = GuidevaultBuildInfo.Version,
            ["exportScope"] = "item",
            ["item"] = exportedItem
        };
    }

    public static string BuildSuggestedFileName(LibraryItem item, ItemMetadataUpdate update, JsonElement? submittedMetadata = null)
    {
        if (submittedMetadata.HasValue && submittedMetadata.Value.ValueKind == JsonValueKind.Object)
        {
            var submitted = submittedMetadata.Value;
            var explicitTarget = GetSubmittedString(submitted, "targetFileName");
            if (!string.IsNullOrWhiteSpace(explicitTarget))
                return SanitizeSuggestedFileName(explicitTarget, item.FileName);

            var schema = GetSubmittedString(submitted, "namingSchema");
            if (!string.IsNullOrWhiteSpace(schema))
            {
                var metadata = JsonSerializer.Deserialize<Dictionary<string, object?>>(submitted.GetRawText(), JsonOptions)
                    ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                metadata = new Dictionary<string, object?>(metadata, StringComparer.OrdinalIgnoreCase);
                var pageCount = update.PageCount.HasValue && update.PageCount.Value > 0 ? update.PageCount.Value : item.PageCount;
                PutIfMissing(metadata, "title", First(update.Title, item.Title));
                PutIfMissing(metadata, "strategyGuideTitle", First(update.Title, item.Title));
                PutIfMissing(metadata, "manualTitle", First(update.ManualTitle, item.ManualTitle));
                PutIfMissing(metadata, "magazineTitle", First(update.MagazineTitle, item.MagazineTitle));
                PutIfMissing(metadata, "gameTitle", First(update.GameTitle, item.GameTitle));
                PutIfMissing(metadata, "preferredPlatform", First(update.Category, item.Category));
                PutIfMissing(metadata, "category", First(update.Category, item.Category));
                PutIfMissing(metadata, "publisher", First(update.Publisher, item.Publisher));
                PutIfMissing(metadata, "year", First(update.Year, item.Year));
                PutIfMissing(metadata, "issueNumber", First(update.IssueNumber, item.IssueNumber));
                PutIfMissing(metadata, "volume", First(update.Volume, item.Volume));
                PutIfMissing(metadata, "kind", First(update.Kind, item.Kind));
                PutIfMissing(metadata, "region", First(update.Region, item.Region));
                PutIfMissing(metadata, "language", First(update.LanguageTag, item.LanguageTag));
                PutIfMissing(metadata, "languageTag", First(update.LanguageTag, item.LanguageTag));
                PutIfMissing(metadata, "edition", First(update.Edition, item.Edition));
                PutIfMissing(metadata, "guideType", First(update.GuideType, item.GuideType));
                PutIfMissing(metadata, "franchise", First(update.Franchise, item.Franchise));
                PutIfMissing(metadata, "gameFranchise", First(update.Franchise, item.Franchise));
                PutIfMissing(metadata, "developer", First(update.Developer, item.Developer));
                PutIfMissing(metadata, "gameDeveloper", First(update.Developer, item.Developer));
                PutIfMissing(metadata, "gamePublisher", First(update.GamePublisher, First(item.GamePublisher, First(update.Publisher, item.Publisher))));
                PutIfMissing(metadata, "gameReleaseYear", First(update.GameReleaseYear, item.GameReleaseYear));
                PutIfMissing(metadata, "genre", First(update.Genre, item.Genre));
                PutIfMissing(metadata, "isbn10", First(update.Isbn10, item.Isbn10));
                PutIfMissing(metadata, "isbn13", First(update.Isbn13, item.Isbn13));
                PutIfMissing(metadata, "isbn", First(update.Isbn13, First(item.Isbn13, First(update.Isbn10, item.Isbn10))));
                PutIfMissing(metadata, "asin", First(update.Asin, item.Asin));
                PutIfMissing(metadata, "manualType", First(update.ManualType, item.ManualType));
                PutIfMissing(metadata, "controlScheme", First(update.ControlScheme, item.ControlScheme));
                PutIfMissing(metadata, "coverStory", First(update.CoverSubject, item.CoverSubject));
                PutIfMissing(metadata, "barcodeUpcIssn", First(update.BarcodeUpcIssn, item.BarcodeUpcIssn));
                if (pageCount > 0) PutIfMissing(metadata, "pageCount", pageCount);

                return SanitizeSuggestedFileName(ApplyNamingSchema(schema, item, metadata), item.FileName);
            }
        }

        var export = BuildExport(item, update, submittedMetadata);
        if (export is IDictionary<string, object?> root
            && root.TryGetValue("item", out var itemObject)
            && itemObject is IDictionary<string, object?> exportedItem
            && exportedItem.TryGetValue("suggestedFileName", out var suggested)
            && suggested is not null)
        {
            return SanitizeSuggestedFileName(suggested.ToString() ?? string.Empty, item.FileName);
        }

        return SanitizeSuggestedFileName(SuggestedFileName(item, new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["manualTitle"] = update.ManualTitle,
            ["magazineTitle"] = update.MagazineTitle,
            ["gameTitle"] = update.GameTitle,
            ["title"] = update.Title
        }), item.FileName);
    }

    private static void PutIfMissing(Dictionary<string, object?> metadata, string key, object? value)
    {
        if (value is null) return;
        if (metadata.TryGetValue(key, out var existing) && !IsEmptyValue(existing)) return;
        metadata[key] = value;
    }

    private static string GetSubmittedString(JsonElement payload, string name)
    {
        if (payload.ValueKind != JsonValueKind.Object) return string.Empty;
        foreach (var property in payload.EnumerateObject())
        {
            if (!property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
            return property.Value.ValueKind == JsonValueKind.String ? (property.Value.GetString() ?? string.Empty).Trim() : property.Value.ToString().Trim();
        }
        return string.Empty;
    }

    private static string ApplyNamingSchema(string schema, LibraryItem item, Dictionary<string, object?> metadata)
    {
        schema = string.IsNullOrWhiteSpace(schema) ? "{title}" : schema.Trim();
        var title = FirstValue(metadata, "strategyGuideTitle", "manualTitle", "magazineTitle", "title");
        if (string.IsNullOrWhiteSpace(title)) title = item.Title;
        var platform = FirstValue(metadata, "preferredPlatform", "category", "system");
        if (string.IsNullOrWhiteSpace(platform)) platform = item.Category;
        var publisher = FirstValue(metadata, "publisher");
        var gamePublisher = FirstValue(metadata, "gamePublisher", "publisher");
        var developer = FirstValue(metadata, "developer", "gameDeveloper");
        var isbn10 = FirstValue(metadata, "isbn10");
        var isbn13 = FirstValue(metadata, "isbn13");
        var isbn = FirstValue(metadata, "isbn", "isbn13", "isbn10");
        if (string.IsNullOrWhiteSpace(isbn)) isbn = First(isbn13, isbn10);

        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["title"] = title,
            ["mainTitle"] = title,
            ["guideTitle"] = title,
            ["strategyGuideTitle"] = FirstValue(metadata, "strategyGuideTitle", "title"),
            ["manualTitle"] = FirstValue(metadata, "manualTitle", "title"),
            ["magazineTitle"] = FirstValue(metadata, "magazineTitle", "title"),
            ["gameTitle"] = FirstValue(metadata, "gameTitle", "coverSubject", "title"),
            ["platform"] = platform,
            ["preferredPlatform"] = platform,
            ["publisher"] = publisher,
            ["year"] = FirstValue(metadata, "year"),
            ["issue"] = FirstValue(metadata, "issueNumber"),
            ["issueNumber"] = FirstValue(metadata, "issueNumber"),
            ["volume"] = FirstValue(metadata, "volume"),
            ["number"] = FirstValue(metadata, "number"),
            ["month"] = FirstValue(metadata, "month", "publicationMonth"),
            ["kind"] = FirstValue(metadata, "kind"),
            ["contentType"] = FirstValue(metadata, "kind"),
            ["region"] = FirstValue(metadata, "region"),
            ["language"] = FirstValue(metadata, "language", "languageTag"),
            ["edition"] = FirstValue(metadata, "edition", "editionType"),
            ["editionType"] = FirstValue(metadata, "editionType"),
            ["editionYear"] = FirstValue(metadata, "editionYear"),
            ["editionVolume"] = FirstValue(metadata, "editionVolume"),
            ["guideType"] = FirstValue(metadata, "guideType"),
            ["franchise"] = FirstValue(metadata, "gameFranchise", "franchise", "series"),
            ["gameFranchise"] = FirstValue(metadata, "gameFranchise", "franchise", "series"),
            ["gamePublisher"] = gamePublisher,
            ["developer"] = developer,
            ["gameDeveloper"] = developer,
            ["genre"] = FirstValue(metadata, "genre"),
            ["gameReleaseYear"] = FirstValue(metadata, "gameReleaseYear"),
            ["isbn"] = isbn,
            ["isbn10"] = isbn10,
            ["isbn13"] = isbn13,
            ["asin"] = FirstValue(metadata, "asin"),
            ["manualType"] = FirstValue(metadata, "manualType"),
            ["controlScheme"] = FirstValue(metadata, "controlScheme"),
            ["coverStory"] = FirstValue(metadata, "coverStory", "coverSubject"),
            ["barcodeUpcIssn"] = FirstValue(metadata, "barcodeUpcIssn", "barcode", "upc", "issn")
        };

        var normalizedTokens = tokens
            .GroupBy(pair => NormalizeSchemaTokenKey(pair.Key), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Value, StringComparer.OrdinalIgnoreCase);

        var expanded = Regex.Replace(schema, @"\{([^{}]+)\}", match =>
        {
            var key = match.Groups[1].Value.Trim();
            if (tokens.TryGetValue(key, out var value)) return value.Trim();
            var normalizedKey = NormalizeSchemaTokenKey(key);
            return normalizedTokens.TryGetValue(normalizedKey, out var normalizedValue) ? normalizedValue.Trim() : string.Empty;
        });
        expanded = Regex.Replace(expanded, @"\s+", " ").Trim();
        expanded = Regex.Replace(expanded, @"\s+-\s+(?=$)", " ").Trim();
        expanded = Regex.Replace(expanded, @"^\s*-\s+", string.Empty).Trim();
        expanded = Regex.Replace(expanded, @"\s+\|\s+(?=$)", " ").Trim();
        expanded = Regex.Replace(expanded, @"^\s*\|\s+", string.Empty).Trim();
        expanded = Regex.Replace(expanded, @"\s+-\s+", " - ").Trim();
        expanded = Regex.Replace(expanded, @"\s+\|\s+", " | ").Trim();
        expanded = Regex.Replace(expanded, @"^[-_.| ]+|[-_.| ]+$", string.Empty).Trim();
        return string.IsNullOrWhiteSpace(expanded) ? title : expanded;
    }

    private static string NormalizeSchemaTokenKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;
        return Regex.Replace(key.Trim().ToLowerInvariant(), @"[^a-z0-9]+", string.Empty);
    }

    private static string FirstValue(Dictionary<string, object?> metadata, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!metadata.TryGetValue(key, out var value) || IsEmptyValue(value)) continue;
            if (value is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.String) return element.GetString()?.Trim() ?? string.Empty;
                if (element.ValueKind == JsonValueKind.Array) return string.Join(", ", element.EnumerateArray().Select(v => v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString()).Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!.Trim()));
                return element.ToString().Trim();
            }
            if (value is IEnumerable<string> values) return string.Join(", ", values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => (v ?? string.Empty).Trim()));
            var converted = value?.ToString();
            return string.IsNullOrWhiteSpace(converted) ? string.Empty : converted.Trim();
        }
        return string.Empty;
    }

    public static bool IsLikelyDosShortFileName(string fileName)
    {
        var leaf = Path.GetFileName(fileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(leaf)) return false;
        var baseName = Path.GetFileNameWithoutExtension(leaf);
        var extension = Path.GetExtension(leaf).TrimStart('.');
        return Regex.IsMatch(baseName, @"^[A-Z0-9]{1,6}~[0-9A-Z]{1,2}$", RegexOptions.IgnoreCase)
            && extension.Length is >= 1 and <= 4
            && Regex.IsMatch(extension, @"^[A-Z0-9]+$", RegexOptions.IgnoreCase);
    }

    public static string SanitizeSuggestedFileName(string suggestedFileName, string fallbackFileName)
    {
        var currentExtension = Path.GetExtension(fallbackFileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(currentExtension))
            currentExtension = Path.GetExtension(suggestedFileName ?? string.Empty);

        var rawLeaf = (suggestedFileName ?? string.Empty).Trim();
        // Guidevault often runs inside a Linux container against a Windows-hosted
        // library. Path.GetInvalidFileNameChars() is platform-specific, so on Linux
        // it does not reject Windows-only characters such as ':' or '?'. Always use
        // a Windows-safe policy for source-file renames so bind-mounted libraries do
        // not end up with 8.3/short-name aliases such as SQBRU6~B.CBZ.
        rawLeaf = rawLeaf.Replace('\\', '/');
        rawLeaf = rawLeaf.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? rawLeaf;
        if (string.IsNullOrWhiteSpace(rawLeaf)) rawLeaf = Path.GetFileName(fallbackFileName ?? string.Empty);

        var baseName = Path.GetFileNameWithoutExtension(rawLeaf);
        if (string.IsNullOrWhiteSpace(baseName)) baseName = Path.GetFileNameWithoutExtension(fallbackFileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(baseName)) baseName = "Guidevault Metadata";

        baseName = ToWindowsSafeFileNameBase(baseName);
        if (string.IsNullOrWhiteSpace(baseName)) baseName = "Guidevault Metadata";

        var maxBaseLength = Math.Max(16, 180 - currentExtension.Length);
        if (baseName.Length > maxBaseLength)
            baseName = ToWindowsSafeFileNameBase(baseName[..maxBaseLength]);
        if (string.IsNullOrWhiteSpace(baseName)) baseName = "Guidevault Metadata";

        return $"{baseName}{currentExtension}";
    }

    private static string ToWindowsSafeFileNameBase(string baseName)
    {
        var value = baseName ?? string.Empty;
        value = value.Replace("\u2013", "-").Replace("\u2014", "-");
        value = Regex.Replace(value, "[\\/:*?\"<>|]+", " - ");
        value = Regex.Replace(value, "[\u0000-\u001F]+", " ");
        value = Regex.Replace(value, @"\s+", " ");
        value = Regex.Replace(value, @"\s*-\s*", " - ");
        value = Regex.Replace(value, @"(?:\s+-\s+){2,}", " - ");
        value = value.Trim().Trim('.', '-').Trim();

        // Avoid Windows reserved device names even when an extension is present.
        if (Regex.IsMatch(value, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$", RegexOptions.IgnoreCase))
            value = $"{value} File";

        return value;
    }

    private static bool IsEmptyValue(object? value)
    {
        if (value is null) return true;
        if (value is string text) return string.IsNullOrWhiteSpace(text);
        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined) return true;
            if (element.ValueKind == JsonValueKind.String) return string.IsNullOrWhiteSpace(element.GetString());
            if (element.ValueKind == JsonValueKind.Array) return !element.EnumerateArray().Any();
        }
        return false;
    }

    private static string SuggestedFileName(LibraryItem item, Dictionary<string, object?> metadata)
    {
        var title = metadata.TryGetValue("strategyGuideTitle", out var strategyGuideTitle) ? strategyGuideTitle?.ToString() : string.Empty;
        if (string.IsNullOrWhiteSpace(title) && metadata.TryGetValue("manualTitle", out var manualTitle)) title = manualTitle?.ToString();
        if (string.IsNullOrWhiteSpace(title) && metadata.TryGetValue("magazineTitle", out var magazineTitle)) title = magazineTitle?.ToString();
        if (string.IsNullOrWhiteSpace(title) && metadata.TryGetValue("gameTitle", out var gameTitle)) title = gameTitle?.ToString();
        if (string.IsNullOrWhiteSpace(title) && metadata.TryGetValue("title", out var itemTitle)) title = itemTitle?.ToString();
        if (string.IsNullOrWhiteSpace(title)) title = item.Title;
        var safeTitle = Regex.Replace(title ?? "Guidevault Metadata", "[\\/:*?\"<>|]+", " - ").Trim();
        return $"{safeTitle}{Path.GetExtension(item.FileName)}";
    }

    private static void Put(Dictionary<string, object?> metadata, string key, object? value)
    {
        if (value is null) return;
        if (value is string text && string.IsNullOrWhiteSpace(text)) return;
        if (value is IEnumerable<string> values)
        {
            var cleaned = values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (cleaned.Length == 0) return;
            metadata[key] = cleaned;
            return;
        }
        metadata[key] = value;
    }

    private static string First(string? candidate, string fallback)
        => string.IsNullOrWhiteSpace(candidate) ? (fallback ?? string.Empty) : candidate.Trim();

    private static string JoinNonEmpty(params string[] values)
        => string.Join(", ", values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).Distinct(StringComparer.OrdinalIgnoreCase));
}

public sealed record ArchiveMetadataWriteResult(bool Success, string Message, string MetadataFileName, string WrittenArchivePath, bool CreatedPackage, string OriginalArchivePath);

public sealed record ArchiveConversionResult(bool Success, string Message, string SourcePath, string SourceFileName, string SourceFormat, long SourceBytes, string OutputPath, string OutputFileName, string TargetFormat, long OutputBytes, bool CreatedPackage);

public sealed record ArchiveValidationResult(bool IsReadable, string Message, int PageCount);

public static class ArchiveReader
{
    public static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".cbz", ".cbr", ".pdf" };
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp"];
    private static readonly ConcurrentDictionary<string, string[]> EntryCache = new();
    private static readonly ConcurrentDictionary<string, Lazy<Task<(byte[] Bytes, string ContentType)?>>> CoverCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly SemaphoreSlim CoverReadGate = new(3, 3);
    private static readonly SemaphoreSlim CoverThumbnailGate = new(2, 2);
    private static string CoverCacheDirectory = Path.Combine(AppContext.BaseDirectory, "data", "cache", "covers");
    private static string CoverThumbnailCacheDirectory = Path.Combine(AppContext.BaseDirectory, "data", "cache", "cover-thumbs");
    private static readonly string[] KnownCoverCacheExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp"];
    private const string CoverCacheVersion = "cover-manual-override-v1";

    public static void ConfigureCoverCache(string cacheDirectory, string? thumbnailCacheDirectory = null)
    {
        if (!string.IsNullOrWhiteSpace(cacheDirectory)) CoverCacheDirectory = cacheDirectory;
        if (!string.IsNullOrWhiteSpace(thumbnailCacheDirectory)) CoverThumbnailCacheDirectory = thumbnailCacheDirectory;
        try { Directory.CreateDirectory(CoverCacheDirectory); } catch { }
        try { Directory.CreateDirectory(CoverThumbnailCacheDirectory); } catch { }
    }

    public static void ClearCache()
    {
        EntryCache.Clear();
        CoverCache.Clear();
    }

    public static void ClearMemoryCaches()
    {
        EntryCache.Clear();
        CoverCache.Clear();
    }

    public static string GetArchiveVersionStamp(string archivePath)
    {
        try
        {
            var info = new FileInfo(archivePath);
            if (!info.Exists) return "missing";
            return $"{info.Length:x}-{info.LastWriteTimeUtc.Ticks:x}";
        }
        catch
        {
            return "0";
        }
    }

    public static void ClearCacheForPath(string archivePath)
    {
        ClearCoverCacheForPath(archivePath);
        ClearEntryCacheForPath(archivePath);
    }

    private static string ArchivePathCachePrefix(string archivePath)
    {
        try { return Path.GetFullPath(archivePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToLowerInvariant(); }
        catch { return (archivePath ?? string.Empty).Trim().TrimEnd('/', '\\').ToLowerInvariant(); }
    }

    private static string ArchiveVersionedCacheKey(string archivePath)
        => $"{ArchivePathCachePrefix(archivePath)}|{GetArchiveVersionStamp(archivePath)}";

    private static void ClearEntryCacheForPath(string archivePath)
    {
        var prefix = ArchivePathCachePrefix(archivePath);
        foreach (var key in EntryCache.Keys)
        {
            if (key.StartsWith(prefix + "|", StringComparison.OrdinalIgnoreCase) || string.Equals(key, archivePath, StringComparison.OrdinalIgnoreCase))
                EntryCache.TryRemove(key, out _);
        }
    }

    public static object GetDiagnostics()
    {
        var diskFiles = 0;
        long diskBytes = 0;
        try
        {
            if (Directory.Exists(CoverCacheDirectory))
            {
                foreach (var file in Directory.EnumerateFiles(CoverCacheDirectory))
                {
                    try
                    {
                        var info = new FileInfo(file);
                        diskFiles++;
                        diskBytes += info.Length;
                    }
                    catch { }
                }
            }
        }
        catch { }

        var thumbFiles = 0;
        long thumbBytes = 0;
        try
        {
            if (Directory.Exists(CoverThumbnailCacheDirectory))
            {
                foreach (var file in Directory.EnumerateFiles(CoverThumbnailCacheDirectory, "*.jpg"))
                {
                    try
                    {
                        var info = new FileInfo(file);
                        thumbFiles++;
                        thumbBytes += info.Length;
                    }
                    catch { }
                }
            }
        }
        catch { }

        return new
        {
            imageEntryCacheCount = EntryCache.Count,
            inFlightCoverReads = CoverCache.Count,
            diskCoverCachePath = CoverCacheDirectory,
            diskCoverCacheFiles = diskFiles,
            diskCoverCacheBytes = diskBytes,
            diskCoverThumbnailCachePath = CoverThumbnailCacheDirectory,
            diskCoverThumbnailCacheFiles = thumbFiles,
            diskCoverThumbnailCacheBytes = thumbBytes
        };
    }

    public static void ClearCoverCacheForPath(string archivePath)
    {
        try
        {
            ClearEntryCacheForPath(archivePath);
            var key = CoverCacheKey(archivePath);
            CoverCache.TryRemove(key, out _);
            if (Directory.Exists(CoverCacheDirectory))
            {
                foreach (var file in Directory.EnumerateFiles(CoverCacheDirectory, key + ".*"))
                {
                    try { File.Delete(file); } catch { }
                }
            }
            if (Directory.Exists(CoverThumbnailCacheDirectory))
            {
                foreach (var file in Directory.EnumerateFiles(CoverThumbnailCacheDirectory, key + "_w*.jpg"))
                {
                    try { File.Delete(file); } catch { }
                }
            }
        }
        catch
        {
            // Cache cleanup is best-effort only.
        }
    }

    public static async Task<ArchiveValidationResult> ValidateArchiveAsync(string archivePath)
    {
        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
            return new ArchiveValidationResult(false, "Missing source file.", 0);

        try
        {
            var info = new FileInfo(archivePath);
            if (info.Length <= 0) return new ArchiveValidationResult(false, "Source file is empty.", 0);
        }
        catch
        {
            return new ArchiveValidationResult(false, "Unable to read source file metadata.", 0);
        }

        var extension = Path.GetExtension(archivePath);
        if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            return new ArchiveValidationResult(true, "PDF source exists.", 0);

        if (!SupportedExtensions.Contains(extension))
            return new ArchiveValidationResult(false, $"Unsupported archive format: {extension}", 0);

        string[] entries;
        try
        {
            entries = GetImageEntries(archivePath);
        }
        catch (Exception ex)
        {
            ClearCoverCacheForPath(archivePath);
            return new ArchiveValidationResult(false, $"Unable to read archive entries: {ex.Message}", 0);
        }

        if (entries.Length == 0)
        {
            ClearCoverCacheForPath(archivePath);
            return new ArchiveValidationResult(false, "No image pages found in archive.", 0);
        }

        var attempts = Math.Min(entries.Length, 8);
        for (var i = 0; i < attempts; i++)
        {
            try
            {
                var image = await GetImagePageAsync(archivePath, i);
                if (image is not null && LooksLikeImageBytes(image.Value.Bytes, image.Value.ContentType))
                    return new ArchiveValidationResult(true, $"Validated {entries.Length} image page(s).", entries.Length);
            }
            catch
            {
                // Try another leading image before marking the archive unreadable.
            }
        }

        ClearCoverCacheForPath(archivePath);
        return new ArchiveValidationResult(false, "Archive contains image entries, but none of the leading pages could be read.", entries.Length);
    }

    private static readonly string[] GuidevaultMetadataEntryNames =
    [
        "guidevault-guide-metadata.json",
        "guidevault-manual-metadata.json",
        "guidevault-magazine-metadata.json"
    ];

    public static string GetGuidevaultMetadataEntryName(string kind)
    {
        if (kind.Equals("Manual", StringComparison.OrdinalIgnoreCase)) return "guidevault-manual-metadata.json";
        if (kind.Equals("Magazine", StringComparison.OrdinalIgnoreCase)) return "guidevault-magazine-metadata.json";
        return "guidevault-guide-metadata.json";
    }

    public static async Task<ItemMetadataUpdate?> GetGuidevaultMetadataAsync(string archivePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath)) return null;
            if (Path.GetExtension(archivePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase)) return null;

            var json = await ReadFirstTextEntryAsync(archivePath, GuidevaultMetadataEntryNames);
            if (string.IsNullOrWhiteSpace(json)) return null;

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var metadataElement = root;
            int? itemPageCount = null;

            if (TryGetJsonProperty(root, "item", out var itemElement))
            {
                if (TryGetJsonProperty(itemElement, "pageCount", out var pageCountElement))
                    itemPageCount = ReadInt(pageCountElement);
                if (TryGetJsonProperty(itemElement, "metadata", out var nestedMetadata))
                    metadataElement = nestedMetadata;
            }
            else if (TryGetJsonProperty(root, "metadata", out var directMetadata))
            {
                metadataElement = directMetadata;
            }

            var update = ItemMetadataJsonReader.Read(metadataElement);
            var metadataPageCount = itemPageCount ?? update.PageCount;
            return update with
            {
                PageCount = metadataPageCount,
                MetadataSource = string.IsNullOrWhiteSpace(update.MetadataSource) ? "Guidevault JSON" : update.MetadataSource
            };
        }
        catch
        {
            return null;
        }
    }

    public static async Task<ArchiveMetadataWriteResult> WriteGuidevaultMetadataAsync(string archivePath, string kind, string json)
    {
        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
            return new ArchiveMetadataWriteResult(false, "Source file was not found.", string.Empty, string.Empty, false, archivePath);

        var metadataFileName = GetGuidevaultMetadataEntryName(kind);
        var ext = Path.GetExtension(archivePath);
        var targetPath = ext.Equals(".cbr", StringComparison.OrdinalIgnoreCase)
            ? GuidevaultPackagePath(archivePath, ".cbz")
            : ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
                ? GuidevaultPackagePath(archivePath, ".zip")
                : archivePath;
        var targetDirectory = Path.GetDirectoryName(targetPath) ?? Directory.GetCurrentDirectory();

        if (!TryVerifyWritableDirectory(targetDirectory, out var writeError))
            return new ArchiveMetadataWriteResult(false, BuildReadOnlyMetadataExportMessage(targetDirectory, writeError), metadataFileName, string.Empty, false, archivePath);

        if (ext.Equals(".cbz", StringComparison.OrdinalIgnoreCase) && IsFileReadOnly(archivePath))
            return new ArchiveMetadataWriteResult(false, $"Unable to write Guidevault metadata: the source CBZ is marked read-only. Clear the read-only file attribute or mount the library read/write, then try export again. Path: {archivePath}", metadataFileName, string.Empty, false, archivePath);

        try
        {
            if (ext.Equals(".cbz", StringComparison.OrdinalIgnoreCase))
            {
                await RewriteZipWithMetadataAsync(archivePath, archivePath, metadataFileName, json);
                ClearCacheForWrittenArchive(archivePath);
                return new ArchiveMetadataWriteResult(true, $"Wrote {metadataFileName} into the CBZ archive.", metadataFileName, archivePath, false, archivePath);
            }

            if (ext.Equals(".cbr", StringComparison.OrdinalIgnoreCase))
            {
                var packagePath = GuidevaultPackagePath(archivePath, ".cbz");
                await CreateCbzFromReadableArchiveAsync(archivePath, packagePath, metadataFileName, json);
                ClearCacheForWrittenArchive(packagePath);
                return new ArchiveMetadataWriteResult(true, $"Created {Path.GetFileName(packagePath)} and wrote {metadataFileName} inside it. CBR/RAR archives are read-only in Guidevault, so the metadata package is a CBZ copy.", metadataFileName, packagePath, true, archivePath);
            }

            if (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                var packagePath = GuidevaultPackagePath(archivePath, ".zip");
                await CreatePdfMetadataZipAsync(archivePath, packagePath, metadataFileName, json);
                return new ArchiveMetadataWriteResult(true, $"Created {Path.GetFileName(packagePath)} with the PDF and {metadataFileName} inside it.", metadataFileName, packagePath, true, archivePath);
            }

            return new ArchiveMetadataWriteResult(false, $"Unsupported source format: {ext}", metadataFileName, string.Empty, false, archivePath);
        }
        catch (Exception ex) when (IsReadOnlyOrPermissionException(ex))
        {
            return new ArchiveMetadataWriteResult(false, BuildReadOnlyMetadataExportMessage(targetDirectory, ex.Message), metadataFileName, string.Empty, false, archivePath);
        }
        catch (Exception ex)
        {
            return new ArchiveMetadataWriteResult(false, $"Unable to write Guidevault metadata: {ex.Message}", metadataFileName, string.Empty, false, archivePath);
        }
    }


    public static async Task<ArchiveConversionResult> ConvertArchiveAsync(string archivePath, string kind, string guidevaultJson, string targetFormat)
    {
        var sourceFileName = string.IsNullOrWhiteSpace(archivePath) ? string.Empty : Path.GetFileName(archivePath);
        var sourceFormat = FormatLabelFromPath(archivePath);
        long sourceBytes = 0;
        try { if (File.Exists(archivePath)) sourceBytes = new FileInfo(archivePath).Length; } catch { }

        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
            return new ArchiveConversionResult(false, "Source file was not found.", archivePath, sourceFileName, sourceFormat, sourceBytes, string.Empty, string.Empty, targetFormat.ToUpperInvariant(), 0, false);

        var requested = (targetFormat ?? string.Empty).Trim().ToLowerInvariant();
        if (requested is not ("cbz" or "pdf"))
            return new ArchiveConversionResult(false, $"Unsupported conversion target: {targetFormat}.", archivePath, sourceFileName, sourceFormat, sourceBytes, string.Empty, string.Empty, requested.ToUpperInvariant(), 0, false);

        var directory = Path.GetDirectoryName(archivePath) ?? Directory.GetCurrentDirectory();
        if (!TryVerifyWritableDirectory(directory, out var writeError))
            return new ArchiveConversionResult(false, BuildReadOnlyMetadataExportMessage(directory, writeError), archivePath, sourceFileName, sourceFormat, sourceBytes, string.Empty, string.Empty, requested.ToUpperInvariant(), 0, false);

        try
        {
            var metadataFileName = GetGuidevaultMetadataEntryName(kind);
            var ext = Path.GetExtension(archivePath).ToLowerInvariant();
            if (requested == "cbz")
            {
                var destination = UniqueSiblingPath(archivePath, ext == ".cbz" ? ".converted.cbz" : ".cbz");

                if (ext == ".pdf")
                {
                    var pageCount = await CreateCbzFromPdfAsync(archivePath, destination, metadataFileName, guidevaultJson);
                    ClearCacheForWrittenArchive(destination);
                    var pdfCbzOutputBytes = new FileInfo(destination).Length;
                    return new ArchiveConversionResult(true, $"Created {Path.GetFileName(destination)} as a CBZ copy with {pageCount} rasterized page image(s). The original PDF was not deleted.", archivePath, sourceFileName, sourceFormat, sourceBytes, destination, Path.GetFileName(destination), "CBZ", pdfCbzOutputBytes, true);
                }

                if (ext == ".cbz") await RepackCbzAsync(archivePath, destination, metadataFileName, guidevaultJson);
                else await CreateCbzFromReadableArchiveAsync(archivePath, destination, metadataFileName, guidevaultJson);
                ClearCacheForWrittenArchive(destination);
                var cbzOutputBytes = new FileInfo(destination).Length;
                return new ArchiveConversionResult(true, $"Created {Path.GetFileName(destination)} as a CBZ copy. The original file was not deleted.", archivePath, sourceFileName, sourceFormat, sourceBytes, destination, Path.GetFileName(destination), "CBZ", cbzOutputBytes, true);
            }

            if (requested == "pdf")
            {
                if (ext == ".pdf")
                    return new ArchiveConversionResult(false, "This item is already a PDF. Choose CBZ if you want a CBZ image copy.", archivePath, sourceFileName, sourceFormat, sourceBytes, string.Empty, string.Empty, "PDF", 0, false);

                var imageEntries = GetImageEntries(archivePath);
                if (imageEntries.Length == 0)
                    return new ArchiveConversionResult(false, "No image pages were found to write into a PDF.", archivePath, sourceFileName, sourceFormat, sourceBytes, string.Empty, string.Empty, "PDF", 0, false);

                var destination = UniqueSiblingPath(archivePath, ".pdf");
                await CreatePdfFromImageArchiveAsync(archivePath, destination, imageEntries);
                var pdfOutputBytes = new FileInfo(destination).Length;
                return new ArchiveConversionResult(true, $"Created {Path.GetFileName(destination)} as a PDF copy. The original file was not deleted.", archivePath, sourceFileName, sourceFormat, sourceBytes, destination, Path.GetFileName(destination), "PDF", pdfOutputBytes, true);
            }

            return new ArchiveConversionResult(false, $"Unsupported conversion target: {targetFormat}.", archivePath, sourceFileName, sourceFormat, sourceBytes, string.Empty, string.Empty, requested.ToUpperInvariant(), 0, false);
        }
        catch (Exception ex) when (IsReadOnlyOrPermissionException(ex))
        {
            return new ArchiveConversionResult(false, BuildReadOnlyMetadataExportMessage(directory, ex.Message), archivePath, sourceFileName, sourceFormat, sourceBytes, string.Empty, string.Empty, requested.ToUpperInvariant(), 0, false);
        }
        catch (Exception ex)
        {
            return new ArchiveConversionResult(false, $"Conversion failed: {ex.Message}", archivePath, sourceFileName, sourceFormat, sourceBytes, string.Empty, string.Empty, requested.ToUpperInvariant(), 0, false);
        }
    }

    private static async Task<int> CreateCbzFromPdfAsync(string sourcePath, string destinationPath, string metadataFileName, string guidevaultJson)
    {
        var directory = Path.GetDirectoryName(destinationPath) ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(directory);
        var tempArchivePath = Path.Combine(directory, $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");
        var tempPagesPath = Path.Combine(directory, $".{Path.GetFileNameWithoutExtension(destinationPath)}.{Guid.NewGuid():N}.pages");

        try
        {
            Directory.CreateDirectory(tempPagesPath);
            var rasterize = await RasterizePdfToImageFilesAsync(sourcePath, tempPagesPath);
            if (!rasterize.Success || rasterize.ImagePaths.Count == 0)
                throw new InvalidOperationException(rasterize.Message);

            using (var output = ZipFile.Open(tempArchivePath, ZipArchiveMode.Create))
            {
                var pageNumber = 1;
                foreach (var imagePath in rasterize.ImagePaths.OrderBy(path => NaturalSortKey(Path.GetFileName(path))))
                {
                    var extension = Path.GetExtension(imagePath).ToLowerInvariant();
                    if (string.IsNullOrWhiteSpace(extension) || !ImageExtensions.Contains(extension)) extension = ".jpg";
                    var entryName = $"{pageNumber:0000}{extension}";
                    var entry = output.CreateEntry(entryName, CompressionLevel.NoCompression);
                    await using var input = File.OpenRead(imagePath);
                    await using var dest = entry.Open();
                    await input.CopyToAsync(dest);
                    pageNumber++;
                }

                if (!string.IsNullOrWhiteSpace(guidevaultJson))
                    await WriteZipTextEntryAsync(output, metadataFileName, guidevaultJson);
            }

            if (File.Exists(destinationPath)) File.Delete(destinationPath);
            File.Move(tempArchivePath, destinationPath);
            return rasterize.ImagePaths.Count;
        }
        finally
        {
            TryDeleteFile(tempArchivePath);
            TryDeleteDirectory(tempPagesPath);
        }
    }

    private sealed record PdfRasterizeResult(bool Success, string Message, IReadOnlyList<string> ImagePaths);

    private static async Task<PdfRasterizeResult> RasterizePdfToImageFilesAsync(string sourcePath, string outputDirectory)
    {
        var attempts = new List<string>();

        async Task<PdfRasterizeResult?> TryToolAsync(string displayName, string executable, IReadOnlyList<string> args)
        {
            if (!CommandExists(executable)) return null;

            var run = await RunProcessAsync(executable, args, TimeSpan.FromMinutes(30));
            var images = Directory.EnumerateFiles(outputDirectory)
                .Where(path => IsImageEntryName(path))
                .OrderBy(path => NaturalSortKey(Path.GetFileName(path)))
                .ToArray();

            if (run.ExitCode == 0 && images.Length > 0)
                return new PdfRasterizeResult(true, $"Rasterized PDF with {displayName}.", images);

            attempts.Add($"{displayName}: exit {run.ExitCode}. {run.Output}".Trim());
            foreach (var file in Directory.EnumerateFiles(outputDirectory)) TryDeleteFile(file);
            return null;
        }

        var prefix = Path.Combine(outputDirectory, "page");
        var pdftoppmWithQuality = await TryToolAsync("Poppler pdftoppm", "pdftoppm", ["-jpeg", "-r", "180", "-jpegopt", "quality=92", sourcePath, prefix]);
        if (pdftoppmWithQuality is not null) return pdftoppmWithQuality;

        var pdftoppm = await TryToolAsync("Poppler pdftoppm", "pdftoppm", ["-jpeg", "-r", "180", sourcePath, prefix]);
        if (pdftoppm is not null) return pdftoppm;

        var pdftocairo = await TryToolAsync("Poppler pdftocairo", "pdftocairo", ["-jpeg", "-r", "180", sourcePath, prefix]);
        if (pdftocairo is not null) return pdftocairo;

        var mutool = await TryToolAsync("MuPDF mutool", "mutool", ["draw", "-r", "180", "-o", Path.Combine(outputDirectory, "page-%04d.jpg"), sourcePath]);
        if (mutool is not null) return mutool;

        var ghostscript = await TryToolAsync("Ghostscript", "gs", ["-dNOPAUSE", "-dBATCH", "-sDEVICE=jpeg", "-r180", "-dJPEGQ=92", $"-sOutputFile={Path.Combine(outputDirectory, "page-%04d.jpg")}", sourcePath]);
        if (ghostscript is not null) return ghostscript;

        var magick = await TryToolAsync("ImageMagick", "magick", ["-density", "180", sourcePath, "-quality", "92", Path.Combine(outputDirectory, "page-%04d.jpg")]);
        if (magick is not null) return magick;

        var details = attempts.Count > 0 ? $" Tried: {string.Join(" | ", attempts)}" : string.Empty;
        return new PdfRasterizeResult(false, "PDF to CBZ needs a PDF rasterizer inside the GuideVault runtime. Install Poppler/pdftoppm in the container image, or make pdftoppm, pdftocairo, mutool, gs, or magick available on PATH." + details, []);
    }

    private sealed record ProcessRunResult(int ExitCode, string Output);

    private static async Task<ProcessRunResult> RunProcessAsync(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout)
    {
        using var process = new Process();
        process.StartInfo.FileName = fileName;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);

        try
        {
            if (!process.Start()) return new ProcessRunResult(-1, $"Unable to start {fileName}.");
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            var waitTask = process.WaitForExitAsync();
            var completed = await Task.WhenAny(waitTask, Task.Delay(timeout));
            if (completed != waitTask)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return new ProcessRunResult(-1, $"{fileName} timed out after {timeout.TotalMinutes:0} minutes.");
            }

            var output = string.Join(" ", new[] { await outputTask, await errorTask }.Where(value => !string.IsNullOrWhiteSpace(value))).Trim();
            if (output.Length > 800) output = output[..800] + "...";
            return new ProcessRunResult(process.ExitCode, output);
        }
        catch (Exception ex)
        {
            return new ProcessRunResult(-1, ex.Message);
        }
    }

    private static bool CommandExists(string executable)
    {
        if (string.IsNullOrWhiteSpace(executable)) return false;
        if (Path.IsPathFullyQualified(executable)) return File.Exists(executable);

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var candidates = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[] { executable, executable + ".exe", executable + ".cmd", executable + ".bat" }
            : new[] { executable };

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var candidate in candidates)
            {
                try
                {
                    var fullPath = Path.Combine(directory, candidate);
                    if (File.Exists(fullPath)) return true;
                }
                catch
                {
                    // Ignore malformed PATH entries.
                }
            }
        }

        return false;
    }

    private static async Task RepackCbzAsync(string sourcePath, string destinationPath, string metadataFileName, string guidevaultJson)
    {
        var directory = Path.GetDirectoryName(destinationPath) ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var output = ZipFile.Open(tempPath, ZipArchiveMode.Create))
            using (var source = ZipFile.OpenRead(sourcePath))
            {
                var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in source.Entries.OrderBy(entry => NaturalSortKey(Normalize(entry.FullName))))
                {
                    if (string.IsNullOrWhiteSpace(entry.FullName)) continue;
                    var entryName = Normalize(entry.FullName).TrimStart('/');
                    if (string.IsNullOrWhiteSpace(entryName) || !usedNames.Add(entryName)) continue;
                    var rootName = Path.GetFileName(entryName);
                    if (GuidevaultMetadataEntryNames.Any(name => string.Equals(rootName, name, StringComparison.OrdinalIgnoreCase))) continue;
                    if (string.IsNullOrEmpty(entry.Name) && entry.FullName.EndsWith("/", StringComparison.Ordinal)) continue;

                    var copied = output.CreateEntry(entryName, CompressionLevel.Optimal);
                    copied.LastWriteTime = entry.LastWriteTime;
                    await using var input = entry.Open();
                    await using var dest = copied.Open();
                    await input.CopyToAsync(dest);
                }

                if (!string.IsNullOrWhiteSpace(guidevaultJson))
                    await WriteZipTextEntryAsync(output, metadataFileName, guidevaultJson);
            }

            if (File.Exists(destinationPath)) File.Delete(destinationPath);
            File.Move(tempPath, destinationPath);
        }
        finally
        {
            TryDeleteFile(tempPath);
        }
    }

    private static async Task CreatePdfFromImageArchiveAsync(string sourcePath, string destinationPath, IReadOnlyList<string> imageEntries)
    {
        var directory = Path.GetDirectoryName(destinationPath) ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            var pages = new List<PdfImagePage>();
            for (var i = 0; i < imageEntries.Count; i++)
            {
                var page = await GetImagePageAsync(sourcePath, i);
                if (page is null) continue;
                var jpeg = await NormalizeImageForPdfAsync(page.Value.Bytes, page.Value.ContentType);
                if (jpeg is not null) pages.Add(jpeg);
            }

            if (pages.Count == 0) throw new InvalidDataException("No readable image pages could be converted to PDF.");
            await WriteSimpleImagePdfAsync(tempPath, pages);
            if (File.Exists(destinationPath)) File.Delete(destinationPath);
            File.Move(tempPath, destinationPath);
        }
        finally
        {
            TryDeleteFile(tempPath);
        }
    }

    private sealed record PdfImagePage(byte[] JpegBytes, int Width, int Height);

    private static async Task<PdfImagePage?> NormalizeImageForPdfAsync(byte[] bytes, string contentType)
    {
        try
        {
            await using var input = new MemoryStream(bytes, writable: false);
            using var image = await Image.LoadAsync<SixLabors.ImageSharp.PixelFormats.Rgba32>(input);
            using var output = new MemoryStream();
            await image.SaveAsJpegAsync(output, new JpegEncoder { Quality = 92 });
            return new PdfImagePage(output.ToArray(), image.Width, image.Height);
        }
        catch
        {
            return null;
        }
    }

    private static async Task WriteSimpleImagePdfAsync(string path, IReadOnlyList<PdfImagePage> pages)
    {
        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        var offsets = new List<long> { 0 };
        async Task WriteAsciiAsync(string value)
        {
            var data = Encoding.ASCII.GetBytes(value);
            await stream.WriteAsync(data);
        }
        async Task WriteObjectAsync(int number, string body)
        {
            offsets.Add(stream.Position);
            await WriteAsciiAsync($"{number} 0 obj\n{body}\nendobj\n");
        }

        await WriteAsciiAsync("%PDF-1.4\n%\u00E2\u00E3\u00CF\u00D3\n");
        var pageCount = pages.Count;
        var catalogObj = 1;
        var pagesObj = 2;
        var firstPageObj = 3;
        var kids = string.Join(' ', Enumerable.Range(0, pageCount).Select(i => $"{firstPageObj + (i * 3)} 0 R"));
        await WriteObjectAsync(catalogObj, $"<< /Type /Catalog /Pages {pagesObj} 0 R >>");
        await WriteObjectAsync(pagesObj, $"<< /Type /Pages /Count {pageCount} /Kids [ {kids} ] >>");

        for (var i = 0; i < pageCount; i++)
        {
            var page = pages[i];
            var pageObj = firstPageObj + (i * 3);
            var imageObj = pageObj + 1;
            var contentObj = pageObj + 2;
            var width = Math.Max(1, page.Width);
            var height = Math.Max(1, page.Height);
            var content = $"q\n{width} 0 0 {height} 0 0 cm\n/Im0 Do\nQ\n";
            var contentBytes = Encoding.ASCII.GetBytes(content);

            await WriteObjectAsync(pageObj, $"<< /Type /Page /Parent {pagesObj} 0 R /MediaBox [0 0 {width} {height}] /Resources << /XObject << /Im0 {imageObj} 0 R >> >> /Contents {contentObj} 0 R >>");

            offsets.Add(stream.Position);
            await WriteAsciiAsync($"{imageObj} 0 obj\n<< /Type /XObject /Subtype /Image /Width {width} /Height {height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {page.JpegBytes.Length} >>\nstream\n");
            await stream.WriteAsync(page.JpegBytes);
            await WriteAsciiAsync("\nendstream\nendobj\n");

            offsets.Add(stream.Position);
            await WriteAsciiAsync($"{contentObj} 0 obj\n<< /Length {contentBytes.Length} >>\nstream\n");
            await stream.WriteAsync(contentBytes);
            await WriteAsciiAsync("endstream\nendobj\n");
        }

        var xref = stream.Position;
        var objectCount = 2 + (pageCount * 3);
        await WriteAsciiAsync($"xref\n0 {objectCount + 1}\n0000000000 65535 f \n");
        for (var i = 1; i <= objectCount; i++)
            await WriteAsciiAsync($"{offsets[i]:D10} 00000 n \n");
        await WriteAsciiAsync($"trailer\n<< /Size {objectCount + 1} /Root {catalogObj} 0 R >>\nstartxref\n{xref}\n%%EOF\n");
    }

    private static string UniqueSiblingPath(string sourcePath, string requestedExtension)
    {
        var directory = Path.GetDirectoryName(sourcePath) ?? Directory.GetCurrentDirectory();
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        var extension = requestedExtension.StartsWith('.') ? requestedExtension : "." + requestedExtension;
        var candidate = Path.Combine(directory, baseName + extension);
        if (!File.Exists(candidate) && !candidate.Equals(sourcePath, StringComparison.OrdinalIgnoreCase)) return candidate;

        var suffix = extension.StartsWith(".converted", StringComparison.OrdinalIgnoreCase) || extension.StartsWith(".optimized", StringComparison.OrdinalIgnoreCase)
            ? extension
            : ".converted" + extension;
        candidate = Path.Combine(directory, baseName + suffix);
        if (!File.Exists(candidate) && !candidate.Equals(sourcePath, StringComparison.OrdinalIgnoreCase)) return candidate;

        for (var i = 2; i < 1000; i++)
        {
            candidate = Path.Combine(directory, $"{baseName}.converted-{i}{extension}");
            if (!File.Exists(candidate) && !candidate.Equals(sourcePath, StringComparison.OrdinalIgnoreCase)) return candidate;
        }

        return Path.Combine(directory, $"{baseName}.converted-{Guid.NewGuid():N}{extension}");
    }

    private static string FormatLabelFromPath(string path)
    {
        var ext = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
        return string.IsNullOrWhiteSpace(ext) ? "Unknown" : ext;
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = Math.Max(0, bytes);
        var unit = 0;
        double display = value;
        while (display >= 1024 && unit < units.Length - 1)
        {
            display /= 1024;
            unit++;
        }
        return $"{display:0.#} {units[unit]}";
    }

    private static async Task<string?> ReadFirstTextEntryAsync(string archivePath, string[] entryNames)
    {
        if (Path.GetExtension(archivePath).Equals(".cbz", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var zip = ZipFile.OpenRead(archivePath);
                foreach (var entryName in entryNames)
                {
                    var entry = zip.Entries.FirstOrDefault(e => string.Equals(Path.GetFileName(e.FullName), entryName, StringComparison.OrdinalIgnoreCase));
                    if (entry is null) continue;
                    await using var stream = entry.Open();
                    using var reader = new StreamReader(stream);
                    return await reader.ReadToEndAsync();
                }

                // Valid ZIP/CBZ with no matching metadata entry. Do not fall through
                // to SharpCompress because that re-walks the same archive and makes
                // metadata enrichment crawl on large libraries.
                return null;
            }
            catch
            {
                // Fall through to SharpCompress only for mislabeled/corrupt archives.
            }
        }

        using var archiveReader = ReaderFactory.OpenReader(archivePath);
        while (archiveReader.MoveToNextEntry())
        {
            var current = archiveReader.Entry;
            if (current.IsDirectory) continue;
            var currentName = Path.GetFileName(current.Key ?? string.Empty);
            if (!entryNames.Any(name => string.Equals(currentName, name, StringComparison.OrdinalIgnoreCase))) continue;
            await using var input = archiveReader.OpenEntryStream();
            using var textReader = new StreamReader(input);
            return await textReader.ReadToEndAsync();
        }

        return null;
    }

    private static bool TryVerifyWritableDirectory(string directory, out string error)
    {
        error = string.Empty;
        try
        {
            Directory.CreateDirectory(directory);
            var probePath = Path.Combine(directory, $".guidevault-write-test-{Guid.NewGuid():N}.tmp");
            using (new FileStream(probePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose))
            {
            }
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool IsFileReadOnly(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReadOnly) != 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsReadOnlyOrPermissionException(Exception ex)
    {
        if (ex is UnauthorizedAccessException) return true;
        if (ex is IOException && ContainsPermissionText(ex.Message)) return true;
        return ex.InnerException is not null && IsReadOnlyOrPermissionException(ex.InnerException);
    }

    private static bool ContainsPermissionText(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        return message.Contains("Read-only file system", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Permission denied", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Access to the path", StringComparison.OrdinalIgnoreCase)
            || message.Contains("UnauthorizedAccess", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildReadOnlyMetadataExportMessage(string directory, string? detail)
    {
        var suffix = string.IsNullOrWhiteSpace(detail) ? string.Empty : $" Details: {detail}";
        return $"Unable to export Guidevault metadata because the library location is read-only: {directory}. Guidevault can scan read-only libraries, but metadata export and file rename need write access to the source folder. If this is Docker, remove ':ro' from the library volume mount, recreate the container, and try again.{suffix}";
    }

    private static async Task RewriteZipWithMetadataAsync(string sourcePath, string destinationPath, string metadataFileName, string json)
    {
        var directory = Path.GetDirectoryName(sourcePath) ?? Directory.GetCurrentDirectory();
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(sourcePath)}.{Guid.NewGuid():N}.tmp");
        var backupPath = Path.Combine(directory, $".{Path.GetFileName(sourcePath)}.{Guid.NewGuid():N}.bak");

        try
        {
            using (var source = ZipFile.OpenRead(sourcePath))
            using (var output = ZipFile.Open(tempPath, ZipArchiveMode.Create))
            {
                foreach (var entry in source.Entries)
                {
                    var rootName = Path.GetFileName(entry.FullName);
                    if (GuidevaultMetadataEntryNames.Any(name => string.Equals(rootName, name, StringComparison.OrdinalIgnoreCase))) continue;

                    var copied = output.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                    copied.LastWriteTime = entry.LastWriteTime;
                    if (string.IsNullOrEmpty(entry.Name) && entry.FullName.EndsWith("/", StringComparison.Ordinal)) continue;

                    await using var input = entry.Open();
                    await using var dest = copied.Open();
                    await input.CopyToAsync(dest);
                }

                await WriteZipTextEntryAsync(output, metadataFileName, json);
            }

            if (Path.GetFullPath(sourcePath).Equals(Path.GetFullPath(destinationPath), StringComparison.OrdinalIgnoreCase))
            {
                File.Move(sourcePath, backupPath);
                try
                {
                    File.Move(tempPath, destinationPath);
                    TryDeleteFile(backupPath);
                }
                catch
                {
                    if (File.Exists(backupPath) && !File.Exists(sourcePath)) File.Move(backupPath, sourcePath);
                    throw;
                }
            }
            else
            {
                if (File.Exists(destinationPath)) File.Delete(destinationPath);
                File.Move(tempPath, destinationPath);
            }
        }
        finally
        {
            TryDeleteFile(tempPath);
        }
    }

    private static async Task CreateCbzFromReadableArchiveAsync(string sourcePath, string destinationPath, string metadataFileName, string json)
    {
        var directory = Path.GetDirectoryName(destinationPath) ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var output = ZipFile.Open(tempPath, ZipArchiveMode.Create))
            using (var reader = ReaderFactory.OpenReader(sourcePath))
            {
                var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                while (reader.MoveToNextEntry())
                {
                    var entry = reader.Entry;
                    if (entry.IsDirectory) continue;
                    var entryName = Normalize(entry.Key ?? string.Empty).TrimStart('/');
                    if (string.IsNullOrWhiteSpace(entryName)) continue;
                    var rootName = Path.GetFileName(entryName);
                    if (GuidevaultMetadataEntryNames.Any(name => string.Equals(rootName, name, StringComparison.OrdinalIgnoreCase))) continue;
                    if (!usedNames.Add(entryName)) continue;

                    var copied = output.CreateEntry(entryName, CompressionLevel.Optimal);
                    await using var input = reader.OpenEntryStream();
                    await using var dest = copied.Open();
                    await input.CopyToAsync(dest);
                }

                await WriteZipTextEntryAsync(output, metadataFileName, json);
            }

            if (File.Exists(destinationPath)) File.Delete(destinationPath);
            File.Move(tempPath, destinationPath);
        }
        finally
        {
            TryDeleteFile(tempPath);
        }
    }

    private static async Task CreatePdfMetadataZipAsync(string sourcePath, string destinationPath, string metadataFileName, string json)
    {
        var directory = Path.GetDirectoryName(destinationPath) ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var output = ZipFile.Open(tempPath, ZipArchiveMode.Create))
            {
                var pdfEntry = output.CreateEntry(Path.GetFileName(sourcePath), CompressionLevel.Optimal);
                await using (var input = File.OpenRead(sourcePath))
                await using (var dest = pdfEntry.Open())
                {
                    await input.CopyToAsync(dest);
                }

                await WriteZipTextEntryAsync(output, metadataFileName, json);
            }

            if (File.Exists(destinationPath)) File.Delete(destinationPath);
            File.Move(tempPath, destinationPath);
        }
        finally
        {
            TryDeleteFile(tempPath);
        }
    }

    private static async Task WriteZipTextEntryAsync(ZipArchive archive, string entryName, string text)
    {
        var metadataEntry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var output = metadataEntry.Open();
        await using var writer = new StreamWriter(output, new UTF8Encoding(false));
        await writer.WriteAsync(text);
    }

    private static string GuidevaultPackagePath(string sourcePath, string extension)
    {
        var directory = Path.GetDirectoryName(sourcePath) ?? Directory.GetCurrentDirectory();
        var name = Path.GetFileNameWithoutExtension(sourcePath);
        return Path.Combine(directory, $"{name}.guidevault-metadata{extension}");
    }

    private static bool TryGetJsonProperty(JsonElement json, string name, out JsonElement value)
    {
        if (json.ValueKind == JsonValueKind.Object)
        {
            if (json.TryGetProperty(name, out value)) return true;
            foreach (var property in json.EnumerateObject())
            {
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static int? ReadInt(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number)) return Math.Max(0, number);
        if (element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out var parsed)) return Math.Max(0, parsed);
        return null;
    }

    private static void ClearCacheForWrittenArchive(string archivePath)
    {
        ClearCacheForPath(archivePath);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Best effort cleanup only.
        }
    }

    public static async Task<ComicInfoMetadata?> GetComicInfoAsync(string archivePath)
    {
        try
        {
            var xml = await ReadTextEntryAsync(archivePath, "comicinfo.xml");
            if (string.IsNullOrWhiteSpace(xml)) return null;
            var doc = XDocument.Parse(xml);
            var root = doc.Root;
            if (root is null) return null;
            string Value(string name) => root.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))?.Value.Trim() ?? string.Empty;
            var information = FirstFilled(Value("Information"), Value("Info"));
            var bookFields = MetadataInferer.ExtractStrategyGuideBookFields(
                information,
                $"ASIN: {Value("ASIN")}",
                $"ISBN: {Value("ISBN")}",
                $"ISBN-10: {Value("ISBN10")}",
                $"ISBN-10: {Value("ISBN-10")}",
                $"ISBN-13: {Value("ISBN13")}",
                $"ISBN-13: {Value("ISBN-13")}",
                $"Language: {Value("Language")}",
                $"LanguageISO: {Value("LanguageISO")}",
                $"LanguageTag: {Value("LanguageTag")}",
                $"LanguageCode: {Value("LanguageCode")}");
            return new ComicInfoMetadata(
                Title: Value("Title"),
                Series: Value("Series"),
                Number: Value("Number"),
                Writer: FirstFilled(Value("Writer"), Value("Penciller"), Value("Inker")),
                Publisher: Value("Publisher"),
                Year: FirstFilled(Value("Year"), ExtractYear(Value("Month"))),
                Summary: FirstFilled(Value("Summary"), Value("Notes")),
                Genre: Value("Genre"),
                Tags: FirstFilled(Value("Tags"), Value("Tag")),
                Rating: FirstFilled(Value("AgeRating"), Value("Rating"), Value("ESRB"), Value("ContentRating")),
                WebLink: FirstFilled(Value("Web"), Value("WebLink"), Value("Website"), Value("Link"), Value("Url"), Value("URL")),
                Information: information,
                Asin: FirstFilled(Value("ASIN"), bookFields.Asin),
                Isbn10: FirstFilled(Value("ISBN10"), Value("ISBN-10"), bookFields.Isbn10),
                Isbn13: FirstFilled(Value("ISBN13"), Value("ISBN-13"), bookFields.Isbn13),
                LanguageTag: FirstFilled(Value("LanguageISO"), Value("LanguageTag"), Value("LanguageCode"), Value("Language"), bookFields.LanguageTag),
                Volume: FirstFilled(Value("Volume"), Value("VolumeNumber")),
                Month: Value("Month"),
                Day: Value("Day"),
                Country: FirstFilled(Value("Country"), Value("Region")));
        }
        catch
        {
            return null;
        }
    }

    private static string FirstFilled(params string[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;

    private static string ExtractYear(string value)
        => System.Text.RegularExpressions.Regex.Match(value ?? string.Empty, "(19|20)\\d{2}").Value;

    private static async Task<string?> ReadTextEntryAsync(string archivePath, string entryName)
    {
        if (Path.GetExtension(archivePath).Equals(".cbz", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var zip = ZipFile.OpenRead(archivePath);
                var entry = zip.Entries.FirstOrDefault(e => string.Equals(Path.GetFileName(e.FullName), entryName, StringComparison.OrdinalIgnoreCase));
                if (entry is not null)
                {
                    await using var stream = entry.Open();
                    using var reader = new StreamReader(stream);
                    return await reader.ReadToEndAsync();
                }

                // Valid ZIP/CBZ with no matching metadata entry. Avoid a second full
                // SharpCompress walk of the same archive.
                return null;
            }
            catch
            {
                // Some magazine archives are mislabeled as CBZ but are actually RAR/7z.
                // Fall through to SharpCompress so metadata/reader access still works.
            }
        }

        using var archiveReader = ReaderFactory.OpenReader(archivePath);
        while (archiveReader.MoveToNextEntry())
        {
            var current = archiveReader.Entry;
            if (current.IsDirectory) continue;
            if (!string.Equals(Path.GetFileName(current.Key ?? string.Empty), entryName, StringComparison.OrdinalIgnoreCase)) continue;
            await using var input = archiveReader.OpenEntryStream();
            using var textReader = new StreamReader(input);
            return await textReader.ReadToEndAsync();
        }

        return null;
    }

    public static Task<(byte[] Bytes, string ContentType)?> GetCachedCoverImageAsync(string archivePath, string? preferredEntryKey = null)
    {
        if (Path.GetExtension(archivePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<(byte[] Bytes, string ContentType)?>(null);

        var key = CoverCacheKey(archivePath, preferredEntryKey);
        return TryReadCoverFromDiskAsync(key);
    }

    public static Task<(byte[] Bytes, string ContentType)?> GetCachedCoverThumbnailAsync(string archivePath, int maxWidth = 360, string? preferredEntryKey = null)
    {
        if (Path.GetExtension(archivePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<(byte[] Bytes, string ContentType)?>(null);

        maxWidth = Math.Clamp(maxWidth, 120, 720);
        var key = $"{CoverCacheKey(archivePath, preferredEntryKey)}_w{maxWidth}";
        return TryReadCoverThumbnailFromDiskAsync(key);
    }

    public static async Task<(byte[] Bytes, string ContentType)?> GetCoverThumbnailAsync(string archivePath, int maxWidth = 360, string? preferredEntryKey = null)
    {
        if (Path.GetExtension(archivePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            return null;

        maxWidth = Math.Clamp(maxWidth, 120, 720);
        var key = $"{CoverCacheKey(archivePath, preferredEntryKey)}_w{maxWidth}";
        var cached = await TryReadCoverThumbnailFromDiskAsync(key);
        if (cached is not null) return cached;

        // Use the normal cover cache/extraction path first, then derive a small
        // persistent JPEG thumbnail.  This keeps large archive pages out of the
        // browser grid and makes the second pass through a library effectively
        // instant.
        var cover = await GetCoverImageAsync(archivePath, preferredEntryKey);
        if (cover is null) return null;

        await CoverThumbnailGate.WaitAsync();
        try
        {
            cached = await TryReadCoverThumbnailFromDiskAsync(key);
            if (cached is not null) return cached;

            var thumb = await CreateCoverThumbnailAsync(cover.Value.Bytes, maxWidth);
            if (thumb is null) return cover;

            await TryWriteCoverThumbnailToDiskAsync(key, thumb.Value.Bytes);
            return thumb;
        }
        finally
        {
            CoverThumbnailGate.Release();
        }
    }

    private static async Task<(byte[] Bytes, string ContentType)?> CreateCoverThumbnailAsync(byte[] bytes, int maxWidth)
    {
        try
        {
            await using var input = new MemoryStream(bytes);
            using var image = await Image.LoadAsync(input);
            if (image.Width <= 0 || image.Height <= 0) return null;

            var targetWidth = Math.Min(maxWidth, image.Width);
            var scale = targetWidth / (double)Math.Max(1, image.Width);
            var targetHeight = Math.Max(1, (int)Math.Round(image.Height * scale));
            if (targetWidth != image.Width || targetHeight != image.Height)
            {
                image.Mutate(x => x.Resize(targetWidth, targetHeight));
            }

            await using var output = new MemoryStream();
            await image.SaveAsJpegAsync(output, new JpegEncoder { Quality = 74 });
            return (output.ToArray(), "image/jpeg");
        }
        catch
        {
            return null;
        }
    }

    public static async Task<(byte[] Bytes, string ContentType)?> GetCoverImageAsync(string archivePath, string? preferredEntryKey = null)
    {
        if (Path.GetExtension(archivePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            return null;

        var key = CoverCacheKey(archivePath, preferredEntryKey);
        var lazy = CoverCache.GetOrAdd(key, _ => new Lazy<Task<(byte[] Bytes, string ContentType)?>>(
            () => LoadCoverImageAsync(archivePath, key, preferredEntryKey),
            LazyThreadSafetyMode.ExecutionAndPublication));
        try
        {
            return await lazy.Value;
        }
        finally
        {
            // Keep cover bytes on disk, not pinned in process memory. The dictionary
            // only de-duplicates simultaneous burst requests from the UI.
            CoverCache.TryRemove(key, out _);
        }
    }

    private static async Task<(byte[] Bytes, string ContentType)?> LoadCoverImageAsync(string archivePath, string key, string? preferredEntryKey = null)
    {
        var cached = await TryReadCoverFromDiskAsync(key);
        if (cached is not null) return cached;

        // Cover requests can arrive in large bursts when the library grid is rendered.
        // Limit archive reads so network-hosted magazine folders do not appear to hang
        // or starve the app while dozens of covers are requested at once.
        await CoverReadGate.WaitAsync();
        try
        {
            cached = await TryReadCoverFromDiskAsync(key);
            if (cached is not null) return cached;

            // Do not open large archives for new cover extraction while a scan or
            // archive write is already walking the same mounted library files.
            // Endpoint handlers return 503 during this window so the browser retries
            // instead of permanently caching a missing-cover placeholder.
            if (GuidevaultLibraryIoGate.IsBusy) return null;

            var image = await GetCoverImageFromArchiveAsync(archivePath, preferredEntryKey);
            if (image is null)
            {
                CoverCache.TryRemove(key, out _);
                ClearEntryCacheForPath(archivePath);
                return null;
            }
            await TryWriteCoverToDiskAsync(key, image.Value.Bytes, image.Value.ContentType);
            return image;
        }
        catch
        {
            CoverCache.TryRemove(key, out _);
            ClearEntryCacheForPath(archivePath);
            return null;
        }
        finally
        {
            CoverReadGate.Release();
        }
    }


    private static async Task<(byte[] Bytes, string ContentType)?> GetCoverImageFromArchiveAsync(string archivePath, string? preferredEntryKey = null)
    {
        var ext = Path.GetExtension(archivePath);
        if (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase)) return null;

        // Covers must be chosen from the same normalized page list the reader uses.
        // Some archives store entries out of page order, so never trust raw ZIP/RAR
        // order for the cover. Prefer an explicit ComicInfo FrontCover page when
        // available, then explicit/front-style names, then 00/000, then 01/001,
        // then the normal natural page sort.
        var imageEntries = GetImageEntries(archivePath);
        if (imageEntries.Length == 0) return null;

        var candidates = new List<string>();
        var seenCandidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalizedPreferred = Normalize(preferredEntryKey ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(normalizedPreferred))
        {
            var matchedPreferred = imageEntries.FirstOrDefault(entry => string.Equals(entry, normalizedPreferred, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(matchedPreferred) && seenCandidates.Add(matchedPreferred)) candidates.Add(matchedPreferred);
        }
        foreach (var entry in await GetCoverCandidateEntriesAsync(archivePath, imageEntries))
        {
            if (seenCandidates.Add(entry)) candidates.Add(entry);
            if (candidates.Count >= 16) break;
        }

        var candidateKeys = candidates.Take(16).ToArray();
        if (candidateKeys.Length == 0) return null;

        // Prefer System.IO.Compression for normal CBZ files because it is fast and
        // random-access. If the file is a mislabeled archive, fall back to
        // SharpCompress instead of treating the item as unreadable.
        if (ext.Equals(".cbz", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var zip = ZipFile.OpenRead(archivePath);
                foreach (var entryKey in candidateKeys)
                {
                    try
                    {
                        var entry = zip.Entries.FirstOrDefault(e => string.Equals(Normalize(e.FullName), entryKey, StringComparison.OrdinalIgnoreCase));
                        if (entry is null) continue;

                        var contentType = ContentTypeFromExtension(Path.GetExtension(entryKey));
                        await using var stream = entry.Open();
                        using var output = new MemoryStream();
                        await stream.CopyToAsync(output);
                        var bytes = output.ToArray();
                        if (LooksLikeImageBytes(bytes, contentType)) return (bytes, contentType);
                    }
                    catch
                    {
                        // Try the next likely cover page.
                    }
                }
            }
            catch
            {
                // Not a valid ZIP despite the .cbz extension, or temporarily unreadable.
                // Fall through to SharpCompress.
            }
        }

        var candidateRanks = candidateKeys
            .Select((key, index) => new { key, index })
            .ToDictionary(x => x.key, x => x.index, StringComparer.OrdinalIgnoreCase);
        (byte[] Bytes, string ContentType)? best = null;
        var bestRank = int.MaxValue;

        using var reader = ReaderFactory.OpenReader(archivePath);
        while (reader.MoveToNextEntry())
        {
            var current = reader.Entry;
            if (current.IsDirectory) continue;

            var entryKey = Normalize(current.Key ?? string.Empty);
            if (!candidateRanks.TryGetValue(entryKey, out var rank) || rank >= bestRank) continue;

            try
            {
                var contentType = ContentTypeFromExtension(Path.GetExtension(entryKey));
                await using var input = reader.OpenEntryStream();
                using var output = new MemoryStream();
                await input.CopyToAsync(output);
                var bytes = output.ToArray();
                if (!LooksLikeImageBytes(bytes, contentType)) continue;

                best = (bytes, contentType);
                bestRank = rank;
                if (bestRank == 0) return best;
            }
            catch
            {
                // Keep looking for another usable candidate page.
            }
        }

        return best;
    }

    private static string CoverCacheKey(string archivePath, string? preferredEntryKey = null)
    {
        var preferred = Normalize(preferredEntryKey ?? string.Empty).Trim();
        try
        {
            var info = new FileInfo(archivePath);
            var raw = $"{CoverCacheVersion}|{info.FullName}|{info.Length}|{info.LastWriteTimeUtc.Ticks}|{preferred}";
            return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
        }
        catch
        {
            return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{CoverCacheVersion}|{archivePath}|{preferred}"))).ToLowerInvariant();
        }
    }

    private static async Task<(byte[] Bytes, string ContentType)?> TryReadCoverThumbnailFromDiskAsync(string key)
    {
        try
        {
            Directory.CreateDirectory(CoverThumbnailCacheDirectory);
            var file = Path.Combine(CoverThumbnailCacheDirectory, key + ".jpg");
            if (!File.Exists(file)) return null;
            return (await File.ReadAllBytesAsync(file), "image/jpeg");
        }
        catch
        {
            return null;
        }
    }

    private static async Task TryWriteCoverThumbnailToDiskAsync(string key, byte[] bytes)
    {
        try
        {
            Directory.CreateDirectory(CoverThumbnailCacheDirectory);
            var path = Path.Combine(CoverThumbnailCacheDirectory, key + ".jpg");
            if (!File.Exists(path)) await File.WriteAllBytesAsync(path, bytes);
        }
        catch
        {
            // Thumbnail cache is a performance optimization. Never fail a cover request because it could not persist.
        }
    }

    private static async Task<(byte[] Bytes, string ContentType)?> TryReadCoverFromDiskAsync(string key)
    {
        try
        {
            Directory.CreateDirectory(CoverCacheDirectory);

            // Avoid Directory.EnumerateFiles(key + ".*") for every cover request.
            // Large libraries can have thousands of cached covers, and wildcard
            // enumeration becomes visible when a menu asks for many covers at once.
            foreach (var extension in KnownCoverCacheExtensions)
            {
                var file = Path.Combine(CoverCacheDirectory, key + extension);
                if (!File.Exists(file)) continue;
                return (await File.ReadAllBytesAsync(file), ContentTypeFromExtension(extension));
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task TryWriteCoverToDiskAsync(string key, byte[] bytes, string contentType)
    {
        try
        {
            Directory.CreateDirectory(CoverCacheDirectory);
            var extension = ExtensionFromContentType(contentType);
            var path = Path.Combine(CoverCacheDirectory, key + extension);
            if (!File.Exists(path)) await File.WriteAllBytesAsync(path, bytes);
        }
        catch
        {
            // Cover cache is a performance optimization. Never fail a cover request because it could not persist.
        }
    }

    private static string ExtensionFromContentType(string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/png" => ".png",
        "image/webp" => ".webp",
        "image/gif" => ".gif",
        "image/bmp" => ".bmp",
        _ => ".jpg"
    };

    private static string ContentTypeFromExtension(string extension) => extension.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        _ => "image/jpeg"
    };

    public static Task<int> GetImagePageCountAsync(string archivePath)
    {
        if (Path.GetExtension(archivePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase)) return Task.FromResult(0);
        return Task.FromResult(GetImageEntries(archivePath).Length);
    }

    public static async Task<(byte[] Bytes, string ContentType)?> GetImagePageAsync(string archivePath, int pageIndex)
    {
        var entries = GetImageEntries(archivePath);
        if (pageIndex < 0 || pageIndex >= entries.Length) return null;
        var entryKey = entries[pageIndex];
        var ext = Path.GetExtension(entryKey).ToLowerInvariant();
        var contentType = ContentTypeFromExtension(ext);

        if (Path.GetExtension(archivePath).Equals(".cbz", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var zip = ZipFile.OpenRead(archivePath);
                var entry = zip.Entries.FirstOrDefault(e => string.Equals(Normalize(e.FullName), entryKey, StringComparison.OrdinalIgnoreCase));
                if (entry is not null)
                {
                    await using var stream = entry.Open();
                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms);
                    var bytes = ms.ToArray();
                    return LooksLikeImageBytes(bytes, contentType) ? (bytes, contentType) : null;
                }
            }
            catch
            {
                // Mislabelled CBZ or transient zip read failure; try SharpCompress below.
            }
        }

        using var reader = ReaderFactory.OpenReader(archivePath);
        while (reader.MoveToNextEntry())
        {
            var current = reader.Entry;
            if (current.IsDirectory) continue;
            if (!string.Equals(Normalize(current.Key ?? string.Empty), entryKey, StringComparison.OrdinalIgnoreCase)) continue;

            await using var input = reader.OpenEntryStream();
            using var output = new MemoryStream();
            await input.CopyToAsync(output);
            var bytes = output.ToArray();
            return LooksLikeImageBytes(bytes, contentType) ? (bytes, contentType) : null;
        }

        return null;
    }

    public static string[] GetImageEntryKeys(string archivePath) => GetImageEntries(archivePath).ToArray();

    public static bool DeleteImageEntry(string archivePath, string entryKey)
    {
        var normalizedEntry = Normalize(entryKey ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedEntry)) return false;

        var extension = Path.GetExtension(archivePath);
        if (!extension.Equals(".cbz", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Only CBZ/ZIP archives can be rewritten by Guidevault.");

        if (!File.Exists(archivePath)) return false;

        using (var zip = ZipFile.Open(archivePath, ZipArchiveMode.Update))
        {
            var entry = zip.Entries.FirstOrDefault(e => string.Equals(Normalize(e.FullName), normalizedEntry, StringComparison.OrdinalIgnoreCase));
            if (entry is null || !IsImageEntryName(entry.FullName)) return false;
            entry.Delete();
        }

        ClearCacheForPath(archivePath);
        return true;
    }

    public static async Task<(byte[] Bytes, string ContentType)?> GetImagePageThumbnailAsync(string archivePath, int pageIndex, int maxWidth = 180)
    {
        var page = await GetImagePageAsync(archivePath, pageIndex);
        if (page is null) return null;
        var thumb = await CreateCoverThumbnailAsync(page.Value.Bytes, Math.Clamp(maxWidth, 80, 360));
        return thumb ?? page;
    }

    private static string[] GetImageEntries(string archivePath)
    {
        var cacheKey = ArchiveVersionedCacheKey(archivePath);
        if (EntryCache.TryGetValue(cacheKey, out var cached) && cached.Length > 0) return cached;

        try
        {
            var entries = ReadImageEntries(archivePath);

            // Cache only successful non-empty reads. Temporary network/archive read
            // failures should not poison the session and make covers/pages randomly
            // unavailable until Guidevault restarts. The cache key includes file size
            // and LastWriteTime so replacing/fixing an archive at the same path cannot
            // leave the reader using an old page manifest.
            if (entries.Length > 0) EntryCache[cacheKey] = entries;
            else EntryCache.TryRemove(cacheKey, out _);
            return entries;
        }
        catch
        {
            EntryCache.TryRemove(cacheKey, out _);
            return [];
        }
    }

    private static string[] ReadImageEntries(string archivePath)
    {
        var ext = Path.GetExtension(archivePath);
        if (ext.Equals(".cbz", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var zip = ZipFile.OpenRead(archivePath);
                var zipEntries = zip.Entries
                    .Where(IsUsableZipImageEntry)
                    .Select(e => Normalize(e.FullName))
                    .OrderBy(NaturalSortKey)
                    .ToArray();
                if (zipEntries.Length > 0) return zipEntries;
            }
            catch
            {
                // Fall through to SharpCompress for mislabeled .cbz files.
            }
        }

        using var reader = ReaderFactory.OpenReader(archivePath);
        var found = new List<string>();
        while (reader.MoveToNextEntry())
        {
            var entry = reader.Entry;
            if (entry.IsDirectory) continue;
            var key = Normalize(entry.Key ?? string.Empty);
            if (IsImageEntryName(key)) found.Add(key);
        }

        return found
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(NaturalSortKey)
            .ToArray();
    }

    private static bool IsImageEntryName(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && ImageExtensions.Contains(Path.GetExtension(value).ToLowerInvariant())
           && !Path.GetFileName(value).StartsWith("._", StringComparison.Ordinal);

    private static bool IsUsableZipImageEntry(ZipArchiveEntry entry)
        => !string.IsNullOrWhiteSpace(entry.Name)
           && entry.Length > 0
           && IsImageEntryName(entry.Name);

    private static async Task<IEnumerable<string>> GetCoverCandidateEntriesAsync(string archivePath, IReadOnlyList<string> imageEntries)
    {
        var preferred = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in await GetComicInfoFrontCoverEntriesAsync(archivePath, imageEntries))
        {
            if (seen.Add(entry)) preferred.Add(entry);
        }

        foreach (var entry in imageEntries
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(CoverCandidatePriority)
            .ThenBy(entry => NaturalSortKey(Path.GetFileName(entry)))
            .ThenBy(NaturalSortKey)
            .ThenBy(entry => entry, StringComparer.OrdinalIgnoreCase))
        {
            if (seen.Add(entry)) preferred.Add(entry);
        }

        return preferred;
    }

    private static async Task<IReadOnlyList<string>> GetComicInfoFrontCoverEntriesAsync(string archivePath, IReadOnlyList<string> imageEntries)
    {
        if (imageEntries.Count == 0) return [];

        try
        {
            var xml = await ReadTextEntryAsync(archivePath, "comicinfo.xml");
            if (string.IsNullOrWhiteSpace(xml)) return [];

            var doc = XDocument.Parse(xml);
            var pageElements = doc.Descendants()
                .Where(e => string.Equals(e.Name.LocalName, "Page", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(e.Name.LocalName, "ComicPageInfo", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (pageElements.Length == 0) return [];

            string ReadPageValue(XElement element, string name)
            {
                var attribute = element.Attributes().FirstOrDefault(a => string.Equals(a.Name.LocalName, name, StringComparison.OrdinalIgnoreCase));
                if (attribute is not null) return attribute.Value.Trim();

                var child = element.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, name, StringComparison.OrdinalIgnoreCase));
                return child?.Value.Trim() ?? string.Empty;
            }

            var output = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var page in pageElements)
            {
                var type = ReadPageValue(page, "Type");
                if (!type.Equals("FrontCover", StringComparison.OrdinalIgnoreCase)) continue;

                var key = Normalize(ReadPageValue(page, "Key"));
                if (!string.IsNullOrWhiteSpace(key))
                {
                    var keyed = imageEntries.FirstOrDefault(entry =>
                        string.Equals(Normalize(entry), key, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(Path.GetFileName(Normalize(entry)), Path.GetFileName(key), StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrWhiteSpace(keyed) && seen.Add(keyed)) output.Add(keyed);
                }

                var image = ReadPageValue(page, "Image");
                if (int.TryParse(image, out var index) && index >= 0 && index < imageEntries.Count)
                {
                    var indexed = imageEntries[index];
                    if (seen.Add(indexed)) output.Add(indexed);
                }
            }

            return output;
        }
        catch
        {
            return [];
        }
    }

    private static int CoverCandidatePriority(string entryKey)
    {
        var fileName = Path.GetFileNameWithoutExtension(Normalize(entryKey)).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(fileName)) return 50;

        var compact = Regex.Replace(fileName, @"[\s_\-.]+", string.Empty);
        if (compact is "cover" or "front" or "frontcover" or "fc" or "folder"
            || compact.StartsWith("cover", StringComparison.OrdinalIgnoreCase)
            || compact.EndsWith("cover", StringComparison.OrdinalIgnoreCase))
            return 0;

        // Many scanned magazines use inconsistent front-page names such as
        // 000a.jpg, Page 000a.jpg, p000a.jpg, or scan000a.jpg. Treat these as
        // page zero so the cover does not incorrectly jump to 001.jpg.
        var pageMatch = Regex.Match(compact, @"^(?:page|pg|p|scan|img|image)?0*(\d+)([a-z]+)?$", RegexOptions.IgnoreCase);
        if (pageMatch.Success && int.TryParse(pageMatch.Groups[1].Value, out var pageNumber))
        {
            if (pageNumber == 0) return 1;
            if (pageNumber == 1) return 2;
            return 10 + Math.Min(pageNumber, 9999);
        }

        return 20;
    }

    private static bool LooksLikeImageBytes(byte[]? bytes, string contentType)
    {
        if (bytes is null || bytes.Length < 8) return false;
        if (bytes[0] == 0xFF && bytes.Length > 2 && bytes[1] == 0xD8) return true; // jpg
        if (bytes.Length > 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return true; // png
        if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46) return true; // gif
        if (bytes[0] == 0x42 && bytes[1] == 0x4D) return true; // bmp
        if (bytes.Length > 12 && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50) return true; // webp
        return contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) && bytes.Length > 512;
    }

    private static string Normalize(string value) => value.Replace('\\', '/');

    private static string NaturalSortKey(string value)
    {
        return System.Text.RegularExpressions.Regex.Replace(value.ToLowerInvariant(), "\\d+", m => m.Value.PadLeft(12, '0'));
    }
}



public sealed record OpenLibraryMetadataResult(
    string Id,
    string WorkKey,
    string EditionKey,
    string Title,
    string AuthorWriter,
    string Publisher,
    string PublishYear,
    string Isbn10,
    string Isbn13,
    string Language,
    string Summary,
    int? PageCount,
    string CoverPreviewUrl,
    string SourceUrl,
    string MatchBy,
    string Confidence);

static class OpenLibraryMetadataClient
{
    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Guidevault", GuidevaultBuildInfo.Version));
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OpenLibraryMetadataLookup", "1.1"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    public static async Task<IReadOnlyList<OpenLibraryMetadataResult>> SearchAsync(string? q, string? secondary, string? isbn, string? title, string? gameTitle, string? publisher, string? year, int limit)
    {
        limit = Math.Clamp(limit, 1, 24);
        var explicitIsbn = CleanIsbn(FirstNonEmpty(isbn, LooksLikeIsbn(q) ? q : null));
        var hasExplicitIsbn = explicitIsbn.Length is 10 or 13;
        var primaryTitle = FirstNonEmpty(q, title, gameTitle);
        var secondaryTitle = FirstNonEmpty(secondary, gameTitle);
        var fields = "key,title,author_name,first_publish_year,publisher,publish_year,isbn,language,number_of_pages_median,cover_i,edition_key";
        var results = new List<OpenLibraryMetadataResult>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (hasExplicitIsbn)
        {
            await AddSearchResultsAsync(results, seen, BuildSearchUrl("isbn", explicitIsbn, limit, fields), "ISBN", primaryTitle, explicitIsbn, limit);
            if (results.Count >= limit) return results;
        }

        foreach (var plan in BuildTitleFirstSearchPlan(primaryTitle, secondaryTitle, publisher, year))
        {
            await AddSearchResultsAsync(results, seen, BuildSearchUrl(plan.Mode, plan.Query, limit, fields), plan.MatchBy, plan.Query, explicitIsbn, limit);
            if (results.Count >= limit) break;
        }

        return results;
    }

    public static async Task<OpenLibraryMetadataResult> ResolveAsync(JsonElement payload)
    {
        var fallback = FromPayload(payload);
        var workKey = NormalizeWorkKey(FirstNonEmpty(GetString(payload, "workKey"), fallback.WorkKey));
        var editionKey = NormalizeEditionKey(FirstNonEmpty(GetString(payload, "editionKey"), fallback.EditionKey));
        var isbn = CleanIsbn(FirstNonEmpty(GetString(payload, "isbn10"), GetString(payload, "isbn13"), GetString(payload, "isbn"), fallback.Isbn10, fallback.Isbn13));

        JsonElement? edition = null;
        JsonElement? work = null;

        if (!string.IsNullOrWhiteSpace(editionKey))
            edition = await TryGetJsonAsync($"https://openlibrary.org{editionKey}.json");
        else if (isbn.Length is 10 or 13)
            edition = await TryGetJsonAsync($"https://openlibrary.org/isbn/{Uri.EscapeDataString(isbn)}.json");

        if (edition.HasValue)
        {
            editionKey = NormalizeEditionKey(FirstNonEmpty(edition.Value.TryGetProperty("key", out var ek) ? ek.GetString() : null, editionKey));
            workKey = NormalizeWorkKey(FirstNonEmpty(workKey, FirstNestedKey(edition.Value, "works")));
        }

        if (!string.IsNullOrWhiteSpace(workKey))
            work = await TryGetJsonAsync($"https://openlibrary.org{workKey}.json");

        var title = FirstNonEmpty(
            edition.HasValue ? GetString(edition.Value, "title") : null,
            work.HasValue ? GetString(work.Value, "title") : null,
            fallback.Title);
        var publisher = FirstNonEmpty(
            edition.HasValue ? FirstStringArrayValue(edition.Value, "publishers") : null,
            fallback.Publisher);
        var publishYear = FirstNonEmpty(
            edition.HasValue ? ExtractYear(GetString(edition.Value, "publish_date")) : null,
            fallback.PublishYear);
        var isbn10 = FirstNonEmpty(
            edition.HasValue ? FirstStringArrayValue(edition.Value, "isbn_10") : null,
            fallback.Isbn10);
        var isbn13 = FirstNonEmpty(
            edition.HasValue ? FirstStringArrayValue(edition.Value, "isbn_13") : null,
            fallback.Isbn13);
        var language = FirstNonEmpty(
            edition.HasValue ? NormalizeLanguage(FirstNestedKey(edition.Value, "languages")) : null,
            fallback.Language);
        var summary = FirstNonEmpty(
            work.HasValue ? DescriptionText(work.Value) : null,
            edition.HasValue ? DescriptionText(edition.Value) : null,
            fallback.Summary);
        var pageCount = FirstInt(
            edition.HasValue ? GetInt(edition.Value, "number_of_pages") : null,
            fallback.PageCount);
        var coverId = FirstNonEmpty(
            edition.HasValue ? FirstIntArrayValue(edition.Value, "covers")?.ToString() : null,
            work.HasValue ? FirstIntArrayValue(work.Value, "covers")?.ToString() : null,
            CoverIdFromUrl(fallback.CoverPreviewUrl));
        var coverUrl = !string.IsNullOrWhiteSpace(coverId)
            ? $"https://covers.openlibrary.org/b/id/{Uri.EscapeDataString(coverId)}-M.jpg"
            : fallback.CoverPreviewUrl;

        return fallback with
        {
            Id = FirstNonEmpty(editionKey, workKey, fallback.Id),
            WorkKey = workKey,
            EditionKey = editionKey,
            Title = title,
            AuthorWriter = fallback.AuthorWriter,
            Publisher = publisher,
            PublishYear = publishYear,
            Isbn10 = CleanIsbn(isbn10),
            Isbn13 = CleanIsbn(isbn13),
            Language = NormalizeLanguage(language),
            Summary = summary,
            PageCount = pageCount,
            CoverPreviewUrl = coverUrl,
            SourceUrl = !string.IsNullOrWhiteSpace(editionKey)
                ? $"https://openlibrary.org{editionKey}"
                : (!string.IsNullOrWhiteSpace(workKey) ? $"https://openlibrary.org{workKey}" : fallback.SourceUrl)
        };
    }

    private sealed record SearchPlan(string Mode, string Query, string MatchBy);

    private static IEnumerable<SearchPlan> BuildTitleFirstSearchPlan(string primaryTitle, string secondaryTitle, string? publisher, string? year)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(List<SearchPlan> plans, string mode, string query, string matchBy)
        {
            query = CleanSearchText(query);
            if (string.IsNullOrWhiteSpace(query)) return;
            var key = mode + "|" + NormalizeText(query);
            if (seen.Add(key)) plans.Add(new SearchPlan(mode, query, matchBy));
        }

        var output = new List<SearchPlan>();
        Add(output, "title", primaryTitle, "Title");
        Add(output, "title", secondaryTitle, "Game title");
        Add(output, "q", JoinNonEmpty(primaryTitle, secondaryTitle), "Title + secondary");
        Add(output, "q", JoinNonEmpty(primaryTitle, publisher, year), "Title + details");
        Add(output, "q", JoinNonEmpty(primaryTitle, "strategy guide"), "Strategy guide title");
        Add(output, "q", JoinNonEmpty(secondaryTitle, "strategy guide"), "Game title + strategy guide");
        Add(output, "q", primaryTitle, "Broad title");
        Add(output, "q", secondaryTitle, "Broad game title");
        return output;
    }

    private static async Task AddSearchResultsAsync(List<OpenLibraryMetadataResult> results, HashSet<string> seen, string url, string matchBy, string query, string isbn, int limit)
    {
        if (string.IsNullOrWhiteSpace(url) || results.Count >= limit) return;
        JsonDocument? doc = null;
        try
        {
            using var stream = await Http.GetStreamAsync(url);
            doc = await JsonDocument.ParseAsync(stream);
            if (!doc.RootElement.TryGetProperty("docs", out var docs) || docs.ValueKind != JsonValueKind.Array) return;
            foreach (var item in docs.EnumerateArray())
            {
                var result = FromSearchDoc(item, matchBy, query, isbn);
                if (string.IsNullOrWhiteSpace(result.Title)) continue;
                var key = FirstNonEmpty(result.EditionKey, result.WorkKey, NormalizeText(result.Title + result.AuthorWriter + result.PublishYear + result.Publisher));
                if (!seen.Add(key)) continue;
                results.Add(result);
                if (results.Count >= limit) break;
            }
        }
        catch
        {
            // One Open Library query variant failing should not prevent the fallback title/game-title variants from running.
            return;
        }
        finally
        {
            doc?.Dispose();
        }
    }

    private static string BuildSearchUrl(string mode, string value, int limit, string fields)
    {
        value = value.Trim();
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var param = mode.Equals("isbn", StringComparison.OrdinalIgnoreCase) ? "isbn"
            : mode.Equals("title", StringComparison.OrdinalIgnoreCase) ? "title"
            : "q";
        return $"https://openlibrary.org/search.json?{param}={Uri.EscapeDataString(value)}&limit={limit}&fields={Uri.EscapeDataString(fields)}";
    }

    private static OpenLibraryMetadataResult FromSearchDoc(JsonElement item, string matchBy, string query, string isbn)
    {
        var workKey = NormalizeWorkKey(GetString(item, "key"));
        var edition = FirstStringArrayValue(item, "edition_key");
        var editionKey = NormalizeEditionKey(edition);
        var isbnValues = StringArrayValues(item, "isbn").Select(CleanIsbn).Where(v => v.Length is 10 or 13).Distinct().ToArray();
        var isbn10 = isbnValues.FirstOrDefault(v => v.Length == 10) ?? string.Empty;
        var isbn13 = isbnValues.FirstOrDefault(v => v.Length == 13) ?? string.Empty;
        var title = GetString(item, "title") ?? string.Empty;
        var author = string.Join(", ", StringArrayValues(item, "author_name").Take(4));
        var publisher = FirstStringArrayValue(item, "publisher") ?? string.Empty;
        var year = FirstNonEmpty(GetInt(item, "first_publish_year")?.ToString(), FirstIntArrayValue(item, "publish_year")?.ToString());
        var language = NormalizeLanguage(FirstStringArrayValue(item, "language"));
        var pageCount = GetInt(item, "number_of_pages_median");
        var coverId = GetInt(item, "cover_i")?.ToString() ?? string.Empty;
        var confidence = Confidence(matchBy, query, isbn, title, isbn10, isbn13, year, publisher);
        return new OpenLibraryMetadataResult(
            Id: FirstNonEmpty(editionKey, workKey, title),
            WorkKey: workKey,
            EditionKey: editionKey,
            Title: title,
            AuthorWriter: author,
            Publisher: publisher,
            PublishYear: year,
            Isbn10: isbn10,
            Isbn13: isbn13,
            Language: language,
            Summary: string.Empty,
            PageCount: pageCount,
            CoverPreviewUrl: string.IsNullOrWhiteSpace(coverId) ? string.Empty : $"https://covers.openlibrary.org/b/id/{Uri.EscapeDataString(coverId)}-M.jpg",
            SourceUrl: !string.IsNullOrWhiteSpace(editionKey) ? $"https://openlibrary.org{editionKey}" : (!string.IsNullOrWhiteSpace(workKey) ? $"https://openlibrary.org{workKey}" : string.Empty),
            MatchBy: matchBy,
            Confidence: confidence);
    }

    private static OpenLibraryMetadataResult FromPayload(JsonElement payload)
    {
        return new OpenLibraryMetadataResult(
            Id: FirstNonEmpty(GetString(payload, "id"), GetString(payload, "editionKey"), GetString(payload, "workKey")),
            WorkKey: NormalizeWorkKey(GetString(payload, "workKey")),
            EditionKey: NormalizeEditionKey(GetString(payload, "editionKey")),
            Title: GetString(payload, "title") ?? string.Empty,
            AuthorWriter: GetString(payload, "authorWriter") ?? string.Empty,
            Publisher: GetString(payload, "publisher") ?? string.Empty,
            PublishYear: GetString(payload, "publishYear") ?? string.Empty,
            Isbn10: CleanIsbn(GetString(payload, "isbn10")),
            Isbn13: CleanIsbn(GetString(payload, "isbn13")),
            Language: NormalizeLanguage(GetString(payload, "language")),
            Summary: GetString(payload, "summary") ?? string.Empty,
            PageCount: GetInt(payload, "pageCount"),
            CoverPreviewUrl: GetString(payload, "coverPreviewUrl") ?? string.Empty,
            SourceUrl: GetString(payload, "sourceUrl") ?? string.Empty,
            MatchBy: GetString(payload, "matchBy") ?? string.Empty,
            Confidence: GetString(payload, "confidence") ?? string.Empty);
    }

    private static async Task<JsonElement?> TryGetJsonAsync(string url)
    {
        try
        {
            using var stream = await Http.GetStreamAsync(url);
            using var doc = await JsonDocument.ParseAsync(stream);
            return doc.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }

    private static bool LooksLikeIsbn(string? value)
    {
        var raw = value ?? string.Empty;
        var clean = CleanIsbn(raw);
        if (clean.Length is not (10 or 13)) return false;
        return Regex.IsMatch(raw, @"^[\s0-9Xx\-]+$");
    }

    private static string CleanSearchText(string value)
    {
        var text = Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();
        text = Regex.Replace(text, @"\b(?:cbz|cbr|pdf|scan|scanned|retromags)\b", " ", RegexOptions.IgnoreCase).Trim();
        return Regex.Replace(text, @"\s+", " ").Trim(' ', '-', '_');
    }

    private static string JoinNonEmpty(params string?[] values) => string.Join(" ", values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!.Trim()));

    private static string? GetString(JsonElement json, string name)
    {
        if (json.ValueKind != JsonValueKind.Object || !json.TryGetProperty(name, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static int? GetInt(JsonElement json, string name)
    {
        if (json.ValueKind != JsonValueKind.Object || !json.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed)) return parsed;
        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out parsed)) return parsed;
        return null;
    }

    private static int? FirstInt(params int?[] values) => values.FirstOrDefault(v => v.HasValue && v.Value > 0);

    private static IEnumerable<string> StringArrayValues(JsonElement json, string name)
    {
        if (json.ValueKind != JsonValueKind.Object || !json.TryGetProperty(name, out var value)) yield break;
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                var text = item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString();
                if (!string.IsNullOrWhiteSpace(text)) yield return text.Trim();
            }
        }
        else if (value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
        {
            yield return value.GetString()!.Trim();
        }
    }

    private static string? FirstStringArrayValue(JsonElement json, string name) => StringArrayValues(json, name).FirstOrDefault();

    private static int? FirstIntArrayValue(JsonElement json, string name)
    {
        if (json.ValueKind != JsonValueKind.Object || !json.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array) return null;
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var parsed)) return parsed;
            if (item.ValueKind == JsonValueKind.String && int.TryParse(item.GetString(), out parsed)) return parsed;
        }
        return null;
    }

    private static string FirstNestedKey(JsonElement json, string name)
    {
        if (json.ValueKind != JsonValueKind.Object || !json.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array) return string.Empty;
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("key", out var key)) return key.GetString() ?? string.Empty;
        }
        return string.Empty;
    }

    private static string DescriptionText(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Object || !json.TryGetProperty("description", out var value)) return string.Empty;
        if (value.ValueKind == JsonValueKind.String) return value.GetString()?.Trim() ?? string.Empty;
        if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("value", out var nested) && nested.ValueKind == JsonValueKind.String)
            return nested.GetString()?.Trim() ?? string.Empty;
        return string.Empty;
    }

    private static string ExtractYear(string? value)
    {
        var match = Regex.Match(value ?? string.Empty, "(?<!\\d)(18|19|20)\\d{2}(?!\\d)");
        return match.Success ? match.Value : string.Empty;
    }

    private static string NormalizeWorkKey(string? value)
    {
        var v = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(v)) return string.Empty;
        if (v.StartsWith("/works/", StringComparison.OrdinalIgnoreCase)) return v;
        if (Regex.IsMatch(v, "^OL\\d+W$", RegexOptions.IgnoreCase)) return $"/works/{v}";
        return v;
    }

    private static string NormalizeEditionKey(string? value)
    {
        var v = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(v)) return string.Empty;
        if (v.StartsWith("/books/", StringComparison.OrdinalIgnoreCase)) return v;
        if (Regex.IsMatch(v, "^OL\\d+M$", RegexOptions.IgnoreCase)) return $"/books/{v}";
        return v;
    }

    private static string CleanIsbn(string? value) => Regex.Replace(value ?? string.Empty, "[^0-9Xx]", string.Empty).ToUpperInvariant();

    private static string NormalizeLanguage(string? value)
    {
        var raw = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var key = raw.Split('/').Last().Trim().ToLowerInvariant();
        return key switch
        {
            "en" or "eng" or "english" => "English",
            "ja" or "jpn" or "japanese" => "Japanese",
            "fr" or "fre" or "fra" or "french" => "French",
            "de" or "ger" or "deu" or "german" => "German",
            "es" or "spa" or "spanish" => "Spanish",
            "it" or "ita" or "italian" => "Italian",
            _ => raw.Length <= 3 ? raw.ToUpperInvariant() : raw
        };
    }

    private static string CoverIdFromUrl(string? url)
    {
        var match = Regex.Match(url ?? string.Empty, @"/b/id/(\d+)-", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static string Confidence(string matchBy, string query, string isbn, string title, string isbn10, string isbn13, string year, string publisher)
    {
        if (matchBy.Equals("ISBN", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(isbn) && (isbn == isbn10 || isbn == isbn13)) return "High";
        var queryText = NormalizeText(query);
        var titleText = NormalizeText(title);
        if (!string.IsNullOrWhiteSpace(queryText) && !string.IsNullOrWhiteSpace(titleText))
        {
            if (queryText.Equals(titleText, StringComparison.OrdinalIgnoreCase)) return "High";
            if (queryText.Contains(titleText, StringComparison.OrdinalIgnoreCase) || titleText.Contains(queryText, StringComparison.OrdinalIgnoreCase)) return "Medium";
        }
        if (!string.IsNullOrWhiteSpace(year) || !string.IsNullOrWhiteSpace(publisher)) return "Medium";
        return "Low";
    }

    private static string NormalizeText(string value) => Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9]+", " ").Trim();

    private static string FirstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;
}


public sealed record IgdbGameMetadataResult(
    int Id,
    string Name,
    string GameTitle,
    string[] Developers,
    string[] Publishers,
    string GameReleaseYear,
    string GameFranchise,
    string[] Genres,
    string[] AssociatedPlatforms,
    string PreferredPlatform,
    string CoverPreviewUrl,
    string SourceUrl,
    string MatchBy,
    string Confidence);

public sealed record IgdbCredentialStatus(bool Ok, string Message, string ClientIdPreview, DateTimeOffset ExpiresAt);

static class IgdbGameMetadataClient
{
    private static readonly HttpClient Http = CreateHttpClient();
    private static readonly SemaphoreSlim TokenGate = new(1, 1);
    private static string _accessToken = string.Empty;
    private static DateTimeOffset _accessTokenExpiresAt = DateTimeOffset.MinValue;
    private static string _tokenClientId = string.Empty;
    private static string _tokenSecretFingerprint = string.Empty;

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Guidevault", GuidevaultBuildInfo.Version));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    public static async Task<IgdbCredentialStatus> TestCredentialsAsync(string? clientId, string? clientSecret)
    {
        var id = CleanCredential(clientId);
        var secret = CleanCredential(clientSecret);
        EnsureConfigured(id, secret);
        await GetAccessTokenAsync(id, secret);
        return new IgdbCredentialStatus(true, "IGDB credentials verified. Batch IGDB lookup can run.", MaskClientId(id), _accessTokenExpiresAt);
    }

    public static async Task<IReadOnlyList<IgdbGameMetadataResult>> SearchAsync(string? q, string? platform, string? year, int limit, string? clientId, string? clientSecret)
    {
        var id = CleanCredential(clientId);
        var secret = CleanCredential(clientSecret);
        EnsureConfigured(id, secret);
        var query = CleanSearchText(q);
        if (string.IsNullOrWhiteSpace(query)) throw new InvalidOperationException("Enter a game title to search IGDB.");
        limit = Math.Clamp(limit, 1, 24);

        var results = await QueryGamesAsync(query, platform, year, limit, id, secret, excludeVersions: true);
        if (results.Count == 0)
            results = await QueryGamesAsync(query, platform, year, limit, id, secret, excludeVersions: false);
        var yearHint = ParseYear(year);
        return results
            .OrderByDescending(r => ConfidenceRank(r.Confidence))
            .ThenByDescending(r => PlatformMatchScore(r.AssociatedPlatforms, platform))
            .ThenBy(r => YearDistance(r.GameReleaseYear, yearHint))
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToArray();
    }

    public static async Task<IgdbGameMetadataResult> ResolveAsync(JsonElement payload, string? clientId, string? clientSecret)
    {
        var fallback = FromPayload(payload);
        if (fallback.Id <= 0) return fallback;
        var id = CleanCredential(clientId);
        var secret = CleanCredential(clientSecret);
        EnsureConfigured(id, secret);
        var token = await GetAccessTokenAsync(id, secret);
        var body = $"fields id,name,first_release_date,genres.name,platforms.name,involved_companies.developer,involved_companies.publisher,involved_companies.company.name,franchise.name,franchises.name,collection.name,collections.name,cover.image_id,url,slug; where id = {fallback.Id}; limit 1;";
        var json = await PostIgdbAsync("games", body, id, token);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            var item = doc.RootElement.EnumerateArray().FirstOrDefault();
            if (item.ValueKind == JsonValueKind.Object)
                return FromGameJson(item, fallback.Name, string.Empty, string.Empty);
        }
        return fallback;
    }

    private static async Task<List<IgdbGameMetadataResult>> QueryGamesAsync(string query, string? platform, string? year, int limit, string clientId, string clientSecret, bool excludeVersions)
    {
        var token = await GetAccessTokenAsync(clientId, clientSecret);
        var escaped = EscapeIgdbString(query);
        var where = excludeVersions ? " where version_parent = null;" : string.Empty;
        var body = $"search \"{escaped}\"; fields id,name,first_release_date,genres.name,platforms.name,involved_companies.developer,involved_companies.publisher,involved_companies.company.name,franchise.name,franchises.name,collection.name,collections.name,cover.image_id,url,slug;{where} limit {limit};";
        var json = await PostIgdbAsync("games", body, clientId, token);
        using var doc = JsonDocument.Parse(json);
        var results = new List<IgdbGameMetadataResult>();
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return results;
        var seen = new HashSet<int>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var result = FromGameJson(item, query, platform, year);
            if (result.Id <= 0 || !seen.Add(result.Id)) continue;
            results.Add(result);
        }
        return results;
    }

    private static async Task<string> PostIgdbAsync(string endpoint, string body, string clientId, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.igdb.com/v4/{endpoint.Trim('/')}");
        request.Headers.TryAddWithoutValidation("Client-ID", clientId);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(body, Encoding.UTF8, "text/plain");
        using var response = await Http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"IGDB returned HTTP {(int)response.StatusCode}: {TrimError(text)}");
        return text;
    }

    private static async Task<string> GetAccessTokenAsync(string clientId, string clientSecret)
    {
        clientId = CleanCredential(clientId);
        clientSecret = CleanCredential(clientSecret);
        var fingerprint = SecretFingerprint(clientSecret);
        if (!string.IsNullOrWhiteSpace(_accessToken)
            && DateTimeOffset.UtcNow < _accessTokenExpiresAt.AddMinutes(-5)
            && string.Equals(_tokenClientId, clientId, StringComparison.Ordinal)
            && string.Equals(_tokenSecretFingerprint, fingerprint, StringComparison.Ordinal))
            return _accessToken;

        await TokenGate.WaitAsync();
        try
        {
            if (!string.IsNullOrWhiteSpace(_accessToken)
                && DateTimeOffset.UtcNow < _accessTokenExpiresAt.AddMinutes(-5)
                && string.Equals(_tokenClientId, clientId, StringComparison.Ordinal)
                && string.Equals(_tokenSecretFingerprint, fingerprint, StringComparison.Ordinal))
                return _accessToken;

            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["grant_type"] = "client_credentials"
            });
            using var response = await Http.PostAsync("https://id.twitch.tv/oauth2/token", content);
            var text = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                ClearAccessTokenCache();
                throw new InvalidOperationException(DescribeTwitchOAuthError(response.StatusCode, text));
            }
            using var doc = JsonDocument.Parse(text);
            var token = doc.RootElement.TryGetProperty("access_token", out var access) ? access.GetString() : string.Empty;
            var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var expires) && expires.TryGetInt32(out var seconds) ? seconds : 3600;
            if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("Twitch OAuth token response did not include an access token.");
            _accessToken = token!;
            _accessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(300, expiresIn));
            _tokenClientId = clientId;
            _tokenSecretFingerprint = fingerprint;
            return _accessToken;
        }
        finally
        {
            TokenGate.Release();
        }
    }

    private static IgdbGameMetadataResult FromGameJson(JsonElement item, string query, string? platform, string? year)
    {
        var id = GetInt(item, "id") ?? 0;
        var name = GetString(item, "name") ?? string.Empty;
        var developers = CompanyNames(item, developer: true).ToArray();
        var publishers = CompanyNames(item, publisher: true).ToArray();
        var genres = ObjectNameArray(item, "genres").ToArray();
        var platforms = NormalizePlatformNames(ObjectNameArray(item, "platforms")).ToArray();
        var franchise = FirstNonEmpty(
            ObjectName(item, "franchise"),
            ObjectNameArray(item, "franchises").FirstOrDefault(),
            ObjectName(item, "collection"),
            ObjectNameArray(item, "collections").FirstOrDefault());
        var releaseYear = ReleaseYear(item);
        var cover = CoverUrl(item);
        var slug = GetString(item, "slug");
        var sourceUrl = GetString(item, "url") ?? (id > 0 && !string.IsNullOrWhiteSpace(slug) ? $"https://www.igdb.com/games/{slug}" : string.Empty);
        return new IgdbGameMetadataResult(
            Id: id,
            Name: name,
            GameTitle: name,
            Developers: developers,
            Publishers: publishers,
            GameReleaseYear: releaseYear,
            GameFranchise: franchise,
            Genres: genres,
            AssociatedPlatforms: platforms,
            PreferredPlatform: platforms.Length == 1 ? platforms[0] : (platforms.Length > 1 ? "Multi-Platform" : string.Empty),
            CoverPreviewUrl: cover,
            SourceUrl: sourceUrl,
            MatchBy: "Game title",
            Confidence: Confidence(query, name, platforms, platform, releaseYear, year));
    }

    private static IgdbGameMetadataResult FromPayload(JsonElement payload)
    {
        return new IgdbGameMetadataResult(
            Id: GetInt(payload, "id") ?? 0,
            Name: FirstNonEmpty(GetString(payload, "name"), GetString(payload, "gameTitle")),
            GameTitle: FirstNonEmpty(GetString(payload, "gameTitle"), GetString(payload, "name")),
            Developers: StringArrayValues(payload, "developers").ToArray(),
            Publishers: StringArrayValues(payload, "publishers").ToArray(),
            GameReleaseYear: GetString(payload, "gameReleaseYear") ?? string.Empty,
            GameFranchise: GetString(payload, "gameFranchise") ?? string.Empty,
            Genres: StringArrayValues(payload, "genres").ToArray(),
            AssociatedPlatforms: NormalizePlatformNames(StringArrayValues(payload, "associatedPlatforms")).ToArray(),
            PreferredPlatform: NormalizeGuidevaultPlatformName(GetString(payload, "preferredPlatform") ?? string.Empty),
            CoverPreviewUrl: GetString(payload, "coverPreviewUrl") ?? string.Empty,
            SourceUrl: GetString(payload, "sourceUrl") ?? string.Empty,
            MatchBy: GetString(payload, "matchBy") ?? "Game title",
            Confidence: GetString(payload, "confidence") ?? "Low");
    }

    private static IEnumerable<string> CompanyNames(JsonElement json, bool developer = false, bool publisher = false)
    {
        if (!json.TryGetProperty("involved_companies", out var companies) || companies.ValueKind != JsonValueKind.Array) yield break;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in companies.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var isDeveloper = GetBool(item, "developer") == true;
            var isPublisher = GetBool(item, "publisher") == true;
            if ((developer && !isDeveloper) || (publisher && !isPublisher)) continue;
            if (!item.TryGetProperty("company", out var company)) continue;
            var name = company.ValueKind == JsonValueKind.Object ? GetString(company, "name") : company.ToString();
            if (!string.IsNullOrWhiteSpace(name) && seen.Add(name.Trim())) yield return name.Trim();
        }
    }

    private static IEnumerable<string> ObjectNameArray(JsonElement json, string name)
    {
        if (!json.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array) yield break;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in value.EnumerateArray())
        {
            var text = item.ValueKind == JsonValueKind.Object ? GetString(item, "name") : item.ToString();
            if (!string.IsNullOrWhiteSpace(text) && seen.Add(text.Trim())) yield return text.Trim();
        }
    }

    private static string ObjectName(JsonElement json, string name)
    {
        if (!json.TryGetProperty(name, out var value)) return string.Empty;
        return value.ValueKind == JsonValueKind.Object ? GetString(value, "name") ?? string.Empty : value.ToString();
    }

    private static IEnumerable<string> StringArrayValues(JsonElement json, string name)
    {
        if (!json.TryGetProperty(name, out var value)) yield break;
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                var text = item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString();
                if (!string.IsNullOrWhiteSpace(text)) yield return text.Trim();
            }
        }
        else if (value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
        {
            yield return value.GetString()!.Trim();
        }
    }

    private static string ReleaseYear(JsonElement json)
    {
        var seconds = GetLong(json, "first_release_date");
        if (!seconds.HasValue || seconds.Value <= 0) return string.Empty;
        try { return DateTimeOffset.FromUnixTimeSeconds(seconds.Value).Year.ToString(); }
        catch { return string.Empty; }
    }

    private static string CoverUrl(JsonElement json)
    {
        if (!json.TryGetProperty("cover", out var cover) || cover.ValueKind != JsonValueKind.Object) return string.Empty;
        var imageId = GetString(cover, "image_id");
        return string.IsNullOrWhiteSpace(imageId) ? string.Empty : $"https://images.igdb.com/igdb/image/upload/t_cover_big/{Uri.EscapeDataString(imageId)}.jpg";
    }

    private static string Confidence(string query, string title, string[] platforms, string? platformHint, string releaseYear, string? yearHint)
    {
        var queryText = NormalizeText(query);
        var titleText = NormalizeText(title);
        if (!string.IsNullOrWhiteSpace(queryText) && queryText.Equals(titleText, StringComparison.OrdinalIgnoreCase)) return "High";
        if (!string.IsNullOrWhiteSpace(queryText) && (titleText.Contains(queryText, StringComparison.OrdinalIgnoreCase) || queryText.Contains(titleText, StringComparison.OrdinalIgnoreCase))) return "Medium";
        if (PlatformMatchScore(platforms, platformHint) > 0 || (!string.IsNullOrWhiteSpace(yearHint) && releaseYear == ExtractYear(yearHint))) return "Medium";
        return "Low";
    }

    private static int ConfidenceRank(string value) => value.Equals("High", StringComparison.OrdinalIgnoreCase) ? 3 : value.Equals("Medium", StringComparison.OrdinalIgnoreCase) ? 2 : 1;

    private static int PlatformMatchScore(string[] platforms, string? platformHint)
    {
        var hint = NormalizeText(NormalizeGuidevaultPlatformName(platformHint ?? string.Empty));
        if (string.IsNullOrWhiteSpace(hint)) return 0;
        return platforms.Any(p =>
        {
            var normalized = NormalizeText(NormalizeGuidevaultPlatformName(p));
            return normalized.Contains(hint, StringComparison.OrdinalIgnoreCase) || hint.Contains(normalized, StringComparison.OrdinalIgnoreCase);
        }) ? 1 : 0;
    }

    private static IEnumerable<string> NormalizePlatformNames(IEnumerable<string> platforms)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var platform in platforms)
        {
            var normalized = NormalizeGuidevaultPlatformName(platform);
            if (string.IsNullOrWhiteSpace(normalized) || IsMultiPlatformName(normalized)) continue;
            if (seen.Add(normalized)) yield return normalized;
        }
    }

    private static string NormalizeGuidevaultPlatformName(string? value)
    {
        var raw = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        if (IsMultiPlatformName(raw)) return "Multi-Platform";
        return PlatformNameKey(raw) switch
        {
            "pc microsoft windows" => "Windows",
            "microsoft windows" => "Windows",
            "windows pc" => "Windows",
            "pc" => "Windows",
            "dos" => "MS-DOS",
            "ms dos" => "MS-DOS",
            "msdos" => "MS-DOS",
            "pc dos" => "MS-DOS",
            "ibm pc dos" => "MS-DOS",
            "playstation 5" => "Sony Playstation 5",
            "sony playstation 5" => "Sony Playstation 5",
            "ps5" => "Sony Playstation 5",
            "playstation 4" => "Sony Playstation 4",
            "sony playstation 4" => "Sony Playstation 4",
            "ps4" => "Sony Playstation 4",
            "playstation 3" => "Sony Playstation 3",
            "sony playstation 3" => "Sony Playstation 3",
            "ps3" => "Sony Playstation 3",
            "playstation 2" => "Sony Playstation 2",
            "sony playstation 2" => "Sony Playstation 2",
            "ps2" => "Sony Playstation 2",
            "playstation" => "Sony Playstation",
            "sony playstation" => "Sony Playstation",
            "ps1" => "Sony Playstation",
            "psx" => "Sony Playstation",
            "playstation portable" => "Sony PSP",
            "sony playstation portable" => "Sony PSP",
            "psp" => "Sony PSP",
            "dreamcast" => "Sega Dreamcast",
            "sega dreamcast" => "Sega Dreamcast",
            "sega dreamcase" => "Sega Dreamcast",
            "xbox" => "Microsoft Xbox",
            "microsoft xbox" => "Microsoft Xbox",
            _ => raw
        };
    }

    private static string PlatformNameKey(string value) => Regex.Replace(value.ToLowerInvariant().Replace("(", " ").Replace(")", " "), "[^a-z0-9]+", " ").Trim();

    private static bool IsMultiPlatformName(string value) => Regex.IsMatch(value ?? string.Empty, @"^multi[-\s]*platform(?: strategy guides?)?$", RegexOptions.IgnoreCase);

    private static int YearDistance(string? value, int? yearHint)
    {
        var parsed = ParseYear(value);
        return yearHint.HasValue && parsed.HasValue ? Math.Abs(parsed.Value - yearHint.Value) : 0;
    }

    private static int? ParseYear(string? value)
    {
        var y = ExtractYear(value);
        return int.TryParse(y, out var parsed) ? parsed : null;
    }

    private static string ExtractYear(string? value)
    {
        var match = Regex.Match(value ?? string.Empty, "(?<!\\d)(18|19|20)\\d{2}(?!\\d)");
        return match.Success ? match.Value : string.Empty;
    }

    private static string CleanSearchText(string? value)
    {
        var text = Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();
        text = Regex.Replace(text, @"\b(?:manual|strategy guide|official guide|unauthorized guide|forbidden game secrets|prima|bradygames|brady games|versus books|cbz|cbr|pdf|scan|scanned)\b", " ", RegexOptions.IgnoreCase).Trim();
        return Regex.Replace(text, @"\s+", " ").Trim(' ', '-', '_', ':');
    }

    private static string EscapeIgdbString(string value) => (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static void EnsureConfigured(string? clientId, string? clientSecret)
    {
        if (string.IsNullOrWhiteSpace(CleanCredential(clientId)) || string.IsNullOrWhiteSpace(CleanCredential(clientSecret)))
            throw new InvalidOperationException("IGDB credentials are not configured. Add your Twitch Developer Client ID and Client Secret in Settings > Server > General > Metadata Sources.");
    }

    private static string CleanCredential(string? value)
    {
        var text = (value ?? string.Empty).Trim().Trim('"', '\'');
        text = Regex.Replace(text, @"^\s*(?:igdb\s*)?(?:twitch\s*)?(?:client[_\s-]*id|client[_\s-]*secret|secret)\s*[:=]\s*", string.Empty, RegexOptions.IgnoreCase).Trim();
        return text.Trim('"', '\'');
    }

    private static string MaskClientId(string value)
    {
        var text = CleanCredential(value);
        if (text.Length <= 8) return string.IsNullOrWhiteSpace(text) ? string.Empty : "configured";
        return $"{text[..4]}...{text[^4..]}";
    }

    private static void ClearAccessTokenCache()
    {
        _accessToken = string.Empty;
        _accessTokenExpiresAt = DateTimeOffset.MinValue;
        _tokenClientId = string.Empty;
        _tokenSecretFingerprint = string.Empty;
    }

    private static string DescribeTwitchOAuthError(HttpStatusCode statusCode, string responseText)
    {
        var message = string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(responseText ?? string.Empty);
            if (doc.RootElement.TryGetProperty("message", out var msg)) message = msg.GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(message) && doc.RootElement.TryGetProperty("error", out var err)) message = err.GetString() ?? string.Empty;
        }
        catch { }
        message = string.IsNullOrWhiteSpace(message) ? TrimError(responseText) : message.Trim();
        if (string.IsNullOrWhiteSpace(message)) message = "token request was rejected";
        if (statusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return $"Twitch rejected the IGDB credentials: {message}. Re-save the Twitch Developer app Client ID and generated Client Secret in Settings > Server > General > Metadata Sources, then use Test IGDB Credentials. Guidevault does not use the OAuth redirect URL for this lookup.";
        return $"Twitch OAuth token request failed with HTTP {(int)statusCode}: {message}";
    }

    private static string SecretFingerprint(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    private static string TrimError(string? value)
    {
        var text = Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();
        return text.Length > 260 ? text[..260] + "..." : text;
    }

    private static string? GetString(JsonElement json, string name)
    {
        if (json.ValueKind != JsonValueKind.Object || !json.TryGetProperty(name, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static int? GetInt(JsonElement json, string name)
    {
        if (json.ValueKind != JsonValueKind.Object || !json.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed)) return parsed;
        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out parsed)) return parsed;
        return null;
    }

    private static long? GetLong(JsonElement json, string name)
    {
        if (json.ValueKind != JsonValueKind.Object || !json.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var parsed)) return parsed;
        if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out parsed)) return parsed;
        return null;
    }

    private static bool? GetBool(JsonElement json, string name)
    {
        if (json.ValueKind != JsonValueKind.Object || !json.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.True) return true;
        if (value.ValueKind == JsonValueKind.False) return false;
        if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed)) return parsed;
        return null;
    }

    private static string NormalizeText(string value) => Regex.Replace((value ?? string.Empty).ToLowerInvariant(), "[^a-z0-9]+", " ").Trim();
    private static string FirstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;
}



public sealed record EsrbRatingMetadataResult(
    string Id,
    string Title,
    string Publisher,
    string[] Platforms,
    string Rating,
    string RatingShort,
    string[] ContentDescriptors,
    string[] InteractiveElements,
    string RatingSummary,
    string SourceUrl,
    string MatchBy,
    string Confidence);

static class EsrbRatingMetadataClient
{
    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Guidevault", GuidevaultBuildInfo.Version));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        return client;
    }

    public static async Task<IReadOnlyList<EsrbRatingMetadataResult>> SearchAsync(string? q, string? platform, int limit)
    {
        var query = CleanSearchText(q);
        if (string.IsNullOrWhiteSpace(query)) throw new InvalidOperationException("Enter a game title to search ESRB.");
        limit = Math.Clamp(limit, 1, 20);

        var urls = new List<string>();
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var url in await FindRatingUrlsAsync(query, limit))
        {
            if (seenUrls.Add(url)) urls.Add(url);
            if (urls.Count >= limit) break;
        }

        var results = new List<EsrbRatingMetadataResult>();
        foreach (var url in urls.Take(limit))
        {
            try
            {
                var html = await GetTextAsync(url);
                var parsed = FromRatingHtml(html, url, query, platform);
                if (!string.IsNullOrWhiteSpace(parsed.Title) && !string.IsNullOrWhiteSpace(parsed.RatingShort))
                    results.Add(parsed);
            }
            catch { }
        }

        return results
            .OrderByDescending(r => ConfidenceRank(r.Confidence))
            .ThenByDescending(r => PlatformMatchScore(r.Platforms, platform))
            .ThenBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToArray();
    }

    public static async Task<EsrbRatingMetadataResult> ResolveAsync(JsonElement payload)
    {
        var fallback = FromPayload(payload);
        if (string.IsNullOrWhiteSpace(fallback.SourceUrl)) return fallback;
        try
        {
            var html = await GetTextAsync(fallback.SourceUrl);
            var parsed = FromRatingHtml(html, fallback.SourceUrl, fallback.Title, fallback.Platforms.FirstOrDefault());
            return string.IsNullOrWhiteSpace(parsed.RatingShort) ? fallback : parsed;
        }
        catch
        {
            return fallback;
        }
    }

    private static async Task<IReadOnlyList<string>> FindRatingUrlsAsync(string query, int limit)
    {
        var urls = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        async Task AddFromTextUrl(string url)
        {
            try
            {
                var text = await GetTextAsync(url);
                foreach (var ratingUrl in ExtractRatingUrls(text))
                {
                    if (seen.Add(ratingUrl)) urls.Add(ratingUrl);
                    if (urls.Count >= limit) return;
                }
            }
            catch { }
        }

        await AddFromWordPressSearchAsync(query, limit, urls, seen);
        if (urls.Count < limit)
            await AddFromTextUrl($"https://www.esrb.org/?s={Uri.EscapeDataString(query)}");
        if (urls.Count < limit)
            await AddFromTextUrl($"https://www.esrb.org/search/?searchKeyword={Uri.EscapeDataString(query)}");
        if (urls.Count < limit)
            await AddFromTextUrl($"https://www.esrb.org/search/?gameTitle={Uri.EscapeDataString(query)}");
        return urls;
    }

    private static async Task AddFromWordPressSearchAsync(string query, int limit, List<string> urls, HashSet<string> seen)
    {
        var encoded = Uri.EscapeDataString(query);
        var endpoints = new[]
        {
            $"https://www.esrb.org/wp-json/wp/v2/search?search={encoded}&per_page={limit}&subtype=rating",
            $"https://www.esrb.org/wp-json/wp/v2/search?search={encoded}&per_page={limit}&subtype[]=rating",
            $"https://www.esrb.org/wp-json/wp/v2/search?search={encoded}&per_page={limit}",
            $"https://www.esrb.org/wp-json/wp/v2/ratings?search={encoded}&per_page={limit}",
            $"https://www.esrb.org/wp-json/wp/v2/rating?search={encoded}&per_page={limit}"
        };

        foreach (var endpoint in endpoints)
        {
            try
            {
                var json = await GetTextAsync(endpoint);
                foreach (var url in ExtractRatingUrls(json))
                {
                    if (seen.Add(url)) urls.Add(url);
                    if (urls.Count >= limit) return;
                }
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) continue;
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    foreach (var candidate in UrlCandidates(item))
                    {
                        foreach (var url in ExtractRatingUrls(candidate))
                        {
                            if (seen.Add(url)) urls.Add(url);
                            if (urls.Count >= limit) return;
                        }
                    }
                }
            }
            catch { }
            if (urls.Count >= limit) return;
        }
    }

    private static IEnumerable<string> UrlCandidates(JsonElement item)
    {
        foreach (var key in new[] { "url", "link", "guid", "permalink" })
        {
            if (!item.TryGetProperty(key, out var value)) continue;
            if (value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
                yield return value.GetString()!;
            else if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("rendered", out var rendered) && rendered.ValueKind == JsonValueKind.String)
                yield return rendered.GetString() ?? string.Empty;
        }
        if (item.TryGetProperty("_links", out var links) && links.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in links.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.Array) continue;
                foreach (var linkItem in prop.Value.EnumerateArray())
                {
                    if (linkItem.ValueKind == JsonValueKind.Object && linkItem.TryGetProperty("href", out var href) && href.ValueKind == JsonValueKind.String)
                        yield return href.GetString() ?? string.Empty;
                }
            }
        }
    }

    private static IEnumerable<string> ExtractRatingUrls(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;
        var decoded = WebUtility.HtmlDecode(text).Replace("\\/", "/");
        foreach (Match match in Regex.Matches(decoded, @"https?://(?:www\.)?esrb\.org/ratings/\d+/[a-z0-9\-]+/?", RegexOptions.IgnoreCase))
            yield return NormalizeRatingUrl(match.Value);
        foreach (Match match in Regex.Matches(decoded, @"/ratings/\d+/[a-z0-9\-]+/?", RegexOptions.IgnoreCase))
            yield return NormalizeRatingUrl("https://www.esrb.org" + match.Value);
    }

    private static EsrbRatingMetadataResult FromRatingHtml(string html, string sourceUrl, string query, string? platformHint)
    {
        var text = HtmlToText(html);
        var lines = CleanLines(text).ToArray();
        var title = Clean(FirstRegex(html, @"<h1[^>]*>(.*?)</h1>") ?? string.Empty);
        if (string.IsNullOrWhiteSpace(title)) title = TitleFromUrl(sourceUrl);
        var id = FirstRegex(sourceUrl, @"/ratings/(\d+)/") ?? string.Empty;
        var publisher = ExtractPublisher(lines, title);
        var platforms = ExtractListAfter(lines, "Platforms", new[] { "Image", "Rating Summary", "Assigned Rating Info", "Explore More Games" });

        var summary = ExtractSummary(text);
        var ratingText = FirstNonEmpty(summary, text);
        var ratingShort = string.Empty;
        var rating = string.Empty;
        var ratingMatch = Regex.Match(ratingText, @"\bis\s+rated\s+([A-Z0-9+]+)\s+for\s+([^\.]+?)\s+by\s+the\s+ESRB", RegexOptions.IgnoreCase);
        if (ratingMatch.Success)
        {
            ratingShort = NormalizeRatingShort(ratingMatch.Groups[1].Value);
            rating = NormalizeRatingLabel(ratingMatch.Groups[2].Value);
        }
        if (string.IsNullOrWhiteSpace(ratingShort))
        {
            ratingShort = RatingShortFromText(text);
            rating = RatingLabelFromShort(ratingShort);
        }

        var descriptors = ExtractContentDescriptors(summary, text);
        var interactive = ExtractInteractiveElements(summary, lines);
        var confidence = Confidence(query, title, platforms, platformHint);
        return new EsrbRatingMetadataResult(
            Id: id,
            Title: title,
            Publisher: publisher,
            Platforms: platforms,
            Rating: rating,
            RatingShort: ratingShort,
            ContentDescriptors: descriptors,
            InteractiveElements: interactive,
            RatingSummary: summary,
            SourceUrl: NormalizeRatingUrl(sourceUrl),
            MatchBy: "Game title",
            Confidence: confidence);
    }

    private static string[] ExtractContentDescriptors(string summary, string text)
    {
        var source = FirstNonEmpty(summary, text);
        var match = Regex.Match(source, @"\bby\s+the\s+ESRB\s+with\s+(.+?)(?:\.\s|\.?$|\s+Also\s+includes\b)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!match.Success) return [];
        var raw = Regex.Replace(match.Groups[1].Value, @"\s+and\s+", ", ", RegexOptions.IgnoreCase);
        return SplitList(raw);
    }

    private static string[] ExtractInteractiveElements(string summary, string[] lines)
    {
        var match = Regex.Match(summary ?? string.Empty, @"\bAlso\s+includes\s+(.+?)(?:\.\s|\.?$)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (match.Success) return SplitList(Regex.Replace(match.Groups[1].Value, @"\s+and\s+", ", ", RegexOptions.IgnoreCase));
        var start = Array.FindIndex(lines, l => l.Equals("No Interactive Elements", StringComparison.OrdinalIgnoreCase) || l.Equals("Users Interact", StringComparison.OrdinalIgnoreCase) || l.StartsWith("In-Game Purchases", StringComparison.OrdinalIgnoreCase) || l.Contains("Online Interactions", StringComparison.OrdinalIgnoreCase));
        if (start < 0) return [];
        var values = new List<string>();
        for (var i = start; i < lines.Length && values.Count < 4; i++)
        {
            var line = lines[i].Trim(' ', '\u2022', '-', '*');
            if (line.Equals("Rating Summary", StringComparison.OrdinalIgnoreCase) || line.Equals("Assigned Rating Info", StringComparison.OrdinalIgnoreCase) || line.Equals("Explore More Games", StringComparison.OrdinalIgnoreCase)) break;
            if (line.Equals("No Interactive Elements", StringComparison.OrdinalIgnoreCase)) return ["No Interactive Elements"];
            if (line.Equals("Users Interact", StringComparison.OrdinalIgnoreCase) || line.StartsWith("In-Game Purchases", StringComparison.OrdinalIgnoreCase) || line.Contains("Online Interactions", StringComparison.OrdinalIgnoreCase))
                values.Add(line);
        }
        return CleanDistinct(values);
    }

    private static string ExtractSummary(string text)
    {
        var match = Regex.Match(text ?? string.Empty, @"(?:Rating Summary|Assigned Rating Info)\s+(.+?)(?:\s+Explore More Games\b|\s+Additional Resources\b|\s+Share\b|$)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!match.Success) return string.Empty;
        return Clean(match.Groups[1].Value);
    }

    private static string ExtractPublisher(string[] lines, string title)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            if (!lines[i].Equals(title, StringComparison.OrdinalIgnoreCase)) continue;
            for (var j = i + 1; j < Math.Min(lines.Length, i + 8); j++)
            {
                var line = lines[j];
                if (string.IsNullOrWhiteSpace(line) || line.Equals("Platforms", StringComparison.OrdinalIgnoreCase) || line.Equals("Image", StringComparison.OrdinalIgnoreCase)) continue;
                if (line.StartsWith("Advanced Search", StringComparison.OrdinalIgnoreCase)) continue;
                return line;
            }
        }
        return string.Empty;
    }

    private static string[] ExtractListAfter(string[] lines, string marker, string[] endMarkers)
    {
        var index = Array.FindIndex(lines, l => l.Equals(marker, StringComparison.OrdinalIgnoreCase));
        if (index < 0 || index + 1 >= lines.Length) return [];
        var values = new List<string>();
        for (var i = index + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (endMarkers.Any(m => line.Equals(m, StringComparison.OrdinalIgnoreCase))) break;
            if (Regex.IsMatch(line, @"\bis\s+rated\b", RegexOptions.IgnoreCase)) break;
            values.AddRange(SplitList(line));
            if (values.Count > 16) break;
        }
        return CleanDistinct(values);
    }

    private static EsrbRatingMetadataResult FromPayload(JsonElement payload)
    {
        return new EsrbRatingMetadataResult(
            Id: GetString(payload, "id") ?? string.Empty,
            Title: FirstNonEmpty(GetString(payload, "title"), GetString(payload, "gameTitle"), GetString(payload, "name")),
            Publisher: GetString(payload, "publisher") ?? string.Empty,
            Platforms: StringArrayValues(payload, "platforms").ToArray(),
            Rating: GetString(payload, "rating") ?? string.Empty,
            RatingShort: GetString(payload, "ratingShort") ?? string.Empty,
            ContentDescriptors: StringArrayValues(payload, "contentDescriptors").ToArray(),
            InteractiveElements: StringArrayValues(payload, "interactiveElements").ToArray(),
            RatingSummary: GetString(payload, "ratingSummary") ?? string.Empty,
            SourceUrl: GetString(payload, "sourceUrl") ?? string.Empty,
            MatchBy: GetString(payload, "matchBy") ?? "Game title",
            Confidence: GetString(payload, "confidence") ?? "Low");
    }

    private static async Task<string> GetTextAsync(string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Referrer = new Uri("https://www.esrb.org/search/");
        using var response = await Http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"ESRB returned HTTP {(int)response.StatusCode}: {TrimError(text)}");
        return text;
    }

    private static string HtmlToText(string html)
    {
        var value = Regex.Replace(html ?? string.Empty, @"<script[\s\S]*?</script>|<style[\s\S]*?</style>", " ", RegexOptions.IgnoreCase);
        value = Regex.Replace(value, @"<(br|/p|/div|/h[1-6]|/li|/section|/article)\b[^>]*>", "\n", RegexOptions.IgnoreCase);
        value = Regex.Replace(value, @"<[^>]+>", " ");
        value = WebUtility.HtmlDecode(value);
        value = Regex.Replace(value, @"[ \t\r\f\v]+", " ");
        value = Regex.Replace(value, @"\n\s*\n+", "\n");
        return value.Trim();
    }

    private static IEnumerable<string> CleanLines(string text)
    {
        foreach (var raw in (text ?? string.Empty).Split('\n'))
        {
            var line = Clean(raw);
            if (!string.IsNullOrWhiteSpace(line)) yield return line;
        }
    }

    private static string[] SplitList(string value)
    {
        return CleanDistinct(Regex.Split(value ?? string.Empty, @",|;|\||\s+/\s+")
            .Select(v => Regex.Replace(Clean(v.Trim(' ', '.', '\u2022', '-', '*')), @"^(?:and|or)\s+", string.Empty, RegexOptions.IgnoreCase).Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v)));
    }

    private static string[] CleanDistinct(IEnumerable<string> values)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>();
        foreach (var value in values)
        {
            var clean = Clean(value);
            if (string.IsNullOrWhiteSpace(clean)) continue;
            if (seen.Add(clean)) list.Add(clean);
        }
        return list.ToArray();
    }

    private static string RatingShortFromText(string text)
    {
        var compact = NormalizeText(text);
        if (compact.Contains("adults only 18") || Regex.IsMatch(text ?? string.Empty, @"\bAO\b")) return "AO";
        if (compact.Contains("mature 17") || Regex.IsMatch(text ?? string.Empty, @"\bM\b")) return "M";
        if (compact.Contains("teen") || Regex.IsMatch(text ?? string.Empty, @"\bT\b")) return "T";
        if (compact.Contains("everyone 10") || Regex.IsMatch(text ?? string.Empty, @"\bE10\+?\b")) return "E10+";
        if (compact.Contains("everyone") || Regex.IsMatch(text ?? string.Empty, @"\bE\b")) return "E";
        if (compact.Contains("rating pending") || Regex.IsMatch(text ?? string.Empty, @"\bRP\b")) return "RP";
        return string.Empty;
    }

    private static string NormalizeRatingShort(string value)
    {
        var compact = Regex.Replace(value ?? string.Empty, @"[^A-Za-z0-9+]+", string.Empty).ToUpperInvariant();
        return compact switch
        {
            "E10" or "E10+" => "E10+",
            "E" => "E",
            "T" => "T",
            "M" => "M",
            "AO" => "AO",
            "RP" => "RP",
            "EC" => "EC",
            "KA" => "KA",
            _ => compact
        };
    }

    private static string NormalizeRatingLabel(string value)
    {
        var text = Clean(value);
        text = Regex.Replace(text, @"\s+with\s+.+$", string.Empty, RegexOptions.IgnoreCase).Trim();
        return text.Trim(' ', '.', ',');
    }

    private static string RatingLabelFromShort(string value) => NormalizeRatingShort(value) switch
    {
        "E" => "Everyone",
        "E10+" => "Everyone 10+",
        "T" => "Teen",
        "M" => "Mature 17+",
        "AO" => "Adults Only 18+",
        "RP" => "Rating Pending",
        "EC" => "Early Childhood",
        "KA" => "Kids to Adults",
        _ => string.Empty
    };

    private static string Confidence(string query, string title, string[] platforms, string? platformHint)
    {
        var q = NormalizeText(query);
        var t = NormalizeText(title);
        if (!string.IsNullOrWhiteSpace(q) && q.Equals(t, StringComparison.OrdinalIgnoreCase)) return "High";
        if (!string.IsNullOrWhiteSpace(q) && (t.Contains(q, StringComparison.OrdinalIgnoreCase) || q.Contains(t, StringComparison.OrdinalIgnoreCase))) return "Medium";
        if (PlatformMatchScore(platforms, platformHint) > 0) return "Medium";
        return "Low";
    }

    private static int ConfidenceRank(string value) => value.Equals("High", StringComparison.OrdinalIgnoreCase) ? 3 : value.Equals("Medium", StringComparison.OrdinalIgnoreCase) ? 2 : 1;

    private static int PlatformMatchScore(string[] platforms, string? platformHint)
    {
        var hint = NormalizeText(platformHint ?? string.Empty);
        if (string.IsNullOrWhiteSpace(hint)) return 0;
        return platforms.Any(platform =>
        {
            var normalized = NormalizeText(platform);
            return normalized.Contains(hint, StringComparison.OrdinalIgnoreCase) || hint.Contains(normalized, StringComparison.OrdinalIgnoreCase);
        }) ? 1 : 0;
    }

    private static string CleanSearchText(string? value)
    {
        var text = Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();
        text = Regex.Replace(text, @"\b(?:manual|strategy guide|official guide|unauthorized guide|forbidden game secrets|prima|bradygames|brady games|versus books|complete|guide|cbz|cbr|pdf|scan|scanned)\b", " ", RegexOptions.IgnoreCase).Trim();
        return Regex.Replace(text, @"\s+", " ").Trim(' ', '-', '_', ':');
    }

    private static string FirstRegex(string value, string pattern)
    {
        var match = Regex.Match(value ?? string.Empty, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? WebUtility.HtmlDecode(Regex.Replace(match.Groups[1].Value, "<[^>]+>", " ")).Trim() : string.Empty;
    }

    private static string TitleFromUrl(string sourceUrl)
    {
        var match = Regex.Match(sourceUrl ?? string.Empty, @"/ratings/\d+/([^/]+)/?", RegexOptions.IgnoreCase);
        if (!match.Success) return string.Empty;
        return string.Join(' ', match.Groups[1].Value.Split('-', StringSplitOptions.RemoveEmptyEntries).Select(w => char.ToUpperInvariant(w[0]) + (w.Length > 1 ? w[1..] : string.Empty)));
    }

    private static string NormalizeRatingUrl(string url)
    {
        var value = (url ?? string.Empty).Trim().Trim('"', '\'', ')', ']', '}');
        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) value = "https://" + value[7..];
        return value.TrimEnd('/') + "/";
    }

    private static string Clean(string value)
    {
        var text = WebUtility.HtmlDecode(value ?? string.Empty);
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return text;
    }

    private static string TrimError(string value)
    {
        var text = Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();
        return text.Length > 260 ? text[..260] + "..." : text;
    }

    private static string NormalizeText(string value) => Regex.Replace((value ?? string.Empty).ToLowerInvariant(), "[^a-z0-9+]+", " ").Trim();
    private static string FirstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;

    private static string? GetString(JsonElement json, string name)
    {
        if (json.ValueKind != JsonValueKind.Object || !json.TryGetProperty(name, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static IEnumerable<string> StringArrayValues(JsonElement json, string name)
    {
        if (json.ValueKind != JsonValueKind.Object || !json.TryGetProperty(name, out var value)) yield break;
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                var text = item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString();
                if (!string.IsNullOrWhiteSpace(text)) yield return text.Trim();
            }
        }
        else if (value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
        {
            yield return value.GetString()!.Trim();
        }
    }
}

static class GuidevaultLibraryIoGate
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static bool IsBusy => Gate.CurrentCount == 0;

    public static string BusyMessage =>
        "A library scan, cleanup, metadata enrichment, metadata export, or file rename is already working with the library files. Wait for that task to finish before exporting metadata or renaming files. Running these at the same time can make large or mounted libraries extremely slow.";

    public static async Task<IDisposable> BeginLibraryScanAsync(CancellationToken cancellationToken = default)
    {
        await Gate.WaitAsync(cancellationToken);
        return new Releaser();
    }

    public static bool TryBeginArchiveWrite(out IDisposable? lease, out string message)
    {
        if (Gate.Wait(0))
        {
            lease = new Releaser();
            message = string.Empty;
            return true;
        }

        lease = null;
        message = BusyMessage;
        return false;
    }

    private sealed class Releaser : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
                Gate.Release();
        }
    }
}

static class GuidevaultBuildInfo
{
    public const string Version = "0.9.217";
}

