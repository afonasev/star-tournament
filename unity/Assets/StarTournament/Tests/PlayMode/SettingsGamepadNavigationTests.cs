using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class SettingsGamepadNavigationTests
    {
        Scene scene;
        Gamepad pad;
        Joystick unsupported;
        ProvingGround ground;
        GamepadLookSettings originalGamepad;
        float originalMouse;
        bool preferencesCaptured;
        Button Button(string name)=>ground.GetComponentsInChildren<Button>(true).Single(button=>button.name==name);
        string Focus=>EventSystem.current.currentSelectedGameObject?.name;

        IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("ProvingGround",LoadSceneMode.Additive);
            scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<ProvingGround>()).Single();
            originalGamepad=GamepadLookSettings.General(ground.Profile);originalMouse=MouseSensitivityPreference.Resolve(ground.Profile);preferencesCaptured=true;
            pad=InputSystem.AddDevice<Gamepad>();yield return null;
            Button("main-action-4").onClick.Invoke();yield return null;
            Assert.That(Focus,Is.EqualTo("settings-section-0"));
        }
        IEnumerator Press(GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(button));
            yield return null;yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
        }
        IEnumerator MoveStick(Vector2 direction)
        {
            InputSystem.QueueStateEvent(pad,new GamepadState { leftStick=direction });
            yield return null;yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
        }
        static void CaptureLtHelp(Canvas canvas)
        {
            var directory=System.Environment.GetEnvironmentVariable("STAR_TOURNAMENT_LT_QA_IMAGES");
            if(string.IsNullOrEmpty(directory))return;
            System.IO.Directory.CreateDirectory(directory);
            var owner=new GameObject("LT help evidence camera");var camera=owner.AddComponent<Camera>();camera.enabled=false;
            var target=new RenderTexture(1920,1080,24);target.Create();camera.targetTexture=target;
            var mode=canvas.renderMode;var previousCamera=canvas.worldCamera;float distance=canvas.planeDistance;
            var active=RenderTexture.active;var pixels=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            try
            {
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,1920,1080),0,0);pixels.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory,"gamepad-lt-help.png"),pixels.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode=mode;canvas.worldCamera=previousCamera;canvas.planeDistance=distance;
                RenderTexture.active=active;camera.targetTexture=null;target.Release();
                Object.Destroy(pixels);Object.Destroy(target);Object.Destroy(owner);Canvas.ForceUpdateCanvases();
            }
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(preferencesCaptured){GamepadLookSettings.SaveGeneral(originalGamepad);MouseSensitivityPreference.Set(ground.Profile,originalMouse);preferencesCaptured=false;}
            if(pad!=null && pad.added)InputSystem.RemoveDevice(pad);
            if(unsupported!=null && unsupported.added)InputSystem.RemoveDevice(unsupported);
            if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest] public IEnumerator SettingsAreFullyNavigableWithGamepad()
        {
            yield return Load();
            yield return Press(GamepadButton.DpadRight);Assert.That(Focus,Is.EqualTo("settings-display-mode"));
            yield return Press(GamepadButton.DpadDown);Assert.That(Focus,Is.EqualTo("settings-resolution"));
            yield return Press(GamepadButton.South);Assert.That(Focus,Is.EqualTo("resolution-choice-0"));
            yield return Press(GamepadButton.DpadDown);Assert.That(Focus,Is.EqualTo("resolution-choice-1"));
            yield return Press(GamepadButton.East);Assert.That(Focus,Is.EqualTo("settings-resolution"));
            yield return Press(GamepadButton.DpadDown);Assert.That(Focus,Is.EqualTo("settings-shadows"));
            yield return Press(GamepadButton.DpadLeft);Assert.That(Focus,Is.EqualTo("settings-section-0"));
            yield return MoveStick(Vector2.down);Assert.That(Focus,Is.EqualTo("settings-section-1"));
            yield return Press(GamepadButton.South);Assert.That(Focus,Is.EqualTo("settings-mouse-sensitivity"));
            var slider=ground.GetComponentsInChildren<Slider>(true).Single(x=>x.name=="settings-mouse-sensitivity");
            slider.value=.5f;float before=slider.value;
            yield return Press(GamepadButton.DpadRight);Assert.That(Focus,Is.EqualTo("settings-mouse-sensitivity"));Assert.That(slider.value,Is.GreaterThan(before));
            yield return Press(GamepadButton.DpadDown);Assert.That(Focus,Is.EqualTo("settings-gamepad-horizontal"));
            yield return Press(GamepadButton.DpadDown);Assert.That(Focus,Is.EqualTo("settings-gamepad-vertical"));
            yield return Press(GamepadButton.DpadDown);Assert.That(Focus,Is.EqualTo("settings-auto-level"));
            var toggle=ground.GetComponentsInChildren<Toggle>(true).Single(x=>x.name=="settings-auto-level");var checkedBefore=toggle.isOn;
            yield return Press(GamepadButton.South);Assert.That(toggle.isOn,Is.EqualTo(!checkedBefore));
            yield return Press(GamepadButton.DpadDown);yield return Press(GamepadButton.South);Assert.That(Focus,Is.EqualTo("settings-help-back"));
            var scroll=ground.GetComponentsInChildren<ScrollRect>(true).Single(x=>x.name=="help-viewport");
            // The complete raster fits the viewport; navigation switches illustrations without cropping.
            Canvas.ForceUpdateCanvases();
            Assert.That(scroll.vertical,Is.False);
            Assert.That(scroll.content.rect.height,Is.EqualTo(scroll.viewport.rect.height).Within(.1f));
            Assert.That(scroll.content.rect.width,Is.EqualTo(scroll.viewport.rect.width).Within(.1f));
            Assert.That(scroll.content.GetComponentInChildren<RawImage>().texture.name,Is.EqualTo("help-keyboard"));
            yield return Press(GamepadButton.DpadDown);Assert.That(Focus,Is.EqualTo("settings-help-keyboard"));
            yield return Press(GamepadButton.DpadRight);Assert.That(Focus,Is.EqualTo("settings-help-gamepad"));
            yield return Press(GamepadButton.South);
            Assert.That(scroll.content.GetComponentInChildren<RawImage>().texture.name,Is.EqualTo("help-gamepad"));
            Assert.That(scroll.content.GetComponentsInChildren<Text>(true).Single(t=>t.name=="help-lt-aim-text").text,
                Does.Contain("короткое нажатие — сброс к горизонту"));
            CaptureLtHelp(scroll.content.GetComponentInParent<Canvas>());
            yield return Press(GamepadButton.DpadLeft);yield return Press(GamepadButton.South);
            Assert.That(scroll.content.GetComponentInChildren<RawImage>().texture.name,Is.EqualTo("help-keyboard"));
            yield return Press(GamepadButton.South);
            Assert.That(scroll.content.GetComponentInChildren<RawImage>().texture.name,Is.EqualTo("help-gamepad"),"Previous arrow wraps from keyboard to gamepad");
            yield return Press(GamepadButton.South);
            Assert.That(scroll.content.GetComponentInChildren<RawImage>().texture.name,Is.EqualTo("help-keyboard"));
            yield return Press(GamepadButton.East);Assert.That(Focus,Is.EqualTo("settings-controls-help"));
            yield return Press(GamepadButton.DpadLeft);Assert.That(Focus,Is.EqualTo("settings-section-1"));
            yield return Press(GamepadButton.DpadDown);Assert.That(Focus,Is.EqualTo("settings-section-2"));
            yield return Press(GamepadButton.South);Assert.That(Focus,Is.EqualTo("settings-fps"));
            Assert.That(ground.GetComponentsInChildren<Text>().Count(t=>t.text.ToUpperInvariant().Contains("ПОКАЗЫВАТЬ FPS")),Is.EqualTo(1));
            yield return Press(GamepadButton.East);Assert.That(Focus,Is.EqualTo("settings-section-2"));
            yield return Press(GamepadButton.DpadUp);Assert.That(Focus,Is.EqualTo("settings-section-1"));
            yield return Press(GamepadButton.South);Assert.That(Focus,Is.EqualTo("settings-mouse-sensitivity"));
            yield return Press(GamepadButton.East);Assert.That(Focus,Is.EqualTo("settings-section-1"));
            yield return Press(GamepadButton.East);Assert.That(Focus,Is.EqualTo("main-action-4"));
        }
        [UnityTest] public IEnumerator ShoulderButtonsCycleOnlyAssignedWeaponOnPress()
        {
            pad=InputSystem.AddDevice<Gamepad>();yield return null;
            var seats=new SeatInputCoordinator();
            Assert.That(seats.Assign(0,pad),Is.True);
            var profile=ProvingProfile.CreateDefault();
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.LeftShoulder));yield return null;
            seats.Capture(profile,.016f);
            Assert.That(seats.Consume(0).SelectWeapon,Is.EqualTo(WeaponSelection.Previous));
            Assert.That(seats.Consume(1).SelectWeapon,Is.EqualTo(WeaponSelection.None));
            yield return null;seats.Capture(profile,.016f);
            Assert.That(seats.Consume(0).SelectWeapon,Is.EqualTo(WeaponSelection.None),"Holding LB must not repeat");
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.RightShoulder));yield return null;
            seats.Capture(profile,.016f);
            Assert.That(seats.Consume(0).SelectWeapon,Is.EqualTo(WeaponSelection.Next));
        }
        [UnityTest] public IEnumerator AllAiOperatorOpensTheSameSettingsAndReturnsToPause()
        {
            yield return Load();Button("settings-back").onClick.Invoke();Button("main-action-0").onClick.Invoke();
            for(int seat=0;seat<ground.LocalSeatCount;seat++)ground.SetSeatAi(seat,true);
            Button("Начать — четыре игрока").onClick.Invoke();yield return new WaitForFixedUpdate();yield return null;
            Assert.That(ground.Running,Is.True);
            yield return Press(GamepadButton.Start);Assert.That(ground.Running,Is.False);
            Button("fallback-settings").onClick.Invoke();yield return null;
            Assert.That(Button("settings-section-0").gameObject.activeInHierarchy,Is.True);
            Assert.That(Focus,Is.EqualTo("settings-section-0"));
            Button("settings-section-1").onClick.Invoke();Button("settings-controls-help").onClick.Invoke();
            Assert.That(Button("settings-help-back").gameObject.activeInHierarchy,Is.True);
            yield return Press(GamepadButton.East);yield return Press(GamepadButton.East);
            Assert.That(Focus,Is.EqualTo("settings-section-1"));yield return Press(GamepadButton.East);
            Assert.That(Focus,Is.EqualTo("fallback-settings"));Assert.That(ground.Running,Is.False);
            Button("Продолжить").onClick.Invoke();yield return new WaitForFixedUpdate();Assert.That(ground.Running,Is.True);
        }
        [UnityTest] public IEnumerator DevicePageShowsSupportedAndUnsupportedControllers()
        {
            yield return Load();unsupported=InputSystem.AddDevice<Joystick>();
            Button("settings-section-1").onClick.Invoke();
            Button("settings-controls-devices").onClick.Invoke();yield return null;
            bool foundPad=false,foundUnsupported=false;
            {
                var rows=ground.GetComponentsInChildren<Text>(true).Where(t=>t.name.StartsWith("device-row-")&&t.gameObject.activeInHierarchy).Select(t=>t.text).ToArray();
                foundPad|=rows.Any(t=>t.Contains(pad.deviceId.ToString())&&t.Contains("Готов к игре"));
                foundUnsupported|=rows.Any(t=>t.Contains(unsupported.deviceId.ToString())&&t.Contains("XInput"));
            }
            Assert.That(foundPad,Is.True);
            Assert.That(foundUnsupported,Is.True);
            Button("devices-back").onClick.Invoke();
            Assert.That(Focus,Is.EqualTo("settings-controls-devices"));
            Button("settings-controls-devices").onClick.Invoke();Assert.That(Focus,Is.EqualTo("devices-back"));
            yield return Press(GamepadButton.East);
            Assert.That(Focus,Is.EqualTo("settings-controls-devices"));
            yield return Press(GamepadButton.East);Assert.That(Focus,Is.EqualTo("settings-section-1"));
        }

        [UnityTest] public IEnumerator GamepadCanCancelDisplayConfirmation()
        {
            yield return Load();
            yield return Press(GamepadButton.DpadRight);Assert.That(Focus,Is.EqualTo("settings-display-mode"));
            yield return Press(GamepadButton.South);
            Assert.That(ground.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="display-confirmation").gameObject.activeInHierarchy,Is.True);
            yield return Press(GamepadButton.East);
            Assert.That(ground.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="display-confirmation").gameObject.activeInHierarchy,Is.False);
            Assert.That(Focus,Is.EqualTo("settings-display-mode"));
        }
        [UnityTest] public IEnumerator SectionFocusShowsParametersWithoutSubmit()
        {
            yield return Load();
            string[] pages={"settings-image","settings-control","settings-interface","settings-audio"};
            for(int section=1;section<4;section++)
            {
                yield return Press(GamepadButton.DpadDown);
                Assert.That(Focus,Is.EqualTo("settings-section-"+section));
                for(int page=0;page<4;page++)
                    Assert.That(ground.GetComponentsInChildren<Transform>(true).Single(x=>x.name==pages[page]).gameObject.activeInHierarchy,Is.EqualTo(page==section));
            }
            yield return Press(GamepadButton.DpadRight);Assert.That(Focus,Is.EqualTo("settings-music"));
            yield return Press(GamepadButton.East);Assert.That(Focus,Is.EqualTo("settings-section-3"));
            yield return MoveStick(Vector2.up);Assert.That(Focus,Is.EqualTo("settings-section-2"));
            Assert.That(ground.GetComponentsInChildren<Toggle>(true).Single(x=>x.name=="settings-fps").gameObject.activeInHierarchy,Is.True);
        }
        [UnityTest] public IEnumerator AudioSettingsPersistAndRemainGamepadNavigable()
        {
            const string musicKey="StarTournament.Audio.MusicPercent",effectsKey="StarTournament.Audio.EffectsPercent";
            bool hadMusic=PlayerPrefs.HasKey(musicKey),hadEffects=PlayerPrefs.HasKey(effectsKey);
            int oldMusic=PlayerPrefs.GetInt(musicKey),oldEffects=PlayerPrefs.GetInt(effectsKey);
            try
            {
                yield return Load();
                yield return Press(GamepadButton.DpadDown);
                yield return Press(GamepadButton.DpadDown);
                yield return Press(GamepadButton.DpadDown);
                Assert.That(Focus,Is.EqualTo("settings-section-3"));
                yield return Press(GamepadButton.South);Assert.That(Focus,Is.EqualTo("settings-music"));
                var musicSlider=ground.GetComponentsInChildren<Slider>(true).Single(x=>x.name=="settings-music");
                musicSlider.value=50;
                yield return Press(GamepadButton.DpadRight);Assert.That(Focus,Is.EqualTo("settings-music"));
                Assert.That(NativeAudioPreferences.Music(ground.Profile),Is.EqualTo(55));
                yield return Press(GamepadButton.DpadDown);Assert.That(Focus,Is.EqualTo("settings-effects"));
                var effectsSlider=ground.GetComponentsInChildren<Slider>(true).Single(x=>x.name=="settings-effects");
                effectsSlider.value=50;
                yield return Press(GamepadButton.DpadRight);Assert.That(Focus,Is.EqualTo("settings-effects"));
                Assert.That(NativeAudioPreferences.Effects(ground.Profile),Is.EqualTo(55));
            }
            finally
            {
                if(hadMusic)PlayerPrefs.SetInt(musicKey,oldMusic);else PlayerPrefs.DeleteKey(musicKey);
                if(hadEffects)PlayerPrefs.SetInt(effectsKey,oldEffects);else PlayerPrefs.DeleteKey(effectsKey);
                PlayerPrefs.Save();
            }
        }
    }
}
