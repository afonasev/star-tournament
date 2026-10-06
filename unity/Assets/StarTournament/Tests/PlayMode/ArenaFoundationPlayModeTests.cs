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
            var root=new GameObject("arena-foundation");var arena=root.AddComponent<ProvingArena>();var frozen=CombatBowlCatalog.Freeze(ProvingProfile.CreateDefault());
            arena.Build(frozen,ProvingProfile.CreateDefault());yield return null;
            var floor=root.transform.Find("north-flank");floor.name="presentation-renamed";var collider=floor.GetComponent<Collider>();
            Assert.That(arena.NavigationSupport(collider),Is.EqualTo("support:lower-loop"));Assert.That(arena.TryRoute(arena.LowerRoutePoint,arena.UpperRoutePoint,.6f,out _),Is.True);Object.Destroy(root);
        }

        [UnityTest] public IEnumerator RepresentativeFamilies_UseOneProjectionWithMapIdentity()
        {
            for(int family=0;family<1;family++)
            {
                var root=new GameObject("arena-family-"+family);var arena=root.AddComponent<ProvingArena>();var frozen=CombatBowlCatalog.Freeze(ProvingProfile.CreateDefault());var definition=frozen.Definition;
                try
                {
                    arena.Build(frozen,ProvingProfile.CreateDefault());yield return null;
                    Assert.That(arena.Definition.MapId,Is.EqualTo(CombatBowlCatalog.Id));Assert.That(root.transform.childCount,Is.EqualTo(definition.Solids.Length+1));Assert.That(arena.TryRoute(arena.LowerRoutePoint,arena.UpperRoutePoint,.8f,out _),Is.True,"family="+definition.MapId);
                }
                finally { Object.Destroy(root); }
                yield return null;
            }
        }

        [UnityTest] public IEnumerator FreezeMismatch_CreatesNoArenaProjectionOrNavigationContext()
        {
            var root=new GameObject("invalid-arena");var arena=root.AddComponent<ProvingArena>();
            var frozen=CombatBowlCatalog.Freeze(ProvingProfile.CreateDefault());
            var mismatch=frozen.Definition;mismatch.SpawnRegions[0].Support="missing";
            Assert.Throws<System.ArgumentException>(()=>frozen.RequireDefinition(mismatch));
            Assert.That(root.transform.childCount,Is.Zero);Assert.That(arena.Definition,Is.Null);Assert.That(arena.FrozenSnapshot,Is.Null);
            var movement=ProvingProfile.CreateDefault();arena.Build(frozen,movement);yield return null;
            arena.Definition.Solids[0].Id+="-drift";
            var error=Assert.Throws<System.ArgumentException>(()=>new NativeNavigationProvider(arena,movement,ProvingProfile.CreateNavigationDefault()));
            Assert.That(error.Message,Does.StartWith("ARENA_FREEZE_DEFINITION_MISMATCH|family:"+frozen.MapId));Object.Destroy(root);yield return null;
        }
    }
}
