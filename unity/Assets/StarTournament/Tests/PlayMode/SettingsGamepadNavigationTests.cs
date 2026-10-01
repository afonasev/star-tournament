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
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(preferencesCaptured){GamepadLookSettings.SaveGeneral(originalGamepad);MouseSensitivityPreference.Set(ground.Profile,originalMouse);preferencesCaptured=false;}
            if(pad!=null && pad.added)InputSystem.RemoveDevice(pad);
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
            yield return Press(GamepadButton.East);Assert.That(Focus,Is.EqualTo("settings-controls-help"));
            yield return Press(GamepadButton.DpadLeft);Assert.That(Focus,Is.EqualTo("settings-section-1"));
            yield return Press(GamepadButton.DpadDown);Assert.That(Focus,Is.EqualTo("settings-section-2"));
            yield return Press(GamepadButton.South);Assert.That(Focus,Is.EqualTo("settings-fps"));
            yield return Press(GamepadButton.East);Assert.That(Focus,Is.EqualTo("main-action-4"));
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
