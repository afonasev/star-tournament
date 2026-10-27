using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class WeaponViewPositionHistoryTests
    {
        static ProvingProfile Previous()=>(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeCloserWeaponView",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ProvingProfile.CreateTrooperDefault(),null);
        [Test] public void OnlyViewTranslationChangesAndSerializedCustomCoordinatesSurvive()
        {
            var old=Previous();var current=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeUnifiedArmGrips",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ProvingProfile.CreateTrooperDefault(),null);
            Assert.That(old.Version,Is.EqualTo(25));Assert.That(current.Version,Is.EqualTo(26));
            Assert.That(current.Get("view.y"),Is.EqualTo(-1.49f));Assert.That(current.Get("view.z"),Is.EqualTo(.01f));
            foreach(var d in old.Descriptors.Where(d=>d.Path!="view.y"&&d.Path!="view.z"))Assert.That(current.Get(d.Path),Is.EqualTo(old.Get(d.Path)),d.Path);
            old.Set("view.y",-1.61f);old.EnsureTrooperDescriptors();
            Assert.That(old.Get("view.y"),Is.EqualTo(-1.61f));Assert.That(old.Get("view.z"),Is.EqualTo(.01f));Assert.That(old.Validate(),Is.Empty);
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void DefaultMigratesButIntentionalHistoryAndSourceBytesStayExact(int mode)
        {
            string path=Path.Combine(Path.GetTempPath(),"weapon-view-history-"+Guid.NewGuid()+".json");
            try
            {
                var old=new LabBundle{Profiles=new List<ProvingProfile>{Previous()}};
                var current=new LabBundle{Profiles=new List<ProvingProfile>{ProvingProfile.CreateTrooperDefault()}};
                var h=new DesignLabHistory(path,old,authoring:mode==2);
                if(mode==1)h.Create("Intentional old placement");else h.Rename(mode==2?"Authored default":"Default");
                string hash=h.Selected.Hash,bytes=JsonUtility.ToJson(h.Selected.Snapshot);
                var upgraded=new DesignLabHistory(path,current,authoring:mode==2);Assert.That(upgraded.StorageError,Is.Null);
                foreach(var key in new[]{"view.y","view.z"})Assert.That(upgraded.Selected.Snapshot.Get(key),Is.EqualTo(mode==0?current.Get(key):old.Get(key)),key);
                var originals=upgraded.Profiles.SelectMany(p=>p.Revisions).Where(r=>r.Hash==hash).ToArray();Assert.That(originals,Is.Not.Empty);
                foreach(var revision in originals)Assert.That(JsonUtility.ToJson(revision.Snapshot),Is.EqualTo(bytes));
                var reopened=new DesignLabHistory(path,current,authoring:mode==2);Assert.That(reopened.StorageError,Is.Null);Assert.That(reopened.Selected.Hash,Is.EqualTo(upgraded.Selected.Hash));
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
    }
}
