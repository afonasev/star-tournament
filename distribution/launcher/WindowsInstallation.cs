using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Velopack.Locators;

namespace StarTournament.Distribution;

/// <summary>The installer owns maintenance outside the replaceable current payload.</summary>
public static class WindowsInstallation
{
    public static string? MachineRoot(IVelopackLocator locator)
    {
        if (!OperatingSystem.IsWindows() || locator.RootAppDir == null) return null;
        var root = locator.RootAppDir;
        if (!File.Exists(Path.Combine(root, ".star-tournament-install.json"))) return null;
        var broker = Path.Combine(root, "maintenance", "StarTournamentMaintenance.exe");
        if (!File.Exists(broker)) throw new InvalidOperationException("Installation maintenance is missing; repair with the installer");
        using var contract = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "maintenance", "maintenance.json")));
        if (contract.RootElement.GetProperty("protocol").GetInt32() != 1)
            throw new InvalidOperationException("Unsupported installation maintenance; use the new installer");
        return root;
    }

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

    public static void Activate(string root, string packages, bool restart, string[] restartArguments)
    {
        var broker = Path.Combine(root, "maintenance", "StarTournamentMaintenance.exe");
        using var caller = Process.GetCurrentProcess();
        var start = new ProcessStartInfo(broker) { UseShellExecute = false, WorkingDirectory = root, RedirectStandardOutput = true };
        start.ArgumentList.Add("--activate");
        start.ArgumentList.Add(packages);
        start.ArgumentList.Add(restart ? "true" : "false");
        start.ArgumentList.Add(caller.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add(caller.StartTime.ToUniversalTime().Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(restartArguments))));
        using var supervisor = Process.Start(start) ?? throw new IOException("Maintenance supervisor did not start");
        var ready = supervisor.StandardOutput.ReadLineAsync();
        if (!ready.Wait(TimeSpan.FromSeconds(30)) || ready.Result != "STAR_TOURNAMENT_ACTIVATION_READY")
            throw new IOException("Maintenance supervisor did not acknowledge the live launcher");
        // Return and release the actual launcher/game lease. The stable normal-user supervisor
        // waits for this exact process to exit before requesting elevation, then restarts unelevated.
    }
}
