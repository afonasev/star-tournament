using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Velopack;

namespace StarTournament.Distribution;

public sealed record BrokerUpgradeFile(string Name, long Size, string Sha256);

/// <summary>Authenticates a bounded, separately signed candidate; never executes it.
/// Callers separately establish protected paths/ACLs, elevation and exact live authority.</summary>
public sealed class BrokerUpgradeCandidate
{
    public static readonly IReadOnlyList<string> FileNames=Array.AsReadOnly(new[]{"StarTournamentMaintenance.exe","maintenance.json","update-public.pem"});
    public const string EnvelopeName="broker-upgrade.json";
    readonly string trustedKey;
    readonly IReadOnlyDictionary<string,BrokerUpgradeFile> files;
    public string DirectoryPath {get;}
    public string ManifestHash {get;}
    public LauncherConfig Payload {get;}
    public IReadOnlyDictionary<string,BrokerUpgradeFile> Files=>files;
    BrokerUpgradeCandidate(string directory,string key,string digest,LauncherConfig payload,Dictionary<string,BrokerUpgradeFile> files)
    {DirectoryPath=directory;trustedKey=key;ManifestHash=digest;Payload=payload;this.files=new ReadOnlyDictionary<string,BrokerUpgradeFile>(files);}

    public static BrokerUpgradeCandidate Load(string directory,string trustedKey,LauncherConfig payload)
    {
        directory=Path.GetFullPath(directory);Plain(directory);
        if(payload.AppId!="tech.afonasev.star-tournament.win-x64"||payload.Platform!="win"||payload.Architecture!="x64"||
            payload.GameExecutable!="game/StarTournament.exe"||payload.UpdateChannel is not("test" or "production")||
            payload.FeedBase!=(payload.UpdateChannel=="test"?AuthenticatedSource.TestLocator:AuthenticatedSource.ProductionBase)||
            !SemanticVersion.TryParse(payload.Version,out var version)||version.ToString()!=payload.Version||version.IsPrerelease!=(payload.UpdateChannel=="test")||
            !DateOnly.TryParseExact(payload.PublicationDate,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _)||payload.Compatibility is not{MinimumUpdaterProtocol:2,PayloadFormat:ReleaseCompatibility.PayloadFormat,UserDataPolicy:ReleaseCompatibility.DataPolicy})
            throw new CryptographicException("Unsupported broker candidate payload identity");
        ReleaseCompatibility.Validate(payload.Compatibility,"win");
        var names=Directory.EnumerateFileSystemEntries(directory).Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal);
        if(!names.SetEquals(FileNames.Append(EnvelopeName)))throw new CryptographicException("Exact broker candidate file set required");
        var raw=ReadBounded(Path.Combine(directory,EnvelopeName),65536);
        using var envelope=JsonDocument.Parse(raw);Fields(envelope.RootElement,"payload","signature");
        var body=Convert.FromBase64String(envelope.RootElement.GetProperty("payload").GetString()!);
        var signature=Convert.FromBase64String(envelope.RootElement.GetProperty("signature").GetString()!);
        using var rsa=RSA.Create();rsa.ImportFromPem(trustedKey);
        if(!rsa.VerifyData(body,signature,HashAlgorithmName.SHA256,RSASignaturePadding.Pss))throw new CryptographicException("Invalid broker candidate signature");
        using var json=JsonDocument.Parse(body);var data=json.RootElement;
        Fields(data,"schema","appId","platform","architecture","version","publicationDate","channel","protocol","metadataReader","files");
        if(data.GetProperty("schema").GetInt32()!=1||data.GetProperty("appId").GetString()!=payload.AppId||data.GetProperty("platform").GetString()!="win"||
            data.GetProperty("architecture").GetString()!="x64"||data.GetProperty("version").GetString()!=payload.Version||data.GetProperty("publicationDate").GetString()!=payload.PublicationDate||
            data.GetProperty("channel").GetString()!=payload.UpdateChannel||data.GetProperty("protocol").GetInt32()!=2||data.GetProperty("metadataReader").GetInt32()!=2)
            throw new CryptographicException("Signed broker identity/capability differs from current payload");
        var files=new Dictionary<string,BrokerUpgradeFile>(StringComparer.Ordinal);
        foreach(var row in data.GetProperty("files").EnumerateArray())
        {
            Fields(row,"name","size","sha256");var name=row.GetProperty("name").GetString()!;var size=row.GetProperty("size").GetInt64();var hash=row.GetProperty("sha256").GetString()!;
            if(!FileNames.Contains(name,StringComparer.Ordinal)||size<=0||size>(name.EndsWith(".exe",StringComparison.Ordinal)?128*1024*1024:65536)||
                !Regex.IsMatch(hash,@"\A[a-f0-9]{64}\z")||!files.TryAdd(name,new(name,size,hash)))throw new CryptographicException("Invalid fixed broker file identity");
        }
        if(files.Count!=3)throw new CryptographicException("Incomplete broker candidate");
        var result=new BrokerUpgradeCandidate(directory,trustedKey,Hash(raw),payload,files);result.VerifyFiles();
        var contract=ReadBounded(Path.Combine(directory,"maintenance.json"),65536);
        using var config=JsonDocument.Parse(contract);Fields(config.RootElement,"protocol","metadataReader","appId","platform","architecture","version","publicationDate","feedBase","gameExecutable","updateChannel","compatibility");
        if(config.RootElement.GetProperty("protocol").GetInt32()!=2||config.RootElement.GetProperty("metadataReader").GetInt32()!=2||
            JsonSerializer.Deserialize<LauncherConfig>(contract,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!=payload)
            throw new CryptographicException("Broker contract differs from signed current identity");
        using var candidateKey=RSA.Create();candidateKey.ImportFromPem(Encoding.UTF8.GetString(ReadBounded(Path.Combine(directory,"update-public.pem"),65536)));
        if(!rsa.ExportSubjectPublicKeyInfo().SequenceEqual(candidateKey.ExportSubjectPublicKeyInfo()))throw new CryptographicException("Candidate cannot rotate the installed trust anchor");
        return result;
    }
    public void Reverify()
    {var current=Load(DirectoryPath,trustedKey,Payload);if(current.ManifestHash!=ManifestHash)throw new CryptographicException("Frozen broker candidate changed");}
    public void VerifyFile(string path,string name)
    {
        if(!files.TryGetValue(name,out var expected))throw new CryptographicException("Unknown broker file");Plain(path);
        using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(input.Length!=expected.Size||Hash(SHA256.HashData(input),alreadyDigest:true)!=expected.Sha256)throw new CryptographicException("Broker file differs from signed identity");
    }
    void VerifyFiles(){foreach(var name in FileNames)VerifyFile(Path.Combine(DirectoryPath,name),name);}
    internal static byte[] ReadBounded(string path,int limit)
    {Plain(path);using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);if(input.Length>limit)throw new IOException("Broker metadata exceeds limit");using var output=new MemoryStream();var buffer=new byte[4096];int n;while((n=input.Read(buffer))>0){if(output.Length+n>limit)throw new IOException("Broker metadata exceeds limit");output.Write(buffer,0,n);}return output.ToArray();}
    internal static void Fields(JsonElement data,params string[] expected)
    {var fields=data.EnumerateObject().Select(p=>p.Name).ToArray();if(fields.Length!=expected.Length||!fields.ToHashSet(StringComparer.Ordinal).SetEquals(expected))throw new CryptographicException("Unknown/duplicate broker contract fields");}
    internal static string Hash(byte[] bytes,bool alreadyDigest=false)=>Convert.ToHexString(alreadyDigest?bytes:SHA256.HashData(bytes)).ToLowerInvariant();
    internal static void Plain(string path)
    {for(string? p=path;p!=null;p=Path.GetDirectoryName(p)){try{if((File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new IOException("Broker path contains a reparse point");}catch(FileNotFoundException){}catch(DirectoryNotFoundException){}}}
}
