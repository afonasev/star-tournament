using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace StarTournament.ProvingGround
{
    // A single static paint batch, independent of simulation availability and pickup model lifetime.
    public static class ArenaPickupMarkings
    {
        public const string Name="Permanent white pickup circles";
        public static Mesh Build(Transform parent,ArenaDefinition definition,Material material,float radius,float width,Func<ArenaPickupDefinition,float> height,Func<ArenaPickupDefinition,bool> include=null)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            // Match Combat Bowl's fixed silhouette tessellation; this is not a balance setting.
            const int segments=64;
            foreach(var pickup in definition.Pickups)
            {
                if(include!=null&&!include(pickup))continue;
                int first=vertices.Count;var center=pickup.Anchor+Vector3.up*height(pickup);
                for(int i=0;i<segments;i++)
                {
                    var direction=new Vector3(Mathf.Sin(i*Mathf.PI*2/segments),0,Mathf.Cos(i*Mathf.PI*2/segments));
                    vertices.Add(center+direction*(radius+width/2));vertices.Add(center+direction*(radius-width/2));
                }
                for(int i=0;i<segments;i++)
                {
                    int a=first+i*2,b=first+(i+1)%segments*2;
                    triangles.Add(a);triangles.Add(b);triangles.Add(a+1);triangles.Add(a+1);triangles.Add(b);triangles.Add(b+1);
                }
            }
            var mesh=new Mesh{name=Name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var go=new GameObject(Name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            return mesh;
        }
    }
}
