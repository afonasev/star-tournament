using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    [Serializable] public sealed class LabBundle
    {
        public List<ProvingProfile> Profiles = new List<ProvingProfile>();
        public LabBundle Clone() => JsonUtility.FromJson<LabBundle>(JsonUtility.ToJson(this));
        [NonSerialized] Dictionary<string,ProvingProfile> owners;
        public ProvingProfile Profile(string path)
        {
            if(owners==null){owners=new Dictionary<string,ProvingProfile>();foreach(var p in Profiles)foreach(var d in p.Descriptors)owners.Add(d.Path,p);}
            return owners[path];
        }
        public float Get(string path) => Profile(path).Get(path);
        public void Set(string path,float value) => Profile(path).Set(path,value);
        public IEnumerable<NumericDescriptor> Descriptors => Profiles.SelectMany(p => p.Descriptors);
        public bool IsAuthoring(string path)=>Profile(path).Id=="combat-bowl-ring-r7-authoring-v1";
        public bool IsDiagnostic(string path)=>path=="weapon.probeRange"||path=="weapon.probeCooldown";
        public bool IsLevelAuthoring(string path)
        {
            if(IsAuthoring(path))return true;
            var profile=Profile(path);
            if(profile.Id!="orbital-league-ring-v1"&&profile.Id!="orbital-league-v1")return false;
            // Scene placement and mesh dimensions belong to map authoring, not balance tuning.
            var group=profile.Descriptor(path).Group;
            return path.StartsWith("layout.")||path.StartsWith("ring.spawn-mark-")||
                path.StartsWith("wayfinding.")||group=="details"||group=="ring"||
                LevelGeometryPaths.Contains(path);
        }
        static readonly HashSet<string> LevelGeometryPaths=new HashSet<string>{
            "light.fixtureSpacing","light.fixtureWidth","light.fixtureLength","light.fixtureFrame","light.fixtureDepth",
            "light.targetDrop","light.outerAimInset","light.hallAimOffsetZ",
            "broadcast.screenWidth","broadcast.screenHeight","broadcast.orbitRadius","broadcast.markWidth",
            "broadcast.ventRadius","broadcast.spawnMarkHeight","broadcast.servicePanelHeight",
            "ring.spaceHalfExtent","ring.spaceCenterY","ring.planetRadius","ring.secondPlanetRadius","ring.sunRadius",
            "ring.celestialElevation","ring.celestialHorizontal"};
        // Approved audit: engine/query controls, obsolete fields and fixed asset placement stay in
        // snapshots for compatibility, but are never exposed as designer balance controls.
        static readonly HashSet<string> AuditedExclusions=new HashSet<string>{
            "navigation.sampleDistance","world.maximumTransitionSegment","world.minimumSupportHeight","simulation.fixedTickHz",
            "light.shadowResolution","light.shadowBias","light.shadowNormalBias","light.shadowCasters",
            "bots.navigation.heightTolerance","bots.navigation.sampleDistance",
            "presentation.viewWeaponX","presentation.viewWeaponY","presentation.viewWeaponZ","presentation.worldWeaponMountY",
            "camera.stairSmoothingSeconds","camera.stairMaximumOffsetMeters",
            "view.x","view.y","view.z","cutter.modelX","cutter.modelY","cutter.modelZ",
            "cutter.modelPitch","cutter.modelYaw","cutter.modelRoll"};
        public static IEnumerable<string> AuditedExcludedPaths=>AuditedExclusions.OrderBy(path=>path,StringComparer.Ordinal);
        public bool IsVisible(string path)=>!IsDiagnostic(path)&&!AuditedExclusions.Contains(path)&&!IsLevelAuthoring(path);
        public bool IsEditable(string path)=>IsVisible(path)&&!IsAuthoring(path);
        public string Domain(string path)=>IsAuthoring(path)?"Authored map":Profile(path).Descriptor(path).Group=="cutter-effects"||Profile(path).Id.Contains("presentation")||path.StartsWith("presentation.")||(path=="camera.fieldOfViewDegrees"||path=="camera.nearClipPlane")||path.StartsWith("ui.")||Profile(path).Id=="orbital-league-ring-v1"?"Presentation":"Gameplay";
        public List<ProfileValidationIssue> Validate()
        {
            var issues=Profiles.SelectMany(p=>p.Validate()).ToList();
            foreach(var duplicate in Descriptors.GroupBy(d=>d.Path).Where(g=>g.Count()!=1))
                issues.Add(new ProfileValidationIssue{Path=duplicate.Key,Message="Путь зарегистрирован несколько раз"});
            foreach(var d in Descriptors)
            {
                // Step is anchored at the shipped value: several authored defaults are not min-aligned.
                double steps=(Get(d.Path)-d.DefaultValue)/d.Step;
                if(Math.Abs(steps-Math.Round(steps))>.002)
                    issues.Add(new ProfileValidationIssue{Path=d.Path,Message="Значение должно соответствовать шагу "+d.Step});
            }
            foreach(var palette in Profiles.Where(p=>p.Id==ProvingProfile.ParticipantPaletteId))
                try { NativeParticipantColors.Read(palette); }
                catch(ArgumentException e) { issues.Add(new ProfileValidationIssue{Path="participant.color.blue.r",Message=e.Message}); }
            CheckOrder(issues,"player.capsule.radius","player.capsule.height",2);
            CheckOrder(issues,"zone.armBottom","zone.armTop",1);
            CheckOrder(issues,"zone.legBottom","zone.legTop",1);
            for(int i=2;i<=5;i++)CheckOrder(issues,"score.chainTotal"+(i-1),"score.chainTotal"+i,1,false);
            return issues;
        }
        void CheckOrder(List<ProfileValidationIssue> issues,string low,string high,float factor,bool strict=true)
        {
            if(Descriptors.Any(d=>d.Path==low)&&Descriptors.Any(d=>d.Path==high)&&(strict?Get(low)*factor>=Get(high):Get(low)*factor>Get(high)))
                issues.Add(new ProfileValidationIssue{Path=high,Message=high+" должно быть больше "+low+" × "+factor});
        }
        public string Hash()
        {
            string canonical=string.Join("\n",Profiles.Select(p=>p.Id+"@"+p.Version))+"\n"+string.Join("\n",Descriptors.OrderBy(d=>d.Path,StringComparer.Ordinal).Select(d=>
                d.Path+"["+Domain(d.Path)+"]="+Get(d.Path).ToString("R",CultureInfo.InvariantCulture)));
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical))).Replace("-","").ToLowerInvariant();
        }
    }
    [Serializable] public sealed class LabRevisionReference
    {
        public string ProfileId; public int Revision; public string Hash;
        public LabRevisionReference Clone()=>JsonUtility.FromJson<LabRevisionReference>(JsonUtility.ToJson(this));
        public bool Matches(LabRevisionReference other)=>other!=null&&ProfileId==other.ProfileId&&Revision==other.Revision&&Hash==other.Hash;
    }
    [Serializable] public sealed class LabRevision
    {
        public int Number; public string Date; public string Hash; public LabBundle Snapshot;
        public string Label => "v"+Number+"-"+(string.IsNullOrEmpty(Date)?"без-даты":Date);
        public LabRevision Clone()=>JsonUtility.FromJson<LabRevision>(JsonUtility.ToJson(this));
    }
    [Serializable] public sealed class LabHistoryProfile
    {
        public string Id; public string Name; public bool Protected;
        public List<LabRevision> Revisions=new List<LabRevision>();
    }
    [Serializable] public sealed class LabHistoryFile
    {
        public int Schema=1; public string SelectedId; public int SelectedRevision;
        public List<LabHistoryProfile> Profiles=new List<LabHistoryProfile>();
    }
    public sealed class DesignLabHistory
    {
        public const string ReleaseId="unity-shipped-release";
        readonly string path; readonly LabBundle shipped,preSwitch,preCutterThickness,preCutterThicknessSwitch,preCutter,preCutterSwitch,prePulse,preHeal; readonly LabBundle[] predecessors; readonly Action<string,string> atomicPublish; LabHistoryFile data;
        public string StorageError {get;private set;}
        public bool Writable=>StorageError==null;
        public IReadOnlyList<LabHistoryProfile> Profiles=>JsonUtility.FromJson<LabHistoryFile>(JsonUtility.ToJson(data)).Profiles;
        public string SelectedProfileId=>data.SelectedId;
        public string SelectedProfileName=>data.Profiles.Single(p=>p.Id==data.SelectedId).Name;
        public bool SelectedProfileProtected=>data.Profiles.Single(p=>p.Id==data.SelectedId).Protected;
        public LabHistoryProfile SelectedProfile=>JsonUtility.FromJson<LabHistoryProfile>(JsonUtility.ToJson(data.Profiles.Single(p=>p.Id==data.SelectedId)));
        public LabRevision Selected=>data.Profiles.Single(p=>p.Id==data.SelectedId).Revisions.Single(r=>r.Number==data.SelectedRevision).Clone();
        public DesignLabHistory(string path,LabBundle shipped,Action<string,string> atomicPublish=null)
        {
            this.path=path;this.shipped=shipped.Clone();var beforePalette=new LabBundle{Profiles=this.shipped.Profiles.Where(p=>p.Id!=ProvingProfile.ParticipantPaletteId).ToList()};preSwitch=new LabBundle{Profiles=beforePalette.Profiles.Select(p=>p.BeforeWeaponSwitch()).ToList()};preCutterThickness=new LabBundle{Profiles=beforePalette.Profiles.Select(p=>p.BeforeCutterThickness()).ToList()};preCutterThicknessSwitch=new LabBundle{Profiles=preSwitch.Profiles.Select(p=>p.BeforeCutterThickness()).ToList()};preCutter=new LabBundle{Profiles=beforePalette.Profiles.Where(p=>p.Id!="cutter-beam-v1").ToList()};preCutterSwitch=new LabBundle{Profiles=preSwitch.Profiles.Where(p=>p.Id!="cutter-beam-v1").ToList()};prePulse=new LabBundle{Profiles=preCutterSwitch.Profiles.Select(p=>p.BeforePulse()).ToList()};preHeal=new LabBundle{Profiles=preCutterSwitch.Profiles.Select(p=>p.BeforeFullHeal()).ToList()};this.atomicPublish=atomicPublish??Publish;
            var medium=new LabBundle{Profiles=this.shipped.Profiles.Select(p=>p.BeforeMediumWeaponSwitch()).ToList()};
            var mediumBeforePalette=new LabBundle{Profiles=medium.Profiles.Where(p=>p.Id!=ProvingProfile.ParticipantPaletteId).ToList()};
            var low=new LabBundle{Profiles=beforePalette.Profiles.Select(p=>p.BeforeLowWeaponSwitch()).ToList()};
            var shoulder=new LabBundle{Profiles=beforePalette.Profiles.Select(p=>p.BeforeShoulderSwitch()).ToList()};
            predecessors=new[]{this.shipped,beforePalette,medium,mediumBeforePalette,new LabBundle{Profiles=mediumBeforePalette.Profiles.Select(p=>p.BeforeCutterThickness()).ToList()},new LabBundle{Profiles=mediumBeforePalette.Profiles.Where(p=>p.Id!="cutter-beam-v1").ToList()},low,new LabBundle{Profiles=low.Profiles.Select(p=>p.BeforeCutterThickness()).ToList()},new LabBundle{Profiles=low.Profiles.Where(p=>p.Id!="cutter-beam-v1").ToList()},preSwitch,preCutterThickness,preCutterThicknessSwitch,preCutter,preCutterSwitch,prePulse,preHeal,shoulder,new LabBundle{Profiles=shoulder.Profiles.Select(p=>p.BeforeCutterThickness()).ToList()},new LabBundle{Profiles=shoulder.Profiles.Where(p=>p.Id!="cutter-beam-v1").ToList()}};
            // Accept exact prior registries across additive palette and weapon-pickup schema upgrades.
            LabBundle WithoutPickups(LabBundle b)=>new LabBundle{Profiles=b.Profiles.Select(p=>p.Id=="unity-combat-state-v1"||p.Id=="combat-bowl-ring-r7-authoring-v1"?p.BeforeWeaponPickups():p).ToList()};
            LabBundle WithPalette(LabBundle b)
            {
                var copy=new LabBundle{Profiles=b.Profiles.ToList()};if(this.shipped.Profiles.Any(p=>p.Id==ProvingProfile.ParticipantPaletteId)&&!copy.Profiles.Any(p=>p.Id==ProvingProfile.ParticipantPaletteId))
                    copy.Profiles.Add(this.shipped.Profiles.Single(p=>p.Id==ProvingProfile.ParticipantPaletteId));
                return copy;
            }
            predecessors=predecessors.SelectMany(b=>new[]{b,WithoutPickups(b),WithPalette(b),WithPalette(WithoutPickups(b))}).GroupBy(b=>string.Join("|",b.Profiles.Select(p=>p.Id+"@"+p.Version))).Select(g=>g.First()).ToArray();
            data=NewFile();
            if(!File.Exists(path))return;
            try
            {
                var loaded=JsonUtility.FromJson<LabHistoryFile>(File.ReadAllText(path));ValidateFile(loaded);data=loaded;
                if(loaded.Profiles.Any(p=>p.Revisions.All(r=>!Current(r.Snapshot)))||!Current(loaded.Profiles.Single(p=>p.Id==loaded.SelectedId).Revisions.Single(r=>r.Number==loaded.SelectedRevision).Snapshot))Commit(MigrateCompatibility);
            }
            catch(Exception e) { StorageError="История не загружена; исходный файл сохранён: "+e.Message; }
        }
        LabHistoryFile NewFile()=>new LabHistoryFile{SelectedId=ReleaseId,SelectedRevision=1,Profiles=new List<LabHistoryProfile>{
            new LabHistoryProfile{Id=ReleaseId,Name="Опубликованный профиль",Protected=true,Revisions=new List<LabRevision>{Revision(shipped,1,null)}}}};
        static LabRevision Revision(LabBundle bundle,int number,string date)=>new LabRevision{Number=number,Date=date,Hash=bundle.Hash(),Snapshot=bundle.Clone()};
        void ValidateFile(LabHistoryFile file)
        {
            if(file==null||file.Schema!=1||file.Profiles==null||file.Profiles.Count==0)throw new InvalidDataException("Неподдерживаемая схема");

            if(file.Profiles.Select(p=>p.Id).Distinct().Count()!=file.Profiles.Count)throw new InvalidDataException("Повтор ID");
            foreach(var p in file.Profiles)
            {
                if(string.IsNullOrWhiteSpace(p.Id)||string.IsNullOrWhiteSpace(p.Name)||p.Revisions==null||p.Revisions.Count==0||p.Protected!=(p.Id==ReleaseId))throw new InvalidDataException("Некорректный профиль");
                if(p.Revisions.Select(r=>r.Number).Distinct().Count()!=p.Revisions.Count)throw new InvalidDataException("Повтор ревизии");
                foreach(var r in p.Revisions)
                {
                    var registry=r.Snapshot==null?shipped:predecessors.FirstOrDefault(b=>RegistryMatches(b,r.Snapshot))??shipped;
                    var paths=registry.Descriptors.Select(d=>d.Path).OrderBy(k=>k).ToArray();
                    if(r.Number<1||r.Snapshot==null||!registry.Profiles.Select(p=>p.Id+"@"+p.Version).SequenceEqual(r.Snapshot.Profiles.Select(p=>p.Id+"@"+p.Version))||!paths.SequenceEqual(r.Snapshot.Descriptors.Select(d=>d.Path).OrderBy(k=>k))||r.Hash!=r.Snapshot.Hash())throw new InvalidDataException("Снимок или hash повреждён");
                    // Metadata is trusted only from the shipped registry, never from the file.
                    var trusted=registry.Clone();foreach(var d in trusted.Descriptors)trusted.Set(d.Path,r.Snapshot.Get(d.Path));
                    if(trusted.Descriptors.Any(d=>(trusted.IsAuthoring(d.Path)||trusted.IsDiagnostic(d.Path))&&trusted.Get(d.Path)!=registry.Get(d.Path)))throw new InvalidDataException("Read-only metadata changed");
                    if(trusted.Validate().Count!=0)throw new InvalidDataException("Невалидный снимок");r.Snapshot=trusted;
                }
            }
            var release=file.Profiles.Single(p=>p.Id==ReleaseId).Revisions.Single(r=>r.Number==1);
            if(!predecessors.Any(b=>release.Hash==b.Hash()))throw new InvalidDataException("Shipped release несовместим");
            if(!file.Profiles.Any(p=>p.Id==file.SelectedId&&p.Revisions.Any(r=>r.Number==file.SelectedRevision)))throw new InvalidDataException("Выбор отсутствует");
        }
        static bool RegistryMatches(LabBundle registry,LabBundle bundle)=>registry.Profiles.Select(p=>p.Id+"@"+p.Version).SequenceEqual(bundle.Profiles.Select(p=>p.Id+"@"+p.Version));
        bool Current(LabBundle bundle)=>RegistryMatches(shipped,bundle);
        public bool Compatible(LabRevision revision)=>Current(revision.Snapshot);
        void MigrateCompatibility(LabHistoryFile file)
        {
            bool selectedRelease=file.SelectedId==ReleaseId;
            // Append current-schema counterparts; keep every old revision number, value and hash intact.
            foreach(var p in file.Profiles)
                foreach(var old in p.Revisions.Where(r=>!Current(r.Snapshot)).ToArray())
                {
                    var next=shipped.Clone();foreach(var d in old.Snapshot.Descriptors)if(next.Descriptors.Any(n=>n.Path==d.Path))next.Set(d.Path,ProvingProfile.SafeSwitchUpgrade(d,next.Profile(d.Path).Descriptor(d.Path),old.Snapshot.Get(d.Path)));
                    var existing=p.Revisions.FirstOrDefault(r=>Current(r.Snapshot)&&r.Hash==next.Hash());
                    if(existing==null){existing=Revision(next,p.Revisions.Max(r=>r.Number)+1,DateTime.Now.ToString("yyyyMMdd"));p.Revisions.Add(existing);}
                    if(file.SelectedId==p.Id&&file.SelectedRevision==old.Number)file.SelectedRevision=existing.Number;
                }
            var release=file.Profiles.Single(p=>p.Id==ReleaseId);
            var latest=release.Revisions.FirstOrDefault(r=>Current(r.Snapshot)&&r.Hash==shipped.Hash());
            if(latest==null){latest=Revision(shipped,release.Revisions.Max(r=>r.Number)+1,DateTime.Now.ToString("yyyyMMdd"));release.Revisions.Add(latest);}
            if(selectedRelease)file.SelectedRevision=latest.Number;
        }
        void Commit(Action<LabHistoryFile> mutation)
        {
            if(!Writable)throw new IOException(StorageError);
            var next=JsonUtility.FromJson<LabHistoryFile>(JsonUtility.ToJson(data));mutation(next);ValidateFile(next);
            string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {byte[] bytes=Encoding.UTF8.GetBytes(JsonUtility.ToJson(next,true));stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                atomicPublish(temp,path);
                data=next;
            }
            finally {if(File.Exists(temp))File.Delete(temp);}
        }
        static void Publish(string temp,string destination){if(File.Exists(destination))File.Replace(temp,destination,null);else File.Move(temp,destination);}
        public void Select(string id,int number)=>Commit(f=>{var r=f.Profiles.Single(p=>p.Id==id).Revisions.Single(r=>r.Number==number);if(!Current(r.Snapshot))throw new InvalidOperationException("Историческая схема: выберите её новую совместимую ревизию");f.SelectedId=id;f.SelectedRevision=number;});
        public void Save(LabBundle draft)
        {
            // Previously editable placement overrides remain valid immutable history.
            // New revisions may retain them, but cannot introduce further changes to hidden fields.
            var baseline=Selected.Snapshot;
            if(draft.Descriptors.Any(d=>!draft.IsEditable(d.Path)&&draft.Get(d.Path)!=baseline.Get(d.Path)))throw new ArgumentException("Read-only metadata cannot be saved");
            if(draft.Validate().Count!=0)throw new ArgumentException("Черновик содержит ошибки");
            if(draft.Hash()==Selected.Hash)throw new ArgumentException("Нет изменений");
            Commit(f=>{var p=f.Profiles.Single(x=>x.Id==f.SelectedId);int n=p.Revisions.Max(r=>r.Number)+1;
                p.Revisions.Add(Revision(draft,n,DateTime.Now.ToString("yyyyMMdd")));f.SelectedRevision=n;});
        }
        public void Create(string name)
        {
            RequireName(name);var baseSnapshot=Selected.Snapshot;string id=Guid.NewGuid().ToString("N");
            Commit(f=>{f.Profiles.Add(new LabHistoryProfile{Id=id,Name=name.Trim(),Revisions=new List<LabRevision>{Revision(baseSnapshot,1,DateTime.Now.ToString("yyyyMMdd"))}});f.SelectedId=id;f.SelectedRevision=1;});
        }
        public void Rename(string name){RequireName(name);Commit(f=>f.Profiles.Single(p=>p.Id==f.SelectedId).Name=name.Trim());}
        public void DeleteSelected()
        {
            if(SelectedProfileProtected)throw new InvalidOperationException("Опубликованный профиль защищён");
            Commit(f=>{f.Profiles.RemoveAll(p=>p.Id==f.SelectedId);f.SelectedId=ReleaseId;f.SelectedRevision=f.Profiles.Single(p=>p.Id==ReleaseId).Revisions.First(r=>Current(r.Snapshot)&&r.Hash==shipped.Hash()).Number;});
        }
        static void RequireName(string name){if(string.IsNullOrWhiteSpace(name)||name.Trim().Length>48)throw new ArgumentException("Имя: от 1 до 48 символов");}
    }
}
