using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StarTournament.Distribution;

static class GitHubDiscoveryChecks
{
    sealed class Transport(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request, token); }
    sealed class Streaming(string body) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) => stream.WriteAsync(Encoding.UTF8.GetBytes(body)).AsTask();
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(body)));
    }
    public static async Task Run()
    {
        int passed = 0;
        void Check(bool value, string label) { if (!value) throw new Exception(label); passed++; Console.WriteLine("PASS " + label); }
        async Task Reject<T>(Func<Task> action, string label) where T : Exception
        { try { await action(); } catch (T) { Check(true, label); return; } throw new Exception("Expected " + typeof(T).Name + ": " + label); }
        using var rsa = RSA.Create(3072);
        string directory = Path.Combine(Path.GetTempPath(), "star-discovery-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        LauncherConfig Config(string channel) => new("tech.test", "osx", "arm64", "1.0.0", "2026-01-01",
            channel == "test" ? AuthenticatedSource.TestLocator : AuthenticatedSource.ProductionBase, "game/Player", channel);
        string Full(string version) => "fixture-" + version + "-full.nupkg";
        object Row(string version, bool draft = false, bool? prerelease = null, bool complete = true, bool wrongFull = false, string? extraManifest = null) => new
        {
            tag_name = "v" + version, draft, prerelease = prerelease ?? version.Contains('-'),
            assets = new[] { "latest-win-x64.json", "latest-osx-arm64.json", $"Star-Tournament-{version}-Windows-x64-Setup.exe",
                $"Star-Tournament-{version}-macOS-arm64.pkg" }.Concat(complete ? new[] { wrongFull ? "wrong-full.nupkg" : Full(version), "counterpart-full.nupkg" } : Array.Empty<string>()).Concat(extraManifest == null ? Array.Empty<string>() : new[] { extraManifest }).Select(name => new { name }).ToArray()
        };
        HttpResponseMessage Page(object[] rows, string? etag = null, string? next = null)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(rows)) };
            if (etag != null) response.Headers.ETag = new EntityTagHeaderValue(etag);
            if (next != null) response.Headers.TryAddWithoutValidation("Link", $"<{next}>; rel=\"next\"");
            return response;
        }
        string Signed(LauncherConfig config, string version)
        {
            var bytes = "valid fixture package"u8.ToArray();
            var payload = JsonSerializer.SerializeToUtf8Bytes(new { schema = 1, platform = config.Platform, architecture = config.Architecture,
                channel = config.UpdateChannel, version, publicationDate = "2026-10-17",
                downloadUrl = $"https://github.com/afonasev/star-tournament/releases/download/v{version}/{Full(version)}",
                feed = new { Assets = new[] { new { PackageId = config.AppId, Version = version, Type = "Full", FileName = Full(version),
                    SHA256 = Convert.ToHexString(SHA256.HashData(bytes)), Size = bytes.Length } } } });
            return JsonSerializer.Serialize(new { payload = Convert.ToBase64String(payload), signature = Convert.ToBase64String(rsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)) });
        }
        HttpResponseMessage Manifest(HttpRequestMessage request, LauncherConfig config)
        {
            Check(!request.Headers.Contains("If-None-Match") && request.RequestUri!.Host == "github.com" && !request.RequestUri.AbsolutePath.Contains("/latest/"), "asset request stays version-pinned without API cache headers");
            string version = request.RequestUri!.Segments[^2].Trim('/')[1..];
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Signed(config, version)) };
        }
        AuthenticatedSource Source(LauncherConfig config, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send, string? cache = null, Func<DateTimeOffset>? clock = null)
            => new(config, rsa.ExportSubjectPublicKeyInfoPem(), new Transport(send), discoveryCachePath: cache, discoveryClock: clock);
        Task Feed(AuthenticatedSource source, LauncherConfig config) => source.GetReleaseFeed(null!, config.AppId, "");
        const string Page2 = AuthenticatedSource.TestLocator + "?page=2&per_page=100";
        try
        {
            foreach (string channel in new[] { "production", "test" })
            {
                var config = Config(channel); string suffix = channel == "test" ? "-test.1" : ""; int api = 0;
                using var source = Source(config, (request, _) =>
                {
                    if (request.RequestUri!.Host != "api.github.com") return Task.FromResult(Manifest(request, config));
                    api++; Check(request.Headers.Authorization == null && request.Headers.UserAgent.Count > 0 && request.Headers.Contains("X-GitHub-Api-Version"), "public versioned GitHub API headers: " + channel);
                    return Task.FromResult(api == 1 ? Page(new[] { Row("1.9.0" + suffix), Row("99.0.0" + suffix, draft: true), Row("98.0.0" + suffix, complete: false),
                        Row(channel == "test" ? "88.0.0" : "88.0.0-test.1"), Row("87.0.0-test.1", prerelease: false) }, next: Page2) :
                        Page(new[] { Row("1.10.0" + suffix) }));
                });
                await Feed(source, config);
                Check(api == 2 && source.Asset!.Version.ToString() == "1.10.0" + suffix, "channel/canonical semantic/draft/incomplete filtering follows Link even on short page: " + channel);
            }
            var intel = Config("production") with { Architecture = "x64" };
            using (var architecture = Source(intel, (request, _) => Task.FromResult(request.RequestUri!.Host == "api.github.com" ?
                Page(new[] { Row("9.0.0"), Row("1.1.0", extraManifest: "latest-osx-x64.json") }) : Manifest(request, intel))))
            { await Feed(architecture, intel); Check(architecture.Asset!.Version.ToString() == "1.1.0", "current architecture manifest is required before candidate ranking"); }
            var stable = Config("production"); string cachePath = Path.Combine(directory, "conditional"); int manifestCalls = 0;
            using (var first = Source(stable, (request, _) =>
            {
                if (request.RequestUri!.Host != "api.github.com") { manifestCalls++; return Task.FromResult(Manifest(request, stable)); }
                Check(!request.Headers.Contains("If-None-Match"), "fresh cache has unconditional API request");
                return Task.FromResult(request.RequestUri.Query.EndsWith("&page=1", StringComparison.Ordinal) ? Page(new[] { Row("1.1.0") }, "\"one\"", Page2) : Page(new[] { Row("1.2.0") }, "\"two\""));
            }, cachePath)) await Feed(first, stable);
            using (var second = Source(stable, (request, _) =>
            {
                if (request.RequestUri!.Host != "api.github.com") { manifestCalls++; return Task.FromResult(Manifest(request, stable)); }
                string expected = request.RequestUri.Query.EndsWith("&page=1", StringComparison.Ordinal) ? "\"one\"" : "\"two\"";
                Check(request.Headers.IfNoneMatch.Single().ToString() == expected, "persisted page-specific ETag supplied");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified));
            }, cachePath))
            { await Feed(second, stable); Check(second.Asset!.Version.ToString() == "1.2.0" && manifestCalls == 2, "304 reuses cached next-link/body but re-fetches signed manifest"); }
            int linked304 = 0;
            using (var changedLink = Source(stable, (request, _) =>
            {
                if (request.RequestUri!.Host != "api.github.com") return Task.FromResult(Manifest(request, stable));
                linked304++;
                if (request.RequestUri.Query.Contains("page=3")) return Task.FromResult(Page(new[] { Row("1.3.0") }));
                var response = new HttpResponseMessage(HttpStatusCode.NotModified);
                if (request.RequestUri.Query.StartsWith("?page=2&")) response.Headers.TryAddWithoutValidation("Link", $"<{AuthenticatedSource.TestLocator}?page=3&per_page=100>; rel=\"next\"");
                return Task.FromResult(response);
            }, cachePath))
            { await Feed(changedLink, stable); Check(linked304 == 3 && changedLink.Asset!.Version.ToString() == "1.3.0", "304 response can extend cached pagination through its fresh Link header"); }
            int exhaustedCalls = 0;
            var exhaustedNow = new DateTimeOffset(2026, 10, 17, 0, 0, 0, TimeSpan.Zero);
            using (var exhausted = Source(stable, (_, _) =>
            {
                exhaustedCalls++; var response = Page(new[] { Row("1.2.0") }, next: Page2);
                response.Headers.Add("X-RateLimit-Remaining", "0"); response.Headers.Add("X-RateLimit-Reset", exhaustedNow.AddMinutes(2).ToUnixTimeSeconds().ToString());
                return Task.FromResult(response);
            }, Path.Combine(directory, "exhausted-success"), () => exhaustedNow))
            { await Reject<HttpRequestException>(() => Feed(exhausted, stable), "successful last-quota response stops before another API page"); Check(exhaustedCalls == 1 && exhausted.Asset == null, "remaining0 cannot produce a partial-traversal candidate"); }
            bool fail = false;
            using (var source = Source(stable, (request, _) =>
            {
                if (request.RequestUri!.Host != "api.github.com") return Task.FromResult(Manifest(request, stable));
                return Task.FromResult(fail ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : Page(new[] { Row("1.2.0") }, "\"current\""));
            }, Path.Combine(directory, "stale")))
            {
                await Feed(source, stable); fail = true;
                await Reject<HttpRequestException>(() => Feed(source, stable), "HTTP error never falls back to stale current");
                Check(source.Asset == null && source.Envelope == null && source.PublicationDate == null, "failed discovery clears prior admitted target");
            }
            using (var noCache = Source(stable, (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified))))
                await Reject<InvalidDataException>(() => Feed(noCache, stable), "304 without valid cache rejects");
            var pageFile = Directory.GetFiles(cachePath, "*.json").Single(file =>
            { using var cached = JsonDocument.Parse(File.ReadAllText(file)); return cached.RootElement.GetProperty("Uri").GetString()!.EndsWith("&page=1", StringComparison.Ordinal); });
            File.WriteAllText(pageFile, File.ReadAllText(pageFile).Replace("fixture", "corrupt"));
            using (var corrupt = Source(stable, (request, _) =>
            {
                Check(!request.Headers.Contains("If-None-Match"), "corrupt cached body/hash cannot send its ETag");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified));
            }, cachePath)) await Reject<InvalidDataException>(() => Feed(corrupt, stable), "corrupt cache cannot grant304 admission");
            string unwritable = Path.Combine(directory, "file-not-directory"); File.WriteAllText(unwritable, "owned fixture");
            using (var noStorage = Source(stable, (request, _) => Task.FromResult(request.RequestUri!.Host == "api.github.com" ? Page(new[] { Row("1.2.0") }, "\"ok\"") : Manifest(request, stable)), unwritable))
            { await Feed(noStorage, stable); Check(noStorage.Asset!.Version.ToString() == "1.2.0", "cache IO failure does not block valid signed network candidate"); }
            foreach (string mode in new[] { "delta", "date", "primary", "both", "default" })
            {
                var now = new DateTimeOffset(2026, 10, 17, 0, 0, 0, TimeSpan.Zero); var start = now; int calls = 0;
                int seconds = mode is "primary" or "both" ? 180 : mode == "default" ? 60 : 120;
                string rateCache = Path.Combine(directory, "rate-" + mode);
                Task<HttpResponseMessage> Rate(HttpRequestMessage request, CancellationToken _)
                {
                    if (request.RequestUri!.Host != "api.github.com") return Task.FromResult(Manifest(request, stable));
                    calls++;
                    if (now >= start.AddSeconds(seconds)) return Task.FromResult(Page(new[] { Row("1.2.0") }));
                    var response = new HttpResponseMessage(mode is "delta" or "date" ? HttpStatusCode.TooManyRequests : HttpStatusCode.Forbidden);
                    if (mode is "delta" or "both") response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(120));
                    if (mode == "date") response.Headers.RetryAfter = new RetryConditionHeaderValue(start.AddSeconds(120));
                    if (mode is "primary" or "both") { response.Headers.Add("X-RateLimit-Remaining", "0"); response.Headers.Add("X-RateLimit-Reset", start.AddSeconds(180).ToUnixTimeSeconds().ToString()); }
                    return Task.FromResult(response);
                }
                using (var limited = Source(stable, Rate, rateCache, () => now)) await Reject<HttpRequestException>(() => Feed(limited, stable), "rate error is explicit: " + mode);
                now = start.AddSeconds(seconds - 1);
                using (var blocked = Source(stable, Rate, rateCache, () => now)) await Reject<HttpRequestException>(() => Feed(blocked, stable), "cooldown persists across launch: " + mode);
                Check(calls == 1, "no request before Retry-After/reset/fallback deadline: " + mode);
                now = start.AddSeconds(seconds);
                using (var resumed = Source(stable, Rate, rateCache, () => now)) { await Feed(resumed, stable); Check(calls == 2 && resumed.Asset != null, "next launch may check at deadline: " + mode); }
            }
            foreach (string next in new[] { "https://evil.example/page", AuthenticatedSource.TestLocator + "?per_page=100&page=1" })
            {
                int calls = 0;
                using var source = Source(stable, (_, _) => { calls++; return Task.FromResult(Page(new[] { Row("1.2.0") }, next: next)); });
                await Reject<Exception>(() => Feed(source, stable), "foreign/repeated next link rejects");
                Check(calls == 1 && source.Asset == null, "pagination cannot request foreign or repeated target");
            }
            int pages = 0;
            using (var source = Source(stable, (_, _) => Task.FromResult(Page(new[] { Row("1.2.0") }, next: AuthenticatedSource.TestLocator + "?per_page=100&page=" + (++pages + 1)))))
            { await Reject<InvalidOperationException>(() => Feed(source, stable), "pagination limit rejects incomplete traversal"); Check(pages == 10, "at most ten API pages"); }
            using (var wrongFull = Source(stable, (request, _) => Task.FromResult(request.RequestUri!.Host == "api.github.com" ? Page(new[] { Row("1.2.0", wrongFull: true) }) : Manifest(request, stable))))
            { await Reject<CryptographicException>(() => Feed(wrongFull, stable), "signed full package must be listed in selected release"); Check(wrongFull.Asset == null && wrongFull.Envelope == null, "incomplete API target cannot leave admission"); }
            using (var badSignature = Source(stable, (request, _) =>
            {
                if (request.RequestUri!.Host == "api.github.com") return Task.FromResult(Page(new[] { Row("1.2.0") }));
                var envelope = JsonSerializer.Deserialize<Dictionary<string, string>>(Signed(stable, "1.2.0"))!;
                var signature = Convert.FromBase64String(envelope["signature"]); signature[0] ^= 1; envelope["signature"] = Convert.ToBase64String(signature);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(envelope)) });
            })) await Reject<CryptographicException>(() => Feed(badSignature, stable), "API candidate never replaces signature verification");
            foreach (bool chunked in new[] { false, true })
            {
                using var tooLarge = Source(stable, (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = chunked ? new Streaming("[" + new string(' ', 2 * 1024 * 1024) + "]") : new StringContent(new string(' ', 2 * 1024 * 1024 + 1)) }));
                await Reject<InvalidDataException>(() => Feed(tooLarge, stable), "listing byte limit enforces declared/chunked body: " + chunked);
            }
            using var cancellation = new CancellationTokenSource(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using (var cancel = Source(stable, async (_, token) => { entered.SetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, token); return Page(Array.Empty<object>()); }))
            {
                cancel.CheckCancellationToken = cancellation.Token; var pending = Feed(cancel, stable); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); cancellation.Cancel();
                await Reject<OperationCanceledException>(() => pending, "session cancellation reaches real discovery transport"); Check(cancel.Asset == null, "cancelled discovery has no target");
            }
            int sessionRequests = 0, sessionDownloads = 0, sessionStages = 0;
            var unavailable = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using (var sessionSource = Source(stable, (_, _) => { sessionRequests++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)); }))
            await using (var session = new UpdateSession(async token =>
            { sessionSource.CheckCancellationToken = token; await Feed(sessionSource, stable); return sessionSource.Asset!.Version.ToString(); },
                (_, _) => { sessionDownloads++; return Task.CompletedTask; }, () => sessionStages++, state => { if (state.State == "unavailable") unavailable.TrySetResult(); }))
            {
                session.Start(); await unavailable.Task.WaitAsync(TimeSpan.FromSeconds(5));
                session.Start(); await session.CommandAsync("check"); await session.CommandAsync("download");
                await session.CommandAsync("match", "fixture-match"); await session.CommandAsync("menu", "fixture-match");
                Check(sessionRequests == 1 && session.State.State == "unavailable", "actual API failure stays unavailable through repeated start/check/menu routes");
                Check(sessionDownloads == 0 && sessionStages == 0 && sessionSource.Asset == null, "failed discovery cannot download or stage a stale target");
            }
            Check(!Directory.GetFiles(directory, "*.write-*", SearchOption.AllDirectories).Any(), "atomic cache writes leave no owned temporary files");
        }
        finally { Directory.Delete(directory, true); }
        Console.WriteLine($"PASS {passed} GitHub discovery contracts");
    }
}
