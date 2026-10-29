using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace StarTournament.ProvingGround.Editor
{
    // Offline art bridge: executes the actual presentation/IK, never edits assets or profiles.
    public static class VectorArtPoseExport
    {
        [Serializable] public class Bone { public string name; public float[] deformation; }
        [Serializable] public class Pose { public string name,state; public bool firstPerson; public float time; public Vector3 viewOffset; public float verticalFov; public Bone[] bones; public float[] weapon; public float[] weaponRest; }
        [Serializable] public class Packet { public string evaluator="TrooperVisual + OrbitalGripPose, factory profile"; public List<Pose> poses=new List<Pose>(); }
        static float[] Flat(Matrix4x4 m) { var a=new float[16];for(int r=0;r<4;r++)for(int c=0;c<4;c++)a[r*4+c]=m[r,c];return a; }
        public static void Run()
        {
            var output=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_ART_POSES");
            if(string.IsNullOrEmpty(output))throw new InvalidOperationException("Explicit output required");
            var clips=TrooperVisual.ClipNames.Select(n=>AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/StarTournament/Trooper/"+n+".anim")).ToArray();
            var packet=new Packet();
            var factory=ProvingProfile.CreateTrooperDefault();
            float runSpeed=factory.Get("animation.runThreshold")+1;
            float fov=ProvingProfile.CreateDefault().Get("camera.fieldOfViewDegrees");
            foreach(bool fp in new[]{false,true})
            {
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/StarTournament/Art/trooper-"+(fp?"arms":"body")+"-animated.glb");
                if(!asset)throw new InvalidOperationException("Missing imported source");
                foreach(var sample in new[]{("idle",0f,.5f),("aim",0f,.7f),("walk",1.4f,.6f),("run",runSpeed,.35f)})
                {
                    var root=UnityEngine.Object.Instantiate(asset);
                    try
                    {
                        var mount=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="weapon:joined");
                        var weaponRest=root.transform.worldToLocalMatrix*mount.localToWorldMatrix;
                        var profile=ProvingProfile.CreateTrooperDefault();
                        var visual=root.AddComponent<TrooperVisual>();visual.EquippedWeapon=WeaponId.Shotgun;visual.Initialize(clips,profile,fp);
                        if(sample.Item1=="aim")visual.Fire(0);
                        for(int i=0;i<=Mathf.RoundToInt(sample.Item3*100);i++)
                        {
                            // Grounded gait measures actual actor displacement, not the velocity argument alone.
                            if(!fp)root.transform.position=new Vector3(0,0,sample.Item2*i*.01f);
                            visual.Render(i*.01,new Vector3(0,0,sample.Item2),100,1);
                        }
                        if(visual.State!=(fp?"aim":sample.Item1))throw new InvalidOperationException("Unexpected sampled state: "+visual.State);
                        var bones=new Dictionary<string,Bone>();
                        foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        for(int i=0;i<skin.bones.Length;i++)
                        {
                            var matrix=root.transform.worldToLocalMatrix*skin.bones[i].localToWorldMatrix*skin.sharedMesh.bindposes[i]*skin.transform.worldToLocalMatrix*root.transform.localToWorldMatrix;
                            bones[skin.bones[i].name]=new Bone{name=skin.bones[i].name,deformation=Flat(matrix)};
                        }
                        if(bones.Count<50)throw new InvalidOperationException("Incomplete skin palette");
                        packet.poses.Add(new Pose{name=(fp?"fps-":"world-")+sample.Item1,state=visual.State,firstPerson=fp,time=sample.Item3,
                            bones=bones.Values.ToArray(),weapon=Flat(root.transform.worldToLocalMatrix*mount.localToWorldMatrix),weaponRest=Flat(weaponRest),
                            viewOffset=new Vector3(profile.Get("view.x"),profile.Get("view.y"),profile.Get("view.z"))+visual.ViewOffset,verticalFov=fov});
                        visual.Release();
                    }
                    finally{UnityEngine.Object.DestroyImmediate(root);}
                }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,JsonUtility.ToJson(packet,true));
            Debug.Log("VECTOR_ART_POSES_COMPLETE "+packet.poses.Count);
        }
    }
}
