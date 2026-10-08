using System.Collections;
using System.Linq;
using UnityEngine.SceneManagement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class GamepadLookAssistanceTests
    {
        [UnityTest] public IEnumerator StationaryStairLookFollowsMarchUpDownAndAcross()
        {
            var root=new GameObject("stair assistance fixture");var arena=root.AddComponent<ProvingArena>();
            var p=ProvingProfile.CreateDefault();arena.Build(CombatBowlCatalog.Freeze(p),p);yield return null;
            var transition=arena.ReadNavigationTransitions().First(x=>x.Id.Contains("outer-rise"));
            var feet=transition.OrderedFeet;var delta=feet[feet.Length-1]-feet[0];var mid=feet[feet.Length/2];
            float yaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;
            var body=new GameObject("stationary stair motor");var motor=body.AddComponent<CharacterMotor>();
            try
            {
                for(int direction=0;direction<3;direction++)
                {
                    motor.Initialize(p,mid,arena);motor.Tick(new LocalAction{LookDegrees=new Vector2(yaw+(direction==1?180:direction==2?90:0),-30),ManualLook=true},.02f);
                    for(int i=0;i<200;i++)motor.Tick(new LocalAction{GamepadLookAssistance=true},.02f);
                    if(direction==0)Assert.That(motor.State.Pitch,Is.LessThan(-10),JsonUtility.ToJson(motor.State));
                    else if(direction==1)Assert.That(motor.State.Pitch,Is.GreaterThan(10));
                    else Assert.That(motor.State.Pitch,Is.EqualTo(0).Within(.1f));
                    Assert.That(motor.State.Grounded,Is.True);
                }
            }
            finally{Object.Destroy(body);Object.Destroy(root);}
            yield return null;
        }
        [UnityTest] public IEnumerator ReturnIsDelayedManualInputWinsAndDisabledOrAirborneLookStaysPut()
        {
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.layer=ProvingArena.WorldLayer;floor.transform.position=new Vector3(0,-.5f,0);floor.transform.localScale=new Vector3(30,1,30);
            var body=new GameObject("assistance test motor");var p=ProvingProfile.CreateDefault();
            var motor=body.AddComponent<CharacterMotor>();motor.Initialize(p,new Vector3(0,.02f,0));Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            try
            {
                for(int i=0;i<5;i++)motor.Tick(default,.02f);
                motor.Tick(new LocalAction{LookDegrees=new Vector2(35,-30),ManualLook=true,GamepadLookAssistance=true},.02f);
                var initial=motor.State;Assert.That(initial.Pitch,Is.EqualTo(30));
                var returning=new LocalAction{GamepadLookAssistance=true};
                for(int i=0;i<10;i++)motor.Tick(returning,.02f);
                Assert.That(motor.State.Pitch,Is.EqualTo(30));
                for(int i=0;i<100;i++)motor.Tick(returning,.02f);
                Assert.That(motor.State.Pitch,Is.LessThan(.2f));Assert.That(motor.State.Yaw,Is.EqualTo(35));
                motor.Tick(new LocalAction{LookDegrees=new Vector2(0,-20),ManualLook=true,GamepadLookAssistance=true},.02f);
                for(int i=0;i<50;i++)motor.Tick(new LocalAction{ManualLook=true,GamepadLookAssistance=true},.02f);
                Assert.That(motor.State.Pitch,Is.EqualTo(20).Within(.2));
                for(int i=0;i<50;i++)motor.Tick(default,.02f);
                Assert.That(motor.State.Pitch,Is.EqualTo(20).Within(.2));
                motor.Tick(new LocalAction{Jump=true,GamepadLookAssistance=true},.02f);
                var airborne=motor.State.Pitch;for(int i=0;i<5;i++)motor.Tick(returning,.02f);
                Assert.That(motor.State.Grounded,Is.False);Assert.That(motor.State.Pitch,Is.EqualTo(airborne));
                var saved=JsonUtility.FromJson<ParticipantState>(JsonUtility.ToJson(motor.State));motor.RestoreState(saved);
                Assert.That(motor.State.LookNeutralSeconds,Is.EqualTo(saved.LookNeutralSeconds));
                Assert.That(motor.State.LookReturnVelocity,Is.EqualTo(saved.LookReturnVelocity));
                for(int i=0;i<200;i++)motor.Tick(returning,.02f);
                Assert.That(motor.State.Grounded,Is.True,"return resumes after landing");
                Assert.That(motor.State.Pitch,Is.EqualTo(0).Within(.2f));
            }
            finally{Object.Destroy(body);Object.Destroy(floor);}
            yield return null;
        }
        [UnityTest] public IEnumerator ContinuousRampUsesItsActualSurfaceNormal()
        {
            var ramp=GameObject.CreatePrimitive(PrimitiveType.Cube);ramp.layer=ProvingArena.WorldLayer;
            ramp.transform.position=new Vector3(80,10,80);
            ramp.transform.localScale=new Vector3(20,1,30);ramp.transform.rotation=Quaternion.Euler(-15,0,0);
            var body=new GameObject("ramp assistance motor");var motor=body.AddComponent<CharacterMotor>();
            var profile=ProvingProfile.CreateDefault();Physics.SyncTransforms();
            var scene=SceneManager.GetActiveScene().GetPhysicsScene();
            Assert.That(scene.Raycast(ramp.transform.position+Vector3.up*5,Vector3.down,out var hit,10,1<<ProvingArena.WorldLayer),Is.True);
            motor.Initialize(profile,hit.point+Vector3.up*.02f);yield return new WaitForFixedUpdate();
            try
            {
                motor.Tick(new LocalAction{LookDegrees=new Vector2(0,-30),ManualLook=true},.02f);
                for(int i=0;i<200;i++)motor.Tick(new LocalAction{GamepadLookAssistance=true},.02f);
                Assert.That(motor.State.Grounded,Is.True);
                Assert.That(motor.State.Pitch,Is.EqualTo(CharacterMotor.SurfacePitch(hit.normal,Vector3.forward)).Within(.3f),JsonUtility.ToJson(motor.State));
            }
            finally{Object.Destroy(body);Object.Destroy(ramp);}
            yield return null;
        }
        [UnityTest] public IEnumerator LtLocksCurrentPitchKeepsYawAndTapResetsToWorldHorizon()
        {
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.layer=ProvingArena.WorldLayer;floor.transform.position=new Vector3(120,-.5f,120);floor.transform.localScale=new Vector3(30,1,30);
            var body=new GameObject("LT aim motor");var motor=body.AddComponent<CharacterMotor>();var profile=ProvingProfile.CreateDefault();
            motor.Initialize(profile,new Vector3(120,.02f,120));Physics.SyncTransforms();yield return new WaitForFixedUpdate();
            try
            {
                motor.Tick(new LocalAction{LookDegrees=new Vector2(25,-32),ManualLook=true},.02f);
                float aimedPitch=motor.State.Pitch;float aimedYaw=motor.State.Yaw;
                motor.Tick(new LocalAction{LookDegrees=new Vector2(18,45),GamepadLookLocked=true,GamepadLookAssistance=true},.02f);
                Assert.That(motor.State.Pitch,Is.EqualTo(aimedPitch),"LT ignores vertical action input and auto-level");
                Assert.That(motor.State.Yaw,Is.EqualTo(aimedYaw+18).Within(.001),"LT keeps horizontal stick control");
                Assert.That(motor.State.LookNeutralSeconds,Is.Zero);Assert.That(motor.State.LookReturnVelocity,Is.Zero);
                for(int tick=0;tick<10;tick++)motor.Tick(new LocalAction{GamepadLookAssistance=true},.02f);
                Assert.That(motor.State.Pitch,Is.EqualTo(aimedPitch),"after LT release auto-level waits for the existing delay");
                var beforeResetYaw=motor.State.Yaw;
                motor.Tick(new LocalAction{ResetLookPitch=true,GamepadLookAssistance=false},.02f);
                Assert.That(motor.State.Pitch,Is.Zero,"tap sets world horizon independently of surface assistance");
                Assert.That(motor.State.Yaw,Is.EqualTo(beforeResetYaw));
            }
            finally{Object.Destroy(body);Object.Destroy(floor);}
            yield return null;
        }
    }
}
