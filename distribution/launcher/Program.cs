using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Velopack;
using Velopack.Locators;
using StarTournament.Distribution;

try
{
    VelopackApp.Build().SetAutoApplyOnStartup(false).Run();
    return await Launcher.Run(args);
}
catch (Exception ex)
{
    UpdateFeedback.Log("launcher-fatal", ex);
    UpdateFeedback.Show("Не удалось запустить игру. " + UpdateFeedback.Explain(ex));
    return 1;
}

static class Launcher
{
    static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
    static void AtomicWrite(string path, string value)
    {
        File.WriteAllText(path + ".tmp", value); File.Move(path + ".tmp", path, true);
    }

    public static async Task<int> Run(string[] args)
    {
        var restartArguments = args;
        var qaIndex = Array.IndexOf(args, "--qa-unity-runner");
        string? qaRunner = null;
        if (qaIndex >= 0)
        {
            if (qaIndex + 1 >= args.Length || !Path.IsPathFullyQualified(args[qaIndex + 1]) || !File.Exists(args[qaIndex + 1]))
                throw new ArgumentException("Absolute existing local QA runner required");
            qaRunner = args[qaIndex + 1];
            args = args.Where((_, index) => index != qaIndex && index != qaIndex + 1).ToArray();
        }
        bool qaLaunch = qaRunner != null || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_UNITY_RUNNER"));
        bool quietLaunch = qaLaunch || args.Contains("-noaudio") || args.Contains("-muted");
        if (quietLaunch)
        {
            if (qaLaunch) args = args.Where(arg => arg != "-humanAudio").ToArray();
            if (!args.Contains("-noaudio")) args = args.Append("-noaudio").ToArray();
            if (!args.Contains("-muted")) args = args.Append("-muted").ToArray();
            if (qaLaunch) restartArguments = restartArguments.Where(arg => arg != "-humanAudio").ToArray();
            if (!restartArguments.Contains("-noaudio")) restartArguments = restartArguments.Append("-noaudio").ToArray();
            if (!restartArguments.Contains("-muted")) restartArguments = restartArguments.Append("-muted").ToArray();
            var qaEnvironmentRunner = Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_UNITY_RUNNER");
            if (qaLaunch && qaRunner == null && !string.IsNullOrEmpty(qaEnvironmentRunner))
                restartArguments = restartArguments.Concat(new[] { "--qa-unity-runner", qaEnvironmentRunner }).ToArray();
        }
        // Preserve the selected launch mode when staged updates relaunch this Player.
        var baseDir = AppContext.BaseDirectory;
        var logRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StarTournament", "launcher");
        Directory.CreateDirectory(logRoot);
        void Log(Exception ex) => UpdateFeedback.Log("launcher", ex);
        var config = JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(Path.Combine(baseDir, "release.json")), Json)!;
        if (config.UpdateChannel is not ("test" or "production"))
            throw new InvalidOperationException("GitHub update channel required");
        FileStream ownership;
        try { ownership = new FileStream(Path.Combine(logRoot, config.AppId + ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { return 0; }
        using var owned = ownership;
        var playerRecord = Path.Combine(logRoot, config.AppId + ".player.json");
        if (File.Exists(playerRecord))
        {
            try
            {
                using var old = JsonDocument.Parse(File.ReadAllText(playerRecord));
                using var live = Process.GetProcessById(old.RootElement.GetProperty("pid").GetInt32());
                if (!live.HasExited && live.StartTime.ToUniversalTime().Ticks == old.RootElement.GetProperty("started").GetInt64())
                    return 0; // An orphaned live Player must finish before any replacement.
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
            catch (JsonException ex) { throw new IOException("Invalid recorded Player identity", ex); }
            File.Delete(playerRecord);
        }
        var key = File.ReadAllText(Path.Combine(baseDir, "update-public.pem"));
        using var source = new AuthenticatedSource(config, key);
        var locator = VelopackLocator.Current;
        var machineRoot = WindowsInstallation.MachineRoot(locator);
        bool Installing() => machineRoot != null && File.Exists(Path.Combine(machineRoot, "maintenance", "installing.flag"));
        if (Installing()) { UpdateFeedback.Show("Установщик изменяет файлы игры. Дождитесь его завершения и запустите игру снова."); return 1; }
        using var installationLease = machineRoot == null ? null : WindowsInstallLease.TryAcquire(machineRoot);
        if (machineRoot != null && (installationLease == null || Installing()))
        { UpdateFeedback.Show("Установка игры занята другим процессом. Дождитесь завершения установки или обновления и запустите игру снова."); return 1; }
        var manager = new UpdateManager(source);
        var packages = locator.PackagesDir ?? throw new InvalidOperationException("Package cache missing");
        string PackagePath(VelopackAsset asset) => Path.Combine(packages, Path.GetFileName(asset.FileName));
        var stagedManifest = Path.Combine(packages, "star-authenticated.json");
        var journal = UpdateAttemptJournal.ForApp(config.AppId);
        journal.ClearIfInstalled(config.Version);
        string? startupError = null;
        void Apply(VelopackAsset asset, bool restartGame)
        {
            journal.Begin(asset.Version.ToString());
            UpdateFeedback.Log("apply-handoff target=" + asset.Version);
            if (machineRoot != null) WindowsInstallation.Activate(machineRoot, packages, restartGame, restartArguments);
            else manager.WaitExitThenApplyUpdates(asset, silent: true, restart: restartGame, restartArgs: restartArguments);
        }
        if (!manager.IsInstalled || manager.CurrentVersion!.ToString() != config.Version)
            throw new InvalidOperationException("Release identity does not match installed package");

        async Task<VelopackAsset?> Staged()
        {
            if (!File.Exists(stagedManifest)) return null;
            if (new FileInfo(stagedManifest).Length > 65536) throw new System.Security.Cryptography.CryptographicException("Staged metadata exceeds limit");
            using var verifier = new AuthenticatedSource(config, key);
            verifier.Admit(await File.ReadAllTextAsync(stagedManifest));
            var asset = verifier.Asset!;
            if (asset.Version <= manager.CurrentVersion) { File.Delete(stagedManifest); return null; }
            await verifier.VerifyPackage(PackagePath(asset), asset);
            return asset;
        }

        VelopackAsset? stagedAtStartup = null;
        try { stagedAtStartup = await Staged(); }
        catch (Exception ex) { Log(ex); File.Delete(stagedManifest); startupError = "Обновление не установлено. " + UpdateFeedback.Explain(ex); } // Invalid staged bytes are never activated.
        if (stagedAtStartup != null && !journal.AlreadyAttempted(stagedAtStartup.Version.ToString()) &&
            (machineRoot == null || !WindowsInstallation.Declined(packages, stagedAtStartup.Version.ToString())))
        {
            try
            {
                Apply(stagedAtStartup, true);
                return 0; // No Unity process has started yet.
            }
            catch (Exception ex) { Log(ex); startupError = "Обновление не установлено. " + UpdateFeedback.Explain(ex); journal.Fail(stagedAtStartup.Version.ToString(), startupError); } // A failed handoff retains the authenticated staged package and starts the installed game.
        }

        var session = Path.Combine(logRoot, "sessions", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(session);
        var statusPath = Path.Combine(session, "status.json");
        var commandPath = Path.Combine(session, "command.json");
        bool restart = false;
        UpdateInfo? available = null;
        async Task<string?> Check(CancellationToken token)
        {
            source.CheckCancellationToken = token;
            UpdateFeedback.Log("check-begin installed=" + config.Version);
            available = await manager.CheckForUpdatesAsync();
            token.ThrowIfCancellationRequested();
            UpdateFeedback.Log("check-complete target=" + available?.TargetFullRelease.Version);
            return available?.TargetFullRelease.Version.ToString();
        }
        UpdateSnapshot? initial = null;
        if (stagedAtStartup != null)
        {
            var previousAttempt = journal.Read();
            var failure = startupError ?? (journal.AlreadyAttempted(stagedAtStartup.Version.ToString()) ? previousAttempt?.Message : null);
            if (failure == null && machineRoot != null && WindowsInstallation.Declined(packages, stagedAtStartup.Version.ToString()))
                failure = "Предыдущая установка отменена или завершилась с ошибкой. Повторите установку и подтвердите запрос Windows.";
            initial = new(failure == null ? "staged" : "apply-error", 100, failure ?? "", Version: stagedAtStartup.Version.ToString());
        }
        else if (startupError != null) initial = new("error", Error: startupError);
        using var history = new ReleaseHistory(config, Path.Combine(UpdateFeedback.LogRoot, Path.GetFileName(config.AppId) + ".history.json"));
        await using var updates = new UpdateSession(Check, Download,
            () => AtomicWrite(stagedManifest, source.Envelope ?? throw new InvalidOperationException("No authenticated target")),
            next => AtomicWrite(statusPath, JsonSerializer.Serialize(next, Json)), initial, history.Fetch);
        updates.Start();
        var executable = Path.GetFullPath(Path.Combine(baseDir, config.GameExecutable));
        if (!executable.StartsWith(Path.GetFullPath(baseDir), StringComparison.Ordinal))
            throw new InvalidOperationException("Player must be inside release payload");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(executable)! };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        start.Environment["STAR_TOURNAMENT_UPDATE_STATUS"] = statusPath;
        start.Environment["STAR_TOURNAMENT_UPDATE_COMMAND"] = commandPath;
        start.Environment["STAR_TOURNAMENT_RELEASE_LABEL"] = config.Version + " (" +
            DateOnly.ParseExact(config.PublicationDate, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) + ")";
        // Local QA only: the actual Unity child remains guarded for its whole lifetime.
        var runner = qaRunner ?? Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_UNITY_RUNNER");
        if (!string.IsNullOrEmpty(runner))
        {
            var guarded = new ProcessStartInfo(runner) { UseShellExecute = false, WorkingDirectory = start.WorkingDirectory };
            guarded.ArgumentList.Add("--exclusive"); guarded.ArgumentList.Add("--"); guarded.ArgumentList.Add(executable);
            foreach (var arg in args) guarded.ArgumentList.Add(arg);
            foreach (var pair in start.Environment) guarded.Environment[pair.Key] = pair.Value;
            start = guarded;
        }
        UpdateFeedback.Log("player-start path=" + executable);
        using var game = Process.Start(start) ?? throw new InvalidOperationException("Player did not start");
        AtomicWrite(playerRecord, JsonSerializer.Serialize(new { pid = game.Id, started = game.StartTime.ToUniversalTime().Ticks }));
        try
        {
            while (!game.HasExited)
            {
                if (File.Exists(commandPath))
                {
                    string? command = null; string request = "";
                    try
                    {
                        using var data = JsonDocument.Parse(await File.ReadAllTextAsync(commandPath));
                        command = data.RootElement.GetProperty("action").GetString();
                        if (data.RootElement.TryGetProperty("id", out var id)) request = id.GetString() ?? "";
                        File.Delete(commandPath);
                    }
                    catch (IOException) { }
                    catch (JsonException ex) { Log(ex); File.Delete(commandPath); }
                    catch (KeyNotFoundException ex) { Log(ex); File.Delete(commandPath); }
                    if (command != null) restart |= await updates.CommandAsync(command, request);
                }
                await Task.Delay(100);
            }
        }
        finally
        {
            await updates.DisposeAsync();
            Directory.Delete(session, true);
            if (game.HasExited) File.Delete(playerRecord);
        }
        UpdateFeedback.Log("player-exit code=" + game.ExitCode);
        if (game.ExitCode != 0) { UpdateFeedback.Show("Игра завершилась с ошибкой (код " + game.ExitCode + "). Обновление не применено. Проверьте журнал запуска игры."); return game.ExitCode; } // A crash is not an explicit update activation.
        try
        {
            var ready = await Staged(); // Reauthenticate after the actual Player has finished.
            if (ready != null)
            {
                if (machineRoot != null)
                {
                    if (restart || (!journal.AlreadyAttempted(ready.Version.ToString()) && !WindowsInstallation.Declined(packages, ready.Version.ToString())))
                        Apply(ready, restart);
                }
                else if (restart || !journal.AlreadyAttempted(ready.Version.ToString())) Apply(ready, restart);
            }
        }
        catch (Exception ex) { Log(ex); UpdateFeedback.Show("Обновление не установлено. " + UpdateFeedback.Explain(ex)); }
        return 0;

        async Task Download(Action<int> progress, CancellationToken token)
        {
            // Only this launch's selected UpdateInfo is retried. No implicit discovery.
            var selected = available ?? throw new InvalidOperationException("No selected update");
            var asset = selected.TargetFullRelease;
            UpdateFeedback.Log("download-begin target=" + asset.Version);
            var cached = PackagePath(asset);
            if (File.Exists(cached))
            {
                try { await source.VerifyPackage(cached, asset, token); }
                catch (System.Security.Cryptography.CryptographicException) { File.Delete(cached); }
            }
            await manager.DownloadUpdatesAsync(selected, progress, token);
            await source.VerifyPackage(cached, asset, token);
            token.ThrowIfCancellationRequested();
            UpdateFeedback.Log("download-authenticated target=" + asset.Version);
        }
    }
}
