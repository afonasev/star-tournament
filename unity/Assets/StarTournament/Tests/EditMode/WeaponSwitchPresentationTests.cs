using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class WeaponSwitchPresentationTests
    {
        [TestCase(.1f)][TestCase(1f)][TestCase(3f)]
        public void AnimationTracksRemainingTimeAndFinishesOnTheFireBoundary(float seconds)
        {
            var profile=ProvingProfile.CreateCombatDefault();profile.Set("weapon.switchSeconds",seconds);
            var life=new CombatLife("p",profile);life.Select(WeaponSelection.Shotgun);
            Assert.That(WeaponSwitchPose.Read(life.Read(),life.SwitchSeconds).Lift,Is.Zero);
            life.Advance(life.SwitchSeconds*.25);
            var before=life.Read();var raised=WeaponSwitchPose.Read(before,life.SwitchSeconds);
            Assert.That(raised.Weapon,Is.EqualTo(WeaponId.Rifle));Assert.That(raised.Lift,Is.EqualTo(.5).Within(.00001));
            Assert.That(life.Fire(true),Is.False);
            // Restore must reconstruct the same visual phase, without a local presentation clock.
            life.Advance(life.SwitchSeconds*.25);var hidden=WeaponSwitchPose.Read(life.Read(),life.SwitchSeconds);
            Assert.That(hidden.Weapon,Is.EqualTo(WeaponId.Shotgun));Assert.That(hidden.Lift,Is.EqualTo(1));
            life.Restore(before);Assert.That(WeaponSwitchPose.Read(life.Read(),life.SwitchSeconds).Lift,Is.EqualTo(raised.Lift));
            life.Advance(life.SwitchSeconds*.5);Assert.That(WeaponSwitchPose.Read(life.Read(),life.SwitchSeconds).Weapon,Is.EqualTo(WeaponId.Shotgun));
            Assert.That(life.Read().SelectedWeapon,Is.EqualTo(WeaponId.Rifle));Assert.That(life.Fire(true),Is.False);
            life.Advance(life.Read().SwitchRemaining);life.ClearHeldInput();
            var complete=WeaponSwitchPose.Read(life.Read(),life.SwitchSeconds);
            Assert.That(complete.Lift,Is.Zero);Assert.That(complete.Weapon,Is.EqualTo(WeaponId.Shotgun));Assert.That(life.Fire(true),Is.True);
            life.Select(WeaponSelection.Rifle);life.Advance(life.SwitchSeconds*.25);life.Damage(life.Read().Life,100);
            Assert.That(WeaponSwitchPose.Read(life.Read(),life.SwitchSeconds).Lift,Is.Zero);
            life.Advance(profile.Get("combat.killcamSeconds"));life.Respawn(1);
            Assert.That(WeaponSwitchPose.Read(life.Read(),life.SwitchSeconds).Weapon,Is.EqualTo(WeaponId.Rifle));
            Assert.That(WeaponSwitchPose.Read(life.Read(),life.SwitchSeconds).Lift,Is.Zero);
        }
        [Test]
        public void AllWeaponPairsShareTheSamePhaseWithoutMutatingState()
        {
            foreach(WeaponId from in Enum.GetValues(typeof(WeaponId)))foreach(WeaponId to in Enum.GetValues(typeof(WeaponId)))
            {
                if(from==to)continue;
                var state=new CombatLifeState{SelectedWeapon=from,PendingWeapon=to,SwitchRemaining=.75};
                string original=JsonUtility.ToJson(state);
                Assert.That(WeaponSwitchPose.Read(state,1).Weapon,Is.EqualTo(from));
                state.SwitchRemaining=.25;Assert.That(WeaponSwitchPose.Read(state,1).Weapon,Is.EqualTo(to));
                Assert.That(WeaponSwitchPose.Read(state,1).Lift,Is.EqualTo(.5));
                state.SwitchRemaining=.75;Assert.That(JsonUtility.ToJson(state),Is.EqualTo(original));
            }
        }
        [Test]
        public void ShoulderGestureKeepsPivotsAttachedAndRepeatedRenderingDoesNotAccumulate()
        {
            var cameraRoot=new GameObject("switch-test-camera");var camera=cameraRoot.AddComponent<Camera>();
            var view=UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/StarTournament/Trooper/TrooperArms.prefab"),camera.transform);
            try
            {
                var visual=view.AddComponent<TrooperVisual>();var profile=ProvingProfile.CreateTrooperDefault();
                visual.Initialize(TrooperVisual.ClipNames.Select(n=>UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/StarTournament/Trooper/"+n+".anim")).ToArray(),profile,true);
                var type=typeof(TrooperVisual).Assembly.GetType("StarTournament.ProvingGround.FirstPersonWeaponSwitch");
                var transition=Activator.CreateInstance(type,new object[]{view,camera});var apply=type.GetMethod("Apply");
                var bones=view.GetComponentsInChildren<Transform>(true);var shoulder=bones.Single(t=>t.name=="RightUpperArm");var hand=bones.Single(t=>t.name=="RightHand");
                var pivot=shoulder.localPosition;var worldPivot=shoulder.position;var baseline=shoulder.localRotation;var handRest=hand.position;var root=view.transform.localPosition;
                var state=new CombatLifeState{SelectedWeapon=WeaponId.Rifle,PendingWeapon=WeaponId.Shotgun,SwitchRemaining=.5};
                apply.Invoke(transition,new object[]{state,1d});var reached=shoulder.localRotation;
                Assert.That(view.transform.localPosition,Is.EqualTo(root));Assert.That(shoulder.localPosition,Is.EqualTo(pivot));Assert.That(shoulder.position,Is.EqualTo(worldPivot));
                Assert.That(Vector3.Distance(handRest,hand.position),Is.GreaterThan(.1f));Assert.That(camera.transform.InverseTransformPoint(hand.position).z,Is.LessThan(0));
                apply.Invoke(transition,new object[]{state,1d});Assert.That(Quaternion.Angle(reached,shoulder.localRotation),Is.LessThan(.001f));
                state.SwitchRemaining=0;state.SelectedWeapon=WeaponId.Shotgun;apply.Invoke(transition,new object[]{state,1d});
                Assert.That(Quaternion.Angle(baseline,shoulder.localRotation),Is.LessThan(.001f));Assert.That(Vector3.Distance(handRest,hand.position),Is.LessThan(.00001f));
            }
            finally{UnityEngine.Object.DestroyImmediate(view);UnityEngine.Object.DestroyImmediate(cameraRoot);}
        }
        [Test]
        public void LabUpgradePreservesPreSwitchAndOlderProfileHistory()
        {
            var current=new LabBundle{Profiles=new System.Collections.Generic.List<ProvingProfile>{ProvingProfile.CreateDefault(),ProvingProfile.CreateTrooperDefault(),ProvingProfile.CreateCombatDefault(),ProvingProfile.CreateNativeCombatDefault(),ProvingProfile.CreateCutterDefault()}};
            foreach(string predecessor in new[]{"BeforeShoulderSwitch","BeforeWeaponSwitch","BeforeCutterSwitch","BeforeCutterThickness","BeforeCutterThicknessSwitch","BeforePulse","BeforeFullHeal"})
            {
                string path=Path.Combine(Path.GetTempPath(),"switch-history-"+Guid.NewGuid()+".json");
                try
                {
                    var legacy=new LabBundle{Profiles=current.Profiles.Where(p=>predecessor=="BeforeShoulderSwitch"||predecessor=="BeforeWeaponSwitch"||predecessor.StartsWith("BeforeCutterThickness")||p.Id!="cutter-beam-v1").Select(p=>
                    {
                        if(predecessor=="BeforeCutterThicknessSwitch")p=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeWeaponSwitch",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,null);
                        string method=predecessor=="BeforeCutterSwitch"?"BeforeWeaponSwitch":predecessor=="BeforeCutterThicknessSwitch"?"BeforeCutterThickness":predecessor;
                        return (ProvingProfile)typeof(ProvingProfile).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,null);
                    }).ToList()};
                    var old=new DesignLabHistory(path,legacy);old.Create("Мой профиль");var draft=old.Selected.Snapshot;draft.Set("rifle.head",31);if(draft.Descriptors.Any(d=>d.Path=="cutter.width"))draft.Set("cutter.width",.021f);old.Save(draft);
                    // Historical policy allowed this placement override. Write the authentic old
                    // snapshot/hash directly: current Save intentionally forbids new placement edits.
                    var file=UnityEngine.JsonUtility.FromJson<LabHistoryFile>(File.ReadAllText(path));
                    var before=file.Profiles.Single(p=>p.Id==file.SelectedId).Revisions.Single(r=>r.Number==file.SelectedRevision);
                    before.Snapshot.Set("view.y",-1.5f);before.Hash=before.Snapshot.Hash();File.WriteAllText(path,UnityEngine.JsonUtility.ToJson(file,true));
                    string id=old.SelectedProfileId;
                    var upgraded=new DesignLabHistory(path,current);Assert.That(upgraded.StorageError,Is.Null);
                    Assert.That(upgraded.SelectedProfileId,Is.EqualTo(id));Assert.That(upgraded.Selected.Number,Is.GreaterThan(before.Number));
                    Assert.That(upgraded.Selected.Snapshot.Get("view.y"),Is.EqualTo(-1.5f));Assert.That(upgraded.Selected.Snapshot.Get("rifle.head"),Is.EqualTo(31));
                    Assert.That(upgraded.Selected.Snapshot.Get("view.switchShoulderDegrees"),Is.EqualTo(145f));
                    if(legacy.Descriptors.Any(d=>d.Path=="cutter.width"))Assert.That(upgraded.Selected.Snapshot.Get("cutter.width"),Is.EqualTo(.021f));
                    Assert.That(upgraded.SelectedProfile.Revisions.Single(r=>r.Number==before.Number).Hash,Is.EqualTo(before.Hash));
                    int count=upgraded.SelectedProfile.Revisions.Count;var reopened=new DesignLabHistory(path,current);
                    Assert.That(reopened.StorageError,Is.Null);Assert.That(reopened.SelectedProfile.Revisions.Count,Is.EqualTo(count));
                }
                finally {if(File.Exists(path))File.Delete(path);}
            }
        }
    }
}
