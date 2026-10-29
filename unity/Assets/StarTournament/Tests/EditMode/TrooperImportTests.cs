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
            // Selected original body is preserved in full; the FPS mesh is its
            // distinct clipped derivative, not a duplicate of the world resolution.
            Assert.That(world.Sum(s=>s.sharedMesh.triangles.Length/3),Is.EqualTo(30757));
            Assert.That(view.Sum(s=>s.sharedMesh.triangles.Length/3),Is.LessThanOrEqualTo(11578));
            var cloth=world.Single(s=>s.name=="original-trooper-Object_3").sharedMesh;
            var hands=view.Single(s=>s.name=="vector-armored-hands").sharedMesh;
            Assert.That(cloth,Is.Not.SameAs(hands));
            Assert.That(hands.triangles.Length,Is.LessThan(cloth.triangles.Length));
        }
        [Test] public void OriginalFirstPersonMapsSurviveIndependentParticipantColors()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"TrooperArms.prefab");
            var first=Object.Instantiate(prefab);var second=Object.Instantiate(prefab);
            try
            {
                var blue=new Color(.06f,.30f,.85f);var orange=new Color(.9f,.23f,.04f);
                TrooperIdentityPresentation.ApplyBody(first,blue);TrooperIdentityPresentation.ApplyBody(second,orange);
                var source=prefab.GetComponentsInChildren<SkinnedMeshRenderer>().Single().sharedMaterials.Single();
                Assert.That(source.name,Is.EqualTo("material"));
                foreach(var pair in new[]{(root:first,color:blue),(root:second,color:orange)})
                {
                    var skin=pair.root.GetComponentsInChildren<SkinnedMeshRenderer>().Single();
                    var material=skin.sharedMaterials.Single();
                    foreach(var map in new[]{"baseColorTexture","normalTexture","metallicRoughnessTexture"})
                    {
                        Assert.That(source.GetTexture(map),Is.Not.Null,map);
                        Assert.That(material.GetTexture(map),Is.SameAs(source.GetTexture(map)));
                    }
                    var block=new MaterialPropertyBlock();skin.GetPropertyBlock(block,0);
                    Assert.That(block.GetFloat("_IdentityMode"),Is.Zero,"Original cloth/gloves remain neutral");
                    Assert.That(block.GetColor("_IdentityColor"),Is.EqualTo(pair.color));
                }
                Assert.That(first.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial,
                    Is.Not.SameAs(second.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial));
            }
            finally{Object.DestroyImmediate(first);Object.DestroyImmediate(second);}
        }
        [Test] public void FirstPersonSleevesExcludeUpperArmObstructionButWorldRemainsComplete()
        {
            int UpperVertices(GameObject prefab)
            {
                int total=0;
                foreach(var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var weights=skin.sharedMesh.boneWeights;
                    foreach(int vertex in skin.sharedMesh.triangles.Distinct())
                    {
                        var w=weights[vertex];var ids=new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};var values=new[]{w.weight0,w.weight1,w.weight2,w.weight3};
                        int index=0;for(int i=1;i<4;i++)if(values[i]>values[index])index=i;
                        string name=skin.bones[ids[index]].name;
                        if(name.EndsWith("UpperArm")||name.EndsWith("Shoulder"))total++;
                    }
                }
                return total;
            }
            Assert.That(UpperVertices(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"TrooperArms.prefab")),Is.Zero);
            Assert.That(UpperVertices(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"TrooperBody.prefab")),Is.GreaterThan(0));
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
        [Test] public void OriginalBodyRetainsSixTexturedSurfacesAndIdentityZones()
        {
            var body=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"TrooperBody.prefab");
            var skins=body.GetComponentsInChildren<SkinnedMeshRenderer>();
            var materials=skins.SelectMany(s=>s.sharedMaterials).ToArray();
            Assert.That(materials.Select(m=>m.name),Is.EquivalentTo(new[]{"TECI_helmet","material","gear","jumpjet","pants","torso"}));
            foreach(var material in materials)
                foreach(var map in new[]{"baseColorTexture","normalTexture","metallicRoughnessTexture"})
                    Assert.That(material.GetTexture(map),Is.Not.Null,material.name+" "+map);
            Assert.That(materials.Where(TrooperIdentityPresentation.IsBodyZone).Select(m=>m.name),
                Is.EquivalentTo(new[]{"TECI_helmet","jumpjet","torso"}));
            foreach(var skin in skins)Assert.That(skin.sharedMesh.triangles.Length,Is.GreaterThan(0));
        }
        [Test] public void B01VariantRemainsByteExactOutsideRuntime()
        {
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../..","assets/source/vector-b01"));
            foreach(var entry in new[]{
                (name:"trooper-body-animated.glb",hash:"b54804645bafcddfa5d19c78e8514d1db855736345ed87fd58d4b30d1625c448"),
                (name:"trooper-arms-animated.glb",hash:"364045503cb7fa2c73f8af4435aaefdd14e2b67397080280774a45a3ead104eb")})
            {
                using(var sha=System.Security.Cryptography.SHA256.Create())
                    Assert.That(System.BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(root,entry.name)))).Replace("-","").ToLowerInvariant(),Is.EqualTo(entry.hash));
            }
        }
    }
}
