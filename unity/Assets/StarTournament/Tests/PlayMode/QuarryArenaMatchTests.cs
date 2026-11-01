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
                yield return EditorUiCapture.Capture(ground,Path.Combine(DirectoryPath,"match-hud.png"),1920,1080);
                ground.OpenPauseReviewSeat(0);yield return null;
                var arena=ground.CameraReviewArena;var nav=arena.Surface.navMeshData;var geometry=JsonUtility.ToJson(arena.Definition);
                var cameraGo=new GameObject("Quarry evidence camera");SceneManager.MoveGameObjectToScene(cameraGo,scene);var camera=cameraGo.AddComponent<Camera>();
                camera.fieldOfView=70;camera.nearClipPlane=.05f;camera.farClipPlane=250;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.48f,.67f,.8f);camera.cullingMask=(1<<0)|(1<<ProvingArena.WorldLayer)|(1<<ProvingArena.MovementOnlyLayer)|(1<<ProvingArena.ParticipantLayer);
                var chunks=arena.GetComponent<ArenaRenderChunks>();
                camera.fieldOfView=50;cameraGo.transform.position=new Vector3(0,75,-75);cameraGo.transform.LookAt(new Vector3(0,0,2));
                chunks.UseChunks(false);Capture(camera,"overview-before");
                var source=arena.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled).ToArray();long triangles=source.Sum(r=>(long)r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3);
                chunks.UseChunks(true);Capture(camera,"overview");
                var active=arena.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled).ToArray();
                File.WriteAllText(Path.Combine(DirectoryPath,"asset-optimization.json"),JsonUtility.ToJson(new Optimization{solids=arena.Definition.Solids.Length,sourceRenderers=source.Length,activeRenderers=active.Length,staticBatched=active.Count(r=>r.isPartOfStaticBatch),materials=active.SelectMany(r=>r.sharedMaterials).Distinct().Count(),sourceTriangles=triangles,unreferencedMeshes=ArenaMeshResidency.ReadUnreferencedOwnedMeshes(arena.transform).Length},true));
                camera.fieldOfView=70;
                var eyes=new[]{new Vector3(-15,1.7f,27),new Vector3(27,5.7f,9),new Vector3(-27,1.7f,-8),new Vector3(0,1.7f,-30)};
                var targets=new[]{new Vector3(24,2,22),new Vector3(28,0,-12),new Vector3(-13,1.7f,0),new Vector3(-22,1.7f,-27)};
                var names=new[]{"excavation","terrace","tunnel","south"};
                for(int i=0;i<eyes.Length;i++){cameraGo.transform.position=eyes[i];cameraGo.transform.LookAt(targets[i]);Capture(camera,names[i]);}
                Object.Destroy(cameraGo);
                ground.SendMessage("Repeat");yield return NativeLoadingTestScene.Wait(ground);
                Assert.That(ground.Session.ArenaIdentity,Is.EqualTo(QuarryArenaCatalog.Identity));Assert.That(JsonUtility.ToJson(ground.CameraReviewArena.Definition),Is.EqualTo(geometry));Assert.That(ground.CameraReviewArena.Surface.navMeshData,Is.SameAs(nav));Assert.That(ground.Session.Time,Is.LessThan(1));
                ground.OpenPauseReviewSeat(0);
            }
            finally{if(had)PlayerPrefs.SetString(ProvingGround.LastPlayedMapPreferenceKey,prior);else PlayerPrefs.DeleteKey(ProvingGround.LastPlayedMapPreferenceKey);}
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
        [Serializable] sealed class Optimization {public int solids,sourceRenderers,activeRenderers,staticBatched,materials,unreferencedMeshes;public long sourceTriangles;}
    }
}
