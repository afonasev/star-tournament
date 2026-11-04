using System.Runtime.Versioning;

namespace StarTournament.Distribution;

public sealed record BootstrapProgressStatus(string State, int Progress = 0, string Error = "", string Version = "");
public enum BootstrapFailureChoice { Play, Retry, Quit }
public sealed class BootstrapDownloadCancelledException : OperationCanceledException { }

/// <summary>One branded Windows surface. Networking never runs on its UI thread.</summary>
public sealed class BootstrapStartupWindow : IAsyncDisposable, IDisposable
{
    readonly CancellationTokenSource cancellation = new();
    readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly TaskCompletionSource stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly LauncherTransfer transfer = new();
    internal readonly LauncherSurface Surface = new();
    internal volatile bool Closing;
    public CancellationToken Token => cancellation.Token;

    [SupportedOSPlatform("windows")]
    public static async Task<BootstrapStartupWindow> Open(BootstrapProgressStatus? initial = null)
    {
        var window = new BootstrapStartupWindow();
        if (initial != null) window.Publish(initial);
        var thread = new Thread(() =>
        {
            try { new LauncherWindowsHost(window).Run(() => window.ready.TrySetResult()); }
            catch (Exception error)
            {
                bool opening=window.ready.TrySetException(error);window.cancellation.Cancel();UpdateFeedback.Log("launcher-window",error);
                if(!opening)UpdateFeedback.Show("Окно лаунчера завершилось с ошибкой. "+UpdateFeedback.Explain(error));
            }
            finally { window.cancellation.Cancel(); window.Surface.Close(); window.stopped.TrySetResult(); }
        }) { IsBackground = true, Name = "Star Tournament launcher" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        await window.ready.Task;
        return window;
    }
    public void Publish(BootstrapProgressStatus status) => Surface.Publish(status);
    public async Task<bool> Confirm(string version)
    {
        var action=await Surface.Decision(new("available", Version:version));
        if(action==LauncherAction.Quit)cancellation.Cancel();
        return action==LauncherAction.Update;
    }
    public async Task<bool> ShowError(BootstrapProgressStatus failure, bool playable = true)
        => await Failure(failure, false, playable) == BootstrapFailureChoice.Play;
    public async Task<BootstrapFailureChoice> Failure(BootstrapProgressStatus failure, bool retry, bool playable = true)
    {
        var action = await Surface.Decision(playable?failure:failure with { State="blocked" }, retry, playable);
        if(action==LauncherAction.Quit)cancellation.Cancel();
        return action == LauncherAction.Retry ? BootstrapFailureChoice.Retry : action == LauncherAction.Play ? BootstrapFailureChoice.Play : BootstrapFailureChoice.Quit;
    }
    public Task Download(Func<Action<int>, CancellationToken, Task> operation, Action<int> progress, CancellationToken token)
        => transfer.Run(operation,progress,token);
    public bool AdmitApply() => Surface.TryBeginApply();
    internal void Choose(long id, LauncherAction action)
    {
        var before = Surface.View;
        if (!Surface.Choose(id, action)) return;
        if (action == LauncherAction.Notes)
            BootstrapChangelog.Open(before.Status.Version, BootstrapChangelog.ReleaseUri(before.Status.Version).AbsoluteUri);
        else if (action == LauncherAction.Quit) cancellation.Cancel();
        else if (action == LauncherAction.Cancel)
            transfer.Cancel();
    }
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
    public async ValueTask DisposeAsync()
    {
        Closing = true;
        Surface.Close();
        await stopped.Task;
    }
}
