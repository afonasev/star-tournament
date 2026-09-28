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
            return new WeaponSwitchPose(progress<.5?state.SelectedWeapon:state.PendingWeapon,lift);
        }
    }

    internal sealed class FirstPersonWeaponSwitch
    {
        readonly Transform root;
        readonly WeaponModelPresentation model;
        readonly Vector3 rest;
        readonly float lift;
        public FirstPersonWeaponSwitch(GameObject view,Camera camera)
        {
            root=view.transform;model=view.GetComponent<WeaponModelPresentation>();
            var profile=view.GetComponent<TrooperVisual>().Tuning;
            rest=new Vector3(profile.Get("view.x"),profile.Get("view.y"),profile.Get("view.z"));
            root.localPosition=rest;
            float clearance=0;
            float slope=Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f);
            // The minimum comes from all equipment geometry (including pending inactive models).
            // It guarantees the mesh swap is above the viewport even for a large launcher or resized split-screen.
            foreach(var renderer in view.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh=null;bool baked=false;
                if(renderer is SkinnedMeshRenderer skin)
                {
                    // Imported skinned culling bounds are deliberately oversized; sample the actual resting equipment.
                    mesh=new Mesh();skin.BakeMesh(mesh);baked=true;
                }
                else if(renderer is MeshRenderer)mesh=renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if(!mesh)continue;
                foreach(var vertex in mesh.vertices)
                {
                    var cameraPoint=camera.transform.InverseTransformPoint(renderer.transform.TransformPoint(vertex));
                    clearance=Mathf.Max(clearance,cameraPoint.z*slope-cameraPoint.y);
                }
                if(baked)UnityEngine.Object.Destroy(mesh);
            }
            lift=clearance+profile.Get("view.switchClearance");
        }
        public void Apply(CombatLifeState state,double duration)
        {
            var pose=WeaponSwitchPose.Read(state,duration);
            root.localPosition=rest+Vector3.up*(pose.Lift*lift);
            model?.Show(pose.Weapon);
        }
    }
}
