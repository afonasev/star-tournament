using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class FirstPersonWalkTests
    {
        GameObject owner,view;TrooperVisual visual;ProvingProfile profile;
        object transition;MethodInfo apply;
        [SetUp] public void Load()
        {
            owner=new GameObject("walk-test-camera");var camera=owner.AddComponent<Camera>();
            view=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/StarTournament/Trooper/TrooperArms.prefab"),owner.transform);
            profile=ProvingProfile.CreateTrooperDefault();visual=view.AddComponent<TrooperVisual>();
            visual.Initialize(TrooperVisual.ClipNames.Select(n=>AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/StarTournament/Trooper/"+n+".anim")).ToArray(),profile,true);
            var type=typeof(TrooperVisual).Assembly.GetType("StarTournament.ProvingGround.FirstPersonWeaponSwitch");
            transition=Activator.CreateInstance(type,new object[]{view,camera});apply=type.GetMethod("Apply");
        }
        [TearDown] public void Cleanup(){UnityEngine.Object.DestroyImmediate(view);UnityEngine.Object.DestroyImmediate(owner);}
        void Render(double time,Vector3 velocity,bool grounded=true,CombatLifeState? state=null)
        {
            visual.Render(time,velocity,100,1,grounded);
            apply.Invoke(transition,new object[]{state??new CombatLifeState{SelectedWeapon=WeaponId.Rifle},1d});
        }
        [Test] public void MovingHandsUseSteadyAimWithSmallSlowStepsAndFrozenSamples()
        {
            var speed=Vector3.forward*8;visual.ResetPose(0);var rest=view.transform.localPosition;
            int horizontalCrossings=0,verticalCrossings=0;Vector3 previous=Vector3.zero;float maximumDelta=0;
            for(int tick=1;tick<=260;tick++)
            {
                Render(tick*.02,speed);var offset=visual.WalkOffset;
                Assert.That(visual.State,Is.EqualTo("aim"),"Body walk/run clips must not shake first-person hands");
                Assert.That(Mathf.Abs(offset.x),Is.LessThanOrEqualTo(profile.Get("view.walkHorizontalMeters")));
                Assert.That(Mathf.Abs(offset.y),Is.LessThanOrEqualTo(profile.Get("view.walkVerticalMeters")));
                Assert.That(Vector3.Distance(view.transform.localPosition,rest+offset),Is.LessThan(.000001f));
                if(offset.x*previous.x<0)horizontalCrossings++;if(offset.y*previous.y<0)verticalCrossings++;
                maximumDelta=Mathf.Max(maximumDelta,Vector3.Distance(offset,previous));previous=offset;
            }
            Assert.That(horizontalCrossings,Is.InRange(7,8));Assert.That(verticalCrossings,Is.InRange(15,16));
            Assert.That(maximumDelta,Is.LessThan(.0015f),"No sharp per-tick translation at the default rhythm");
            var bones=view.GetComponentsInChildren<Transform>().Where(t=>t.name=="RightHand"||t.name=="LeftHand"||t.name=="trooper:rig:weapon-mount").ToArray();
            var positions=bones.Select(t=>t.position).ToArray();var frozen=view.transform.localPosition;
            for(int repeat=0;repeat<5;repeat++)Render(5.2,speed);
            Assert.That(view.transform.localPosition,Is.EqualTo(frozen));
            for(int i=0;i<bones.Length;i++)Assert.That(Vector3.Distance(positions[i],bones[i].position),Is.LessThan(.000001f));
        }
        [Test] public void ResponseIsCadenceIndependentAndFadesAfterStoppingOrLeavingGround()
        {
            Vector3 Sample(float delta)
            {
                visual.ResetPose(0);for(int tick=1;tick<=Mathf.RoundToInt(.5f/delta);tick++)Render(tick*(double)delta,Vector3.forward*8);
                return visual.WalkOffset;
            }
            var slow=Sample(.02f);var fast=Sample(.01f);Assert.That(Vector3.Distance(slow,fast),Is.LessThan(.000001f));
            Assert.That(fast.magnitude,Is.GreaterThan(.002f));
            Render(.51,Vector3.zero);Assert.That(Vector3.Distance(fast,visual.WalkOffset),Is.LessThan(.001f));
            for(int tick=52;tick<=200;tick++)Render(tick*.01,Vector3.zero);
            Assert.That(visual.WalkOffset.magnitude,Is.LessThan(.00001f));
            visual.ResetPose(0);Render(.5,Vector3.forward*8);Render(2,Vector3.forward*8,false);
            Assert.That(visual.WalkOffset.magnitude,Is.LessThan(.00001f),"No continuing ground steps in air");
        }
        [Test] public void WalkingKeepsCombatClipsSwitchPhaseAndResetsOnNewLife()
        {
            Render(.3,Vector3.forward*8);var state=new CombatLifeState{SelectedWeapon=WeaponId.Rifle,PendingWeapon=WeaponId.Shotgun,SwitchRemaining=.5};
            var shoulder=view.GetComponentsInChildren<Transform>().Single(t=>t.name=="RightUpperArm");var pivot=shoulder.localPosition;
            Render(.4,Vector3.forward*8,true,state);var rotation=shoulder.localRotation;
            apply.Invoke(transition,new object[]{state,1d});Assert.That(Quaternion.Angle(rotation,shoulder.localRotation),Is.LessThan(.001f));
            Assert.That(shoulder.localPosition,Is.EqualTo(pivot));Assert.That(WeaponSwitchPose.Read(state,1).Lift,Is.EqualTo(1));
            visual.Fire(.4);Render(.42,Vector3.forward*8);Assert.That(visual.State,Is.EqualTo("fire"));
            visual.Render(.44,Vector3.forward*8,90,1);Assert.That(visual.State,Is.EqualTo("hit"));
            visual.Render(.5,Vector3.zero,100,2);Assert.That(visual.WalkOffset,Is.EqualTo(Vector3.zero));Assert.That(visual.State,Is.EqualTo("aim"));
            visual.BeginDeath(.6);Assert.That(visual.WalkOffset,Is.EqualTo(Vector3.zero));
        }
        [Test] public void SerializedTrooperUpgradeRefreshesCachedValuesAndPreservesCustomGrip()
        {
            var old=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeSmoothFirstPersonWalk",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(profile,null);
            old.Set("grip.supportRoll",40);
            var serialized=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(old));
            serialized.EnsureTrooperDescriptors();
            foreach(var descriptor in serialized.Descriptors)
            {
                Assert.That(serialized.Get(descriptor.Path),Is.Not.NaN,descriptor.Path);
                serialized.Set(descriptor.Path,serialized.Get(descriptor.Path));
            }
            Assert.That(serialized.Get("view.walkCycleSeconds"),Is.EqualTo(1.3f));
            Assert.That(serialized.Get("grip.supportRoll"),Is.EqualTo(40));Assert.That(serialized.Validate(),Is.Empty);
        }
        [TestCase(false)][TestCase(true)] public void LabUpgradePreservesOldRevisionsAndCustomValues(bool olderInput)
        {
            var current=DesignLabHistoryTests.Shipped();
            var old=new LabBundle{Profiles=current.Profiles.Select(p=>(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeSmoothFirstPersonWalk",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,null)).ToList()};
            if(olderInput)old.Profiles=old.Profiles.Select(p=>(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeSmoothGamepadTap",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,null)).ToList();
            string path=Path.Combine(Path.GetTempPath(),"walk-history-"+Guid.NewGuid()+".json");
            try
            {
                var history=new DesignLabHistory(path,old);history.Create("Мои настройки");var draft=history.Selected.Snapshot;
                draft.Set("rifle.damage",31);draft.Set("animation.blendSeconds",.3f);history.Save(draft);
                string hash=history.Selected.Hash;int number=history.Selected.Number;string id=history.SelectedProfileId;
                var upgraded=new DesignLabHistory(path,current);Assert.That(upgraded.StorageError,Is.Null);
                Assert.That(upgraded.SelectedProfileId,Is.EqualTo(id));Assert.That(upgraded.Selected.Number,Is.GreaterThan(number));
                Assert.That(upgraded.Selected.Snapshot.Get("rifle.damage"),Is.EqualTo(31));Assert.That(upgraded.Selected.Snapshot.Get("animation.blendSeconds"),Is.EqualTo(.3f));
                Assert.That(upgraded.Selected.Snapshot.Get("view.walkCycleSeconds"),Is.EqualTo(1.3f));
                Assert.That(upgraded.SelectedProfile.Revisions.Single(r=>r.Number==number).Hash,Is.EqualTo(hash));
                int count=upgraded.SelectedProfile.Revisions.Count;var reopened=new DesignLabHistory(path,current);
                Assert.That(reopened.StorageError,Is.Null);Assert.That(reopened.SelectedProfile.Revisions.Count,Is.EqualTo(count));
                var file=JsonUtility.FromJson<LabHistoryFile>(File.ReadAllText(path));var legacy=file.Profiles.Single(p=>p.Id==id).Revisions.Single(r=>r.Number==number);
                legacy.Snapshot.Profiles.Single(p=>p.Id=="unity-trooper-presentation-v1").Descriptors.Single(d=>d.Path=="animation.blendSeconds").Maximum=99;
                File.WriteAllText(path,JsonUtility.ToJson(file));
                var sanitized=new DesignLabHistory(path,current);Assert.That(sanitized.StorageError,Is.Null);
                Assert.That(sanitized.SelectedProfile.Revisions.Single(r=>r.Number==number).Snapshot.Descriptors.Single(d=>d.Path=="animation.blendSeconds").Maximum,Is.EqualTo(.5f),"File metadata cannot override the trusted registry");
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
    }
}
