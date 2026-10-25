using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class ForgePlacementVisualTests
    {
        Scene scene;GameObject cameraGo;
        static string Output=>Environment.GetEnvironmentVariable("STAR_TOURNAMENT_FORGE_EVIDENCE");
        [UnityTearDown]public IEnumerator Cleanup(){if(cameraGo)Object.Destroy(cameraGo);if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);}
        static void Capture(Camera camera,string file)
        {
            var rt=new RenderTexture(1600,900,24,RenderTextureFormat.DefaultHDR);var active=RenderTexture.active;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Output,file),image.EncodeToPNG());}
            finally{camera.targetTexture=null;RenderTexture.active=active;Object.Destroy(image);Object.Destroy(rt);}
        }
        static Vector3 Eye(string map,Vector3 anchor)=>anchor+(map==WellArenaCatalog.Id?new Vector3(-Mathf.Sign(anchor.x)*2.6f,2,0):map==LunarLaboratoryCatalog.Id?new Vector3(Mathf.Sign(anchor.x)*3.2f,2.3f,2.5f):map==IndustrialTunnelsCatalog.Id?new Vector3(3.2f,2.2f,0):new Vector3(0,2.4f,-3.2f));
        [UnityTest]public IEnumerator StaticAssetOptimizationComparison()
        {
            Assert.That(Output,Is.Not.Null.And.Not.Empty);Directory.CreateDirectory(Output);var metrics=new List<string>();
            foreach(string map in new[]{CombatBowlCatalog.Id,IndustrialTunnelsCatalog.Id,LunarLaboratoryCatalog.Id,WellArenaCatalog.Id})
            {
                var current=AuthoredArenaCatalog.Resolve(map);string previous=map==CombatBowlCatalog.Id?"19":map==LunarLaboratoryCatalog.Id?"8":map==WellArenaCatalog.Id?"6":IndustrialTunnelsCatalog.Revision;
                var anchor=current.Pickups.First(p=>p.Kind==ArenaPickupKind.Plasma).Anchor;
                foreach(bool before in new[]{true,false})
                {
                    scene=SceneManager.CreateScene("Forge optimization "+map,new CreateSceneParameters(LocalPhysicsMode.Physics3D));
                    var owner=new GameObject("Static authored comparison");SceneManager.MoveGameObjectToScene(owner,scene);
                    var move=ProvingProfile.CreateDefault();var arena=owner.AddComponent<ProvingArena>();arena.Build(ArenaFreezeSnapshot.Create(before?AuthoredArenaCatalog.Resolve(map,previous):current,move),move);
                    cameraGo=new GameObject("Static comparison camera");SceneManager.MoveGameObjectToScene(cameraGo,scene);var camera=cameraGo.AddComponent<Camera>();camera.fieldOfView=70;camera.nearClipPlane=.05f;camera.transform.position=Eye(map,anchor);camera.transform.LookAt(anchor+Vector3.up*.5f);
                    for(int warm=0;warm<3;warm++){camera.Render();yield return null;}
                    var renderers=arena.GetComponentsInChildren<Renderer>().Where(x=>x.enabled).ToArray();var meshes=arena.GetComponentsInChildren<MeshFilter>().Where(m=>m.GetComponent<Renderer>()?.enabled==true).Select(m=>m.sharedMesh).Distinct().ToArray();
                    metrics.Add(map+" "+(before?"before":"after")+" renderers="+renderers.Length+" materials="+renderers.SelectMany(x=>x.sharedMaterials).Distinct().Count()+" triangles="+meshes.Sum(m=>Enumerable.Range(0,m.subMeshCount).Sum(i=>(long)m.GetIndexCount(i)/3))+" lights="+arena.GetComponentsInChildren<Light>().Length+" colliders="+arena.GetComponentsInChildren<Collider>().Length);
                    Capture(camera,map+"-static-"+(before?"before":"after")+".png");Object.Destroy(cameraGo);yield return SceneManager.UnloadSceneAsync(scene);
                }
            }
            File.WriteAllLines(Path.Combine(Output,"asset-metrics.txt"),metrics);
        }
        [UnityTest]public IEnumerator AllSixForgeriesHavePermanentCirclesAndPhysicalPickupViews()
        {
            Assert.That(Output,Is.Not.Null.And.Not.Empty);Directory.CreateDirectory(Output);
            bool had=PlayerPrefs.HasKey(ProvingGround.LastPlayedMapPreferenceKey);string prior=PlayerPrefs.GetString(ProvingGround.LastPlayedMapPreferenceKey);
            var records=new List<string>();
            try
            {
                foreach(string map in new[]{CombatBowlCatalog.Id,IndustrialTunnelsCatalog.Id,LunarLaboratoryCatalog.Id,WellArenaCatalog.Id})
                {
                    yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");
                    var ground=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ProvingGround>()).Single();ground.SendMessage("OpenSetup");ground.SelectAuthoredMap(map);
                    var composition=new NativeMatchComposition(NativeMatchRoster.Ffa(2),Enumerable.Range(0,2).Select(i=>new NativeParticipantInfo(NativeParticipantKind.Bot,"Forge fixture "+i,NativeParticipantColors.For(i),(int)NativeBotDifficulty.Normal)).ToArray(),new[]{0});
                    ground.StartBotReview(composition,7219);yield return NativeLoadingTestScene.Wait(ground);ground.OpenPauseReviewSeat(0);ground.enabled=false;ground.SendMessage("LateUpdate");yield return null;
                    var arena=ground.CameraReviewArena;var definition=arena.Definition;Physics.SyncTransforms();
                    cameraGo=new GameObject("FORGE placement observer");var camera=cameraGo.AddComponent<Camera>();camera.fieldOfView=70;camera.nearClipPlane=.05f;
                    foreach(var anchor in definition.Pickups.Where(p=>p.Kind==ArenaPickupKind.Plasma))
                    {
                        // Sample the full white ring footprint against actual physics: no wall/void/ramp.
                        var settings=map==WellArenaCatalog.Id?ProvingProfile.CreateWellDefault():map==LunarLaboratoryCatalog.Id?ProvingProfile.CreateLunarPresentation():map==IndustrialTunnelsCatalog.Id?ProvingProfile.CreateIndustrialTunnelsPresentation():ProvingProfile.CreateCombatBowlRingPresentationDefault();
                        string prefix=map==WellArenaCatalog.Id?"well.":map==LunarLaboratoryCatalog.Id?"lunar.":"tunnels.";
                        float radius=map==CombatBowlCatalog.Id?settings.Get("detail.ringRadius")+settings.Get("detail.lineWidth")/2:settings.Get(prefix+"pickupRingRadius")+settings.Get(prefix+"pickupRingWidth")/2;
                        for(int i=0;i<64;i++)
                        {
                            var p=anchor.Anchor+new Vector3(Mathf.Sin(i*Mathf.PI/32)*radius,.15f,Mathf.Cos(i*Mathf.PI/32)*radius);
                            Assert.That(Physics.Raycast(p,Vector3.down,out var hit,.3f,(1<<ProvingArena.WorldLayer)|(1<<ProvingArena.MovementOnlyLayer)),Is.True,anchor.Id+" ring support "+i);
                            Assert.That(hit.normal.y,Is.GreaterThan(.999f));Assert.That(hit.point.y,Is.EqualTo(anchor.Anchor.y).Within(.06f));
                        }
                        var meshes=arena.GetComponentsInChildren<MeshFilter>();
                        var paint=map==CombatBowlCatalog.Id?meshes.Where(m=>m.sharedMesh.isReadable&&m.GetComponent<MeshRenderer>()?.sharedMaterial?.name=="Broad white luminaire").ToArray():meshes.Where(m=>m.name==(map==WellArenaCatalog.Id?"Well permanent pickup circles":ArenaPickupMarkings.Name)).ToArray();
                        Assert.That(paint,Is.Not.Empty);var before=paint.Select(m=>m.sharedMesh.vertices).ToArray();
                        var visual=ground.GetComponentsInChildren<Transform>(true).Single(t=>t.name==anchor.Id+"-presentation");Assert.That(visual.gameObject.activeInHierarchy,Is.True);
                        camera.transform.position=Eye(map,anchor.Anchor);camera.transform.LookAt(anchor.Anchor+Vector3.up*.5f);for(int warm=0;warm<3;warm++){camera.Render();yield return null;}
                        Capture(camera,map+"-"+anchor.Id+"-available.png");
                        var state=ground.Session.Capture();state.Lives[0].PlasmaOwned=false;state.Lives[0].PlasmaAmmo=0;ground.Session.Restore(state);
                        ground.PlaceCombatReviewSeat(0,anchor.Anchor,0);ground.Session.Tick(new LocalAction[2],.02f);ground.SendMessage("LateUpdate");yield return null;
                        Assert.That(ground.Session.Life(0).PlasmaOwned,Is.True,anchor.Id+" real pickup");Assert.That(ground.Session.WeaponPickups.Single(p=>p.InstanceId==anchor.Id).Available,Is.False);
                        Assert.That(visual.gameObject.activeInHierarchy,Is.False);
                        for(int i=0;i<paint.Length;i++){Assert.That(paint[i].gameObject.activeInHierarchy,Is.True);Assert.That(paint[i].sharedMesh.vertices,Is.EqualTo(before[i]));}
                        ground.PlaceCombatReviewSeat(0,definition.Spawns[0],0);ground.SendMessage("LateUpdate");yield return null;
                        Capture(camera,map+"-"+anchor.Id+"-collected.png");records.Add(map+" "+anchor.Id+" "+anchor.Anchor+" support="+anchor.Support+" pickup=PASS ring=PASS");
                    }
                    Object.Destroy(cameraGo);yield return SceneManager.UnloadSceneAsync(scene);
                }
                Assert.That(records.Count,Is.EqualTo(6));File.WriteAllLines(Path.Combine(Output,"contacts.txt"),records);
            }
            finally{if(had)PlayerPrefs.SetString(ProvingGround.LastPlayedMapPreferenceKey,prior);else PlayerPrefs.DeleteKey(ProvingGround.LastPlayedMapPreferenceKey);}
        }
    }
}
