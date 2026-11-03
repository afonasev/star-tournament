using System.Security.Cryptography;
using StarTournament.Distribution;

static class BootstrapStartupChecks
{
    public static async Task Run()
    {
        var passed = 0;
        void Check(bool value, string label)
        { if (!value) throw new Exception(label); passed++; Console.WriteLine("PASS " + label); }
        foreach (var target in new string?[] { null, "1.0.0", "0.9.0" })
        {
            var called = false;
            var result = await BootstrapStartup.Run("1.0.0", _ => Task.FromResult(target),
                _ => { called = true; return Task.FromResult(true); },
                _ => { called = true; return Task.CompletedTask; },
                _ => { called = true; return Task.CompletedTask; }, _ => { called = true; });
            Check(!result.HandedOff && result.Snapshot.State == "current" && !called,
                "absent/equal/older target starts current without UI/download/apply: " + target);
        }
        foreach (var mode in new[] { "decline", "accept", "discovery-error", "prompt-error", "download-error", "verify-error", "apply-error", "cancel-before", "cancel-after-consent", "cancel-after-download" })
        {
            using var token = new CancellationTokenSource();
            var events = new List<string>();
            if (mode == "cancel-before") token.Cancel();
            var result = await BootstrapStartup.Run("1.0.0", cancellation =>
            {
                events.Add("discover"); cancellation.ThrowIfCancellationRequested();
                if (mode == "discovery-error") throw new CryptographicException("Invalid signature");
                return Task.FromResult<string?>("1.1.0");
            }, version =>
            {
                events.Add("consent:" + version);
                if (mode == "prompt-error") throw new IOException("No UI");
                if (mode == "cancel-after-consent") token.Cancel();
                return Task.FromResult(mode != "decline");
            }, cancellation =>
            {
                events.Add("download"); cancellation.ThrowIfCancellationRequested();
                if (mode == "download-error") throw new IOException("Incomplete download");
                if (mode == "verify-error") throw new CryptographicException("Wrong digest");
                if (mode == "cancel-after-download") token.Cancel();
                events.Add("verified"); return Task.CompletedTask;
            }, version =>
            {
                events.Add("apply:" + version);
                if (mode == "apply-error") throw new UnauthorizedAccessException("UAC denied");
                events.Add("acknowledged"); return Task.CompletedTask;
            }, _ => events.Add("log"), token.Token);
            Check(result.HandedOff == (mode == "accept"), mode + ": handoff only after acknowledged apply");
            if (mode == "accept")
                Check(events.SequenceEqual(new[] { "discover", "consent:1.1.0", "download", "verified", "apply:1.1.0", "acknowledged" }), "accepted update preserves consent/verification/activation ordering");
            else if (mode == "decline")
                Check(events.SequenceEqual(new[] { "discover", "consent:1.1.0" }) && result.Snapshot is { State: "available", Version: "1.1.0" }, "declined update never downloads or activates");
            else if (mode is "discovery-error" or "prompt-error" or "cancel-before" or "cancel-after-consent")
                Check(!events.Contains("download") && !events.Any(x => x.StartsWith("apply:")) && events.Last() == "log", mode + ": fail closed without downloading");
            else if (mode == "apply-error")
                Check(result.Snapshot.State == "apply-error" && events.Last() == "log", "failed handoff remains explicit retry with current playable");
            else
                Check(!events.Any(x => x.StartsWith("apply:")) && result.Snapshot.State == "error", mode + ": unverified/cancelled package never activates");
        }
        Console.WriteLine($"PASS {passed} bootstrap startup contracts");
    }
}
