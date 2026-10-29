#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class WristDigitReviewTests
    {
        Scene scene; Gamepad pad; bool previousAsync; string temporaryOutput;
        [UnityTest] public IEnumerator NativeWristAndSeamDigits()
        {
            previousAsync=UnityEditor.ShaderUtil.allowAsyncCompilation;UnityEditor.ShaderUtil.allowAsyncCompilation=false;
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");
            var ground=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ProvingGround>()).Single();
            yield return NativeLoadingTestScene.WaitLabHistory(ground);ground.enabled=false;
            pad=InputSystem.AddDevice<Gamepad>();ground.StartCombatReview(new[]{pad},ensureOpponent:true);yield return null;
            ground.PlaceCombatReviewSeat(0,new Vector3(-8,0,26),0,90);ground.PlaceCombatReviewSeat(1,new Vector3(8,0,30),0,180);
            var camera=ground.transform.Find("seat-camera-1").GetComponent<Camera>();camera.aspect=16f/9;
            var view=camera.transform.Find("trooper-view").GetComponent<TrooperVisual>();
            var bones=view.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Left")||t.name.StartsWith("Right")).ToArray();
            var output=Environment.GetEnvironmentVariable("STAR_WRIST_DIGIT_CAPTURE");
            if(string.IsNullOrEmpty(output))output=temporaryOutput=Path.Combine(Application.temporaryCachePath,"wrist-digit-"+Guid.NewGuid());
            Directory.CreateDirectory(output);
            var capture=typeof(TrooperPresentationTests).GetMethod("CaptureSingleView",BindingFlags.Static|BindingFlags.NonPublic);
            var close=typeof(TrooperPresentationTests).GetMethod("CaptureGrip",BindingFlags.Instance|BindingFlags.NonPublic);
            foreach(WeaponId weapon in Enum.GetValues(typeof(WeaponId)))
            {
                var state=ground.Session.Capture();var life=state.Lives[0];life.ShotgunOwned=life.RocketOwned=life.CutterOwned=life.PlasmaOwned=true;
                life.ShotgunAmmo=20;life.RocketAmmo=8;life.PlasmaAmmo=100;life.SelectedWeapon=weapon;life.PendingWeapon=default;life.SwitchRemaining=0;life.Ammo=CombatLife.AmmoFor(life,weapon);state.Lives[0]=life;
                ground.Session.Restore(state);view.ResetPose(ground.Session.Time);ground.SendMessage("LateUpdate");yield return null;
                capture.Invoke(null,new object[]{output,weapon.ToString(),camera,1920,1080});
                if(weapon==WeaponId.Cutter||weapon==WeaponId.RocketLauncher)close.Invoke(new TrooperPresentationTests(),new object[]{output,weapon.ToString(),view,camera});
                if(weapon==WeaponId.Cutter)
                {
                    var gun=view.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="cutter");var tuning=ProvingProfile.CreateCutterDefault();
                    var target=gun.TransformPoint(new Vector3(tuning.Get("cutter.seam.surface.thumbContactX"),tuning.Get("cutter.seam.surface.thumbContactY"),tuning.Get("cutter.seam.surface.thumbContactZ")));
                    Assert.That(Vector3.Distance(bones.Single(t=>t.name=="LeftThumbDistal").position,target),Is.LessThan(.001f),"Support thumb must close on the SEAM grip, not point into free space");
                    var thumb=bones.Where(t=>t.name.StartsWith("LeftThumb")).ToArray();var authored=thumb.Select(t=>t.rotation).ToArray();
                    const string customPath="grip.Cutter.left.Thumb.Proximal";float previous=view.Tuning.Get(customPath);
                    view.Tuning.Set(customPath,previous+1);
                    typeof(TrooperVisual).GetMethod("FitFirstPersonSeamThumb",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(view,null);
                    for(int i=0;i<thumb.Length;i++)Assert.That(Quaternion.Angle(authored[i],thumb[i].rotation),Is.LessThan(.001f),"Explicit custom thumb controls must remain authoritative");
                    view.Tuning.Set(customPath,previous);
                }
                var pose=bones.Select(t=>(t.position,t.rotation,t.localPosition)).ToArray();ground.SendMessage("LateUpdate");ground.SendMessage("LateUpdate");
                for(int i=0;i<bones.Length;i++){Assert.That(Vector3.Distance(pose[i].position,bones[i].position),Is.LessThan(.00001f),weapon+" frozen "+bones[i].name);Assert.That(Quaternion.Angle(pose[i].rotation,bones[i].rotation),Is.LessThan(.05f));Assert.That(Vector3.Distance(pose[i].localPosition,bones[i].localPosition),Is.LessThan(.00001f));}
                File.WriteAllLines(Path.Combine(output,weapon+"-bones.tsv"),bones.Select(t=>t.name+"\t"+camera.transform.InverseTransformPoint(t.position).ToString("F6")));
                if(weapon==WeaponId.RocketLauncher)
                {
                    var skin=view.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name=="vector-armored-hands");var mesh=new Mesh();skin.BakeMesh(mesh);
                    var lines=new List<string>{"material,index,x,y,z"};var vertices=mesh.vertices;
                    var cloth=new List<int>();var pads=new HashSet<int>();var mounts=new List<List<int>>();var weights=skin.sharedMesh.boneWeights;
                    for(int sub=0;sub<mesh.subMeshCount;sub++)
                    {
                        string material=skin.sharedMaterials[sub].name;
                        if(material=="orbital-restored-material"||material=="orbital-restored-vector-glove")cloth.AddRange(mesh.GetIndices(sub));
                        if(material=="vector-reference-edge-metal")
                        {
                            var indices=mesh.GetIndices(sub);var adjacent=new Dictionary<int,HashSet<int>>();
                            foreach(int i in indices)if(!adjacent.ContainsKey(i))adjacent[i]=new HashSet<int>();
                            for(int t=0;t<indices.Length;t+=3)for(int v=0;v<3;v++)for(int n=0;n<3;n++)adjacent[indices[t+v]].Add(indices[t+n]);
                            var remaining=new HashSet<int>(adjacent.Keys);
                            while(remaining.Count>0)
                            {
                                var part=new List<int>();var pending=new Stack<int>();int first=remaining.First();remaining.Remove(first);pending.Push(first);
                                while(pending.Count>0){int i=pending.Pop();part.Add(i);foreach(int j in adjacent[i])if(remaining.Remove(j))pending.Push(j);}
                                // Wrist panels are closed 140-vertex islands. Some edge
                                // vertices legitimately blend into the forearm skin.
                                if(part.Count==140&&part.Count(i=>skin.bones[weights[i].boneIndex0].name.EndsWith("Hand"))>part.Count/2)
                                {mounts.Add(part);foreach(int i in part)pads.Add(i);}
                            }
                        }
                    }
                    Assert.That(pads.Count,Is.EqualTo(560),"Both paired wrist mounting frames must remain complete");
                    Assert.That(mounts.Count,Is.EqualTo(4));float gap=0;int mountedVertices=0;
                    var bind=skin.sharedMesh.vertices;
                    foreach(var mount in mounts)
                    {
                        int attached=0;
                        foreach(int i in mount)
                        {
                            float bindGap=float.PositiveInfinity;
                            for(int t=0;t<cloth.Count;t+=3)bindGap=Mathf.Min(bindGap,(bind[i]-ClosestPoint(bind[i],bind[cloth[t]],bind[cloth[t+1]],bind[cloth[t+2]])).sqrMagnitude);
                            if(bindGap>=.0015f*.0015f)continue;
                            attached++;float posedGap=float.PositiveInfinity;
                            for(int t=0;t<cloth.Count;t+=3)posedGap=Mathf.Min(posedGap,(vertices[i]-ClosestPoint(vertices[i],vertices[cloth[t]],vertices[cloth[t+1]],vertices[cloth[t+2]])).sqrMagnitude);
                            gap=Mathf.Max(gap,Mathf.Sqrt(posedGap));
                            Assert.That(Mathf.Sqrt(posedGap),Is.LessThan(.0015f),"Every authored mounting contact must remain on the posed glove");
                        }
                        Assert.That(attached,Is.GreaterThan(0),"Each of the four closed wrist frames must have a textile attachment");mountedVertices+=attached;
                    }
                    // The paired source frames have different back-face tessellation;
                    // validate all 96 real underside contacts rather than assuming 24 per frame.
                    Assert.That(mountedVertices,Is.EqualTo(96));
                    File.WriteAllText(Path.Combine(output,"wrist-clearance.txt"),gap.ToString("R",System.Globalization.CultureInfo.InvariantCulture));
                    for(int s=0;s<mesh.subMeshCount;s++)foreach(var i in mesh.GetIndices(s).Distinct()){var p=camera.transform.InverseTransformPoint(skin.transform.TransformPoint(vertices[i]));lines.Add(FormattableString.Invariant($"{skin.sharedMaterials[s].name},{i},{p.x:R},{p.y:R},{p.z:R}"));}
                    File.WriteAllLines(Path.Combine(output,"crimson-skin.csv"),lines);UnityEngine.Object.Destroy(mesh);
                }
            }
        }
        static Vector3 ClosestPoint(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
        {
            var ab=b-a;var ac=c-a;var ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);
            if(d1<=0&&d2<=0)return a;
            var bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0&&d4<=d3)return b;
            float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return a+ab*(d1/(d1-d3));
            var cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return c;
            float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return a+ac*(d2/(d2-d6));
            float va=d3*d6-d5*d4;if(va<=0&&d4-d3>=0&&d5-d6>=0)return b+(c-b)*((d4-d3)/((d4-d3)+(d5-d6)));
            float inverse=1/(va+vb+vc);return a+ab*(vb*inverse)+ac*(vc*inverse);
        }
        [UnityTearDown] public IEnumerator Cleanup(){UnityEditor.ShaderUtil.allowAsyncCompilation=previousAsync;if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);if(temporaryOutput!=null&&Directory.Exists(temporaryOutput))Directory.Delete(temporaryOutput,true);}
    }
}
#endif
