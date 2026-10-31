#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class ImmutableBodyHitPoseCacheTests
    {
        GameObject body;Transform bone;SkinnedMeshRenderer skin;readonly List<Mesh> owned=new List<Mesh>();
        readonly Vector3 point=new Vector3(0,0,-.02f),direction=Vector3.forward;
        void Setup(float size=1,bool blend=false)
        {
            body=new GameObject("immutable body cache fixture");bone=new GameObject("bone").transform;bone.SetParent(body.transform,false);
            skin=body.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=Quad(size,blend);skin.bones=new[]{bone};skin.rootBone=bone;skin.updateWhenOffscreen=true;skin.localBounds=new Bounds(Vector3.zero,Vector3.one*4);
        }
        Mesh Quad(float size,bool blend=false)
        {
            var mesh=new Mesh{name="immutable fixture quad"};owned.Add(mesh);
            mesh.vertices=new[]{new Vector3(-size,-size,0),new Vector3(size,-size,0),new Vector3(size,size,0),new Vector3(-size,size,0)};mesh.triangles=new[]{0,2,1,0,3,2};mesh.RecalculateNormals();
            mesh.bindposes=new[]{Matrix4x4.identity};mesh.boneWeights=new[]{Weight(),Weight(),Weight(),Weight()};
            if(blend){var delta=new[]{Vector3.forward*.1f,Vector3.forward*.1f,Vector3.forward*.1f,Vector3.forward*.1f};mesh.AddBlendShapeFrame("inflate",100,delta,new Vector3[4],new Vector3[4]);}
            mesh.RecalculateBounds();return mesh;
        }
        static BoneWeight Weight()=>new BoneWeight{boneIndex0=0,weight0=1};
        [TearDown] public void Cleanup(){if(body)Object.DestroyImmediate(body);foreach(var mesh in owned)if(mesh)Object.DestroyImmediate(mesh);owned.Clear();}
        static void Vector(BinaryWriter w,Vector3 v){w.Write(v.x);w.Write(v.y);w.Write(v.z);}
        static byte[] Data(SkinnedContactPatch.Contact contact)
        {
            using var data=new MemoryStream();using var w=new BinaryWriter(data);
            if(contact==null){w.Write(false);return data.ToArray();}w.Write(true);Vector(w,contact.Point);Vector(w,contact.Normal);
            var patch=SkinnedContactPatch.Create(contact,.3f,null,"hit-contact-test",out var mesh);
            try
            {
                w.Write(mesh!=null);if(!mesh)return data.ToArray();
                var v=mesh.vertices;w.Write(v.Length);foreach(var x in v)Vector(w,x);
                var n=mesh.normals;w.Write(n.Length);foreach(var x in n)Vector(w,x);
                var uv=mesh.uv;w.Write(uv.Length);foreach(var x in uv){w.Write(x.x);w.Write(x.y);}
                var t=mesh.triangles;w.Write(t.Length);foreach(int x in t)w.Write(x);
                var weights=mesh.boneWeights;w.Write(weights.Length);foreach(var x in weights){w.Write(x.boneIndex0);w.Write(x.boneIndex1);w.Write(x.boneIndex2);w.Write(x.boneIndex3);w.Write(x.weight0);w.Write(x.weight1);w.Write(x.weight2);w.Write(x.weight3);}
                var binds=mesh.bindposes;w.Write(binds.Length);foreach(var x in binds)for(int i=0;i<16;i++)w.Write(x[i]);
                return data.ToArray();
            }
            finally {if(patch)Object.DestroyImmediate(patch);if(mesh)Object.DestroyImmediate(mesh);}
        }
        SkinnedContactPatch.Contact EqualFresh(ImmutableBodyHitPoseCache cache,Vector3 p,Vector3 d)
        {
            var actual=cache.Find(p,d);var fresh=new SkinnedContactPatch.Projection(body,immutableTopology:true).Find(p,d);
            Assert.That(Data(actual),Is.EqualTo(Data(fresh)),"Contact and every mesh/UV/weight/bindpose byte must match fresh projection");return actual;
        }
        [UnityTest] public IEnumerator SameFrameRepeatsReuseButKeepEveryGeometryByte()
        {
            Setup();var cache=new ImmutableBodyHitPoseCache(body);var a=EqualFresh(cache,point,direction);Assert.That(a,Is.Not.Null);
            for(int i=0;i<8;i++)Assert.That(EqualFresh(cache,point,direction),Is.SameAs(a));
            Assert.That(cache.ProjectionBuilds,Is.EqualTo(1));Assert.That(cache.FindCalls,Is.EqualTo(1));Assert.That(cache.ContactReuses,Is.EqualTo(8));
            var b=EqualFresh(cache,point+Vector3.right*.1f,direction);Assert.That(b,Is.Not.SameAs(a));Assert.That(cache.ProjectionBuilds,Is.EqualTo(1));Assert.That(cache.FindCalls,Is.EqualTo(2));yield return null;
        }
        [UnityTest] public IEnumerator BoneAndRootTransformsInvalidateWithinTheSameFrame()
        {
            Setup();var cache=new ImmutableBodyHitPoseCache(body);var a=EqualFresh(cache,point,direction);
            bone.localRotation=Quaternion.Euler(7,9,3);var b=EqualFresh(cache,point,direction);Assert.That(b,Is.Not.SameAs(a));
            body.transform.position=new Vector3(.125f,0,.01f);var c=EqualFresh(cache,point,direction);Assert.That(c,Is.Not.SameAs(b));
            body.transform.localScale=new Vector3(1.1f,.9f,1);Assert.That(EqualFresh(cache,point,direction),Is.Not.SameAs(c));yield return null;
        }
        [UnityTest] public IEnumerator MeshBoneIdentityAndBlendWeightsInvalidateExactly()
        {
            Setup(blend:true);var cache=new ImmutableBodyHitPoseCache(body);var a=EqualFresh(cache,point,direction);
            skin.SetBlendShapeWeight(0,40);var b=EqualFresh(cache,point,direction);Assert.That(b,Is.Not.SameAs(a));
            skin.sharedMesh=Quad(.8f,true);var c=EqualFresh(cache,point,direction);Assert.That(c,Is.Not.SameAs(b));
            var other=new GameObject("replacement bone").transform;other.SetParent(body.transform,false);skin.bones=new[]{other};skin.rootBone=other;
            Assert.That(EqualFresh(cache,point,direction),Is.Not.SameAs(c));yield return null;
        }
        [UnityTest] public IEnumerator BoundsRendererMembershipEnableAndQualityInvalidate()
        {
            Setup();skin.updateWhenOffscreen=false;skin.localBounds=new Bounds(Vector3.zero,Vector3.one*4);
            var cache=new ImmutableBodyHitPoseCache(body);var a=EqualFresh(cache,point,direction);
            skin.localBounds=new Bounds(Vector3.zero,Vector3.one*5);
            Assert.That(skin.localBounds.size.x,Is.EqualTo(5f),"Fixture must retain the explicitly changed local bounds rather than automatic offscreen bounds");
            var b=EqualFresh(cache,point,direction);Assert.That(b,Is.Not.SameAs(a),"Changed manual bounds must invalidate pose/contact reuse");
            skin.enabled=false;Assert.That(EqualFresh(cache,point,direction),Is.Null);skin.enabled=true;var c=EqualFresh(cache,point,direction);Assert.That(c,Is.Not.SameAs(b));
            skin.quality=SkinQuality.Bone1;var d=EqualFresh(cache,point,direction);Assert.That(d,Is.Not.SameAs(c));
            var extra=new GameObject("another source skin");extra.transform.SetParent(body.transform,false);var renderer=extra.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=skin.sharedMesh;renderer.bones=skin.bones;renderer.rootBone=bone;renderer.localBounds=skin.localBounds;
            Assert.That(EqualFresh(cache,point,direction),Is.Not.SameAs(d));yield return null;
        }
        [UnityTest] public IEnumerator FrameAndExplicitClearNeverReuseAnEarlierPose()
        {
            Setup();var cache=new ImmutableBodyHitPoseCache(body);var a=EqualFresh(cache,point,direction);cache.Clear();var b=EqualFresh(cache,point,direction);Assert.That(b,Is.Not.SameAs(a));
            yield return null;Assert.That(EqualFresh(cache,point,direction),Is.Not.SameAs(b));
        }
        [UnityTest] public IEnumerator AddedHitOverlayDoesNotInvalidateOrEnterTheSourceSurface()
        {
            Setup();var cache=new ImmutableBodyHitPoseCache(body);var a=EqualFresh(cache,point,direction);
            var patch=SkinnedContactPatch.Create(a,.3f,null,"hit-contact-shield",out var mesh);owned.Add(mesh);
            Assert.That(EqualFresh(cache,point,direction),Is.SameAs(a));Assert.That(cache.ProjectionBuilds,Is.EqualTo(1));Object.DestroyImmediate(patch);
            Assert.That(EqualFresh(cache,point,direction),Is.SameAs(a));yield return null;
        }
        [UnityTest] public IEnumerator RemovedPreexistingOverlayCannotLeaveADeadProjectionRenderer()
        {
            Setup();var original=new SkinnedContactPatch.Projection(body,immutableTopology:true).Find(point,direction);
            var patch=SkinnedContactPatch.Create(original,.3f,null,"hit-contact-shield",out var mesh);owned.Add(mesh);
            var cache=new ImmutableBodyHitPoseCache(body);var a=EqualFresh(cache,point,direction);
            Object.DestroyImmediate(patch);
            Assert.That(EqualFresh(cache,point,direction),Is.SameAs(a));
            Assert.That(EqualFresh(cache,point+Vector3.right*.1f,direction),Is.Not.Null);
            Assert.That(cache.ProjectionBuilds,Is.EqualTo(1));yield return null;
        }
        [UnityTest] public IEnumerator TinyFaceZeroDirectionAndSignedZeroKeepFreshSemantics()
        {
            Setup(.002f);var cache=new ImmutableBodyHitPoseCache(body);var a=EqualFresh(cache,point,direction);Assert.That(a,Is.Not.Null);Assert.That(a.Normal.sqrMagnitude,Is.GreaterThan(.9f));
            Assert.That(EqualFresh(cache,point,Vector3.zero),Is.Null);
            float negativeZero=BitConverter.Int32BitsToSingle(unchecked((int)0x80000000));var negativePoint=new Vector3(negativeZero,0,-.02f);
            Assert.That(EqualFresh(cache,negativePoint,direction),Is.Not.SameAs(a));Assert.That(cache.FindCalls,Is.EqualTo(3));yield return null;
        }
    }
}
#endif
