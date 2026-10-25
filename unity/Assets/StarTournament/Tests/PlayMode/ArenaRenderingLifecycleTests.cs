using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class ArenaRenderingLifecycleTests
    {
        [UnityTest] public IEnumerator EveryArenaRebuildReleasesOwnedMeshesAndPreservesAuthoredSolids()
        {
            foreach(string map in new[]{CombatBowlCatalog.Id,IndustrialTunnelsCatalog.Id,LunarLaboratoryCatalog.Id,WellArenaCatalog.Id})
            {
                var root=new GameObject("render lifecycle "+map);var arena=root.AddComponent<ProvingArena>();var profile=ProvingProfile.CreateDefault();var frozen=AuthoredArenaCatalog.Freeze(map,profile);
                try
                {
                    for(int rebuild=0;rebuild<2;rebuild++)
                    {
                        arena.Build(frozen,profile);var optimizer=root.GetComponent<ArenaRenderChunks>();Assert.That(optimizer,Is.Not.Null);Assert.That(optimizer.SourceCount,Is.GreaterThan(0));
                        var meshes=optimizer.ReadOwnedMeshes();Assert.That(meshes.Length,Is.GreaterThan(0));
                        Debug.Log($"ARENA_RENDER_MEMORY|{map}|rebuild={rebuild}|meshes={meshes.Length}|bytes={meshes.Sum(mesh=>UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(mesh))}|preparation_ms={optimizer.PreparationMilliseconds}");
                        Assert.That(root.GetComponentsInChildren<MeshRenderer>().Any(r=>r.isPartOfStaticBatch),Is.True);
                        frozen.RequireDefinition(arena.Definition);
                        foreach(var solid in arena.Definition.Solids)
                        {
                            var projected=root.transform.Find(solid.Id);Assert.That(projected,Is.Not.Null,solid.Id);
                            Assert.That(projected.position,Is.EqualTo(solid.Position));Assert.That(Quaternion.Angle(projected.rotation,solid.Rotation),Is.LessThan(.001f));Assert.That(projected.localScale,Is.EqualTo(solid.Size));
                            Assert.That(projected.GetComponent<Collider>(),Is.Not.Null,solid.Id);
                        }
                        foreach(var lift in arena.Definition.Lifts??System.Array.Empty<ArenaLiftDefinition>())
                            Assert.That(root.transform.Find("lift-"+lift.Id).GetComponent<MeshRenderer>().enabled,Is.True,"moving lift must retain its own renderer");
                        arena.ClearProjection();yield return null;
                        Assert.That(root.transform.childCount,Is.Zero);Assert.That(meshes.All(mesh=>!mesh),Is.True,"native mesh leak after rebuild "+map);
                    }
                }
                finally{Object.Destroy(root);}yield return null;
            }
        }
    }
}
