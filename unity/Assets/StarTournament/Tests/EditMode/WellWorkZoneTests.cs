using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class WellWorkZoneTests
    {
        [Test] public void OnlyFourBodiesChangeWhileCasesAndAllOtherAuthoredDataRemainExact()
        {
            var old=AuthoredArenaCatalog.Resolve(WellArenaCatalog.Id,"7");var current=AuthoredArenaCatalog.Resolve(WellArenaCatalog.Id,"8");
            Assert.That(current.Revision,Is.EqualTo("8"));Assert.That(old.Revision,Is.EqualTo("7"));
            Assert.That(current.Solids.Length,Is.EqualTo(old.Solids.Length));
            Assert.That(current.Solids.Count(WellFurniturePlan.IsFurniture),Is.EqualTo(56));
            var changed=current.Solids.Where(s=>JsonUtility.ToJson(s)!=JsonUtility.ToJson(old.Solids.Single(o=>o.Id==s.Id))).ToArray();
            Assert.That(changed.Select(s=>s.Id),Is.EquivalentTo(new[]{"furniture-f0-corner-bench-1-1","furniture-f1-corridor-bench","furniture-f2-corner-bench--1-1","furniture-f2-cabinet--1-1"}));
            Assert.That(current.Solids.Where(s=>s.Surface=="well-furniture-case").Select(JsonUtility.ToJson),Is.EqualTo(old.Solids.Where(s=>s.Surface=="well-furniture-case").Select(JsonUtility.ToJson)));
            Assert.That(current.Spawns,Is.EqualTo(old.Spawns));Assert.That(current.RouteAnchors,Is.EqualTo(old.RouteAnchors));
            Assert.That(current.Pickups.Select(JsonUtility.ToJson),Is.EqualTo(old.Pickups.Select(JsonUtility.ToJson)));
            Assert.That(current.SpawnRegions.Select(JsonUtility.ToJson),Is.EqualTo(old.SpawnRegions.Select(JsonUtility.ToJson)));
            Assert.That(current.Transitions.Select(JsonUtility.ToJson),Is.EqualTo(old.Transitions.Select(JsonUtility.ToJson)));
            Assert.That(current.Lifts.Select(JsonUtility.ToJson),Is.EqualTo(old.Lifts.Select(JsonUtility.ToJson)));
            foreach(var solid in changed)
            {
                var bounds=new Bounds(solid.Position,solid.Size-Vector3.one*.002f);
                foreach(var other in current.Solids.Where(o=>o.Id!=solid.Id))
                    Assert.That(bounds.Intersects(new Bounds(other.Position,other.Size-Vector3.one*.002f)),Is.False,solid.Id+" overlaps "+other.Id);
            }
            var profile=WellArenaCatalog.AuthoringProfile(previousDensity:true);Assert.That(profile.Version,Is.EqualTo(8));Assert.That(profile.Validate(),Is.Empty);
            foreach(var solid in changed)foreach(var axis in new[]{"x","y","z"})
                Assert.That(profile.Descriptors.Any(d=>d.Path=="well.map."+solid.Id+".position."+axis),Is.True,solid.Id);
        }
        [TestCase(false,7)][TestCase(true,7)][TestCase(false,6)][TestCase(true,6)]
        public void PreviousAuthoringHashesAndCustomValuesSurviveBothStorageFormats(bool compact,int revision)
        {
            var path=Path.Combine(Path.GetTempPath(),"well-work-zones-"+Guid.NewGuid()+".json");
            try
            {
                var current=DesignLabHistoryTests.Shipped();var old=current.Clone();
                old.Profiles=old.Profiles.Select(p=>p.Id=="well-authoring-v1"?WellArenaCatalog.AuthoringProfile(revision==6,revision==7):p).ToList();
                var history=new DesignLabHistory(path,old);history.Create("Старая расстановка");
                var draft=history.Selected.Snapshot;draft.Set("well.liftSpeed",5);history.Save(draft);
                string hash=history.Selected.Hash,bytes=JsonUtility.ToJson(history.Selected.Snapshot);int number=history.Selected.Number;
                if(!compact)File.WriteAllText(path,JsonUtility.ToJson(typeof(DesignLabHistory).GetField("data",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(history)));
                var next=new DesignLabHistory(path,current);Assert.That(next.StorageError,Is.Null);
                var saved=next.SelectedProfile.Revisions.Single(r=>r.Number==number);
                Assert.That(saved.Hash,Is.EqualTo(hash));Assert.That(saved.Snapshot.Hash(),Is.EqualTo(hash));Assert.That(JsonUtility.ToJson(saved.Snapshot),Is.EqualTo(bytes));
                Assert.That(next.Selected.Snapshot.Get("well.liftSpeed"),Is.EqualTo(5));
                Assert.That(next.Selected.Snapshot.Get("well.map.furniture-f1-corridor-bench.position.x"),Is.EqualTo(-18.2f));
                Assert.That(next.Selected.Snapshot.Profiles.Single(p=>p.Id=="well-authoring-v1").Version,Is.EqualTo(9));
                var restarted=new DesignLabHistory(path,current);Assert.That(restarted.StorageError,Is.Null);Assert.That(restarted.Selected.Hash,Is.EqualTo(next.Selected.Hash));
            }
            finally{foreach(var suffix in new[]{"",".schema1-backup.json"})if(File.Exists(path+suffix))File.Delete(path+suffix);}
        }
    }
}
