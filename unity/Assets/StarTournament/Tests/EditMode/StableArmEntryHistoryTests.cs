using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class StableArmEntryHistoryTests
    {
        static ProvingProfile Previous()=>(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeStableArmEntry",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ProvingProfile.CreateTrooperDefault(),null);
        [Test] public void OnlyViewAnchorsAndShortSwitchLiftChange()
        {
            var old=Previous();var current=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeCommonForearms",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ProvingProfile.CreateTrooperDefault(),null);Assert.That(old.Version,Is.EqualTo(27));Assert.That(current.Version,Is.EqualTo(28));
            Assert.That(old.Get("view.switchShoulderDegrees"),Is.EqualTo(60));Assert.That(current.Get("view.switchShoulderDegrees"),Is.EqualTo(10));
            foreach(var d in old.Descriptors.Where(d=>d.Path!="view.switchShoulderDegrees"))Assert.That(current.Get(d.Path),Is.EqualTo(old.Get(d.Path)),d.Path);
            Assert.That(current.Descriptors.Count(d=>d.Path.StartsWith("view.armEntry")),Is.EqualTo(3));Assert.That(current.Validate(),Is.Empty);
            old.Set("view.switchShoulderDegrees",17);old.EnsureTrooperDescriptors();Assert.That(old.Get("view.switchShoulderDegrees"),Is.EqualTo(17));
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void DefaultUpgradesWhileSavedArmEntryAndSourceBytesSurvive(int mode)
        {
            var path=Path.Combine(Path.GetTempPath(),"arm-entry-history-"+Guid.NewGuid()+".json");
            try
            {
                var old=new LabBundle{Profiles=new List<ProvingProfile>{Previous()}};var current=new LabBundle{Profiles=new List<ProvingProfile>{ProvingProfile.CreateTrooperDefault()}};
                var history=new DesignLabHistory(path,old,authoring:mode==2);
                if(mode==1)history.Create("Intentional high switch");else history.Rename(mode==2?"Authored default":"Default");
                string hash=history.Selected.Hash,bytes=JsonUtility.ToJson(history.Selected.Snapshot);
                var migrated=new DesignLabHistory(path,current,authoring:mode==2);Assert.That(migrated.StorageError,Is.Null);
                Assert.That(migrated.Selected.Snapshot.Get("view.armEntryEnabled"),Is.EqualTo(mode==0?1:0));
                foreach(var d in old.Descriptors)Assert.That(migrated.Selected.Snapshot.Get(d.Path),Is.EqualTo(mode==0&&d.Path=="view.switchShoulderDegrees"?10:old.Get(d.Path)),d.Path);
                var originals=migrated.Profiles.SelectMany(p=>p.Revisions).Where(r=>r.Hash==hash).ToArray();Assert.That(originals,Is.Not.Empty);
                foreach(var revision in originals)Assert.That(JsonUtility.ToJson(revision.Snapshot),Is.EqualTo(bytes));
                var reopened=new DesignLabHistory(path,current,authoring:mode==2);Assert.That(reopened.StorageError,Is.Null);Assert.That(reopened.Selected.Hash,Is.EqualTo(migrated.Selected.Hash));
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
    }
}
