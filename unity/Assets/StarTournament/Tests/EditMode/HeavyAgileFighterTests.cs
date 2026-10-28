using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class HeavyAgileFighterTests
    {
        static ProvingProfile Previous()=>(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeHeavyAgileGait",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ProvingProfile.CreateTrooperDefault(),null);
        [TestCase(30)][TestCase(60)][TestCase(120)]
        public void SprintCadenceIsReducedWithoutMovingTheActor(int fps)
        {
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/StarTournament/Trooper/TrooperBody.prefab"));
            try
            {
                var visual=root.AddComponent<TrooperVisual>();var profile=ProvingProfile.CreateTrooperDefault();
                visual.Initialize(TrooperVisual.ClipNames.Select(n=>AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/StarTournament/Trooper/"+n+".anim")).ToArray(),profile);
                var gait=typeof(TrooperVisual).GetField("gait",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(visual);
                var count=gait.GetType().GetProperty("Steps");visual.Render(0,Vector3.zero,100,1);
                int warmup=0;
                for(int frame=1;frame<=fps*2;frame++)
                {
                    root.transform.position=Vector3.forward*(6f*frame/fps);var pose=root.transform.position;
                    visual.Render((double)frame/fps,Vector3.forward*6,100,1);
                    Assert.That(root.transform.position,Is.EqualTo(pose));
                    if(frame==fps)warmup=(int)count.GetValue(gait);
                }
                int steps=(int)count.GetValue(gait)-warmup;
                Assert.That(steps,Is.InRange(4,7),"A sustained sprint uses longer strides rather than the former roughly nine steps/second");
                var feet=root.GetComponentsInChildren<Transform>().Where(t=>t.name.EndsWith("Foot")).ToArray();
                Assert.That(feet.All(t=>float.IsFinite(t.position.y)),Is.True);
            }
            finally{Object.DestroyImmediate(root);}
        }
        [Test]
        public void StopStrafeReverseAndPivotRetainReachableSupport()
        {
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/StarTournament/Trooper/TrooperBody.prefab"));
            try
            {
                var visual=root.AddComponent<TrooperVisual>();visual.Initialize(TrooperVisual.ClipNames.Select(n=>AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/StarTournament/Trooper/"+n+".anim")).ToArray(),ProvingProfile.CreateTrooperDefault());
                var gait=typeof(TrooperVisual).GetField("gait",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(visual);
                var swing=gait.GetType().GetProperty("SwingFoot");var anchor=gait.GetType().GetMethod("Anchor");
                var bones=root.GetComponentsInChildren<Transform>();var feet=new[]{bones.Single(t=>t.name=="LeftFoot"),bones.Single(t=>t.name=="RightFoot")};
                visual.Render(0,Vector3.zero,100,1);float maxSlip=0,worst=0;
                for(int frame=1;frame<240;frame++)
                {
                    float t=frame/24f;Vector3 velocity=Vector3.zero;
                    if(t>=.5f&&t<3)velocity=Vector3.forward*(6*Mathf.Clamp01((t-.5f)/.5f));
                    else if(t>=3&&t<3.5f)velocity=Vector3.forward*(6*(1-(t-3)/.5f));
                    else if(t>=4&&t<5.3f)velocity=Vector3.right*3;
                    else if(t>=5.3f&&t<6.5f)velocity=Vector3.left*3;
                    else if(t>=7&&t<8)root.transform.rotation=Quaternion.Euler(0,(t-7)*100,0);
                    root.transform.position+=velocity/24;visual.Render(t,velocity,100,1);
                    for(int foot=0;foot<2;foot++)if(foot!=(int)swing.GetValue(gait))
                    {
                        float slip=Vector3.Distance(feet[foot].position,(Vector3)anchor.Invoke(gait,new object[]{foot}));
                        if(slip>maxSlip){maxSlip=slip;worst=t;}
                    }
                }
                Assert.That(maxSlip,Is.LessThan(.002f),"Support reach; worst time="+worst);
            }
            finally{Object.DestroyImmediate(root);}
        }
        [TestCase(false)][TestCase(true)]
        public void Version29SnapshotsAndCustomCadenceKeepTheirExactBytes(bool authored)
        {
            var previous=Previous();var current=ProvingProfile.CreateTrooperDefault();
            Assert.That(previous.Version,Is.EqualTo(29));Assert.That(current.Version,Is.EqualTo(30));
            Assert.That(previous.Get("animation.stepLengthMeters"),Is.EqualTo(.65f));
            foreach(var d in previous.Descriptors.Where(d=>!d.Path.StartsWith("animation.step")))Assert.That(current.Get(d.Path),Is.EqualTo(previous.Get(d.Path)),d.Path);
            Assert.That(current.Validate(),Is.Empty);
            string path=Path.Combine(Path.GetTempPath(),"heavy-agile-"+Guid.NewGuid()+".json");
            try
            {
                var history=new DesignLabHistory(path,new LabBundle{Profiles=new List<ProvingProfile>{previous}});
                if(authored){history.Create("Authored cadence");var snapshot=history.Selected.Snapshot;snapshot.Set("animation.stepLengthMeters",.8f);history.Save(snapshot);}else history.Rename("Default");
                var originals=history.Profiles.SelectMany(p=>p.Revisions).Select(r=>(r.Hash,JsonUtility.ToJson(r.Snapshot))).ToArray();
                var migrated=new DesignLabHistory(path,new LabBundle{Profiles=new List<ProvingProfile>{current}});
                Assert.That(migrated.StorageError,Is.Null);Assert.That(migrated.Selected.Snapshot.Get("animation.stepLengthMeters"),Is.EqualTo(authored?.8f:1.1f));
                foreach(var original in originals)Assert.That(migrated.Profiles.SelectMany(p=>p.Revisions).Any(r=>r.Hash==original.Hash&&JsonUtility.ToJson(r.Snapshot)==original.Item2),Is.True);
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
    }
}
