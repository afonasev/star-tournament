using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class WeaponViewPositionTests
    {
        Scene scene;Gamepad[] pads;bool previousAsync;
        [UnityTest] public IEnumerator DefaultPlacementForAllWeaponsInSingleAndSplitViews()
        {
            previousAsync=UnityEditor.ShaderUtil.allowAsyncCompilation;UnityEditor.ShaderUtil.allowAsyncCompilation=false;
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");
            var ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();
            yield return NativeLoadingTestScene.WaitLabHistory(ground);ground.enabled=false;
            pads=new[]{InputSystem.AddDevice<Gamepad>(),InputSystem.AddDevice<Gamepad>()};
            var output=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_VIEW_POSITION_CAPTURE");
            var capture=typeof(TrooperPresentationTests).GetMethod("CaptureSingleView",BindingFlags.Static|BindingFlags.NonPublic);
            foreach(int count in new[]{1,2})
            {
                ground.StartCombatReview(pads.Take(count).ToArray(),ensureOpponent:true);yield return null;
                for(int seat=0;seat<count;seat++)ground.PlaceCombatReviewSeat(seat,new Vector3(seat*4,-1.2f,-5),0);
                var state=ground.Session.Capture();
                foreach(WeaponId weapon in Enum.GetValues(typeof(WeaponId)))
                {
                    for(int seat=0;seat<count;seat++)
                    {
                        var life=state.Lives[seat];life.ShotgunOwned=life.RocketOwned=life.CutterOwned=life.PlasmaOwned=true;
                        life.PlasmaAmmo=100;life.SelectedWeapon=weapon;life.PendingWeapon=default;life.SwitchRemaining=0;
                        life.Ammo=CombatLife.AmmoFor(life,weapon);state.Lives[seat]=life;
                    }
                    ground.Session.Restore(state);
                    foreach(var view in ground.GetComponentsInChildren<TrooperVisual>())view.ResetPose(ground.Session.Time);
                    ground.SendMessage("LateUpdate");yield return null;ground.SendMessage("LateUpdate");yield return null;
                    for(int seat=0;seat<count;seat++)
                    {
                        var camera=ground.transform.Find("seat-camera-"+(seat+1)).GetComponent<Camera>();
                        var view=camera.transform.Find("trooper-view").GetComponent<TrooperVisual>();
                        Assert.That(view.Tuning.Get("view.y"),Is.EqualTo(-1.49f));Assert.That(view.Tuning.Get("view.z"),Is.EqualTo(.01f));
                        var expected=new Vector3(view.Tuning.Get("view.x"),-1.49f,.01f);
                        if(weapon==WeaponId.RocketLauncher&&view.Tuning.Version>=31&&view.Tuning.Get("grip.crimson.enabled")>0)
                            expected+=new Vector3(view.Tuning.Get("grip.crimson.viewX"),view.Tuning.Get("grip.crimson.viewY"),view.Tuning.Get("grip.crimson.viewZ"));
                        Assert.That(Vector3.Distance(view.transform.localPosition,expected+view.WalkOffset),Is.LessThan(.000001f));
                        Assert.That(camera.rect,Is.EqualTo(LocalSeatLayout.Viewport(count,seat)));
                        if(!string.IsNullOrEmpty(output))
                        {
                            string directory=Path.Combine(output,"seats-"+count,"seat-"+seat);Directory.CreateDirectory(directory);
                            File.WriteAllText(Path.Combine(directory,"profile.json"),JsonUtility.ToJson(view.Tuning,true));
                            capture.Invoke(null,new object[]{directory,weapon.ToString(),camera,count==1?1600:800,900});
                        }
                    }
                }
            }
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            UnityEditor.ShaderUtil.allowAsyncCompilation=previousAsync;
            if(pads!=null)foreach(var pad in pads)if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);
            if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
