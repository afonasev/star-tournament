using System.Security.Cryptography;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using Velopack;
using Velopack.Logging;
using Velopack.Sources;

namespace StarTournament.Distribution;

public sealed record LauncherConfig(string AppId, string Platform, string Architecture, string Version,
    string PublicationDate, string FeedBase, string GameExecutable, string? UpdateChannel = null, ReleaseRequirements? Compatibility = null);

public sealed class AuthenticatedSource : IUpdateSource, IDisposable
{
    readonly LauncherConfig config;
    readonly string key;
    readonly HttpClient http;
    readonly TimeSpan stallTimeout;
    readonly GitHubDiscovery discovery;
    readonly int readerProtocol;
    readonly Func<string, Version, bool>? hostProbe;
    ReleaseRequirements? requirements;
    Uri? packageUri;
    public string? Envelope { get; private set; }
    public VelopackAsset? Asset { get; private set; }
    public string? PublicationDate { get; private set; }
    public CancellationToken CheckCancellationToken { get; set; }

    public AuthenticatedSource(LauncherConfig config, string key, HttpMessageHandler? transport = null, TimeSpan? stallTimeout = null, string? discoveryCachePath = null, Func<DateTimeOffset>? discoveryClock = null, int readerProtocol = ReleaseCompatibility.ReaderProtocol, Func<string, Version, bool>? hostProbe = null)
    {
        this.config = config; this.key = key;
        this.readerProtocol = readerProtocol; this.hostProbe = hostProbe;
        this.stallTimeout = stallTimeout ?? TimeSpan.FromSeconds(30);
        if (config.UpdateChannel != null && (config.UpdateChannel is not ("test" or "production") ||
            config.FeedBase != (config.UpdateChannel == "test" ? TestLocator : ProductionBase)))
            throw new CryptographicException("Untrusted GitHub channel configuration");
        http = new HttpClient(transport ?? new HttpClientHandler { AllowAutoRedirect = false })
        { Timeout = TimeSpan.FromMinutes(30), MaxResponseContentBufferSize = 65536 };
        discovery = new GitHubDiscovery(http, this.stallTimeout, discoveryCachePath, discoveryClock);
    }

    public const string TestLocator = "https://api.github.com/repos/afonasev/star-tournament/releases";
    public const string ProductionBase = "https://github.com/afonasev/star-tournament/releases/latest/download/";
    string ManifestName => $"latest-{config.Platform}-{config.Architecture}.json";
    string ModernManifestName => $"latest-v2-{config.Platform}-{config.Architecture}.json";

    public void Admit(string envelope)
    {
        // Clear prior admission before parsing any new, potentially rejected descriptor.
        Envelope = null; Asset = null; PublicationDate = null; packageUri = null; requirements = null;
        if (envelope.Length > 65536) throw new CryptographicException("Release metadata exceeds limit");
        using var outer = JsonDocument.Parse(envelope);
        var payload = Convert.FromBase64String(outer.RootElement.GetProperty("payload").GetString()!);
        var signature = Convert.FromBase64String(outer.RootElement.GetProperty("signature").GetString()!);
        using var rsa = RSA.Create(); rsa.ImportFromPem(key);
        if (!rsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
            throw new CryptographicException("Invalid release signature");
        using var doc = JsonDocument.Parse(payload); var release = doc.RootElement;
        if (release.GetProperty("platform").GetString() != config.Platform ||
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
        var parsedRequirements = ReleaseCompatibility.Parse(release, config.Platform);
        packageUri = authenticatedUrl; requirements = parsedRequirements;
        Envelope = envelope; Asset = asset; PublicationDate = date;
    }

    public void RequireCompatibleTarget()
    {
        if (requirements == null || Asset == null) throw new CryptographicException("No authenticated target");
        ReleaseCompatibility.Require(requirements, config.Platform, readerProtocol, hostProbe);
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
            HttpResponseMessage response;
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                deadline.CancelAfter(stallTimeout);
                try { response = await http.GetAsync(target, HttpCompletionOption.ResponseHeadersRead, deadline.Token); }
                catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new TimeoutException("Update response headers stalled"); }
            }
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
        while ((count = await ReadWithDeadline(input, buffer, token)) > 0)
        {
            if (output.Length + count > limit) throw new CryptographicException("Metadata exceeds limit");
            output.Write(buffer, 0, count);
        }
        return System.Text.Encoding.UTF8.GetString(output.ToArray());
    }

