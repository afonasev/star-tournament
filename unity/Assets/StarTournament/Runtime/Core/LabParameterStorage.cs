using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    [Serializable] internal sealed class LabStorageHeader { public int Schema; }
    [Serializable] public sealed class LabParameterFile
    {
        public int Schema=2;
        public string SelectedId,Integrity;
        public int SelectedRevision;
        public List<LabParameterSchema> Schemas=new List<LabParameterSchema>();
        public List<LabParameterProfile> Profiles=new List<LabParameterProfile>();
    }
    [Serializable] public sealed class LabParameterSchema
    {
        public string BaselineHash;
        public List<LabParameterShape> Profiles=new List<LabParameterShape>();
    }
    [Serializable] public sealed class LabParameterShape
    {
        public string Id,DefinitionHash;
        public int Version;
        // Authored profiles carry only their code identity, never object coordinates/paths.
        public List<string> Paths=new List<string>();
    }
    [Serializable] public sealed class LabParameterProfile
    {
        public string Id,Name;
        public bool Protected;
        public List<LabParameterRevision> Revisions=new List<LabParameterRevision>();
    }
    [Serializable] public sealed class LabParameterRevision
    {
        public int Number,SchemaIndex,ReleaseSequence,ReleaseNumber;
        public string Date,Hash,ReleaseHash,ReleaseDate;
        public bool ReleaseCandidate;
        public List<ProvingProfileValue> Values=new List<ProvingProfileValue>();
        // Only previously configured hidden parameters; never unchanged engine defaults.
        public List<ProvingProfileValue> LegacyOverrides=new List<ProvingProfileValue>();
    }
    public sealed partial class DesignLabHistory
    {
        static readonly object storageRegistryLock=new object();
        static readonly Dictionary<string,LabBundle> storageRegistryCache=new Dictionary<string,LabBundle>(StringComparer.Ordinal);
        static readonly Queue<string> storageRegistryOrder=new Queue<string>();
        readonly Dictionary<string,LabBundle> storageResolvedSchemas=new Dictionary<string,LabBundle>(StringComparer.Ordinal);
        readonly Dictionary<string,ProvingProfile> storageAuthored=new Dictionary<string,ProvingProfile>(StringComparer.Ordinal);
        string StorageRegistryKey(LabBundle shape,string baselineHash)
        {
            string source=ShapeKey(shape);
            using(var sha=SHA256.Create())source=Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(source)));
            return (shippedCacheKey??(shippedCacheKey=TrustedRegistryFingerprint()))+"|"+source+"|"+baselineHash;
        }
        void RememberStorageRegistry(LabBundle registry)
        {
            string key=StorageRegistryKey(registry,registry.Hash());
            lock(storageRegistryLock)
            {
                if(storageRegistryCache.ContainsKey(key))return;
                if(storageRegistryCache.Count>=8)storageRegistryCache.Remove(storageRegistryOrder.Dequeue());
                storageRegistryOrder.Enqueue(key);
                var detached=registry.Clone();detached.Hash(); // Initialize read-only lookup tables before sharing across loaders.
                storageRegistryCache.Add(key,detached);
            }
        }
        static string DefinitionHash(ProvingProfile p)=>new LabBundle{Profiles={p}}.Hash();
        ProvingProfile ResolveStorageAuthored(LabParameterShape shape)
        {
            string key=shape.Id+"@"+shape.Version+"|"+shape.DefinitionHash;
            if(storageAuthored.TryGetValue(key,out var known))return known;
            var current=shipped.Profiles.SingleOrDefault(p=>p.Id==shape.Id);
            if(current==null||shape.Paths.Count!=0||string.IsNullOrEmpty(shape.DefinitionHash))throw new InvalidDataException("Недоступно поддерживаемое определение ресурса "+shape.Id+"@"+shape.Version);
            // Every candidate is produced by existing trusted compatibility code, never file data.
            IEnumerable<ProvingProfile> Candidates()
            {
                yield return current;
                if(shape.Id=="well-authoring-v1"&&shape.Version==4)yield return current.BeforeWellSecondFloorWeapons();
                if(shape.Id=="well-authoring-v1"&&shape.Version==3)yield return current.BeforeWellCorridorCover();
                if(shape.Id=="well-authoring-v1"&&shape.Version==2)yield return current.BeforeWellFurniture();
                if(shape.Id=="well-authoring-v1"&&shape.Version==1)yield return current.BeforeWellStructure();
                if(shape.Id=="combat-bowl-ring-r7-authoring-v1")yield return current.BeforeWeaponPickups();
                if(shape.Id=="lunar-laboratory-authoring-v1")
                {
                    foreach(var basis in new[]{current,current.BeforeLunarBypass(),current.BeforeLunarFlush(),current.BeforeLunarInterior(),current.BeforeLunarOpenCentre()})
                    {yield return basis;yield return basis.BeforeLunarOffice();yield return basis.BeforeLunarRework();}
                }
            }
            var authored=Candidates().FirstOrDefault(p=>p.Version==shape.Version&&DefinitionHash(p)==shape.DefinitionHash);
            if(authored==null)throw new InvalidDataException("Недоступно поддерживаемое определение ресурса "+shape.Id+"@"+shape.Version);
            storageAuthored.Add(key,authored);return authored;
        }
        static bool AuthoredProfile(string id)=>id=="combat-bowl-ring-r7-authoring-v1"||id==ProvingProfile.TunnelsAuthoringId||id=="lunar-laboratory-authoring-v1"||id=="well-authoring-v1";
        static string ShapeKey(LabBundle b)=>string.Join("|",b.Profiles.Select(p=>p.Id+"@"+p.Version+":"+string.Join(",",p.Descriptors.Select(d=>d.Path).OrderBy(path=>path,StringComparer.Ordinal))));
        string SchemaIdentity(LabBundle bundle)
        {
            var changed=bundle.Profiles.Where(p=>!shipped.Profiles.Any(known=>known.Id==p.Id&&known.Version==p.Version&&
                known.Descriptors.Select(d=>d.Path).OrderBy(x=>x,StringComparer.Ordinal).SequenceEqual(p.Descriptors.Select(d=>d.Path).OrderBy(x=>x,StringComparer.Ordinal)))).ToArray();
            var relevant=changed.Length==0?bundle.Profiles.ToArray():changed;
            return string.Join(", ",relevant.Take(2).Select(p=>p.Id+"@"+p.Version))+(relevant.Length>2?", ещё: "+(relevant.Length-2):"");
        }
        LabBundle StorageRegistry(LabBundle snapshot)
        {
            if(Current(snapshot))return shipped;
            if(storageResolvedSchemas.TryGetValue(ShapeKey(snapshot),out var known))return known;
            return HistoricalPredecessors(snapshot).FirstOrDefault(b=>RegistryMatches(b,snapshot))??throw new InvalidDataException("Неподдерживаемая схема Lab: "+SchemaIdentity(snapshot)+"; недоступные поля: "+string.Join(", ",snapshot.Descriptors.Select(d=>d.Path).Except(shipped.Descriptors.Select(d=>d.Path)).Take(3)));
        }
        static string StorageIntegrity(LabParameterFile file)
        {
            string saved=file.Integrity;file.Integrity=null;
            try {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(file)))).Replace("-","").ToLowerInvariant();}
            finally {file.Integrity=saved;}
        }
        string WriteStorage(LabHistoryFile file)
        {
            var compact=new LabParameterFile{SelectedId=file.SelectedId,SelectedRevision=file.SelectedRevision};
            var schemas=new Dictionary<string,int>(StringComparer.Ordinal);
            var registries=new Dictionary<string,LabBundle>(StringComparer.Ordinal);
            foreach(var profile in file.Profiles)
            {
                var p=new LabParameterProfile{Id=profile.Id,Name=profile.Name,Protected=profile.Protected};compact.Profiles.Add(p);
                foreach(var r in profile.Revisions)
                {
                    var key=ShapeKey(r.Snapshot);
                    if(!registries.TryGetValue(key,out var registry)){registry=StorageRegistry(r.Snapshot);registries.Add(key,registry);}
                    if(!schemas.TryGetValue(key,out int index))
                    {
                        index=compact.Schemas.Count;schemas.Add(key,index);RememberStorageRegistry(registry);
                        compact.Schemas.Add(new LabParameterSchema{BaselineHash=registry.Hash(),Profiles=registry.Profiles.Select(b=>new LabParameterShape{Id=b.Id,Version=b.Version,
                            DefinitionHash=AuthoredProfile(b.Id)?DefinitionHash(b):null,Paths=AuthoredProfile(b.Id)?new List<string>():b.Descriptors.Select(d=>d.Path).ToList()}).ToList()});
                    }
                    var revision=new LabParameterRevision{Number=r.Number,Date=r.Date,Hash=r.Hash,SchemaIndex=index,ReleaseCandidate=r.ReleaseCandidate,
                        ReleaseSequence=r.ReleaseSequence,ReleaseNumber=r.ReleaseNumber,ReleaseHash=r.ReleaseHash,ReleaseDate=r.ReleaseDate};p.Revisions.Add(revision);
                    foreach(var d in registry.Descriptors)
                    {
                        float value=r.Snapshot.Get(d.Path);
                        if(registry.IsEditable(d.Path))revision.Values.Add(new ProvingProfileValue{Path=d.Path,Value=value});
                        else if(value!=registry.Get(d.Path))
                        {
                            if(registry.IsAuthoring(d.Path)||registry.IsDiagnostic(d.Path))throw new InvalidDataException("Read-only metadata changed");
                            revision.LegacyOverrides.Add(new ProvingProfileValue{Path=d.Path,Value=value});
                        }
                    }
                }
            }
            compact.Integrity=StorageIntegrity(compact);
            return WriteCompatibilityEnvelope(compact,file);
        }
        LabHistoryFile ReadStorage(string json)
        {
            var header=JsonUtility.FromJson<LabStorageHeader>(json);
            if(header?.Schema==3)return ReadCompatibilityEnvelope(json);
            if(header?.Schema==1)return JsonUtility.FromJson<LabHistoryFile>(json);
            if(header?.Schema!=2)throw new InvalidDataException("Unsupported history schema");
            var file=JsonUtility.FromJson<LabParameterFile>(json);
            if(file?.Schemas==null||file.Profiles==null||string.IsNullOrEmpty(file.Integrity)||file.Integrity!=StorageIntegrity(file))throw new InvalidDataException("Parameter history integrity mismatch");
            var registries=new List<LabBundle>();
            foreach(var schema in file.Schemas)
            {
                startupCancellation.ThrowIfCancellationRequested();
                if(schema?.Profiles==null||string.IsNullOrEmpty(schema.BaselineHash))throw new InvalidDataException("Missing schema baseline");
                var sought=new LabBundle();
                foreach(var shape in schema.Profiles)
                {
                    if(shape==null||string.IsNullOrWhiteSpace(shape.Id)||shape.Version<1||shape.Paths==null||shape.Paths.Any(string.IsNullOrWhiteSpace)||shape.Paths.Distinct().Count()!=shape.Paths.Count)throw new InvalidDataException("Invalid schema shape");
                    if(AuthoredProfile(shape.Id))
                    {
                        var authored=ResolveStorageAuthored(shape);
                        sought.Profiles.Add(authored);
                    }
                    else sought.Profiles.Add(ProvingProfile.StorageShape(shape.Id,shape.Version,shape.Paths));
                }
                if(sought.Profiles.Select(p=>p.Id).Distinct().Count()!=sought.Profiles.Count||sought.Descriptors.Select(d=>d.Path).Distinct().Count()!=sought.Descriptors.Count())throw new InvalidDataException("Duplicate schema keys");
                LabBundle registry;
                lock(storageRegistryLock)storageRegistryCache.TryGetValue(StorageRegistryKey(sought,schema.BaselineHash),out registry);
                if(registry==null)registry=Current(sought)?shipped:HistoricalPredecessors(sought,true).FirstOrDefault(b=>RegistryMatches(b,sought)&&b.Hash()==schema.BaselineHash);
                if(registry==null||registry.Hash()!=schema.BaselineHash)throw new InvalidDataException("Недоступна или изменена доверенная схема Lab: "+SchemaIdentity(sought));
                RememberStorageRegistry(registry);registries.Add(registry);
                string shapeKey=ShapeKey(registry);
                if(!storageResolvedSchemas.ContainsKey(shapeKey)&&storageResolvedSchemas.Count>=8)storageResolvedSchemas.Remove(storageResolvedSchemas.Keys.First());
                storageResolvedSchemas[shapeKey]=registry;
            }
            var restored=new LabHistoryFile{SelectedId=file.SelectedId,SelectedRevision=file.SelectedRevision};
            foreach(var p in file.Profiles)
            {
                if(p?.Revisions==null)throw new InvalidDataException("Missing revision list");
                var profile=new LabHistoryProfile{Id=p.Id,Name=p.Name,Protected=p.Protected};restored.Profiles.Add(profile);
                foreach(var r in p.Revisions)
                {
                    startupCancellation.ThrowIfCancellationRequested();
                    if(r==null||r.SchemaIndex<0||r.SchemaIndex>=registries.Count||r.Values==null||r.LegacyOverrides==null)throw new InvalidDataException("Invalid parameter revision");
                    var registry=registries[r.SchemaIndex];var bundle=registry.Clone();
                    var expected=new HashSet<string>(registry.Descriptors.Where(d=>registry.IsEditable(d.Path)).Select(d=>d.Path),StringComparer.Ordinal);
                    if(r.Values.Any(v=>v==null)||r.Values.Count!=expected.Count||!expected.SetEquals(r.Values.Select(v=>v.Path)))throw new InvalidDataException("Incomplete or unknown designer parameters");
                    foreach(var v in r.Values)bundle.Set(v.Path,v.Value);
                    var paths=new HashSet<string>(registry.Descriptors.Select(d=>d.Path),StringComparer.Ordinal);var legacy=new HashSet<string>(StringComparer.Ordinal);
                    foreach(var v in r.LegacyOverrides)
                    {
                        if(v==null||!paths.Contains(v.Path)||!legacy.Add(v.Path)||registry.IsEditable(v.Path)||registry.IsAuthoring(v.Path)||registry.IsDiagnostic(v.Path)||v.Value==registry.Get(v.Path))throw new InvalidDataException("Invalid legacy parameter override");
                        bundle.Set(v.Path,v.Value);
                    }
                    profile.Revisions.Add(new LabRevision{Number=r.Number,Date=r.Date,Hash=r.Hash,Snapshot=bundle,ReleaseCandidate=r.ReleaseCandidate,
                        ReleaseSequence=r.ReleaseSequence,ReleaseNumber=r.ReleaseNumber,ReleaseHash=r.ReleaseHash,ReleaseDate=r.ReleaseDate});
                }
            }
            return restored;
        }
        string VerifiedStorage(LabHistoryFile next)
        {
            var json=WriteStorage(next);var restored=ReadStorage(json);ValidateFile(restored);
            if(JsonUtility.ToJson(restored)!=JsonUtility.ToJson(next))throw new InvalidDataException("Compact history is not lossless");
            return json;
        }
        void PreserveLegacyStorage()
        {
            if(!File.Exists(path)||JsonUtility.FromJson<LabStorageHeader>(File.ReadAllText(path))?.Schema!=1)return;
            string backup=path+".schema1-backup.json";
            byte[] original=File.ReadAllBytes(path);
            if(File.Exists(backup))
            {
                if(!original.SequenceEqual(File.ReadAllBytes(backup)))throw new IOException("A different legacy backup already exists: "+backup);
                return;
            }
            string temp=backup+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {stream.Write(original,0,original.Length);stream.Flush(true);}
                File.Move(temp,backup);
            }
            finally {if(File.Exists(temp))File.Delete(temp);}
        }
        // Explicit maintenance: unlike background loading, this validates both representations
        // and transactionally rewrites storage without changing revision selection or content.
        public void CompactStorage()
        {
            BeginMutation();
            try
            {
                var next=data.Clone();ValidateFile(next);
                var json=VerifiedStorage(next);PreserveLegacyStorage();
                string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                    {var bytes=Encoding.UTF8.GetBytes(json);stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                    atomicPublish(temp,path);data=next;
                }
                finally {if(File.Exists(temp))File.Delete(temp);}
            }
            finally {Volatile.Write(ref mutationInFlight,0);}
        }
    }
}
