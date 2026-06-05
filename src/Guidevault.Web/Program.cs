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

var builder = WebApplication.CreateBuilder(args);
const string GuidevaultVersion = "0.9.83";
var app = builder.Build();
var metadataJsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
var options = app.Configuration.GetSection("Guidevault").Get<GuidevaultOptions>() ?? new GuidevaultOptions();
var contentRoot = app.Environment.ContentRootPath;
var configPath = Path.Combine(contentRoot, "data", "config", "library.settings.json");
var metadataPath = Path.Combine(contentRoot, "data", "config", "metadata.overrides.json");
var fileIdentityPath = Path.Combine(contentRoot, "data", "config", "file.identity-map.json");
var opdsSettingsPath = Path.Combine(contentRoot, "data", "config", "opds.settings.json");
var serverSettingsPath = Path.Combine(contentRoot, "data", "config", "server.settings.json");
var emailSettingsPath = Path.Combine(contentRoot, "data", "config", "email.settings.json");
var emailHistoryPath = Path.Combine(contentRoot, "data", "config", "email.history.json");
var usersSettingsPath = Path.Combine(contentRoot, "data", "config", "users.settings.json");
var taskSettingsPath = Path.Combine(contentRoot, "data", "config", "task.settings.json");
var customizeSettingsPath = Path.Combine(contentRoot, "data", "config", "customize.settings.json");
var deviceHistoryPath = Path.Combine(contentRoot, "data", "config", "device.history.json");
var systemInfoPath = Path.Combine(contentRoot, "data", "config", "system.info.json");
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
var fileIdentityStore = new FileIdentityStore(fileIdentityPath);
var opdsStore = new OpdsSettingsStore(opdsSettingsPath);
var serverSettingsStore = new GuidevaultServerSettingsStore(serverSettingsPath, contentRoot);
var emailSettingsStore = new GuidevaultEmailSettingsStore(emailSettingsPath);
var emailHistoryStore = new GuidevaultEmailHistoryStore(emailHistoryPath);
var usersStore = new GuidevaultUsersStore(usersSettingsPath);
var taskSettingsStore = new GuidevaultTaskSettingsStore(taskSettingsPath);
var customizeSettingsStore = new GuidevaultCustomizeSettingsStore(customizeSettingsPath);
var deviceStore = new DeviceHistoryStore(deviceHistoryPath);
var systemInfoStore = new SystemInfoStore(systemInfoPath, GuidevaultVersion);
var indexCachePath = Path.Combine(contentRoot, "data", "cache", "library-index.json");
var coverCachePath = Path.Combine(contentRoot, "data", "cache", "covers");
ArchiveReader.ConfigureCoverCache(coverCachePath);
var taskMonitor = new TaskMonitor();
var cache = new LibraryCache(loadedSettings.Libraries, metadataStore, fileIdentityStore, indexCachePath, taskMonitor);
var updateChecker = new StableUpdateChecker(options.Updates, GuidevaultVersion);
// Do not create configured user library folders here. Guidevault scans existing folders in place.
// Creating missing folders can hide typo/path mistakes and make libraries appear empty.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new
{
    app = "Guidevault",
    version = GuidevaultVersion,
    libraryPaths = cache.LibraryPaths,
    dockerReady = true
}));

app.MapGet("/api/devices", () => Results.Ok(deviceStore.GetSnapshot()));

app.MapGet("/api/system/info", () => Results.Ok(systemInfoStore.GetSnapshot()));

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
    return Results.Ok(saved);
});

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
    return Results.Ok(new { device, snapshot = deviceStore.GetSnapshot() });
});

app.MapPost("/api/devices/email", (EmailDeviceUpsert payload) =>
{
    if (payload is null || string.IsNullOrWhiteSpace(payload.Name) || string.IsNullOrWhiteSpace(payload.Email))
        return Results.BadRequest(new { error = "Name and email are required." });
    return Results.Ok(deviceStore.UpsertEmailDevice(payload));
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

app.MapGet("/api/tasks", () => Results.Ok(new { tasks = taskMonitor.RecentTasks() }));
app.MapPost("/api/tasks/clear", () => Results.Ok(new { cleared = taskMonitor.ClearNonRunning(), tasks = taskMonitor.RecentTasks() }));


app.MapGet("/api/reader/backgrounds", () =>
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
            return new
            {
                name = fileName,
                displayName,
                url = $"{backgroundUrlBase}/{Uri.EscapeDataString(fileName)}"
            };
        })
        .OrderBy(bg => bg.displayName, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    var defaultName = backgrounds.FirstOrDefault(bg => bg.name.Equals("librarydesk.png", StringComparison.OrdinalIgnoreCase))?.name
        ?? backgrounds.FirstOrDefault()?.name
        ?? string.Empty;

    return Results.Ok(new
    {
        folder = activeBackgroundsPath,
        defaultName,
        backgrounds
    });
});


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


