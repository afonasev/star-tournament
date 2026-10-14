using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class NativeLabStartupTests
    {
        static readonly FieldInfo Loader=typeof(ProvingGround).GetField("LabHistoryLoaderForTests",BindingFlags.Static|BindingFlags.NonPublic);
        Task<DesignLabHistory> worker;ManualResetEventSlim gate,started;Scene scene;ProvingGround ground;string directory,priorEnvironment;object priorLoader;
        [SetUp] public void Setup()
        {
            directory=Path.Combine(Path.GetTempPath(),"st-lab-startup-"+Guid.NewGuid());Directory.CreateDirectory(directory);
            priorEnvironment=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_HISTORY");
            Environment.SetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_HISTORY",Path.Combine(directory,"history.json"));
            gate=new ManualResetEventSlim();started=new ManualResetEventSlim();priorLoader=Loader.GetValue(null);
            Func<string,LabBundle,LabReleaseCatalog,CancellationToken,DesignLabHistory> load=(path,baseline,catalogue,token)=>
            {
                started.Set();gate.Wait(token);
                return new DesignLabHistory(path,baseline,releases:catalogue,resetToLatestDefault:true,persistMigration:false,cancellationToken:token);
            };
            Loader.SetValue(null,load);
        }
        IEnumerator Load()
        {
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");
            ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();
            float deadline=Time.realtimeSinceStartup+120;
            while(!started.IsSet){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}
            worker=Field<Task<DesignLabHistory>>(ground,"historyLoading");
        }
        Button B(string name)=>ground.GetComponentsInChildren<Button>(true).First(b=>b.name==name||b.GetComponentInChildren<Text>()?.text==name);
        static T Field<T>(ProvingGround owner,string name)=>(T)typeof(ProvingGround).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);
        [UnityTest] public IEnumerator BlockedHistoryAllowsDefaultMenuMatchAndLateCompletionKeepsOwners()
        {
            yield return Load();Assert.That(ground.IsReady,Is.True);Assert.That(ground.IsLabHistoryReady,Is.False);
            var baseline=Field<LabBundle>(ground,"historyStartupBaseline");var catalogue=Field<LabReleaseCatalog>(ground,"historyStartupCatalogue");
            var expected=DesignLabHistory.ResolvePublishedDefault(baseline,catalogue);
            var active=Field<LabRevisionReference>(ground,"activeLabReference");
            Assert.That(active.ProfileId,Is.EqualTo(DesignLabHistory.ReleaseId));Assert.That(active.Revision,Is.EqualTo(expected.Selected.ReleaseNumber));
            Assert.That(active.Hash,Is.EqualTo(expected.Selected.Snapshot.Hash()));
            Assert.That(ground.CaptureLabBundle().Hash(),Is.EqualTo(active.Hash));
            B("main-action-2").onClick.Invoke();yield return null;
            Assert.That(B("lab-save").interactable,Is.False);Assert.That(B("lab-create").interactable,Is.False);Assert.That(B("lab-back").interactable,Is.True);
            B("lab-back").onClick.Invoke();yield return null;
            B("main-action-0").onClick.Invoke();B("Диагностика четырёх камер · без управления").onClick.Invoke();
            float deadline=Time.realtimeSinceStartup+120;while(!ground.Running){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}
            var before=JsonUtility.ToJson(Field<LabRevisionReference>(ground,"frozenLabReference"));
            var identity=ground.LabSavedIdentity;var frozen=JsonUtility.ToJson(Field<ProvingProfile>(ground,"frozenMovement"));var values=ground.CaptureLabBundle().Hash();
            gate.Set();yield return NativeLoadingTestScene.WaitLabHistory(ground);
            Assert.That(ground.Running,Is.True);Assert.That(ground.LabSavedIdentity,Is.EqualTo(identity));
            Assert.That(JsonUtility.ToJson(Field<LabRevisionReference>(ground,"frozenLabReference")),Is.EqualTo(before));
            Assert.That(JsonUtility.ToJson(Field<ProvingProfile>(ground,"frozenMovement")),Is.EqualTo(frozen));Assert.That(ground.CaptureLabBundle().Hash(),Is.EqualTo(values));
            Assert.That(File.Exists(Path.Combine(directory,"history.json")),Is.False,"Read-only startup must not create a history file");
        }
        void SaveUi(string name)
        {
            var root=ground.transform.Find("native-ui").gameObject;var canvas=root.GetComponent<Canvas>();
            var mode=canvas.renderMode;var cameraBefore=canvas.worldCamera;float distance=canvas.planeDistance;
            var cameraObject=new GameObject("Lab startup capture");var camera=cameraObject.AddComponent<Camera>();
            var target=new RenderTexture(1600,900,24);var pixels=new Texture2D(1600,900,TextureFormat.RGB24,false);
            var children=root.GetComponentsInChildren<Transform>(true);var layers=children.Select(t=>t.gameObject.layer).ToArray();var active=RenderTexture.active;
            try
            {
                foreach(var child in children)child.gameObject.layer=5;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color32(16,28,41,255);camera.cullingMask=1<<5;camera.orthographic=true;camera.targetTexture=target;
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,1600,900),0,0);pixels.Apply();var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../docs/evidence/fix-large-lab-startup/editor-views"));Directory.CreateDirectory(output);File.WriteAllBytes(Path.Combine(output,name+".png"),pixels.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode=mode;canvas.worldCamera=cameraBefore;canvas.planeDistance=distance;
                for(int i=0;i<children.Length;i++)children[i].gameObject.layer=layers[i];RenderTexture.active=active;
                UnityEngine.Object.Destroy(cameraObject);UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(pixels);
            }
        }
        [UnityTest] public IEnumerator SaveDefaultMenuAndLabWaitingEditorViews()
        {
            yield return Load();SaveUi("default-menu");B("main-action-2").onClick.Invoke();yield return null;SaveUi("lab-waiting");
            Assert.That(B("lab-back").interactable,Is.True);Assert.That(B("lab-save").interactable,Is.False);
        }
        [UnityTest] public IEnumerator InvalidHistoryKeepsDefaultAndLabCannotWriteSource()
        {
            string path=Path.Combine(directory,"history.json");File.WriteAllText(path,"invalid-json");var bytes=File.ReadAllBytes(path);
            yield return Load();var before=ground.CaptureLabBundle().Hash();gate.Set();
            float deadline=Time.realtimeSinceStartup+120;
            while(ground.IsLabHistoryLoading){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}
            Assert.That(ground.IsReady,Is.True);Assert.That(ground.IsLabHistoryReady,Is.False);Assert.That(ground.LabHistoryLoadError,Is.Not.Null);
            Assert.That(ground.CaptureLabBundle().Hash(),Is.EqualTo(before));
            B("main-action-2").onClick.Invoke();yield return null;
            Assert.That(B("lab-save").interactable,Is.False);Assert.That(B("lab-create").interactable,Is.False);Assert.That(B("lab-back").interactable,Is.True);
            B("lab-back").onClick.Invoke();yield return null;CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));
        }
        [UnityTest] public IEnumerator UnloadCancelsBlockedHistoryWithoutCreatingFile()
        {
            yield return Load();Assert.That(ground.IsLabHistoryLoading,Is.True);
            yield return SceneManager.UnloadSceneAsync(scene);scene=default;yield return null;
            Assert.That(ground==null,Is.True);Assert.That(File.Exists(Path.Combine(directory,"history.json")),Is.False);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
            gate.Set();Loader.SetValue(null,priorLoader);Environment.SetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_HISTORY",priorEnvironment);
            if(Directory.Exists(directory))Directory.Delete(directory,true);
            float deadline=Time.realtimeSinceStartup+120;
            while(worker!=null&&!worker.IsCompleted){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}
            gate.Dispose();started.Dispose();worker=null;yield return null;
        }
    }
}
