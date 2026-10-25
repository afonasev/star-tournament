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
    public sealed class PlasmaVisualTests
    {
        Scene scene;Gamepad pad;
        [UnityTearDown]public IEnumerator Cleanup(){if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);}
        static void Capture(Camera camera,string path)
        {
            var target=new RenderTexture(1600,900,24);var prior=camera.targetTexture;var rect=camera.rect;var active=RenderTexture.active;
            var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
            try{camera.rect=new Rect(0,0,1,1);camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{camera.targetTexture=prior;camera.rect=rect;RenderTexture.active=active;UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(target);}
        }
        [UnityTest]public IEnumerator ForgeViewmodelWorldFlightAndPickupRender()
        {
            string output=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_FORGE_EVIDENCE");
            Assert.That(output,Is.Not.Null.And.Not.Empty,"Run this visual fixture with an explicit owned output directory and graphics enabled.");Directory.CreateDirectory(output);
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");
            var ground=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ProvingGround>()).Single();
            Assert.That(ground.PlasmaPrefab,Is.Not.Null);Assert.That(ground.PlasmaLod1Prefab,Is.Not.Null);
            pad=InputSystem.AddDevice<Gamepad>();ground.StartCombatReview(new[]{pad},backgroundDiagnostic:true,ensureOpponent:true);Assert.That(ground.Running,Is.True,ground.CombatReviewDiagnostic());ground.enabled=false;
            var s=ground.Session;ground.PlaceCombatReviewSeat(0,new Vector3(-6,4,0),0,90);ground.PlaceCombatReviewSeat(1,new Vector3(6,4,0),0,270);
            var state=s.Capture();for(int i=0;i<state.Lives.Length;i++){state.Lives[i].PlasmaOwned=true;state.Lives[i].PlasmaAmmo=100;state.Lives[i].SelectedWeapon=WeaponId.Plasma;state.Lives[i].Ammo=100;}s.Restore(state);
            s.Tick(new LocalAction[s.ParticipantCount],.02f);ground.SendMessage("LateUpdate");yield return null;
            var camera=ground.GetComponentsInChildren<Camera>().Where(c=>c.enabled&&c.targetTexture==null).OrderBy(c=>c.depth).First();
            for(int warm=0;warm<3;warm++){camera.Render();yield return null;ground.SendMessage("LateUpdate");}
            var guns=ground.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="forge-plasma").ToArray();
            Assert.That(guns.Any(t=>t.gameObject.layer>=15),Is.True);
            Assert.That(guns.Any(t=>t.gameObject.layer<15),Is.True);
            foreach(var gun in guns)
                Assert.That(gun.localScale.x,Is.EqualTo(ground.PlasmaProfile.Get("plasma.modelScale")*(gun.gameObject.layer>=15?ground.PlasmaProfile.Get("plasma.viewScale"):1)).Within(.0001f));
            File.WriteAllLines(Path.Combine(output,"model-scales.txt"),guns.Select(t=>"layer="+t.gameObject.layer+" scale="+t.localScale));
            Capture(camera,Path.Combine(output,"01-first-person.png"));
            var actions=new LocalAction[s.ParticipantCount];actions[0].FireHeld=true;
            for(int i=0;i<10;i++){s.Tick(actions,.02f);ground.SendMessage("LateUpdate");}
            Assert.That(s.PlasmaBolts.Length,Is.GreaterThan(0));Capture(camera,Path.Combine(output,"02-green-plasma-flight.png"));
            // A separate observer camera reads the real world model, including its LODGroup.
            var observer=new GameObject("FORGE observer").AddComponent<Camera>();SceneManager.MoveGameObjectToScene(observer.gameObject,scene);observer.CopyFrom(camera);observer.cullingMask=~(15<<15);observer.targetTexture=null;
            File.WriteAllLines(Path.Combine(output,"effect-layers.txt"),ground.GetComponentsInChildren<Renderer>(true).Where(r=>r.transform.GetComponentsInParent<Transform>().Any(t=>t.name=="FORGE energy sphere")).Select(r=>r.name+" layer="+r.gameObject.layer+" active="+r.gameObject.activeInHierarchy+" position="+r.transform.position+" scale="+r.transform.lossyScale));
            observer.transform.position=new Vector3(-1,5.4f,-2.4f);observer.transform.LookAt(new Vector3(-1,5.4f,0));Capture(observer,Path.Combine(output,"05-plasma-flight-side.png"));
            observer.transform.position=s.Pose(1).Position+new Vector3(-2,1.5f,-2);observer.transform.LookAt(s.Pose(1).Position+Vector3.up*1.1f);Capture(observer,Path.Combine(output,"03-world-weapon.png"));
            var pickup=s.WeaponPickups.First(p=>p.Weapon==WeaponId.Plasma);observer.transform.position=pickup.Anchor+new Vector3(2,2,2);observer.transform.LookAt(pickup.Anchor+Vector3.up);Capture(observer,Path.Combine(output,"04-map-pickup.png"));
            UnityEngine.Object.Destroy(observer.gameObject);
            File.WriteAllText(Path.Combine(output,"visual-state.json"),JsonUtility.ToJson(s.Capture(),true));
        }
    }
}
