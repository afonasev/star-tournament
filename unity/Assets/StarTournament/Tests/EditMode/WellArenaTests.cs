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
            Assert.That(d.Revision,Is.EqualTo("5"));Assert.That(d.Spawns.Length,Is.EqualTo(10));Assert.That(d.Pickups.Length,Is.EqualTo(11));
            Assert.That(d.Pickups.Count(p=>p.Kind==ArenaPickupKind.Armor),Is.EqualTo(3));
            foreach(var kind in new[]{ArenaPickupKind.Speed,ArenaPickupKind.Pulse,ArenaPickupKind.Damage,ArenaPickupKind.FullHeal})
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
        [Test] public void SecondFloorWeaponsPreserveRevisionFourAndStayClearOfSpawns()
        {
            var current=WellArenaCatalog.Build();var previous=WellArenaRevisionFour.Build();
            var saved=JsonUtility.FromJson<ArenaDefinition>(System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath,"StarTournament/Tests/EditMode/Fixtures/well-daylight-arena.json")));
            Assert.That(JsonUtility.ToJson(previous),Is.EqualTo(JsonUtility.ToJson(saved)),"Historical v4 stays byte exact");
            Assert.That(JsonUtility.ToJson(AuthoredArenaCatalog.Resolve(WellArenaCatalog.Id,"4")),Is.EqualTo(JsonUtility.ToJson(previous)));
            Assert.That(current.Spawns,Is.EqualTo(previous.Spawns));
            Assert.That(current.SpawnRegions.Select(JsonUtility.ToJson),Is.EqualTo(previous.SpawnRegions.Select(JsonUtility.ToJson)));
            Assert.That(current.Solids.Select(JsonUtility.ToJson),Is.EqualTo(previous.Solids.Select(JsonUtility.ToJson)));
            Assert.That(current.Transitions.Select(JsonUtility.ToJson),Is.EqualTo(previous.Transitions.Select(JsonUtility.ToJson)));
            Assert.That(current.Lifts.Select(JsonUtility.ToJson),Is.EqualTo(previous.Lifts.Select(JsonUtility.ToJson)));
            Assert.That(current.RouteAnchors,Is.EqualTo(previous.RouteAnchors));
            Assert.That(current.Pickups.Where(p=>p.Id!="shotgun-02"&&p.Id!="cutter-02").Select(JsonUtility.ToJson),Is.EqualTo(previous.Pickups.Select(JsonUtility.ToJson)));
            foreach(var kind in new[]{ArenaPickupKind.Shotgun,ArenaPickupKind.Cutter})Assert.That(current.Pickups.Count(p=>p.Kind==kind),Is.EqualTo(2));
            foreach(var id in new[]{"shotgun-02","cutter-02"})
            {
                var pickup=current.Pickups.Single(p=>p.Id==id);int side=id=="shotgun-02"?-1:1;
                Assert.That(pickup.Kind,Is.EqualTo(side<0?ArenaPickupKind.Shotgun:ArenaPickupKind.Cutter));
                Assert.That(pickup.Anchor,Is.EqualTo(new Vector3(side*18,WellArenaCatalog.Storey,side*20)));
                Assert.That(pickup.Support,Is.EqualTo(WellArenaCatalog.Support(1)));
                foreach(var spawn in current.Spawns.Where(p=>p.y==pickup.Anchor.y))Assert.That(Vector3.Distance(spawn,pickup.Anchor),Is.GreaterThan(ProvingProfile.CreateCombatDefault().Get("weaponPickup.radius")));
                Assert.That(WellArenaCatalog.AuthoringProfile().Descriptors.Any(d=>d.Path=="well.map."+id+".anchor.x"),Is.True);
            }
        }
        [Test] public void ApprovedRoomsSpawnsAndPickupSectorsAreExplicit()
        {
            var d=WellArenaCatalog.Build();
            for(int f=0;f<3;f++)
            {
                float y=f*WellArenaCatalog.Storey;
                Assert.That(d.Pickups.Count(p=>p.Anchor.y==y),Is.EqualTo(f==1?5:3));
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
            var current=WellArenaRevisionFour.Build();var previous=WellArenaRevisionTwo.Build();
            var furniture=current.Solids.Where(WellFurniturePlan.IsFurniture).ToArray();
            Assert.That(furniture.Length,Is.EqualTo(56));Assert.That(furniture.Count(s=>s.Id.Contains("gallery-cover")),Is.EqualTo(6));
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
            Assert.That(JsonUtility.ToJson(current),Is.EqualTo(JsonUtility.ToJson(WellArenaRevisionFour.Build())),"No randomized layout");
        }
        [Test] public void CorridorZonesAreSparseGroupedAndPreserveRevisionThreeExactly()
        {
            var current=WellArenaRevisionFour.Build();var old=WellArenaRevisionThree.Build();
            var added=current.Solids.Where(s=>s.Id.Contains("-corridor-")).ToArray();Assert.That(added.Length,Is.EqualTo(12));
            Assert.That(current.Solids.Where(s=>!s.Id.Contains("-corridor-")).Select(JsonUtility.ToJson),Is.EqualTo(old.Solids.Select(JsonUtility.ToJson)));
            var saved=JsonUtility.FromJson<ArenaDefinition>(System.IO.File.ReadAllText(System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,"StarTournament/Tests/EditMode/Fixtures/well-furniture-arena.json"))));
            Assert.That(JsonUtility.ToJson(old),Is.EqualTo(JsonUtility.ToJson(saved)),"Frozen v3 must match the original saved authoring, not a reconstruction with current changes");
            Assert.That(JsonUtility.ToJson(AuthoredArenaCatalog.Resolve(WellArenaCatalog.Id,"3")),Is.EqualTo(JsonUtility.ToJson(old)));
            float margin=ProvingProfile.CreateDefault().Get("player.capsule.radius")*2;
            var headers=current.Solids.Where(s=>s.Id.EndsWith("-header")).ToArray();
            for(int floor=0;floor<3;floor++)
            {
                var objects=added.Where(s=>s.Id.StartsWith("furniture-f"+floor+"-")).ToArray();Assert.That(objects.Length,Is.EqualTo(4));
                Assert.That(objects.Count(s=>s.Surface.EndsWith("case")),Is.EqualTo(2));Assert.That(objects.Count(s=>s.Surface.EndsWith("bench")),Is.EqualTo(1));Assert.That(objects.Count(s=>s.Surface.EndsWith("cabinet")),Is.EqualTo(1));
                var cases=objects.Where(s=>s.Surface.EndsWith("case")).ToArray();Assert.That(cases[0].Position.x,Is.EqualTo(cases[1].Position.x));Assert.That(Vector3.Distance(cases[0].Position,cases[1].Position),Is.LessThan(2),"one coherent staging bay");
                foreach(var body in objects)
                {
                    var bodyBounds=new Bounds(body.Position,body.Size-Vector3.one*.002f);
                    foreach(var header in headers)
                    {
                        float bottom=header.Position.y-header.Size.y/2,baseY=Mathf.Floor(bottom/WellArenaCatalog.Storey)*WellArenaCatalog.Storey,height=bottom-baseY;
                        bool alongX=header.Size.x>header.Size.z;
                        var clearance=new Bounds(new Vector3(header.Position.x,baseY+height/2,header.Position.z),new Vector3(alongX?header.Size.x+margin:4,height,alongX?4:header.Size.z+margin));
                        Assert.That(bodyBounds.Intersects(clearance),Is.False,body.Id+" crowds doorway "+header.Id);
                    }
                    Assert.That(new Bounds(body.Position,body.Size).min.y,Is.EqualTo(floor*WellArenaCatalog.Storey).Within(.0001f));
                    Assert.That(Mathf.Abs(body.Position.z)>20?Mathf.Abs(body.Position.x)-body.Size.x/2:100,Is.GreaterThan(4),"lift approach stays open");
                }
            }
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
        [TestCase(false)][TestCase(true)] public void PreviousWellCorridorLoadsWithoutChangingSavedHashes(bool compact)
        {
            var path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"well-revision-"+Guid.NewGuid()+".json");
            try
            {
                var current=StarTournament.ProvingGround.Tests.EditMode.DesignLabHistoryTests.Shipped();
                var method=typeof(ProvingProfile).GetMethod("BeforeWellCorridorCover",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                var old=current.Clone();old.Profiles=old.Profiles.Select(p=>(ProvingProfile)method.Invoke(p,null)).ToList();
                var history=new DesignLabHistory(path,old);history.Create("Колодец до коридорных зон");var draft=history.Selected.Snapshot;draft.Set("well.liftSpeed",5);draft.Set("well.idleDepartureSeconds",21);history.Save(draft);
                var prior=history.Selected;string priorHash=prior.Hash;int priorNumber=prior.Number;
                if(!compact)
                {
                    var file=typeof(DesignLabHistory).GetField("data",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(history);
                    System.IO.File.WriteAllText(path,JsonUtility.ToJson(file));
                }
                var migrated=new DesignLabHistory(path,current);Assert.That(migrated.StorageError,Is.Null);
                Assert.That(migrated.Selected.Snapshot.Profiles.Single(p=>p.Id=="well-authoring-v1").Version,Is.EqualTo(5));
                Assert.That(migrated.Selected.Snapshot.Get("well.idleDepartureSeconds"),Is.EqualTo(21));
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
        [TestCase(false)][TestCase(true)] public void PreviousWellWeaponsLoadWithoutChangingSavedHashes(bool compact)
        {
            var path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"well-revision-"+Guid.NewGuid()+".json");
            try
            {
                var current=StarTournament.ProvingGround.Tests.EditMode.DesignLabHistoryTests.Shipped();
                var method=typeof(ProvingProfile).GetMethod("BeforeWellSecondFloorWeapons",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                var old=current.Clone();old.Profiles=old.Profiles.Select(p=>(ProvingProfile)method.Invoke(p,null)).ToList();
                var history=new DesignLabHistory(path,old);history.Create("Колодец до нового оружия");var draft=history.Selected.Snapshot;draft.Set("well.liftSpeed",5);draft.Set("well.idleDepartureSeconds",21);history.Save(draft);
                var prior=history.Selected;string priorHash=prior.Hash;int priorNumber=prior.Number;
                if(!compact)
                {
                    var file=typeof(DesignLabHistory).GetField("data",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(history);
                    System.IO.File.WriteAllText(path,JsonUtility.ToJson(file));
                }
                var migrated=new DesignLabHistory(path,current);Assert.That(migrated.StorageError,Is.Null);
                Assert.That(migrated.Selected.Snapshot.Profiles.Single(p=>p.Id=="well-authoring-v1").Version,Is.EqualTo(5));
                Assert.That(migrated.Selected.Snapshot.Get("well.idleDepartureSeconds"),Is.EqualTo(21));
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
        [TestCase(false)][TestCase(true)] public void PreviousWellArtKeepsSavedValuesOnExpandedGrid(bool compact)
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
                var bytes=System.IO.File.ReadAllBytes(path);
                var migrated=new DesignLabHistory(path,current,persistMigration:false);
                Assert.That(migrated.StorageError,Is.Null);Assert.That(migrated.Writable,Is.True);
                Assert.That(migrated.SelectedProfileId,Is.EqualTo(history.SelectedProfileId));
                var preserved=StarTournament.ProvingGround.Tests.EditMode.DesignLabHistoryTests.ReadFixture(path,old).Profiles.Single(p=>p.Id==history.SelectedProfileId).Revisions.Single(r=>r.Number==priorNumber);
                Assert.That(preserved.Hash,Is.EqualTo(priorHash));Assert.That(preserved.Snapshot.Hash(),Is.EqualTo(priorHash));
                Assert.That(preserved.Snapshot.Get("well.fill"),Is.EqualTo(.8f));
                Assert.That(preserved.Snapshot.Get("well.liftSpeed"),Is.EqualTo(5));Assert.That(preserved.Snapshot.Get("well.lamp"),Is.EqualTo(9));
                Assert.That(migrated.Selected.Snapshot.Get("well.fill"),Is.EqualTo(.8f));
                Assert.That(migrated.Selected.Snapshot.Get("well.liftSpeed"),Is.EqualTo(5));Assert.That(migrated.Selected.Snapshot.Get("well.lamp"),Is.EqualTo(9));
                CollectionAssert.AreEqual(bytes,System.IO.File.ReadAllBytes(path));
                var restarted=new DesignLabHistory(path,current,persistMigration:false);Assert.That(restarted.StorageError,Is.Null);
                Assert.That(restarted.Selected.Hash,Is.EqualTo(migrated.Selected.Hash));
                CollectionAssert.AreEqual(bytes,System.IO.File.ReadAllBytes(path));
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
                Assert.That(migrated.Selected.Snapshot.Get("well.daylight"),Is.EqualTo(.5f));
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
        [TestCase(false)][TestCase(true)] public void PreviousWellIdleProfileLoadsWithoutChangingSavedHashes(bool compact)
        {
            var path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"well-idle-"+Guid.NewGuid()+".json");
            try
            {
                var current=StarTournament.ProvingGround.Tests.EditMode.DesignLabHistoryTests.Shipped();
                var method=typeof(ProvingProfile).GetMethod("BeforeWellIdleDeparture",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
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
                Assert.That(preserved.Snapshot.Get("well.daylight"),Is.EqualTo(.8f));
                Assert.That(preserved.Snapshot.Descriptors.Any(d=>d.Path=="well.idleDepartureSeconds"),Is.False);
                Assert.That(migrated.Selected.Snapshot.Get("well.liftSpeed"),Is.EqualTo(5));
                Assert.That(migrated.Selected.Snapshot.Get("well.fill"),Is.EqualTo(.42f));
                Assert.That(migrated.Selected.Snapshot.Get("well.lamp"),Is.EqualTo(9));
                Assert.That(migrated.Selected.Snapshot.Get("well.plantHeight"),Is.EqualTo(.5f));
                Assert.That(migrated.Selected.Snapshot.Get("well.glassOpacity"),Is.EqualTo(.4f));
                Assert.That(migrated.Selected.Snapshot.Get("well.daylight"),Is.EqualTo(.8f));
                Assert.That(migrated.Selected.Snapshot.Get("well.glassSmoothness"),Is.EqualTo(.85f));
                Assert.That(migrated.Selected.Snapshot.Get("well.idleDepartureSeconds"),Is.EqualTo(15));
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
            Assert.That(lifts.Capture()[1].Height,Is.EqualTo(2*WellArenaCatalog.Storey));lifts.Advance(10,Empty());Assert.That(lifts.Capture()[1].Height,Is.EqualTo(2*WellArenaCatalog.Storey),"empty platform remains docked before idle timeout");
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
        [Test] public void EmptyLiftDepartsAtExactlyFifteenSecondsAndUsesTickRemainder()
        {
            var lifts=Lifts();lifts.Advance(14.5f,Empty());
            Assert.That(lifts.Capture()[1].Phase,Is.EqualTo(WellLiftPhase.Ready));
            Assert.That(lifts.Capture()[1].IdleSeconds,Is.EqualTo(14.5));
            lifts.Advance(.5f,Empty());var state=lifts.Capture()[1];
            Assert.That(state.Phase,Is.EqualTo(WellLiftPhase.Moving));Assert.That(state.Height,Is.Zero);Assert.That(state.Countdown,Is.Zero);Assert.That(state.IdleSeconds,Is.Zero);
            lifts.Advance(.25f,Empty());Assert.That(lifts.Capture()[1].Height,Is.EqualTo(1));
            var spanning=Lifts();spanning.Advance(15.25f,Empty());Assert.That(spanning.Capture()[1].Height,Is.EqualTo(1));
            Assert.That(spanning.Capture()[0].Height,Is.EqualTo(2*WellArenaCatalog.Storey-1));
        }
        [Test] public void EmptyArrivalAndPassengerExitStartFreshIdleWait()
        {
            var lifts=Lifts();lifts.Advance(1,Occupied());lifts.Advance(2*WellArenaCatalog.Storey/4,Empty());
            lifts.Advance(14.5f,Empty());Assert.That(lifts.Capture()[1].Phase,Is.EqualTo(WellLiftPhase.Ready));
            lifts.Advance(.5f,Empty());Assert.That(lifts.Capture()[1].Phase,Is.EqualTo(WellLiftPhase.Moving));Assert.That(lifts.Capture()[1].Target,Is.Zero);
            var passenger=Lifts();passenger.Advance(10,Occupied());passenger.Advance(30,Occupied());
            Assert.That(passenger.Capture()[1].Phase,Is.EqualTo(WellLiftPhase.AwaitEmpty));Assert.That(passenger.Capture()[1].IdleSeconds,Is.Zero);
            passenger.Advance(14.5f,Empty());Assert.That(passenger.Capture()[1].IdleSeconds,Is.EqualTo(14.5));
            passenger.Advance(.5f,Empty());Assert.That(passenger.Capture()[1].Phase,Is.EqualTo(WellLiftPhase.Moving));
        }
        [Test] public void BoardingCancelsIdleAndKeepsOneSecondDelay()
        {
            var lifts=Lifts();lifts.Advance(14.5f,Empty());lifts.Advance(.5f,Occupied());
            Assert.That(lifts.Capture()[1].IdleSeconds,Is.Zero);Assert.That(lifts.Capture()[1].Phase,Is.EqualTo(WellLiftPhase.Boarding));
            Assert.That(lifts.Capture()[1].Countdown,Is.EqualTo(.5));lifts.Advance(.5f,Occupied());Assert.That(lifts.Capture()[1].Height,Is.Zero);
            lifts.Advance(.25f,Occupied());Assert.That(lifts.Capture()[1].Height,Is.EqualTo(1));
        }
        [Test] public void IdleRestoreResetAndLargeTickMatchSmallTicks()
        {
            var lifts=Lifts();lifts.Advance(7.5f,Empty());var restored=Lifts();restored.Restore(JsonUtility.FromJson<Payload>(JsonUtility.ToJson(new Payload{Lifts=lifts.Capture()})).Lifts);
            lifts.Advance(7.75f,Empty());restored.Advance(7.75f,Empty());
            Assert.That(JsonUtility.ToJson(new Payload{Lifts=restored.Capture()}),Is.EqualTo(JsonUtility.ToJson(new Payload{Lifts=lifts.Capture()})));
            restored.Reset();Assert.That(restored.Capture().All(l=>l.IdleSeconds==0&&l.Phase==WellLiftPhase.Ready),Is.True);
            var small=Lifts();var large=Lifts();for(int i=0;i<800;i++)small.Advance(.125f,Empty());large.Advance(100,Empty());
            for(int i=0;i<2;i++)
            {var a=large.Capture()[i];var b=small.Capture()[i];Assert.That(a.Phase,Is.EqualTo(b.Phase));Assert.That(a.Target,Is.EqualTo(b.Target));Assert.That(a.Height,Is.EqualTo(b.Height).Within(.0001f));Assert.That(a.IdleSeconds,Is.EqualTo(b.IdleSeconds).Within(.00001));}
        }
        [Test] public void HistoricalWellProfileUsesDefaultIdleDelayWithoutMutation()
        {
            var method=typeof(ProvingProfile).GetMethod("BeforeWellIdleDeparture",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var old=(ProvingProfile)method.Invoke(ProvingProfile.CreateWellDefault(),null);string before=JsonUtility.ToJson(old);
            var lifts=new WellLiftSystem(WellArenaCatalog.Build().Lifts,old);lifts.Advance(15,Empty());
            Assert.That(lifts.Capture()[1].Phase,Is.EqualTo(WellLiftPhase.Moving));Assert.That(JsonUtility.ToJson(old),Is.EqualTo(before));
        }
        [Test] public void IdleDelayUsesValidatedLabProfileAndRejectsInvalidSnapshotsAtomically()
        {
            var profile=ProvingProfile.CreateWellDefault();var descriptor=profile.Descriptors.Single(d=>d.Path=="well.idleDepartureSeconds");
            Assert.That(descriptor.Group,Is.EqualTo("well-lifts"));profile.Set("well.idleDepartureSeconds",3);
            var lifts=new WellLiftSystem(WellArenaCatalog.Build().Lifts,profile);lifts.Advance(3,Empty());Assert.That(lifts.Capture()[1].Phase,Is.EqualTo(WellLiftPhase.Moving));
            lifts.Reset();string before=JsonUtility.ToJson(new Payload{Lifts=lifts.Capture()});
            foreach(double invalid in new[]{double.NaN,double.PositiveInfinity,-1,3})
            {var snapshot=lifts.Capture();snapshot[1].IdleSeconds=invalid;Assert.Throws<ArgumentException>(()=>lifts.Restore(snapshot));Assert.That(JsonUtility.ToJson(new Payload{Lifts=lifts.Capture()}),Is.EqualTo(before));}
            var moving=lifts.Capture();moving[1].Phase=WellLiftPhase.Moving;moving[1].Target=2*WellArenaCatalog.Storey;moving[1].IdleSeconds=1;
            Assert.Throws<ArgumentException>(()=>lifts.Restore(moving));
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
