using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Velopack;
namespace StarTournament.Distribution;

public sealed record BrokerPreviousFile(string Name,long Size,string Sha256);
public sealed record BrokerUpgradeState(int Schema,string RootId,string AppId,string Version,string Channel,string Id,string ManifestHash,string Phase,BrokerPreviousFile[] Previous);

/// <summary>Read-only routing. This does not authorize protected mutations.</summary>
public static class BrokerUpgradeStateStore
{
    public static string Slot(string root)=>Path.Combine(root,"maintenance","broker-upgrade-v1");
    public static BrokerUpgradeState? Read(string root,LauncherConfig payload)
    {
        root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));var slot=Slot(root);var statePath=Path.Combine(slot,"state.json");
        var Json=new JsonSerializerOptions{UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
        var Names=new[]{"broker","contract","entry"};

        Plain(statePath);if(File.Exists(slot))throw new IOException("Broker upgrade storage is a file");if(!File.Exists(statePath)){if(Directory.Exists(slot)&&Directory.EnumerateFileSystemEntries(slot).Any())throw new IOException("Unowned broker upgrade storage");return null;}
        var state=JsonSerializer.Deserialize<BrokerUpgradeState>(BrokerUpgradeCandidate.ReadBounded(statePath,65536),Json)??throw new IOException("Broker upgrade state missing");
        if(state.Schema!=1||state.RootId!=StartupRecovery.InstallationId(root)||state.AppId!=payload.AppId||(!SemanticVersion.TryParse(state.Version,out var version)||version.ToString()!=state.Version||version.IsPrerelease!=(payload.UpdateChannel=="test"))||state.Channel!=payload.UpdateChannel||
            !Regex.IsMatch(state.Id??"",@"\A[a-f0-9]{32}\z")||!Digest(state.ManifestHash)||state.Phase is not("capturing" or "prepared" or "replacing" or "restoring" or "restored" or "committed")||
            state.Previous==null||state.Previous.Length!=3||state.Previous.Any(f=>f==null)||!state.Previous.Select(f=>f.Name).ToHashSet(StringComparer.Ordinal).SetEquals(Names)||
            state.Previous.Any(f=>f.Size<=0||f.Size>(f.Name=="contract"?65536:128*1024*1024)||!Digest(f.Sha256)))throw new IOException("Foreign/damaged broker transaction");
        return state;
    }
    static bool Digest(string? value)=>Regex.IsMatch(value??"",@"\A[a-f0-9]{64}\z");
    static void Plain(string path)=>BrokerUpgradeCandidate.Plain(path);
}
