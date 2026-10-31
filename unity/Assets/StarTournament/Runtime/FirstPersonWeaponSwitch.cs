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
        readonly string[][] commonPaths=new string[2][];
        readonly Transform[] fingerBones=new Transform[30];
        readonly Quaternion[] fingerSamples=new Quaternion[30];
        readonly Vector3[] fingerTargets=new Vector3[10];
        readonly Transform leftHand,rightHand,mount,leftShoulder,rightShoulder,leftPalm,rightPalm;
        readonly Quaternion leftHandBind=Quaternion.identity,rightHandBind=Quaternion.identity;
        readonly Transform[] sleeveTwists=new Transform[4];
        readonly Quaternion[] sleeveTwistBind=new Quaternion[4];
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
            int fingerIndex=0;
            foreach(var side in new[]{"Left","Right"})foreach(var digit in new[]{"Thumb","Index","Middle","Ring","Little"})foreach(var segment in new[]{"Proximal","Intermediate","Distal"})
                fingerBones[fingerIndex++]=Array.Find(bones,b=>b.name==side+digit+segment);
            // Joint names are the imported Humanoid rig contract. The hidden view-only shoulder attachment may translate.
            for(int side=0;side<2;side++){string prefix="view.common"+(side==0?"Left":"Right");commonPaths[side]=new[]{prefix+"X",prefix+"Y",prefix+"Z",prefix+"DirectionX",prefix+"DirectionY",prefix+"DirectionZ"};}
            joints=new Transform[4];sampled=new Quaternion[4];angles=new float[4];
            string[] names={"LeftUpperArm","RightUpperArm","LeftLowerArm","RightLowerArm"};
            for(int i=0;i<names.Length;i++)
            {
                joints[i]=Array.Find(bones,b=>b.name==names[i]);
                if(!joints[i])throw new InvalidOperationException("Weapon switch joint missing: "+names[i]);
                angles[i]=profile.Get(i<2?"view.switchShoulderDegrees":"view.switchElbowDegrees");
            }
            // Read the skin bind frame, not an already sampled animation pose.
            foreach(var skin in view.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var bind=skin.sharedMesh.bindposes;var skinBones=skin.bones;
                int left=Array.IndexOf(skinBones,leftHand),right=Array.IndexOf(skinBones,rightHand);
                int lowerLeft=Array.IndexOf(skinBones,joints[2]),lowerRight=Array.IndexOf(skinBones,joints[3]);
                if(left<0||right<0||lowerLeft<0||lowerRight<0)continue;
                leftHandBind=(bind[lowerLeft]*bind[left].inverse).rotation;
                rightHandBind=(bind[lowerRight]*bind[right].inverse).rotation;
                for(int side=0;side<2;side++)for(int segment=0;segment<2;segment++)
                {
                    int index=side*2+segment;
                    int bone=Array.FindIndex(skinBones,b=>b.name==(side==0?"Left":"Right")+"LowerArmTwist"+(segment+1));
                    if(bone<0)continue;
                    sleeveTwists[index]=skinBones[bone];
                    sleeveTwistBind[index]=(bind[side==0?lowerLeft:lowerRight]*bind[bone].inverse).rotation;
                }
                break;
            }
        }
        public void Apply(CombatLifeState state,double duration)
        {
            var pose=WeaponSwitchPose.Read(state,duration);
            if(revision!=visual.PoseRevision)
            {
                for(int i=0;i<joints.Length;i++)sampled[i]=joints[i].localRotation;
                for(int i=0;i<fingerBones.Length;i++)fingerSamples[i]=fingerBones[i].localRotation;
                leftShoulderSample=leftShoulder.localPosition;rightShoulderSample=rightShoulder.localPosition;
                leftSample=leftHand.localRotation;rightSample=rightHand.localRotation;
                mountSamplePosition=mount.localPosition;mountSampleRotation=mount.localRotation;
                revision=visual.PoseRevision;
            }
            // Start from the sampled clip each time, including repeated render and completion-tick muzzle reads.
            for(int i=0;i<joints.Length;i++)joints[i].localRotation=sampled[i];
            for(int i=0;i<fingerBones.Length;i++)fingerBones[i].localRotation=fingerSamples[i];
            leftShoulder.localPosition=leftShoulderSample;rightShoulder.localPosition=rightShoulderSample;
            leftHand.localRotation=leftSample;rightHand.localRotation=rightSample;
            mount.localPosition=mountSamplePosition;mount.localRotation=mountSampleRotation;
            for(int i=0;i<joints.Length;i++)
                joints[i].rotation=Quaternion.AngleAxis(-angles[i]*pose.Lift,axis.right)*joints[i].rotation;
            bool crimson=pose.Weapon==WeaponId.RocketLauncher&&visual.Tuning.Version>=31&&visual.Tuning.Get("grip.crimson.enabled")>0;
            var crimsonOffset=crimson?new Vector3(visual.Tuning.Get("grip.crimson.viewX"),visual.Tuning.Get("grip.crimson.viewY"),visual.Tuning.Get("grip.crimson.viewZ")):Vector3.zero;
            root.localPosition=rest+visual.ViewOffset+crimsonOffset;model?.Show(pose.Weapon);
            if(visual.Tuning.Version>=28&&visual.Tuning.Get("view.armEntryEnabled")>0)
            {
                var weaponPosition=mount.position;var weaponRotation=mount.rotation;
                if(!crimson&&visual.Tuning.Version>=29&&visual.Tuning.Get("view.commonForearmsEnabled")>0)
                {
                    for(int finger=0;finger<10;finger++)fingerTargets[finger]=fingerBones[finger*3+2].position;
                    FitCommon(leftShoulder,joints[2],leftHand,leftPalm,visual.LeftForearmReferenceRotation,0,pose.Lift);
                    FitCommon(rightShoulder,joints[3],rightHand,rightPalm,visual.RightForearmReferenceRotation,1,pose.Lift);
                }
                else if(crimson&&visual.Tuning.Version>=32)
                {
                    FitCrimsonEntry(leftShoulder,joints[2],leftHand,visual.Tuning.Get("grip.crimson.detail.entryLeftX"));
                    FitCrimsonEntry(rightShoulder,joints[3],rightHand,visual.Tuning.Get("grip.crimson.detail.entryRightX"));
                }
                else
                {
                    FitEntry(leftShoulder,joints[2],leftHand,leftPalm,visual.Tuning.Get("view.armEntryLeftX"));
                    FitEntry(rightShoulder,joints[3],rightHand,rightPalm,visual.Tuning.Get("view.armEntryRightX"));
                }
                mount.SetPositionAndRotation(weaponPosition,weaponRotation);
                if(!crimson&&visual.Tuning.Version>=29&&visual.Tuning.Get("view.commonForearmsEnabled")>0)FitCommonFingers();
                if(pose.Weapon==WeaponId.Cutter&&visual.Tuning.Version>=29&&visual.Tuning.Get("view.commonForearmsEnabled")>0)visual.FitFirstPersonSeamThumb();
            }
            // Wrist contact fits may leave almost half a turn between the cuff's
            // two skin bones. Linear blending then collapses its cross-section.
            // Carry axial roll with the whole forearm; preserve the fitted hand.
            UnwindSleeve(joints[2],leftHand,leftHandBind);
            UnwindSleeve(joints[3],rightHand,rightHandBind);
            // The imported clip also animates two sleeve-only twist bones per arm.
            // Its old roll is stale after the wrist/contact fit. With roll carried
            // by the forearm, these bones retain their skin bind frame.
            for(int i=0;i<sleeveTwists.Length;i++)if(sleeveTwists[i])sleeveTwists[i].localRotation=sleeveTwistBind[i];
        }
        static void UnwindSleeve(Transform elbow,Transform hand,Quaternion bind)
        {
            var axis=hand.localPosition.normalized;
            var delta=hand.localRotation*Quaternion.Inverse(bind);
            var projection=axis*Vector3.Dot(new Vector3(delta.x,delta.y,delta.z),axis);
            var twist=new Quaternion(projection.x,projection.y,projection.z,delta.w);
            float norm=Mathf.Sqrt(Quaternion.Dot(twist,twist));
            if(norm<.000001f)return; // Pure 180-degree swing has no defined axial twist.
            twist=new Quaternion(twist.x/norm,twist.y/norm,twist.z/norm,twist.w/norm);
            var rotation=hand.rotation;
            elbow.rotation=elbow.rotation*twist;
            hand.rotation=rotation;
        }
        void FitCommon(Transform shoulder,Transform elbow,Transform hand,Transform palm,Quaternion reference,int side,float lift)
        {
            var p=visual.Tuning;var paths=commonPaths[side];
            var point=new Vector3(p.Get(paths[0]),p.Get(paths[1]),p.Get(paths[2]));
            var direction=new Vector3(p.Get(paths[3]),p.Get(paths[4]),p.Get(paths[5])).normalized;
            if(direction.sqrMagnitude<.000001f)return; // A zero authored direction has no valid orientation.
            var turn=Quaternion.AngleAxis(-p.Get("view.switchShoulderDegrees")*lift,Vector3.right);
            point=turn*point+visual.ViewOffset;direction=axis.TransformDirection(turn*direction);
            var handRotation=hand.rotation;var palmPosition=palm.position;var palmDirection=palm.position-hand.position;float length=Vector3.Distance(elbow.position,hand.position);
            var rotation=Quaternion.FromToRotation(reference*hand.localPosition,direction)*reference;
            var target=axis.TransformPoint(point)-direction*length;
            shoulder.position+=target-elbow.position;
            elbow.rotation=rotation;
            var aim=Vector3.RotateTowards(direction,(palmPosition-hand.position).normalized,p.Get("view.commonWristLimit")*Mathf.Deg2Rad,0);
            hand.rotation=Quaternion.FromToRotation(palmDirection,aim)*handRotation;
        }
        void FitCommonFingers()
        {
            int iterations=(int)visual.Tuning.Get("view.commonFingerIterations");float step=visual.Tuning.Get("view.commonFingerStep");
            for(int finger=0;finger<10;finger++)for(int iteration=0;iteration<iterations;iteration++)for(int link=1;link>=0;link--)
            {
                var joint=fingerBones[finger*3+link];var end=fingerBones[finger*3+2];
                var from=end.position-joint.position;var to=fingerTargets[finger]-joint.position;
                if(from.sqrMagnitude<.0000001f||to.sqrMagnitude<.0000001f)continue;
                joint.rotation=Quaternion.RotateTowards(Quaternion.identity,Quaternion.FromToRotation(from,to),step)*joint.rotation;
            }
        }
        void FitCrimsonEntry(Transform shoulder,Transform elbow,Transform hand,float entryX)
        {
            var wrist=hand.position;var rotation=hand.rotation;float length=Vector3.Distance(elbow.position,wrist);
            var ray=camera.ViewportPointToRay(new Vector3(.5f+entryX/camera.aspect,-visual.Tuning.Get("grip.crimson.detail.entryBelow"),0));
            var delta=ray.origin-wrist;float along=Vector3.Dot(delta,ray.direction);
            float discriminant=along*along-delta.sqrMagnitude+length*length;
            // Intersection of the off-screen entry ray and the fixed forearm-length sphere.
            // If unreachable, use its nearest point; never stretch bones or move hand contacts.
            float distance=discriminant>=0?-along-Mathf.Sqrt(discriminant):-along;
            var direction=(wrist-ray.GetPoint(Mathf.Max(0,distance))).normalized;
            if(direction.sqrMagnitude<.000001f)return;
            var target=wrist-direction*length;
            shoulder.position+=target-elbow.position;
            elbow.rotation=Quaternion.FromToRotation(hand.position-elbow.position,wrist-elbow.position)*elbow.rotation;
            hand.rotation=rotation;
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
