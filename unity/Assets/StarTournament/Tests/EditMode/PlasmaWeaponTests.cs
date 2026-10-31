using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class PlasmaWeaponTests
    {
        [Test] public void SustainedPlasmaHasTwentyPercentMoreShotsAndSnapshotKeepsPhase()
        {
            var life=new CombatLife("p",ProvingProfile.CreateCombatDefault());life.CollectWeapon(WeaponId.Plasma);life.Select(WeaponSelection.Plasma);life.Advance(1);
            Assert.That(life.Fire(true),Is.True);int shots=1;
            for(int tick=0;tick<750;tick++){life.Advance(.02f);if(life.Fire(true))shots++;}
            Assert.That(shots,Is.EqualTo(91),"90 intervals in 15s = 6/s, versus previous 5/s");
            // The first late tick carries phase into the next cooldown; JSON restore is exact.
            life.Advance(.02f);var saved=life.Read();
            var restored=new CombatLife("p",ProvingProfile.CreateCombatDefault());restored.Restore(JsonUtility.FromJson<CombatLifeState>(JsonUtility.ToJson(saved)));
            for(int i=0;i<30;i++){life.Advance(.02f);restored.Advance(.02f);Assert.That(restored.Fire(true),Is.EqualTo(life.Fire(true)));Assert.That(JsonUtility.ToJson(restored.Read()),Is.EqualTo(JsonUtility.ToJson(life.Read())));}
            var bad=saved;bad.PlasmaCadenceCarry=double.NaN;Assert.Throws<ArgumentException>(()=>restored.Restore(bad));
            life.ClearHeldInput();Assert.That(life.Read().PlasmaCadenceCarry,Is.Zero);
            life.Advance(1);Assert.That(life.Fire(true),Is.True);Assert.That(life.Read().PlasmaCooldownRemaining,Is.EqualTo(ProvingProfile.CreatePlasmaDefault().Get("plasma.cooldownSeconds")));
        }
        [Test] public void ApprovedDefaultsAndEveryParameterHasMetadata()
        {
            var p=ProvingProfile.CreatePlasmaDefault();Assert.That(p.Validate(),Is.Empty);
            Assert.That(p.Get("plasma.damage")/p.Get("plasma.cooldownSeconds"),Is.EqualTo(144).Within(.001));
            Assert.That(p.Get("plasma.speed"),Is.EqualTo(14.4f));Assert.That(p.Get("plasma.pickupAmmo"),Is.EqualTo(100));
            Assert.That(p.Version,Is.EqualTo(6));Assert.That(p.Get("plasma.orbRadius"),Is.EqualTo(.1521f));
            foreach(var d in p.Descriptors){Assert.That(d.Label,Is.Not.Empty);Assert.That(d.Description,Is.Not.Empty);}
        }
        [Test] public void PickupHeldCadenceExhaustionAndRefillAreIndependent()
        {
            var life=new CombatLife("p",ProvingProfile.CreateCombatDefault());int initialRifleAmmo=life.Read().RifleAmmo;Assert.That(CombatLife.Owned(life.Read(),WeaponId.Plasma),Is.False);
            Assert.That(life.CollectWeapon(WeaponId.Plasma),Is.True);life.Select(WeaponSelection.Plasma);life.Advance(1);
            Assert.That(life.Read().Ammo,Is.EqualTo(100));Assert.That(life.Fire(true),Is.True);
            life.Advance(.16);Assert.That(life.Fire(true),Is.False);life.Advance(.02);Assert.That(life.Fire(true),Is.True);
            for(int i=0;i<98;i++){life.Advance(.2);Assert.That(life.Fire(true),Is.True);}
            life.Advance(.2);Assert.That(life.Fire(true),Is.False);Assert.That(life.Read().RifleAmmo,Is.EqualTo(initialRifleAmmo));
            Assert.That(life.CollectWeapon(WeaponId.Plasma),Is.True);Assert.That(life.Read().PlasmaAmmo,Is.EqualTo(100));Assert.That(life.CollectWeapon(WeaponId.Plasma),Is.False);
        }
        [Test] public void FifthSlotCyclesAndDeathClearsOwnedCharge()
        {
            var life=new CombatLife("p",ProvingProfile.CreateCombatDefault());life.CollectWeapon(WeaponId.Plasma);life.Select(WeaponSelection.Previous);life.Advance(1);
            Assert.That(life.Read().SelectedWeapon,Is.EqualTo(WeaponId.Plasma));life.Select(WeaponSelection.Next);life.Advance(1);Assert.That(life.Read().SelectedWeapon,Is.EqualTo(WeaponId.Rifle));
            life.Damage(1,100);Assert.That(life.Read().PlasmaOwned,Is.False);Assert.That(life.Read().PlasmaAmmo,Is.Zero);
        }
        [Test] public void RestoreRejectsUnownedChargeAndBadCooldownWithoutMutation()
        {
            var life=new CombatLife("p",ProvingProfile.CreateCombatDefault());var original=life.Read();var bad=original;bad.PlasmaAmmo=1;
            Assert.Throws<ArgumentException>(()=>life.Restore(bad));bad=original;bad.PlasmaCooldownRemaining=double.NaN;Assert.Throws<ArgumentException>(()=>life.Restore(bad));
            Assert.That(JsonUtility.ToJson(life.Read()),Is.EqualTo(JsonUtility.ToJson(original)));
        }
        [Test] public void AllCurrentMapsHaveAuthoredForgeAndValidProfiles()
        {
            foreach(var d in new[]{CombatBowlCatalog.Resolve(CombatBowlCatalog.Id,CombatBowlCatalog.Revision),IndustrialTunnelsCatalog.Build(),LunarLaboratoryCatalog.Build(),WellArenaCatalog.Build()})
                Assert.That(d.Pickups.Any(p=>p.Kind==ArenaPickupKind.Plasma),Is.True,d.Identity);
        }
    }
}
