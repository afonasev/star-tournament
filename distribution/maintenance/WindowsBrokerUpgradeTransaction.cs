using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Velopack;

namespace StarTournament.Distribution;

/// <summary>Fixed-file durable transaction only. Elevated adapter MUST validate root/candidate
/// ownership/ACLs, exact caller/process fence and installation lease before use.</summary>
public sealed class WindowsBrokerUpgradeTransaction
{
    static readonly string[] Targets=["maintenance/StarTournamentMaintenance.exe","maintenance/maintenance.json","Star Tournament.exe"];
    static readonly string[] Names=["broker","contract","entry"];
    static readonly JsonSerializerOptions Json=new(){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    readonly string root,slot,statePath;
    readonly LauncherConfig payload;
    readonly Action<string> mkdir,checkpoint;
    readonly Func<string,FileStream> create;
    public WindowsBrokerUpgradeTransaction(string root,LauncherConfig currentPayload,Action<string>? mkdir=null,Func<string,FileStream>? create=null,Action<string>? checkpoint=null)
    {
        if(!Path.IsPathFullyQualified(root)||currentPayload.Platform!="win"||currentPayload.Architecture!="x64"||currentPayload.AppId!="tech.afonasev.star-tournament.win-x64"||
            currentPayload.UpdateChannel is not("production" or "test"))throw new ArgumentException("Fixed Windows installation required");
        this.root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));payload=currentPayload;
        slot=Path.Combine(this.root,"maintenance","broker-upgrade-v1");statePath=Path.Combine(slot,"state.json");Plain(slot);
        this.mkdir=mkdir??(p=>Directory.CreateDirectory(p));this.create=create??(p=>new FileStream(p,FileMode.CreateNew,FileAccess.Write,FileShare.None));this.checkpoint=checkpoint??(_=>{});
    }
    public BrokerUpgradeState? Read()=>BrokerUpgradeStateStore.Read(root,payload);
    public void Prepare(BrokerUpgradeCandidate candidate)=>PrepareCore(candidate,false);
    public void PrepareRetained(BrokerUpgradeCandidate candidate)=>PrepareCore(candidate,true);
    void PrepareCore(BrokerUpgradeCandidate candidate,bool retained)
    {
        Candidate(candidate,retained);if(Read()!=null)throw new IOException("Settle the prior broker transaction first");
        ValidatePreviousContract();var previous=new List<BrokerPreviousFile>();
        for(int i=0;i<Targets.Length;i++){var source=Target(i);var (size,hash)=Identity(source);if(size<=0||size>(i==1?65536:128*1024*1024))throw new IOException("Previous broker component exceeds bounds");previous.Add(new(Names[i],size,hash));}
        var state=new BrokerUpgradeState(1,StartupRecovery.InstallationId(root),payload.AppId,payload.Version,payload.UpdateChannel!,Guid.NewGuid().ToString("N"),candidate.ManifestHash,"capturing",previous.ToArray());
        // Publish the complete initial intent by directory rename; partial initial writes never
        // create a pending transaction or authorize a root-file mutation.
        var preparing=slot+".prepare-"+state.Id;Plain(preparing);if(Directory.Exists(preparing)||File.Exists(preparing))throw new IOException("Existing upgrade preparation");
        mkdir(preparing);using(var output=create(Path.Combine(preparing,"state.json"))){JsonSerializer.Serialize(output,state,Json);output.Flush(true);}checkpoint("intent-prepared");
        if(Directory.Exists(slot))Directory.Delete(slot);Directory.Move(preparing,slot);checkpoint("capture-started");
        foreach(var file in state.Previous)
        {var index=Array.IndexOf(Names,file.Name);Copy(Target(index),Backup(file.Name));if(Identity(Target(index))!=(file.Size,file.Sha256)||Identity(Backup(file.Name))!=(file.Size,file.Sha256))throw new IOException("Previous broker changed during snapshot");checkpoint("captured-"+file.Name);}
        Write(state with{Phase="prepared"});checkpoint("prepared");
    }
    public void Apply(BrokerUpgradeCandidate candidate)=>ApplyCore(candidate,false);
    public void ApplyRetained(BrokerUpgradeCandidate candidate)=>ApplyCore(candidate,true);
    void ApplyCore(BrokerUpgradeCandidate candidate,bool retained)
    {
        Candidate(candidate,retained);var state=Read()??throw new IOException("Broker snapshot missing");
        if(state.Phase!="prepared"||state.Version!=candidate.Payload.Version||state.ManifestHash!=candidate.ManifestHash)throw new IOException("Broker transaction/consent changed");
        VerifyBackups(state);VerifyOld(state);
        try
        {
            Write(state with{Phase="replacing"});checkpoint("replacing");
            for(int i=0;i<Targets.Length;i++)
            {
                var name=i==1?"maintenance.json":"StarTournamentMaintenance.exe";
                candidate.Reverify();Replace(Path.Combine(candidate.DirectoryPath,name),Target(i),path=>candidate.VerifyFile(path,name),state.Id);checkpoint("replaced-"+Names[i]);
            }
            VerifyNew(candidate);Write(state with{Phase="committed"});checkpoint("committed");
        }
        catch {Recover();throw;}
    }
    public void Recover()
    {
        var state=Read();if(state==null)return;
        if(state.Phase=="committed")return; // Commit point cannot be undone by cleanup failure.
        if(state.Phase is "capturing" or "prepared")VerifyOld(state);
        else
        {
            VerifyBackups(state);Write(state with{Phase="restoring"});
            for(int i=0;i<Targets.Length;i++)
            {var file=state.Previous.Single(f=>f.Name==Names[i]);Replace(Backup(file.Name),Target(i),p=>{if(Identity(p)!=(file.Size,file.Sha256))throw new IOException("Broker recovery readback failed");},state.Id);checkpoint("restored-"+Names[i]);}
            VerifyOld(state);
        }
        Write(state with{Phase="restored"});checkpoint("restored");
    }
    public void Cleanup(BrokerUpgradeCandidate? candidate=null)=>CleanupCore(candidate,false);
    public void CleanupRetained(BrokerUpgradeCandidate actor)=>CleanupCore(actor,true);
    public void CleanupAfterPayloadRestore(BrokerUpgradeCandidate actor,WindowsRecoveryStore recovery)=>CleanupCore(actor,true,recovery);
    void CleanupCore(BrokerUpgradeCandidate? candidate,bool retained,WindowsRecoveryStore? recovery=null)
    {
        var state=Read();if(state==null)return;
        if(state.Phase=="committed")
        {
            if(candidate==null)throw new IOException("Signed committed candidate required for cleanup");
            if(retained)Retained(candidate);else Candidate(candidate);
            if(state.ManifestHash!=candidate.ManifestHash)throw new IOException("Wrong committed candidate");
            if(recovery==null)VerifyNew(candidate);
            else
            {
                candidate.VerifyFile(Target(0),"StarTournamentMaintenance.exe");
                recovery.VerifyRestoredBrokerPlayback(root,payload);
            }
        }
        else if(state.Phase=="restored")VerifyOld(state);else throw new IOException("Settle broker recovery before cleanup");
        // Fixed names only. Never recursively remove unknown or foreign storage.
        var expected=state.Previous.Select(f=>f.Name+".previous").Append("state.json").Append("state.json.next-"+state.Id).ToHashSet(StringComparer.Ordinal);
        var entries=Directory.EnumerateFileSystemEntries(slot).ToArray();
        if(entries.Any(p=>!expected.Contains(Path.GetFileName(p))||Directory.Exists(p)))throw new IOException("Unknown retained broker upgrade files");
        foreach(var file in entries.Where(p=>Path.GetFileName(p)!="state.json")){Plain(file);File.Delete(file);}File.Delete(statePath);Directory.Delete(slot);
    }
    void Retained(BrokerUpgradeCandidate candidate)
    {
        if(candidate.Payload.AppId!=payload.AppId||candidate.Payload.UpdateChannel!=payload.UpdateChannel||
            candidate.DirectoryPath!=Path.Combine(root,"maintenance","broker-upgrade-hosts",candidate.ManifestHash))throw new IOException("Foreign retained upgrade actor");
        var anchor=System.Text.Encoding.UTF8.GetString(BrokerUpgradeCandidate.ReadBounded(Path.Combine(root,"maintenance","update-public.pem"),65536));
        if(BrokerUpgradeCandidate.Load(candidate.DirectoryPath,anchor,candidate.Payload).ManifestHash!=candidate.ManifestHash)throw new CryptographicException("Retained actor changed");
    }
    void Candidate(BrokerUpgradeCandidate candidate,bool retained=false)
    {
        var allowed=retained?Path.Combine(root,"maintenance","broker-upgrade-hosts",candidate.ManifestHash):Path.Combine(root,"current","broker-upgrade");
        if(candidate.Payload!=payload||!string.Equals(candidate.DirectoryPath,allowed,StringComparison.OrdinalIgnoreCase))throw new IOException("Candidate is not this fixed current payload");
        candidate.Reverify();
        var current=JsonSerializer.Deserialize<LauncherConfig>(BrokerUpgradeCandidate.ReadBounded(Path.Combine(root,"current","release.json"),65536),new JsonSerializerOptions{PropertyNameCaseInsensitive=true});
        if(current!=payload)throw new IOException("Current payload differs from upgrade identity");
        var anchor=System.Text.Encoding.UTF8.GetString(BrokerUpgradeCandidate.ReadBounded(Path.Combine(root,"maintenance","update-public.pem"),65536));
        if(BrokerUpgradeCandidate.Load(candidate.DirectoryPath,anchor,payload).ManifestHash!=candidate.ManifestHash)throw new CryptographicException("Candidate is not signed by this installation trust anchor");
    }
    void ValidatePreviousContract()
    {
        using var json=JsonDocument.Parse(BrokerUpgradeCandidate.ReadBounded(Target(1),65536));var value=json.RootElement;
        var names=value.EnumerateObject().Select(p=>p.Name).ToArray();
        if(names.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=names.Length || value.TryGetProperty("metadataReader",out var reader)&&reader.GetInt32() is not(1 or 2))
            throw new IOException("Ambiguous or unsupported previous broker capability");
        var old=JsonSerializer.Deserialize<LauncherConfig>(value.GetRawText(),new JsonSerializerOptions{PropertyNameCaseInsensitive=true});
        if(value.GetProperty("protocol").GetInt32() is not(1 or 2)||old==null||old.AppId!=payload.AppId||old.Platform!="win"||old.Architecture!="x64"||old.UpdateChannel!=payload.UpdateChannel||
            !SemanticVersion.TryParse(old.Version,out var version)||version.ToString()!=old.Version||version>SemanticVersion.Parse(payload.Version))throw new IOException("Foreign or newer installed broker contract");
    }
    void VerifyBackups(BrokerUpgradeState state){foreach(var file in state.Previous)if(Identity(Backup(file.Name))!=(file.Size,file.Sha256))throw new IOException("Retained broker snapshot changed");}
    void VerifyOld(BrokerUpgradeState state){for(int i=0;i<Targets.Length;i++){var file=state.Previous.Single(f=>f.Name==Names[i]);if(Identity(Target(i))!=(file.Size,file.Sha256))throw new IOException("Previous broker files changed");}}
    void VerifyNew(BrokerUpgradeCandidate candidate){for(int i=0;i<Targets.Length;i++)candidate.VerifyFile(Target(i),i==1?"maintenance.json":"StarTournamentMaintenance.exe");}
    string Target(int i)=>Path.Combine(root,Targets[i]);
    string Backup(string name)=>Path.Combine(slot,name+".previous");
    static bool Digest(string? value)=>Regex.IsMatch(value??"",@"\A[a-f0-9]{64}\z");
    static (long,string) Identity(string path){Plain(path);using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);var length=input.Length;if(length<=0||length>128*1024*1024)throw new IOException("Broker file exceeds bounds");return(length,Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant());}
    void Replace(string source,string target,Action<string> verify,string id)
    {
        Plain(source);Plain(target);var next=target+".broker-next-"+id;Plain(next);
        if(File.Exists(next))File.Delete(next); // Reserved next file under the validated transaction/lease.
        try{Copy(source,next);verify(next);File.Move(next,target,true);verify(target);}finally{File.Delete(next);}
    }
    void Copy(string source,string target)
    {Plain(source);Plain(target);using var input=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read);using var output=create(target);input.CopyTo(output);output.Flush(true);}
    void Write(BrokerUpgradeState state)
    {
        var next=statePath+".next-"+state.Id;Plain(next);if(File.Exists(next))File.Delete(next);
        try{using(var output=create(next)){JsonSerializer.Serialize(output,state,Json);output.Flush(true);}File.Move(next,statePath,true);}finally{File.Delete(next);}
    }
    static void Plain(string path)=>BrokerUpgradeCandidate.Plain(path);
}
