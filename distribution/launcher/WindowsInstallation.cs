using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Velopack.Locators;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace StarTournament.Distribution;

/// <summary>The installer owns maintenance outside the replaceable current payload.</summary>
public static class WindowsInstallation
{
    public static int MetadataReader(string root)
    {
        var path = Path.Combine(root, "maintenance", "maintenance.json");
        if (new FileInfo(path).Length > 65536) throw new IOException("Maintenance contract exceeds limit");
        using var data = JsonDocument.Parse(File.ReadAllText(path));
        // Rollback restores playback IPC1 while retaining the new broker/parser.
        if (data.RootElement.GetProperty("protocol").GetInt32() is not (1 or 2)) throw new IOException("Unsupported maintenance playback protocol");
        if (!data.RootElement.TryGetProperty("metadataReader", out var reader)) return 1;
        var protocol = reader.GetInt32();
        if (protocol < 1) throw new IOException("Invalid metadata reader capability");
        return Math.Min(protocol, ReleaseCompatibility.ReaderProtocol);
    }

    public static string? MarkedRoot(IVelopackLocator locator)
    {
        if(!OperatingSystem.IsWindows()||locator.RootAppDir==null)return null;
        var root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(locator.RootAppDir));
        return File.Exists(Path.Combine(root,".star-tournament-install.json"))?root:null;
    }

    public static string? MachineRoot(IVelopackLocator locator)
    {
        if (!OperatingSystem.IsWindows() || locator.RootAppDir == null) return null;
        var root = locator.RootAppDir;
        if (!File.Exists(Path.Combine(root, ".star-tournament-install.json"))) return null;
        var broker = Path.Combine(root, "maintenance", "StarTournamentMaintenance.exe");
        if (!File.Exists(broker)) throw new InvalidOperationException("Installation maintenance is missing; repair with the installer");
        using var contract = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "maintenance", "maintenance.json")));
        if (contract.RootElement.GetProperty("protocol").GetInt32() is not (1 or 2))
            throw new InvalidOperationException("Unsupported installation maintenance; use the new installer");
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
    }

    public static bool RecoveryNeedsRepair(string root)
    {
        var path = Path.Combine(root, "maintenance", "recovery", "state.json");
        if (!File.Exists(path)) return false;
        if (new FileInfo(path).Length > 131072 || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Invalid protected recovery status");
        using var data = JsonDocument.Parse(File.ReadAllText(path)); var state = data.RootElement;
        if (state.GetProperty("Schema").GetInt32() != 1 || state.GetProperty("RootId").GetString() != StartupRecovery.InstallationId(root))
            throw new IOException("Foreign protected recovery status");
        var attempt = state.GetProperty("Attempt"); if (attempt.ValueKind == JsonValueKind.Null) return false;
        return attempt.GetProperty("Phase").GetString() switch
        {
            "capturing" or "prepared" or "restoring" => true,
            "installed" or "restored" => false,
            _ => throw new IOException("Unknown protected recovery phase")
        };
    }

    public static void MarkStartupReady(string root, string version)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        var statePath = Path.Combine(root, "maintenance", "recovery", "state.json");
        if (!File.Exists(statePath)) return;
        if (new FileInfo(statePath).Length > 131072) throw new IOException("Protected recovery status exceeds limit");
        using var data = JsonDocument.Parse(File.ReadAllText(statePath)); var state = data.RootElement;
        if (state.GetProperty("Schema").GetInt32() != 1 || state.GetProperty("RootId").GetString() != StartupRecovery.InstallationId(root)) throw new IOException("Foreign recovery status");
        var attempt = state.GetProperty("Attempt");
        if (attempt.ValueKind == JsonValueKind.Null || attempt.GetProperty("Phase").GetString() != "installed") return;
        if (attempt.GetProperty("Target").GetString() != version) throw new IOException("Startup latch version mismatch");
        var id = attempt.GetProperty("Id").GetString();
        if (id == null || id.Length != 32 || id.Any(c => !"0123456789abcdef".Contains(c))) throw new IOException("Invalid protected recovery id");
        var path = Path.Combine(root, "maintenance", "recovery", id + ".ready");
        // Only append, never truncate/replace: a normal user can conservatively forbid a
        // downgrade, but cannot authorize application writes or erase another user's health.
        var nativePath = path.StartsWith(@"\\?\", StringComparison.Ordinal) ? path : @"\\?\" + Path.GetFullPath(path);
        using var handle = CreateFileW(nativePath, 0x00100004, 3, IntPtr.Zero, 3, 0x80000080, IntPtr.Zero);
        if (handle.IsInvalid) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        using var output = new FileStream(handle, FileAccess.Write); output.WriteByte(1); output.Flush();
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

    public static bool IsBlocked(string root, string version)
    {
        var path = Path.Combine(root, "maintenance", "recovery", "state.json"); if (!File.Exists(path)) return false;
        if (new FileInfo(path).Length > 131072) throw new IOException("Protected recovery status exceeds limit");
        using var data = JsonDocument.Parse(File.ReadAllText(path)); var state = data.RootElement;
        if (state.GetProperty("Schema").GetInt32() != 1 || state.GetProperty("RootId").GetString() != StartupRecovery.InstallationId(root)) throw new IOException("Foreign recovery status");
        return state.GetProperty("Blocked").EnumerateArray().Any(v => v.GetString() == version);
    }

    public static string? PendingStartup(string root, string version)
    {
        var path = Path.Combine(root, "maintenance", "recovery", "state.json");
        if (!File.Exists(path)) return null;
        if (new FileInfo(path).Length > 131072) throw new IOException("Protected recovery status exceeds limit");
        using var data = JsonDocument.Parse(File.ReadAllText(path)); var state = data.RootElement;
        if (state.GetProperty("Schema").GetInt32() != 1 || state.GetProperty("RootId").GetString() != StartupRecovery.InstallationId(root)) throw new IOException("Foreign recovery status");
        var attempt = state.GetProperty("Attempt");
        if (attempt.ValueKind == JsonValueKind.Null || attempt.GetProperty("Phase").GetString() != "installed") return null;
        if (attempt.GetProperty("Target").GetString() != version) throw new IOException("Startup recovery target mismatch");
        var id = attempt.GetProperty("Id").GetString() ?? "";
        if (id.Length != 32 || id.Any(c => !"0123456789abcdef".Contains(c))) throw new IOException("Invalid protected recovery id");
        return StartupConfirmed(root, id) ? null : id;
    }
    public static bool StartupConfirmed(string root, string id)
    {
        if (id.Length != 32 || id.Any(c => !"0123456789abcdef".Contains(c))) throw new IOException("Invalid protected recovery id");
        var path = Path.Combine(root, "maintenance", "recovery", "state.json");
        if (new FileInfo(path).Length > 131072) throw new IOException("Protected recovery status exceeds limit");
        using var data = JsonDocument.Parse(File.ReadAllText(path)); var state = data.RootElement;
        if (state.GetProperty("Schema").GetInt32() != 1 || state.GetProperty("RootId").GetString() != StartupRecovery.InstallationId(root) ||
            state.GetProperty("Attempt").GetProperty("Id").GetString() != id || state.GetProperty("Attempt").GetProperty("Phase").GetString() != "installed")
            throw new IOException("Startup recovery transaction changed");
        // FileInfo.Length throws for a lost latch: uncertainty never permits rollback.
        return new FileInfo(Path.Combine(root, "maintenance", "recovery", id + ".ready")).Length != 0;
    }
    public static void WatchStartup(string root, string version, string[] arguments)
    {
        var id = PendingStartup(root, version); if (id == null) return;
        using var caller = Process.GetCurrentProcess();
        var start = new ProcessStartInfo(Path.Combine(root, "maintenance", "StarTournamentMaintenance.exe"))
        { UseShellExecute = false, RedirectStandardOutput = true, WorkingDirectory = root };
        foreach (var arg in new[] { "--watch-startup", id, caller.Id.ToString(), caller.StartTime.ToUniversalTime().Ticks.ToString(),
            Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(arguments))) }) start.ArgumentList.Add(arg);
        using var watcher = Process.Start(start) ?? throw new IOException("Startup supervisor did not start");
        var ack = watcher.StandardOutput.ReadLineAsync();
        if (!ack.Wait(TimeSpan.FromSeconds(30)) || ack.Result != "STAR_TOURNAMENT_STARTUP_WATCH_READY") throw new IOException("Startup supervision was not acknowledged");
    }

    public static bool InstallerAllowsLaunch(string root)
    {
        var path=Path.TrimEndingDirectorySeparator(Path.GetFullPath(root))+".installer/state.json";
        try
        {
            using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            if(input.Length>131072)throw new IOException("Installer state exceeds limit");
            using var data=JsonDocument.Parse(input);var state=data.RootElement;
            if(state.GetProperty("Schema").GetInt32()!=1||state.GetProperty("RootId").GetString()!=StartupRecovery.InstallationId(root))throw new IOException("Foreign installer state");
            return state.GetProperty("Phase").GetString() switch
            {
                "extracting" or "committed" or "restored"=>true,
                "prepared" or "saved" or "installed" or "restoring"=>false,
                _=>throw new IOException("Unknown installer state")
            };
        }
        catch(FileNotFoundException){return true;}catch(DirectoryNotFoundException){return true;}
        catch(Exception error) when(error is JsonException or KeyNotFoundException or InvalidOperationException){throw new IOException("Invalid installer launch state",error);}
    }

    public static bool InstallerPending(string root) => File.Exists(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root))+".installer/state.json") || Directory.Exists(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root))+".installer/state.json");

    public static bool Declined(string packages, string version)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(packages, "star-apply-declined.json")));
            return doc.RootElement.GetProperty("version").GetString() == version;
        }
        catch (IOException) { return false; }
        catch (JsonException) { return false; }
        catch (KeyNotFoundException) { return false; }
    }

    public static void Activate(string root, string packages, bool restart, string[] restartArguments, bool explicitRetry = false)
    {
        if(InstallerPending(root))throw new IOException("Завершите или повторите установку перед обновлением игры.");
        using var contract = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "maintenance", "maintenance.json")));
        if (contract.RootElement.GetProperty("protocol").GetInt32() != 2) throw new MaintenanceUpgradeRequiredException();
        var broker = Path.Combine(root, "maintenance", "StarTournamentMaintenance.exe");
        using var caller = Process.GetCurrentProcess();
        var start = new ProcessStartInfo(broker) { UseShellExecute = false, WorkingDirectory = root, RedirectStandardOutput = true };
        start.ArgumentList.Add("--activate");
        start.ArgumentList.Add(packages);
        start.ArgumentList.Add(restart ? "true" : "false");
        start.ArgumentList.Add(caller.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add(caller.StartTime.ToUniversalTime().Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(restartArguments))));
        start.ArgumentList.Add(explicitRetry ? "true" : "false");
        using var supervisor = Process.Start(start) ?? throw new IOException("Maintenance supervisor did not start");
        var ready = supervisor.StandardOutput.ReadLineAsync();
        if (!ready.Wait(TimeSpan.FromSeconds(30)) || ready.Result != "STAR_TOURNAMENT_ACTIVATION_READY")
            throw new IOException("Maintenance supervisor did not acknowledge the live launcher");
        // Return and release the actual launcher/game lease. The stable normal-user supervisor
        // waits for this exact process to exit before requesting elevation, then restarts unelevated.
    }
}
