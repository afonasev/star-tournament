using System.Collections;
using System.Linq;
using System.Reflection;
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
    public sealed class NativeAudioRoutingTests
    {
        Scene scene;
        Gamepad pad;
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);
            if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);
        }
        [UnityTest] public IEnumerator ConfirmedRifleShotSelectsTheChosenRuntimeClip()
        {
            const string effectsKey="StarTournament.Audio.EffectsPercent";
            bool hadEffects=PlayerPrefs.HasKey(effectsKey);int previousEffects=PlayerPrefs.GetInt(effectsKey);
            NativeAudioPreferences.SetEffects(80);
            try
            {
                yield return SceneManager.LoadSceneAsync("ProvingGround",LoadSceneMode.Additive);
                scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
                var ground=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<ProvingGround>()).Single();
                ground.GetComponentsInChildren<Button>(true).Single(b=>b.name=="main-action-0").onClick.Invoke();
                pad=InputSystem.AddDevice<Gamepad>();
                ground.StartCombatReview(new[]{pad},ensureOpponent:true);
                // Keep the Editor run inaudible at the AudioListener while exercising routing.
                var audio=(NativeGameAudio)typeof(ProvingGround).GetField("gameAudio",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ground);
                audio.SetDiagnosticMute(false);
                var sourceRoot=ground.transform.Find("native-game-audio");
                Assert.That(sourceRoot,Is.Not.Null);
                Assert.That(Resources.Load<AudioClip>("Audio/rifle"),Is.Not.Null);
                yield return new WaitForSeconds(.4f);
                int before=ground.Session.ShotCount;
                bool fired=false;ground.Session.ShotResolved+=notice=>fired|=notice.Shooter==0&&notice.Weapon==WeaponId.Rifle;
                var actions=new LocalAction[ground.Session.ParticipantCount];actions[0].Fire=true;actions[0].FireHeld=true;
                ground.Session.Tick(actions,Time.fixedDeltaTime);
                Assert.That(ground.Session.ShotCount,Is.GreaterThan(before));
                Assert.That(fired,Is.True);
                Assert.That(sourceRoot.GetComponentsInChildren<AudioSource>().Any(s=>s.name.StartsWith("voice-")&&s.clip!=null&&s.clip.name=="rifle"),Is.True);
            }
            finally
            {
                if(hadEffects)PlayerPrefs.SetInt(effectsKey,previousEffects);else PlayerPrefs.DeleteKey(effectsKey);
                PlayerPrefs.Save();
            }
        }
        [UnityTest] public IEnumerator MouseHoverIsSilentButGamepadNavigationStillSounds()
        {
            const string effectsKey="StarTournament.Audio.EffectsPercent";
            bool hadEffects=PlayerPrefs.HasKey(effectsKey);int previousEffects=PlayerPrefs.GetInt(effectsKey);
            float previousListenerVolume=AudioListener.volume;
            NativeAudioPreferences.SetEffects(80);
            AudioListener.volume=0;
            try
            {
                yield return SceneManager.LoadSceneAsync("ProvingGround",LoadSceneMode.Additive);
                scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
                var ground=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<ProvingGround>()).Single();
                var audio=(NativeGameAudio)typeof(ProvingGround).GetField("gameAudio",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ground);
                audio.SetDiagnosticMute(false);
                var source=ground.transform.Find("native-game-audio/menu-ui").GetComponent<AudioSource>();
                source.Stop();source.clip=null;
                var battle=ground.GetComponentsInChildren<Button>(true).Single(b=>b.name=="main-action-0");
                ExecuteEvents.Execute(battle.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);
                yield return null;
                Assert.That(source.clip,Is.Null,"Hovering a menu button must not trigger navigation audio");
                pad=InputSystem.AddDevice<Gamepad>();
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadDown));
                yield return null;yield return null;
                InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
                Assert.That(source.clip?.name,Is.EqualTo("menu_move"));
            }
            finally
            {
                AudioListener.volume=previousListenerVolume;
                if(hadEffects)PlayerPrefs.SetInt(effectsKey,previousEffects);else PlayerPrefs.DeleteKey(effectsKey);
                PlayerPrefs.Save();
            }
        }
    }
}
