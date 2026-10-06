using System.Security.Cryptography;
using System.Text.Json;
using StarTournament.Distribution;
using Velopack;
using Velopack.Logging;

using var rsa = RSA.Create(3072);
var config = new LauncherConfig("tech.test", "osx", "arm64", "1.0.0", "2026-10-04", "https://fixture.invalid/", "game/Player");
var bytes = "authenticated package fixture"u8.ToArray();
string Signed(string platform = "osx", string architecture = "arm64", string filename = "test-1.1.0-full.nupkg", string? sha = null, string? downloadUrl = null, string version = "1.1.0", string? channel = null)
{
    var payload = JsonSerializer.SerializeToUtf8Bytes(new { schema = 1, platform, architecture, channel,
        version, publicationDate = "2026-10-04", downloadUrl, feed = new { Assets = new[] { new {
            PackageId = "tech.test", Version = version, Type = "Full", FileName = filename,
            SHA256 = sha ?? Convert.ToHexString(SHA256.HashData(bytes)), Size = bytes.Length } } } });
    return JsonSerializer.Serialize(new { payload = Convert.ToBase64String(payload),
        signature = Convert.ToBase64String(rsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)) });
}
// Live transport probe uses the production verifier, with no SDK auto-activation.
if (args.Length == 3 && args[0] == "--live")
{
    var liveConfig = JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(args[1]), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    using var live = new AuthenticatedSource(liveConfig, File.ReadAllText(Path.Combine(Path.GetDirectoryName(args[1])!, "update-public.pem")));
    await live.GetReleaseFeed(null!, liveConfig.AppId, "");
    await live.DownloadReleaseEntry(null!, live.Asset!, args[2], _ => { });
    Console.WriteLine(JsonSerializer.Serialize(new { version = live.Asset!.Version.ToString(), sha256 = live.Asset.SHA256, size = live.Asset.Size, channel = liveConfig.UpdateChannel }));
    return;
}
int passed = 0;
async Task Reject(string label, Func<Task> test)
{
    try { await test(); } catch (CryptographicException) { passed++; Console.WriteLine("PASS " + label); return; }
    throw new Exception("Did not reject: " + label);
}
var production = config with { UpdateChannel = "production", FeedBase = AuthenticatedSource.ProductionBase };
var test = config with { UpdateChannel = "test", FeedBase = AuthenticatedSource.TestLocator };
await Reject("GitHub config wrong feed", () => { using var bad = new AuthenticatedSource(test with { FeedBase = "https://evil.example/" }, rsa.ExportSubjectPublicKeyInfoPem()); return Task.CompletedTask; });
await Reject("unknown channel", () => { using var bad = new AuthenticatedSource(test with { UpdateChannel = "other" }, rsa.ExportSubjectPublicKeyInfoPem()); return Task.CompletedTask; });
const string prodUrl = "https://github.com/afonasev/star-tournament/releases/download/v1.1.0/test-1.1.0-full.nupkg";
using (var prod = new AuthenticatedSource(production, rsa.ExportSubjectPublicKeyInfoPem()))
{
    prod.Admit(Signed(downloadUrl: prodUrl, channel: "production")); passed++;
    await Reject("missing signed channel", () => { prod.Admit(Signed(downloadUrl: prodUrl)); return Task.CompletedTask; });
    await Reject("wrong signed channel", () => { prod.Admit(Signed(downloadUrl: prodUrl, channel: "test")); return Task.CompletedTask; });
    await Reject("missing pinned download URL", () => { prod.Admit(Signed(channel: "production")); return Task.CompletedTask; });
    if (prod.Asset != null || prod.Envelope != null) throw new Exception("Rejected metadata retained stale admission"); passed++;
}
var testEnvelope = Signed(version: "1.1.0-test.1", channel: "test", downloadUrl: prodUrl.Replace("v1.1.0/", "v1.1.0-test.1/"));
foreach (var ch in new[] { production, test })
{
    var envelope = ch.UpdateChannel == "test" ? testEnvelope : Signed(downloadUrl: prodUrl, channel: "production");
    using var transport = new MetadataTransport(envelope);
    using var github = new AuthenticatedSource(ch, rsa.ExportSubjectPublicKeyInfoPem(), transport);
    await github.GetReleaseFeed(null!, ch.AppId, "");
    if (github.Asset!.Version.ToString() != (ch.UpdateChannel == "test" ? "1.1.0-test.1" : "1.1.0")) throw new Exception("Channel discovery version");
    if (transport.Hosts.Any(x => x is not ("github.com" or "api.github.com" or "release-assets.githubusercontent.com"))) throw new Exception("Non-GitHub request");
    passed++;
}
foreach (var failure in new[] { "oversize", "foreign", "downgrade", "loop", "wrongrepo", "wrongversion", "signature" })
{
    using var github = new AuthenticatedSource(production, rsa.ExportSubjectPublicKeyInfoPem(), new MetadataTransport(Signed(downloadUrl: prodUrl, channel: "production"), failure));
    await Reject("metadata " + failure, () => github.GetReleaseFeed(null!, production.AppId, ""));
}
foreach (var failure in new[] { "offline", "rate-limit", "empty", "pagination" })
{
    using var github = new AuthenticatedSource(test, rsa.ExportSubjectPublicKeyInfoPem(), new MetadataTransport(testEnvelope, failure));
    try { await github.GetReleaseFeed(null!, test.AppId, ""); throw new Exception("Discovery failure admitted"); }
    catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException) { passed++; }
}
using (var mismatch = new AuthenticatedSource(test, rsa.ExportSubjectPublicKeyInfoPem(), new MetadataTransport(testEnvelope.Replace("never", "used"), "tag-mismatch")))
    await Reject("API tag signed version mismatch", () => mismatch.GetReleaseFeed(null!, test.AppId, ""));
