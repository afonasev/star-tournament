using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Velopack;
using Velopack.Locators;
using StarTournament.Distribution;

try
{
    if (args.FirstOrDefault() == "--recovery-supervisor") return await MacRecoverySupervisor.Run(args);
    var payload = BootstrapHost.Payload(ref args);
    var app = VelopackApp.Build().SetAutoApplyOnStartup(false);
    if (payload != null) app.SetLocator(VelopackLocator.CreateDefaultForPlatform(new BootstrapHost.PayloadProcess(payload)));
    app.Run();
    if (payload == null && OperatingSystem.IsMacOS())
    {
        using var host = BootstrapHost.RouteMac(AppContext.BaseDirectory, args);
        if (host != null) return 0;
    }
    return await Launcher.Run(args, payload);
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

    public static async Task<int> Run(string[] args, string? payload = null)
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
        var baseDir = payload ?? AppContext.BaseDirectory;
        var logRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StarTournament", "launcher");
        Directory.CreateDirectory(logRoot);
        void Log(Exception ex) => UpdateFeedback.Log("launcher", ex);
        var config = JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(Path.Combine(baseDir, "release.json")), Json)!;
        PlatformRequirements.RequireSupported(config.Platform);
        if (payload == null && config.Compatibility != null) ReleaseCompatibility.Require(config.Compatibility, config.Platform);
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
        var key = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "update-public.pem"));
        var locator = VelopackLocator.Current;
        var markedRoot=WindowsInstallation.MarkedRoot(locator);
        if(markedRoot!=null)await WindowsBrokerUpgradeStartup.EnsureWindows(markedRoot,config);
        var machineRoot = WindowsInstallation.MachineRoot(locator);
        var readerProtocol = machineRoot == null ? ReleaseCompatibility.ReaderProtocol : WindowsInstallation.MetadataReader(machineRoot);
        using var source = new AuthenticatedSource(config, key, discoveryCachePath: Path.Combine(logRoot, "release-discovery-v1"), readerProtocol: readerProtocol, bootstrap: payload != null);
        bool Installing() => machineRoot != null && File.Exists(Path.Combine(machineRoot, "maintenance", "installing.flag"));
        if (Installing()) { UpdateFeedback.Show("Установщик изменяет файлы игры. Дождитесь его завершения и запустите игру снова."); return 1; }
        using var installationLease = machineRoot == null ? null : WindowsInstallLease.TryAcquire(machineRoot);
        if (machineRoot != null && (installationLease == null || Installing()))
        { UpdateFeedback.Show("Установка игры занята другим процессом. Дождитесь завершения установки или обновления и запустите игру снова."); return 1; }
        if(machineRoot!=null&&!WindowsInstallation.InstallerAllowsLaunch(machineRoot))
        {UpdateFeedback.Show("Установка не завершена. Дождитесь мастера или повторите установщик.");return 1;}
        if (machineRoot != null && WindowsInstallation.RecoveryNeedsRepair(machineRoot))
        { UpdateFeedback.Show("Установка прервана. Запустите StarTournamentMaintenance.exe --recover из папки maintenance установленной игры."); return 1; }
        var manager = new UpdateManager(source);
        var packages = locator.PackagesDir ?? throw new InvalidOperationException("Package cache missing");
        string PackagePath(VelopackAsset asset) => Path.Combine(packages, Path.GetFileName(asset.FileName));
        var stagedManifest = Path.Combine(packages, "star-authenticated.json");
        var journal = UpdateAttemptJournal.ForApp(config.AppId);
        var installedRoot = machineRoot ?? locator.RootAppDir ?? throw new IOException("Installed root missing");
        var recovery = StartupRecovery.ForInstallation(config, logRoot, installedRoot);
        recovery.RecoverInterrupted(config.Version);
        var macRecovery = OperatingSystem.IsMacOS() ? new MacRecoverySupervisor(config, logRoot, installedRoot) : null;
        if (macRecovery != null && recovery.Read().Attempt is { } pending)
        {
            if (pending.Phase is "confirmed" or "restored")
            {
                // A settled release remains playable if backup cleanup fails.
                var cleanupDeadline = DateTimeOffset.UtcNow.AddSeconds(30);
                try { while (!macRecovery.CleanupSettled(config.Version) && DateTimeOffset.UtcNow < cleanupDeadline) await Task.Delay(100); }
                catch (IOException ex) { Log(ex); }
            }
            else if (pending.Phase != "prepared" || config.Version != pending.Target)
            {
                if (!macRecovery.IsActive()) macRecovery.StartHost(pending.BackupId, "recover", restartArguments);
                return 0; // Stable old code waits for this exact launcher, restores, then relaunches.
            }
            else if (!macRecovery.IsActive()) macRecovery.StartHost(pending.BackupId, "watch", restartArguments);
        }
        if (machineRoot != null) WindowsInstallation.WatchStartup(machineRoot, config.Version, restartArguments);
        var knownHealthy = recovery.Read().HealthyVersion == config.Version;
        void Apply(VelopackAsset asset, bool restartGame, bool explicitRetry = false)
        {
            journal.Begin(asset.Version.ToString());
            UpdateFeedback.Log("apply-handoff target=" + asset.Version);
            if (machineRoot != null) WindowsInstallation.Activate(machineRoot, packages, restartGame, restartArguments, explicitRetry);
            else if (macRecovery != null) macRecovery.PrepareAndStart(locator.RootAppDir ?? throw new IOException("Installed bundle missing"),
                packages, asset, File.ReadAllText(stagedManifest), restartGame, restartArguments);
            else throw new InvalidOperationException("Unsupported recovery platform");
        }
        if (!manager.IsInstalled || manager.CurrentVersion!.ToString() != config.Version)
            throw new InvalidOperationException("Release identity does not match installed package");

        async Task<VelopackAsset?> Staged(CancellationToken token = default)
        {
            if (!File.Exists(stagedManifest)) return null;
            if (new FileInfo(stagedManifest).Length > 65536) throw new System.Security.Cryptography.CryptographicException("Staged metadata exceeds limit");
            using var verifier = new AuthenticatedSource(config, key, readerProtocol: readerProtocol, bootstrap: payload != null);
            verifier.Admit(await File.ReadAllTextAsync(stagedManifest, token));
            verifier.RequireCompatibleTarget();
            var asset = verifier.Asset!;
            if (asset.Version <= manager.CurrentVersion) { File.Delete(stagedManifest); return null; }
            await verifier.VerifyPackage(PackagePath(asset), asset, token);
            return asset;
        }

        await using var startupWindow = OperatingSystem.IsWindows() ? await BootstrapStartupWindow.Open() : null;
        var startupToken = startupWindow?.Token ?? CancellationToken.None;
        VelopackAsset? stagedAtStartup = null;
        try { stagedAtStartup = await Staged(startupToken); }
        catch (OperationCanceledException) when (startupToken.IsCancellationRequested) { return 0; }
        catch (Exception ex) { Log(ex); File.Delete(stagedManifest);  } // Invalid staged bytes are never activated.

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
        UpdateSnapshot? initial;
        // Pending first-start/rollback supervision must settle before another replacement.
        // A downloaded package from an earlier launch does not carry consent for this launch.
        if (knownHealthy && recovery.Read().Attempt == null)
        {
            var bootstrap = await BootstrapStartup.Run(config.Version, Check,
                version => startupWindow != null ? startupWindow.Confirm(version) : BootstrapPrompt.Confirm(version),
                Download, async target =>
                {
                    AtomicWrite(stagedManifest, source.Envelope ?? throw new InvalidOperationException("No authenticated target"));
                    var ready = await Staged() ?? throw new IOException("Authenticated staged target missing");
                    if (ready.Version.ToString() != target) throw new IOException("Selected update changed before activation");
                    if (recovery.IsBlocked(target)) recovery.PermitRetry(target);
                    try { Apply(ready, true, explicitRetry: true); }
                    catch (Exception error) { journal.Fail(target, "Обновление не установлено. " + UpdateFeedback.Explain(error)); throw; }
                }, Log, startupToken, next => startupWindow?.Publish(new(next.State, next.Progress, next.Error, next.Version)));
            if (bootstrap.HandedOff) return 0;
            initial = bootstrap.Snapshot;
        }
        else
        {
            // First installation has no verified working fallback yet. Discover now;
            // replacement must wait for its first genuine startup-ready confirmation.
            try
            {
                var target = await Check(startupToken);
                initial = new(target == null ? "current" : "available", Version: target ?? "");
            }
            catch (Exception error) { Log(error); initial = new("unavailable", Error: UpdateFeedback.Explain(error)); }
        }
        if (startupToken.IsCancellationRequested) return 0;
        if (startupWindow != null && !string.IsNullOrEmpty(initial?.Error))
        {
            if (!await startupWindow.ShowError(new(initial.State, initial.Progress, initial.Error, initial.Version))) return 0;
        }
        startupWindow?.Publish(new("launching"));
        using var history = new ReleaseHistory(config, Path.Combine(UpdateFeedback.LogRoot, Path.GetFileName(config.AppId) + ".history.json"));
        await using var updates = new UpdateSession(Check, Download,
            () => AtomicWrite(stagedManifest, source.Envelope ?? throw new InvalidOperationException("No authenticated target")),
            next => AtomicWrite(statusPath, JsonSerializer.Serialize(next, Json)), initial, history.Fetch, history.FetchMedia);
        updates.Start();
        var executable = Path.GetFullPath(Path.Combine(baseDir, config.GameExecutable));
        if (!executable.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(baseDir)) + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
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
            guarded.ArgumentList.Add("--shared"); guarded.ArgumentList.Add("--"); guarded.ArgumentList.Add(executable);
            foreach (var arg in args) guarded.ArgumentList.Add(arg);
            foreach (var pair in start.Environment) guarded.Environment[pair.Key] = pair.Value;
            start = guarded;
        }
        StartupHealthMonitor? startupHealth = null;
        if (recovery.Read().Attempt is not { Phase: "confirmed" or "restored" })
        {
            startupHealth = new StartupHealthMonitor(recovery, config.Version, Path.Combine(session, "startup-ready.json"), DateTimeOffset.UtcNow,
                () => { try { journal.ClearIfInstalled(config.Version); } catch (IOException ex) { Log(ex); }
                    UpdateFeedback.Log("startup-health-confirmed version=" + config.Version); },
                beforeConfirm: () => {
                    if (machineRoot != null)
                    { try { WindowsInstallation.MarkStartupReady(machineRoot, config.Version); }
                      catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { Log(ex); return false; } }
                    return true;
                });
            start.Environment["STAR_TOURNAMENT_STARTUP_READY"] = startupHealth.ReadyPath;
            start.Environment["STAR_TOURNAMENT_STARTUP_VERSION"] = startupHealth.Ticket.Version;
            start.Environment["STAR_TOURNAMENT_STARTUP_TOKEN"] = startupHealth.Ticket.Token;
        }
        bool cleanupTried = false;
        UpdateFeedback.Log("player-start path=" + executable);
        Process game;
        try { game = Process.Start(start) ?? throw new InvalidOperationException("Player did not start"); }
        catch
        {
            try { startupHealth?.End(DateTimeOffset.UtcNow); }
            finally { await updates.DisposeAsync(); Directory.Delete(session, true); }
            throw;
        }
        using var ownedGame = game;
        AtomicWrite(playerRecord, JsonSerializer.Serialize(new { pid = game.Id, started = game.StartTime.ToUniversalTime().Ticks }));
        try
        {
            while (!game.HasExited)
            {
                startupHealth?.Poll(DateTimeOffset.UtcNow);
                if (startupWindow != null && (startupHealth?.IsHealthy ?? knownHealthy))
                    await startupWindow.DisposeAsync();
                if (!cleanupTried && macRecovery != null && recovery.Read().Attempt is { Phase: "confirmed" } && !macRecovery.IsActive())
                { cleanupTried = true; try { macRecovery.CleanupSettled(config.Version); } catch (IOException ex) { Log(ex); } }
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
            try { startupHealth?.End(DateTimeOffset.UtcNow); }
            finally
            {
                await updates.DisposeAsync();
                Directory.Delete(session, true);
                if (game.HasExited) File.Delete(playerRecord);
            }
        }
        UpdateFeedback.Log("player-exit code=" + game.ExitCode);
        if (game.ExitCode != 0) { UpdateFeedback.Show("Игра завершилась с ошибкой (код " + game.ExitCode + "). Обновление не применено. Проверьте журнал запуска игры."); return game.ExitCode; } // A crash is not an explicit update activation.
        if (!(startupHealth?.IsHealthy ?? knownHealthy))
        { UpdateFeedback.Log("startup-health-unconfirmed; staged update retained without activation"); return 0; }
        try
        {
            var ready = await Staged(); // Reauthenticate after the actual Player has finished.
            if (ready != null && recovery.Read().Attempt == null)
            {
                if (!restart) return 0; // Only explicit in-game restart consent permits post-Player activation.
                if (restart && recovery.IsBlocked(ready.Version.ToString())) recovery.PermitRetry(ready.Version.ToString());
                if (recovery.IsBlocked(ready.Version.ToString())) return 0;
                if (machineRoot != null && !restart && WindowsInstallation.IsBlocked(machineRoot, ready.Version.ToString())) return 0;
                if (machineRoot != null)
                {
                    if (restart || (!journal.AlreadyAttempted(ready.Version.ToString()) && !WindowsInstallation.Declined(packages, ready.Version.ToString())))
                        Apply(ready, restart, explicitRetry: restart);
                }
                else if (restart || !journal.AlreadyAttempted(ready.Version.ToString())) Apply(ready, restart, explicitRetry: restart);
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
