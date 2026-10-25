using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class ShotgunWeightPresentationTests
    {
        Scene scene;Gamepad pad;
#if UNITY_EDITOR
        bool previousAsyncCompilation;
#endif
        [UnityTest]public IEnumerator ShotgunUsesNeutralPelletsAndCapturesRenderedShot()
        {
#if UNITY_EDITOR
            previousAsyncCompilation=UnityEditor.ShaderUtil.allowAsyncCompilation;
            UnityEditor.ShaderUtil.allowAsyncCompilation=false;
#endif
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            var ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();
            Assert.That(ground.CaptureLabBundle().Get("shot.spread"),Is.EqualTo(4.8f));
            ground.enabled=false;pad=InputSystem.AddDevice<Gamepad>();ground.StartCombatReview(new[]{pad},ensureOpponent:true);
            ground.PlaceCombatReviewSeat(0,new Vector3(0,-1.2f,-5),0);
            ground.PlaceCombatReviewSeat(1,new Vector3(5,-1.2f,5),0,180);
            var snap=ground.Session.Capture();snap.Lives[0].ShotgunOwned=true;snap.Lives[0].ShotgunAmmo=20;ground.Session.Restore(snap);
            var action=new LocalAction[ground.Session.Capture().Lives.Length];action[0].SelectWeapon=WeaponSelection.Shotgun;
            ground.Session.Tick(action,.02f);action[0]=default;
            for(int tick=0;tick<60;tick++)ground.Session.Tick(action,.02f);
            ground.SendMessage("RefreshInterface");ground.SendMessage("LateUpdate");
            action[0].Fire=true;ground.Session.Tick(action,.02f);
            var before=JsonUtility.ToJson(ground.Session.Capture());ground.SendMessage("LateUpdate");
            Assert.That(JsonUtility.ToJson(ground.Session.Capture()),Is.EqualTo(before));
            var traces=ground.GetComponentsInChildren<Renderer>().Where(r=>r.name=="pellet-streak").ToArray();
            Assert.That(traces.Length,Is.EqualTo(18));
            foreach(var trace in traces)
            {
                Assert.That(trace.enabled,Is.True);Assert.That(trace.sharedMaterial.color,Is.EqualTo(new Color(.32f,.32f,.32f,1)));
                Assert.That(trace.GetComponents<Collider>().Any(c=>c.enabled),Is.False);
            }
            var gun=ground.GetComponentsInChildren<Renderer>(true).First(r=>r.name=="weapon:joined"&&r.enabled);
            foreach(var name in new[]{"vector-chassis","vector-titanium"})
            {
                var material=gun.sharedMaterials.Single(m=>m.name==name);
                Assert.That(material.GetTexturePropertyNames().Any(p=>p.ToLowerInvariant().Contains("basecolor")&&material.GetTexture(p)&&material.GetTexture(p).name.Contains("restored-shotgun-metal")),Is.True,name+" must retain the exact restored metal map at runtime");
            }
            string output=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_SHOTGUN_CAPTURE");
            if(!string.IsNullOrEmpty(output))
            {
                Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null));
                var camera=ground.GetComponentsInChildren<Camera>().First(c=>c.name=="seat-camera-1");
                var target=new RenderTexture(1280,720,24);var prior=camera.targetTexture;var active=RenderTexture.active;
                var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
                try
                {
                    camera.targetTexture=target;camera.Render();yield return null;camera.Render();yield return null;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllBytes(output,image.EncodeToPNG());
                    var textured=image.GetPixels32();
                    var viewGun=camera.GetComponentsInChildren<Renderer>(true).Single(r=>r.name=="weapon:joined");
                    var materials=viewGun.sharedMaterials.Where(m=>m.name=="vector-chassis"||m.name=="vector-titanium").ToArray();
                    var maps=materials.Select(m=>m.GetTexture("baseColorTexture")).ToArray();
                    foreach(var material in materials)TestContext.WriteLine(material.name+" shader="+material.shader.name+" map="+material.GetTexture("baseColorTexture")?.name);
                    try
                    {
                        foreach(var material in materials)material.SetTexture("baseColorTexture",Texture2D.whiteTexture);
                        camera.Render();image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();var plain=image.GetPixels32();
                        int changed=0;for(int i=0;i<plain.Length;i++)if(!plain[i].Equals(textured[i]))changed++;
                        File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(output),"shotgun-untextured-control.png"),image.EncodeToPNG());
                        TestContext.WriteLine("Shotgun texture affected pixels="+changed);Assert.That(changed,Is.GreaterThan(0),"Assigned metal map must affect the rendered first-person gun");
                    }
                    finally{for(int i=0;i<materials.Length;i++)materials[i].SetTexture("baseColorTexture",maps[i]);}
                    camera.Render();image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
                    CollectionAssert.AreEqual(textured,image.GetPixels32(),"Restoring the map must reproduce the textured frame exactly");
                }
                finally{camera.targetTexture=prior;RenderTexture.active=active;UnityEngine.Object.Destroy(image);target.Release();UnityEngine.Object.Destroy(target);}
            }
        }
        [UnityTearDown]public IEnumerator Cleanup()
        {
#if UNITY_EDITOR
            UnityEditor.ShaderUtil.allowAsyncCompilation=previousAsyncCompilation;
#endif
            if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
