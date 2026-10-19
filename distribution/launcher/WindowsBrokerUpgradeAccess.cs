using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
namespace StarTournament.Distribution;

/// <summary>Read-only preflight repeated by the elevated actor. Never creates a path.</summary>
[SupportedOSPlatform("windows")]
public static class WindowsBrokerUpgradeAccess
{
    const FileSystemRights Writes=FileSystemRights.WriteData|FileSystemRights.AppendData|FileSystemRights.WriteAttributes|FileSystemRights.WriteExtendedAttributes|FileSystemRights.Delete|FileSystemRights.DeleteSubdirectoriesAndFiles|FileSystemRights.ChangePermissions|FileSystemRights.TakeOwnership;
    static bool Trusted(IdentityReference? sid)=>sid!=null&&(sid.Equals(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null))||sid.Equals(new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null))||sid.Equals(new SecurityIdentifier("S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464")));
    static void Acl(FileSystemSecurity acl)
    {
        if(!Trusted(acl.GetOwner(typeof(SecurityIdentifier))))throw new UnauthorizedAccessException("Untrusted broker path owner");
        foreach(FileSystemAccessRule rule in acl.GetAccessRules(true,true,typeof(SecurityIdentifier)))
            if(rule.AccessControlType==AccessControlType.Allow&&(rule.PropagationFlags&PropagationFlags.InheritOnly)==0&&(rule.FileSystemRights&Writes)!=0&&!Trusted(rule.IdentityReference))throw new UnauthorizedAccessException("Broker path is writable by an untrusted principal");
    }
    public static void DirectoryPath(string path){BrokerUpgradeCandidate.Plain(path);Acl(new DirectoryInfo(path).GetAccessControl());}
    public static void FilePath(string path){BrokerUpgradeCandidate.Plain(path);Acl(new FileInfo(path).GetAccessControl());}
    public static string Root(string value)
    {
        if(!Path.IsPathFullyQualified(value))throw new IOException("Absolute install root required");var root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));
        var pf=Path.TrimEndingDirectorySeparator(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        if(!root.StartsWith(pf+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Protected Program Files installation required");
        for(var path=root;;path=Path.GetDirectoryName(path)??throw new IOException("Invalid install ancestor")){DirectoryPath(path);if(path.Equals(pf,StringComparison.OrdinalIgnoreCase))break;}
        var marker=Path.Combine(root,".star-tournament-install.json");FilePath(marker);
        using var json=JsonDocument.Parse(BrokerUpgradeCandidate.ReadBounded(marker,65536));
        if(json.RootElement.GetProperty("protocol").GetInt32()!=1||json.RootElement.GetProperty("product").GetString()!="Star Tournament")throw new IOException("Unknown installation marker");
        DirectoryPath(Path.Combine(root,"maintenance"));FilePath(Path.Combine(root,"maintenance","update-public.pem"));return root;
    }
    public static void Candidate(BrokerUpgradeCandidate candidate)
    {DirectoryPath(candidate.DirectoryPath);foreach(var name in BrokerUpgradeCandidate.FileNames.Append(BrokerUpgradeCandidate.EnvelopeName))FilePath(Path.Combine(candidate.DirectoryPath,name));candidate.Reverify();}
    public static void RequireNormalUser()
    {using var identity=WindowsIdentity.GetCurrent();if(identity.IsSystem||new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))throw new UnauthorizedAccessException("Broker migration must be requested by the normal launcher");}
    public static void ClearRuntimeEnvironment()
    {
        foreach(var key in Environment.GetEnvironmentVariables().Keys.Cast<string>().ToArray())
            if(key.StartsWith("CORECLR_",StringComparison.OrdinalIgnoreCase)||key.StartsWith("COR_",StringComparison.OrdinalIgnoreCase)||key.StartsWith("COMPlus_",StringComparison.OrdinalIgnoreCase)||key.Equals("DOTNET_STARTUP_HOOKS",StringComparison.OrdinalIgnoreCase)||key.Equals("DOTNET_ADDITIONAL_DEPS",StringComparison.OrdinalIgnoreCase)||key.Equals("DOTNET_SHARED_STORE",StringComparison.OrdinalIgnoreCase)||key.StartsWith("DOTNET_PROFILER",StringComparison.OrdinalIgnoreCase))Environment.SetEnvironmentVariable(key,null);
    }
}
