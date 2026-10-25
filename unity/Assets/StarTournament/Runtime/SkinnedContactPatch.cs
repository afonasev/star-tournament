using System.Collections.Generic;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    /// <summary>Maps authoritative hit-volume contacts onto visible skin without physics queries or asset mutation.</summary>
    public static class SkinnedContactPatch
    {
        public sealed class Contact
        {
            public SkinnedMeshRenderer Skin;
            public Vector3 Point,Normal;
            internal Vector3[] Baked;
            internal int[] Triangles;
            internal Snapshot Pose;
        }
        // One synchronous damage notice can contain several pellets. Skin the pose once
        // for that notice, then reuse it for every contact and both overlay kinds.
        public sealed class Projection
        {
            readonly SkinnedMeshRenderer[] skins;
            readonly Dictionary<SkinnedMeshRenderer,Snapshot> snapshots=new Dictionary<SkinnedMeshRenderer,Snapshot>();
            public Projection(GameObject body)
            {skins=body?body.GetComponentsInChildren<SkinnedMeshRenderer>(true):System.Array.Empty<SkinnedMeshRenderer>();}
            public Contact Find(Vector3 point,Vector3 direction)
            {
                if(direction.sqrMagnitude==0)return null;
                direction.Normalize();Contact closest=null,fallback=null;float nearest=float.PositiveInfinity,fallbackDistance=float.PositiveInfinity;
                foreach(var skin in skins)
                {
                    if(!skin.enabled||!skin.sharedMesh||!skin.sharedMesh.isReadable||Excluded(skin.transform))continue;
                    float depth=skin.bounds.extents.magnitude*2;
                    var origin=point-direction*depth;
                    if(!skin.bounds.IntersectRay(new Ray(origin,direction))&&skin.bounds.SqrDistance(point)>skin.bounds.extents.sqrMagnitude)continue;
                    if(!snapshots.TryGetValue(skin,out var snapshot))
                    {snapshot=new Snapshot(skin);snapshots.Add(skin,snapshot);}
                    var world=snapshot.World;var triangles=snapshot.Triangles;
                    foreach(var block in snapshot.Blocks)
                    {
                        if((closest!=null||BoxDistanceSquared(point,block.Min,block.Max)>=fallbackDistance)&&
                        !block.Intersects(origin,direction))continue;
                        for(int i=block.Start;i<block.End;i+=3)
                        {
                            var a=world[triangles[i]];var b=world[triangles[i+1]];var c=world[triangles[i+2]];
                            var normal=Vector3.Cross(b-a,c-a).normalized;
                            if(Vector3.Dot(normal,direction)>0)continue;
                            if(closest==null&&BoxDistanceSquared(point,a,b,c)<fallbackDistance)
                            {
                                var surface=ClosestPoint(point,a,b,c);float gap=(surface-point).sqrMagnitude;
                                if(gap<fallbackDistance)
                                {fallbackDistance=gap;fallback=snapshot.Contact(skin,surface,normal);}
                            }
                            if(!Intersect(origin,direction,a,b,c,out float distance)||distance>depth*2)continue;
                            var found=origin+direction*distance;float score=Vector3.Dot(found-point,direction);
                            if(score>=nearest)continue;
                            nearest=score;closest=snapshot.Contact(skin,found,normal);
                        }
                    }
                }
                return closest??fallback;
            }
        }
        internal sealed class Snapshot
        {
            public readonly Vector3[] Baked,World;
            public readonly int[] Triangles;
            public readonly TriangleBlock[] Blocks;
            readonly Mesh source;
            Vector3[] vertices,normals;BoneWeight[] weights;Matrix4x4[] bindposes;
            public Vector3[] Vertices=>vertices??(vertices=source.vertices);
            public Vector3[] Normals=>normals??(normals=source.normals);
            public BoneWeight[] Weights=>weights??(weights=source.boneWeights);
            public Matrix4x4[] Bindposes=>bindposes??(bindposes=source.bindposes);
            public Snapshot(SkinnedMeshRenderer skin)
            {
                source=skin.sharedMesh;Triangles=source.triangles;
                var mesh=new Mesh();
                try{skin.BakeMesh(mesh);Baked=mesh.vertices;}
                finally{Destroy(mesh);}
                // Transform shared vertices once rather than making three native Transform
                // calls for each triangle, again for every pellet and every overlay.
                World=new Vector3[Baked.Length];
                // Keep native TransformPoint arithmetic: thin triangles amplify even tiny
                // matrix-rounding differences in their normals and contact UV basis.
                for(int i=0;i<Baked.Length;i++)World[i]=skin.transform.TransformPoint(Baked[i]);
                // Consecutive authored triangles have useful spatial locality. Fixed-size
                // blocks preserve their order and cost linear time to build per pose.
                const int blockIndices=96; // 32 triangles; acceleration granularity only.
                Blocks=new TriangleBlock[(Triangles.Length+blockIndices-1)/blockIndices];
                for(int i=0;i<Blocks.Length;i++)
                {
                    int start=i*blockIndices,end=System.Math.Min(start+blockIndices,Triangles.Length);
                    var min=World[Triangles[start]];var max=min;
                    for(int j=start+1;j<end;j++)
                    {
                        var v=World[Triangles[j]];
                        if(v.x<min.x)min.x=v.x;if(v.y<min.y)min.y=v.y;if(v.z<min.z)min.z=v.z;
                        if(v.x>max.x)max.x=v.x;if(v.y>max.y)max.y=v.y;if(v.z>max.z)max.z=v.z;
                    }
                    Blocks[i]=new TriangleBlock{Start=start,End=end,Min=min,Max=max};
                }
            }
            public Contact Contact(SkinnedMeshRenderer skin,Vector3 point,Vector3 normal)
            =>new Contact{Skin=skin,Point=point,Normal=normal,Baked=Baked,Triangles=Triangles,Pose=this};
        }
        internal struct TriangleBlock
        {
            public int Start,End;public Vector3 Min,Max;
            public bool Intersects(Vector3 origin,Vector3 direction)
            {
                float near=0,far=float.PositiveInfinity;
                return Clip(origin.x,direction.x,Min.x,Max.x,ref near,ref far)&&
                Clip(origin.y,direction.y,Min.y,Max.y,ref near,ref far)&&
                Clip(origin.z,direction.z,Min.z,Max.z,ref near,ref far);
            }
            static bool Clip(float origin,float direction,float min,float max,ref float near,ref float far)
            {
                if(direction==0)return origin>=min&&origin<=max;
                float a=(min-origin)/direction,b=(max-origin)/direction;
                if(a>b){float swap=a;a=b;b=swap;}
                if(a>near)near=a;if(b<far)far=b;return near<=far;
            }
        }
        public static Contact Find(GameObject body,Vector3 point,Vector3 direction)
        =>new Projection(body).Find(point,direction);
        static bool Excluded(Transform t)
        {
            for(;t;t=t.parent)
            if(t.name=="weapon:joined"||t.name=="own-shadow-proxy"||t.name=="vector-armored-hands"||
            t.name=="automatic-rifle"||t.name=="pulse-launcher"||t.name=="cutter"||t.name=="forge-plasma"||t.name.StartsWith("hit-contact-"))return true;
            return false;
        }
        static bool Intersect(Vector3 origin,Vector3 direction,Vector3 a,Vector3 b,Vector3 c,out float distance)
        {
            distance=0;var ab=b-a;var ac=c-a;var cross=Vector3.Cross(direction,ac);float determinant=Vector3.Dot(ab,cross);
            if(Mathf.Abs(determinant)<1e-7f)return false;
            float inverse=1/determinant;var start=origin-a;float u=Vector3.Dot(start,cross)*inverse;
            if(u<0||u>1)return false;var q=Vector3.Cross(start,ab);float v=Vector3.Dot(direction,q)*inverse;
            if(v<0||u+v>1)return false;distance=Vector3.Dot(ac,q)*inverse;return distance>=0;
        }
        public static GameObject Create(Contact contact,float diameter,Material material,string name,out Mesh mesh)
        {
            mesh=null;if(contact==null||diameter<=0)return null;
            var skin=contact.Skin;var source=skin.sharedMesh;
            var sourceVertices=contact.Pose.Vertices;var sourceNormals=contact.Pose.Normals;var sourceWeights=contact.Pose.Weights;
            if(sourceNormals.Length!=sourceVertices.Length||sourceWeights.Length!=sourceVertices.Length)return null;
            var tangent=Vector3.Cross(Mathf.Abs(contact.Normal.y)>.9f?Vector3.right:Vector3.up,contact.Normal).normalized;
            var bitangent=Vector3.Cross(contact.Normal,tangent);float radius=diameter*.5f;
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var weights=new List<BoneWeight>();var uv=new List<Vector2>();var indices=new List<int>();
            var remap=new Dictionary<int,int>();
            foreach(var block in contact.Pose.Blocks)
            {
                if(BoxDistanceSquared(contact.Point,block.Min,block.Max)>radius*radius)continue;
                for(int i=block.Start;i<block.End;i+=3)
                {
                    int a=contact.Triangles[i],b=contact.Triangles[i+1],c=contact.Triangles[i+2];
                    var wa=contact.Pose.World[a];var wb=contact.Pose.World[b];var wc=contact.Pose.World[c];
                    // AABB rejection is conservative; the exact sphere/triangle test below
                    // still defines the patch, including edges and layered surfaces.
                    if(BoxDistanceSquared(contact.Point,wa,wb,wc)>radius*radius)continue;
                    var normal=Vector3.Cross(wb-wa,wc-wa).normalized;
                    if(Vector3.Dot(normal,contact.Normal)<.35f)continue;
                    // A surface triangle must intersect the contact sphere. Bounds alone would leak across limbs.
                    var nearest=ClosestPoint(contact.Point,wa,wb,wc);
                    if((nearest-contact.Point).sqrMagnitude>radius*radius)continue;
                    foreach(int index in new[]{a,b,c})
                    {
                        if(!remap.TryGetValue(index,out int target))
                        {
                            target=vertices.Count;remap.Add(index,target);
                            // Small normal separation prevents coplanar z-fighting; never expands a hitbox.
                            vertices.Add(sourceVertices[index]+sourceNormals[index]*.0015f);
                            normals.Add(sourceNormals[index]);weights.Add(sourceWeights[index]);
                            var delta=contact.Pose.World[index]-contact.Point;
                            uv.Add(new Vector2(Vector3.Dot(delta,tangent)/radius,Vector3.Dot(delta,bitangent)/radius));
                        }
                        indices.Add(target);
                    }
                }
            }
            if(indices.Count==0)return null;
            mesh=new Mesh{name=name+"-mesh"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.boneWeights=weights.ToArray();mesh.bindposes=contact.Pose.Bindposes;mesh.RecalculateBounds();
            var root=new GameObject(name);root.layer=skin.gameObject.layer;root.transform.SetParent(skin.transform,false);
            var patch=root.AddComponent<SkinnedMeshRenderer>();patch.sharedMesh=mesh;patch.bones=skin.bones;patch.rootBone=skin.rootBone;patch.localBounds=skin.localBounds;
            patch.sharedMaterial=material;patch.updateWhenOffscreen=true;patch.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;patch.receiveShadows=false;
            return root;
        }
        // Scalar bounds avoid temporary vector operations for every dense skin triangle.
        static float BoxDistanceSquared(Vector3 p,Vector3 min,Vector3 max)
        {
            float x=p.x<min.x?min.x-p.x:p.x>max.x?p.x-max.x:0;
            float y=p.y<min.y?min.y-p.y:p.y>max.y?p.y-max.y:0;
            float z=p.z<min.z?min.z-p.z:p.z>max.z?p.z-max.z:0;
            return x*x+y*y+z*z;
        }
        static float BoxDistanceSquared(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
        {
            float min=a.x<b.x?a.x:b.x;min=min<c.x?min:c.x;
            float max=a.x>b.x?a.x:b.x;max=max>c.x?max:c.x;
            float x=p.x<min?min-p.x:p.x>max?p.x-max:0;
            min=a.y<b.y?a.y:b.y;min=min<c.y?min:c.y;
            max=a.y>b.y?a.y:b.y;max=max>c.y?max:c.y;
            float y=p.y<min?min-p.y:p.y>max?p.y-max:0;
            min=a.z<b.z?a.z:b.z;min=min<c.z?min:c.z;
            max=a.z>b.z?a.z:b.z;max=max>c.z?max:c.z;
            float z=p.z<min?min-p.z:p.z>max?p.z-max:0;
            return x*x+y*y+z*z;
        }
        // Closest point on a triangle (Voronoi regions); preserves mapping at shoulders and mesh edges.
        static Vector3 ClosestPoint(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
        {
            var ab=b-a;var ac=c-a;var ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);
            if(d1<=0&&d2<=0)return a;
            var bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0&&d4<=d3)return b;
            float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return a+ab*(d1/(d1-d3));
            var cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return c;
            float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return a+ac*(d2/(d2-d6));
            float va=d3*d6-d5*d4;if(va<=0&&d4-d3>=0&&d5-d6>=0)return b+(c-b)*((d4-d3)/((d4-d3)+(d5-d6)));
            float denom=1/(va+vb+vc);return a+ab*(vb*denom)+ac*(vc*denom);
        }
        static void Destroy(Object item){if(Application.isPlaying)Object.Destroy(item);else Object.DestroyImmediate(item);}
    }
}
