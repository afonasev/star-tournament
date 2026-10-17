using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Velopack;

namespace StarTournament.Distribution;

public sealed record WindowsInstallerPlan(string Mode, string? InstalledVersion)
{
    public static WindowsInstallerPlan Inspect(string root, LauncherConfig candidate, bool requireCompleteMetadata=false)
    {
        if(!Directory.Exists(root)||!Directory.EnumerateFileSystemEntries(root).Any())return new("fresh",null);
        using(var marker=JsonDocument.Parse(Text(Path.Combine(root,".star-tournament-install.json"))))
            if(marker.RootElement.GetProperty("protocol").GetInt32()!=1||marker.RootElement.GetProperty("product").GetString()!="Star Tournament")throw new IOException("Unknown installation marker");
        string? version=null;
        var path=Path.Combine(root,"current","release.json");
        if(File.Exists(path))
        {
            LauncherConfig? installed=null;
            try{installed=JsonSerializer.Deserialize<LauncherConfig>(Text(path),new JsonSerializerOptions{PropertyNameCaseInsensitive=true});}catch(JsonException){}
            if(installed!=null)
            {
                if(installed.AppId!=candidate.AppId||installed.Platform!="win"||installed.Architecture!="x64"||installed.UpdateChannel!=candidate.UpdateChannel)throw new IOException("Another platform, application or channel is installed");
                version=Version(installed.Version);
            }
        }
        var sq=Path.Combine(root,"current","sq.version");
        if(File.Exists(sq))
        {
            string? native=null;
            try
            {
                var xml=Text(sq);using var reader=XmlReader.Create(new StringReader(xml),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=65536});
                var document=XDocument.Load(reader);var metadata=document.Root?.Elements().SingleOrDefault(e=>e.Name.LocalName=="metadata");
                string? Field(string name)=>metadata?.Elements().SingleOrDefault(e=>e.Name.LocalName==name)?.Value;
                if(Field("id")!=candidate.AppId||Field("os")!="win"||Field("rid")!="win-x64")throw new IOException("Foreign native installation identity");
                native=Version(Field("version"));
            }
            catch(XmlException) when(version!=null&&!requireCompleteMetadata) { /* Damaged redundant metadata can be repaired from release.json. */ }
            if(native!=null)
            {if(version!=null&&version!=native)throw new IOException("Installed version records disagree");version=native;}
        }
        if(version==null)throw new IOException("Cannot establish the installed version; repair its retained metadata before replacing files");
        var installedVersion=SemanticVersion.Parse(version);var target=SemanticVersion.Parse(Version(candidate.Version));
        if(installedVersion.IsPrerelease!=(candidate.UpdateChannel=="test"))throw new IOException("Installer channel differs from the installed release");
        if(target<installedVersion)throw new IOException("Installer downgrade is not allowed");
        return new(target==installedVersion?"repair":"upgrade",version);
    }
    static string Version(string? value)=>value!=null&&value.Length<=128&&SemanticVersion.TryParse(value,out var parsed)&&parsed.ToString()==value?value:throw new IOException("Invalid installed version");
    static string Text(string path)
    {
        for(string? part=path;part!=null;part=Path.GetDirectoryName(part))
            if((File.GetAttributes(part)&FileAttributes.ReparsePoint)!=0)throw new IOException("Reparse point in installation identity path");
        if(new FileInfo(path).Length>65536)throw new IOException("Unsafe installation identity file");
        return File.ReadAllText(path);
    }
}
