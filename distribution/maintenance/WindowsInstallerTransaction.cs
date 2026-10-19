using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;

namespace StarTournament.Distribution;

public sealed record InstallerTransactionState(int Schema, string RootId, string AppId, string Target, string? Previous, string Phase);

/// <summary>Fixed sibling-slot installer replacement. The elevated adapter validates Program Files,
/// owner/ACLs, candidate identity/channel, idle processes and holds WindowsInstallLease for mutations.
/// NSIS may only extract into Stage, never over the existing application.</summary>
public sealed class WindowsInstallerTransaction
{
    static readonly JsonSerializerOptions Json = new() { UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow };
    readonly string root, slot, previous, abandoned, statePath;
    readonly LauncherConfig candidate;
    readonly Action<string> mkdir;
    readonly Func<string,FileStream> create;
    readonly Action<string> checkpoint;
    readonly Func<string,FileStream> ready;
    public string Stage { get; }
    public WindowsInstallerTransaction(string root, LauncherConfig candidate, Action<string>? mkdir=null, Func<string,FileStream>? create=null, Action<string>? checkpoint=null,Func<string,FileStream>? createReady=null)
    {
        if(!Path.IsPathFullyQualified(root)||candidate.Platform!="win"||candidate.Architecture!="x64"||!Canonical(candidate.Version)||candidate.UpdateChannel is not("test" or "production")||Velopack.SemanticVersion.Parse(candidate.Version).IsPrerelease!=(candidate.UpdateChannel=="test"))throw new ArgumentException("Absolute Windows x64 install required");
        this.root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));this.candidate=candidate;slot=this.root+".installer";
        Stage=Path.Combine(slot,"stage");previous=Path.Combine(slot,"previous");abandoned=Path.Combine(slot,"abandoned");statePath=Path.Combine(slot,"state.json");
        this.mkdir=mkdir??(p=>Directory.CreateDirectory(p));this.create=create??(p=>new FileStream(p,FileMode.CreateNew,FileAccess.Write,FileShare.None));this.checkpoint=checkpoint??(_=>{});this.ready=createReady??this.create;
        Plain(this.root);Plain(slot);
    }
    public InstallerTransactionState? Read()
    {
        Plain(statePath);if(!File.Exists(statePath))return null;
        using var input=File.OpenRead(statePath);if(input.Length>4096)throw new IOException("Installer journal exceeds limit");
        InstallerTransactionState state;
        try{state=JsonSerializer.Deserialize<InstallerTransactionState>(input,Json)??throw new JsonException();}catch(JsonException e){throw new IOException("Invalid installer journal",e);}
        if(state.Schema!=1||state.RootId!=StartupRecovery.InstallationId(root)||state.AppId!=candidate.AppId||!Canonical(state.Target)||
            state.Previous!=null&&!Canonical(state.Previous)||state.Phase is not("extracting" or "prepared" or "saved" or "installed" or "committed" or "restoring" or "restored"))throw new IOException("Foreign installer journal");
        return state;
    }
    public void Begin(string? installedVersion)
    {
        if(Read() is { } existing)
        {
            if(existing.Phase=="committed")CleanupCommitted();
            else Recover();
            ClearSettled();
        }
        if(Directory.Exists(slot)&&Directory.EnumerateFileSystemEntries(slot).Any())throw new IOException("Unknown installer slot contents");
        if(installedVersion!=null&&(!Canonical(installedVersion)||Velopack.SemanticVersion.Parse(candidate.Version)<Velopack.SemanticVersion.Parse(installedVersion)||
            Velopack.SemanticVersion.Parse(installedVersion).IsPrerelease!=(candidate.UpdateChannel=="test")))throw new IOException("Installer downgrade or channel change is not allowed");
        if(installedVersion==null&&Directory.Exists(root)&&!Directory.EnumerateFileSystemEntries(root).Any())Directory.Delete(root);
        if(Directory.Exists(root)!=(installedVersion!=null))throw new IOException("Install root changed before preparation");
        mkdir(slot);Write(new(1,StartupRecovery.InstallationId(root),candidate.AppId,candidate.Version,installedVersion,"extracting"));
        mkdir(Stage);checkpoint("stage-created");
    }
    public void Commit()
    {
        var state=Read()??throw new IOException("Installer journal missing");
        if(state.Phase!="extracting"||state.Target!=candidate.Version)throw new IOException("Wrong installer phase or target");
        if(Directory.Exists(root)!=(state.Previous!=null))throw new IOException("Install root appeared or disappeared while extracting; no root replaced");
        ValidateCandidate(Stage);checkpoint("candidate-verified");
        using var transfer=WindowsRecoveryStore.TransferForInstaller(root,candidate,mkdir,create,ready,checkpoint);
        Write(state with{Phase="prepared"});checkpoint("prepared");
        if(state.Previous!=null){Plain(root);Directory.Move(root,previous);checkpoint("old-root-moved");}
        Write(state with{Phase="saved"});checkpoint("saved");
        Directory.Move(Stage,root);checkpoint("new-root-moved");
        ValidateCandidate(root);
        if(File.Exists(Path.Combine(root,"maintenance","recovery","state.json")))new WindowsRecoveryStore(root,candidate,mkdir,create,ready).CompleteInstallerTransfer();
        Write(state with{Phase="installed"});checkpoint("installed");
    }
    public void FinalizeInstall()
    {
        var state=Read()??throw new IOException("Installer journal missing");
        if(state.Phase is not("installed" or "committed"))throw new IOException("Installation not committed");
        ValidateCandidate(root);Write(state with{Phase="committed"});checkpoint("committed");
        CleanupCommitted();
    }
    public void Recover()
    {
        var state=Read();if(state==null)return;
        if(state.Phase=="committed"){CleanupCommitted();return;}
        if(state.Phase=="restored"){Remove(Stage);Remove(abandoned);return;}
        if(state.Previous!=null&&!Directory.Exists(previous)&&
            (!Directory.Exists(root)||state.Phase is "saved" or "installed"))throw new IOException("Previous backup missing; no files changed");
        if(state.Previous==null&&state.Phase=="extracting"&&Directory.Exists(root))throw new IOException("Unexpected public root during extraction");
        Write(state with{Phase="restoring"});
        if(Directory.Exists(previous))
        {
            Plain(previous);Plain(abandoned);
            if(Directory.Exists(root))
            {
                if(Directory.Exists(abandoned))throw new IOException("Unexpected second abandoned install; no files removed");
                Directory.Move(root,abandoned);checkpoint("failed-root-retained");
            }
            Directory.Move(previous,root);checkpoint("previous-restored");
        }
        else if(state.Previous!=null)
        {
            // Before the old rename, or after restoration completed but before its journal write.
            if(!Directory.Exists(root))throw new IOException("Previous installation missing; retained files require inspection");
            if(Directory.Exists(abandoned)) { /* Recovery already restored the old root. */ }

        }
        else if(Directory.Exists(root))
        {
            if(state.Phase=="extracting")throw new IOException("Unexpected new root during extraction");
            if(Directory.Exists(abandoned))throw new IOException("Abandoned install already exists");
            Directory.Move(root,abandoned);checkpoint("fresh-root-retained");
        }
        Write(state with{Phase="restored"});checkpoint("restored");
        // Only owned candidate/partial files are discarded after the previous root is safe.
        Remove(Stage);Remove(abandoned);
    }
    public void ClearSettled()
    {
        var state=Read();if(state==null)return;
        if(state.Phase is not("committed" or "restored"))throw new IOException("Installer transaction is not settled");
        if(state.Phase=="committed")CleanupCommitted();else{Remove(Stage);Remove(abandoned);}
        if(Directory.Exists(previous))throw new IOException("Previous root still retained");
        File.Delete(statePath);if(Directory.Exists(slot)&&!Directory.EnumerateFileSystemEntries(slot).Any())Directory.Delete(slot);
    }
    void CleanupCommitted()
    {
        var state=Read()??throw new IOException("Installer journal missing");if(state.Phase!="committed")throw new IOException("Not committed");
        // Commit point cannot be undone even if backup deletion is interrupted.
        ValidateCandidate(root,state.Target);
        var flag=Path.Combine(root,"maintenance","installing.flag");Plain(flag);File.Delete(flag);Remove(previous);Remove(Stage);Remove(abandoned);
    }
    public void ValidateCandidate(string directory, string? expectedVersion=null)
    {
        var expected=expectedVersion==null?candidate:candidate with{Version=expectedVersion};
        Plain(directory);TreePlain(directory);
        foreach(var relative in new[]{".star-tournament-install.json",".portable","Star Tournament.exe","Update.exe","Uninstall.exe","app-icon.ico","current/release.json","current/sq.version","current/StarTournamentLauncher.exe","maintenance/StarTournamentMaintenance.exe","maintenance/maintenance.json","maintenance/update-public.pem"})
            if(!File.Exists(Path.Combine(directory,relative))||(relative!=".portable"&&new FileInfo(Path.Combine(directory,relative)).Length==0))throw new IOException("Incomplete candidate: "+relative);
        if(WindowsInstallerPlan.Inspect(directory,expected,requireCompleteMetadata:true).InstalledVersion!=expected.Version)throw new IOException("Candidate native version mismatch");
        var configPath=Path.Combine(directory,"current","release.json");if(new FileInfo(configPath).Length>65536)throw new IOException("Candidate config exceeds limit");
        var config=JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(configPath),new JsonSerializerOptions{PropertyNameCaseInsensitive=true});
        if(config==null||config.AppId!=expected.AppId||config.Platform!="win"||config.Architecture!="x64"||config.Version!=expected.Version||config.UpdateChannel!=expected.UpdateChannel||(expectedVersion==null&&config.Compatibility!=expected.Compatibility))throw new IOException("Candidate identity mismatch");
        var game=Path.GetFullPath(Path.Combine(directory,"current",config.GameExecutable));
        if(!game.StartsWith(Path.Combine(Path.GetFullPath(directory),"current")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||(!File.Exists(game)||new FileInfo(game).Length==0))throw new IOException("Candidate Player is missing or outside payload");
        var contract=Path.Combine(directory,"maintenance","maintenance.json");if(new FileInfo(contract).Length>65536)throw new IOException("Candidate maintenance metadata exceeds limit");
        using var data=JsonDocument.Parse(File.ReadAllText(contract));var identity=data.RootElement;
        if(config.Compatibility!=null && WindowsInstallation.MetadataReader(directory)<config.Compatibility.MinimumUpdaterProtocol)throw new IOException("Candidate broker cannot read its release metadata");
        if(identity.GetProperty("protocol").GetInt32()!=2||identity.GetProperty("platform").GetString()!="win"||identity.GetProperty("architecture").GetString()!="x64"||identity.GetProperty("appId").GetString()!=expected.AppId||identity.GetProperty("updateChannel").GetString()!=expected.UpdateChannel||identity.GetProperty("version").GetString()!=expected.Version)throw new IOException("Candidate maintenance contract mismatch");
        using var entry=File.OpenRead(Path.Combine(directory,"Star Tournament.exe"));using var broker=File.OpenRead(Path.Combine(directory,"maintenance","StarTournamentMaintenance.exe"));
        if(!SHA256.HashData(entry).SequenceEqual(SHA256.HashData(broker)))throw new IOException("Candidate stable entry differs from maintenance");
    }
    void Write(InstallerTransactionState state)
    {
        var next=statePath+".next";Plain(next);
        if(File.Exists(next))File.Delete(next);
        try{using(var output=create(next)){JsonSerializer.Serialize(output,state,Json);output.Flush(true);}File.Move(next,statePath,true);}finally{File.Delete(next);}
    }
    static bool Canonical(string? value)=>value!=null&&value.Length<=128&&Velopack.SemanticVersion.TryParse(value,out var v)&&v.ToString()==value;
    static void Remove(string path){Plain(path);if(Directory.Exists(path)){TreePlain(path);Directory.Delete(path,true);}}
    static void TreePlain(string path){Plain(path);foreach(var item in Directory.EnumerateFileSystemEntries(path)){Plain(item);if(Directory.Exists(item))TreePlain(item);}}
    static void Plain(string path)
    {
        for(string? p=path;p!=null;p=Path.GetDirectoryName(p))
        {try{if((File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new IOException("Installer path contains a reparse point");}catch(FileNotFoundException){}catch(DirectoryNotFoundException){}}
    }
}
