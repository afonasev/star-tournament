using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class QuarryArenaMatchTests
    {
        Scene scene;
        static string DirectoryPath=>Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QUARRY_QA_DIR")??Path.GetFullPath(Path.Combine(Application.dataPath,"../../.local/quarry-prototype/views"));
        [UnityTearDown] public IEnumerator Cleanup(){if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);}
        static void Capture(Camera camera,string name)
        {
            var rt=new RenderTexture(1600,1000,24);var old=RenderTexture.active;var texture=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;
                var request=new RenderPipeline.StandardRequest{destination=rt};Assert.That(RenderPipeline.SupportsRenderRequest(camera,request),Is.True);RenderPipeline.SubmitRenderRequest(camera,request);RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(DirectoryPath,name+".png"),texture.EncodeToPNG());}
            finally{camera.targetTexture=null;RenderTexture.active=old;Object.DestroyImmediate(texture);Object.DestroyImmediate(rt);}
        }
        [UnityTest] public IEnumerator MenuEightBotsRepeatAndActualEditorViews()
        {
            QuarryWarmDetails.CaptureUngroupedReference=true;
            bool had=PlayerPrefs.HasKey(ProvingGround.LastPlayedMapPreferenceKey);string prior=PlayerPrefs.GetString(ProvingGround.LastPlayedMapPreferenceKey);Directory.CreateDirectory(DirectoryPath);
            try
            {
                yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");var ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();
                ground.SendMessage("OpenSetup");ground.SendMessage("OpenLobbyMaps");yield return null;
                var option=ground.GetComponentsInChildren<Button>().Single(b=>b.name=="lobby-map-"+QuarryArenaCatalog.Id);
                Assert.That(option.transform.Find("label").GetComponent<Text>().text,Is.EqualTo("Карьер — прототип"));
                yield return EditorUiCapture.Capture(ground,Path.Combine(DirectoryPath,"map-picker.png"),1920,1080);
                option.onClick.Invoke();yield return null;Assert.That(ground.SelectedMapId,Is.EqualTo(QuarryArenaCatalog.Id));
                var composition=new NativeMatchComposition(NativeMatchRoster.Ffa(8),Enumerable.Range(0,8).Select(i=>new NativeParticipantInfo(NativeParticipantKind.Bot,"Quarry bot "+i,NativeParticipantColors.For(i),(int)NativeBotDifficulty.Normal)).ToArray(),new[]{0});
                ground.StartBotReview(composition,1126);yield return NativeLoadingTestScene.Wait(ground);
                Assert.That(ground.Running,Is.True,ground.CombatReviewDiagnostic());Assert.That(ground.Session.ParticipantCount,Is.EqualTo(8));Assert.That(ground.Session.ArenaIdentity,Is.EqualTo(QuarryArenaCatalog.Identity));
                var before=ground.Session.Capture();float deadline=Time.realtimeSinceStartup+90;
                while(ground.Session.Time<12){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline),"Short bot match must progress");yield return null;}
                var played=ground.Session.Capture();int moved=Enumerable.Range(0,8).Count(i=>Vector3.Distance(before.Poses[i].Position,played.Poses[i].Position)>3);
                Assert.That(moved,Is.GreaterThanOrEqualTo(2),"Bots leave spawn pockets");File.WriteAllText(Path.Combine(DirectoryPath,"bot-match.json"),JsonUtility.ToJson(played,true));
                // Preserve the original 12s match witness, then let the configured first damage bonus appear naturally for the layout frames.
                double appearance=before.Time+before.DamagePickup.Remaining+.1;
                while(ground.Session.Time<appearance){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline),"First pickup appearance must remain within the original deadline");yield return null;}
                File.WriteAllText(Path.Combine(DirectoryPath,"pickup-view-state.json"),JsonUtility.ToJson(ground.Session.Capture(),true));
                yield return EditorUiCapture.Capture(ground,Path.Combine(DirectoryPath,"match-hud.png"),1920,1080);
                ground.OpenPauseReviewSeat(0);yield return null;
                var arena=ground.CameraReviewArena;File.WriteAllText(Path.Combine(DirectoryPath,"arena-definition.json"),JsonUtility.ToJson(arena.Definition,true));var nav=arena.Surface.navMeshData;var geometry=JsonUtility.ToJson(arena.Definition);
                var cameraGo=new GameObject("Quarry evidence camera");SceneManager.MoveGameObjectToScene(cameraGo,scene);var camera=cameraGo.AddComponent<Camera>();
                camera.fieldOfView=70;camera.nearClipPlane=.05f;camera.farClipPlane=250;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.48f,.67f,.8f);camera.cullingMask=(1<<0)|(1<<ProvingArena.WorldLayer)|(1<<ProvingArena.MovementOnlyLayer)|(1<<ProvingArena.ParticipantLayer);
                var chunks=arena.GetComponent<ArenaRenderChunks>();
                camera.fieldOfView=48;cameraGo.transform.position=new Vector3(58,67,-90);cameraGo.transform.LookAt(new Vector3(0,0,2));
                var warm=arena.GetComponentInChildren<QuarryWarmDetails>();Assert.That(warm,Is.Not.Null);
                chunks.UseChunks(false);warm.UseGrouped(false);Capture(camera,"overview-ungrouped-fittings");
                warm.UseGrouped(true);Capture(camera,"overview-before");
                var source=arena.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled).ToArray();long triangles=source.Where(r=>r.GetComponent<MeshFilter>()&&r.GetComponent<MeshFilter>().sharedMesh).Sum(r=>(long)r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3);
                chunks.UseChunks(true);Capture(camera,"overview");
                var active=arena.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled).ToArray();
                File.WriteAllText(Path.Combine(DirectoryPath,"asset-optimization.json"),JsonUtility.ToJson(new Optimization{solids=arena.Definition.Solids.Length,sourceRenderers=source.Length,activeRenderers=active.Length,staticBatched=active.Count(r=>r.isPartOfStaticBatch),materials=active.SelectMany(r=>r.sharedMaterials).Distinct().Count(),sourceTriangles=triangles,unreferencedMeshes=ArenaMeshResidency.ReadUnreferencedOwnedMeshes(arena.transform).Length},true));
                Assert.That(warm.GetComponentsInChildren<Collider>().Length,Is.Zero,"Art cannot add collision");
                var textures=active.SelectMany(r=>r.sharedMaterials).Distinct().Select(m=>m.HasProperty("_BaseMap")?m.GetTexture("_BaseMap"):null).Where(t=>t&&t.name!="Font Texture").Distinct().ToArray();
                Assert.That(active.SelectMany(r=>r.sharedMaterials).Where(m=>m.name.StartsWith("Quarry warm A")).All(m=>m.shader.isSupported),Is.True);
                File.WriteAllText(Path.Combine(DirectoryPath,"warm-art-costs.json"),JsonUtility.ToJson(new WarmCosts{identity=QuarryWarmArt.Identity,textureCount=textures.Length,textureBytes=textures.Sum(t=>UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t)),uncombinedFittingParts=warm.UncombinedPartCount,fittingRenderers=warm.GroupedRendererCount,colliders=arena.GetComponentsInChildren<Collider>().Length,localLights=arena.GetComponentsInChildren<Light>().Length,localShadowLights=arena.GetComponentsInChildren<Light>().Count(l=>l.shadows!=LightShadows.None)},true));
                camera.fieldOfView=70;
                var eyes=new[]{new Vector3(38,1.7f,-32),new Vector3(38,5.7f,-5),new Vector3(-6,1.7f,-23),new Vector3(19,1.7f,-7),new Vector3(-6,1.7f,-7),new Vector3(10,9.7f,22),new Vector3(-13,1.7f,-32),new Vector3(0,5.7f,-1),new Vector3(16,9.7f,23),new Vector3(-19,1.7f,-32)};
                var targets=new[]{new Vector3(38,7,21),new Vector3(38,9,24),new Vector3(-6,2,-10),new Vector3(14,2,4),new Vector3(-6,1.7f,4),new Vector3(0,2,-22),new Vector3(-24,6,14),new Vector3(0,6,20),new Vector3(24,10.5f,36),new Vector3(-11,2,-23)};
                var names=new[]{"climb-from-bottom","climb-from-middle","tunnel-front","tunnel-east","tunnel-inside","upper-view","three-levels-from-court","center-roof","workshop","loader"};
                for(int i=0;i<eyes.Length;i++)
                {
                    cameraGo.transform.position=eyes[i];cameraGo.transform.LookAt(targets[i]);
                    if(names[i]=="loader"||names[i]=="workshop")
                    {
                        chunks.UseChunks(false);warm.UseGrouped(false);Capture(camera,names[i]+"-ungrouped");
                        warm.UseGrouped(true);chunks.UseChunks(true);
                    }
                    Capture(camera,names[i]);
                }
                camera.fieldOfView=65;
                cameraGo.transform.position=new Vector3(-30,1.7f,-35);cameraGo.transform.LookAt(new Vector3(-20,1.8f,-25));Capture(camera,"open-lower-cover");
                cameraGo.transform.position=new Vector3(-31,5.7f,9);cameraGo.transform.LookAt(new Vector3(-21,5.5f,1));Capture(camera,"open-middle-cover");
                cameraGo.transform.position=new Vector3(-7,9.7f,23);cameraGo.transform.LookAt(new Vector3(-22,9.6f,29));Capture(camera,"open-upper-cover");
                cameraGo.transform.position=new Vector3(-6,1.7f,4);cameraGo.transform.LookAt(new Vector3(13,1.7f,4));Capture(camera,"s3-tunnel-exit");
                cameraGo.transform.position=new Vector3(6,5.7f,-7);cameraGo.transform.LookAt(new Vector3(0,4.2f,0));Capture(camera,"center-damage-point");
                cameraGo.transform.position=new Vector3(-35,1.7f,-33);cameraGo.transform.LookAt(new Vector3(-41,1,-35));Capture(camera,"armor-south-west");
                cameraGo.transform.position=new Vector3(34,9.7f,30);cameraGo.transform.LookAt(new Vector3(41,9,35));Capture(camera,"armor-north-east");
                camera.orthographic=true;camera.orthographicSize=49;cameraGo.transform.position=new Vector3(0,95,1);cameraGo.transform.rotation=Quaternion.Euler(90,0,0);Capture(camera,"top-down");
                Object.Destroy(cameraGo);
                ground.SendMessage("Repeat");yield return NativeLoadingTestScene.Wait(ground);
                Assert.That(ground.Session.ArenaIdentity,Is.EqualTo(QuarryArenaCatalog.Identity));Assert.That(JsonUtility.ToJson(ground.CameraReviewArena.Definition),Is.EqualTo(geometry));Assert.That(ground.CameraReviewArena.Surface.navMeshData,Is.SameAs(nav));Assert.That(ground.Session.Time,Is.LessThan(1));
                ground.OpenPauseReviewSeat(0);
            }
            finally{QuarryWarmDetails.CaptureUngroupedReference=false;if(had)PlayerPrefs.SetString(ProvingGround.LastPlayedMapPreferenceKey,prior);else PlayerPrefs.DeleteKey(ProvingGround.LastPlayedMapPreferenceKey);}
        }
        [UnityTest] public IEnumerator MapPreviewShowsFinalArtwork()
        {
            Directory.CreateDirectory(DirectoryPath);yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");
            var ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();ground.SendMessage("OpenSetup");ground.SendMessage("OpenLobbyMaps");yield return null;
            var option=ground.GetComponentsInChildren<Button>().Single(b=>b.name=="lobby-map-"+QuarryArenaCatalog.Id);
            Assert.That(option.transform.Find("label").GetComponent<Text>().text,Is.EqualTo("Карьер — прототип"));
            var preview=option.GetComponentInChildren<ArenaPreviewGraphic>();Assert.That(preview.texture,Is.SameAs(Resources.Load<Texture2D>("MapMenuArt/"+QuarryArenaCatalog.Id)));
            yield return EditorUiCapture.Capture(ground,Path.Combine(DirectoryPath,"map-picker-final.png"),1920,1080);
        }
        [Serializable] sealed class WarmCosts {public string identity;public int textureCount,uncombinedFittingParts,fittingRenderers,colliders,localLights,localShadowLights;public long textureBytes;}
        [Serializable] sealed class Optimization {public int solids,sourceRenderers,activeRenderers,staticBatched,materials,unreferencedMeshes;public long sourceTriangles;}
    }
}
