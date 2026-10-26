using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    /// <summary>Additive equipment fit after clip evaluation; never changes gameplay or camera state.</summary>
    internal sealed class OrbitalGripPose
    {
        readonly Transform wrist,primaryWrist,mount,upper,lower,palm,rigRoot;
        readonly List<(Transform proximal,Transform middle,Transform distal,Transform target)> forgeContacts=new List<(Transform,Transform,Transform,Transform)>();
        readonly Transform[] sampledBones;
        readonly List<(Transform bone,Vector3 position,Quaternion rotation)> restored=new List<(Transform,Vector3,Quaternion)>();
        Vector3 mountPosition;
        Quaternion mountRotation,primaryRotation;
        bool primaryApplied;
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
        public OrbitalGripPose(Transform root)
        {
            rigRoot=root;
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
            sampledBones=new[]{upper,lower,wrist}.Concat(digits.Select(d=>d.bone)).Concat(primaryDigits.Select(d=>d.bone)).ToArray();
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
        void Reach(Vector3 target,Vector3 handDirection,float wristAlignment)
        {
            var a=upper.position;var b=lower.position;var c=wrist.position;
            // Numerical guard only: avoid a singular straight elbow, not an art parameter.
            const float reachEpsilon=.000001f;
            float first=Vector3.Distance(a,b),second=Vector3.Distance(b,c);
            var delta=target-a;float distance=Mathf.Clamp(delta.magnitude,Mathf.Abs(first-second)+reachEpsilon,first+second-reachEpsilon);
            var direction=delta.normalized;
            var bend=Vector3.ProjectOnPlane(b-a,direction).normalized;
            // The elbow lies on a circle around shoulder-to-wrist. Its nearest
            // point to the backwards palm axis minimizes wrist flexion while
            // retaining both bone lengths and the authored palm contact.
            var aligned=Vector3.ProjectOnPlane(-handDirection,direction);
            if(aligned.sqrMagnitude>reachEpsilon*reachEpsilon)
                bend=Vector3.Slerp(bend,aligned.normalized,wristAlignment).normalized;
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
            foreach(var d in profile.Descriptors)
                if((d.Path.StartsWith("grip.Plasma.")||(d.Path.StartsWith("grip.")&&d.Path.IndexOf('.',5)<0))&&!Mathf.Approximately(profile.Get(d.Path),d.DefaultValue))return;
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
            return seamRig;
        }
        public void Apply(ProvingProfile profile,WeaponId weapon,float supportScale=1,ProvingProfile cutter=null)
        {
            if(!wrist)return;
            if(weapon==WeaponId.Cutter&&cutter!=null)profile=SeamRig(profile,cutter);
            foreach(var bone in sampledBones)
                restored.Add((bone,bone.localPosition,bone.localRotation));
            string fit="grip."+weapon+".";
            // Fit the wrist without displacing the gun or the support hand.
            primaryRotation=primaryWrist.localRotation;
            mountPosition=mount.localPosition;mountRotation=mount.localRotation;
            var position=mount.position;var rotation=mount.rotation;
            primaryWrist.rotation=Quaternion.AngleAxis((profile.Get("grip.primaryWristYaw")+profile.Get(fit+"primaryYaw")),mount.up)*primaryWrist.rotation;
            mount.SetPositionAndRotation(position,rotation);primaryApplied=true;
            // glTF-to-Unity handedness reflection reverses authored axial rotations.
            wrist.localRotation*=Quaternion.AngleAxis(-(profile.Get("grip.supportRoll")+profile.Get(fit+"supportRoll")),rollAxis);
            // Rotate around the palm contact, then solve the elbow without stretching bones.
            var turn=Quaternion.AngleAxis(profile.Get(fit+"supportPitch"),mount.right)*Quaternion.AngleAxis(profile.Get(fit+"supportYaw"),mount.up);
            var pivot=palm.position;
            var target=mount.TransformPoint(new Vector3(profile.Get(fit+"supportX"),profile.Get(fit+"supportY"),profile.Get(fit+"supportZ"))*supportScale)+turn*(wrist.position-pivot);
            var handRotation=turn*wrist.rotation;
            var handDirection=turn*(palm.position-wrist.position).normalized;
            Reach(target,handDirection,profile.Get(fit+"wristAlignment"));wrist.rotation=handRotation;
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
            if(weapon==WeaponId.Plasma)FitForgeDigits(profile);
        }
    }
}
