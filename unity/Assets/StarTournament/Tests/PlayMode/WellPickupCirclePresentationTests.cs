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
    public sealed class WellPickupCirclePresentationTests
    {
        Scene scene;GameObject cameraGo;RenderTexture rt;
        [UnityTearDown] public IEnumerator Cleanup()
        {if(cameraGo){cameraGo.GetComponent<Camera>().targetTexture=null;Object.Destroy(cameraGo);}if(rt)Object.Destroy(rt);if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);}
        [UnityTest] public IEnumerator EveryPickupHasPermanentWhitePaintAndRealMatchViews()
        {
            bool had=PlayerPrefs.HasKey(ProvingGround.LastPlayedMapPreferenceKey);string prior=PlayerPrefs.GetString(ProvingGround.LastPlayedMapPreferenceKey);
            try
            {
                yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");
                var ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();ground.SendMessage("OpenSetup");ground.SelectAuthoredMap(WellArenaCatalog.Id);
                var composition=new NativeMatchComposition(NativeMatchRoster.Ffa(2),Enumerable.Range(0,2).Select(i=>new NativeParticipantInfo(NativeParticipantKind.Bot,"Well bot "+i,NativeParticipantColors.For(i),(int)NativeBotDifficulty.Normal)).ToArray(),new[]{0});
                ground.StartBotReview(composition,7219);yield return NativeLoadingTestScene.Wait(ground);ground.OpenPauseReviewSeat(0);ground.SendMessage("LateUpdate");yield return null;
                var arena=ground.CameraReviewArena;var definition=arena.Definition;
                Assert.That(JsonUtility.ToJson(definition),Is.EqualTo(JsonUtility.ToJson(WellArenaCatalog.Build())),"Presentation preserves exact map @5");
                var skin=arena.GetComponentInChildren<WellArenaPresentation>();var rings=skin.GetComponentsInChildren<MeshFilter>().Single(m=>m.name=="Well permanent pickup circles");var mesh=rings.sharedMesh;
                Assert.That(definition.Pickups.Length,Is.EqualTo(11));Assert.That(mesh.vertexCount,Is.EqualTo(11*64*2));Assert.That(mesh.triangles.Length,Is.EqualTo(11*64*6));
                Assert.That(rings.GetComponentsInChildren<Collider>(),Is.Empty);Assert.That(rings.GetComponentsInChildren<Light>(),Is.Empty);
                var renderer=rings.GetComponent<MeshRenderer>();Assert.That(renderer.sharedMaterial.color,Is.EqualTo(Color.white));Assert.That(renderer.shadowCastingMode,Is.EqualTo(ShadowCastingMode.Off));Assert.That(renderer.receiveShadows,Is.False);
                Assert.That(skin.GetComponentsInChildren<Light>().Length,Is.EqualTo(24));Assert.That(skin.GetComponentsInChildren<Collider>(),Is.Empty);
                var settings=skin.Settings;var vertices=mesh.vertices;
                for(int i=0;i<definition.Pickups.Length;i++)
                {
                    var anchor=definition.Pickups[i].Anchor;
                    foreach(var v in vertices.Skip(i*128).Take(128))
                    {Assert.That(v.y,Is.EqualTo(anchor.y+settings.Get("well.pickupRingHeight")).Within(.0001f));Assert.That(Vector2.Distance(new Vector2(v.x,v.z),new Vector2(anchor.x,anchor.z)),Is.EqualTo(settings.Get("well.pickupRingRadius")).Within(settings.Get("well.pickupRingWidth")/2+.0001f));}
                }
                cameraGo=new GameObject("Well circle review");var camera=cameraGo.AddComponent<Camera>();camera.fieldOfView=75;camera.nearClipPlane=.05f;rt=new RenderTexture(1280,720,24,RenderTextureFormat.DefaultHDR);camera.targetTexture=rt;
                var directory=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_WELL_CIRCLES_OUTPUT")??Path.GetFullPath(Path.Combine(Application.dataPath,"../../docs/evidence/well-pickup-circles/views"));Directory.CreateDirectory(directory);
                var ids=new[]{"shotgun-02","cutter-02","rocket-02","heal-H","damage-garden","armor-01"};
                var eyes=new[]{new Vector3(-14.5f,WellArenaCatalog.Storey+1.7f,-16.9f),new Vector3(14.5f,WellArenaCatalog.Storey+1.7f,16.9f),new Vector3(0,WellArenaCatalog.Storey+2.2f,5),new Vector3(0,2*WellArenaCatalog.Storey+2.2f,5),new Vector3(4,2.8f,-4),new Vector3(4,1.7f,18)};
                for(int i=0;i<ids.Length;i++)
                {
                    var pickup=definition.Pickups.Single(p=>p.Id==ids[i]);cameraGo.transform.position=eyes[i];cameraGo.transform.LookAt(pickup.Anchor+Vector3.up*.4f);camera.Render();yield return null;
                    var previous=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                    try{image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(directory,ids[i]+".png"),image.EncodeToPNG());}finally{RenderTexture.active=previous;Object.Destroy(image);}
                }
                var before=mesh.vertices;var weapon=ground.Session.WeaponPickups.Single(p=>p.InstanceId=="shotgun-02");ground.PlaceCombatReviewSeat(0,weapon.Anchor,0);
                ground.Session.Tick(new LocalAction[2],1/60f);ground.SendMessage("LateUpdate");yield return null;
                Assert.That(ground.Session.WeaponPickups.Single(p=>p.InstanceId=="shotgun-02").Available,Is.False,"Real pickup enters cooldown");
                Assert.That(renderer.gameObject.activeInHierarchy,Is.True);Assert.That(mesh.vertices,Is.EqualTo(before),"Paint remains unchanged during cooldown");
            }
            finally{if(had)PlayerPrefs.SetString(ProvingGround.LastPlayedMapPreferenceKey,prior);else PlayerPrefs.DeleteKey(ProvingGround.LastPlayedMapPreferenceKey);}
        }
    }
}
