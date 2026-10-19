using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    [Serializable] public sealed class LabBundle
    {
        public List<ProvingProfile> Profiles = new List<ProvingProfile>();
        public LabBundle Clone() => Profiles==null||Profiles.Any(p=>p==null)
            ? JsonUtility.FromJson<LabBundle>(JsonUtility.ToJson(this))
            : new LabBundle{Profiles=Profiles.Select(p=>p.DetachedCopy()).ToList()};
        [NonSerialized] Dictionary<string,ProvingProfile> owners;
        public ProvingProfile Profile(string path)
        {
            if(owners==null){owners=new Dictionary<string,ProvingProfile>();foreach(var p in Profiles)foreach(var d in p.Descriptors)owners.Add(d.Path,p);}
            return owners[path];
        }
        public float Get(string path) => Profile(path).Get(path);
        public void Set(string path,float value) => Profile(path).Set(path,value);
        public IEnumerable<NumericDescriptor> Descriptors => Profiles.SelectMany(p => p.Descriptors);
        public bool IsAuthoring(string path)=>Profile(path).Id=="combat-bowl-ring-r7-authoring-v1"||Profile(path).Id==ProvingProfile.TunnelsAuthoringId||Profile(path).Id=="lunar-laboratory-authoring-v1"||Profile(path).Id=="well-authoring-v1";
        public bool IsDiagnostic(string path)=>path.StartsWith("bots.evaluation.")||path=="weapon.probeRange"||path=="weapon.probeCooldown";
        public bool IsLevelAuthoring(string path)
        {
            if(IsAuthoring(path))return true;
            var profile=Profile(path);
            if(profile.Id==ProvingProfile.TunnelsArtId)return profile.Descriptor(path).Group=="tunnel-detail";
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
            "presentation.rocketExplosionSize", // Obsolete independent VFX radius; preserved in immutable history.
            "cutter.contactTickSeconds","cutter.hitRadius",
            // Legacy heuristic; actual switch duration now owns the attack opportunity cost.
            "bots.strategy.switchPenalty",
            "input.gamepadDegreesPerSecond", // Legacy shared-axis value retained only for revision migration.
            "navigation.sampleDistance","world.maximumTransitionSegment","world.minimumSupportHeight","simulation.fixedTickHz",
            "light.shadowResolution","light.shadowBias","light.shadowNormalBias","light.shadowCasters",
            "bots.navigation.heightTolerance","bots.navigation.sampleDistance",
            "presentation.viewWeaponX","presentation.viewWeaponY","presentation.viewWeaponZ","presentation.worldWeaponMountY",
            "camera.stairSmoothingSeconds","camera.stairMaximumOffsetMeters",
            "view.x","view.y","view.z","cutter.modelX","cutter.modelY","cutter.modelZ",
            "cutter.modelPitch","cutter.modelYaw","cutter.modelRoll"};
        public static IEnumerable<string> AuditedExcludedPaths=>AuditedExclusions.OrderBy(path=>path,StringComparer.Ordinal);
        public bool IsVisible(string path)=>!(Profile(path).Id=="unity-native-match-v1"&&Profile(path).Version>=3&&((path.StartsWith("score.chainTotal")&&path!="score.chainTotal1")||path=="score.chainIncrement"))&&!IsDiagnostic(path)&&!AuditedExclusions.Contains(path)&&!IsLevelAuthoring(path);
        public bool IsEditable(string path)=>IsVisible(path)&&!IsAuthoring(path);
        public string Domain(string path)=>path.StartsWith("well.")&&!IsAuthoring(path)?(Profile(path).Descriptor(path).Group=="well-light"||Profile(path).Descriptor(path).Group=="well-art"?"Presentation":"Gameplay"):path.StartsWith("audio.music.")?"Presentation":IsAuthoring(path)?"Authored map":Profile(path).Descriptor(path).Group=="cutter-effects"||Profile(path).Id==ProvingProfile.RocketEffectsId||Profile(path).Id.Contains("presentation")||path.StartsWith("presentation.")||(path=="camera.fieldOfViewDegrees"||path=="camera.nearClipPlane")||path.StartsWith("ui.")||Profile(path).Id=="orbital-league-ring-v1"?"Presentation":"Gameplay";
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
            CheckOrder(issues,"corpse.cutterSpeed","corpse.rifleSpeed",1);
            CheckOrder(issues,"corpse.rifleSpeed","corpse.shotgunSpeed",1);
            CheckOrder(issues,"player.capsule.radius","player.capsule.height",2);
            CheckOrder(issues,"zone.armBottom","zone.armTop",1);
            CheckOrder(issues,"zone.legBottom","zone.legTop",1);
            CheckOrder(issues,"ui.killNoticeBottom","ui.killNoticeTop",1);
            for(int i=2;Descriptors.Any(d=>d.Path=="score.chainTotal1")&&Profile("score.chainTotal1").Version<3&&i<=5;i++)CheckOrder(issues,"score.chainTotal"+(i-1),"score.chainTotal"+i,1,false);
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
        public bool ReleaseCandidate;
        public int ReleaseSequence,ReleaseNumber;
        public int CompatibilitySourceNumber;public string CompatibilitySourceHash;
        public string ReleaseHash,ReleaseDate;
        public bool IsCompatibilityView=>CompatibilitySourceNumber>0||(ReleaseSequence>0&&Hash!=ReleaseHash);
        public string Label => "v"+(ReleaseSequence>0?ReleaseNumber:Number)+"-"+(string.IsNullOrEmpty(ReleaseSequence>0?ReleaseDate:Date)?"без-даты":ReleaseSequence>0?ReleaseDate:Date);
        public LabRevision Clone()=>Snapshot==null?JsonUtility.FromJson<LabRevision>(JsonUtility.ToJson(this))
            :new LabRevision{Number=Number,Date=Date??"",Hash=Hash??"",Snapshot=Snapshot.Clone(),ReleaseCandidate=ReleaseCandidate,
                ReleaseSequence=ReleaseSequence,ReleaseNumber=ReleaseNumber,ReleaseHash=ReleaseHash,ReleaseDate=ReleaseDate,
                CompatibilitySourceNumber=CompatibilitySourceNumber,CompatibilitySourceHash=CompatibilitySourceHash};
    }
    public sealed class LabSavedRevisionIdentity
    {
        public int Number {get;} public string Date {get;} public string Hash {get;}
        public string Label=>"v"+Number+"-"+(string.IsNullOrEmpty(Date)?"без-даты":Date);
        internal LabSavedRevisionIdentity(int number,string date,string hash){Number=number;Date=date??"";Hash=hash;}
    }
    [Serializable] public sealed class LabHistoryProfile
    {
        public string Id; public string Name; public bool Protected;
        public List<LabRevision> Revisions=new List<LabRevision>();
        internal LabHistoryProfile Clone()=>Revisions==null||Revisions.Any(r=>r==null)
            ?JsonUtility.FromJson<LabHistoryProfile>(JsonUtility.ToJson(this))
            :new LabHistoryProfile{Id=Id??"",Name=Name??"",Protected=Protected,Revisions=Revisions.Select(r=>r.Clone()).ToList()};
    }
    [Serializable] public sealed class LabHistoryFile
    {
        public int Schema=1; public string SelectedId; public int SelectedRevision;
        public List<LabHistoryProfile> Profiles=new List<LabHistoryProfile>();
        internal LabHistoryFile Clone()=>Profiles==null||Profiles.Any(p=>p==null)
            ?JsonUtility.FromJson<LabHistoryFile>(JsonUtility.ToJson(this))
            :new LabHistoryFile{Schema=Schema,SelectedId=SelectedId??"",SelectedRevision=SelectedRevision,Profiles=Profiles.Select(p=>p.Clone()).ToList()};
    }
    [Serializable] public sealed class LabHistoryStartupDiagnostics
    {
        public double TotalSeconds,CatalogueSeconds,ReadSeconds,ParseSeconds,ExistingValidationSeconds,MigrationSeconds,MergeSeconds,FinalValidationSeconds;
        public double HistoricalLookupSeconds,HashSeconds,TrustedCopySeconds,SnapshotValidationSeconds;
        public long FileBytes,ManagedMemoryDelta;
        public int Revisions,SnapshotHashes,SnapshotValidations,RegistryCacheHits,RegistryCacheMisses,Gen0Collections;
        public int CachedRegistrySets,CachedCandidateBundles,CachedDistinctProfiles,CachedDescriptors;
        public override string ToString()=>JsonUtility.ToJson(this);
    }
    public sealed partial class DesignLabHistory
    {
        public const string ReleaseId="unity-shipped-release";
        static readonly object historicalLock=new object();
        static readonly Dictionary<string,LabBundle[]> historicalCache=new Dictionary<string,LabBundle[]>();
        readonly Dictionary<string,LabBundle[]> historicalForInstance=new Dictionary<string,LabBundle[]>(StringComparer.Ordinal);
        readonly Queue<string> historicalInstanceOrder=new Queue<string>();
        const int InstanceRegistryLimit=8,GlobalRegistryLimit=2;
        static readonly object releaseValidationLock=new object();
        static readonly Dictionary<string,LabReleaseCatalog> validatedReleases=new Dictionary<string,LabReleaseCatalog>();
        readonly LabHistoryStartupDiagnostics startupDiagnostics=new LabHistoryStartupDiagnostics();
        public LabHistoryStartupDiagnostics StartupDiagnostics=>startupDiagnostics;
        bool collectingStartupDiagnostics=true;
        static double Clock()=>System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency;
        CancellationToken startupCancellation;
        readonly bool authoring;
        public bool IsAuthoring=>authoring;
        public string StoragePath=>path;
        readonly string path; readonly LabBundle shipped; readonly LabReleaseCatalog releases; string shippedCacheKey; LabBundle[] predecessors; readonly Action<string,string> atomicPublish; LabHistoryFile data;
        public string StorageError {get;private set;}
        public bool Writable=>StorageError==null;
        int mutationInFlight;
        public bool Busy=>Volatile.Read(ref mutationInFlight)!=0;
        LabHistoryProfile DisplayProfile(LabHistoryProfile profile)
        {
            var copy=profile.Clone();
            copy.Name=DisplayName(profile);
            copy.Protected=IsProtected(profile.Id);
            return copy;
        }
        string DisplayName(LabHistoryProfile profile)=>!authoring&&releases==null&&profile.Id==ReleaseId&&profile.Name=="Опубликованный профиль"?"Default":profile.Name;
        public IReadOnlyList<LabHistoryProfile> Profiles=>data.Profiles.Select(DisplayProfile).ToList();
        public string SelectedProfileId=>data.SelectedId;
        static int SavedVersionNumber(LabRevision revision)=>revision.ReleaseSequence>0?revision.ReleaseNumber:revision.Number;
        public int LatestAvailableRevision(string profileId)
        {
            var profile=data.Profiles.Single(p=>p.Id==profileId);
            // Packaged profiles keep legacy local rows for explicit inspection/copy.
            // Those physical keys must never outrank the actual available package.
            bool packaged=!authoring&&releases!=null&&releases.Entries.Any(e=>e.ProfileId==profileId);
            var saved=profile.Revisions.Where(r=>r.CompatibilitySourceNumber==0&&(!packaged||IsShipped(r))).OrderByDescending(SavedVersionNumber)
                .ThenByDescending(r=>Current(r.Snapshot)).ThenByDescending(r=>r.Number).First();
            if(Current(saved.Snapshot))return saved.Number;
            return profile.Revisions.SingleOrDefault(r=>r.CompatibilitySourceNumber==saved.Number&&r.CompatibilitySourceHash==saved.Hash)?.Number??saved.Number;
        }
        public string SelectedProfileName=>DisplayName(data.Profiles.Single(p=>p.Id==data.SelectedId));
        bool IsProtected(string id)=>id==ReleaseId||(!authoring&&releases!=null&&releases.Entries.Any(e=>e.ProfileId==id));
        public bool SelectedProfileProtected=>IsProtected(data.SelectedId);
        public bool SelectedProfileReadOnly=>!authoring&&releases!=null&&releases.Entries.Any(e=>e.ProfileId==data.SelectedId);
        void RequireLocalProfile(){if(SelectedProfileReadOnly)throw new InvalidOperationException("Релизный профиль доступен только для чтения. Создайте копию для своих изменений.");}
        public LabHistoryProfile SelectedProfile=>DisplayProfile(data.Profiles.Single(p=>p.Id==data.SelectedId));
        public LabRevision Selected=>data.Profiles.Single(p=>p.Id==data.SelectedId).Revisions.Single(r=>r.Number==data.SelectedRevision).Clone();
        public LabSavedRevisionIdentity SavedIdentity(string profileId,int revisionNumber)
        {
            var profile=data.Profiles.Single(p=>p.Id==profileId);
            var revision=profile.Revisions.Single(r=>r.Number==revisionNumber);
            if(revision.CompatibilitySourceNumber>0)
                revision=profile.Revisions.Single(r=>r.Number==revision.CompatibilitySourceNumber&&r.Hash==revision.CompatibilitySourceHash);
            return revision.ReleaseSequence>0
                ?new LabSavedRevisionIdentity(revision.ReleaseNumber,revision.ReleaseDate,revision.ReleaseHash)
                :new LabSavedRevisionIdentity(revision.Number,revision.Date,revision.Hash);
        }
        public static DesignLabHistory ResolvePublishedDefault(LabBundle shipped,LabReleaseCatalog releases)
        {
            if(releases==null)throw new ArgumentNullException(nameof(releases));
            // Reuse exact trusted catalogue validation and compatibility migration. No user IO.
            return new DesignLabHistory(null,shipped,releases:releases,resetToLatestDefault:true,persistMigration:false);
        }
        public DesignLabHistory(string path,LabBundle shipped,Action<string,string> atomicPublish=null,LabReleaseCatalog releases=null,bool resetToLatestDefault=false,bool persistMigration=true,CancellationToken cancellationToken=default,string legacyPath=null,bool authoring=false)
        {
            double start=Clock(),stage=start;long memory=GC.GetTotalMemory(false);int collections=GC.CollectionCount(0);
            startupCancellation=cancellationToken;startupCancellation.ThrowIfCancellationRequested();
            this.authoring=authoring;
            this.path=path;this.shipped=shipped.Clone();this.atomicPublish=atomicPublish??Publish;this.releases=releases?.Clone();
            data=NewFile();
            stage=Clock();if(this.releases!=null)ValidateReleases();startupDiagnostics.CatalogueSeconds=Clock()-stage;
            string source=VersionedUserData.ReadPath(path,legacyPath);
            if(File.Exists(source))
            try
            {
                startupCancellation.ThrowIfCancellationRequested();
                stage=Clock();startupDiagnostics.FileBytes=new FileInfo(source).Length;var json=File.ReadAllText(source);startupDiagnostics.ReadSeconds=Clock()-stage;
                startupCancellation.ThrowIfCancellationRequested();
                stage=Clock();var loaded=ReadStorage(json);startupDiagnostics.ParseSeconds=Clock()-stage;
                startupDiagnostics.Revisions=loaded?.Profiles?.Sum(p=>p.Revisions?.Count??0)??0;
                stage=Clock();ValidateFile(loaded);startupDiagnostics.ExistingValidationSeconds=Clock()-stage;data=loaded;
                if(loaded.Profiles.Any(p=>p.Revisions.Any(r=>r.CompatibilitySourceNumber>0||!Current(r.Snapshot))))
                {
                    stage=Clock();
                    if(persistMigration&&this.releases==null)Commit(MigrateCompatibility);
                    else{var next=data.Clone();MigrateCompatibility(next);ValidateFile(next);data=next;}
                    startupDiagnostics.MigrationSeconds+=Clock()-stage;
                }
            }
            catch(OperationCanceledException){throw;}
            catch(Exception e) { data=NewFile();StorageError="История не загружена; исходный файл сохранён: "+e.Message; }
            startupCancellation.ThrowIfCancellationRequested();
            if(this.releases!=null)
            {
                bool seedAuthoring=authoring&&!File.Exists(source);
                stage=Clock();
                if(seedAuthoring)data=SeedAuthoring();
                else if(!authoring)MergeReleases(data);
                startupDiagnostics.MergeSeconds=Clock()-stage;
                bool migrated=!authoring&&data.Profiles.Any(p=>p.Revisions.Any(r=>IsShipped(r)&&!Current(r.Snapshot)));
                if(migrated){stage=Clock();MigrateCompatibility(data);startupDiagnostics.MigrationSeconds+=Clock()-stage;}
                if(seedAuthoring)
                    foreach(var profile in data.Profiles)
                        foreach(var revision in profile.Revisions.Where(r=>IsShipped(r)&&Current(r.Snapshot)))revision.ReleaseCandidate=true;
                if(resetToLatestDefault&&(!authoring||ReleaseError==null))SelectLatestDefault(data);
                // Existing local snapshots and catalogue snapshots remain fully validated.
                stage=Clock();ValidateFile(data,migrated);startupDiagnostics.FinalValidationSeconds=Clock()-stage;
            }
            startupCancellation=default;
            startupDiagnostics.TotalSeconds=Clock()-start;startupDiagnostics.ManagedMemoryDelta=GC.GetTotalMemory(false)-memory;startupDiagnostics.Gen0Collections=GC.CollectionCount(0)-collections;
            startupDiagnostics.CachedRegistrySets=historicalForInstance.Count;
            startupDiagnostics.CachedCandidateBundles=historicalForInstance.Values.Sum(a=>a.Length);
            var retainedProfiles=historicalForInstance.Values.SelectMany(a=>a).SelectMany(b=>b.Profiles).Distinct().ToArray();
            startupDiagnostics.CachedDistinctProfiles=retainedProfiles.Length;startupDiagnostics.CachedDescriptors=retainedProfiles.Sum(p=>p.Descriptors.Count);
            collectingStartupDiagnostics=false;
        }
        void ValidateReleases()
        {
            if(releases.Schema!=1||releases.Entries==null||releases.Entries.Count==0||releases.Entries.Any(e=>e==null||e.Sequence<1||e.Revision<1||string.IsNullOrWhiteSpace(e.ProfileId)||string.IsNullOrWhiteSpace(e.ProfileName)||e.ProfileName.Length>48||e.Snapshot==null||e.Hash!=e.Snapshot.Hash())
                ||releases.Entries.Select(e=>e.Sequence).Distinct().Count()!=releases.Entries.Count
                ||!releases.Entries.Any(e=>e.ProfileId==ReleaseId))throw new InvalidDataException("Некорректный релизный каталог");
            string key;
            using(var sha=SHA256.Create())key=Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(shipped)+"\n"+JsonUtility.ToJson(releases))));
            lock(releaseValidationLock)
            {
                if(validatedReleases.TryGetValue(key,out var cached)){releases.Entries=cached.Clone().Entries;return;}
                foreach(var e in releases.Entries)
                {
                    startupCancellation.ThrowIfCancellationRequested();
                    var file=NewFile();var r=Revision(e.Snapshot,2,e.Date);file.Profiles[0].Revisions.Add(r);
                    ValidateFile(file);e.Snapshot=r.Snapshot;
                    if(e.Hash!=e.Snapshot.Hash())throw new InvalidDataException("Релизный hash не соответствует доверенному реестру");
                }
                if(validatedReleases.Count>=4)validatedReleases.Clear();
                validatedReleases.Add(key,releases.Clone());
            }
        }
        public bool IsShipped(LabRevision revision)=>releases!=null&&releases.Entries.Any(e=>e.Sequence==revision.ReleaseSequence&&e.Hash==revision.ReleaseHash);
        static void BindRelease(LabRevision revision,LabReleaseEntry entry)
        {revision.ReleaseSequence=entry.Sequence;revision.ReleaseNumber=entry.Revision;revision.ReleaseHash=entry.Hash;revision.ReleaseDate=entry.Date;revision.ReleaseCandidate=false;}
        void MergeReleases(LabHistoryFile file)
        {
            // Never trust locally stored claims of being shipped. Rebind only from the packaged catalogue.
            foreach(var p in file.Profiles)foreach(var r in p.Revisions){r.ReleaseSequence=0;r.ReleaseNumber=0;r.ReleaseHash=null;r.ReleaseDate=null;}
            foreach(var e in releases.Entries.OrderBy(e=>e.Sequence))
            {
                var p=file.Profiles.FirstOrDefault(p=>p.Id==e.ProfileId);
                if(p==null){p=new LabHistoryProfile{Id=e.ProfileId,Name=e.ProfileName,Protected=e.ProfileId==ReleaseId};file.Profiles.Add(p);}
                p.Name=e.ProfileName;
                var r=p.Revisions.FirstOrDefault(r=>r.Hash==e.Hash&&r.ReleaseSequence==0);
                if(r==null)
                {
                    int number=p.Revisions.Any(r=>r.Number==e.Revision)?p.Revisions.Max(r=>r.Number)+1:e.Revision;
                    r=Revision(e.Snapshot,number,e.Date);p.Revisions.Add(r);
                }
                BindRelease(r,e);
            }
        }
        void SelectLatestDefault(LabHistoryFile file)
        {
            if(authoring)
            {
                var candidate=AuthorDefaultExecution(file);
                if(candidate==null)throw new InvalidOperationException("В проекте нет совместимой релизной версии Default");
                file.SelectedId=ReleaseId;file.SelectedRevision=candidate.Number;return;
            }
            if(releases==null){file.SelectedId=ReleaseId;file.SelectedRevision=file.Profiles.Single(p=>p.Id==ReleaseId).Revisions.First(r=>Current(r.Snapshot)&&r.Hash==shipped.Hash()).Number;return;}
            int sequence=releases.Entries.Where(e=>e.ProfileId==ReleaseId).OrderByDescending(e=>e.Revision).ThenByDescending(e=>e.Sequence).First().Sequence;
            file.SelectedId=ReleaseId;file.SelectedRevision=file.Profiles.Single(p=>p.Id==ReleaseId).Revisions.First(r=>r.ReleaseSequence==sequence&&Current(r.Snapshot)).Number;
        }
        public void MarkSelectedForRelease(bool marked)
        {
            if(authoring){MarkForRelease(data.SelectedId,data.SelectedRevision,marked);return;}
            RequireLocalProfile();
            if(!authoring&&IsShipped(Selected))throw new InvalidOperationException("Ревизия уже встроена в клиент");
            if(!Current(Selected.Snapshot))throw new InvalidOperationException("Выберите совместимую сохранённую ревизию");
            Commit(f=>SetReleaseFlag(f,f.SelectedId,f.SelectedRevision,marked));
        }
        // Most matches use the current registry. Generate historical combinations only when
        // a saved revision actually needs one, then share the exact immutable registry set.
        string TrustedRegistryFingerprint()
        {
            var canonical=this.shipped.Hash()+"|"+JsonUtility.ToJson(this.shipped);
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical))).Replace("-","").ToLowerInvariant();
        }
        LabBundle[] HistoricalPredecessors(LabBundle sought,bool shapeOnly=false)
        {
            double start=Clock();
            try{return BuildHistoricalPredecessors(sought,shapeOnly);}
            finally{if(collectingStartupDiagnostics)startupDiagnostics.HistoricalLookupSeconds+=Clock()-start;}
        }
        LabBundle[] BuildHistoricalPredecessors(LabBundle sought,bool shapeOnly=false)
        {
            // Compact fingerprint covers the same full trusted values and metadata as before.
            // Only candidate registries are cached: every saved hash/value still validates below.
            string key=(shippedCacheKey??(shippedCacheKey=TrustedRegistryFingerprint()))+"|"+
                string.Join("|",sought.Profiles.Select(p=>p.Id+"@"+p.Version))+"|"+
                string.Join("|",sought.Descriptors.Select(d=>d.Path))+(shapeOnly?"|storage-shape":"");
            if(historicalForInstance.TryGetValue(key,out var local))
            {if(collectingStartupDiagnostics)startupDiagnostics.RegistryCacheHits++;return local;}
            LabBundle[] Remember(LabBundle[] registries)
            {
                if(!historicalForInstance.ContainsKey(key))
                {
                    if(historicalForInstance.Count>=InstanceRegistryLimit)historicalForInstance.Remove(historicalInstanceOrder.Dequeue());
                    historicalInstanceOrder.Enqueue(key);
                }
                historicalForInstance[key]=registries;
                lock(historicalLock)
                {
                    if(!historicalCache.ContainsKey(key)&&historicalCache.Count>=GlobalRegistryLimit)
                        historicalCache.Remove(historicalCache.Keys.First());
                    historicalCache[key]=registries;
                }
                return registries;
            }
            lock(historicalLock)
                if(historicalCache.TryGetValue(key,out var warm))
                {if(collectingStartupDiagnostics)startupDiagnostics.RegistryCacheHits++;return Remember(warm);}
            if(collectingStartupDiagnostics)startupDiagnostics.RegistryCacheMisses++;
            var shipped=this.shipped;
            if(!sought.Descriptors.Any(d=>d.Path=="visual.bowlLocalLightScale"))
                shipped=new LabBundle{Profiles=shipped.Profiles.Select(p=>p.BeforeArenaHarmonization()).ToList()};
            if((sought.Profiles.FirstOrDefault(p=>p.Id=="unity-trooper-presentation-v1")?.Version??8)<8)
                shipped=new LabBundle{Profiles=shipped.Profiles.Select(p=>p.BeforeEquipmentReadability()).ToList()};

            if((sought.Profiles.FirstOrDefault(p=>p.Id=="well-gameplay-presentation-v1")?.Version??5)<5)
                shipped=new LabBundle{Profiles=shipped.Profiles.Select(p=>p.BeforeWellDaylight()).ToList()};
            if((sought.Profiles.FirstOrDefault(p=>p.Id=="well-gameplay-presentation-v1")?.Version??4)<4)
                shipped=new LabBundle{Profiles=shipped.Profiles.Select(p=>p.BeforeWellIdleDeparture()).ToList()};
            if((sought.Profiles.FirstOrDefault(p=>p.Id=="well-gameplay-presentation-v1")?.Version??3)<3)
                shipped=new LabBundle{Profiles=shipped.Profiles.Select(p=>p.BeforeWellDepth()).ToList()};
            if((sought.Profiles.FirstOrDefault(p=>p.Id=="well-gameplay-presentation-v1")?.Version??2)<2)
                shipped=new LabBundle{Profiles=shipped.Profiles.Select(p=>p.BeforeWellDetail()).ToList()};

            if(sought.Profiles.FirstOrDefault(p=>p.Id=="well-authoring-v1")?.Version==3)
                shipped=new LabBundle{Profiles=shipped.Profiles.Select(p=>p.BeforeWellCorridorCover()).ToList()};
            if(sought.Profiles.FirstOrDefault(p=>p.Id=="well-authoring-v1")?.Version==2)
                shipped=new LabBundle{Profiles=shipped.Profiles.Select(p=>p.BeforeWellFurniture()).ToList()};

            // Revision 1 is code-owned immutable geometry; saved hashes/metadata must remain exact.
            if(sought.Profiles.FirstOrDefault(p=>p.Id=="well-authoring-v1")?.Version==1)
                shipped=new LabBundle{Profiles=shipped.Profiles.Select(p=>p.BeforeWellStructure()).ToList()};

            // Exact trusted predecessor: addition of both Well profiles preserves all previous metadata and hashes.
            if(!sought.Profiles.Any(p=>p.Id=="well-authoring-v1"||p.Id=="well-gameplay-presentation-v1"))
                shipped=new LabBundle{Profiles=shipped.Profiles.Where(p=>p.Id!="well-authoring-v1"&&p.Id!="well-gameplay-presentation-v1").ToList()};
            if(!sought.Descriptors.Any(d=>d.Path==NativeVisualPreferences.Path(VisualEffect.ColorGrading)))
                shipped=new LabBundle{Profiles=shipped.Profiles.Select(p=>p.BeforeNeutralColorGrading()).ToList()};
            if(!sought.Descriptors.Any(d=>d.Path==NativeVisualPreferences.Path(VisualEffect.Lighting)))
                shipped=new LabBundle{Profiles=shipped.Profiles.Select(p=>p.BeforeVisualPolish()).ToList()};
            if((sought.Profiles.FirstOrDefault(p=>p.Id=="unity-native-match-v1")?.Version??3)<3)
                shipped=new LabBundle{Profiles=shipped.Profiles.Select(p=>p.BeforeTimedKillChains()).ToList()};
            // Project the exact trusted predecessor before the existing legacy search.
            // Historical snapshots/hashes stay intact; migration appends the new control.
            if(!sought.Descriptors.Any(d=>d.Path==ProvingProfile.BotBanterIntervalPath))
                shipped=new LabBundle{Profiles=shipped.Profiles.Select(p=>p.BeforeBotBanterInterval()).ToList()};
            if(!sought.Descriptors.Any(d=>d.Path=="audio.weaponGain"))
                shipped=new LabBundle{Profiles=shipped.Profiles.Select(p=>p.BeforeCombatAudioMix()).ToList()};
            if(!sought.Descriptors.Any(d=>d.Path=="audio.weaponReadyGain"))
                shipped=new LabBundle{Profiles=shipped.Profiles.Select(p=>p.BeforeWeaponReadyAudioGain()).ToList()};
            if(!sought.Descriptors.Any(d=>d.Path=="audio.deathGain"))
                shipped=new LabBundle{Profiles=shipped.Profiles.Select(p=>p.BeforeDeathAudioGain()).ToList()};
            if((sought.Profiles.FirstOrDefault(p=>p.Id=="unity-trooper-presentation-v1")?.Version??7)<7)
                shipped=new LabBundle{Profiles=shipped.Profiles.Select(p=>p.BeforeSmoothFirstPersonWalk()).ToList()};
            if(RegistryMatches(shipped,sought))return Remember(new[]{shipped});
            // The preceding LT registries lack only the new timing/gesture descriptors.
            // Match the trusted shipped projection first, without expanding all older schemas.
            // Values and metadata still pass the unchanged ValidateFile checks below.
            int previousVersion=sought.Profiles.FirstOrDefault(p=>p.Id==ProvingProfile.DefaultId)?.Version??0;
            if(previousVersion==14||previousVersion==13)
            {
                var previous=new LabBundle{Profiles=shipped.Profiles.Select(p=>previousVersion==14?p.BeforeSmoothGamepadTap():p.BeforeGamepadTriggerAim()).ToList()};
                if(RegistryMatches(previous,sought))return Remember(new[]{previous});
            }
            // The first weapon-switch releases are exact linear predecessors, but the general
            // compatibility search below also explores later additive branches. Recognize these
            // trusted schemas before expanding that graph: opening an old local Lab profile must
            // not turn into minutes of metadata-only candidate generation.
            var preRebalance=new LabBundle{Profiles=shipped.Profiles.Select(p=>p.BeforeWeaponRebalance()).ToList()};
            var beforePalette=new LabBundle{Profiles=preRebalance.Profiles.Where(p=>p.Id!=ProvingProfile.ParticipantPaletteId).ToList()};
            var preSwitch=new LabBundle{Profiles=beforePalette.Profiles.Select(p=>p.BeforeWeaponSwitch()).ToList()};
            var preCutterThickness=new LabBundle{Profiles=beforePalette.Profiles.Select(p=>p.BeforeCutterThickness()).ToList()};
            var preCutterThicknessSwitch=new LabBundle{Profiles=preSwitch.Profiles.Select(p=>p.BeforeCutterThickness()).ToList()};
            var preCutter=new LabBundle{Profiles=beforePalette.Profiles.Where(p=>p.Id!="cutter-beam-v1").ToList()};
            var preCutterSwitch=new LabBundle{Profiles=preSwitch.Profiles.Where(p=>p.Id!="cutter-beam-v1").ToList()};
            var prePulse=new LabBundle{Profiles=preCutterSwitch.Profiles.Select(p=>p.BeforePulse()).ToList()};
            var preHeal=new LabBundle{Profiles=preCutterSwitch.Profiles.Select(p=>p.BeforeFullHeal()).ToList()};
            var medium=new LabBundle{Profiles=preRebalance.Profiles.Select(p=>p.BeforeMediumWeaponSwitch()).ToList()};
            var low=new LabBundle{Profiles=beforePalette.Profiles.Select(p=>p.BeforeLowWeaponSwitch()).ToList()};
            var shoulder=new LabBundle{Profiles=beforePalette.Profiles.Select(p=>p.BeforeShoulderSwitch()).ToList()};
            foreach(var previous in new[]{medium,low,shoulder,preSwitch,preCutter,preCutterThickness,preCutterThicknessSwitch,prePulse,preHeal})
                if(RegistryMatches(previous,sought))return Remember(new[]{previous});
            lock(historicalLock)
            {
                if(historicalCache.TryGetValue(key,out var cached))return Remember(cached);
                var mediumBeforePalette=new LabBundle{Profiles=medium.Profiles.Where(p=>p.Id!=ProvingProfile.ParticipantPaletteId).ToList()};
                predecessors=new[]{preRebalance,beforePalette,medium,mediumBeforePalette,new LabBundle{Profiles=mediumBeforePalette.Profiles.Select(p=>p.BeforeCutterThickness()).ToList()},new LabBundle{Profiles=mediumBeforePalette.Profiles.Where(p=>p.Id!="cutter-beam-v1").ToList()},low,new LabBundle{Profiles=low.Profiles.Select(p=>p.BeforeCutterThickness()).ToList()},new LabBundle{Profiles=low.Profiles.Where(p=>p.Id!="cutter-beam-v1").ToList()},preSwitch,preCutterThickness,preCutterThicknessSwitch,preCutter,preCutterSwitch,prePulse,preHeal,shoulder,new LabBundle{Profiles=shoulder.Profiles.Select(p=>p.BeforeCutterThickness()).ToList()},new LabBundle{Profiles=shoulder.Profiles.Where(p=>p.Id!="cutter-beam-v1").ToList()}};
                // Accept exact prior registries across additive palette and weapon-pickup schema upgrades.
                var pickupPredecessors=new Dictionary<ProvingProfile,ProvingProfile>();
                ProvingProfile WithoutPickupProfile(ProvingProfile profile)
                {
                    if(profile.Id!="unity-combat-state-v1"&&profile.Id!="combat-bowl-ring-r7-authoring-v1")return profile;
                    if(!pickupPredecessors.TryGetValue(profile,out var previous))
                    {previous=profile.BeforeWeaponPickups();pickupPredecessors.Add(profile,previous);}
                    return previous;
                }
                LabBundle WithoutPickups(LabBundle b)=>new LabBundle{Profiles=b.Profiles.Select(WithoutPickupProfile).ToList()};
                LabBundle WithPalette(LabBundle b)
                {
                    var copy=new LabBundle{Profiles=b.Profiles.ToList()};if(shipped.Profiles.Any(p=>p.Id==ProvingProfile.ParticipantPaletteId)&&!copy.Profiles.Any(p=>p.Id==ProvingProfile.ParticipantPaletteId))
                        copy.Profiles.Add(shipped.Profiles.Single(p=>p.Id==ProvingProfile.ParticipantPaletteId));
                    return copy;
                }
                // Keep exact historical registries and additive schema combinations; version tuples
                // alone cannot distinguish a new descriptor on an otherwise unchanged profile.
                var vignettePredecessors=new Dictionary<ProvingProfile,ProvingProfile>();
                ProvingProfile WithoutVignette(ProvingProfile profile)
                {
                    if(!vignettePredecessors.TryGetValue(profile,out var previous))
                    {previous=profile.BeforeDamageVignette();vignettePredecessors.Add(profile,previous);}
                    return previous;
                }
                var previousDeath=shipped.Profiles.FirstOrDefault(p=>p.Id==ProvingProfile.DeathPresentationId)?.BeforeDeathImpulseIncrease();
                // These profiles are immutable trusted candidates, not the file's mutable metadata.
                // Reuse their schema fragments across branches, preserving the exact flattened key.
                var profilePaths=new Dictionary<ProvingProfile,string>();
                string ProfilePaths(ProvingProfile profile)
                {
                    if(!profilePaths.TryGetValue(profile,out var paths))
                    {paths=string.Join("|",profile.Descriptors.Select(d=>d.Path));profilePaths.Add(profile,paths);}
                    return paths;
                }
                string RegistryKey(LabBundle b)=>string.Join("|",b.Profiles.Select(p=>p.Id+"@"+p.Version))+"|"+
                    string.Join("|",b.Profiles.Select(ProfilePaths).Where(paths=>paths.Length!=0));
                LabBundle[] DistinctRegistries(IEnumerable<LabBundle> bundles)=>bundles.GroupBy(RegistryKey).Select(g=>g.First()).ToArray();
                // Historical combinations share immutable profiles. Memoize each pure schema transform
                // within this migration so equivalent branches do not repeat native JSON serialization.
                var targetPaths=new HashSet<string>(sought.Descriptors.Select(d=>d.Path),StringComparer.Ordinal);
                var targetIds=new HashSet<string>(sought.Profiles.Select(p=>p.Id),StringComparer.Ordinal);
                var profileDistances=new Dictionary<ProvingProfile,int>();
                int ProfileDistance(ProvingProfile profile)
                {
                    if(!profileDistances.TryGetValue(profile,out int distance))
                    {
                        // Trusted registries have disjoint unique paths and IDs. Symmetric difference
                        // is |target| + sum(|profile| - 2 * intersection); only target depends on the file.
                        distance=profile.Descriptors.Sum(d=>targetPaths.Contains(d.Path)?-1:1)+
                            (targetIds.Contains(profile.Id)?-1:1);
                        profileDistances.Add(profile,distance);
                    }
                    return distance;
                }
                int SchemaDistance(LabBundle bundle)=>targetPaths.Count+targetIds.Count+bundle.Profiles.Sum(ProfileDistance);
                IEnumerable<LabBundle> Closest(params LabBundle[] alternatives)
                {
                    var distances=alternatives.Select(SchemaDistance).ToArray();int minimum=distances.Min();
                    for(int i=0;i<alternatives.Length;i++)if(distances[i]==minimum)yield return alternatives[i];
                }
                LabBundle Change(LabBundle b,Func<ProvingProfile,ProvingProfile> transform)=>new LabBundle{Profiles=b.Profiles.Select(transform).ToList()};
                Func<ProvingProfile,ProvingProfile> Cached(Func<ProvingProfile,ProvingProfile> transform)
                {
                    var cache=new Dictionary<ProvingProfile,ProvingProfile>();
                    return profile=>{startupCancellation.ThrowIfCancellationRequested();if(!cache.TryGetValue(profile,out var previous)){previous=transform(profile);cache.Add(profile,previous);}return previous;};
                }
                var withoutModeTargets=Cached(p=>p.BeforeModeTargets());
                var beforeMatchAchievements=Cached(p=>p.BeforeMatchAchievements());
                var beforeBotMovement=Cached(p=>p.BeforeBotCombatMovement());
                var withoutEvaluation=Cached(p=>p.BeforeBotWeaponEvaluation());
                var withoutTactics=Cached(p=>p.BeforeBotTactics());var withoutOrigins=Cached(p=>p.BeforeShotOrigins());
                var withoutCutterDamage=Cached(p=>p.BeforeCutterDamage());
                var withoutScoreboard=Cached(p=>p.BeforeScoreboard());
                var withoutBonusAlerts=Cached(p=>p.BeforeDamageBonusAlerts());
                var withoutMusic=Cached(p=>p.BeforeRoundMusic());
                var withoutMenuMusic=Cached(p=>p.BeforeMenuMusic());
                var withoutMusicBalance=Cached(p=>p.BeforeMusicBalance());
                var withoutMovementAudio=Cached(p=>p.BeforeMovementAudio());
                var withoutCompactMatchMenu=Cached(p=>p.BeforeCompactMatchMenu());
                var withoutFootstepMix=Cached(p=>p.BeforeFootstepMix());
                var withoutSmoothGamepadTap=Cached(p=>p.BeforeSmoothGamepadTap());
                var withoutGamepadTriggerAim=Cached(p=>p.BeforeGamepadTriggerAim());
                var withoutGamepadLook=Cached(p=>p.BeforeGamepadLook());
                var withoutUnifiedDamage=Cached(p=>p.BeforeUnifiedBodyDamage());
                var withoutDiscreteIdentityPanels=Cached(p=>p.BeforeDiscreteIdentityPanels());
                var withoutPulseVisibility=Cached(p=>p.BeforePulseVisibility());
                var withoutPulseRefinement=Cached(p=>p.BeforePulseRefinement());
                var withoutIdentitySurface=Cached(p=>p.BeforeIdentitySurface());
                var beforeBloodIntensity=Cached(p=>p.BeforeBloodIntensityIncrease());
                // Recent additive upgrades have an exact trusted registry. Prove the
                // complete metadata/hash before skipping the combinatorial legacy search.
                // Values alone or matching path counts never establish this shortcut.
                predecessors=DistinctRegistries(predecessors.SelectMany(b=>Closest(b,Change(b,withoutMusicBalance))));
                var currentGameplay=Closest(shipped,Change(shipped,withoutPulseVisibility),Change(shipped,withoutPulseRefinement),new LabBundle{Profiles=shipped.Profiles.Where(p=>p.Id!=ProvingProfile.RocketEffectsId).ToList()});
                currentGameplay=DistinctRegistries(currentGameplay.SelectMany(b=>Closest(b,Change(b,withoutMusicBalance))));
                currentGameplay=DistinctRegistries(currentGameplay.SelectMany(b=>Closest(b,Change(b,withoutSmoothGamepadTap))));
                currentGameplay=DistinctRegistries(currentGameplay.SelectMany(b=>Closest(b,Change(b,withoutGamepadTriggerAim))));
                currentGameplay=DistinctRegistries(currentGameplay.SelectMany(b=>Closest(b,Change(b,withoutModeTargets))));
                currentGameplay=DistinctRegistries(currentGameplay.SelectMany(b=>Closest(b,Change(b,beforeMatchAchievements))));
                // Music can be absent alongside an earlier Pulse registry without changing gameplay defaults.
                currentGameplay=DistinctRegistries(currentGameplay.SelectMany(b=>Closest(b,Change(b,withoutMusic))));
                currentGameplay=DistinctRegistries(currentGameplay.SelectMany(b=>Closest(b,Change(b,withoutMenuMusic))));
                // Blood defaults can be retuned independently of the gameplay registry upgrade.
                // Include that exact combination without rewriting historical snapshots.
                currentGameplay=currentGameplay.SelectMany(b=>Closest(b,Change(b,beforeBotMovement)));
                currentGameplay=currentGameplay.SelectMany(b=>Closest(b,Change(b,withoutEvaluation)));
                var recentGameplay=currentGameplay.SelectMany(b=>Closest(b,Change(b,beforeBloodIntensity))).ToArray();
                foreach(var registry in recentGameplay)
                {
                    if(!RegistryMatches(registry,sought))continue;
                    if(shapeOnly)return Remember(recentGameplay);
                    // Gameplay hashes omit descriptor metadata. Compare the complete
                    // trusted descriptors separately before taking the recent-schema path.
                    var supplied=sought.Descriptors.ToDictionary(d=>d.Path,StringComparer.Ordinal);
                    if(registry.Descriptors.Any(d=>JsonUtility.ToJson(d)!=JsonUtility.ToJson(supplied[d.Path])))continue;
                    var trusted=registry.Clone();
                    foreach(var descriptor in trusted.Descriptors)trusted.Set(descriptor.Path,sought.Get(descriptor.Path));
                    if(trusted.Hash()!=sought.Hash())continue;
                    return Remember(recentGameplay);
                }
                // Collapse exact duplicate registries after each additive branch. Delaying this to
                // the end serializes the same historical profile thousands of times on Lab open.
                predecessors=DistinctRegistries(predecessors.SelectMany(b=>Closest(b,Change(b,withoutModeTargets))));
                predecessors=DistinctRegistries(predecessors.SelectMany(b=>Closest(b,Change(b,beforeMatchAchievements))));
                predecessors=DistinctRegistries(predecessors.SelectMany(b=>Closest(b,Change(b,withoutPulseVisibility))));
                predecessors=DistinctRegistries(predecessors.SelectMany(b=>Closest(b,Change(b,withoutPulseRefinement))));
                predecessors=DistinctRegistries(predecessors.SelectMany(b=>Closest(b,new LabBundle{Profiles=b.Profiles.Where(p=>p.Id!=ProvingProfile.RocketEffectsId).ToList()})));
                predecessors=DistinctRegistries(predecessors.SelectMany(b=>Closest(b,WithoutPickups(b),WithPalette(b),WithPalette(WithoutPickups(b)))));
                predecessors=DistinctRegistries(predecessors.SelectMany(b=>Closest(b,Change(b,beforeBotMovement))));
                predecessors=DistinctRegistries(predecessors.SelectMany(b=>Closest(b,Change(b,withoutEvaluation))));
                predecessors=DistinctRegistries(predecessors.SelectMany(b=>Closest(b,new LabBundle{Profiles=b.Profiles.Where(p=>p.Id!="native-bot-evaluation-v1").Select(withoutTactics).ToList()})));
                predecessors=DistinctRegistries(predecessors.SelectMany(b=>Closest(b,Change(b,withoutMusic))));
                predecessors=DistinctRegistries(predecessors.SelectMany(b=>Closest(b,Change(b,withoutMenuMusic))));
                predecessors=DistinctRegistries(predecessors.SelectMany(b=>Closest(b,Change(b,withoutBonusAlerts))));
                predecessors=DistinctRegistries(predecessors.SelectMany(b=>Closest(b,Change(b,withoutMovementAudio))));
                predecessors=DistinctRegistries(predecessors.SelectMany(b=>Closest(b,Change(b,withoutCompactMatchMenu))));
                predecessors=DistinctRegistries(predecessors.SelectMany(b=>Closest(b,Change(b,withoutFootstepMix))));
                predecessors=DistinctRegistries(predecessors.SelectMany(b=>Closest(b,Change(b,WithoutVignette))));
                predecessors=DistinctRegistries(predecessors.SelectMany(b=>Closest(b,Change(b,withoutOrigins))));
                predecessors=DistinctRegistries(predecessors.SelectMany(b=>Closest(b,Change(b,withoutCutterDamage))));
                predecessors=DistinctRegistries(predecessors.SelectMany(b=>Closest(b,Change(b,withoutScoreboard))));
                predecessors=DistinctRegistries(predecessors.SelectMany(b=>Closest(b,Change(b,withoutGamepadLook))));
                predecessors=DistinctRegistries(predecessors.SelectMany(b=>Closest(b,Change(b,withoutSmoothGamepadTap))));
                predecessors=DistinctRegistries(predecessors.SelectMany(b=>Closest(b,Change(b,withoutGamepadTriggerAim))));
                predecessors=DistinctRegistries(predecessors.SelectMany(b=>Closest(b,Change(b,withoutDiscreteIdentityPanels))));
                predecessors=DistinctRegistries(predecessors.SelectMany(b=>Closest(b,Change(b,withoutIdentitySurface))));
                predecessors=DistinctRegistries(predecessors.SelectMany(b=>Closest(b,Change(b,p=>p.Id==ProvingProfile.DeathPresentationId?previousDeath:p))));
                predecessors=DistinctRegistries(predecessors.SelectMany(b=>Closest(b,new LabBundle{Profiles=b.Profiles.Where(p=>p.Id!=ProvingProfile.DeathPresentationId).ToList()})));
                // Preserve old immutable Lab hashes when the presentation-only audio descriptors
                // are added to the default profile; every earlier registry remains a valid source.
                predecessors=DistinctRegistries(predecessors.SelectMany(b=>Closest(b,Change(b,beforeBloodIntensity))));
                predecessors=DistinctRegistries(predecessors.SelectMany(b=>Closest(b,new LabBundle{Profiles=b.Profiles.Where(p=>p.Id!=ProvingProfile.BloodPresentationId).ToList()})));
                var withoutAudio=Cached(p=>p.BeforeAudio());
                predecessors=DistinctRegistries(predecessors.SelectMany(b=>Closest(b,Change(b,withoutAudio))));
                predecessors=DistinctRegistries(predecessors.SelectMany(b=>Closest(b,Change(b,withoutUnifiedDamage))));
                var beforeTravelingRifle=Cached(p=>p.BeforeTravelingRifle());
                predecessors=DistinctRegistries(predecessors.SelectMany(b=>Closest(b,Change(b,beforeTravelingRifle))));
                // Add the current gameplay registry only after historical transforms: identical
                // legacy schema keys can otherwise discard the actual pre-rebalance defaults.
                predecessors=predecessors.Concat(currentGameplay.SelectMany(b=>Closest(b,Change(b,beforeBloodIntensity)))).ToArray();
                var beforeTunnelWayfinding=Cached(p=>p.BeforeTunnelWayfinding());
                predecessors=predecessors.SelectMany(b=>Closest(b,Change(b,beforeTunnelWayfinding))).ToArray();
                // Preserve distinct legacy defaults even when registry keys coincide.
                predecessors=predecessors.SelectMany(b=>Closest(b,new LabBundle{Profiles=b.Profiles.Where(p=>p.Id!=ProvingProfile.TunnelsArtId&&p.Id!=ProvingProfile.TunnelsAuthoringId).ToList()})).ToArray();
                var beforeLunarBypass=Cached(p=>p.BeforeLunarBypass());
                predecessors=predecessors.SelectMany(b=>Closest(b,Change(b,beforeLunarBypass))).ToArray();
                var beforeLunarLighting=Cached(p=>p.BeforeLunarLighting());
                predecessors=predecessors.SelectMany(b=>Closest(b,Change(b,beforeLunarLighting))).ToArray();
                var beforeLunarFlush=Cached(p=>p.BeforeLunarFlush());
                predecessors=predecessors.SelectMany(b=>Closest(b,Change(b,beforeLunarFlush))).ToArray();
                var beforeLunarInterior=Cached(p=>p.BeforeLunarInterior());
                predecessors=predecessors.SelectMany(b=>Closest(b,Change(b,beforeLunarInterior))).ToArray();
                var beforeLunarOpenCentre=Cached(p=>p.BeforeLunarOpenCentre());
                predecessors=predecessors.SelectMany(b=>Closest(b,Change(b,beforeLunarOpenCentre))).ToArray();
                var beforeLunarOffice=Cached(p=>p.BeforeLunarOffice());
                predecessors=predecessors.SelectMany(b=>Closest(b,Change(b,beforeLunarOffice))).ToArray();
                var beforeLunarRework=Cached(p=>p.BeforeLunarRework());
                predecessors=predecessors.SelectMany(b=>Closest(b,Change(b,beforeLunarRework))).ToArray();
                if(shipped.Profiles.Any(p=>p.Id=="lunar-laboratory-authoring-v1"))
                    predecessors=predecessors.SelectMany(b=>Closest(b,new LabBundle{Profiles=b.Profiles.Where(p=>p.Id!="lunar-laboratory-authoring-v1"&&p.Id!="lunar-laboratory-presentation-v1").ToList()})).ToArray();
                // Adding the contact profile must not rewrite any previous immutable registry/hash.
                predecessors=predecessors.SelectMany(b=>Closest(b,new LabBundle{Profiles=b.Profiles.Where(p=>p.Id!=ProvingProfile.HitFeedbackPresentationId).ToList()})).ToArray();
                return Remember(predecessors);
            }
        }
        LabHistoryFile NewFile()=>new LabHistoryFile{SelectedId=ReleaseId,SelectedRevision=1,Profiles=new List<LabHistoryProfile>{
            new LabHistoryProfile{Id=ReleaseId,Name="Default",Protected=true,Revisions=new List<LabRevision>{Revision(shipped,1,null)}}}};
        static LabRevision Revision(LabBundle bundle,int number,string date)=>new LabRevision{Number=number,Date=date,Hash=bundle.Hash(),Snapshot=bundle.Clone()};
        void ValidateFile(LabHistoryFile file,bool validateSnapshots=true)
        {
            if(file==null||file.Schema!=1||file.Profiles==null||file.Profiles.Count==0)throw new InvalidDataException("Неподдерживаемая схема");

            if(file.Profiles.Select(p=>p.Id).Distinct().Count()!=file.Profiles.Count)throw new InvalidDataException("Повтор ID");
            foreach(var p in file.Profiles)
            {
                startupCancellation.ThrowIfCancellationRequested();
                if(string.IsNullOrWhiteSpace(p.Id)||string.IsNullOrWhiteSpace(p.Name)||p.Revisions==null||p.Revisions.Count==0||p.Protected!=(p.Id==ReleaseId))throw new InvalidDataException("Некорректный профиль");
                if(p.Revisions.Select(r=>r.Number).Distinct().Count()!=p.Revisions.Count)throw new InvalidDataException("Повтор ревизии");
                foreach(var r in p.Revisions)
                {
                    startupCancellation.ThrowIfCancellationRequested();
                    double hashStart=Clock();var actualHash=r.Snapshot?.Hash();
                if(collectingStartupDiagnostics){startupDiagnostics.HashSeconds+=Clock()-hashStart;startupDiagnostics.SnapshotHashes++;}
                if(r.Number<1||r.Snapshot==null||r.Hash!=actualHash)throw new InvalidDataException("Снимок или hash повреждён");
                    ValidateCompatibilitySource(p,r);
                    if(!validateSnapshots)continue;
                    var registry=StorageRegistry(r.Snapshot);
                    if(!RegistryMatches(registry,r.Snapshot))
                    {
                        var paths=registry.Descriptors.Select(d=>d.Path).OrderBy(k=>k).ToArray();
                        throw new InvalidDataException("Снимок или hash повреждён: missing="+string.Join(",",paths.Except(r.Snapshot.Descriptors.Select(d=>d.Path)))+"; extra="+string.Join(",",r.Snapshot.Descriptors.Select(d=>d.Path).Except(paths)));
                    }
                    // Metadata is trusted only from the shipped registry, never from the file.
                    double copyStart=Clock();var trusted=registry.Clone();foreach(var d in trusted.Descriptors)trusted.Set(d.Path,r.Snapshot.Get(d.Path));
                    if(collectingStartupDiagnostics)startupDiagnostics.TrustedCopySeconds+=Clock()-copyStart;
                    if(trusted.Descriptors.Any(d=>(trusted.IsAuthoring(d.Path)||trusted.IsDiagnostic(d.Path))&&trusted.Get(d.Path)!=registry.Get(d.Path)))throw new InvalidDataException("Read-only metadata changed");
                    double validationStart=Clock();var issues=trusted.Validate();
                    if(collectingStartupDiagnostics){startupDiagnostics.SnapshotValidationSeconds+=Clock()-validationStart;startupDiagnostics.SnapshotValidations++;}
                    if(issues.Count!=0)throw new InvalidDataException("Несовместимый снимок "+p.Id+" v"+r.Number+": "+string.Join("; ",issues.Take(1).Select(i=>i.Path+": "+i.Message))+(issues.Count>1?"; ещё ошибок: "+(issues.Count-1):""));r.Snapshot=trusted;
                }
            }
            var release=file.Profiles.Single(p=>p.Id==ReleaseId).Revisions.Single(r=>r.Number==1);
            if(release.Hash!=shipped.Hash()&&release.Hash!=StorageRegistry(release.Snapshot).Hash()&&!HistoricalPredecessors(release.Snapshot).Any(b=>release.Hash==b.Hash()))throw new InvalidDataException("Shipped release несовместим");
            if(!file.Profiles.Any(p=>p.Id==file.SelectedId&&p.Revisions.Any(r=>r.Number==file.SelectedRevision)))throw new InvalidDataException("Выбор отсутствует");
        }
        static bool RegistryMatches(LabBundle registry,LabBundle bundle)
        {
            if(!registry.Profiles.Select(p=>p.Id+"@"+p.Version).SequenceEqual(bundle.Profiles.Select(p=>p.Id+"@"+p.Version)))return false;
            var paths=registry.Descriptors.Select(d=>d.Path).ToArray();
            var other=bundle.Descriptors.Select(d=>d.Path).ToArray();
            // Serialized snapshots normally retain trusted descriptor order. Keep the exact
            // sorted multiset comparison for reordered files, including duplicate paths.
            return paths.Length==other.Length&&(paths.SequenceEqual(other)||
                paths.OrderBy(p=>p,StringComparer.Ordinal).SequenceEqual(other.OrderBy(p=>p,StringComparer.Ordinal)));
        }
        bool Current(LabBundle bundle)=>RegistryMatches(shipped,bundle);
        public bool Compatible(LabRevision revision)=>Current(revision.Snapshot);
        // Catalogue defaults follow the same authority as upgrade defaults, never the
        // selected experiment or a descriptor's factory tuning. Return detached data.
        public LabBundle ReleaseParameterDefaults()
        {
            if(authoring&&ReleaseError!=null)throw new InvalidOperationException(ReleaseError);
            return CompatibilityDefaults(data,shipped.Descriptors.ToDictionary(d=>d.Path,StringComparer.Ordinal));
        }
        public string CompatibilityReason(string profileId,int revisionNumber)
        {
            var revision=data.Profiles.Single(p=>p.Id==profileId).Revisions.Single(r=>r.Number==revisionNumber);
            if(Current(revision.Snapshot))return null;
            var descriptors=shipped.Descriptors.ToDictionary(d=>d.Path,StringComparer.Ordinal);
            var view=UpgradeSnapshot(revision.Snapshot,CompatibilityDefaults(data,descriptors),profileId==ReleaseId,descriptors,
                authoring||(profileId!=ReleaseId&&!IsShipped(revision)));
            var issues=view.Validate();
            return issues.Count==0?"Прежняя схема параметров · для игры доступно отдельное представление":issues[0].Path+": "+issues[0].Message;
        }
        LabBundle CompatibilityDefaults(LabHistoryFile file,IReadOnlyDictionary<string,NumericDescriptor> currentDescriptors)
        {
            LabBundle selected=null;
            if(authoring)selected=AuthorDefaultExecution(file)?.Snapshot??AuthorDefault(file)?.Snapshot;
            else if(releases!=null)selected=releases.Entries.Where(e=>e.ProfileId==ReleaseId)
                .OrderByDescending(e=>e.Revision).ThenByDescending(e=>e.Sequence).First().Snapshot;
            // The source Default may itself predate the current registry. Its missing
            // fields use registry defaults; all experiment fields use this resolved source.
            return selected==null?shipped.Clone():Current(selected)?selected.Clone():UpgradeSnapshot(selected,shipped,true,currentDescriptors,authoring);
        }
        LabBundle UpgradeSnapshot(LabBundle snapshot,LabBundle defaults,bool isDefault,IReadOnlyDictionary<string,NumericDescriptor> currentDescriptors,bool preserveSavedValues=false)
        {
            var next=defaults.Clone();foreach(var d in snapshot.Descriptors)if(currentDescriptors.TryGetValue(d.Path,out var currentDescriptor))
            {
                // The compatible revision uses current trusted map/diagnostic metadata.
                // Previous read-only values remain intact only in their historical revision.
                if(next.IsAuthoring(d.Path)||next.IsDiagnostic(d.Path))continue;
                float value=snapshot.Get(d.Path);
                // Own saved values are intentional, even when equal to an old factory
                // default. Keep them exact; trusted validation rejects incompatible
                // values instead of clamping or silently retuning the experiment.
                if(!preserveSavedValues)
                {
                    value=ProvingProfile.SafeSwitchUpgrade(d,currentDescriptor,value);
                    // The packaged Default adopts this behavior update. Named local
                    // profiles keep their tuning; original release snapshots stay intact.
                    if(isDefault&&ProvingProfile.BotCombatMovementDefaults.Contains(d.Path)&&snapshot.Profile(d.Path).Version<4)
                        value=currentDescriptor.DefaultValue;
                    // Interpret the untouched v9/v10 step interval as the new shipped default.
                    // Historical snapshots remain byte-for-byte intact with their original hash.
                    if(d.Path=="audio.footstepDistanceMeters"&&snapshot.Profile(d.Path).Version>=9&&snapshot.Profile(d.Path).Version<=10&&Mathf.Approximately(value,1.7f))
                        value=currentDescriptor.DefaultValue;
                    // Only the old shipped intensity becomes the stronger default. Keep custom
                    // values and the immutable source revision unchanged.
                    if(d.Path=="blood.drops"&&snapshot.Profile(d.Path).Version==1&&Mathf.Approximately(value,10f))
                        value=currentDescriptor.DefaultValue;
                    value=ProvingProfile.UpgradeArenaVisualValue(d.Path,snapshot.Profile(d.Path).Version,value,currentDescriptor);
                    value=ProvingProfile.UpgradeWellDetailValue(d.Path,snapshot.Profile(d.Path).Version,value,currentDescriptor);
                }
                next.Set(d.Path,value);
            }
            // Version3 used the office controls for both floors and stair annexes.
            // Preserve that custom stair lighting when the controls become independent.
            if(currentDescriptors.ContainsKey("lunar.stairLamp")&&currentDescriptors.ContainsKey("lunar.stairFill")
                &&snapshot.Descriptors.Any(d=>d.Path=="lunar.officeLamp")
                &&snapshot.Profile("lunar.officeLamp").Version==3)
            {
                next.Set("lunar.stairLamp",snapshot.Get("lunar.officeLamp"));
                next.Set("lunar.stairFill",snapshot.Get("lunar.officeFill"));
            }
            foreach(var weapon in new[]{"rifle","shot"})
                if(snapshot.Descriptors.Any(d=>d.Path==weapon+".torso")&&currentDescriptors.ContainsKey(weapon+".damage"))
                    next.Set(weapon+".damage",snapshot.Get(weapon+".torso"));
            if(!snapshot.Descriptors.Any(d=>d.Path==GamepadLookSettings.HorizontalPath)
                &&snapshot.Descriptors.Any(d=>d.Path=="input.gamepadDegreesPerSecond")
                &&currentDescriptors.ContainsKey(GamepadLookSettings.HorizontalPath))
            {
                var legacy=snapshot.Get("input.gamepadDegreesPerSecond");
                next.Set(GamepadLookSettings.HorizontalPath,legacy);next.Set(GamepadLookSettings.VerticalPath,legacy);
            }
            return next;
        }
        void MigrateCompatibility(LabHistoryFile file)
        {
            bool selectedRelease=file.SelectedId==ReleaseId;
            var views=file.Profiles.ToDictionary(p=>p.Id,p=>p.Revisions.Where(r=>r.CompatibilitySourceNumber>0).ToList());
            var nextNumbers=file.Profiles.ToDictionary(p=>p.Id,p=>p.Revisions.Max(r=>r.Number)+1);
            foreach(var profile in file.Profiles)
            {
                var selected=views[profile.Id].FirstOrDefault(r=>file.SelectedId==profile.Id&&file.SelectedRevision==r.Number);
                if(selected!=null)file.SelectedRevision=selected.CompatibilitySourceNumber;
                profile.Revisions.RemoveAll(r=>r.CompatibilitySourceNumber>0);
            }
            // Registry metadata is frozen for this migration. Index once instead of scanning the
            // whole (including authored-map) registry for every value in every old revision.
            var currentDescriptors=shipped.Descriptors.ToDictionary(d=>d.Path,StringComparer.Ordinal);
            var defaults=CompatibilityDefaults(file,currentDescriptors);
            string shippedHash=shipped.Hash();
            // Append current-schema counterparts; keep every old revision number, value and hash intact.
            foreach(var p in file.Profiles)
                foreach(var old in p.Revisions.Where(r=>!Current(r.Snapshot)).ToArray())
                {
                    startupCancellation.ThrowIfCancellationRequested();
                    // A stored current published interpretation is already authoritative;
                    // don't invent a second author interpretation of the same package row.
                    if(authoring&&old.ReleaseSequence>0)
                    {
                        var published=p.Revisions.Where(r=>r.ReleaseSequence==old.ReleaseSequence&&r.ReleaseHash==old.ReleaseHash&&Current(r.Snapshot)).ToArray();
                        if(published.Length==1)
                        {
                            if(file.SelectedId==p.Id&&file.SelectedRevision==old.Number)file.SelectedRevision=published[0].Number;
                            continue;
                        }
                    }
                    var next=UpgradeSnapshot(old.Snapshot,defaults,p.Id==ReleaseId,currentDescriptors,authoring||(p.Id!=ReleaseId&&!IsShipped(old)));
                    var issues=next.Validate();
                    if(issues.Count!=0)
                    {
                        // A trusted historical row can be valid in its own schema but
                        // impossible to execute now. Keep it inspectable without blocking
                        // a different valid selection. Never run or substitute that row.
                        if((file.SelectedId==p.Id&&file.SelectedRevision==old.Number)||(!authoring&&IsShipped(old)))
                            throw new InvalidDataException("Несовместимый снимок "+p.Id+" v"+old.Number+": "+issues[0].Path+": "+issues[0].Message);
                        continue;
                    }
                    string nextHash=next.Hash();
                    bool derived=authoring||(releases!=null&&p.Id!=ReleaseId&&!IsShipped(old));
                    var previous=views[p.Id].SingleOrDefault(r=>r.CompatibilitySourceNumber==old.Number&&r.CompatibilitySourceHash==old.Hash);
                    var existing=derived?null:p.Revisions.FirstOrDefault(r=>Current(r.Snapshot)&&r.Hash==nextHash&&(old.ReleaseSequence==0||r.ReleaseSequence==0||r.ReleaseSequence==old.ReleaseSequence));
                    if(existing==null)
                    {
                        existing=Revision(next,derived&&previous!=null?previous.Number:nextNumbers[p.Id]++,derived&&previous!=null?previous.Date:DateTime.Now.ToString("yyyyMMdd"));
                        if(derived){existing.CompatibilitySourceNumber=old.Number;existing.CompatibilitySourceHash=old.Hash;}
                        p.Revisions.Add(existing);
                    }
                    if(!authoring&&IsShipped(old))BindRelease(existing,releases.Entries.Single(e=>e.Sequence==old.ReleaseSequence));
                    if(file.SelectedId==p.Id&&file.SelectedRevision==old.Number)file.SelectedRevision=existing.Number;
                }
            // A catalogue supplies Default; source->execution mapping above already
            // preserves selection. The legacy factory tail would silently replace it.
            if(!authoring&&releases!=null)return;
            var release=file.Profiles.Single(p=>p.Id==ReleaseId);
            if(authoring&&release.Revisions.Any(r=>Current(r.Snapshot)))return;
            var latest=release.Revisions.FirstOrDefault(r=>Current(r.Snapshot)&&r.Hash==shippedHash);
            if(latest==null){latest=Revision(shipped,nextNumbers[release.Id]++,DateTime.Now.ToString("yyyyMMdd"));release.Revisions.Add(latest);}
            if(selectedRelease&&!authoring)file.SelectedRevision=latest.Number;
        }
        void Commit(Action<LabHistoryFile> mutation)
        {
            BeginMutation();
            try {data=PrepareCommit(mutation);}
            finally {Volatile.Write(ref mutationInFlight,0);}
        }
        void BeginMutation()
        {
            if(!Writable)throw new IOException(StorageError);
            if(Interlocked.CompareExchange(ref mutationInFlight,1,0)!=0)
                throw new InvalidOperationException("Дождитесь завершения выбора ревизии");
        }
        LabHistoryFile PrepareCommit(Action<LabHistoryFile> mutation)
        {
            var next=data.Clone();mutation(next);ValidateFile(next);
            string json=VerifiedStorage(next);PreserveLegacyStorage();
            string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {byte[] bytes=Encoding.UTF8.GetBytes(json);stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                atomicPublish(temp,path);
                return next;
            }
            finally {if(File.Exists(temp))File.Delete(temp);}
        }
        static void Publish(string temp,string destination){if(File.Exists(destination))File.Replace(temp,destination,null);else File.Move(temp,destination);}
        void SelectIn(LabHistoryFile file,string id,int number)
        {
            var revision=file.Profiles.Single(p=>p.Id==id).Revisions.Single(r=>r.Number==number);
            if(!Current(revision.Snapshot))throw new InvalidOperationException("Историческая схема: выберите её новую совместимую ревизию");
            file.SelectedId=id;file.SelectedRevision=number;
        }
        public void Select(string id,int number)=>Commit(f=>SelectIn(f,id,number));
        public async Task SelectAsync(string id,int number)
        {
            BeginMutation();
            try
            {
                // Serializable data only. Keep all validation and atomic disk publication off
                // the frame loop; publish in-memory selection on the caller's Unity context.
                data=await Task.Run(()=>PrepareCommit(f=>SelectIn(f,id,number)));
            }
            finally {Volatile.Write(ref mutationInFlight,0);}
        }
        public void Save(LabBundle draft)
        {
            RequireLocalProfile();
            // Previously editable placement overrides remain valid immutable history.
            // New revisions may retain them, but cannot introduce further changes to hidden fields.
            var baseline=Selected.Snapshot;
            if(draft.Descriptors.Any(d=>!draft.IsEditable(d.Path)&&draft.Get(d.Path)!=baseline.Get(d.Path)))throw new ArgumentException("Read-only metadata cannot be saved");
            if(draft.Validate().Count!=0)throw new ArgumentException("Черновик содержит ошибки");
            if(draft.Hash()==Selected.Hash&&!CanMaterializeSelected)throw new ArgumentException("Нет изменений");
            Commit(f=>{var p=f.Profiles.Single(x=>x.Id==f.SelectedId);
                // Public package numbers and technical storage keys need not coincide.
                // Reserve both: a new authored save must follow every saved version,
                // even if public v7 was deduplicated into physical row1.
                int n=Math.Max(p.Revisions.Max(r=>r.Number),p.Revisions.Where(r=>r.CompatibilitySourceNumber==0).Max(SavedVersionNumber))+1;
                p.Revisions.Add(Revision(draft,n,DateTime.Now.ToString("yyyyMMdd")));f.SelectedRevision=n;});
        }
        public void Create(string name)
        {
            RequireName(name);var baseSnapshot=Selected.Snapshot;string id=Guid.NewGuid().ToString("N");
            Commit(f=>{f.Profiles.Add(new LabHistoryProfile{Id=id,Name=name.Trim(),Revisions=new List<LabRevision>{Revision(baseSnapshot,1,DateTime.Now.ToString("yyyyMMdd"))}});f.SelectedId=id;f.SelectedRevision=1;});
        }
        public void Rename(string name){RequireLocalProfile();RequireName(name);Commit(f=>f.Profiles.Single(p=>p.Id==f.SelectedId).Name=name.Trim());}
        public void DeleteSelected()
        {
            if(SelectedProfileProtected)throw new InvalidOperationException("Стандартный профиль защищён от удаления");
            Commit(f=>{f.Profiles.RemoveAll(p=>p.Id==f.SelectedId);SelectLatestDefault(f);});
        }
        static void RequireName(string name){if(string.IsNullOrWhiteSpace(name)||name.Trim().Length>48)throw new ArgumentException("Имя: от 1 до 48 символов");}
    }
}
