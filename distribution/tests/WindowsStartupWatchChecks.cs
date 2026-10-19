using System.Diagnostics;
using StarTournament.Distribution;

static class WindowsStartupWatchChecks
{
    public static async Task Child(string path)
    {
        File.WriteAllText(path + ".started", "ready");
        while (!File.Exists(path + ".exit")) await Task.Delay(20);
    }
    static void Require(bool condition) { if (!condition) throw new Exception("Windows startup observation contract failed"); }
    static async Task Reject(Func<Task> action)
    { try { await action(); } catch (IOException) { return; } throw new Exception("Expected failure"); }
    static async Task Until(Func<bool> condition)
    {
        var limit=Stopwatch.StartNew(); while (!condition()) { if(limit.Elapsed>TimeSpan.FromSeconds(10)) throw new TimeoutException("Fixture did not reach expected state"); await Task.Delay(20); }
    }
    public static async Task Run()
    {
        int passed=0;
        async Task Test(string name, Func<Task> run) { await run(); passed++; Console.WriteLine("PASS " + name); }
        await Test("healthy live launcher returns without waiting for or replacing Player",async()=>
        {
            Require(await WindowsStartupWatch.Observe(()=>true,()=>true,()=>throw new Exception("wait"),()=>false,()=>throw new Exception("restore"),()=>throw new Exception("launch")));
        });
        await Test("early exit waits for Player before protected restore and relaunch",async()=>
        {
            var order=new List<string>();
            Require(!await WindowsStartupWatch.Observe(()=>false,()=>false,()=>{order.Add("player-exit");return Task.CompletedTask;},()=>false,()=>{order.Add("protected-restore");return Task.CompletedTask;},()=>order.Add("old-launch")));
            Require(order.SequenceEqual(new[]{"player-exit","protected-restore","old-launch"}));
        });
        await Test("late confirmation while orphan exits forbids restore",async()=>
        {
            bool healthy=false;
            Require(await WindowsStartupWatch.Observe(()=>false,()=>healthy,()=>{healthy=true;return Task.CompletedTask;},()=>false,()=>throw new Exception("restore"),()=>throw new Exception("launch")));
        });
        await Test("durable menu health conservatively forbids rollback after latch IO failure",async()=>
        {
            Require(await WindowsStartupWatch.Observe(()=>false,()=>false,()=>Task.CompletedTask,()=>true,()=>throw new Exception("restore"),()=>throw new Exception("launch")));
        });
        await Test("declined UAC never retries or relaunches",async()=>
        {
            int prompts=0,launches=0;
            await Reject(()=>WindowsStartupWatch.Observe(()=>false,()=>false,()=>Task.CompletedTask,()=>false,()=>{prompts++;throw new IOException("UAC cancelled");},()=>launches++));Require(prompts==1&&launches==0);
        });
        await Test("unknown process liveness cannot grant rollback",async()=>
        {
            await Reject(()=>WindowsStartupWatch.Observe(()=>throw new IOException("access denied"),()=>false,()=>throw new Exception("wait"),()=>false,()=>throw new Exception("restore"),()=>throw new Exception("launch")));
        });
        await Test("corrupt orphan identity preserves every payload",async()=>
        {
            await Reject(()=>WindowsStartupWatch.Observe(()=>false,()=>false,()=>throw new IOException("bad identity"),()=>false,()=>throw new Exception("restore"),()=>throw new Exception("launch")));
        });
        await Test("transaction/latch read failure never authorizes downgrade",async()=>
        {
            await Reject(()=>WindowsStartupWatch.Observe(()=>false,()=>throw new IOException("foreign id"),()=>Task.CompletedTask,()=>false,()=>throw new Exception("restore"),()=>throw new Exception("launch")));
        });
        await Test("real orphan process blocks replacement until actual exit",async()=>
        {
            var folder=Path.Combine(Directory.GetCurrentDirectory(),".local","windows-watch-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            var signal=Path.Combine(folder,"player");Process? child=null;
            try
            {
                var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false};
                foreach(var arg in new[]{"--windows-watch-child",signal})start.ArgumentList.Add(arg);
                child=Process.Start(start)??throw new IOException("fixture launch");await Until(()=>File.Exists(signal+".started"));
                bool waiting=false,restored=false;
                var watch=WindowsStartupWatch.Observe(()=>false,()=>false,async()=>{waiting=true;await child.WaitForExitAsync();},()=>false,()=>{restored=true;return Task.CompletedTask;},()=>{});
                await Until(()=>waiting);Require(!watch.IsCompleted&&!restored&&!child.HasExited);
                File.WriteAllText(signal+".exit","exit");Require(!await watch);Require(child.HasExited&&restored);
            }
            finally
            {
                File.WriteAllText(signal+".exit","exit");if(child!=null){await child.WaitForExitAsync();child.Dispose();}Directory.Delete(folder,true);
            }
        });
        await Test("Windows argv encodes empty quoted unicode and terminal slashes",()=>
        {
            Require(WindowsDetachedProcess.Quote("")=="\"\"");Require(WindowsDetachedProcess.Quote("a b")=="\"a b\"");
            Require(WindowsDetachedProcess.Quote("a\"b")=="\"a\\\"b\"");Require(WindowsDetachedProcess.Quote("x\\")=="\"x\\\\\"");
            Require(WindowsDetachedProcess.Quote("тест")=="\"тест\"");
            try{WindowsDetachedProcess.Quote("bad\0arg");throw new Exception("NUL accepted");}catch(ArgumentException){}
            return Task.CompletedTask;
        });
        Console.WriteLine($"Windows startup watch: {passed} passed; native Windows process/handle-list runtime NotRun on non-Windows");
    }
}
