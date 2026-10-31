using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class WellDensityTests
    {
        [Test]public void SixNewBodiesPreserveEveryPreviousSolidAndAnchorExactly()
        {
            var previous=AuthoredArenaCatalog.Resolve(WellArenaCatalog.Id,"8");var current=WellArenaCatalog.Build();
            var saved=JsonUtility.FromJson<ArenaDefinition>(File.ReadAllText(Path.Combine(Application.dataPath,"StarTournament/Tests/EditMode/Fixtures/well-work-zones-arena.json")));
            Assert.That(JsonUtility.ToJson(previous),Is.EqualTo(JsonUtility.ToJson(saved)),"Frozen revision 8 stays byte exact");
            Assert.That(current.Revision,Is.EqualTo("9"));Assert.That(current.Solids.Take(previous.Solids.Length).Select(JsonUtility.ToJson),Is.EqualTo(previous.Solids.Select(JsonUtility.ToJson)));
            var added=current.Solids.Skip(previous.Solids.Length).ToArray();Assert.That(added.Length,Is.EqualTo(6));
            Assert.That(current.Solids.Count(WellFurniturePlan.IsFurniture),Is.EqualTo(62));
            for(int floor=0;floor<3;floor++)
            {
                var bodies=added.Where(s=>s.Id.StartsWith("furniture-f"+floor+"-density-")).ToArray();
                Assert.That(bodies.Length,Is.EqualTo(2));Assert.That(bodies.Count(s=>s.Surface=="well-furniture-bench"),Is.EqualTo(1));Assert.That(bodies.Count(s=>s.Surface=="well-furniture-cabinet"),Is.EqualTo(1));
            }
            foreach(var body in added)
            {
                Assert.That(body.Layer,Is.EqualTo(ProvingArena.WorldLayer));var bounds=new Bounds(body.Position,body.Size-Vector3.one*.002f);
                foreach(var other in current.Solids.Where(s=>s.Id!=body.Id))Assert.That(bounds.Intersects(new Bounds(other.Position,other.Size-Vector3.one*.002f)),Is.False,body.Id+" overlaps "+other.Id);
            }
            Assert.That(current.Spawns,Is.EqualTo(previous.Spawns));Assert.That(current.RouteAnchors,Is.EqualTo(previous.RouteAnchors));
            Assert.That(current.SpawnRegions.Select(JsonUtility.ToJson),Is.EqualTo(previous.SpawnRegions.Select(JsonUtility.ToJson)));
            Assert.That(current.Pickups.Select(JsonUtility.ToJson),Is.EqualTo(previous.Pickups.Select(JsonUtility.ToJson)));
            Assert.That(current.Lifts.Select(JsonUtility.ToJson),Is.EqualTo(previous.Lifts.Select(JsonUtility.ToJson)));
            Assert.That(current.Transitions.Select(JsonUtility.ToJson),Is.EqualTo(previous.Transitions.Select(JsonUtility.ToJson)));
            var profile=WellArenaCatalog.AuthoringProfile();Assert.That(profile.Version,Is.EqualTo(9));Assert.That(profile.Validate(),Is.Empty);
            foreach(var body in added)foreach(var axis in new[]{"x","y","z"})Assert.That(profile.Descriptors.Any(d=>d.Path=="well.map."+body.Id+".position."+axis),Is.True);
        }
        [TestCase(false)][TestCase(true)]public void RevisionEightHashesAndCustomValuesSurviveMigrationAndRestart(bool compact)
        {
            var path=Path.Combine(Path.GetTempPath(),"well-density-"+Guid.NewGuid()+".json");
            try
            {
                var old=new LabBundle{Profiles=new System.Collections.Generic.List<ProvingProfile>{ProvingProfile.CreateDefault(),ProvingProfile.CreateWellDefault(),WellArenaCatalog.AuthoringProfile(previousDensity:true)}};
                var current=old.Clone();current.Profiles[current.Profiles.FindIndex(p=>p.Id=="well-authoring-v1")]=WellArenaCatalog.AuthoringProfile();
                var history=new DesignLabHistory(path,old);history.Create("Колодец до уплотнения");var draft=history.Selected.Snapshot;draft.Set("well.liftSpeed",5);history.Save(draft);
                var prior=history.Selected;string hash=prior.Hash,bytes=JsonUtility.ToJson(prior.Snapshot);int number=prior.Number;
                if(!compact)File.WriteAllText(path,JsonUtility.ToJson(typeof(DesignLabHistory).GetField("data",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(history)));
                var migrated=new DesignLabHistory(path,current);Assert.That(migrated.StorageError,Is.Null);
                var saved=migrated.SelectedProfile.Revisions.Single(r=>r.Number==number);
                Assert.That(saved.Hash,Is.EqualTo(hash));Assert.That(saved.Snapshot.Hash(),Is.EqualTo(hash));Assert.That(JsonUtility.ToJson(saved.Snapshot),Is.EqualTo(bytes));
                Assert.That(migrated.Selected.Snapshot.Profiles.Single(p=>p.Id=="well-authoring-v1").Version,Is.EqualTo(9));
                Assert.That(migrated.Selected.Snapshot.Get("well.liftSpeed"),Is.EqualTo(5));
                Assert.That(migrated.Selected.Snapshot.Get("well.map.furniture-f1-density-room-bench.position.x"),Is.EqualTo(15.3f));
                var restarted=new DesignLabHistory(path,current);Assert.That(restarted.StorageError,Is.Null);Assert.That(restarted.Selected.Hash,Is.EqualTo(migrated.Selected.Hash));
                Assert.That(restarted.SelectedProfile.Revisions.Single(r=>r.Number==number).Hash,Is.EqualTo(hash));
            }
            finally{foreach(var suffix in new[]{"",".schema1-backup.json"})if(File.Exists(path+suffix))File.Delete(path+suffix);}
        }
    }
}
