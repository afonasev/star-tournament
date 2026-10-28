using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class VectorAirbornePresentationTests
    {
        ProvingGround ground;Scene scene;Gamepad pad;
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);
            if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);
        }
        [UnityTest] public IEnumerator AirborneBodyAndViewFreezeLandAndResetWithoutMovingMotor()
        {
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ProvingGround>()).Single();
            pad=InputSystem.AddDevice<Gamepad>();ground.StartCombatReview(new[]{pad});yield return null;
            typeof(ProvingGround).GetField("FullHealReviewManualTick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(ground,true);
            var body=ground.GetComponentsInChildren<TrooperVisual>(true).First(v=>v.name=="trooper-presentation");
            var view=ground.GetComponentsInChildren<TrooperVisual>(true).First(v=>v.name=="trooper-view");
            var knee=body.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="LeftLowerLeg");
            var hips=body.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Hips");
            var actor=body.transform.parent;var position=actor.position;
            body.ResetPose(0);view.ResetPose(0);body.Render(.1,Vector3.zero,100,1);view.Render(.1,Vector3.zero,100,1);
            var grounded=knee.localRotation;var groundedHipHeight=hips.position.y;
            Capture(body,"grounded");
            foreach(var v in new[]{body,view})v.Render(.25,new Vector3(2,5,0),100,1,false);
            Assert.That(body.AirborneState,Is.EqualTo("rising"));
            Assert.That(Quaternion.Angle(grounded,knee.localRotation),Is.GreaterThan(5));
            Assert.That(view.ViewOffset.y,Is.LessThan(0));
            Assert.That(view.WalkOffset.magnitude,Is.LessThan(.00001f),"Air pose must not be reported as ground steps");
            Capture(body,"rising");
            var frozen=knee.localRotation;var frozenView=view.ViewOffset;
            foreach(var v in new[]{body,view})v.Render(.25,new Vector3(2,5,0),100,1,false);
            Assert.That(Quaternion.Angle(frozen,knee.localRotation),Is.LessThan(.001));Assert.That(view.ViewOffset,Is.EqualTo(frozenView));
            body.Render(.55,new Vector3(2,-3,0),100,1,false);Assert.That(body.AirborneState,Is.EqualTo("falling"));
            Capture(body,"falling");
            body.Render(.7,Vector3.zero,100,1,true);body.Render(.8,Vector3.zero,100,1,true);Assert.That(body.AirborneState,Is.EqualTo("landing"));
            Assert.That(groundedHipHeight-hips.position.y,Is.GreaterThan(.005f),"Grounded stabilization must retain landing compression");
            Capture(body,"landing");
            foreach(var v in new[]{body,view}){v.BeginDeath(.9);v.Render(1,Vector3.zero,0,1,false);Assert.That(v.State,Is.EqualTo("death"));v.ResetPose(2);}
            Assert.That(view.ViewOffset,Is.EqualTo(Vector3.zero));Assert.That(body.AirborneState,Is.EqualTo("grounded"));
            Assert.That(actor.position,Is.EqualTo(position));
        }
        static void Capture(TrooperVisual body,string state)
        {
            var directory=System.Environment.GetEnvironmentVariable("STAR_TOURNAMENT_VECTOR_EVIDENCE");
            if(string.IsNullOrEmpty(directory))return;
            System.IO.Directory.CreateDirectory(directory);
            var owner=new GameObject("vector-pose-evidence-camera");var camera=owner.AddComponent<Camera>();
            var center=body.transform.position+Vector3.up;
            owner.transform.position=center+body.transform.forward*3+body.transform.right*1.6f+Vector3.up*.5f;
            owner.transform.LookAt(center);camera.fieldOfView=36;camera.nearClipPlane=.05f;camera.farClipPlane=30;
            camera.cullingMask=1|(1<<11); // World and first seat body; exclude FPS arms.
            var target=new RenderTexture(1000,1000,24);var pixels=new Texture2D(1000,1000,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;
            try
            {
                target.Create();camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,1000,1000),0,0);pixels.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory,state+".png"),pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active=previous;camera.targetTexture=null;target.Release();
                Object.Destroy(target);Object.Destroy(pixels);Object.Destroy(owner);
            }
        }
    }
}
