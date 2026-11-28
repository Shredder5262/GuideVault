using System.Diagnostics;
using System.Text.Json;

// Read-only scan bridge to the image-owned v5.1 engine. Decisions use the existing relationship store.
internal static class GuidevaultContentIntelligence
{
    private const string Python = "/opt/guidevault-vision/bin/python";
    private const string Script = "/opt/guidevault-vision/content_intelligence.py";
    private const string ReportName = "magazine-review.json";
    private static string ReportDirectory(string root, string key) =>
        Path.Combine(root, "content-intelligence-results", "managed", key);

    private static async Task<JsonElement> ReadReportAsync(string path, string key, CancellationToken token)
    {
        await using var input = File.OpenRead(path);
        using var json = await JsonDocument.ParseAsync(input, cancellationToken: token);
        var report = json.RootElement;
        if (report.GetProperty("revision").GetString() != "unified-magazine-review-v1" ||
            report.GetProperty("document_key").GetString() != key ||
            report.GetProperty("source_fingerprint").GetString() != key.Split('/')[1] ||
            !report.GetProperty("complete_index_discovery").GetBoolean())
            throw new InvalidOperationException("Report does not match the current complete index.");
        return report.Clone();
    }

    public static string ReviewToken(JsonElement report) => Convert.ToHexString(
        System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(report.GetRawText())));

    public static (string Fingerprint, string Coverage) ValidateDecision(JsonElement report, GuidevaultContentDecision payload)
    {
        if (payload.Action is not ("confirm" or "reject" or "correct") || string.IsNullOrWhiteSpace(payload.GameId))
            throw new InvalidOperationException("Invalid review decision.");
        var matches = report.GetProperty("games").EnumerateArray().Where(g =>
            string.Equals(g.GetProperty("game_id").GetString(), payload.GameId, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException("Select a current game review lead.");
        var fingerprint = report.GetProperty("source_fingerprint").GetString()!;
        if (payload.Action == "reject") return (fingerprint, "");
        if (payload.Page is null || payload.Page < 1 || payload.Page > report.GetProperty("total_pages").GetInt32())
            throw new InvalidOperationException("Choose a valid reviewed digital page.");
        var types = matches[0].GetProperty("evidence").EnumerateArray()
            .Where(e => e.GetProperty("digital_page").ValueKind == JsonValueKind.Number && e.GetProperty("digital_page").TryGetInt32(out var page) && page == payload.Page)
            .Select(e => e.GetProperty("content_kind").GetString() ?? "")
            .Where(IsSubstantive).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (types.Length == 0) throw new InvalidOperationException("Choose a substantive coverage page, not a checklist or cover reference.");
        return (fingerprint, string.Join(" / ", types));
    }

    internal static bool IsCurrentSource(LibraryItem item, string fingerprint)
    {
        try { return GuidevaultDocumentIndex.CatalogFingerprint(item) == fingerprint; }
        catch (Exception) { return false; }
    }

    internal static bool IsSubstantive(string kind) =>
        kind.StartsWith("Game review", StringComparison.OrdinalIgnoreCase) ||
        kind.StartsWith("Game feature", StringComparison.OrdinalIgnoreCase) ||
        kind.StartsWith("Game profile", StringComparison.OrdinalIgnoreCase) ||
        kind.StartsWith("Preview", StringComparison.OrdinalIgnoreCase) ||
        kind.StartsWith("Game preview", StringComparison.OrdinalIgnoreCase) ||
        kind.StartsWith("Tips", StringComparison.OrdinalIgnoreCase) ||
        kind.StartsWith("Gameplay", StringComparison.OrdinalIgnoreCase) ||
        kind.StartsWith("Cheats", StringComparison.OrdinalIgnoreCase) ||
        kind.StartsWith("Walkthrough", StringComparison.OrdinalIgnoreCase) ||
        kind.StartsWith("Strategy", StringComparison.OrdinalIgnoreCase) ||
        kind.StartsWith("Game title heading", StringComparison.OrdinalIgnoreCase);

    public static async Task<JsonElement?> LatestAsync(string root, LibraryItem item, CancellationToken token)
    {
        var key = GuidevaultDocumentIndex.ContentIntelligenceKey(item);
        var path = Path.Combine(ReportDirectory(root, key), ReportName);
        if (!File.Exists(path)) return null;
        return await ReadReportAsync(path, key, token);
    }

    public static async Task<(JsonElement Report, double Seconds)> ScanAsync(string root, LibraryItem item, string catalogPath, bool visual, CancellationToken token)
    {
        if (!File.Exists(Python) || !File.Exists(Script))
            throw new InvalidOperationException("Packaged Content Intelligence runtime is missing. Rebuild the GuideVault image.");
        if (!File.Exists(catalogPath)) throw new InvalidOperationException("Sync the LaunchBox catalog first.");
        var key = GuidevaultDocumentIndex.ContentIntelligenceKey(item);
        var fingerprint = key.Split('/')[1];
        var index = GuidevaultDocumentIndex.ContentIntelligenceDirectory(root, item, fingerprint);
        if (!File.Exists(Path.Combine(index, "manifest.json")))
            throw new InvalidOperationException("Index this magazine using Scan Magazine before running Content Intelligence.");
        // The engine validates every indexed page itself; no expensive index rebuild in this request.
        var extension = Path.GetExtension(item.Path).ToLowerInvariant();
        if (visual && extension is not (".cbz" or ".zip" or ".pdf"))
            throw new InvalidOperationException("Targeted visual OCR supports PDF and CBZ/ZIP only. Disable visual OCR for this format.");
        var target = ReportDirectory(root, key);
        var run = Path.Combine(target, "runs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(run);
        var label = string.Join(" | ", string.IsNullOrWhiteSpace(item.MagazineTitle) ? item.Title : item.MagazineTitle,
            item.IssueNumber, item.CoverDate);
        using var process = new Process { StartInfo = new ProcessStartInfo(Python)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
        }};
        foreach (var arg in new[] { Script, "--index-directory", index, "--document-key", key, "--label", label,
            "--catalog", catalogPath, "--output", run, "--budget-seconds", "118" }) process.StartInfo.ArgumentList.Add(arg);
        if (visual) foreach (var arg in new[] { "--visual", "--source", item.Path }) process.StartInfo.ArgumentList.Add(arg);
        process.StartInfo.Environment["GUIDEVAULT_DATA"] = root;
        process.StartInfo.Environment["HF_HUB_OFFLINE"] = "1";
        process.StartInfo.Environment["OMP_THREAD_LIMIT"] = "2";
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(120));
        var watch = Stopwatch.StartNew();
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(deadline.Token);
            await Task.WhenAll(stdout, stderr).WaitAsync(deadline.Token);
            // Never publish an incomplete or failed run over the previous review.
            if (process.ExitCode != 0)
                throw new InvalidOperationException(process.ExitCode == 2 ? "Content Intelligence reached its time budget or stopped. Previous review retained." : "Content Intelligence failed. Check that the current document index is complete and the catalog is readable.");
            var outputs = Directory.GetDirectories(run, "gv-ci-catalog-*");
            if (outputs.Length != 1) throw new InvalidOperationException("No unique scan output was produced.");
            var path = Path.Combine(outputs[0], ReportName);
            if (!File.Exists(path)) throw new InvalidOperationException("Scan did not finish a unified review. Previous review retained.");
            var report = await ReadReportAsync(path, key, deadline.Token);
            if (GuidevaultDocumentIndex.ContentIntelligenceKey(item) != key)
                throw new InvalidOperationException("Magazine changed during the scan; report was not published.");
            var published = Path.Combine(target, ReportName);
            var temp = published + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await File.WriteAllTextAsync(temp, report.GetRawText(), deadline.Token);
                deadline.Token.ThrowIfCancellationRequested();
                File.Move(temp, published, true);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
            return (report, Math.Round(watch.Elapsed.TotalSeconds, 2));
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        { throw new InvalidOperationException("Content Intelligence reached the 120-second request budget. Previous review retained."); }
        finally
        {
            try { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3)); } } catch { }
            // Observe pipe errors even if the request was cancelled.
            try { await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(1)); } catch { }
        }
    }
}

public sealed class GuidevaultContentDecision
{
    public string Action { get; set; } = "";
    public string GameId { get; set; } = "";
    public string TargetGameId { get; set; } = "";
    public string ReviewToken { get; set; } = "";
    public string ExpectedVersion { get; set; } = "";
    public int? Page { get; set; }
}
