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
        [Test] public void ProfileAndAttributionAreComplete()
        {
            var profile=ProvingProfile.CreateTrooperDefault();Assert.That(profile.Validate(),Is.Empty);
            foreach(var d in profile.Descriptors) { Assert.That(d.Validate(out _),Is.True); profile.Set(d.Path,d.Maximum+d.Step); Assert.That(profile.Validate(),Is.Not.Empty);profile.Set(d.Path,d.DefaultValue); }
            string license=File.ReadAllText("Assets/StreamingAssets/trooper-ATTRIBUTION.txt");
            Assert.That(license,Does.Contain("ART_LOLL"));Assert.That(license,Does.Contain("creativecommons.org/licenses/by/4.0/"));
        }
    }
}
