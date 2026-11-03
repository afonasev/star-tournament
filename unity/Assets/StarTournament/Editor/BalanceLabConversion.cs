using System;
using System.IO;
using UnityEngine;
namespace StarTournament.ProvingGround.Editor
{
    public static class BalanceLabConversion
    {
        public static void MigrateAuthoring()
        {
            var history=LabProjectStorage.Open(Path.GetFullPath(Path.Combine(Application.dataPath,"..")),BalanceLab.Defaults(),LabReleaseCatalog.Load());
            if(history.StorageError!=null||history.ReleaseError!=null)throw new InvalidDataException(history.StorageError??history.ReleaseError);
            history.CompactStorage();
            Debug.Log("Balance author source ready: "+history.StoragePath+"; Default="+history.Selected.Number+"; hash="+history.Selected.Hash);
        }
        public static void Generate()
        {
            string archive=Path.Combine(Application.dataPath,"..","DesignLab","Archive","LabReleaseCatalog-full-v1.json");
            var original=BalanceLab.ReadAuthoringArchive(archive);
            foreach(var entry in original.Entries)
            {
                if(entry.Hash!=entry.Snapshot.Hash())throw new InvalidDataException("Исходный каталог повреждён");
                var defaults=entry.Snapshot.Clone();foreach(var d in defaults.Descriptors)defaults.Set(d.Path,d.DefaultValue);
                Debug.Log("Archive registry v"+entry.Revision+" hash="+defaults.Hash());
            }
            var runtime=BalanceLab.RuntimeDefaults();
            var projected=BalanceLab.ProjectExecutedReleases(original,runtime,out var final);
            var text=BalanceLab.WriteReleases(projected,BalanceLab.Defaults());
            File.WriteAllText(Path.Combine(Application.dataPath,"StarTournament","Resources","RuntimeDesignSnapshot.json"),BalanceLab.WriteRuntimeSnapshot(final,runtime,original.Entries.FindLast(e=>e.ProfileId==DesignLabHistory.ReleaseId).Hash));
            File.WriteAllText(Path.Combine(Application.dataPath,"StarTournament","Resources","LabReleaseCatalog.json"),text);
            Debug.Log("Balance catalogue generated: "+text.Length+" characters; "+BalanceLab.Fields.Count+" fields");
        }
    }
}
