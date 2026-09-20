using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class ArenaFoundationPlayModeTests
    {
        [UnityTest] public IEnumerator DefinitionBuild_PreservesSupportAfterObjectRename()
        {
            var root=new GameObject("arena-foundation");var arena=root.AddComponent<ProvingArena>();var definition=ArenaGenerator.Generate(new ArenaGenerationRequest{Seed=9,GeneratorVersion=ArenaGenerator.FamiliesVersion,Profile=ProvingProfile.CreateArenaFamiliesDefault()});
            arena.Build(definition,ProvingProfile.CreateDefault());yield return null;
            var floor=root.transform.Find("floor-lower");floor.name="presentation-renamed";var collider=floor.GetComponent<Collider>();
            Assert.That(arena.NavigationSupport(collider),Is.EqualTo("floor:lower"));Assert.That(arena.TryRoute(arena.LowerRoutePoint,arena.UpperRoutePoint,.6f,out _),Is.True);Object.Destroy(root);
        }

        [UnityTest] public IEnumerator RepresentativeFamilies_UseOneProjectionWithFamilyIdentity()
        {
            for(int family=0;family<ArenaFamilyCatalog.Ids.Count;family++)
            {
                var root=new GameObject("arena-family-"+family);var arena=root.AddComponent<ProvingArena>();var definition=ArenaGenerator.Generate(new ArenaGenerationRequest{Seed=23,GeneratorVersion=ArenaGenerator.FamiliesVersion,Profile=ProvingProfile.CreateArenaFamiliesDefault(family)});
                try
                {
                    arena.Build(definition,ProvingProfile.CreateDefault());yield return null;
                    Assert.That(arena.Definition.FamilyId,Is.EqualTo(ArenaFamilyCatalog.Ids[family]));Assert.That(root.transform.childCount,Is.EqualTo(definition.Solids.Length));Assert.That(arena.TryRoute(arena.LowerRoutePoint,arena.UpperRoutePoint,.8f,out _),Is.True,"family="+definition.FamilyId);
                }
                finally { Object.Destroy(root); }
                yield return null;
            }
        }
    }
}
