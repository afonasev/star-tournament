using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class WellWayfindingTests
    {
        static ProvingProfile Legacy(ProvingProfile p)=>(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeWellWayfinding",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,null);
        [Test]public void ReadabilityMetadataPreservesEveryPreviousDefinitionAndValue()
        {
            var current=ProvingProfile.CreateWellDefault();var old=Legacy(current);Assert.That(current.Version,Is.EqualTo(7));Assert.That(old.Version,Is.EqualTo(6));Assert.That(current.Validate(),Is.Empty);
            foreach(var d in old.Descriptors){Assert.That(JsonUtility.ToJson(current.Descriptors.Single(x=>x.Path==d.Path)),Is.EqualTo(JsonUtility.ToJson(d)),d.Path);Assert.That(current.Get(d.Path),Is.EqualTo(old.Get(d.Path)),d.Path);}
            var added=current.Descriptors.Where(d=>!old.Descriptors.Any(o=>o.Path==d.Path)).ToArray();Assert.That(added.Select(d=>d.Path),Is.EquivalentTo(new[]{"well.signReaderHeight","well.signHeaderHeight","well.signAtriumHeight","well.signTextHeight"}));
            foreach(var descriptor in added){Assert.That(descriptor.Group,Is.EqualTo("well-art"));Assert.That(descriptor.Unit,Is.EqualTo("m"));Assert.That(descriptor.Validate(out var reason),Is.True,reason);}
        }
        [TestCase(false)][TestCase(true)]public void PreviousPresentationHashesAndUserValuesSurviveBothStorageFormats(bool compact)
        {
            var path=Path.Combine(Path.GetTempPath(),"well-wayfinding-"+Guid.NewGuid()+".json");
            try
            {
                var current=new LabBundle{Profiles=new System.Collections.Generic.List<ProvingProfile>{ProvingProfile.CreateDefault(),ProvingProfile.CreateWellDefault(),WellArenaCatalog.AuthoringProfile()}};
                var old=current.Clone();old.Profiles=old.Profiles.Select(Legacy).ToList();var history=new DesignLabHistory(path,old);history.Create("Колодец до табличек");var draft=history.Selected.Snapshot;draft.Set("well.signScale",1.4f);draft.Set("well.lamp",9);history.Save(draft);
                string hash=history.Selected.Hash,bytes=JsonUtility.ToJson(history.Selected.Snapshot);int number=history.Selected.Number;
                if(!compact)File.WriteAllText(path,JsonUtility.ToJson(typeof(DesignLabHistory).GetField("data",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(history)));
                var migrated=new DesignLabHistory(path,current);Assert.That(migrated.StorageError,Is.Null);var saved=migrated.SelectedProfile.Revisions.Single(r=>r.Number==number);
                Assert.That(saved.Hash,Is.EqualTo(hash));Assert.That(saved.Snapshot.Hash(),Is.EqualTo(hash));Assert.That(JsonUtility.ToJson(saved.Snapshot),Is.EqualTo(bytes));
                Assert.That(migrated.Selected.Snapshot.Get("well.signScale"),Is.EqualTo(1.4f));Assert.That(migrated.Selected.Snapshot.Get("well.lamp"),Is.EqualTo(9));Assert.That(migrated.Selected.Snapshot.Get("well.signReaderHeight"),Is.EqualTo(2.1f));
                Assert.That(migrated.Selected.Snapshot.Profiles.Single(p=>p.Id=="well-gameplay-presentation-v1").Version,Is.EqualTo(7));
                var restarted=new DesignLabHistory(path,current);Assert.That(restarted.StorageError,Is.Null);Assert.That(restarted.Selected.Hash,Is.EqualTo(migrated.Selected.Hash));Assert.That(restarted.SelectedProfile.Revisions.Single(r=>r.Number==number).Hash,Is.EqualTo(hash));
            }
            finally{foreach(var suffix in new[]{"",".schema1-backup.json"})if(File.Exists(path+suffix))File.Delete(path+suffix);}
        }
    }
}
