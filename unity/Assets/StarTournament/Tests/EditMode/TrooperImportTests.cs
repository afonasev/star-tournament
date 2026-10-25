using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace StarTournament.ProvingGround.Tests
{
    public sealed class TrooperImportTests
    {
        const string Root="Assets/StarTournament/Trooper/";
        [Test] public void PersistedClipsHaveCompleteBindingsAndCorrectLoopPolicy()
        {
            var body=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"TrooperBody.prefab");
            var arms=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"TrooperArms.prefab");
            foreach(var prefab in new[]{body,arms})
            {
                Assert.That(prefab,Is.Not.Null); var animator=prefab.GetComponentInChildren<Animator>();
                Assert.That(animator.applyRootMotion,Is.False);
                for(int i=0;i<TrooperVisual.ClipNames.Length;i++)
                {
                    var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+TrooperVisual.ClipNames[i]+".anim");
                    Assert.That(clip,Is.Not.Null); Assert.That(clip.legacy,Is.False);
                    Assert.That(AnimationUtility.GetAnimationClipSettings(clip).loopTime,Is.EqualTo(i<4));
                    Assert.That(AnimationUtility.GetAnimationEvents(clip),Is.Empty);
                    var bindings=AnimationUtility.GetCurveBindings(clip); Assert.That(bindings.Length,Is.GreaterThan(100));
                    foreach(var b in bindings) if(b.path!="") Assert.That(animator.transform.Find(b.path),Is.Not.Null,b.path);
                }
                Assert.That(prefab.GetComponentsInChildren<Collider>(true),Is.Empty);
            }
        }
        [Test] public void SkinsUseSharedBodyMaterialsAndBoundedWeights()
        {
            var body=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"TrooperBody.prefab");
            var arms=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"TrooperArms.prefab");
            var materials=body.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).ToArray();
            foreach(var renderer in arms.GetComponentsInChildren<Renderer>(true))
                foreach(var material in renderer.sharedMaterials) { Assert.That(material,Is.Not.Null,"missing material"); Assert.That(materials.Contains(material),Is.True,material.name); }
            foreach(var prefab in new[]{body,arms})
                foreach(var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    Assert.That(skin.bones.All(b=>b),Is.True);
                    foreach(var w in skin.sharedMesh.boneWeights)
                        Assert.That(w.weight0+w.weight1+w.weight2+w.weight3,Is.EqualTo(1).Within(.0001));
                }
        }
        [Test] public void WorldMeshBudgetDoesNotReuseFirstPersonResolution()
        {
            var world=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"TrooperBody.prefab").GetComponentsInChildren<SkinnedMeshRenderer>();
            var view=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"TrooperArms.prefab").GetComponentsInChildren<SkinnedMeshRenderer>();
            // Asset resource budgets only: the world cap is the measured v0.1.8 skin
            // count. They are not gameplay/balance or camera configuration values.
            Assert.That(world.Sum(s=>s.sharedMesh.triangles.Length/3),Is.LessThanOrEqualTo(32989));
            Assert.That(view.Sum(s=>s.sharedMesh.triangles.Length/3),Is.LessThanOrEqualTo(45000));
            var forearms=world.Single(s=>s.name=="VECTOR world forearms").sharedMesh;
            var hands=view.Single(s=>s.name=="vector-armored-hands").sharedMesh;
            Assert.That(forearms,Is.Not.SameAs(hands));
            Assert.That(forearms.triangles.Length,Is.LessThan(hands.triangles.Length));
        }
        [Test] public void ShotgunMetalRetainsOriginalTextureInBodyAndArms()
        {
            foreach(var name in new[]{"TrooperBody.prefab","TrooperArms.prefab"})
            {
                var gun=AssetDatabase.LoadAssetAtPath<GameObject>(Root+name).GetComponentsInChildren<Renderer>(true).Single(r=>r.name=="weapon:joined");
                foreach(var key in new[]{"vector-chassis","vector-titanium"})
                {
                    var material=gun.sharedMaterials.Single(m=>m.name==key);
                    var textures=material.GetTexturePropertyNames().Where(p=>p.ToLowerInvariant().Contains("basecolor")).Select(material.GetTexture).Where(t=>t).ToArray();
                    Assert.That(textures,Is.Not.Empty,key);Assert.That(textures.All(t=>t.width<=128&&t.height<=128),Is.True,"Reuse bounded original metal texture");
                }
            }
        }
        [Test] public void ProfileAndAttributionAreComplete()
        {
            var profile=ProvingProfile.CreateTrooperDefault();Assert.That(profile.Validate(),Is.Empty);
            foreach(var d in profile.Descriptors) { Assert.That(d.Validate(out _),Is.True); profile.Set(d.Path,d.Maximum+d.Step); Assert.That(profile.Validate(),Is.Not.Empty);profile.Set(d.Path,d.DefaultValue); }
            string license=File.ReadAllText("Assets/StreamingAssets/trooper-ATTRIBUTION.txt");
            Assert.That(license,Does.Contain("ART_LOLL"));Assert.That(license,Does.Contain("creativecommons.org/licenses/by/4.0/"));
        }
        [Test] public void AssaultArmorKeepsNeutralMajorityAndRealIdentityMarks()
        {
            var body=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"TrooperBody.prefab");
            float neutral=0,painted=0;
            foreach(var skin in body.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh=skin.sharedMesh;var vertices=mesh.vertices;
                for(int slot=0;slot<skin.sharedMaterials.Length;slot++)
                {
                    string name=skin.sharedMaterials[slot].name;
                    bool armor=name.StartsWith("vector-assault-armor");
                    if(!armor&&!name.StartsWith("vector-fighter-identity"))continue;
                    var indices=mesh.GetIndices(slot);float area=0;
                    for(int i=0;i<indices.Length;i+=3)
                        area+=Vector3.Cross(vertices[indices[i+1]]-vertices[indices[i]],vertices[indices[i+2]]-vertices[indices[i]]).magnitude*.5f;
                    if(armor)neutral+=area;else painted+=area;
                }
            }
            Assert.That(painted,Is.GreaterThan(.01f),"Identity marks must have real surface coverage");
            Assert.That(neutral,Is.GreaterThan(painted*2),"Selected assault armor is mostly neutral rather than full-body participant color");
        }
    }
}
