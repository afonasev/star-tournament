#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class ProjectilePerformanceIsolationTests
    {
        [UnityTest] public IEnumerator OrdinaryFocusStillClearsBeamWhileExplicitSyntheticOwnerRetainsItsWorkload()
        {
            yield return NativeLoadingTestScene.Load();var scene=SceneManager.GetSceneByName("ProvingGround");
            var ground=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<ProvingGround>()).Single();
            yield return NativeLoadingTestScene.WaitLabHistory(ground);
            GameObject driverObject=null;
            try
            {
                var info=new[]{new NativeParticipantInfo(NativeParticipantKind.LocalHuman,"Focus QA",NativeParticipantColors.For(0),-1),new NativeParticipantInfo(NativeParticipantKind.Bot,"Target QA",NativeParticipantColors.For(1),0)};
                ground.StartBotReview(new NativeMatchComposition(NativeMatchRoster.Ffa(2),info,new[]{0}),20261029);ground.enabled=false;
                yield return NativeLoadingTestScene.Wait(ground);
                ground.PlaceCombatReviewSeat(0,new Vector3(0,20,-5),0);ground.PlaceCombatReviewSeat(1,new Vector3(10,20,5),0);
                var snapshot=ground.Session.Capture();snapshot.Lives[0].CutterOwned=true;snapshot.Lives[0].CutterEnergy=30;snapshot.Lives[0].SelectedWeapon=WeaponId.Cutter;
                snapshot.Lives[0].PendingWeapon=default;snapshot.Lives[0].SwitchRemaining=0;snapshot.Lives[0].Ammo=CombatLife.AmmoFor(snapshot.Lives[0],WeaponId.Cutter);snapshot.BeamReleaseRequired=new bool[2];ground.Session.Restore(snapshot);
                var actions=new LocalAction[2];actions[0].FireHeld=true;ground.Session.Tick(actions,.1f);Assert.That(ground.Session.Beam(0).Active,Is.True);
                ground.SendMessage("OnApplicationFocus",false);Assert.That(ground.Session.Beam(0).Active,Is.False,"Ordinary focus loss must stop fire");
                ground.Session.Tick(actions,.02f);Assert.That(ground.Session.Beam(0).Active,Is.False,"Ordinary resume still requires a release");
                // Complete the checks synchronously before Start can run the opt-in CLI driver.
                driverObject=new GameObject("Explicit synthetic input owner fixture");var driver=driverObject.AddComponent<NativeProjectilePerformanceReview>();
                typeof(NativeProjectilePerformanceReview).GetField("ground",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(driver,ground);
                ground.Session.Tick(new LocalAction[2],.02f);ground.Session.Tick(actions,.1f);Assert.That(ground.Session.Beam(0).Active,Is.True);
                ground.SendMessage("OnApplicationFocus",false);Assert.That(ground.Session.Beam(0).Active,Is.True,"Explicit background performance input retains its measured state");
                Object.DestroyImmediate(driverObject);driverObject=null;
                ground.SendMessage("OnApplicationFocus",false);Assert.That(ground.Session.Beam(0).Active,Is.False,"Destroying the QA owner restores the ordinary boundary immediately");
            }
            finally {if(driverObject)Object.DestroyImmediate(driverObject);}
            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
#endif
