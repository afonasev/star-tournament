using System.Security.Cryptography;
using System.Text.Json;
using StarTournament.Distribution;

static class BootstrapHostChecks
{
    public static void Run()
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException("macOS host seeding fixture");
        var root = Path.Combine(Directory.GetCurrentDirectory(), ".local", "bootstrap-host-" + Guid.NewGuid().ToString("N"));
        var payload = Path.Combine(root, "Test.app", "Contents", "MacOS");
        var logs = Path.Combine(root, "state");
        Directory.CreateDirectory(payload);
        var json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var config = new LauncherConfig("tech.afonasev.star-tournament.osx-arm64", "osx", "arm64", "1.0.0", "2026-11-03", AuthenticatedSource.ProductionBase,
            "game/Player.app/Contents/MacOS/Player", "production", new("12.0", 2, ReleaseCompatibility.PayloadFormat, ReleaseCompatibility.DataPolicy), 1);
        var passed = 0;
        void Check(bool value, string label) { if (!value) throw new Exception(label); passed++; Console.WriteLine("PASS " + label); }
        try
        {
            File.WriteAllText(Path.Combine(payload, "release.json"), JsonSerializer.Serialize(config, json));
            File.WriteAllText(Path.Combine(payload, "update-public.pem"), "retained pinned key");
            File.WriteAllText(Path.Combine(payload, "StarTournamentLauncher"), "initial self-contained code");
            File.SetUnixFileMode(Path.Combine(payload, "StarTournamentLauncher"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            try { BootstrapHost.PrepareMacHost(payload, logs); throw new Exception("Missing updater accepted"); }
            catch (FileNotFoundException) { Check(true, "incomplete seed fails before atomic host publication"); }
            Check(!Directory.Exists(BootstrapHost.MacDirectory(config, logs, Path.Combine(root, "Test.app"))), "failed seed leaves no runnable partial host");
            File.WriteAllText(Path.Combine(payload, "UpdateMac"), "retained native updater");
            File.SetUnixFileMode(Path.Combine(payload, "UpdateMac"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var host = BootstrapHost.PrepareMacHost(payload, logs);
            Check(File.ReadAllText(Path.Combine(host, "StarTournamentLauncher")) == "initial self-contained code" &&
                File.ReadAllText(Path.Combine(host, "UpdateMac")) == "retained native updater", "standalone code/updater/trust/config are seeded together");
            Check((File.GetUnixFileMode(Path.Combine(host, "StarTournamentLauncher")) & UnixFileMode.UserExecute) != 0, "retained host stays executable");
            File.WriteAllText(Path.Combine(payload, "release.json"), JsonSerializer.Serialize(config with { Version = "1.1.0", Compatibility = config.Compatibility! with { MinimumUpdaterProtocol = 3, PayloadFormat = "launcher-generation3" } }, json));
            File.WriteAllText(Path.Combine(payload, "StarTournamentLauncher"), "incompatible new code");
            Check(BootstrapHost.PrepareMacHost(payload, logs) == host && File.ReadAllText(Path.Combine(host, "StarTournamentLauncher")) == "initial self-contained code", "incompatible replacement cannot overwrite independent host");
            File.WriteAllText(Path.Combine(payload, "update-public.pem"), "substituted key");
            try { BootstrapHost.PrepareMacHost(payload, logs); throw new Exception("Trust substitution accepted"); }
            catch (CryptographicException) { Check(true, "new payload cannot substitute stable trust anchor"); }
            Check(File.ReadAllText(Path.Combine(host, "update-public.pem")) == "retained pinned key", "stable trust bytes preserved after refusal");
            var arguments = new[] { "-muted", "-noaudio", "профиль ' two" };
            var start = BootstrapHost.Start(Path.Combine(host, "StarTournamentLauncher"), payload, arguments);
            Check(start.ArgumentList.SequenceEqual(new[] { "--bootstrap-payload", payload }.Concat(arguments)), "bootstrap invocation preserves exact quiet argv as data");
            var process = new BootstrapHost.PayloadProcess(payload);
            Check(process.GetCurrentProcessPath() == Path.Combine(payload, "StarTournamentLauncher") && process.GetCurrentProcessId() == Environment.ProcessId, "SDK resolves installed payload while process identity remains independent");
        }
        finally { Directory.Delete(root, true); }
        Console.WriteLine($"PASS {passed} independent bootstrap host contracts");
    }
}
