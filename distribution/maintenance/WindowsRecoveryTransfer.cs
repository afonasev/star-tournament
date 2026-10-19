using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StarTournament.Distribution;

public sealed partial class WindowsRecoveryStore
{
    /// <summary>Caller holds the elevated install lease/idle guards. Only the fixed sibling stage
    /// is writable. The logical root and PayloadSnapshot.InstallPath stay the final installation.</summary>
    internal static IDisposable TransferForInstaller(string installation,LauncherConfig candidate,Action<string> mkdir,
        Func<string,FileStream> create,Func<string,FileStream> ready,Action<string>? checkpoint=null,Func<long>? availableBytes=null)
    {
        var root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(installation));
        var stage=Path.Combine(root+".installer","stage");Plain(stage);
        var sourcePath=Path.Combine(root,"maintenance","recovery","state.json");
        if(!Directory.Exists(root))return new EmptyLease();
        var current=Path.Combine(root,"current");var sourceDirectory=Path.Combine(root,"maintenance","recovery");Plain(sourceDirectory);
        WindowsRecoveryStore? source=null;
        if(File.Exists(sourcePath))source=new WindowsRecoveryStore(root,candidate,mkdir,create,ready);
        else if(Directory.Exists(sourceDirectory)&&Directory.EnumerateFileSystemEntries(sourceDirectory).Any(p=>
            Directory.Exists(p)||!Regex.IsMatch(Path.GetFileName(p),@"\Astate\.json\.next-[0-9a-f]{32}\z")))
            throw new IOException("Recovery data exists without its journal; preserve it before installing");
        var state=source?.Read()??new WindowsRecoveryState(1,StartupRecovery.InstallationId(root),candidate.AppId,[],null);var attempt=state.Attempt;
        var installedVersion=attempt==null?WindowsInstallerPlan.Inspect(root,candidate).InstalledVersion:
            attempt.Phase=="installed"?attempt.Target:attempt.Previous.Version;
        if(installedVersion==null)throw new IOException("Previous installed version is missing");
        if(attempt is {Phase:"capturing" or "prepared" or "restoring"})throw new IOException("Finish interrupted update recovery before transferring its backup");
        FileStream? latch=null;
        try
        {
            bool floor=false;
            if(attempt is {Phase:"installed"})
            {
                var path=source!.ReadyPath(attempt.Id);Plain(path);
                try{latch=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete);floor=latch.Length!=0;}
                catch(FileNotFoundException){floor=true;} // Missing latch is conservative: never reopen an older rollback.
            }
            bool same=candidate.Version==installedVersion;
            bool reuse=attempt is {Phase:"installed"}&&!floor;
            if(reuse){source!.payload.Verify(attempt!.Id,current);source.VerifyBootstrap(attempt);}
            bool fresh=!same&&!reuse;
            if(attempt is {Phase:"installed"}&&Velopack.SemanticVersion.Parse(candidate.Version)<Velopack.SemanticVersion.Parse(attempt.Target))throw new IOException("Installer cannot go below the retained target");
            var retained=(state.Retained??[]).ToList();
            if(fresh&&attempt!=null){retained.Add(attempt.Id);if(retained.Count>256)throw new IOException("Confirm the current startup before accumulating more installer backups");}
            if(source==null&&same)return new EmptyLease();
            long required=checked((source!=null?TreeBytes(source.store,metadata:false):0)+131072);
            if(fresh)
            {
                required=checked(required+TreeBytes(current,metadata:true)+131072);
                foreach(var name in Names)required=checked(required+new FileInfo(Path.Combine(root,name)).Length+512);
            }
            long free=availableBytes?.Invoke()??new DriveInfo(Path.GetPathRoot(stage)!).AvailableFreeSpace;
            if(free<required)throw new IOException("Недостаточно места для сохранения резервной версии. Освободите место и повторите установку.");
            checkpoint?.Invoke("transfer-space-checked");
            var destinationPath=Path.Combine(stage,"maintenance","recovery");Plain(destinationPath);
            if(Directory.Exists(destinationPath)||File.Exists(destinationPath))throw new IOException("Candidate unexpectedly contains a recovery store");
            if(source!=null)CopyTree(source.store,destinationPath,mkdir,create,ready);else mkdir(destinationPath);
            checkpoint?.Invoke("transfer-copied");
            var destination=new WindowsRecoveryStore(root,candidate,mkdir,create,ready,destinationPath);
            if(attempt==null&&!fresh) { destination.Write(state); }
            else if(same&&attempt!=null)
            {
                // Preserve installed floor/pending semantics and partial cleanup journals as-is.
                if(attempt.Phase=="installed"&&floor)
                {
                    var path=destination.ReadyPath(attempt.Id);Plain(path);
                    using var output=File.Exists(path)?new FileStream(path,FileMode.Append,FileAccess.Write,FileShare.Read):ready(path);
                    output.WriteByte(1);output.Flush(true);
                }
                destination.Write(state);
                if(reuse){destination.payload.Verify(attempt.Id,current);destination.VerifyBootstrap(attempt);}
            }
            else if(reuse)
            {
                // The original known-good payload is still the fallback for this newer target.
                var changed=attempt! with{Target=candidate.Version,Phase="prepared"};
                destination.Write(state with{Attempt=changed});
                destination.payload.Verify(attempt!.Id,current);destination.VerifyBootstrap(attempt);
            }
            else
            {
                // Keep every original backup until a real startup attestation authorizes cleanup.
                // The new fallback cannot be below the existing confirmed/conservative floor.
                var currentVersion=installedVersion;
                LauncherConfig config;
                try{config=JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(Path.Combine(current,"release.json")),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new IOException("Current identity missing");}
                catch(JsonException error){throw new IOException("Не удаётся сохранить текущую версию как резервную. Сначала восстановите её установщиком той же версии.",error);}
                if(config.Version!=currentVersion)throw new IOException("Current release differs from the retained floor");
                destination.Write(state with{Attempt=null,Retained=null});
                destination.Prepare(config,candidate.Version,explicitRetry:true);
                destination.Write(destination.Read() with{Retained=retained.Count==0?null:retained.ToArray()});
                checkpoint?.Invoke("transfer-new-floor-captured");
            }
            checkpoint?.Invoke("transfer-prepared");
            var result=latch;latch=null;return result is null?new EmptyLease():result;
        }
        finally{latch?.Dispose();}
    }
    internal void CompleteInstallerTransfer()
    {
        var state=Read();if(state.Attempt is not {Phase:"prepared"} attempt)return;
        payload.Verify(attempt.Id,current);VerifyBootstrap(attempt);AssertConfig(attempt.Previous,attempt.Target);
        Write(state with{Attempt=attempt with{Phase="installed"}});
    }
    static long TreeBytes(string directory,bool metadata,string? relativeRoot=null)
    {
        Plain(directory);relativeRoot??=directory;long bytes=0;
        foreach(var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            Plain(entry);if(Directory.Exists(entry))bytes=checked(bytes+TreeBytes(entry,metadata,relativeRoot));
            else bytes=checked(bytes+(Regex.IsMatch(Path.GetFileName(entry),@"\A[0-9a-f]{32}\.ready\z")?1:new FileInfo(entry).Length));
            if(metadata)bytes=checked(bytes+512+Encoding.UTF8.GetByteCount(Path.GetRelativePath(relativeRoot,entry))*6L);
        }
        return bytes;
    }
    static void CopyTree(string source,string destination,Action<string> mkdir,Func<string,FileStream> create,Func<string,FileStream> ready)
    {
        Plain(source);Plain(destination);mkdir(destination);
        foreach(var entry in Directory.EnumerateFileSystemEntries(source))
        {
            Plain(entry);var target=Path.Combine(destination,Path.GetFileName(entry));
            if(Directory.Exists(entry))CopyTree(entry,target,mkdir,create,ready);
            else if(Regex.IsMatch(Path.GetFileName(entry),@"\A[0-9a-f]{32}\.ready\z"))
            {
                using var input=new FileStream(entry,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete);
                using var output=ready(target);if(input.Length!=0)output.WriteByte(1);output.Flush(true);
            }
            else
            {
                using var input=new FileStream(entry,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete);
                using var output=create(target);input.CopyTo(output);output.Flush(true);
            }
            if(!OperatingSystem.IsWindows())File.SetUnixFileMode(target,File.GetUnixFileMode(entry));
        }
        if(!OperatingSystem.IsWindows())File.SetUnixFileMode(destination,File.GetUnixFileMode(source));
    }
    sealed class EmptyLease:IDisposable {public void Dispose(){} }
}
