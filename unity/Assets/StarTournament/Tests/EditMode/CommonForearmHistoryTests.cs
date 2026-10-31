using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class CommonForearmHistoryTests
    {
        static ProvingProfile Previous()=>(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeCommonForearms",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ProvingProfile.CreateTrooperDefault(),null);
        [Test] public void OnlyCommonForearmFieldsChange()
        {
            var old=Previous();var current=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeHeavyAgileGait",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ProvingProfile.CreateTrooperDefault(),null);Assert.That(old.Version,Is.EqualTo(28));Assert.That(current.Version,Is.EqualTo(29));
            foreach(var d in old.Descriptors)Assert.That(current.Get(d.Path),Is.EqualTo(old.Get(d.Path)),d.Path);
            Assert.That(current.Descriptors.Count(d=>d.Path.StartsWith("view.common")),Is.EqualTo(16));Assert.That(current.Validate(),Is.Empty);
            foreach(var d in current.Descriptors.Where(d=>d.Path.StartsWith("view.common")))NativeMatchConfiguration.ValidateValue(current,d.Path,current.Get(d.Path));
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void DefaultUpgradesWhileAuthoredForearmsAndSourceBytesSurvive(int mode)
        {
            var path=Path.Combine(Path.GetTempPath(),"common-forearm-history-"+Guid.NewGuid()+".json");
            try
            {
                var old=new LabBundle{Profiles=new List<ProvingProfile>{Previous()}};var current=new LabBundle{Profiles=new List<ProvingProfile>{(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeHeavyAgileGait",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ProvingProfile.CreateTrooperDefault(),null)}};
                var history=new DesignLabHistory(path,old,authoring:mode==2);
                if(mode==1)history.Create("Intentional high switch");else history.Rename(mode==2?"Authored default":"Default");
                string hash=history.Selected.Hash,bytes=JsonUtility.ToJson(history.Selected.Snapshot);
                var migrated=new DesignLabHistory(path,current,authoring:mode==2);Assert.That(migrated.StorageError,Is.Null);
                Assert.That(migrated.Selected.Snapshot.Get("view.commonForearmsEnabled"),Is.EqualTo(mode==0?1:0));
                foreach(var d in old.Descriptors)Assert.That(migrated.Selected.Snapshot.Get(d.Path),Is.EqualTo(old.Get(d.Path)),d.Path);
                var originals=migrated.Profiles.SelectMany(p=>p.Revisions).Where(r=>r.Hash==hash).ToArray();Assert.That(originals,Is.Not.Empty);
                foreach(var revision in originals)Assert.That(JsonUtility.ToJson(revision.Snapshot),Is.EqualTo(bytes));
                var reopened=new DesignLabHistory(path,current,authoring:mode==2);Assert.That(reopened.StorageError,Is.Null);Assert.That(reopened.Selected.Hash,Is.EqualTo(migrated.Selected.Hash));
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
    }
}
