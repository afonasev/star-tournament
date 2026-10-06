using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class FullHealPickupTests
    {
        [TestCase(1)] [TestCase(50)] [TestCase(99)] [TestCase(100)]
        public void HealIsInstantPreservesArmorAndConsumesAtFullHealth(float health)
        {
            var p=ProvingProfile.CreateCombatDefault();var life=new CombatLife("p",p);
            life.Damage(1,100-health,null,0);life.GrantArmor(35);
            var pickup=new FullHealPickup(Vector3.zero,p,"heal-basement");pickup.Advance(20);
            Assert.That(pickup.TryCollect(Vector3.zero,life),Is.True);
            Assert.That(life.Read().Health,Is.EqualTo(100));Assert.That(life.Read().Armor,Is.EqualTo(35));
            Assert.That(life.Read().Life,Is.EqualTo(1));Assert.That(life.Read().KillerId,Is.Null);
            Assert.That(pickup.Read().Available,Is.False);Assert.That(pickup.Read().Remaining,Is.EqualTo(30));
        }
        [Test] public void DeadAndOtherFloorCannotCollect()
        {
            var p=ProvingProfile.CreateCombatDefault();var life=new CombatLife("p",p);var pickup=new FullHealPickup(new Vector3(0,-1.2f,0),p);pickup.Advance(20);
            Assert.That(pickup.TryCollect(new Vector3(0,4,0),life),Is.False);
            life.Damage(1,100,null,0);Assert.That(pickup.TryCollect(new Vector3(0,-1.2f,0),life),Is.False);
            Assert.That(pickup.Read().Available,Is.True);Assert.That(life.Read().Dead,Is.True);
        }
        [Test] public void TimersSnapshotAndRepeatKeepExactBoundaries()
        {
            var p=ProvingProfile.CreateCombatDefault();var pickup=new FullHealPickup(Vector3.zero,p,"heart");pickup.Advance(19);
            var preSpawn=JsonUtility.FromJson<FullHealPickupState>(JsonUtility.ToJson(pickup.Read()));Assert.That(preSpawn.Available,Is.False);
            pickup.Advance(1);Assert.That(pickup.Read().Available,Is.True);pickup.TryCollect(Vector3.zero,new CombatLife("p",p));pickup.Advance(29);
            var copy=new FullHealPickup(Vector3.zero,p,"heart");copy.Restore(JsonUtility.FromJson<FullHealPickupState>(JsonUtility.ToJson(pickup.Read())));
            Assert.That(copy.Read().Available,Is.False);copy.Advance(1);Assert.That(copy.Read().Available,Is.True);
            copy.Restore(preSpawn);copy.Advance(1);Assert.That(copy.Read().Available,Is.True);
            var repeat=new FullHealPickup(Vector3.zero,p,"heart");Assert.That(repeat.Read().Remaining,Is.EqualTo(20));
            var invalid=copy.Read();invalid.InstanceId="other";Assert.Throws<ArgumentException>(()=>copy.Restore(invalid));
        }
        [TestCase(200,150,100,150)] [TestCase(60,1,100,60)]
        public void TargetIsCappedAndNeverDamages(float maximum,float current,float target,float expected)
        {
            var p=ProvingProfile.CreateCombatDefault();p.Set("combat.maximumHealth",maximum);p.Set("heal.targetHealth",target);
            var life=new CombatLife("p",p);life.Damage(1,maximum-current,null,0);
            var pickup=new FullHealPickup(Vector3.zero,p);pickup.Advance(20);pickup.TryCollect(Vector3.zero,life);Assert.That(life.Read().Health,Is.EqualTo(expected));
        }
        [Test] public void MigrationHasAllDescriptorsAndRejectsInvalidRanges()
        {
            var life=ProvingProfile.CreateCombatDefault();var presentation=ProvingProfile.CreateDefault();life.EnsureCombatDescriptors();presentation.EnsureDefaultDescriptors();
            Assert.That(life.Version,Is.EqualTo(7));Assert.That(presentation.Version,Is.EqualTo(ProvingProfile.DefaultVersion));
            foreach(var pair in new[]{(life,"heal.targetHealth"),(life,"heal.initialDelaySeconds"),(life,"heal.respawnSeconds"),(life,"heal.pickupRadius"),(presentation,"presentation.healPickupHoverHeight"),(presentation,"presentation.healPickupRotationDegreesPerSecond"),(presentation,"presentation.healPickupScale")})
            {
                var d=pair.Item1.Descriptor(pair.Item2);Assert.That(d,Is.Not.Null);Assert.That(d.Group,Is.EqualTo("full-heal"));Assert.That(d.Label,Is.Not.Empty);Assert.That(d.Description,Is.Not.Empty);Assert.That(d.Unit,Is.Not.Empty);
                Assert.That(pair.Item1.Descriptors.Count(x=>x.Path==pair.Item2),Is.EqualTo(1));
                var original=pair.Item1.Get(pair.Item2);pair.Item1.Set(pair.Item2,-1);Assert.That(pair.Item1.Validate().Any(x=>x.Path==pair.Item2),Is.True);pair.Item1.Set(pair.Item2,original);
            }
            Assert.That(life.Validate(),Is.Empty);Assert.That(presentation.Validate(),Is.Empty);
        }
        [Test] public void SavedHealAndPresentationRevisionRestartsWithoutChangingEarlierHash()
        {
            var directory=Path.Combine(Path.GetTempPath(),"full-heal-profile-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            try
            {
                var bundle=new LabBundle{Profiles=new List<ProvingProfile>{ProvingProfile.CreateDefault(),ProvingProfile.CreateCombatDefault(),ProvingProfile.CreateNativeCombatDefault()}};
                var path=Path.Combine(directory,"history.json");var history=new DesignLabHistory(path,bundle);history.Create("Heart tuning");var original=history.Selected;
                var draft=original.Snapshot.Clone();draft.Set("heal.targetHealth",80);draft.Set("heal.initialDelaySeconds",12);draft.Set("heal.respawnSeconds",9);draft.Set("heal.pickupRadius",1.5f);
                draft.Set("presentation.healPickupScale",.8f);draft.Set("presentation.healPickupHoverHeight",.9f);draft.Set("presentation.healPickupRotationDegreesPerSecond",100);
                history.Save(draft);var loaded=new DesignLabHistory(path,bundle);
                Assert.That(loaded.StorageError,Is.Null);Assert.That(loaded.Selected.Number,Is.EqualTo(2));Assert.That(loaded.Selected.Hash,Is.EqualTo(draft.Hash()));
                Assert.That(loaded.Selected.Snapshot.Get("heal.targetHealth"),Is.EqualTo(80));Assert.That(loaded.Selected.Snapshot.Get("presentation.healPickupScale"),Is.EqualTo(.8f));
                Assert.That(loaded.SelectedProfile.Revisions[0].Hash,Is.EqualTo(original.Hash));Assert.That(loaded.SelectedProfile.Revisions[0].Snapshot.Get("heal.targetHealth"),Is.EqualTo(100));
                var lifecycle=loaded.Selected.Snapshot.Profiles.Single(p=>p.Id=="unity-combat-state-v1");var pickup=new FullHealPickup(Vector3.zero,lifecycle);var life=new CombatLife("p",lifecycle);life.Damage(1,50,null,0);pickup.Advance(12);pickup.TryCollect(Vector3.zero,life);
                Assert.That(life.Read().Health,Is.EqualTo(80));Assert.That(pickup.Read().Remaining,Is.EqualTo(9));
            }
            finally{Directory.Delete(directory,true);}
        }
    }
}
