using System;
using System.Collections.Generic;
using UnityEngine;
namespace StarTournament.ProvingGround
{
    /// <summary>Boundary of axis-aligned boxes. Removes buried and duplicate faces, including partial T joints.</summary>
    public static class WellSurfaceMesh
    {
        const float Epsilon=.0001f;
        /// <summary>Partition the existing triangles only: top, vertical edge, underside. No overlays or new vertices.</summary>
        public static void SplitHorizontalMaterials(Mesh mesh)
        {
            var triangles=mesh.triangles;var normals=mesh.normals;
            var groups=new[]{new List<int>(),new List<int>(),new List<int>()};
            for(int i=0;i<triangles.Length;i+=3)
            {
                float y=normals[triangles[i]].y;
                int slot=y>.5f?0:y<-.5f?2:1;
                groups[slot].Add(triangles[i]);groups[slot].Add(triangles[i+1]);groups[slot].Add(triangles[i+2]);
            }
            mesh.subMeshCount=3;
            for(int i=0;i<groups.Length;i++)mesh.SetTriangles(groups[i],i,false);
        }
        public static Mesh Build(IReadOnlyList<Bounds> boxes,IReadOnlyList<Bounds> occluders=null)
        {
            var uv0=new List<Vector2>();var vertices=new List<Vector3>();var normals=new List<Vector3>();var triangles=new List<int>();
            for(int i=0;i<boxes.Count;i++)for(int axis=0;axis<3;axis++)for(int sign=-1;sign<=1;sign+=2)
            {
                int u=(axis+1)%3,v=(axis+2)%3;var b=boxes[i];float plane=sign>0?b.max[axis]:b.min[axis];
                var pieces=new List<Rect>{Rect.MinMaxRect(b.min[u],b.min[v],b.max[u],b.max[v])};
                void Cut(Bounds other,bool earlier)
                {
                    float lo=other.min[axis],hi=other.max[axis];
                    bool buried=sign>0?lo<=plane+Epsilon&&hi>plane+Epsilon:hi>=plane-Epsilon&&lo<plane-Epsilon;
                    bool duplicate=earlier&&(sign>0?Mathf.Abs(hi-plane)<Epsilon&&lo<plane-Epsilon:Mathf.Abs(lo-plane)<Epsilon&&hi>plane+Epsilon);
                    if(!buried&&!duplicate)return;
                    var next=new List<Rect>();
                    foreach(var r in pieces)
                    {
                        float x0=Mathf.Max(r.xMin,other.min[u]),x1=Mathf.Min(r.xMax,other.max[u]),y0=Mathf.Max(r.yMin,other.min[v]),y1=Mathf.Min(r.yMax,other.max[v]);
                        if(x1-x0<Epsilon||y1-y0<Epsilon){next.Add(r);continue;}
                        void Add(float a,float b0,float c,float d){if(c-a>Epsilon&&d-b0>Epsilon)next.Add(Rect.MinMaxRect(a,b0,c,d));}
                        Add(r.xMin,r.yMin,x0,r.yMax);Add(x1,r.yMin,r.xMax,r.yMax);
                        Add(x0,r.yMin,x1,y0);Add(x0,y1,x1,r.yMax);
                    }
                    pieces=next;
                }
                for(int j=0;j<boxes.Count&&pieces.Count>0;j++)if(j!=i)Cut(boxes[j],j<i);
                if(occluders!=null)foreach(var other in occluders){if(pieces.Count==0)break;Cut(other,false);}
                foreach(var r in pieces)
                {
                    var normal=Vector3.zero;normal[axis]=sign;int start=vertices.Count;
                    foreach(var uv in new[]{new Vector2(r.xMin,r.yMin),new Vector2(r.xMax,r.yMin),new Vector2(r.xMax,r.yMax),new Vector2(r.xMin,r.yMax)})
                    {var p=Vector3.zero;p[axis]=plane;p[u]=uv.x;p[v]=uv.y;vertices.Add(p);normals.Add(normal);uv0.Add(axis==1?new Vector2(p.x,p.z):new Vector2(axis==0?p.z:p.x,p.y));}
                    if(sign>0)triangles.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
                    else triangles.AddRange(new[]{start,start+2,start+1,start,start+3,start+2});
                }
            }
            var mesh=new Mesh{name="Well joined surfaces",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv0);mesh.SetTriangles(triangles,0);mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
        }
    }
}
