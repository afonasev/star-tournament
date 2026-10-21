using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class VectorGroundedGaitTests
    {
        [TestCase(.03f,0f)][TestCase(2f,0f)][TestCase(6f,0f)][TestCase(1f,90f)][TestCase(1f,180f)]
        public void ActualDisplacementPlantsStanceFootAndShortMotionSteps(float speed,float angle)
        => CheckDisplacement(speed,angle,false);
        [Test] public void AccelerationKeepsTheFirstSupportFootWithinReach()=>CheckDisplacement(6,0,true);
        static void CheckDisplacement(float speed,float angle,bool acceleration)
        {
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/StarTournament/Trooper/TrooperBody.prefab"));
            try
            {
                var visual=root.AddComponent<TrooperVisual>();var profile=ProvingProfile.CreateTrooperDefault();
                visual.Initialize(TrooperVisual.ClipNames.Select(n=>AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/StarTournament/Trooper/"+n+".anim")).ToArray(),profile);
                var bones=root.GetComponentsInChildren<Transform>();var feet=new[]{bones.Single(t=>t.name=="LeftFoot"),bones.Single(t=>t.name=="RightFoot")};
                var gait=typeof(TrooperVisual).GetField("gait",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(visual);
                var swing=gait.GetType().GetProperty("SwingFoot");var anchor=gait.GetType().GetMethod("Anchor");
                visual.Render(0,Vector3.zero,100,1);float maxLift=0,maxSlip=0;var initial=feet.Select(t=>t.position).ToArray();
                var direction=Quaternion.Euler(0,angle,0)*Vector3.forward;
                for(int i=1;i<=60;i++)
                {
                    float actualSpeed=acceleration?Mathf.Min(speed,i*.8f):speed;
                    root.transform.position+=direction*actualSpeed*.02f;var position=root.transform.position;
                    visual.Render(i*.02,direction*actualSpeed,100,1);
                    int moving=(int)swing.GetValue(gait);
                    for(int foot=0;foot<2;foot++)
                    {
                        if(foot!=moving)maxSlip=Mathf.Max(maxSlip,Vector3.Distance(feet[foot].position,(Vector3)anchor.Invoke(gait,new object[]{foot})));
                        maxLift=Mathf.Max(maxLift,feet[foot].position.y-initial[foot].y);
                    }
                    Assert.That(root.transform.position,Is.EqualTo(position),"Presentation never drives root motion");
                }
                Assert.That(maxSlip,Is.LessThan(.002f),"Stance contact stays planted in world space");
                Assert.That(maxLift,Is.GreaterThan(.0002f),"Even motion below the old walk threshold lifts a foot");
                Assert.That(visual.State,Is.EqualTo(speed<profile.Get("animation.runThreshold")?"walk":"run"));
                var frozen=bones.Select(t=>(t.localPosition,t.localRotation)).ToArray();
                for(int repeat=0;repeat<4;repeat++)visual.Render(1.2,direction*speed,100,1);
                for(int i=0;i<bones.Length;i++)
                {Assert.That(Vector3.Distance(frozen[i].localPosition,bones[i].localPosition),Is.LessThan(1e-6f));Assert.That(Quaternion.Angle(frozen[i].localRotation,bones[i].localRotation),Is.LessThan(.01f));}
                visual.Render(1.3,new Vector3(0,4,0),100,1,false);Assert.That(visual.AirborneState,Is.EqualTo("rising"));
                Assert.That((int)swing.GetValue(gait),Is.EqualTo(-1),"Feet release from floor in air");
                visual.Render(1.5,new Vector3(0,-2,0),100,1,false);Assert.That(visual.AirborneState,Is.EqualTo("falling"));
                visual.ResetPose(2);visual.Render(2,Vector3.zero,100,2);Assert.That(visual.State,Is.EqualTo("idle"));
            }
            finally{Object.DestroyImmediate(root);}
        }
    }
}
