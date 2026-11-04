using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using StarTournament.Distribution;

static class PlayerUpdateIsolationChecks
{
    static void Check(bool value,string message)
    { if(!value)throw new Exception(message);Console.WriteLine("PASS "+message); }
    public static void Probe()
    {
        Check(Environment.GetEnvironmentVariable(PlayerLaunchEnvironment.LegacyStatus)==null,"child has no update status channel");
        Check(Environment.GetEnvironmentVariable(PlayerLaunchEnvironment.LegacyCommand)==null,"child has no update command channel");
        foreach(var name in new[]{"RELEASE_LABEL","STARTUP_READY","STARTUP_VERSION","STARTUP_TOKEN"})
            Check(Environment.GetEnvironmentVariable("STAR_TOURNAMENT_"+name)=="fixture-"+name,"child retains "+name);
    }
    public static async Task Run()
    {
        var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        if(Path.GetFileNameWithoutExtension(start.FileName)=="dotnet")start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        start.ArgumentList.Add("--player-env-probe");
        start.Environment[PlayerLaunchEnvironment.LegacyStatus]="stale-status.json";
        start.Environment[PlayerLaunchEnvironment.LegacyCommand]="stale-command.json";
        foreach(var name in new[]{"RELEASE_LABEL","STARTUP_READY","STARTUP_VERSION","STARTUP_TOKEN"})start.Environment["STAR_TOURNAMENT_"+name]="fixture-"+name;
        PlayerLaunchEnvironment.IsolateUpdates(start);
        using(var child=Process.Start(start)!)
        {
            var output=child.StandardOutput.ReadToEndAsync();var error=child.StandardError.ReadToEndAsync();
            await child.WaitForExitAsync();Console.Write(await output);
            Check(child.ExitCode==0,"actual Player child environment isolation: "+await error);
        }
        var machine=typeof(Launcher).GetMethod("Run")!.GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType;
        Check(!machine.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Any(f=>f.FieldType==typeof(UpdateSession)||f.FieldType==typeof(ReleaseHistory)),"launcher async state has no in-game updater/history session (structural)");
        const string hostile="\" & do shell script \"touch /tmp/unwanted\"";
        foreach(var retry in new[]{false,true})
        {
            var prompt=BootstrapPrompt.FailureCommand(new UpdateSnapshot("error",Error:hostile),retry);
            Check(!prompt.UseShellExecute&&prompt.ArgumentList[2]=="--"&&prompt.ArgumentList[3].Contains(hostile),"failure text is an argument");
            Check(!prompt.ArgumentList[1].Contains(hostile)&&prompt.ArgumentList[1].Contains("Повторить")==retry,"failure prompt offers only allowed retry");
        }
    }
}
