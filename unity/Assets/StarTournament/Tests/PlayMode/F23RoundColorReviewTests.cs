#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class F23RoundColorReviewTests
    {
        [UnityTest,Explicit("Focused current F23 color ownership and native idle/run capture; set STAR_F23_ROUND_EVIDENCE")]
        public IEnumerator CurrentWorldKeepsAssignedColorsAndNativePose()
        {
            string output=Environment.GetEnvironmentVariable("STAR_F23_ROUND_EVIDENCE");
            Assert.That(Path.IsPathFullyQualified(output??""),Is.True);Directory.CreateDirectory(output);
            Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(GraphicsDeviceType.Null));
            yield return NativeLoadingTestScene.Load();var scene=SceneManager.GetSceneByName("ProvingGround");
            var ground=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<ProvingGround>()).Single();
            var first=Object.Instantiate(ground.TrooperBodyPrefab,new Vector3(2000,2000,2000),Quaternion.identity);
            var second=Object.Instantiate(ground.TrooperBodyPrefab,new Vector3(2001,2000,2000),Quaternion.identity);
            var blue=new Color(.06f,.30f,.85f);var orange=new Color(.9f,.23f,.04f);
            void Check(GameObject root,Color expected)
            {
                int zones=0;
                foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
                for(int slot=0;slot<skin.sharedMaterials.Length;slot++)
                {
                    var material=skin.sharedMaterials[slot];if(!material.name.StartsWith("vector-fighter-identity"))continue;
                    zones++;Assert.That(material.GetColor("_IdentityColor"),Is.EqualTo(expected));
                    var block=new MaterialPropertyBlock();skin.GetPropertyBlock(block,slot);
                    Assert.That(block.GetColor("_IdentityColor"),Is.EqualTo(expected));
                    Assert.That(block.GetFloat("_IdentityMode"),Is.EqualTo(1));
                    var source=ground.TrooperBodyPrefab.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).First(m=>m.name==material.name);
                    foreach(var map in new[]{"baseColorTexture","normalTexture","metallicRoughnessTexture"})
                        Assert.That(material.GetTexture(map),Is.EqualTo(source.GetTexture(map)),"Keep authored map "+map);
                }
                Assert.That(zones,Is.GreaterThan(0));
            }
            try
            {
                TrooperIdentityPresentation.ApplyBody(first,blue);TrooperIdentityPresentation.ApplyBody(second,orange);
                Check(first,blue);Check(second,orange);
                TrooperIdentityPresentation.ApplyBody(first,orange);Check(first,orange);Check(second,orange);
                TrooperIdentityPresentation.ApplyBody(second,blue);Check(first,orange);Check(second,blue);
                yield return NativeModelPerformanceReview.CaptureWorld(ground,ground.TrooperBodyPrefab,output,true,false);
                foreach(string state in new[]{"idle","run"})foreach(string view in new[]{"front","side","back"})
                    Assert.That(File.Exists(Path.Combine(output,"world-"+state+"-"+view+".png")),Is.True);
            }
            finally{Object.Destroy(first);Object.Destroy(second);}
            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
#endif
