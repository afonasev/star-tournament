using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StarTournament.Distribution;

static class BrokerUpgradeChecks
{
    static readonly JsonSerializerOptions Camel=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase};
    static readonly LauncherConfig Payload=new("tech.afonasev.star-tournament.win-x64","win","x64","1.6.0","2026-10-17",AuthenticatedSource.ProductionBase,"game/StarTournament.exe","production",new("10.0.19043",2,ReleaseCompatibility.PayloadFormat,ReleaseCompatibility.DataPolicy));
    static int count;
    sealed class Fixture:IDisposable
    {
        public readonly string Root=Path.Combine(Directory.GetCurrentDirectory(),".local","broker-upgrade-test-"+Guid.NewGuid().ToString("N"));
        public string Candidate=>Path.Combine(Root,"current/broker-upgrade");
        public readonly RSA Key=RSA.Create(2048);
        public string Public=>Key.ExportSubjectPublicKeyInfoPem();
        public Fixture()
        {
            Directory.CreateDirectory(Candidate);Directory.CreateDirectory(Path.Combine(Root,"maintenance"));Directory.CreateDirectory(Path.Combine(Root,"current/game"));
            File.WriteAllText(Path.Combine(Root,"current/release.json"),JsonSerializer.Serialize(Payload,Camel));File.WriteAllText(Path.Combine(Root,"current/game/StarTournament.exe"),"unchanged player");File.WriteAllText(Path.Combine(Root,"balance.json"),"0 false own balance");
            File.WriteAllText(Path.Combine(Root,"maintenance/update-public.pem"),Public);
            File.WriteAllText(Path.Combine(Root,"maintenance/StarTournamentMaintenance.exe"),"old broker bytes");File.WriteAllText(Path.Combine(Root,"Star Tournament.exe"),"old root entry");
            File.WriteAllText(Path.Combine(Root,"maintenance/maintenance.json"),Contract(Payload with{Version="1.5.0",Compatibility=null},1));
            File.WriteAllText(Path.Combine(Candidate,"StarTournamentMaintenance.exe"),"new authenticated broker fixture NEVER EXECUTED");
            File.WriteAllText(Path.Combine(Candidate,"maintenance.json"),Contract(Payload,2));File.WriteAllText(Path.Combine(Candidate,"update-public.pem"),Public);Sign();
        }
        public BrokerUpgradeCandidate Load()=>BrokerUpgradeCandidate.Load(Candidate,Public,Payload);
        public WindowsBrokerUpgradeTransaction Tx(Action<string>? checkpoint=null)=>new(Root,Payload,checkpoint:checkpoint);
        public (WindowsRecoveryStore Recovery,BrokerUpgradeCandidate Actor,LauncherConfig Previous) RollbackWithCommittedBroker()
        {
            var previous=Payload with{Version="1.5.0",Compatibility=null};
            File.WriteAllText(Path.Combine(Root,"current/release.json"),JsonSerializer.Serialize(previous,Camel));
            File.WriteAllText(Path.Combine(Root,"Update.exe"),"old updater");File.WriteAllBytes(Path.Combine(Root,".portable"),[]);
            var recovery=new WindowsRecoveryStore(Root,previous);recovery.Prepare(previous,Payload.Version,false);
            recovery.Apply(()=>File.WriteAllText(Path.Combine(Root,"current/release.json"),JsonSerializer.Serialize(Payload,Camel)));
            var candidate=Load();var tx=Tx();tx.Prepare(candidate);tx.Apply(candidate);
            var retained=Path.Combine(Root,"maintenance/broker-upgrade-hosts",candidate.ManifestHash);Directory.CreateDirectory(retained);
            foreach(var name in BrokerUpgradeCandidate.FileNames.Append(BrokerUpgradeCandidate.EnvelopeName))File.Copy(Path.Combine(Candidate,name),Path.Combine(retained,name));
            var actor=BrokerUpgradeCandidate.Load(retained,Public,Payload);recovery.Restore();
            return(recovery,actor,previous);
        }
        public void Sign(Action<Dictionary<string,object?>>? edit=null,RSA? signer=null)
        {
            var files=BrokerUpgradeCandidate.FileNames.Select(name=>new Dictionary<string,object?>{["name"]=name,["size"]=new FileInfo(Path.Combine(Candidate,name)).Length,["sha256"]=Hash(File.ReadAllBytes(Path.Combine(Candidate,name)))}).ToList();
            var body=new Dictionary<string,object?>{["schema"]=1,["appId"]=Payload.AppId,["platform"]="win",["architecture"]="x64",["version"]=Payload.Version,["publicationDate"]=Payload.PublicationDate,["channel"]="production",["protocol"]=2,["metadataReader"]=2,["files"]=files};edit?.Invoke(body);SignRaw(JsonSerializer.Serialize(body),signer);
        }
        public void SignRaw(string body,RSA? signer=null)
        {
            var bytes=Encoding.UTF8.GetBytes(body);File.WriteAllText(Path.Combine(Candidate,BrokerUpgradeCandidate.EnvelopeName),JsonSerializer.Serialize(new{payload=Convert.ToBase64String(bytes),signature=Convert.ToBase64String((signer??Key).SignData(bytes,HashAlgorithmName.SHA256,RSASignaturePadding.Pss))}));
        }
        public void AssertOld(){Require(File.ReadAllText(Path.Combine(Root,"maintenance/StarTournamentMaintenance.exe"))=="old broker bytes");Require(File.ReadAllText(Path.Combine(Root,"Star Tournament.exe"))=="old root entry");Require(JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"maintenance/maintenance.json"))).RootElement.GetProperty("protocol").GetInt32()==1);AssertPayload();}
        public void AssertNew(){var c=Load();c.VerifyFile(Path.Combine(Root,"maintenance/StarTournamentMaintenance.exe"),"StarTournamentMaintenance.exe");c.VerifyFile(Path.Combine(Root,"Star Tournament.exe"),"StarTournamentMaintenance.exe");c.VerifyFile(Path.Combine(Root,"maintenance/maintenance.json"),"maintenance.json");AssertPayload();}
        public void AssertPayload(){Require(File.ReadAllText(Path.Combine(Root,"current/game/StarTournament.exe"))=="unchanged player");Require(File.ReadAllText(Path.Combine(Root,"balance.json"))=="0 false own balance");Require(File.ReadAllText(Path.Combine(Root,"maintenance/update-public.pem"))==Public);}
        public void Dispose(){Key.Dispose();if(Directory.Exists(Root))Directory.Delete(Root,true);}
    }
    static string Contract(LauncherConfig config,int protocol)
    {
        var value=JsonSerializer.SerializeToElement(config,Camel).EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value.Clone());
        value["protocol"]=JsonSerializer.SerializeToElement(protocol);if(protocol==2)value["metadataReader"]=JsonSerializer.SerializeToElement(2);return JsonSerializer.Serialize(value);
    }
    static string Hash(byte[] value)=>Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
    static void Require(bool value){if(!value)throw new Exception("Broker upgrade assertion failed");}
    static void Reject(Action action)
    {try{action();}catch(Exception e)when(e is IOException or CryptographicException or JsonException or InvalidOperationException or FormatException or ArgumentException){return;}throw new Exception("Expected broker upgrade rejection");}
    public static async Task Run()
    {
        void Test(string name,Action<Fixture> action){using var f=new Fixture();action(f);count++;Console.WriteLine("PASS "+name);}
        Test("separately signed exact candidate authenticates",f=>{Require(f.Load().Files.Count==3);f.AssertOld();});
        Test("canonical publication date is independent of OS calendar",f=>{var before=CultureInfo.CurrentCulture;try{CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("ar-SA");Require(f.Load().Payload.PublicationDate==Payload.PublicationDate);}finally{CultureInfo.CurrentCulture=before;}});
        foreach(var field in new[]{"schema","appId","platform","architecture","version","publicationDate","channel","protocol","metadataReader"})
            Test("signed candidate mismatch rejected: "+field,f=>{f.Sign(d=>d[field]=field is "schema" or "protocol" or "metadataReader"?99:"foreign");Reject(()=>f.Load());f.AssertOld();});
        Test("wrong signer rejected",f=>{using var other=RSA.Create(2048);f.Sign(signer:other);Reject(()=>f.Load());f.AssertOld();});
        Test("signature tamper rejected",f=>{var path=Path.Combine(f.Candidate,BrokerUpgradeCandidate.EnvelopeName);using var json=JsonDocument.Parse(File.ReadAllText(path));File.WriteAllText(path,JsonSerializer.Serialize(new{payload=json.RootElement.GetProperty("payload").GetString(),signature=Convert.ToBase64String(new byte[256])}));Reject(()=>f.Load());f.AssertOld();});
        foreach(var name in BrokerUpgradeCandidate.FileNames)
            Test("candidate bytes tamper rejected: "+name,f=>{File.AppendAllText(Path.Combine(f.Candidate,name),"changed");Reject(()=>f.Load());f.AssertOld();});
        Test("trusted signer cannot silently rotate root key",f=>{using var other=RSA.Create(2048);File.WriteAllText(Path.Combine(f.Candidate,"update-public.pem"),other.ExportSubjectPublicKeyInfoPem());f.Sign();Reject(()=>f.Load());f.AssertOld();});
        Test("signed foreign file path rejected",f=>{f.Sign(d=>((List<Dictionary<string,object?>>)d["files"]!)[0]["name"]="../outside.exe");Reject(()=>f.Load());f.AssertOld();});
        Test("duplicate file identity rejected",f=>{f.Sign(d=>{var rows=(List<Dictionary<string,object?>>)d["files"]!;rows[1]["name"]=rows[0]["name"];});Reject(()=>f.Load());f.AssertOld();});
        Test("missing declared file rejected",f=>{f.Sign(d=>((List<Dictionary<string,object?>>)d["files"]!).RemoveAt(0));Reject(()=>f.Load());f.AssertOld();});
        Test("unknown and duplicate signed root fields rejected",f=>{f.Sign(d=>d["extra"]=true);Reject(()=>f.Load());f.Sign();using var outer=JsonDocument.Parse(File.ReadAllText(Path.Combine(f.Candidate,BrokerUpgradeCandidate.EnvelopeName)));var body=Encoding.UTF8.GetString(Convert.FromBase64String(outer.RootElement.GetProperty("payload").GetString()!));f.SignRaw("{\"schema\":1,"+body[1..]);Reject(()=>f.Load());f.AssertOld();});
        Test("extra filesystem input rejected",f=>{File.WriteAllText(Path.Combine(f.Candidate,"other.exe"),"unknown");Reject(()=>f.Load());f.AssertOld();});
        Test("reparse candidate rejected",f=>{var path=Path.Combine(f.Candidate,"update-public.pem");File.Delete(path);File.CreateSymbolicLink(path,Path.Combine(f.Root,"maintenance/update-public.pem"));Reject(()=>f.Load());f.AssertOld();});
        Test("contract identity cannot differ despite valid signature",f=>{File.WriteAllText(Path.Combine(f.Candidate,"maintenance.json"),Contract(Payload with{Version="9.9.9"},2));f.Sign();Reject(()=>f.Load());f.AssertOld();});
        Test("duplicate case contract fields rejected",f=>{var path=Path.Combine(f.Candidate,"maintenance.json");var json=File.ReadAllText(path);File.WriteAllText(path,"{\"AppId\":\"foreign\","+json[1..]);f.Sign();Reject(()=>f.Load());f.AssertOld();});
        Test("bounded metadata rejected",f=>{File.WriteAllText(Path.Combine(f.Candidate,BrokerUpgradeCandidate.EnvelopeName),new string(' ',65537));Reject(()=>f.Load());f.AssertOld();});
        Test("transaction independently binds installed trust anchor",f=>{var c=f.Load();using var other=RSA.Create(2048);var path=Path.Combine(f.Root,"maintenance/update-public.pem");File.WriteAllText(path,other.ExportSubjectPublicKeyInfoPem());Reject(()=>f.Tx().Prepare(c));Require(File.ReadAllText(path)==other.ExportSubjectPublicKeyInfoPem());File.WriteAllText(path,f.Public);f.AssertOld();Require(f.Tx().Read()==null);});
        Test("transaction independently binds current payload",f=>{var c=f.Load();var path=Path.Combine(f.Root,"current/release.json");var changed=JsonSerializer.Serialize(Payload with{Version="9.9.9"},Camel);File.WriteAllText(path,changed);Reject(()=>f.Tx().Prepare(c));Require(File.ReadAllText(path)==changed);f.AssertOld();Require(f.Tx().Read()==null);});
        Test("unowned storage is never removed",f=>{var slot=Path.Combine(f.Root,"maintenance/broker-upgrade-v1");Directory.CreateDirectory(slot);File.WriteAllText(Path.Combine(slot,"foreign.txt"),"preserve");Reject(()=>f.Tx().Prepare(f.Load()));Require(File.ReadAllText(Path.Combine(slot,"foreign.txt"))=="preserve");f.AssertOld();});
        Test("file used as storage is never overwritten",f=>{var path=Path.Combine(f.Root,"maintenance/broker-upgrade-v1");File.WriteAllText(path,"preserve");Reject(()=>f.Tx().Prepare(f.Load()));Require(File.ReadAllText(path)=="preserve");f.AssertOld();});
        Test("unknown future installed reader is never downgraded",f=>{var path=Path.Combine(f.Root,"maintenance/maintenance.json");using var json=JsonDocument.Parse(File.ReadAllText(path));var fields=json.RootElement.EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value.Clone());fields["metadataReader"]=JsonSerializer.SerializeToElement(3);var changed=JsonSerializer.Serialize(fields);File.WriteAllText(path,changed);Reject(()=>f.Tx().Prepare(f.Load()));Require(File.ReadAllText(path)==changed&&f.Tx().Read()==null);f.AssertOld();});
        Test("oversized installed component rejected before snapshot",f=>{var path=Path.Combine(f.Root,"maintenance/StarTournamentMaintenance.exe");var original=File.ReadAllBytes(path);using(var file=new FileStream(path,FileMode.Open,FileAccess.Write))file.SetLength(128L*1024*1024+1);Reject(()=>f.Tx().Prepare(f.Load()));Require(new FileInfo(path).Length==128L*1024*1024+1&&f.Tx().Read()==null);File.WriteAllBytes(path,original);f.AssertOld();});
        Test("root entry replaced last and exact backup retained",f=>
        {
            var candidate=f.Load();var tx=f.Tx(p=>{if(p=="replaced-broker")Require(File.ReadAllText(Path.Combine(f.Root,"Star Tournament.exe"))=="old root entry");if(p=="replaced-contract")Require(File.ReadAllText(Path.Combine(f.Root,"Star Tournament.exe"))=="old root entry");});tx.Prepare(candidate);Require(tx.Read()?.Phase=="prepared");tx.Apply(candidate);f.AssertNew();Require(tx.Read()?.Phase=="committed");Require(File.Exists(Path.Combine(f.Root,"maintenance/broker-upgrade-v1/broker.previous")));tx.Cleanup(candidate);Require(tx.Read()==null);
        });
        foreach(var point in new[]{"capture-started","captured-broker","prepared"})
            Test("snapshot interruption recoverable: "+point,f=>{var tx=f.Tx(p=>{if(p==point)throw new IOException("injected");});Reject(()=>tx.Prepare(f.Load()));f.Tx().Recover();f.AssertOld();Require(f.Tx().Read()?.Phase=="restored");f.Tx().Cleanup();});
        foreach(var point in new[]{"replacing","replaced-broker","replaced-contract","replaced-entry"})
            Test("replacement exception restores all exact old files: "+point,f=>{var tx=f.Tx(p=>{if(p==point)throw new IOException("injected");});var c=f.Load();tx.Prepare(c);Reject(()=>tx.Apply(c));f.AssertOld();Require(tx.Read()?.Phase=="restored");});
        Test("committed callback failure never undoes component",f=>{var tx=f.Tx(p=>{if(p=="committed")throw new IOException("after commit");});var c=f.Load();tx.Prepare(c);Reject(()=>tx.Apply(c));Require(tx.Read()?.Phase=="committed");f.AssertNew();tx.Recover();f.AssertNew();tx.Cleanup(c);});
        Test("candidate disappearance mid-transaction still restores old files",f=>{var tx=f.Tx(p=>{if(p=="replaced-broker")Directory.Delete(f.Candidate,true);});var c=f.Load();tx.Prepare(c);Reject(()=>tx.Apply(c));f.AssertOld();Require(tx.Read()?.Phase=="restored");});
        Test("damaged snapshot rejected before any replacement",f=>{var tx=f.Tx();var c=f.Load();tx.Prepare(c);File.WriteAllText(Path.Combine(f.Root,"maintenance/broker-upgrade-v1/broker.previous"),"tampered");Reject(()=>tx.Apply(c));f.AssertOld();});
        Test("changed frozen candidate rejected before replacement",f=>{var tx=f.Tx();var c=f.Load();tx.Prepare(c);File.WriteAllText(Path.Combine(f.Candidate,"StarTournamentMaintenance.exe"),"changed");Reject(()=>tx.Apply(c));f.AssertOld();});
        Test("foreign pending root journal rejected",f=>{var tx=f.Tx();tx.Prepare(f.Load());var path=Path.Combine(f.Root,"maintenance/broker-upgrade-v1/state.json");var state=tx.Read()!;File.WriteAllText(path,JsonSerializer.Serialize(state with{RootId="foreign"}));Reject(()=>tx.Recover());f.AssertOld();});
        Test("recovery survives missing candidate and newer current metadata",f=>{var tx=f.Tx(p=>{if(p=="replaced-broker")throw new IOException("injected");});var c=f.Load();tx.Prepare(c);Reject(()=>tx.Apply(c));Directory.Delete(f.Candidate,true);var recovery=new WindowsBrokerUpgradeTransaction(f.Root,Payload with{Version="2.0.0"});recovery.Recover();Require(recovery.Read()?.Phase=="restored");f.AssertOld();});
        Test("unknown retained files prevent cleanup without deletion",f=>{var tx=f.Tx();var c=f.Load();tx.Prepare(c);tx.Apply(c);var path=Path.Combine(f.Root,"maintenance/broker-upgrade-v1/foreign.txt");File.WriteAllText(path,"preserve");Reject(()=>tx.Cleanup(c));Require(File.ReadAllText(path)=="preserve"&&tx.Read()?.Phase=="committed");f.AssertNew();});
        Test("unrelated next file never overwritten or deleted",f=>{var other=Path.Combine(f.Root,"Star Tournament.exe.broker-next-foreign");File.WriteAllText(other,"preserve");var tx=f.Tx();var c=f.Load();tx.Prepare(c);tx.Apply(c);tx.Cleanup(c);Require(File.ReadAllText(other)=="preserve");f.AssertNew();});
        Test("retained signed actor cleans committed state after payload advances",f=>{var c=f.Load();var tx=f.Tx();tx.Prepare(c);tx.Apply(c);var path=Path.Combine(f.Root,"maintenance/broker-upgrade-hosts",c.ManifestHash);Directory.CreateDirectory(path);foreach(var name in BrokerUpgradeCandidate.FileNames.Append(BrokerUpgradeCandidate.EnvelopeName))File.Copy(Path.Combine(f.Candidate,name),Path.Combine(path,name));var retained=BrokerUpgradeCandidate.Load(path,f.Public,Payload);File.WriteAllText(Path.Combine(f.Root,"current/release.json"),JsonSerializer.Serialize(Payload with{Version="2.0.0"},Camel));Directory.Delete(f.Candidate,true);var next=new WindowsBrokerUpgradeTransaction(f.Root,Payload with{Version="2.0.0"});next.CleanupRetained(retained);Require(next.Read()==null);f.AssertPayload();});
        Test("retained signed actor retries without current candidate",f=>{var c=f.Load();var path=Path.Combine(f.Root,"maintenance/broker-upgrade-hosts",c.ManifestHash);Directory.CreateDirectory(path);foreach(var name in BrokerUpgradeCandidate.FileNames.Append(BrokerUpgradeCandidate.EnvelopeName))File.Copy(Path.Combine(f.Candidate,name),Path.Combine(path,name));var retained=BrokerUpgradeCandidate.Load(path,f.Public,Payload);Directory.Delete(f.Candidate,true);var tx=f.Tx();tx.PrepareRetained(retained);tx.ApplyRetained(retained);retained.VerifyFile(Path.Combine(f.Root,"Star Tournament.exe"),"StarTournamentMaintenance.exe");Require(tx.Read()?.Phase=="committed");tx.CleanupRetained(retained);f.AssertPayload();});
        Test("pending recovery falls back to signed current actor if retained copy missing",f=>{var tx=f.Tx();tx.Prepare(f.Load());var selected=WindowsBrokerUpgradeStartup.SelectActor(f.Root,f.Public,Payload,tx.Read());Require(selected.DirectoryPath==f.Candidate);f.AssertOld();});
        Test("actor selection never admits invalid current or retained signature",f=>{var tx=f.Tx();tx.Prepare(f.Load());File.AppendAllText(Path.Combine(f.Candidate,"StarTournamentMaintenance.exe"),"bad");Reject(()=>WindowsBrokerUpgradeStartup.SelectActor(f.Root,f.Public,Payload,tx.Read()));f.AssertOld();});
        Test("ordinary cleanup still rejects legitimately restored playback bytes",f=>
        {var rollback=f.RollbackWithCommittedBroker();var tx=new WindowsBrokerUpgradeTransaction(f.Root,rollback.Previous);Reject(()=>tx.CleanupRetained(rollback.Actor));Require(tx.Read()?.Phase=="committed"&&rollback.Recovery.Read().Attempt?.Phase=="restored");});
        Test("payload cleanup settles committed component before deleting rollback proof",f=>
        {
            var rollback=f.RollbackWithCommittedBroker();rollback.Recovery.Cleanup(rollback.Previous.Version);
            Require(new WindowsBrokerUpgradeTransaction(f.Root,rollback.Previous).Read()==null&&rollback.Recovery.Read().Attempt==null);
            rollback.Actor.VerifyFile(Path.Combine(f.Root,"maintenance/StarTournamentMaintenance.exe"),"StarTournamentMaintenance.exe");
            Require(File.ReadAllText(Path.Combine(f.Root,"Star Tournament.exe"))=="old root entry"&&WindowsInstallation.MetadataReader(f.Root)==2);f.AssertPayload();
        });
        Test("payload cleanup retries after component journal already removed",f=>
        {var rollback=f.RollbackWithCommittedBroker();new WindowsBrokerUpgradeTransaction(f.Root,rollback.Previous).CleanupAfterPayloadRestore(rollback.Actor,rollback.Recovery);Require(rollback.Recovery.Read().Attempt?.Phase=="restored");rollback.Recovery.Cleanup(rollback.Previous.Version);Require(rollback.Recovery.Read().Attempt==null);f.AssertPayload();});
        foreach(var name in new[]{"maintenance/StarTournamentMaintenance.exe","Star Tournament.exe","maintenance/maintenance.json","Update.exe"})
        Test("rollback cleanup rejects changed installed file "+name,f=>
        {var rollback=f.RollbackWithCommittedBroker();var path=Path.Combine(f.Root,name);File.AppendAllText(path,"changed");Reject(()=>rollback.Recovery.Cleanup(rollback.Previous.Version));Require(new WindowsBrokerUpgradeTransaction(f.Root,rollback.Previous).Read()?.Phase=="committed"&&rollback.Recovery.Read().Attempt?.Phase=="restored");Require(File.ReadAllText(path).EndsWith("changed"));});
        Test("rollback cleanup preserves both journals if retained snapshot changed",f=>
        {var rollback=f.RollbackWithCommittedBroker();var id=rollback.Recovery.Read().Attempt!.Id;File.AppendAllText(Path.Combine(f.Root,"maintenance/recovery",id+".bootstrap","Star Tournament.exe"),"changed");Reject(()=>rollback.Recovery.Cleanup(rollback.Previous.Version));Require(new WindowsBrokerUpgradeTransaction(f.Root,rollback.Previous).Read()?.Phase=="committed"&&rollback.Recovery.Read().Attempt?.Phase=="restored");});
        Test("restored cleanup needs restored protected phase",f=>
        {var rollback=f.RollbackWithCommittedBroker();var path=Path.Combine(f.Root,"maintenance/recovery/state.json");File.WriteAllText(path,File.ReadAllText(path).Replace("\"restored\"","\"installed\""));Reject(()=>new WindowsBrokerUpgradeTransaction(f.Root,rollback.Previous).CleanupAfterPayloadRestore(rollback.Actor,rollback.Recovery));Require(new WindowsBrokerUpgradeTransaction(f.Root,rollback.Previous).Read()?.Phase=="committed");});
        Test("unknown component files preserve payload rollback proof",f=>
        {var rollback=f.RollbackWithCommittedBroker();var path=Path.Combine(f.Root,"maintenance/broker-upgrade-v1/foreign");File.WriteAllText(path,"preserve");Reject(()=>rollback.Recovery.Cleanup(rollback.Previous.Version));Require(rollback.Recovery.Read().Attempt?.Phase=="restored"&&File.ReadAllText(path)=="preserve");});
        Test("another root cannot authorize rollback cleanup",f=>
        {var rollback=f.RollbackWithCommittedBroker();using var other=new Fixture();var foreign=other.RollbackWithCommittedBroker();Reject(()=>new WindowsBrokerUpgradeTransaction(f.Root,rollback.Previous).CleanupAfterPayloadRestore(rollback.Actor,foreign.Recovery));Require(rollback.Recovery.Read().Attempt?.Phase=="restored"&&foreign.Recovery.Read().Attempt?.Phase=="restored"&&new WindowsBrokerUpgradeTransaction(f.Root,rollback.Previous).Read()?.Phase=="committed");});
        Test("rollback cleanup rejects changed current publication identity",f=>
        {var rollback=f.RollbackWithCommittedBroker();File.WriteAllText(Path.Combine(f.Root,"current/release.json"),JsonSerializer.Serialize(rollback.Previous with{PublicationDate="2026-10-16"},Camel));Reject(()=>rollback.Recovery.Cleanup(rollback.Previous.Version));Require(new WindowsBrokerUpgradeTransaction(f.Root,rollback.Previous).Read()?.Phase=="committed");});
        Test("invalid retained actor preserves both rollback journals",f=>
        {var rollback=f.RollbackWithCommittedBroker();File.AppendAllText(Path.Combine(rollback.Actor.DirectoryPath,"StarTournamentMaintenance.exe"),"changed");Reject(()=>rollback.Recovery.Cleanup(rollback.Previous.Version));Require(rollback.Recovery.Read().Attempt?.Phase=="restored"&&new WindowsBrokerUpgradeTransaction(f.Root,rollback.Previous).Read()?.Phase=="committed");});
        using(var f=new Fixture())
        {
            var rollback=f.RollbackWithCommittedBroker();var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false};
            if(Path.GetFileNameWithoutExtension(Environment.ProcessPath)=="dotnet")start.ArgumentList.Add(typeof(BrokerUpgradeChecks).Assembly.Location);
            start.ArgumentList.Add("--broker-rollback-cleanup-crash");start.ArgumentList.Add(f.Root);
            using var child=Process.Start(start)??throw new Exception("Child missing");await child.WaitForExitAsync();Require(child.ExitCode==73);
            Require(new WindowsBrokerUpgradeTransaction(f.Root,rollback.Previous).Read()==null&&rollback.Recovery.Read().Attempt?.Phase=="restored");
            rollback.Recovery.Cleanup(rollback.Previous.Version);Require(rollback.Recovery.Read().Attempt==null);f.AssertPayload();
            count++;Console.WriteLine("PASS real process death between component cleanup and payload cleanup");
        }
        foreach(var point in new[]{"intent-prepared","capture-started","prepared","replaced-broker","replaced-contract","replaced-entry","committed"})
        {
            using var f=new Fixture();var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false};if(Path.GetFileNameWithoutExtension(Environment.ProcessPath)=="dotnet")start.ArgumentList.Add(typeof(BrokerUpgradeChecks).Assembly.Location);
            foreach(var arg in new[]{"--broker-upgrade-crash",f.Root,point})start.ArgumentList.Add(arg);
            using var child=Process.Start(start)??throw new Exception("Child missing");await child.WaitForExitAsync();Require(child.ExitCode==73);
            var tx=f.Tx();if(point=="intent-prepared"){Require(tx.Read()==null);f.AssertOld();tx.Prepare(f.Load());tx.Apply(f.Load());f.AssertNew();}
            else if(point=="committed"){tx.Recover();Require(tx.Read()?.Phase=="committed");f.AssertNew();}
            else{Directory.Delete(f.Candidate,true);tx.Recover();Require(tx.Read()?.Phase=="restored");f.AssertOld();}
            count++;Console.WriteLine("PASS real process death at "+point);
        }
        Console.WriteLine($"Broker upgrade foundation: {count} passed; portable signed files/real process crashes, not native elevated startup or a usable broker");
    }
    public static void Crash(string root,string point)
    {
        var candidate=BrokerUpgradeCandidate.Load(Path.Combine(root,"current/broker-upgrade"),File.ReadAllText(Path.Combine(root,"maintenance/update-public.pem")),Payload);
        var tx=new WindowsBrokerUpgradeTransaction(root,Payload,checkpoint:p=>{if(p==point)Environment.Exit(73);});tx.Prepare(candidate);tx.Apply(candidate);throw new Exception("Missing crash checkpoint");
    }
    public static void CrashRollbackCleanup(string root)
    {
        var previous=JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(Path.Combine(root,"current/release.json")),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
        var tx=new WindowsBrokerUpgradeTransaction(root,previous);var state=tx.Read()!;
        var actor=WindowsBrokerUpgradeStartup.Retained(root,state.ManifestHash,File.ReadAllText(Path.Combine(root,"maintenance/update-public.pem")),previous);
        tx.CleanupAfterPayloadRestore(actor,new WindowsRecoveryStore(root,previous));Environment.Exit(73);
    }
}
