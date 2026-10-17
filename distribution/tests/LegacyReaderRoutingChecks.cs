using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using StarTournament.Distribution;
using ProductionV1 = StarTournament.Distribution.ReleasedProductionV1;
using TestV1 = StarTournament.Distribution.ReleasedTestV1;

static class LegacyReaderRoutingChecks
{
    sealed class Transport(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(send(request)); }
    public static async Task Run()
    {
        int passed = 0;
        void Check(bool value, string label) { if (!value) throw new Exception(label); passed++; Console.WriteLine("PASS " + label); }
        async Task Reject<T>(Func<Task> action, string label) where T : Exception
        { try { await action(); } catch (T) { Check(true, label); return; } throw new Exception("Expected " + typeof(T).Name + ": " + label); }
        foreach(var (name,hash) in new[]{("ReleasedProductionV1","6913c81de58e98fb9011d48388cdb54f6427b877c351d32f9cca6537acdc5d72"),("ReleasedTestV1","94acf706c416a9671752a2221a8d94bef9297a9905d4fb17bd6d19e8a30869f0")})
        {
            var body=File.ReadAllText("distribution/tests/fixtures/legacy-reader/"+name+".cs").Replace("\r\n","\n").Replace("namespace StarTournament.Distribution."+name+";","namespace StarTournament.Distribution;");
            Check(Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(body))).ToLowerInvariant()==hash,"released source preserved except namespace: "+name);
        }
        using var rsa = RSA.Create(3072); string key = rsa.ExportSubjectPublicKeyInfoPem();
        string Signed(string version, string channel, int schema, string architecture = "arm64")
        {
            var value = new Dictionary<string, object?> { ["schema"] = schema, ["platform"] = "osx", ["architecture"] = architecture, ["channel"] = channel,
                ["version"] = version, ["publicationDate"] = "2026-10-17", ["downloadUrl"] = $"https://github.com/afonasev/star-tournament/releases/download/v{version}/fixture-{version}-full.nupkg",
                ["feed"] = new { Assets = new[] { new { PackageId = "tech.test", Version = version, Type = "Full", FileName = $"fixture-{version}-full.nupkg", SHA256 = new string('0',64), Size = 1 } } } };
            if (schema == 2) value["compatibility"] = new { minimumOsVersion = "12.0", minimumUpdaterProtocol = 2, payloadFormat = ReleaseCompatibility.PayloadFormat, userDataPolicy = ReleaseCompatibility.DataPolicy };
            var payload = JsonSerializer.SerializeToUtf8Bytes(value);
            return JsonSerializer.Serialize(new { payload = Convert.ToBase64String(payload), signature = Convert.ToBase64String(rsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)) });
        }
        foreach (var channel in new[] { "production", "test" })
        {
            string suffix = channel == "test" ? "-test.1" : "", bridge = "1.1.0" + suffix, future = "1.2.0" + suffix;
            string bridgeEnvelope = Signed(bridge, channel, 1), futureEnvelope = Signed(future, channel, 2);
            object Row(string version, bool modern, bool both = false, string? extra = null) => new { tag_name = "v" + version, draft = false, prerelease = channel == "test", assets = new[] {
                modern ? "latest-v2-win-x64.json" : "latest-win-x64.json", modern ? "latest-v2-osx-arm64.json" : "latest-osx-arm64.json",
                $"Star-Tournament-{version}-Windows-x64-Setup.exe", $"Star-Tournament-{version}-macOS-arm64.pkg", $"fixture-{version}-full.nupkg", "counterpart-full.nupkg" }.Concat(both?new[]{"latest-win-x64.json","latest-osx-arm64.json"}:Array.Empty<string>()).Concat(extra==null?Array.Empty<string>():new[]{extra}).Select(name => new { name }).ToArray() };
            int api = 0, legacy = 0, modernRequests = 0;
            HttpResponseMessage Route(HttpRequestMessage request)
            {
                var uri = request.RequestUri!; string body;
                if(uri.AbsolutePath.Contains("/releases/latest/download/"))
                {
                    var redirect=new HttpResponseMessage(HttpStatusCode.Redirect);
                    redirect.Headers.Location=new Uri($"https://github.com/afonasev/star-tournament/releases/download/v{future}/latest-osx-arm64.json");
                    return redirect; // Current production release carries the immutable older bridge alias.
                }
                if (uri.Host == "api.github.com") { api++; body = JsonSerializer.Serialize(new[] { Row(future, true, channel == "production"), Row(bridge, false) }); }
                else if (uri.AbsolutePath.Contains("latest-v2-")) { modernRequests++; body = futureEnvelope; }
                else { legacy++; body = bridgeEnvelope; }
                return new(HttpStatusCode.OK) { Content = new StringContent(body) };
            }
            string feed = channel == "production" ? AuthenticatedSource.ProductionBase : AuthenticatedSource.TestLocator;
            if (channel == "production")
            {
                var config = new ProductionV1.LauncherConfig("tech.test","osx","arm64","1.0.0","2026-10-17",feed,"game/Player",channel);
                using var old = new ProductionV1.AuthenticatedSource(config,key,new Transport(Route)); await old.GetReleaseFeed(null!,config.AppId,"");
                Check(old.Asset!.Version.ToString() == bridge && api == 0 && legacy == 1, "released production source reads frozen bridge alias through current production latest redirect");
                using var rejected = new ProductionV1.AuthenticatedSource(config,key,new Transport(_=>new(HttpStatusCode.OK){Content=new StringContent(futureEnvelope)}));
                await Reject<CryptographicException>(()=>rejected.GetReleaseFeed(null!,config.AppId,""),"released production source really rejects unbridged schema2");
            }
            else
            {
                var config = new TestV1.LauncherConfig("tech.test","osx","arm64","1.0.0","2026-10-17",feed,"game/Player",channel);
                using var old = new TestV1.AuthenticatedSource(config,key,new Transport(Route)); await old.GetReleaseFeed(null!,config.AppId,"");
                Check(old.Asset!.Version.ToString() == bridge && api == 1 && legacy == 1 && modernRequests == 0, "released test source skips newer modern-only assets and selects legacy bridge");
                await Reject<CryptographicException>(()=> { old.Admit(futureEnvelope); return Task.CompletedTask; },"released test source really rejects schema2");
            }
            api = legacy = modernRequests = 0;
            var currentConfig = new LauncherConfig("tech.test","osx","arm64","1.0.0","2026-10-17",feed,"game/Player",channel);
            using var current = new AuthenticatedSource(currentConfig,key,new Transport(Route),hostProbe:(_,minimum)=>new Version(15,0)>=minimum);
            await current.GetReleaseFeed(null!,currentConfig.AppId,"");
            Check(current.Asset!.Version.ToString() == future && api == 1 && modernRequests == 1 && legacy == 0, "current source chooses signed modern endpoint from same listing: " + channel);
            int legacyFallback=0;
            using(var badModern=new AuthenticatedSource(currentConfig,key,new Transport(request=>
            {
                if(request.RequestUri!.Host=="api.github.com")return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new[]{Row(future,true,true)}))};
                if(request.RequestUri.AbsolutePath.Contains("latest-v2-"))return new(HttpStatusCode.OK){Content=new StringContent(futureEnvelope.Replace("payload\":\"","payload\":\"AAAA"))};
                legacyFallback++;return new(HttpStatusCode.OK){Content=new StringContent(bridgeEnvelope)};
            }),hostProbe:(_,_)=>true))
            {
                await Reject<CryptographicException>(()=>badModern.GetReleaseFeed(null!,currentConfig.AppId,""),"bad modern signature cannot downgrade to legacy endpoint: "+channel);
                Check(legacyFallback==0&&badModern.Asset==null&&badModern.Envelope==null,"modern trust failure stops without stale/legacy admission: "+channel);
            }
            int wrongArchitectureAlias=0;
            using(var architecture=new AuthenticatedSource(currentConfig with{Architecture="x64"},key,new Transport(request=>
            {
                if(request.RequestUri!.Host=="api.github.com")return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new[]{Row(future,true,true,"latest-osx-x64.json"),Row(bridge,false,extra:"latest-osx-x64.json")}))};
                if(request.RequestUri.AbsolutePath.Contains("/v"+future+"/"))wrongArchitectureAlias++;
                return new(HttpStatusCode.OK){Content=new StringContent(Signed(bridge,channel,1,"x64"))};
            }),hostProbe:(_,_)=>true))
            {
                await architecture.GetReleaseFeed(null!,currentConfig.AppId,"");
                Check(architecture.Asset!.Version.ToString()==bridge&&wrongArchitectureAlias==0,"modern family without own target skips historical cross-architecture alias: "+channel);
            }
            current.Admit(bridgeEnvelope); current.RequireCompatibleTarget();
            Check(current.Asset!.Version.ToString() == bridge, "historical schema1 remains readable for routing/bootstrap investigation: " + channel);
        }
        Console.WriteLine($"Legacy reader routing: {passed} passed (released source logic, not installed binary/bootstrap acceptance)");
    }
}
