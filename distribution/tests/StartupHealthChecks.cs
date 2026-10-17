using System.Text.Json;
using StarTournament.Distribution;
using StarTournament.ProvingGround;

static class StartupHealthChecks
{
    static readonly RecoveryIdentity Identity=new("tech.test","osx","arm64","production");
    static readonly DateTimeOffset Now=new(2026,10,16,12,0,0,TimeSpan.Zero);
    static int passed;
    public static void Run()
    {
        var root=Path.Combine(Directory.GetCurrentDirectory(),".local","startup-health-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            void Test(string name,Action<StartupRecovery,string> run)
            {
                var directory=Path.Combine(root,passed.ToString());Directory.CreateDirectory(directory);
                run(new(Path.Combine(directory,"health.json"),Identity),directory);passed++;Console.WriteLine("PASS "+name);
            }
            Test("absence and exit0 do not confirm menu",(journal,directory)=>
            {
                int called=0;var monitor=new StartupHealthMonitor(journal,"1.0.0",Path.Combine(directory,"ready.json"),Now,()=>called++);
                monitor.Poll(Now.AddSeconds(1));monitor.End(Now.AddSeconds(2));
                Require(!monitor.IsHealthy&&called==0&&journal.Read().HealthyVersion==null&&journal.Read().Launch==null);
            });
            Test("actual Unity writer confirms exact launcher challenge once",(journal,directory)=>
            {
                int called=0;var path=Path.Combine(directory,"ready.json");var monitor=new StartupHealthMonitor(journal,"1.0.0",path,Now,()=>called++);
                Require(NativeStartupHealth.Publish(path,monitor.Ticket.Version,monitor.Ticket.Token));
                monitor.Poll(Now.AddSeconds(1));monitor.Poll(Now.AddSeconds(2));monitor.End(Now.AddSeconds(3));
                Require(monitor.IsHealthy&&called==1&&journal.Read().HealthyVersion=="1.0.0");
                Require(!Directory.EnumerateFiles(directory,"*.unity-*").Any());
            });
            Test("legacy apply attempt is retained until actual ready",(journal,directory)=>
            {
                var legacy=new UpdateAttemptJournal(Path.Combine(directory,"apply.json"));legacy.Begin("1.0.0");
                var monitor=new StartupHealthMonitor(journal,"1.0.0",Path.Combine(directory,"ready.json"),Now,()=>legacy.ClearIfInstalled("1.0.0"));
                monitor.Poll(Now);Require(legacy.AlreadyAttempted("1.0.0"));
                NativeStartupHealth.Publish(monitor.ReadyPath,"1.0.0",monitor.Ticket.Token);monitor.Poll(Now.AddSeconds(1));
                Require(legacy.Read()==null);
            });
            Test("same version foreign launch token cannot clear apply attempt",(journal,directory)=>
            {
                var legacy=new UpdateAttemptJournal(Path.Combine(directory,"apply.json"));legacy.Begin("1.0.0");
                var monitor=new StartupHealthMonitor(journal,"1.0.0",Path.Combine(directory,"ready.json"),Now,()=>legacy.ClearIfInstalled("1.0.0"));
                NativeStartupHealth.Publish(monitor.ReadyPath,"1.0.0",new string('f',32));monitor.End(Now.AddSeconds(1));
                Require(!monitor.IsHealthy&&legacy.AlreadyAttempted("1.0.0"));
            });
            Test("version phase schema and unknown fields fail closed",(journal,directory)=>
            {
                var monitor=new StartupHealthMonitor(journal,"1.0.0",Path.Combine(directory,"ready.json"),Now,()=>throw new Exception("Invalid health"));
                foreach(var value in new[]{
                    new{schema=1,version="1.1.0",token=monitor.Ticket.Token,phase="menu-ready"},
                    new{schema=1,version="1.0.0",token=monitor.Ticket.Token,phase="installed"},
                    new{schema=2,version="1.0.0",token=monitor.Ticket.Token,phase="menu-ready"}})
                {File.WriteAllText(monitor.ReadyPath,JsonSerializer.Serialize(value));monitor.Poll(Now.AddSeconds(1));Require(!monitor.IsHealthy);}
                File.WriteAllText(monitor.ReadyPath,"{\"schema\":1,\"version\":\"1.0.0\",\"token\":\""+monitor.Ticket.Token+"\",\"phase\":\"menu-ready\",\"unknown\":true}");
                monitor.End(Now.AddSeconds(2));Require(!monitor.IsHealthy);
            });
            Test("malformed oversize and unreadable markers never confirm",(journal,directory)=>
            {
                var monitor=new StartupHealthMonitor(journal,"1.0.0",Path.Combine(directory,"ready.json"),Now,()=>throw new Exception("Invalid health"));
                foreach(var value in new[]{"{","null","{}",new string('x',4097)})
                {File.WriteAllText(monitor.ReadyPath,value);monitor.Poll(Now.AddSeconds(1));Require(!monitor.IsHealthy);}
                File.Delete(monitor.ReadyPath);Directory.CreateDirectory(monitor.ReadyPath);monitor.End(Now.AddSeconds(2));Require(!monitor.IsHealthy);
            });
            Test("timeout boundary blocks even a matching late marker",(journal,directory)=>
            {
                var old=journal.BeginLaunch("1.0.0",Now.AddMinutes(1));journal.AcceptReady(new(1,"1.0.0",old.Token,"menu-ready"),Now);
                journal.Prepare("1.0.0","1.1.0",new string('a',32));
                var monitor=new StartupHealthMonitor(journal,"1.1.0",Path.Combine(directory,"ready.json"),Now,()=>throw new Exception("Late ready admitted"));
                NativeStartupHealth.Publish(monitor.ReadyPath,"1.1.0",monitor.Ticket.Token);monitor.Poll(Now+StartupHealthMonitor.StartupTimeout);
                Require(!monitor.IsHealthy&&journal.IsBlocked("1.1.0")&&journal.Read().Attempt?.Phase=="recovery-required");
            });
            Test("last ready before exit is consumed without a poll race",(journal,directory)=>
            {
                int called=0;var monitor=new StartupHealthMonitor(journal,"1.0.0",Path.Combine(directory,"ready.json"),Now,()=>called++);
                NativeStartupHealth.Publish(monitor.ReadyPath,"1.0.0",monitor.Ticket.Token);monitor.End(Now.AddSeconds(1));
                Require(monitor.IsHealthy&&called==1);
            });
            Test("preexisting readiness path is not reused",(journal,directory)=>
            {var path=Path.Combine(directory,"ready.json");File.WriteAllText(path,"sentinel");Reject(()=>new StartupHealthMonitor(journal,"1.0.0",path,Now,()=>{}));Require(File.ReadAllText(path)=="sentinel");});
            Test("writer does not overwrite another marker",(_,directory)=>
            {var path=Path.Combine(directory,"ready.json");File.WriteAllText(path,"sentinel");Require(!NativeStartupHealth.Publish(path,"1.0.0",new string('a',32)));Require(File.ReadAllText(path)=="sentinel");});
            Test("writer rejects incomplete and unsafe handoffs",(_,directory)=>
            {
                Require(!NativeStartupHealth.Publish(null,null,null));
                foreach(var value in new[]{"","relative",""+Path.DirectorySeparatorChar})
                    Reject(()=>NativeStartupHealth.Publish(value,"1.0.0",null));
                Reject(()=>NativeStartupHealth.Publish(Path.Combine(directory,"ready.json"),"1.0.0\"",new string('a',32)));
                Reject(()=>NativeStartupHealth.Publish(Path.Combine(directory,"ready.json"),"1.0.0",new string('z',32)));
                Require(!File.Exists(Path.Combine(directory,"ready.json")));
            });
            Test("actual environment entrypoint emits the expected protocol",(journal,directory)=>
            {
                var monitor=new StartupHealthMonitor(journal,"1.0.0",Path.Combine(directory,"ready.json"),Now,()=>{});
                var names=new[]{NativeStartupHealth.ReadyEnvironment,NativeStartupHealth.VersionEnvironment,NativeStartupHealth.TokenEnvironment};
                var previous=names.Select(Environment.GetEnvironmentVariable).ToArray();
                try
                {
                    Environment.SetEnvironmentVariable(names[0],monitor.ReadyPath);Environment.SetEnvironmentVariable(names[1],monitor.Ticket.Version);Environment.SetEnvironmentVariable(names[2],monitor.Ticket.Token);
                    Require(NativeStartupHealth.ReportReady());monitor.End(Now.AddSeconds(1));Require(monitor.IsHealthy);
                }
                finally{for(int i=0;i<names.Length;i++)Environment.SetEnvironmentVariable(names[i],previous[i]);}
            });
            Test("platform latch is durable before journal health and callback",(journal,directory)=>
            {
                bool latched=false,notified=false;
                var monitor=new StartupHealthMonitor(journal,"1.0.0",Path.Combine(directory,"ready.json"),Now,
                    ()=>{Require(latched&&journal.Read().HealthyVersion=="1.0.0");notified=true;},
                    ()=>{Require(journal.Read().HealthyVersion==null);latched=true;return true;});
                File.WriteAllText(monitor.ReadyPath,JsonSerializer.Serialize(new StartupReady(1,"1.0.0",monitor.Ticket.Token,"menu-ready")));
                monitor.Poll(Now.AddSeconds(1));Require(monitor.IsHealthy&&latched&&notified);
            });
            Test("failed platform persistence leaves health pending and retries same marker",(journal,directory)=>
            {
                bool writable=false;int notifications=0;
                var monitor=new StartupHealthMonitor(journal,"1.0.0",Path.Combine(directory,"ready.json"),Now,()=>notifications++,()=>writable);
                File.WriteAllText(monitor.ReadyPath,JsonSerializer.Serialize(new StartupReady(1,"1.0.0",monitor.Ticket.Token,"menu-ready")));
                monitor.Poll(Now.AddSeconds(1));Require(!monitor.IsHealthy&&journal.Read().HealthyVersion==null&&journal.Read().Launch!=null&&notifications==0);
                writable=true;monitor.Poll(Now.AddSeconds(2));Require(monitor.IsHealthy&&notifications==1);
            });
            Test("foreign ready token cannot latch platform success",(journal,directory)=>
            {
                int hooks=0;var monitor=new StartupHealthMonitor(journal,"1.0.0",Path.Combine(directory,"ready.json"),Now,()=>{},()=>{hooks++;return true;});
                File.WriteAllText(monitor.ReadyPath,JsonSerializer.Serialize(new StartupReady(1,"1.0.0","foreign","menu-ready")));
                monitor.Poll(Now.AddSeconds(1));Require(hooks==0&&!monitor.IsHealthy&&journal.Read().HealthyVersion==null);
            });
            Console.WriteLine($"Startup health: {passed} passed");
        }
        finally{Directory.Delete(root,true);}
    }
    static void Require(bool value){if(!value)throw new Exception("Startup health contract failed");}
    static void Reject(Action action){try{action();}catch(ArgumentException){return;}throw new Exception("Invalid startup handoff admitted");}
}
