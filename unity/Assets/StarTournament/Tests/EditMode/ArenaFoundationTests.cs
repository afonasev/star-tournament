using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class ArenaFoundationTests
    {
        static ArenaDefinition Generate()=>CombatBowlCatalog.Build();
        [Test] public void PublishedDefinitionIsStableAndFrozenAgainstProfileAndCopyEdits()
        {
            var profile=ProvingProfile.CreateDefault();var frozen=CombatBowlCatalog.Freeze(profile);var before=JsonUtility.ToJson(frozen.Definition);
            profile.Set("player.movement.maximumGroundSpeed",10);Assert.That(JsonUtility.ToJson(CombatBowlCatalog.Build()),Is.EqualTo(before));
            var copy=frozen.Definition;copy.Solids[0].Size.x++;Assert.Throws<System.ArgumentException>(()=>frozen.RequireDefinition(copy));Assert.That(JsonUtility.ToJson(frozen.Definition),Is.EqualTo(before));
        }
        [Test] public void UnknownPublishedIdentityIsRejectedWithoutFallback()
        {Assert.Throws<System.ArgumentException>(()=>CombatBowlCatalog.Resolve("unknown","1"));Assert.Throws<System.ArgumentException>(()=>CombatBowlCatalog.Resolve(CombatBowlCatalog.Id,"unknown"));var d=Generate();d.Identity="unknown@1";Assert.That(ArenaDefinitionValidator.Validate(d,ProvingProfile.CreateDefault()).IsValid,Is.False);}
        [TestCase("solid")] [TestCase("layer")] [TestCase("spawn")] [TestCase("armor")] [TestCase("region")] [TestCase("transition")]
        public void ArenaFourValidator_RejectsMutatedCanonicalDataWithElementDiagnostic(string mutation)
        {
            var definition=Generate().Copy(); string expected;
            switch(mutation)
            {
                case "solid": definition.Solids[0].Size.x=0; expected="ARENA_INVALID_SOLID_GEOMETRY"; break;
                case "layer": definition.Solids[0].Layer=0; expected="ARENA_INVALID_COLLISION_LAYER"; break;
                case "spawn": definition.Spawns[0].y=99; expected="ARENA_INVALID_SPAWN"; break;
                case "armor": definition.Pickups.First(x=>x.Kind==ArenaPickupKind.Armor).Anchor.y=99; expected="ARENA_INVALID_ARMOR_PICKUP_ANCHOR"; break;
                case "region": definition.SpawnRegions[0].Support="missing"; expected="ARENA_UNKNOWN_SPAWN_SUPPORT"; break;
                default: definition.Transitions[0].OrderedFeet[0].y=99; expected="ARENA_INVALID_TRANSITION_ENDPOINT"; break;
            }
            var result=ArenaDefinitionValidator.Validate(definition,ProvingProfile.CreateDefault());
            Assert.That(result.IsValid,Is.False);Assert.That(result.Code,Is.EqualTo(expected));Assert.That(result.MapId,Is.EqualTo(definition.MapId));Assert.That(result.ElementId,Is.Not.Empty);
        }
        [Test] public void ArenaFourValidator_RejectsDuplicateIdsAndBrokenTransitionOrder()
        {
            var duplicate=Generate().Copy();duplicate.Solids[1].Id=duplicate.Solids[0].Id;
            Assert.That(ArenaDefinitionValidator.Validate(duplicate,ProvingProfile.CreateDefault()).Code,Is.EqualTo("ARENA_DUPLICATE_SOLID_ID"));
            var broken=Generate().Copy();var feet=broken.Transitions[0].OrderedFeet;feet[1]=feet[0];
            Assert.That(ArenaDefinitionValidator.Validate(broken,ProvingProfile.CreateDefault()).Code,Is.EqualTo("ARENA_DISCONTINUOUS_TRANSITION"));
        }
    }
}
