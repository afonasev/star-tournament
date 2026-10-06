using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Velopack;
using Velopack.Locators;
using StarTournament.Distribution;

VelopackApp.Build().SetAutoApplyOnStartup(false).Run();
return await Launcher.Run(args);

static class Launcher
{
    static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
    sealed record Snapshot(string State, int Progress = 0, string Error = "", bool RestartAcknowledged = false);
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
        var baseDir = AppContext.BaseDirectory;
        var logRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StarTournament", "launcher");
        Directory.CreateDirectory(logRoot);
        void Log(Exception ex) => File.AppendAllText(Path.Combine(logRoot, "errors.log"), DateTimeOffset.UtcNow + " " + ex + Environment.NewLine);
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
            catch (JsonException ex) { Log(ex); return 1; }
            File.Delete(playerRecord);
        }
        var key = File.ReadAllText(Path.Combine(baseDir, "update-public.pem"));
        using var source = new AuthenticatedSource(config, key);
        var locator = VelopackLocator.Current;
        var machineRoot = WindowsInstallation.MachineRoot(locator);
        bool Installing() => machineRoot != null && File.Exists(Path.Combine(machineRoot, "maintenance", "installing.flag"));
        if (Installing()) return 1;
        using var installationLease = machineRoot == null ? null : WindowsInstallLease.TryAcquire(machineRoot);
        if (machineRoot != null && (installationLease == null || Installing())) return 1;
        var manager = new UpdateManager(source);
        var packages = locator.PackagesDir ?? throw new InvalidOperationException("Package cache missing");
        string PackagePath(VelopackAsset asset) => Path.Combine(packages, Path.GetFileName(asset.FileName));
        var stagedManifest = Path.Combine(packages, "star-authenticated.json");
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
        catch (Exception ex) { Log(ex); File.Delete(stagedManifest); } // Invalid staged bytes are never activated.
        if (stagedAtStartup != null && (machineRoot == null || !WindowsInstallation.Declined(packages, stagedAtStartup.Version.ToString())))
        {
            try
            {
                if (machineRoot != null) WindowsInstallation.Activate(machineRoot, packages, true, restartArguments);
                else manager.WaitExitThenApplyUpdates(stagedAtStartup, silent: true, restart: true, restartArgs: restartArguments);
                return 0; // No Unity process has started yet.
            }
            catch (Exception ex) { Log(ex); } // A failed handoff retains the authenticated staged package and starts the installed game.
        }

        var session = Path.Combine(logRoot, "sessions", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(session);
        var statusPath = Path.Combine(session, "status.json");
        var commandPath = Path.Combine(session, "command.json");
        var stateLock = new object();
        Snapshot state = new("checking");
        bool restart = false;
        void Status(Snapshot next)
        {
            lock (stateLock) { state = next; AtomicWrite(statusPath, JsonSerializer.Serialize(next, Json)); }
        }
        Status(state);
        using var cancellation = new CancellationTokenSource();
        UpdateInfo? available = null;
        async Task Check()
        {
            available = null;
            try
            {
                available = await manager.CheckForUpdatesAsync();
                Status(new(available == null ? "current" : "available"));
            }
            catch (Exception ex) { Log(ex); Status(new("unavailable", Error: "Не удалось проверить обновления")); }
        }
        var operation = stagedAtStartup == null ? Check() : Task.CompletedTask;
        if (stagedAtStartup != null) Status(new("staged", 100));
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
        using var game = Process.Start(start) ?? throw new InvalidOperationException("Player did not start");
        AtomicWrite(playerRecord, JsonSerializer.Serialize(new { pid = game.Id, started = game.StartTime.ToUniversalTime().Ticks }));
        try
        {
            while (!game.HasExited)
            {
                if (File.Exists(commandPath))
                {
                    Snapshot current; lock (stateLock) current = state;
                    string? command = null;
                    try { command = JsonDocument.Parse(await File.ReadAllTextAsync(commandPath)).RootElement.GetProperty("action").GetString(); File.Delete(commandPath); }
                    catch (IOException) { }
                    catch (JsonException ex) { Log(ex); File.Delete(commandPath); }
                    catch (KeyNotFoundException ex) { Log(ex); File.Delete(commandPath); }
                    if (command == "download" && operation.IsCompleted && current.State != "staged")
                    {
                        operation = Download();
                    }
                    else if (command == "restart" && current.State == "staged")
                    {
                        restart = true; Status(current with { RestartAcknowledged = true });
                    }
                    else if (command == "check" && operation.IsCompleted && current.State != "staged") operation = Check();
                }
                await Task.Delay(100);
            }
        }
        finally
        {
            cancellation.Cancel();
            try { await operation; } catch (OperationCanceledException) { }
            Directory.Delete(session, true);
            if (game.HasExited) File.Delete(playerRecord);
        }
        if (game.ExitCode != 0) return game.ExitCode; // A crash is not an explicit update activation.
        try
        {
            var ready = await Staged(); // Reauthenticate after the actual Player has finished.
            if (ready != null)
            {
                if (machineRoot != null)
                {
                    if (restart || !WindowsInstallation.Declined(packages, ready.Version.ToString()))
                        WindowsInstallation.Activate(machineRoot, packages, restart, restartArguments);
                }
                else manager.WaitExitThenApplyUpdates(ready, silent: true, restart: restart, restartArgs: restartArguments);
            }
        }
        catch (Exception ex) { Log(ex); File.Delete(stagedManifest); }
        return 0;

        async Task Download()
        {
            try
            {
                if (available == null) { await Check(); if (available == null) return; }
                var asset = available.TargetFullRelease;
                var cached = PackagePath(asset);
                // The SDK skips its source when a complete cache file exists.
                if (File.Exists(cached))
                {
                    try { await source.VerifyPackage(cached, asset, cancellation.Token); }
                    catch (System.Security.Cryptography.CryptographicException) { File.Delete(cached); }
                }
                Status(new("downloading"));
                await manager.DownloadUpdatesAsync(available, value => Status(new("downloading", value)), cancellation.Token);
                await source.VerifyPackage(cached, asset, cancellation.Token);
                AtomicWrite(stagedManifest, source.Envelope!);
                Status(new("staged", 100));
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Log(ex); Status(new("error", Error: "Не удалось скачать обновление. Попробуйте ещё раз.")); }
        }
    }
}