using var source = new AuthenticatedSource(config, rsa.ExportSubjectPublicKeyInfoPem());
source.Admit(Signed());
if (source.PublicationDate != "2026-10-04" || source.Asset!.Version.ToString() != "1.1.0") throw new Exception("Release metadata");
passed++;
await Reject("wrong platform", () => { source.Admit(Signed(platform: "win")); return Task.CompletedTask; });
await Reject("wrong architecture", () => { source.Admit(Signed(architecture: "x64")); return Task.CompletedTask; });
await Reject("path traversal", () => { source.Admit(Signed(filename: "../package.nupkg")); return Task.CompletedTask; });
await Reject("external URL", () => { source.Admit(Signed(filename: "https://other/package.nupkg")); return Task.CompletedTask; });
await Reject("missing SHA256", () => { source.Admit(Signed(sha: "")); return Task.CompletedTask; });
using (var wrongKey = RSA.Create(3072))
using (var wrongSource = new AuthenticatedSource(config, wrongKey.ExportSubjectPublicKeyInfoPem()))
    await Reject("untrusted signing key", () => { wrongSource.Admit(Signed()); return Task.CompletedTask; });
var root = Path.Combine(Path.GetTempPath(), "star-update-contracts-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var legacyRoot = Path.Combine(root, "legacy-probe"); Directory.CreateDirectory(legacyRoot);
    Directory.CreateDirectory(Path.Combine(legacyRoot, "packages"));
    await File.WriteAllTextAsync(Path.Combine(legacyRoot, "Update.exe"), "cache runtime stub");
    if (LegacyInstallationProbe.IsInstalled(legacyRoot)) throw new Exception("Package-cache Update.exe was treated as a legacy install");
    passed++; Console.WriteLine("PASS cache-only Update.exe is not a legacy install");
    Directory.CreateDirectory(Path.Combine(legacyRoot, "current"));
    await File.WriteAllTextAsync(Path.Combine(legacyRoot, "current", "release.json"), JsonSerializer.Serialize(new {
        appId = "tech.afonasev.star-tournament.win-x64", platform = "win", architecture = "x64", version = "1.2.3" }));
    if (!LegacyInstallationProbe.IsInstalled(legacyRoot)) throw new Exception("Valid legacy release metadata was not detected");
    passed++; Console.WriteLine("PASS valid legacy release metadata is detected");
    File.Delete(Path.Combine(legacyRoot, "current", "release.json"));
    await File.WriteAllTextAsync(Path.Combine(legacyRoot, "current", "StarTournamentLauncher.exe"), "launcher");
    if (!LegacyInstallationProbe.IsInstalled(legacyRoot)) throw new Exception("Actual legacy launcher was not detected");
    passed++; Console.WriteLine("PASS actual legacy launcher is detected");
    if (!LegacyInstallationProbe.IsInstalled(legacyRoot, hasUninstallEntry: true)) throw new Exception("Legacy uninstall entry was not detected");
    passed++;

    source.Admit(Signed());
    var file = Path.Combine(root, "cached.nupkg"); await File.WriteAllBytesAsync(file, bytes);
    await source.VerifyPackage(file, source.Asset!); passed++;
    await File.WriteAllBytesAsync(file, bytes.Select(b => (byte)(b ^ 1)).ToArray());
    await Reject("tampered staged cache", () => source.VerifyPackage(file, source.Asset!));
    await File.WriteAllBytesAsync(file, bytes[..^1]);
    await Reject("interrupted package", () => source.VerifyPackage(file, source.Asset!));
    var winConfig = config with { Platform = "win", Architecture = "x64" };
    using var winSource = new AuthenticatedSource(winConfig, rsa.ExportSubjectPublicKeyInfoPem());
    var winPayload = JsonSerializer.SerializeToUtf8Bytes(new { schema = 1, platform = "win", architecture = "x64",
        version = "1.1.0", publicationDate = "2026-10-04", feed = new { Assets = new[] { new {
            PackageId = "tech.test", Version = "1.1.0", Type = "Full", FileName = "test-1.1.0-full.nupkg",
            SHA256 = Convert.ToHexString(SHA256.HashData(bytes)), Size = bytes.Length } } } });
    var winEnvelope = JsonSerializer.Serialize(new { payload = Convert.ToBase64String(winPayload),
        signature = Convert.ToBase64String(rsa.SignData(winPayload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)) });
    winSource.Admit(winEnvelope);
    var validSourcePackage = Path.Combine(root, "valid.nupkg"); await File.WriteAllBytesAsync(validSourcePackage, bytes);
    var protectedDir = Path.Combine(root, "protected-stage"); Directory.CreateDirectory(protectedDir);
    var copied = await ProtectedPackage.CopyAndVerifyAsync(winSource, winEnvelope, winSource.Asset!, validSourcePackage, protectedDir);
    if (Path.GetFileName(copied.PackagePath) != "test-1.1.0-full.nupkg" || new FileInfo(copied.PackagePath).Length != bytes.Length)
        throw new Exception("Protected package copy contract");
    passed++; Console.WriteLine("PASS copied signed package re-verification");
    using var parsedWinEnvelope = JsonDocument.Parse(winEnvelope);
    var tamperedSignature = Convert.FromBase64String(parsedWinEnvelope.RootElement.GetProperty("signature").GetString()!);
    tamperedSignature[0] ^= 1;
    var tamperedEnvelope = JsonSerializer.Serialize(new { payload = parsedWinEnvelope.RootElement.GetProperty("payload").GetString(),
        signature = Convert.ToBase64String(tamperedSignature) });
    var badCopy = Path.Combine(root, "bad-copy"); Directory.CreateDirectory(badCopy);
    await Reject("copied package metadata signature", () => ProtectedPackage.CopyAndVerifyAsync(winSource, tamperedEnvelope,
        winSource.Asset!, validSourcePackage, badCopy));
    winSource.Admit(winEnvelope); // Failed metadata cleared the previous admission.
    var corruptSourcePackage = Path.Combine(root, "corrupt-source.nupkg");
    await File.WriteAllBytesAsync(corruptSourcePackage, bytes.Select(b => (byte)(b ^ 1)).ToArray());
    var corruptCopy = Path.Combine(root, "corrupt-copy"); Directory.CreateDirectory(corruptCopy);
    await Reject("copied package digest mismatch", () => ProtectedPackage.CopyAndVerifyAsync(winSource, winEnvelope,
        winSource.Asset!, corruptSourcePackage, corruptCopy));
    const string githubUrl = "https://github.com/afonasev/star-tournament/releases/download/v1.1.0/test-1.1.0-full.nupkg";
    await Reject("signed URL wrong repository", () => { source.Admit(Signed(downloadUrl: githubUrl.Replace("afonasev", "other"))); return Task.CompletedTask; });
    await Reject("signed URL HTTP downgrade", () => { source.Admit(Signed(downloadUrl: githubUrl.Replace("https:", "http:"))); return Task.CompletedTask; });
    await Reject("signed URL mismatched version", () => { source.Admit(Signed(downloadUrl: githubUrl.Replace("v1.1.0", "v2.0.0"))); return Task.CompletedTask; });
    var githubDestination = Path.Combine(root, "github.nupkg");
    using (var github = new AuthenticatedSource(config, rsa.ExportSubjectPublicKeyInfoPem(),
        new RedirectTransport(bytes, "https://release-assets.githubusercontent.com/fixture/package")))
    {
        github.Admit(Signed(downloadUrl: githubUrl));
        await github.DownloadReleaseEntry(null!, github.Asset!, githubDestination, _ => { });
        await github.VerifyPackage(githubDestination, github.Asset!); passed++;
    }
    foreach (var unsafeLocation in new[] { "http://release-assets.githubusercontent.com/package", "https://other.example/package", githubUrl })
    {
        using var redirected = new AuthenticatedSource(config, rsa.ExportSubjectPublicKeyInfoPem(), new RedirectTransport(bytes, unsafeLocation));
        redirected.Admit(Signed(downloadUrl: githubUrl));
        await Reject("redirect host/downgrade/loop", () => redirected.DownloadReleaseEntry(null!, redirected.Asset!, githubDestination + ".bad", _ => { }));
        if (File.Exists(githubDestination + ".bad")) throw new Exception("Untrusted redirect admitted");
    }
    var destination = Path.Combine(root, "sdk.partial");
    using var download = new AuthenticatedSource(config, rsa.ExportSubjectPublicKeyInfoPem(), new FixtureTransport(bytes));
    download.Admit(Signed());
    await download.DownloadReleaseEntry(null!, download.Asset!, destination, _ => { });
    await download.VerifyPackage(destination, download.Asset!); passed++;
    File.Delete(destination);
    using var corrupt = new AuthenticatedSource(config, rsa.ExportSubjectPublicKeyInfoPem(), new FixtureTransport(bytes.Select(b => (byte)(b ^ 1)).ToArray()));
    corrupt.Admit(Signed());
    await Reject("quarantine before SDK admission", () => corrupt.DownloadReleaseEntry(null!, corrupt.Asset!, destination, _ => { }));
    if (File.Exists(destination) || File.Exists(destination + ".authenticated-download")) throw new Exception("Untrusted bytes admitted or retained");
    passed++;
    using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
    try { await download.DownloadReleaseEntry(null!, download.Asset!, destination, _ => { }, cancelled.Token); throw new Exception("Cancellation ignored"); }
    catch (OperationCanceledException) { passed++; }
    if (File.Exists(destination)) throw new Exception("Cancelled download admitted");
}
finally { Directory.Delete(root, true); }
Console.WriteLine($"PASS {passed} security contracts");

