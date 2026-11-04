using System.Diagnostics;

namespace StarTournament.Distribution;

/// <summary>The Player has startup-health IPC, never update-control IPC.</summary>
public static class PlayerLaunchEnvironment
{
    public const string LegacyStatus = "STAR_TOURNAMENT_UPDATE_STATUS";
    public const string LegacyCommand = "STAR_TOURNAMENT_UPDATE_COMMAND";
    public static void IsolateUpdates(ProcessStartInfo player)
    {
        // Remove inherited legacy values too, not just assignments by this launcher.
        player.Environment.Remove(LegacyStatus);
        player.Environment.Remove(LegacyCommand);
    }
}
