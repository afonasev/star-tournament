using System.Collections.Generic;
using UnityEngine;
namespace StarTournament.ProvingGround
{
    public static class WellDetailMesh
    {
        // Fixed four-point bevel topology. Dimensions are actual metres, so a long rail keeps a small edge radius.
        public static Mesh Bevel(Vector3 size)
        {
            float radius=Mathf.Min(.035f,Mathf.Min(size.x,Mathf.Min(size.y,size.z))*.24f);
            var v=new List<Vector3>();var n=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
            for(int axis=0;axis<3;axis++)for(int sign=-1;sign<=1;sign+=2)
            {
                int u=(axis+1)%3,w=(axis+2)%3,start=v.Count;
                var a=new[]{-size[u]/2,-size[u]/2+radius,size[u]/2-radius,size[u]/2};
                var b=new[]{-size[w]/2,-size[w]/2+radius,size[w]/2-radius,size[w]/2};
                for(int y=0;y<4;y++)for(int x=0;x<4;x++)
                {
                    var p=Vector3.zero;p[axis]=sign*size[axis]/2;p[u]=a[x];p[w]=b[y];var inner=Vector3.zero;
                    for(int k=0;k<3;k++)inner[k]=Mathf.Clamp(p[k],-size[k]/2+radius,size[k]/2-radius);
                    var normal=(p-inner).normalized;v.Add(inner+normal*radius);n.Add(normal);uv.Add(new Vector2(a[x]/size[u]+.5f,b[y]/size[w]+.5f));
                }
                for(int y=0;y<3;y++)for(int x=0;x<3;x++)
                {int i=start+y*4+x;if(sign>0)indices.AddRange(new[]{i,i+1,i+5,i,i+5,i+4});else indices.AddRange(new[]{i,i+5,i+1,i,i+4,i+5});}
            }
            var mesh=new Mesh{name="Well machined bevel"};mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
        }
        public static Mesh Cylinder()
        {
            const int count=16;var v=new List<Vector3>();var n=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
            for(int i=0;i<=count;i++)
            {
                float angle=i*Mathf.PI*2/count;var radial=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                foreach(int side in new[]{-1,1}){v.Add(radial*.5f+Vector3.up*side*.5f);n.Add(radial);uv.Add(new Vector2(i/(float)count,(side+1)*.5f));}
                if(i<count){int b=i*2;t.AddRange(new[]{b,b+1,b+3,b,b+3,b+2});}
            }
            foreach(int side in new[]{-1,1})
            {
                int center=v.Count;v.Add(Vector3.up*side*.5f);n.Add(Vector3.up*side);uv.Add(Vector2.one*.5f);
                for(int i=0;i<=count;i++){float angle=i*Mathf.PI*2/count;v.Add(new Vector3(Mathf.Cos(angle)*.5f,side*.5f,Mathf.Sin(angle)*.5f));n.Add(Vector3.up*side);uv.Add(new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*.5f+Vector2.one*.5f);if(i<count){if(side>0)t.AddRange(new[]{center,center+i+2,center+i+1});else t.AddRange(new[]{center,center+i+1,center+i+2});}}
            }
            var mesh=new Mesh{name="Well turned component"};mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetUVs(0,uv);mesh.SetTriangles(t,0);mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
        }
    }
}
