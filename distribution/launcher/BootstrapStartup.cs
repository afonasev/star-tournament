using Velopack;

namespace StarTournament.Distribution;

public sealed record BootstrapStartupResult(bool HandedOff, UpdateSnapshot Snapshot, bool ErrorAcknowledged = false, bool QuitRequested = false);

/// <summary>Discovery is once; retry stays on the same authenticated candidate and consent.
/// Apply must acknowledge a recoverable handoff, never merely spawn an updater.</summary>
public static class BootstrapStartup
{
    public static async Task<BootstrapStartupResult> Run(string installed,
        Func<CancellationToken, Task<string?>> discover, Func<string, Task<bool>> confirm,
        Func<Action<int>, CancellationToken, Task> downloadAndVerify, Func<string, Task> apply, Action<Exception> log,
        CancellationToken token = default, Action<UpdateSnapshot>? publish = null,
        Func<UpdateSnapshot, bool, Task<BootstrapFailureChoice>>? failure = null, Func<bool>? admitApply = null)
    {
        string? target = null; bool consent = false, verified = false;
        try
        {
            publish?.Invoke(new("checking"));
            target = await discover(token); token.ThrowIfCancellationRequested();
            if (target == null || SemanticVersion.Parse(target) <= SemanticVersion.Parse(installed)) return new(false, new("current"));
            publish?.Invoke(new("available", Version: target));
            consent = await confirm(target); token.ThrowIfCancellationRequested();
            if (!consent) return new(false, new("available", Version: target));
            for (;;)
            {
                try
                {
                    publish?.Invoke(new("downloading", Version: target));
                    await downloadAndVerify(progress => publish?.Invoke(new("downloading", Math.Clamp(progress,0,100), Version: target)), token);
                    token.ThrowIfCancellationRequested();
                    if (admitApply != null && !admitApply()) throw new BootstrapDownloadCancelledException();
                    verified = true;
                    publish?.Invoke(new("applying",100,Version:target));
                    await apply(target);
                    return new(true,new("staged",100,Version:target));
                }
                catch (BootstrapDownloadCancelledException) when (!token.IsCancellationRequested)
                {
                    publish?.Invoke(new("launching"));
                    return new(false,new("available",Version:target)); // Drain completed; installed game may now start.
                }
                catch (Exception error) when (!verified && failure != null && !token.IsCancellationRequested)
                {
                    log(error);
                    var state = new UpdateSnapshot("error",Error:"Обновление не установлено. "+UpdateFeedback.Explain(error),Version:target);
                    publish?.Invoke(state);
                    var choice = await failure(state,true);
                    if(choice==BootstrapFailureChoice.Quit)return new(false,state,ErrorAcknowledged:true,QuitRequested:true);
                    token.ThrowIfCancellationRequested();
                    if (choice == BootstrapFailureChoice.Retry) continue;
                    return new(false,state,ErrorAcknowledged:true,QuitRequested:choice==BootstrapFailureChoice.Quit);
                }
            }
        }
        catch (Exception error)
        {
            log(error);
            var state = new UpdateSnapshot(verified?"apply-error":consent?"error":"unavailable",Error:"Обновление не установлено. "+UpdateFeedback.Explain(error),Version:target??"");
            publish?.Invoke(state);
            if (failure != null && !token.IsCancellationRequested)
            { var choice=await failure(state,false); return new(false,state,ErrorAcknowledged:true,QuitRequested:choice==BootstrapFailureChoice.Quit); }
            return new(false,state);
        }
    }
}
