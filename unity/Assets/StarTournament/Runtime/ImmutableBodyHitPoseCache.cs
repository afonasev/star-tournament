using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    /// <summary>Caller-owned reuse for immutable imported body meshes, never across a frame or an exact pose change.</summary>
    public sealed class ImmutableBodyHitPoseCache
    {
        static readonly ProfilerMarker QueryMarker=new ProfilerMarker("Hit.ContactPoseReuse.Query");
        public GameObject Body {get;}
        readonly List<SkinnedMeshRenderer> renderers=new List<SkinnedMeshRenderer>();
        readonly List<SkinPose> poses=new List<SkinPose>();
        readonly Dictionary<RayKey,SkinnedContactPatch.Contact> contacts=new Dictionary<RayKey,SkinnedContactPatch.Contact>();
        SkinnedContactPatch.Projection projection;int frame=-1,poseCount,weightLimit;
        public long ProjectionBuilds {get;private set;}
        public long FindCalls {get;private set;}
        public long ContactReuses {get;private set;}
        public ImmutableBodyHitPoseCache(GameObject body){Body=body;}
        public SkinnedContactPatch.Contact Find(Vector3 point,Vector3 direction)
        {
            using var measurement=QueryMarker.Auto();
            bool same=projection!=null&&frame==Time.frameCount;
            int weights=(int)QualitySettings.skinWeights;same&=weights==weightLimit;weightLimit=weights;
            renderers.Clear();if(Body)Body.GetComponentsInChildren(true,renderers);
            renderers.RemoveAll(renderer=>IsOverlay(renderer.transform));
            int count=0;
            foreach(var renderer in renderers)
            {
                // Generated patches follow skin but are excluded by Projection. Their
                // insertion/removal cannot change the source body surface being queried.
                if(count==poses.Count)poses.Add(new SkinPose());
                same&=poses[count++].Capture(renderer);
            }
            same&=count==poseCount;poseCount=count;frame=Time.frameCount;
            if(!same){projection=new SkinnedContactPatch.Projection(renderers.ToArray(),immutableTopology:true);contacts.Clear();ProjectionBuilds++;}
            var key=new RayKey(point,direction);
            if(contacts.TryGetValue(key,out var found)){ContactReuses++;return found;}
            FindCalls++;found=projection.Find(point,direction);contacts.Add(key,found);return found;
        }
        public void Clear(){projection=null;contacts.Clear();frame=-1;}
        static bool IsOverlay(Transform node)
        {for(;node;node=node.parent)if(node.name.StartsWith("hit-contact-",StringComparison.Ordinal))return true;return false;}
        static bool Same(float a,float b)=>BitConverter.SingleToInt32Bits(a)==BitConverter.SingleToInt32Bits(b);
        static bool Same(Vector3 a,Vector3 b)=>Same(a.x,b.x)&&Same(a.y,b.y)&&Same(a.z,b.z);
        static bool Same(Matrix4x4 a,Matrix4x4 b)
        {for(int i=0;i<16;i++)if(!Same(a[i],b[i]))return false;return true;}
        sealed class SkinPose
        {
            SkinnedMeshRenderer renderer;Mesh mesh;Transform root;
            bool enabled,active;int quality;Matrix4x4 world;Bounds bounds,localBounds;
            Transform[] bones=Array.Empty<Transform>();Matrix4x4[] matrices=Array.Empty<Matrix4x4>();float[] blends=Array.Empty<float>();
            public bool Capture(SkinnedMeshRenderer skin)
            {
                var source=skin.sharedMesh;var transform=skin.localToWorldMatrix;var box=skin.bounds;var local=skin.localBounds;var currentBones=skin.bones;
                bool same=ReferenceEquals(renderer,skin)&&ReferenceEquals(mesh,source)&&ReferenceEquals(root,skin.rootBone)&&enabled==skin.enabled&&active==skin.gameObject.activeInHierarchy&&quality==(int)skin.quality&&Same(world,transform)&&Same(bounds.center,box.center)&&Same(bounds.extents,box.extents)&&Same(localBounds.center,local.center)&&Same(localBounds.extents,local.extents);
                renderer=skin;mesh=source;root=skin.rootBone;enabled=skin.enabled;active=skin.gameObject.activeInHierarchy;quality=(int)skin.quality;world=transform;bounds=box;localBounds=local;
                if(bones.Length!=currentBones.Length){same=false;bones=new Transform[currentBones.Length];matrices=new Matrix4x4[currentBones.Length];}
                for(int i=0;i<currentBones.Length;i++)
                {
                    var bone=currentBones[i];var matrix=bone?bone.localToWorldMatrix:Matrix4x4.zero;
                    same&=ReferenceEquals(bones[i],bone)&&Same(matrices[i],matrix);bones[i]=bone;matrices[i]=matrix;
                }
                int blendCount=source?source.blendShapeCount:0;
                if(blends.Length!=blendCount){same=false;blends=new float[blendCount];}
                for(int i=0;i<blendCount;i++){float value=skin.GetBlendShapeWeight(i);same&=Same(blends[i],value);blends[i]=value;}
                return same;
            }
        }
        readonly struct RayKey:IEquatable<RayKey>
        {
            readonly int px,py,pz,dx,dy,dz;
            public RayKey(Vector3 p,Vector3 d){px=BitConverter.SingleToInt32Bits(p.x);py=BitConverter.SingleToInt32Bits(p.y);pz=BitConverter.SingleToInt32Bits(p.z);dx=BitConverter.SingleToInt32Bits(d.x);dy=BitConverter.SingleToInt32Bits(d.y);dz=BitConverter.SingleToInt32Bits(d.z);}
            public bool Equals(RayKey other)=>px==other.px&&py==other.py&&pz==other.pz&&dx==other.dx&&dy==other.dy&&dz==other.dz;
            public override bool Equals(object other)=>other is RayKey key&&Equals(key);
            public override int GetHashCode(){unchecked{int h=px;h=h*397^py;h=h*397^pz;h=h*397^dx;h=h*397^dy;return h*397^dz;}}
        }
    }
}
