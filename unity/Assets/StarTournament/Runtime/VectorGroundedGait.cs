using System;
using System.Linq;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    /// <summary>World-space planted feet for the visual body; never moves the actor or motor.</summary>
    internal sealed class VectorGroundedGait
    {
        sealed class Leg
        {
            public Transform Upper,Lower,Foot;
            public Vector3 Rest,Anchor,From,Target;
            public Quaternion Rotation;
        }
        readonly Leg[] legs=new Leg[2];
        readonly Vector3[] targets=new Vector3[2];
        readonly Transform actor;
        readonly Transform hips;
        readonly ProvingProfile profile;
        double previous,stepAt;
        Vector3 previousPosition;
        float duration,height;
        int swing=-1,next;
        bool initialized,wasGrounded;
        public int SwingFoot => swing;
        public Vector3 Anchor(int side)=>legs[side].Anchor;
        public VectorGroundedGait(Transform root,Transform actor,ProvingProfile profile)
        {
            this.actor=actor;this.profile=profile;
            var bones=root.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
            hips=bones["Hips"];
            for(int i=0;i<2;i++)
            {
                string side=i==0?"Left":"Right";
                if(!bones.TryGetValue(side+"Foot",out var foot))return;
                legs[i]=new Leg{Upper=bones[side+"UpperLeg"],Lower=bones[side+"LowerLeg"],Foot=foot,
                    Rest=actor.InverseTransformPoint(foot.position),Rotation=Quaternion.Inverse(actor.rotation)*foot.rotation};
            }
        }
        public void Reset(double clock)
        {initialized=false;previous=clock;previousPosition=actor.position;swing=-1;next=0;wasGrounded=false;}
        public void Update(double clock,bool grounded)
        {
            if(legs[0]==null||legs[1]==null)return;
            double dt=Math.Max(0,clock-previous);
            var displacement=Vector3.ProjectOnPlane(actor.position-previousPosition,actor.up);
            if(!initialized||!grounded||!wasGrounded||clock<previous)
            {
                foreach(var leg in legs)leg.Anchor=actor.TransformPoint(leg.Rest);
                swing=-1;initialized=true;
            }
            if(grounded&&dt>0)
            {
                var velocity=displacement/(float)dt;float speed=velocity.magnitude;
                if(swing>=0&&speed>1e-5f)
                {
                    float nextDuration=Mathf.Min(profile.Get("animation.stepSeconds"),profile.Get("animation.stepLengthMeters")/speed);
                    // Preserve the current phase while adapting to acceleration. A
                    // first slow sample must not hold the support foot through a sprint.
                    double phase=Math.Max(0,previous-stepAt)/duration;
                    stepAt=previous-phase*nextDuration;duration=nextDuration;
                }
                if(swing>=0&&clock-stepAt>=duration)
                {legs[swing].Anchor=legs[swing].Target;next=1-swing;swing=-1;}
                // Epsilon is only floating point noise, not the old walk/idle speed dead zone.
                bool moving=displacement.sqrMagnitude>1e-10f;
                if(swing<0&&moving)
                {
                    swing=next;stepAt=clock-dt;
                    duration=Mathf.Min(profile.Get("animation.stepSeconds"),profile.Get("animation.stepLengthMeters")/Mathf.Max(speed,1e-5f));
                    var leg=legs[swing];leg.From=leg.Anchor;
                    leg.Target=actor.TransformPoint(leg.Rest)+velocity*duration*.5f;
                    height=profile.Get("animation.stepHeightMeters")*Mathf.Sqrt(Mathf.Clamp01(speed*duration/profile.Get("animation.stepLengthMeters")));
                }
                if(swing>=0)
                {
                    // Keep the landing under the moving body, including a reversal or short tap.
                    var leg=legs[swing];float t=Mathf.Clamp01((float)(clock-stepAt)/duration);
                    var desired=actor.TransformPoint(leg.Rest)+velocity*duration*.5f;
                    leg.Target=Vector3.Lerp(leg.Target,desired,t);
                    height=Mathf.Max(height,profile.Get("animation.stepHeightMeters")*Mathf.Sqrt(Mathf.Clamp01(Vector3.Distance(leg.From,leg.Target)/profile.Get("animation.stepLengthMeters"))));
                }
            }
            previous=clock;previousPosition=actor.position;wasGrounded=grounded;
        }
        public void Apply(double clock)
        {
            if(!wasGrounded||legs[0]==null||legs[1]==null)return;
            float lowerHips=0;
            for(int i=0;i<2;i++)
            {
                var leg=legs[i];var target=leg.Anchor;
                if(i==swing)
                {
                    float t=Mathf.Clamp01((float)(clock-stepAt)/duration);
                    target=Vector3.Lerp(leg.From,leg.Target,Mathf.SmoothStep(0,1,t))+actor.up*(Mathf.Sin(Mathf.PI*t)*height);
                }
                targets[i]=target;
                float length=Vector3.Distance(leg.Upper.position,leg.Lower.position)+Vector3.Distance(leg.Lower.position,leg.Foot.position);
                var offset=leg.Upper.position-target;float horizontal=Vector3.ProjectOnPlane(offset,actor.up).sqrMagnitude;
                // Geometric reach constraint, not a designer offset: bend the knees
                // instead of stretching a planted leg or letting its foot slide.
                float available=Mathf.Sqrt(Mathf.Max(0,length*length-horizontal));
                lowerHips=Mathf.Max(lowerHips,Vector3.Dot(offset,actor.up)-available+1e-5f);
            }
            hips.position-=actor.up*lowerHips;
            for(int i=0;i<2;i++)
            {
                var leg=legs[i];var target=targets[i];
                Reach(leg,target);
                leg.Foot.rotation=actor.rotation*leg.Rotation;
            }
        }
        void Reach(Leg leg,Vector3 target)
        {
            var a=leg.Upper.position;var b=leg.Lower.position;var c=leg.Foot.position;
            float first=Vector3.Distance(a,b),second=Vector3.Distance(b,c);
            var delta=target-a;float distance=Mathf.Clamp(delta.magnitude,Mathf.Abs(first-second)+1e-6f,first+second-1e-6f);
            var direction=delta.normalized;var bend=Vector3.ProjectOnPlane(actor.forward,direction).normalized;
            float along=(first*first+distance*distance-second*second)/(2*distance);
            var knee=a+direction*along+bend*Mathf.Sqrt(Mathf.Max(0,first*first-along*along));
            leg.Upper.rotation=Quaternion.FromToRotation(b-a,knee-a)*leg.Upper.rotation;
            leg.Lower.rotation=Quaternion.FromToRotation(leg.Foot.position-leg.Lower.position,target-leg.Lower.position)*leg.Lower.rotation;
        }
    }
}
