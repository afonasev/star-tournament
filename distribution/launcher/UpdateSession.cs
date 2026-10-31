namespace StarTournament.Distribution;

public sealed record UpdateSnapshot(string State, int Progress = 0, string Error = "",
    bool RestartAcknowledged = false, string Version = "", bool MatchPaused = false, string MatchRequest = "", ReleaseHistorySnapshot? History = null);

// Owns one launch's discovery, selected target and consent. Unity never owns networking.
public sealed class UpdateSession : IAsyncDisposable
{
    readonly object gate = new();
    readonly SemaphoreSlim commands = new(1, 1);
    readonly Func<CancellationToken, Task<string?>> discover;
    readonly Func<Action<int>, CancellationToken, Task> download;
    readonly Action stage;
    readonly Action<UpdateSnapshot> publish;
    readonly Func<CancellationToken, Task<ReleaseHistorySnapshot>>? historyFetch;
    CancellationTokenSource? historyCancellation;
    Task historyOperation = Task.CompletedTask;
    bool historyResume;
    readonly Func<string, CancellationToken, Task<ReleaseHistorySnapshot?>>? mediaFetch;
    CancellationTokenSource? mediaCancellation;
    Task mediaOperation = Task.CompletedTask;
    bool historyOpen;
    CancellationTokenSource? cancellation;
    Task operation = Task.CompletedTask;
    UpdateSnapshot state;
    string version;
    bool started, match, consent, resume, closing;
    public UpdateSnapshot State { get { lock (gate) return state; } }

