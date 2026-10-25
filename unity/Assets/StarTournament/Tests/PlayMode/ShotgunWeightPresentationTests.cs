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
        [UnityTest]public IEnumerator ShotgunUsesBronzePelletsAndCapturesRenderedShot()
        {
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
                Assert.That(trace.enabled,Is.True);Assert.That(trace.sharedMaterial.color,Is.EqualTo(new Color(.46f,.30f,.14f,1)));
                Assert.That(trace.GetComponents<Collider>().Any(c=>c.enabled),Is.False);
            }
            string output=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_SHOTGUN_CAPTURE");
            if(!string.IsNullOrEmpty(output))
            {
                Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null));
                var camera=ground.GetComponentsInChildren<Camera>().First(c=>c.name=="seat-camera-1");
                var target=new RenderTexture(1280,720,24);var prior=camera.targetTexture;var active=RenderTexture.active;
                var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
                try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllBytes(output,image.EncodeToPNG());}
                finally{camera.targetTexture=prior;RenderTexture.active=active;UnityEngine.Object.Destroy(image);target.Release();UnityEngine.Object.Destroy(target);}
            }
        }
        [UnityTearDown]public IEnumerator Cleanup(){if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);}
    }
}
