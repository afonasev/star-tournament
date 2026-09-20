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
            var root=new GameObject("arena-foundation");var arena=root.AddComponent<ProvingArena>();var definition=ArenaGenerator.Generate(new ArenaGenerationRequest{Seed=9,GeneratorVersion=ArenaGenerator.FoundationVersion,Profile=ProvingProfile.CreateArenaFoundationDefault()});
            arena.Build(definition,ProvingProfile.CreateDefault());yield return null;
            var floor=root.transform.Find("floor-lower");floor.name="presentation-renamed";var collider=floor.GetComponent<Collider>();
            Assert.That(arena.NavigationSupport(collider),Is.EqualTo("floor:lower"));Assert.That(arena.TryRoute(arena.LowerRoutePoint,arena.UpperRoutePoint,.6f,out _),Is.True);Object.Destroy(root);
        }
    }
}
