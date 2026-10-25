using System.Collections;
using System.Linq;
using UnityEngine.SceneManagement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class SlopeGroundingTests
    {
        [UnityTest] public IEnumerator WalkingAlongRampKeepsGroundContactWithoutJumpTransitions()
        {
            foreach(float angle in new[]{15f,30f})
            foreach(float dt in new[]{1f/60f,1f/30f})
            foreach(float direction in new[]{1f,-1f})
            {
                var ramp=GameObject.CreatePrimitive(PrimitiveType.Cube);
                ramp.layer=ProvingArena.WorldLayer;
                ramp.transform.position=new Vector3(80,10,80);
                ramp.transform.localScale=new Vector3(12,1,100);
                ramp.transform.rotation=Quaternion.Euler(-angle,0,0);
                var body=new GameObject("slope grounding motor");
                body.layer=ProvingArena.ParticipantLayer;
                var motor=body.AddComponent<CharacterMotor>();
                Physics.SyncTransforms();
                try
                {
                    Assert.That(Physics.Raycast(ramp.transform.position+Vector3.up*5,Vector3.down,out var hit,10,1<<ProvingArena.WorldLayer),Is.True);
                    motor.Initialize(ProvingProfile.CreateDefault(),hit.point+Vector3.up*.05f);
                    for(int tick=0;tick<60;tick++)motor.Tick(default,dt);
                    Assert.That(motor.State.Grounded,Is.True);
                    var start=motor.State.Position;
                    for(int tick=0;tick<90;tick++)
                    {
                        motor.Tick(new LocalAction{Move=Vector2.up*direction},dt);
                        Assert.That(motor.AcceptedJumpThisTick,Is.False);
                        Assert.That(motor.State.Grounded,Is.True,$"angle={angle} dt={dt} direction={direction} tick={tick} pose={JsonUtility.ToJson(motor.State)}");
                    }
                    Assert.That((motor.State.Position.y-start.y)*direction,Is.GreaterThan(1),"Must actually traverse the incline");
                    motor.Tick(new LocalAction{Jump=true},dt);
                    Assert.That(motor.AcceptedJumpThisTick,Is.True);
                    Assert.That(motor.State.Grounded,Is.False,"A real jump must leave the ramp");
                    bool landed=false;
                    for(int tick=0;tick<180;tick++)
                    {
                        motor.Tick(default,dt);
                        if(motor.State.Grounded){landed=true;break;}
                    }
                    Assert.That(landed,Is.True,"Real jumps still land");
                }
                finally{Object.DestroyImmediate(body);Object.DestroyImmediate(ramp);}
            }
            yield return null;
        }
        [UnityTest] public IEnumerator RampWalkAnimationStaysGroundedAndWalkingOffEdgeFalls()
        {
            yield return NativeLoadingTestScene.Load();
            var scene=SceneManager.GetSceneByName("ProvingGround");
            var ground=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<ProvingGround>()).Single();
            var ramp=GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.layer=ProvingArena.WorldLayer;
            ramp.transform.position=new Vector3(300,20,300);
            ramp.transform.localScale=new Vector3(8,1,30);
            ramp.transform.rotation=Quaternion.Euler(-15,0,0);
            var actor=new GameObject("slope visual motor");actor.layer=ProvingArena.ParticipantLayer;
            try
            {
                Physics.SyncTransforms();
                Assert.That(Physics.Raycast(ramp.transform.position+Vector3.up*5,Vector3.down,out var hit,10,1<<ProvingArena.WorldLayer),Is.True);
                var motor=actor.AddComponent<CharacterMotor>();var profile=ProvingProfile.CreateDefault();
                motor.Initialize(profile,hit.point+Vector3.up*.05f);
                var body=Object.Instantiate(ground.TrooperBodyPrefab,actor.transform);
                body.transform.localScale=Vector3.one*(profile.Get("player.capsule.height")/1.8f);
                var visual=body.AddComponent<TrooperVisual>();visual.Initialize(ground.TrooperClips,ProvingProfile.CreateTrooperDefault());
                const float dt=1f/60f;
                for(int tick=0;tick<60;tick++)motor.Tick(default,dt);
                visual.ResetPose(0);
                for(int tick=0;tick<70;tick++)
                {
                    motor.Tick(new LocalAction{Move=Vector2.up},dt);
                    visual.Render((tick+1)*dt,motor.State.Velocity,100,1,motor.State.Grounded);
                    Assert.That(motor.State.Grounded,Is.True);
                    Assert.That(visual.AirborneState,Is.EqualTo("grounded"));
                }
                Capture(body);
                bool airborne=false;
                for(int tick=70;tick<240;tick++)
                {
                    motor.Tick(new LocalAction{Move=Vector2.up},dt);
                    if(!motor.State.Grounded){airborne=true;Assert.That(motor.State.Velocity.y,Is.LessThan(0));break;}
                }
                Assert.That(airborne,Is.True,"Ground support must end at the edge");
                for(int tick=0;tick<30;tick++)motor.Tick(default,dt);
                Assert.That(motor.State.Grounded,Is.False);
                Assert.That(motor.State.Velocity.y,Is.LessThan(-1));
            }
            finally
            {
                Object.DestroyImmediate(actor);Object.DestroyImmediate(ramp);
            }
            yield return SceneManager.UnloadSceneAsync(scene);
        }
        static void Capture(GameObject body)
        {
            var directory=System.Environment.GetEnvironmentVariable("STAR_TOURNAMENT_SLOPE_EVIDENCE");
            if(string.IsNullOrEmpty(directory))return;
            System.IO.Directory.CreateDirectory(directory);
            var owner=new GameObject("slope evidence camera");var camera=owner.AddComponent<Camera>();
            var center=body.transform.position+Vector3.up;
            owner.transform.position=center+new Vector3(3,1,-4);owner.transform.LookAt(center);
            camera.fieldOfView=45;camera.nearClipPlane=.05f;camera.farClipPlane=20;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.19f,.23f);
            var light=owner.AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;
            var target=new RenderTexture(960,720,24);var pixels=new Texture2D(960,720,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;
            try
            {
                target.Create();camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,960,720),0,0);pixels.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory,"walking-up-ramp.png"),pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active=previous;camera.targetTexture=null;target.Release();
                Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);Object.DestroyImmediate(owner);
            }
        }
    }
}
