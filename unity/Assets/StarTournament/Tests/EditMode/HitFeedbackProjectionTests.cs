using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class HitFeedbackProjectionTests
    {
        [Test] public void LayeredBodyContactSelectsOutermostPlateInsteadOfInnerClothing()
        {
            var root=new GameObject("layered-hit-fixture");var bone=new GameObject("bone");bone.transform.SetParent(root.transform,false);
            Mesh outer=null,inner=null;
            try
            {
                SkinnedMeshRenderer Layer(string name,float z,out Mesh mesh)
                {
                    var go=new GameObject(name);go.transform.SetParent(root.transform,false);var skin=go.AddComponent<SkinnedMeshRenderer>();
                    mesh=new Mesh{vertices=new[]{new Vector3(-1,-1,z),new Vector3(1,-1,z),new Vector3(1,1,z),new Vector3(-1,1,z)},
                        normals=new[]{Vector3.back,Vector3.back,Vector3.back,Vector3.back},triangles=new[]{0,2,1,0,3,2},
                        boneWeights=new[]{new BoneWeight{boneIndex0=0,weight0=1},new BoneWeight{boneIndex0=0,weight0=1},new BoneWeight{boneIndex0=0,weight0=1},new BoneWeight{boneIndex0=0,weight0=1}},
                        bindposes=new[]{Matrix4x4.identity}};
                    skin.sharedMesh=mesh;skin.bones=new[]{bone.transform};skin.rootBone=bone.transform;skin.localBounds=new Bounds(new Vector3(0,0,z),new Vector3(2,2,.01f));return skin;
                }
                var plate=Layer("outer-plate",0,out outer);Layer("inner-cloth",.1f,out inner);
                var contact=SkinnedContactPatch.Find(root,new Vector3(0,0,.1f),Vector3.forward);
                Assert.That(contact,Is.Not.Null);Assert.That(contact.Skin,Is.EqualTo(plate));Assert.That(contact.Point.z,Is.Zero.Within(.001f));
            }
            finally{Object.DestroyImmediate(root);if(outer)Object.DestroyImmediate(outer);if(inner)Object.DestroyImmediate(inner);}
        }
    }
}
