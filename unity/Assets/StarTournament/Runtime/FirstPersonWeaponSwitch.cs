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
        int revision=-1;
        public FirstPersonWeaponSwitch(GameObject view,Camera camera)
        {
            root=view.transform;axis=camera.transform;model=view.GetComponent<WeaponModelPresentation>();
            visual=view.GetComponent<TrooperVisual>();var profile=visual.Tuning;
            rest=new Vector3(profile.Get("view.x"),profile.Get("view.y"),profile.Get("view.z"));root.localPosition=rest;
            var bones=view.GetComponentsInChildren<Transform>(true);
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
                revision=visual.PoseRevision;
            }
            // Start from the sampled clip each time, including repeated render and completion-tick muzzle reads.
            for(int i=0;i<joints.Length;i++)joints[i].localRotation=sampled[i];
            for(int i=0;i<joints.Length;i++)
                joints[i].rotation=Quaternion.AngleAxis(-angles[i]*pose.Lift,axis.right)*joints[i].rotation;
            root.localPosition=rest+visual.WalkOffset;model?.Show(pose.Weapon);
        }
    }
}