app.MapGet("/api/openlibrary/search", async (string? q, string? secondary, string? isbn, string? title, string? gameTitle, string? publisher, string? year, int? limit) =>
{
    try
    {
        var results = await OpenLibraryMetadataClient.SearchAsync(q, secondary, isbn, title, gameTitle, publisher, year, limit ?? 16);
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
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = $"Open Library metadata lookup failed: {ex.Message}" });
    }
});

app.MapGet("/api/igdb/search", async (string? q, string? platform, string? year, int? limit) =>
{
    try
    {
        var settings = serverSettingsStore.GetSnapshot();
        var results = await IgdbGameMetadataClient.SearchAsync(q, platform, year, limit ?? 16, settings.IgdbClientId, settings.IgdbClientSecret);
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
    metadataStore.MergeOverride(id, update);

    // If the item is already loaded, update the active/persisted cache immediately
    // and return the full updated item. If it is not loaded, still return success;
    // the saved override will be applied the next time the item is indexed/loaded.
    var cached = cache.TryGetCachedItem(id);
    if (cached is not null)
    {
        var updated = metadataStore.ApplyOverride(cached);
        updated = MetadataStore.ApplyUpdateSnapshot(updated, update);
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

app.MapPost("/api/items/{id}/metadata/native-export", async (string id, JsonElement payload) =>
{
    if (string.IsNullOrWhiteSpace(id))
        return Results.BadRequest(new { error = "Item id is required." });

    var update = ItemMetadataJsonReader.Read(payload);
    metadataStore.MergeOverride(id, update);

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
        updated = MetadataStore.ApplyUpdateSnapshot(updated, update);
        cache.ReplaceCachedItem(updated, persist: false);

        var exportDocument = GuidevaultNativeMetadata.BuildExport(updated, update, payload);
        var exportJson = JsonSerializer.Serialize(exportDocument, GuidevaultNativeMetadata.JsonOptions);
        var writeResult = await ArchiveReader.WriteGuidevaultMetadataAsync(updated.Path, updated.Kind, exportJson);
        if (!writeResult.Success)
            return Results.BadRequest(new { error = writeResult.Message });

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

        metadataStore.MergeOverride(id, normalizedUpdate);
        var updated = metadataStore.ApplyOverride(cached);
        updated = MetadataStore.ApplyUpdateSnapshot(updated, normalizedUpdate);
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
    metadataStore.MergeOverride(id, update);

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
    updated = MetadataStore.ApplyUpdateSnapshot(updated, update);

    var suggestedFileName = GuidevaultNativeMetadata.BuildSuggestedFileName(updated, update, payload);
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

    metadataStore.MergeOverride(id, new ItemMetadataUpdate(
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
        PlatformResolverConfidence: resolution.Confidence));

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
    metadataStore.RemoveOverride(id);
    cache.RemoveCachedItem(id, persist: false);
    return Results.Ok(new { removedId = id, mode = "removed-from-index", rescanWillRediscover = true, pathPreserved = true });
});

app.MapGet("/api/items/{id}/cover", async (string id, HttpResponse response) =>
{
    var item = cache.TryGetCachedItem(id) ?? (await cache.GetItemsAsync()).FirstOrDefault(i => i.Id == id);
    if (item is null) return Results.NotFound();
    response.Headers.CacheControl = "public, max-age=86400";
    if (item.Format == "PDF") return Results.Redirect("/assets/pdf-cover.svg");

    var image = await ArchiveReader.GetCoverImageAsync(item.Path);
    if (image is null) return Results.Redirect("/assets/missing-cover.svg");

    return Results.File(
        image.Value.Bytes,
        image.Value.ContentType,
        fileDownloadName: null,
        lastModified: item.Modified,
        entityTag: new Microsoft.Net.Http.Headers.EntityTagHeaderValue($"\"{item.Id}-{item.Modified.ToUnixTimeSeconds()}\""),
        enableRangeProcessing: true);
});

app.MapGet("/api/items/{id}/pages", async (string id) =>
{
    var item = (await cache.GetItemsAsync()).FirstOrDefault(i => i.Id == id);
    if (item is null) return Results.NotFound();

    if (item.Format == "PDF")
    {
        return Results.Ok(new
        {
            item.Id,
            item.PageCount,
            format = item.Format,
            pdfUrl = $"/api/items/{item.Id}/file"
        });
    }

    var pageCount = await ArchiveReader.GetImagePageCountAsync(item.Path);
    return Results.Ok(new
    {
        item.Id,
        pageCount,
        format = item.Format,
        pages = Enumerable.Range(0, pageCount).Select(i => new
        {
            index = i,
            imageUrl = $"/api/items/{item.Id}/page/{i}"
        })
    });
});

app.MapGet("/api/items/{id}/page/{page:int}", async (string id, int page, HttpResponse response) =>
{
    var item = (await cache.GetItemsAsync()).FirstOrDefault(i => i.Id == id);
    if (item is null) return Results.NotFound();
    response.Headers.CacheControl = "private, max-age=3600";
    if (item.Format == "PDF") return Results.BadRequest(new { error = "PDF pages are handled through the browser PDF viewer in this prototype." });

    var image = await ArchiveReader.GetImagePageAsync(item.Path, page);
    return image is null
        ? Results.NotFound()
        : Results.File(image.Value.Bytes, image.Value.ContentType, enableRangeProcessing: true);
});

app.MapGet("/api/items/{id}/file", async (string id) =>
{
    var item = (await cache.GetItemsAsync()).FirstOrDefault(i => i.Id == id);
    return item is null ? Results.NotFound() : Results.File(item.Path, contentType: item.ContentType, enableRangeProcessing: true);
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

app.MapGet("/opds", async (HttpRequest request) =>
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
});

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
    var items = (await cache.GetItemsAsync()).OrderByDescending(i => i.Added).ThenBy(i => DisplayItemTitle(i), StringComparer.OrdinalIgnoreCase).Take(100).ToArray();
    return OpdsXml(OpdsAcquisitionCatalog(request, auth.Secret, "Guidevault - Recently Added", "The most recently indexed Guidevault items.", items));
});

app.MapGet("/opds/kind/{kind}", async (HttpRequest request, string kind) =>
{
    var auth = AuthorizeOpdsRequest(request, opdsStore, deviceStore);
    if (!auth.Success) return auth.Response!;
    var normalizedKind = Uri.UnescapeDataString(kind ?? string.Empty);
    var items = (await cache.GetItemsAsync())
        .Where(i => KindEquals(i, normalizedKind))
        .OrderBy(i => DisplayItemTitle(i), StringComparer.OrdinalIgnoreCase)
        .ToArray();
    return OpdsXml(OpdsAcquisitionCatalog(request, auth.Secret, $"Guidevault - {normalizedKind}", $"Items marked as {normalizedKind}.", items));
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
        .Take(200)
        .ToArray();
    return OpdsXml(OpdsAcquisitionCatalog(request, auth.Secret, $"Guidevault Search - {query}", string.IsNullOrWhiteSpace(query) ? "Search Guidevault." : $"Search results for {query}.", items));
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

    var image = await ArchiveReader.GetCoverImageAsync(item.Path);
    return image is null
        ? Results.Redirect("/assets/missing-cover.svg")
        : Results.File(image.Value.Bytes, image.Value.ContentType, enableRangeProcessing: true);
});

app.MapFallbackToFile("index.html");
app.Run();


static string DefaultOpdsConnectionUrl(HttpRequest request) => BuildAbsoluteUrl(request, "/opds");

static string BuildAbsoluteUrl(HttpRequest request, string pathAndQuery)
{
    var path = pathAndQuery.StartsWith("/", StringComparison.Ordinal) ? pathAndQuery : "/" + pathAndQuery;
    var pathBase = request.PathBase.HasValue ? request.PathBase.Value : string.Empty;
    return $"{request.Scheme}://{request.Host}{pathBase}{path}";
}

static string AppendOpdsAuth(HttpRequest request, string path, string secret)
{
    var joiner = path.Contains('?', StringComparison.Ordinal) ? "&" : "?";
    return BuildAbsoluteUrl(request, $"{path}{joiner}auth={Uri.EscapeDataString(secret)}");
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
    var itemArray = items.ToArray();
    var updated = itemArray.Select(i => i.Modified).DefaultIfEmpty(DateTimeOffset.UtcNow).Max().ToString("O");

    return new XDocument(
        new XElement(atom + "feed",
            new XAttribute(XNamespace.Xmlns + "atom", atom.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "dc", dc.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "opds", opds.NamespaceName),
            new XElement(atom + "id", $"urn:guidevault:opds:{request.Path.Value}"),
            new XElement(atom + "title", title),
            new XElement(atom + "updated", updated),
            new XElement(atom + "author", new XElement(atom + "name", "Guidevault")),
            new XElement(atom + "subtitle", subtitle),
            new XElement(atom + "link", new XAttribute("rel", "self"), new XAttribute("type", "application/atom+xml;profile=opds-catalog;kind=acquisition"), new XAttribute("href", AppendOpdsAuth(request, request.Path.Value ?? "/opds/all", secret))),
            new XElement(atom + "link", new XAttribute("rel", "start"), new XAttribute("type", "application/atom+xml;profile=opds-catalog;kind=navigation"), new XAttribute("href", AppendOpdsAuth(request, "/opds", secret))),
            itemArray.Select(item => OpdsItemEntry(request, secret, item))));
}

static XElement OpdsItemEntry(HttpRequest request, string secret, LibraryItem item)
{
    XNamespace atom = "http://www.w3.org/2005/Atom";
    XNamespace dc = "http://purl.org/dc/terms/";
    var title = DisplayItemTitle(item);
    var summary = string.IsNullOrWhiteSpace(item.Summary) ? OpdsItemDescription(item) : item.Summary.Trim();
    var downloadHref = AppendOpdsAuth(request, $"/opds/items/{Uri.EscapeDataString(item.Id)}/download", secret);
    var coverHref = AppendOpdsAuth(request, $"/opds/items/{Uri.EscapeDataString(item.Id)}/cover", secret);
    var contentType = string.IsNullOrWhiteSpace(item.ContentType) ? "application/octet-stream" : item.ContentType;

    return new XElement(atom + "entry",
        new XElement(atom + "title", title),
        new XElement(atom + "id", $"urn:guidevault:item:{item.Id}"),
        new XElement(atom + "updated", item.Modified.ToString("O")),
        new XElement(atom + "author", new XElement(atom + "name", string.IsNullOrWhiteSpace(item.Publisher) ? "Guidevault" : item.Publisher)),
        new XElement(atom + "summary", summary),
        new XElement(atom + "content", new XAttribute("type", "text"), OpdsItemDescription(item)),
        string.IsNullOrWhiteSpace(item.Year) ? null : new XElement(dc + "issued", item.Year),
        string.IsNullOrWhiteSpace(item.Publisher) ? null : new XElement(dc + "publisher", item.Publisher),
        string.IsNullOrWhiteSpace(item.LanguageTag) ? null : new XElement(dc + "language", item.LanguageTag),
        new XElement(atom + "category", new XAttribute("term", item.Kind), new XAttribute("label", item.Kind)),
        new XElement(atom + "link", new XAttribute("rel", "http://opds-spec.org/acquisition"), new XAttribute("type", contentType), new XAttribute("href", downloadHref)),
        new XElement(atom + "link", new XAttribute("rel", "http://opds-spec.org/image"), new XAttribute("type", "image/*"), new XAttribute("href", coverHref)),
        new XElement(atom + "link", new XAttribute("rel", "http://opds-spec.org/image/thumbnail"), new XAttribute("type", "image/*"), new XAttribute("href", coverHref)));
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
        if (string.IsNullOrWhiteSpace(text) || text == "—" || IsMultiPlatformBucket(text)) return;
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

static string DisplayItemTitle(LibraryItem item)
{
    if (string.Equals(item.Kind, "Magazine", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(item.Series) && !string.IsNullOrWhiteSpace(item.IssueNumber))
        return $"{item.Series.Trim()} #{item.IssueNumber.Trim()}";
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
    return string.Join(" • ", parts);
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
        IgdbClientSecret = value.IgdbClientSecret
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
}

public sealed record GuidevaultBackupResult(string FileName, string Path, long SizeBytes, DateTimeOffset CreatedAt, string BackupDirectory);

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
    string BarcodeUpcIssn = "");


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
    string? Notes = null,
    bool? Removed = null);

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

        var title = FirstText(GetString(payload, "title"), GetString(payload, "strategyGuideTitle"), GetString(payload, "manualTitle"), GetString(payload, "magazineTitle"));
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
            GamePublisher: FirstText(GetString(payload, "gamePublisher"), GetString(payload, "publisher")),
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
            Notes: GetString(payload, "notes"),
            Removed: GetBool(payload, "removed"));
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
                Notes = First(o.Notes, item.Notes)
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
            Notes = Keep(update.Notes, item.Notes)
        };
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
        lock (_gate)
        {
            _overrides.TryGetValue(id, out var existing);
            _overrides[id] = new ItemMetadataUpdate(
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
                Notes: update.Notes ?? existing?.Notes,
                Removed: update.Removed ?? existing?.Removed);
            Persist();
        }
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
            var validRoots = new HashSet<string>(LibraryPaths.Select(p => Path.GetFullPath(p)), StringComparer.OrdinalIgnoreCase);
            var stillRelevant = cached
                .Where(item =>
                {
                    if (string.IsNullOrWhiteSpace(item.Path)) return false;
                    string itemPath;
                    try { itemPath = Path.GetFullPath(item.Path); }
                    catch { return false; }
                    return validRoots.Count == 0 || validRoots.Any(root => itemPath.StartsWith(root, StringComparison.OrdinalIgnoreCase));
                })
                .Select(_metadataStore.ApplyOverride)
                .GroupBy(i => NormalizeFilePathKey(i.Path), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(i => LibraryTypeSpecificity(i.LibraryType)).ThenByDescending(i => i.Modified).First())
                .GroupBy(i => i.Id, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
            return stillRelevant.Count > 0 ? stillRelevant : null;
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

    private static bool CachedItemMatchesFile(LibraryItem item, FileInfo info)
    {
        try
        {
            if (!string.Equals(Path.GetFullPath(item.Path), Path.GetFullPath(info.FullName), StringComparison.OrdinalIgnoreCase)) return false;
            if (item.SizeBytes != info.Length) return false;
            var cachedModified = item.Modified.UtcDateTime;
            return Math.Abs((cachedModified - info.LastWriteTimeUtc).TotalSeconds) < 2;
        }
        catch
        {
            return false;
        }
    }

    private static LibraryItem RefreshCachedItemForLibrary(LibraryItem cached, FileInfo info, string relativePath, LibraryDefinition library)
        => cached with
        {
            Path = info.FullName,
            RelativePath = relativePath,
            FileName = info.Name,
            SizeBytes = info.Length,
            Added = info.CreationTimeUtc,
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
                var id = _identityStore.GetItemId(candidate.File) ?? StableId(candidate.File);
                if (_metadataStore.IsRemoved(id)) return;

                var info = new FileInfo(candidate.File);
                if (!info.Exists) return;
                var ext = info.Extension.ToLowerInvariant();
                var format = ext.TrimStart('.').ToUpperInvariant();
                var relativePath = Path.GetRelativePath(candidate.Root, candidate.File);

                var hasMatchingCache = previousItems.TryGetValue(id, out var cached) && CachedItemMatchesFile(cached, info);
                if (hasMatchingCache && cached is not null)
                {
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
                    MetadataSource: merged.MetadataSource);
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
            GameTitle: strategyGameTitle,
            GuideType: strategyGuideType,
            Franchise: strategyGameTitle,
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
            IncludedExtras = inferred.Kind == "Magazine" || inferred.Kind == "Strategy Guide" ? includedExtras : [],
            GameTitle = inferred.Kind == "Strategy Guide" ? gameTitle : string.Empty,
            GuideType = inferred.Kind == "Strategy Guide" ? guideType : string.Empty,
            Edition = inferred.Kind == "Strategy Guide" ? edition : string.Empty,
            Franchise = inferred.Kind == "Strategy Guide" ? franchise : string.Empty,
            Developer = inferred.Kind == "Strategy Guide" ? developer : string.Empty,
            GamePublisher = inferred.Kind == "Strategy Guide" ? gamePublisher : string.Empty,
            GameReleaseYear = inferred.Kind == "Strategy Guide" ? gameReleaseYear : string.Empty,
            Genre = inferred.Kind == "Strategy Guide" ? genre : string.Empty,
            CoveredGames = inferred.Kind == "Strategy Guide" ? coveredGames : [],
            CoveredPlatforms = inferred.Kind == "Strategy Guide" ? coveredPlatforms : [],
            GuideTopics = inferred.Kind == "Strategy Guide" ? guideTopics : [],
            CharactersCovered = inferred.Kind == "Strategy Guide" ? charactersCovered : [],
            LocationsCovered = inferred.Kind == "Strategy Guide" ? locationsCovered : [],
            MetadataSource = metadataSource
        };
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
        return suffix.Count == 0 ? magazineTitle : $"{magazineTitle} — {string.Join(" • ", suffix)}";
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
        AddIf("buyer", "Buyer’s guide");
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
            ["metadataSource"] = "Guidevault JSON"
        };

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
        PutIfMissing("tags", update.Tags ?? item.Tags);
        if (pageCount > 0)
        {
            metadata["pageCount"] = pageCount;
            metadata["metadataPageCount"] = pageCount;
        }
        metadata["metadataSource"] = "Guidevault JSON";
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

public sealed record ArchiveValidationResult(bool IsReadable, string Message, int PageCount);

public static class ArchiveReader
{
    public static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".cbz", ".cbr", ".pdf" };
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp"];
    private static readonly ConcurrentDictionary<string, string[]> EntryCache = new();
    private static readonly ConcurrentDictionary<string, Lazy<Task<(byte[] Bytes, string ContentType)?>>> CoverCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly SemaphoreSlim CoverReadGate = new(6, 6);
    private static string CoverCacheDirectory = Path.Combine(AppContext.BaseDirectory, "data", "cache", "covers");

    public static void ConfigureCoverCache(string cacheDirectory)
    {
        if (!string.IsNullOrWhiteSpace(cacheDirectory)) CoverCacheDirectory = cacheDirectory;
        try { Directory.CreateDirectory(CoverCacheDirectory); } catch { }
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

        return new
        {
            imageEntryCacheCount = EntryCache.Count,
            inFlightCoverReads = CoverCache.Count,
            diskCoverCachePath = CoverCacheDirectory,
            diskCoverCacheFiles = diskFiles,
            diskCoverCacheBytes = diskBytes
        };
    }

    public static void ClearCoverCacheForPath(string archivePath)
    {
        try
        {
            EntryCache.TryRemove(archivePath, out _);
            var key = CoverCacheKey(archivePath);
            CoverCache.TryRemove(key, out _);
            if (Directory.Exists(CoverCacheDirectory))
            {
                foreach (var file in Directory.EnumerateFiles(CoverCacheDirectory, key + ".*"))
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
        ClearCoverCacheForPath(archivePath);
        EntryCache.TryRemove(archivePath, out _);
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

    public static async Task<(byte[] Bytes, string ContentType)?> GetCoverImageAsync(string archivePath)
    {
        if (Path.GetExtension(archivePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            return null;

        var key = CoverCacheKey(archivePath);
        var lazy = CoverCache.GetOrAdd(key, _ => new Lazy<Task<(byte[] Bytes, string ContentType)?>>(
            () => LoadCoverImageAsync(archivePath, key),
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

    private static async Task<(byte[] Bytes, string ContentType)?> LoadCoverImageAsync(string archivePath, string key)
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

            var image = await GetCoverImageFromArchiveAsync(archivePath);
            if (image is null)
            {
                CoverCache.TryRemove(key, out _);
                EntryCache.TryRemove(archivePath, out _);
                return null;
            }
            await TryWriteCoverToDiskAsync(key, image.Value.Bytes, image.Value.ContentType);
            return image;
        }
        catch
        {
            CoverCache.TryRemove(key, out _);
            EntryCache.TryRemove(archivePath, out _);
            return null;
        }
        finally
        {
            CoverReadGate.Release();
        }
    }


    private static async Task<(byte[] Bytes, string ContentType)?> GetCoverImageFromArchiveAsync(string archivePath)
    {
        var ext = Path.GetExtension(archivePath);
        if (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase)) return null;

        // Prefer System.IO.Compression for normal CBZ files because it is fast and
        // random-access. If the file is a mislabeled archive, fall back to
        // SharpCompress instead of treating the item as unreadable.
        if (ext.Equals(".cbz", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var zip = ZipFile.OpenRead(archivePath);
                var entries = zip.Entries
                    .Where(e => IsUsableZipImageEntry(e))
                    .OrderBy(e => NaturalSortKey(Normalize(e.FullName)))
                    .Take(12)
                    .ToArray();

                foreach (var entry in entries)
                {
                    try
                    {
                        var contentType = ContentTypeFromExtension(Path.GetExtension(entry.Name));
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

        using var reader = ReaderFactory.OpenReader(archivePath);
        var checkedImages = 0;
        while (reader.MoveToNextEntry())
        {
            var current = reader.Entry;
            if (current.IsDirectory) continue;
            var entryKey = current.Key ?? string.Empty;
            if (!IsImageEntryName(entryKey)) continue;

            checkedImages++;
            try
            {
                var contentType = ContentTypeFromExtension(Path.GetExtension(entryKey));
                await using var input = reader.OpenEntryStream();
                using var output = new MemoryStream();
                await input.CopyToAsync(output);
                var bytes = output.ToArray();
                if (LooksLikeImageBytes(bytes, contentType)) return (bytes, contentType);
            }
            catch
            {
                // Keep looking for a usable leading image.
            }

            if (checkedImages >= 12) break;
        }

        return null;
    }

    private static string CoverCacheKey(string archivePath)
    {
        try
        {
            var info = new FileInfo(archivePath);
            var raw = $"{info.FullName}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
            return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
        }
        catch
        {
            return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(archivePath))).ToLowerInvariant();
        }
    }

    private static async Task<(byte[] Bytes, string ContentType)?> TryReadCoverFromDiskAsync(string key)
    {
        try
        {
            Directory.CreateDirectory(CoverCacheDirectory);
            var file = Directory.EnumerateFiles(CoverCacheDirectory, key + ".*").FirstOrDefault();
            if (file is null || !File.Exists(file)) return null;
            return (await File.ReadAllBytesAsync(file), ContentTypeFromExtension(Path.GetExtension(file)));
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

    private static string[] GetImageEntries(string archivePath)
    {
        if (EntryCache.TryGetValue(archivePath, out var cached) && cached.Length > 0) return cached;

        try
        {
            var entries = ReadImageEntries(archivePath);

            // Cache only successful non-empty reads. Temporary network/archive read
            // failures should not poison the session and make covers/pages randomly
            // unavailable until Guidevault restarts.
            if (entries.Length > 0) EntryCache[archivePath] = entries;
            else EntryCache.TryRemove(archivePath, out _);
            return entries;
        }
        catch
        {
            EntryCache.TryRemove(archivePath, out _);
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

    public static async Task<IReadOnlyList<IgdbGameMetadataResult>> SearchAsync(string? q, string? platform, string? year, int limit, string? clientId, string? clientSecret)
    {
        EnsureConfigured(clientId, clientSecret);
        var query = CleanSearchText(q);
        if (string.IsNullOrWhiteSpace(query)) throw new InvalidOperationException("Enter a game title to search IGDB.");
        limit = Math.Clamp(limit, 1, 24);

        var results = await QueryGamesAsync(query, platform, year, limit, clientId!, clientSecret!, excludeVersions: true);
        if (results.Count == 0)
            results = await QueryGamesAsync(query, platform, year, limit, clientId!, clientSecret!, excludeVersions: false);
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
        EnsureConfigured(clientId, clientSecret);
        var token = await GetAccessTokenAsync(clientId!, clientSecret!);
        var body = $"fields id,name,first_release_date,genres.name,platforms.name,involved_companies.developer,involved_companies.publisher,involved_companies.company.name,franchise.name,franchises.name,collection.name,collections.name,cover.image_id,url,slug; where id = {fallback.Id}; limit 1;";
        var json = await PostIgdbAsync("games", body, clientId!, token);
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
                throw new InvalidOperationException($"Twitch OAuth token request failed with HTTP {(int)response.StatusCode}: {TrimError(text)}");
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
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            throw new InvalidOperationException("IGDB credentials are not configured. Add your IGDB/Twitch Client ID and Client Secret in Settings > Server > General > Metadata Sources.");
    }

    private static string SecretFingerprint(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    private static string TrimError(string value)
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
            var line = lines[i].Trim(' ', '•', '-', '*');
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
            .Select(v => Regex.Replace(Clean(v.Trim(' ', '.', '•', '-', '*')), @"^(?:and|or)\s+", string.Empty, RegexOptions.IgnoreCase).Trim())
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
    public const string Version = "0.9.83";
}
