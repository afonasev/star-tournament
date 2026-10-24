#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    /// <summary>Opt-in same-pose mesh comparison. Frozen render diagnostics, not Windows or human acceptance.</summary>
    public sealed class VectorMeshOptimizationReviewTests
    {
        Scene scene;ProvingGround ground;GameObject studio,fill;Camera camera;
        sealed class SkinState
        {
            public SkinnedMeshRenderer Renderer;public Mesh Mesh;public Transform[] Bones;public Transform Root;
            public Material[] Materials;public MaterialPropertyBlock[] Blocks;public Bounds Bounds;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {if(studio)Object.Destroy(studio);if(fill)Object.Destroy(fill);if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);}
        static void Render(Camera source,RenderTexture target)
        {
            if(GraphicsSettings.currentRenderPipeline!=null)RenderPipeline.SubmitRenderRequest(source,new RenderPipeline.StandardRequest{destination=target});else source.Render();
        }
        static void Capture(Camera source,string path)
        {
            var target=new RenderTexture(1200,1350,24);target.Create();var image=new Texture2D(1200,1350,TextureFormat.RGB24,false);
            var old=source.targetTexture;var active=RenderTexture.active;var rect=source.rect;
            try
            {
                source.rect=new Rect(0,0,1,1);source.targetTexture=target;Render(source,target);RenderTexture.active=target;
                image.ReadPixels(new Rect(0,0,1200,1350),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
            }
            finally{source.rect=rect;source.targetTexture=old;RenderTexture.active=active;target.Release();Object.Destroy(target);Object.Destroy(image);}
        }
        void UseVariant(string variant,SkinState[] states)
        {
            if(variant=="baseline")
            {
                foreach(var s in states){s.Renderer.sharedMesh=s.Mesh;s.Renderer.bones=s.Bones;s.Renderer.rootBone=s.Root;s.Renderer.sharedMaterials=s.Materials;s.Renderer.localBounds=s.Bounds;for(int i=0;i<s.Blocks.Length;i++)s.Renderer.SetPropertyBlock(s.Blocks[i].isEmpty?null:s.Blocks[i],i);}
                return;
            }
            var world=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/StarTournament/Art/RuntimeMeshReview/"+variant+"-body.glb");
            var view=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/StarTournament/Art/RuntimeMeshReview/"+variant+"-arms.glb");
            Assert.That(world&&view,Is.True,"Generate both runtime candidates before this explicit review");
            foreach(var state in states)
            {
                var target=state.Renderer;var owner=target.GetComponentInParent<TrooperVisual>();
                var source=(owner.name=="trooper-view"?view:world).GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Single(s=>s.name==(target.name=="own-shadow-proxy"?target.transform.parent.name:target.name));
                var bones=owner.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
                target.sharedMesh=source.sharedMesh;target.bones=source.bones.Select(b=>bones[b.name]).ToArray();
                target.rootBone=source.rootBone?bones[source.rootBone.name]:null;
                var materials=state.Materials.Where(m=>m).GroupBy(m=>m.name).ToDictionary(g=>g.Key,g=>g.First());
                target.sharedMaterials=source.sharedMaterials.Select(m=>
                {
                    if(materials.TryGetValue(m.name,out var found))return found;
                    int dot=m.name.LastIndexOf('.');
                    if(dot>=0&&int.TryParse(m.name.Substring(dot+1),out _)&&materials.TryGetValue(m.name.Substring(0,dot),out found))return found;
                    throw new InvalidOperationException("Candidate changed material role: "+m.name+" on "+target.name);
                }).ToArray();
                // Identity blocks are indexed by material slot, not by mesh vertex.
                // Candidate export can reorder slots; carry each role's original block.
                for(int slot=0;slot<target.sharedMaterials.Length;slot++)
                {
                    int oldSlot=Array.IndexOf(state.Materials,target.sharedMaterials[slot]);
                    Assert.That(oldSlot,Is.GreaterThanOrEqualTo(0));
                    target.SetPropertyBlock(state.Blocks[oldSlot].isEmpty?null:state.Blocks[oldSlot],slot);
                }
                target.localBounds=state.Bounds;
                Assert.That(target.sharedMesh.bindposes.Length,Is.EqualTo(target.bones.Length));
            }
        }
        [UnityTest, Explicit("Graphical exclusive baseline/conservative/balanced comparison; generated candidate imports required")]
        public IEnumerator CompareWorldAndFirstPersonMeshes()
        {
            string output=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_MESH_EVIDENCE");Assert.That(output,Is.Not.Null.And.Not.Empty);Directory.CreateDirectory(output);
            Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(GraphicsDeviceType.Null));
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<ProvingGround>()).Single();
            var roster=NativeMatchRoster.Ffa(8);
            var info=Enumerable.Range(0,8).Select(p=>new NativeParticipantInfo(p==0?NativeParticipantKind.LocalHuman:NativeParticipantKind.Bot,"Participant "+p,NativeParticipantColors.For(p),p==0?-1:p%3)).ToArray();
            ground.StartBotReview(new NativeMatchComposition(roster,info,new[]{0}),4193);ground.enabled=false;
            yield return NativeLoadingTestScene.Wait(ground);Assert.That(ground.Running,Is.True);ground.SendMessage("LateUpdate");yield return null;
            var state=ground.Session.Capture();
            var bodies=Enumerable.Range(1,7).Select(i=>ground.transform.Find("player-"+(i+1)+"/trooper-presentation").GetComponent<TrooperVisual>()).ToArray();
            var seat=ground.transform.Find("seat-camera-1").GetComponent<Camera>();
            var snapshots=ground.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(s=>new SkinState{Renderer=s,Mesh=s.sharedMesh,Bones=s.bones,Root=s.rootBone,Materials=s.sharedMaterials,Bounds=s.localBounds,
                Blocks=Enumerable.Range(0,s.sharedMaterials.Length).Select(slot=>{var block=new MaterialPropertyBlock();s.GetPropertyBlock(block,slot);return block;}).ToArray()}).ToArray();
            foreach(var body in bodies)foreach(var r in body.GetComponentsInChildren<Renderer>(true))r.gameObject.layer=30;
            studio=new GameObject("mesh-review-camera");camera=studio.AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=39;camera.nearClipPlane=.05f;camera.farClipPlane=100;
            camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.13f,.15f,.17f);
            fill=new GameObject("mesh-review-fill");var light=fill.AddComponent<Light>();light.type=LightType.Directional;light.intensity=.8f;light.cullingMask=1<<30;fill.transform.rotation=Quaternion.Euler(35,-30,0);
            var positions=bodies.Select(b=>b.transform.localPosition).ToArray();var rotations=bodies.Select(b=>b.transform.localRotation).ToArray();
            var skinRows=new List<string>{"variant,sample,seven_actor_cpu_skin_ms"};
            var rows=new List<string>{"variant,sample,seven_frozen_world_render_ms"};var stats=new List<string>{"variant,world_skin_triangles,view_skin_triangles,world_skin_vertices,view_skin_vertices"};
            bool shippingOnly=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_MESH_SHIPPING_ONLY")=="1";
            foreach(var variant in shippingOnly?new[]{"baseline"}:new[]{"baseline","conservative","balanced","baseline-repeat"})
            {
                for(int i=0;i<bodies.Length;i++){bodies[i].transform.localPosition=positions[i];bodies[i].transform.localRotation=rotations[i];bodies[i].ResetPose(ground.Session.Time);}
                ground.SendMessage("LateUpdate");
                UseVariant(variant=="baseline-repeat"?"baseline":variant,snapshots);yield return null;
                if(variant!="baseline-repeat")
                {
                    var path=Path.Combine(output,variant);Directory.CreateDirectory(path);
                    var actual=bodies[0].GetComponentsInChildren<SkinnedMeshRenderer>();var hands=ground.transform.Find("seat-camera-1/trooper-view").GetComponentsInChildren<SkinnedMeshRenderer>();
                    stats.Add($"{variant},{actual.Sum(s=>s.sharedMesh.triangles.Length/3)},{hands.Sum(s=>s.sharedMesh.triangles.Length/3)},{actual.Sum(s=>s.sharedMesh.vertexCount)},{hands.Sum(s=>s.sharedMesh.vertexCount)}");
                    foreach(var body in bodies.Skip(1))foreach(var r in body.GetComponentsInChildren<Renderer>())r.enabled=false;
                    var subject=bodies[0];var center=subject.transform.position+Vector3.up*.95f;
                    foreach(var angle in new[]{0,90,180,270})
                    {
                        var direction=Quaternion.AngleAxis(angle,Vector3.up)*subject.transform.forward;
                        camera.transform.position=center+direction*3.4f+Vector3.up*.15f;camera.transform.LookAt(center);
                        Capture(camera,Path.Combine(path,"world-"+angle+".png"));
                    }
                    foreach(var body in bodies)foreach(var r in body.GetComponentsInChildren<Renderer>())r.enabled=false;
                    foreach(WeaponId weapon in Enum.GetValues(typeof(WeaponId)))
                    {
                        var snapshot=ground.Session.Capture();var life=snapshot.Lives[0];life.ShotgunOwned=life.RocketOwned=life.CutterOwned=true;
                        life.SelectedWeapon=weapon;life.PendingWeapon=default;life.SwitchRemaining=0;life.Ammo=CombatLife.AmmoFor(life,weapon);
                        snapshot.Lives[0]=life;ground.Session.Restore(snapshot);ground.SendMessage("LateUpdate");yield return null;
                        Capture(seat,Path.Combine(path,"view-"+weapon+".png"));
                    }
                    ground.Session.Restore(state);ground.SendMessage("LateUpdate");
                }
                foreach(var body in bodies)foreach(var r in body.GetComponentsInChildren<Renderer>())r.enabled=true;
                if(!shippingOnly)
                {
                    // Fixed presentation arrangement isolates geometry/skin/render cost from AI,
                    // visibility, camera movement and the arena. Simulation is never advanced.
                    for(int i=0;i<bodies.Length;i++){bodies[i].transform.position=new Vector3((i-3)*.75f,0,(i%2)*.45f);bodies[i].transform.rotation=Quaternion.Euler(0,180,0);}
                    var bake=new Mesh();
                    try
                    {
                        for(int sample=0;sample<13;sample++)
                        {
                            var watch=Stopwatch.StartNew();
                            foreach(var body in bodies)foreach(var skin in body.GetComponentsInChildren<SkinnedMeshRenderer>())skin.BakeMesh(bake);
                            watch.Stop();if(sample>=3)skinRows.Add($"{variant},{sample},{watch.Elapsed.TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                        }
                    }
                    finally{Object.Destroy(bake);}
                    camera.fieldOfView=50;camera.transform.position=new Vector3(0,1.35f,-5.5f);camera.transform.LookAt(new Vector3(0,.95f,0));
                    var target=new RenderTexture(1920,1080,24);target.Create();var pixel=new Texture2D(1,1,TextureFormat.RGB24,false);var active=RenderTexture.active;
                    try
                    {
                        camera.targetTexture=target;
                        for(int i=0;i<35;i++)
                        {
                            var watch=Stopwatch.StartNew();Render(camera,target);RenderTexture.active=target;pixel.ReadPixels(new Rect(0,0,1,1),0,0);pixel.Apply();watch.Stop();
                            if(i>=10)rows.Add($"{variant},{i},{watch.Elapsed.TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}");yield return null;
                        }
                    }
                    finally{camera.targetTexture=null;RenderTexture.active=active;target.Release();Object.Destroy(target);Object.Destroy(pixel);}
                    Capture(camera,Path.Combine(output,variant+"-seven.png"));camera.fieldOfView=39;
                }
                // Reset the complete presentation root before the next comparison; LateUpdate
                // samples bones but deliberately does not own the child root transform.
                for(int i=0;i<bodies.Length;i++){bodies[i].transform.localPosition=positions[i];bodies[i].transform.localRotation=rotations[i];bodies[i].ResetPose(ground.Session.Time);}
                ground.SendMessage("LateUpdate");
                Assert.That(JsonUtility.ToJson(ground.Session.Capture()),Is.EqualTo(JsonUtility.ToJson(state)),"Mesh choice must not change authoritative state");
            }
            File.WriteAllLines(Path.Combine(output,"skin.csv"),skinRows);File.WriteAllLines(Path.Combine(output,"render.csv"),rows);File.WriteAllLines(Path.Combine(output,"meshes.csv"),stats);UseVariant("baseline",snapshots);
        }
    }
}
#endif
