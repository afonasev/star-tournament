using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Velopack;
using Velopack.Logging;
using Velopack.Sources;

namespace StarTournament.Distribution;

public sealed record LauncherConfig(string AppId, string Platform, string Architecture, string Version,
    string PublicationDate, string FeedBase, string GameExecutable, string? UpdateChannel = null);

public sealed class AuthenticatedSource : IUpdateSource, IDisposable
{
    readonly LauncherConfig config;
    readonly string key;
    readonly HttpClient http;
    Uri? packageUri;
    public string? Envelope { get; private set; }
    public VelopackAsset? Asset { get; private set; }
    public string? PublicationDate { get; private set; }

    public AuthenticatedSource(LauncherConfig config, string key, HttpMessageHandler? transport = null)
    {
        this.config = config; this.key = key;
        if (config.UpdateChannel != null && (config.UpdateChannel is not ("test" or "production") ||
            config.FeedBase != (config.UpdateChannel == "test" ? TestLocator : ProductionBase)))
            throw new CryptographicException("Untrusted GitHub channel configuration");
        http = new HttpClient(transport ?? new HttpClientHandler { AllowAutoRedirect = false })
        { Timeout = TimeSpan.FromMinutes(30), MaxResponseContentBufferSize = 65536 };
    }

    public const string TestLocator = "https://api.github.com/repos/afonasev/star-tournament/releases";
    public const string ProductionBase = "https://github.com/afonasev/star-tournament/releases/latest/download/";
    string ManifestName => $"latest-{config.Platform}-{config.Architecture}.json";

