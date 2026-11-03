using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using StarTournament.Distribution;

static class ReleaseCompatibilityChecks
{
    sealed class Transport(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(send(request)); }
    public static async Task Run()
    {
        int passed = 0;
        void Check(bool value, string label) { if (!value) throw new Exception(label); passed++; Console.WriteLine("PASS " + label); }
        async Task Reject<T>(Func<Task> action, string label) where T : Exception
        { try { await action(); } catch (T) { Check(true, label); return; } throw new Exception("Not rejected: " + label); }
        using var rsa = RSA.Create(3072);
        var config = new LauncherConfig("tech.test", "osx", "arm64", "1.0.0", "2026-10-17", AuthenticatedSource.ProductionBase, "game/Player", "production");
        var requirements = new ReleaseRequirements("12.0", 2, ReleaseCompatibility.PayloadFormat, ReleaseCompatibility.DataPolicy);
        var json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var windowsRequirements = new ReleaseRequirements("10.0.22631", 2, ReleaseCompatibility.PayloadFormat, ReleaseCompatibility.DataPolicy);
        await Reject<ReleaseCompatibilityException>(() => { ReleaseCompatibility.Require(windowsRequirements, "win", hostProbe: (_, minimum) => new Version(10, 0, 19043) >= minimum); return Task.CompletedTask; }, "Windows target may raise baseline build requirement");
        ReleaseCompatibility.Require(windowsRequirements, "win", hostProbe: (_, minimum) => new Version(10, 0, 22631) >= minimum);
        Check(true, "exact raised Windows target floor accepted");
        await Reject<CryptographicException>(() => { ReleaseCompatibility.Require(windowsRequirements with { MinimumOsVersion = "10.0.19042" }, "win"); return Task.CompletedTask; }, "target cannot underdeclare Windows baseline");
        var wire = File.ReadAllBytes("distribution/tests/fixtures/release-compatibility.json");
        var wireEnvelope = JsonSerializer.Serialize(new { payload = Convert.ToBase64String(wire), signature = Convert.ToBase64String(rsa.SignData(wire, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)) });
        using (var wireReader = new AuthenticatedSource(config with { Platform = "win", Architecture = "x64" }, rsa.ExportSubjectPublicKeyInfoPem(), hostProbe: (_, minimum) => new Version(10, 0, 19043) >= minimum))
        { wireReader.Admit(wireEnvelope); wireReader.RequireCompatibleTarget(); Check(true, "actual Python producer wire fixture admitted by C# signature/compatibility reader"); }
        byte[] Package(ReleaseRequirements? actual = null, string version = "1.1.0", bool duplicate = false, int bootstrapProtocol = 0)
        {
            using var bytes = new MemoryStream();
            using (var archive = new ZipArchive(bytes, ZipArchiveMode.Create, true))
            {
                void Entry(string path) { using var writer = new StreamWriter(archive.CreateEntry(path).Open()); writer.Write(JsonSerializer.Serialize(config with { Version = version, Compatibility = actual ?? requirements, BootstrapProtocol = bootstrapProtocol }, json)); }
                Entry("lib/osx/release.json"); if (duplicate) Entry("other/release.json");
            }
            return bytes.ToArray();
        }
        string Signed(byte[] bytes, ReleaseRequirements? target = null, int schema = 2, string version = "1.1.0", bool include = true, ReleaseRequirements? bootstrapRequirements = null)
        {
            var data = new Dictionary<string, object?> { ["schema"] = schema, ["platform"] = config.Platform, ["architecture"] = config.Architecture,
                ["channel"] = config.UpdateChannel, ["version"] = version, ["publicationDate"] = config.PublicationDate,
                ["downloadUrl"] = $"https://github.com/afonasev/star-tournament/releases/download/v{version}/fixture-{version}-full.nupkg",
                ["feed"] = new { Assets = new[] { new { PackageId = config.AppId, Version = version, Type = "Full", FileName = $"fixture-{version}-full.nupkg", SHA256 = Convert.ToHexString(SHA256.HashData(bytes)), Size = bytes.Length } } } };
            if (include) data["compatibility"] = target ?? requirements;
            if (bootstrapRequirements != null) data["bootstrapCompatibility"] = bootstrapRequirements;
            var payload = JsonSerializer.SerializeToUtf8Bytes(data, json);
            return JsonSerializer.Serialize(new { payload = Convert.ToBase64String(payload), signature = Convert.ToBase64String(rsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)) });
        }
        AuthenticatedSource Source(HttpMessageHandler? transport = null, int reader = 2, Version? host = null, bool bootstrap = false) => new(config, rsa.ExportSubjectPublicKeyInfoPem(), transport,
            readerProtocol: reader, hostProbe: (platform, minimum) => platform == "osx" && (host ?? new Version(15, 0)) >= minimum, bootstrap: bootstrap);
        var root = Path.Combine(Path.GetTempPath(), "star-target-compat-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var bytes = Package(); var file = Path.Combine(root, "payload.nupkg"); File.WriteAllBytes(file, bytes);
            using (var source = Source())
            {
                source.Admit(Signed(bytes)); source.RequireCompatibleTarget(); await source.VerifyPackage(file, source.Asset!);
                Check(true, "schema2 signature/capabilities/actual payload config agree");
                source.Admit(Signed(bytes, schema: 1, include: false)); source.RequireCompatibleTarget();
                Check(true, "historical schema1 retains fixed baseline");
                await Reject<CryptographicException>(() => { source.Admit(Signed(bytes, schema: 1)); return Task.CompletedTask; }, "schema1 cannot smuggle compatibility override");
                Check(source.Asset == null && source.Envelope == null, "invalid admission clears prior target");
                await Reject<CryptographicException>(() => { source.Admit(Signed(bytes, include: false)); return Task.CompletedTask; }, "schema2 requires explicit requirements");
                foreach (var bad in new[] { requirements with { MinimumOsVersion = "11.0" }, requirements with { MinimumOsVersion = "012.0" }, requirements with { MinimumUpdaterProtocol = 1 }, requirements with { UserDataPolicy = "legacy-preserved-v1" } })
                    await Reject<CryptographicException>(() => { source.Admit(Signed(bytes, bad)); return Task.CompletedTask; }, "invalid descriptor " + bad);
                foreach (var unsupported in new[] { requirements with { MinimumOsVersion = "16.0" }, requirements with { MinimumUpdaterProtocol = 3 }, requirements with { PayloadFormat = "future" }, requirements with { UserDataPolicy = "destructive-v2" } })
                {
                    source.Admit(Signed(bytes, unsupported));
                    await Reject<ReleaseCompatibilityException>(() => { source.RequireCompatibleTarget(); return Task.CompletedTask; }, "signed incompatible target " + unsupported);
                }
            }
            using (var old = Source(reader: 1))
            { old.Admit(Signed(bytes)); await Reject<ReleaseCompatibilityException>(() => { old.RequireCompatibleTarget(); return Task.CompletedTask; }, "actual reader capability1 cannot admit schema2 activation"); }
            using (var exact = Source(host: new Version(12, 0)))
            { exact.Admit(Signed(bytes)); exact.RequireCompatibleTarget(); Check(true, "exact minimum OS accepted"); }
            foreach (var bad in new[] { Package(requirements with { MinimumOsVersion = "16.0" }), Package(version: "9.9.9"), Package(duplicate: true), "not a zip"u8.ToArray() })
            {
                File.WriteAllBytes(file, bad); using var source = Source(); source.Admit(Signed(bad)); source.RequireCompatibleTarget();
                await Reject<CryptographicException>(() => source.VerifyPackage(file, source.Asset!), "signed bytes must contain matching unique atomic payload config");
            }
            foreach (var mode in new[] { "os", "reader", "policy", "schema", "all", "signature", "duplicate", "conflict" })
            {
                int api = 0, targets = 0;
                object Row(string version, bool conflict = false) => new { tag_name = "v" + version, draft = false, prerelease = false, assets = new[] { "latest-osx-arm64.json", "latest-win-x64.json", $"Star-Tournament-{version}-Windows-x64-Setup.exe", $"Star-Tournament-{version}-macOS-arm64.pkg", $"fixture-{version}-full.nupkg", "other-full.nupkg" }.Concat(conflict ? new[] { "foreign-extra" } : Array.Empty<string>()).Select(name => new { name }) };
                using var source = Source(new Transport(request =>
                {
                    string content;
                    if (request.RequestUri!.Host == "api.github.com") { api++; content = JsonSerializer.Serialize(mode is "duplicate" or "conflict" ? new[] { Row("1.1.0"), Row("1.1.0", mode == "conflict") } : new[] { Row("2.0.0"), Row("1.1.0") }); }
                    else
                    {
                        targets++; string version = request.RequestUri.Segments[^2].Trim('/')[1..];
                        var target = version == "1.1.0" && mode != "all" ? requirements : mode switch
                        { "reader" => requirements with { MinimumUpdaterProtocol = 3 }, "policy" => requirements with { UserDataPolicy = "future" }, _ => requirements with { MinimumOsVersion = "16.0" } };
                        content = Signed(bytes, target, schema: mode == "schema" && version == "2.0.0" ? 3 : 2, version: version);
                        if (mode == "signature") content = content.Replace("payload\":\"", "payload\":\"AAAA");
                    }
                    return new(HttpStatusCode.OK) { Content = new StringContent(content) };
                }));
                if (mode == "conflict")
                { await Reject<InvalidDataException>(() => source.GetReleaseFeed(null!, config.AppId, ""), "conflicting duplicate locator cannot choose a target"); Check(targets == 0, "conflicting locator fails before manifest request"); }
                else if (mode == "duplicate")
                { await source.GetReleaseFeed(null!, config.AppId, ""); Check(source.Asset!.Version.ToString() == "1.1.0" && targets == 1, "identical duplicate locator is coalesced"); }
                else if (mode == "all")
                { await Reject<ReleaseCompatibilityException>(() => source.GetReleaseFeed(null!, config.AppId, ""), "all signed targets incompatible gives explicit error"); Check(source.Asset == null && source.Envelope == null, "no stale incompatible target retained"); }
                else if (mode == "signature")
                { await Reject<CryptographicException>(() => source.GetReleaseFeed(null!, config.AppId, ""), "signature failure cannot masquerade as compatible fallback"); Check(targets == 1, "trust failure stops before lower target"); }
                else
                { await source.GetReleaseFeed(null!, config.AppId, ""); Check(source.Asset!.Version.ToString() == "1.1.0" && targets == 2, "newest compatible signed target selected: " + mode); }
                Check(api == 1, "one frozen discovery API snapshot: " + mode);
            }
            var incompatible = requirements with { MinimumUpdaterProtocol = 3, PayloadFormat = "launcher-generation3" };
            var fullReplacement = Package(incompatible, bootstrapProtocol: 1);
            var fullPath = Path.Combine(root, "bootstrap-full.nupkg"); File.WriteAllBytes(fullPath, fullReplacement);
            var bootstrapEnvelope = Signed(fullReplacement, incompatible, bootstrapRequirements: requirements);
            using (var ordinary = Source())
            {
                ordinary.Admit(bootstrapEnvelope);
                await Reject<ReleaseCompatibilityException>(() => { ordinary.RequireCompatibleTarget(); return Task.CompletedTask; }, "ordinary reader cannot bypass incompatible payload via bootstrap declaration");
            }
            using (var independent = Source(bootstrap: true))
            {
                independent.Admit(bootstrapEnvelope); independent.RequireCompatibleTarget(); await independent.VerifyPackage(fullPath, independent.Asset!);
                Check(true, "independent transport authenticates full replacement for incompatible launcher reader/format");
                await Reject<CryptographicException>(() => { independent.Admit(Signed(fullReplacement, incompatible, bootstrapRequirements: requirements with { MinimumOsVersion = "13.0" })); return Task.CompletedTask; }, "bootstrap OS floor cannot differ from target");
                await Reject<CryptographicException>(() => { independent.Admit(Signed(fullReplacement, incompatible, bootstrapRequirements: requirements with { UserDataPolicy = "unknown-data" })); return Task.CompletedTask; }, "bootstrap never bypasses data preservation");
                await Reject<CryptographicException>(() => { independent.Admit(Signed(fullReplacement, incompatible, bootstrapRequirements: requirements with { PayloadFormat = "unknown-outer" })); return Task.CompletedTask; }, "unknown outer extraction format is rejected");
                await Reject<CryptographicException>(() => { independent.Admit(Signed(fullReplacement, incompatible, bootstrapRequirements: requirements with { MinimumUpdaterProtocol = 99 })); return Task.CompletedTask; }, "unsupported bootstrap protocol fails closed");
                var missingEntry = Package(incompatible);
                File.WriteAllBytes(fullPath, missingEntry);
                independent.Admit(Signed(missingEntry, incompatible, bootstrapRequirements: requirements));
                await Reject<CryptographicException>(() => independent.VerifyPackage(fullPath, independent.Asset!), "full replacement must bind independent entry capability");
            }
            var maintenance = Path.Combine(root, "maintenance"); Directory.CreateDirectory(maintenance);
            foreach (var (protocol, reader, expected) in new[] { (1, 0, 1), (1, 1, 1), (1, 2, 2), (2, 0, 1), (2, 1, 1), (2, 2, 2), (2, 99, 2) })
            {
                var values = new Dictionary<string, int> { ["protocol"] = protocol }; if (reader != 0) values["metadataReader"] = reader;
                File.WriteAllText(Path.Combine(maintenance, "maintenance.json"), JsonSerializer.Serialize(values));
                Check(WindowsInstallation.MetadataReader(root) == expected, $"protected broker protocol{protocol}/reader{reader} => effective{expected}");
            }
            File.WriteAllText(Path.Combine(maintenance, "maintenance.json"), "{\"protocol\":2,\"metadataReader\":0}");
            await Reject<IOException>(() => { WindowsInstallation.MetadataReader(root); return Task.CompletedTask; }, "invalid capability cannot become supported reader");
            foreach(var protocol in new[]{0,3})
            {
                File.WriteAllText(Path.Combine(maintenance,"maintenance.json"),JsonSerializer.Serialize(new{protocol,metadataReader=2}));
                await Reject<IOException>(()=>{WindowsInstallation.MetadataReader(root);return Task.CompletedTask;},"unknown playback protocol cannot advertise a supported reader");
            }
        }
        finally { Directory.Delete(root, true); }
        Console.WriteLine($"Release compatibility: {passed} passed");
    }
}
