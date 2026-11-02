using Velopack;

namespace StarTournament.Distribution;

public sealed record BootstrapStartupResult(bool HandedOff, UpdateSnapshot Snapshot);

/// <summary>The pre-Player boundary. Discovery never grants download or activation consent.
/// An apply callback must acknowledge a recoverable handoff, not merely spawn an updater.</summary>
public static class BootstrapStartup
{
    public static async Task<BootstrapStartupResult> Run(string installed,
        Func<CancellationToken, Task<string?>> discover,
        Func<string, Task<bool>> confirm,
        Func<CancellationToken, Task> downloadAndVerify,
        Func<string, Task> apply,
        Action<Exception> log,
        CancellationToken token = default)
    {
        string? target = null;
        bool consent = false;
        bool verified = false;
        try
        {
            target = await discover(token);
            token.ThrowIfCancellationRequested();
            if (target == null || SemanticVersion.Parse(target) <= SemanticVersion.Parse(installed))
                return new(false, new("current"));
            consent = await confirm(target);
            token.ThrowIfCancellationRequested();
            if (!consent) return new(false, new("available", Version: target));
            await downloadAndVerify(token);
            token.ThrowIfCancellationRequested();
            verified = true;
            await apply(target);
            return new(true, new("staged", 100, Version: target));
        }
        catch (Exception error)
        {
            log(error);
            return new(false, new(verified ? "apply-error" : consent ? "error" : "unavailable",
                Error: "Обновление не установлено. " + UpdateFeedback.Explain(error), Version: target ?? ""));
        }
    }
}
