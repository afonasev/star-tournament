using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Velopack;

namespace StarTournament.Distribution;

/// <summary>Full win-x64 payload staging only; installer-owned root files never execute or change.
/// Caller must hold the protected installation lease, validate ACLs and exact idle processes,
/// and invoke Activate inside WindowsRecoveryStore.Apply after its durable snapshot.</summary>
public sealed class WindowsFullPayload
{
    readonly string directory;
    readonly Dictionary<string, string> files;
    readonly Action<string> checkpoint;
    public LauncherConfig Target { get; }
    WindowsFullPayload(string directory, LauncherConfig target, Dictionary<string,string> files, Action<string> checkpoint)
    { this.directory=directory; Target=target; this.files=files; this.checkpoint=checkpoint; }

    public static async Task<WindowsFullPayload> Stage(AuthenticatedSource source, VelopackAsset asset, string package, string destination,
        Action<string>? mkdir=null, Func<string,FileStream>? create=null, Action<string>? checkpoint=null)
    {
        var admitted=source.Asset;
        if(admitted==null || admitted.PackageId!=asset.PackageId || admitted.Version!=asset.Version || admitted.FileName!=asset.FileName ||
            admitted.Size!=asset.Size || admitted.SHA256!=asset.SHA256) throw new CryptographicException("Staging must use the admitted target");
        source.RequireCompatibleTarget();
        destination=Path.GetFullPath(destination); Plain(destination);
        if(Directory.Exists(destination)||File.Exists(destination))throw new IOException("Payload stage already exists");
        // Hold the exact package against write/replacement while hashing, parsing and extracting.
        using var held=new FileStream(package,FileMode.Open,FileAccess.Read,FileShare.Read);
        await source.VerifyPackage(package,asset);
        using var archive=new ZipArchive(held,ZipArchiveMode.Read,leaveOpen:true);
        if(archive.Entries.Count is <1 or >200000)throw new IOException("Unsupported payload entry count");
        var mapped=new Dictionary<string,ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        var dirs=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var archiveNames=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var containerFiles=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var containerDirs=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ZipArchiveEntry? manifest=null;
        long total=0;
        foreach(var entry in archive.Entries)
        {
            var raw=entry.FullName;var isDirectory=raw.EndsWith('/');var name=isDirectory?raw[..^1]:raw;
            SafeName(name);
            if(!archiveNames.Add(name)||((entry.ExternalAttributes>>16)&0xf000)==0xa000 || (entry.ExternalAttributes&0x400)!=0 ||
                name.EndsWith(".__symlink",StringComparison.OrdinalIgnoreCase))throw new IOException("Duplicate, link or reparse archive entry");
            if(isDirectory)containerDirs.Add(name);else containerFiles.Add(name);
            for(var parent=Path.GetDirectoryName(name);!string.IsNullOrEmpty(parent);parent=Path.GetDirectoryName(parent))containerDirs.Add(parent.Replace('\\','/'));
            total=checked(total+entry.Length);if(total>16L*1024*1024*1024)throw new IOException("Unpacked payload exceeds limit");
            if(!name.Contains('/')&&name.EndsWith(".nuspec",StringComparison.OrdinalIgnoreCase))
            {if(isDirectory||manifest!=null)throw new IOException("Unique package manifest required");manifest=entry;continue;}
            if(!name.StartsWith("lib/",StringComparison.Ordinal))continue; // NuGet container metadata, never a write path.
            if(name=="lib"||name=="lib/app")continue;
            if(!name.StartsWith("lib/app/",StringComparison.Ordinal))throw new IOException("Unsupported payload framework");
            var relative=name[8..];SafeName(relative);
            if(relative.Equals("Squirrel.exe",StringComparison.OrdinalIgnoreCase)||relative.EndsWith("_ExecutionStub.exe",StringComparison.OrdinalIgnoreCase))continue;
            if(isDirectory)dirs.Add(relative);
            else if(!mapped.TryAdd(relative,entry))throw new IOException("Payload path collision");
            for(var parent=Path.GetDirectoryName(relative);!string.IsNullOrEmpty(parent);parent=Path.GetDirectoryName(parent))dirs.Add(parent.Replace('\\','/'));
        }
        if(manifest==null||mapped.Count==0||mapped.Keys.Any(dirs.Contains)||containerFiles.Any(containerDirs.Contains))throw new IOException("Incomplete or colliding payload namespace");
        if(!mapped.TryGetValue("release.json",out var release)||!mapped.ContainsKey("StarTournamentLauncher.exe")||!mapped.ContainsKey("game/StarTournament.exe"))
            throw new IOException("Complete fixed launcher and Player payload required");
        var config=JsonSerializer.Deserialize<LauncherConfig>(Read(release),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new IOException("Payload config missing");
        if(config.Platform!="win"||config.Architecture!="x64"||config.PublicationDate!=source.PublicationDate||config.AppId!=asset.PackageId||config.Version!=asset.Version.ToString()||
            config.GameExecutable!="game/StarTournament.exe"||config.UpdateChannel!=(asset.Version.IsPrerelease?"test":"production")||
            config.FeedBase!=(config.UpdateChannel=="test"?AuthenticatedSource.TestLocator:AuthenticatedSource.ProductionBase))throw new IOException("Unsupported full payload identity");
        using(var envelope=JsonDocument.Parse(source.Envelope!))
        using(var signed=JsonDocument.Parse(Convert.FromBase64String(envelope.RootElement.GetProperty("payload").GetString()!)))
            if(signed.RootElement.GetProperty("schema").GetInt32()==1&&config.Compatibility!=null)
                throw new IOException("Historical schema1 payload cannot silently introduce new requirements");
        var manifestBytes=Read(manifest);ValidateManifest(manifestBytes,config);
        if(mapped.TryGetValue("sq.version",out var native))ValidateManifest(Read(native),config);
        // Package metadata and target config have now passed before any stage file is created.
        mkdir??=p=>Directory.CreateDirectory(p);create??=p=>new FileStream(p,FileMode.CreateNew,FileAccess.Write,FileShare.None);
        mkdir(destination);
        var hashes=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(var dir in dirs.OrderBy(p=>p.Count(c=>c=='/')))mkdir(Path.Combine(destination,dir));
        var copyBuffer=new byte[128*1024];
        foreach(var (relative,entry) in mapped)
        {
            var path=Path.Combine(destination,relative);Plain(path);
            using var input=entry.Open();using(var output=create(path))
            {
                long written=0;int count;
                while((count=input.Read(copyBuffer))>0)
                {if(written+count>entry.Length)throw new IOException("Payload entry exceeds declared size");output.Write(copyBuffer,0,count);written+=count;}
                if(written!=entry.Length)throw new IOException("Truncated payload entry");output.Flush(true);
            }
            hashes.Add(relative,Hash(path));
        }
        if(!mapped.ContainsKey("sq.version"))
        {
            using(var output=create(Path.Combine(destination,"sq.version"))){output.Write(manifestBytes);output.Flush(true);}
            hashes.Add("sq.version",Hash(Path.Combine(destination,"sq.version")));
        }
        foreach(var relative in new[]{"release.json","StarTournamentLauncher.exe","game/StarTournament.exe","sq.version"})
            if(new FileInfo(Path.Combine(destination,relative)).Length==0)throw new IOException("Empty required payload component");
        var result=new WindowsFullPayload(destination,config,hashes,checkpoint??(_=>{}));result.Verify();return result;
    }

    public void Activate(string root)
    {
        root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));Plain(root);
        var prefix=Path.Combine(root,"maintenance","staging")+Path.DirectorySeparatorChar;
        if(!directory.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new IOException("Payload stage must be owned protected staging");
        var current=Path.Combine(root,"current");var previous=Path.Combine(Path.GetDirectoryName(directory)!,"previous-current");
        Plain(current);Plain(previous);Verify();
        if(!Directory.Exists(current)||Directory.Exists(previous)||File.Exists(previous))throw new IOException("Unexpected activation paths");
        Directory.Move(current,previous);checkpoint("current-saved");
        Directory.Move(directory,current);checkpoint("target-activated");
        VerifyAt(current);
        // Previous-current is temporary; WindowsRecoveryStore holds the independent durable snapshot.
    }
    void Verify()=>VerifyAt(directory);
    void VerifyAt(string path)
    {
        Plain(path);var observed=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        void Walk(string dir)
        {
            foreach(var entry in Directory.EnumerateFileSystemEntries(dir))
            {Plain(entry);if(Directory.Exists(entry))Walk(entry);else observed.Add(Path.GetRelativePath(path,entry).Replace('\\','/'),Hash(entry));}
        }
        Walk(path);
        if(observed.Count!=files.Count||files.Any(p=>!observed.TryGetValue(p.Key,out var hash)||hash!=p.Value))throw new IOException("Prepared payload changed");
    }
    static void ValidateManifest(byte[] bytes,LauncherConfig config)
    {
        using var stream=new MemoryStream(bytes);using var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=65536});
        var doc=XDocument.Load(reader);var metadata=doc.Root?.Elements().SingleOrDefault(e=>e.Name.LocalName=="metadata");
        string? Field(string name)=>metadata?.Elements().SingleOrDefault(e=>e.Name.LocalName==name)?.Value;
        if(doc.Root?.Name.LocalName!="package"||doc.Root.Elements().Count()!=1||metadata==null||metadata.Elements().Any(e=>e.HasElements)||Field("id")!=config.AppId||Field("version")!=config.Version||Field("mainExe")!="StarTournamentLauncher.exe"||
            Field("os")!="win"||Field("rid")!="win-x64"||Field("channel")!="win-x64"||Field("machineArchitecture")!="x64"||
            !string.IsNullOrEmpty(Field("runtimeDependencies"))||Field("shortcutLocations") is not(null or "" or "None"))throw new IOException("Unsupported package runtime manifest");
        if(Field("osMinVersion") is {Length:>0} floor && (!Version.TryParse(floor,out var minimum)||minimum>Version.Parse(config.Compatibility?.MinimumOsVersion??"10.0.19043")))
            throw new IOException("Package manifest raises the signed OS floor");
    }
    static byte[] Read(ZipArchiveEntry entry)
    {
        if(entry.Length>65536)throw new IOException("Payload metadata exceeds limit");using var input=entry.Open();using var output=new MemoryStream();
        var buffer=new byte[4096];int n;while((n=input.Read(buffer))>0){if(output.Length+n>65536)throw new IOException("Payload metadata exceeds limit");output.Write(buffer,0,n);}return output.ToArray();
    }
    static void SafeName(string value)
    {
        if(value.Length is <1 or >4096||value.Contains('\\'))throw new IOException("Noncanonical Windows archive path");
        foreach(var part in value.Split('/'))
        {
            var stem=part.Split('.')[0].TrimEnd(' ');
            if(part.Length==0||part is "." or ".."||part.EndsWith('.')||part.EndsWith(' ')||part.Any(c=>c<32||"<>:\"|?*".Contains(c))||
                new[]{"CON","PRN","AUX","NUL","CONIN$","CONOUT$"}.Contains(stem,StringComparer.OrdinalIgnoreCase)||
                (stem.Length==4&&("COM".Equals(stem[..3],StringComparison.OrdinalIgnoreCase)||"LPT".Equals(stem[..3],StringComparison.OrdinalIgnoreCase))&&"123456789¹²³".Contains(stem[3])))throw new IOException("Unsafe Windows archive path");
        }
    }
    static string Hash(string path){using var input=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(input));}
    static void Plain(string path)
    {for(string? p=path;p!=null;p=Path.GetDirectoryName(p)){try{if((File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new IOException("Payload path contains a reparse point");}catch(FileNotFoundException){}catch(DirectoryNotFoundException){}}}
}
