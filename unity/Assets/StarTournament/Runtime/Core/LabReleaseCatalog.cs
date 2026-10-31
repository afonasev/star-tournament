using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Text;
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
        // Only Resources.Load establishes this immutable authority. Mutable caller/history
        // metadata is still reconstructed from the trusted registry, never promoted to it.
        [NonSerialized] Dictionary<int,LabReleaseEntry> packagedEntries;
        [NonSerialized] string packagedIdentity;
        internal string PackagedIdentity=>packagedIdentity??"";
        public LabReleaseCatalog Clone()=>new LabReleaseCatalog{Schema=Schema,Entries=Entries?.Select(e=>e?.Clone()).ToList(),
            packagedEntries=packagedEntries,packagedIdentity=packagedIdentity};
        internal LabBundle PackagedSnapshot(string profileId,string hash)
            =>packagedEntries?.Values.FirstOrDefault(e=>e.ProfileId==profileId&&e.Hash==hash)?.Snapshot.Clone();
        internal void ValidatePackagedEntry(LabReleaseEntry entry)
        {
            if(packagedEntries==null)return;
            if(!packagedEntries.TryGetValue(entry.Sequence,out var original)||entry.ProfileId!=original.ProfileId||
                entry.ProfileName!=original.ProfileName||entry.Revision!=original.Revision||entry.Date!=original.Date||entry.Hash!=original.Hash)
                throw new InvalidDataException("Встроенная релизная идентичность изменена");
        }
        public static LabReleaseCatalog Factory(LabBundle bundle)=>new LabReleaseCatalog{Entries=new List<LabReleaseEntry>{
            new LabReleaseEntry{Sequence=1,ProfileId=DesignLabHistory.ReleaseId,ProfileName="Default",Revision=1,
                Date="",Hash=bundle.Hash(),Snapshot=bundle.Clone()}}};
        public static LabReleaseCatalog Load()
        {
            var asset=Resources.Load<TextAsset>("LabReleaseCatalog");
            if(asset==null)throw new InvalidOperationException("Встроенный каталог релизных профилей отсутствует");
            var catalog=JsonUtility.FromJson<LabReleaseCatalog>(asset.text);
            catalog.packagedEntries=catalog.Entries.ToDictionary(e=>e.Sequence,e=>e.Clone());
            using(var sha=SHA256.Create())catalog.packagedIdentity=Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(asset.text)));
            return catalog;
        }
    }
}
