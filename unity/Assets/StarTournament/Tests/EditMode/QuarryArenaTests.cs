using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class QuarryArenaTests
    {
        [Test] public void CatalogFreezesTenSuppliesAndEightSpawnsAcrossThreeLevels()
        {
            var d=AuthoredArenaCatalog.Resolve(QuarryArenaCatalog.Id);var movement=ProvingProfile.CreateDefault();
            Assert.That(ArenaDefinitionValidator.Validate(d,movement).IsValid,Is.True,ArenaDefinitionValidator.Validate(d,movement).ToString());
            Assert.That(AuthoredArenaCatalog.Name(d.MapId),Is.EqualTo("Карьер — прототип"));
            Assert.That(AuthoredArenaCatalog.Supports(d.MapId,8),Is.True);Assert.That(AuthoredArenaCatalog.Supports(d.MapId,9),Is.False);
            Assert.That(d.Spawns.Length,Is.EqualTo(8));Assert.That(d.Spawns.Select(p=>p.y).Distinct(),Is.EquivalentTo(new[]{0f,4f,8f}));Assert.That(d.SpawnRegions.Length,Is.EqualTo(8));
            Assert.That(d.Pickups.Length,Is.EqualTo(10));Assert.That(d.Pickups.Count(p=>p.Kind==ArenaPickupKind.Shotgun),Is.EqualTo(2));Assert.That(d.Pickups.Count(p=>p.Kind==ArenaPickupKind.Armor),Is.EqualTo(2));
            foreach(var kind in new[]{ArenaPickupKind.Pulse,ArenaPickupKind.Cutter,ArenaPickupKind.Plasma,ArenaPickupKind.Speed,ArenaPickupKind.Damage,ArenaPickupKind.FullHeal})Assert.That(d.Pickups.Count(p=>p.Kind==kind),Is.EqualTo(1));
            Assert.That(d.Pickups.Single(p=>p.Kind==ArenaPickupKind.Cutter).Anchor.y,Is.EqualTo(8));Assert.That(d.Pickups.Single(p=>p.Kind==ArenaPickupKind.Pulse).Anchor.y,Is.EqualTo(8));Assert.That(d.Pickups.Single(p=>p.Kind==ArenaPickupKind.FullHeal).Anchor.y,Is.Zero);
            Assert.That(d.Transitions.Length,Is.EqualTo(4));Assert.That(d.Transitions.Count(t=>t.LowerSupport==QuarryArenaCatalog.Ground),Is.EqualTo(2));Assert.That(d.Transitions.Count(t=>t.UpperSupport==QuarryArenaCatalog.Upper),Is.EqualTo(2));
            var legacy=AuthoredArenaCatalog.Resolve(d.MapId,"1");Assert.That(legacy.Identity,Is.EqualTo("quarry-prototype-v1@1"));Assert.That(legacy.Spawns.All(p=>p.y==0),Is.True);Assert.That(legacy.Transitions.Length,Is.EqualTo(2));
            var prior=AuthoredArenaCatalog.Resolve(d.MapId,"2");Assert.That(prior.Identity,Is.EqualTo("quarry-prototype-v1@2"));Assert.That(prior.Transitions.Length,Is.EqualTo(4));Assert.That(prior.Solids.Any(s=>s.Id=="center-east-bridge"),Is.False);
            Assert.That(d.Solids.Single(s=>s.Id=="center-east-bridge").Support,Is.EqualTo(QuarryArenaCatalog.Terrace));
            var frozen=AuthoredArenaCatalog.Freeze(d.MapId,movement);Assert.That(JsonUtility.ToJson(frozen.Definition),Is.EqualTo(JsonUtility.ToJson(d)));
            d.Spawns[0]=Vector3.one;Assert.That(frozen.Definition.Spawns[0],Is.Not.EqualTo(d.Spawns[0]),"Frozen definition owns its data");
        }
        [Test] public void MapMetadataAndMenuAssetAreCompleteWithoutChangingExistingMapIdentities()
        {
            var d=QuarryArenaCatalog.Build();var p=QuarryArenaCatalog.AuthoringProfile();Assert.That(p.Validate(),Is.Empty);
            foreach(var s in d.Solids)Assert.That(p.Get("quarry.map."+s.Id+".position.x"),Is.EqualTo(s.Position.x));
            foreach(var item in d.Pickups)Assert.That(p.Get("quarry.map."+item.Id+".anchor.y"),Is.EqualTo(item.Anchor.y));
            Assert.That(Resources.Load<Texture2D>("MapMenuArt/"+d.MapId),Is.Not.Null);
            var seen=new System.Collections.Generic.HashSet<string>();string id=CombatBowlCatalog.Id;
            do{Assert.That(seen.Add(id),Is.True);Assert.That(AuthoredArenaCatalog.Resolve(id).MapId,Is.EqualTo(id));id=AuthoredArenaCatalog.Next(id);}while(id!=CombatBowlCatalog.Id);
            Assert.That(seen,Does.Contain(QuarryArenaCatalog.Id));Assert.That(seen.Count,Is.EqualTo(6));
            Assert.Throws<System.ArgumentException>(()=>AuthoredArenaCatalog.Resolve(QuarryArenaCatalog.Id,"missing"));
        }
    }
}
