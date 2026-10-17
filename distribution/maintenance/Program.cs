using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Runtime.Versioning;
using Microsoft.Win32;
using StarTournament.Distribution;
using Velopack;

[SupportedOSPlatform("windows")]
static partial class Maintenance
{
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    sealed record Marker(int Protocol, string Product, string InstallerVersion);
    sealed record Caller(int Pid, long StartedTicks);
    sealed record RestartEnvelope(bool Restart, int Pid, long StartedTicks, string[] Args);

    static int Main(string[] args)
    {
        try
        {
            if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("Windows only");
            ClearRuntimeInjectionEnvironment();
            if (string.Equals(Path.GetFileName(Environment.ProcessPath), "Star Tournament.exe", StringComparison.OrdinalIgnoreCase))
                return ForwardStableEntry(args);
            if (args.FirstOrDefault() == "--launch-stable") return LaunchStable(args[1..]).GetAwaiter().GetResult();
            return args.Length switch
            {
                5 when args[0] == "--upgrade-broker" => UpgradeBroker(args[1], new Caller(int.Parse(args[2]), long.Parse(args[3])), args[4]),
                2 when args[0] == "--validate-root" => ValidateRoot(args[1]),
                2 when args[0] == "--check-idle" => CheckIdle(args[1]),
                3 when args[0] == "--check-install" => InstallerOperation("plan",args[1],args[2]),
                3 when args[0] == "--installed-version" => InstallerOperation("version",args[1],args[2]),
                5 when args[0] == "--installer" => InstallerOperation(args[1],args[2],args[3],int.Parse(args[4])),
                2 when args[0] == "--launch-user" => LaunchUser(args[1]),
                1 when args[0] == "--check-legacy-user" => CheckLegacyUser(),
                7 when args[0] == "--activate" => Activate(args),
                5 when args[0] == "--watch-startup" => WatchStartup(args).GetAwaiter().GetResult(),
                7 when args[0] == "--apply-staged" => ElevatedApply(args[1], new Caller(int.Parse(args[2]), long.Parse(args[3])), args[4], bool.Parse(args[5]), args[6]),
                0 => RecoverUser(),
                1 when args[0] == "--recover" => RecoverUser(),
                4 when args[0] == "--restore-protected" => RestoreProtected(new Caller(int.Parse(args[1]), long.Parse(args[2])), args[3]),
                _ => throw new ArgumentException("Unsupported maintenance command")
            };
        }
        catch (Exception ex)
        {
            UpdateFeedback.Log("maintenance " + (args.FirstOrDefault() ?? "unknown"), ex);
            Console.Error.WriteLine("Star Tournament maintenance: " + ex.Message);
            if (args.Length == 0 || args.FirstOrDefault() is "--activate" or "--recover" or "--launch-stable" || string.Equals(Path.GetFileName(Environment.ProcessPath), "Star Tournament.exe", StringComparison.OrdinalIgnoreCase)) UpdateFeedback.Show("Обновление или перезапуск не выполнены. " + UpdateFeedback.Explain(ex));
            return 1;
        }
    }

