using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class WellArenaTests
    {
        [Test] public void AuthoredIdentityGeometryAndLoadoutAreValid()
        {
            var d=WellArenaCatalog.Build();Assert.That(ArenaDefinitionValidator.Validate(d,ProvingProfile.CreateDefault()).IsValid,Is.True,ArenaDefinitionValidator.Validate(d,ProvingProfile.CreateDefault()).ToString());
            Assert.That(AuthoredArenaCatalog.Name(d.MapId),Is.EqualTo("Колодец"));Assert.That(AuthoredArenaCatalog.Maximum(d.MapId),Is.EqualTo(8));
            Assert.That(d.Revision,Is.EqualTo("3"));Assert.That(d.Spawns.Length,Is.EqualTo(10));Assert.That(d.Pickups.Length,Is.EqualTo(9));
            Assert.That(d.Pickups.Count(p=>p.Kind==ArenaPickupKind.Armor),Is.EqualTo(3));
            foreach(var kind in new[]{ArenaPickupKind.Speed,ArenaPickupKind.Shotgun,ArenaPickupKind.Pulse,ArenaPickupKind.Cutter,ArenaPickupKind.Damage,ArenaPickupKind.FullHeal})
                Assert.That(d.Pickups.Count(p=>p.Kind==kind),Is.EqualTo(1),kind.ToString());
            Assert.That(d.DamagePickupAnchor,Is.EqualTo(Vector3.zero));
            Assert.That(d.Pickups.Single(p=>p.Kind==ArenaPickupKind.Pulse).Anchor,Is.EqualTo(new Vector3(0,WellArenaCatalog.Storey,0)));
            Assert.That(d.Pickups.Single(p=>p.Kind==ArenaPickupKind.FullHeal).Anchor,Is.EqualTo(new Vector3(0,2*WellArenaCatalog.Storey,0)));
            Assert.That(d.Lifts.Single(x=>x.Id=="north").InitialHeight,Is.EqualTo(2*WellArenaCatalog.Storey));Assert.That(d.Lifts.Single(x=>x.Id=="south").InitialHeight,Is.EqualTo(0));
            foreach(var lift in d.Lifts)Assert.That(Mathf.Abs(lift.Center.z)-lift.Size.z/2,Is.GreaterThan(WellArenaCatalog.Edge));
            Assert.That(d.Transitions.Length,Is.EqualTo(8));Assert.That(d.Solids.Where(x=>x.Surface=="well-grate").All(x=>x.Layer==ProvingArena.MovementOnlyLayer),Is.True);
            var profile=WellArenaCatalog.AuthoringProfile();Assert.That(profile.Validate(),Is.Empty);Assert.That(ProvingProfile.CreateWellDefault().Validate(),Is.Empty);
            Assert.That(JsonUtility.ToJson(AuthoredArenaCatalog.Freeze(d.MapId,ProvingProfile.CreateDefault()).Definition),Is.EqualTo(JsonUtility.ToJson(d)));
        }
        [Test] public void ApprovedRoomsSpawnsAndPickupSectorsAreExplicit()
        {
            var d=WellArenaCatalog.Build();
            for(int f=0;f<3;f++)
            {
                float y=f*WellArenaCatalog.Storey;
                Assert.That(d.Pickups.Count(p=>p.Anchor.y==y),Is.EqualTo(3));
                foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})
                    Assert.That(d.Spawns.Contains(new Vector3(x*21,y,z*21)),Is.EqualTo(!(f==2&&x==z)),"corner "+f+"/"+x+"/"+z);
                foreach(var deck in d.Solids.Where(q=>q.Id.StartsWith("f"+f+"-")&&q.Material=="floor"&&q.Surface!="well-grate"))
                    Assert.That(deck.Size.y,Is.EqualTo(.8f));
                Assert.That(d.Solids.Single(q=>q.Id=="f"+f+"-outer-x1-a").Size.y,Is.EqualTo(6.4f).Within(.00001f));
            }
            Assert.That(d.Pickups.Single(p=>p.Id=="armor-01").Anchor,Is.EqualTo(new Vector3(0,0,21)));
            Assert.That(d.Pickups.Single(p=>p.Id=="shotgun-01").Anchor,Is.EqualTo(new Vector3(0,0,-21)));
            Assert.That(d.Pickups.Single(p=>p.Id=="armor-02").Anchor,Is.EqualTo(new Vector3(-12.8f,WellArenaCatalog.Storey,6.4f)));
            Assert.That(d.Pickups.Single(p=>p.Id=="speed-02").Anchor,Is.EqualTo(new Vector3(12.8f,WellArenaCatalog.Storey,-6.4f)));
            Assert.That(d.Pickups.Single(p=>p.Id=="armor-03").Anchor,Is.EqualTo(new Vector3(-21,2*WellArenaCatalog.Storey,-21)));
            Assert.That(d.Pickups.Single(p=>p.Id=="cutter-03").Anchor,Is.EqualTo(new Vector3(21,2*WellArenaCatalog.Storey,21)));
            Assert.That(d.Solids.Any(q=>q.Id.StartsWith("spawn-cover")),Is.False);
            foreach(var transition in d.Transitions)
            {
                Assert.That(transition.OrderedFeet.Length,Is.EqualTo(17));
                Assert.That(Mathf.Abs(transition.OrderedFeet.Last().y-transition.OrderedFeet.First().y),Is.EqualTo(3.6f).Within(.00001f));
            }
        }
        [Test] public void FurnitureIsFixedSparseAndPreservesEveryExistingAuthoredElement()
        {
            var current=WellArenaCatalog.Build();var previous=WellArenaRevisionTwo.Build();
            var furniture=current.Solids.Where(WellFurniturePlan.IsFurniture).ToArray();
            Assert.That(furniture.Length,Is.EqualTo(44));Assert.That(furniture.Count(s=>s.Id.Contains("gallery-cover")),Is.EqualTo(6));
            Assert.That(current.Solids.Where(s=>!WellFurniturePlan.IsFurniture(s)).Select(JsonUtility.ToJson),Is.EqualTo(previous.Solids.Select(JsonUtility.ToJson)),"Every old structural solid stays exact");
            Assert.That(current.Spawns,Is.EqualTo(previous.Spawns));Assert.That(current.Pickups.Select(JsonUtility.ToJson),Is.EqualTo(previous.Pickups.Select(JsonUtility.ToJson)));
            Assert.That(current.Lifts.Select(JsonUtility.ToJson),Is.EqualTo(previous.Lifts.Select(JsonUtility.ToJson)));Assert.That(current.Transitions.Select(JsonUtility.ToJson),Is.EqualTo(previous.Transitions.Select(JsonUtility.ToJson)));
            foreach(var item in furniture)
            {
                Assert.That(item.Layer,Is.EqualTo(ProvingArena.WorldLayer));
                var bounds=new Bounds(item.Position,item.Size-Vector3.one*.002f);
                foreach(var other in current.Solids.Where(s=>s.Id!=item.Id))Assert.That(bounds.Intersects(new Bounds(other.Position,other.Size-Vector3.one*.002f)),Is.False,item.Id+" overlaps "+other.Id);
                foreach(var pos in current.Spawns.Concat(current.Pickups.Select(p=>p.Anchor)))
                    Assert.That(bounds.SqrDistance(pos+Vector3.up*.7f),Is.GreaterThan(1),item.Id+" obstructs "+pos);
                if(item.Id.Contains("gallery-cover"))Assert.That(Mathf.Abs(item.Position.z)-item.Size.z/2-WellArenaCatalog.Pit,Is.GreaterThan(3),"wide gallery bypass");
            }
            Assert.That(JsonUtility.ToJson(current),Is.EqualTo(JsonUtility.ToJson(WellArenaCatalog.Build())),"No randomized layout");
        }
        [Test] public void AddingWellProfilesPreservesHistoricalLabHashAndEditableValues()
        {
            var path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"well-lab-"+Guid.NewGuid()+".json");
            try
            {
                var old=StarTournament.ProvingGround.Tests.EditMode.DesignLabHistoryTests.Shipped();old.Profiles.RemoveAll(x=>x.Id=="well-authoring-v1"||x.Id=="well-gameplay-presentation-v1");var history=new DesignLabHistory(path,old);history.Create("До Колодца");var draft=history.Selected.Snapshot;draft.Set("lunar.officeLamp",15);history.Save(draft);var prior=history.Selected;
                var next=old.Clone();next.Profiles.Add(ProvingProfile.CreateWellDefault());next.Profiles.Add(WellArenaCatalog.AuthoringProfile());
                var migrated=new DesignLabHistory(path,next);Assert.That(migrated.StorageError,Is.Null);Assert.That(migrated.SelectedProfile.Revisions.Single(x=>x.Number==prior.Number).Hash,Is.EqualTo(prior.Hash));
                Assert.That(migrated.Selected.Snapshot.Get("lunar.officeLamp"),Is.EqualTo(15));Assert.That(migrated.Selected.Snapshot.Get("well.liftSpeed"),Is.EqualTo(4));Assert.That(migrated.Selected.Snapshot.Domain("well.liftSpeed"),Is.EqualTo("Gameplay"));Assert.That(migrated.Selected.Snapshot.IsAuthoring("well.map.lift-south.size.x"),Is.True);
            }
            finally{if(System.IO.File.Exists(path))System.IO.File.Delete(path);}
        }
        [TestCase(false)][TestCase(true)] public void PreviousWellGeometryLoadsWithoutChangingSavedHashes(bool compact)
        {
            var path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"well-revision-"+Guid.NewGuid()+".json");
            try
            {
                var current=StarTournament.ProvingGround.Tests.EditMode.DesignLabHistoryTests.Shipped();
                var method=typeof(ProvingProfile).GetMethod("BeforeWellStructure",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                var old=current.Clone();old.Profiles=old.Profiles.Select(p=>(ProvingProfile)method.Invoke(p,null)).ToList();
                var history=new DesignLabHistory(path,old);history.Create("Колодец 02");var draft=history.Selected.Snapshot;draft.Set("well.liftSpeed",5);history.Save(draft);
                var prior=history.Selected;string priorHash=prior.Hash;int priorNumber=prior.Number;
                if(!compact)
                {
                    var file=typeof(DesignLabHistory).GetField("data",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(history);
                    System.IO.File.WriteAllText(path,JsonUtility.ToJson(file));
                }
                var migrated=new DesignLabHistory(path,current);Assert.That(migrated.StorageError,Is.Null);
                var preserved=migrated.SelectedProfile.Revisions.Single(r=>r.Number==priorNumber);
                Assert.That(preserved.Hash,Is.EqualTo(priorHash));Assert.That(preserved.Snapshot.Hash(),Is.EqualTo(priorHash));
                Assert.That(preserved.Snapshot.Get("well.map.lift-north.top"),Is.EqualTo(10));
                Assert.That(migrated.Selected.Snapshot.Get("well.liftSpeed"),Is.EqualTo(5));
                Assert.That(migrated.Selected.Snapshot.Get("well.map.lift-north.top"),Is.EqualTo(2*WellArenaCatalog.Storey));
                var restarted=new DesignLabHistory(path,current);Assert.That(restarted.StorageError,Is.Null);
                Assert.That(restarted.Selected.Hash,Is.EqualTo(migrated.Selected.Hash));
                Assert.That(restarted.SelectedProfile.Revisions.Single(r=>r.Number==priorNumber).Hash,Is.EqualTo(priorHash));
            }
            finally
            {
                if(System.IO.File.Exists(path))System.IO.File.Delete(path);
                if(System.IO.File.Exists(path+".schema1-backup.json"))System.IO.File.Delete(path+".schema1-backup.json");
            }
        }
        [TestCase(false)][TestCase(true)] public void PreviousUnfurnishedWellLoadsWithoutChangingSavedHashes(bool compact)
        {
            var path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"well-revision-"+Guid.NewGuid()+".json");
            try
            {
                var current=StarTournament.ProvingGround.Tests.EditMode.DesignLabHistoryTests.Shipped();
                var method=typeof(ProvingProfile).GetMethod("BeforeWellFurniture",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                var old=current.Clone();old.Profiles=old.Profiles.Select(p=>(ProvingProfile)method.Invoke(p,null)).ToList();
                var history=new DesignLabHistory(path,old);history.Create("Колодец без мебели");var draft=history.Selected.Snapshot;draft.Set("well.liftSpeed",5);history.Save(draft);
                var prior=history.Selected;string priorHash=prior.Hash;int priorNumber=prior.Number;
                if(!compact)
                {
                    var file=typeof(DesignLabHistory).GetField("data",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(history);
                    System.IO.File.WriteAllText(path,JsonUtility.ToJson(file));
                }
                var migrated=new DesignLabHistory(path,current);Assert.That(migrated.StorageError,Is.Null);
                var preserved=migrated.SelectedProfile.Revisions.Single(r=>r.Number==priorNumber);
                Assert.That(preserved.Hash,Is.EqualTo(priorHash));Assert.That(preserved.Snapshot.Hash(),Is.EqualTo(priorHash));
                Assert.That(preserved.Snapshot.Get("well.map.lift-north.top"),Is.EqualTo(2*WellArenaCatalog.Storey));
                Assert.That(migrated.Selected.Snapshot.Get("well.liftSpeed"),Is.EqualTo(5));
                Assert.That(migrated.Selected.Snapshot.Get("well.map.lift-north.top"),Is.EqualTo(2*WellArenaCatalog.Storey));
                var restarted=new DesignLabHistory(path,current);Assert.That(restarted.StorageError,Is.Null);
                Assert.That(restarted.Selected.Hash,Is.EqualTo(migrated.Selected.Hash));
                Assert.That(restarted.SelectedProfile.Revisions.Single(r=>r.Number==priorNumber).Hash,Is.EqualTo(priorHash));
            }
            finally
            {
                if(System.IO.File.Exists(path))System.IO.File.Delete(path);
                if(System.IO.File.Exists(path+".schema1-backup.json"))System.IO.File.Delete(path+".schema1-backup.json");
            }
        }
        [TestCase(false)][TestCase(true)] public void PreviousWellArtLoadsWithoutChangingSavedHashes(bool compact)
        {
            var path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"well-revision-"+Guid.NewGuid()+".json");
            try
            {
                var current=StarTournament.ProvingGround.Tests.EditMode.DesignLabHistoryTests.Shipped();
                var method=typeof(ProvingProfile).GetMethod("BeforeWellDetail",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                var old=current.Clone();old.Profiles=old.Profiles.Select(p=>(ProvingProfile)method.Invoke(p,null)).ToList();
                var history=new DesignLabHistory(path,old);history.Create("Колодец 02");var draft=history.Selected.Snapshot;draft.Set("well.liftSpeed",5);draft.Set("well.lamp",9);history.Save(draft);
                var prior=history.Selected;string priorHash=prior.Hash;int priorNumber=prior.Number;
                if(!compact)
                {
                    var file=typeof(DesignLabHistory).GetField("data",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(history);
                    System.IO.File.WriteAllText(path,JsonUtility.ToJson(file));
                }
                var migrated=new DesignLabHistory(path,current);Assert.That(migrated.StorageError,Is.Null);
                var preserved=migrated.SelectedProfile.Revisions.Single(r=>r.Number==priorNumber);
                Assert.That(preserved.Hash,Is.EqualTo(priorHash));Assert.That(preserved.Snapshot.Hash(),Is.EqualTo(priorHash));
                Assert.That(preserved.Snapshot.Get("well.fill"),Is.EqualTo(.8f));
                Assert.That(migrated.Selected.Snapshot.Get("well.liftSpeed"),Is.EqualTo(5));
                Assert.That(migrated.Selected.Snapshot.Get("well.fill"),Is.EqualTo(.42f));
                Assert.That(migrated.Selected.Snapshot.Get("well.lamp"),Is.EqualTo(9));
                Assert.That(migrated.Selected.Snapshot.Get("well.plantHeight"),Is.EqualTo(.5f));
                var restarted=new DesignLabHistory(path,current);Assert.That(restarted.StorageError,Is.Null);
                Assert.That(restarted.Selected.Hash,Is.EqualTo(migrated.Selected.Hash));
                Assert.That(restarted.SelectedProfile.Revisions.Single(r=>r.Number==priorNumber).Hash,Is.EqualTo(priorHash));
            }
            finally
            {
                if(System.IO.File.Exists(path))System.IO.File.Delete(path);
                if(System.IO.File.Exists(path+".schema1-backup.json"))System.IO.File.Delete(path+".schema1-backup.json");
            }
        }
        [TestCase(false)][TestCase(true)] public void PreviousWellDepthLoadsWithoutChangingSavedHashes(bool compact)
        {
            var path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"well-depth-"+Guid.NewGuid()+".json");
            try
            {
                var current=StarTournament.ProvingGround.Tests.EditMode.DesignLabHistoryTests.Shipped();
                var method=typeof(ProvingProfile).GetMethod("BeforeWellDepth",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                var old=current.Clone();old.Profiles=old.Profiles.Select(p=>(ProvingProfile)method.Invoke(p,null)).ToList();
                var history=new DesignLabHistory(path,old);history.Create("Колодец 02");var draft=history.Selected.Snapshot;draft.Set("well.liftSpeed",5);draft.Set("well.lamp",9);draft.Set("well.glassOpacity",.4f);history.Save(draft);
                var prior=history.Selected;string priorHash=prior.Hash;int priorNumber=prior.Number;
                if(!compact)
                {
                    var file=typeof(DesignLabHistory).GetField("data",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(history);
                    System.IO.File.WriteAllText(path,JsonUtility.ToJson(file));
                }
                var migrated=new DesignLabHistory(path,current);Assert.That(migrated.StorageError,Is.Null);
                var preserved=migrated.SelectedProfile.Revisions.Single(r=>r.Number==priorNumber);
                Assert.That(preserved.Hash,Is.EqualTo(priorHash));Assert.That(preserved.Snapshot.Hash(),Is.EqualTo(priorHash));
                Assert.That(preserved.Snapshot.Get("well.fill"),Is.EqualTo(.42f));
                Assert.That(preserved.Snapshot.Get("well.daylight"),Is.EqualTo(.5f));
                Assert.That(migrated.Selected.Snapshot.Get("well.liftSpeed"),Is.EqualTo(5));
                Assert.That(migrated.Selected.Snapshot.Get("well.fill"),Is.EqualTo(.42f));
                Assert.That(migrated.Selected.Snapshot.Get("well.lamp"),Is.EqualTo(9));
                Assert.That(migrated.Selected.Snapshot.Get("well.plantHeight"),Is.EqualTo(.5f));
                Assert.That(migrated.Selected.Snapshot.Get("well.glassOpacity"),Is.EqualTo(.4f));
                Assert.That(migrated.Selected.Snapshot.Get("well.daylight"),Is.EqualTo(.8f));
                Assert.That(migrated.Selected.Snapshot.Get("well.glassSmoothness"),Is.EqualTo(.85f));
                var restarted=new DesignLabHistory(path,current);Assert.That(restarted.StorageError,Is.Null);
                Assert.That(restarted.Selected.Hash,Is.EqualTo(migrated.Selected.Hash));
                Assert.That(restarted.SelectedProfile.Revisions.Single(r=>r.Number==priorNumber).Hash,Is.EqualTo(priorHash));
            }
            finally
            {
                if(System.IO.File.Exists(path))System.IO.File.Delete(path);
                if(System.IO.File.Exists(path+".schema1-backup.json"))System.IO.File.Delete(path+".schema1-backup.json");
            }
        }
        WellLiftSystem Lifts()=>new WellLiftSystem(WellArenaCatalog.Build().Lifts,ProvingProfile.CreateWellDefault());
        int[][] Occupied()=>new[]{Array.Empty<int>(),new[]{0}};
        int[][] Empty()=>new[]{Array.Empty<int>(),Array.Empty<int>()};
        [Test] public void ExactDelayEmptyTripTerminalStopAndReboarding()
        {
            var lifts=Lifts();lifts.Advance(.5f,Occupied());Assert.That(lifts.Capture()[1].Height,Is.EqualTo(0));Assert.That(lifts.Capture()[1].Countdown,Is.EqualTo(.5));
            lifts.Advance(.5f,Empty());Assert.That(lifts.Capture()[1].Height,Is.EqualTo(0));Assert.That(lifts.Capture()[1].Phase,Is.EqualTo(WellLiftPhase.Moving));
            lifts.Advance(1.25f,Empty());Assert.That(lifts.Capture()[1].Height,Is.EqualTo(5));
            var copy=lifts.Capture();var restored=Lifts();restored.Restore(JsonUtility.FromJson<Payload>(JsonUtility.ToJson(new Payload{Lifts=copy})).Lifts);
            float remaining=(2*WellArenaCatalog.Storey-5)/4;lifts.Advance(remaining,Empty());restored.Advance(remaining,Empty());Assert.That(JsonUtility.ToJson(new Payload{Lifts=lifts.Capture()}),Is.EqualTo(JsonUtility.ToJson(new Payload{Lifts=restored.Capture()})));
            Assert.That(lifts.Capture()[1].Height,Is.EqualTo(2*WellArenaCatalog.Storey));lifts.Advance(10,Empty());Assert.That(lifts.Capture()[1].Height,Is.EqualTo(2*WellArenaCatalog.Storey),"empty platform stops until a new boarding");
            lifts.Advance(.1f,Empty());lifts.Advance(.5f,Occupied());Assert.That(lifts.Capture()[1].Target,Is.EqualTo(0));Assert.That(lifts.Capture()[1].Countdown,Is.EqualTo(.5));
        }
        [Test] public void PassengerStayingAtTerminalCannotRearmAndEmptyArrivalAllowsNewBoarding()
        {
            var lifts=Lifts();lifts.Advance(10,Occupied());Assert.That(lifts.Capture()[1].Height,Is.EqualTo(2*WellArenaCatalog.Storey));Assert.That(lifts.Capture()[1].Phase,Is.EqualTo(WellLiftPhase.AwaitEmpty));
            lifts.Advance(10,Occupied());Assert.That(lifts.Capture()[1].Height,Is.EqualTo(2*WellArenaCatalog.Storey),"remaining passenger must not rearm");
            lifts.Advance(.1f,Empty());lifts.Advance(.5f,Occupied());Assert.That(lifts.Capture()[1].Countdown,Is.EqualTo(.5));
            var emptyTrip=Lifts();emptyTrip.Advance(1,Occupied());emptyTrip.Advance(2*WellArenaCatalog.Storey/4,Empty());Assert.That(emptyTrip.Capture()[1].Phase,Is.EqualTo(WellLiftPhase.Ready));
            emptyTrip.Advance(.5f,Occupied());Assert.That(emptyTrip.Capture()[1].Countdown,Is.EqualTo(.5));Assert.That(emptyTrip.Capture()[1].Target,Is.EqualTo(0),"new boarding after an empty arrival");
        }
        [Serializable] public sealed class Payload{public WellLiftState[] Lifts;}
        [Test] public void InvalidRestoreIsAtomicAndNoStationaryMidFloorAllowed()
        {
            var lifts=Lifts();string before=JsonUtility.ToJson(new Payload{Lifts=lifts.Capture()});var invalid=lifts.Capture();invalid[1].Height=5;
            Assert.Throws<ArgumentException>(()=>lifts.Restore(invalid));Assert.That(JsonUtility.ToJson(new Payload{Lifts=lifts.Capture()}),Is.EqualTo(before));
            invalid=lifts.Capture();invalid[1].Countdown=double.NaN;Assert.Throws<ArgumentException>(()=>lifts.Restore(invalid));
        }
    }
}
