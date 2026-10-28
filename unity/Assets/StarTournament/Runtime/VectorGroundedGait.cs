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
            public Vector3 Rest,UpperRest,Anchor,From,Target;
            public float Length;
            public Quaternion Rotation,AnchorRotation,FromRotation,TargetRotation;
        }
        readonly Leg[] legs=new Leg[2];
        readonly Vector3[] targets=new Vector3[2];
        readonly Transform actor;
        readonly Transform hips;
        readonly ProvingProfile profile;
        readonly bool hasHeavy;
        bool Heavy => hasHeavy && profile.Get("animation.heavy.enabled")>0;
        readonly Vector3 restHips;
        readonly Quaternion restHipRotation;
        double previous,stepAt;
        Vector3 previousPosition;
        Vector3 filteredVelocity;
        float duration,height;
        int swing=-1,next;
        int steps;
        bool firstStep;
        bool initialized,wasGrounded;
        public int SwingFoot => swing;
        public int Steps => steps;
        public Vector3 Anchor(int side)=>legs[side].Anchor;
        public VectorGroundedGait(Transform root,Transform actor,ProvingProfile profile)
        {
            this.actor=actor;this.profile=profile;hasHeavy=profile.Descriptors.Any(d=>d.Path=="animation.heavy.enabled");
            var bones=root.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
            hips=bones["Hips"];
            restHips=hips.localPosition;restHipRotation=hips.localRotation;
            for(int i=0;i<2;i++)
            {
                string side=i==0?"Left":"Right";
                if(!bones.TryGetValue(side+"Foot",out var foot))return;
                legs[i]=new Leg{Upper=bones[side+"UpperLeg"],Lower=bones[side+"LowerLeg"],Foot=foot,
                    Rest=actor.InverseTransformPoint(foot.position),Rotation=Quaternion.Inverse(actor.rotation)*foot.rotation};
                legs[i].UpperRest=actor.InverseTransformPoint(legs[i].Upper.position);
                legs[i].Length=Vector3.Distance(legs[i].Upper.position,legs[i].Lower.position)+Vector3.Distance(legs[i].Lower.position,foot.position);
            }
        }
        public void Reset(double clock)
        {initialized=false;previous=clock;previousPosition=actor.position;swing=-1;next=0;steps=0;wasGrounded=false;filteredVelocity=Vector3.zero;firstStep=true;}
        public void Update(double clock,bool grounded)
        {
            if(legs[0]==null||legs[1]==null)return;
            double dt=Math.Max(0,clock-previous);
            var displacement=Vector3.ProjectOnPlane(actor.position-previousPosition,actor.up);
            if(!initialized||!grounded||!wasGrounded||clock<previous)
            {
                foreach(var leg in legs){leg.Anchor=actor.TransformPoint(leg.Rest);leg.AnchorRotation=actor.rotation*leg.Rotation;}
                swing=-1;initialized=true;filteredVelocity=Vector3.zero;firstStep=true;
            }
            if(grounded&&dt>0)
            {
                var velocity=displacement/(float)dt;float speed=velocity.magnitude;
                float response=hasHeavy?profile.Get("animation.heavy.velocityResponseSeconds"):0;
                filteredVelocity=Vector3.Lerp(velocity,filteredVelocity,response>0?(float)Math.Exp(-dt/response):0);
                if(swing>=0&&speed>1e-5f)
                {
                    float nextDuration=Mathf.Min(profile.Get("animation.stepSeconds"),profile.Get("animation.stepLengthMeters")/speed);
                    // The first support foot starts under the pelvis, not half a
                    // stride ahead. Its first transfer has only half the reach.
                    if(firstStep&&Heavy)nextDuration*=.5f;
                    // Preserve the current phase while adapting to acceleration. A
                    // first slow sample must not hold the support foot through a sprint.
                    double phase=Math.Max(0,previous-stepAt)/duration;
                    if(Heavy)
                    {
                        // At a reversal the old support can be on the wrong side
                        // of the body. Land the moving foot before that planted
                        // leg exhausts its geometric reach; never stretch bones.
                        float available=Mathf.Max(0,SupportSeconds(legs[1-swing],velocity)-(float)dt);
                        nextDuration=Mathf.Min(nextDuration,Mathf.Max(1e-6f,available/(float)Math.Max(1e-6,1-phase)));
                    }
                    stepAt=previous-phase*nextDuration;duration=nextDuration;
                }
                if(swing>=0&&clock-stepAt>=duration)
                {
                    if(Heavy)
                    {
                        float lead=Mathf.Min(profile.Get("animation.stepSeconds"),profile.Get("animation.stepLengthMeters")/Mathf.Max(speed,1e-5f));
                        legs[swing].Target=actor.TransformPoint(legs[swing].Rest)+velocity*lead*.5f;
                    }
                    legs[swing].Anchor=legs[swing].Target;legs[swing].AnchorRotation=legs[swing].TargetRotation;next=1-swing;swing=-1;firstStep=false;
                }
                // Epsilon is only floating point noise, not the old walk/idle speed dead zone.
                bool moving=displacement.sqrMagnitude>1e-10f;
                bool turning=Heavy&&legs.Any(leg=>Quaternion.Angle(leg.AnchorRotation,actor.rotation*leg.Rotation)>profile.Get("animation.heavy.turnStepDegrees"));
                if(swing<0&&(moving||turning))
                {
                    swing=next;steps++;stepAt=clock-dt;
                    duration=Mathf.Min(profile.Get("animation.stepSeconds"),profile.Get("animation.stepLengthMeters")/Mathf.Max(speed,1e-5f));
                    if(firstStep&&Heavy)duration*=.5f;
                    if(Heavy&&speed>1e-5f)duration=Mathf.Min(duration,Mathf.Max((float)dt,SupportSeconds(legs[1-swing],velocity)));
                    var leg=legs[swing];leg.From=leg.Anchor;
                    float lead=Heavy?Mathf.Min(profile.Get("animation.stepSeconds"),profile.Get("animation.stepLengthMeters")/Mathf.Max(speed,1e-5f)):duration;
                    leg.Target=actor.TransformPoint(leg.Rest)+velocity*lead*.5f;
                    leg.FromRotation=leg.AnchorRotation;leg.TargetRotation=actor.rotation*leg.Rotation;
                    height=profile.Get("animation.stepHeightMeters")*Mathf.Sqrt(Mathf.Clamp01(speed*duration/profile.Get("animation.stepLengthMeters")));
                    if(turning)height=Mathf.Max(height,profile.Get("animation.stepHeightMeters"));
                }
                if(swing>=0)
                {
                    // Keep the landing under the moving body, including a reversal or short tap.
                    var leg=legs[swing];float t=Mathf.Clamp01((float)(clock-stepAt)/duration);
                    float lead=Heavy?Mathf.Min(profile.Get("animation.stepSeconds"),profile.Get("animation.stepLengthMeters")/Mathf.Max(speed,1e-5f)):duration;
                    // Direction can ease through a turn, but forward reach must
                    // anticipate the next support phase even during acceleration.
                    var heading=Heavy&&filteredVelocity.sqrMagnitude>1e-10f&&Vector3.Dot(filteredVelocity,velocity)>0?filteredVelocity.normalized*speed:velocity;
                    var desired=actor.TransformPoint(leg.Rest)+heading*lead*.5f;
                    leg.Target=Vector3.Lerp(leg.Target,desired,t);
                    leg.TargetRotation=actor.rotation*leg.Rotation;
                    height=Mathf.Max(height,profile.Get("animation.stepHeightMeters")*Mathf.Sqrt(Mathf.Clamp01(Vector3.Distance(leg.From,leg.Target)/profile.Get("animation.stepLengthMeters"))));
                }
            }
            previous=clock;previousPosition=actor.position;wasGrounded=grounded;
        }
        float SupportSeconds(Leg leg,Vector3 velocity)
        {
            var offset=Vector3.ProjectOnPlane(actor.TransformPoint(leg.UpperRest)-leg.Anchor,actor.up);
            float speedSquared=velocity.sqrMagnitude;
            if(speedSquared<1e-10f)return float.PositiveInfinity;
            float along=Vector3.Dot(offset,velocity);
            float radius=leg.Length-1e-4f; // Numerical margin on the exact two-bone reach.
            float determinant=along*along+speedSquared*(radius*radius-offset.sqrMagnitude);
            return Mathf.Max(0,(-along+Mathf.Sqrt(Mathf.Max(0,determinant)))/speedSquared);
        }
        public void Apply(double clock,float landingDrop=0)
        {
            if(!wasGrounded||legs[0]==null||legs[1]==null)return;
            bool heavy=Heavy;
            if(heavy)
            {
                // Own one coherent grounded cycle. Imported hip bounce runs at a
                // different clip rate and would otherwise fight planted-leg IK.
                var local=actor.InverseTransformDirection(filteredVelocity);
                float run=Mathf.Max(.001f,profile.Get("animation.runThreshold"));
                hips.localPosition=restHips-hips.parent.InverseTransformVector(actor.up*landingDrop);
                hips.localRotation=restHipRotation*Quaternion.Euler(
                    Mathf.Clamp(local.z/run,-1,1)*profile.Get("animation.heavy.leanDegrees"),0,
                    -Mathf.Clamp(local.x/run,-1,1)*profile.Get("animation.heavy.leanDegrees"));
            }
            float lowerHips=0;
            if(heavy)
            {
                // Keep the pelvis at the clearance required by the whole planned
                // stride, not a new height for every clip frame. This avoids a
                // deep squat/rise oscillation when the feet exchange support.
                float halfStride=Mathf.Min(profile.Get("animation.stepLengthMeters"),filteredVelocity.magnitude*profile.Get("animation.stepSeconds"))*.5f;
                foreach(var leg in legs)
                {
                    float restHeight=Vector3.Dot(actor.TransformVector(leg.UpperRest-leg.Rest),actor.up);
                    lowerHips=Mathf.Max(lowerHips,restHeight-Mathf.Sqrt(Mathf.Max(0,leg.Length*leg.Length-halfStride*halfStride)));
                }
            }
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
                float phase=i==swing?Mathf.Clamp01((float)(clock-stepAt)/duration):0;
                leg.Foot.rotation=heavy?(i==swing?Quaternion.Slerp(leg.FromRotation,leg.TargetRotation,Mathf.SmoothStep(0,1,phase)):leg.AnchorRotation):actor.rotation*leg.Rotation;
                if(heavy&&i==swing)leg.Foot.rotation=Quaternion.AngleAxis(Mathf.Sin(phase*Mathf.PI*2)*profile.Get("animation.heavy.toePitchDegrees"),actor.right)*leg.Foot.rotation;
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