    static void ClearRuntimeInjectionEnvironment()
    {
        foreach (var key in Environment.GetEnvironmentVariables().Keys.Cast<string>().ToArray())
            if (key.StartsWith("CORECLR_", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("COR_", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("COMPlus_", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("DOTNET_STARTUP_HOOKS", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("DOTNET_ADDITIONAL_DEPS", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("DOTNET_SHARED_STORE", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("DOTNET_PROFILER", StringComparison.OrdinalIgnoreCase))
                Environment.SetEnvironmentVariable(key, null);
    }

    static string SafeRoot(string root)
    {
        if (!Path.IsPathFullyQualified(root)) throw new InvalidOperationException("Absolute install path required");
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var drive = Path.GetPathRoot(full)!;
        if (string.Equals(full.TrimEnd('\\'), drive.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Drive root cannot be an install");
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\');
        if (full.Equals(windows, StringComparison.OrdinalIgnoreCase) || full.StartsWith(windows + "\\", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Windows system directories cannot be install targets");
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).TrimEnd('\\');
        if (full.Equals(programFiles, StringComparison.OrdinalIgnoreCase) || !full.StartsWith(programFiles + "\\", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Choose an install folder inside 64-bit Program Files");
        var cursor = drive;
        foreach (var part in full[drive.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            cursor = Path.Combine(cursor, part);
            if (Directory.Exists(cursor) && (File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Reparse point in install path");
        }
        return full;
    }

    static string SafeAbsoluteNonReparsePath(string path)
    {
        if (!Path.IsPathFullyQualified(path)) throw new InvalidOperationException("Absolute path required");
        var full = Path.GetFullPath(path);
        var drive = Path.GetPathRoot(full)!;
        if (full.TrimEnd('\\').Equals(drive.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A volume root is not a valid cache directory");
        var cursor = drive;
        foreach (var part in full[drive.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            cursor = Path.Combine(cursor, part);
            if (Directory.Exists(cursor) && (File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Reparse point in cache path");
        }
        return full;
    }

    static int ValidateRoot(string raw)
    {
        var root = SafeRoot(raw);
        ValidateInstallParents(root);
        if (Directory.Exists(root))
        {
            var entries = Directory.GetFileSystemEntries(root).Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (entries.Count != 0)
            {
                var markerPath = Path.Combine(root, ".star-tournament-install.json");
                if (!entries.Contains(Path.GetFileName(markerPath)))
                    throw new InvalidOperationException("Existing target contains unknown files");
                if ((File.GetAttributes(markerPath) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Install marker cannot be a reparse point");
                var marker = JsonSerializer.Deserialize<Marker>(File.ReadAllText(markerPath), Json);
                if (marker is not { Protocol: 1, Product: "Star Tournament" })
                    throw new InvalidOperationException("Install marker is invalid");
                RequireSemVer(marker.InstallerVersion);
            }
        }
        else
        {
        }
        return 0;
    }

    static void ValidateInstallParents(string root)
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).TrimEnd('\\');
        var candidate = Directory.Exists(root) ? root : Path.GetDirectoryName(root)!;
        while (candidate.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase))
        {
            if (Directory.Exists(candidate)) ValidateProtectedDirectory(candidate);
            if (candidate.Equals(programFiles, StringComparison.OrdinalIgnoreCase)) return;
            candidate = Path.GetDirectoryName(candidate)!;
        }
        throw new UnauthorizedAccessException("Install folder is outside protected 64-bit Program Files");
    }

    static void ValidateProtectedDirectory(string path)
    {
        var info = new DirectoryInfo(path);
        if ((info.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Install parent is a reparse point");
        var acl = info.GetAccessControl(); var owner = acl.GetOwner(typeof(SecurityIdentifier));
        if (!TrustedOwner(owner))
            throw new UnauthorizedAccessException("Install parent must be owned by Administrators, SYSTEM, or TrustedInstaller");
        foreach (FileSystemAccessRule rule in acl.GetAccessRules(true, true, typeof(SecurityIdentifier)))
        {
            var sid = (SecurityIdentifier)rule.IdentityReference;
            if (rule.AccessControlType == AccessControlType.Allow && (rule.PropagationFlags & PropagationFlags.InheritOnly) == 0 &&
                (rule.FileSystemRights & WriteRights) != 0 && !TrustedOwner(sid))
                throw new UnauthorizedAccessException("Install parent has a writable access rule outside the trusted installer principals");
        }
    }

    static int CheckIdle(string raw)
    {
        var root = SafeRoot(raw);
        GuardInstallerOwner(root,null);
        if(WindowsInstallation.InstallerPending(root))throw new IOException("Повторите установщик, чтобы завершить восстановление перед удалением.");
        using var lease = WindowsInstallLease.TryAcquire(root) ?? throw new IOException("Install is in use or lease access is unknown");
        CheckNoProcesses(root);
        return 0;
    }

    static void CheckNoProcesses(string root, int allowedPid = 0)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "StarTournament", "Star Tournament", "StarTournamentLauncher", "StarTournamentMaintenance", "Update", "Uninstall" };
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (!names.Contains(process.ProcessName)) continue;
                if (process.Id == Environment.ProcessId || process.Id == allowedPid) continue;
                try
                {
                    var path = process.MainModule?.FileName;
                    if (path != null && IsBelow(path, root)) throw new IOException("Star Tournament is still running");
                }
                catch (IOException) { throw; }
                catch { throw new IOException("Cannot establish whether a matching process is using the install"); }
            }
        }
    }

    static bool IsFreshOrInstallLockedTarget(string root)
    {
        if (!Directory.Exists(root)) { ValidateRoot(root); return true; }
        ValidateProtectedDirectory(root);
        var entries = Directory.GetFileSystemEntries(root);
        if (entries.Length == 0) return true;
        var maintenance = Path.Combine(root, "maintenance");
        var flag = Path.Combine(maintenance, "installing.flag");
        if (entries.Length != 1 || !string.Equals(entries[0], maintenance, StringComparison.OrdinalIgnoreCase) ||
            !Directory.Exists(maintenance) || (File.GetAttributes(maintenance) & FileAttributes.ReparsePoint) != 0) return false;
        var maintenanceEntries = Directory.GetFileSystemEntries(maintenance);
        return maintenanceEntries.Length == 1 && string.Equals(maintenanceEntries[0], flag, StringComparison.OrdinalIgnoreCase) &&
            File.Exists(flag) && (File.GetAttributes(flag) & FileAttributes.ReparsePoint) == 0;
    }

    static void RequireSemVer(string v)
    {
        if (!Regex.IsMatch(v, @"\A(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-([0-9A-Za-z.-]+))?(?:\+[0-9A-Za-z.-]+)?\z"))
            throw new InvalidOperationException("Invalid semantic version");
        try { _ = SemanticVersion.Parse(v); }
        catch (Exception ex) { throw new InvalidOperationException("Invalid semantic version", ex); }
    }
    static int CompareSemVer(string a, string b)
    {
        return SemanticVersion.Parse(a).CompareTo(SemanticVersion.Parse(b));
    }

    static int LaunchUser(string raw)
    {
        var stub = Path.GetFullPath(raw);
        if (!Path.IsPathFullyQualified(raw) || !File.Exists(stub)) throw new FileNotFoundException("Stable launcher stub is missing");
        var root = SafeRoot(Path.GetDirectoryName(stub)!);
        if (!string.Equals(Path.GetFileName(stub), "Star Tournament.exe", StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(Path.Combine(root, ".star-tournament-install.json")))
            throw new InvalidOperationException("Expected the stable Star Tournament root launcher stub");
        try
        {
            var type = Type.GetTypeFromCLSID(new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39"), true)!;
            dynamic windows = Activator.CreateInstance(type)!;
            int desktopHwnd = 0;
            dynamic desktop = windows.FindWindowSW(0, 0, 8, ref desktopHwnd, 1);
            if (desktop == null) throw new InvalidOperationException("Unelevated desktop Explorer is unavailable; launch manually");
            var hwnd = new IntPtr(desktopHwnd);
            var thread = GetWindowThreadProcessId(hwnd, out var pid);
            if (thread == 0 || pid == 0 || !SameSession((int)pid) || IsElevated((int)pid))
                throw new InvalidOperationException("Desktop Explorer is not a same-session unelevated process; launch manually");
            dynamic app = desktop.Document.Application;
            app.ShellExecute(stub, "", Path.GetDirectoryName(stub), "open", 1);
            return 0;
        }
        catch (Exception ex) { throw new InvalidOperationException("Could not start the launcher through unelevated desktop Explorer; launch manually", ex); }
    }

    static int CheckLegacyUser()
    {
        var desktop = FindWindowW("Progman", null);
        if (desktop == IntPtr.Zero || GetWindowThreadProcessId(desktop, out var pid) == 0 || pid == 0)
            throw new UnauthorizedAccessException("Cannot identify the original desktop Explorer user");
        using var explorer = Process.GetProcessById((int)pid);
        var explorerPath = Path.GetFullPath(explorer.MainModule?.FileName ?? throw new UnauthorizedAccessException("Cannot inspect desktop Explorer"));
        if (!Path.GetFileName(explorerPath).Equals("explorer.exe", StringComparison.OrdinalIgnoreCase) ||
            !explorerPath.Equals(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), StringComparison.OrdinalIgnoreCase) ||
            !SameSession((int)pid) || IsElevated((int)pid))
            throw new UnauthorizedAccessException("The original desktop is not a same-session unelevated Explorer");
        var sid = GetProcessUserSid((int)pid);
        using var users = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Registry64);
        using var originalUser = users.OpenSubKey(sid) ?? throw new InvalidOperationException("Original user registry hive is unavailable");
        const string uninstall = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\tech.afonasev.star-tournament.win-x64";
        using var uninstallKey = originalUser.OpenSubKey(uninstall);
        var uninstallPath = uninstallKey?.GetValue("InstallLocation") as string;
        using var shellFolders = originalUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders")
            ?? throw new InvalidOperationException("Original user Local AppData registry value is unavailable");
        var localRaw = shellFolders.GetValue("Local AppData", null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string
            ?? throw new InvalidOperationException("Original user Local AppData registry value is unavailable");
        using var profileList = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var profileKey = profileList.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\" + sid)
            ?? throw new InvalidOperationException("Original user profile metadata is unavailable");
        var profileRaw = profileKey.GetValue("ProfileImagePath") as string
            ?? throw new InvalidOperationException("Original user profile path is unavailable");
        var profile = Environment.ExpandEnvironmentVariables(profileRaw);
        if (!Path.IsPathFullyQualified(profile)) throw new InvalidOperationException("Original user profile path could not be expanded");
        string ExpandOriginal(string value) => Environment.ExpandEnvironmentVariables(value.Replace("%USERPROFILE%", profile, StringComparison.OrdinalIgnoreCase));
        var localAppData = Path.GetFullPath(ExpandOriginal(localRaw));
        var install = string.IsNullOrWhiteSpace(uninstallPath)
            ? Path.Combine(localAppData, "Programs", "tech.afonasev.star-tournament.win-x64")
            : Path.GetFullPath(ExpandOriginal(uninstallPath));
        if (uninstallKey != null)
            throw new InvalidOperationException("A per-user Star Tournament uninstall entry exists for the signed-in desktop user. Uninstall it from that account before installing for all users.");
        var candidates = new[] { install, Path.Combine(localAppData, "tech.afonasev.star-tournament.win-x64"),
            Path.Combine(localAppData, "Programs", "tech.afonasev.star-tournament.win-x64"), Path.Combine(localAppData, "StarTournament") };
        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            if (LegacyInstallationProbe.IsInstalled(candidate))
                throw new InvalidOperationException("A per-user Star Tournament installation exists for the signed-in desktop user. Close and uninstall it from that user account before installing for all users.");
        return 0;
    }

    static string GetProcessUserSid(int pid)
    {
        var process = OpenProcess(0x1000, false, (uint)pid);
        if (process == IntPtr.Zero) throw new UnauthorizedAccessException("Cannot open original desktop process");
        try
        {
            if (!OpenProcessToken(process, 0x0008, out var token)) throw new UnauthorizedAccessException("Cannot inspect original desktop token");
            try
            {
                GetTokenInformation(token, 1, IntPtr.Zero, 0, out var required);
                if (required <= 0) throw new UnauthorizedAccessException("Cannot size original desktop token information");
                var buffer = Marshal.AllocHGlobal(required);
                try
                {
                    if (!GetTokenInformation(token, 1, buffer, required, out _)) throw new UnauthorizedAccessException("Cannot read original desktop token user");
                    var user = Marshal.PtrToStructure<TOKEN_USER>(buffer);
                    return new SecurityIdentifier(user.UserSid).Value;
                }
                finally { Marshal.FreeHGlobal(buffer); }
            }
            finally { CloseHandle(token); }
        }
        finally { CloseHandle(process); }
    }

    static int Activate(string[] a)
    {
        PlatformRequirements.RequireSupported("win");
        if (IsElevated(Environment.ProcessId)) throw new UnauthorizedAccessException("Update supervision must start from a normal-user launcher");
        var root = SafeRoot(Path.GetDirectoryName(Path.GetDirectoryName(Environment.ProcessPath!))!);
        var cache = Path.GetFullPath(a[1]); var restart = bool.Parse(a[2]);
        var caller = new Caller(int.Parse(a[3]), long.Parse(a[4]));
        var restartArgs = JsonSerializer.Deserialize<string[]>(Convert.FromBase64String(a[5])) ?? [];
        ValidateCaller(root, caller);
        var config = JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(Path.Combine(root, "current", "release.json")), Json)!;
        using var source = new AuthenticatedSource(config, File.ReadAllText(Path.Combine(root, "maintenance", "update-public.pem")), readerProtocol: WindowsInstallation.MetadataReader(root));
        source.Admit(ReadBoundedText(Path.Combine(cache, "star-authenticated.json"), 65536));
        source.RequireCompatibleTarget();
        var expected = source.Asset!.Version.ToString();
        var frozenEnvelope = source.Envelope!;
        var frozenHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(frozenEnvelope)));
        bool explicitRetry = bool.Parse(a[6]);
        var observed = StartupRecovery.ForInstallation(config, UpdateFeedback.LogRoot, root).Read();
        if (observed.HealthyVersion != config.Version || observed.Launch != null)
            throw new InvalidOperationException("Current Player startup must be confirmed before activation");
        using var watchLease = TryWatchLease(root, config.AppId) ?? throw new IOException("Startup supervision is already active");
        var journal = UpdateAttemptJournal.ForApp(config.AppId);
        journal.Begin(expected);
        UpdateFeedback.Log("maintenance-ready target=" + expected);
        Console.WriteLine("STAR_TOURNAMENT_ACTIVATION_READY");
        Console.Out.Flush();
        WaitForExactProcess(caller);
        var broker = Path.Combine(root, "maintenance", "StarTournamentMaintenance.exe");
        int result = -1;
        string failure = "Установка обновления завершилась с ошибкой. Подробности сохранены в журнале.";
        try
        {
            using var elevated = Process.Start(new ProcessStartInfo(broker) { UseShellExecute = true, Verb = "runas", WorkingDirectory = root,
                ArgumentList = { "--apply-staged", cache, Environment.ProcessId.ToString(), Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks.ToString(), frozenHash, explicitRetry.ToString(), observed.HealthyVersion } }) ?? throw new IOException("Could not start protected updater");
            elevated.WaitForExit(); result = elevated.ExitCode;
            UpdateFeedback.Log("protected-updater-exit code=" + result + " target=" + expected);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            result = ex.NativeErrorCode == 1223 ? 1223 : 1;
            failure = UpdateFeedback.Explain(ex);
            UpdateFeedback.Log("elevated-start", ex);
        }
        catch (Exception ex) { result = 1; failure = UpdateFeedback.Explain(ex); UpdateFeedback.Log("elevated-apply", ex); }
        var current = Path.Combine(root, "current", "release.json");
        bool applied = false;
        try { applied = result == 0 && File.Exists(current) && JsonDocument.Parse(File.ReadAllText(current)).RootElement.GetProperty("version").GetString() == expected; }
        catch (Exception ex) { UpdateFeedback.Log("verify-applied-release", ex); failure = UpdateFeedback.Explain(ex); }
        if (!applied)
        {
            journal.Fail(expected, failure);
            try { File.WriteAllText(Path.Combine(cache, "star-apply-declined.json"), JsonSerializer.Serialize(new { version = expected })); }
            catch (Exception ex) { UpdateFeedback.Log("write-declined", ex); }
            UpdateFeedback.Show("Обновление не установлено. " + failure);
        }
        else if (applied)
        {
            // Installed files are not proof that the Player reached a working menu.
            // The normal-user launcher clears its attempt journal after the ready handshake.
            var declined = Path.Combine(cache, "star-apply-declined.json"); if (File.Exists(declined)) File.Delete(declined);
        }
        if (restart && WindowsInstallation.RecoveryNeedsRepair(root))
        { UpdateFeedback.Show("Установка прервана. Запустите восстановление Star Tournament перед повторным запуском игры."); return 1; }
        if (restart)
        {
            WindowsDetachedProcess? launched = null;
            try { launched = LaunchCurrent(root, restartArgs); }
            catch (Exception ex)
            {
                UpdateFeedback.Log("restart-launcher-failed", ex);
                if (!applied) { UpdateFeedback.Show("Не удалось перезапустить игру. " + UpdateFeedback.Explain(ex)); return 1; }
            }
            using (launched)
            {
                if (applied && WindowsInstallation.PendingStartup(root, expected) is { } id)
                    ObserveStartup(root, config, id, expected, () => launched?.IsAlive() ?? false, restartArgs).GetAwaiter().GetResult();
            }
        }
        return 0; // The launcher already exited successfully; a failed or declined update keeps the old install usable.
    }

    static int ForwardStableEntry(string[] args)
    {
        if (IsElevated(Environment.ProcessId)) throw new UnauthorizedAccessException("Start the game as the normal user");
        var root = SafeRoot(Path.GetDirectoryName(Environment.ProcessPath!)!);
        using var child = WindowsDetachedProcess.Start(Path.Combine(root, "maintenance", "StarTournamentMaintenance.exe"),
            new[] { "--launch-stable" }.Concat(args), root);
        return 0;
    }
    static async Task<int> LaunchStable(string[] args)
    {
        if (IsElevated(Environment.ProcessId)) throw new UnauthorizedAccessException("Start the game as the normal user");
        var root = SafeRoot(Path.GetDirectoryName(Path.GetDirectoryName(Environment.ProcessPath!))!);
        if(!WindowsInstallation.InstallerAllowsLaunch(root))throw new IOException("Finish or repeat the installer before launching");
        if(File.Exists(Path.Combine(root,"maintenance","installing.flag")))throw new IOException("Installer is changing files; wait for it to finish");
        var identity = JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(Path.Combine(root, "maintenance", "maintenance.json")), Json)!;
        using var watchLease = TryWatchLease(root, identity.AppId); if (watchLease == null) return 0;
        // Read-only routing from protected state, including when current is missing/broken.
        // Only the UAC store can validate/perform mutations; the normal host creates no protected paths.
        var statePath = Path.Combine(root, "maintenance", "recovery", "state.json");
        WindowsRecoveryAttempt? pending = null;
        if (File.Exists(statePath))
        {
            var state = JsonSerializer.Deserialize<WindowsRecoveryState>(ReadBoundedText(statePath, 131072)) ?? throw new IOException("Protected recovery state missing");
            if (state.Schema != 1 || state.RootId != StartupRecovery.InstallationId(root) || state.AppId != identity.AppId) throw new IOException("Foreign protected recovery state");
            pending = state.Attempt;
        }
        if (pending != null && pending.Phase is not ("installed" or "restored"))
            throw new IOException("Interrupted installation requires maintenance --recover before launch");
        bool watch = pending is { Phase: "installed" } && !WindowsInstallation.StartupConfirmed(root, pending.Id);
        WindowsDetachedProcess? child = null;
        try { child = LaunchCurrent(root, args); }
        catch (Exception ex) when (watch) { UpdateFeedback.Log("stable-first-launch-failed", ex); }
        using (child)
        {
            if (watch) await ObserveStartup(root, identity, pending!.Id, pending.Target, () => child?.IsAlive() ?? false, args);
        }
        return 0;
    }

    static FileStream? TryWatchLease(string root, string appId)
    {
        Directory.CreateDirectory(UpdateFeedback.LogRoot);
        var path = Path.Combine(UpdateFeedback.LogRoot, appId + "." + StartupRecovery.InstallationId(root) + ".windows-watch.lock");
        try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException error) when ((error.HResult & 0xffff) is 32 or 33) { return null; }
    }
    static WindowsDetachedProcess LaunchCurrent(string root, string[] args) =>
        WindowsDetachedProcess.Start(Path.Combine(root, "current", "StarTournamentLauncher.exe"), args, root);

    static async Task<int> WatchStartup(string[] args)
    {
        if (IsElevated(Environment.ProcessId)) throw new UnauthorizedAccessException("Startup supervision must run as the normal user");
        var root = SafeRoot(Path.GetDirectoryName(Path.GetDirectoryName(Environment.ProcessPath!))!);
        var caller = new Caller(int.Parse(args[2]), long.Parse(args[3])); ValidateCaller(root, caller);
        var config = JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(Path.Combine(root, "current", "release.json")), Json)!;
        var id = WindowsInstallation.PendingStartup(root, config.Version);
        if (id != null && id != args[1]) throw new IOException("Startup transaction changed before supervision");
        var restartArgs = JsonSerializer.Deserialize<string[]>(Convert.FromBase64String(args[4])) ?? throw new IOException("Restart arguments missing");
        using var watchLease = TryWatchLease(root, config.AppId);
        Console.WriteLine("STAR_TOURNAMENT_STARTUP_WATCH_READY"); Console.Out.Flush();
        // An activation supervisor already holds the lease before launching its exact child.
        if (id == null || watchLease == null) return 0;
        await ObserveStartup(root, config, id, config.Version, () => ExactAlive(caller), restartArgs);
        return 0;
    }
    static bool ExactAlive(Caller caller)
    {
        try
        {
            using var process = Process.GetProcessById(caller.Pid);
            return !process.HasExited && process.StartTime.ToUniversalTime().Ticks == caller.StartedTicks;
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
        // Access failures are not proof of exit. Let the caller fail closed with its backup.
    }
    static async Task WaitForRecordedPlayer(string appId)
    {
        var path = Path.Combine(UpdateFeedback.LogRoot, appId + ".player.json");
        if (!File.Exists(path)) return;
        using var record = JsonDocument.Parse(ReadBoundedText(path, 4096));
        var player = new Caller(record.RootElement.GetProperty("pid").GetInt32(), record.RootElement.GetProperty("started").GetInt64());
        if (player.Pid <= 0 || player.StartedTicks <= 0) throw new IOException("Invalid recorded Player identity");
        while (ExactAlive(player)) await Task.Delay(100);
        // Do not delete a record another launcher could have replaced while we waited.
    }
    static async Task ObserveStartup(string root, LauncherConfig config, string id, string target, Func<bool> alive, string[] args)
    {
        var journal = StartupRecovery.ForInstallation(config, UpdateFeedback.LogRoot, root);
        await WindowsStartupWatch.Observe(alive, () => WindowsInstallation.StartupConfirmed(root, id),
            () => WaitForRecordedPlayer(config.AppId),
            () => journal.Read().HealthyVersion == target,
            () => { RequestRestore(root, id);
                using var restored=JsonDocument.Parse(ReadBoundedText(Path.Combine(root,"maintenance","maintenance.json"),65536));
                var retry=restored.RootElement.GetProperty("protocol").GetInt32()==1?" Для повторной попытки используйте новый установщик Star Tournament.":" Повторить попытку можно явно через перезапуск с обновлением.";
                UpdateAttemptJournal.ForApp(config.AppId).Fail(target, "Новая версия не подтвердила запуск. Восстановлена предыдущая версия."+retry); return Task.CompletedTask; },
            () => { using var child = LaunchCurrent(root, args); });
    }
    static void RequestRestore(string root, string id)
    {
        using var self = Process.GetCurrentProcess();
        var start = new ProcessStartInfo(Path.Combine(root, "maintenance", "StarTournamentMaintenance.exe"))
        { UseShellExecute = true, Verb = "runas", WorkingDirectory = root };
        foreach (var arg in new[] { "--restore-protected", self.Id.ToString(), self.StartTime.ToUniversalTime().Ticks.ToString(), id }) start.ArgumentList.Add(arg);
        using var elevated = Process.Start(start) ?? throw new IOException("Protected recovery did not start"); elevated.WaitForExit();
        if (elevated.ExitCode != 0) throw new IOException("Protected recovery did not complete; inspect the journal");
    }

    static void WaitForExactProcess(Caller caller)
    {
        while (true)
        {
            try
            {
                using var process = Process.GetProcessById(caller.Pid);
                if (process.StartTime.ToUniversalTime().Ticks != caller.StartedTicks) return;
                process.WaitForExit(250);
            }
            catch (ArgumentException) { return; }
            catch (InvalidOperationException) { return; }
            catch (System.ComponentModel.Win32Exception) { throw new IOException("Cannot establish caller process exit"); }
        }
    }

    static void ValidateCaller(string root, Caller caller)
    {
        using var process = Process.GetProcessById(caller.Pid);
        if (process.StartTime.ToUniversalTime().Ticks != caller.StartedTicks || process.ProcessName != "StarTournamentLauncher" ||
            !SameSession(caller.Pid) || IsElevated(caller.Pid) || !IsBelow(process.MainModule!.FileName!, root))
            throw new UnauthorizedAccessException("Update request must come from the same-session normal-user launcher");
    }

    static void ValidateSupervisor(string root, Caller caller)
    {
        using var supervisor = Process.GetProcessById(caller.Pid);
        if (supervisor.StartTime.ToUniversalTime().Ticks != caller.StartedTicks || supervisor.ProcessName != "StarTournamentMaintenance" ||
            IsElevated(caller.Pid) || !SameSession(caller.Pid) ||
            !string.Equals(Path.GetFullPath(supervisor.MainModule!.FileName!), Path.Combine(root, "maintenance", "StarTournamentMaintenance.exe"), StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Request must come from the exact stable normal-user supervisor");
    }
    static int ElevatedApply(string cache, Caller supervisor, string frozenHash, bool explicitRetry, string confirmedCurrent)
    {
        PlatformRequirements.RequireSupported("win");
        if (!IsElevated(Environment.ProcessId)) throw new UnauthorizedAccessException("Protected apply requires elevation");
        var root = SafeRoot(Path.GetDirectoryName(Path.GetDirectoryName(Environment.ProcessPath!))!);
        EnsureProtectedRoot(root); ValidateSupervisor(root, supervisor);
        if(WindowsInstallation.InstallerPending(root))throw new IOException("Finish or repeat the installer before updating/recovering");
        using var lease = WindowsInstallLease.TryAcquire(root) ?? throw new IOException("Install lease is held");
        CheckNoProcesses(root, supervisor.Pid);
        cache = SafeAbsoluteNonReparsePath(cache);
        if ((File.GetAttributes(cache) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Staged cache path is unsafe");
        var envelopePath = Path.Combine(cache, "star-authenticated.json");
        if (!File.Exists(envelopePath)) throw new CryptographicException("Staged release envelope missing");
        var envelope = ReadBoundedText(envelopePath, 65536);
        ProtectedPackage.RequireEnvelopeHash(envelope, frozenHash);
        var currentDir = Path.Combine(root, "current"); var currentConfigPath = Path.Combine(currentDir, "release.json");
        if ((File.GetAttributes(currentDir) & FileAttributes.ReparsePoint) != 0 || (File.GetAttributes(currentConfigPath) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Installed configuration path is a reparse point");
        var config = JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(currentConfigPath), Json)!;
        var protectedConfig = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "maintenance", "maintenance.json"))).RootElement;
        if (protectedConfig.GetProperty("protocol").GetInt32() != 2 || protectedConfig.GetProperty("appId").GetString() != config.AppId ||
            protectedConfig.GetProperty("platform").GetString() != config.Platform || protectedConfig.GetProperty("architecture").GetString() != config.Architecture)
            throw new CryptographicException("Protected installation identity mismatch");
        using var verifier = new AuthenticatedSource(config, File.ReadAllText(Path.Combine(root, "maintenance", "update-public.pem")), readerProtocol: WindowsInstallation.MetadataReader(root));
        verifier.Admit(envelope); var asset = verifier.Asset!;
        verifier.RequireCompatibleTarget();
        var sourcePackage = Path.Combine(cache, Path.GetFileName(asset.FileName));
        if (!File.Exists(sourcePackage)) throw new CryptographicException("Staged package missing");
        verifier.VerifyPackage(sourcePackage, asset).GetAwaiter().GetResult();
        if (asset.Version.ToString() != JsonDocument.Parse(Convert.FromBase64String(JsonDocument.Parse(envelope).RootElement.GetProperty("payload").GetString()!)).RootElement.GetProperty("version").GetString())
            throw new CryptographicException("Package release identity mismatch");
        if (CompareSemVer(asset.Version.ToString(), config.Version) <= 0) throw new InvalidOperationException("Refusing downgrade or same-version update");
        if (confirmedCurrent != config.Version) throw new InvalidOperationException("Supervisor health attestation does not match current release");
        ValidateProtectedTree(currentDir);
        var recovery = new WindowsRecoveryStore(root, config, CreateRecoveryDirectory, CreateRecoveryFile, CreateStartupMarker);
        if (recovery.Read().Attempt != null) recovery.Cleanup(confirmedCurrent);
        var stagingRoot = Path.Combine(root, "maintenance", "staging");
        if (Directory.Exists(stagingRoot) && (File.GetAttributes(stagingRoot) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Protected staging parent is a reparse point");
        Directory.CreateDirectory(stagingRoot); ProtectDirectory(stagingRoot);
        var staging = Path.Combine(stagingRoot, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(staging); ProtectDirectory(staging);
        try
        {
            var copied = ProtectedPackage.CopyAndVerifyAsync(verifier, envelope, asset, sourcePackage, staging).GetAwaiter().GetResult();
            var copiedPackage = copied.PackagePath;
            using var held = new FileStream(copiedPackage, FileMode.Open, FileAccess.Read, FileShare.Read);
            var candidate = WindowsFullPayload.Stage(verifier, asset, copiedPackage, Path.Combine(staging, "payload"),
                CreateRecoveryDirectory, CreateRecoveryFile).GetAwaiter().GetResult();
            ValidateProtectedTree(Path.Combine(staging, "payload"));
            try
            {
                recovery.Prepare(config, asset.Version.ToString(), explicitRetry);
                recovery.Apply(() =>
                {
                    CheckNoProcesses(root, supervisor.Pid); // Recheck after potentially lengthy staging/snapshot.
                    candidate.Activate(root); // No native apply: resident supervisor/broker survive.
                    // Root entry/updater/registration remain installer-owned and unchanged.
                    ValidateProtectedTree(currentDir);
                });
            }
            catch
            {
                if (recovery.Read().Attempt is { Phase: "capturing" }) recovery.Restore(); // Activation never began.
                throw;
            }
            return 0;
        }
        finally { try { Directory.Delete(staging, true); } catch { } }
    }

    static int RecoverUser()
    {
        if (IsElevated(Environment.ProcessId)) throw new UnauthorizedAccessException("Start recovery as the normal user");
        var root = SafeRoot(Path.GetDirectoryName(Path.GetDirectoryName(Environment.ProcessPath!))!);
        var statePath = Path.Combine(root, "maintenance", "recovery", "state.json");
        using var state = JsonDocument.Parse(ReadBoundedText(statePath, 131072));
        if (state.RootElement.GetProperty("Schema").GetInt32() != 1 || state.RootElement.GetProperty("RootId").GetString() != StartupRecovery.InstallationId(root))
            throw new IOException("Foreign recovery status");
        var expectedId = state.RootElement.GetProperty("Attempt").GetProperty("Id").GetString() ?? throw new IOException("No recovery transaction");
        if (!Regex.IsMatch(expectedId, "\\A[0-9a-f]{32}\\z")) throw new IOException("Invalid recovery identity");
        RequestRestore(root, expectedId);
        Console.WriteLine("Предыдущая рабочая версия восстановлена. Запустите игру обычным способом."); return 0;
    }
    static int RestoreProtected(Caller supervisor, string expectedId)
    {
        if (!IsElevated(Environment.ProcessId)) throw new UnauthorizedAccessException("Protected recovery requires elevation");
        var root = SafeRoot(Path.GetDirectoryName(Path.GetDirectoryName(Environment.ProcessPath!))!);
        EnsureProtectedRoot(root); ValidateSupervisor(root, supervisor);
        if(WindowsInstallation.InstallerPending(root))throw new IOException("Finish or repeat the installer before updating/recovering");
        using var lease = WindowsInstallLease.TryAcquire(root) ?? throw new IOException("Install lease is held");
        CheckNoProcesses(root, supervisor.Pid);
        var config = JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(Path.Combine(root, "maintenance", "maintenance.json")), Json)!;
        var store = new WindowsRecoveryStore(root, config, CreateRecoveryDirectory, CreateRecoveryFile, CreateStartupMarker);
        store.Restore(expectedId); ValidateProtectedTree(Path.Combine(root, "current")); return 0;
    }
    static DirectorySecurity RecoveryDirectorySecurity()
    {
        var value = new DirectorySecurity(); value.SetAccessRuleProtection(true, false);
        var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null); value.SetOwner(admins);
        foreach (var sid in new[] { admins, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null) })
            value.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        value.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null), FileSystemRights.ReadAndExecute,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow)); return value;
    }
    static void CreateRecoveryDirectory(string path)
    {
        if (Directory.Exists(path)) { ValidateProtectedDirectory(path); return; }
        new DirectoryInfo(path).Create(RecoveryDirectorySecurity());
    }
    static FileStream CreateRecoveryFile(string path)
    {
        var security = new FileSecurity(); security.SetAccessRuleProtection(true, false);
        var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null); security.SetOwner(admins);
        foreach (var sid in new[] { admins, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null) })
            security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null), FileSystemRights.ReadAndExecute, AccessControlType.Allow));
        return new FileInfo(path).Create(FileMode.CreateNew, FileSystemRights.FullControl, FileShare.None, 4096, FileOptions.None, security);
    }
    static FileStream CreateStartupMarker(string path)
    {
        var security = new FileSecurity(); security.SetAccessRuleProtection(true, false);
        var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null); security.SetOwner(admins);
        foreach (var sid in new[] { admins, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null) })
            security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            FileSystemRights.Read | FileSystemRights.AppendData | FileSystemRights.Synchronize, AccessControlType.Allow));
        return new FileInfo(path).Create(FileMode.CreateNew, FileSystemRights.FullControl, FileShare.None, 4096, FileOptions.WriteThrough, security);
    }
    static void ValidateProtectedTree(string root)
    {
        ValidateProtectedDirectory(root);
        foreach (var entry in new DirectoryInfo(root).EnumerateFileSystemInfos())
        {
            if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Protected payload contains a reparse point");
            if (entry is DirectoryInfo) ValidateProtectedTree(entry.FullName);
            else
            {
                var acl = ((FileInfo)entry).GetAccessControl();
                if (!TrustedOwner(acl.GetOwner(typeof(SecurityIdentifier)))) throw new UnauthorizedAccessException("Payload input owner is not trusted");
                foreach (FileSystemAccessRule rule in acl.GetAccessRules(true, true, typeof(SecurityIdentifier)))
                    if (rule.AccessControlType == AccessControlType.Allow && (rule.FileSystemRights & WriteRights) != 0 && !TrustedOwner((SecurityIdentifier)rule.IdentityReference))
                        throw new UnauthorizedAccessException("Payload input is writable outside installer principals");
            }
        }
    }

    static void EnsureProtectedRoot(string root)
    {
        var exe = Environment.ProcessPath!; var maint = Path.GetDirectoryName(exe)!;
        if (!IsBelow(exe, root) || !string.Equals(maint, Path.Combine(root, "maintenance"), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Elevated broker must run from this install's protected maintenance directory");
        ValidateProtectedDirectory(root); ValidateProtectedDirectory(maint);
        var protectedFiles = Directory.EnumerateFiles(maint, "*", SearchOption.TopDirectoryOnly)
            .Where(p => new[] { ".exe", ".dll", ".json", ".pem" }.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase)).ToList();
        var updater = Path.Combine(root, "Update.exe");
        if (File.Exists(updater)) protectedFiles.Add(updater);
        if (!protectedFiles.Contains(exe, StringComparer.OrdinalIgnoreCase) ||
            !protectedFiles.Contains(Path.Combine(maint, "maintenance.json"), StringComparer.OrdinalIgnoreCase) ||
            !protectedFiles.Contains(Path.Combine(maint, "update-public.pem"), StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Protected maintenance runtime/configuration is incomplete");
        foreach (var p in protectedFiles)
        {
            if (!File.Exists(p) || (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Protected broker input is missing or reparse");
            var acl = new FileInfo(p).GetAccessControl(); var owner = acl.GetOwner(typeof(SecurityIdentifier));
            if (!TrustedOwner(owner)) throw new UnauthorizedAccessException("Protected broker input has untrusted owner");
            foreach (FileSystemAccessRule rule in acl.GetAccessRules(true, true, typeof(SecurityIdentifier)))
                if (rule.AccessControlType == AccessControlType.Allow && (rule.PropagationFlags & PropagationFlags.InheritOnly) == 0 &&
                    (rule.FileSystemRights & WriteRights) != 0 && !TrustedOwner((SecurityIdentifier)rule.IdentityReference))
                    throw new UnauthorizedAccessException("Protected broker input has a writable access rule outside trusted installer principals");
        }
    }
    static void ProtectDirectory(string p)
    {
        var security = new DirectorySecurity(); security.SetAccessRuleProtection(true, false);
        var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null); var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        security.AddAccessRule(new FileSystemAccessRule(admins, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(system, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(p).SetAccessControl(security);
    }
    static bool IsBelow(string path, string root) => Path.GetFullPath(path).StartsWith(Path.GetFullPath(root).TrimEnd('\\') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    static bool TrustedOwner(IdentityReference? identity) => identity != null &&
        (identity.Equals(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null)) ||
         identity.Equals(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null)) ||
         identity.Equals(new SecurityIdentifier("S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464")));
    const FileSystemRights WriteRights = FileSystemRights.WriteData | FileSystemRights.AppendData | FileSystemRights.WriteAttributes |
        FileSystemRights.WriteExtendedAttributes | FileSystemRights.Delete | FileSystemRights.DeleteSubdirectoriesAndFiles |
        FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership;
    static bool SameSession(int pid) => ProcessIdToSessionId((uint)pid, out var session) && session == (uint)Process.GetCurrentProcess().SessionId;
    static bool IsElevated(int pid)
    {
        var process = OpenProcess(0x1000, false, (uint)pid);
        if (process == IntPtr.Zero) throw new UnauthorizedAccessException("Cannot open process to inspect its token");
        try
        {
            if (!OpenProcessToken(process, 0x0008, out var token)) throw new UnauthorizedAccessException("Cannot inspect process token");
            try
            {
                var size = Marshal.SizeOf<TOKEN_ELEVATION>(); var data = Marshal.AllocHGlobal(size);
                try
                {
                    if (!GetTokenInformation(token, 20, data, size, out _)) throw new UnauthorizedAccessException("Cannot read process elevation token");
                    return Marshal.PtrToStructure<TOKEN_ELEVATION>(data).TokenIsElevated != 0;
                }
                finally { Marshal.FreeHGlobal(data); }
            }
            finally { CloseHandle(token); }
        }
        finally { CloseHandle(process); }
    }
    static string ReadBoundedText(string path, int maxBytes)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > maxBytes) throw new CryptographicException("Signed envelope exceeds limit");
        using var reader = new StreamReader(stream, new System.Text.UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var content = reader.ReadToEnd();
        if (stream.Position > maxBytes) throw new CryptographicException("Signed envelope exceeds limit");
        return content;
    }
    [StructLayout(LayoutKind.Sequential)] struct TOKEN_ELEVATION { public int TokenIsElevated; }
    [StructLayout(LayoutKind.Sequential)] struct TOKEN_USER { public IntPtr UserSid; public int Attributes; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr FindWindowW(string className, string? windowName);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool ProcessIdToSessionId(uint pid, out uint session);
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("advapi32.dll", SetLastError = true)] static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
    [DllImport("advapi32.dll", SetLastError = true)] static extern bool GetTokenInformation(IntPtr token, int info, IntPtr buffer, int length, out int needed);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
}
