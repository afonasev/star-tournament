using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class RocketCombatTests
    {
        [Test] public void ThirdSlotCyclesIndependentlyNeedsReleaseAndRestoresExactly()
        {
            var p=ProvingProfile.CreateCombatDefault();var life=new CombatLife("p",p);
            life.CollectWeapon(WeaponId.RocketLauncher);life.CollectWeapon(WeaponId.Cutter);life.CollectWeapon(WeaponId.Shotgun);life.Select(WeaponSelection.RocketLauncher);life.Advance(1);
            Assert.That(life.Read().SelectedWeapon,Is.EqualTo(WeaponId.RocketLauncher));
            Assert.That(life.Read().Ammo,Is.EqualTo(20));Assert.That(life.Fire(true),Is.True);
            life.Advance(p.Get("rocket.cooldownSeconds"));Assert.That(life.Fire(true),Is.False,"Holding does not auto-fire Pulse");
            life.Fire(false);Assert.That(life.Fire(true),Is.True);
            var snapshot=life.Read();life.Select(WeaponSelection.Next);life.Advance(1);
            Assert.That(life.Read().SelectedWeapon,Is.EqualTo(WeaponId.Cutter));
            life.Select(WeaponSelection.Next);life.Advance(1);
            Assert.That(life.Read().SelectedWeapon,Is.EqualTo(WeaponId.Rifle));
            Assert.That(life.Read().RifleAmmo,Is.EqualTo(200));Assert.That(life.Read().ShotgunAmmo,Is.EqualTo(20));
            life.Restore(snapshot);Assert.That(life.Read().RocketAmmo,Is.EqualTo(18));
            life.Damage(1,100);life.Advance(p.Get("combat.killcamSeconds"));Assert.That(life.Respawn(1),Is.True);
            Assert.That(life.Read().RocketAmmo,Is.Zero);Assert.That(life.Read().SelectedWeapon,Is.EqualTo(WeaponId.Rifle));
        }
        [Test] public void DamageUsesUniformBoostAndArmorPrecedesHealth()
        {
            var resolver=new RocketResolver(ProvingProfile.CreateNativeCombatDefault(),ProvingProfile.CreateDefault());
            Assert.That(resolver.Damage(0,true,1.5f),Is.EqualTo(127.5f));
            Assert.That(resolver.Damage(2,false,1),Is.EqualTo(42.5f));
            Assert.That(resolver.Damage(2,false,1.5f),Is.EqualTo(63.75f));
            Assert.That(resolver.Damage(4,false,2),Is.Zero);Assert.That(resolver.Damage(5,false,1),Is.Zero);
            var life=new CombatLife("p",ProvingProfile.CreateCombatDefault());life.GrantArmor(50);
            life.Damage(1,resolver.Damage(0,true,1));Assert.That(life.Read().Armor,Is.Zero);Assert.That(life.Read().Health,Is.EqualTo(65));Assert.That(life.Read().Dead,Is.False);
        }
        [Test] public void SegmentHitsNearestCapsuleAndWallWinsTieAndOwnerRespawnIsDistinct()
        {
            var resolver=new RocketResolver(ProvingProfile.CreateNativeCombatDefault(),ProvingProfile.CreateDefault());
            var targets=new[]{new CombatTarget(0,1,new ParticipantState()),new CombatTarget(1,1,new ParticipantState{Position=Vector3.forward*5}),new CombatTarget(2,1,new ParticipantState{Position=Vector3.forward*10})};
            var hit=resolver.Contact(Vector3.up,Vector3.forward,100,targets,0,1);
            Assert.That(hit.TargetIndex,Is.EqualTo(1));Assert.That(hit.Distance,Is.EqualTo(4.45f).Within(.001));
            Assert.That(resolver.Contact(Vector3.up,Vector3.forward,hit.Distance,targets,0,1).TargetIndex,Is.EqualTo(-1));
            targets[0]=new CombatTarget(0,2,new ParticipantState());
            Assert.That(resolver.Contact(Vector3.up,Vector3.forward,100,targets,0,1).TargetIndex,Is.EqualTo(0));
            Assert.That(resolver.ClosestPoint(new Vector3(2,1,0),Vector3.zero).x,Is.EqualTo(.55f).Within(.001));
        }
        [Test] public void AllRocketNumbersHaveValidatedMetadataAndNoSplitDamage()
        {
            foreach(var p in new[]{ProvingProfile.CreateDefault(),ProvingProfile.CreateNativeCombatDefault(),ProvingProfile.CreateCombatDefault()})
            {
                Assert.That(p.Validate(),Is.Empty);
                foreach(var d in p.Descriptors)if(d.Path.Contains("rocket"))
                { Assert.That(d.Validate(out _),Is.True);p.Set(d.Path,d.Maximum+d.Step);Assert.That(p.Validate(),Is.Not.Empty);p.Set(d.Path,d.DefaultValue); }
            }
            var c=ProvingProfile.CreateNativeCombatDefault();Assert.Throws<System.Collections.Generic.KeyNotFoundException>(()=>c.Get("rocket.directDamage"));
            var r=new RocketResolver(c,ProvingProfile.CreateDefault());
            Assert.Throws<ArgumentException>(()=>r.Validate(new RocketState{Id=1,OwnerLife=1,Direction=new Vector3(float.NaN,0,1),DamageMultiplier=1},1,1));
        }
        [TestCase("BeforePulse")][TestCase("BeforeFullHeal")]
        public void LabUpgradeAppendsPulseRevisionAndPreservesEveryHistoricalHashAndValue(string predecessor)
        {
            var bundle=new LabBundle{Profiles=new System.Collections.Generic.List<ProvingProfile>{ProvingProfile.CreateDefault(),ProvingProfile.CreateCombatDefault(),ProvingProfile.CreateNativeCombatDefault()}};
            var legacy=new LabBundle{Profiles=bundle.Profiles.Select(p=>(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeWeaponRebalance",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,null)).Select(p=>(ProvingProfile)typeof(ProvingProfile).GetMethod(predecessor,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,null)).ToList()};
            string path=Path.Combine(Path.GetTempPath(),"pulse-history-"+Guid.NewGuid()+".json");
            try
            {
                var old=new DesignLabHistory(path,legacy);old.Create("Мой профиль");var draft=old.Selected.Snapshot;draft.Set("rifle.damage",31);old.Save(draft);
                var before=old.Selected;string id=old.SelectedProfileId;
                var upgraded=new DesignLabHistory(path,bundle);Assert.That(upgraded.StorageError,Is.Null);
                Assert.That(upgraded.SelectedProfileId,Is.EqualTo(id));Assert.That(upgraded.Selected.Number,Is.GreaterThan(before.Number));
                Assert.That(upgraded.Selected.Snapshot.Get("rocket.maximumDamage"),Is.EqualTo(85));Assert.That(upgraded.Selected.Snapshot.Get("rifle.damage"),Is.EqualTo(31));
                Assert.That(upgraded.SelectedProfile.Revisions.Single(r=>r.Number==before.Number).Hash,Is.EqualTo(before.Hash));
                int count=upgraded.SelectedProfile.Revisions.Count;var reopened=new DesignLabHistory(path,bundle);Assert.That(reopened.StorageError,Is.Null);Assert.That(reopened.SelectedProfile.Revisions.Count,Is.EqualTo(count));
                Assert.Throws<InvalidOperationException>(()=>reopened.Select(id,before.Number));
            }
            finally {if(File.Exists(path))File.Delete(path);}
        }
    }
}
