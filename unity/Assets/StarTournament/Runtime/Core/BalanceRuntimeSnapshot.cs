using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace StarTournament.ProvingGround
{
    [Serializable] public sealed class BalanceRuntimeSnapshot
    {
        public int Schema=1;
        public string SourceHash,Integrity;
        public List<ProvingProfileValue> Overrides=new List<ProvingProfileValue>();
    }
    public static partial class BalanceLab
    {
        // One final independent snapshot, represented only by deviations from trusted code.
        // Lab versions never contain or apply these retired controls.
        public static string WriteRuntimeSnapshot(LabBundle final,LabBundle defaults,string sourceHash)
        {
            var snapshot=new BalanceRuntimeSnapshot{SourceHash=sourceHash};
            foreach(var d in final.Descriptors.Where(d=>!Fields.ContainsKey(d.Path)))
                if(final.Get(d.Path)!=defaults.Get(d.Path))snapshot.Overrides.Add(new ProvingProfileValue{Path=d.Path,Value=final.Get(d.Path)});
            snapshot.Integrity=Integrity(snapshot,v=>snapshot.Integrity=v,snapshot.Integrity);
            return JsonUtility.ToJson(snapshot,true);
        }
        public static void ApplyRuntimeSnapshot(LabBundle runtime,string json)
        {
            var snapshot=JsonUtility.FromJson<BalanceRuntimeSnapshot>(json);
            if(snapshot?.Schema!=1||snapshot.Overrides==null||string.IsNullOrEmpty(snapshot.SourceHash)||snapshot.Integrity!=Integrity(snapshot,v=>snapshot.Integrity=v,snapshot.Integrity))throw new InvalidDataException("Повреждён независимый снимок оформления");
            var paths=runtime.Descriptors.ToDictionary(d=>d.Path,StringComparer.Ordinal);var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var value in snapshot.Overrides)
            {
                if(value==null||!seen.Add(value.Path)||Fields.ContainsKey(value.Path)||!paths.TryGetValue(value.Path,out var descriptor)||!descriptor.Contains(value.Value)||Math.Abs((value.Value-descriptor.DefaultValue)/descriptor.Step-Math.Round((value.Value-descriptor.DefaultValue)/descriptor.Step))>.002)
                    throw new InvalidDataException("Недопустимое значение независимого runtime-ресурса");
            }
            foreach(var value in snapshot.Overrides)runtime.Set(value.Path,value.Value);
        }
        // Preserve existing production interpretation of every shipped source. This runs
        // only in source conversion/QA; the client reads the resulting compact codec.
        public static LabReleaseCatalog ProjectExecutedReleases(LabReleaseCatalog original,LabBundle runtime,out LabBundle final)
        {
            var history=DesignLabHistory.ResolvePublishedDefault(runtime,original);var result=new LabReleaseCatalog();
            final=history.Selected.Snapshot;var profiles=history.Profiles;
            foreach(var source in original.Entries.OrderBy(e=>e.Sequence))
            {
                var execution=profiles.Single(p=>p.Id==source.ProfileId).Revisions.Single(r=>r.ReleaseSequence==source.Sequence&&history.Compatible(r));
                var projected=Project(execution.Snapshot);
                result.Entries.Add(new LabReleaseEntry{Sequence=source.Sequence,ProfileId=source.ProfileId,ProfileName=source.ProfileName,Revision=source.Revision,
                    Date=source.Date,SourceHash=source.Hash,Hash=projected.Hash(),Snapshot=projected});
            }
            return result;
        }
    }
}
