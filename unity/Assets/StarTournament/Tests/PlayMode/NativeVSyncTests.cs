using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class NativeVSyncTests
    {
        const string Key="StarTournament.Graphics.VSync";
        Scene scene;ProvingGround ground;bool existed;int saved,originalVSync,originalTarget;
        Toggle Toggle=>ground.GetComponentsInChildren<Toggle>(true).Single(t=>t.name=="settings-vsync");
        Button B(string name)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        [SetUp] public void Preserve()
        {existed=PlayerPrefs.HasKey(Key);saved=PlayerPrefs.GetInt(Key);originalVSync=QualitySettings.vSyncCount;originalTarget=Application.targetFrameRate;PlayerPrefs.DeleteKey(Key);}
        IEnumerator Load()
        {yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();}
        IEnumerator Unload(){yield return SceneManager.UnloadSceneAsync(scene);scene=default;ground=null;}
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(scene.IsValid())yield return Unload();
            QualitySettings.vSyncCount=originalVSync;Application.targetFrameRate=originalTarget;
            if(existed)PlayerPrefs.SetInt(Key,saved);else PlayerPrefs.DeleteKey(Key);PlayerPrefs.Save();
        }
        [UnityTest] public IEnumerator MissingAndInvalidPreferencesEnableVSyncByDefault()
        {
            foreach(int quality in new[]{0,2})foreach(int preference in new[]{-1,7})
            {
                QualitySettings.vSyncCount=quality;if(preference==-1)PlayerPrefs.DeleteKey(Key);else PlayerPrefs.SetInt(Key,preference);
                yield return Load();Assert.That(QualitySettings.vSyncCount,Is.EqualTo(1));
                B("main-action-4").onClick.Invoke();B("settings-section-0").onClick.Invoke();Assert.That(Toggle.isOn,Is.True);
                if(quality==0&&preference==-1)yield return NativeLoadingScreenTests.Capture("vsync-default-on",ground.transform.Find("native-ui").gameObject,()=>Toggle.GetComponent<MenuScrollFocus>().Reveal());
                yield return Unload();Assert.That(QualitySettings.vSyncCount,Is.EqualTo(quality));
            }
        }
        [UnityTest] public IEnumerator TogglePersistsAcrossReloadAndRestoresEngineState()
        {
            QualitySettings.vSyncCount=2;Application.targetFrameRate=73;yield return Load();
            B("main-action-4").onClick.Invoke();B("settings-section-0").onClick.Invoke();
            Assert.That(Toggle.navigation.selectOnUp,Is.EqualTo(ground.GetComponentsInChildren<Toggle>(true).Single(t=>t.name=="settings-shadows")));
            Assert.That(Toggle.navigation.selectOnDown,Is.EqualTo(ground.GetComponentsInChildren<Toggle>(true).Single(t=>t.name=="settings-visual-Lighting-enabled")));
            Toggle.isOn=false;Assert.That(QualitySettings.vSyncCount,Is.Zero);Assert.That(PlayerPrefs.GetInt(Key),Is.Zero);
            Toggle.isOn=true;Assert.That(QualitySettings.vSyncCount,Is.EqualTo(1));Assert.That(PlayerPrefs.GetInt(Key),Is.EqualTo(1));Assert.That(Application.targetFrameRate,Is.EqualTo(73));
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(Toggle.gameObject);
            yield return NativeLoadingScreenTests.Capture("vsync-on",ground.transform.Find("native-ui").gameObject,()=>Toggle.GetComponent<MenuScrollFocus>().Reveal());
            Toggle.isOn=false;yield return NativeLoadingScreenTests.Capture("vsync-off",ground.transform.Find("native-ui").gameObject,()=>Toggle.GetComponent<MenuScrollFocus>().Reveal());
            yield return Unload();Assert.That(QualitySettings.vSyncCount,Is.EqualTo(2));
            yield return Load();Assert.That(QualitySettings.vSyncCount,Is.Zero);Assert.That(Application.targetFrameRate,Is.EqualTo(73));
            B("main-action-4").onClick.Invoke();B("settings-section-0").onClick.Invoke();Assert.That(Toggle.isOn,Is.False);
            yield return Unload();Assert.That(QualitySettings.vSyncCount,Is.EqualTo(2));
        }
    }
}
