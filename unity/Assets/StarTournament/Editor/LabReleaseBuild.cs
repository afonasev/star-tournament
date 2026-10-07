using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace StarTournament.ProvingGround.Editor
{
    public static class LabReleaseBuild
    {
        const string CatalogPath="Assets/StarTournament/Resources/LabReleaseCatalog.json";
        static LabBundle Baseline()=>UnityEngine.Object.FindAnyObjectByType<ProvingGround>().CaptureLabBundle();
        public static void Bootstrap()
        {
            ProvingGroundBuild.Prepare();
            if(File.Exists(CatalogPath))throw new BuildFailedException("Catalog already exists; never overwrite release history");
            Write(LabReleaseCatalog.Factory(Baseline()));
            Validate();
        }
        public static void Validate()
        {
            if(!File.Exists(CatalogPath))throw new BuildFailedException("Missing LabReleaseCatalog; explicitly bootstrap before first release");
            var catalog=JsonUtility.FromJson<LabReleaseCatalog>(File.ReadAllText(CatalogPath));
            var history=new DesignLabHistory(Path.Combine(Path.GetTempPath(),"st-release-check-"+Guid.NewGuid()+".json"),Baseline(),releases:catalog,resetToLatestDefault:true);
            foreach(var entry in catalog.Entries)
                if(!history.Profiles.Single(p=>p.Id==entry.ProfileId).Revisions.Any(r=>r.ReleaseSequence==entry.Sequence&&history.Compatible(r)))
                    throw new BuildFailedException("Release is not runnable: "+entry.Sequence);
        }
        // Explicit staging only. This command never builds, uploads or publishes a client.
        public static void PromoteMarked()
        {
            string source=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_LAB_RELEASE_HISTORY");
            if(string.IsNullOrWhiteSpace(source)||!Path.IsPathFullyQualified(source)||!File.Exists(source))throw new BuildFailedException("Set absolute STAR_TOURNAMENT_LAB_RELEASE_HISTORY to the reviewed history file");
            ProvingGroundBuild.Prepare();Validate();
            var catalog=JsonUtility.FromJson<LabReleaseCatalog>(File.ReadAllText(CatalogPath));
            var history=new DesignLabHistory(source,Baseline(),releases:catalog,persistMigration:false);
            if(!history.Writable)throw new BuildFailedException(history.StorageError);
            var sourceNames=JsonUtility.FromJson<LabHistoryFile>(File.ReadAllText(source)).Profiles.ToDictionary(p=>p.Id,p=>p.Name);
            int added=0,sequence=catalog.Entries.Max(e=>e.Sequence);
            foreach(var p in history.Profiles)
                foreach(var r in p.Revisions.Where(r=>r.ReleaseCandidate&&!history.IsShipped(r)).OrderBy(r=>r.Number))
                {
                    catalog.Entries.Add(new LabReleaseEntry{Sequence=++sequence,ProfileId=p.Id,ProfileName=p.Id==DesignLabHistory.ReleaseId?"Default":sourceNames.TryGetValue(p.Id,out var name)?name:p.Name,
                        Revision=r.Number,Date=r.Date,Hash=r.Hash,Snapshot=r.Snapshot.Clone()});added++;
                }
            if(added==0)throw new BuildFailedException("No new marked saved revisions");
            // Validate the complete proposed union before touching the checked-in catalogue.
            new DesignLabHistory(Path.Combine(Path.GetTempPath(),"st-release-check-"+Guid.NewGuid()+".json"),Baseline(),releases:catalog,resetToLatestDefault:true);
            Write(catalog);Validate();Debug.Log("STAR_LAB_RELEASES_STAGED "+added);
        }
        static void Write(LabReleaseCatalog catalog)
        {
            string temp=CatalogPath+".tmp";
            try
            {
                File.WriteAllText(temp,JsonUtility.ToJson(catalog,true),new UTF8Encoding(false));
                if(File.Exists(CatalogPath))File.Replace(temp,CatalogPath,null);else File.Move(temp,CatalogPath);
            }
            finally{if(File.Exists(temp))File.Delete(temp);}
            AssetDatabase.ImportAsset(CatalogPath,ImportAssetOptions.ForceUpdate);
        }
    }
}
