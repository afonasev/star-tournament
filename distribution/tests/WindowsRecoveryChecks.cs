using System.Text.Json;
using StarTournament.Distribution;

static class WindowsRecoveryChecks
{
    static readonly LauncherConfig Config=new("tech.fixture.windows","win","x64","1.0.0","2026-10-16",AuthenticatedSource.ProductionBase,"game/Player.exe","production");
    static int passed;
    public static void Run()
    {
        var root=Path.Combine(Directory.GetCurrentDirectory(),".local","windows-recovery-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            void Test(string name,Action<string,WindowsRecoveryStore> action)
            {
                var install=Path.Combine(root,passed.ToString());Directory.CreateDirectory(Path.Combine(install,"current","game"));
                WriteConfig(install,"1.0.0");File.WriteAllText(Path.Combine(install,"current","game","Player.exe"),"old game");
                File.WriteAllText(Path.Combine(install,"Update.exe"),"old updater");File.WriteAllText(Path.Combine(install,"Star Tournament.exe"),"old stub");File.WriteAllBytes(Path.Combine(install,".portable"),[]);
                action(install,new(install,Config));passed++;Console.WriteLine("PASS "+name);
            }
            Test("complete protected snapshot precedes native callback",(install,store)=>
            {
                var attempt=store.Prepare(Config,"1.1.0",false);Require(store.Read().Attempt?.Phase=="prepared"&&attempt.Bootstrap.Length==3);
                store.Apply(()=>{Require(store.Read().Attempt?.Phase=="prepared");Changed(install);});
                Require(store.Read().Attempt?.Phase=="installed"&&File.ReadAllText(Path.Combine(install,"Update.exe"))=="new updater");
                Require(Directory.Exists(Path.Combine(install,"maintenance","recovery",attempt.Id)));
            });
            Test("native partial failure restores current and every bootstrap file",(install,store)=>
            {
                store.Prepare(Config,"1.1.0",false);
                Reject(()=>store.Apply(()=>{Changed(install);Directory.Delete(Path.Combine(install,"current"),true);throw new IOException("native failed");}));
                Old(install);Require(store.Read().Attempt?.Phase=="restored"&&store.Read().Blocked.Contains("1.1.0"));
            });
            Test("bad installed identity triggers restore",(install,store)=>
            {store.Prepare(Config,"1.1.0",false);Reject(()=>store.Apply(()=>{Changed(install);WriteConfig(install,"9.9.9");}));Old(install);});
            Test("manual restoration does not require replacement config or executable",(install,store)=>
            {
                store.Prepare(Config,"1.1.0",false);Changed(install);File.Delete(Path.Combine(install,"current","release.json"));
                new WindowsRecoveryStore(install,Config).Restore();Old(install);
            });
            Test("unknown bootstrap bytes are rejected before replacing current",(install,store)=>
            {
                var attempt=store.Prepare(Config,"1.1.0",false);Changed(install);
                File.WriteAllText(Path.Combine(install,"maintenance","recovery",attempt.Id+".bootstrap","Update.exe"),"tampered");
                Reject(()=>store.Restore());Require(File.ReadAllText(Path.Combine(install,"current","game","Player.exe"))=="new game");
            });
            Test("payload corruption does not activate unverified backup",(install,store)=>
            {
                var attempt=store.Prepare(Config,"1.1.0",false);Changed(install);
                File.WriteAllText(Path.Combine(install,"maintenance","recovery",attempt.Id,"payload","game","Player.exe"),"tampered");
                Reject(()=>store.Restore());Require(File.ReadAllText(Path.Combine(install,"Update.exe"))=="new updater");
            });
            Test("unconfirmed installed release cannot authorize cleanup",(install,store)=>
            {store.Prepare(Config,"1.1.0",false);store.Apply(()=>Changed(install));Reject(()=>store.Cleanup("1.0.0"));Require(store.Read().Attempt?.Phase=="installed");});
            Test("healthy target attestation permits cleanup and next transaction",(install,store)=>
            {store.Prepare(Config,"1.1.0",false);store.Apply(()=>Changed(install));MarkReady(install,store);store.Cleanup("1.1.0");Require(store.Read().Attempt==null);store.Prepare(Config with{Version="1.1.0"},"1.2.0",false);});
            Test("restored target remains blocked until explicit retry",(install,store)=>
            {store.Prepare(Config,"1.1.0",false);Changed(install);store.Restore();store.Cleanup("1.0.0");Reject(()=>store.Prepare(Config,"1.1.0",false));store.Prepare(Config,"1.1.0",true);});
            Test("new target is eligible after cleanup despite old failure",(install,store)=>
            {store.Prepare(Config,"1.1.0",false);Changed(install);store.Restore();store.Cleanup("1.0.0");store.Prepare(Config,"1.2.0",false);});
            Test("successful retry retires the historical failure block",(install,store)=>
            {store.Prepare(Config,"1.1.0",false);Changed(install);store.Restore();store.Cleanup("1.0.0");store.Prepare(Config,"1.1.0",true);store.Apply(()=>Changed(install));MarkReady(install,store);store.Cleanup("1.1.0");Require(!store.Read().Blocked.Contains("1.1.0"));});
            Test("pending transaction cannot be overwritten",(install,store)=>
            {var attempt=store.Prepare(Config,"1.1.0",false);Reject(()=>store.Prepare(Config,"1.2.0",false));Require(store.Read().Attempt?.Id==attempt.Id);});
            Test("capturing crash cannot run updater and preserves original",(install,store)=>
            {
                File.Delete(Path.Combine(install,"Update.exe"));Reject(()=>store.Prepare(Config,"1.1.0",false));Require(store.Read().Attempt?.Phase=="capturing");
                bool invoked=false;Reject(()=>store.Apply(()=>invoked=true));Require(!invoked);
                // Source file was removed by fixture before capture; no root mutation by broker.
                File.WriteAllText(Path.Combine(install,"Update.exe"),"old updater");store.Restore();Old(install);store.Cleanup("1.0.0");
                Require(store.Read().Attempt==null);
            });
            Test("bootstrap allow-list rejects metadata path traversal",(install,store)=>
            {
                store.Prepare(Config,"1.1.0",false);var state=store.Read();var attempt=state.Attempt!;
                var list=attempt.Bootstrap.ToArray();list[0]=list[0] with{Name="../../outside"};
                File.WriteAllText(State(install),JsonSerializer.Serialize(state with{Attempt=attempt with{Bootstrap=list}}));Reject(()=>store.Read());
            });
            Test("foreign root and identity state fail closed",(install,store)=>
            {
                store.Prepare(Config,"1.1.0",false);var state=store.Read();
                foreach(var value in new[]{state with{RootId=new string('a',64)},state with{AppId="foreign"},state with{Schema=2},state with{Blocked=null!}})
                {File.WriteAllText(State(install),JsonSerializer.Serialize(value));Reject(()=>store.Read());}
            });
            Test("malformed oversized state never becomes empty",(install,store)=>
            {
                store.Prepare(Config,"1.1.0",false);
                foreach(var value in new[]{"","null","{}",new string('x',131073)}){File.WriteAllText(State(install),value);Reject(()=>store.Read());Reject(()=>store.Prepare(Config,"1.1.0",false));}
            });
            Test("downgrade same-version or foreign-channel target is rejected",(_,store)=>
            {foreach(var version in new[]{"1.0.0","0.9.0","1.1.0-test.1","garbage"})Reject(()=>store.Prepare(Config,version,false));Require(store.Read().Attempt==null);});
            Test("resume cleanup after snapshot disappeared before state acknowledgement",(install,store)=>
            {
                var attempt=store.Prepare(Config,"1.1.0",false);store.Apply(()=>Changed(install));
                MarkReady(install,store);
                new PayloadRecovery(Path.Combine(install,"maintenance","recovery"),new(Config.AppId,"win","x64","production")).Remove(attempt.Id,Path.Combine(install,"current"));
                store.Cleanup("1.1.0");Require(store.Read().Attempt==null);
            });
            Test("repeat restored operation remains idempotent",(install,store)=>
            {store.Prepare(Config,"1.1.0",false);Changed(install);var first=store.Restore();var second=new WindowsRecoveryStore(install,Config).Restore();Require(JsonSerializer.Serialize(first)==JsonSerializer.Serialize(second));Old(install);});
            Test("atomic protected file creator is used for metadata and bootstrap",(install,_)=>
            {
                var created=new List<string>();
                var store=new WindowsRecoveryStore(install,Config,p=>Directory.CreateDirectory(p),p=>{created.Add(p);return new FileStream(p,FileMode.CreateNew,FileAccess.Write,FileShare.None);});
                store.Prepare(Config,"1.1.0",false);
                Require(created.Any(p=>p.Contains("state.json.next-"))&&created.Any(p=>p.EndsWith(".bootstrap"+Path.DirectorySeparatorChar+"Update.exe")));
            });
            Test("saturated failure history denies a new attempt before mutation",(install,store)=>
            {
                var state=store.Read() with{Blocked=Enumerable.Range(1,256).Select(i=>"1.0."+i).ToArray()};
                File.WriteAllText(State(install),JsonSerializer.Serialize(state));
                Reject(()=>store.Prepare(Config,"1.1.0",false));Require(store.Read().Attempt==null);Old(install);
            });
            Test("frozen consent hash rejects replaced signed-metadata bytes",(_,store)=>
            {
                const string envelope="frozen signed envelope fixture";
                var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(envelope)));
                ProtectedPackage.RequireEnvelopeHash(envelope,hash);
                foreach(var value in new[]{envelope+"changed","another"})
                    try{ProtectedPackage.RequireEnvelopeHash(value,hash);throw new Exception("Frozen target changed");}catch(System.Security.Cryptography.CryptographicException){}
            });
            Test("interrupted bootstrap restore is completed on next recovery",(install,store)=>
            {
                store.Prepare(Config,"1.1.0",false);Changed(install);
                var failing=new WindowsRecoveryStore(install,Config,p=>Directory.CreateDirectory(p),p=>
                {if(p.Contains("Star Tournament.exe.restore-"))throw new IOException("fixture interrupted restore");return new FileStream(p,FileMode.CreateNew,FileAccess.Write,FileShare.None);});
                Reject(()=>failing.Restore());Require(store.Read().Attempt?.Phase=="restoring");
                Require(File.ReadAllText(Path.Combine(install,"current","game","Player.exe"))=="old game"&&File.ReadAllText(Path.Combine(install,"Update.exe"))=="new updater");
                Require(WindowsInstallation.RecoveryNeedsRepair(install));
                new WindowsRecoveryStore(install,Config).Restore();Old(install);Require(!WindowsInstallation.RecoveryNeedsRepair(install));
            });
            Test("launcher blocks unfinished but allows settled protected status",(install,store)=>
            {
                Require(!WindowsInstallation.RecoveryNeedsRepair(install));store.Prepare(Config,"1.1.0",false);Require(WindowsInstallation.RecoveryNeedsRepair(install));
                store.Apply(()=>Changed(install));Require(!WindowsInstallation.RecoveryNeedsRepair(install));
            });
            Test("installed cleanup needs both current attestation and startup latch",(install,store)=>
            {store.Prepare(Config,"1.1.0",false);store.Apply(()=>Changed(install));Reject(()=>store.Cleanup("1.1.0"));MarkReady(install,store);store.Cleanup("1.1.0");});
            Test("confirmed latch forbids later rollback across users",(install,store)=>
            {store.Prepare(Config,"1.1.0",false);store.Apply(()=>Changed(install));MarkReady(install,store);Reject(()=>new WindowsRecoveryStore(install,Config).Restore());Require(store.Read().Attempt?.Phase=="installed");});
            Test("manual startup recovery handles installed payload with no launcher config",(install,store)=>
            {store.Prepare(Config,"1.1.0",false);store.Apply(()=>Changed(install));File.Delete(Path.Combine(install,"current","release.json"));store.Restore();Old(install);});
            Test("lost startup latch never silently permits a downgrade",(install,store)=>
            {var attempt=store.Prepare(Config,"1.1.0",false);store.Apply(()=>Changed(install));File.Delete(Path.Combine(install,"maintenance","recovery",attempt.Id+".ready"));Reject(()=>store.Restore());});
            Test("failed target block is visible to every normal-user launcher",(install,store)=>
            {store.Prepare(Config,"1.1.0",false);Changed(install);store.Restore();Require(WindowsInstallation.IsBlocked(install,"1.1.0")&&!WindowsInstallation.IsBlocked(install,"1.2.0"));store.Cleanup("1.0.0");Require(WindowsInstallation.IsBlocked(install,"1.1.0"));});
            Test("recovery freezes exact transaction across elevation",(install,store)=>
            {var attempt=store.Prepare(Config,"1.1.0",false);Changed(install);Reject(()=>store.Restore(new string('a',32)));Require(store.Read().Attempt?.Id==attempt.Id&&File.ReadAllText(Path.Combine(install,"Update.exe"))=="new updater");store.Restore(attempt.Id);Old(install);});
            Test("deferred startup binds the protected transaction and target",(install,store)=>
            {Require(WindowsInstallation.PendingStartup(install,"1.0.0")==null);var a=store.Prepare(Config,"1.1.0",false);store.Apply(()=>Changed(install));Require(WindowsInstallation.PendingStartup(install,"1.1.0")==a.Id);Require(!WindowsInstallation.StartupConfirmed(install,a.Id));Reject(()=>WindowsInstallation.PendingStartup(install,"9.9.9"));});
            Test("confirmed transaction needs no new watchdog",(install,store)=>
            {var a=store.Prepare(Config,"1.1.0",false);store.Apply(()=>Changed(install));MarkReady(install,store);Require(WindowsInstallation.PendingStartup(install,"1.1.0")==null&&WindowsInstallation.StartupConfirmed(install,a.Id));});
            Test("missing latch is uncertainty not permission for startup rollback",(install,store)=>
            {var a=store.Prepare(Config,"1.1.0",false);store.Apply(()=>Changed(install));File.Delete(Path.Combine(install,"maintenance","recovery",a.Id+".ready"));Reject(()=>WindowsInstallation.StartupConfirmed(install,a.Id));Reject(()=>WindowsInstallation.PendingStartup(install,"1.1.0"));});
            Test("watchdog cannot observe another protected attempt",(install,store)=>
            {store.Prepare(Config,"1.1.0",false);store.Apply(()=>Changed(install));Reject(()=>WindowsInstallation.StartupConfirmed(install,new string('e',32)));Reject(()=>WindowsInstallation.StartupConfirmed(install,"../escape"));});
            Test("pending installer blocks only activation including trailing-root form",(install,store)=>
            {Require(!WindowsInstallation.InstallerPending(install));Directory.CreateDirectory(install+".installer");File.WriteAllText(Path.Combine(install+".installer","state.json"),"{}");Require(WindowsInstallation.InstallerPending(install)&&WindowsInstallation.InstallerPending(install+Path.DirectorySeparatorChar));Require(!WindowsInstallation.RecoveryNeedsRepair(install));});
            Test("rollback keeps retained reader2 with previous playback protocol1",(install,store)=>
            {
                var maintenance=Path.Combine(install,"maintenance");Directory.CreateDirectory(maintenance);
                File.WriteAllText(Path.Combine(maintenance,"maintenance.json"),"{\"protocol\":1}");
                var broker=Path.Combine(maintenance,"StarTournamentMaintenance.exe");File.WriteAllText(broker,"retained new broker fixture");
                var attempt=store.Prepare(Config,"1.1.0",false);Require(attempt.PreviousProtocol==1);
                store.Apply(()=>Changed(install));store.Restore();Old(install);
                using(var restored=JsonDocument.Parse(File.ReadAllText(Path.Combine(maintenance,"maintenance.json"))))
                {Require(restored.RootElement.GetProperty("protocol").GetInt32()==1&&restored.RootElement.GetProperty("metadataReader").GetInt32()==2);}
                Require(WindowsInstallation.MetadataReader(install)==2&&File.ReadAllText(broker)=="retained new broker fixture");
                store.Cleanup("1.0.0");Require(WindowsInstallation.MetadataReader(install)==2);
            });
            Console.WriteLine($"Windows recovery store: {passed} passed (portable filesystem contracts; Windows ACL/UAC NotRun)");
        }
        finally{Directory.Delete(root,true);}
    }
    static void MarkReady(string root,WindowsRecoveryStore store)=>File.AppendAllText(Path.Combine(root,"maintenance","recovery",store.Read().Attempt!.Id+".ready"),"1");
    static string State(string root)=>Path.Combine(root,"maintenance","recovery","state.json");
    static void WriteConfig(string root,string version)=>File.WriteAllText(Path.Combine(root,"current","release.json"),JsonSerializer.Serialize(Config with{Version=version},new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase}));
    static void Changed(string root)
    {WriteConfig(root,"1.1.0");File.WriteAllText(Path.Combine(root,"current","game","Player.exe"),"new game");File.WriteAllText(Path.Combine(root,"Update.exe"),"new updater");File.WriteAllText(Path.Combine(root,"Star Tournament.exe"),"new stub");File.WriteAllText(Path.Combine(root,".portable"),"changed marker");}
    static void Old(string root)
    {Require(File.ReadAllText(Path.Combine(root,"current","game","Player.exe"))=="old game"&&File.ReadAllText(Path.Combine(root,"Update.exe"))=="old updater"&&File.ReadAllText(Path.Combine(root,"Star Tournament.exe"))=="old stub"&&new FileInfo(Path.Combine(root,".portable")).Length==0);}
    static void Require(bool value){if(!value)throw new Exception("Windows recovery store contract failed");}
    static void Reject(Action action){try{action();}catch(Exception ex)when(ex is IOException or ArgumentException){return;}throw new Exception("Unsafe protected transaction was admitted");}
}
