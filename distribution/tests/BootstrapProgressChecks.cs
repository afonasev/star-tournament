using System.Security.Cryptography;
using StarTournament.Distribution;

static class BootstrapProgressChecks
{
    public static async Task Run()
    {
        int passed = 0;
        void Check(bool condition, string message)
        { if (!condition) throw new Exception(message); passed++; Console.WriteLine("PASS " + message); }
        foreach (var mode in new[] { "success", "offline", "timeout", "bad-signature", "apply-error", "cancel" })
        {
            var snapshots = new List<UpdateSnapshot>();
            using var token = new CancellationTokenSource();
            var midway = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            bool applied = false;
            var run = BootstrapStartup.Run("1.0.0", _ => Task.FromResult<string?>("1.1.0"),
                _ => Task.FromResult(true), async (progress, cancellation) =>
                {
                    progress(42);
                    midway.SetResult();
                    await resume.Task;
                    cancellation.ThrowIfCancellationRequested();
                    if (mode == "offline") throw new HttpRequestException("Offline");
                    if (mode == "timeout") throw new TimeoutException("Stalled");
                    if (mode == "bad-signature") throw new CryptographicException("Wrong digest");
                    progress(100);
                }, _ =>
                {
                    applied = true;
                    Check(snapshots.Last().State == "applying", "installation status published before handoff: " + mode);
                    if (mode == "apply-error") throw new UnauthorizedAccessException();
                    return Task.CompletedTask;
                }, _ => { }, token.Token, snapshots.Add);
            await midway.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Check(!run.IsCompleted && !applied && snapshots.Last() is { State: "downloading", Progress: 42, Version: "1.1.0" }, "live percentage visible before download completes: " + mode);
            Check(snapshots.Select(x => x.State).Take(3).SequenceEqual(new[] { "checking", "available", "downloading" }), "window states begin before discovery/consent/download: " + mode);
            if (mode == "cancel") token.Cancel();
            resume.SetResult();
            var result = await run;
            if (mode == "success") Check(result.HandedOff && applied, "only verified success hands off");
            else
            {
                Check(!result.HandedOff && snapshots.Last() == result.Snapshot && result.Snapshot.Error.Length > 0, "failure published without hidden successful handoff: " + mode);
                Check(applied == (mode == "apply-error"), "network/hash/cancellation failures cannot activate: " + mode);
                var content = BootstrapProgressContent.From(new(result.Snapshot.State, result.Snapshot.Progress, result.Snapshot.Error, result.Snapshot.Version));
                Check(content.Detail.Contains(UpdateFeedback.LogPath) && content.Detail.Contains("ОК") && content.CanCancel, "visible error offers log and explicit continuation/exit: " + mode);
            }
        }
        Check(!BootstrapProgressContent.From(new("applying")).CanCancel, "handoff cannot be interrupted by UI cancellation");
        Check(BootstrapProgressContent.From(new("checking")).Marquee && !BootstrapProgressContent.From(new("downloading", 42)).Marquee,
            "unknown duration uses activity indicator, download uses percentage");
        Console.WriteLine($"PASS {passed} bootstrap progress contracts; native Windows presentation NotRun");
    }
}