    async Task<List<(Uri Uri, string? Version, HashSet<string>? Assets)>> Discover(CancellationToken token)
    {
        if (config.UpdateChannel == null) return new() { (Remote("latest.json"), null, null) }; // Legacy broker/config only.
        var candidates = new Dictionary<SemanticVersion, (Uri Uri, string? Version, HashSet<string>? Assets)>();
        Uri? next = new(TestLocator + "?per_page=100&page=1"); var visited = new HashSet<string>(StringComparer.Ordinal);
        for (int page = 0; next != null; page++)
        {
            if (page >= 10 || !visited.Add(next.AbsoluteUri)) throw new InvalidOperationException("Release discovery exceeds pagination limit or repeats a page");
            var result = await discovery.Get(next, token);
            using var doc = JsonDocument.Parse(result.Body);
            foreach (var release in doc.RootElement.EnumerateArray())
            {
                bool test = config.UpdateChannel == "test";
                if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean() != test) continue;
                var tag = release.GetProperty("tag_name").GetString() ?? "";
                if (!tag.StartsWith('v') || !SemanticVersion.TryParse(tag[1..], out var version) || version.IsPrerelease != test || tag != "v" + version) continue;
                var names = release.GetProperty("assets").EnumerateArray().Select(x => x.GetProperty("name").GetString() ?? "").ToHashSet(StringComparer.Ordinal);
                bool modernFamily = names.Contains("latest-v2-win-x64.json") && names.Contains("latest-v2-osx-arm64.json");
                bool modern = modernFamily && names.Contains(ModernManifestName);
                bool legacy = !modernFamily && names.Contains(ManifestName) && names.Contains("latest-win-x64.json") && names.Contains("latest-osx-arm64.json");
                if ((!modern && !legacy) ||
                    !names.Contains($"Star-Tournament-{version}-Windows-x64-Setup.exe") ||
                    (!names.Contains($"Star-Tournament-{version}-macOS-arm64.pkg") && !names.Contains($"Star-Tournament-{version}-macOS-arm64.dmg")) ||
                    names.Count(n => n.EndsWith("-full.nupkg", StringComparison.Ordinal)) < 2) continue;
                // Locator data cannot assign a different repository or target URL.
                var uri = new Uri($"https://github.com/afonasev/star-tournament/releases/download/{tag}/{(modern ? ModernManifestName : ManifestName)}");
                if (!candidates.TryAdd(version, (uri, version.ToString(), names)) && !candidates[version].Assets!.SetEquals(names))
                    throw new InvalidDataException("Conflicting release version in discovery snapshot");
            }
            next = result.Next;
        }
        return candidates.Count == 0 ? throw new InvalidOperationException("No complete release for this channel") : candidates.OrderByDescending(p => p.Key).Select(p => p.Value).ToList();
    }

    public async Task<VelopackAssetFeed> GetReleaseFeed(IVelopackLogger logger, string? appId, string channel,
        Guid? stagingId = null, VelopackAsset? latestLocalRelease = null)
    {
        Envelope = null; Asset = null; PublicationDate = null; packageUri = null; requirements = null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(CheckCancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var locations = await Discover(timeout.Token);
        ReleaseCompatibilityException? incompatible = null;
        foreach (var location in locations)
        {
            var envelope = await ReadBounded(location.Uri, 65536, config.UpdateChannel != null, timeout.Token);
            timeout.Token.ThrowIfCancellationRequested();
            try
            {
                Admit(envelope);
                if (location.Version != null && (Asset!.Version.ToString() != location.Version || !location.Assets!.Contains(Asset.FileName)))
                    throw new CryptographicException("Release discovery version or full asset mismatch");
                RequireCompatibleTarget();
                return new VelopackAssetFeed { Assets = new[] { Asset! } };
            }
            catch (ReleaseCompatibilityException error)
            {
                incompatible = error;
                Envelope = null; Asset = null; PublicationDate = null; packageUri = null; requirements = null;
            }
            catch
            {
                Envelope = null; Asset = null; PublicationDate = null; packageUri = null; requirements = null;
                throw;
            }
        }
        throw incompatible ?? new ReleaseCompatibilityException("Совместимого обновления нет.");
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
        if (requirements is { UserDataPolicy: not "legacy-preserved-v1", PayloadFormat: ReleaseCompatibility.PayloadFormat })
        {
            // The signed descriptor must describe the actual atomic launcher/Player config.
            stream.Position = 0;
            try
            {
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
                var entries = archive.Entries.Where(e => e.Name == "release.json").ToArray();
                if (entries.Length != 1 || entries[0].Length > 65536) throw new CryptographicException("Unique bounded payload config required");
                using var input = entries[0].Open(); using var content = new MemoryStream();
                var buffer = new byte[8192]; int count;
                while ((count = await input.ReadAsync(buffer, token)) > 0)
                {
                    if (content.Length + count > 65536) throw new CryptographicException("Payload config exceeds limit");
                    content.Write(buffer, 0, count);
                }
                var target = JsonSerializer.Deserialize<LauncherConfig>(content.ToArray(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (target == null || target.AppId != config.AppId || target.Version != asset.Version.ToString() || target.Platform != config.Platform ||
                    target.Architecture != config.Architecture || target.UpdateChannel != config.UpdateChannel || target.PublicationDate != PublicationDate || target.Compatibility != requirements)
                    throw new CryptographicException("Payload config differs from signed target requirements");
            }
            catch (Exception error) when (error is InvalidDataException or JsonException)
            { throw new CryptographicException("Invalid authenticated payload config", error); }
        }
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
                while ((count = await ReadWithDeadline(input, buffer, cancelToken)) > 0)
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

    async Task<int> ReadWithDeadline(Stream input, byte[] buffer, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(stallTimeout);
        try { return await input.ReadAsync(buffer.AsMemory(), deadline.Token); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new TimeoutException("Update download stalled"); }
    }

    public void Dispose() => http.Dispose();
}
