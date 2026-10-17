using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace StarTournament.Distribution;

/// <summary>One upgrade attempt, before the game/install lease. No game argv is passed to UAC.</summary>
public static class WindowsBrokerUpgradeStartup
{
    public static bool Pending(BrokerUpgradeState? state)=>state is {Phase:not("committed" or "restored")};
    public static async Task Ensure(Func<BrokerUpgradeState?> status,Func<int> reader,Func<Task<int>> elevate)
    {
        var state=status();if(!Pending(state)&&reader()>=ReleaseCompatibility.ReaderProtocol)return;
        int result=await elevate();
        var after=status();if(!Pending(after)&&reader()>=ReleaseCompatibility.ReaderProtocol)return; // Another actor/commit can finish even if this invocation fails.
        if(result!=0)throw new IOException(result==1223?"Обновление компонента отменено. Игра не запускалась; повторите запуск, когда будете готовы.":"Компонент обновления не установлен. Повторите запуск игры; подробности сохранены в журнале.");
        throw new IOException("Не подтверждена готовность компонента обновления.");
    }
    public static BrokerUpgradeCandidate Retained(string root,string hash,string key,LauncherConfig identity)
    {
        if(!Regex.IsMatch(hash,@"\A[a-f0-9]{64}\z"))throw new IOException("Invalid retained actor identity");
        var path=Path.Combine(root,"maintenance","broker-upgrade-hosts",hash);var config=JsonSerializer.Deserialize<LauncherConfig>(BrokerUpgradeCandidate.ReadBounded(Path.Combine(path,"maintenance.json"),65536),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new IOException("Retained actor identity missing");
        var candidate=BrokerUpgradeCandidate.Load(path,key,config);
        if(candidate.ManifestHash!=hash||config.AppId!=identity.AppId||config.UpdateChannel!=identity.UpdateChannel)throw new IOException("Foreign retained upgrade actor");return candidate;
    }
    static bool RepairableRead(Exception e)=>e is IOException or System.Security.Cryptography.CryptographicException or JsonException or FormatException or InvalidOperationException or ArgumentException;
    public static BrokerUpgradeCandidate SelectActor(string root,string key,LauncherConfig payload,BrokerUpgradeState? state)
    {
        BrokerUpgradeCandidate Current()=>BrokerUpgradeCandidate.Load(Path.Combine(root,"current","broker-upgrade"),key,payload);
        if(Pending(state))
        {try{return Retained(root,state!.ManifestHash,key,payload);}catch(Exception error)when(RepairableRead(error)){return Current();}}
        try{return Current();}
        catch(Exception error)when(state is {Phase:"restored"}&&RepairableRead(error))
        {var actor=Retained(root,state.ManifestHash,key,payload);if(actor.Payload!=payload)throw new IOException("No signed candidate for current payload");return actor;}
    }
    public static async Task EnsureWindows(string root,LauncherConfig payload)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        WindowsBrokerUpgradeAccess.RequireNormalUser();root=WindowsBrokerUpgradeAccess.Root(root);
        if(File.Exists(Path.Combine(root,"maintenance","installing.flag"))||!WindowsInstallation.InstallerAllowsLaunch(root))throw new IOException("Finish or recover the installer before upgrading maintenance");
        BrokerUpgradeState? Status()=>BrokerUpgradeStateStore.Read(root,payload);
        await Ensure(Status,()=>WindowsInstallation.MetadataReader(root),async()=>
        {
            if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException(); // Guard the deferred native callback too.
            var key=System.Text.Encoding.UTF8.GetString(BrokerUpgradeCandidate.ReadBounded(Path.Combine(root,"maintenance","update-public.pem"),65536));
            var state=Status();var actor=SelectActor(root,key,payload,state);
            WindowsBrokerUpgradeAccess.Candidate(actor);
            var image=Path.Combine(root,"current","StarTournamentLauncher.exe");WindowsBrokerUpgradeAccess.FilePath(image);
            if(!string.Equals(Path.GetFullPath(Environment.ProcessPath!),image,StringComparison.OrdinalIgnoreCase))throw new UnauthorizedAccessException("Exact current launcher required for migration");
            using var input=new FileStream(image,FileMode.Open,FileAccess.Read,FileShare.Read);
            var hash=Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();using var self=Process.GetCurrentProcess();
            WindowsBrokerUpgradeAccess.ClearRuntimeEnvironment();
            var start=new ProcessStartInfo(Path.Combine(actor.DirectoryPath,"StarTournamentMaintenance.exe")){UseShellExecute=true,Verb="runas",WorkingDirectory=root};
            foreach(var arg in new[]{"--upgrade-broker",root,self.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),self.StartTime.ToUniversalTime().Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture),hash})start.ArgumentList.Add(arg);
            try{using var child=Process.Start(start)??throw new IOException("Broker upgrade actor did not start");await child.WaitForExitAsync();return child.ExitCode;}
            catch(System.ComponentModel.Win32Exception error)when(error.NativeErrorCode==1223){return 1223;}
        });
    }
}
