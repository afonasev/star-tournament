using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using StarTournament.Distribution;

static class LauncherSurfaceChecks
{
    public static void Render(string directory)
    {
        if(!Path.IsPathFullyQualified(directory))throw new ArgumentException("Absolute owned preview output required");
        Directory.CreateDirectory(directory);
        var offer=new LauncherSurface();_ = offer.Decision(new("available",Version:"1.1.0"));
        var error=new LauncherSurface();_ = error.Decision(new("error",Error:"Не удалось связаться с GitHub. Проверьте подключение и повторите попытку.",Version:"1.1.0"),true);
        LauncherView[] views=[offer.View,new(1,1,new("downloading",42,Version:"1.1.0"),[LauncherAction.Cancel]),new(1,1,new("applying"),[]),error.View,new LauncherSurface().View,new(1,1,new("launching"),[])];
        File.WriteAllText(Path.Combine(directory,"index.html"),LauncherPresentation.Html(views));
        File.WriteAllText(Path.Combine(directory,"source-note.txt"),"Source-derived client-area layout preview. Fixture version 1.1.0 is not a release claim. Native Windows HWND, fonts/DPI/UAC/controller acceptance NotRun.\n");
        offer.Close();error.Close();Console.WriteLine("Preview written: "+directory);
    }
    public static async Task Run()
    {
        int passed=0;
        void Check(bool ok,string why){if(!ok)throw new Exception(why);passed++;Console.WriteLine("PASS "+why);}
        var surface=new LauncherSurface();
        var decision=surface.Decision(new("available",Version:"1.1.0"));var offer=surface.View;
        Check(!decision.IsCompleted&&offer.Actions.SequenceEqual(new[]{LauncherAction.Play,LauncherAction.Update}),"one offer owns an explicit play/update choice");
        Check(surface.Choose(offer.Id,LauncherAction.Notes)&&!decision.IsCompleted,"release notes are not consent and keep the offer open");
        Check(!surface.Choose(offer.Id,LauncherAction.Retry)&&!surface.Choose(offer.Id-1,LauncherAction.Update),"unoffered/stale actions cannot grant consent");
        Check(surface.Choose(offer.Id,LauncherAction.Update)&&await decision==LauncherAction.Update,"explicit update is consumed once");
        Check(!surface.Choose(offer.Id,LauncherAction.Update),"double click cannot consume the decision twice");
        surface.Publish(new("downloading",10,Version:"1.1.0"));long progressId=surface.View.Id;
        surface.Publish(new("downloading",42,Version:"1.1.0"));
        Check(surface.View.Id==progressId&&surface.View.Status.Progress==42,"progress updates preserve the cancel control's decision identity");
        Check(surface.Choose(progressId,LauncherAction.Cancel),"cancel is admitted during transfer");
        surface.Publish(new("downloading",100,Version:"1.1.0"));
        Check(surface.View.Status.State=="cancelling"&&!surface.TryBeginApply(),"late completion cannot overwrite cancellation or start installation");
        var apply=new LauncherSurface();apply.Publish(new("downloading",100));long before=apply.View.Id;
        Check(apply.TryBeginApply()&&!apply.Choose(before,LauncherAction.Cancel)&&apply.CloseAction==null,"atomic installation admission rejects late cancel/close");
        var close=new LauncherSurface();var waiting=close.Decision(new("available",Version:"1.1.0"));
        Check(close.Choose(close.View.Id,LauncherAction.Quit)&&await waiting==LauncherAction.Quit,"window close on an offer quits rather than silently launching");
        var invalidNotes=new LauncherSurface();_ = invalidNotes.Decision(new("available",Version:"not-a-release"));
        Check(!invalidNotes.View.Notes&&!invalidNotes.Choose(invalidNotes.View.Id,LauncherAction.Notes),"invalid release text cannot become a clickable external target");invalidNotes.Close();
        var blocked=new LauncherSurface();_ = blocked.Decision(new("blocked",Error:"Восстановите установку"),playable:false);
        Check(blocked.View.Actions.SequenceEqual(new[]{LauncherAction.Quit}),"unconfirmed broken install never offers an unsafe Play or Retry action");blocked.Close();
        foreach(var state in new[]{"checking","available","downloading","cancelling","applying","launching","error","unavailable","blocked"})
        foreach(int width in new[]{380,620,800})
        {
            LauncherAction[] actions=state=="available"?[LauncherAction.Play,LauncherAction.Update]:state=="downloading"?[LauncherAction.Cancel]:state is "error" or "unavailable"?[LauncherAction.Play,LauncherAction.Retry]:state=="blocked"?[LauncherAction.Quit]:[];
            var view=new LauncherView(1,1,new(state,42,"Проверочная ошибка",Version:"1.1.0"),actions,state=="available");
            var frame=LauncherPresentation.Frame(view,width,350);
            Check(frame.Buttons.All(b=>b.Box.X>=0&&b.Box.Y>=0&&b.Box.X+b.Box.Width<=width&&b.Box.Y+b.Box.Height<=350&&b.Box.Height>=30),$"buttons fit supported client area: {state}/{width}");
            Check(frame.Buttons.Zip(frame.Buttons.Skip(1),(a,b)=>a.Box.Y!=b.Box.Y||a.Box.X+a.Box.Width<b.Box.X).All(x=>x),$"same-row buttons have separate hit regions: {state}/{width}");
        }
        var hostile=new LauncherView(1,1,new("error",Error:"<script>bad()</script> & error"),[LauncherAction.Play,LauncherAction.Quit]);
        string html=LauncherPresentation.Html([hostile]);
        Check(!html.Contains("<script>")&&html.Contains("&lt;script&gt;"),"source-derived preview renders error strings as text");
        var edges=new LauncherPadEdges();
        Check(!edges.Read(true,true,0x1000).Confirm&&!edges.Read(true,true,0x1000).Confirm,"already-held controller confirm on connect is not consent");
        edges.Read(true,true,0);Check(edges.Read(true,true,0x1000).Confirm&&!edges.Read(true,true,0x1000).Confirm,"controller confirm is a single rising edge");
        edges.Read(true,false,0);Check(!edges.Read(true,true,0x1000).Confirm,"regaining focus with held confirm is not consent");
        edges.Read(false,true,0);Check(!edges.Read(true,true,0x1000).Confirm,"reconnect with held confirm is not consent");
        edges.Read(true,true,0);Check(edges.Read(true,true,8).Move==1&&edges.Read(true,true,8).Move==0,"held controller navigation cannot oscillate selection");
        var assembly=typeof(BootstrapStartupWindow).Assembly;
        foreach(var item in new[]{("WindowClass",80),("DrawItem",64),("Message",48),("Paint",72),("MinMax",40),("PadState",16)})
            Check(Marshal.SizeOf(assembly.GetType("StarTournament.Distribution.LauncherWin32+"+item.Item1,true)!)==item.Item2,"native win-x64 ABI: "+item.Item1);
        foreach(var mode in new[]{"retry","play","quit","apply-error"})
        {
            int discoveries=0,confirms=0,downloads=0,applies=0,errors=0;
            var result=await BootstrapStartup.Run("1.0.0",_=>{discoveries++;return Task.FromResult<string?>("1.1.0");},_=>{confirms++;return Task.FromResult(true);},
                (_,_)=>{downloads++;if(downloads==1&&mode!="apply-error")throw new CryptographicException("Bad package");return Task.CompletedTask;},
                target=>{applies++;Check(target=="1.1.0","activation retains frozen target: "+mode);if(mode=="apply-error")throw new IOException("Apply failed");return Task.CompletedTask;},_=>{},
                failure:(_,retry)=>{errors++;Check(retry==(mode!="apply-error"),"retry only offered before activation: "+mode);return Task.FromResult(mode=="retry"?BootstrapFailureChoice.Retry:mode=="quit"?BootstrapFailureChoice.Quit:BootstrapFailureChoice.Play);});
            Check(discoveries==1&&confirms==1,"retry/exit never repeat discovery or consent: "+mode);
            Check(downloads==(mode=="retry"?2:1)&&applies==(mode is "retry" or "apply-error"?1:0),"only verified completion reaches one apply: "+mode);
            Check(result.HandedOff==(mode=="retry")&&result.QuitRequested==(mode=="quit")&&errors==1,"failure choice reaches the expected single outcome: "+mode);
        }
        var transfer=new LauncherTransfer();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var drain=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);bool installed=false;
        var run=BootstrapStartup.Run("1.0.0",_=>Task.FromResult<string?>("1.1.0"),_=>Task.FromResult(true),
            (progress,token)=>transfer.Run(async(_,inner)=>{entered.SetResult();try{await Task.Delay(Timeout.Infinite,inner);}catch(OperationCanceledException){await drain.Task;throw;}},progress,token),
            _=>{installed=true;return Task.CompletedTask;},_=>{});
        await entered.Task;transfer.Cancel();
        Check(!run.IsCompleted&&!installed,"download cancel waits for actual network drain before fallback can launch");
        drain.SetResult();var cancelled=await run;
        Check(!cancelled.HandedOff&&!cancelled.QuitRequested&&cancelled.Snapshot.Error.Length==0&&!installed,"drained user cancellation permits installed play without activation/error modal");
        var late=await BootstrapStartup.Run("1.0.0",_=>Task.FromResult<string?>("1.1.0"),_=>Task.FromResult(true),(_,_)=>Task.CompletedTask,
            _=>throw new Exception("Cancelled completion must not apply"),_=>{},admitApply:()=>false);
        Check(!late.HandedOff&&late.Snapshot.Error.Length==0,"UI cancellation winning the final admission race prevents apply");
        Console.WriteLine($"PASS {passed} branded launcher contracts; native Windows HWND/device/DPI/UAC acceptance NotRun");
    }
}
