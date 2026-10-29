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
    public sealed class StableArmEntryTests
    {
        Scene scene;Gamepad pad;bool previousAsync;
        [UnityTest] public IEnumerator SharedEntriesPreserveContactsAndAllSwitchPairs()
        {
            previousAsync=UnityEditor.ShaderUtil.allowAsyncCompilation;UnityEditor.ShaderUtil.allowAsyncCompilation=false;
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");
            var ground=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ProvingGround>()).Single();
            yield return NativeLoadingTestScene.WaitLabHistory(ground);ground.enabled=false;
            pad=InputSystem.AddDevice<Gamepad>();ground.StartCombatReview(new[]{pad},ensureOpponent:true);yield return null;
            ground.PlaceCombatReviewSeat(0,new Vector3(0,-1.2f,-5),0);ground.PlaceCombatReviewSeat(1,new Vector3(5,-1.2f,5),0,180);
            var camera=ground.transform.Find("seat-camera-1").GetComponent<Camera>();
            camera.aspect=1600f/900f; // The visual witness uses the same explicit projection as its output frame.
            var view=camera.transform.Find("trooper-view").GetComponent<TrooperVisual>();
            // This fixture preserves the @28 axis-only contract; common visible forearms have a separate witness.
            var legacy=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeCommonForearms",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(view.Tuning,null);
            view.Initialize(ground.TrooperClips,legacy,true);
            var presentation=typeof(ProvingGround).GetField("presentation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ground);
            var transition=((Array)presentation.GetType().GetField("weaponSwitches",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(presentation)).GetValue(0);
            var applyTransition=transition.GetType().GetMethod("Apply");
            var all=view.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Left")||t.name.StartsWith("Right")).ToArray();var bones=all.ToDictionary(t=>t.name);
            var contacts=all.Where(t=>new[]{"Left","Right"}.Any(side=>new[]{"Hand","Thumb","Index","Middle","Ring","Little"}.Any(d=>t.name.StartsWith(side+d)))).ToArray();
            var mount=view.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="trooper:rig:weapon-mount");
            var world=ground.transform.Find("player-1/trooper-presentation").GetComponent<TrooperVisual>();
            var worldBones=world.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Left")||t.name.StartsWith("Right")).ToArray();
            var output=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_ARM_ENTRY_CAPTURE");
            var rows=new List<string>{"pose,side,entry_x,wrist_angle,shoulder_x,shoulder_y,shoulder_z,elbow_x,elbow_y,elbow_z,hand_x,hand_y,hand_z"};var errors=new List<string>();
            var capture=typeof(TrooperPresentationTests).GetMethod("CaptureSingleView",BindingFlags.Static|BindingFlags.NonPublic);
            var close=typeof(TrooperPresentationTests).GetMethod("CaptureGrip",BindingFlags.Instance|BindingFlags.NonPublic);
            void State(WeaponId from,WeaponId to,double remaining)
            {
                var state=ground.Session.Capture();var life=state.Lives[0];life.ShotgunOwned=life.RocketOwned=life.CutterOwned=life.PlasmaOwned=true;life.PlasmaAmmo=100;
                life.SelectedWeapon=remaining==0?to:from;life.PendingWeapon=remaining==0?default:to;life.SwitchRemaining=remaining;life.Ammo=CombatLife.AmmoFor(life,life.SelectedWeapon);state.Lives[0]=life;
                ground.Session.Restore(state);view.ResetPose(ground.Session.Time);ground.SendMessage("LateUpdate");
            }
            void Measure(string label,bool check)
            {
                foreach(string side in new[]{"Left","Right"})
                {
                    var a=camera.transform.InverseTransformPoint(bones[side+"UpperArm"].position);var b=camera.transform.InverseTransformPoint(bones[side+"LowerArm"].position);var c=camera.transform.InverseTransformPoint(bones[side+"Hand"].position);
                    var e=camera.WorldToViewportPoint(bones[side+"LowerArm"].position);var h=camera.WorldToViewportPoint(bones[side+"Hand"].position);
                    float x=e.x-e.y*(h.x-e.x)/(h.y-e.y);float angle=Vector3.Angle(bones[side+"Hand"].position-bones[side+"LowerArm"].position,bones[side+"MiddleProximal"].position-bones[side+"Hand"].position);
                    rows.Add(FormattableString.Invariant($"{label},{side},{x:R},{angle:R},{a.x:R},{a.y:R},{a.z:R},{b.x:R},{b.y:R},{b.z:R},{c.x:R},{c.y:R},{c.z:R}"));
                    float target=.5f+view.Tuning.Get("view.armEntry"+side+"X")/camera.aspect;
                    if(check&&(!float.IsFinite(x)||Mathf.Abs(x-target)>.0015f))errors.Add(label+" "+side+" entry "+x+" expected "+target);
                    if(check&&angle>40)errors.Add(label+" "+side+" wrist angle "+angle);
                }
            }
            foreach(WeaponId weapon in Enum.GetValues(typeof(WeaponId)))
            {
                view.Tuning.Set("view.armEntryEnabled",0);State(weapon,weapon,0);yield return null;
                var positions=all.Select(t=>t.localPosition).ToArray();var fitted=contacts.Select(t=>t.position).ToArray();var mountPosition=mount.position;var mountRotation=mount.rotation;
                var worldPositions=worldBones.Select(t=>t.position).ToArray();
                Measure("before-"+weapon,false);
                if(!string.IsNullOrEmpty(output))capture.Invoke(null,new object[]{Path.Combine(output,"before"),weapon.ToString(),camera,1600,900});
                view.Tuning.Set("view.armEntryEnabled",1);State(weapon,weapon,0);yield return null;Measure("after-"+weapon,true);
                Assert.That(Vector3.Distance(mountPosition,mount.position),Is.LessThan(.00001f),weapon+" mount");Assert.That(Quaternion.Angle(mountRotation,mount.rotation),Is.LessThan(.05f));
                for(int i=0;i<all.Length;i++)if(!all[i].name.EndsWith("Shoulder"))Assert.That(Vector3.Distance(positions[i],all[i].localPosition),Is.LessThan(.00001f),weapon+" length "+all[i].name);
                for(int i=0;i<contacts.Length;i++)Assert.That(Vector3.Distance(fitted[i],contacts[i].position),Is.LessThan(.0001f),weapon+" contact "+contacts[i].name);
                for(int i=0;i<worldBones.Length;i++)Assert.That(Vector3.Distance(worldPositions[i],worldBones[i].position),Is.LessThan(.00001f),"world body unchanged");
                if(!string.IsNullOrEmpty(output)){string dir=Path.Combine(output,"after",weapon.ToString());capture.Invoke(null,new object[]{dir,weapon.ToString(),camera,1600,900});close.Invoke(new TrooperPresentationTests(),new object[]{dir,weapon.ToString(),view,camera});}
                var repeated=all.Select(t=>t.position).ToArray();
                for(int repeat=0;repeat<3;repeat++)applyTransition.Invoke(transition,new object[]{ground.Session.Life(0),ground.Session.WeaponSwitchSeconds});
                for(int i=0;i<all.Length;i++)Assert.That(Vector3.Distance(repeated[i],all[i].position),Is.LessThan(.00001f),"same-revision render "+weapon+" "+all[i].name);
                var frozen=all.Select(t=>t.position).ToArray();ground.SendMessage("LateUpdate");for(int i=0;i<all.Length;i++)Assert.That(Vector3.Distance(frozen[i],all[i].position),Is.LessThan(.00001f),"frozen "+weapon+" "+all[i].name);
            }
            foreach(WeaponId weapon in Enum.GetValues(typeof(WeaponId)))
            {
                State(weapon,weapon,0);var actions=new LocalAction[ground.Session.ParticipantCount];actions[0].Fire=actions[0].FireHeld=true;ground.Session.Tick(actions,.02f);actions[0]=default;
                for(int tick=0;tick<5;tick++)ground.Session.Tick(actions,.02f);
                ground.SendMessage("LateUpdate");Measure("fire-"+weapon,true);
                if(!string.IsNullOrEmpty(output))capture.Invoke(null,new object[]{Path.Combine(output,"fire"),weapon.ToString(),camera,1600,900});
            }
            foreach(WeaponId from in Enum.GetValues(typeof(WeaponId)))foreach(WeaponId to in Enum.GetValues(typeof(WeaponId)))
            {
                if(from==to)continue;
                foreach(double phase in new[]{.999,.75,.5001,.5,.4999,.25,.001,0})
                {
                    view.Tuning.Set("view.armEntryEnabled",0);State(from,to,ground.Session.WeaponSwitchSeconds*phase);var fitted=contacts.Select(t=>t.position).ToArray();var mp=mount.position;
                    view.Tuning.Set("view.armEntryEnabled",1);State(from,to,ground.Session.WeaponSwitchSeconds*phase);
                    Measure(from+"-"+to+"-"+phase,true);
                    if(!string.IsNullOrEmpty(output)&&(int)to==((int)from+1)%5)
                        capture.Invoke(null,new object[]{Path.Combine(output,"switch",from+"-"+to),phase.ToString("F4",System.Globalization.CultureInfo.InvariantCulture),camera,1600,900});
                    Assert.That(Vector3.Distance(mp,mount.position),Is.LessThan(.00001f),"switch mount");
                    for(int i=0;i<contacts.Length;i++)Assert.That(Vector3.Distance(fitted[i],contacts[i].position),Is.LessThan(.0001f),"switch contact "+contacts[i].name);
                }
            }
            float aspect=camera.aspect;
            foreach(float splitAspect in new[]{aspect*.5f,aspect*2})
            {
                camera.aspect=splitAspect;
                foreach(WeaponId weapon in Enum.GetValues(typeof(WeaponId))){State(weapon,weapon,0);Measure("aspect-"+splitAspect+"-"+weapon,true);}
            }
            camera.aspect=aspect;
            if(!string.IsNullOrEmpty(output)){File.WriteAllLines(Path.Combine(output,"entry-points.csv"),rows);File.WriteAllLines(Path.Combine(output,"issues.txt"),errors);File.WriteAllText(Path.Combine(output,"profile.json"),JsonUtility.ToJson(view.Tuning,true));}
            Assert.That(errors,Is.Empty,string.Join("\n",errors.Take(30)));
        }
        [UnityTearDown] public IEnumerator Cleanup(){UnityEditor.ShaderUtil.allowAsyncCompilation=previousAsync;if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);}
    }
}
