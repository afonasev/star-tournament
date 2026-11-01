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
            var industrial=AuthoredArenaCatalog.Resolve(d.MapId,"3");Assert.That(industrial.Identity,Is.EqualTo("quarry-prototype-v1@3"));Assert.That(industrial.Solids.Any(s=>s.Id=="spawn-0-roof"),Is.True);
            Assert.That(d.Solids.Any(s=>s.Id.StartsWith("spawn-")),Is.False,"No repeated spawn room geometry remains");
            var revision5=AuthoredArenaCatalog.Resolve(d.MapId,"5");Assert.That(revision5.Identity,Is.EqualTo("quarry-prototype-v1@5"));
            Assert.That(JsonUtility.ToJson(new ArenaDefinition{Solids=d.Solids,Spawns=d.Spawns,Transitions=d.Transitions}),Is.EqualTo(JsonUtility.ToJson(new ArenaDefinition{Solids=revision5.Solids,Spawns=revision5.Spawns,Transitions=revision5.Transitions})),"Geometry, starts and routes unchanged from revision5");
            for(int i=0;i<d.Pickups.Length;i++)
            {
                var expected=JsonUtility.FromJson<ArenaPickupDefinition>(JsonUtility.ToJson(revision5.Pickups[i]));
                if(i==0)expected.Anchor=new Vector3(-17,4,-5);
                if(i==4)expected.Anchor=new Vector3(25,4,-5);
                Assert.That(JsonUtility.ToJson(d.Pickups[i]),Is.EqualTo(JsonUtility.ToJson(expected)),"Only W1/W5 anchors move");
            }
            var open=AuthoredArenaCatalog.Resolve(d.MapId,"4");Assert.That(open.Identity,Is.EqualTo("quarry-prototype-v1@4"));
            Assert.That(open.Spawns,Is.EqualTo(industrial.Spawns));Assert.That(open.Pickups.Select(p=>p.Anchor),Is.EqualTo(industrial.Pickups.Select(p=>p.Anchor)));
            // The only removed solids are buried toe fill cells; walking ramp geometry stays exact.
            var previousSolids=open.Solids.Where(s=>!s.Id.Contains("-lower-fill-")||s.Size.y>.02f).ToArray();
            Assert.That(open.Solids.Length-previousSolids.Length,Is.EqualTo(8));
            Assert.That(d.Solids.Length,Is.EqualTo(previousSolids.Length));
            for(int i=0;i<d.Solids.Length;i++)
            {
                var previous=JsonUtility.FromJson<ArenaSolid>(JsonUtility.ToJson(previousSolids[i]));
                if(previous.Support==QuarryArenaCatalog.Terrace&&(previous.Id.StartsWith("outcrop-")||previous.Id.StartsWith("tunnel-roof-")))
                {Assert.That(d.Solids[i].Material,Is.EqualTo("floor"));Assert.That(previous.Material,Is.EqualTo("quarry-rock"));previous.Material="floor";}
                Assert.That(JsonUtility.ToJson(d.Solids[i]),Is.EqualTo(JsonUtility.ToJson(previous)),"Unchanged geometry except explicit roof floor classification: "+previous.Id);
            }
            for(int i=0;i<8;i++)if(i!=2)Assert.That(d.Spawns[i],Is.EqualTo(open.Spawns[i]));
            Assert.That(d.Spawns[2],Is.EqualTo(new Vector3(-6,0,4)));Assert.That(d.SpawnRegions[2].Support,Is.EqualTo(QuarryArenaCatalog.Ground));
            var damage=d.Pickups.Single(p=>p.Kind==ArenaPickupKind.Damage);Assert.That(damage.Anchor,Is.EqualTo(new Vector3(0,4,0)));Assert.That(damage.Support,Is.EqualTo(QuarryArenaCatalog.Terrace));
            var armor=d.Pickups.Where(p=>p.Kind==ArenaPickupKind.Armor).ToArray();Assert.That(armor[0].Anchor.x,Is.LessThan(0));Assert.That(armor[0].Anchor.z,Is.LessThan(0));Assert.That(armor[0].Anchor.y,Is.Zero);Assert.That(armor[1].Anchor.x,Is.GreaterThan(0));Assert.That(armor[1].Anchor.z,Is.GreaterThan(0));Assert.That(armor[1].Anchor.y,Is.EqualTo(8));
            Assert.That(d.Pickups.Select(p=>p.Kind),Is.EqualTo(open.Pickups.Select(p=>p.Kind)));
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
