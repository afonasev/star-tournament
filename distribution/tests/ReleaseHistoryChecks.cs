using System.Net;
using System.Text;
using System.Text.Json;
using StarTournament.Distribution;

static class ReleaseHistoryChecks
{
    public static async Task Run()
    {
        int passed = 0;
        void Check(bool condition, string label) { if (!condition) throw new Exception(label); passed++; Console.WriteLine("PASS " + label); }
        var languages = ReleaseHistory.Languages("## Русский\n### Новое\n- Арены.\n\n## English\n### Added\n- Arenas.");
        Check(languages.Russian == "### Новое\n- Арены." && languages.English == "### Added\n- Arenas.", "matching bilingual sections isolated");
        Check(ReleaseHistory.Languages("Legacy English release").Russian == "", "legacy notes get no fabricated translation");
        string directory = Path.Combine(Path.GetTempPath(), "star-history-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        string cache = Path.Combine(directory, "cache.json");
        var config = new LauncherConfig("tech.test", "osx", "arm64", "0.1.0", "2026-01-01", "", "game", "production");
        object Row(string version, bool draft = false, bool prerelease = false) => new { tag_name = "v" + version, draft, prerelease, published_at = "2026-10-16T10:00:00Z", body = "## Русский\n- Русское описание.\n## English\n- English notes." };
        var rows = Enumerable.Range(1, 12).Select(i => Row("1.0." + i)).Concat(new[] { Row("9.0.0", true), Row("8.0.0-test.1", false, true) }).ToArray();
        try
        {
            using var http = new FakeTransport((request, token) =>
            {
                if (request.RequestUri!.AbsolutePath.EndsWith("/tags/v0.1.0")) return Task.FromResult(Response(JsonSerializer.Serialize(Row("0.1.0"))));
                Check(request.RequestUri.Host == "api.github.com" && !request.Headers.Contains("Authorization"), "fixed public GitHub history endpoint without token");
                var result = Response(JsonSerializer.Serialize(rows)); result.Headers.ETag = new("\"history-v1\""); return Task.FromResult(result);
            });
            using (var history = new ReleaseHistory(config, cache, http))
            {
                var result = await history.Fetch(default);
                Check(result.State == "ready" && result.Items.Length == 11 && result.Items[0].Version == "1.0.12", "ten recent releases plus installed outside page; semantic tie order");
                Check(result.Items.Count(x => x.Installed) == 1 && result.Items.Last().Version == "0.1.0", "exact installed version retained");
                Check(result.Items.All(x => x.Version != "9.0.0" && x.Version != "8.0.0-test.1"), "draft and foreign channel excluded");
                Check(result.Items.All(x => x.Russian.Contains("Русское") && x.English.Contains("English")), "each release carries its own bilingual notes");
                Check(File.Exists(cache), "bounded successful history cached");
            }
            using (var history = new ReleaseHistory(config, cache, new FakeTransport((request, _) =>
            {
                Check(request.Headers.GetValues("If-None-Match").Single() == "\"history-v1\"", "conditional ETag survives next launch");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified));
            })))
                Check((await history.Fetch(default)).Items.Length == 11, "304 reuses complete cached history");
            using (var history = new ReleaseHistory(config, cache, new FakeTransport((_, _) => throw new HttpRequestException("offline"))))
                Check((await history.Fetch(default)).State == "cached", "offline fallback explicitly marked cached");
            using (var history = new ReleaseHistory(config with { Version = "0.2.0" }, cache, new FakeTransport((_, _) => throw new HttpRequestException("offline"))))
                Check((await history.Fetch(default)).State == "error", "cache from another installed version is not mislabelled");
            int limitedRequests=0;
            using(var history=new ReleaseHistory(config,cache,new FakeTransport((_,_)=>{
                limitedRequests++;var response=new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                response.Headers.RetryAfter=new(TimeSpan.FromHours(1));return Task.FromResult(response);
            })))
            {
                await history.Fetch(default);await history.Fetch(default);
                Check(limitedRequests==1,"Retry-After prevents repeated rate-limited history requests");
            }
            var began = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var drain = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); int discovery = 0, stages = 0, fetches = 0;
            using var waiting = new ReleaseHistory(config, cache, new FakeTransport(async (_, token) =>
            { began.TrySetResult(); try { await Task.Delay(Timeout.Infinite, token); } finally { await drain.Task; } return Response("[]"); }));
            await using var session = new UpdateSession(_ => { discovery++; return Task.FromResult<string?>("2.0.0"); },
                (_, _) => Task.CompletedTask, () => stages++, _ => { }, historyFetch: async token => { fetches++; return await waiting.Fetch(token); });
            session.Start(); await session.CommandAsync("history"); await began.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var pause = session.CommandAsync("match", "history-drain");
            Check(!pause.IsCompleted && !session.State.MatchPaused, "history HTTP drain is part of match admission");
            drain.TrySetResult(); await pause.WaitAsync(TimeSpan.FromSeconds(5));
            Check(session.State.MatchPaused && session.State.Version == "2.0.0" && stages == 0, "history neither admits new candidate nor stages update");
            await session.CommandAsync("history"); Check(fetches == 1, "history request forbidden during match");
            await session.CommandAsync("menu", "history-drain");
            Check(discovery == 1 && session.State.Version == "2.0.0" && fetches == 2, "menu resumes requested history with frozen candidate and no discovery");
        }
        finally { Directory.Delete(directory, true); }
        Console.WriteLine(passed + " focused release-history checks passed");
    }
    static HttpResponseMessage Response(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    sealed class FakeTransport(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> handler) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token) => handler(request,token); }
}
