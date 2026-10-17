namespace StarTournament.Distribution;

/// <summary>Normal-user observation only. The elevated restore adapter rechecks fixed transaction,
/// startup latch, exact supervisor, install lease and cross-user idle before touching files.</summary>
public static class WindowsStartupWatch
{
    public static async Task<bool> Observe(Func<bool> launcherAlive, Func<bool> confirmed,
        Func<Task> waitForPlayer, Func<bool> mustPreserve, Func<Task> restore, Action relaunch)
    {
        while (launcherAlive())
        {
            if (confirmed()) return true;
            await Task.Delay(100);
        }
        // Launcher death can leave a real Player alive. No timeout grants replacement.
        await waitForPlayer();
        if (confirmed() || mustPreserve()) return true;
        await restore(); // Cancellation/failure must never launch old files or report restoration.
        relaunch();
        return false;
    }
}
