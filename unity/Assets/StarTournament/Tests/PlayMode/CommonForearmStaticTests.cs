using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class CommonForearmStaticTests
    {
        Scene scene;Gamepad pad;bool previousAsync;
        [UnityTest] public IEnumerator AllFiveStaticForearmsAndWrists()
        {
            previousAsync=UnityEditor.ShaderUtil.allowAsyncCompilation;UnityEditor.ShaderUtil.allowAsyncCompilation=false;
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");
            var ground=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ProvingGround>()).Single();
            yield return NativeLoadingTestScene.WaitLabHistory(ground);ground.enabled=false;
            pad=InputSystem.AddDevice<Gamepad>();ground.StartCombatReview(new[]{pad},ensureOpponent:true);yield return null;Assert.That(ground.Running,Is.True,ground.CombatReviewDiagnostic());
            ground.PlaceCombatReviewSeat(0,new Vector3(0,-1.2f,-5),0);ground.PlaceCombatReviewSeat(1,new Vector3(5,-1.2f,5),0,180);
            var camera=ground.transform.Find("seat-camera-1").GetComponent<Camera>();camera.aspect=1600f/900f;
            var view=camera.transform.Find("trooper-view").GetComponent<TrooperVisual>();
            var bones=view.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Left")||t.name.StartsWith("Right")).ToDictionary(t=>t.name);
            var mount=view.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="trooper:rig:weapon-mount");
            var capture=typeof(TrooperPresentationTests).GetMethod("CaptureSingleView",BindingFlags.Static|BindingFlags.NonPublic);
            var close=typeof(TrooperPresentationTests).GetMethod("CaptureGrip",BindingFlags.Instance|BindingFlags.NonPublic);
            string output=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_COMMON_FOREARM_CAPTURE");Directory.CreateDirectory(output);
            var reference=new Dictionary<string,(Vector3 position,Quaternion rotation)>();var errors=new List<string>();var rows=new List<string>{"weapon,side,elbowX,elbowY,elbowZ,handX,handY,handZ,wristAngle"};
            foreach(WeaponId weapon in Enum.GetValues(typeof(WeaponId)))
            {
                var state=ground.Session.Capture();var life=state.Lives[0];life.ShotgunOwned=life.RocketOwned=life.CutterOwned=life.PlasmaOwned=true;life.PlasmaAmmo=100;life.SelectedWeapon=weapon;life.SwitchRemaining=0;life.Ammo=CombatLife.AmmoFor(life,weapon);state.Lives[0]=life;
                ground.Session.Restore(state);view.Tuning.Set("view.commonForearmsEnabled",0);view.ResetPose(ground.Session.Time);ground.SendMessage("LateUpdate");yield return null;
                var mp=mount.position;var mr=mount.rotation;var lengths=new[]{Vector3.Distance(bones["LeftLowerArm"].position,bones["LeftHand"].position),Vector3.Distance(bones["RightLowerArm"].position,bones["RightHand"].position)};
                capture.Invoke(null,new object[]{Path.Combine(output,"before"),weapon.ToString(),camera,1600,900});
                view.Tuning.Set("view.commonForearmsEnabled",1);view.ResetPose(ground.Session.Time);ground.SendMessage("LateUpdate");yield return null;
                capture.Invoke(null,new object[]{Path.Combine(output,"after",weapon.ToString()),weapon.ToString(),camera,1600,900});
                close.Invoke(new TrooperPresentationTests(),new object[]{Path.Combine(output,"after",weapon.ToString()),weapon.ToString(),view,camera});
                Assert.That(Vector3.Distance(mp,mount.position),Is.LessThan(.00001f));Assert.That(Quaternion.Angle(mr,mount.rotation),Is.LessThan(.05f));
                int sideIndex=0;
                foreach(string side in new[]{"Left","Right"})
                {
                    var elbow=bones[side+"LowerArm"];var hand=bones[side+"Hand"];var e=camera.transform.InverseTransformPoint(elbow.position);var h=camera.transform.InverseTransformPoint(hand.position);var q=Quaternion.Inverse(camera.transform.rotation)*elbow.rotation;
                    float angle=Vector3.Angle(hand.position-elbow.position,bones[side+"MiddleProximal"].position-hand.position);
                    rows.Add(FormattableString.Invariant($"{weapon},{side},{e.x:R},{e.y:R},{e.z:R},{h.x:R},{h.y:R},{h.z:R},{angle:R}"));
                    if(reference.TryGetValue(side,out var first))
                    {if(Vector3.Distance(first.position,e)>.00001f||Quaternion.Angle(first.rotation,q)>.05f)errors.Add(weapon+" "+side+" differs from common forearm transform");}
                    else reference[side]=(e,q);
                    if(angle>40)errors.Add(weapon+" "+side+" wrist angle "+angle);
                    Assert.That(Vector3.Distance(elbow.position,hand.position),Is.EqualTo(lengths[sideIndex++]).Within(.00001f));
                }
                var frozen=bones.ToDictionary(k=>k.Key,k=>k.Value.position);ground.SendMessage("LateUpdate");foreach(var b in bones)Assert.That(Vector3.Distance(frozen[b.Key],b.Value.position),Is.LessThan(.00001f),"repeated render "+b.Key);
            }
            File.WriteAllLines(Path.Combine(output,"measurements.csv"),rows);File.WriteAllLines(Path.Combine(output,"issues.txt"),errors);File.WriteAllText(Path.Combine(output,"profile.json"),JsonUtility.ToJson(view.Tuning,true));
            Assert.That(errors,Is.Empty,string.Join("\n",errors));
        }
        [UnityTearDown] public IEnumerator Cleanup(){UnityEditor.ShaderUtil.allowAsyncCompilation=previousAsync;if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);}
    }
}
