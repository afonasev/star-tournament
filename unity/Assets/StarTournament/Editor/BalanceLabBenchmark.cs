using System;
using System.IO;
using System.Diagnostics;
using UnityEngine;
namespace StarTournament.ProvingGround.Editor
{
    public static class BalanceLabBenchmark
    {
        public static void Run()
        {
            string source=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_BALANCE_LEGACY_FIXTURE");
            if(string.IsNullOrEmpty(source)||!File.Exists(source+".balance-v1.json"))throw new InvalidOperationException("Requires a validated owned original and compact sidecar");
            var full=BalanceLab.RuntimeDefaults();var clock=Stopwatch.StartNew();
            var archive=JsonUtility.FromJson<LabReleaseCatalog>(File.ReadAllText(Path.Combine(Application.dataPath,"..","DesignLab","Archive","LabReleaseCatalog-full-v1.json")));
            var before=new DesignLabHistory(source,full,releases:archive,resetToLatestDefault:true,persistMigration:false);clock.Stop();
            if(before.StorageError!=null)throw new InvalidDataException(before.StorageError);
            var previousBalance=BalanceLab.Project(before.Selected.Snapshot);
            double old=clock.Elapsed.TotalMilliseconds;string oldSource=before.SavedIdentity(before.SelectedProfileId,before.Selected.Number).Hash;
            before=null;archive=null;GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
            clock.Restart();var compact=LabReleaseCatalog.Load();var after=DesignLabHistory.OpenBalance(source,full,compact);clock.Stop();
            if(after.StorageError!=null)throw new InvalidDataException(after.StorageError);
            if(after.SavedIdentity(after.SelectedProfileId,after.Selected.Number).Hash!=oldSource)throw new InvalidDataException("Different Default sources");
            foreach(var d in previousBalance.Descriptors)if(previousBalance.Get(d.Path)!=after.Selected.Snapshot.Get(d.Path))throw new InvalidDataException("BALANCE_EFFECTIVE_DIFF "+d.Path+" before="+previousBalance.Get(d.Path)+" after="+after.Selected.Snapshot.Get(d.Path));
            UnityEngine.Debug.Log("BALANCE_EFFECTIVE_PARITY_PASS fields="+BalanceLab.Fields.Count);
            UnityEngine.Debug.Log("BALANCE_DIAGNOSTIC full_load_ms="+old+" compact_load_ms="+clock.Elapsed.TotalMilliseconds+" source_bytes="+new FileInfo(source).Length+" compact_bytes="+new FileInfo(after.StoragePath).Length+" compact="+after.StartupDiagnostics);
        }
    }
}
