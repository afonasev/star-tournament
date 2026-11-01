using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class QuarryWarmArtLifecycleTests
    {
        [UnityTest] public IEnumerator WarmFittingsPreserveSolidsAndReleaseMeshes()
        {
            var root=new GameObject("Quarry art lifecycle");var arena=root.AddComponent<ProvingArena>();var movement=ProvingProfile.CreateDefault();
            try
            {
                var frozen=AuthoredArenaCatalog.Freeze(QuarryArenaCatalog.Id,movement);arena.Build(frozen,movement);yield return null;
                frozen.RequireDefinition(arena.Definition);
                var warm=root.GetComponentInChildren<QuarryWarmDetails>();Assert.That(warm,Is.Not.Null);
                Assert.That(warm.GetComponentsInChildren<Collider>(),Is.Empty);
                Assert.That(ArenaMeshResidency.ReadUnreferencedOwnedMeshes(root.transform),Is.Empty);
                foreach(var solid in frozen.Definition.Solids)
                {
                    var projected=root.transform.Find(solid.Id);Assert.That(projected,Is.Not.Null,solid.Id);
                    Assert.That(projected.position,Is.EqualTo(solid.Position),solid.Id);
                    Assert.That(projected.localScale,Is.EqualTo(solid.Size),solid.Id);
                    Assert.That(Quaternion.Angle(projected.rotation,solid.Rotation),Is.LessThan(.001f),solid.Id);
                    Assert.That(projected.GetComponent<Collider>(),Is.Not.Null,solid.Id);
                }
                var meshes=warm.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh).Distinct().ToArray();
                Assert.That(meshes.Length,Is.GreaterThan(0));
                var materials=root.GetComponentsInChildren<MeshRenderer>().SelectMany(r=>r.sharedMaterials).Where(m=>m&&m.name.StartsWith("Quarry warm A")).Distinct().ToArray();
                Assert.That(materials.Length,Is.GreaterThan(0));
                arena.ClearProjection();yield return null;
                Assert.That(meshes.All(m=>!m),Is.True,"Generated fitting meshes released on clear");
                Assert.That(materials.All(m=>!m),Is.True,"Warm materials released on clear");
                // Historical authoring profile retains its pre-A palette and parameter set.
                var legacy=AuthoredArenaCatalog.Resolve(QuarryArenaCatalog.Id,"5");
                Assert.That(QuarryWarmArt.Applies(legacy),Is.False);
                var profile=ProvingProfile.CreateQuarryAuthoring(legacy);
                Assert.That(profile.Get("quarry.art.color-1.0"),Is.EqualTo(134f/255).Within(.0001));
                Assert.That(profile.Descriptors.Any(d=>d.Path.StartsWith("quarry.warm.")),Is.False);
            }
            finally{Object.Destroy(root);}yield return null;
        }
    }
}
