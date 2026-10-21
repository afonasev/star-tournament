using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    /// <summary>Additive equipment fit after clip evaluation; never changes gameplay or camera state.</summary>
    internal sealed class OrbitalGripPose
    {
        readonly Transform wrist,primaryWrist,mount;
        Vector3 mountPosition;
        Quaternion mountRotation,primaryRotation;
        bool primaryApplied;
        readonly Vector3 rollAxis;
        readonly List<(Transform bone,Vector3 axis,bool thumb)> digits=new List<(Transform,Vector3,bool)>();
        readonly List<(Transform bone,Vector3 axis,string path,bool thumb)> primaryDigits=new List<(Transform,Vector3,string,bool)>();
        public OrbitalGripPose(Transform root)
        {
            var bones=root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Left")||t.name.StartsWith("Right")).ToDictionary(t=>t.name,t=>t);
            if(!bones.TryGetValue("LeftHand",out wrist))return;
            var row=(bones["LeftLittleProximal"].position-bones["LeftIndexProximal"].position).normalized;
            rollAxis=wrist.InverseTransformDirection((bones["LeftMiddleProximal"].position-wrist.position).normalized);
            foreach(var digit in new[]{"Thumb","Index","Middle","Ring","Little"})
                foreach(var segment in new[]{"Proximal","Intermediate","Distal"})
                {
                    var bone=bones["Left"+digit+segment];
                    digits.Add((bone,bone.InverseTransformDirection(row),digit=="Thumb"));
                }
            if(!bones.TryGetValue("RightHand",out primaryWrist))return;
            mount=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="trooper:rig:weapon-mount");
            var primaryRow=(bones["RightIndexProximal"].position-bones["RightLittleProximal"].position).normalized;
            foreach(var digit in new[]{"Thumb","Index","Middle","Ring","Little"})
                foreach(var segment in new[]{"Proximal","Intermediate","Distal"})
                {
                    var bone=bones["Right"+digit+segment];
                    primaryDigits.Add((bone,bone.InverseTransformDirection(primaryRow),"grip.primary"+segment+"Curl",digit=="Thumb"));
                }
        }
        // Some clips have no wrist/attachment channel. Undo our previous additive
        // correction before the graph samples; never capture a corrected bind pose.
        public void RestorePrimary()
        {
            if(!primaryApplied)return;
            primaryWrist.localRotation=primaryRotation;
            mount.localPosition=mountPosition;mount.localRotation=mountRotation;
            primaryApplied=false;
        }
        public void Apply(ProvingProfile profile)
        {
            if(!wrist)return;
            // Fit the wrist without displacing the gun or the support hand.
            primaryRotation=primaryWrist.localRotation;
            mountPosition=mount.localPosition;mountRotation=mount.localRotation;
            var position=mount.position;var rotation=mount.rotation;
            primaryWrist.rotation=Quaternion.AngleAxis(profile.Get("grip.primaryWristYaw"),mount.up)*primaryWrist.rotation;
            mount.SetPositionAndRotation(position,rotation);primaryApplied=true;
            // glTF-to-Unity handedness reflection reverses authored axial rotations.
            wrist.localRotation*=Quaternion.AngleAxis(-profile.Get("grip.supportRoll"),rollAxis);
            foreach(var item in digits)
                item.bone.localRotation*=Quaternion.AngleAxis(-profile.Get(item.thumb?"grip.supportThumbCurl":"grip.supportFingerCurl"),item.axis);
            foreach(var item in primaryDigits)
            {
                var angle=profile.Get(item.path)*(item.thumb?profile.Get("grip.primaryThumbFactor"):1f);
                item.bone.localRotation*=Quaternion.AngleAxis(-angle,item.axis);
            }
        }
    }
}
