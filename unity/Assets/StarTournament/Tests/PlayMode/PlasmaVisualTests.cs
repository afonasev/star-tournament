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
            for(int i=0;i<10;i++){s.Tick(actions,.02f);ground.SendMessage("LateUpdate");if(i==0)Capture(camera,Path.Combine(output,"06-muzzle-first-person.png"));}
            Assert.That(s.PlasmaBolts.Length,Is.GreaterThan(0));Capture(camera,Path.Combine(output,"02-green-plasma-flight.png"));
            // A separate observer camera reads the real world model, including its LODGroup.
            var observer=new GameObject("FORGE observer").AddComponent<Camera>();SceneManager.MoveGameObjectToScene(observer.gameObject,scene);observer.CopyFrom(camera);observer.cullingMask=~(15<<15);observer.targetTexture=null;
            File.WriteAllLines(Path.Combine(output,"effect-layers.txt"),ground.GetComponentsInChildren<Renderer>(true).Where(r=>r.transform.GetComponentsInParent<Transform>().Any(t=>t.name=="FORGE energy sphere")).Select(r=>r.name+" layer="+r.gameObject.layer+" active="+r.gameObject.activeInHierarchy+" position="+r.transform.position+" scale="+r.transform.lossyScale));
            observer.transform.position=new Vector3(-1,5.4f,-2.4f);observer.transform.LookAt(new Vector3(-1,5.4f,0));Capture(observer,Path.Combine(output,"05-plasma-flight-side.png"));
            var orb=s.PlasmaBolts.First();observer.fieldOfView=32;observer.transform.position=orb.Position+new Vector3(0,.22f,-1.2f);observer.transform.LookAt(orb.Position);Capture(observer,Path.Combine(output,"07-orb-close.png"));observer.fieldOfView=camera.fieldOfView;
            observer.transform.position=s.Pose(1).Position+new Vector3(-2,1.5f,-2);observer.transform.LookAt(s.Pose(1).Position+Vector3.up*1.1f);Capture(observer,Path.Combine(output,"03-world-weapon.png"));
            var pickup=s.WeaponPickups.First(p=>p.Weapon==WeaponId.Plasma);observer.transform.position=pickup.Anchor+new Vector3(2,2,2);observer.transform.LookAt(pickup.Anchor+Vector3.up);Capture(observer,Path.Combine(output,"04-map-pickup.png"));
            // Same runtime meshes, inspected from multiple sides without changing the rig.
            var held=guns.First(t=>t.gameObject.layer>=15);
            var center=held.TransformPoint(new Vector3(0,.12f,.32f));
            observer.cullingMask=1<<held.gameObject.layer;observer.clearFlags=CameraClearFlags.SolidColor;observer.backgroundColor=new Color(.07f,.085f,.1f);observer.fieldOfView=38;observer.nearClipPlane=.01f;
            var directions=new[]{new Vector3(1,.4f,.75f),new Vector3(-1,.3f,.6f),new Vector3(0,.9f,.2f),new Vector3(.8f,-.6f,.3f)};
            for(int v=0;v<directions.Length;v++)
            {
                observer.transform.position=center+held.TransformDirection(directions[v]).normalized*1.15f;
                observer.transform.LookAt(center,held.up);Capture(observer,Path.Combine(output,"grip-"+v+".png"));
            }
            File.WriteAllLines(Path.Combine(output,"grip-bones.txt"),ground.GetComponentsInChildren<Transform>(true).Where(t=>t.gameObject.layer==held.gameObject.layer&&(t.name.Contains("Hand")||t.name.Contains("Proximal")||t.name.Contains("Intermediate")||t.name.Contains("Distal"))).Select(t=>t.name+" localToGun="+held.InverseTransformPoint(t.position).ToString("F4")));
            // Capture actual contact after advancing the existing scene, with no fabricated VFX.
            observer.CopyFrom(camera);observer.cullingMask=~(15<<15);observer.targetTexture=null;
            observer.transform.position=new Vector3(4.7f,5.4f,-2);observer.transform.LookAt(new Vector3(5.7f,5.4f,0));
            PlasmaBoltContact lastHit=default;bool hit=false,contactCaptured=false;
            Action<PlasmaBoltContact> observeHit=c=>{lastHit=c;hit=true;};s.PlasmaBoltHit+=observeHit;
            for(int step=0;step<30;step++)
            {
                s.Tick(new LocalAction[s.ParticipantCount],.02f);ground.SendMessage("LateUpdate");
                if(hit&&!contactCaptured)
                {
                    var normal=lastHit.Contact.Normal.sqrMagnitude>.001f?lastHit.Contact.Normal:-lastHit.Bullet.Direction;
                    observer.fieldOfView=42;observer.transform.position=lastHit.Contact.Endpoint+normal*.85f+Vector3.up*.25f+Vector3.Cross(normal,Vector3.up)*.3f;
                    observer.transform.LookAt(lastHit.Contact.Endpoint);Capture(observer,Path.Combine(output,"08-contact-close.png"));contactCaptured=true;
                }
                if(step==7||step==12||step==17)Capture(observer,Path.Combine(output,"impact-"+step+".png"));
            }
            s.PlasmaBoltHit-=observeHit;
            UnityEngine.Object.Destroy(observer.gameObject);
            File.WriteAllText(Path.Combine(output,"visual-state.json"),JsonUtility.ToJson(s.Capture(),true));
        }
    }
}
