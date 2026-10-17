using System.Collections.Generic;
using UnityEngine;
namespace StarTournament.ProvingGround
{
    public static class WellDetailMesh
    {
        struct ClipVertex
        {
            public Vector3 Position,Normal;public Vector2 Uv;
            public static ClipVertex Lerp(ClipVertex a,ClipVertex b,float t)=>new ClipVertex{Position=Vector3.Lerp(a.Position,b.Position,t),Normal=Vector3.Lerp(a.Normal,b.Normal,t).normalized,Uv=Vector2.Lerp(a.Uv,b.Uv,t)};
        }
        sealed class FlatFace { public Vector3[] Points;public Rect Bounds; }
        static float Side(ClipVertex p,Vector3 a,Vector3 b,int u,int v,float winding)
            =>winding*((b[u]-a[u])*(p.Position[v]-a[v])-(b[v]-a[v])*(p.Position[u]-a[u]));
        static List<ClipVertex> ClipEdge(List<ClipVertex> polygon,Vector3 a,Vector3 b,int u,int v,float winding,bool inside)
        {
            var result=new List<ClipVertex>();
            for(int i=0;i<polygon.Count;i++)
            {
                var p=polygon[i];var q=polygon[(i+1)%polygon.Count];float dp=Side(p,a,b,u,v,winding),dq=Side(q,a,b,u,v,winding);
                bool keep=inside?dp>=0:dp<=0,next=inside?dq>=0:dq<=0;
                if(keep)result.Add(p);if(keep!=next)result.Add(ClipVertex.Lerp(p,q,dp/(dp-dq)));
            }
            return result;
        }
        /// <summary>Union the coplanar flat patches of combined fittings, retaining the first face's attributes.</summary>
        public static Mesh JoinFlatFaces(Mesh source)
        {
            var input=source.vertices;var normals=source.normals;var uv=source.uv;var indices=source.triangles;
            var vertices=new List<Vector3>();var outputNormals=new List<Vector3>();var outputUv=new List<Vector2>();var output=new List<int>();
            var planes=new Dictionary<string,List<FlatFace>>();
            var vertexIndex=new Dictionary<(Vector3,Vector3,Vector2),int>();
            int Vertex(ClipVertex vertex)
            {
                var key=(vertex.Position,vertex.Normal,vertex.Uv);if(vertexIndex.TryGetValue(key,out int index))return index;
                index=vertices.Count;vertices.Add(vertex.Position);outputNormals.Add(vertex.Normal);outputUv.Add(vertex.Uv);vertexIndex.Add(key,index);return index;
            }
            for(int i=0;i<indices.Length;i+=3)
            {
                var triangle=new List<ClipVertex>();for(int j=0;j<3;j++){int k=indices[i+j];triangle.Add(new ClipVertex{Position=input[k],Normal=normals[k],Uv=uv[k]});}
                var a=triangle[0].Position;var b=triangle[1].Position;var c=triangle[2].Position;
                var normal=Vector3.Cross(b-a,c-a).normalized;int axis=Mathf.Abs(normal.x)>.99999f?0:Mathf.Abs(normal.y)>.99999f?1:Mathf.Abs(normal.z)>.99999f?2:-1;
                var pieces=new List<List<ClipVertex>>{triangle};
                if(axis>=0&&Mathf.Abs(a[axis]-b[axis])<.00001f&&Mathf.Abs(a[axis]-c[axis])<.00001f)
                {
                    int u=(axis+1)%3,v=(axis+2)%3;string key=axis+":"+Mathf.Sign(normal[axis])+":"+Mathf.RoundToInt(a[axis]*10000);
                    var bounds=Rect.MinMaxRect(Mathf.Min(a[u],b[u],c[u]),Mathf.Min(a[v],b[v],c[v]),Mathf.Max(a[u],b[u],c[u]),Mathf.Max(a[v],b[v],c[v]));
                    if(!planes.TryGetValue(key,out var earlier))planes[key]=earlier=new List<FlatFace>();
                    foreach(var owner in earlier)
                    {
                        if(!bounds.Overlaps(owner.Bounds))continue;var clip=owner.Points;
                        float winding=Mathf.Sign((clip[1][u]-clip[0][u])*(clip[2][v]-clip[0][v])-(clip[1][v]-clip[0][v])*(clip[2][u]-clip[0][u]));var next=new List<List<ClipVertex>>();
                        foreach(var piece in pieces)
                        {
                            var intersection=piece;for(int edge=0;edge<3&&intersection.Count>=3;edge++)intersection=ClipEdge(intersection,clip[edge],clip[(edge+1)%3],u,v,winding,true);
                            bool overlaps=false;for(int j=1;j<intersection.Count-1;j++)overlaps|=Vector3.Cross(intersection[j].Position-intersection[0].Position,intersection[j+1].Position-intersection[0].Position).sqrMagnitude>1e-16f;
                            if(!overlaps){next.Add(piece);continue;}
                            var inside=piece;
                            for(int edge=0;edge<3&&inside.Count>=3;edge++)
                            {var outside=ClipEdge(inside,clip[edge],clip[(edge+1)%3],u,v,winding,false);if(outside.Count>=3)next.Add(outside);inside=ClipEdge(inside,clip[edge],clip[(edge+1)%3],u,v,winding,true);}
                        }
                        pieces=next;if(pieces.Count==0)break;
                    }
                    earlier.Add(new FlatFace{Points=new[]{a,b,c},Bounds=bounds});
                }
                foreach(var polygon in pieces)
                {
                    for(int j=1;j<polygon.Count-1;j++)if(Vector3.Cross(polygon[j].Position-polygon[0].Position,polygon[j+1].Position-polygon[0].Position).sqrMagnitude>1e-16f)output.AddRange(new[]{Vertex(polygon[0]),Vertex(polygon[j]),Vertex(polygon[j+1])});
                }
            }
            var mesh=new Mesh{name="Well joined fittings",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetNormals(outputNormals);mesh.SetUVs(0,outputUv);mesh.SetTriangles(output,0);mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
        }
        /// <summary>Remove the buried back of wall-mounted fittings; the wall closes the cut, so no cap or overlay is needed.</summary>
        public static Mesh ClipBack(Mesh source,float minimumZ)
        {
            var input=source.vertices;var normals=source.normals;var uv=source.uv;var indices=source.triangles;
            var vertices=new List<Vector3>();var outputNormals=new List<Vector3>();var outputUv=new List<Vector2>();var output=new List<int>();
            for(int i=0;i<indices.Length;i+=3)
            {
                var polygon=new List<ClipVertex>();
                for(int edge=0;edge<3;edge++)
                {
                    int a=indices[i+edge],b=indices[i+(edge+1)%3];
                    var p=new ClipVertex{Position=input[a],Normal=normals[a],Uv=uv[a]};var q=new ClipVertex{Position=input[b],Normal=normals[b],Uv=uv[b]};
                    bool inside=p.Position.z>=minimumZ,nextInside=q.Position.z>=minimumZ;
                    if(inside)polygon.Add(p);
                    if(inside!=nextInside)
                    {var cut=ClipVertex.Lerp(p,q,(minimumZ-p.Position.z)/(q.Position.z-p.Position.z));cut.Position.z=minimumZ;polygon.Add(cut);}
                }
                int start=vertices.Count;foreach(var vertex in polygon){vertices.Add(vertex.Position);outputNormals.Add(vertex.Normal);outputUv.Add(vertex.Uv);}
                for(int j=1;j<polygon.Count-1;j++)
                    if(Vector3.Cross(polygon[j].Position-polygon[0].Position,polygon[j+1].Position-polygon[0].Position).sqrMagnitude>1e-16f)output.AddRange(new[]{start,start+j,start+j+1});
            }
            var mesh=new Mesh{name="Well flush wall fitting"};mesh.SetVertices(vertices);mesh.SetNormals(outputNormals);mesh.SetUVs(0,outputUv);mesh.SetTriangles(output,0);mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
        }
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
