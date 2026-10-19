using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using StarTournament.Distribution;

static partial class Maintenance
{
    static LauncherConfig UpgradeConfig(string path)=>JsonSerializer.Deserialize<LauncherConfig>(BrokerUpgradeCandidate.ReadBounded(path,65536),Json)??throw new IOException("Broker payload identity missing");
    static int UpgradeBroker(string raw,Caller caller,string callerHash)
    {
        PlatformRequirements.RequireSupported("win");if(!IsElevated(Environment.ProcessId))throw new UnauthorizedAccessException("Broker upgrade requires elevation");
        var root=WindowsBrokerUpgradeAccess.Root(SafeRoot(raw));
        var payload=UpgradeConfig(Path.Combine(root,"current","release.json"));
        var anchor=Encoding.UTF8.GetString(BrokerUpgradeCandidate.ReadBounded(Path.Combine(root,"maintenance","update-public.pem"),65536));
        var directory=Path.GetDirectoryName(Path.GetFullPath(Environment.ProcessPath!))!;
        var actor=BrokerUpgradeCandidate.Load(directory,anchor,UpgradeConfig(Path.Combine(directory,"maintenance.json")));
        WindowsBrokerUpgradeAccess.Candidate(actor);
        var state=BrokerUpgradeStateStore.Read(root,payload);
        bool current=directory.Equals(Path.Combine(root,"current","broker-upgrade"),StringComparison.OrdinalIgnoreCase)&&actor.Payload==payload;
        bool retained=state!=null&&actor.ManifestHash==state.ManifestHash&&directory.Equals(Path.Combine(root,"maintenance","broker-upgrade-hosts",actor.ManifestHash),StringComparison.OrdinalIgnoreCase);
        if(!current&&!retained)throw new UnauthorizedAccessException("Upgrade actor is not this installation's signed current/retained component");
        RequireUpgradeCaller(root,caller,callerHash);
        if(File.Exists(Path.Combine(root,"maintenance","installing.flag"))||!WindowsInstallation.InstallerAllowsLaunch(root))throw new IOException("Finish/recover the installer before changing maintenance");
        using var lease=WindowsInstallLease.TryAcquire(root)??throw new IOException("Installation is busy");
        CheckNoProcesses(root,caller.Pid);RequireUpgradeCaller(root,caller,callerHash);
        var tx=new WindowsBrokerUpgradeTransaction(root,payload,CreateRecoveryDirectory,CreateRecoveryFile);
        state=tx.Read();
        if(state is {Phase:"committed"})
        {
            var previous=WindowsBrokerUpgradeStartup.Retained(root,state.ManifestHash,anchor,payload);
            WindowsBrokerUpgradeAccess.Candidate(previous);tx.CleanupRetained(previous);
        }
        else if(state!=null){tx.Recover();tx.Cleanup();}
        if(WindowsInstallation.MetadataReader(root)>=ReleaseCompatibility.ReaderProtocol)return 0;
        BrokerUpgradeCandidate candidate;
        try{candidate=BrokerUpgradeCandidate.Load(Path.Combine(root,"current","broker-upgrade"),anchor,payload);}
        catch(Exception e)when(state!=null&&actor.Payload==payload&&actor.ManifestHash==state.ManifestHash&&e is IOException or System.Security.Cryptography.CryptographicException or JsonException or FormatException or ArgumentException)
        {candidate=actor;}
        bool retainedSource=!candidate.DirectoryPath.Equals(Path.Combine(root,"current","broker-upgrade"),StringComparison.OrdinalIgnoreCase);
        WindowsBrokerUpgradeAccess.Candidate(candidate);ReleaseCompatibility.Require(payload.Compatibility!,"win");
        RetainUpgradeActor(root,candidate,anchor);
        RequireUpgradeCaller(root,caller,callerHash);CheckNoProcesses(root,caller.Pid);
        try
        {
            if(retainedSource)tx.PrepareRetained(candidate);else tx.Prepare(candidate);
            RequireUpgradeCaller(root,caller,callerHash);CheckNoProcesses(root,caller.Pid);
            if(retainedSource)tx.ApplyRetained(candidate);else tx.Apply(candidate);
        }
        catch{tx.Recover();throw;}
        try{if(retainedSource)tx.CleanupRetained(candidate);else tx.Cleanup(candidate);}catch(Exception error){UpdateFeedback.Log("broker-upgrade-committed-cleanup",error);} // Never undo a committed component.
        if(WindowsInstallation.MetadataReader(root)<ReleaseCompatibility.ReaderProtocol)throw new IOException("Upgraded broker capability readback failed");
        return 0;
    }
    static void RequireUpgradeCaller(string root,Caller caller,string expected)
    {
        if(!Regex.IsMatch(expected,@"\A[a-f0-9]{64}\z"))throw new UnauthorizedAccessException("Frozen current launcher hash required");
        ValidateCaller(root,caller);
        var fixedImage=Path.Combine(root,"current","StarTournamentLauncher.exe");WindowsBrokerUpgradeAccess.FilePath(fixedImage);
        using var process=Process.GetProcessById(caller.Pid);
        if(!string.Equals(Path.GetFullPath(process.MainModule!.FileName!),fixedImage,StringComparison.OrdinalIgnoreCase))throw new UnauthorizedAccessException("Exact current launcher required");
        using var input=new FileStream(fixedImage,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant()!=expected)throw new UnauthorizedAccessException("Frozen launcher image changed");
        WindowsBrokerUpgradeAccess.FilePath(Path.Combine(root,"current","release.json"));
    }
    static void RetainUpgradeActor(string root,BrokerUpgradeCandidate candidate,string anchor)
    {
        var parent=Path.Combine(root,"maintenance","broker-upgrade-hosts");CreateRecoveryDirectory(parent);
        var final=Path.Combine(parent,candidate.ManifestHash);BrokerUpgradeCandidate.Plain(final);
        if(Directory.Exists(final))
        {
            var existing=BrokerUpgradeCandidate.Load(final,anchor,candidate.Payload);WindowsBrokerUpgradeAccess.Candidate(existing);
            if(existing.ManifestHash!=candidate.ManifestHash)throw new IOException("Retained actor differs from target");return;
        }
        var next=final+".prepare-"+Guid.NewGuid().ToString("N");CreateRecoveryDirectory(next);
        try
        {
            candidate.Reverify();
            foreach(var name in BrokerUpgradeCandidate.FileNames.Append(BrokerUpgradeCandidate.EnvelopeName))
            {
                using var input=new FileStream(Path.Combine(candidate.DirectoryPath,name),FileMode.Open,FileAccess.Read,FileShare.Read);
                using var output=CreateRecoveryFile(Path.Combine(next,name));input.CopyTo(output);output.Flush(true);
            }
            var copied=BrokerUpgradeCandidate.Load(next,anchor,candidate.Payload);WindowsBrokerUpgradeAccess.Candidate(copied);
            if(copied.ManifestHash!=candidate.ManifestHash)throw new IOException("Retained actor changed during copy");
            Directory.Move(next,final);
        }
        finally{if(Directory.Exists(next)){foreach(var name in BrokerUpgradeCandidate.FileNames.Append(BrokerUpgradeCandidate.EnvelopeName)){var path=Path.Combine(next,name);BrokerUpgradeCandidate.Plain(path);File.Delete(path);}Directory.Delete(next);}}
    }
}
