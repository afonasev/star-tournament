using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using StarTournament.Distribution;
using StarTournament.ProvingGround;

static class MacRecoveryChecks
{
    static readonly JsonSerializerOptions Json=new(){PropertyNameCaseInsensitive=true};
    const string AppId="tech.fixture.recovery";
    static string Logs(string scenario)=>Path.Combine(scenario,"state");
    static string Store(string scenario)=>Path.Combine(Logs(scenario),AppId+".recovery-"+StartupRecovery.InstallationId(Live(scenario)));
    static string Live(string scenario)=>Path.Combine(scenario,"Star Tournament.app");
    static string Exe(string root)=>Path.Combine(root,"Contents","MacOS","StarTournamentLauncher");
    static string ConfigPath(string root)=>Path.Combine(root,"Contents","MacOS","release.json");
    static LauncherConfig Config(string version)=>new(AppId,"osx","arm64",version,"2026-10-16",AuthenticatedSource.ProductionBase,"fixture-player", "production", version=="1.1.0"?new ReleaseRequirements("12.0",2,ReleaseCompatibility.PayloadFormat,ReleaseCompatibility.DataPolicy):null);
    static StartupRecovery Journal(string scenario)=>StartupRecovery.ForInstallation(Config("1.0.0"),Logs(scenario),Live(scenario));
    static string Version(string scenario)=>JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(ConfigPath(Live(scenario))),Json)!.Version;
    public static async Task Run()
    {
        if(!OperatingSystem.IsMacOS())throw new InvalidOperationException("Mac supervisor fixtures require actual macOS");
        var root=Path.Combine(Directory.GetCurrentDirectory(),".local","mac-supervision-'quoted-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        int passed=0;
        try
        {
            foreach(var mode in new[]{"success","exit-before-ready","missing-launcher","updater-error","bad-readback","deferred","orphan","manual-no-host","handoff-error"})
            {
                var scenario=Path.Combine(root,mode);Directory.CreateDirectory(scenario);Directory.CreateDirectory(Logs(scenario));
                File.WriteAllText(Path.Combine(scenario,"mode"),mode);Populate(Live(scenario),"1.0.0");
                var candidate=Path.Combine(scenario,"candidate.app");Populate(candidate,"1.1.0");
                var packages=Path.Combine(scenario,"packages");Directory.CreateDirectory(packages);
                using var rsa=RSA.Create(3072);
                foreach(var app in new[]{Live(scenario),candidate})File.WriteAllText(Path.Combine(app,"Contents","MacOS","update-public.pem"),rsa.ExportSubjectPublicKeyInfoPem());
                const string file="fixture-1.1.0-full.nupkg";var package=Path.Combine(packages,file);ZipFile.CreateFromDirectory(candidate,package);
                var bytes=File.ReadAllBytes(package);var payload=JsonSerializer.SerializeToUtf8Bytes(new{schema=2,compatibility=new{minimumOsVersion="12.0",minimumUpdaterProtocol=2,payloadFormat=ReleaseCompatibility.PayloadFormat,userDataPolicy=ReleaseCompatibility.DataPolicy},platform="osx",architecture="arm64",channel="production",version="1.1.0",publicationDate="2026-10-16",downloadUrl="https://github.com/afonasev/star-tournament/releases/download/v1.1.0/"+file,feed=new{Assets=new[]{new{PackageId=AppId,Version="1.1.0",Type="Full",FileName=file,SHA256=Convert.ToHexString(SHA256.HashData(bytes)),Size=bytes.Length}}}});
                File.WriteAllText(Path.Combine(packages,"star-authenticated.json"),JsonSerializer.Serialize(new{payload=Convert.ToBase64String(payload),signature=Convert.ToBase64String(rsa.SignData(payload,HashAlgorithmName.SHA256,RSASignaturePadding.Pss))}));
                using(var caller=Start(Exe(Live(scenario)),"--mac-caller-fixture",scenario))
                {await Exit(caller);if(caller.ExitCode!=0)throw new Exception("Caller fixture failed: "+caller.StandardError.ReadToEnd());}
                if(mode=="handoff-error")
                {
                    Require(Version(scenario)=="1.0.0"&&Journal(scenario).Read().Attempt==null&&Journal(scenario).IsBlocked("1.1.0"));
                    Require(!Directory.EnumerateDirectories(Store(scenario)).Any());
                    passed++;Console.WriteLine("PASS failed supervisor handoff preserves playable old payload");continue;
                }
                var identity=JsonSerializer.Deserialize<RecoverySupervisorProcess>(File.ReadAllText(Path.Combine(scenario,"host.json")),Json)!;
                if(mode=="orphan")
                {
                    await Until(()=>File.Exists(Path.Combine(scenario,"orphan-started.json"))&&Journal(scenario).Read().Attempt?.Phase=="recovery-required");
                    Require(Version(scenario)=="1.1.0"&&Alive(identity));
                    File.WriteAllText(Path.Combine(scenario,"orphan-may-exit"),"go");
                }
                await Until(()=>!Alive(identity));
                if(mode=="success")
                {
                    await Until(()=>File.Exists(Path.Combine(scenario,"target-finished.json")));
                    await WaitRecorded(Path.Combine(scenario,"target-finished.json"));
                    Require(Version(scenario)=="1.1.0"&&Journal(scenario).Read().Attempt==null&&Journal(scenario).Read().HealthyVersion=="1.1.0");
                    var args=JsonSerializer.Deserialize<string[]>(File.ReadAllText(Path.Combine(scenario,"observed-args.json")))!;
                    Require(args.Contains("-muted")&&args.Contains("-noaudio")&&args.Contains("профиль two ' quoted"));
                    Require(!Directory.EnumerateDirectories(Store(scenario)).Any());
                }
                else if(mode is "deferred" or "manual-no-host")
                {
                    Require(Version(scenario)=="1.1.0"&&Journal(scenario).Read().Attempt?.Phase=="prepared"&&!File.Exists(Path.Combine(scenario,"target-finished.json")));
                    if(mode=="manual-no-host") { File.WriteAllText(ConfigPath(Live(scenario)),"broken-json");File.Delete(Exe(Live(scenario))); }
                    var command=Directory.GetFiles(Store(scenario),"Restore-*.command").Single();
                    using(var recovery=Start("/bin/sh",command)){await Exit(recovery);Require(recovery.ExitCode==0);}
                    Require(Version(scenario)=="1.0.0"&&Journal(scenario).IsBlocked("1.1.0"));
                    Require(new MacRecoverySupervisor(Config("1.0.0"),Logs(scenario),Live(scenario)).CleanupSettled("1.0.0"));
                    Require(Journal(scenario).Read().Attempt==null);
                }
                else
                {
                    await Until(()=>File.Exists(Path.Combine(scenario,"restored-launch.json")));await WaitRecorded(Path.Combine(scenario,"restored-launch.json"));
                    Require(Version(scenario)=="1.0.0"&&Journal(scenario).IsBlocked("1.1.0")&&Journal(scenario).Read().Attempt?.Phase=="restored");
                    Require(File.Exists(Path.Combine(packages,"star-authenticated.json")));
                    var state=Journal(scenario).Read();
                    try{Journal(scenario).PermitRetry("1.1.0");throw new Exception("Retry permitted before cleanup");}catch(InvalidOperationException){}
                    Require(new MacRecoverySupervisor(Config("1.0.0"),Logs(scenario),Live(scenario)).CleanupSettled("1.0.0"));
                    Require(Journal(scenario).IsBlocked("1.1.0")&&Journal(scenario).Read().Attempt==null);
                    Journal(scenario).PermitRetry("1.1.0");Require(!Journal(scenario).IsBlocked("1.1.0"));
                }
                foreach(var record in new[]{"host.json","target-finished.json","restored-launch.json","orphan-started.json"})
                    if(File.Exists(Path.Combine(scenario,record)))await WaitRecorded(Path.Combine(scenario,record));
                passed++;Console.WriteLine("PASS real stable supervisor processes: "+mode);
            }
            var isolated=Path.Combine(root,"installation-separation");Directory.CreateDirectory(isolated);
            var first=StartupRecovery.ForInstallation(Config("1.0.0"),isolated,Path.Combine(root,"first.app"));
            var second=StartupRecovery.ForInstallation(Config("1.0.0"),isolated,Path.Combine(root,"second.app"));
            var ticket=first.BeginLaunch("1.0.0",DateTimeOffset.UtcNow.AddMinutes(1));first.AcceptReady(new(1,"1.0.0",ticket.Token,"menu-ready"),DateTimeOffset.UtcNow);
            Require(first.Read().HealthyVersion=="1.0.0"&&second.Read().HealthyVersion==null);
            try{second.Prepare("1.0.0","1.1.0",new string('a',32));throw new Exception("Another root's health granted update admission");}catch(InvalidOperationException){}
            passed++;Console.WriteLine("PASS a different installation root cannot inherit known health");
            Console.WriteLine($"Mac recovery: {passed} passed");
        }
        finally
        {
            // Kill only exact fixture processes on failure; no Unity or foreign process is touched.
            foreach(var record in Directory.EnumerateFiles(root,"*.json",SearchOption.AllDirectories).Where(p=>new[]{"host.json","target-finished.json","restored-launch.json","orphan-started.json"}.Contains(Path.GetFileName(p))))
            {
                try{var id=JsonSerializer.Deserialize<RecoverySupervisorProcess>(File.ReadAllText(record),Json);if(id!=null){using var owned=Process.GetProcessById(id.Pid);if(!owned.HasExited&&owned.StartTime.ToUniversalTime().Ticks==id.StartedTicks){owned.Kill(entireProcessTree:true);owned.WaitForExit();}}}
                catch(ArgumentException){}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception error)when(error.NativeErrorCode==3){}
            }
            Directory.Delete(root,true);
        }
    }
    static void Populate(string root,string version)
    {
        var bin=Path.Combine(root,"Contents","MacOS");Directory.CreateDirectory(bin);
        foreach(var source in Directory.GetFiles(AppContext.BaseDirectory))File.Copy(source,Path.Combine(bin,Path.GetFileName(source)),true);
        foreach(var name in new[]{"StarTournamentLauncher","UpdateMac"})
        {
            File.Copy(Environment.ProcessPath!,Path.Combine(bin,name),true);
            if(!OperatingSystem.IsWindows())File.SetUnixFileMode(Path.Combine(bin,name),UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
        }
        File.WriteAllText(ConfigPath(root),JsonSerializer.Serialize(Config(version),new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase}));
        var resources=Path.Combine(root,"Contents","Resources");Directory.CreateDirectory(resources);File.WriteAllText(Path.Combine(resources,"sq.version"),version);
        File.CreateSymbolicLink(Path.Combine(bin,"sq.version"),"../Resources/sq.version");
    }
    public static void Caller(string scenario)
    {
        using var own=new FileStream(Path.Combine(Logs(scenario),AppId+".lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        var journal=Journal(scenario);var ticket=journal.BeginLaunch("1.0.0",DateTimeOffset.UtcNow.AddMinutes(1));
        journal.AcceptReady(new(1,"1.0.0",ticket.Token,"menu-ready"),DateTimeOffset.UtcNow);
        var config=Config("1.0.0");var packages=Path.Combine(scenario,"packages");var envelope=File.ReadAllText(Path.Combine(packages,"star-authenticated.json"));
        using var verifier=new AuthenticatedSource(config,File.ReadAllText(Path.Combine(Live(scenario),"Contents","MacOS","update-public.pem")));verifier.Admit(envelope);
        RecoverySupervisorProcess host;
        try { host=new MacRecoverySupervisor(config,Logs(scenario),Live(scenario)).PrepareAndStart(Live(scenario),packages,verifier.Asset!,envelope,File.ReadAllText(Path.Combine(scenario,"mode")) is not ("deferred" or "manual-no-host"),
            new[]{"--mac-target-fixture",scenario,"-muted","-noaudio","профиль two ' quoted"}); }
        catch(IOException) when(File.ReadAllText(Path.Combine(scenario,"mode"))=="handoff-error") { return; }
        Record(Path.Combine(scenario,"host.json"),host);
    }
    public static async Task<int> Supervisor(string[] args)
    {
        var logs=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../.."));
        if(File.ReadAllText(Path.Combine(Path.GetDirectoryName(logs)!,"mode"))=="handoff-error")
        {Console.WriteLine("WRONG_ACK");await Task.Delay(10000);return 2;}
        return await MacRecoverySupervisor.Run(args,AppContext.BaseDirectory,logs);
    }
    public static void Update(string[] args)
    {
        Console.WriteLine("native updater output survives a closed caller ACK pipe");Console.Error.WriteLine("native stderr detached");
        string Arg(string name)=>args[Array.IndexOf(args,name)+1];
        Require(args.Contains("--norestart")&&args.Contains("--silent"));
        var root=Arg("--rootDir");var scenario=Path.GetDirectoryName(root)!;var mode=File.ReadAllText(Path.Combine(scenario,"mode"));
        Directory.Delete(root,true);Directory.CreateDirectory(root);
        if(mode=="updater-error"){File.WriteAllText(Path.Combine(root,"partial"),"failed");Environment.Exit(42);}
        ZipFile.ExtractToDirectory(Arg("--package"),root);
        if(mode=="missing-launcher")File.Delete(Exe(root));
        if(mode=="bad-readback")File.WriteAllText(ConfigPath(root),JsonSerializer.Serialize(Config("9.9.9"),new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase}));
    }
    public static void Target(string[] args)
    {
        Console.WriteLine("replacement output survives supervisor exit");Console.Error.WriteLine("replacement stderr detached");
        var scenario=args[1];Record(Path.Combine(scenario,"observed-args.json"),args);
        using var current=Process.GetCurrentProcess();var identity=new RecoverySupervisorProcess(current.Id,current.StartTime.ToUniversalTime().Ticks);
        if(Version(scenario)=="1.0.0")
        {Record(Path.Combine(scenario,"restored-launch.json"),identity);return;}
        using var own=new FileStream(Path.Combine(Logs(scenario),AppId+".lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        var mode=File.ReadAllText(Path.Combine(scenario,"mode"));var journal=Journal(scenario);
        var monitor=new StartupHealthMonitor(journal,"1.1.0",Path.Combine(scenario,"ready.json"),DateTimeOffset.UtcNow,()=>{});
        if(mode=="orphan")
        {
            using var child=Start(Exe(Live(scenario)),"--mac-orphan-fixture",scenario);
            Record(Path.Combine(Logs(scenario),AppId+".player.json"),new{pid=child.Id,started=child.StartTime.ToUniversalTime().Ticks});
        }
        if(mode is "exit-before-ready" or "orphan"){monitor.End(DateTimeOffset.UtcNow);return;}
        NativeStartupHealth.Publish(monitor.ReadyPath,"1.1.0",monitor.Ticket.Token);monitor.End(DateTimeOffset.UtcNow);
        var deadline=DateTimeOffset.UtcNow.AddSeconds(30);var supervisor=new MacRecoverySupervisor(Config("1.1.0"),Logs(scenario),Live(scenario));
        while(!supervisor.CleanupSettled("1.1.0")){if(DateTimeOffset.UtcNow>=deadline)throw new TimeoutException("Supervisor did not release confirmed backup");Thread.Sleep(25);}
        Record(Path.Combine(scenario,"target-finished.json"),identity);
    }
    public static void Orphan(string scenario)
    {
        using var process=Process.GetCurrentProcess();Record(Path.Combine(scenario,"orphan-started.json"),new RecoverySupervisorProcess(process.Id,process.StartTime.ToUniversalTime().Ticks));
        var deadline=DateTimeOffset.UtcNow.AddSeconds(30);
        while(!File.Exists(Path.Combine(scenario,"orphan-may-exit"))){if(DateTimeOffset.UtcNow>=deadline)throw new TimeoutException("Owned orphan fixture not released");Thread.Sleep(25);}
    }
    static void Record<T>(string path,T value)
    {
        var next=path+".next-"+Guid.NewGuid().ToString("N");
        using(var output=new FileStream(next,FileMode.CreateNew,FileAccess.Write,FileShare.None)){JsonSerializer.Serialize(output,value);output.Flush(true);}
        File.Move(next,path,true);
    }
    static Process Start(string executable,params string[] args)
    {
        var start=new ProcessStartInfo(executable){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in args)start.ArgumentList.Add(arg);
        start.Environment["DOTNET_ROOT"]=Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(),"../../.."));
        return Process.Start(start)!;
    }
    static bool Alive(RecoverySupervisorProcess identity)
    {try{using var p=Process.GetProcessById(identity.Pid);return !p.HasExited&&p.StartTime.ToUniversalTime().Ticks==identity.StartedTicks;}catch(ArgumentException){return false;}catch(InvalidOperationException){return false;}
        catch(System.ComponentModel.Win32Exception error)when(error.NativeErrorCode==3)
        {try{using var again=Process.GetProcessById(identity.Pid);return !again.HasExited;}catch(ArgumentException){return false;}}}
    static async Task Until(Func<bool> condition)
    {var deadline=DateTimeOffset.UtcNow.AddSeconds(30);while(!condition()){if(DateTimeOffset.UtcNow>=deadline)throw new TimeoutException("Owned fixture did not reach expected state");await Task.Delay(25);}}
    static async Task WaitRecorded(string path)
    {var identity=JsonSerializer.Deserialize<RecoverySupervisorProcess>(File.ReadAllText(path),Json)!;await Until(()=>!Alive(identity));}
    static async Task Exit(Process process)
    {using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));try{await process.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){process.Kill(entireProcessTree:true);throw;}}
    static void Require(bool value){if(!value)throw new Exception("Mac recovery contract failed");}
}
