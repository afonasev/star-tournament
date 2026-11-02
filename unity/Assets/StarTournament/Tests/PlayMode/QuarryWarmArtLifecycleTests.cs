using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class QuarryWarmArtLifecycleTests
    {
        [UnityTest] public IEnumerator WarmFittingsPreserveSolidsAndReleaseMeshes()
        {
            var root=new GameObject("Quarry art lifecycle");var arena=root.AddComponent<ProvingArena>();var movement=ProvingProfile.CreateDefault();
            var priorPipeline=QualitySettings.renderPipeline;var priorActive=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            float priorDistance=priorActive?priorActive.shadowDistance:0;
            var priorAmbient=RenderSettings.ambientLight;var priorMode=RenderSettings.ambientMode;
            try
            {
                var frozen=AuthoredArenaCatalog.Freeze(QuarryArenaCatalog.Id,movement);arena.Build(frozen,movement);yield return null;
                frozen.RequireDefinition(arena.Definition);
                var ownedPipeline=QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
                if(priorActive)
                {
                    Assert.That(ownedPipeline,Is.Not.SameAs(priorActive));
                    Assert.That(ownedPipeline.shadowDistance,Is.EqualTo(ProvingProfile.CreateQuarryAuthoring(frozen.Definition).Get("quarry.warm.shadowDistance")));
                    Assert.That(ownedPipeline.shadowCascadeCount,Is.EqualTo(4));
                    Assert.That(priorActive.shadowDistance,Is.EqualTo(priorDistance),"Do not mutate shared quality asset");
                }
                var warm=root.GetComponentInChildren<QuarryWarmDetails>();Assert.That(warm,Is.Not.Null);
                Assert.That(warm.GetComponentsInChildren<Collider>(),Is.Empty);
                Assert.That(warm.ChipPositions.Count,Is.GreaterThan(0));Assert.That(warm.ChipRendererCount,Is.InRange(1,4));
                float clearance=ProvingProfile.CreateQuarryAuthoring(frozen.Definition).Get("quarry.warm.detailClearance");
                foreach(var chip in warm.ChipPositions)
                    foreach(var anchor in frozen.Definition.Spawns.Concat(frozen.Definition.Pickups.Select(x=>x.Anchor)))
                        if(Mathf.Abs(chip.y-anchor.y)<2)Assert.That(Vector2.Distance(new Vector2(chip.x,chip.z),new Vector2(anchor.x,anchor.z)),Is.GreaterThanOrEqualTo(clearance),"Keep gameplay points clear");
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
                Assert.That(QualitySettings.renderPipeline,Is.SameAs(priorPipeline),"Restore prior quality on map teardown");
                if(priorActive)Assert.That(!ownedPipeline,Is.True,"Release transient lighting pipeline");
                Assert.That(RenderSettings.ambientMode,Is.EqualTo(priorMode));
                Assert.That(RenderSettings.ambientLight,Is.EqualTo(priorAmbient));
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
