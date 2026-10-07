using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    [Serializable] public sealed class LabReleaseEntry
    {
        public int Sequence;
        public string ProfileId,ProfileName;
        public int Revision;
        public string Date,Hash;
        public LabBundle Snapshot;
        public LabReleaseEntry Clone()=>new LabReleaseEntry{Sequence=Sequence,ProfileId=ProfileId,ProfileName=ProfileName,
            Revision=Revision,Date=Date,Hash=Hash,Snapshot=Snapshot?.Clone()};
    }
    [Serializable] public sealed class LabReleaseCatalog
    {
        public int Schema=1;
        public List<LabReleaseEntry> Entries=new List<LabReleaseEntry>();
        public LabReleaseCatalog Clone()=>new LabReleaseCatalog{Schema=Schema,Entries=Entries?.Select(e=>e?.Clone()).ToList()};
        public static LabReleaseCatalog Factory(LabBundle bundle)=>new LabReleaseCatalog{Entries=new List<LabReleaseEntry>{
            new LabReleaseEntry{Sequence=1,ProfileId=DesignLabHistory.ReleaseId,ProfileName="Default",Revision=1,
                Date="",Hash=bundle.Hash(),Snapshot=bundle.Clone()}}};
        public static LabReleaseCatalog Load()
        {
            var asset=Resources.Load<TextAsset>("LabReleaseCatalog");
            if(asset==null)throw new InvalidOperationException("Встроенный каталог релизных профилей отсутствует");
            return JsonUtility.FromJson<LabReleaseCatalog>(asset.text);
        }
    }
}
