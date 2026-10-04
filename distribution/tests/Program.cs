using System.Security.Cryptography;
using System.Text.Json;
using StarTournament.Distribution;
using Velopack.Logging;

using var rsa = RSA.Create(3072);
var config = new LauncherConfig("tech.test", "osx", "arm64", "1.0.0", "2026-10-04", "https://fixture.invalid/", "game/Player");
var bytes = "authenticated package fixture"u8.ToArray();
string Signed(string platform = "osx", string architecture = "arm64", string filename = "test-1.1.0-full.nupkg", string? sha = null)
{
    var payload = JsonSerializer.SerializeToUtf8Bytes(new { schema = 1, platform, architecture,
        version = "1.1.0", publicationDate = "2026-10-04", feed = new { Assets = new[] { new {
            PackageId = "tech.test", Version = "1.1.0", Type = "Full", FileName = filename,
            SHA256 = sha ?? Convert.ToHexString(SHA256.HashData(bytes)), Size = bytes.Length } } } });
    return JsonSerializer.Serialize(new { payload = Convert.ToBase64String(payload),
        signature = Convert.ToBase64String(rsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)) });
}
int passed = 0;
async Task Reject(string label, Func<Task> test)
{
    try { await test(); } catch (CryptographicException) { passed++; Console.WriteLine("PASS " + label); return; }
    throw new Exception("Did not reject: " + label);
}
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
    source.Admit(Signed());
    var file = Path.Combine(root, "cached.nupkg"); await File.WriteAllBytesAsync(file, bytes);
    await source.VerifyPackage(file, source.Asset!); passed++;
    await File.WriteAllBytesAsync(file, bytes.Select(b => (byte)(b ^ 1)).ToArray());
    await Reject("tampered staged cache", () => source.VerifyPackage(file, source.Asset!));
    await File.WriteAllBytesAsync(file, bytes[..^1]);
    await Reject("interrupted package", () => source.VerifyPackage(file, source.Asset!));
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