sealed class FixtureTransport(byte[] bytes) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
    }
}

sealed class RedirectTransport(byte[] bytes, string location) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (request.RequestUri!.Host == "release-assets.githubusercontent.com")
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.Redirect);
        response.Headers.Location = new Uri(location);
        return Task.FromResult(response);
    }
}

sealed class MetadataTransport(string envelope, string failure = "") : HttpMessageHandler
{
    public List<string> Hosts { get; } = new();
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var uri = request.RequestUri!; Hosts.Add(uri.Host);
        if (failure == "offline") throw new HttpRequestException("Offline fixture");
        if (failure == "rate-limit") return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.Forbidden));
        if (uri.Host == "api.github.com")
        {
            var version = failure == "tag-mismatch" ? "1.1.0-test.2" : "1.1.0-test.1";
            var release = new { tag_name = "v" + version, draft = false, prerelease = true, assets = new[] {
                new { name = "latest-win-x64.json" }, new { name = "latest-osx-arm64.json" },
                new { name = $"Star-Tournament-{version}-Windows-x64-Setup.exe" }, new { name = $"Star-Tournament-{version}-macOS-arm64.pkg" } } };
            var rows = failure == "empty" ? Array.Empty<object>() : failure == "pagination" ? Enumerable.Repeat<object>(release, 100).ToArray() : new object[] { release,
                new { tag_name = "v99.0.0-test.1", draft = false, prerelease = true, assets = new[] { new { name = "installer-only.exe" } } } };
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(rows)) });
        }
        if (uri.Host == "github.com")
        {
            var location = failure switch {
                "foreign" => "https://evil.example/manifest", "downgrade" => "http://release-assets.githubusercontent.com/fixture",
                "loop" => uri.ToString(), "wrongrepo" => "https://github.com/other/star-tournament/releases/download/v1.1.0/latest-osx-arm64.json",
                "wrongversion" => "https://github.com/afonasev/star-tournament/releases/download/v1.1.0-test.1/latest-osx-arm64.json",
                _ => uri.AbsolutePath.Contains("/latest/") ? "https://github.com/afonasev/star-tournament/releases/download/v1.1.0/latest-osx-arm64.json" : "https://release-assets.githubusercontent.com/fixture/manifest" };
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.Redirect); response.Headers.Location = new Uri(location); return Task.FromResult(response);
        }
        var body = failure == "oversize" ? new string('x',65537) : envelope;
        if (failure == "signature") { using var doc = JsonDocument.Parse(envelope); var sig = Convert.FromBase64String(doc.RootElement.GetProperty("signature").GetString()!); sig[0] ^= 1;
            body = JsonSerializer.Serialize(new { payload = doc.RootElement.GetProperty("payload").GetString(), signature = Convert.ToBase64String(sig) }); }
        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(body) });
    }
}
