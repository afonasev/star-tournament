using System.Diagnostics;
using System.Text;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Velopack;

namespace StarTournament.Distribution;

public sealed record RecoverySupervisorProcess(int Pid, long StartedTicks);
public sealed record MacRecoveryHandoff(int Schema, string Id, LauncherConfig Config, string InstallRoot,
    string Packages, string Envelope, string Target, bool Restart, string[] Arguments);

/// <summary>Normal-user macOS supervisor, executed from the retained old payload, never from the replacement.</summary>
public sealed class MacRecoverySupervisor
{
    static readonly JsonSerializerOptions Json = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    readonly LauncherConfig config;
    readonly string logs, store, appLock, supervisorLock, journalPath;
    readonly StartupRecovery journal;
    readonly PayloadRecovery snapshots;
    public MacRecoverySupervisor(LauncherConfig config, string logs, string installedRoot)
    {
        if (config.Platform != "osx" || config.UpdateChannel is not ("test" or "production") || !Path.IsPathFullyQualified(logs))
            throw new ArgumentException("macOS release identity and absolute state directory required");
        this.config = config; this.logs = logs;
        store = Path.Combine(logs, config.AppId + ".recovery-" + StartupRecovery.InstallationId(installedRoot));
        appLock = Path.Combine(logs, config.AppId + ".lock");
        supervisorLock = Path.Combine(logs, config.AppId + ".recovery.lock");
        journalPath = Path.Combine(logs, config.AppId + "." + StartupRecovery.InstallationId(installedRoot) + ".startup.json");
        var identity = new RecoveryIdentity(config.AppId, config.Platform, config.Architecture, config.UpdateChannel);
        journal = new(journalPath, identity); snapshots = new(store, identity);
    }
    string HandoffPath(string id) => Path.Combine(store, id + ".handoff.json");
    string Host(string id) => Path.Combine(store, id, "payload", "Contents", "MacOS", "StarTournamentLauncher");
    string RecoveryCommand(string id) => Path.Combine(store, "Restore-" + id + ".command");
    static string[] Arguments(string encoded)
    {
        if (encoded.Length > 32768) throw new ArgumentException("Restart arguments exceed limit");
        var args = JsonSerializer.Deserialize<string[]>(Convert.FromBase64String(encoded)) ?? throw new ArgumentException("Arguments missing");
        if (args.Any(a => a == null || a.Contains('\0'))) throw new ArgumentException("Invalid restart arguments");
        return args;
    }
    static string Encoded(string[] args) => Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(args));
    static string Quote(string arg) => "'" + arg.Replace("'", "'\\''") + "'";

    public RecoverySupervisorProcess PrepareAndStart(string installed, string packages, VelopackAsset asset, string envelope, bool restart, string[] args)
    {
        PlatformRequirements.RequireSupported("osx");
        if (journal.Read() is not { Attempt: null, Launch: null } state || state.HealthyVersion != config.Version || journal.IsBlocked(asset.Version.ToString()))
            throw new InvalidOperationException("Confirmed working version required before update handoff");
        var id = Guid.NewGuid().ToString("N");
        var handoff = new MacRecoveryHandoff(1, id, config, Path.GetFullPath(installed), Path.GetFullPath(packages), envelope, asset.Version.ToString(), restart, args);
        ValidateTarget(handoff);
        snapshots.Capture(handoff.InstallRoot, config.Version, id);
        WriteNew(HandoffPath(id), handoff);
        // A durable external entry remains usable if the replacement .NET runtime cannot load.
        using (var script = new FileStream(RecoveryCommand(id), FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { var bytes = Encoding.UTF8.GetBytes("#!/bin/sh\nexec " + Quote(Host(id)) + " --recovery-supervisor " + Quote(id) + " recover 0 0 " + Quote(Encoded(args)) + "\n"); script.Write(bytes); script.Flush(true); }
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(RecoveryCommand(id), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        journal.Prepare(config.Version, handoff.Target, id);
        try { return StartHost(id, "apply", args); }
        catch
        {
            // StartHost terminates only its own unacknowledged child while this caller is
            // still alive, before any updater can run. Verify that no files changed.
            journal.InstallationFailed(handoff.Target, "Не удалось запустить восстановление; обновление не применено.");
            snapshots.VerifyInstalled(id, handoff.InstallRoot);
            journal.MarkRestored(config.Version, id);
            CleanupSettled(config.Version);
            throw;
        }
    }
    public RecoverySupervisorProcess StartHost(string id, string mode, string[] args)
    {
        var handoff = ReadHandoff(id); snapshots.Verify(id, handoff.InstallRoot);
        using var caller = Process.GetCurrentProcess();
        var start = new ProcessStartInfo(Host(id)) { UseShellExecute = false, RedirectStandardOutput = true, WorkingDirectory = logs };
        foreach (var value in new[] { "--recovery-supervisor", id, mode, caller.Id.ToString(), caller.StartTime.ToUniversalTime().Ticks.ToString(), Encoded(args) })
            start.ArgumentList.Add(value);
        using var host = Process.Start(start) ?? throw new IOException("Recovery supervisor did not start");
        var ack = host.StandardOutput.ReadLineAsync();
        if (!ack.Wait(TimeSpan.FromSeconds(30)) || ack.Result != "STAR_TOURNAMENT_RECOVERY_READY")
        {
            // The exact caller remains alive, so this child cannot have begun activation.
            if (!host.HasExited) { host.Kill(); host.WaitForExit(); }
            throw new IOException("Recovery supervisor did not acknowledge the exact launcher");
        }
        return new(host.Id, host.StartTime.ToUniversalTime().Ticks);
    }
    public bool IsActive()
    { using var lease = TryLease(supervisorLock); return lease == null; }

    /// <summary>Only removes a settled snapshot after the stable process has released its executable.</summary>
    public bool CleanupSettled(string installedVersion)
    {
        using var owner = TryLease(supervisorLock);
        if (owner == null) return false;
        var state = journal.Read();
        if (state.Attempt is not { } attempt || attempt.Phase is not ("confirmed" or "restored")) return true;
        if (state.HealthyVersion != installedVersion) throw new IOException("Settled recovery identity differs from current release");
        var handoff = ReadHandoff(attempt.BackupId);
        CleanupStage(handoff);
        snapshots.Remove(attempt.BackupId, handoff.InstallRoot);
        journal.ConfirmBackupRemoved(attempt.BackupId);
        File.Delete(RecoveryCommand(attempt.BackupId)); File.Delete(HandoffPath(attempt.BackupId));
        return true;
    }

    public static Task<int> Run(string[] args) => Run(args, AppContext.BaseDirectory, UpdateFeedback.LogRoot);
    internal static async Task<int> Run(string[] args, string hostDirectory, string logs)
    {
        if (!OperatingSystem.IsMacOS() || GetEffectiveUserId() == 0) throw new UnauthorizedAccessException("macOS recovery must run as the normal user");
        if (args.Length != 6 || args[0] != "--recovery-supervisor" || args[2] is not ("apply" or "watch" or "recover"))
            throw new ArgumentException("Invalid recovery invocation");
        var config = JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(Path.Combine(hostDirectory, "release.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new IOException("Recovery host identity missing");
        if (args[1].Length != 32 || args[1].Any(c => !"0123456789abcdef".Contains(c))) throw new ArgumentException("Recovery id required");
        var entryPath = Path.Combine(Path.GetFullPath(Path.Combine(hostDirectory, "../../../..")), args[1] + ".handoff.json");
        if (new FileInfo(entryPath).Length > 131072) throw new IOException("Recovery handoff exceeds limit");
        var entry = JsonSerializer.Deserialize<MacRecoveryHandoff>(File.ReadAllText(entryPath), Json) ?? throw new IOException("Recovery handoff missing");
        var supervisor = new MacRecoverySupervisor(config, logs, entry.InstallRoot);
        var handoff = supervisor.ReadHandoff(args[1]);
        if (!Path.GetFullPath(hostDirectory).TrimEnd(Path.DirectorySeparatorChar).Equals(Path.GetDirectoryName(supervisor.Host(args[1])), StringComparison.Ordinal))
            throw new IOException("Supervisor must run from the retained payload");
        using var own = TryLease(supervisor.supervisorLock) ?? throw new IOException("Recovery supervision is already active");
        if (supervisor.snapshots.Verify(handoff.Id, handoff.InstallRoot).Version != config.Version || config != handoff.Config)
            throw new IOException("Retained host does not match handoff source release");
        if (supervisor.journal.Read().Attempt is not { } recorded || recorded.BackupId != handoff.Id ||
            recorded.Previous != config.Version || recorded.Target != handoff.Target)
            throw new IOException("Recovery handoff differs from the durable attempt");
        var pid = int.Parse(args[3]); var ticks = long.Parse(args[4]);
        var restartArgs = Arguments(args[5]);
        if (pid != 0)
        {
            using var caller = Process.GetProcessById(pid);
            if (caller.StartTime.ToUniversalTime().Ticks != ticks ||
                !Path.GetFullPath(caller.MainModule!.FileName!).Equals(Path.Combine(handoff.InstallRoot, "Contents", "MacOS", "StarTournamentLauncher"), StringComparison.Ordinal))
                throw new IOException("Recovery caller is not this installation's exact launcher");
        }
        else if (args[2] != "recover" || ticks != 0) throw new ArgumentException("Only manual recovery can omit a caller");
        Console.WriteLine("STAR_TOURNAMENT_RECOVERY_READY"); Console.Out.Flush();
        if (args[2] == "watch")
        {
            while (ExactAlive(pid, ticks))
            {
                if (supervisor.journal.Read().Attempt is { Phase: "confirmed" }) return 0;
                await Task.Delay(100);
            }
            var state = supervisor.journal.Read();
            if (state.Attempt is { Phase: "confirmed" }) return 0;
            await supervisor.Restore(handoff, "Игра завершилась до подтверждения готовности меню.");
            supervisor.Launch(handoff, restartArgs); return 1;
        }
        if (pid != 0) await WaitExact(pid, ticks);
        if (args[2] == "recover")
        {
            var state = supervisor.journal.Read();
            if (state.Attempt is not { } pending || pending.BackupId != handoff.Id) return 0;
            if (pending.Phase is "confirmed" or "restored") return 0;
            await supervisor.Restore(handoff, "Восстановление после прерванного запуска или установки.");
            if (pid != 0) supervisor.Launch(handoff, restartArgs);
            return 0;
        }
        bool stageOwned = false;
        try
        {
            PlatformRequirements.RequireSupported("osx");
            using (var install = Lease(supervisor.appLock))
            {
                await supervisor.WaitForPlayer();
                var asset = supervisor.ValidateTarget(handoff);
                var stage = Path.Combine(supervisor.store, handoff.Id + ".stage");
                if (Directory.Exists(stage) || File.Exists(stage)) throw new IOException("Fresh private updater staging required");
                Directory.CreateDirectory(stage);
                if ((File.GetAttributes(stage) & FileAttributes.ReparsePoint) != 0) throw new IOException("Updater staging is a link");
                stageOwned = true;
                WriteNew(Path.Combine(stage, "owner.json"), handoff);
                var package = Path.Combine(stage, asset.FileName);
                File.Copy(Path.Combine(handoff.Packages, asset.FileName), package, overwrite: false);
                using var verifier = supervisor.Verifier(handoff); await verifier.VerifyPackage(package, asset);
                using var held = new FileStream(package, FileMode.Open, FileAccess.Read, FileShare.Read);
                var update = DetachedOutput(Path.Combine(hostDirectory, "UpdateMac"),
                    new[] { "apply", "--silent", "--rootDir", handoff.InstallRoot, "--packageDir", stage, "--package", package, "--norestart", "--log", Path.Combine(logs, config.AppId + ".native-update.log") }, logs);
                using var child = Process.Start(update) ?? throw new IOException("Retained updater did not start");
                await child.WaitForExitAsync();
                if (child.ExitCode != 0) throw new IOException("Updater failed: " + child.ExitCode);
                supervisor.AssertInstalled(handoff, handoff.Target);
            }
            if (!handoff.Restart) return 0; // The external recovery command survives until next-launch confirmation.
            using var launched = supervisor.Launch(handoff, restartArgs);
            while (!launched.HasExited)
            {
                if (supervisor.journal.Read().Attempt is { Phase: "confirmed" }) return 0;
                await Task.Delay(100);
            }
            if (supervisor.journal.Read().Attempt is { Phase: "confirmed" }) return 0;
            throw new IOException("Replacement launcher exited before startup confirmation");
        }
        catch (Exception error)
        {
            supervisor.Log(error);
            if (supervisor.journal.Read().Attempt is { Phase: "confirmed" }) return 0;
            await supervisor.Restore(handoff, "Новая версия не запустилась. Восстановлена предыдущая рабочая версия.");
            if (handoff.Restart) supervisor.Launch(handoff, restartArgs);
            return 1;
        }
        finally
        {
            var stage = Path.Combine(supervisor.store, handoff.Id + ".stage");
            if (stageOwned && Directory.Exists(stage))
                try { supervisor.CleanupStage(handoff); } catch (Exception error) when (error is IOException or UnauthorizedAccessException) { supervisor.Log(error); }
        }
    }
    Process Launch(MacRecoveryHandoff handoff, string[] args)
    {
        var start = DetachedOutput(Path.Combine(handoff.InstallRoot, "Contents", "MacOS", "StarTournamentLauncher"), args, logs);
        return Process.Start(start) ?? throw new IOException("Installed launcher did not start");
    }
    static ProcessStartInfo DetachedOutput(string executable, IEnumerable<string> args, string workingDirectory)
    {
        // The ACK pipe belongs to the old launcher and closes at handoff. A replacement
        // (including Unity descendants) must not inherit that dead pipe after this host exits.
        // Fixed shell code only; executable and every user argument remain separate argv data.
        var start = new ProcessStartInfo("/bin/sh") { UseShellExecute = false, WorkingDirectory = workingDirectory };
        foreach (var arg in new[] { "-c", "exec \"$@\" </dev/null >/dev/null 2>&1", "star-recovery", executable }.Concat(args)) start.ArgumentList.Add(arg);
        return start;
    }
    async Task Restore(MacRecoveryHandoff handoff, string reason)
    {
        using var install = Lease(appLock); await WaitForPlayer();
        var state = journal.Read();
        if (state.Attempt is not { } attempt || attempt.BackupId != handoff.Id) throw new IOException("Recovery transaction identity changed");
        if (attempt.Phase == "confirmed") return; // Never roll back a release whose startup was already accepted.
        if (attempt.Phase == "prepared") journal.InstallationFailed(handoff.Target, reason);
        else if (attempt.Phase == "starting") journal.RecoverInterrupted(handoff.Target);
        snapshots.Restore(handoff.Id, handoff.InstallRoot);
        AssertInstalled(handoff, config.Version);
        if (journal.Read().Attempt?.Phase != "restored") journal.MarkRestored(config.Version, handoff.Id);
        new UpdateAttemptJournal(Path.Combine(logs, config.AppId + ".apply.json")).Fail(handoff.Target, reason);
        var manifest = Path.Combine(handoff.Packages, "star-authenticated.json");
        var next = manifest + ".restore-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var output = new FileStream(next, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { var bytes = Encoding.UTF8.GetBytes(handoff.Envelope); output.Write(bytes); output.Flush(true); }
            File.Move(next, manifest, overwrite: true);
        }
        finally { File.Delete(next); }
    }
    void Log(Exception error)
    {
        try { File.AppendAllText(Path.Combine(logs, config.AppId + ".recovery.log"), DateTimeOffset.UtcNow.ToString("O") + " " + error + "\n"); }
        catch (Exception logError) when (logError is IOException or UnauthorizedAccessException) { }
    }
    void CleanupStage(MacRecoveryHandoff handoff)
    {
        var stage = Path.Combine(store, handoff.Id + ".stage");
        if (!Directory.Exists(stage)) return;
        if ((File.GetAttributes(stage) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked updater staging retained");
        if (!Directory.EnumerateFileSystemEntries(stage).Any()) { Directory.Delete(stage); return; }
        var ownerPath = Path.Combine(stage, "owner.json");
        if (new FileInfo(ownerPath).Length > 131072 || (File.GetAttributes(ownerPath) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Invalid updater staging ownership");
        var owner = JsonSerializer.Deserialize<MacRecoveryHandoff>(File.ReadAllText(ownerPath), Json);
        if (owner == null || owner.Schema != 1 || owner.Id != handoff.Id || owner.Config != handoff.Config ||
            owner.InstallRoot != handoff.InstallRoot || owner.Packages != handoff.Packages || owner.Envelope != handoff.Envelope || owner.Target != handoff.Target)
            throw new IOException("Unowned updater staging retained");
        Directory.Delete(stage, true);
    }
    void AssertInstalled(MacRecoveryHandoff handoff, string version)
    {
        var actual = JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(Path.Combine(handoff.InstallRoot, "Contents", "MacOS", "release.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (actual == null || actual.AppId != config.AppId || actual.Platform != config.Platform || actual.Architecture != config.Architecture ||
            actual.UpdateChannel != config.UpdateChannel || actual.Version != version) throw new IOException("Installed release readback mismatch");
    }
    AuthenticatedSource Verifier(MacRecoveryHandoff handoff)
    {
        var verifier = new AuthenticatedSource(config, File.ReadAllText(Path.Combine(Path.GetDirectoryName(Host(handoff.Id))!, "update-public.pem")));
        try { verifier.Admit(handoff.Envelope); return verifier; } catch { verifier.Dispose(); throw; }
    }
    VelopackAsset ValidateTarget(MacRecoveryHandoff handoff)
    {
        // Before Capture the key comes from the currently executing healthy payload.
        var keyPath = File.Exists(Path.Combine(Path.GetDirectoryName(Host(handoff.Id))!, "update-public.pem"))
            ? Path.Combine(Path.GetDirectoryName(Host(handoff.Id))!, "update-public.pem")
            : Path.Combine(handoff.InstallRoot, "Contents", "MacOS", "update-public.pem");
        using var verifier = new AuthenticatedSource(config, File.ReadAllText(keyPath)); verifier.Admit(handoff.Envelope);
        verifier.RequireCompatibleTarget();
        var asset = verifier.Asset ?? throw new IOException("Authenticated target missing");
        if (asset.Version.ToString() != handoff.Target) throw new IOException("Frozen recovery target changed");
        verifier.VerifyPackage(Path.Combine(handoff.Packages, asset.FileName), asset).GetAwaiter().GetResult();
        return asset;
    }
    MacRecoveryHandoff ReadHandoff(string id)
    {
        if (id.Length != 32 || id.Any(c => !"0123456789abcdef".Contains(c))) throw new ArgumentException("Recovery id required");
        var path = HandoffPath(id);
        if (new FileInfo(path).Length > 131072) throw new IOException("Recovery handoff exceeds limit");
        var value = JsonSerializer.Deserialize<MacRecoveryHandoff>(File.ReadAllText(path), Json) ?? throw new IOException("Recovery handoff missing");
        if (value.Schema != 1 || value.Id != id || value.Config == null || value.Config.AppId != config.AppId ||
            value.Config.Platform != config.Platform || value.Config.Architecture != config.Architecture || value.Config.UpdateChannel != config.UpdateChannel || !Path.IsPathFullyQualified(value.InstallRoot) ||
            !value.InstallRoot.EndsWith(".app", StringComparison.Ordinal) || !Path.IsPathFullyQualified(value.Packages) || value.Arguments == null)
            throw new IOException("Foreign recovery handoff");
        return value;
    }
    async Task WaitForPlayer()
    {
        var path = Path.Combine(logs, config.AppId + ".player.json");
        if (!File.Exists(path)) return;
        if (new FileInfo(path).Length > 4096) throw new IOException("Player identity exceeds limit");
        using var record = JsonDocument.Parse(File.ReadAllText(path));
        var pid = record.RootElement.GetProperty("pid").GetInt32(); var started = record.RootElement.GetProperty("started").GetInt64();
        if (pid <= 0 || started <= 0) throw new IOException("Invalid Player identity; no payload replaced");
        await WaitExact(pid, started);
        File.Delete(path);
    }
    static bool ExactAlive(int pid, long ticks)
    {
        try { using var process = Process.GetProcessById(pid); return !process.HasExited && process.StartTime.ToUniversalTime().Ticks == ticks; }
        catch (ArgumentException) { return false; } catch (InvalidOperationException) { return false; }
        catch (System.ComponentModel.Win32Exception error) when (error.NativeErrorCode == 3)
        {
            // macOS may reap a process between HasExited and StartTime. Re-query; an
            // uncertain/live PID keeps the restore waiting, never grants replacement.
            try { using var again = Process.GetProcessById(pid); return !again.HasExited; }
            catch (ArgumentException) { return false; } catch (InvalidOperationException) { return false; }
            catch (System.ComponentModel.Win32Exception repeated) when (repeated.NativeErrorCode == 3) { return true; }
        }
    }
    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "geteuid")] static extern uint GetEffectiveUserId();
    static async Task WaitExact(int pid, long ticks)
    { while (ExactAlive(pid, ticks)) await Task.Delay(100); }
    static FileStream Lease(string path) => TryLease(path) ?? throw new IOException("Installation is busy; no payload replaced");
    static FileStream? TryLease(string path)
    { try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); } catch (IOException) { return null; } }
    static void WriteNew<T>(string path, T value)
    { using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); JsonSerializer.Serialize(output, value, Json); output.Flush(true); }
}
