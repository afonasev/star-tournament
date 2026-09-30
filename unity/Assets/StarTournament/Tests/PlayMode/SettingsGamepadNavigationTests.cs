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
        Button Button(string name)=>ground.GetComponentsInChildren<Button>(true).Single(button=>button.name==name);
        string Focus=>EventSystem.current.currentSelectedGameObject?.name;

        IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("ProvingGround",LoadSceneMode.Additive);
            scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<ProvingGround>()).Single();
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
            yield return Press(GamepadButton.South);Assert.That(Focus,Is.EqualTo("settings-sensitivity-minus"));
            yield return Press(GamepadButton.DpadRight);Assert.That(Focus,Is.EqualTo("settings-sensitivity-plus"));
            yield return Press(GamepadButton.DpadLeft);Assert.That(Focus,Is.EqualTo("settings-sensitivity-minus"));
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
                yield return Press(GamepadButton.South);Assert.That(Focus,Is.EqualTo("music-minus"));
                int music=NativeAudioPreferences.Music(ground.Profile),effects=NativeAudioPreferences.Effects(ground.Profile);
                yield return Press(GamepadButton.DpadRight);Assert.That(Focus,Is.EqualTo("music-plus"));
                yield return Press(GamepadButton.South);
                Assert.That(NativeAudioPreferences.Music(ground.Profile),Is.EqualTo(Mathf.Min(100,music+5)));
                yield return Press(GamepadButton.DpadDown);Assert.That(Focus,Is.EqualTo("effects-plus"));
                yield return Press(GamepadButton.South);
                Assert.That(NativeAudioPreferences.Effects(ground.Profile),Is.EqualTo(Mathf.Min(100,effects+5)));
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
