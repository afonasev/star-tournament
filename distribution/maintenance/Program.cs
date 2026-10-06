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
static class Maintenance
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
            return args.Length switch
            {
                2 when args[0] == "--validate-root" => ValidateRoot(args[1]),
                2 when args[0] == "--check-idle" => CheckIdle(args[1]),
                3 when args[0] == "--check-install" => CheckInstall(args[1], args[2]),
                2 when args[0] == "--launch-user" => LaunchUser(args[1]),
                1 when args[0] == "--check-legacy-user" => CheckLegacyUser(),
                6 when args[0] == "--activate" => Activate(args),
                3 when args[0] == "--apply-staged" => ApplyStaged(args[1], int.Parse(args[2])),
                _ => throw new ArgumentException("Unsupported maintenance command")
            };
        }
        catch (Exception ex) { Console.Error.WriteLine("Star Tournament maintenance: " + ex.Message); return 1; }
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
        var full = Path.GetFullPath(root);
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
        using var lease = WindowsInstallLease.TryAcquire(root) ?? throw new IOException("Install is in use or lease access is unknown");
        CheckNoProcesses(root);
        return 0;
    }

    static void CheckNoProcesses(string root, int allowedPid = 0)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "StarTournament", "Star Tournament", "StarTournamentLauncher", "StarTournamentMaintenance", "Update" };
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

    static int CheckInstall(string raw, string version)
    {
        var root = SafeRoot(raw); ValidateInstallParents(root); RequireSemVer(version);
        var marker = Path.Combine(root, ".star-tournament-install.json");
        if (File.Exists(marker)) ValidateRoot(root);
        else if (!IsFreshOrInstallLockedTarget(root)) throw new InvalidOperationException("Target is not an empty or marked Star Tournament installation");
        var currentPath = Path.Combine(root, "current", "release.json");
        if (!File.Exists(currentPath))
        {
            return 0; // Empty first install or a partial marked install repaired by NSIS.
        }
        if ((File.GetAttributes(Path.Combine(root, "current")) & FileAttributes.ReparsePoint) != 0 ||
            (File.GetAttributes(currentPath) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Installed release metadata path is a reparse point");
        using var current = JsonDocument.Parse(File.ReadAllText(currentPath));
        var installed = current.RootElement.GetProperty("version").GetString()!;
        RequireSemVer(installed);
        if (CompareSemVer(version, installed) < 0) throw new InvalidOperationException("Downgrades are not allowed");
        throw new InvalidOperationException("An existing release is present; uninstall it before running this fresh installer");
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
        if (IsElevated(Environment.ProcessId)) throw new UnauthorizedAccessException("Update supervision must start from a normal-user launcher");
        var root = SafeRoot(Path.GetDirectoryName(Path.GetDirectoryName(Environment.ProcessPath!))!);
        var cache = Path.GetFullPath(a[1]); var restart = bool.Parse(a[2]);
        var caller = new Caller(int.Parse(a[3]), long.Parse(a[4]));
        var restartArgs = JsonSerializer.Deserialize<string[]>(Convert.FromBase64String(a[5])) ?? [];
        ValidateCaller(root, caller);
        Console.WriteLine("STAR_TOURNAMENT_ACTIVATION_READY");
        Console.Out.Flush();
        WaitForExactProcess(caller);
        var broker = Path.Combine(root, "maintenance", "StarTournamentMaintenance.exe");
        int result = -1;
        try
        {
            using var elevated = Process.Start(new ProcessStartInfo(broker) { UseShellExecute = true, Verb = "runas", WorkingDirectory = root,
                ArgumentList = { "--apply-staged", cache, Environment.ProcessId.ToString() } }) ?? throw new IOException("Could not start protected updater");
            elevated.WaitForExit(); result = elevated.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception) { result = 1223; } // UAC was declined.
        catch { result = 1; }
        string? expected = null;
        try
        {
            var config = JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(Path.Combine(root, "current", "release.json")), Json)!;
            using var source = new AuthenticatedSource(config, File.ReadAllText(Path.Combine(root, "maintenance", "update-public.pem")));
            source.Admit(ReadBoundedText(Path.Combine(cache, "star-authenticated.json"), 65536));
            expected = source.Asset!.Version.ToString();
        }
        catch { }
        var current = Path.Combine(root, "current", "release.json");
        var applied = result == 0 && expected != null && File.Exists(current) &&
            JsonDocument.Parse(File.ReadAllText(current)).RootElement.GetProperty("version").GetString() == expected;
        if (!applied && expected != null)
            File.WriteAllText(Path.Combine(cache, "star-apply-declined.json"), JsonSerializer.Serialize(new { version = expected }));
        else if (applied)
        {
            var declined = Path.Combine(cache, "star-apply-declined.json"); if (File.Exists(declined)) File.Delete(declined);
        }
        if (restart)
        {
            var stub = Path.Combine(root, "Star Tournament.exe");
            var launch = new ProcessStartInfo(stub) { UseShellExecute = false, WorkingDirectory = root };
            foreach (var arg in restartArgs) launch.ArgumentList.Add(arg);
            Process.Start(launch);
        }
        return 0; // The launcher already exited successfully; a failed or declined update keeps the old install usable.
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

    static int ApplyStaged(string cache, int supervisorPid) => ElevatedApply(cache, supervisorPid);
    static int ElevatedApply(string cache, int supervisorPid)
    {
        var root = SafeRoot(Path.GetDirectoryName(Path.GetDirectoryName(Environment.ProcessPath!))!);
        EnsureProtectedRoot(root);
        using (var supervisor = Process.GetProcessById(supervisorPid))
        {
            if (supervisor.ProcessName != "StarTournamentMaintenance" || IsElevated(supervisorPid) || !SameSession(supervisorPid) ||
                !string.Equals(Path.GetFullPath(supervisor.MainModule!.FileName!), Path.Combine(root, "maintenance", "StarTournamentMaintenance.exe"), StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Apply request did not come from the stable normal-user supervisor");
        }
        using var lease = WindowsInstallLease.TryAcquire(root) ?? throw new IOException("Install lease is held");
        CheckNoProcesses(root, supervisorPid);
        cache = SafeAbsoluteNonReparsePath(cache);
        if ((File.GetAttributes(cache) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Staged cache path is unsafe");
        var envelopePath = Path.Combine(cache, "star-authenticated.json");
        if (!File.Exists(envelopePath)) throw new CryptographicException("Staged release envelope missing");
        var envelope = ReadBoundedText(envelopePath, 65536);
        var currentDir = Path.Combine(root, "current"); var currentConfigPath = Path.Combine(currentDir, "release.json");
        if ((File.GetAttributes(currentDir) & FileAttributes.ReparsePoint) != 0 || (File.GetAttributes(currentConfigPath) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Installed configuration path is a reparse point");
        var config = JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(currentConfigPath), Json)!;
        var protectedConfig = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "maintenance", "maintenance.json"))).RootElement;
        if (protectedConfig.GetProperty("protocol").GetInt32() != 1 || protectedConfig.GetProperty("appId").GetString() != config.AppId ||
            protectedConfig.GetProperty("platform").GetString() != config.Platform || protectedConfig.GetProperty("architecture").GetString() != config.Architecture)
            throw new CryptographicException("Protected installation identity mismatch");
        using var verifier = new AuthenticatedSource(config, File.ReadAllText(Path.Combine(root, "maintenance", "update-public.pem")));
        verifier.Admit(envelope); var asset = verifier.Asset!;
        var sourcePackage = Path.Combine(cache, Path.GetFileName(asset.FileName));
        if (!File.Exists(sourcePackage)) throw new CryptographicException("Staged package missing");
        verifier.VerifyPackage(sourcePackage, asset).GetAwaiter().GetResult();
        if (asset.Version.ToString() != JsonDocument.Parse(Convert.FromBase64String(JsonDocument.Parse(envelope).RootElement.GetProperty("payload").GetString()!)).RootElement.GetProperty("version").GetString())
            throw new CryptographicException("Package release identity mismatch");
        if (CompareSemVer(asset.Version.ToString(), config.Version) <= 0) throw new InvalidOperationException("Refusing downgrade or same-version update");
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
        var updater = Path.Combine(root, "Update.exe");
        if (!File.Exists(updater) || (File.GetAttributes(updater) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Protected updater is missing or reparse");
        var psi = new ProcessStartInfo(updater) { UseShellExecute = false, WorkingDirectory = root };
        foreach (var v in new[] { "apply", "--package", copiedPackage, "--rootDir", root, "--packageDir", staging, "--norestart" }) psi.ArgumentList.Add(v);
            using var update = Process.Start(psi) ?? throw new IOException("Protected updater did not start"); update.WaitForExit();
            if (update.ExitCode != 0) throw new IOException("Protected updater failed with exit code " + update.ExitCode);
            return 0;
        }
        finally { try { Directory.Delete(staging, true); } catch { } }
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
