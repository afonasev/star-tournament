using System;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    /// <summary>Presentation derived from the authoritative remaining switch time, with no local clock.</summary>
    public readonly struct WeaponSwitchPose
    {
        public readonly WeaponId Weapon;
        public readonly float Lift;
        public WeaponSwitchPose(WeaponId weapon,float lift) { Weapon=weapon;Lift=lift; }
        public static WeaponSwitchPose Read(CombatLifeState state,double duration)
        {
            if(state.Dead || state.SwitchRemaining<=0)return new WeaponSwitchPose(state.SelectedWeapon,0);
            if(!(duration>0))throw new ArgumentOutOfRangeException(nameof(duration));
            double progress=Math.Max(0,Math.Min(1,1-state.SwitchRemaining/duration));
            // Equal halves and smoothstep endpoints are the shared animation shape, not a second gameplay duration.
            float half=(float)(progress<.5?progress*2:(1-progress)*2);
            float lift=half*half*(3-2*half);
            // Compare the authoritative timer directly so subtraction rounding cannot swap just before the apex.
            bool rising=state.SwitchRemaining>duration*.5;
            return new WeaponSwitchPose(rising?state.SelectedWeapon:state.PendingWeapon,lift);
        }
    }

    internal sealed class FirstPersonWeaponSwitch
    {
        readonly Transform root,axis;
        readonly WeaponModelPresentation model;
        readonly TrooperVisual visual;
        readonly Transform[] joints;
        readonly Quaternion[] sampled;
        readonly float[] angles;
        readonly Vector3 rest;
        readonly Camera camera;
        readonly Transform leftHand,rightHand,mount,leftShoulder,rightShoulder,leftPalm,rightPalm;
        Vector3 leftShoulderSample,rightShoulderSample;
        Quaternion leftSample,rightSample,mountSampleRotation;
        Vector3 mountSamplePosition;
        int revision=-1;
        public FirstPersonWeaponSwitch(GameObject view,Camera camera)
        {
            this.camera=camera;root=view.transform;axis=camera.transform;model=view.GetComponent<WeaponModelPresentation>();
            visual=view.GetComponent<TrooperVisual>();var profile=visual.Tuning;
            rest=new Vector3(profile.Get("view.x"),profile.Get("view.y"),profile.Get("view.z"));root.localPosition=rest;
            var bones=view.GetComponentsInChildren<Transform>(true);
            leftShoulder=Array.Find(bones,b=>b.name=="LeftShoulder");rightShoulder=Array.Find(bones,b=>b.name=="RightShoulder");
            leftPalm=Array.Find(bones,b=>b.name=="LeftMiddleProximal");rightPalm=Array.Find(bones,b=>b.name=="RightMiddleProximal");
            leftHand=Array.Find(bones,b=>b.name=="LeftHand");rightHand=Array.Find(bones,b=>b.name=="RightHand");
            mount=Array.Find(bones,b=>b.name=="trooper:rig:weapon-mount");
            // Joint names are the imported Humanoid rig contract; pivots never translate.
            joints=new Transform[4];sampled=new Quaternion[4];angles=new float[4];
            string[] names={"LeftUpperArm","RightUpperArm","LeftLowerArm","RightLowerArm"};
            for(int i=0;i<names.Length;i++)
            {
                joints[i]=Array.Find(bones,b=>b.name==names[i]);
                if(!joints[i])throw new InvalidOperationException("Weapon switch joint missing: "+names[i]);
                angles[i]=profile.Get(i<2?"view.switchShoulderDegrees":"view.switchElbowDegrees");
            }
        }
        public void Apply(CombatLifeState state,double duration)
        {
            var pose=WeaponSwitchPose.Read(state,duration);
            if(revision!=visual.PoseRevision)
            {
                for(int i=0;i<joints.Length;i++)sampled[i]=joints[i].localRotation;
                leftShoulderSample=leftShoulder.localPosition;rightShoulderSample=rightShoulder.localPosition;
                leftSample=leftHand.localRotation;rightSample=rightHand.localRotation;
                mountSamplePosition=mount.localPosition;mountSampleRotation=mount.localRotation;
                revision=visual.PoseRevision;
            }
            // Start from the sampled clip each time, including repeated render and completion-tick muzzle reads.
            for(int i=0;i<joints.Length;i++)joints[i].localRotation=sampled[i];
            leftShoulder.localPosition=leftShoulderSample;rightShoulder.localPosition=rightShoulderSample;
            leftHand.localRotation=leftSample;rightHand.localRotation=rightSample;
            mount.localPosition=mountSamplePosition;mount.localRotation=mountSampleRotation;
            for(int i=0;i<joints.Length;i++)
                joints[i].rotation=Quaternion.AngleAxis(-angles[i]*pose.Lift,axis.right)*joints[i].rotation;
            root.localPosition=rest+visual.ViewOffset;model?.Show(pose.Weapon);
            if(visual.Tuning.Version>=28&&visual.Tuning.Get("view.armEntryEnabled")>0)
            {
                var weaponPosition=mount.position;var weaponRotation=mount.rotation;
                FitEntry(leftShoulder,joints[2],leftHand,leftPalm,visual.Tuning.Get("view.armEntryLeftX"));
                FitEntry(rightShoulder,joints[3],rightHand,rightPalm,visual.Tuning.Get("view.armEntryRightX"));
                mount.SetPositionAndRotation(weaponPosition,weaponRotation);
            }
        }
        void FitEntry(Transform shoulder,Transform elbow,Transform hand,Transform palm,float horizontalOffset)
        {
            var b=elbow.position;var c=hand.position;var handRotation=hand.rotation;
            float length=Vector3.Distance(b,c);
            // Height-relative coordinates preserve the physical ray across split-screen aspect ratios.
            float viewportX=.5f+horizontalOffset/camera.aspect;
            var ray=camera.ViewportPointToRay(new Vector3(viewportX,0,0));
            var normal=Vector3.Cross(c-ray.origin,ray.direction).normalized;
            var direction=Vector3.ProjectOnPlane(palm.position-c,normal).normalized;
            // Singular plane guard, not art tuning. No change is safer than a collapsed forearm.
            if(direction.sqrMagnitude<.000001f)return;
            var target=c-direction*length;
            // The first-person sleeve's hidden shoulder attachment may move; upper/lower arm
            // lengths, palm contact and weapon pose cannot. World-body rigs never use this pass.
            shoulder.position+=target-b;
            elbow.rotation=Quaternion.FromToRotation(hand.position-elbow.position,c-elbow.position)*elbow.rotation;
            hand.rotation=handRotation;
        }
    }
}
