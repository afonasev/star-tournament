using System.Text.Json;
using System.Text.Json.Serialization;

namespace StarTournament.Distribution;

public sealed record InstallerRegistryValue(string Name, string Kind, string? Text=null, int? Number=null);
public sealed record InstallerRegistrySnapshot(int Schema, string RootId, bool Existed, bool Applying, InstallerRegistryValue[] Values);

/// <summary>Durable rollback of this product's fixed registry values. No key/path comes from JSON.</summary>
public sealed class WindowsInstallerRegistration
{
    public static readonly string[] Names=["DisplayName","DisplayVersion","Publisher","DisplayIcon","InstallLocation","UninstallString","NoModify","NoRepair"];
    static readonly JsonSerializerOptions Json=new(){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    readonly string path,rootId;
    readonly Func<string,FileStream> create;
    public WindowsInstallerRegistration(string root,Func<string,FileStream>? create=null)
    {path=Path.TrimEndingDirectorySeparator(Path.GetFullPath(root))+".installer/registration.json";rootId=StartupRecovery.InstallationId(root);this.create=create??(p=>new FileStream(p,FileMode.CreateNew,FileAccess.Write,FileShare.None));}
    public InstallerRegistrySnapshot? Read()
    {
        Plain(path);if(Directory.Exists(path))throw new IOException("Registration snapshot is a directory");if(!File.Exists(path))return null;
        using var input=File.OpenRead(path);if(input.Length>65536)throw new IOException("Installer registration snapshot exceeds limit");
        InstallerRegistrySnapshot value;
        try{value=JsonSerializer.Deserialize<InstallerRegistrySnapshot>(input,Json)??throw new JsonException();}catch(JsonException e){throw new IOException("Invalid installer registration snapshot",e);}
        Validate(value);return value;
    }
    public void Capture(bool existed,InstallerRegistryValue[] values)
    {if(Read()!=null)throw new IOException("Registration snapshot already exists");Write(new(1,rootId,existed,false,values));}
    public InstallerRegistrySnapshot BeforeApply()
    {var value=Read()??throw new IOException("Registration snapshot missing before commit");value=value with{Applying=true};Write(value);return value;}
    public void Replay(Action<string> removeValue,Action<InstallerRegistryValue> restoreValue)
    {
        var snapshot=Read()??throw new IOException("Registration snapshot missing");
        if(!snapshot.Applying)return;
        foreach(var name in Names)removeValue(name);
        foreach(var value in snapshot.Values)restoreValue(value);
        // Snapshot is retained until the caller flushes the actual registry successfully.
    }
    public void Remove(){Plain(path);Plain(path+".next");File.Delete(path);File.Delete(path+".next");}
    void Validate(InstallerRegistrySnapshot value)
    {
        if(value.Schema!=1||value.RootId!=rootId||value.Values==null||value.Values.Length>Names.Length||!value.Existed&&value.Values.Length!=0||
            value.Values.Any(v=>v==null||!Names.Contains(v.Name)||v.Kind is not("String" or "DWord")||
                v.Kind=="String"&&(v.Text==null||v.Text.Length>32768||v.Number!=null)||v.Kind=="DWord"&&(v.Number==null||v.Text!=null))||
            value.Values.Select(v=>v.Name).Distinct().Count()!=value.Values.Length)throw new IOException("Foreign installer registration snapshot");
    }
    void Write(InstallerRegistrySnapshot value)
    {
        Validate(value);var bytes=JsonSerializer.SerializeToUtf8Bytes(value,Json);if(bytes.Length>65536)throw new IOException("Registration snapshot exceeds limit");Plain(path);var next=path+".next";Plain(next);
        try{if(File.Exists(next))File.Delete(next);using(var output=create(next)){output.Write(bytes);output.Flush(true);}File.Move(next,path,true);}
        finally{File.Delete(next);}
    }
    static void Plain(string path)
    {for(string? p=path;p!=null;p=Path.GetDirectoryName(p)){try{if((File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new IOException("Reparse registration snapshot path");}catch(FileNotFoundException){}catch(DirectoryNotFoundException){}}}
}
