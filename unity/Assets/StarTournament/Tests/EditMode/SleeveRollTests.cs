using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class SleeveRollTests
    {
        static void Correct(Transform elbow,Transform hand,Quaternion bind)=>typeof(TrooperVisual).Assembly
            .GetType("StarTournament.ProvingGround.FirstPersonWeaponSwitch")
            .GetMethod("UnwindSleeve",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{elbow,hand,bind});

        [TestCase(-170f)][TestCase(135f)][TestCase(-90f)]
        public void AxialRollPreservesCuffRadiusContactsAndRepeatedPose(float degrees)
        {
            var root=new GameObject("sleeve-test");var mesh=new Mesh();var baked=new Mesh();
            try
            {
                var elbow=new GameObject("elbow").transform;elbow.SetParent(root.transform,false);
                var hand=new GameObject("hand").transform;hand.SetParent(elbow,false);hand.localPosition=Vector3.up*.3f;
                var contact=new GameObject("weapon-contact").transform;contact.SetParent(hand,false);contact.localPosition=new Vector3(.07f,.1f,.03f);
                mesh.vertices=new[]{new Vector3(.05f,.3f,0),new Vector3(0,.3f,.05f),new Vector3(-.05f,.3f,0),new Vector3(0,.3f,-.05f)};
                mesh.triangles=new[]{0,1,2,0,2,3};
                mesh.bindposes=new[]{elbow.worldToLocalMatrix,hand.worldToLocalMatrix};
                mesh.boneWeights=Enumerable.Repeat(new BoneWeight{boneIndex0=0,boneIndex1=1,weight0=.5f,weight1=.5f},4).ToArray();
                var skin=root.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=mesh;skin.bones=new[]{elbow,hand};
                hand.localRotation=Quaternion.AngleAxis(degrees,Vector3.up);
                skin.BakeMesh(baked);
                Assert.That(baked.vertices.Max(v=>new Vector2(v.x,v.z).magnitude),Is.LessThan(.04f),"Control must reproduce the collapsed LBS cuff");
                var hp=hand.position;var hr=hand.rotation;var cp=contact.position;var cr=contact.rotation;
                Correct(elbow,hand,Quaternion.identity);skin.BakeMesh(baked);
                foreach(var v in baked.vertices)Assert.That(new Vector2(v.x,v.z).magnitude,Is.EqualTo(.05f).Within(.000001f),"Cuff must retain its authored radius");
                Assert.That(Vector3.Distance(hand.position,hp),Is.LessThan(.000001f));
                Assert.That(Quaternion.Angle(hand.rotation,hr),Is.LessThan(.05f));
                Assert.That(Vector3.Distance(contact.position,cp),Is.LessThan(.000001f));
                Assert.That(Quaternion.Angle(contact.rotation,cr),Is.LessThan(.05f));
                Assert.That(Vector3.Distance(hand.position,elbow.position),Is.EqualTo(.3f).Within(.000001f));
                var corrected=elbow.rotation;Correct(elbow,hand,Quaternion.identity);
                Assert.That(Quaternion.Angle(elbow.rotation,corrected),Is.LessThan(.05f));
            }
            finally{UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(baked);}
        }
        [TestCase(WeaponId.Rifle)][TestCase(WeaponId.Shotgun)][TestCase(WeaponId.RocketLauncher)]
        [TestCase(WeaponId.Cutter)][TestCase(WeaponId.Plasma)]
        public void ImportedSleeveTwistsFollowFinalPoseWithoutAccumulation(WeaponId weapon)
        {
            var owner=new GameObject("sleeve-camera");var camera=owner.AddComponent<Camera>();
            var view=UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/StarTournament/Trooper/TrooperArms.prefab"),camera.transform);
            try
            {
                var visual=view.AddComponent<TrooperVisual>();
                visual.Initialize(TrooperVisual.ClipNames.Select(n=>UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/StarTournament/Trooper/"+n+".anim")).ToArray(),ProvingProfile.CreateTrooperDefault(),true);
                var type=typeof(TrooperVisual).Assembly.GetType("StarTournament.ProvingGround.FirstPersonWeaponSwitch");
                var transition=Activator.CreateInstance(type,new object[]{view,camera});var apply=type.GetMethod("Apply");
                visual.EquippedWeapon=weapon;visual.Render(.25,Vector3.zero,100,1);
                var state=new CombatLifeState{SelectedWeapon=weapon};apply.Invoke(transition,new object[]{state,1d});
                var skin=view.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r=>r.name=="vector-armored-hands");
                var bones=skin.bones;var binds=skin.sharedMesh.bindposes;
                foreach(var side in new[]{"Left","Right"})for(int segment=1;segment<=2;segment++)
                {
                    int twist=Array.FindIndex(bones,b=>b.name==side+"LowerArmTwist"+segment);
                    int lower=Array.FindIndex(bones,b=>b.name==side+"LowerArm");
                    var expected=(binds[lower]*binds[twist].inverse).rotation;
                    Assert.That(Quaternion.Angle(bones[twist].localRotation,expected),Is.LessThan(.05f),weapon+" "+bones[twist].name);
                }
                var transforms=view.GetComponentsInChildren<Transform>();
                var positions=transforms.Select(t=>t.position).ToArray();var rotations=transforms.Select(t=>t.rotation).ToArray();
                for(int repeat=0;repeat<3;repeat++)apply.Invoke(transition,new object[]{state,1d});
                for(int i=0;i<transforms.Length;i++)
                {
                    Assert.That(Vector3.Distance(transforms[i].position,positions[i]),Is.LessThan(.00001f),transforms[i].name);
                    Assert.That(Quaternion.Angle(transforms[i].rotation,rotations[i]),Is.LessThan(.05f),transforms[i].name);
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(owner);}
        }
        [Test]
        public void NonIdentityBindAndWristBendKeepContactsAndBoneLength()
        {
            var elbow=new GameObject("elbow").transform;var hand=new GameObject("hand").transform;hand.SetParent(elbow,false);
            try
            {
                var bind=Quaternion.Euler(0,0,23);hand.localPosition=new Vector3(.1f,.3f,.04f);
                var axis=hand.localPosition.normalized;var perpendicular=Vector3.Cross(axis,Vector3.forward).normalized;
                hand.localRotation=Quaternion.AngleAxis(25,perpendicular)*Quaternion.AngleAxis(150,axis)*bind;
                elbow.rotation=Quaternion.Euler(10,35,28);var position=hand.position;var rotation=hand.rotation;var length=hand.localPosition;
                Correct(elbow,hand,bind);
                Assert.That(Vector3.Distance(hand.position,position),Is.LessThan(.000001f));
                Assert.That(Quaternion.Angle(hand.rotation,rotation),Is.LessThan(.05f));
                Assert.That(hand.localPosition,Is.EqualTo(length));
                var delta=hand.localRotation*Quaternion.Inverse(bind);
                Assert.That(Mathf.Abs(Vector3.Dot(new Vector3(delta.x,delta.y,delta.z),axis)),Is.LessThan(.000001f));
            }
            finally{UnityEngine.Object.DestroyImmediate(elbow.gameObject);}
        }
    }
}
