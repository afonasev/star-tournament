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
        Func<Action<int>, CancellationToken, Task> downloadAndVerify,
        Func<string, Task> apply,
        Action<Exception> log,
        CancellationToken token = default, Action<UpdateSnapshot>? publish = null)
    {
        string? target = null;
        bool consent = false;
        bool verified = false;
        try
        {
            publish?.Invoke(new("checking"));
            target = await discover(token);
            token.ThrowIfCancellationRequested();
            if (target == null || SemanticVersion.Parse(target) <= SemanticVersion.Parse(installed))
                return new(false, new("current"));
            publish?.Invoke(new("available", Version: target));
            consent = await confirm(target);
            token.ThrowIfCancellationRequested();
            if (!consent) return new(false, new("available", Version: target));
            publish?.Invoke(new("downloading", Version: target));
            await downloadAndVerify(progress => publish?.Invoke(new("downloading", Math.Clamp(progress, 0, 100), Version: target)), token);
            token.ThrowIfCancellationRequested();
            verified = true;
            publish?.Invoke(new("applying", 100, Version: target));
            await apply(target);
            return new(true, new("staged", 100, Version: target));
        }
        catch (Exception error)
        {
            log(error);
            var failure = new UpdateSnapshot(verified ? "apply-error" : consent ? "error" : "unavailable",
                Error: "Обновление не установлено. " + UpdateFeedback.Explain(error), Version: target ?? "");
            publish?.Invoke(failure);
            return new(false, failure);
        }
    }
}
