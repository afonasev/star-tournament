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
            Assert.That(AuthoredArenaCatalog.Name(d.MapId),Is.EqualTo("Колодец"));Assert.That(AuthoredArenaCatalog.Maximum(d.MapId),Is.EqualTo(8));Assert.That(d.Spawns.Length,Is.EqualTo(8));Assert.That(d.Pickups.Length,Is.EqualTo(12));
            foreach(var kind in new[]{ArenaPickupKind.Armor,ArenaPickupKind.Speed,ArenaPickupKind.Shotgun,ArenaPickupKind.Pulse,ArenaPickupKind.Cutter})Assert.That(d.Pickups.Count(p=>p.Kind==kind),Is.EqualTo(2));
            Assert.That(d.DamagePickupAnchor.y,Is.EqualTo(5));Assert.That(d.DamagePickupAnchor.z,Is.GreaterThan(0));Assert.That(d.Pickups.Single(p=>p.Kind==ArenaPickupKind.FullHeal).Anchor,Is.EqualTo(new Vector3(0,10,0)));
            Assert.That(d.Lifts.Single(x=>x.Id=="north").InitialHeight,Is.EqualTo(10));Assert.That(d.Lifts.Single(x=>x.Id=="south").InitialHeight,Is.EqualTo(0));
            foreach(var lift in d.Lifts)Assert.That(Mathf.Abs(lift.Center.z)-lift.Size.z/2,Is.GreaterThan(WellArenaCatalog.Edge));
            Assert.That(d.Transitions.Length,Is.EqualTo(8));Assert.That(d.Solids.Where(x=>x.Surface=="well-grate").All(x=>x.Layer==ProvingArena.MovementOnlyLayer),Is.True);
            var profile=WellArenaCatalog.AuthoringProfile();Assert.That(profile.Validate(),Is.Empty);Assert.That(ProvingProfile.CreateWellDefault().Validate(),Is.Empty);
            Assert.That(JsonUtility.ToJson(AuthoredArenaCatalog.Freeze(d.MapId,ProvingProfile.CreateDefault()).Definition),Is.EqualTo(JsonUtility.ToJson(d)));
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
        WellLiftSystem Lifts()=>new WellLiftSystem(WellArenaCatalog.Build().Lifts,ProvingProfile.CreateWellDefault());
        int[][] Occupied()=>new[]{Array.Empty<int>(),new[]{0}};
        int[][] Empty()=>new[]{Array.Empty<int>(),Array.Empty<int>()};
        [Test] public void ExactDelayEmptyTripTerminalStopAndReboarding()
        {
            var lifts=Lifts();lifts.Advance(.5f,Occupied());Assert.That(lifts.Capture()[1].Height,Is.EqualTo(0));Assert.That(lifts.Capture()[1].Countdown,Is.EqualTo(.5));
            lifts.Advance(.5f,Empty());Assert.That(lifts.Capture()[1].Height,Is.EqualTo(0));Assert.That(lifts.Capture()[1].Phase,Is.EqualTo(WellLiftPhase.Moving));
            lifts.Advance(1.25f,Empty());Assert.That(lifts.Capture()[1].Height,Is.EqualTo(5));
            var copy=lifts.Capture();var restored=Lifts();restored.Restore(JsonUtility.FromJson<Payload>(JsonUtility.ToJson(new Payload{Lifts=copy})).Lifts);
            lifts.Advance(1.25f,Empty());restored.Advance(1.25f,Empty());Assert.That(JsonUtility.ToJson(new Payload{Lifts=lifts.Capture()}),Is.EqualTo(JsonUtility.ToJson(new Payload{Lifts=restored.Capture()})));
            Assert.That(lifts.Capture()[1].Height,Is.EqualTo(10));lifts.Advance(10,Empty());Assert.That(lifts.Capture()[1].Height,Is.EqualTo(10),"empty platform stops until a new boarding");
            lifts.Advance(.1f,Empty());lifts.Advance(.5f,Occupied());Assert.That(lifts.Capture()[1].Target,Is.EqualTo(0));Assert.That(lifts.Capture()[1].Countdown,Is.EqualTo(.5));
        }
        [Test] public void PassengerStayingAtTerminalCannotRearmAndEmptyArrivalAllowsNewBoarding()
        {
            var lifts=Lifts();lifts.Advance(10,Occupied());Assert.That(lifts.Capture()[1].Height,Is.EqualTo(10));Assert.That(lifts.Capture()[1].Phase,Is.EqualTo(WellLiftPhase.AwaitEmpty));
            lifts.Advance(10,Occupied());Assert.That(lifts.Capture()[1].Height,Is.EqualTo(10),"remaining passenger must not rearm");
            lifts.Advance(.1f,Empty());lifts.Advance(.5f,Occupied());Assert.That(lifts.Capture()[1].Countdown,Is.EqualTo(.5));
            var emptyTrip=Lifts();emptyTrip.Advance(1,Occupied());emptyTrip.Advance(3,Empty());Assert.That(emptyTrip.Capture()[1].Phase,Is.EqualTo(WellLiftPhase.Ready));
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
