using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class CombatBowlP4Tests
    {
        [Test] public void R7HasEightRegionsAndIndependentAuthoredBonuses()
        {
            var d=CombatBowlCatalog.Build();var valid=ArenaDefinitionValidator.Validate(d,ProvingProfile.CreateDefault());
            Assert.That(valid.IsValid,Is.True,valid.ToString());
            Assert.That(d.Identity,Is.EqualTo("combat-bowl-v1@18"));Assert.That(d.Spawns.Length,Is.EqualTo(8));Assert.That(d.Transitions.Length,Is.EqualTo(12));
            Assert.That(d.Pickups.Count(x=>x.Kind==ArenaPickupKind.Armor),Is.EqualTo(2));Assert.That(d.Pickups.Count(x=>x.Kind==ArenaPickupKind.Speed),Is.EqualTo(2));
            Assert.That(d.Solids.Count(s=>s.Id.StartsWith("column-")),Is.EqualTo(4));
            Assert.That(d.Solids.Single(s=>s.Id=="armor-corridor").Size.x,Is.EqualTo(5.2f).Within(.001f));
            Assert.That(d.Pickups.Single(x=>x.Kind==ArenaPickupKind.FullHeal).Anchor,Is.EqualTo(new Vector3(0,-1.2f,0)));Assert.That(d.DamagePickupAnchor,Is.EqualTo(new Vector3(0,4,0)));
            Assert.That(d.DisableSpeedPickup,Is.False);Assert.That(d.HasDamagePickup,Is.True);
        }
        [Test] public void FrozenMapAndRepeatAreIndependentOfGameplayProfileAndReturnedCopies()
        {
            var p=ProvingProfile.CreateDefault();var frozen=CombatBowlCatalog.Freeze(p);var original=JsonUtility.ToJson(frozen.Definition);
            p.Set("player.movement.maximumGroundSpeed",10);var copy=frozen.Definition;copy.Solids[0].Position=Vector3.one*90;
            Assert.That(JsonUtility.ToJson(frozen.Definition),Is.EqualTo(original));Assert.That(JsonUtility.ToJson(CombatBowlCatalog.Freeze(p).Definition),Is.EqualTo(original));
            Assert.Throws<ArgumentException>(()=>frozen.RequireDefinition(copy));
            var authoring=CombatBowlCatalog.AuthoringProfile();Assert.That(authoring.Validate(),Is.Empty);authoring.Set("heal-basement.anchor.y",-2);
            Assert.That(CombatBowlCatalog.Build().Pickups.Single(x=>x.Kind==ArenaPickupKind.FullHeal).Anchor.y,Is.EqualTo(-1.2f));
        }
        [Test] public void RejectUnsupportedRosterBeforeAllocation()
        {foreach(var n in new[]{2,3,4,5,6,7,8})Assert.DoesNotThrow(()=>CombatBowlRules.RequireParticipantCount(n));foreach(var n in new[]{0,1,9})Assert.Throws<ArgumentException>(()=>CombatBowlRules.RequireParticipantCount(n));}
        [Test] public void ArmorAndDamageCannotBeCollectedAcrossFloors()
        {
            var profile=ProvingProfile.CreateCombatDefault();var armor=new ArmorPickup(new Vector3(0,-1.2f,0),profile);var life=new CombatLife("p",profile);
            Assert.That(armor.TryCollect(Vector3.zero,life),Is.False);Assert.That(armor.TryCollect(new Vector3(0,4,0),life),Is.False);Assert.That(armor.TryCollect(new Vector3(0,-1.2f,0),life),Is.True);
            var damage=new DamageBoostPickup(new Vector3(0,4,0),profile);damage.Advance(15);Assert.That(damage.TryCollect(new Vector3(0,-1.2f,0)),Is.False);Assert.That(damage.TryCollect(new Vector3(0,4,0)),Is.True);
        }
        [Test] public void DamageUsesApprovedDelayDurationMultiplierAndCooldownWithSnapshot()
        {
            var p=ProvingProfile.CreateCombatDefault();var pickup=new DamageBoostPickup(Vector3.zero,p);pickup.Advance(14);Assert.That(pickup.Read().Available,Is.False);pickup.Advance(1);Assert.That(pickup.Read().Available,Is.True);
            Assert.That(pickup.Multiplier,Is.EqualTo(1.5f));Assert.That(pickup.Duration,Is.EqualTo(10));pickup.TryCollect(Vector3.zero);pickup.Advance(44);Assert.That(pickup.Read().Available,Is.False);
            var copy=new DamageBoostPickup(Vector3.zero,p);copy.Restore(JsonUtility.FromJson<DamageBoostPickupState>(JsonUtility.ToJson(pickup.Read())));copy.Advance(1);Assert.That(copy.Read().Available,Is.True);
        }
    }
}
