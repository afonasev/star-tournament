using System.Security.AccessControl;
using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using StarTournament.Distribution;

static partial class Maintenance
{
    const string ProductKey=@"Software\Microsoft\Windows\CurrentVersion\Uninstall\StarTournament.Native";
    static LauncherConfig InstallerConfig(string version,string root,bool activate)
    {
        if(!IsElevated(Environment.ProcessId))throw new UnauthorizedAccessException("Installer operations require elevation");
        var source=Path.GetDirectoryName(Environment.ProcessPath!)!;
        if(source.Equals(root,StringComparison.OrdinalIgnoreCase)||source.Equals(root+".installer",StringComparison.OrdinalIgnoreCase)||IsBelow(source,root)||IsBelow(source,root+".installer"))throw new IOException("Installer broker must run outside the replaceable installation");
        // NSIS extracts the embedded broker into a unique protected Program Files directory.
        ValidateInstallParents(source);ValidateProtectedTree(source);
        using(var marker=JsonDocument.Parse(ReadBoundedText(Path.Combine(source,".star-tournament-installer-temp.json"),4096)))
            if(marker.RootElement.GetProperty("schema").GetInt32()!=1||marker.RootElement.GetProperty("product").GetString()!="Star Tournament installer"||marker.RootElement.GetProperty("pid").GetInt32()<=0)throw new IOException("Installer source custody marker missing");
        CleanupInstallerSources(source);
        using var data=JsonDocument.Parse(ReadBoundedText(Path.Combine(source,"maintenance.json"),65536));
        var config=JsonSerializer.Deserialize<LauncherConfig>(data.RootElement.GetRawText(),Json)??throw new IOException("Installer identity missing");
        if(data.RootElement.GetProperty("protocol").GetInt32()!=2||config.Platform!="win"||config.Architecture!="x64"||config.Version!=version||config.UpdateChannel is not("production" or "test"))throw new IOException("Installer identity mismatch");
        if(activate&&config.Compatibility!=null)
        {
            var reader=data.RootElement.TryGetProperty("metadataReader",out var declared)?Math.Min(declared.GetInt32(),ReleaseCompatibility.ReaderProtocol):1;
            ReleaseCompatibility.Require(config.Compatibility,"win",reader);
        }
        RequireSemVer(version);return config;
    }
    static void CleanupInstallerSources(string current)
    {
        var parent=Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        foreach(var directory in Directory.EnumerateDirectories(parent,"*.tmp"))
        {
            if(directory.Equals(current,StringComparison.OrdinalIgnoreCase))continue;
            var marker=Path.Combine(directory,".star-tournament-installer-temp.json");if(!File.Exists(marker))continue;
            try
            {
                ValidateProtectedTree(directory);
                using var data=JsonDocument.Parse(ReadBoundedText(marker,4096));var value=data.RootElement;
                if(value.GetProperty("schema").GetInt32()!=1||value.GetProperty("product").GetString()!="Star Tournament installer")continue;
                int pid=value.GetProperty("pid").GetInt32();if(pid<=0)continue;
                try{using var owner=Process.GetProcessById(pid);if(!owner.HasExited)continue;}
                catch(ArgumentException){}catch(InvalidOperationException){}
                // The NSIS parent may be gone while its protected broker is still finishing.
                CheckNoProcesses(directory);Directory.Delete(directory,true);
            }
            catch(Exception error) when(error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or System.ComponentModel.Win32Exception or KeyNotFoundException or FormatException)
            {UpdateFeedback.Log("installer-source-cleanup-retained",error);}
        }
    }
    static void RegisteredRoot(string root,bool recovering=false)
    {
        using var machine=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64);
        using var key=machine.OpenSubKey(ProductKey);if(key==null)return;
        var location=key.GetValue("InstallLocation",null,RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
        if(string.IsNullOrEmpty(location))
        {if(recovering)return;throw new IOException("Existing installation registration has no verified location; restore it before continuing");}
        if(!Path.GetFullPath(location).TrimEnd('\\').Equals(root.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase))throw new IOException("Another Star Tournament directory is registered; use its installer location or uninstall it first");
    }
    static void InstallerTree(string root)
    {
        if(!Directory.Exists(root))return;ValidateProtectedDirectory(root);
        foreach(var entry in new DirectoryInfo(root).EnumerateFileSystemInfos())
        {
            if((entry.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Installer tree contains a reparse point");
            if(entry is DirectoryInfo){InstallerTree(entry.FullName);continue;}
            var acl=((FileInfo)entry).GetAccessControl();if(!TrustedOwner(acl.GetOwner(typeof(SecurityIdentifier))))throw new UnauthorizedAccessException("Installer input owner is not trusted");
            bool latch=Regex.IsMatch(entry.Name,@"\A[0-9a-f]{32}\.ready\z")&&entry.FullName.EndsWith(Path.Combine("maintenance","recovery",entry.Name),StringComparison.OrdinalIgnoreCase);
            foreach(FileSystemAccessRule rule in acl.GetAccessRules(true,true,typeof(SecurityIdentifier)))
                if(rule.AccessControlType==AccessControlType.Allow&&!TrustedOwner((SecurityIdentifier)rule.IdentityReference)&&
                    (rule.FileSystemRights&(latch?WriteRights&~FileSystemRights.AppendData:WriteRights))!=0)throw new UnauthorizedAccessException("Installer tree is writable outside installer principals");
        }
    }
    static void InstallerRoot(string root)
    {
        ValidateRoot(root);InstallerTree(root);
        var allowed=new HashSet<string>(StringComparer.OrdinalIgnoreCase){".star-tournament-install.json",".portable","app-icon.ico","Star Tournament.exe","Update.exe","Uninstall.exe","current","packages","maintenance"};
        if(Directory.Exists(root)&&Directory.EnumerateFileSystemEntries(root).Any(p=>!allowed.Contains(Path.GetFileName(p))))throw new IOException("Install root contains unknown files; nothing replaced");
    }
    static int InstallerOperation(string action,string raw,string version,int ownerPid=0)
    {
        if(action is "plan" or "begin" or "commit")PlatformRequirements.RequireSupported("win");
        var root=SafeRoot(raw);var config=InstallerConfig(version,root,action is "plan" or "begin" or "commit");ValidateInstallParents(root);
        using var product=WindowsInstallLease.TryAcquire(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"StarTournamentInstallerProduct"))??throw new IOException("Another installer is changing the product registration");
        using var lease=WindowsInstallLease.TryAcquire(root)??throw new IOException("Close Star Tournament before installing");CheckNoProcesses(root);
        Caller? caller=null;
        if(ownerPid!=0)
        {
            using var owner=Process.GetProcessById(ownerPid);
            if(!IsElevated(ownerPid)||!SameSession(ownerPid)||GetProcessUserSid(ownerPid)!=GetProcessUserSid(Environment.ProcessId))throw new UnauthorizedAccessException("Installer owner identity mismatch");
            using var custody=JsonDocument.Parse(ReadBoundedText(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath!)!,".star-tournament-installer-temp.json"),4096));
            if(custody.RootElement.GetProperty("pid").GetInt32()!=ownerPid)throw new UnauthorizedAccessException("Installer source belongs to another window");
            caller=new(ownerPid,owner.StartTime.ToUniversalTime().Ticks);
        }
        if(action is not("plan" or "version")&&caller==null)throw new IOException("Live installer owner required");
        GuardInstallerOwner(root,caller);
        var transaction=new WindowsInstallerTransaction(root,config,CreateRecoveryDirectory,CreateRecoveryFile,checkpoint:p=>{if(p=="transfer-prepared")CheckNoProcesses(root);},createReady:CreateStartupMarker);
        var registration=new WindowsInstallerRegistration(root,CreateRecoveryFile);
        var slot=root+".installer";if(Directory.Exists(slot))InstallerTree(slot);
        if(action=="resume")
        {
            ResumeInstaller(root,transaction,registration);ResumeInterruptedUpdate(root,config);return 0;
        }
        RegisteredRoot(root);
        switch(action)
        {
            case "plan":case "version":
                InstallerRoot(root);var plan=WindowsInstallerPlan.Inspect(root,config);Console.Write(action=="plan"?plan.Mode:plan.InstalledVersion??"");return 0;
            case "begin":
                ResumeInstaller(root,transaction,registration);ResumeInterruptedUpdate(root,config);RegisteredRoot(root);InstallerRoot(root);
                var current=WindowsInstallerPlan.Inspect(root,config);
                transaction.Begin(current.InstalledVersion);WriteInstallerOwner(root,caller!);
                using(var machine=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64))
                using(var key=machine.OpenSubKey(ProductKey))
                {
                    var values=new List<InstallerRegistryValue>();
                    if(key!=null)foreach(var name in WindowsInstallerRegistration.Names)
                    {
                        if(!key.GetValueNames().Contains(name,StringComparer.OrdinalIgnoreCase))continue;
                        var kind=key.GetValueKind(name);var value=key.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames);
                        values.Add(kind switch{RegistryValueKind.String=>new(name,"String",(string)value!),RegistryValueKind.DWord=>new(name,"DWord",Number:(int)value!),_=>throw new IOException("Unsupported existing registration type: "+name)});
                    }
                    registration.Capture(key!=null,values.ToArray());
                }
                Console.Write(current.Mode+"|"+(current.InstalledVersion??""));return 0;
            case "commit":
                if(registration.Read()==null)throw new IOException("Registration snapshot missing");
                InstallerTree(transaction.Stage);
                if(ReadBoundedText(Path.Combine(transaction.Stage,"maintenance","installing.flag"),256).Trim()!=config.Version)throw new IOException("Candidate install flag missing or wrong target");
                var staged=transaction.Read()??throw new IOException("Installer transaction missing");
                InstallerRoot(root);var actual=WindowsInstallerPlan.Inspect(root,config);
                if(actual.InstalledVersion!=staged.Previous)throw new IOException("Installed release changed while extracting; repeat the installer");
                transaction.Commit();registration.BeforeApply();WriteRegistration(root,config);return 0;
            case "finalize":
                using(var machine=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64))
                using(var key=machine.OpenSubKey(ProductKey))
                    if(key?.GetValue("DisplayVersion") as string!=config.Version)throw new IOException("Registration readback mismatch");
                try{transaction.FinalizeInstall();}
                catch(Exception error) when(transaction.Read()?.Phase=="committed"&&!File.Exists(Path.Combine(root,"maintenance","installing.flag")))
                {UpdateFeedback.Log("installer-committed-cleanup-pending",error);return 0;}
                registration.Remove();RemoveInstallerOwner(root);transaction.ClearSettled();return 0;
            default:throw new ArgumentException("Unknown installer action");
        }
    }
    static void ResumeInterruptedUpdate(string root,LauncherConfig config)
    {
        if(!File.Exists(Path.Combine(root,"maintenance","recovery","state.json")))return;
        InstallerTree(root);
        var recovery=new WindowsRecoveryStore(root,config,CreateRecoveryDirectory,CreateRecoveryFile,CreateStartupMarker);
        if(recovery.Read().Attempt is {Phase:"capturing" or "prepared" or "restoring"})recovery.Restore();
    }
    static void ResumeInstaller(string root,WindowsInstallerTransaction transaction,WindowsInstallerRegistration registration)
    {
        var state=transaction.Read();if(state==null){if(registration.Read()!=null)throw new IOException("Registration snapshot exists without transaction; no files changed");return;}
        var snapshot=registration.Read();if(snapshot is {Applying:true})RegisteredRoot(root,recovering:true);
        transaction.Recover();
        if(state.Phase!="committed"&&snapshot is {Applying:true})
        {
            using var machine=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64);
            using(var key=machine.CreateSubKey(ProductKey,true))
            {
                registration.Replay(name=>key.DeleteValue(name,false),value=>
                    key.SetValue(value.Name,value.Kind=="String"?(object)value.Text!:value.Number!.Value,value.Kind=="String"?RegistryValueKind.String:RegistryValueKind.DWord));
                key.Flush();
            }
            if(!snapshot.Existed)
            {
                using var key=machine.OpenSubKey(ProductKey);
                if(key!=null&&key.ValueCount==0&&key.SubKeyCount==0){key.Dispose();machine.DeleteSubKey(ProductKey,false);}
            }
        }
        registration.Remove();RemoveInstallerOwner(root);transaction.ClearSettled();
    }
    sealed record InstallerOwner(int Schema,string RootId,int Pid,long Ticks);
    static string OwnerPath(string root)=>Path.Combine(root+".installer","owner.json");
    static void GuardInstallerOwner(string root,Caller? caller)
    {
        var path=OwnerPath(root);if(!File.Exists(path))return;
        var owner=JsonSerializer.Deserialize<InstallerOwner>(ReadBoundedText(path,4096))??throw new IOException("Installer owner missing");
        if(owner.Schema!=1||owner.RootId!=StartupRecovery.InstallationId(root)||owner.Pid<=0||owner.Ticks<=0)throw new IOException("Foreign installer owner");
        if(ExactAlive(new(owner.Pid,owner.Ticks))&&(caller==null||owner.Pid!=caller.Pid||owner.Ticks!=caller.StartedTicks))throw new IOException("Another live installer owns this transaction; close its window before continuing");
    }
    static void WriteInstallerOwner(string root,Caller caller)
    {
        var path=OwnerPath(root);var next=path+".next";
        try{using(var output=CreateRecoveryFile(next)){JsonSerializer.Serialize(output,new InstallerOwner(1,StartupRecovery.InstallationId(root),caller.Pid,caller.StartedTicks));output.Flush(true);}File.Move(next,path,true);}
        finally{File.Delete(next);}
    }
    static void RemoveInstallerOwner(string root)
    {File.Delete(OwnerPath(root));File.Delete(OwnerPath(root)+".next");}
    static void WriteRegistration(string root,LauncherConfig config)
    {
        using var machine=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64);using var key=machine.CreateSubKey(ProductKey,true);
        foreach(var pair in new Dictionary<string,string>{{"DisplayName","Star Tournament"},{"DisplayVersion",config.Version},{"Publisher","Star Tournament"},{"DisplayIcon",Path.Combine(root,"app-icon.ico")},{"InstallLocation",root},{"UninstallString","\""+Path.Combine(root,"Uninstall.exe")+"\""}})key.SetValue(pair.Key,pair.Value,RegistryValueKind.String);
        key.SetValue("NoModify",1,RegistryValueKind.DWord);key.SetValue("NoRepair",1,RegistryValueKind.DWord);key.Flush();
    }
}
