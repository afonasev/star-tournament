using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    /// <summary>Additive equipment fit after clip evaluation; never changes gameplay or camera state.</summary>
    internal sealed class OrbitalGripPose
    {
        readonly Transform wrist,primaryWrist,mount,upper,lower,palm,rightUpper,rightLower,rightPalm,rigRoot;
        readonly bool firstPerson;
        readonly Transform torso;
        readonly Quaternion torsoFrame;
        Quaternion worldFrame;
        bool worldFit;
        float elbowForward,primaryElbowForward,elbowDown;
        static readonly Dictionary<WeaponId,string> worldForwardPaths=System.Enum.GetValues(typeof(WeaponId)).Cast<WeaponId>().ToDictionary(w=>w,w=>"grip.world."+w+".forward");
        public float WorldGripError {get;private set;}
        readonly List<(Transform proximal,Transform middle,Transform distal,Transform target)> forgeContacts=new List<(Transform,Transform,Transform,Transform)>();
        readonly Transform[] sampledBones;
        readonly List<(Transform bone,Vector3 position,Quaternion rotation)> restored=new List<(Transform,Vector3,Quaternion)>();
        Vector3 mountPosition;
        Quaternion mountRotation,primaryRotation;
        bool primaryApplied;
        Transform crimsonShoulder,crimsonGrip;
        readonly Vector3 rollAxis;
        readonly Dictionary<(WeaponId weapon,Transform bone),string> jointPaths=new Dictionary<(WeaponId,Transform),string>();
        readonly Dictionary<(WeaponId weapon,Transform bone),string> spreadPaths=new Dictionary<(WeaponId,Transform),string>();
        void CacheDigitPaths(Transform bone,string side,string digit,string segment)
        {
            foreach(WeaponId weapon in System.Enum.GetValues(typeof(WeaponId)))
            {
                var prefix="grip."+weapon+"."+side+"."+digit+".";
                jointPaths[(weapon,bone)]=prefix+segment;
                if(segment=="Proximal")spreadPaths[(weapon,bone)]=prefix+"Spread";
            }
        }
        readonly Dictionary<Transform,Vector3> spreadAxes=new Dictionary<Transform,Vector3>();
        readonly List<(Transform bone,Vector3 axis,bool thumb,string bias)> digits=new List<(Transform,Vector3,bool,string)>();
        readonly List<(Transform bone,Vector3 axis,string path,bool thumb)> primaryDigits=new List<(Transform,Vector3,string,bool)>();
        public OrbitalGripPose(Transform root,bool firstPerson)
        {
            rigRoot=root;this.firstPerson=firstPerson;
            torso=root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Chest");
            torsoFrame=torso?Quaternion.Inverse(torso.rotation)*root.rotation:Quaternion.identity;
            var bones=root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Left")||t.name.StartsWith("Right")).ToDictionary(t=>t.name,t=>t);
            if(!bones.TryGetValue("LeftHand",out wrist))return;
            var row=(bones["LeftLittleProximal"].position-bones["LeftIndexProximal"].position).normalized;
            rollAxis=wrist.InverseTransformDirection((bones["LeftMiddleProximal"].position-wrist.position).normalized);
            foreach(var digit in new[]{"Thumb","Index","Middle","Ring","Little"})
                foreach(var segment in new[]{"Proximal","Intermediate","Distal"})
                {
                    var bone=bones["Left"+digit+segment];
                    CacheDigitPaths(bone,"left",digit,segment);
                    spreadAxes[bone]=bone.InverseTransformDirection(Vector3.Cross(row,(bones["LeftMiddleProximal"].position-wrist.position).normalized));
                    digits.Add((bone,bone.InverseTransformDirection(row),digit=="Thumb","grip.support"+segment+"Bias"));
                }
            if(!bones.TryGetValue("RightHand",out primaryWrist))return;
            upper=bones["LeftUpperArm"];lower=bones["LeftLowerArm"];palm=bones["LeftMiddleProximal"];
            rightUpper=bones["RightUpperArm"];rightLower=bones["RightLowerArm"];rightPalm=bones["RightMiddleProximal"];
            mount=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="trooper:rig:weapon-mount");
            var primaryRow=(bones["RightIndexProximal"].position-bones["RightLittleProximal"].position).normalized;
            foreach(var digit in new[]{"Thumb","Index","Middle","Ring","Little"})
                foreach(var segment in new[]{"Proximal","Intermediate","Distal"})
                {
                    var bone=bones["Right"+digit+segment];
                    CacheDigitPaths(bone,"right",digit,segment);
                    spreadAxes[bone]=bone.InverseTransformDirection(Vector3.Cross(primaryRow,(bones["RightMiddleProximal"].position-primaryWrist.position).normalized));
                    primaryDigits.Add((bone,bone.InverseTransformDirection(primaryRow),"grip.primary"+segment+"Curl",digit=="Thumb"));
                }
            sampledBones=new[]{bones["LeftShoulder"],bones["RightShoulder"],upper,lower,wrist,rightUpper,rightLower}.Concat(digits.Select(d=>d.bone)).Concat(primaryDigits.Select(d=>d.bone)).ToArray();
        }
        // Some clips have no wrist/attachment channel. Undo our previous additive
        // correction before the graph samples; never capture a corrected bind pose.
        public void RestorePrimary()
        {
            foreach(var item in restored){item.bone.localPosition=item.position;item.bone.localRotation=item.rotation;}
            restored.Clear();
            if(!primaryApplied)return;
            primaryWrist.localRotation=primaryRotation;
            mount.localPosition=mountPosition;mount.localRotation=mountRotation;
            primaryApplied=false;
        }
        void Reach(Vector3 target,Vector3 handDirection,float wristAlignment,float elbowSwivel=0)=>ReachArm(upper,lower,wrist,target,handDirection,wristAlignment,elbowSwivel);
        void ReachArm(Transform upper,Transform lower,Transform wrist,Vector3 target,Vector3 handDirection,float wristAlignment,float elbowSwivel=0)
        {
            var a=upper.position;var b=lower.position;var c=wrist.position;
            // Numerical guard only: avoid a singular straight elbow, not an art parameter.
            const float reachEpsilon=.000001f;
            float first=Vector3.Distance(a,b),second=Vector3.Distance(b,c);
            var delta=target-a;float distance=Mathf.Clamp(delta.magnitude,Mathf.Abs(first-second)+reachEpsilon,first+second-reachEpsilon);
            var direction=delta.normalized;
            var bend=Vector3.ProjectOnPlane(b-a,direction).normalized;
            if(worldFit)
            {
                var pole=worldFrame*new Vector3(upper==rightUpper?1:-1,-elbowDown,upper==rightUpper?primaryElbowForward:elbowForward);
                var projected=Vector3.ProjectOnPlane(pole,direction);
                if(projected.sqrMagnitude>reachEpsilon*reachEpsilon)bend=projected.normalized;
            }
            // The elbow lies on a circle around shoulder-to-wrist. Its nearest
            // point to the backwards palm axis minimizes wrist flexion while
            // retaining both bone lengths and the authored palm contact.
            var aligned=Vector3.ProjectOnPlane(-handDirection,direction);
            // World elbows use an anatomical outside/front pole, independent of the
            // camera-local palm-axis fit. Historical profiles keep their original solver.
            if(!worldFit&&aligned.sqrMagnitude>reachEpsilon*reachEpsilon)
                bend=Vector3.Slerp(bend,aligned.normalized,wristAlignment).normalized;
            // Swivel only the elbow circle; the fitted palm and bone lengths stay fixed.
            if(elbowSwivel!=0)bend=Quaternion.AngleAxis(elbowSwivel,direction)*bend;
            float along=(first*first+distance*distance-second*second)/(2*distance);
            var elbow=a+direction*along+bend*Mathf.Sqrt(Mathf.Max(0,first*first-along*along));
            upper.rotation=Quaternion.FromToRotation(b-a,elbow-a)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(wrist.position-lower.position,target-lower.position)*lower.rotation;
        }
        // Authored joint targets sit just outside the grip surface to account for glove pads.
        // Small CCD rotations preserve every bone length and the existing palm/wrist pose.
        void FitForgeDigits(ProvingProfile profile)
        {
            // A saved/custom Lab pose remains authoritative. Surface fitting is the factory pose only.
            // Elbow alignment leaves the palm contact unchanged, including in migrated saved poses.
            foreach(var d in profile.Descriptors)
                if(!d.Path.EndsWith(".wristAlignment")&&!d.Path.EndsWith(".primaryWristAlignment")&&
                    (d.Path.StartsWith("grip.Plasma.")||(d.Path.StartsWith("grip.")&&d.Path.IndexOf('.',5)<0))&&!Mathf.Approximately(profile.Get(d.Path),d.DefaultValue))return;
            if(forgeContacts.Count==0)
            {
                var all=rigRoot.GetComponentsInChildren<Transform>(true);
                foreach(var side in new[]{"Left","Right"})foreach(var digit in new[]{"Index","Middle","Ring","Little","Thumb"})
                {
                    var target=all.FirstOrDefault(t=>t.name=="forge-contact-"+side.ToLowerInvariant()+"-"+digit);
                    if(!target)continue;
                    forgeContacts.Add((all.First(t=>t.name==side+digit+"Proximal"),all.First(t=>t.name==side+digit+"Intermediate"),all.First(t=>t.name==side+digit+"Distal"),target));
                }
            }
            foreach(var c in forgeContacts)
                for(int step=0;step<4;step++)
                    for(int link=0;link<2;link++)
                    {
                        var joint=link==0?c.middle:c.proximal;
                        var from=c.distal.position-joint.position;var to=c.target.position-joint.position;
                        if(from.sqrMagnitude<.0000001f||to.sqrMagnitude<.0000001f)continue;
                        var turn=Quaternion.FromToRotation(from,to);
                        joint.rotation=Quaternion.RotateTowards(Quaternion.identity,turn,12)*joint.rotation;
                    }
        }
        static readonly ProvingProfile seamDefaults=ProvingProfile.CreateCutterDefault();
        readonly Dictionary<string,float> seamSurfaceValues=new Dictionary<string,float>();
        ProvingProfile seamSurfaceEquipment;
        bool seamSurfaceLoaded;
        float SeamSurface(ProvingProfile equipment,string key)
        {
            // Equipment profiles are immutable for a match. Resolve legacy defaults
            // once, avoiding descriptor scans and profile allocations per joint/frame.
            if(!seamSurfaceLoaded||!ReferenceEquals(equipment,seamSurfaceEquipment))
            {
                seamSurfaceEquipment=equipment;seamSurfaceLoaded=true;seamSurfaceValues.Clear();
                const string prefix="cutter.seam.surface.";
                foreach(var d in seamDefaults.Descriptors.Where(d=>d.Path.StartsWith(prefix)))
                    seamSurfaceValues[d.Path.Substring(prefix.Length)]=equipment!=null&&equipment.Descriptors.Any(e=>e.Path==d.Path)?equipment.Get(d.Path):seamDefaults.Get(d.Path);
            }
            return seamSurfaceValues[key];
        }
        readonly List<(Transform proximal,Transform middle,Transform distal,string digit)> seamContacts=new List<(Transform,Transform,Transform,string)>();
        Transform seamModel;
        void FitSeamDigits(ProvingProfile rig,ProvingProfile equipment)
        {
            // Explicit custom joint controls stay authoritative, as for FORGE.
            foreach(var d in rig.Descriptors)
                if(d.Path.StartsWith("grip.Cutter.left.")&&!Mathf.Approximately(rig.Get(d.Path),d.DefaultValue))return;
            if(!seamModel)
            {
                var all=rigRoot.GetComponentsInChildren<Transform>(true);
                seamModel=all.FirstOrDefault(t=>t.name=="cutter");
                if(!seamModel)return;
                foreach(var digit in new[]{"Ring","Little"})
                    seamContacts.Add((all.First(t=>t.name=="Left"+digit+"Proximal"),all.First(t=>t.name=="Left"+digit+"Intermediate"),all.First(t=>t.name=="Left"+digit+"Distal"),digit));
            }
            foreach(var c in seamContacts)
            {
                var target=seamModel.TransformPoint(new Vector3(SeamSurface(equipment,"left"+c.digit+"X"),SeamSurface(equipment,"left"+c.digit+"Y"),SeamSurface(equipment,"left"+c.digit+"Z")));
                for(int step=0;step<(int)SeamSurface(equipment,"gripFitIterations");step++)for(int link=0;link<2;link++)
                {
                    var joint=link==0?c.middle:c.proximal;
                    var from=c.distal.position-joint.position;var to=target-joint.position;
                    if(from.sqrMagnitude<.0000001f||to.sqrMagnitude<.0000001f)continue;
                    joint.rotation=Quaternion.RotateTowards(Quaternion.identity,Quaternion.FromToRotation(from,to),SeamSurface(equipment,"gripFitStep"))*joint.rotation;
                }
            }
        }
        public void FitFirstPersonSeamThumb(ProvingProfile rig,ProvingProfile equipment)
        {
            if(!firstPerson||!wrist||equipment==null)return;
            // Saved custom joint controls remain authoritative, as in the other contact fits.
            foreach(var d in rig.Descriptors)
                if(d.Path.StartsWith("grip.Cutter.left.Thumb.")&&!Mathf.Approximately(rig.Get(d.Path),d.DefaultValue))return;
            if(!seamModel)seamModel=rigRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="cutter");
            if(!seamModel)return;
            var a=digits[0].bone;var b=digits[1].bone;var c=digits[2].bone;
            var target=seamModel.TransformPoint(new Vector3(SeamSurface(equipment,"thumbContactX"),SeamSurface(equipment,"thumbContactY"),SeamSurface(equipment,"thumbContactZ")));
            var direction=seamModel.TransformDirection(new Vector3(SeamSurface(equipment,"thumbDirectionX"),SeamSurface(equipment,"thumbDirectionY"),SeamSurface(equipment,"thumbDirectionZ")));
            // The same exact two-link fit used for arms preserves all phalanx lengths.
            // It runs after common-forearm IK, whose thumb correction otherwise opens the grip.
            ReachArm(a,b,c,target,(c.position-b.position).normalized,0);
            if(direction.sqrMagnitude>0)c.rotation=Quaternion.FromToRotation(c.position-b.position,direction)*c.rotation;
        }
        ProvingProfile seamRig,seamInput,seamEquipment;
        // Immutable match profiles: cache equipment fit without changing saved rig values.
        ProvingProfile SeamRig(ProvingProfile rig,ProvingProfile equipment)
        {
            if(seamRig!=null&&ReferenceEquals(rig,seamInput)&&ReferenceEquals(equipment,seamEquipment))return seamRig;
            seamInput=rig;seamEquipment=equipment;seamRig=rig.DetachedCopy();
            var defaults=ProvingProfile.CreateCutterDefault();
            foreach(string key in new[]{"supportRoll","supportYaw","supportCurl","thumbOpposition","supportX","supportY","supportZ"})
            {
                string path="cutter.seam.grip."+key;
                float value=equipment!=null&&equipment.Descriptors.Any(d=>d.Path==path)?equipment.Get(path):defaults.Get(path);
                seamRig.Set("grip.Cutter."+key,value);
            }
            seamRig.Set("grip.Cutter.supportPitch",rig.Get("grip.Cutter.supportPitch")+SeamSurface(equipment,"gripSupportPitch"));
            seamRig.Set("grip.Cutter.supportZ",seamRig.Get("grip.Cutter.supportZ")+SeamSurface(equipment,"gripSupportZ"));
            return seamRig;
        }
        public void Apply(ProvingProfile profile,WeaponId weapon,float supportScale=1,ProvingProfile cutter=null,CrimsonRecoilSample recoil=default)
        {
            if(!wrist)return;
            worldFit=!firstPerson&&profile.Version>=34&&profile.Get("grip.world.enabled")>0;
            WorldGripError=0;
            if(worldFit)
            {
                // The chest follows gait/clip lean. Keeping poles and clearance in actor
                // axes would push a valid resting arm back into the leaning torso.
                worldFrame=torso?torso.rotation*torsoFrame:rigRoot.rotation;
                elbowForward=profile.Get("grip.world.elbowForward");elbowDown=profile.Get("grip.world.elbowDown");
                primaryElbowForward=weapon==WeaponId.RocketLauncher?profile.Get("grip.world.RocketLauncher.primaryElbowForward"):elbowForward;
            }
            if(weapon==WeaponId.Cutter&&cutter!=null)profile=SeamRig(profile,cutter);
            foreach(var bone in sampledBones)
                restored.Add((bone,bone.localPosition,bone.localRotation));
            string fit="grip."+weapon+".";
            // Fit the wrist without displacing the gun or the support hand.
            primaryRotation=primaryWrist.localRotation;
            mountPosition=mount.localPosition;mountRotation=mount.localRotation;
            var position=mount.position;var rotation=mount.rotation;
            if(weapon==WeaponId.RocketLauncher&&profile.Version>=31&&profile.Get("grip.crimson.enabled")>0)
            {
                if(!crimsonShoulder)crimsonShoulder=rigRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="crimson-shoulder");
                if(crimsonShoulder)
                {
                    var shoulderOffset=new Vector3(profile.Get("grip.crimson.shoulderX"),profile.Get("grip.crimson.shoulderY"),profile.Get("grip.crimson.shoulderZ"));
                    var shoulderTarget=rightUpper.position+(worldFit?worldFrame*shoulderOffset:rigRoot.TransformDirection(shoulderOffset));
                    var delta=shoulderTarget-crimsonShoulder.position;var crimsonHandRotation=primaryWrist.rotation;
                    if(!crimsonGrip)crimsonGrip=rigRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="pulse-grip");
                    var gripDelta=crimsonGrip?(crimsonGrip.position-mount.position)*(1-1/supportScale):Vector3.zero;
                    var crimsonHandDirection=(rightPalm.position-primaryWrist.position).normalized;
                    position+=delta;
                    ReachArm(rightUpper,rightLower,primaryWrist,primaryWrist.position+delta+gripDelta,crimsonHandDirection,profile.Get(fit+"primaryWristAlignment"));
                    primaryWrist.rotation=crimsonHandRotation;mount.SetPositionAndRotation(position,rotation);
                }
            }

            primaryWrist.rotation=Quaternion.AngleAxis((profile.Get("grip.primaryWristYaw")+profile.Get(fit+"primaryYaw")),mount.up)*primaryWrist.rotation;
            mount.SetPositionAndRotation(position,rotation);primaryApplied=true;
            if(weapon==WeaponId.Cutter&&cutter!=null)
            {
                // Rotate about the palm, then solve the arm to its fitted contact.
                // Keep both bone lengths and the weapon's original world transform.
                var primaryTurn=Quaternion.AngleAxis(SeamSurface(cutter,"gripPrimaryPitch"),mount.right);
                var primaryPivot=rightPalm.position;
                var primaryOffset=mount.TransformVector(new Vector3(0,SeamSurface(cutter,"gripPrimaryY"),SeamSurface(cutter,"gripPrimaryZ")));
                var primaryHandRotation=primaryTurn*primaryWrist.rotation;
                var primaryDirection=primaryTurn*(rightPalm.position-primaryWrist.position).normalized;
                ReachArm(rightUpper,rightLower,primaryWrist,primaryPivot+primaryOffset+primaryTurn*(primaryWrist.position-primaryPivot),primaryDirection,SeamSurface(cutter,"gripPrimaryAlignment"));
                primaryWrist.rotation=primaryHandRotation;mount.SetPositionAndRotation(position,rotation);
            }
            if((weapon==WeaponId.Shotgun&&profile.Version>=23)||profile.Version>=27)
            {
                // Move the elbow around the shoulder/wrist circle instead of bending the wrist.
                // All weapons share this arm fit; authored hand contacts and bone lengths stay fixed.
                var primaryHandRotation=primaryWrist.rotation;
                ReachArm(rightUpper,rightLower,primaryWrist,primaryWrist.position,(rightPalm.position-primaryWrist.position).normalized,profile.Get(fit+"primaryWristAlignment"));
                primaryWrist.rotation=primaryHandRotation;mount.SetPositionAndRotation(position,rotation);
            }
            // glTF-to-Unity handedness reflection reverses authored axial rotations.
            wrist.localRotation*=Quaternion.AngleAxis(-(profile.Get("grip.supportRoll")+profile.Get(fit+"supportRoll")),rollAxis);
            // Rotate around the palm contact, then solve the elbow without stretching bones.
            var turn=Quaternion.AngleAxis(profile.Get(fit+"supportPitch"),mount.right)*Quaternion.AngleAxis(profile.Get(fit+"supportYaw"),mount.up);
            var pivot=palm.position;
            var target=mount.TransformPoint(new Vector3(profile.Get(fit+"supportX"),profile.Get(fit+"supportY"),profile.Get(fit+"supportZ"))*supportScale)+turn*(wrist.position-pivot);
            var handRotation=turn*wrist.rotation;
            var handDirection=turn*(palm.position-wrist.position).normalized;
            float elbowSwivel=weapon==WeaponId.Cutter&&cutter!=null?SeamSurface(cutter,"supportElbowSwivel"):0;
            Reach(target,handDirection,profile.Get(fit+"wristAlignment"),elbowSwivel);wrist.rotation=handRotation;
            foreach(var thumb in digits.Where(d=>d.thumb&&d.bone.name.EndsWith("Proximal")))
                thumb.bone.rotation=Quaternion.AngleAxis(profile.Get(fit+"thumbOpposition"),mount.forward)*thumb.bone.rotation;
            foreach(var item in digits)
            {
                if(spreadPaths.TryGetValue((weapon,item.bone),out var spreadPath))
                    item.bone.localRotation*=Quaternion.AngleAxis(profile.Get(spreadPath),spreadAxes[item.bone]);
                item.bone.localRotation*=Quaternion.AngleAxis(-(profile.Get(item.thumb?"grip.supportThumbCurl":"grip.supportFingerCurl")+profile.Get(fit+(item.thumb?"supportThumb":"supportCurl"))+(item.thumb?0:profile.Get(item.bias))+profile.Get(jointPaths[(weapon,item.bone)])),item.axis);
            }
            foreach(var item in primaryDigits)
            {
                var angle=(profile.Get(item.path)+profile.Get(fit+"primaryCurl"))*(item.thumb?profile.Get("grip.primaryThumbFactor"):1f);
                if(spreadPaths.TryGetValue((weapon,item.bone),out var spreadPath))
                    item.bone.localRotation*=Quaternion.AngleAxis(profile.Get(spreadPath),spreadAxes[item.bone]);
                item.bone.localRotation*=Quaternion.AngleAxis(-(angle+profile.Get(jointPaths[(weapon,item.bone)])),item.axis);
            }
            if(weapon==WeaponId.Cutter&&cutter!=null)FitSeamDigits(profile,cutter);
            if(weapon==WeaponId.Plasma)FitForgeDigits(profile);
            if(worldFit)
            {
                var offset=worldFrame*new Vector3(weapon==WeaponId.RocketLauncher?profile.Get("grip.world.RocketLauncher.side"):0,0,profile.Get(worldForwardPaths[weapon]));
                var rp=primaryWrist.position+offset;var lp=wrist.position+offset;
                var rr=primaryWrist.rotation;var lr=wrist.rotation;
                var mp=mount.position+offset;var mr=mount.rotation;
                ReachArm(rightUpper,rightLower,primaryWrist,rp,(rightPalm.position-primaryWrist.position).normalized,0);
                primaryWrist.rotation=rr;mount.SetPositionAndRotation(mp,mr);
                Reach(lp,(palm.position-wrist.position).normalized,0);wrist.rotation=lr;
                mount.SetPositionAndRotation(mp,mr);
                WorldGripError=Mathf.Max(Vector3.Distance(primaryWrist.position,rp),Vector3.Distance(wrist.position,lp));
            }
            if(weapon==WeaponId.RocketLauncher&&crimsonShoulder&&recoil.Weight>0)
                ApplyCrimsonRecoil(recoil,profile,fit);
        }
        void ApplyCrimsonRecoil(CrimsonRecoilSample sample,ProvingProfile profile,string fit)
        {
            var pivot=crimsonShoulder.position;
            var turn=Quaternion.AngleAxis(-sample.PitchDegrees,mount.right)*Quaternion.AngleAxis(sample.RollDegrees,mount.forward);
            var offset=-mount.forward*sample.BackMeters;
            Vector3 Move(Vector3 point)=>pivot+turn*(point-pivot)+offset;
            var weaponPosition=Move(mount.position);var weaponRotation=turn*mount.rotation;
            var rightTarget=Move(primaryWrist.position);var leftTarget=Move(wrist.position);
            var rightRotation=turn*primaryWrist.rotation;var leftRotation=turn*wrist.rotation;
            var rightDirection=turn*(rightPalm.position-primaryWrist.position).normalized;
            var leftDirection=turn*(palm.position-wrist.position).normalized;
            ReachArm(rightUpper,rightLower,primaryWrist,rightTarget,rightDirection,profile.Get(fit+"primaryWristAlignment"));
            primaryWrist.rotation=rightRotation;mount.SetPositionAndRotation(weaponPosition,weaponRotation);
            Reach(leftTarget,leftDirection,profile.Get(fit+"wristAlignment"));wrist.rotation=leftRotation;
            mount.SetPositionAndRotation(weaponPosition,weaponRotation);
            if(worldFit)WorldGripError=Mathf.Max(WorldGripError,Mathf.Max(Vector3.Distance(primaryWrist.position,rightTarget),Vector3.Distance(wrist.position,leftTarget)));
        }
    }
}
