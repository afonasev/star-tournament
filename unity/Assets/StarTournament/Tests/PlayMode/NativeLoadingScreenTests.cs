using System;
using System.IO;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using System.Security.Cryptography;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class NativeLoadingScreenTests
    {
        Scene scene;Gamepad pad;
        bool hadLastMap;string priorLastMap;
        [SetUp] public void PreserveLastMap()
        {
            hadLastMap=PlayerPrefs.HasKey(ProvingGround.LastPlayedMapPreferenceKey);
            priorLastMap=PlayerPrefs.GetString(ProvingGround.LastPlayedMapPreferenceKey);
            PlayerPrefs.DeleteKey(ProvingGround.LastPlayedMapPreferenceKey);
        }
        internal static IEnumerator Capture(string name,GameObject root=null)
        {
            var directory=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_LOADING_SCREENSHOTS");
            if(string.IsNullOrEmpty(directory))yield break;
            Directory.CreateDirectory(directory);yield return null;yield return null;
            // Batch Editor does not present a window framebuffer. Render the actual Canvas
            // into a camera texture, restoring its normal overlay mode immediately.
            var screen=root??UnityEngine.Object.FindObjectsByType<NativeLoadingScreen>(FindObjectsSortMode.None).Single().gameObject;
            var canvas=screen.GetComponent<Canvas>();var priorMode=canvas.renderMode;var priorCamera=canvas.worldCamera;float priorDistance=canvas.planeDistance;
            var cameraObject=new GameObject("loading-capture-camera");var camera=cameraObject.AddComponent<Camera>();
            var target=new RenderTexture(1600,900,24);var pixels=new Texture2D(1600,900,TextureFormat.RGB24,false);
            var children=screen.GetComponentsInChildren<Transform>(true);var layers=children.Select(t=>t.gameObject.layer).ToArray();var priorTarget=RenderTexture.active;
            try
            {
                foreach(var child in children)child.gameObject.layer=5;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color32(16,28,41,255);camera.cullingMask=1<<5;camera.orthographic=true;camera.targetTexture=target;
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1600,900),0,0);pixels.Apply();
                File.WriteAllBytes(Path.Combine(directory,name+".png"),pixels.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode=priorMode;canvas.worldCamera=priorCamera;canvas.planeDistance=priorDistance;
                for(int i=0;i<children.Length;i++)children[i].gameObject.layer=layers[i];RenderTexture.active=priorTarget;
                camera.targetTexture=null;target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(pixels);UnityEngine.Object.Destroy(cameraObject);Canvas.ForceUpdateCanvases();
            }
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup(){if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);
            if(hadLastMap)PlayerPrefs.SetString(ProvingGround.LastPlayedMapPreferenceKey,priorLastMap);
            else PlayerPrefs.DeleteKey(ProvingGround.LastPlayedMapPreferenceKey);PlayerPrefs.Save();}
        [UnityTest] public IEnumerator StartupShowsApprovedBrandAndAnimatedIndicatorBeforeReady()
        {
            yield return SceneManager.LoadSceneAsync("ProvingGround",LoadSceneMode.Additive);
            scene=SceneManager.GetSceneByName("ProvingGround");
            var ground=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<ProvingGround>()).Single();
            Assert.That(ground.IsLoading,Is.True);Assert.That(ground.IsReady,Is.False);
            var screen=ground.LoadingScreen;Assert.That(screen.Visible,Is.True);
            var texts=screen.GetComponentsInChildren<Text>().Select(x=>x.text).ToArray();
            Assert.That(texts.Any(x=>x.Contains("Made with Codex")),Is.True);
            Assert.That(texts.Any(x=>x=="Подождите немного"||x=="Подготовка главного меню"),Is.False);
            yield return Capture("startup");
            float before=screen.AnimationPosition;yield return null;yield return null;
            Assert.That(screen.AnimationPosition,Is.Not.EqualTo(before));
            Assert.That(ground.Running,Is.False);
            yield return NativeLoadingTestScene.Wait(ground);
            Assert.That(screen.Visible,Is.False);
        }
        [UnityTest] public IEnumerator MatchBlocksDuplicateLaunchAndTicksOnlyAfterReady()
        {
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");
            var ground=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<ProvingGround>()).Single();
            var buttons=ground.GetComponentsInChildren<Button>(true);buttons.Single(b=>b.name=="main-action-0").onClick.Invoke();
            // Existing explicit all-AI diagnostic action exercises the same UI loading path.
            var begin=buttons.Single(b=>b.name=="Диагностика четырёх камер · без управления");
            begin.onClick.Invoke();Assert.That(ground.IsLoading,Is.True);Assert.That(ground.Running,Is.False);
            var prior=ground.Session;begin.onClick.Invoke();
            var texts=ground.LoadingScreen.GetComponentsInChildren<Text>().ToDictionary(t=>t.name,t=>t.text);
            Assert.That(texts["loading-participants-count"],Is.EqualTo(ground.RosterTotalForLoadingTest().ToString()));
            Assert.That(texts.Values.Any(x=>x=="ПОДГОТОВКА МАТЧА"||x=="Подождите немного"),Is.False);
            yield return Capture("match");
            float before=ground.LoadingScreen.AnimationPosition;yield return null;yield return null;
            Assert.That(ground.LoadingScreen.AnimationPosition,Is.Not.EqualTo(before));Assert.That(ground.Running,Is.False);
            float deadline=Time.realtimeSinceStartup+120;
            while(ground.IsLoading)
            {
                Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));
                if(ground.Session!=prior){Assert.That(ground.Session.Time,Is.Zero);Assert.That(ground.Session.Match.RemainingSeconds,Is.EqualTo(ground.Configuration.DurationMinutes*60));}
                yield return null;
            }
            Assert.That(ground.Session,Is.Not.SameAs(prior));Assert.That(ground.Running,Is.True);Assert.That(ground.LoadingScreen.Visible,Is.False);
        }
        ProvingGround Ground()=>scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<ProvingGround>()).Single();
        static Button B(ProvingGround ground,string name)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        IEnumerator HumanSetup()
        {
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");var ground=Ground();
            pad=InputSystem.AddDevice<Gamepad>();
            typeof(ProvingGround).GetField("menuDevice",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ground,pad);
            B(ground,"main-action-0").onClick.Invoke();B(ground,"setup-next").onClick.Invoke();B(ground,"setup-next").onClick.Invoke();
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.North));yield return null;yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
            Assert.That(B(ground,"Начать — четыре игрока").interactable,Is.True);
        }
        [UnityTest] public IEnumerator FocusLostDuringLoadingPausesBeforeFirstTick()
        {
            yield return HumanSetup();var ground=Ground();B(ground,"Начать — четыре игрока").onClick.Invoke();
            typeof(ProvingGround).GetMethod("OnApplicationFocus",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ground,new object[]{false});
            yield return NativeLoadingTestScene.Wait(ground);
            Assert.That(ground.Running,Is.False);Assert.That(ground.Session.Time,Is.Zero);Assert.That(ground.LoadingScreen.Visible,Is.False);
            typeof(ProvingGround).GetMethod("OnApplicationFocus",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ground,new object[]{true});
        }
        [UnityTest] public IEnumerator DeviceLostDuringLoadingPausesBeforeFirstTick()
        {
            yield return HumanSetup();var ground=Ground();B(ground,"Начать — четыре игрока").onClick.Invoke();InputSystem.RemoveDevice(pad);
            yield return NativeLoadingTestScene.Wait(ground);
            Assert.That(ground.Running,Is.False);Assert.That(ground.Session.Time,Is.Zero);Assert.That(ground.LoadingScreen.Visible,Is.False);
        }
        [UnityTest] public IEnumerator MissingActorAssetRecoversEditableSetupAndCanRetry()
        {
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");var ground=Ground();
            B(ground,"main-action-0").onClick.Invoke();var prefab=ground.TrooperBodyPrefab;ground.TrooperBodyPrefab=null;
            B(ground,"Диагностика четырёх камер · без управления").onClick.Invoke();yield return NativeLoadingTestScene.Wait(ground);
            Assert.That(ground.Running,Is.False);Assert.That(ground.LoadingScreen.Visible,Is.False);
            Assert.That(ground.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("Trooper assets missing")),Is.True);
            Assert.That(B(ground,"main-action-0").gameObject.activeInHierarchy,Is.False);
            ground.TrooperBodyPrefab=prefab;B(ground,"Диагностика четырёх камер · без управления").onClick.Invoke();yield return NativeLoadingTestScene.Wait(ground);
            Assert.That(ground.Running,Is.True);
        }
        [UnityTest] public IEnumerator FailedSpawnKeepsPartialCamerasDisabledDuringRosterEditingAndRetry()
        {
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");var ground=Ground();B(ground,"main-action-0").onClick.Invoke();
            var cameras=(Camera[])typeof(ProvingGround).GetField("cameras",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ground);
            var old=cameras[0];B(ground,"Диагностика четырёх камер · без управления").onClick.Invoke();
            float deadline=Time.realtimeSinceStartup+120;
            while(cameras[0]==null||cameras[0]==old){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}
            var arena=ground.GetComponentInChildren<ProvingArena>();
            typeof(ProvingArena).GetProperty("Spawns").SetValue(arena,Array.Empty<Vector3>());
            yield return NativeLoadingTestScene.Wait(ground);Assert.That(ground.Running,Is.False);
            typeof(ProvingGround).GetMethod("SetSeatCount",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ground,new object[]{2});
            Assert.That(cameras.Where(c=>c).All(c=>!c.enabled),Is.True);
            B(ground,"Диагностика четырёх камер · без управления").onClick.Invoke();yield return NativeLoadingTestScene.Wait(ground);
            Assert.That(ground.Running,Is.True);Assert.That(cameras[0].enabled,Is.True);
        }
        [UnityTest] public IEnumerator UnloadDuringHistoryOrProjectionDoesNotPublishIntoRemovedScene()
        {
            yield return SceneManager.LoadSceneAsync("ProvingGround",LoadSceneMode.Additive);scene=SceneManager.GetSceneByName("ProvingGround");
            yield return null;yield return SceneManager.UnloadSceneAsync(scene);scene=default;
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");var ground=Ground();B(ground,"main-action-0").onClick.Invoke();
            B(ground,"Диагностика четырёх камер · без управления").onClick.Invoke();yield return null;yield return null;
            yield return SceneManager.UnloadSceneAsync(scene);scene=default;yield return null;
            Assert.That(UnityEngine.Object.FindObjectsByType<NativeLoadingScreen>(FindObjectsSortMode.None),Is.Empty);
        }
        static string HashFile(string path){using(var file=File.OpenRead(path))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(file));}
        [UnityTest,Timeout(1800000)] public IEnumerator HistoryRemainsReadOnlyWhileLoadingKeepsUpdating()
        {
            string priorEnvironment=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_HISTORY");
            string directory=Path.Combine(Application.temporaryCachePath,"loading-history-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            string path=Path.Combine(directory,"history.json");
            try
            {
                var source=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_LOADING_HISTORY_SOURCE");
                if(!string.IsNullOrEmpty(source))File.Copy(source,path);
                else
                {
                    yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");var initial=(DesignLabHistory)typeof(ProvingGround).GetField("labHistory",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Ground());
                    var bundle=((LabBundle)typeof(DesignLabHistory).GetField("shipped",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(initial)).Clone();
                    yield return SceneManager.UnloadSceneAsync(scene);scene=default;
                    var history=new DesignLabHistory(path,bundle,releases:LabReleaseCatalog.Load(),resetToLatestDefault:true,persistMigration:false);history.Create("Loading QA");
                }
                var before=HashFile(path);Environment.SetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_HISTORY",path);
                yield return SceneManager.LoadSceneAsync("ProvingGround",LoadSceneMode.Additive);scene=SceneManager.GetSceneByName("ProvingGround");var ground=Ground();
                float deadline=Time.realtimeSinceStartup+1700;bool animated=false;float last=ground.LoadingScreen.AnimationPosition;
                while(!ground.IsReady)
                {
                    Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));Assert.That(ground.Running,Is.False);
                    if(ground.LoadingScreen.AnimationPosition!=last)animated=true;
                    last=ground.LoadingScreen.AnimationPosition;yield return null;
                }
                var loaded=(DesignLabHistory)typeof(ProvingGround).GetField("labHistory",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ground);
                Assert.That(loaded.StorageError,Is.Null);Assert.That(loaded.Writable,Is.True);
                Debug.Log("Loading history QA: bytes="+new FileInfo(path).Length+" animation="+animated+" writable="+loaded.Writable+" sha256="+before);
                Assert.That(animated,Is.True);Assert.That(HashFile(path),Is.EqualTo(before));Assert.That(ground.LoadingScreen.Visible,Is.False);
            }
            finally{Environment.SetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_HISTORY",priorEnvironment);if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }

    }
    static class LoadingTestRoster
    {
        public static int RosterTotalForLoadingTest(this ProvingGround ground)=>(int)typeof(ProvingGround).GetProperty("RosterTotal",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ground);
    }
}
