using System.Net;
using System.Security.Cryptography;
using StarTournament.Distribution;

static class UpdateSessionChecks
{
    public static async Task Run()
    {
        int passed = 0;
        void Check(bool value, string label) { if (!value) throw new Exception(label); passed++; Console.WriteLine("PASS " + label); }
        static TaskCompletionSource Ready() => new(TaskCreationOptions.RunContinuationsAsynchronously);

        int checks = 0, downloads = 0, commits = 0;
        await using (var session = new UpdateSession(_ => { checks++; return Task.FromResult<string?>("1.2.3"); },
            (_, _) => { downloads++; if (downloads == 1) throw new IOException("fixture"); return Task.CompletedTask; }, () => commits++, _ => { }))
        {
            session.Start(); session.Start(); await session.CommandAsync("check");
            Check(checks == 1 && downloads == 0, "one startup discovery, no consent from checking");
            await session.CommandAsync("match", "first"); await session.CommandAsync("menu", "first");
            Check(downloads == 0 && checks == 1, "menu return without consent starts nothing");
            Check(!await session.CommandAsync("restart"), "restart requires authenticated ready");
            await session.CommandAsync("download"); Check(session.State.State == "error", "download failure remains explicit");
            await session.CommandAsync("check"); await session.CommandAsync("download");
            Check(checks == 1 && downloads == 2 && commits == 1 && session.State is { State: "staged", Version: "1.2.3" }, "retry pins selected version, no discovery");
            Check(await session.CommandAsync("restart") && session.State.RestartAcknowledged, "restart only after committed ready");
        }

        checks = downloads = commits = 0;
        var release = Ready(); Action<int>? lateProgress = null;
        await using (var session = new UpdateSession(_ => { checks++; return Task.FromResult<string?>("2.0.0"); },
            async (progress, _) => { downloads++; lateProgress = progress; if (downloads == 1) await release.Task; }, () => commits++, _ => { }))
        {
            try
            {
                session.Start(); await session.CommandAsync("download"); await session.CommandAsync("download");
                Check(downloads == 1, "duplicate download has one operation");
                var pause = session.CommandAsync("match", "active-match");
                Check(!pause.IsCompleted && !session.State.MatchPaused, "pause ack waits actual operation drain");
                lateProgress!(100); Check(session.State.State != "staged" && commits == 0, "late progress cannot stage while stopping");
                release.SetResult(); await pause.WaitAsync(TimeSpan.FromSeconds(5));
                Check(session.State is { State: "paused", MatchPaused: true, MatchRequest: "active-match" } && commits == 0, "late success after pause cannot publish ready");
                await session.CommandAsync("download"); await session.CommandAsync("restart");
                Check(downloads == 1 && !session.State.RestartAcknowledged, "match blocks download and restart");
                await session.CommandAsync("menu", "stale-match"); Check(downloads == 1 && session.State.MatchPaused, "stale menu request cannot release a newer match");
                await session.CommandAsync("menu", "active-match");
                Check(downloads == 2 && checks == 1 && commits == 1 && session.State.State == "staged", "menu resumes consented target without checking");
            }
            finally { release.TrySetResult(); }
        }

        checks = downloads = commits = 0; release = Ready();
        await using (var session = new UpdateSession(_ => { checks++; return Task.FromResult<string?>("3.0.0"); },
            async (progress, _) => { downloads++; lateProgress = progress; if (downloads == 1) await release.Task; }, () => commits++, _ => { }))
        {
            try
            {
                session.Start(); await session.CommandAsync("download"); var cancelledProgress = lateProgress;
                var cancel = session.CommandAsync("cancel"); Check(!cancel.IsCompleted, "cancel drains even a late success");
                release.SetResult(); await cancel.WaitAsync(TimeSpan.FromSeconds(5));
                Check(commits == 0 && session.State.State == "available", "cancel withdraws consent before ready manifest");
                cancelledProgress!(100); Check(session.State.State == "available", "old callback cannot overwrite cancelled state");
                await session.CommandAsync("match", "cancelled"); await session.CommandAsync("menu", "cancelled");
                Check(downloads == 1 && checks == 1, "cancelled consent never auto resumes");
                await session.CommandAsync("download"); Check(downloads == 2 && commits == 1, "explicit retry after cancel is allowed");
            }
            finally { release.TrySetResult(); }
        }

        commits = 0;
        var exitSession = new UpdateSession(_ => Task.FromResult<string?>("4.0.0"),
            async (_, token) => await Task.Delay(Timeout.InfiniteTimeSpan, token), () => commits++, _ => { });
        exitSession.Start(); await exitSession.CommandAsync("download");
        await exitSession.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Check(commits == 0, "exit cancels transfer instead of waiting for full download");

        using var rsa = RSA.Create(2048);
        using var transport = new WaitingTransport();
        using var source = new AuthenticatedSource(new LauncherConfig("tech.test", "osx", "arm64", "1.0.0", "2026-10-04", "https://fixture.invalid/", "game/Player"), rsa.ExportSubjectPublicKeyInfoPem(), transport);
        checks = 0;
        await using (var session = new UpdateSession(async token =>
        {
            checks++; source.CheckCancellationToken = token; await source.GetReleaseFeed(null!, "tech.test", ""); return source.Asset?.Version.ToString();
        }, (_, _) => throw new Exception("no admitted target"), () => throw new Exception("no ready bytes"), _ => { }))
        {
            session.Start(); await transport.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await session.CommandAsync("match", "metadata").WaitAsync(TimeSpan.FromSeconds(5));
            Check(transport.Cancelled && session.State is { State: "unavailable", MatchPaused: true }, "match cancels actual AuthenticatedSource HTTP metadata before ack");
            await session.CommandAsync("menu", "metadata"); await session.CommandAsync("check"); await session.CommandAsync("download");
            Check(checks == 1 && transport.Requests == 1, "interrupted check is never repeated in session");
        }
        Console.WriteLine($"{passed} focused update-session checks passed");
    }
    sealed class WaitingTransport : HttpMessageHandler
    {
        public readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Requests; public bool Cancelled;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Requests++; Started.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); return new(HttpStatusCode.OK); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { Cancelled = true; throw; }
        }
    }
}