    public void Admit(string envelope)
    {
        // Clear prior admission before parsing any new, potentially rejected descriptor.
        Envelope = null; Asset = null; PublicationDate = null; packageUri = null;
        if (envelope.Length > 65536) throw new CryptographicException("Release metadata exceeds limit");
        using var outer = JsonDocument.Parse(envelope);
        var payload = Convert.FromBase64String(outer.RootElement.GetProperty("payload").GetString()!);
        var signature = Convert.FromBase64String(outer.RootElement.GetProperty("signature").GetString()!);
        using var rsa = RSA.Create(); rsa.ImportFromPem(key);
        if (!rsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
            throw new CryptographicException("Invalid release signature");
        using var doc = JsonDocument.Parse(payload); var release = doc.RootElement;
        if (release.GetProperty("schema").GetInt32() != 1 ||
            release.GetProperty("platform").GetString() != config.Platform ||
            release.GetProperty("architecture").GetString() != config.Architecture)
            throw new CryptographicException("Release target mismatch");
        if (config.UpdateChannel != null && (!release.TryGetProperty("channel", out var signedChannel) ||
            signedChannel.GetString() != config.UpdateChannel))
            throw new CryptographicException("Release channel mismatch");
        var date = release.GetProperty("publicationDate").GetString()!;
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out _)) throw new CryptographicException("Invalid publication date");
        var feed = VelopackAssetFeed.FromJson(release.GetProperty("feed").GetRawText());
        if (feed.Assets.Length != 1) throw new CryptographicException("Exactly one full package required");
        var asset = feed.Assets[0];
        if (asset.PackageId != config.AppId || asset.Type != VelopackAssetType.Full ||
            asset.Version.ToString() != release.GetProperty("version").GetString() ||
            !Regex.IsMatch(asset.SHA256 ?? "", "\\A[0-9a-fA-F]{64}\\z") || asset.Size <= 0 ||
            !Regex.IsMatch(asset.FileName ?? "", "\\A[A-Za-z0-9][A-Za-z0-9._-]*\\.nupkg\\z"))
            throw new CryptographicException("Invalid authenticated package identity");
        Uri? authenticatedUrl = null;
        if (release.TryGetProperty("downloadUrl", out var url) && url.ValueKind != JsonValueKind.Null)
        {
            if (!Uri.TryCreate(url.GetString(), UriKind.Absolute, out authenticatedUrl))
                throw new CryptographicException("Invalid signed download URL");
            var expected = $"/afonasev/star-tournament/releases/download/v{asset.Version}/{asset.FileName}";
            if (!SafeHttps(authenticatedUrl) || authenticatedUrl.Host != "github.com" ||
                authenticatedUrl.AbsolutePath != expected || authenticatedUrl.Query.Length != 0)
                throw new CryptographicException("Untrusted signed download URL");
        }
        if (config.UpdateChannel != null && (authenticatedUrl == null ||
            (config.UpdateChannel == "test") != asset.Version.IsPrerelease))
            throw new CryptographicException("Missing GitHub identity or invalid channel version");
        packageUri = authenticatedUrl;
        Envelope = envelope; Asset = asset; PublicationDate = date;
    }

    Uri Remote(string filename)
    {
        var origin = new Uri(config.FeedBase.TrimEnd('/') + "/");
        if (origin.Scheme != "https" || !string.IsNullOrEmpty(origin.UserInfo))
            throw new CryptographicException("HTTPS feed required");
        return new Uri(origin, filename);
    }

    static bool SafeHttps(Uri uri) => uri.Scheme == "https" && uri.Port == 443 &&
        uri.UserInfo.Length == 0 && uri.Fragment.Length == 0;

    async Task<HttpResponseMessage> OpenAsset(Uri initial, bool metadata, bool redirectsAllowed, CancellationToken token)
    {
        var target = initial;
        for (var redirects = 0; ; redirects++)
        {
            var response = await http.GetAsync(target, HttpCompletionOption.ResponseHeadersRead, token);
            if ((int)response.StatusCode is not (301 or 302 or 303 or 307 or 308)) return response;
            var location = response.Headers.Location;
            response.Dispose();
            if (!redirectsAllowed || redirects >= 3 || location == null)
                throw new CryptographicException("Unexpected or excessive asset redirect");
            var next = location.IsAbsoluteUri ? location : new Uri(target, location);
            var productionRedirect = metadata && initial.AbsolutePath.Contains("/releases/latest/download/") &&
                Regex.IsMatch(next.AbsolutePath, @"\A/afonasev/star-tournament/releases/download/v[0-9]+\.[0-9]+\.[0-9]+/" + Regex.Escape(ManifestName) + @"\z");
            var github = next.Host == "github.com" && next.Query.Length == 0 &&
                (next.AbsolutePath == initial.AbsolutePath || productionRedirect);
            if (!SafeHttps(next) || !(github || next.Host is "release-assets.githubusercontent.com" or "objects.githubusercontent.com"))
                throw new CryptographicException("Untrusted asset redirect");
            target = next;
        }
    }

    async Task<string> ReadBounded(Uri uri, int limit, bool redirects, CancellationToken token)
    {
        using var response = await OpenAsset(uri, true, redirects, token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > limit) throw new CryptographicException("Metadata exceeds limit");
        await using var input = await response.Content.ReadAsStreamAsync(token);
        using var output = new MemoryStream();
        var buffer = new byte[8192]; int count;
        while ((count = await input.ReadAsync(buffer, token)) > 0)
        {
            if (output.Length + count > limit) throw new CryptographicException("Metadata exceeds limit");
            output.Write(buffer, 0, count);
        }
        return System.Text.Encoding.UTF8.GetString(output.ToArray());
    }

    async Task<(Uri Uri, string? Version)> Discover(CancellationToken token)
    {
        if (config.UpdateChannel == null) return (Remote("latest.json"), null); // Legacy broker/config only.
        if (config.UpdateChannel == "production") return (new Uri(ProductionBase + ManifestName), null);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("StarTournament-Updater/1");
        SemanticVersion? best = null;
        Uri? bestUri = null;
        for (var page = 1; page <= 10; page++)
        {
            using var doc = JsonDocument.Parse(await ReadBounded(new Uri(TestLocator + $"?per_page=100&page={page}"), 2 * 1024 * 1024, false, token));
            var releases = doc.RootElement;
            foreach (var release in releases.EnumerateArray())
            {
                if (release.GetProperty("draft").GetBoolean() || !release.GetProperty("prerelease").GetBoolean()) continue;
                var tag = release.GetProperty("tag_name").GetString() ?? "";
                if (!tag.StartsWith('v') || !SemanticVersion.TryParse(tag[1..], out var version) || !version.IsPrerelease || tag != "v" + version) continue;
                var names = release.GetProperty("assets").EnumerateArray().Select(x => x.GetProperty("name").GetString()).ToHashSet();
                if (!names.Contains("latest-win-x64.json") || !names.Contains("latest-osx-arm64.json") ||
                    !names.Contains($"Star-Tournament-{version}-Windows-x64-Setup.exe") ||
                    !names.Contains($"Star-Tournament-{version}-macOS-arm64.pkg")) continue;
                if (best == null || version > best)
                {
                    best = version;
                    // Construct the pinned URL; never trust a locator-provided arbitrary URL.
                    bestUri = new Uri($"https://github.com/afonasev/star-tournament/releases/download/{tag}/{ManifestName}");
                }
            }
            if (releases.GetArrayLength() < 100)
                return bestUri == null ? throw new InvalidOperationException("No complete test release") : (bestUri, best!.ToString());
        }
        throw new InvalidOperationException("Release discovery exceeds pagination limit");
    }

    public async Task<VelopackAssetFeed> GetReleaseFeed(IVelopackLogger logger, string? appId, string channel,
        Guid? stagingId = null, VelopackAsset? latestLocalRelease = null)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var location = await Discover(timeout.Token);
        var envelope = await ReadBounded(location.Uri, 65536, config.UpdateChannel != null, timeout.Token);
        Admit(envelope);
        if (location.Version != null && Asset!.Version.ToString() != location.Version)
        {
            Envelope = null; Asset = null; packageUri = null;
            throw new CryptographicException("Release discovery version mismatch");
        }
        return new VelopackAssetFeed { Assets = new[] { Asset! } };
    }

    public async Task VerifyPackage(string path, VelopackAsset asset, CancellationToken token = default)
    {
        if (Asset == null || asset.FileName != Asset.FileName || asset.SHA256 != Asset.SHA256 ||
            asset.Version != Asset.Version || asset.Size != Asset.Size) throw new CryptographicException("Package was not admitted");
        await using var stream = File.OpenRead(path);
        if (stream.Length != asset.Size) throw new CryptographicException("Package size mismatch");
        var hash = await SHA256.HashDataAsync(stream, token);
        if (!CryptographicOperations.FixedTimeEquals(hash, Convert.FromHexString(asset.SHA256)))
            throw new CryptographicException("Package digest mismatch");
    }

    public async Task DownloadReleaseEntry(IVelopackLogger logger, VelopackAsset releaseEntry, string localFile,
        Action<int> progress, CancellationToken cancelToken = default)
    {
        if (Asset == null || releaseEntry.FileName != Asset.FileName) throw new CryptographicException("Unadmitted package");
        var quarantine = localFile + ".authenticated-download";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(30));
        cancelToken = timeout.Token;
        try
        {
            using var response = await OpenAsset(packageUri ?? Remote(releaseEntry.FileName), false, packageUri != null, cancelToken);
            response.EnsureSuccessStatusCode();
            await using (var input = await response.Content.ReadAsStreamAsync(cancelToken))
            await using (var output = File.Create(quarantine))
            {
                var buffer = new byte[128 * 1024]; long received = 0; int count;
                while ((count = await input.ReadAsync(buffer, cancelToken)) > 0)
                {
                    received += count;
                    if (received > releaseEntry.Size) throw new CryptographicException("Package exceeds signed size");
                    await output.WriteAsync(buffer.AsMemory(0, count), cancelToken);
                    progress((int)Math.Min(99, received * 100 / releaseEntry.Size));
                }
            }
            await VerifyPackage(quarantine, releaseEntry, cancelToken);
            File.Move(quarantine, localFile, true); // SDK sees bytes only after authentication.
            progress(100);
        }
        finally { if (File.Exists(quarantine)) File.Delete(quarantine); }
    }

    public void Dispose() => http.Dispose();
}