    public UpdateSession(Func<CancellationToken, Task<string?>> discover,
        Func<Action<int>, CancellationToken, Task> download, Action stage, Action<UpdateSnapshot> publish,
        UpdateSnapshot? initial = null, Func<CancellationToken, Task<ReleaseHistorySnapshot>>? historyFetch = null,
        Func<string, CancellationToken, Task<ReleaseHistorySnapshot?>>? mediaFetch = null)
    {
        this.mediaFetch = mediaFetch; this.historyFetch = historyFetch; this.discover = discover; this.download = download; this.stage = stage; this.publish = publish;
        state = initial ?? new("checking"); version = state.Version; publish(state);
    }
    void Status(string name, int progress = 0, string error = "")
    { state = state with { State = name, Progress = progress, Error = error, Version = version }; publish(state); }
    public void Start()
    {
        lock (gate)
        {
            if (started || closing) return; started = true;
            if (state.State != "checking") return;
            if (match) { Status("unavailable", error: "Проверка прервана перед началом матча."); return; }
            cancellation = new(); operation = Discover(cancellation.Token);
        }
    }
    async Task Discover(CancellationToken token)
    {
        try
        {
            var found = await discover(token); token.ThrowIfCancellationRequested();
            lock (gate)
            {
                if (closing || token.IsCancellationRequested) return;
                version = found ?? ""; Status(found == null ? "current" : "available");
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        { lock (gate) if (!closing) Status("unavailable", error: "Проверка прервана перед началом матча."); }
        catch (Exception ex)
        { UpdateFeedback.Log("check-error", ex); lock (gate) if (!closing) Status("unavailable", error: "Не удалось проверить обновления. " + UpdateFeedback.Explain(ex)); }
    }
    void BeginDownload()
    {
        cancellation?.Dispose(); cancellation = new();
        Status("downloading"); operation = Download(cancellation.Token);
    }
    async Task Download(CancellationToken token)
    {
        try
        {
            await download(value =>
            {
                lock (gate)
                    if (!closing && !match && consent && !token.IsCancellationRequested)
                        Status(value >= 100 ? "verifying" : "downloading", Math.Clamp(value, 0, 100));
            }, token);
            lock (gate)
            {
                if (closing || match || !consent || token.IsCancellationRequested) return;
                // Serialize consent withdrawal with the only durable ready-manifest write.
                stage(); Status("staged", 100);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        { UpdateFeedback.Log("download-error", ex); lock (gate) if (!closing && !match && consent && !token.IsCancellationRequested) Status("error", error: "Не удалось скачать обновление. " + UpdateFeedback.Explain(ex)); }
    }
    void BeginHistory()
    {
        historyCancellation?.Dispose(); historyCancellation = new();
        state = state with { History = new("loading", state.History?.Items ?? Array.Empty<ReleaseHistoryItem>()) }; publish(state);
        historyOperation = FetchHistory(historyCancellation.Token);
    }
    async Task FetchHistory(CancellationToken token)
    {
        try
        {
            var result = await historyFetch!(token);
            lock (gate) if (!closing && !match && !token.IsCancellationRequested) { state = state with { History = result }; publish(state); }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        { UpdateFeedback.Log("history-error", ex); lock (gate) if (!closing && !match && !token.IsCancellationRequested) { state = state with { History = new("error", Array.Empty<ReleaseHistoryItem>(), "Не удалось получить историю версий.") }; publish(state); } }
    }
    async Task FetchMedia(string version, CancellationToken token)
    {
        try
        {
            var result = await mediaFetch!(version, token);
            lock (gate) if (result != null && !closing && !match && historyOpen && !token.IsCancellationRequested)
            { state = state with { History = result }; publish(state); }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { UpdateFeedback.Log("history-media", ex); }
    }
    public async Task<bool> CommandAsync(string action, string request = "")
    {
        await commands.WaitAsync();
        try { return await CommandCore(action, request); }
        finally { commands.Release(); }
    }
    async Task<bool> CommandCore(string action, string request)
    {
        Task? drain = null;
        lock (gate)
        {
            if (closing) return false;
            if (action == "history-media")
            {
                if (match || !historyOpen || mediaFetch == null || !historyOperation.IsCompleted || state.History?.Items.Any(x => x.Version == request) != true) return false;
                mediaCancellation?.Cancel(); drain = mediaOperation;
            }
            else if (action == "history-close")
            {
                historyOpen = historyResume = false; historyCancellation?.Cancel(); mediaCancellation?.Cancel();
                drain = Task.WhenAll(historyOperation, mediaOperation);
            }
            else if (action == "match")
            {
                match = true; historyResume = !historyOperation.IsCompleted;
                if (historyResume) historyCancellation?.Cancel();
                mediaCancellation?.Cancel();
                resume = consent && state.State is "downloading" or "verifying";
                state = state with { MatchPaused = false, MatchRequest = request };
                if (!operation.IsCompleted) cancellation?.Cancel();
                drain = Task.WhenAll(operation, historyOperation, mediaOperation);
            }
            else if (action == "menu")
            {
                if (request != state.MatchRequest) return false;
                match = false; state = state with { MatchPaused = false }; publish(state);
                if (historyResume && historyFetch != null) { historyResume = false; BeginHistory(); }
                if (resume && consent && version.Length > 0 && operation.IsCompleted) { resume = false; BeginDownload(); }
                return false;
            }
            else if (action == "history")
            {
                if (match || historyFetch == null) return false;
                historyOpen = true;
                if (!historyOperation.IsCompleted || state.History?.State is "ready" or "cached") return false;
                BeginHistory(); return false;
            }
            else if (action == "cancel")
            {
                if (state.State is not ("downloading" or "verifying" or "paused")) return false;
                consent = resume = false; cancellation?.Cancel(); drain = operation; Status("cancelling");
            }
            else if (action == "download")
            {
                if (match || !operation.IsCompleted || version.Length == 0 || state.State is not ("available" or "error")) return false;
                consent = true; BeginDownload(); return false;
            }
            else if (action == "restart")
            {
                if (match || state.State is not ("staged" or "apply-error")) return false;
                state = state with { RestartAcknowledged = true }; publish(state); return true;
            }
            else return false; // No command can repeat discovery, including stale legacy "check".
        }
        if (drain != null) await drain;
        lock (gate)
        {
            if (closing) return false;
            if (action == "history-media")
            {
                if (!match && historyOpen)
                { mediaCancellation?.Dispose(); mediaCancellation = new(); mediaOperation = FetchMedia(request, mediaCancellation.Token); }
            }
            else if (action == "match")
            {
                if (resume) Status("paused", state.Progress);
                state = state with { MatchPaused = true }; publish(state);
            }
            else if (action == "cancel") Status("available");
        }
        return false;
    }
    public async ValueTask DisposeAsync()
    {
        Task pending;
        lock (gate) { closing = true; consent = resume = false; cancellation?.Cancel(); historyCancellation?.Cancel(); mediaCancellation?.Cancel(); pending = Task.WhenAll(operation, historyOperation, mediaOperation); }
        await pending;
        lock (gate) { cancellation?.Dispose(); cancellation = null; historyCancellation?.Dispose(); historyCancellation = null; mediaCancellation?.Dispose(); mediaCancellation = null; }
    }
}
