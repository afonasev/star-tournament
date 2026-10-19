using System.Text.Json;
using StarTournament.Distribution;

static class WindowsInstallerRegistrationChecks
{
    static void Require(bool value){if(!value)throw new Exception("Installer registration contract failed");}
    static void Reject(Action run){try{run();}catch(IOException){return;}throw new Exception("Unsafe registry journal accepted");}
    public static void Run()
    {
        int passed=0;var home=Path.Combine(Directory.GetCurrentDirectory(),".local","installer-registration-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(home);
        try
        {
            void Test(string name,Action<string,WindowsInstallerRegistration> run)
            {var root=Path.Combine(home,passed.ToString());Directory.CreateDirectory(root+".installer");run(root,new(root));passed++;Console.WriteLine("PASS "+name);}
            var values=new[]{new InstallerRegistryValue("DisplayVersion","String","1.0.0"),new InstallerRegistryValue("NoModify","DWord",Number:1)};
            Test("capture and reopen preserve exact fixed values and types",(root,store)=>{store.Capture(true,values);var read=new WindowsInstallerRegistration(root).Read()!;Require(read.Existed&&!read.Applying&&read.Values.SequenceEqual(values));});
            Test("no replay mutation occurs before durable applying intent",(root,store)=>{store.Capture(true,values);store.Replay(_=>throw new Exception("delete"),_=>throw new Exception("write"));Require(!store.Read()!.Applying);});
            Test("applying survives reopening before registry changes",(root,store)=>{store.Capture(true,values);store.BeforeApply();Require(new WindowsInstallerRegistration(root).Read()!.Applying);});
            Test("replay restores owned values and preserves unknown ones",(root,store)=>
            {
                store.Capture(true,values);store.BeforeApply();var registry=new Dictionary<string,InstallerRegistryValue>{{"DisplayVersion",new("DisplayVersion","String","2.0.0")},{"Publisher",new("Publisher","String","new")},{"Foreign",new("Foreign","String","keep")}};
                store.Replay(name=>registry.Remove(name),value=>registry[value.Name]=value);
                Require(registry["DisplayVersion"].Text=="1.0.0"&&registry["NoModify"].Number==1&&registry["Foreign"].Text=="keep"&&!registry.ContainsKey("Publisher"));Require(store.Read()!=null);
            });
            Test("interrupted registry callback keeps replayable snapshot",(root,store)=>
            {
                store.Capture(true,values);store.BeforeApply();Reject(()=>store.Replay(_=>{},_=>throw new IOException("registry write failed")));Require(store.Read()!.Applying);int restored=0;
                new WindowsInstallerRegistration(root).Replay(_=>{},_=>restored++);Require(restored==2);
            });
            Test("fresh rollback records absence without inventing previous keys",(root,store)=>{store.Capture(false,[]);store.BeforeApply();int removed=0,written=0;store.Replay(_=>removed++,_=>written++);Require(!store.Read()!.Existed&&removed==WindowsInstallerRegistration.Names.Length&&written==0);});
            Test("snapshot cannot replace another pending capture",(root,store)=>{store.Capture(true,values);Reject(()=>store.Capture(false,[]));Require(store.Read()!.Values.SequenceEqual(values));});
            Test("foreign root and unknown value names fail closed",(root,store)=>
            {
                store.Capture(true,values);var path=Path.Combine(root+".installer","registration.json");File.WriteAllText(path,File.ReadAllText(path).Replace("DisplayVersion","ExecutablePath"));Reject(()=>store.Read());
                File.WriteAllText(path,JsonSerializer.Serialize(new InstallerRegistrySnapshot(1,new string('a',64),true,true,values)));Reject(()=>store.Read());
            });
            Test("huge snapshots fail before replacing a durable file",(root,store)=>{var many=WindowsInstallerRegistration.Names.Select(name=>new InstallerRegistryValue(name,"String",new string('я',32768))).ToArray();Reject(()=>store.Capture(true,many));Require(store.Read()==null);});
            Test("protected creator is used for capture and applying writes",(root,store)=>{int created=0;var secure=new WindowsInstallerRegistration(root,path=>{created++;return new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);});secure.Capture(true,values);secure.BeforeApply();Require(created==2);});
            Test("trailing root resolves to the same protected sibling snapshot",(root,store)=>{store.Capture(true,values);Require(new WindowsInstallerRegistration(root+Path.DirectorySeparatorChar).Read()!.Values.SequenceEqual(values));store.Remove();Require(store.Read()==null);});
            Test("directory and redirected state are never treated as empty",(root,store)=>{var path=Path.Combine(root+".installer","registration.json");Directory.CreateDirectory(path);Reject(()=>store.Read());Directory.Delete(path);var target=Path.Combine(home,"foreign.json");File.WriteAllText(target,"{}");File.CreateSymbolicLink(path,target);Reject(()=>store.Read());File.Delete(path);});
            Test("interrupted atomic-write sidecar is removed with settled snapshot",(root,store)=>{store.Capture(true,values);var file=Path.Combine(root+".installer","registration.json");File.Copy(file,file+".next");store.Remove();Require(!File.Exists(file)&&!File.Exists(file+".next"));});
            Console.WriteLine($"Windows installer registration: {passed} passed (actual snapshot/replay logic; Windows Registry API NotRun)");
        }
        finally{Directory.Delete(home,true);}
    }
}
