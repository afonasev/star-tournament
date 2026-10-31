using System.Collections.Generic;
using UnityEngine;
namespace StarTournament.ProvingGround
{
    /// <summary>Static contact visibility sampled against authored opaque boxes, never Physics or simulation state.</summary>
    public static class WellContactMesh
    {
        // Fixed spatial/sample tier, shared by all Well surfaces. No per-frame work or physics queries.
        const float Step=.8f,Reach=2.4f;const int Samples=12;
        public static Mesh Build(Mesh source,IReadOnlyList<Bounds> solids)
        {
            var input=source.vertices;var normals=source.normals;
            var v=new List<Vector3>();var n=new List<Vector3>();var uv=new List<Vector2>();var colors=new List<Color>();var indices=new List<int>();
            // WellSurfaceMesh emits independent rectangular quads in groups of four.
            for(int q=0;q<input.Length;q+=4)
            {
                var normal=normals[q];var du=input[q+1]-input[q];var dv=input[q+3]-input[q];
                int nx=Mathf.Max(1,Mathf.CeilToInt(du.magnitude/Step)),ny=Mathf.Max(1,Mathf.CeilToInt(dv.magnitude/Step));
                var neighborhood=new Bounds((input[q]+input[q+2])*.5f,Vector3.zero);for(int k=0;k<4;k++)neighborhood.Encapsulate(input[q+k]);neighborhood.Expand(Reach*2);
                var nearby=new List<Bounds>();foreach(var box in solids)if(neighborhood.Intersects(box))nearby.Add(box);
                var tangent=du.normalized;var bitangent=Vector3.Cross(normal,tangent);int start=v.Count;
                var directions=Directions(normal,tangent,bitangent);var pointBoxes=new List<Bounds>(nearby.Count);
                for(int y=0;y<=ny;y++)for(int x=0;x<=nx;x++)
                {
                    var p=input[q]+du*(x/(float)nx)+dv*(y/(float)ny);// Move the sampling point just inside the exposed face at a junction. Sampling exactly
                    // on the neighboring wall's boundary would count every ray as trapped inside it.
                    var interior=((input[q]+input[q+2])*.5f-p).normalized*.04f;
                    float visibility=Visibility(p+interior,normal,directions,nearby,pointBoxes);
                    v.Add(p);n.Add(normal);colors.Add(new Color(visibility,visibility,visibility,1));
                    uv.Add(Mathf.Abs(normal.y)>.5f?new Vector2(p.x,p.z):new Vector2(Mathf.Abs(normal.x)>.5f?p.z:p.x,p.y));
                }
                bool forward=Vector3.Dot(Vector3.Cross(du,dv),normal)>0;
                for(int y=0;y<ny;y++)for(int x=0;x<nx;x++)
                {int i=start+y*(nx+1)+x,j=i+nx+1;if(forward)indices.AddRange(new[]{i,i+1,j+1,i,j+1,j});else indices.AddRange(new[]{i,j+1,i+1,i,j,j+1});}
            }
            var result=new Mesh{name="Well contact-lit surfaces",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};result.SetVertices(v);result.SetNormals(n);result.SetUVs(0,uv);result.SetColors(colors);result.SetTriangles(indices,0);result.RecalculateTangents();result.RecalculateBounds();return result;
        }
        static Vector3[] Directions(Vector3 normal,Vector3 tangent,Vector3 bitangent)
        {
            var directions=new Vector3[Samples];
            for(int i=0;i<Samples;i++)
            {
                float z=Mathf.Sqrt((i+.5f)/Samples),radius=Mathf.Sqrt(1-z*z),angle=i*2.39996323f;
                directions[i]=normal*z+(tangent*Mathf.Cos(angle)+bitangent*Mathf.Sin(angle))*radius;
            }
            return directions;
        }
        public static float Visibility(Vector3 p,Vector3 normal,Vector3 tangent,Vector3 bitangent,IReadOnlyList<Bounds> boxes)
            =>Visibility(p,normal,Directions(normal,tangent,bitangent),boxes,new List<Bounds>(boxes.Count));
        static float Visibility(Vector3 p,Vector3 normal,Vector3[] directions,IReadOnlyList<Bounds> boxes,List<Bounds> pointBoxes)
        {
            float occlusion=0;var origin=p+normal*.025f;pointBoxes.Clear();
            // This test depends only on the point, not on the twelve sample rays.
            foreach(var box in boxes)if(box.SqrDistance(origin)<Reach*Reach)pointBoxes.Add(box);
            for(int i=0;i<Samples;i++)
            {
                var ray=new Ray(origin,directions[i]);float nearest=Reach;
                foreach(var box in pointBoxes)if(box.IntersectRay(ray,out float distance)&&distance>=0&&distance<nearest)nearest=distance;
                float blocked=1-nearest/Reach;occlusion+=blocked*blocked;
            }
            return Mathf.Clamp01(1-occlusion/Samples);
        }
    }
}
