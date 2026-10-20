using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class WellPickupCircleProfileTests
    {
        [Test] public void PaintMetadataFitsEveryBridgeWithoutChangingLighting()
        {
            var current=ProvingProfile.CreateWellDefault();Assert.That(current.Version,Is.EqualTo(6));Assert.That(current.Validate(),Is.Empty);
            var predecessor=typeof(ProvingProfile).GetMethod("BeforeWellPickupMarkings",BindingFlags.Instance|BindingFlags.NonPublic);
            var old=(ProvingProfile)predecessor.Invoke(current,null);Assert.That(old.Version,Is.EqualTo(5));
            foreach(var d in old.Descriptors)Assert.That(JsonUtility.ToJson(current.Descriptors.Single(x=>x.Path==d.Path)),Is.EqualTo(JsonUtility.ToJson(d)),d.Path);
            foreach(var d in old.Descriptors)Assert.That(current.Get(d.Path),Is.EqualTo(old.Get(d.Path)),d.Path);
            foreach(var key in new[]{"well.pickupRingRadius","well.pickupRingWidth","well.pickupRingHeight"})
            {var d=current.Descriptors.Single(x=>x.Path==key);Assert.That(d.Group,Is.EqualTo("well-pickups"));Assert.That(d.Validate(out var reason),Is.True,reason);}
            var radius=current.Descriptors.Single(x=>x.Path=="well.pickupRingRadius");var width=current.Descriptors.Single(x=>x.Path=="well.pickupRingWidth");
            Assert.That(radius.Maximum+width.Maximum/2,Is.LessThan(1.5f),"Narrowest H deck half-width");
            Assert.That(current.Get("well.pickupRingWidth"),Is.EqualTo(ProvingProfile.CreateOrbitalLeagueDefault().Get("detail.lineWidth")));
        }
        [TestCase(false)][TestCase(true)] public void PreserveV5HashesAndCustomValues(bool compact)
        {
            string directory=Path.Combine(Path.GetTempPath(),"well-circles-"+Guid.NewGuid());Directory.CreateDirectory(directory);string path=Path.Combine(directory,"history.json");
            try
            {
                var current=DesignLabHistoryTests.Shipped();var old=current.Clone();
                var predecessor=typeof(ProvingProfile).GetMethod("BeforeWellPickupMarkings",BindingFlags.Instance|BindingFlags.NonPublic);
                old.Profiles=old.Profiles.Select(p=>(ProvingProfile)predecessor.Invoke(p,null)).ToList();
                var history=new DesignLabHistory(path,old);history.Create("Колодец без кругов");var draft=history.Selected.Snapshot.Clone();draft.Set("well.lamp",9);draft.Set("well.liftSpeed",5);history.Save(draft);
                string hash=history.Selected.Hash;int number=history.Selected.Number;
                if(!compact){var file=typeof(DesignLabHistory).GetField("data",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(history);File.WriteAllText(path,JsonUtility.ToJson(file));}
                var upgraded=new DesignLabHistory(path,current);Assert.That(upgraded.StorageError,Is.Null);
                var preserved=upgraded.SelectedProfile.Revisions.Single(r=>r.Number==number);
                Assert.That(preserved.Hash,Is.EqualTo(hash));Assert.That(preserved.Snapshot.Hash(),Is.EqualTo(hash));
                Assert.That(upgraded.Selected.Snapshot.Profiles.Single(p=>p.Id=="well-gameplay-presentation-v1").Version,Is.EqualTo(6));
                Assert.That(upgraded.Selected.Snapshot.Get("well.lamp"),Is.EqualTo(9));Assert.That(upgraded.Selected.Snapshot.Get("well.liftSpeed"),Is.EqualTo(5));
                Assert.That(upgraded.Selected.Snapshot.Get("well.pickupRingRadius"),Is.EqualTo(1.35f));
                var reopened=new DesignLabHistory(path,current);Assert.That(reopened.StorageError,Is.Null);Assert.That(reopened.Selected.Hash,Is.EqualTo(upgraded.Selected.Hash));
                Assert.That(reopened.SelectedProfile.Revisions.Single(r=>r.Number==number).Hash,Is.EqualTo(hash));
            }
            finally{Directory.Delete(directory,true);}
        }
    }
}
