using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class IndependentPauseMenuTests
    {
        Scene scene;
        ProvingGround ground;
        readonly Gamepad[] pads=new Gamepad[2];
        bool hadLastMap;string priorLastMap;
        [SetUp] public void PreserveLastMap()
        {
            hadLastMap=PlayerPrefs.HasKey(ProvingGround.LastPlayedMapPreferenceKey);priorLastMap=PlayerPrefs.GetString(ProvingGround.LastPlayedMapPreferenceKey);
            PlayerPrefs.DeleteKey(ProvingGround.LastPlayedMapPreferenceKey);
        }
        Button Button(string name)=>ground.GetComponentsInChildren<Button>(true).Single(button=>button.name==name);
        GameObject Object(string name)=>ground.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name).gameObject;
        IEnumerator Load()
        {
            yield return NativeLoadingTestScene.Load();
            scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<ProvingGround>()).Single();
            Button("main-action-0").onClick.Invoke();NativeSetupFixture.UnboundHumans(ground);
            while(ground.LocalSeatCount>2)Button("seats-minus").onClick.Invoke();
            Button("setup-next").onClick.Invoke();Button("setup-next").onClick.Invoke();
            for(int seat=0;seat<2;seat++)
            {
                pads[seat]=InputSystem.AddDevice<Gamepad>();
                InputSystem.QueueStateEvent(pads[seat],new GamepadState().WithButton(GamepadButton.North));
                yield return null;yield return null;
                InputSystem.QueueStateEvent(pads[seat],new GamepadState());yield return null;
                Button("roster-card-"+seat).onClick.Invoke();Button("roster-identity").onClick.Invoke();
                Button("roster-choice-guest").onClick.Invoke();Button("roster-done").onClick.Invoke();
            }
            Button("Начать — четыре игрока").onClick.Invoke();yield return new WaitForFixedUpdate();yield return null;
            yield return NativeLoadingTestScene.Wait(ground);Assert.That(ground.Running,Is.True);
        }
        IEnumerator Press(int seat,GamepadButton button)
        {
            InputSystem.QueueStateEvent(pads[seat],new GamepadState().WithButton(button));yield return null;yield return null;
            InputSystem.QueueStateEvent(pads[seat],new GamepadState());yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach(var pad in pads)if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);
            if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);
            if(hadLastMap)PlayerPrefs.SetString(ProvingGround.LastPlayedMapPreferenceKey,priorLastMap);else PlayerPrefs.DeleteKey(ProvingGround.LastPlayedMapPreferenceKey);
            PlayerPrefs.Save();
        }
        [UnityTest] public IEnumerator SplitScreenDrawsOneGlobalFpsCounter()
        {
            yield return Load();yield return Press(0,GamepadButton.Start);
            Button("pause-action-0-1").onClick.Invoke();Button("seat-settings-0-settings-section-2").onClick.Invoke();yield return null;
            var toggle=ground.GetComponentsInChildren<Toggle>(true).Single(t=>t.name=="seat-settings-0-settings-fps");toggle.isOn=true;yield return null;
            var counters=ground.GetComponentsInChildren<Text>().Where(t=>t.name=="fps-value"||t.name.StartsWith("seat-fps-")).ToArray();
            Assert.That(counters.Length,Is.EqualTo(1));Assert.That(counters[0].name,Is.EqualTo("fps-value"));
            var rect=(RectTransform)counters[0].transform.parent;Assert.That(rect.anchorMax,Is.EqualTo(Vector2.one));
        }
        [UnityTest] public IEnumerator VSyncPropagatesBetweenSeatsWithoutAdvancingMatch()
        {
            const string key="StarTournament.Graphics.VSync";bool existed=PlayerPrefs.HasKey(key);int saved=PlayerPrefs.GetInt(key),original=QualitySettings.vSyncCount,target=Application.targetFrameRate;
            try
            {
                PlayerPrefs.SetInt(key,0);yield return Load();yield return Press(0,GamepadButton.Start);yield return Press(1,GamepadButton.Start);
                Button("pause-action-0-1").onClick.Invoke();Button("pause-action-1-1").onClick.Invoke();
                var first=Object("seat-settings-0-settings-vsync").GetComponent<Toggle>();var second=Object("seat-settings-1-settings-vsync").GetComponent<Toggle>();var clock=ground.Session.Time;
                first.isOn=true;yield return null;Assert.That(second.isOn,Is.True);Assert.That(QualitySettings.vSyncCount,Is.EqualTo(1));Assert.That(ground.Session.Time,Is.EqualTo(clock));
                second.isOn=false;yield return null;Assert.That(first.isOn,Is.False);Assert.That(QualitySettings.vSyncCount,Is.Zero);Assert.That(ground.Session.Time,Is.EqualTo(clock));Assert.That(Application.targetFrameRate,Is.EqualTo(target));
            }
            finally{if(existed)PlayerPrefs.SetInt(key,saved);else PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();QualitySettings.vSyncCount=original;}
        }
        [UnityTest] public IEnumerator GraphicsEffectsPropagateBetweenSeatsWithoutAdvancingMatch()
        {
            yield return Load();yield return Press(0,GamepadButton.Start);yield return Press(1,GamepadButton.Start);
            Button("pause-action-0-1").onClick.Invoke();Button("pause-action-1-1").onClick.Invoke();
            var toggle0=Object("seat-settings-0-settings-visual-Bloom-enabled").GetComponent<Toggle>();
            var toggle1=Object("seat-settings-1-settings-visual-Bloom-enabled").GetComponent<Toggle>();
            var slider0=Object("seat-settings-0-settings-visual-Bloom-strength").GetComponent<Slider>();
            var slider1=Object("seat-settings-1-settings-visual-Bloom-strength").GetComponent<Slider>();
            string key=NativeVisualPreferences.Key(VisualEffect.Bloom);bool hadStrength=PlayerPrefs.HasKey(key+".Strength"),hadEnabled=PlayerPrefs.HasKey(key+".Enabled");float oldStrength=PlayerPrefs.GetFloat(key+".Strength");int oldEnabled=PlayerPrefs.GetInt(key+".Enabled");
            var clock=ground.Session.Time;
            try
            {
                slider0.value=60;toggle0.isOn=true;yield return null;
                Assert.That(slider1.value,Is.EqualTo(60));Assert.That(toggle1.isOn,Is.True);Assert.That(ground.Session.Time,Is.EqualTo(clock));
                foreach(var camera in ground.GetComponentsInChildren<Camera>().Where(c=>c.enabled))Assert.That(UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(camera).renderPostProcessing,Is.True);
                toggle1.isOn=false;yield return null;Assert.That(toggle0.isOn,Is.False);Assert.That(slider0.value,Is.EqualTo(60));
            }
            finally
            {if(hadStrength)PlayerPrefs.SetFloat(key+".Strength",oldStrength);else PlayerPrefs.DeleteKey(key+".Strength");if(hadEnabled)PlayerPrefs.SetInt(key+".Enabled",oldEnabled);else PlayerPrefs.DeleteKey(key+".Enabled");PlayerPrefs.Save();}
        }
        [UnityTest] public IEnumerator AntialiasingPropagatesAcrossOpenSeatSettingsAndAllCameras()
        {
            const string key=NativeVisualPreferences.AntialiasingKey;bool existed=PlayerPrefs.HasKey(key);int saved=PlayerPrefs.GetInt(key);
            try
            {
                NativeVisualPreferences.SetAntialiasingLevel(NativeVisualPreferences.AntialiasingOff);
                yield return Load();yield return Press(0,GamepadButton.Start);yield return Press(1,GamepadButton.Start);
                Button("pause-action-0-1").onClick.Invoke();Button("pause-action-1-1").onClick.Invoke();
                var first=Button("seat-settings-0-settings-antialiasing");var second=Button("seat-settings-1-settings-antialiasing");
                var scroll=Object("seat-settings-0-settings-image").GetComponent<ScrollRect>();
                yield return Press(0,GamepadButton.DpadRight);yield return Press(0,GamepadButton.DpadUp);
                Assert.That(scroll.verticalNormalizedPosition,Is.LessThan(.2f),"Wrapping to the last image control must reveal it");
                for(int i=0;i<14;i++)yield return Press(0,GamepadButton.DpadUp);
                Assert.That(scroll.verticalNormalizedPosition,Is.GreaterThan(.5f),"Gamepad focus must scroll the AA setting back into view");
                yield return Press(0,GamepadButton.South);
                Assert.That(NativeVisualPreferences.AntialiasingLevel,Is.EqualTo(NativeVisualPreferences.AntialiasingLow));
                Assert.That(first.GetComponentInChildren<Text>().text,Is.EqualTo(NativeVisualPreferences.AntialiasingNames[NativeVisualPreferences.AntialiasingLow]));
                Assert.That(second.GetComponentInChildren<Text>().text,Is.EqualTo(first.GetComponentInChildren<Text>().text));
                var cameras=ground.GetComponentsInChildren<Camera>().Where(camera=>camera.enabled).ToArray();Assert.That(cameras.Length,Is.EqualTo(2));
                foreach(var camera in cameras)
                {
                    var data=UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(camera);Assert.That(data.antialiasing,Is.EqualTo(UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing));
                    Assert.That(data.antialiasingQuality,Is.EqualTo(UnityEngine.Rendering.Universal.AntialiasingQuality.Low));Assert.That(data.renderPostProcessing,Is.True);
                }
                second.onClick.Invoke();yield return null;Assert.That(first.GetComponentInChildren<Text>().text,Is.EqualTo(NativeVisualPreferences.AntialiasingNames[NativeVisualPreferences.AntialiasingMedium]));
            }
            finally{if(existed)PlayerPrefs.SetInt(key,saved);else PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();}
        }
        [UnityTest] public IEnumerator ColorGradingPropagatesBetweenSeatsWithoutAdvancingMatch()
        {
            yield return Load();yield return Press(0,GamepadButton.Start);yield return Press(1,GamepadButton.Start);
            Button("pause-action-0-1").onClick.Invoke();Button("pause-action-1-1").onClick.Invoke();
            var toggle0=Object("seat-settings-0-settings-visual-ColorGrading-enabled").GetComponent<Toggle>();
            var toggle1=Object("seat-settings-1-settings-visual-ColorGrading-enabled").GetComponent<Toggle>();
            var slider0=Object("seat-settings-0-settings-visual-ColorGrading-strength").GetComponent<Slider>();
            var slider1=Object("seat-settings-1-settings-visual-ColorGrading-strength").GetComponent<Slider>();
            string key=NativeVisualPreferences.Key(VisualEffect.ColorGrading);bool hadStrength=PlayerPrefs.HasKey(key+".Strength"),hadEnabled=PlayerPrefs.HasKey(key+".Enabled");float oldStrength=PlayerPrefs.GetFloat(key+".Strength");int oldEnabled=PlayerPrefs.GetInt(key+".Enabled");
            var exposure0=Object("seat-settings-0-settings-color-0").GetComponent<Slider>();var exposure1=Object("seat-settings-1-settings-color-0").GetComponent<Slider>();
            var saturation0=Object("seat-settings-0-settings-color-1").GetComponent<Slider>();var saturation1=Object("seat-settings-1-settings-color-1").GetComponent<Slider>();
            string exposureKey="StarTournament.Graphics."+NativeVisualPreferences.ExposurePath,saturationKey="StarTournament.Graphics."+NativeVisualPreferences.SaturationPath;
            bool hadExposure=PlayerPrefs.HasKey(exposureKey),hadSaturation=PlayerPrefs.HasKey(saturationKey);float oldExposure=PlayerPrefs.GetFloat(exposureKey),oldSaturation=PlayerPrefs.GetFloat(saturationKey);
            var clock=ground.Session.Time;
            try
            {
                slider0.value=60;toggle0.isOn=true;exposure0.value=.3f;saturation0.value=110;yield return null;
                Assert.That(slider1.value,Is.EqualTo(60));Assert.That(toggle1.isOn,Is.True);Assert.That(ground.Session.Time,Is.EqualTo(clock));
                Assert.That(exposure1.value,Is.EqualTo(.3f).Within(.00001f));Assert.That(saturation1.value,Is.EqualTo(110));
                foreach(var camera in ground.GetComponentsInChildren<Camera>().Where(c=>c.enabled))Assert.That(UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(camera).renderPostProcessing,Is.True);
                toggle1.isOn=false;yield return null;Assert.That(toggle0.isOn,Is.False);Assert.That(slider0.value,Is.EqualTo(60));
            }
            finally
            {if(hadStrength)PlayerPrefs.SetFloat(key+".Strength",oldStrength);else PlayerPrefs.DeleteKey(key+".Strength");if(hadEnabled)PlayerPrefs.SetInt(key+".Enabled",oldEnabled);else PlayerPrefs.DeleteKey(key+".Enabled");if(hadExposure)PlayerPrefs.SetFloat(exposureKey,oldExposure);else PlayerPrefs.DeleteKey(exposureKey);if(hadSaturation)PlayerPrefs.SetFloat(saturationKey,oldSaturation);else PlayerPrefs.DeleteKey(saturationKey);PlayerPrefs.Save();}
        }
        [UnityTest] public IEnumerator TwoSeatMenusKeepOneFrozenMatchUntilTheLastCloses()
        {
            yield return Load();
            yield return Press(0,GamepadButton.Start);
            Assert.That(Cursor.visible,Is.False,"Gamepad pause keeps the cursor hidden");
            Assert.That(ground.Running,Is.False);
            Assert.That(Object("seat-pause-0").activeSelf,Is.True);
            Assert.That(Object("seat-pause-1").activeSelf,Is.True,"both viewport backgrounds dim");
            Assert.That(Object("seat-pause-0").transform.Find("pause-card").GetComponent<CanvasGroup>().blocksRaycasts,Is.False);
            Assert.That(Object("seat-pause-1").transform.Find("pause-card").GetComponent<CanvasGroup>().blocksRaycasts,Is.False);
            Assert.That(Object("seat-pause-0").transform.Find("pause-card/actions").gameObject.activeSelf,Is.True);
            var clock=ground.Session.Time;
            yield return Press(1,GamepadButton.Start);
            Assert.That(Object("pause-action-1-0").activeInHierarchy,Is.True);
            Button("pause-action-0-0").onClick.Invoke();yield return new WaitForFixedUpdate();
            Assert.That(ground.Running,Is.False);
            Assert.That(ground.Session.Time,Is.EqualTo(clock));
            Assert.That(Object("pause-action-1-0").activeInHierarchy,Is.True);
            Button("pause-action-1-0").onClick.Invoke();yield return new WaitForFixedUpdate();
            yield return NativeLoadingTestScene.Wait(ground);Assert.That(ground.Running,Is.True);
            Assert.That(Object("seat-pause-0").activeSelf,Is.False);
        }
        [UnityTest] public IEnumerator PersonalSettingsAndGlobalConfirmationStayScoped()
        {
            yield return Load();yield return Press(0,GamepadButton.Start);yield return Press(1,GamepadButton.Start);
            Button("pause-action-0-1").onClick.Invoke();Button("pause-action-1-1").onClick.Invoke();
            Button("seat-settings-0-settings-section-1").onClick.Invoke();Button("seat-settings-1-settings-section-1").onClick.Invoke();
            var slider0=Object("seat-settings-0-settings-mouse-sensitivity").GetComponent<Slider>();var slider1=Object("seat-settings-1-settings-mouse-sensitivity").GetComponent<Slider>();
            var before0=slider0.value;var before1=slider1.value;
            slider0.value+=.01f;
            var after0=slider0.value;
            var after1=slider1.value;
            Assert.That(after0,Is.Not.EqualTo(before0));Assert.That(after1,Is.EqualTo(before1));
            var x0=Object("seat-settings-0-settings-gamepad-horizontal").GetComponent<Slider>();var x1=Object("seat-settings-1-settings-gamepad-horizontal").GetComponent<Slider>();
            var otherX=x1.value;x0.value=x0.value==120?121:120;yield return null;
            Assert.That(x1.value,Is.EqualTo(otherX),"gamepad axis belongs to its seat");
            var level0=Object("seat-settings-0-settings-auto-level").GetComponent<Toggle>();var level1=Object("seat-settings-1-settings-auto-level").GetComponent<Toggle>();
            var otherLevel=level1.isOn;level0.isOn=!level0.isOn;yield return null;
            Assert.That(level1.isOn,Is.EqualTo(otherLevel),"auto-level preference belongs to its seat");
            int priorEffects=NativeAudioPreferences.Effects(ground.Profile);
            Button("seat-settings-0-settings-section-3").onClick.Invoke();Button("seat-settings-1-settings-section-3").onClick.Invoke();
            var effects0=Object("seat-settings-0-settings-effects").GetComponent<Slider>();var effects1=Object("seat-settings-1-settings-effects").GetComponent<Slider>();
            effects0.value=50;yield return null;
            Assert.That(NativeAudioPreferences.Effects(ground.Profile),Is.EqualTo(50));
            Assert.That(effects1.value,Is.EqualTo(50),"audio volume is shared across local seats");
            NativeAudioPreferences.SetEffects(priorEffects);
            Button("seat-settings-0-settings-section-1").onClick.Invoke();Button("seat-settings-1-settings-section-1").onClick.Invoke();
            Button("seat-settings-0-settings-back").onClick.Invoke();Button("seat-settings-1-settings-back").onClick.Invoke();
            Button("pause-action-1-3").onClick.Invoke();
            Assert.That(Object("pause-exit-yes-1").activeInHierarchy,Is.True);
            Button("pause-exit-no-1").onClick.Invoke();
            Assert.That(Object("pause-action-0-0").activeInHierarchy,Is.True);
            Assert.That(ground.Running,Is.False);
            var original=ground.Session;
            Button("pause-action-1-2").onClick.Invoke();Button("pause-repeat-yes-1").onClick.Invoke();yield return new WaitForFixedUpdate();
            yield return NativeLoadingTestScene.Wait(ground);Assert.That(ground.Running,Is.True);Assert.That(ground.Session,Is.Not.SameAs(original));
            Assert.That(Object("seat-pause-0").activeSelf,Is.False);
        }
        [UnityTest] public IEnumerator GamepadNavigationSubmitsOnlyItsOwnViewport()
        {
            yield return Load();yield return Press(0,GamepadButton.Start);yield return Press(1,GamepadButton.Start);
            yield return Press(0,GamepadButton.DpadDown);yield return Press(0,GamepadButton.South);
            Assert.That(Object("seat-settings-0-settings-image").activeInHierarchy,Is.True);
            Assert.That(Object("pause-action-1-0").activeInHierarchy,Is.True);
            yield return Press(1,GamepadButton.DpadDown);yield return Press(1,GamepadButton.DpadDown);
            yield return Press(1,GamepadButton.South);
            Assert.That(Object("pause-repeat-yes-1").activeInHierarchy,Is.True);
            Assert.That(Object("seat-settings-0-settings-image").activeInHierarchy,Is.True);
            Assert.That(ground.Running,Is.False);
        }
        [UnityTest] public IEnumerator SettingsHaveIdenticalCapabilitiesAndHelpInEveryViewport()
        {
            yield return Load();yield return Press(0,GamepadButton.Start);yield return Press(1,GamepadButton.Start);
            Button("pause-action-0-1").onClick.Invoke();Button("pause-action-1-1").onClick.Invoke();
            var shell=Object("settings-screen");
            var expected=shell.GetComponentsInChildren<Selectable>(true).Select(x=>x.name+":"+x.GetType().Name).OrderBy(x=>x).ToArray();
            for(int seat=0;seat<2;seat++)
            {
                string prefix="seat-settings-"+seat+"-";
                var view=Object(prefix+"settings-screen");
                var actual=view.GetComponentsInChildren<Selectable>(true).Select(x=>x.name.Substring(prefix.Length)+":"+x.GetType().Name).OrderBy(x=>x).ToArray();
                Assert.That(actual,Is.EqualTo(expected),"same complete control set as main menu");
                foreach(int section in new[]{0,1,2,3})Assert.That(Button(prefix+"settings-section-"+section).interactable,Is.True);
                Assert.That(Object(prefix+"settings-image").activeInHierarchy,Is.True);
                Button(prefix+"settings-section-1").onClick.Invoke();Button(prefix+"settings-controls-help").onClick.Invoke();
                var text=Object(prefix+"keyboard-help").GetComponent<Text>();
                Assert.That(text.text,Does.Contain("W A S D"));
                Button(prefix+"settings-help-gamepad").onClick.Invoke();Assert.That(text.text,Does.Contain("Правый триггер"));
                Assert.That(Object(prefix+"help-viewport").GetComponent<ScrollRect>(),Is.Not.Null);
            }
            yield return Press(0,GamepadButton.East);
            Assert.That(Object("seat-settings-0-settings-control").activeInHierarchy,Is.True);
            Assert.That(Object("seat-settings-1-settings-help-page").activeInHierarchy,Is.True,"back only changes its own viewport");
            Assert.That(ground.Running,Is.False);
        }
        [UnityTest] public IEnumerator SectionPreviewChangesOnlyItsOwnViewportWithoutSubmit()
        {
            yield return Load();yield return Press(0,GamepadButton.Start);yield return Press(1,GamepadButton.Start);
            Button("pause-action-0-1").onClick.Invoke();Button("pause-action-1-1").onClick.Invoke();
            yield return Press(0,GamepadButton.DpadDown);
            Assert.That(Object("seat-settings-0-settings-control").activeInHierarchy,Is.True);
            Assert.That(Object("seat-settings-0-settings-image").activeInHierarchy,Is.False);
            Assert.That(Object("seat-settings-1-settings-image").activeInHierarchy,Is.True);
            yield return Press(0,GamepadButton.DpadRight);
            Assert.That(Object("seat-settings-0-settings-control").activeInHierarchy,Is.True);
            Assert.That(ground.Running,Is.False);
        }
        [UnityTest] public IEnumerator MatchDisplayConfirmationHasOneOwnerAndRollsBackOnClose()
        {
            yield return Load();yield return Press(0,GamepadButton.Start);yield return Press(1,GamepadButton.Start);
            Button("pause-action-0-1").onClick.Invoke();Button("pause-action-1-1").onClick.Invoke();
            var before=Screen.fullScreenMode;
            Button("seat-settings-0-settings-display-mode").onClick.Invoke();yield return null;
            Assert.That(Object("seat-settings-0-display-confirmation").activeInHierarchy,Is.True);
            Assert.That(Object("seat-settings-1-display-confirmation").activeInHierarchy,Is.False);
            Assert.That(Button("seat-settings-1-settings-display-mode").interactable,Is.False);
            Button("seat-settings-1-settings-display-mode").onClick.Invoke();
            Assert.That(Object("seat-settings-1-display-confirmation").activeInHierarchy,Is.False,"direct callback cannot replace owner");
            yield return Press(0,GamepadButton.Start);yield return null;
            Assert.That(Object("seat-settings-0-display-confirmation").activeInHierarchy,Is.False);
            Assert.That(Screen.fullScreenMode,Is.EqualTo(before));
            Assert.That(Button("seat-settings-1-settings-display-mode").interactable,Is.True);
            Assert.That(ground.Running,Is.False,"other menu still holds the pause");
            Button("seat-settings-1-settings-display-mode").onClick.Invoke();yield return null;
            Assert.That(Object("seat-settings-1-display-confirmation").activeInHierarchy,Is.True);
            yield return Press(1,GamepadButton.East);
            Assert.That(Object("seat-settings-1-display-confirmation").activeInHierarchy,Is.False);
            Assert.That(Screen.fullScreenMode,Is.EqualTo(before));
        }
        [UnityTest] public IEnumerator DisconnectedSeatCannotResumeThroughTheOtherMenu()
        {
            yield return Load();yield return Press(0,GamepadButton.Start);
            InputSystem.RemoveDevice(pads[1]);yield return null;
            Button("pause-action-0-0").onClick.Invoke();
            Assert.That(ground.Running,Is.False);
            InputSystem.AddDevice(pads[1]);yield return null;
            Button("pause-action-0-0").onClick.Invoke();yield return new WaitForFixedUpdate();
            yield return NativeLoadingTestScene.Wait(ground);Assert.That(ground.Running,Is.True);
        }
    }
}
