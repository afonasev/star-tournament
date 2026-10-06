using System;
using NUnit.Framework;
using UnityEngine;

namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class CombatLifeTests
    {
        [Test] public void BothWeaponsStopAtZeroUntilRespawnAndShotgunNeedsANewEdge()
        {
            var profile = ProvingProfile.CreateCombatDefault();
            profile.Set("combat.startingAmmo", 1);
            profile.Set("rifle.startingAmmo", 1);
            var life = new CombatLife("p1", profile);
            Assert.That(life.Fire(true), Is.True);
            Assert.That(life.Read().RifleAmmo, Is.Zero);
            life.Advance(10);
            Assert.That(life.Fire(true), Is.False);
            life.CollectWeapon(WeaponId.Shotgun);life.Select(WeaponSelection.Shotgun);
            Assert.That(life.Fire(true),Is.False,"Holding the rifle trigger does not edge-fire the shotgun");
            life.Advance(profile.Get("weapon.switchSeconds"));
            Assert.That(life.Fire(false), Is.False);
            Assert.That(life.Fire(true), Is.True);
            Assert.That(life.Read().ShotgunAmmo,Is.Zero);
            life.Advance(10);life.Fire(false);
            Assert.That(life.Fire(true),Is.False);
            life.Damage(1,100);life.Advance(profile.Get("combat.killcamSeconds"));life.Respawn(1);
            Assert.That(life.Read().SelectedWeapon,Is.EqualTo(WeaponId.Rifle));
            Assert.That(life.Read().RifleAmmo,Is.EqualTo(1));
            Assert.That(life.Read().ShotgunAmmo,Is.Zero);
        }
        [Test] public void CooldownBoundaryAndExplicitInputReset()
        {
            var profile = ProvingProfile.CreateCombatDefault();
            var life = new CombatLife("p1", profile);
            life.Fire(true);
            var cooldown = life.Read().CooldownRemaining;
            life.Advance(cooldown / 2);
            life.ClearHeldInput();
            Assert.That(life.Fire(true), Is.False);
            life.Advance(cooldown / 2);
            life.ClearHeldInput();
            Assert.That(life.Fire(true), Is.True);
        }
        [Test] public void SlotsKeepIndependentAmmoAndCooldownAcrossSelectionAndSnapshot()
        {
            var profile=ProvingProfile.CreateCombatDefault();var life=new CombatLife("p1",profile);
            Assert.That(life.Read().SelectedWeapon,Is.EqualTo(WeaponId.Rifle));
            Assert.That(life.Fire(true),Is.True);
            life.CollectWeapon(WeaponId.Shotgun);life.Select(WeaponSelection.Next);
            Assert.That(life.Read().SelectedWeapon,Is.EqualTo(WeaponId.Rifle));
            Assert.That(life.Read().SwitchRemaining,Is.EqualTo(.5).Within(.00001));
            Assert.That(life.Fire(true),Is.False);
            life.Advance(profile.Get("weapon.switchSeconds"));
            Assert.That(life.Read().SelectedWeapon,Is.EqualTo(WeaponId.Shotgun));
            Assert.That(life.Read().CooldownRemaining,Is.Zero);
            Assert.That(life.Fire(true),Is.False);
            life.Fire(false);Assert.That(life.Fire(true),Is.True);
            var snapshot=JsonUtility.FromJson<CombatLifeState>(JsonUtility.ToJson(life.Read()));
            life.Select(WeaponSelection.Previous);
            Assert.That(life.Read().PendingWeapon,Is.EqualTo(WeaponId.Rifle));
            Assert.That(life.Read().SelectedWeapon,Is.EqualTo(WeaponId.Shotgun));
            life.Advance(profile.Get("weapon.switchSeconds"));
            Assert.That(life.Read().RifleAmmo,Is.EqualTo(profile.Get("rifle.startingAmmo")-1));
            Assert.That(life.Read().CooldownRemaining,Is.Zero);
            life.Restore(snapshot);
            Assert.That(life.Read().SelectedWeapon,Is.EqualTo(WeaponId.Shotgun));
            Assert.That(life.Read().ShotgunAmmo,Is.EqualTo(profile.Get("combat.startingAmmo")-1));
        }
        [Test] public void SwitchingWaitsForSimulationTimeSurvivesRestoreAndDeathCancelsIt()
        {
            var profile=ProvingProfile.CreateCombatDefault();
            var life=new CombatLife("p1",profile);
            life.CollectWeapon(WeaponId.Shotgun);life.Select(WeaponSelection.Shotgun);
            life.Select(WeaponSelection.Rifle);
            Assert.That(life.Read().PendingWeapon,Is.EqualTo(WeaponId.Shotgun),"Second command cannot replace the pending switch");
            Assert.That(life.Fire(true),Is.False);
            life.Advance(.2);
            var snapshot=JsonUtility.FromJson<CombatLifeState>(JsonUtility.ToJson(life.Read()));
            Assert.That(snapshot.SwitchRemaining,Is.EqualTo(.3).Within(.00001));
            life.Advance(.3);
            Assert.That(life.Read().SelectedWeapon,Is.EqualTo(WeaponId.Shotgun));
            life.Restore(snapshot);
            Assert.That(life.Read().SelectedWeapon,Is.EqualTo(WeaponId.Rifle));
            Assert.That(life.Read().PendingWeapon,Is.EqualTo(WeaponId.Shotgun));
            life.Advance(.3);
            Assert.That(life.Read().SelectedWeapon,Is.EqualTo(WeaponId.Shotgun));
            life.Select(WeaponSelection.Rifle);
            life.Damage(1,100);
            Assert.That(life.Read().SwitchRemaining,Is.Zero);
            Assert.That(life.Read().PendingWeapon,Is.EqualTo(default(WeaponId)));
            life.Advance(profile.Get("combat.killcamSeconds"));life.Respawn(1);
            Assert.That(life.Read().SelectedWeapon,Is.EqualTo(WeaponId.Rifle));
        }
        [Test] public void DeathIsOnceAndRespawnRejectsHitsForOldLife()
        {
            var profile = ProvingProfile.CreateCombatDefault();
            var life = new CombatLife("p1", profile);
            var first = life.Damage(1, 30, "p2", 7);
            Assert.That(first.Applied, Is.EqualTo(30)); Assert.That(first.Killed, Is.False);
            var death = life.Damage(1, 500, "p2", 7);
            Assert.That(death.Applied, Is.EqualTo(70)); Assert.That(death.Killed, Is.True);
            Assert.That(life.Read().Health, Is.Zero);
            Assert.That(life.Read().KillerLife, Is.EqualTo(7));
            Assert.That(life.Damage(1, 50).Killed, Is.False);
            Assert.That(life.Fire(true), Is.False);
            Assert.That(life.Respawn(1), Is.False);
            life.Advance(profile.Get("combat.killcamSeconds"));
            Assert.That(life.ReadyToRespawn, Is.True);
            Assert.That(life.Respawn(9), Is.False);
            Assert.That(life.Respawn(1), Is.True);
            Assert.That(life.Read().Life, Is.EqualTo(2));
            Assert.That(life.Read().Health, Is.EqualTo(profile.Get("combat.maximumHealth")));
            Assert.That(life.Read().Ammo, Is.EqualTo(profile.Get("rifle.startingAmmo")));
            Assert.That(life.Read().ShotgunAmmo,Is.Zero);
            Assert.That(life.Read().KillerId, Is.Null);
            Assert.That(life.Read().FireHeld, Is.False);
            Assert.That(life.Damage(1, 1000).Applied, Is.Zero);
            Assert.That(life.Respawn(2), Is.False);
        }
        [Test] public void InvalidDamageAndTimeDoNotMutateState()
        {
            var life = new CombatLife("p1", ProvingProfile.CreateCombatDefault());
            var before = JsonUtility.ToJson(life.Read());
            foreach(var value in new[]{-1f, float.NaN, float.PositiveInfinity})
                Assert.Throws<ArgumentOutOfRangeException>(() => life.Damage(1, value));
            Assert.Throws<ArgumentException>(() => life.Damage(1, 100, "p2", 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => life.Advance(double.NaN));
            Assert.That(JsonUtility.ToJson(life.Read()), Is.EqualTo(before));
        }
        [Test] public void ProfileValidationUsesAmmoMetadataAndRuntimeFreezesTuning()
        {
            var profile = ProvingProfile.CreateCombatDefault();
            Assert.That(profile.Validate(), Is.Empty);
            var life = new CombatLife("p1", profile);
            profile.Set("combat.maximumHealth", 200);
            Assert.That(life.Read().Health, Is.EqualTo(100));
            profile.Set("combat.startingAmmo", 1.5f);
            Assert.That(profile.Validate(), Is.Not.Empty);
            Assert.Throws<ArgumentException>(() => new CombatLife("p2", profile));
            Assert.Throws<ArgumentException>(() => new CombatLife("p2", ProvingProfile.CreateDefault()));
        }
        [Test] public void SnapshotsAreIndependentAndJsonPreservesTransitions()
        {
            var life = new CombatLife("p1", ProvingProfile.CreateCombatDefault());
            life.Fire(true); life.Advance(.2);
            var copy = life.Read(); copy.Health = 0;
            Assert.That(life.Read().Health, Is.EqualTo(100));
            var restored = JsonUtility.FromJson<CombatLifeState>(JsonUtility.ToJson(life.Read()));
            Assert.That(restored.FireHeld, Is.True);
            Assert.That(restored.CooldownRemaining, Is.EqualTo(life.Read().CooldownRemaining));
            life.Damage(1, 100, "p2", 3);
            restored = JsonUtility.FromJson<CombatLifeState>(JsonUtility.ToJson(life.Read()));
            Assert.That(restored.Dead, Is.True);
            Assert.That(restored.KillerId, Is.EqualTo("p2"));
            Assert.That(restored.KillerLife, Is.EqualTo(3));
            Assert.That(restored.RespawnRemaining, Is.EqualTo(life.Read().RespawnRemaining));
        }
        [Test] public void ZeroDelayIsReadyImmediatelyAndNoTickMeansNoTimePasses()
        {
            var profile = ProvingProfile.CreateCombatDefault();
            profile.Set("combat.killcamSeconds", 0);
            var life = new CombatLife("p1", profile);
            life.Damage(1, 100);
            Assert.That(life.ReadyToRespawn, Is.True);
            life.Respawn(1); life.Fire(true);
            var remaining = life.Read().CooldownRemaining;
            life.Advance(0);
            Assert.That(life.Read().CooldownRemaining, Is.EqualTo(remaining));
        }
        [Test] public void ArmorAbsorbsDamageBeforeHealthAndResetsOnRespawn()
        {
            var profile=ProvingProfile.CreateCombatDefault();var life=new CombatLife("p1",profile);
            life.GrantArmor(50);Assert.That(life.Damage(1,40).Applied,Is.EqualTo(40));Assert.That(life.Read().Armor,Is.EqualTo(10));Assert.That(life.Read().Health,Is.EqualTo(100));
            Assert.That(life.Damage(1,50).Applied,Is.EqualTo(50));Assert.That(life.Read().Armor,Is.Zero);Assert.That(life.Read().Health,Is.EqualTo(60));
            life.GrantArmor(50);life.Damage(1,200);life.Advance(profile.Get("combat.killcamSeconds"));life.Respawn(1);Assert.That(life.Read().Armor,Is.Zero);
        }
    }
}
