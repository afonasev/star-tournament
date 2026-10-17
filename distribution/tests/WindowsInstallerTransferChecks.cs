using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using StarTournament.Distribution;

static class WindowsInstallerTransferChecks
{
    static readonly LauncherConfig Config=new("tech.fixture.transfer","win","x64","1.0.0","2026-10-16",AuthenticatedSource.ProductionBase,"game/Player.exe","production");
    static int passed;
    static void Require(bool value){if(!value)throw new Exception("Installer startup transfer contract failed");}
    static void Reject(Action action){try{action();}catch(IOException){return;}throw new Exception("Unsafe transfer allowed");}
    public static void Crash(string root,string point)
    {
        var t=new WindowsInstallerTransaction(root,Config with{Version="1.2.0"},checkpoint:p=>{if(p==point)Environment.Exit(61);});t.Commit();throw new Exception("Fixture missed checkpoint");
    }
    public static void Native()
    {
        if(!OperatingSystem.IsWindows()){Console.WriteLine("NotRun: Windows native append-handle/share-delete contracts require Windows");return;}
        var home=Path.Combine(Path.GetTempPath(),"st-transfer-native-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(home);
        var root=Path.Combine(home,"installed");var moved=root+".moved";
        try
        {
            var source=Update(root);Ready(root,source);var id=source.Read().Attempt!.Id;long before=new FileInfo(Latch(root,id)).Length;
            WindowsInstallation.MarkStartupReady(root,"1.1.0");Require(new FileInfo(Latch(root,id)).Length==before+1);Console.WriteLine("PASS native FILE_APPEND_DATA readiness write preserves existing bytes");
            Begin(root,"1.2.0");
            using(WindowsRecoveryStore.TransferForInstaller(root,Config with{Version="1.2.0"},p=>Directory.CreateDirectory(p),Create,Create))
            {
                bool denied=false;try{using var append=new FileStream(Latch(root,id),FileMode.Open,FileAccess.Write,FileShare.ReadWrite|FileShare.Delete);}catch(IOException error) when((error.HResult&0xffff)==32){denied=true;}
                Require(denied);Directory.Move(root,moved);Directory.Move(moved,root);
            }
            Console.WriteLine("PASS native latch lease denies append and permits whole-root directory rename");
            Console.WriteLine("Windows native transfer: 2 passed (temporary owned files; protected ACL/UAC/NSIS/Player NOT covered)");
        }
        finally{if(Directory.Exists(moved)&&!Directory.Exists(root))Directory.Move(moved,root);Directory.Delete(home,true);}
    }
    public static void Run()
    {
        var home=Path.Combine(Directory.GetCurrentDirectory(),".local","installer-transfer-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(home);
        try
        {
            void Test(string name,Action<string> action){var root=Path.Combine(home,passed.ToString(),"installed");Directory.CreateDirectory(Path.GetDirectoryName(root)!);action(root);passed++;Console.WriteLine("PASS "+name);}
            Test("unconfirmed newer installer keeps original known-good payload and bootstrap",root=>
            {
                var old=Update(root);var original=old.Read().Attempt!.Id;var t=Begin(root,"1.2.0");t.Commit();t.FinalizeInstall();t.ClearSettled();var current=new WindowsRecoveryStore(root,Config);
                Require(current.Read().Attempt is {Target:"1.2.0",Previous.Version:"1.0.0"}&&current.Read().Attempt!.Id==original);current.Restore();Old(root,"1.0.0");Require(current.Read().Blocked.Contains("1.2.0"));
            });
            Test("unconfirmed same-version repair retains exact fallback",root=>
            {Update(root);var t=Begin(root,"1.1.0");t.Commit();t.FinalizeInstall();t.ClearSettled();new WindowsRecoveryStore(root,Config).Restore();Old(root,"1.0.0");});
            Test("confirmed same-version repair never reopens older rollback",root=>
            {var old=Update(root);Ready(root,old);var t=Begin(root,"1.1.0");t.Commit();t.FinalizeInstall();t.ClearSettled();var store=new WindowsRecoveryStore(root,Config);Reject(()=>store.Restore());Require(WindowsInstallation.PendingStartup(root,"1.1.0")==null);store.Cleanup("1.1.0");Require(store.Read().Attempt==null);});
            Test("missing latch same-version repair keeps conservative floor playable",root=>
            {var old=Update(root);File.Delete(Latch(root,old.Read().Attempt!.Id));var t=Begin(root,"1.1.0");t.Commit();t.FinalizeInstall();t.ClearSettled();var store=new WindowsRecoveryStore(root,Config);Reject(()=>store.Restore());Require(WindowsInstallation.PendingStartup(root,"1.1.0")==null);});
            Test("confirmed upgrade captures current floor and retains older backup until attestation",root=>
            {
                var old=Update(root);Ready(root,old);var oldId=old.Read().Attempt!.Id;var t=Begin(root,"1.2.0");t.Commit();t.FinalizeInstall();t.ClearSettled();var store=new WindowsRecoveryStore(root,Config);var state=store.Read();
                Require(state.Attempt is {Previous.Version:"1.1.0",Target:"1.2.0"}&&state.Retained!.SequenceEqual(new[]{oldId}));Require(Directory.Exists(Path.Combine(Recovery(root),oldId)));
                Reject(()=>store.Restore(oldId));store.Restore();Old(root,"1.1.0");Require(Directory.Exists(Path.Combine(Recovery(root),oldId)));store.Cleanup("1.1.0");Require(store.Read().Attempt==null&&store.Read().Retained==null&&!Directory.Exists(Path.Combine(Recovery(root),oldId)));
            });
            Test("missing latch upgrade does not fall below conservative current floor",root=>
            {var old=Update(root);File.Delete(Latch(root,old.Read().Attempt!.Id));var t=Begin(root,"1.2.0");t.Commit();t.FinalizeInstall();t.ClearSettled();var store=new WindowsRecoveryStore(root,Config);Require(store.Read().Attempt!.Previous.Version=="1.1.0");store.Restore();Old(root,"1.1.0");});
            Test("new target health authorizes resumable removal of every archive",root=>
            {var old=Update(root);Ready(root,old);var id=old.Read().Attempt!.Id;var t=Begin(root,"1.2.0");t.Commit();t.FinalizeInstall();t.ClearSettled();var store=new WindowsRecoveryStore(root,Config);Reject(()=>store.Cleanup("1.2.0"));Ready(root,store);store.Cleanup("1.2.0");Require(store.Read().Attempt==null&&!Directory.Exists(Path.Combine(Recovery(root),id)));});
            Test("restored partial deletion is carried while fresh fallback captures actual previous",root=>
            {
                var old=Update(root);old.Restore();var id=old.Read().Attempt!.Id;
                var payload=new PayloadRecovery(Recovery(root),new(Config.AppId,"win","x64","production"),p=>{if(p=="snapshot-delete-authorized")throw new IOException("crash");});
                Reject(()=>payload.Remove(id,Path.Combine(root,"current")));File.Delete(Path.Combine(Recovery(root),id,"payload/game/Player.exe"));
                var t=Begin(root,"1.2.0","1.0.0");t.Commit();t.FinalizeInstall();t.ClearSettled();var store=new WindowsRecoveryStore(root,Config);Require(store.Read().Attempt!.Previous.Version=="1.0.0"&&store.Read().Retained!.Contains(id));
                Ready(root,store);store.Cleanup("1.2.0");Require(store.Read().Attempt==null&&!File.Exists(Path.Combine(Recovery(root),".delete-"+id+".json")));
            });
            Test("restored same-version repair preserves partial cleanup intent",root=>
            {var old=Update(root);old.Restore();var t=Begin(root,"1.0.0","1.0.0");t.Commit();t.FinalizeInstall();t.ClearSettled();var store=new WindowsRecoveryStore(root,Config);Require(store.Read().Attempt!.Phase=="restored");store.Cleanup("1.0.0");Require(store.Read().Attempt==null);});
            Test("corrupt original fallback refuses replacement before root move",root=>
            {var old=Update(root);var id=old.Read().Attempt!.Id;File.WriteAllText(Path.Combine(Recovery(root),id,"payload/game/Player.exe"),"corrupt");var fingerprint=Fingerprint(root);var t=Begin(root,"1.2.0");Reject(t.Commit);Require(Fingerprint(root)==fingerprint&&t.Read()!.Phase=="extracting");t.Recover();});
            Test("insufficient transfer space leaves source byte-exact",root=>
            {
                var old=Update(root);Ready(root,old);var fingerprint=Fingerprint(root);Begin(root,"1.2.0");
                Reject(()=>WindowsRecoveryStore.TransferForInstaller(root,Config with{Version="1.2.0"},p=>Directory.CreateDirectory(p),Create,Create,availableBytes:()=>0));Require(Fingerprint(root)==fingerprint);
            });
            Test("repeated unconfirmed installers never promote an unconfirmed fallback",root=>
            {Update(root);var t=Begin(root,"1.2.0");t.Commit();t.FinalizeInstall();t.ClearSettled();t=Begin(root,"1.3.0","1.2.0");t.Commit();t.FinalizeInstall();t.ClearSettled();var store=new WindowsRecoveryStore(root,Config);Require(store.Read().Attempt!.Previous.Version=="1.0.0");store.Restore();Old(root,"1.0.0");});
            foreach(var point in new[]{"transfer-space-checked","transfer-copied","transfer-new-floor-captured","transfer-prepared","old-root-moved","new-root-moved","installed"})
                Test("outer rollback after "+point+" restores exact original recovery",root=>
                {var old=Update(root);Ready(root,old);var before=Fingerprint(root);var t=Begin(root,"1.2.0",checkpoint:p=>{if(p==point)throw new IOException("interrupted");});Reject(t.Commit);new WindowsInstallerTransaction(root,Config with{Version="1.2.0"}).Recover();Require(Fingerprint(root)==before);});
            foreach(var point in new[]{"transfer-copied","transfer-new-floor-captured","old-root-moved","new-root-moved"})
                Test("abrupt transfer process exit at "+point+" preserves original store",root=>
                {
                    var old=Update(root);Ready(root,old);var before=Fingerprint(root);Begin(root,"1.2.0");var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false};
                    foreach(var arg in new[]{"--installer-transfer-crash",root,point})start.ArgumentList.Add(arg);using var child=Process.Start(start)??throw new IOException("fixture");
                    try{Require(child.WaitForExit(10000)&&child.ExitCode==61);}finally{if(!child.HasExited){child.Kill();child.WaitForExit();}}
                    new WindowsInstallerTransaction(root,Config with{Version="1.2.0"}).Recover();Require(Fingerprint(root)==before);
                });
            Test("cleanup state-write failure retains the current floor latch and can resume",root=>
            {
                var old=Update(root);Ready(root,old);var t=Begin(root,"1.2.0");t.Commit();t.FinalizeInstall();t.ClearSettled();var store=new WindowsRecoveryStore(root,Config);Ready(root,store);var id=store.Read().Attempt!.Id;
                var failing=new WindowsRecoveryStore(root,Config,p=>Directory.CreateDirectory(p),p=>{if(Path.GetFileName(p).StartsWith("state.json.next-"))throw new IOException("disk fault");return Create(p);});
                Reject(()=>failing.Cleanup("1.2.0"));Require(File.Exists(Latch(root,id))&&new FileInfo(Latch(root,id)).Length!=0&&WindowsInstallation.PendingStartup(root,"1.2.0")==null);
                store.Cleanup("1.2.0");Require(store.Read().Attempt==null&&store.Read().Retained==null);
            });
            Test("archived identifiers cannot escape the store or refer to current attempt",root=>
            {
                var old=Update(root);var state=old.Read();var path=Path.Combine(Recovery(root),"state.json");
                File.WriteAllText(path,JsonSerializer.Serialize(state with{Retained=["../escape"]}));Reject(()=>old.Read());
                File.WriteAllText(path,JsonSerializer.Serialize(state with{Retained=[state.Attempt!.Id]}));Reject(()=>old.Read());
            });
            Test("explicit installer retry preserves other failed targets until exact new health",root=>
            {
                var old=Update(root);Ready(root,old);var state=old.Read();File.WriteAllText(Path.Combine(Recovery(root),"state.json"),JsonSerializer.Serialize(state with{Blocked=["1.2.0","1.4.0"]}));
                var t=Begin(root,"1.2.0");t.Commit();t.FinalizeInstall();t.ClearSettled();var store=new WindowsRecoveryStore(root,Config);Require(store.Read().Blocked.SequenceEqual(new[]{"1.2.0","1.4.0"}));
                Ready(root,store);store.Cleanup("1.2.0");Require(store.Read().Blocked.SequenceEqual(new[]{"1.4.0"}));
            });
            Test("unconfirmed damaged release metadata does not replace the original fallback",root=>
            {Update(root);File.WriteAllText(Path.Combine(root,"current/release.json"),"broken");var t=Begin(root,"1.1.0");t.Commit();t.FinalizeInstall();t.ClearSettled();new WindowsRecoveryStore(root,Config).Restore();Old(root,"1.0.0");});
            Test("same-version repair can fix floor metadata before a newer installer captures it",root=>
            {
                var old=Update(root);Ready(root,old);File.WriteAllText(Path.Combine(root,"current/release.json"),"broken");var before=Fingerprint(root);var upgrade=Begin(root,"1.2.0");Reject(upgrade.Commit);upgrade.Recover();upgrade.ClearSettled();Require(Fingerprint(root)==before);
                var repair=Begin(root,"1.1.0");repair.Commit();repair.FinalizeInstall();repair.ClearSettled();upgrade=Begin(root,"1.2.0");upgrade.Commit();upgrade.FinalizeInstall();upgrade.ClearSettled();var store=new WindowsRecoveryStore(root,Config);Require(store.Read().Attempt!.Previous.Version=="1.1.0");store.Restore();Old(root,"1.1.0");
            });
            Test("legacy state without retained field transfers without inventing an archive",root=>
            {
                var old=Update(root);using var document=JsonDocument.Parse(JsonSerializer.Serialize(old.Read()));var fields=document.RootElement.EnumerateObject().Where(p=>p.Name!="Retained").ToDictionary(p=>p.Name,p=>p.Value.Clone());File.WriteAllText(Path.Combine(Recovery(root),"state.json"),JsonSerializer.Serialize(fields));
                var t=Begin(root,"1.2.0");t.Commit();t.FinalizeInstall();t.ClearSettled();var store=new WindowsRecoveryStore(root,Config);Require(store.Read().Retained==null);store.Restore();Old(root,"1.0.0");
            });
            Test("successive confirmed installer floors retain the whole archive chain",root=>
            {
                var old=Update(root);Ready(root,old);var t=Begin(root,"1.2.0");t.Commit();t.FinalizeInstall();t.ClearSettled();var middle=new WindowsRecoveryStore(root,Config);Ready(root,middle);t=Begin(root,"1.3.0","1.2.0");t.Commit();t.FinalizeInstall();t.ClearSettled();var store=new WindowsRecoveryStore(root,Config);var ids=store.Read().Retained!;Require(ids.Length==2&&store.Read().Attempt!.Previous.Version=="1.2.0");
                store.Restore();Old(root,"1.2.0");store.Cleanup("1.2.0");Require(ids.All(id=>!Directory.Exists(Path.Combine(Recovery(root),id)))&&store.Read().Retained==null);
            });
            Test("both copied and new target latches use the protected append-only creator",root=>
            {
                var old=Update(root);Ready(root,old);int created=0;var t=new WindowsInstallerTransaction(root,Config with{Version="1.2.0"},createReady:p=>{created++;return Create(p);});t.Begin("1.1.0");Layout(t.Stage,"1.2.0");t.Commit();Require(created>=2);t.Recover();Old(root,"1.1.0");
            });
            Test("interrupted capture can be restored before installer retries the target",root=>
            {
                Layout(root,"1.0.0");File.Delete(Path.Combine(root,"maintenance/installing.flag"));var broken=new WindowsRecoveryStore(root,Config,p=>Directory.CreateDirectory(p),p=>{if(Path.GetFileName(p).StartsWith("manifest.json.write-"))throw new IOException("capture fault");return Create(p);});Reject(()=>broken.Prepare(Config,"1.1.0",false));var source=new WindowsRecoveryStore(root,Config);Require(source.Read().Attempt!.Phase=="capturing");source.Restore();var t=Begin(root,"1.1.0","1.0.0");t.Commit();t.FinalizeInstall();t.ClearSettled();var store=new WindowsRecoveryStore(root,Config);store.Restore();Old(root,"1.0.0");
            });
            Test("prepared update restores before installer establishes its next fallback",root=>
            {
                Layout(root,"1.0.0");File.Delete(Path.Combine(root,"maintenance/installing.flag"));var source=new WindowsRecoveryStore(root,Config);source.Prepare(Config,"1.1.0",false);source.Restore();var t=Begin(root,"1.2.0","1.0.0");t.Commit();t.FinalizeInstall();t.ClearSettled();var store=new WindowsRecoveryStore(root,Config);Require(store.Read().Attempt!.Previous.Version=="1.0.0");store.Restore();Old(root,"1.0.0");
            });
            Test("interrupted bootstrap restore resumes before installer transfer",root=>
            {
                Update(root);var broken=new WindowsRecoveryStore(root,Config,p=>Directory.CreateDirectory(p),p=>{if(Path.GetFileName(p).StartsWith("Star Tournament.exe.restore-"))throw new IOException("bootstrap fault");return Create(p);});Reject(()=>broken.Restore());var source=new WindowsRecoveryStore(root,Config);Require(source.Read().Attempt!.Phase=="restoring");source.Restore();Old(root,"1.0.0");var t=Begin(root,"1.2.0","1.0.0");t.Commit();t.FinalizeInstall();t.ClearSettled();var store=new WindowsRecoveryStore(root,Config);store.Restore();Old(root,"1.0.0");
            });
            Test("first installer upgrade with no journal retains previous payload until ready",root=>
            {
                Layout(root,"1.0.0");File.Delete(Path.Combine(root,"maintenance/installing.flag"));Require(!Directory.Exists(Recovery(root)));var t=Begin(root,"1.2.0","1.0.0");t.Commit();t.FinalizeInstall();t.ClearSettled();var store=new WindowsRecoveryStore(root,Config);Require(store.Read().Attempt is{Previous.Version:"1.0.0",Target:"1.2.0"});Reject(()=>store.Cleanup("1.2.0"));store.Restore();Old(root,"1.0.0");
            });
            Test("first upgrade stages all recovery metadata without changing source directories",root=>
            {
                Layout(root,"1.0.0");File.Delete(Path.Combine(root,"maintenance/installing.flag"));var before=Fingerprint(root);var t=Begin(root,"1.2.0","1.0.0",p=>{if(p=="transfer-prepared")throw new IOException("cancel");});Reject(t.Commit);Require(!Directory.Exists(Recovery(root))&&Fingerprint(root)==before);t.Recover();Require(Fingerprint(root)==before);
            });
            Test("settled no-attempt store still captures a fallback for a newer installer",root=>
            {
                var old=Update(root);Ready(root,old);old.Cleanup("1.1.0");Require(old.Read().Attempt==null);var t=Begin(root,"1.2.0");t.Commit();t.FinalizeInstall();t.ClearSettled();var store=new WindowsRecoveryStore(root,Config);Require(store.Read().Attempt!.Previous.Version=="1.1.0");store.Restore();Old(root,"1.1.0");
            });
            Test("legacy playback protocol is restored while the new broker binary is retained",root=>
            {
                Layout(root,"1.0.0");File.Delete(Path.Combine(root,"maintenance/installing.flag"));var path=Path.Combine(root,"maintenance/maintenance.json");File.WriteAllText(path,File.ReadAllText(path).Replace("\"protocol\":2","\"protocol\":1"));File.WriteAllText(Path.Combine(root,"maintenance/StarTournamentMaintenance.exe"),"old broker");
                var t=Begin(root,"1.2.0","1.0.0");t.Commit();t.FinalizeInstall();t.ClearSettled();var store=new WindowsRecoveryStore(root,Config);Require(store.Read().Attempt!.PreviousProtocol==1);store.Restore();using var contract=JsonDocument.Parse(File.ReadAllText(path));Require(contract.RootElement.GetProperty("metadataReader").GetInt32()==2);Require(WindowsInstallation.MetadataReader(root)==2);Require(contract.RootElement.GetProperty("protocol").GetInt32()==1&&contract.RootElement.GetProperty("version").GetString()=="1.0.0");Require(File.ReadAllText(Path.Combine(root,"maintenance/StarTournamentMaintenance.exe"))=="stable");Old(root,"1.0.0");
                store.Cleanup("1.0.0");Require(store.Read().Attempt==null&&WindowsInstallation.MetadataReader(root)==2);Require(File.ReadAllText(Path.Combine(root,"maintenance/StarTournamentMaintenance.exe"))=="stable");Old(root,"1.0.0");
            });
            Test("same-version repair with no recovery journal creates no downgrade snapshot",root=>
            {Layout(root,"1.0.0");File.Delete(Path.Combine(root,"maintenance/installing.flag"));var t=Begin(root,"1.0.0","1.0.0");t.Commit();t.FinalizeInstall();t.ClearSettled();Require(!File.Exists(Path.Combine(Recovery(root),"state.json")));});
            Console.WriteLine($"Windows installer transfer: {passed} passed (actual portable source/files/processes; Windows sharing/ACL/UAC NotRun)");
        }
        finally{Directory.Delete(home,true);}
    }
    static FileStream Create(string path)=>new(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);
    static string Recovery(string root)=>Path.Combine(root,"maintenance","recovery");
    static string Latch(string root,string id)=>Path.Combine(Recovery(root),id+".ready");
    static void Ready(string root,WindowsRecoveryStore store)=>File.AppendAllText(Latch(root,store.Read().Attempt!.Id),"confirmed");
    static WindowsRecoveryStore Update(string root)
    {
        Layout(root,"1.0.0");File.Delete(Path.Combine(root,"maintenance/installing.flag"));var store=new WindowsRecoveryStore(root,Config);store.Prepare(Config,"1.1.0",false);
        store.Apply(()=>{Current(root,"1.1.0");File.WriteAllText(Path.Combine(root,"Star Tournament.exe"),"boot 1.1.0");File.WriteAllText(Path.Combine(root,"Update.exe"),"updater 1.1.0");});return store;
    }
    static WindowsInstallerTransaction Begin(string root,string version,string installed="1.1.0",Action<string>? checkpoint=null)
    {var t=new WindowsInstallerTransaction(root,Config with{Version=version},checkpoint:checkpoint);t.Begin(installed);Layout(t.Stage,version);return t;}
    static void Layout(string root,string version)
    {
        Directory.CreateDirectory(Path.Combine(root,"maintenance"));Current(root,version);
        File.WriteAllText(Path.Combine(root,".star-tournament-install.json"),"{\"protocol\":1,\"product\":\"Star Tournament\",\"installerVersion\":\""+version+"\"}");
        File.WriteAllBytes(Path.Combine(root,".portable"),[]);File.WriteAllText(Path.Combine(root,"Star Tournament.exe"),"stable");File.WriteAllText(Path.Combine(root,"Update.exe"),"updater "+version);
        File.WriteAllText(Path.Combine(root,"Uninstall.exe"),"uninstaller");File.WriteAllText(Path.Combine(root,"app-icon.ico"),"icon");File.WriteAllText(Path.Combine(root,"maintenance/StarTournamentMaintenance.exe"),"stable");File.WriteAllText(Path.Combine(root,"maintenance/update-public.pem"),"key");File.WriteAllText(Path.Combine(root,"maintenance/installing.flag"),version);
        using var config=JsonDocument.Parse(JsonSerializer.Serialize(Config with{Version=version},new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase}));
        var fields=config.RootElement.EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value.Clone());fields["protocol"]=JsonSerializer.SerializeToElement(2);File.WriteAllText(Path.Combine(root,"maintenance/maintenance.json"),JsonSerializer.Serialize(fields));
    }
    static void Current(string root,string version)
    {Directory.CreateDirectory(Path.Combine(root,"current/game"));File.WriteAllText(Path.Combine(root,"current/game/Player.exe"),"game "+version);File.WriteAllText(Path.Combine(root,"current/StarTournamentLauncher.exe"),"launcher "+version);File.WriteAllText(Path.Combine(root,"current/release.json"),JsonSerializer.Serialize(Config with{Version=version},new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase}));File.WriteAllText(Path.Combine(root,"current/sq.version"),"<package><metadata><id>"+Config.AppId+"</id><version>"+version+"</version><os>win</os><rid>win-x64</rid></metadata></package>");}
    static void Old(string root,string version){Require(File.ReadAllText(Path.Combine(root,"current/game/Player.exe"))=="game "+version);Require(File.ReadAllText(Path.Combine(root,"Update.exe"))=="updater "+version);}
    static string Fingerprint(string root)=>string.Join("|",Directory.GetFiles(root,"*",SearchOption.AllDirectories).Order().Select(p=>Path.GetRelativePath(root,p)+":"+Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)))));
}
