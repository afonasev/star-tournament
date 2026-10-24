using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace StarTournament.ProvingGround
{
    public sealed partial class WellArenaPresentation
    {
        void BuildPickupMarkings(ArenaDefinition definition)
        {
            // Historical settings stay immutable; presentation can still show today's pickup markings.
            var settings=profile.Version>=6?profile:ProvingProfile.CreateWellDefault();
            float radius=settings.Get("well.pickupRingRadius"),width=settings.Get("well.pickupRingWidth"),height=settings.Get("well.pickupRingHeight");
            var vertices=new List<Vector3>();var triangles=new List<int>();
            // Same 64-segment silhouette as Combat Bowl. Fixed tessellation is an asset invariant.
            const int segments=64;
            foreach(var pickup in definition.Pickups)
            {
                int first=vertices.Count;var center=pickup.Anchor+Vector3.up*height;
                for(int i=0;i<segments;i++)
                {
                    float angle=i*Mathf.PI*2/segments;var direction=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle));
                    vertices.Add(center+direction*(radius+width/2));vertices.Add(center+direction*(radius-width/2));
                }
                for(int i=0;i<segments;i++)
                {
                    int a=first+i*2,b=first+(i+1)%segments*2;
                    triangles.Add(a);triangles.Add(b);triangles.Add(a+1);
                    triangles.Add(a+1);triangles.Add(b);triangles.Add(b+1);
                }
            }
            var mesh=new Mesh{name="Well permanent pickup circles"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();owned.Add(mesh);
            // One batch for all anchors; no colliders, extra lights, realtime updates or shadow passes.
            var go=new GameObject("Well permanent pickup circles");go.transform.SetParent(transform,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=Mat("Well white pickup paint",Color.white,true);
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
    }
}
