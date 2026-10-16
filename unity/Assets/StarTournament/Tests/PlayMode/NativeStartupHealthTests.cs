using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class NativeStartupHealthTests
    {
        static readonly FieldInfo Loader=typeof(ProvingGround).GetField("LabHistoryLoaderForTests",BindingFlags.Static|BindingFlags.NonPublic);
        Scene scene;ProvingGround ground;string directory;string[] prior;object priorLoader;
        ManualResetEventSlim historyGate;
        readonly string[] names={NativeStartupHealth.ReadyEnvironment,NativeStartupHealth.VersionEnvironment,NativeStartupHealth.TokenEnvironment,"STAR_TOURNAMENT_QA_LAB_HISTORY"};
        string Marker=>Path.Combine(directory,"ready.json");
        [SetUp]public void Setup()
        {
            directory=Path.Combine(Path.GetTempPath(),"st-startup-health-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            prior=names.Select(Environment.GetEnvironmentVariable).ToArray();priorLoader=Loader.GetValue(null);
            Environment.SetEnvironmentVariable(names[0],Marker);Environment.SetEnvironmentVariable(names[1],"1.2.3-test.4");Environment.SetEnvironmentVariable(names[2],new string('a',32));
            Environment.SetEnvironmentVariable(names[3],Path.Combine(directory,"history.json"));
        }
        IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("ProvingGround",LoadSceneMode.Additive);
            scene=SceneManager.GetSceneByName("ProvingGround");
            ground=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<ProvingGround>()).Single();
        }
        IEnumerator WaitReady()
        {
            float deadline=Time.realtimeSinceStartup+120;
            while(!File.Exists(Marker))
            {Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline),"No startup marker after successful menu");yield return null;}
        }
        [UnityTest]public IEnumerator MenuConfirmsChallengeWhileBackgroundHistoryIsBlocked()
        {
            historyGate=new ManualResetEventSlim();
            Func<string,LabBundle,LabReleaseCatalog,CancellationToken,DesignLabHistory> loader=(path,baseline,catalogue,token)=>
            {historyGate.Wait(token);return new DesignLabHistory(path,baseline,releases:catalogue,resetToLatestDefault:true,persistMigration:false,cancellationToken:token);};
            Loader.SetValue(null,loader);
            yield return Load();Assert.That(ground.IsReady,Is.False);Assert.That(File.Exists(Marker),Is.False);
            int firstReady=-1;float deadline=Time.realtimeSinceStartup+120;
            while(!File.Exists(Marker))
            {
                if(ground.IsReady&&firstReady<0)firstReady=Time.frameCount;
                Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;
            }
            Assert.That(ground.IsReady,Is.True);Assert.That(ground.IsLoading,Is.False);
            Assert.That(firstReady,Is.GreaterThanOrEqualTo(0));Assert.That(Time.frameCount-firstReady,Is.GreaterThanOrEqualTo(1));
            Assert.That(ground.IsLabHistoryReady,Is.False);Assert.That(ground.IsLabHistoryLoading,Is.True);
            Assert.That(EventSystem.current,Is.Not.Null);Assert.That(EventSystem.current.currentSelectedGameObject,Is.Not.Null);
            var report=JsonUtility.FromJson<Ready>(File.ReadAllText(Marker));
            Assert.That(report.schema,Is.EqualTo(1));Assert.That(report.version,Is.EqualTo("1.2.3-test.4"));Assert.That(report.token,Is.EqualTo(new string('a',32)));Assert.That(report.phase,Is.EqualTo("menu-ready"));
            var bytes=File.ReadAllBytes(Marker);yield return null;yield return null;CollectionAssert.AreEqual(bytes,File.ReadAllBytes(Marker));
        }
        [UnityTest]public IEnumerator DisabledStartupCannotConfirmBeforeInitialization()
        {
            void Disable(Scene loaded,LoadSceneMode mode)
            {if(loaded.name=="ProvingGround")foreach(var item in loaded.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<ProvingGround>()))item.enabled=false;}
            SceneManager.sceneLoaded+=Disable;
            try{yield return Load();}finally{SceneManager.sceneLoaded-=Disable;}
            yield return null;yield return null;yield return null;
            Assert.That(ground.IsReady,Is.False);Assert.That(File.Exists(Marker),Is.False);
            ground.enabled=true;yield return WaitReady();Assert.That(ground.IsReady,Is.True);
        }
        [UnityTest]public IEnumerator CriticalInitializationFailureDoesNotConfirmHealth()
        {
            void BreakProfile(Scene loaded,LoadSceneMode mode)
            {if(loaded.name=="ProvingGround")foreach(var item in loaded.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<ProvingGround>()))item.Profile=null;}
            LogAssert.Expect(LogType.Exception,new System.Text.RegularExpressions.Regex("NullReferenceException"));
            SceneManager.sceneLoaded+=BreakProfile;
            try{yield return Load();}finally{SceneManager.sceneLoaded-=BreakProfile;}
            float deadline=Time.realtimeSinceStartup+120;
            while(!ground.LoadingScreen.GetComponentsInChildren<Text>().Any(t=>t.text=="Не удалось загрузить игру"))
            {Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}
            yield return null;yield return null;Assert.That(ground.IsReady,Is.False);Assert.That(File.Exists(Marker),Is.False);
        }
        [Serializable]sealed class Ready{public int schema;public string version,token,phase;}
        [UnityTearDown]public IEnumerator Cleanup()
        {
            var worker=ground==null?null:typeof(ProvingGround).GetField("historyLoading",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ground) as Task;
            if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
            historyGate?.Set();Loader.SetValue(null,priorLoader);
            float deadline=Time.realtimeSinceStartup+120;
            while(worker!=null&&!worker.IsCompleted){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}
            historyGate?.Dispose();historyGate=null;
            for(int i=0;i<names.Length;i++)Environment.SetEnvironmentVariable(names[i],prior[i]);
            if(Directory.Exists(directory))Directory.Delete(directory,true);
            yield return null;
        }
    }
}
