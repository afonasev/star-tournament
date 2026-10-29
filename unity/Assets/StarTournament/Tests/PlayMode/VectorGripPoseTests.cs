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
    public sealed class VectorGripPoseTests
    {
        Scene scene;Gamepad pad;bool previousAsync;
        [UnityTest] public IEnumerator NativeShotgunGripReview()
        {
            previousAsync=UnityEditor.ShaderUtil.allowAsyncCompilation;UnityEditor.ShaderUtil.allowAsyncCompilation=false;
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            var ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();yield return NativeLoadingTestScene.WaitLabHistory(ground);ground.enabled=false;
            pad=InputSystem.AddDevice<Gamepad>();ground.StartCombatReview(new[]{pad},ensureOpponent:true);yield return null;
            ground.PlaceCombatReviewSeat(0,new Vector3(0,-1.2f,-5),0);ground.PlaceCombatReviewSeat(1,new Vector3(5,-1.2f,5),0,180);
            var snap=ground.Session.Capture();snap.Lives[0].ShotgunOwned=true;snap.Lives[0].ShotgunAmmo=20;ground.Session.Restore(snap);
            var actions=new LocalAction[ground.Session.ParticipantCount];actions[0].SelectWeapon=WeaponSelection.Shotgun;ground.Session.Tick(actions,.02f);actions[0]=default;
            for(int i=0;i<60;i++)ground.Session.Tick(actions,.02f);
            ground.SendMessage("RefreshInterface");ground.SendMessage("LateUpdate");
            var camera=ground.GetComponentsInChildren<Camera>().Single(c=>c.name=="seat-camera-1");var view=camera.transform.Find("trooper-view").GetComponent<TrooperVisual>();
            var output=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_VECTOR_GRIP_CAPTURE");
            var bones=view.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Left")||t.name.StartsWith("Right")).ToDictionary(t=>t.name);
            if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"profile.json"),JsonUtility.ToJson(view.Tuning,true));}
            var rows=new System.Collections.Generic.List<string>{"stage,side,wrist_angle"};
            var binding=BindingFlags.Instance|BindingFlags.NonPublic;
            foreach(var phase in new[]{"idle","recoil"})
            {
                if(phase=="recoil")
                {
                    actions[0].Fire=true;ground.Session.Tick(actions,.02f);actions[0]=default;
                    // Inspect a visible recoil phase, not the zero-time fire birth frame.
                    for(int tick=0;tick<5;tick++)ground.Session.Tick(actions,.02f);
                    ground.SendMessage("LateUpdate");Assert.That(view.State,Is.EqualTo("fire"));Assert.That(view.SampleTime,Is.GreaterThan(.05d));
                }
                foreach(var side in new[]{"Left","Right"})
                {
                    float angle=Vector3.Angle(bones[side+"Hand"].position-bones[side+"LowerArm"].position,bones[side+"MiddleProximal"].position-bones[side+"Hand"].position);
                    rows.Add(System.FormattableString.Invariant($"{phase},{side},{angle:R}"));
                    Assert.That(angle,Is.LessThan(side=="Left"?35f:15f),side+" wrist deflection");
                }
                var positions=bones.Values.Select(b=>b.position).ToArray();var rotations=bones.Values.Select(b=>b.rotation).ToArray();
                ground.SendMessage("LateUpdate");ground.SendMessage("LateUpdate");
                for(int i=0;i<positions.Length;i++){Assert.That(Vector3.Distance(positions[i],bones.Values.ElementAt(i).position),Is.LessThan(.00001f),phase+" frozen "+bones.Values.ElementAt(i).name);Assert.That(Quaternion.Angle(rotations[i],bones.Values.ElementAt(i).rotation),Is.LessThan(.05f));}
                if(!string.IsNullOrEmpty(output))
                {
                    var directory=Path.Combine(output,phase);Directory.CreateDirectory(directory);
                    typeof(TrooperPresentationTests).GetMethod("CaptureGrip",binding).Invoke(new TrooperPresentationTests(),new object[]{directory,"Shotgun",view,camera});
                    typeof(TrooperPresentationTests).GetMethod("CaptureSingleView",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{directory,"fps",camera,1600,1000});
                }
            }
            VerifyRestorationAndOtherWeapons(view,bones);
            if(!string.IsNullOrEmpty(output)){File.WriteAllLines(Path.Combine(output,"wrist-angles.csv"),rows);File.WriteAllText(Path.Combine(output,"profile.json"),JsonUtility.ToJson(view.Tuning,true));}
        }
        static void VerifyRestorationAndOtherWeapons(TrooperVisual view,System.Collections.Generic.Dictionary<string,Transform> bones)
        {
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var grip=typeof(TrooperVisual).GetField("equipmentGrip",flags).GetValue(view);
            var type=grip.GetType();var restore=type.GetMethod("RestorePrimary");var apply=type.GetMethod("Apply");
            restore.Invoke(grip,null);
            var all=bones.Values.ToArray();var localPositions=all.Select(b=>b.localPosition).ToArray();var localRotations=all.Select(b=>b.localRotation).ToArray();
            var mount=view.GetComponentsInChildren<Transform>().Single(t=>t.name=="trooper:rig:weapon-mount");var mountPosition=mount.position;var mountRotation=mount.rotation;
            var current=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeCobaltThumb",flags).Invoke(view.Tuning,null);
            var previous=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeVectorGrip",flags).Invoke(current,null);
            foreach(var weapon in new[]{WeaponId.Rifle,WeaponId.RocketLauncher,WeaponId.Cutter,WeaponId.Plasma,WeaponId.Shotgun})
            {
                apply.Invoke(grip,new object[]{current,weapon,1f,null,default(CrimsonRecoilSample)});
                for(int i=0;i<all.Length;i++)Assert.That(Vector3.Distance(all[i].localPosition,localPositions[i]),Is.LessThan(.00001f),weapon+" "+all[i].name+" bone length");
                Assert.That(Vector3.Distance(mount.position,mountPosition),Is.LessThan(.00001f),weapon+" mount position");
                Assert.That(Quaternion.Angle(mount.rotation,mountRotation),Is.LessThan(.05f),weapon+" mount rotation");
                var positions=all.Select(b=>b.position).ToArray();var rotations=all.Select(b=>b.rotation).ToArray();restore.Invoke(grip,null);
                for(int i=0;i<all.Length;i++)
                {
                    Assert.That(Vector3.Distance(all[i].localPosition,localPositions[i]),Is.LessThan(.00001f),weapon+" restored position");
                    Assert.That(Quaternion.Angle(all[i].localRotation,localRotations[i]),Is.LessThan(.05f),weapon+" restored rotation");
                }
                if(weapon==WeaponId.Shotgun)continue;
                apply.Invoke(grip,new object[]{previous,weapon,1f,null,default(CrimsonRecoilSample)});
                for(int i=0;i<all.Length;i++)
                {
                    Assert.That(Vector3.Distance(all[i].position,positions[i]),Is.LessThan(.00001f),weapon+" unchanged pose");
                    Assert.That(Quaternion.Angle(all[i].rotation,rotations[i]),Is.LessThan(.05f),weapon+" unchanged rotation");
                }
                restore.Invoke(grip,null);
            }
        }
        [UnityTearDown]public IEnumerator Cleanup(){UnityEditor.ShaderUtil.allowAsyncCompilation=previousAsync;if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);}
    }
}
