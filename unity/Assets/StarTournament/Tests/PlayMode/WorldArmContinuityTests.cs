using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class WorldArmContinuityTests
    {
        readonly TrooperPresentationTests fixture=new TrooperPresentationTests();
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        [UnityTest, Timeout(240000)]
        public IEnumerator AllWeaponsKeepForearmsOutsideTorsoAndPreserveFrozenPose()
        {
            yield return (IEnumerator)typeof(TrooperPresentationTests).GetMethod("Load",Private).Invoke(fixture,new object[]{1});
            var ground=(ProvingGround)typeof(TrooperPresentationTests).GetField("ground",Private).GetValue(fixture);
            ground.enabled=false;
            typeof(ProvingGround).GetField("FullHealReviewManualTick",Private).SetValue(ground,true);
            ground.PlaceCombatReviewSeat(0,new Vector3(-8,0,26),0,90);
            var body=ground.transform.Find("player-1/trooper-presentation").GetComponent<TrooperVisual>();
            var output=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_WORLD_ARM_OUTPUT");
            var errors=new List<string>();var rows=new List<string>{"weapon,phase,side,torso_crossings"};
            var bones=body.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Left")||t.name.StartsWith("Right")).ToArray();
            foreach(WeaponId weapon in Enum.GetValues(typeof(WeaponId)))
            {
                ground.PlaceCombatReviewSeat(0,new Vector3(-8,0,26),0,90);
                var state=ground.Session.Capture();var life=state.Lives[0];
                life.ShotgunOwned=life.RocketOwned=life.CutterOwned=life.PlasmaOwned=true;
                life.ShotgunAmmo=20;life.RocketAmmo=8;life.CutterEnergy=30;life.PlasmaAmmo=100;
                life.SelectedWeapon=weapon;life.PendingWeapon=default;life.SwitchRemaining=0;life.Ammo=CombatLife.AmmoFor(life,weapon);
                state.Lives[0]=life;ground.Session.Restore(state);body.ResetPose(state.Time);
                var mount=body.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="trooper:rig:weapon-mount");
                Vector3[] gripContacts=null;
                foreach(string phase in weapon==WeaponId.RocketLauncher?new[]{"idle","walk","kick","fire","recovery"}:new[]{"idle","walk","fire"})
                {
                    var actions=new LocalAction[ground.Session.ParticipantCount];
                    if(phase=="walk")
                    {
                        actions[0].Move=Vector2.up;
                        for(int tick=0;tick<16;tick++)ground.Session.Tick(actions,.02f);
                    }
                    if(phase=="kick"||(phase=="fire"&&weapon!=WeaponId.RocketLauncher))
                    {
                        actions[0].Fire=true;actions[0].FireHeld=true;ground.Session.Tick(actions,.02f);
                        actions[0]=default;for(int tick=0;tick<(phase=="kick"?2:4);tick++)ground.Session.Tick(actions,.02f);
                    }
                    else if(phase=="fire"||phase=="recovery")
                        for(int tick=0;tick<(phase=="fire"?2:8);tick++)ground.Session.Tick(actions,.02f);
                    ground.SendMessage("LateUpdate");yield return null;
                    var fittedGrip=typeof(TrooperVisual).GetField("equipmentGrip",Private).GetValue(body);
                    Assert.That((float)fittedGrip.GetType().GetProperty("WorldGripError").GetValue(fittedGrip),Is.LessThan(.0001f),weapon+" "+phase+" both hand targets remain reachable");
                    var hands=new[]{"LeftHand","RightHand"}.Select(name=>bones.Single(t=>t.name==name)).ToArray();
                    if(phase=="idle")gripContacts=hands.Select(t=>mount.InverseTransformPoint(t.position)).ToArray();
                    else for(int i=0;i<hands.Length;i++)
                        Assert.That(Vector3.Distance(hands[i].position,mount.TransformPoint(gripContacts[i])),Is.LessThan(.0001f),weapon+" "+phase+" hand remains on the same grip");
                    foreach(string side in new[]{"Left","Right"})
                    {
                        var elbow=bones.Single(t=>t.name==side+"LowerArm");var hand=bones.Single(t=>t.name==side+"Hand");
                        int count=TorsoCrossings(body,elbow,hand);rows.Add(weapon+","+phase+","+side+","+count);
                        if(count!=0)errors.Add(weapon+" "+phase+" "+side+" forearm crosses "+count+" torso triangles");
                    }
                    var positions=bones.Select(t=>t.position).ToArray();var rotations=bones.Select(t=>t.rotation).ToArray();
                    var localPositions=bones.Select(t=>t.localPosition).ToArray();
                    string simulation=JsonUtility.ToJson(ground.Session.Capture());
                    ground.SendMessage("LateUpdate");ground.SendMessage("LateUpdate");
                    Assert.That(JsonUtility.ToJson(ground.Session.Capture()),Is.EqualTo(simulation),"Presentation cannot advance simulation");
                    for(int i=0;i<bones.Length;i++)
                    {
                        Assert.That(Vector3.Distance(bones[i].position,positions[i]),Is.LessThan(.00001f),weapon+" "+phase+" frozen "+bones[i].name);
                        Assert.That(Quaternion.Angle(bones[i].rotation,rotations[i]),Is.LessThan(.05f),weapon+" "+phase+" frozen rotation "+bones[i].name);
                    }
                    var grip=typeof(TrooperVisual).GetField("equipmentGrip",Private).GetValue(body);
                    grip.GetType().GetMethod("RestorePrimary").Invoke(grip,null);
                    for(int i=0;i<bones.Length;i++)
                        Assert.That(Vector3.Distance(bones[i].localPosition,localPositions[i]),Is.LessThan(.00001f),weapon+" "+phase+" unchanged attachment "+bones[i].name);
                    ground.SendMessage("LateUpdate");yield return null;
                    if(!string.IsNullOrEmpty(output))
                    {
                        string directory=Path.Combine(output,phase);Directory.CreateDirectory(directory);
                        typeof(TrooperPresentationTests).GetMethod("CaptureWorldModel",Private).Invoke(fixture,new object[]{directory,weapon.ToString()});
                        File.WriteAllLines(Path.Combine(output,"torso-crossings.csv"),rows);
                    }
                }
            }
            if(!string.IsNullOrEmpty(output))
            {
                var skins=body.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r=>r.name!="own-shadow-proxy").ToArray();
                File.WriteAllLines(Path.Combine(output,"asset-inventory.txt"),skins.Select(r=>r.name+": triangles="+r.sharedMesh.triangles.Length/3+", material_slots="+r.sharedMaterials.Length+", shadows="+r.shadowCastingMode));
            }
            Assert.That(errors,Is.Empty,string.Join("; ",errors));
        }
        static int TorsoCrossings(TrooperVisual body,Transform elbow,Transform hand)
        {
            int crossings=0;
            foreach(var skin in body.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if(skin.name=="own-shadow-proxy")continue;
                var weights=skin.sharedMesh.boneWeights;
                if(weights.Length!=skin.sharedMesh.vertexCount)continue;
                var torso=new bool[weights.Length];
                for(int i=0;i<weights.Length;i++)
                {
                    var w=weights[i];int bone=w.boneIndex0;float amount=w.weight0;
                    if(w.weight1>amount){bone=w.boneIndex1;amount=w.weight1;}
                    if(w.weight2>amount){bone=w.boneIndex2;amount=w.weight2;}
                    if(w.weight3>amount)bone=w.boneIndex3;
                    string name=skin.bones[bone].name;
                    torso[i]=name=="Spine"||name=="Chest"||name=="UpperChest";
                }
                var mesh=new Mesh();
                try
                {
                    skin.BakeMesh(mesh);var vertices=mesh.vertices;var triangles=mesh.triangles;
                    var origin=skin.transform.InverseTransformPoint(elbow.position);
                    var segment=skin.transform.InverseTransformPoint(hand.position)-origin;
                    for(int i=0;i<triangles.Length;i+=3)
                    {
                        int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                        if(!torso[a]||!torso[b]||!torso[c])continue;
                        var edge1=vertices[b]-vertices[a];var edge2=vertices[c]-vertices[a];
                        var cross=Vector3.Cross(segment,edge2);float determinant=Vector3.Dot(edge1,cross);
                        if(Mathf.Abs(determinant)<1e-10f)continue;
                        var offset=origin-vertices[a];float u=Vector3.Dot(offset,cross)/determinant;
                        if(u<0||u>1)continue;
                        var q=Vector3.Cross(offset,edge1);float v=Vector3.Dot(segment,q)/determinant;
                        if(v<0||u+v>1)continue;
                        float t=Vector3.Dot(edge2,q)/determinant;
                        if(t>0&&t<1)crossings++;
                    }
                }
                finally{UnityEngine.Object.Destroy(mesh);}
            }
            return crossings;
        }
        [UnityTearDown] public IEnumerator Cleanup(){yield return fixture.Cleanup();}
    }
}
