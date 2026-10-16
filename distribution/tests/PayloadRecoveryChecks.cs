using System.Diagnostics;
using System.Text.Json;
using StarTournament.Distribution;

static class PayloadRecoveryChecks
{
    static readonly RecoveryIdentity Identity = new("tech.test", "osx", "arm64", "production");
    static readonly string Id = new('a',32);
    sealed class Interrupted : Exception { }
    static int passed;
    public static void Run()
    {
        var root=Path.Combine(Directory.GetCurrentDirectory(),".local","payload-contracts-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            void Test(string name,Action<string,string,PayloadRecovery> action)
            {
                var scenario=Path.Combine(root,passed.ToString());Directory.CreateDirectory(scenario);
                var live=Path.Combine(scenario,"Star Tournament.app"); Directory.CreateDirectory(Path.Combine(live,"Contents","MacOS"));
                Config(live,"1.0.0");
                File.WriteAllText(Path.Combine(live,"Contents","MacOS","Player"),"old working game");
                Directory.CreateDirectory(Path.Combine(live,"empty"));
                if(!OperatingSystem.IsWindows())File.SetUnixFileMode(Path.Combine(live,"Contents","MacOS","Player"),UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
                action(scenario,live,new PayloadRecovery(Path.Combine(scenario,"store"),Identity));
                passed++;Console.WriteLine("PASS "+name);
            }
            string ConfigPath(string live)=>Path.Combine(live,"Contents","MacOS","release.json");
            void Config(string live,string version)=>File.WriteAllText(ConfigPath(live),JsonSerializer.Serialize(new{appId=Identity.AppId,platform=Identity.Platform,architecture=Identity.Architecture,version,updateChannel=Identity.Channel}));
            string Game(string live)=>Path.Combine(live,"Contents","MacOS","Player");
            void Changed(string live)
            {File.WriteAllText(Game(live),"new broken game");Config(live,"1.1.0");File.WriteAllText(Path.Combine(live,"new-extra"),"new version only");}
            void Restored(string live)
            {Require(File.ReadAllText(Game(live))=="old working game" && JsonDocument.Parse(File.ReadAllText(ConfigPath(live))).RootElement.GetProperty("version").GetString()=="1.0.0" && !File.Exists(Path.Combine(live,"new-extra")));}

            Test("full snapshot includes files empty directories and modes",(_,live,store)=>
            {
                var snapshot=store.Capture(live,"1.0.0",Id);
                Require(snapshot.Version=="1.0.0" && snapshot.Entries.Any(v=>v.Path=="empty"&&v.Kind=="directory"));
                Require(snapshot.Entries.Any(v=>v.Path=="Contents/MacOS/Player"&&v.Kind=="file"&&v.Sha256?.Length==64));
                Changed(live);store.Restore(Id,live);Restored(live);store.Verify(Id,live);
                if(!OperatingSystem.IsWindows())Require(File.GetUnixFileMode(Game(live)).HasFlag(UnixFileMode.UserExecute));
            });
            Test("same snapshot id is immutable and bound to version",(_,live,store)=>
            {
                var snapshot=store.Capture(live,"1.0.0",Id); Changed(live);
                Require(store.Capture(live,"1.0.0",Id).Entries.SequenceEqual(snapshot.Entries));
                Reject(()=>store.Capture(live,"1.1.0",Id));store.Restore(Id,live);Restored(live);
            });
            Test("foreign install cannot consume a snapshot",(scenario,live,store)=>
            {
                store.Capture(live,"1.0.0",Id);var other=Path.Combine(scenario,"other.app");Directory.CreateDirectory(other);
                Reject(()=>store.Verify(Id,other));Reject(()=>store.Restore(Id,other));
                Require(!Directory.EnumerateFileSystemEntries(other).Any());
            });
            Test("foreign identity cannot restore payload",(scenario,live,store)=>
            {
                store.Capture(live,"1.0.0",Id);Changed(live);
                foreach(var identity in new[]{Identity with{AppId="other"},Identity with{Platform="win"},Identity with{Architecture="x64"},Identity with{Channel="test"}})
                    Reject(()=>new PayloadRecovery(Path.Combine(scenario,"store"),identity).Restore(Id,live));
                Require(File.ReadAllText(Game(live))=="new broken game");
            });
            Test("corrupted backup bytes never replace current payload",(scenario,live,store)=>
            {
                store.Capture(live,"1.0.0",Id);Changed(live);File.WriteAllText(Game(Path.Combine(scenario,"store",Id,"payload")),"corrupt");
                Reject(()=>store.Restore(Id,live));Require(File.ReadAllText(Game(live))=="new broken game");
            });
            Test("extra or missing backup entry is rejected",(scenario,live,store)=>
            {
                store.Capture(live,"1.0.0",Id);var payload=Path.Combine(scenario,"store",Id,"payload");
                var extra=Path.Combine(payload,"extra");File.WriteAllText(extra,"bad");Reject(()=>store.Verify(Id,live));File.Delete(extra);
                File.Delete(Game(payload));Reject(()=>store.Verify(Id,live));
            });
            Test("manifest paths cannot select outside files",(scenario,live,store)=>
            {
                var snapshot=store.Capture(live,"1.0.0",Id);Changed(live);
                var sentinel=Path.Combine(scenario,"sentinel");File.WriteAllText(sentinel,"keep");
                var manifest=Path.Combine(scenario,"store",Id,"manifest.json");
                File.WriteAllText(manifest,JsonSerializer.Serialize(snapshot with {Entries=[new("../../sentinel","file",null,null,0)]}));
                Reject(()=>store.Restore(Id,live));Require(File.ReadAllText(sentinel)=="keep"&&File.ReadAllText(Game(live))=="new broken game");
            });
            Test("invalid id and overlapping roots fail before copy",(scenario,live,store)=>
            {
                foreach(var id in new[]{"../outside","",new string('F',32)})Reject(()=>store.Capture(live,"1.0.0",id));
                var inside=new PayloadRecovery(Path.Combine(live,"backups"),Identity);Reject(()=>inside.Capture(live,"1.0.0",Id));
                Reject(()=>store.Capture(Path.Combine(scenario,"store"),"1.0.0",Id));
                Reject(()=>store.Capture(live,"1.0.0-test.1",Id));
            });
            foreach(var phase in new[]{"capture-verified","capture-committed"})
                Test("capture resumes after "+phase,(scenario,live,store)=>
                {
                    var interrupted=new PayloadRecovery(Path.Combine(scenario,"store"),Identity,p=>{if(p==phase)throw new Interrupted();});
                    Interrupt(()=>interrupted.Capture(live,"1.0.0",Id));
                    store.Capture(live,"1.0.0",Id);Changed(live);store.Restore(Id,live);Restored(live);
                });
            foreach(var phase in new[]{"restore-prepared","candidate-renamed","backup-activated","restore-verified"})
                Test("restore resumes after "+phase,(scenario,live,store)=>
                {
                    store.Capture(live,"1.0.0",Id);Changed(live);
                    var interrupted=new PayloadRecovery(Path.Combine(scenario,"store"),Identity,p=>{if(p==phase)throw new Interrupted();});
                    Interrupt(()=>interrupted.Restore(Id,live));
                    store.Restore(Id,live);Restored(live);store.Verify(Id,live);
                    Require(!Directory.EnumerateFileSystemEntries(scenario,".star-recovery-*").Any());
                    Require(!File.Exists(Path.Combine(scenario,"store",".restore-"+Id+".json")));
                });
            Test("missing candidate can be recovered from retained snapshot",(_,live,store)=>
            {store.Capture(live,"1.0.0",Id);Directory.Delete(live,true);store.Restore(Id,live);Restored(live);});
            Test("unowned sibling is preserved",(scenario,live,store)=>
            {
                store.Capture(live,"1.0.0",Id);Changed(live);
                var foreign=Path.Combine(scenario,".star-recovery-"+Path.GetFileName(live)+"-"+Id+".next");
                Directory.CreateDirectory(foreign);File.WriteAllText(Path.Combine(foreign,"keep"),"foreign");
                Reject(()=>store.Restore(Id,live));Require(File.ReadAllText(Path.Combine(foreign,"keep"))=="foreign");
            });
            Test("damaged intent cannot trigger deletion",(scenario,live,store)=>
            {
                store.Capture(live,"1.0.0",Id);Changed(live);
                File.WriteAllText(Path.Combine(scenario,"store",".restore-"+Id+".json"),"{}");
                Reject(()=>store.Restore(Id,live));Require(File.ReadAllText(Game(live))=="new broken game");
            });
            Test("pending restoration prevents snapshot deletion",(scenario,live,store)=>
            {
                store.Capture(live,"1.0.0",Id);Changed(live);
                var interrupted=new PayloadRecovery(Path.Combine(scenario,"store"),Identity,p=>{if(p=="candidate-renamed")throw new Interrupted();});
                Interrupt(()=>interrupted.Restore(Id,live));Reject(()=>store.Remove(Id,live));
                store.Restore(Id,live);Restored(live);store.Remove(Id,live);Require(!Directory.Exists(Path.Combine(scenario,"store",Id)));
            });
            if(!OperatingSystem.IsWindows())
            {
                Test("root links cannot redirect a snapshot",(scenario,live,store)=>
                {var link=Path.Combine(scenario,"live-link");Directory.CreateSymbolicLink(link,live);Reject(()=>store.Capture(link,"1.0.0",Id));Directory.Delete(link);});
                Test("outside and dangling payload links are rejected",(scenario,live,store)=>
                {
                    var outside=Path.Combine(scenario,"outside");File.WriteAllText(outside,"keep");
                    var link=Path.Combine(live,"escape");File.CreateSymbolicLink(link,"../outside");Reject(()=>store.Capture(live,"1.0.0",Id));File.Delete(link);
                    File.CreateSymbolicLink(link,"missing");Reject(()=>store.Capture(live,"1.0.0",Id));File.Delete(link);
                    Require(File.ReadAllText(outside)=="keep");
                });
                Test("linked store and journal cannot escape root",(scenario,live,store)=>
                {
                    var outside=Path.Combine(scenario,"outside");Directory.CreateDirectory(outside);
                    var link=Path.Combine(scenario,"linked-store");Directory.CreateSymbolicLink(link,outside);
                    Reject(()=>new PayloadRecovery(link,Identity));Directory.Delete(link);
                    store.Capture(live,"1.0.0",Id);var manifest=Path.Combine(scenario,"store",Id,"manifest.json");File.Delete(manifest);
                    File.CreateSymbolicLink(manifest,Path.Combine(outside,"manifest"));Reject(()=>store.Restore(Id,live));File.Delete(manifest);
                });
            }
            if(OperatingSystem.IsMacOS())
            {
                Test("macOS framework links and extended attributes survive restore",(_,live,store)=>
                {
                    var framework=Path.Combine(live,"Contents","Frameworks","Demo.framework");Directory.CreateDirectory(Path.Combine(framework,"Versions","A"));
                    File.WriteAllText(Path.Combine(framework,"Versions","A","Demo"),"framework");
                    Directory.CreateSymbolicLink(Path.Combine(framework,"Versions","Current"),"A");
                    File.CreateSymbolicLink(Path.Combine(framework,"Demo"),"Versions/Current/Demo");
                    Tool("/usr/bin/xattr","-w","com.star.recovery","metadata preserved",Game(live));
                    store.Capture(live,"1.0.0",Id);Changed(live);Directory.Delete(framework,true);store.Restore(Id,live);Restored(live);
                    Require(File.ReadAllText(Path.Combine(framework,"Demo"))=="framework");
                    Require(Tool("/usr/bin/xattr","-p","com.star.recovery",Game(live)).Trim()=="metadata preserved");
                    Require(new FileInfo(Path.Combine(framework,"Demo")).LinkTarget=="Versions/Current/Demo");
                });
                Test("cleanup of failed candidate never follows an outside symlink",(scenario,live,store)=>
                {
                    store.Capture(live,"1.0.0",Id);Changed(live);
                    var outside=Path.Combine(scenario,"user-data");Directory.CreateDirectory(outside);File.WriteAllText(Path.Combine(outside,"keep"),"untouched");
                    Directory.CreateSymbolicLink(Path.Combine(live,"outside-link"),outside);
                    store.Restore(Id,live);Restored(live);Require(File.ReadAllText(Path.Combine(outside,"keep"))=="untouched");
                });
            }
            Test("retained descriptor must bind actual product channel and version",(_,live,store)=>
            {
                Config(live,"1.1.0");Reject(()=>store.Capture(live,"1.0.0",Id));
                File.WriteAllText(ConfigPath(live),"{}");Reject(()=>store.Capture(live,"1.0.0",Id));
                File.WriteAllText(ConfigPath(live),"{");Reject(()=>store.Capture(live,"1.0.0",Id));
            });
            Test("capture rejects files changing during copy",(scenario,live,store)=>
            {
                var moving=new PayloadRecovery(Path.Combine(scenario,"store"),Identity,p=>{if(p=="capture-copied")File.WriteAllText(Game(live),"changed while copying");});
                Reject(()=>moving.Capture(live,"1.0.0",Id));
                Require(!Directory.Exists(Path.Combine(scenario,"store",Id)));
                store.Capture(live,"1.0.0",Id);Require(store.Verify(Id,live).Version=="1.0.0");
            });
            if(!OperatingSystem.IsWindows())Test("backup modes are part of verification",(scenario,live,store)=>
            {
                store.Capture(live,"1.0.0",Id);
                if(!OperatingSystem.IsWindows())
                {
                    var file=Game(Path.Combine(scenario,"store",Id,"payload"));
                    File.SetUnixFileMode(file,UnixFileMode.UserRead|UnixFileMode.UserWrite);Reject(()=>store.Verify(Id,live));
                }
            });
            if(OperatingSystem.IsMacOS())Test("case aliases cannot nest backup inside live app",(_,live,store)=>
            {
                var nested=new PayloadRecovery(Path.Combine(live,"inner-store"),Identity);
                Reject(()=>nested.Capture(live.ToUpperInvariant(),"1.0.0",Id));
            });
            Test("health journal and filesystem restore retain failed-target barrier",(scenario,live,store)=>
            {
                var journalPath=Path.Combine(scenario,"health.json");var journal=new StartupRecovery(journalPath,Identity);
                var now=DateTimeOffset.UtcNow;var old=journal.BeginLaunch("1.0.0",now.AddMinutes(1));
                Require(journal.AcceptReady(new(1,old.Version,old.Token,"menu-ready"),now));
                store.Capture(live,"1.0.0",Id);journal.Prepare("1.0.0","1.1.0",Id);Changed(live);
                var attempt=journal.BeginLaunch("1.1.0",now.AddMinutes(1));journal.EndBeforeReady(attempt);
                store.Restore(Id,live);Restored(live);journal.MarkRestored("1.0.0",Id);
                Require(journal.Read().Attempt?.Phase=="restored"&&journal.IsBlocked("1.1.0"));
                store.Remove(Id,live); // A stop here must retain the journal's cleanup identity.
                journal=new StartupRecovery(journalPath,Identity);journal.RecoverInterrupted("1.0.0");
                Require(journal.Read().Attempt?.BackupId==Id&&!Directory.Exists(Path.Combine(scenario,"store",Id)));
                journal.ConfirmBackupRemoved(Id);Require(journal.Read().Attempt==null&&journal.IsBlocked("1.1.0"));
            });
            Test("successful menu-ready permits cleanup without rolling back",(scenario,live,store)=>
            {
                var journal=new StartupRecovery(Path.Combine(scenario,"health.json"),Identity);var now=DateTimeOffset.UtcNow;
                var old=journal.BeginLaunch("1.0.0",now.AddMinutes(1));Require(journal.AcceptReady(new(1,old.Version,old.Token,"menu-ready"),now));
                store.Capture(live,"1.0.0",Id);journal.Prepare("1.0.0","1.1.0",Id);Changed(live);
                var next=journal.BeginLaunch("1.1.0",now.AddMinutes(1));Require(journal.AcceptReady(new(1,next.Version,next.Token,"menu-ready"),now));
                journal.RecoverInterrupted("1.1.0");Require(journal.Read().Attempt?.Phase=="confirmed");
                store.Remove(Id,live);journal.ConfirmBackupRemoved(Id);
                Require(journal.Read().HealthyVersion=="1.1.0"&&File.ReadAllText(Game(live))=="new broken game"); // Fixture bytes: health comes only from the explicit marker.
            });
            if(!OperatingSystem.IsWindows())Test("dangling partial-capture link cannot redirect writes",(scenario,live,store)=>
            {
                var outside=Path.Combine(scenario,"outside-missing");var link=Path.Combine(scenario,"store",".capture-"+Id);
                Directory.CreateSymbolicLink(link,outside);Reject(()=>store.Capture(live,"1.0.0",Id));
                Require(!Directory.Exists(outside));File.Delete(link);
            });
            foreach(var phase in new[]{"restore-prepared","candidate-renamed","backup-activated","restore-verified"})
                Test("fresh process resumes abrupt child exit at "+phase,(scenario,live,store)=>
                {
                    store.Capture(live,"1.0.0",Id);Changed(live);
                    var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
                    if(Path.GetFileNameWithoutExtension(Environment.ProcessPath)=="dotnet")start.ArgumentList.Add(typeof(PayloadRecoveryChecks).Assembly.Location);
                    start.ArgumentList.Add("--payload-crash-fixture");start.ArgumentList.Add(scenario);start.ArgumentList.Add(phase);
                    using(var child=Process.Start(start)!)
                    {
                        if(!child.WaitForExit(30000)){child.Kill(entireProcessTree:true);throw new TimeoutException("Owned recovery fixture did not exit");}
                        if(child.ExitCode!=91)throw new Exception("Abrupt-exit fixture failed: "+child.StandardError.ReadToEnd());
                    }
                    var reopened=new PayloadRecovery(Path.Combine(scenario,"store"),Identity);reopened.Restore(Id,live);Restored(live);reopened.Verify(Id,live);
                    Require(!Directory.EnumerateFileSystemEntries(scenario,".star-recovery-*").Any());
                });
            foreach(var phase in new[]{"snapshot-delete-authorized","snapshot-deleted"})
                Test("removal resumes after "+phase,(scenario,live,store)=>
                {
                    store.Capture(live,"1.0.0",Id);
                    var interrupted=new PayloadRecovery(Path.Combine(scenario,"store"),Identity,p=>{if(p==phase)throw new Interrupted();});
                    Interrupt(()=>interrupted.Remove(Id,live));
                    new PayloadRecovery(Path.Combine(scenario,"store"),Identity).Remove(Id,live);
                    Require(!Directory.Exists(Path.Combine(scenario,"store",Id))&&!File.Exists(Path.Combine(scenario,"store",".delete-"+Id+".json")));
                    store.Remove(Id,live);Restored(live);
                });
            Test("VerifyInstalled rejects changed working payload",(_,live,store)=>
            {store.Capture(live,"1.0.0",Id);store.VerifyInstalled(Id,live);Changed(live);Reject(()=>store.VerifyInstalled(Id,live));});
            if(OperatingSystem.IsMacOS())
            {
                Test("actual Velopack sq.version parent link survives snapshot restore",(_,live,store)=>
                {
                    var resource=Path.Combine(live,"Contents","Resources");Directory.CreateDirectory(resource);File.WriteAllText(Path.Combine(resource,"sq.version"),"1.0.0");
                    var link=Path.Combine(live,"Contents","MacOS","sq.version");File.CreateSymbolicLink(link,"../Resources/sq.version");
                    store.Capture(live,"1.0.0",Id);Changed(live);store.Restore(Id,live);
                    Require(new FileInfo(link).LinkTarget=="../Resources/sq.version"&&File.ReadAllText(link)=="1.0.0");
                });
                Test("parent escapes through chained internal links are rejected",(scenario,live,store)=>
                {
                    Directory.CreateDirectory(Path.Combine(live,"Contents","Resources"));
                    Directory.CreateSymbolicLink(Path.Combine(live,"Contents","safe"),"Resources");
                    var outside=Path.Combine(scenario,"outside");File.WriteAllText(outside,"keep");
                    File.CreateSymbolicLink(Path.Combine(live,"Contents","MacOS","escape"),"../safe/../../../outside");
                    Reject(()=>store.Capture(live,"1.0.0",Id));Require(File.ReadAllText(outside)=="keep");
                });
                Test("cyclic and absolute internal links remain rejected",(_,live,store)=>
                {
                    var path=Path.Combine(live,"cycle");File.CreateSymbolicLink(path,"cycle");Reject(()=>store.Capture(live,"1.0.0",Id));File.Delete(path);
                    File.CreateSymbolicLink(path,Game(live));Reject(()=>store.Capture(live,"1.0.0",Id));File.Delete(path);
                });
            }
            Console.WriteLine($"Payload recovery: {passed} passed");
        }
        finally{Directory.Delete(root,true);}
    }
    public static void CrashFixture(string scenario,string phase)
    {
        var store=new PayloadRecovery(Path.Combine(scenario,"store"),Identity,p=>{if(p==phase)Environment.Exit(91);});
        store.Restore(Id,Path.Combine(scenario,"Star Tournament.app"));
        throw new Exception("Abrupt-exit checkpoint was not reached");
    }
    static string Tool(string executable,params string[] args)
    {
        var start=new ProcessStartInfo(executable){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in args)start.ArgumentList.Add(arg);
        using var process=Process.Start(start)!;var output=process.StandardOutput.ReadToEnd();var error=process.StandardError.ReadToEnd();process.WaitForExit();
        if(process.ExitCode!=0)throw new Exception(error);return output;
    }
    static void Require(bool value){if(!value)throw new Exception("Payload recovery contract failed");}
    static void Reject(Action action)
    {try{action();}catch(Exception ex)when(ex is IOException or ArgumentException){return;}throw new Exception("Unsafe payload recovery was admitted");}
    static void Interrupt(Action action)
    {try{action();}catch(Interrupted){return;}throw new Exception("Fault injection did not interrupt transaction");}
}
