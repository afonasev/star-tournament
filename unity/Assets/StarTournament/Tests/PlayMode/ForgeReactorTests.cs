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
    public sealed class ForgeReactorTests
    {
        Scene scene;Gamepad pad,secondPad;ProvingGround ground;
        [UnityTearDown]public IEnumerator Cleanup(){if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);if(secondPad!=null&&secondPad.added)InputSystem.RemoveDevice(secondPad);if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);}
        IEnumerator Setup(bool twoViews=false)
        {
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");
            ground=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ProvingGround>()).Single();
            pad=InputSystem.AddDevice<Gamepad>();if(twoViews)secondPad=InputSystem.AddDevice<Gamepad>();ground.StartCombatReview(twoViews?new[]{pad,secondPad}:new[]{pad},backgroundDiagnostic:true,ensureOpponent:true);
            Assert.That(ground.Running,Is.True,ground.CombatReviewDiagnostic());ground.enabled=false;
            ground.PlaceCombatReviewSeat(0,new Vector3(-6,4,0),0,90);ground.PlaceCombatReviewSeat(1,new Vector3(6,4,0),0,270);
            var state=ground.Session.Capture();
            for(int i=0;i<state.Lives.Length;i++){state.Lives[i].PlasmaOwned=true;state.Lives[i].PlasmaAmmo=100;state.Lives[i].SelectedWeapon=WeaponId.Plasma;state.Lives[i].Ammo=100;}
            ground.Session.Restore(state);ground.SendMessage("LateUpdate");yield return null;
        }
        static void Capture(Camera camera,string path,int width,int height)
        {
            var target=new RenderTexture(width,height,24);var old=camera.targetTexture;var rect=camera.rect;var active=RenderTexture.active;
            var image=new Texture2D(width,height,TextureFormat.RGB24,false);
            try{camera.rect=new Rect(0,0,1,1);camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{camera.targetTexture=old;camera.rect=rect;RenderTexture.active=active;UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(target);}
        }

        static Vector4 Clock(Renderer surface)
        {var block=new MaterialPropertyBlock();surface.GetPropertyBlock(block,0);return block.GetVector("_ReactorClock");}
        Renderer[] Lenses()=>ground.GetComponentsInChildren<Renderer>(true).Where(r=>r.sharedMaterials.Any(m=>m&&m.name=="FORGE session reactor lens")).ToArray();
        [UnityTest]public IEnumerator SessionClockActualFireRestoreSwitchDeathAndTwoViews()
        {
            yield return Setup(true);var session=ground.Session;var effect=ground.ForgeReactorForReview;
            Assert.That(effect,Is.Not.Null);Assert.That(effect.SurfaceCount,Is.GreaterThanOrEqualTo(8));
            var views=Lenses().Where(r=>r.gameObject.layer>=15).OrderBy(r=>r.gameObject.layer).ToArray();
            Assert.That(views.Length,Is.EqualTo(2));Assert.That(views[0].sharedMaterial,Is.SameAs(views[1].sharedMaterial));
            // Startup input gate requires a real released tick before the next press.
            session.Tick(new LocalAction[session.ParticipantCount],.02f);ground.SendMessage("LateUpdate");
            var before=session.Capture();string authority=JsonUtility.ToJson(before);effect.Render();effect.Render();
            Assert.That(JsonUtility.ToJson(session.Capture()),Is.EqualTo(authority));
            var actions=new LocalAction[session.ParticipantCount];actions[0].FireHeld=true;
            session.Tick(actions,.02f);ground.SendMessage("LateUpdate");
            Assert.That(session.ShotCount,Is.EqualTo(before.ShotCount+1));Assert.That(effect.ShotPulse(0),Is.GreaterThan(.9f));Assert.That(effect.ShotPulse(1),Is.Zero);
            session.Tick(new LocalAction[session.ParticipantCount],.02f);ground.SendMessage("LateUpdate");
            var saved=session.Capture();var savedClock=Clock(views[0]);Assert.That(savedClock.y,Is.InRange(.1f,.99f));Assert.That(Clock(views[1]).y,Is.Zero);
            yield return new WaitForSecondsRealtime(.1f);ground.SendMessage("LateUpdate");
            Assert.That(Clock(views[0]),Is.EqualTo(savedClock),"Wall clock must not advance paused energy");
            for(int n=0;n<15;n++)session.Tick(new LocalAction[session.ParticipantCount],.02f);
            ground.SendMessage("LateUpdate");Assert.That(effect.ShotPulse(0),Is.Zero);
            session.Restore(saved);ground.SendMessage("LateUpdate");Assert.That(Clock(views[0]),Is.EqualTo(savedClock),"Restore reconstructs a mid-shot pulse and flow phase");
            actions=new LocalAction[session.ParticipantCount];actions[1].FireHeld=true;session.Tick(actions,.02f);ground.SendMessage("LateUpdate");
            Assert.That(effect.ShotPulse(1),Is.GreaterThan(.9f));Assert.That(Clock(views[0]).y,Is.LessThan(Clock(views[1]).y));
            actions=new LocalAction[session.ParticipantCount];actions[0].SelectWeapon=WeaponSelection.Rifle;session.Tick(actions,.02f);
            for(int n=0;n<40;n++)session.Tick(new LocalAction[session.ParticipantCount],.02f);
            ground.SendMessage("LateUpdate");Assert.That(session.Life(0).SelectedWeapon,Is.EqualTo(WeaponId.Rifle));Assert.That(effect.ShotPulse(0),Is.Zero);
            session.Restore(saved);session.ApplyDamage(0,session.Life(0).Life,1000);ground.SendMessage("LateUpdate");
            Assert.That(session.Life(0).Dead,Is.True);Assert.That(effect.ShotPulse(0),Is.Zero);
            session.Restore(before);ground.SendMessage("LateUpdate");Assert.That(Clock(views[0]).y,Is.Zero);
            Assert.That(Clock(views[0]).x,Is.EqualTo((float)before.Time));
        }
        [UnityTest]public IEnumerator MaterialsAreSharedAndDisposedWithoutExtraLightsOrMeshes()
        {
            yield return Setup(true);var effect=ground.ForgeReactorForReview;var lens=effect.LensMaterial;var flux=effect.FluxMaterial;
            int lightCount=ground.GetComponentsInChildren<Light>(true).Length;
            var meshes=ground.GetComponentsInChildren<MeshFilter>(true).Select(m=>m.sharedMesh).ToArray();
            for(int i=0;i<100;i++)effect.Render();
            Assert.That(ground.GetComponentsInChildren<MeshFilter>(true).Select(m=>m.sharedMesh),Is.EqualTo(meshes));
            Assert.That(ground.GetComponentsInChildren<Light>(true).Length,Is.EqualTo(lightCount));
            Assert.That(Resources.FindObjectsOfTypeAll<Material>().Count(m=>m.name=="FORGE session reactor lens"||m.name=="FORGE session reactor flux"),Is.EqualTo(2));
            effect.Dispose();effect.Dispose();effect.Render();yield return null;
            Assert.That(lens==null&&flux==null,Is.True);Assert.That(effect.SurfaceCount,Is.Zero);
            Assert.That(ground.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Any(m=>m&&(m==lens||m==flux)),Is.False);
        }
        [UnityTest]public IEnumerator ReactorMotionCapture()
        {
            var output=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_REACTOR_EVIDENCE");Assert.That(output,Is.Not.Null.And.Not.Empty);
            Directory.CreateDirectory(output);Directory.CreateDirectory(Path.Combine(output,"fps"));Directory.CreateDirectory(Path.Combine(output,"rear"));
            yield return Setup();var s=ground.Session;
            var camera=ground.GetComponentsInChildren<Camera>().Where(c=>c.enabled&&c.targetTexture==null).OrderBy(c=>c.depth).First();
            var held=ground.GetComponentsInChildren<Transform>(true).First(t=>t.name=="forge-plasma"&&t.gameObject.layer>=15);
            var rear=new GameObject("reactor review camera").AddComponent<Camera>();SceneManager.MoveGameObjectToScene(rear.gameObject,scene);rear.CopyFrom(camera);
            rear.cullingMask=1<<held.gameObject.layer;rear.clearFlags=CameraClearFlags.SolidColor;rear.backgroundColor=new Color(.025f,.035f,.04f);rear.fieldOfView=35;rear.nearClipPlane=.01f;rear.enabled=false;
            var reactor=held.GetComponentsInChildren<Renderer>(true).Single(r=>r.sharedMaterials.Any(m=>m&&m.name=="FORGE session reactor lens"));
            Assert.That(reactor.sharedMaterial.shader.isSupported,Is.True);
            Assert.That(reactor.sharedMaterial.GetTexture("_FlowMap").width,Is.EqualTo(512));
            File.WriteAllLines(Path.Combine(output,"surfaces.txt"),held.GetComponentsInChildren<Renderer>(true).Select(r=>r.name+" parent="+r.transform.parent.name+" modelCenter="+held.InverseTransformPoint(r.bounds.center)+" materials="+string.Join(",",r.sharedMaterials.Select(m=>m.name))));
            for(int warm=0;warm<3;warm++){camera.Render();yield return null;}
            var rows=new System.Collections.Generic.List<string>{"frame,sessionTime,shots,paused"};
            for(int frame=0;frame<144;frame++)
            {
                bool paused=frame>=96&&frame<120;
                if(!paused){var actions=new LocalAction[s.ParticipantCount];actions[0].FireHeld=frame>=60&&frame<72;s.Tick(actions,1f/24);}
                ground.SendMessage("LateUpdate");
                var center=reactor.bounds.center;
                rear.transform.position=center+held.TransformDirection(new Vector3(.07f,.025f,-.48f));rear.transform.LookAt(center,held.up);
                Capture(camera,Path.Combine(output,"fps",frame.ToString("D4")+".png"),1280,720);
                Capture(rear,Path.Combine(output,"rear",frame.ToString("D4")+".png"),640,640);
                rows.Add(frame+","+s.Time.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+s.ShotCount+","+paused);
                yield return null;
            }
            File.WriteAllLines(Path.Combine(output,"timeline.csv"),rows);
            Assert.That(s.ShotCount,Is.GreaterThan(0));
            var longSession=s.Capture();longSession.Time=3600;s.Restore(longSession);ground.SendMessage("LateUpdate");
            var longCenter=reactor.bounds.center;rear.transform.position=longCenter+held.TransformDirection(new Vector3(.07f,.025f,-.48f));rear.transform.LookAt(longCenter,held.up);
            Capture(rear,Path.Combine(output,"one-hour-reactor.png"),640,640);
            UnityEngine.Object.Destroy(rear.gameObject);
        }
    }
}
