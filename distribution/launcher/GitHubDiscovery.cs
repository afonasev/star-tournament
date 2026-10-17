using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StarTournament.Distribution;

internal sealed record GitHubPage(string Body, Uri? Next);

// Public API pages are locator hints only. Signed metadata remains the admission authority.
internal sealed class GitHubDiscovery
{
    const int BodyLimit = 2 * 1024 * 1024, CacheLimit = 14 * 1024 * 1024;
    readonly HttpClient http;
    readonly TimeSpan stallTimeout;
    readonly string? directory;
    readonly Func<DateTimeOffset> clock;
    DateTimeOffset blockedUntil;
    sealed record Cached(int Schema, string Uri, string ETag, string Body, string Hash, string? Next);
    sealed record Cooldown(int Schema, DateTimeOffset Until);
    public GitHubDiscovery(HttpClient http, TimeSpan stallTimeout, string? directory, Func<DateTimeOffset>? clock)
    {
        this.http = http; this.stallTimeout = stallTimeout;
        this.directory = directory == null ? null : Path.GetFullPath(directory);
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
        var cooldown = Read<Cooldown>("cooldown.json", 1024);
        if (cooldown?.Schema == 1) blockedUntil = cooldown.Until;
    }
    static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    static void ValidateUri(Uri uri)
    {
        if (uri.Scheme != "https" || uri.Host != "api.github.com" || uri.Port != 443 || uri.UserInfo.Length != 0 ||
            uri.Fragment.Length != 0 || uri.AbsolutePath != "/repos/afonasev/star-tournament/releases" || uri.Query.Length > 96 ||
            !Regex.IsMatch(uri.Query, @"\A\?(?:per_page=100&page=[1-9][0-9]*|page=[1-9][0-9]*&per_page=100)\z"))
            throw new InvalidDataException("Untrusted release pagination URL");
    }
    T? Read<T>(string name, int limit) where T : class
    {
        if (directory == null) return null;
        try
        {
            var path = Path.Combine(directory, name);
            return File.Exists(path) && new FileInfo(path).Length <= limit ? JsonSerializer.Deserialize<T>(File.ReadAllText(path)) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }
    void Write<T>(string name, T value)
    {
        if (directory == null) return;
        string? temp = null;
        try
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, name); temp = path + ".write-" + Guid.NewGuid().ToString("N");
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { JsonSerializer.Serialize(stream, value); stream.Flush(true); }
            File.Move(temp, path, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* Cache IO cannot admit or invalidate a network response. */ }
        finally { if (temp != null) { try { File.Delete(temp); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { } } }
    }
    Cached? Cache(Uri uri)
    {
        var cache = Read<Cached>(Hash(uri.AbsoluteUri) + ".json", CacheLimit);
        if (cache == null) return null;
        try
        {
            if (cache.Schema != 1 || cache.Uri != uri.AbsoluteUri || cache.Body == null || Encoding.UTF8.GetByteCount(cache.Body) > BodyLimit ||
                cache.Hash != Hash(cache.Body) || cache.ETag == null || cache.ETag.Length > 1024 ||
                !EntityTagHeaderValue.TryParse(cache.ETag, out var tag) || tag.Tag == "*") return null;
            ValidateBody(cache.Body);
            if (cache.Next != null) ValidateUri(new Uri(cache.Next, UriKind.Absolute));
            return cache;
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or UriFormatException) { return null; }
    }
    static void ValidateBody(string body)
    {
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() > 100)
            throw new InvalidDataException("Invalid release listing");
    }
    static Uri? Next(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Link", out var values)) return null;
        var links = string.Join(",", values);
        if (links.Length > 8192) throw new InvalidDataException("Pagination headers exceed limit");
        var next = Regex.Matches(links, "<([^>]+)>\\s*;\\s*rel=\"next\"");
        if (next.Count > 1) throw new InvalidDataException("Ambiguous release pagination");
        if (next.Count == 0) return null;
        var uri = new Uri(next[0].Groups[1].Value, UriKind.Absolute); ValidateUri(uri); return uri;
    }
    DateTimeOffset Deadline(HttpResponseMessage response)
    {
        var now = clock(); DateTimeOffset? until = null;
        var retry = response.Headers.RetryAfter;
        if (retry?.Delta is { } delta) until = now.Add(delta);
        else if (retry?.Date is { } date) until = date;
        if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining) && remaining.FirstOrDefault() == "0" &&
            response.Headers.TryGetValues("X-RateLimit-Reset", out var reset) && long.TryParse(reset.FirstOrDefault(), out var seconds))
        {
            try { var primary = DateTimeOffset.FromUnixTimeSeconds(seconds); if (until == null || primary > until) until = primary; }
            catch (ArgumentOutOfRangeException) { }
        }
        return until is { } found && found >= now ? found : now.AddMinutes(1);
    }
    public async Task<GitHubPage> Get(Uri uri, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); ValidateUri(uri);
        if (clock() < blockedUntil) throw new HttpRequestException("GitHub API cooldown is active", null, HttpStatusCode.TooManyRequests);
        // Parsing a bounded disk cache must not delay spawning the Player before
        // the first HTTP await in the launcher's background discovery operation.
        var cache = directory == null ? null : await Task.Run(() => Cache(uri), token);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("StarTournament-Updater/1");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        if (cache != null) request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(cache.ETag));
        using var headers = CancellationTokenSource.CreateLinkedTokenSource(token); headers.CancelAfter(stallTimeout);
        HttpResponseMessage response;
        try { response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, headers.Token); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new TimeoutException("Release discovery headers stalled"); }
        using (response)
        {
            bool rateError = (int)response.StatusCode is 403 or 429;
            bool exhausted = response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining) && remaining.FirstOrDefault() == "0";
            if (rateError || exhausted)
            {
                blockedUntil = Deadline(response); Write("cooldown.json", new Cooldown(1, blockedUntil));
                if (rateError) throw new HttpRequestException("GitHub API rate limit; next check waits for the server deadline", null, response.StatusCode);
            }
            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                if (cache == null) throw new InvalidDataException("GitHub returned304 without a valid cached page");
                token.ThrowIfCancellationRequested();
                // Pagination headers can change while the page representation stays the same.
                var cachedNext = response.Headers.Contains("Link") ? Next(response) : cache.Next == null ? null : new Uri(cache.Next);
                var tag = response.Headers.ETag?.ToString();
                var updated = cache with { Next = cachedNext?.AbsoluteUri, ETag = tag is { Length: > 0 and <= 1024 } && tag != "*" ? tag : cache.ETag };
                if (updated != cache) Write(Hash(uri.AbsoluteUri) + ".json", updated);
                return new GitHubPage(cache.Body, cachedNext);
            }
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > BodyLimit) throw new InvalidDataException("Release listing exceeds limit");
            await using var stream = await response.Content.ReadAsStreamAsync(token);
            using var output = new MemoryStream(); var buffer = new byte[8192];
            while (true)
            {
                using var read = CancellationTokenSource.CreateLinkedTokenSource(token); read.CancelAfter(stallTimeout);
                int count;
                try { count = await stream.ReadAsync(buffer, read.Token); }
                catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new TimeoutException("Release discovery body stalled"); }
                if (count == 0) break;
                if (output.Length + count > BodyLimit) throw new InvalidDataException("Release listing exceeds limit");
                output.Write(buffer, 0, count);
            }
            var body = Encoding.UTF8.GetString(output.ToArray()); ValidateBody(body); var next = Next(response);
            var etag = response.Headers.ETag?.ToString();
            token.ThrowIfCancellationRequested();
            if (etag is { Length: > 0 and <= 1024 } && etag != "*")
                Write(Hash(uri.AbsoluteUri) + ".json", new Cached(1, uri.AbsoluteUri, etag, body, Hash(body), next?.AbsoluteUri));
            return new GitHubPage(body, next);
        }
    }
}
