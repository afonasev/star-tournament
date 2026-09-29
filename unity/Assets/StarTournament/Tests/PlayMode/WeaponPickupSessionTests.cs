using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class WeaponPickupSessionTests
    {
        [UnityTest]public IEnumerator SixPhysicalAnchorsIndependentTimersRestoreAndFreshSession()
        {
            var scene=SceneManager.CreateScene("weapon-pickups-"+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            var owner=new GameObject("pickup physical fixture");SceneManager.MoveGameObjectToScene(owner,scene);
            try
            {
                var move=ProvingProfile.CreateDefault();var life=ProvingProfile.CreateCombatDefault();var combat=ProvingProfile.CreateNativeCombatDefault();
                var arena=owner.AddComponent<ProvingArena>();arena.Build(CombatBowlCatalog.Freeze(move),move);
                var motors=new CharacterMotor[2];for(int i=0;i<2;i++){var o=new GameObject("p"+i);o.transform.SetParent(owner.transform);o.layer=ProvingArena.ParticipantLayer;motors[i]=o.AddComponent<CharacterMotor>();motors[i].Initialize(move,new Vector3(0,-1.2f,i*2));}
                var session=new NativeCombatSession(motors,arena,scene.GetPhysicsScene(),move,life,combat);
                Assert.That(session.WeaponPickups.Length,Is.EqualTo(6));Assert.That(session.WeaponPickups.All(x=>x.Available),Is.True);
                var shortJump=session.WeaponPickups.Single(x=>x.InstanceId=="shotgun-west");
                motors[0].Initialize(move,shortJump.Anchor+Vector3.up*.2f);session.Tick(new LocalAction[2],.02f);
                Assert.That(session.Life(0).ShotgunOwned,Is.True,"Small jump within contact height retains named support");
                foreach(var pickup in session.WeaponPickups)
                {
                    // New life for each point proves physical unlock even when both participants contact together.
                    session=new NativeCombatSession(motors,arena,scene.GetPhysicsScene(),move,life,combat);
                    motors[0].Initialize(move,pickup.Anchor);motors[1].Initialize(move,pickup.Anchor);session.Tick(new LocalAction[2],.02f);
                    Assert.That(CombatLife.Owned(session.Life(0),pickup.Weapon),Is.True,pickup.InstanceId);
                    Assert.That(CombatLife.Owned(session.Life(1),pickup.Weapon),Is.False,"One physical instance wins once");
                    Assert.That(session.WeaponPickups.Count(x=>!x.Available),Is.EqualTo(1));
                    var snap=session.Capture();Assert.That(snap.WeaponPickups.Single(x=>x.InstanceId==pickup.InstanceId).Remaining,Is.EqualTo(15));
                    motors[0].Initialize(move,new Vector3(0,-1.2f,0));motors[1].Initialize(move,new Vector3(0,-1.2f,3));
                    for(int i=0;i<50;i++)session.Tick(new LocalAction[2],.02f);
                    session.Restore(snap);Assert.That(session.WeaponPickups.Single(x=>x.InstanceId==pickup.InstanceId).Remaining,Is.EqualTo(15));
                    Assert.That(session.Pose(0).Position,Is.EqualTo(snap.Poses[0].Position),"Complete snapshots restore motor pose as well as camera return state");
                    // The cooldown check requires both restored participants to leave the pickup.
                    motors[0].Initialize(move,new Vector3(0,-1.2f,0));motors[1].Initialize(move,new Vector3(0,-1.2f,3));
                    for(int i=0;i<750;i++)session.Tick(new LocalAction[2],.02f);session.Tick(new LocalAction[2],.02f);
                    Assert.That(session.WeaponPickups.All(x=>x.Available),Is.True);
                }
                session=new NativeCombatSession(motors,arena,scene.GetPhysicsScene(),move,life,combat);
                var shotgun=session.WeaponPickups.Single(x=>x.InstanceId=="shotgun-west");
                var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.SetParent(owner.transform);blocker.layer=ProvingArena.WorldLayer;
                blocker.transform.position=shotgun.Anchor+new Vector3(0,1,-.45f);blocker.transform.localScale=new Vector3(3,2,.1f);
                motors[0].Initialize(move,shotgun.Anchor+Vector3.back*.9f);motors[1].Initialize(move,new Vector3(0,-1.2f,3));session.Tick(new LocalAction[2],.02f);
                Assert.That(session.Life(0).ShotgunOwned,Is.False,"Solid barrier inside contact radius must prevent collection");
                Assert.That(session.WeaponPickups.Single(x=>x.InstanceId==shotgun.InstanceId).Available,Is.True);
                UnityEngine.Object.DestroyImmediate(blocker);
                // A ceiling separates the central upper support from its lower floor even at identical XZ.
                var pulse=session.WeaponPickups.First(x=>x.Weapon==WeaponId.RocketLauncher);
                motors[0].Initialize(move,new Vector3(pulse.Anchor.x,0,pulse.Anchor.z));session.Tick(new LocalAction[2],.02f);
                Assert.That(session.Life(0).RocketOwned,Is.False);Assert.That(session.WeaponPickups.Single(x=>x.InstanceId==pulse.InstanceId).Available,Is.True);
                var repeated=new NativeCombatSession(motors,arena,scene.GetPhysicsScene(),move,life,combat);
                Assert.That(repeated.WeaponPickups.All(x=>x.Available),Is.True);Assert.That(repeated.Life(0).ShotgunAmmo+repeated.Life(0).RocketAmmo+repeated.Life(0).CutterEnergy,Is.Zero);
                yield return null;
            }
            finally {UnityEngine.Object.DestroyImmediate(owner);SceneManager.UnloadSceneAsync(scene);}
        }
    }
}
