using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class ArenaPickupCircleCoverageTests
    {
        Scene scene;GameObject cameraGo;RenderTexture rt;
        [UnityTearDown] public IEnumerator Cleanup(){if(cameraGo){cameraGo.GetComponent<Camera>().targetTexture=null;Object.Destroy(cameraGo);}if(rt)Object.Destroy(rt);if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);}
        [UnityTest] public IEnumerator TunnelsAllSevenAnchors()=>Verify(IndustrialTunnelsCatalog.Id,7);
        [UnityTest] public IEnumerator LunarAllThirteenAnchors()=>Verify(LunarLaboratoryCatalog.Id,13);
        [UnityTest] public IEnumerator CombatBowlAllTwelveAnchors()=>Verify(CombatBowlCatalog.Id,12);
        [UnityTest] public IEnumerator WellAllThirteenAnchors()=>Verify(WellArenaCatalog.Id,13);
        IEnumerator Verify(string id,int count)
        {
            bool had=PlayerPrefs.HasKey(ProvingGround.LastPlayedMapPreferenceKey);string prior=PlayerPrefs.GetString(ProvingGround.LastPlayedMapPreferenceKey);
            try
            {
                yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");
                var ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();ground.SendMessage("OpenSetup");ground.SelectAuthoredMap(id);
                var composition=new NativeMatchComposition(NativeMatchRoster.Ffa(2),Enumerable.Range(0,2).Select(i=>new NativeParticipantInfo(NativeParticipantKind.Bot,"Circle bot "+i,NativeParticipantColors.For(i),(int)NativeBotDifficulty.Normal)).ToArray(),new[]{0});
                ground.StartBotReview(composition,7219);yield return NativeLoadingTestScene.Wait(ground);ground.OpenPauseReviewSeat(0);ground.SendMessage("LateUpdate");yield return null;
                var arena=ground.CameraReviewArena;var definition=arena.Definition;var canonical=AuthoredArenaCatalog.Resolve(id);
                Assert.That(JsonUtility.ToJson(definition),Is.EqualTo(JsonUtility.ToJson(canonical)),"Exact map/positions/spawns/gameplay preserved");Assert.That(definition.Pickups.Length,Is.EqualTo(count));
                var meshes=arena.GetComponentsInChildren<MeshFilter>();bool well=id==WellArenaCatalog.Id,bowl=id==CombatBowlCatalog.Id,lunar=id==LunarLaboratoryCatalog.Id;
                var settings=well?ProvingProfile.CreateWellDefault():lunar?ProvingProfile.CreateLunarPresentation():id==IndustrialTunnelsCatalog.Id?ProvingProfile.CreateIndustrialTunnelsPresentation():ProvingProfile.CreateCombatBowlRingPresentationDefault();
                string prefix=well?"well.":lunar?"lunar.":"tunnels.";float radius=bowl?settings.Get("detail.ringRadius"):settings.Get(prefix+"pickupRingRadius"),width=bowl?settings.Get("detail.lineWidth"):settings.Get(prefix+"pickupRingWidth");
                var whitePickups=definition.Pickups.Where(item=>id!=IndustrialTunnelsCatalog.Id||item.Id!="tunnels-damage-central").ToArray();
                Assert.That(radius,Is.EqualTo(1.35f),"Shared default radius across all four maps");
                MeshFilter paint=null;Vector3[] before=null;
                if(!bowl)
                {
                    paint=meshes.Single(m=>m.name==(well?"Well permanent pickup circles":ArenaPickupMarkings.Name));before=paint.sharedMesh.vertices;
                    Assert.That(paint.sharedMesh.vertexCount,Is.EqualTo(whitePickups.Length*128));Assert.That(paint.sharedMesh.triangles.Length,Is.EqualTo(whitePickups.Length*384));
                    Assert.That(paint.GetComponents<Collider>(),Is.Empty);Assert.That(paint.GetComponents<Light>(),Is.Empty);
                    var renderer=paint.GetComponent<MeshRenderer>();Assert.That(renderer.sharedMaterial.color,Is.EqualTo(Color.white));Assert.That(renderer.shadowCastingMode,Is.EqualTo(ShadowCastingMode.Off));Assert.That(renderer.receiveShadows,Is.False);
                    for(int i=0;i<whitePickups.Length;i++)foreach(var v in before.Skip(i*128).Take(128))
                    {var anchor=whitePickups[i].Anchor;float height=settings.Get(prefix+"pickupRingHeight")+(lunar&&anchor.y==0?.054f:0);Assert.That(v.y,Is.EqualTo(anchor.y+height).Within(.0001f));Assert.That(Vector2.Distance(new Vector2(v.x,v.z),new Vector2(anchor.x,anchor.z)),Is.EqualTo(radius).Within(width/2+.0001f));}
                }
                else
                {
                    // Read the retained source batch: GPU static-batch outputs deliberately discard CPU vertices.
                    // The same source feeds the rendered batch; all 12 anchors remain asserted below.
                    var white=meshes.Where(m=>m.sharedMesh.isReadable&&m.GetComponent<MeshRenderer>()?.sharedMaterial?.name=="Broad white luminaire").ToArray();
                    Assert.That(white,Is.Not.Empty,"Existing white markings batch");
                    var vertices=white.SelectMany(m=>m.sharedMesh.vertices.Select(v=>m.transform.TransformPoint(v))).ToArray();
                    foreach(var pickup in definition.Pickups)Assert.That(vertices.Count(v=>Mathf.Abs(v.y-pickup.Anchor.y-.012f)<.004f&&Mathf.Abs(Vector2.Distance(new Vector2(v.x,v.z),new Vector2(pickup.Anchor.x,pickup.Anchor.z))-radius)<width),Is.GreaterThanOrEqualTo(64),pickup.Id);
                }
                if(id==IndustrialTunnelsCatalog.Id)
                {
                    Assert.That(whitePickups.Length,Is.EqualTo(6),"All six other points, including FORGE, get white paint");
                    var central=definition.Pickups.Single(item=>item.Id=="tunnels-damage-central");
                    var sector=meshes.Single(m=>m.name=="Sector 5 muted paint");
                    Assert.That(sector.GetComponent<MeshRenderer>().sharedMaterial.color,Is.EqualTo(new Color(.69f,.65f,.52f)*settings.Get("tunnels.wayfinding.paint")));
                    float yellowRadius=settings.Get("tunnels.wayfinding.ringRadius");
                    Assert.That(sector.sharedMesh.vertices.Count(v=>Mathf.Abs(v.y-central.Anchor.y-.008f)<.004f&&Mathf.Abs(Vector2.Distance(new Vector2(v.x,v.z),new Vector2(central.Anchor.x,central.Anchor.z))-yellowRadius)<.13f),Is.GreaterThanOrEqualTo(64),"Existing yellow circle stays at the central anchor");
                }
                cameraGo=new GameObject("Pickup circle review");var camera=cameraGo.AddComponent<Camera>();camera.fieldOfView=70;camera.nearClipPlane=.05f;rt=new RenderTexture(960,720,24,RenderTextureFormat.DefaultHDR);camera.targetTexture=rt;
                string directory=Path.Combine(Environment.GetEnvironmentVariable("STAR_TOURNAMENT_ALL_CIRCLES_OUTPUT")??Path.GetFullPath(Path.Combine(Application.dataPath,"../../docs/evidence/all-arena-pickup-circles/views")),id);Directory.CreateDirectory(directory);
                foreach(var pickup in definition.Pickups)
                {
                    var eyeOffset=new Vector3(0,2.7f,-3.1f);
                    if(id==IndustrialTunnelsCatalog.Id&&(pickup.Id=="tunnels-pulse-south"||pickup.Id=="forge-tunnels-north"))eyeOffset=new Vector3(3.1f,2.7f,0);
                    if(lunar&&pickup.Id=="H")eyeOffset=new Vector3(0,2.7f,3.1f);
                    cameraGo.transform.position=pickup.Anchor+eyeOffset;cameraGo.transform.LookAt(pickup.Anchor+Vector3.up*.15f);camera.Render();yield return null;
                    var previous=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                    try{image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(directory,pickup.Id+".png"),image.EncodeToPNG());}finally{RenderTexture.active=previous;Object.Destroy(image);}
                }
                var weapon=ground.Session.WeaponPickups.First();ground.PlaceCombatReviewSeat(0,weapon.Anchor,0);ground.Session.Tick(new LocalAction[2],1/60f);ground.SendMessage("LateUpdate");yield return null;
                Assert.That(ground.Session.WeaponPickups.Single(p=>p.InstanceId==weapon.InstanceId).Available,Is.False,"Real weapon cooldown");
                if(paint){Assert.That(paint.gameObject.activeInHierarchy,Is.True);Assert.That(paint.sharedMesh.vertices,Is.EqualTo(before),"Paint remains at every weapon/bonus anchor during cooldown");}
            }
            finally{if(had)PlayerPrefs.SetString(ProvingGround.LastPlayedMapPreferenceKey,prior);else PlayerPrefs.DeleteKey(ProvingGround.LastPlayedMapPreferenceKey);}
        }
    }
}
