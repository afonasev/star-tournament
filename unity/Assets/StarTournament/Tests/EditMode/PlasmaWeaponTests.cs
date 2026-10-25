using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class PlasmaWeaponTests
    {
        [Test] public void ApprovedDefaultsAndEveryParameterHasMetadata()
        {
            var p=ProvingProfile.CreatePlasmaDefault();Assert.That(p.Validate(),Is.Empty);
            Assert.That(p.Get("plasma.damage")/p.Get("plasma.cooldownSeconds"),Is.EqualTo(200).Within(.001));
            Assert.That(p.Get("plasma.speed"),Is.EqualTo(30));Assert.That(p.Get("plasma.pickupAmmo"),Is.EqualTo(100));
            foreach(var d in p.Descriptors){Assert.That(d.Label,Is.Not.Empty);Assert.That(d.Description,Is.Not.Empty);}
        }
        [Test] public void PickupHeldCadenceExhaustionAndRefillAreIndependent()
        {
            var life=new CombatLife("p",ProvingProfile.CreateCombatDefault());int initialRifleAmmo=life.Read().RifleAmmo;Assert.That(CombatLife.Owned(life.Read(),WeaponId.Plasma),Is.False);
            Assert.That(life.CollectWeapon(WeaponId.Plasma),Is.True);life.Select(WeaponSelection.Plasma);life.Advance(1);
            Assert.That(life.Read().Ammo,Is.EqualTo(100));Assert.That(life.Fire(true),Is.True);
            life.Advance(.08);Assert.That(life.Fire(true),Is.False);life.Advance(.02);Assert.That(life.Fire(true),Is.True);
            for(int i=0;i<98;i++){life.Advance(.1);Assert.That(life.Fire(true),Is.True);}
            life.Advance(.1);Assert.That(life.Fire(true),Is.False);Assert.That(life.Read().RifleAmmo,Is.EqualTo(initialRifleAmmo));
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
