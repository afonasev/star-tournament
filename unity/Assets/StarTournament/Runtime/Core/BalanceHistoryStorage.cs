using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    [Serializable] internal sealed class BalanceHistoryFile
    {
        public int Schema=4;
        public string RegistryHash,Integrity,SelectedId;
        public int SelectedRevision;
        public List<BalanceHistoryProfile> Profiles=new List<BalanceHistoryProfile>();
    }
    [Serializable] internal sealed class BalanceHistoryProfile
    {
        public string Id,Name;public bool Protected;
        public List<BalanceHistoryRevision> Revisions=new List<BalanceHistoryRevision>();
    }
    [Serializable] internal sealed class BalanceHistoryRevision
    {
        public int Number,ReleaseSequence,ReleaseNumber,SourceNumber;
        public string Date,Hash,ReleaseHash,ReleaseDate,SourceDate,SourceHash;
        public bool ReleaseCandidate;
        public List<ProvingProfileValue> Values=new List<ProvingProfileValue>();
    }
    [Serializable] internal sealed class BalanceHistoryOrigin { public string SourcePath,ArchivePath,Sha256; public long Bytes; }
    public sealed partial class DesignLabHistory
    {
        string WriteBalanceStorage(LabHistoryFile file)
        {
            var compact=new BalanceHistoryFile{RegistryHash=BalanceLab.RegistryIdentity(shipped),SelectedId=file.SelectedId,SelectedRevision=file.SelectedRevision};
            foreach(var profile in file.Profiles)
            {
                var p=new BalanceHistoryProfile{Id=profile.Id,Name=profile.Name,Protected=profile.Protected};compact.Profiles.Add(p);
                foreach(var r in profile.Revisions)
                {
                    if(!Current(r.Snapshot)||r.CompatibilitySourceNumber>0)throw new InvalidDataException("Неподдерживаемая проекция баланса");
                    p.Revisions.Add(new BalanceHistoryRevision{Number=r.Number,Date=r.Date,Hash=r.Hash,SourceNumber=r.SourceNumber,SourceDate=r.SourceDate,SourceHash=r.SourceHash,
                        ReleaseCandidate=r.ReleaseCandidate,ReleaseSequence=r.ReleaseSequence,ReleaseNumber=r.ReleaseNumber,ReleaseDate=r.ReleaseDate,ReleaseHash=r.ReleaseHash,
                        Values=r.Snapshot.Descriptors.Select(d=>new ProvingProfileValue{Path=d.Path,Value=r.Snapshot.Get(d.Path)}).ToList()});
                }
            }
            compact.Integrity=BalanceLab.Integrity(compact,v=>compact.Integrity=v,compact.Integrity);
            return JsonUtility.ToJson(compact,true);
        }
        LabHistoryFile ReadBalanceStorage(string json)
        {
            var compact=JsonUtility.FromJson<BalanceHistoryFile>(json);
            if(!BalanceLab.IsProjection(shipped)||compact?.Schema!=4||compact.RegistryHash!=BalanceLab.RegistryIdentity(shipped)||compact.Profiles==null||
                string.IsNullOrEmpty(compact.Integrity)||compact.Integrity!=BalanceLab.Integrity(compact,v=>compact.Integrity=v,compact.Integrity))throw new InvalidDataException("Повреждена история баланса или её реестр");
            var file=new LabHistoryFile{SelectedId=compact.SelectedId,SelectedRevision=compact.SelectedRevision};
            foreach(var profile in compact.Profiles)
            {
                if(profile?.Revisions==null)throw new InvalidDataException("Отсутствуют версии баланса");
                var p=new LabHistoryProfile{Id=profile.Id,Name=profile.Name,Protected=profile.Protected};file.Profiles.Add(p);
                foreach(var r in profile.Revisions)
                {
                    if(r==null||(!string.IsNullOrEmpty(r.SourceHash)&&r.SourceNumber<1))throw new InvalidDataException("Неверная исходная идентичность баланса");
                    p.Revisions.Add(new LabRevision{Number=r.Number,Date=r.Date,Hash=r.Hash,SourceNumber=r.SourceNumber,SourceDate=r.SourceDate,SourceHash=r.SourceHash,
                        ReleaseCandidate=r.ReleaseCandidate,ReleaseSequence=r.ReleaseSequence,ReleaseNumber=r.ReleaseNumber,ReleaseHash=r.ReleaseHash,ReleaseDate=r.ReleaseDate,
                        Snapshot=BalanceLab.Restore(shipped,r.Values,true)});
                }
            }
            return file;
        }
        public static DesignLabHistory ReadOriginalForBalanceImport(string source,LabBundle full,CancellationToken token=default,bool authoring=false)
            =>new DesignLabHistory(source,full,null,null,false,false,token,null,authoring,false,true,authoring?BalanceLab.AuthoringRegistries(source):null);
        // The full source remains untouched. Only a validated projection and a byte-exact
        // archive are published, then subsequent starts never read the authored graph.
        public static DesignLabHistory OpenBalance(string sourcePath,LabBundle runtime,LabReleaseCatalog catalogue,CancellationToken token=default,string legacyPath=null,bool authoring=false)
        {
            var registry=BalanceLab.Project(runtime);
            string destination=sourcePath==null?null:sourcePath+".balance-v1.json";
            string source=VersionedUserData.ReadPath(sourcePath,legacyPath);
            if(destination!=null&&!File.Exists(destination)&&File.Exists(source))
            {
                token.ThrowIfCancellationRequested();
                var original=File.ReadAllBytes(source);string hash;
                using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(original)).Replace("-","").ToLowerInvariant();
                var legacy=ReadOriginalForBalanceImport(source,BalanceLab.IsProjection(runtime)?BalanceLab.RuntimeDefaults():runtime,token,authoring);
                if(legacy.StorageError!=null)throw new InvalidDataException(legacy.StorageError);
                var defaults=catalogue.Entries.Where(e=>e.ProfileId==ReleaseId).OrderByDescending(e=>e.Revision).ThenByDescending(e=>e.Sequence).First().Snapshot;
                // Read the validated original representation, excluding constructor-generated upgrades.
                var projected=legacy.data.Clone();
                var selected=projected.Profiles.Single(p=>p.Id==projected.SelectedId).Revisions.Single(r=>r.Number==projected.SelectedRevision);
                if(selected.CompatibilitySourceNumber>0)projected.SelectedRevision=selected.CompatibilitySourceNumber;
                foreach(var profile in projected.Profiles)
                {
                    // Compatibility rows are generated execution views, not new user revisions.
                    // Carry author flags back to their immutable saved source before projecting.
                    foreach(var view in profile.Revisions.Where(r=>r.CompatibilitySourceNumber>0&&r.ReleaseCandidate))
                        profile.Revisions.Single(r=>r.Number==view.CompatibilitySourceNumber).ReleaseCandidate=true;
                    profile.Revisions.RemoveAll(r=>r.CompatibilitySourceNumber>0);
                    foreach(var r in profile.Revisions)
                    {
                        r.SourceNumber=r.Number;r.SourceDate=r.Date;r.SourceHash=r.Hash;
                        r.Snapshot=BalanceLab.Overlay(r.Snapshot,defaults);r.Hash=r.Snapshot.Hash();
                        r.CompatibilitySourceNumber=0;r.CompatibilitySourceHash=null;
                        r.ReleaseSequence=0;r.ReleaseNumber=0;r.ReleaseDate=null;r.ReleaseHash=null;
                    }
                }
                var validator=new DesignLabHistory(null,registry,releases:catalogue,persistMigration:false,authoring:authoring);
                validator.ValidateFile(projected);
                string json=validator.VerifiedStorage(projected);
                token.ThrowIfCancellationRequested();
                // Reject a changing source before committing its archive/manifest/projection.
                if(!File.ReadAllBytes(source).SequenceEqual(original))throw new IOException("Исходная история изменилась во время переноса");
                string archive=authoring?source:source+".archive-"+hash+".json";
                if(File.Exists(archive))
                {if(!File.ReadAllBytes(archive).SequenceEqual(original))throw new IOException("Архив исходной истории повреждён");}
                else AtomicNew(archive,original);
                var origin=new BalanceHistoryOrigin{SourcePath=authoring?Path.GetFileName(source):source,ArchivePath=authoring?Path.GetFileName(archive):archive,Sha256=hash,Bytes=original.LongLength};
                string manifest=destination+".origin.json";
                if(File.Exists(manifest))
                {
                    var prior=JsonUtility.FromJson<BalanceHistoryOrigin>(File.ReadAllText(manifest));
                    if(prior==null||prior.Sha256!=hash||prior.Bytes!=original.LongLength||prior.SourcePath!=origin.SourcePath||prior.ArchivePath!=origin.ArchivePath)throw new IOException("Происхождение переноса не соответствует исходному файлу");
                }
                else AtomicNew(manifest,System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(origin,true)));
                AtomicNew(destination,System.Text.Encoding.UTF8.GetBytes(json));
            }
            return new DesignLabHistory(destination,registry,releases:catalogue,resetToLatestDefault:true,persistMigration:false,cancellationToken:token,authoring:authoring);
        }
        static void AtomicNew(string target,byte[] bytes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target));string temp=target+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                File.Move(temp,target);
            }
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
    }
}
