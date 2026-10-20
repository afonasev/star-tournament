using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    /// <summary>Session-clock additive pose over absolute clip samples; presentation only.</summary>
    internal sealed class VectorAirbornePose
    {
        readonly Dictionary<string,Transform> bones=new Dictionary<string,Transform>();
        readonly Transform actor;
        readonly ProvingProfile profile;
        readonly bool firstPerson;
        readonly float legLength;
        double previous,landed=double.NegativeInfinity;
        bool wasGrounded=true;
        float weight,vertical,landing;
        public Vector3 ViewOffset {get;private set;}
        public string State {get;private set;}="grounded";
        public VectorAirbornePose(Transform root,Transform actor,ProvingProfile profile,bool firstPerson)
        {
            foreach(var t in root.GetComponentsInChildren<Transform>(true))bones[t.name]=t;
            this.actor=actor;this.profile=profile;this.firstPerson=firstPerson;
            if(bones.ContainsKey("LeftUpperLeg")&&bones.ContainsKey("LeftLowerLeg")&&bones.ContainsKey("LeftFoot"))
                legLength=Vector3.Distance(bones["LeftUpperLeg"].position,bones["LeftLowerLeg"].position)+Vector3.Distance(bones["LeftLowerLeg"].position,bones["LeftFoot"].position);
        }
        float Get(string path)=>profile.Descriptor(path)!=null?profile.Get(path):0;
        public void Reset(double clock)
        {previous=clock;landed=double.NegativeInfinity;wasGrounded=true;weight=vertical=landing=0;ViewOffset=Vector3.zero;State="grounded";}
        public void Update(double clock,Vector3 velocity,bool grounded)
        {
            double delta=Math.Max(0,clock-previous);previous=clock;
            if(grounded&&!wasGrounded)landed=clock;
            wasGrounded=grounded;vertical=velocity.y;
            float response=Get("animation.airResponseSeconds");
            if(response<=0){weight=landing=0;ViewOffset=Vector3.zero;State="grounded";return;}
            float target=grounded?0:1;
            weight=target+(weight-target)*(float)Math.Exp(-delta/response);
            float duration=Get("animation.landSeconds");
            double t=clock-landed;
            landing=duration>0&&t>=0&&t<duration?(float)Math.Sin(Math.PI*t/duration):0;
            State=!grounded?(vertical>0?"rising":"falling"):landing>0?"landing":"grounded";
            ViewOffset=Vector3.down*Get("view.airMeters")*(weight+landing);
        }
        void Rotate(string name,float degrees)
        {
            if(!bones.TryGetValue(name,out var t))return;
            // Actor-space pitch axis expressed in the sampled bone's parent space.
            var axis=t.parent.InverseTransformDirection(actor.right);
            t.localRotation=Quaternion.AngleAxis(degrees,axis)*t.localRotation;
        }
        public void Apply()
        {
            if(firstPerson)return; // Both hands travel together; aim and grip remain intact.
            float hip=Get("animation.airHipDegrees")*weight;
            float knee=Get("animation.airKneeDegrees")*weight;
            float compression=Get("animation.landDegrees")*landing;
            // A symmetric tuck keeps the inherited capsule ragdoll balanced when death
            // begins in mid-air. Adjustable amplitudes live in the profile.
            Rotate("LeftUpperLeg",-hip-compression);
            Rotate("RightUpperLeg",-hip-compression);
            Rotate("LeftLowerLeg",knee+compression*2);
            Rotate("RightLowerLeg",knee+compression*2);
            if(bones.TryGetValue("Hips",out var hips))
                hips.localPosition+=hips.parent.InverseTransformVector(-actor.up*(legLength*(1-Mathf.Cos(compression*Mathf.Deg2Rad))));
        }
    }
}
