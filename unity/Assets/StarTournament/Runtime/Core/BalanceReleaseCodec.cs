using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    [Serializable] public sealed class BalanceReleaseFile
    {
        public int Schema=4;
        public string RegistryHash,Integrity;
        public List<ProvingProfileValue> Current=new List<ProvingProfileValue>();
        public List<BalanceReleaseDelta> Versions=new List<BalanceReleaseDelta>();
    }
    [Serializable] public sealed class BalanceReleaseDelta
    {
        public int Sequence,Revision;
        public string ProfileId,ProfileName,Date,Hash,SourceHash;
        public List<ProvingProfileValue> Delta=new List<ProvingProfileValue>();
    }
    public static partial class BalanceLab
    {
        public static LabBundle Defaults()=>Project(new LabBundle{Profiles=new List<ProvingProfile>{
            ProvingProfile.CreateDefault(),ProvingProfile.CreateCombatDefault(),ProvingProfile.CreateNativeCombatDefault(),
            ProvingProfile.CreateTrooperDefault(),ProvingProfile.CreateMatchDefault(),ProvingProfile.CreateTeamDefault(),ProvingProfile.CreateRosterDefault(),
            ProvingProfile.CreateBotPerceptionDefault(),ProvingProfile.CreateNavigationDefault(),ProvingProfile.CreateBotBehaviorDefault(),
            ProvingProfile.CreateCutterDefault(),ProvingProfile.CreatePlasmaDefault(),ProvingProfile.CreateWellDefault()}});
        // Schema identity excludes tuned default values. A release can change its defaults
        // without invalidating an independently saved, still-valid user balance.
        public static string RegistryIdentity(LabBundle registry)
        {
            string shape="balance-registry-v1\n"+string.Join("\n",registry.Profiles.Select(p=>p.Id+"@"+p.Version+":"+string.Join(",",p.Descriptors.Select(d=>d.Path))));
            using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(shape))).Replace("-","").ToLowerInvariant();
        }
        internal static string Integrity<T>(T file,Action<string> set,string previous)
        {
            set(null);
            try{using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(file)))).Replace("-","").ToLowerInvariant();}
            finally{set(previous);}
        }
        static List<ProvingProfileValue> Values(LabBundle bundle)=>bundle.Descriptors.OrderBy(d=>d.Path,StringComparer.Ordinal)
            .Select(d=>new ProvingProfileValue{Path=d.Path,Value=bundle.Get(d.Path)}).ToList();
        internal static LabBundle Restore(LabBundle registry,List<ProvingProfileValue> values,bool complete)
        {
            if(values==null||values.Any(v=>v==null)||values.Select(v=>v.Path).Distinct().Count()!=values.Count)throw new InvalidDataException("Повтор или отсутствие параметров баланса");
            var paths=new HashSet<string>(registry.Descriptors.Select(d=>d.Path),StringComparer.Ordinal);
            if(values.Any(v=>!paths.Contains(v.Path))||(complete&&values.Count!=paths.Count))throw new InvalidDataException("Неизвестные или пропущенные параметры баланса");
            var bundle=registry.Clone();foreach(var value in values)bundle.Set(value.Path,value.Value);
            var issues=bundle.Validate();if(issues.Count>0)throw new InvalidDataException(issues[0].Path+": "+issues[0].Message);
            return bundle;
        }
        public static string WriteReleases(LabReleaseCatalog catalog,LabBundle registry)
        {
            registry=Project(registry);
            if(catalog?.Entries==null||catalog.Entries.Count==0)throw new InvalidDataException("Пустой каталог");
            var latest=catalog.Entries.Where(e=>e.ProfileId==DesignLabHistory.ReleaseId).OrderByDescending(e=>e.Revision).ThenByDescending(e=>e.Sequence).First();
            var current=Overlay(latest.Snapshot,registry);
            var file=new BalanceReleaseFile{RegistryHash=RegistryIdentity(registry),Current=Values(current)};
            foreach(var entry in catalog.Entries.OrderBy(e=>e.Sequence))
            {
                var snapshot=Overlay(entry.Snapshot,current);
                if(snapshot.Validate().Count>0)throw new InvalidDataException("Несовместимые игровые значения версии "+entry.Revision);
                file.Versions.Add(new BalanceReleaseDelta{Sequence=entry.Sequence,Revision=entry.Revision,ProfileId=entry.ProfileId,ProfileName=entry.ProfileName,
                    Date=entry.Date,SourceHash=string.IsNullOrEmpty(entry.SourceHash)?entry.Hash:entry.SourceHash,Hash=snapshot.Hash(),
                    Delta=Values(snapshot).Where(v=>v.Value!=current.Get(v.Path)).ToList()});
            }
            file.Integrity=Integrity(file,v=>file.Integrity=v,file.Integrity);
            string json=JsonUtility.ToJson(file,true);
            ReadReleases(json,registry);return json;
        }
        internal static LabReleaseCatalog ReadReleases(string json,LabBundle registry)
        {
            var file=JsonUtility.FromJson<BalanceReleaseFile>(json);registry=Project(registry);
            if(file?.Schema!=4||file.RegistryHash!=RegistryIdentity(registry)||string.IsNullOrEmpty(file.Integrity)||file.Integrity!=Integrity(file,v=>file.Integrity=v,file.Integrity)||file.Versions==null||file.Versions.Count==0)
                throw new InvalidDataException("Повреждён каталог баланса или изменён доверенный реестр");
            var current=Restore(registry,file.Current,true);var catalog=new LabReleaseCatalog();
            foreach(var version in file.Versions)
            {
                if(version==null||string.IsNullOrEmpty(version.SourceHash))throw new InvalidDataException("Отсутствует исходная идентичность версии");
                var snapshot=Restore(current,version.Delta,false);
                if(snapshot.Hash()!=version.Hash)throw new InvalidDataException("Hash дельты баланса изменён");
                catalog.Entries.Add(new LabReleaseEntry{Sequence=version.Sequence,Revision=version.Revision,ProfileId=version.ProfileId,ProfileName=version.ProfileName,
                    Date=version.Date,Hash=version.Hash,SourceHash=version.SourceHash,Snapshot=snapshot});
            }
            // The current payload must be the newest Default, not a separate unlabelled snapshot.
            var latest=catalog.Entries.Where(e=>e.ProfileId==DesignLabHistory.ReleaseId).OrderByDescending(e=>e.Revision).ThenByDescending(e=>e.Sequence).FirstOrDefault();
            if(latest==null||latest.Hash!=current.Hash())throw new InvalidDataException("Текущий снимок не соответствует последнему Default");
            return catalog;
        }
    }
}
