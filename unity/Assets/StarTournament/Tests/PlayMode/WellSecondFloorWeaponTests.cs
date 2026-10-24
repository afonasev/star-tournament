using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class WellSecondFloorWeaponTests
    {
        [UnityTest] public IEnumerator RealMatchShowsBothSecondFloorWeapons()
        {
            bool had=PlayerPrefs.HasKey(ProvingGround.LastPlayedMapPreferenceKey);string prior=PlayerPrefs.GetString(ProvingGround.LastPlayedMapPreferenceKey);
            Scene scene=default;GameObject cameraGo=null;RenderTexture rt=null;
            try
            {
                yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");
                var ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();
                ground.SendMessage("OpenSetup");ground.SelectAuthoredMap(WellArenaCatalog.Id);
                var composition=new NativeMatchComposition(NativeMatchRoster.Ffa(2),Enumerable.Range(0,2).Select(i=>new NativeParticipantInfo(NativeParticipantKind.Bot,"Well bot "+i,NativeParticipantColors.For(i),(int)NativeBotDifficulty.Normal)).ToArray(),new[]{0});
                ground.StartBotReview(composition,7219);yield return NativeLoadingTestScene.Wait(ground);ground.OpenPauseReviewSeat(0);
                Assert.That(ground.CameraReviewArena.Definition.Revision,Is.EqualTo("5"));
                Assert.That(ground.Session.WeaponPickups.Length,Is.EqualTo(5));
                ground.SendMessage("LateUpdate");yield return null;
                cameraGo=new GameObject("Well corner weapon review");var camera=cameraGo.AddComponent<Camera>();camera.fieldOfView=75;camera.nearClipPlane=.05f;
                rt=new RenderTexture(1280,720,24,RenderTextureFormat.DefaultHDR);camera.targetTexture=rt;
                var directory=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_WELL_WEAPONS_OUTPUT")??Path.GetFullPath(Path.Combine(Application.dataPath,"../../docs/evidence/well-second-floor-weapons/views"));Directory.CreateDirectory(directory);
                foreach(var id in new[]{"shotgun-02","cutter-02"})
                {
                    var pickup=ground.Session.WeaponPickups.Single(p=>p.InstanceId==id);
                    var visual=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Single(t=>t.name==id+"-presentation");
                    Assert.That(pickup.Available,Is.True);Assert.That(visual.gameObject.activeInHierarchy,Is.True);
                    Assert.That(visual.GetComponentsInChildren<Renderer>().Length,Is.GreaterThan(0));
                    Assert.That(Vector3.Distance(visual.position,pickup.Anchor),Is.LessThan(2));
                    int side=id=="shotgun-02"?-1:1;
                    cameraGo.transform.position=new Vector3(side*14.5f,WellArenaCatalog.Storey+1.7f,side*16.9f);
                    cameraGo.transform.LookAt(pickup.Anchor+Vector3.up*.7f);camera.Render();yield return null;
                    var previous=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                    try{image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(directory,id+".png"),image.EncodeToPNG());}
                    finally{RenderTexture.active=previous;Object.Destroy(image);}
                }
                File.WriteAllText(Path.Combine(directory,"arena.json"),JsonUtility.ToJson(ground.CameraReviewArena.Definition,true));
            }
            finally
            {
                if(cameraGo){cameraGo.GetComponent<Camera>().targetTexture=null;Object.Destroy(cameraGo);}if(rt)Object.Destroy(rt);
                if(had)PlayerPrefs.SetString(ProvingGround.LastPlayedMapPreferenceKey,prior);else PlayerPrefs.DeleteKey(ProvingGround.LastPlayedMapPreferenceKey);
                if(scene.IsValid()&&scene.isLoaded)SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }
    }
}
