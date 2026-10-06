using System.Security.AccessControl;
using System.Security.Principal;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.Versioning;

namespace StarTournament.Distribution;

/// <summary>A cross-user, per-install mutex. The owning thread lives until Dispose.</summary>
public sealed class WindowsInstallLease : IDisposable
{
    readonly ManualResetEventSlim release = new(false);
    readonly Thread owner;
    readonly TaskCompletionSource<bool> acquired = new(TaskCreationOptions.RunContinuationsAsynchronously);
    bool disposed;

    [SupportedOSPlatform("windows")]
    WindowsInstallLease(string root)
    {
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(root).TrimEnd('\\').ToUpperInvariant())));
        owner = new Thread(() => Own("Global\\StarTournamentInstall-" + digest)) { IsBackground = true, Name = "StarTournamentInstallLease" };
        owner.Start();
        if (!acquired.Task.GetAwaiter().GetResult()) { release.Dispose(); throw new IOException("Install is busy"); }
    }

    [SupportedOSPlatform("windows")]
    void Own(string name)
    {
        try
        {
            var security = new MutexSecurity();
            security.AddAccessRule(new MutexAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                MutexRights.Synchronize | MutexRights.Modify, AccessControlType.Allow));
            Mutex mutex;
            try { mutex = MutexAcl.Create(false, name, out _, security); }
            catch (UnauthorizedAccessException)
            {
                // Create opens an existing mutex with broader rights than this lease needs.
                // A mutex created by another account grants World only these two rights.
                mutex = MutexAcl.OpenExisting(name, MutexRights.Synchronize | MutexRights.Modify);
            }
            using (mutex)
            try
            {
                if (!mutex.WaitOne(0)) { acquired.TrySetResult(false); return; }
                acquired.TrySetResult(true); release.Wait(); mutex.ReleaseMutex();
            }
            catch (AbandonedMutexException) { acquired.TrySetResult(true); release.Wait(); mutex.ReleaseMutex(); }
        }
        catch { acquired.TrySetResult(false); }
    }

    public static WindowsInstallLease? TryAcquire(string root)
    {
        if (!OperatingSystem.IsWindows()) return null;
        try { return new WindowsInstallLease(root); } catch { return null; }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true; release.Set(); owner.Join(); release.Dispose();
    }
}
