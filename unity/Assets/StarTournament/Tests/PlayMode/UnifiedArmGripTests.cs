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
    public sealed class UnifiedArmGripTests
    {
        Scene scene;Gamepad pad;bool previousAsync;
        [UnityTest] public IEnumerator SharedArmFitPreservesContactsMountBonesAndFrozenPose()
        {
            previousAsync=UnityEditor.ShaderUtil.allowAsyncCompilation;UnityEditor.ShaderUtil.allowAsyncCompilation=false;
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");
            var ground=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ProvingGround>()).Single();
            yield return NativeLoadingTestScene.WaitLabHistory(ground);ground.enabled=false;
            pad=InputSystem.AddDevice<Gamepad>();ground.StartCombatReview(new[]{pad},ensureOpponent:true);yield return null;
            ground.PlaceCombatReviewSeat(0,new Vector3(0,-1.2f,-5),0);ground.PlaceCombatReviewSeat(1,new Vector3(5,-1.2f,5),0,180);
            var camera=ground.transform.Find("seat-camera-1").GetComponent<Camera>();
            var view=camera.transform.Find("trooper-view").GetComponent<TrooperVisual>();
            var model=view.GetComponent<WeaponModelPresentation>();
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var grip=typeof(TrooperVisual).GetField("equipmentGrip",flags).GetValue(view);
            var restore=grip.GetType().GetMethod("RestorePrimary");var apply=grip.GetType().GetMethod("Apply");
            var contactControl=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeUnifiedArmGrips",flags).Invoke(view.Tuning,null);
            var baselinePath=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_ARM_GRIP_BASELINE");
            var previous=string.IsNullOrEmpty(baselinePath)?contactControl:JsonUtility.FromJson<ProvingProfile>(File.ReadAllText(baselinePath));
            var all=view.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Left")||t.name.StartsWith("Right")).ToArray();
            var bones=all.ToDictionary(t=>t.name);var mount=view.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="trooper:rig:weapon-mount");
            var output=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_ARM_GRIP_CAPTURE");
            var rows=new List<string>{"weapon,phase,side,wrist_angle,elbow_x,elbow_y,elbow_z"};
            var capture=typeof(TrooperPresentationTests).GetMethod("CaptureSingleView",BindingFlags.Static|BindingFlags.NonPublic);
            var close=typeof(TrooperPresentationTests).GetMethod("CaptureGrip",flags);
            void Measure(WeaponId weapon,string phase)
            {
                foreach(string side in new[]{"Left","Right"})
                {
                    var hand=bones[side+"Hand"];var lower=bones[side+"LowerArm"];var palm=bones[side+"MiddleProximal"];
                    float angle=Vector3.Angle(hand.position-lower.position,palm.position-hand.position);
                    var elbow=mount.InverseTransformPoint(lower.position);
                    rows.Add(FormattableString.Invariant($"{weapon},{phase},{side},{angle:R},{elbow.x:R},{elbow.y:R},{elbow.z:R}"));
                }
            }
            foreach(WeaponId weapon in Enum.GetValues(typeof(WeaponId)))
            {
                var state=ground.Session.Capture();var life=state.Lives[0];
                life.ShotgunOwned=life.RocketOwned=life.CutterOwned=life.PlasmaOwned=true;life.PlasmaAmmo=100;
                life.SelectedWeapon=weapon;life.PendingWeapon=default;life.SwitchRemaining=0;life.Ammo=CombatLife.AmmoFor(life,weapon);state.Lives[0]=life;ground.Session.Restore(state);
                view.ResetPose(ground.Session.Time);ground.SendMessage("LateUpdate");yield return null;
                restore.Invoke(grip,null);
                var rawPositions=all.Select(t=>t.localPosition).ToArray();var rawRotations=all.Select(t=>t.localRotation).ToArray();
                var mountPosition=mount.position;var mountRotation=mount.rotation;
                // Isolate elbow fitting from the intentional legacy PULSE palm repair.
                var contactBones=all.Where(t=>new[]{"Left","Right"}.Any(side=>new[]{"Hand","Thumb","Index","Middle","Ring","Little"}.Any(digit=>t.name.StartsWith(side+digit)))).ToArray();
                apply.Invoke(grip,new object[]{contactControl,weapon,weapon==WeaponId.Plasma?model.PlasmaViewScale:1,model.CutterGripProfile});
                var fittedContacts=contactBones.Select(t=>t.position).ToArray();restore.Invoke(grip,null);
                if(weapon==WeaponId.Plasma)
                {
                    // Migrated named poses retain their old elbow controls with current metadata.
                    var savedPose=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(view.Tuning));
                    savedPose.Set("grip.Plasma.wristAlignment",0);savedPose.Set("grip.Plasma.primaryWristAlignment",0);
                    apply.Invoke(grip,new object[]{savedPose,weapon,model.PlasmaViewScale,model.CutterGripProfile});
                    for(int i=0;i<contactBones.Length;i++)Assert.That(Vector3.Distance(contactBones[i].position,fittedContacts[i]),Is.LessThan(.0001f),"saved Forge finger contact "+contactBones[i].name);
                    restore.Invoke(grip,null);
                }
                foreach(string phase in new[]{"before","after"})
                {
                    var profile=phase=="before"?previous:view.Tuning;
                    apply.Invoke(grip,new object[]{profile,weapon,weapon==WeaponId.Plasma?model.PlasmaViewScale:1,model.CutterGripProfile});
                    yield return null;
                    Assert.That(Vector3.Distance(mount.position,mountPosition),Is.LessThan(.00001f),weapon+" mount");
                    Assert.That(Quaternion.Angle(mount.rotation,mountRotation),Is.LessThan(.05f),weapon+" mount rotation");
                    for(int i=0;i<all.Length;i++)Assert.That(Vector3.Distance(all[i].localPosition,rawPositions[i]),Is.LessThan(.00001f),weapon+" bone length "+all[i].name);
                    if(phase=="after")for(int i=0;i<contactBones.Length;i++)Assert.That(Vector3.Distance(contactBones[i].position,fittedContacts[i]),Is.LessThan(.0001f),weapon+" fitted hand/finger contact");
                    Measure(weapon,phase);
                    if(!string.IsNullOrEmpty(output))
                    {
                        string directory=Path.Combine(output,phase,weapon.ToString());Directory.CreateDirectory(directory);
                        capture.Invoke(null,new object[]{directory,weapon.ToString(),camera,1600,900});
                        close.Invoke(new TrooperPresentationTests(),new object[]{directory,weapon.ToString(),view,camera});
                    }
                    restore.Invoke(grip,null);
                    for(int i=0;i<all.Length;i++)
                    {
                        Assert.That(Vector3.Distance(all[i].localPosition,rawPositions[i]),Is.LessThan(.00001f),weapon+" restore position "+all[i].name);
                        Assert.That(Quaternion.Angle(all[i].localRotation,rawRotations[i]),Is.LessThan(.05f),weapon+" restore rotation "+all[i].name);
                    }
                }
                ground.SendMessage("LateUpdate");var frozen=all.Select(t=>t.position).ToArray();ground.SendMessage("LateUpdate");
                for(int i=0;i<all.Length;i++)Assert.That(Vector3.Distance(all[i].position,frozen[i]),Is.LessThan(.00001f),weapon+" frozen clock");
                var actions=new LocalAction[ground.Session.ParticipantCount];actions[0].Fire=true;actions[0].FireHeld=true;
                ground.Session.Tick(actions,.02f);actions[0]=default;
                for(int tick=0;tick<5;tick++)ground.Session.Tick(actions,.02f);
                ground.SendMessage("LateUpdate");yield return null;Measure(weapon,"fire");
                if(!string.IsNullOrEmpty(output))capture.Invoke(null,new object[]{Path.Combine(output,"fire"),weapon.ToString(),camera,1600,900});
            }
            if(!string.IsNullOrEmpty(output))
            {
                File.WriteAllLines(Path.Combine(output,"wrist-angles.csv"),rows);
                File.WriteAllText(Path.Combine(output,"profile.json"),JsonUtility.ToJson(view.Tuning,true));
            }
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            UnityEditor.ShaderUtil.allowAsyncCompilation=previousAsync;
            if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);
            if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
