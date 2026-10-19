using System.Text.Json;
using System.Diagnostics;
using StarTournament.Distribution;

static class WindowsInstallerTransactionChecks
{
    static readonly LauncherConfig Candidate=new("tech.fixture.win","win","x64","1.1.0","2026-10-16",AuthenticatedSource.ProductionBase,"game/Player.exe","production");
    static void Require(bool value){if(!value)throw new Exception("Installer transaction contract failed");}
    static void Reject(Action action){try{action();}catch(IOException){return;}throw new Exception("Unsafe installer transaction allowed");}
    public static void Crash(string root,string point,string mode)
    {
        var transaction=new WindowsInstallerTransaction(root,Candidate,checkpoint:p=>{if(p==point)Environment.Exit(53);});
        if(mode=="commit")transaction.Commit();else if(mode=="recover")transaction.Recover();else throw new ArgumentException("Fixture mode");
        throw new Exception("Crash fixture did not reach checkpoint");
    }
    public static void Run()
    {
        int passed=0;var home=Path.Combine(Directory.GetCurrentDirectory(),".local","installer-transaction-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(home);
        try
        {
            void Test(string name,Action<string> run){var root=Path.Combine(home,(passed+1).ToString(),"installed");Directory.CreateDirectory(Path.GetDirectoryName(root)!);run(root);passed++;Console.WriteLine("PASS "+name);}
            void Old(string root,string version="1.0.0"){Stage(root,version);File.Delete(Path.Combine(root,"maintenance/installing.flag"));File.WriteAllText(Path.Combine(root,"current/old-game"),"old exact bytes");}
            void AssertOld(string root){Require(File.ReadAllText(Path.Combine(root,"current/old-game"))=="old exact bytes");using var data=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"current/release.json")));Require(data.RootElement.GetProperty("version").GetString()=="1.0.0");}
            Test("extraction and cancellation never touch existing files",root=>{Old(root);var t=new WindowsInstallerTransaction(root,Candidate);t.Begin("1.0.0");File.WriteAllText(Path.Combine(t.Stage,"partial"),"partial");AssertOld(root);t.Recover();AssertOld(root);t.ClearSettled();Require(!Directory.Exists(root+".installer"));});
            Test("same-version repair replaces exact complete tree without stale files",root=>{Old(root,"1.1.0");var t=new WindowsInstallerTransaction(root,Candidate);t.Begin("1.1.0");Require(WindowsInstallation.InstallerAllowsLaunch(root));Stage(t.Stage);t.Commit();Require(!WindowsInstallation.InstallerAllowsLaunch(root));Require(!File.Exists(Path.Combine(root,"current/old-game")));Require(t.Read()?.Phase=="installed");Require(File.Exists(Path.Combine(root+".installer/previous/current/old-game")));t.FinalizeInstall();Require(WindowsInstallation.InstallerAllowsLaunch(root));Require(t.Read()?.Phase=="committed"&&!File.Exists(Path.Combine(root,"maintenance/installing.flag")));t.ClearSettled();Require(!Directory.Exists(root+".installer"));});
            foreach(var point in new[]{"prepared","old-root-moved","saved","new-root-moved","installed"})
                Test("interrupted "+point+" restores previous root",root=>{Old(root);var t=new WindowsInstallerTransaction(root,Candidate,checkpoint:p=>{if(p==point)throw new IOException("crash");});t.Begin("1.0.0");Stage(t.Stage);Reject(t.Commit);var reopened=new WindowsInstallerTransaction(root,Candidate);reopened.Recover();AssertOld(root);reopened.ClearSettled();Require(!Directory.Exists(root+".installer"));});
            foreach(var point in new[]{"failed-root-retained","previous-restored","restored"})
                Test("interrupted rollback "+point+" is restart-safe",root=>{Old(root);var t=new WindowsInstallerTransaction(root,Candidate);t.Begin("1.0.0");Stage(t.Stage);t.Commit();var interrupted=new WindowsInstallerTransaction(root,Candidate,checkpoint:p=>{if(p==point)throw new IOException("crash");});Reject(interrupted.Recover);var reopened=new WindowsInstallerTransaction(root,Candidate);reopened.Recover();AssertOld(root);reopened.ClearSettled();});
            Test("interrupted committed cleanup never downgrades new tree",root=>{Old(root);var t=new WindowsInstallerTransaction(root,Candidate,checkpoint:p=>{if(p=="committed")throw new IOException("crash");});t.Begin("1.0.0");Stage(t.Stage);t.Commit();Reject(t.FinalizeInstall);var reopened=new WindowsInstallerTransaction(root,Candidate);reopened.Recover();Require(File.Exists(Path.Combine(root,"current/release.json"))&&!File.Exists(Path.Combine(root,"current/old-game")));reopened.ClearSettled();});
            Test("fresh failed install retains no public partial entry",root=>{var t=new WindowsInstallerTransaction(root,Candidate);t.Begin(null);Stage(t.Stage);t.Commit();t.Recover();Require(!Directory.Exists(root));t.ClearSettled();});
            Test("empty precreated destination supports a fresh install",root=>{Directory.CreateDirectory(root);var t=new WindowsInstallerTransaction(root,Candidate);t.Begin(null);Stage(t.Stage);t.Commit();t.FinalizeInstall();t.ClearSettled();Require(File.Exists(Path.Combine(root,"Star Tournament.exe")));});
            Test("incomplete stage cannot replace previous files",root=>{Old(root);var t=new WindowsInstallerTransaction(root,Candidate);t.Begin("1.0.0");Stage(t.Stage);File.Delete(Path.Combine(t.Stage,"current/game/Player.exe"));Reject(t.Commit);AssertOld(root);t.Recover();});
            Test("foreign channel cannot replace previous installation",root=>{Old(root);var t=new WindowsInstallerTransaction(root,Candidate);t.Begin("1.0.0");Stage(t.Stage);WriteConfig(t.Stage,Candidate with{UpdateChannel="test"});Reject(t.Commit);AssertOld(root);t.Recover();});
            Test("root stable entry must be exact maintenance bytes",root=>{Old(root);var t=new WindowsInstallerTransaction(root,Candidate);t.Begin("1.0.0");Stage(t.Stage);File.WriteAllText(Path.Combine(t.Stage,"Star Tournament.exe"),"generated stub");Reject(t.Commit);AssertOld(root);t.Recover();});
            Test("candidate symlink escape cannot replace root",root=>{Old(root);var t=new WindowsInstallerTransaction(root,Candidate);t.Begin("1.0.0");Stage(t.Stage);File.CreateSymbolicLink(Path.Combine(t.Stage,"escape"),Path.Combine(root,"current/old-game"));Reject(t.Commit);AssertOld(root);File.Delete(Path.Combine(t.Stage,"escape"));t.Recover();});
            Test("foreign journal is rejected before mutation",root=>{Old(root);var t=new WindowsInstallerTransaction(root,Candidate);t.Begin("1.0.0");var path=Path.Combine(root+".installer/state.json");File.WriteAllText(path,File.ReadAllText(path).Replace(StartupRecovery.InstallationId(root),new string('b',64)));Reject(t.Recover);AssertOld(root);});
            Test("unknown slot is preserved",root=>{Old(root);Directory.CreateDirectory(root+".installer");File.WriteAllText(Path.Combine(root+".installer/foreign"),"keep");var t=new WindowsInstallerTransaction(root,Candidate);Reject(()=>t.Begin("1.0.0"));Require(File.ReadAllText(Path.Combine(root+".installer/foreign"))=="keep");AssertOld(root);});
            Test("new attempt first recovers previous interrupted extraction",root=>{Old(root);var t=new WindowsInstallerTransaction(root,Candidate);t.Begin("1.0.0");File.WriteAllText(Path.Combine(t.Stage,"partial"),"old partial");new WindowsInstallerTransaction(root,Candidate).Begin("1.0.0");AssertOld(root);Require(!File.Exists(Path.Combine(t.Stage,"partial")));});
            Test("older installer cannot downgrade before staging",root=>{Old(root);var t=new WindowsInstallerTransaction(root,Candidate);Reject(()=>t.Begin("2.0.0"));AssertOld(root);});
            Test("cross-channel installer cannot overwrite existing release",root=>{Old(root);var t=new WindowsInstallerTransaction(root,Candidate);Reject(()=>t.Begin("1.0.0-test.1"));AssertOld(root);});
            Test("unknown fresh root appearing during extraction is preserved",root=>{var t=new WindowsInstallerTransaction(root,Candidate);t.Begin(null);Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"foreign"),"keep");Reject(t.Recover);Reject(t.Recover);Require(File.ReadAllText(Path.Combine(root,"foreign"))=="keep");});
            Test("native metadata recovers a damaged release JSON identity",root=>{Stage(root);File.WriteAllText(Path.Combine(root,"current/release.json"),"corrupt");var plan=WindowsInstallerPlan.Inspect(root,Candidate);Require(plan.Mode=="repair"&&plan.InstalledVersion==Candidate.Version);});
            Test("newer installer identifies upgrade and older one refuses",root=>{Stage(root);Require(WindowsInstallerPlan.Inspect(root,Candidate with{Version="1.2.0"}).Mode=="upgrade");Reject(()=>WindowsInstallerPlan.Inspect(root,Candidate with{Version="1.0.0"}));});
            Test("inconsistent version records cannot become downgrade authority",root=>{Stage(root);WriteConfig(root,Candidate with{Version="1.0.0"});Reject(()=>WindowsInstallerPlan.Inspect(root,Candidate));});
            Test("native XML cannot load external entities",root=>{Stage(root);File.Delete(Path.Combine(root,"current/release.json"));File.WriteAllText(Path.Combine(root,"current/sq.version"),"<!DOCTYPE x [<!ENTITY e SYSTEM 'file:///etc/passwd'>]><package>&e;</package>");try{WindowsInstallerPlan.Inspect(root,Candidate);throw new Exception("XXE accepted");}catch(System.Xml.XmlException){}});
            Test("committed cleanup retains previous if installed candidate disappeared",root=>{Old(root);var t=new WindowsInstallerTransaction(root,Candidate,checkpoint:p=>{if(p=="committed")throw new IOException("crash");});t.Begin("1.0.0");Stage(t.Stage);t.Commit();Reject(t.FinalizeInstall);Directory.Delete(root,true);Reject(()=>new WindowsInstallerTransaction(root,Candidate).Recover());Require(File.Exists(Path.Combine(root+".installer/previous/current/old-game")));});
            Test("later installer can clean committed previous target before staging its own",root=>{Old(root);var t=new WindowsInstallerTransaction(root,Candidate);t.Begin("1.0.0");Stage(t.Stage);t.Commit();t.FinalizeInstall();new WindowsInstallerTransaction(root,Candidate with{Version="1.2.0"}).Begin(Candidate.Version);Require(File.Exists(Path.Combine(root,"current/release.json")));});
            Test("broken candidate native metadata cannot pass complete composition",root=>{Old(root);var t=new WindowsInstallerTransaction(root,Candidate);t.Begin("1.0.0");Stage(t.Stage);File.WriteAllText(Path.Combine(t.Stage,"current/sq.version"),"<broken");try{t.Commit();throw new Exception("Broken metadata accepted");}catch(System.Xml.XmlException){}AssertOld(root);});
            foreach(var point in new[]{"old-root-moved","saved","new-root-moved","installed","failed-root-retained","previous-restored"})
                Test("abrupt child exit at "+point+" recovers durable files",root=>
                {
                    Old(root);var transaction=new WindowsInstallerTransaction(root,Candidate);transaction.Begin("1.0.0");Stage(transaction.Stage);
                    var mode=point is "failed-root-retained" or "previous-restored"?"recover":"commit";if(mode=="recover")transaction.Commit();
                    var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false};
                    foreach(var argument in new[]{"--installer-crash-fixture",root,point,mode})start.ArgumentList.Add(argument);
                    using var child=Process.Start(start)??throw new IOException("Fixture did not start");
                    try{Require(child.WaitForExit(10000)&&child.ExitCode==53);}
                    finally{if(!child.HasExited){child.Kill();child.WaitForExit();}}
                    var reopened=new WindowsInstallerTransaction(root,Candidate);reopened.Recover();AssertOld(root);reopened.ClearSettled();Require(!Directory.Exists(root+".installer"));
                });
            Test("trailing separator cannot put installer slot inside original root",root=>{Old(root);var t=new WindowsInstallerTransaction(root+Path.DirectorySeparatorChar,Candidate);Require(t.Stage==Path.Combine(root+".installer","stage"));t.Begin("1.0.0");t.Recover();AssertOld(root);t.ClearSettled();});
            Test("an external root change cannot advance the prepared journal",root=>{Old(root);var t=new WindowsInstallerTransaction(root,Candidate);t.Begin("1.0.0");Stage(t.Stage);Directory.Delete(root,true);Reject(t.Commit);Require(t.Read()!.Phase=="extracting"&&Directory.Exists(t.Stage));});
            Test("settled failed-target history survives installer replacement at the same root",root=>
            {
                Old(root);Directory.CreateDirectory(Path.Combine(root,"maintenance/recovery"));
                File.WriteAllText(Path.Combine(root,"maintenance/recovery/state.json"),JsonSerializer.Serialize(new WindowsRecoveryState(1,StartupRecovery.InstallationId(root),Candidate.AppId,["1.3.0"],null)));
                var t=new WindowsInstallerTransaction(root,Candidate);t.Begin("1.0.0");Stage(t.Stage);t.Commit();Require(new WindowsRecoveryStore(root,Candidate).Read().Blocked.SequenceEqual(new[]{"1.3.0"}));t.FinalizeInstall();t.ClearSettled();Require(WindowsInstallation.IsBlocked(root,"1.3.0"));
            });
            Test("retained startup backup cannot be silently discarded by the installer",root=>
            {
                Old(root);Directory.CreateDirectory(Path.Combine(root,"maintenance/recovery"));
                var attempt=new WindowsRecoveryAttempt(new string('a',32),Candidate with{Version="1.0.0"},Candidate.Version,"capturing",[]);
                File.WriteAllText(Path.Combine(root,"maintenance/recovery/state.json"),JsonSerializer.Serialize(new WindowsRecoveryState(1,StartupRecovery.InstallationId(root),Candidate.AppId,[],attempt)));
                var t=new WindowsInstallerTransaction(root,Candidate);t.Begin("1.0.0");Stage(t.Stage);Reject(t.Commit);AssertOld(root);Require(t.Read()!.Phase=="extracting");t.Recover();AssertOld(root);
            });
            Test("schema2 installer candidate requires capable broker before replacing current",root=>
            {
                Old(root);var requirements=new ReleaseRequirements("10.0.19043",2,ReleaseCompatibility.PayloadFormat,ReleaseCompatibility.DataPolicy);
                var candidate=Candidate with{Compatibility=requirements};var t=new WindowsInstallerTransaction(root,candidate);t.Begin("1.0.0");Stage(t.Stage);WriteConfig(t.Stage,candidate);
                Reject(t.Commit);AssertOld(root);Require(t.Read()!.Phase=="extracting");
                var path=Path.Combine(t.Stage,"maintenance/maintenance.json");using var document=JsonDocument.Parse(File.ReadAllText(path));var fields=document.RootElement.EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value.Clone());fields["metadataReader"]=JsonSerializer.SerializeToElement(2);File.WriteAllText(path,JsonSerializer.Serialize(fields));
                t.Commit();Require(WindowsInstallation.MetadataReader(root)==2);t.FinalizeInstall();t.ClearSettled();
            });
            Test("schema2 installer can finish cleanup of a committed legacy target",root=>
            {
                Old(root);var legacy=new WindowsInstallerTransaction(root,Candidate);legacy.Begin("1.0.0");Stage(legacy.Stage);legacy.Commit();legacy.FinalizeInstall();
                var candidate=Candidate with{Version="1.2.0",Compatibility=new ReleaseRequirements("10.0.19043",2,ReleaseCompatibility.PayloadFormat,ReleaseCompatibility.DataPolicy)};
                var next=new WindowsInstallerTransaction(root,candidate);next.Begin(Candidate.Version);Require(next.Read()!.Target=="1.2.0"&&File.Exists(Path.Combine(root,"current/release.json")));next.Recover();next.ClearSettled();
            });
            Console.WriteLine($"Windows installer transaction: {passed} passed (portable filesystem; Windows ACL/NSIS runtime NotRun)");
        }
        finally{Directory.Delete(home,true);}
    }
    static void Stage(string path,string? version=null)
    {
        var config=Candidate with{Version=version??Candidate.Version};
        foreach(var relative in new[]{".portable","Star Tournament.exe","Update.exe","Uninstall.exe","app-icon.ico","current/sq.version","current/StarTournamentLauncher.exe","current/game/Player.exe","maintenance/StarTournamentMaintenance.exe","maintenance/update-public.pem","maintenance/installing.flag"})
        {var file=Path.Combine(path,relative);Directory.CreateDirectory(Path.GetDirectoryName(file)!);File.WriteAllText(file,"same bytes");}
        File.WriteAllText(Path.Combine(path,".star-tournament-install.json"),"{\"protocol\":1,\"product\":\"Star Tournament\"}");
        File.WriteAllText(Path.Combine(path,"current/sq.version"),"<package><metadata><id>"+config.AppId+"</id><version>"+config.Version+"</version><os>win</os><rid>win-x64</rid></metadata></package>");
        WriteConfig(path,config);File.WriteAllText(Path.Combine(path,"maintenance/maintenance.json"),JsonSerializer.Serialize(new{protocol=2,platform="win",architecture="x64",appId=config.AppId,updateChannel=config.UpdateChannel,version=config.Version}));
    }
    static void WriteConfig(string path,LauncherConfig config)=>File.WriteAllText(Path.Combine(path,"current/release.json"),JsonSerializer.Serialize(config,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase}));
}
