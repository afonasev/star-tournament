using System;
using System.Linq;
using System.IO;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class FiringRangeTests
    {
        [Test]public void FiringRangeAuthoringUpgradePreservesPreviousHashAndValues()
        {
            string directory=Path.Combine(Path.GetTempPath(),"st-range-lab-"+Guid.NewGuid());Directory.CreateDirectory(directory);string path=Path.Combine(directory,"history.json");
            try
            {
            var old=DesignLabHistoryTests.Shipped();var history=new DesignLabHistory(path,old);history.Create("До полигона");
            var draft=history.Selected.Snapshot;draft.Set("combat.maximumHealth",150);history.Save(draft);var prior=history.Selected;
            string hash=prior.Hash;var shipped=DesignLabHistoryTests.Shipped();shipped.Profiles.Add(FiringRangeCatalog.AuthoringProfile());
            var upgraded=new DesignLabHistory(path,shipped);Assert.That(upgraded.StorageError,Is.Null);
            Assert.That(upgraded.Selected.Snapshot.Get("combat.maximumHealth"),Is.EqualTo(150));
            Assert.That(upgraded.SelectedProfile.Revisions.Single(r=>r.Number==prior.Number).Hash,Is.EqualTo(hash));
            Assert.That(upgraded.Selected.Snapshot.Get("range.map.targetYaw"),Is.EqualTo(180));
            }finally{Directory.Delete(directory,true);}
        }
        [Test] public void FrozenHallHasAllBonusesAndIndependentTargets()
        {
            var d=AuthoredArenaCatalog.Resolve(FiringRangeCatalog.Id);var movement=ProvingProfile.CreateDefault();
            Assert.That(ArenaDefinitionValidator.Validate(d,movement).IsValid,Is.True,ArenaDefinitionValidator.Validate(d,movement).ToString());
            Assert.That(d.TrainingTargets.Length,Is.EqualTo(12));Assert.That(d.Spawns.Length,Is.EqualTo(4));
            Assert.That(d.Pickups.Select(p=>p.Kind),Is.EquivalentTo(Enum.GetValues(typeof(ArenaPickupKind))));
            Assert.That(d.Pickups.All(p=>p.Anchor.x==-16),Is.True);
            var frozen=ArenaFreezeSnapshot.Create(d,movement);d.TrainingTargets[0]=Vector3.one;
            Assert.That(frozen.Definition.TrainingTargets[0],Is.Not.EqualTo(Vector3.one));
            Assert.Throws<ArgumentException>(()=>AuthoredArenaCatalog.Resolve(FiringRangeCatalog.Id,"unknown"));
        }
        [TestCase(1)][TestCase(4)] public void HumansAndTargetsUseSeparateSeats(int humans)
        {
            var c=FiringRangeCatalog.Composition(humans);Assert.That(c.LocalCount,Is.EqualTo(humans));
            Assert.That(c.ParticipantCount,Is.EqualTo(humans+12));Assert.That(c.Roster.Mode,Is.EqualTo(NativeMatchMode.Training));
            c.RequireSupportedSources(false,false);
            for(int p=humans;p<c.ParticipantCount;p++){Assert.That(c.SeatOf(p),Is.EqualTo(-1));Assert.That(c.Participant(p).Kind,Is.EqualTo(NativeParticipantKind.TrainingDummy));}
            Assert.That(JsonUtility.ToJson(NativeMatchComposition.Restore(c.Read()).Read()),Is.EqualTo(JsonUtility.ToJson(c.Read())));
            Assert.That(AuthoredArenaCatalog.Supports(FiringRangeCatalog.Id,humans),Is.True);
        }
        [Test] public void CompetitiveLimitsAndTargetAdmissionStayStrict()
        {
            Assert.Throws<ArgumentOutOfRangeException>(()=>NativeMatchRoster.Ffa(1));Assert.Throws<ArgumentOutOfRangeException>(()=>NativeMatchRoster.Ffa(9));
            Assert.That(AuthoredArenaCatalog.Supports(CombatBowlCatalog.Id,1),Is.False);Assert.That(AuthoredArenaCatalog.Supports(FiringRangeCatalog.Id,5),Is.False);
            var t=FiringRangeCatalog.Composition(1).Read();t.Participants[1].Kind=NativeParticipantKind.Bot;t.Participants[1].Difficulty=1;
            Assert.Throws<ArgumentException>(()=>NativeMatchComposition.Restore(t));
        }
        [Test] public void GeometryAndTargetsHaveAuthoringMetadata()
        {
            var p=FiringRangeCatalog.AuthoringProfile();Assert.That(p.Validate(),Is.Empty);
            Assert.That(p.Descriptors.Any(d=>d.Path=="range.map.target-11.z"),Is.True);
            var b=new LabBundle{Profiles={p}};
            foreach(var d in p.Descriptors){Assert.That(d.Description,Is.Not.Empty);Assert.That(b.IsAuthoring(d.Path),Is.True);Assert.That(d.Maximum,Is.GreaterThan(d.Minimum));}
        }
    }
}
