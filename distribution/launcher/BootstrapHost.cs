using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Velopack.Locators;

namespace StarTournament.Distribution;

/// <summary>Stable executable outside current/outer app replacement. Its code and trust
/// remain independent of the target launcher. Never loads an assembly from the payload.</summary>
public static class BootstrapHost
{
    const string Switch = "--bootstrap-payload";
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    public static string MacDirectory(LauncherConfig config, string logs, string installRoot) =>
        Path.Combine(logs, config.AppId + ".recovery-" + StartupRecovery.InstallationId(installRoot), "host", "payload", "Contents", "MacOS");

    public static string? Payload(ref string[] args)
    {
        if (args.FirstOrDefault() != Switch) return null;
        if (args.Length < 2 || !Path.IsPathFullyQualified(args[1])) throw new ArgumentException("Absolute bootstrap payload required");
        var payload = Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[1]));
        if (OperatingSystem.IsWindows() && Path.GetFileName(payload) != "current") throw new IOException("Fixed current payload required");
        if (OperatingSystem.IsMacOS() && !payload.EndsWith(".app/Contents/MacOS", StringComparison.Ordinal)) throw new IOException("Fixed macOS payload required");
        var identity = ReadConfig(payload);
        var expected = OperatingSystem.IsWindows()
            ? Path.Combine(Path.GetDirectoryName(payload)!, "maintenance", "bootstrap")
            : MacDirectory(identity, UpdateFeedback.LogRoot, Path.GetFullPath(Path.Combine(payload, "../..")));
        if (!Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppContext.BaseDirectory)).Equals(expected,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new IOException("Payload may only be hosted by its own independent bootstrap");
        RequirePlainParents(expected);
        var anchor = ReadConfig(expected);
        if (anchor.AppId != identity.AppId || anchor.Platform != identity.Platform || anchor.Architecture != identity.Architecture || anchor.UpdateChannel != identity.UpdateChannel)
            throw new CryptographicException("Bootstrap cannot host a different product or channel");
        args = args[2..];
        return payload;
    }

    public static LauncherConfig ReadConfig(string directory)
    {
        var config = JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(Path.Combine(directory, "release.json")), Json)
            ?? throw new IOException("Bootstrap release identity missing");
        if (config.Platform is not ("win" or "osx") || config.Architecture is not ("x64" or "arm64") ||
            config.AppId != "tech.afonasev.star-tournament." + config.Platform + "-" + config.Architecture || config.UpdateChannel is not ("production" or "test"))
            throw new IOException("Invalid bootstrap product identity");
        return config;
    }

    public static ProcessStartInfo Start(string host, string payload, IEnumerable<string> args)
    {
        var start = new ProcessStartInfo(host) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(host)! };
        start.ArgumentList.Add(Switch); start.ArgumentList.Add(payload);
        foreach (var arg in args) start.ArgumentList.Add(arg);
        return start;
    }

    // First-install entry seeds a private, atomic copy. Later launches always reuse it,
    // even when the replaceable launcher is upgraded or cannot execute on this host.
    public static Process? RouteMac(string payload, string[] args)
    {
        if (!OperatingSystem.IsMacOS()) return null;
        var host = PrepareMacHost(payload, UpdateFeedback.LogRoot);
        return Process.Start(Start(Path.Combine(host, "StarTournamentLauncher"), payload, args)) ?? throw new IOException("Independent bootstrap did not start");
    }

    public static string PrepareMacHost(string payload, string logs)
    {
        var config = ReadConfig(payload);
        var root = Path.GetFullPath(Path.Combine(payload, "../.."));
        RequirePlainParents(logs);
        Directory.CreateDirectory(logs);
        using var lease = new FileStream(Path.Combine(logs, config.AppId + ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var host = MacDirectory(config, logs, root);
        var parent = Path.GetDirectoryName(host)!;
        RequirePlainParents(parent); Directory.CreateDirectory(parent);
        if (!Directory.Exists(host))
        {
            // Runtime publish is self-contained/single-file with native libraries embedded.
            // UpdateMac is retained too: applying the next payload never executes its updater.
            var stage = Path.Combine(parent, ".bootstrap-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            try
            {
                foreach (var name in new[] { "StarTournamentLauncher", "UpdateMac", "release.json", "update-public.pem" })
                {
                    var source = Path.Combine(payload, name);
                    using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var output = new FileStream(Path.Combine(stage, name), FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    { input.CopyTo(output); output.Flush(true); }
                    if (name is "StarTournamentLauncher" or "UpdateMac")
                        File.SetUnixFileMode(Path.Combine(stage, name), File.GetUnixFileMode(source));
                }
                try { Directory.Move(stage, host); }
                catch (IOException) when (Directory.Exists(host)) { /* Another first launch won the atomic install. */ }
            }
            finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
        }
        RequirePlainParents(host);
        var anchor = ReadConfig(host);
        if (anchor.AppId != config.AppId || anchor.Platform != config.Platform || anchor.Architecture != config.Architecture || anchor.UpdateChannel != config.UpdateChannel ||
            !File.ReadAllBytes(Path.Combine(host, "update-public.pem")).SequenceEqual(File.ReadAllBytes(Path.Combine(payload, "update-public.pem"))))
            throw new CryptographicException("Independent bootstrap identity/trust mismatch");
        return host;
    }

    static void RequirePlainParents(string path)
    {
        for (var cursor = Path.GetFullPath(path); cursor != null; cursor = Path.GetDirectoryName(cursor))
            if (Directory.Exists(cursor) && (File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Bootstrap directory cannot use links");
    }

    public sealed class PayloadProcess(string payload) : IProcessImpl
    {
        public string GetCurrentProcessPath() => Path.Combine(payload, OperatingSystem.IsWindows() ? "StarTournamentLauncher.exe" : "StarTournamentLauncher");
        public uint GetCurrentProcessId() => checked((uint)Environment.ProcessId);
        public void Exit(int exitCode) => Environment.Exit(exitCode);
        public void StartProcess(string exePath, IEnumerable<string> args, string workDir, bool showWindow)
        {
            var start = new ProcessStartInfo(exePath) { UseShellExecute = false, WorkingDirectory = workDir, CreateNoWindow = !showWindow };
            foreach (var arg in args) start.ArgumentList.Add(arg);
            using var process = Process.Start(start) ?? throw new IOException("Updater process did not start");
        }
    }
}
