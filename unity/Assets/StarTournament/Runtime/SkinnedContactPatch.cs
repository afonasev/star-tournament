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
        }
        public static Contact Find(GameObject body,Vector3 point,Vector3 direction)
        {
            if(!body||direction.sqrMagnitude==0)return null;
            direction.Normalize();Contact closest=null,fallback=null;float nearest=float.PositiveInfinity,fallbackDistance=float.PositiveInfinity;
            foreach(var skin in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if(!skin.enabled||!skin.sharedMesh||!skin.sharedMesh.isReadable||Excluded(skin.transform))continue;
                // Bounds-derived projection depth covers the skin; this is a mapping invariant, not VFX tuning.
                float depth=skin.bounds.extents.magnitude*2;
                var origin=point-direction*depth;
                if(!skin.bounds.IntersectRay(new Ray(origin,direction))&&skin.bounds.SqrDistance(point)>skin.bounds.extents.sqrMagnitude)continue;
                var baked=new Mesh();skin.BakeMesh(baked);
                var local=baked.vertices;var triangles=skin.sharedMesh.triangles;
                for(int i=0;i<triangles.Length;i+=3)
                {
                    var a=skin.transform.TransformPoint(local[triangles[i]]);
                    var b=skin.transform.TransformPoint(local[triangles[i+1]]);
                    var c=skin.transform.TransformPoint(local[triangles[i+2]]);
                    var normal=Vector3.Cross(b-a,c-a).normalized;
                    // Contact patches belong to the entering side, not the exit/back surface.
                    if(Vector3.Dot(normal,direction)>0)continue;
                    if(closest==null)
                    {
                        var surface=ClosestPoint(point,a,b,c);float gap=(surface-point).sqrMagnitude;
                        if(gap<fallbackDistance)
                        {fallbackDistance=gap;fallback=new Contact{Skin=skin,Point=surface,Normal=normal,Baked=local,Triangles=triangles};}
                    }
                    if(!Intersect(origin,direction,a,b,c,out float distance)||distance>depth*2)continue;
                    var found=origin+direction*distance;
                    // The capsule contact can lie inside layered skin/clothing/plates. Choose the
                    // first surface along the incoming ray so the overlay never sits beneath armor.
                    float score=Vector3.Dot(found-point,direction);
                    if(score>=nearest)continue;
                    nearest=score;closest=new Contact{Skin=skin,Point=found,Normal=normal,Baked=local,Triangles=triangles};
                }
                Destroy(baked);
            }
            // An authoritative capsule can be hit in a gap between visible limbs. Keep feedback on skin.
            return closest??fallback;
        }
        static bool Excluded(Transform t)
        {
            for(;t;t=t.parent)
                if(t.name=="weapon:joined"||t.name=="own-shadow-proxy"||t.name=="vector-armored-hands"||
                    t.name=="automatic-rifle"||t.name=="pulse-launcher"||t.name=="cutter"||t.name.StartsWith("hit-contact-"))return true;
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
            var sourceVertices=source.vertices;var sourceNormals=source.normals;var sourceWeights=source.boneWeights;
            if(sourceNormals.Length!=sourceVertices.Length||sourceWeights.Length!=sourceVertices.Length)return null;
            var tangent=Vector3.Cross(Mathf.Abs(contact.Normal.y)>.9f?Vector3.right:Vector3.up,contact.Normal).normalized;
            var bitangent=Vector3.Cross(contact.Normal,tangent);float radius=diameter*.5f;
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var weights=new List<BoneWeight>();var uv=new List<Vector2>();var indices=new List<int>();
            var remap=new Dictionary<int,int>();
            for(int i=0;i<contact.Triangles.Length;i+=3)
            {
                int a=contact.Triangles[i],b=contact.Triangles[i+1],c=contact.Triangles[i+2];
                var wa=skin.transform.TransformPoint(contact.Baked[a]);var wb=skin.transform.TransformPoint(contact.Baked[b]);var wc=skin.transform.TransformPoint(contact.Baked[c]);
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
                        var delta=skin.transform.TransformPoint(contact.Baked[index])-contact.Point;
                        uv.Add(new Vector2(Vector3.Dot(delta,tangent)/radius,Vector3.Dot(delta,bitangent)/radius));
                    }
                    indices.Add(target);
                }
            }
            if(indices.Count==0)return null;
            mesh=new Mesh{name=name+"-mesh"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.boneWeights=weights.ToArray();mesh.bindposes=source.bindposes;mesh.RecalculateBounds();
            var root=new GameObject(name);root.layer=skin.gameObject.layer;root.transform.SetParent(skin.transform,false);
            var patch=root.AddComponent<SkinnedMeshRenderer>();patch.sharedMesh=mesh;patch.bones=skin.bones;patch.rootBone=skin.rootBone;patch.localBounds=skin.localBounds;
            patch.sharedMaterial=material;patch.updateWhenOffscreen=true;patch.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;patch.receiveShadows=false;
            return root;
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
