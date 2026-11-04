namespace StarTournament.Distribution;

/// <summary>Cancellation owns and drains one transfer; it never aborts installation.</summary>
public sealed class LauncherTransfer
{
    readonly object gate = new();
    CancellationTokenSource? active;
    bool cancelPending;
    public void Cancel() { lock(gate){cancelPending=true;active?.Cancel();} }
    public async Task Run(Func<Action<int>,CancellationToken,Task> operation,Action<int> progress,CancellationToken token)
    {
        using var pending=CancellationTokenSource.CreateLinkedTokenSource(token);
        lock(gate){if(active!=null)throw new InvalidOperationException("A launcher transfer is already running");active=pending;if(cancelPending)pending.Cancel();}
        try {await operation(progress,pending.Token);pending.Token.ThrowIfCancellationRequested();}
        catch(OperationCanceledException) when(pending.IsCancellationRequested&&!token.IsCancellationRequested){throw new BootstrapDownloadCancelledException();}
        finally{lock(gate){active=null;cancelPending=false;}}
    }
}
