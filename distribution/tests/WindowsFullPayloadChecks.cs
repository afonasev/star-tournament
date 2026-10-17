using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using StarTournament.Distribution;
using Velopack;

static class WindowsFullPayloadChecks
{
    static readonly ReleaseRequirements Requirements=new("10.0.19043",2,ReleaseCompatibility.PayloadFormat,ReleaseCompatibility.DataPolicy);
    static readonly LauncherConfig Old=new("tech.afonasev.star-tournament.win-x64","win","x64","1.0.0","2026-10-17",AuthenticatedSource.ProductionBase,"game/StarTournament.exe","production",Requirements);
    static readonly LauncherConfig Target=Old with{Version="1.1.0"};
    static readonly JsonSerializerOptions Json=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase};
    static int passed;
    static void Require(bool value){if(!value)throw new Exception("Windows full payload assertion failed");}
    sealed class Fixture:IDisposable
    {
        public string Root {get;}=Path.Combine(Directory.GetCurrentDirectory(),".local","windows-full-payload-"+Guid.NewGuid().ToString("N"));
        public Fixture(){Initialize(Root);}
        public void Dispose(){if(Directory.Exists(Root))Directory.Delete(Root,true);}
    }
    static void Initialize(string root)
    {
        Directory.CreateDirectory(Path.Combine(root,"current/game"));Directory.CreateDirectory(Path.Combine(root,"maintenance/staging"));
        File.WriteAllText(Path.Combine(root,"current/release.json"),JsonSerializer.Serialize(Old,Json));
        File.WriteAllText(Path.Combine(root,"current/game/StarTournament.exe"),"old player");
        File.WriteAllText(Path.Combine(root,"current/StarTournamentLauncher.exe"),"old launcher");
        File.WriteAllText(Path.Combine(root,"Update.exe"),"old native updater MUST NOT execute");
        File.WriteAllText(Path.Combine(root,"Star Tournament.exe"),"old stable entry");File.WriteAllBytes(Path.Combine(root,".portable"),[]);
        File.WriteAllText(Path.Combine(root,"balance.json"),"user values 0 false old-default");
    }
    static string Manifest(string version="1.1.0",Action<XElement>? edit=null)
    {
        var xml=XDocument.Load("distribution/tests/fixtures/released-windows-0.1.5.nuspec");var metadata=xml.Root!.Elements().Single();
        metadata.Elements().Single(e=>e.Name.LocalName=="version").Value=version;edit?.Invoke(metadata);return xml.ToString();
    }
    static void Entry(ZipArchive zip,string name,string contents,int attributes=0)
    {var e=zip.CreateEntry(name);e.ExternalAttributes=attributes;using var output=new StreamWriter(e.Open(),new UTF8Encoding(false));output.Write(contents);}
    static async Task<WindowsFullPayload> Stage(string root,Action<ZipArchive>? edit=null,string? manifest=null,LauncherConfig? config=null,Action<string>? checkpoint=null)
    {
        var parent=Path.Combine(root,"maintenance/staging",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(parent);
        var package=Path.Combine(parent,"candidate-full.nupkg");
        using(var zip=ZipFile.Open(package,ZipArchiveMode.Create))
        {
            Entry(zip,"app.nuspec",manifest??Manifest());
            Entry(zip,"lib/app/release.json",JsonSerializer.Serialize(config??Target,Json));
            Entry(zip,"lib/app/StarTournamentLauncher.exe","new launcher");Entry(zip,"lib/app/game/StarTournament.exe","new player");
            Entry(zip,"lib/app/game/UnityPlayer.dll","new runtime");Entry(zip,"lib/app/Squirrel.exe","must not execute");Entry(zip,"lib/app/Star Tournament_ExecutionStub.exe","must not become stable entry");edit?.Invoke(zip);
        }
        var bytes=File.ReadAllBytes(package);var asset=new VelopackAsset{PackageId=Old.AppId,Version=SemanticVersion.Parse(Target.Version),Type=VelopackAssetType.Full,FileName=Path.GetFileName(package),Size=bytes.Length,SHA256=Convert.ToHexString(SHA256.HashData(bytes))};
        var descriptor=new{schema=2,platform="win",architecture="x64",channel="production",version=Target.Version,publicationDate=Target.PublicationDate,compatibility=new{minimumOsVersion=Requirements.MinimumOsVersion,minimumUpdaterProtocol=2,payloadFormat=Requirements.PayloadFormat,userDataPolicy=Requirements.UserDataPolicy},downloadUrl=$"https://github.com/afonasev/star-tournament/releases/download/v{Target.Version}/{asset.FileName}",feed=new{Assets=new[]{new{asset.PackageId,Version=asset.Version.ToString(),Type="Full",asset.FileName,asset.Size,asset.SHA256}}}};
        using var rsa=RSA.Create(2048);var payload=JsonSerializer.SerializeToUtf8Bytes(descriptor);var envelope=JsonSerializer.Serialize(new{payload=Convert.ToBase64String(payload),signature=Convert.ToBase64String(rsa.SignData(payload,HashAlgorithmName.SHA256,RSASignaturePadding.Pss))});
        using var source=new AuthenticatedSource(Old,rsa.ExportSubjectPublicKeyInfoPem(),hostProbe:(_,_)=>true);source.Admit(envelope);
        return await WindowsFullPayload.Stage(source,source.Asset!,package,Path.Combine(parent,"payload"),checkpoint:checkpoint);
    }
    static void AssertOld(string root)
    {
        Require(File.ReadAllText(Path.Combine(root,"current/game/StarTournament.exe"))=="old player");
        Require(JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(Path.Combine(root,"current/release.json")),Json)!.Version==Old.Version);
        AssertRoot(root);
    }
    static void AssertRoot(string root)
    {
        Require(File.ReadAllText(Path.Combine(root,"Update.exe"))=="old native updater MUST NOT execute");
        Require(File.ReadAllText(Path.Combine(root,"Star Tournament.exe"))=="old stable entry");
        Require(File.ReadAllText(Path.Combine(root,"balance.json"))=="user values 0 false old-default");
    }
    static async Task Reject(Func<Task> action)
    {try{await action();}catch(Exception e)when(e is IOException or CryptographicException or System.Xml.XmlException or InvalidOperationException or ArgumentException){return;}throw new Exception("Expected unsafe package rejection");}
    public static async Task Run()
    {
        async Task Test(string name,Func<string,Task> body){using var f=new Fixture();await body(f.Root);passed++;Console.WriteLine("PASS "+name);}
        await Test("actual released manifest provenance",root=>{Require(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes("distribution/tests/fixtures/released-windows-0.1.5.nuspec"))).ToLowerInvariant()=="10d14a72b3282e2bb5436cfbc74f3e5fe470526c2cf264750cfcaf7b6a68adb9");return Task.CompletedTask;});
        await Test("complete current applies without executing updater or touching root/user data",async root=>
        {var stage=await Stage(root);var store=new WindowsRecoveryStore(root,Old);store.Prepare(Old,Target.Version,false);store.Apply(()=>stage.Activate(root));Require(store.Read().Attempt?.Phase=="installed");Require(File.ReadAllText(Path.Combine(root,"current/game/StarTournament.exe"))=="new player");Require(!File.Exists(Path.Combine(root,"current/Squirrel.exe")));Require(File.ReadAllText(Path.Combine(root,"current/sq.version"))==Manifest());AssertRoot(root);store.Restore();AssertOld(root);});
        foreach(var point in new[]{"current-saved","target-activated"})
            await Test("exception at "+point+" restores exact old payload",async root=>{var stage=await Stage(root,checkpoint:p=>{if(p==point)throw new IOException("injected");});var store=new WindowsRecoveryStore(root,Old);store.Prepare(Old,Target.Version,false);await Reject(()=>{store.Apply(()=>stage.Activate(root));return Task.CompletedTask;});AssertOld(root);Require(store.Read().Attempt?.Phase=="restored");});
        await Test("staged mutation rejected before current rename",async root=>{var stage=await Stage(root);var staged=Directory.GetFiles(Path.Combine(root,"maintenance/staging"),"UnityPlayer.dll",SearchOption.AllDirectories).Single();File.WriteAllText(staged,"tampered");await Reject(()=>{stage.Activate(root);return Task.CompletedTask;});AssertOld(root);});
        foreach(var name in new[]{"../escape","lib/app/../escape","lib/app/game/../../escape","lib/app/C:/escape","lib/app/game/file:ads","lib/app/game/CON.txt","lib/app/game/NUL .txt","lib/app/game/trailing.","lib/app/game/space ","lib/app/game/COM¹.txt","lib/app/game/back\\slash","/absolute","lib/app/game/link.__symlink","lib/app/GAME/startournament.EXE","lib/app/game/StarTournament.exe/child"})
            await Test("unsafe Windows namespace rejected: "+name,async root=>{await Reject(async()=>{await Stage(root,z=>Entry(z,name,"bad"));});AssertOld(root);Require(!Directory.EnumerateDirectories(Path.Combine(root,"maintenance/staging"),"payload",SearchOption.AllDirectories).Any());});
        await Test("unix symbolic link rejected",async root=>{await Reject(async()=>{await Stage(root,z=>Entry(z,"lib/app/game/link","../outside",unchecked((int)0xa1ff0000)));});AssertOld(root);});
        foreach(var field in new[]{"id","version","os","rid","channel","machineArchitecture","mainExe","runtimeDependencies","shortcutLocations","osMinVersion"})
            await Test("manifest mismatch rejected: "+field,async root=>{var manifest=Manifest(edit:m=>{var e=m.Elements().SingleOrDefault(e=>e.Name.LocalName==field);if(e==null){e=new XElement(m.Name.Namespace+field);m.Add(e);}e.Value=field=="osMinVersion"?"99.0":"foreign";});await Reject(async()=>{await Stage(root,manifest:manifest);});AssertOld(root);});
        await Test("nested manifest identity rejected",async root=>{var manifest=Manifest(edit:m=>{var e=m.Elements().Single(e=>e.Name.LocalName=="id");e.RemoveNodes();e.Add(new XElement("nested",Old.AppId));});await Reject(async()=>{await Stage(root,manifest:manifest);});AssertOld(root);});
        await Test("duplicate critical manifest field rejected",async root=>{var manifest=Manifest(edit:m=>m.Add(new XElement(m.Name.Namespace+"id",Old.AppId)));await Reject(async()=>{await Stage(root,manifest:manifest);});AssertOld(root);});
        await Test("unexpected root manifest element rejected",async root=>{var xml=XDocument.Parse(Manifest());xml.Root!.Add(new XElement("other",new XElement("id","foreign")));await Reject(async()=>{await Stage(root,manifest:xml.ToString());});AssertOld(root);});
        await Test("signed payload date mismatch rejected",async root=>{await Reject(async()=>{await Stage(root,config:Target with{PublicationDate="2026-10-16"});});AssertOld(root);});
        await Test("payload sq.version is checked and preserved",async root=>{var stage=await Stage(root,z=>Entry(z,"lib/app/sq.version",Manifest()));stage.Activate(root);Require(File.ReadAllText(Path.Combine(root,"current/sq.version"))==Manifest());AssertRoot(root);});
        await Test("foreign payload sq.version rejected",async root=>{await Reject(async()=>{await Stage(root,z=>Entry(z,"lib/app/sq.version",Manifest("9.9.9")));});AssertOld(root);});
        await Test("duplicate package manifests rejected",async root=>{await Reject(async()=>{await Stage(root,z=>Entry(z,"other.nuspec",Manifest()));});AssertOld(root);});
        await Test("external entities never parsed",async root=>{await Reject(async()=>{await Stage(root,manifest:"<!DOCTYPE x [<!ENTITY a SYSTEM 'file:///etc/passwd'>]><package>&a;</package>");});AssertOld(root);});
        await Test("config executable traversal rejected",async root=>{await Reject(async()=>{await Stage(root,config:Target with{GameExecutable="../outside.exe"});});AssertOld(root);});
        await Test("oversized native manifest rejected",async root=>{await Reject(async()=>{await Stage(root,manifest:new string(' ',65537));});AssertOld(root);});
        await Test("unsupported framework rejected",async root=>{await Reject(async()=>{await Stage(root,z=>Entry(z,"lib/net8/app.exe","foreign"));});AssertOld(root);});
        foreach(var point in new[]{"current-saved","target-activated"})
            await Test("process death at "+point+" retains durable recovery",async root=>
            {
                var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false};
                if(Path.GetFileNameWithoutExtension(Environment.ProcessPath)=="dotnet")start.ArgumentList.Add(typeof(WindowsFullPayloadChecks).Assembly.Location);
                foreach(var arg in new[]{"--windows-payload-crash",root,point})start.ArgumentList.Add(arg);
                using var child=Process.Start(start)??throw new Exception("Child missing");await child.WaitForExitAsync();Require(child.ExitCode==73);
                var store=new WindowsRecoveryStore(root,Old);Require(store.Read().Attempt?.Phase=="prepared");store.Restore();AssertOld(root);Require(store.Read().Attempt?.Phase=="restored");
            });
        Console.WriteLine($"Windows full payload: {passed} passed; portable source/real process-crash checks, not native Windows ACL acceptance");
    }
    public static async Task InspectReleased(string package,string envelope)
    {
        using var fixture=new Fixture();
        using var source=new AuthenticatedSource(Old with{Version="0.1.4"},File.ReadAllText("distribution/update-public.pem"),hostProbe:(_,_)=>true);
        source.Admit(File.ReadAllText(envelope));
        var parent=Path.Combine(fixture.Root,"maintenance/staging",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(parent);
        var stage=await WindowsFullPayload.Stage(source,source.Asset!,package,Path.Combine(parent,"payload"));
        AssertOld(fixture.Root);
        Console.WriteLine(JsonSerializer.Serialize(new{actualPublishedPackage=true,version=stage.Target.Version,source.Asset.SHA256,source.Asset.Size,
            payloadFiles=Directory.EnumerateFiles(Path.Combine(parent,"payload"),"*",SearchOption.AllDirectories).Count(),
            nativeMetadata=File.ReadAllText(Path.Combine(parent,"payload/sq.version")),gameExecuted=false,activated=false}));
    }
    public static async Task Crash(string root,string point)
    {var stage=await Stage(root,checkpoint:p=>{if(p==point)Environment.Exit(73);});var store=new WindowsRecoveryStore(root,Old);store.Prepare(Old,Target.Version,false);store.Apply(()=>stage.Activate(root));throw new Exception("Crash checkpoint missing");}
}
