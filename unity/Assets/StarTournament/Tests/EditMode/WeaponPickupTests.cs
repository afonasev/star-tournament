using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class WeaponPickupTests
    {
        static WeaponPickup Pickup(string id="test",ArenaPickupKind kind=ArenaPickupKind.Cutter)=>new WeaponPickup(new ArenaPickupDefinition{Id=id,Support="floor",Kind=kind,Anchor=Vector3.zero},ProvingProfile.CreateCombatDefault());
        [Test]public void LifeStartsAndRespawnsWithOnlyRifleAndCycleSkipsClosedSlots()
        {
            var life=new CombatLife("p",ProvingProfile.CreateCombatDefault());
            foreach(var select in new[]{WeaponSelection.Next,WeaponSelection.Previous,WeaponSelection.Shotgun,WeaponSelection.RocketLauncher,WeaponSelection.Cutter})life.Select(select);
            Assert.That(life.Read().SwitchRemaining,Is.Zero);Assert.That(life.Read().SelectedWeapon,Is.EqualTo(WeaponId.Rifle));
            Assert.That(life.Read().ShotgunAmmo+life.Read().RocketAmmo+life.Read().CutterEnergy,Is.Zero);
            Pickup().TryCollect(Vector3.zero,"floor",life);life.Select(WeaponSelection.Next);life.Advance(1);
            Assert.That(life.Read().SelectedWeapon,Is.EqualTo(WeaponId.Cutter));life.ConsumeCutter(30);life.Select(WeaponSelection.Previous);life.Advance(1);life.Select(WeaponSelection.Next);life.Advance(1);
            Assert.That(life.Read().SelectedWeapon,Is.EqualTo(WeaponId.Cutter),"Empty collected slot stays owned");
            life.Damage(1,100);Assert.That(life.Read().CutterOwned,Is.False);life.Advance(10);life.Respawn(1);
            Assert.That(life.Read().RifleAmmo,Is.EqualTo(200));Assert.That(life.Read().CutterEnergy,Is.Zero);
        }
        [Test]public void FractionalRefillPreservesTransitionCooldownAndHeldInput()
        {
            var life=new CombatLife("p",ProvingProfile.CreateCombatDefault());var item=Pickup();
            Assert.That(item.TryCollect(Vector3.zero,"floor",life),Is.True);life.ConsumeCutter(.05);life.Fire(true);life.Select(WeaponSelection.Cutter);
            var before=life.Read();item.Advance(15);Assert.That(item.TryCollect(Vector3.zero,"floor",life),Is.True);
            var after=life.Read();Assert.That(after.CutterEnergy,Is.EqualTo(30));Assert.That(after.SwitchRemaining,Is.EqualTo(before.SwitchRemaining));Assert.That(after.RifleCooldownRemaining,Is.EqualTo(before.RifleCooldownRemaining));Assert.That(after.FireHeld,Is.EqualTo(before.FireHeld));Assert.That(after.SelectedWeapon,Is.EqualTo(before.SelectedWeapon));
            item.Advance(15);Assert.That(item.TryCollect(Vector3.zero,"floor",life),Is.False);Assert.That(item.Read().Available,Is.True);
        }
        [Test]public void TimersAreIndependentSerializableAndResolveSimultaneousContactOnce()
        {
            var a=Pickup("a");var b=Pickup("b",ArenaPickupKind.Pulse);var first=new CombatLife("1",ProvingProfile.CreateCombatDefault());var second=new CombatLife("2",ProvingProfile.CreateCombatDefault());
            Assert.That(a.TryCollect(Vector3.zero,"floor",first),Is.True);Assert.That(a.TryCollect(Vector3.zero,"floor",second),Is.False);Assert.That(second.Read().CutterOwned,Is.False);
            a.Advance(4);b.TryCollect(Vector3.zero,"floor",first);a.Advance(11);b.Advance(11);
            Assert.That(a.Read().Available,Is.True);Assert.That(b.Read().Remaining,Is.EqualTo(4));
            var json=JsonUtility.ToJson(b.Read());var lifeJson=JsonUtility.ToJson(first.Read());b.Advance(4);b.Restore(JsonUtility.FromJson<WeaponPickupState>(json));first.Restore(JsonUtility.FromJson<CombatLifeState>(lifeJson));
            Assert.That(b.Read().Remaining,Is.EqualTo(4));Assert.That(first.Read().RocketOwned,Is.True);
            b.Advance(0);Assert.That(b.Read().Remaining,Is.EqualTo(4));
        }
        [Test]public void ContactRequiresCorrectSupportHeightAndNeedsResource()
        {
            var item=Pickup();var life=new CombatLife("p",ProvingProfile.CreateCombatDefault());
            Assert.That(item.TryCollect(Vector3.up*4,"floor",life),Is.False);Assert.That(item.TryCollect(Vector3.zero,"other-floor",life),Is.False);Assert.That(item.TryCollect(Vector3.forward*3,"floor",life),Is.False);
            var s=item.Read();s.Remaining=1;Assert.Throws<ArgumentException>(()=>item.Restore(s));
            var state=life.Read();state.SelectedWeapon=WeaponId.Cutter;Assert.Throws<ArgumentException>(()=>life.Restore(state));
        }
        [Test]public void CombatBowlContainsSixNamedWeaponAnchorsAndCompleteMetadata()
        {
            var d=CombatBowlCatalog.Build();var weapons=d.Pickups.Where(x=>WeaponPickup.IsWeapon(x.Kind)).ToArray();Assert.That(weapons.Length,Is.EqualTo(6));
            Assert.That(d.Identity,Is.EqualTo("combat-bowl-v1@18"));Assert.That(weapons.Single(x=>x.Id=="shotgun-west").Anchor,Is.EqualTo(new Vector3(-33,0,0)));
            Assert.That(ArenaDefinitionValidator.Validate(d,ProvingProfile.CreateDefault()).IsValid,Is.True);
            foreach(var descriptor in ProvingProfile.CreateCombatDefault().Descriptors.Where(x=>x.Path.StartsWith("weaponPickup.")))
            {Assert.That(descriptor.Minimum,Is.LessThan(descriptor.Maximum));Assert.That(descriptor.Step,Is.GreaterThan(0));Assert.That(descriptor.Description,Is.Not.Empty);}
        }
    }
}
