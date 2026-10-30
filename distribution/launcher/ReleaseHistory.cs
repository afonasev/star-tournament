using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Velopack;

namespace StarTournament.Distribution;

public sealed record ReleaseHistoryItem(string Version, string Date, string Russian, string English, bool Installed, string PublishedAt = "", ReleaseHistoryBlock[]? RussianBlocks = null, ReleaseHistoryBlock[]? EnglishBlocks = null);
public sealed record ReleaseHistorySnapshot(string State, ReleaseHistoryItem[] Items, string Error = "");
// Release text is presentation data only. It cannot admit or replace an update target.
public sealed class ReleaseHistory : IDisposable
{
    public const int Depth = 10;
    readonly LauncherConfig config;
    readonly string cachePath;
    readonly HttpClient http;
    readonly ReleaseHistoryMedia media;
    ReleaseHistorySnapshot? latest;
    DateTimeOffset blockedUntil;
    static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
    sealed record Cache(string Channel, string Installed, string ETag, ReleaseHistoryItem[] Items);
    public ReleaseHistory(LauncherConfig config, string cachePath, HttpMessageHandler? transport = null, HttpMessageHandler? mediaTransport = null)
    {
        this.config = config; this.cachePath = cachePath;
        media = new ReleaseHistoryMedia(cachePath + ".media", mediaTransport);
        http = new HttpClient(transport ?? new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("StarTournament-History/1");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }
    public static (string Russian, string English) Languages(string body)
    {
        // Only canonical language sections are shown; no invented translation of legacy notes.
        string Extract(string language)
        {
            var match = Regex.Match(body, @"(?ms)^##[ \t]+" + language + @"[ \t]*\r?$\n(.*?)(?=^##[ \t]+(?:Русский|English)[ \t]*\r?$|\z)");
            return match.Success ? match.Groups[1].Value.Trim() : "";
        }
        return (Extract("Русский"), Extract("English"));
    }
    ReleaseHistoryItem? Item(JsonElement row, bool installedOnly = false)
    {
        if (row.GetProperty("draft").GetBoolean()) return null;
        var tag = row.GetProperty("tag_name").GetString() ?? "";
        if (!tag.StartsWith('v') || !SemanticVersion.TryParse(tag[1..], out var version) || tag != "v" + version) return null;
        bool installed = version.ToString() == config.Version;
        if (installedOnly ? !installed : row.GetProperty("prerelease").GetBoolean() != (config.UpdateChannel == "test")) return null;
        var date = row.GetProperty("published_at").GetString() ?? "";
        if (!DateTimeOffset.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var publication)) return null;
        string body = row.TryGetProperty("body", out var value) ? value.GetString() ?? "" : "";
        if (body.Length > 65536) throw new InvalidDataException("Release notes exceed limit");
        var languages = Languages(body);
        return new(version.ToString(), publication.UtcDateTime.ToString("yyyy-MM-dd"), languages.Russian, languages.English, installed, publication.ToUniversalTime().ToString("O"), ReleaseHistoryMedia.Parse(languages.Russian, version.ToString(), compact: true), ReleaseHistoryMedia.Parse(languages.English, version.ToString(), compact: true));
    }
    Cache? ReadCache()
    {
        try
        {
            if (!File.Exists(cachePath) || new FileInfo(cachePath).Length > 8 * 1024 * 1024) return null;
            var cache = JsonSerializer.Deserialize<Cache>(File.ReadAllText(cachePath), Json);
            return cache is { Items.Length: > 0 and <= Depth + 1 } && cache.Channel == (config.UpdateChannel ?? "production") && cache.Installed == config.Version ? cache : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }
    async Task<(HttpStatusCode Status, string Body, string ETag)> Get(string path, CancellationToken token, string etag = "")
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/afonasev/star-tournament/releases" + path);
        if (etag.Length > 0) request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if (response.StatusCode is HttpStatusCode.NotModified or HttpStatusCode.NotFound) return (response.StatusCode, "", "");
        if ((int)response.StatusCode is 403 or 429)
        {
            blockedUntil=response.Headers.RetryAfter?.Date ?? DateTimeOffset.UtcNow.Add(response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(1));
            if(response.Headers.TryGetValues("X-RateLimit-Reset",out var reset)&&long.TryParse(reset.FirstOrDefault(),out var epoch))
                blockedUntil=DateTimeOffset.FromUnixTimeSeconds(epoch)>blockedUntil?DateTimeOffset.FromUnixTimeSeconds(epoch):blockedUntil;
        }
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 2 * 1024 * 1024) throw new InvalidDataException("History metadata exceeds limit");
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var buffer = new MemoryStream(); var bytes = new byte[8192]; int count;
        while ((count = await stream.ReadAsync(bytes, token)) > 0)
        { if (buffer.Length + count > 2 * 1024 * 1024) throw new InvalidDataException("History metadata exceeds limit"); buffer.Write(bytes, 0, count); }
        return (response.StatusCode, System.Text.Encoding.UTF8.GetString(buffer.ToArray()), response.Headers.ETag?.ToString() ?? "");
    }
    public async Task<ReleaseHistorySnapshot> Fetch(CancellationToken token) => latest = await FetchCore(token);
    async Task<ReleaseHistorySnapshot> FetchCore(CancellationToken token)
    {
        var cache = ReadCache();
        try
        {
            if(DateTimeOffset.UtcNow<blockedUntil)throw new HttpRequestException("GitHub request limit");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var rows = new List<ReleaseHistoryItem>(); string etag = ""; bool complete = false;
            for (int page = 1; page <= 10; page++)
            {
                var response = await Get("?per_page=100&page=" + page, timeout.Token, page == 1 ? cache?.ETag ?? "" : "");
                if (response.Status == HttpStatusCode.NotModified && cache != null) return new("ready", cache.Items);
                if (response.Status != HttpStatusCode.OK) throw new HttpRequestException("История релизов недоступна.");
                if (page == 1) etag = response.ETag;
                using var data = JsonDocument.Parse(response.Body);
                foreach (var row in data.RootElement.EnumerateArray()) { var item = Item(row); if (item != null) rows.Add(item); }
                if (rows.Count >= Depth || data.RootElement.GetArrayLength() < 100) { complete = true; break; }
            }
            if (!complete) throw new InvalidDataException("History pagination exceeds limit");
            var items = rows.DistinctBy(x => x.Version).OrderByDescending(x => x.PublishedAt, StringComparer.Ordinal).ThenByDescending(x => SemanticVersion.Parse(x.Version)).Take(Depth).ToList();
            if (!items.Any(x => x.Installed))
            {
                var installed = await Get("/tags/" + Uri.EscapeDataString("v" + config.Version), timeout.Token);
                ReleaseHistoryItem? item = null;
                if (installed.Status == HttpStatusCode.OK) { using var row = JsonDocument.Parse(installed.Body); item = Item(row.RootElement, true); }
                items.Add(item ?? new(config.Version, config.PublicationDate, "", "", true));
            }
            token.ThrowIfCancellationRequested();
            var result = items.ToArray();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
                File.WriteAllText(cachePath + ".tmp", JsonSerializer.Serialize(new Cache(config.UpdateChannel ?? "production", config.Version, etag, result), Json));
                File.Move(cachePath + ".tmp", cachePath, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { UpdateFeedback.Log("history-cache", ex); }
            return new("ready", result);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or JsonException or OperationCanceledException)
        {
            return cache != null ? new("cached", cache.Items, "Сеть недоступна. Показана сохранённая история.") :
                new("error", Array.Empty<ReleaseHistoryItem>(), "Не удалось получить историю версий. Проверьте подключение.");
        }
    }
    public async Task<ReleaseHistorySnapshot?> FetchMedia(string version, CancellationToken token)
    {
        var snapshot = latest;
        var item = snapshot?.Items.FirstOrDefault(x => x.Version == version);
        if (snapshot == null || item == null) return null;
        // Old caches have no blocks. Reparse from retained canonical language text.
        var ru = item.RussianBlocks ?? ReleaseHistoryMedia.Parse(item.Russian, version, compact: true);
        var en = item.EnglishBlocks ?? ReleaseHistoryMedia.Parse(item.English, version, compact: true);
        var loaded = new Dictionary<string, ReleaseHistoryBlock>();
        var referenced = ru.Concat(en).Where(x => x.Kind == "image").DistinctBy(x => x.Sha256).ToArray();
        if (referenced.Length > ReleaseHistoryMedia.MaxImages)
        {
            ReleaseHistoryBlock[] Unavailable(ReleaseHistoryBlock[] blocks) => blocks.Select(x => x.Kind == "image" ? x with { State = "unavailable", Path = "" } : x).ToArray();
            return latest = snapshot with { Items = snapshot.Items.Select(x => x.Version == version ? item with { RussianBlocks = Unavailable(ru), EnglishBlocks = Unavailable(en) } : x).ToArray() };
        }
        foreach (var block in referenced)
            if (!loaded.ContainsKey(block.Sha256)) loaded[block.Sha256] = await media.Load(block, version, token);
        ReleaseHistoryBlock[] Apply(ReleaseHistoryBlock[] blocks) => blocks.Select(x => loaded.TryGetValue(x.Sha256, out var image) ? image with { Text = x.Text } : x).ToArray();
        token.ThrowIfCancellationRequested();
        return latest = snapshot with { Items = snapshot.Items.Select(x => x.Version == version ? item with { RussianBlocks = Apply(ru), EnglishBlocks = Apply(en) } : x).ToArray() };
    }
    public void Dispose() { http.Dispose(); media.Dispose(); }
}
