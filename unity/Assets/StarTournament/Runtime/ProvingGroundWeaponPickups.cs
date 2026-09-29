using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingGround
    {
        readonly List<GameObject> weaponPickupVisuals=new List<GameObject>();
        readonly List<Mesh> pickupMeshes=new List<Mesh>();
        float pickupModelBaseLength;
        void CreateWeaponPickupVisuals()
        {
            pickupModelBaseLength=LifeProfile.Get("weaponPickup.modelLengthMeters");
            foreach(var pickup in Session.WeaponPickups)
            {
                var root=Owned(pickup.InstanceId+"-presentation");GameObject model;
                if(pickup.Weapon==WeaponId.Shotgun)
                {
                    var source=TrooperBodyPrefab.GetComponentsInChildren<Renderer>(true).Single(r=>r.name=="weapon:joined");
                    model=new GameObject("shotgun-world-model");model.transform.SetParent(root.transform,false);
                    Mesh mesh;
                    if(source is SkinnedMeshRenderer skin)
                    {mesh=new Mesh();skin.BakeMesh(mesh);pickupMeshes.Add(mesh);}
                    else mesh=source.GetComponent<MeshFilter>().sharedMesh;
                    model.AddComponent<MeshFilter>().sharedMesh=mesh;
                    model.AddComponent<MeshRenderer>().sharedMaterials=source.sharedMaterials;
                }
                else model=Instantiate(pickup.Weapon==WeaponId.Cutter?CutterPrefab:PulsePrefab,root.transform);
                model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.identity;model.transform.localScale=Vector3.one;
                foreach(var collider in model.GetComponentsInChildren<Collider>())Destroy(collider);
                var renderers=model.GetComponentsInChildren<Renderer>(true);
                var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                float size=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));
                float scale=pickupModelBaseLength/size;
                model.transform.localScale=Vector3.one*scale;model.transform.localPosition=-root.transform.InverseTransformPoint(bounds.center)*scale;
                weaponPickupVisuals.Add(root);
            }
        }
        void RenderWeaponPickups()
        {
            if(Session==null)return;var states=Session.WeaponPickups;var profile=frozenLife??LifeProfile;
            for(int i=0;i<states.Length;i++)
            {
                var pickup=states[i];var visual=weaponPickupVisuals[i];visual.SetActive(pickup.Available);
                visual.transform.localScale=Vector3.one*(profile.Get("weaponPickup.modelLengthMeters")/pickupModelBaseLength);
                visual.transform.position=pickup.Anchor+Vector3.up*profile.Get("weaponPickup.hoverMeters");
                visual.transform.rotation=Quaternion.Euler(0,(float)(Session.Time*profile.Get("weaponPickup.rotationDegreesPerSecond")),0);
            }
        }
    }
}
