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
    public sealed class NativeBotSeatsTests
    {
        Scene scene;
        ProvingGround ground;
        Gamepad[] pads;
        Keyboard syntheticKeyboard;
        InputSettings.EditorInputBehaviorInPlayMode priorEditorInput;
        InputSettings.BackgroundBehavior priorBackground;
        bool hadLastMap;string priorLastMap;

        Button Button(string name) => ground.GetComponentsInChildren<Button>(true).Single(button => button.name == name);
        IEnumerator Load()
        {
            hadLastMap=PlayerPrefs.HasKey(ProvingGround.LastPlayedMapPreferenceKey);priorLastMap=PlayerPrefs.GetString(ProvingGround.LastPlayedMapPreferenceKey);
            // Batchmode has no focused GameView. Route only this test's synthetic keyboard to runtime.
            priorEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            priorBackground=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            yield return NativeLoadingTestScene.Load();
            scene = SceneManager.GetSceneByName("ProvingGround");
            yield return null;
            ground = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ProvingGround>()).Single();
            Button("main-action-0").onClick.Invoke();NativeSetupFixture.UnboundHumans(ground);
            Button("setup-next").onClick.Invoke();Button("setup-next").onClick.Invoke();
            // Use our own current device: batchmode may keep an unrelated keyboard alive.
            syntheticKeyboard = InputSystem.AddDevice<Keyboard>();
        }
        IEnumerator Join(Gamepad pad)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.North));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
            var input=(SeatInputCoordinator)typeof(ProvingGround).GetField("input",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(ground);
            int seat=Enumerable.Range(0,ground.LocalSeatCount).Single(i=>input.DeviceAt(i)==pad);
            Button("roster-card-"+seat).onClick.Invoke();Button("roster-identity").onClick.Invoke();
            Button("roster-choice-guest").onClick.Invoke();Button("roster-done").onClick.Invoke();
        }
        IEnumerator PauseEscape()
        {
            Assert.That(syntheticKeyboard, Is.Not.Null);
            InputSystem.QueueStateEvent(syntheticKeyboard, new KeyboardState());
            yield return null;
            InputSystem.QueueStateEvent(syntheticKeyboard, new KeyboardState(Key.Escape));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(syntheticKeyboard, new KeyboardState());
            yield return null;
            Assert.That(ground.Running, Is.False, "unassigned operator Escape pauses all-AI");
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (pads != null) foreach (var pad in pads) if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
            if (syntheticKeyboard != null && syntheticKeyboard.added) InputSystem.RemoveDevice(syntheticKeyboard);
            if (scene.IsValid()) yield return SceneManager.UnloadSceneAsync(scene);
            InputSystem.settings.editorInputBehaviorInPlayMode=priorEditorInput;
            InputSystem.settings.backgroundBehavior=priorBackground;
            if(hadLastMap)PlayerPrefs.SetString(ProvingGround.LastPlayedMapPreferenceKey,priorLastMap);else PlayerPrefs.DeleteKey(ProvingGround.LastPlayedMapPreferenceKey);PlayerPrefs.Save();
        }

        [UnityTest] public IEnumerator SparseHumanAllAiOperatorAndFrozenLifecycleUseOrdinarySetup()
        {
            yield return Load();
            // P3 alone owns a device; the other displayed views are genuine bots.
            ground.SetSeatAi(0, true); ground.SetSeatDifficulty(0, 0);
            ground.SetSeatAi(1, true); ground.SetSeatDifficulty(1, 1);
            ground.SetSeatAi(3, true); ground.SetSeatDifficulty(3, 2);
            pads = new[] { InputSystem.AddDevice<Gamepad>() };
            yield return Join(pads[0]);
            Button("Начать — четыре игрока").onClick.Invoke(); yield return new WaitForFixedUpdate(); yield return null;
            yield return NativeLoadingTestScene.Wait(ground);Assert.That(ground.Running, Is.True);
            Assert.That(ground.Composition.ParticipantAt(2), Is.EqualTo(2));
            Assert.That(ground.Composition.Participant(2).Kind, Is.EqualTo(NativeParticipantKind.LocalHuman));
            Assert.That(ground.GetComponentsInChildren<Camera>(true).Count(camera => camera.enabled), Is.EqualTo(4));
            Assert.That(ground.BotDriver.Planner(0), Is.Not.Null); Assert.That(ground.BotDriver.Planner(1), Is.Not.Null); Assert.That(ground.BotDriver.Planner(3), Is.Not.Null);
            ground.SendMessage("Pause", "test setup transition"); Button("В главное меню").onClick.Invoke(); yield return null;
            Button("main-action-0").onClick.Invoke();NativeSetupFixture.UnboundHumans(ground);
            Button("setup-next").onClick.Invoke();Button("setup-next").onClick.Invoke();
            InputSystem.RemoveDevice(pads[0]); pads[0] = null;

            // An all-AI ordinary match has no hidden device prerequisite and keeps one TrooperVisual implementation per body/view.
            for (var seat = 0; seat < 4; seat++) ground.SetSeatAi(seat, true);
            ground.SetMatchMode(NativeMatchMode.Teams);
            ground.SetTeam(0, NativeTeam.TeamA); ground.SetTeam(1, NativeTeam.TeamB);
            ground.SetTeam(2, NativeTeam.TeamA); ground.SetTeam(3, NativeTeam.TeamB);
            Button("Начать — четыре игрока").onClick.Invoke(); yield return new WaitForFixedUpdate(); yield return null;
            yield return NativeLoadingTestScene.Wait(ground);Assert.That(ground.Running, Is.True); Assert.That(ground.BotDriver, Is.Not.Null);
            Assert.That(ground.GetComponentsInChildren<TrooperVisual>(true).Length, Is.EqualTo(8), "views reuse TrooperVisual rather than a bot-only presentation path");
            var frozen = JsonUtility.ToJson(ground.Composition.Read()); var session = ground.Session; var driver = ground.BotDriver;
            yield return PauseEscape(); var clock = ground.Session.Time; var ticks = driver.Ticks;
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(ground.Session.Time, Is.EqualTo(clock)); Assert.That(driver.Ticks, Is.EqualTo(ticks));
            Button("Повторить матч").onClick.Invoke(); yield return null;
            yield return NativeLoadingTestScene.Wait(ground);Assert.That(ground.Running, Is.True); Assert.That(ground.Session, Is.Not.SameAs(session)); Assert.That(ground.BotDriver, Is.Not.SameAs(driver));
            Assert.That(JsonUtility.ToJson(ground.Composition.Read()), Is.EqualTo(frozen));
            yield return PauseEscape(); Button("В главное меню").onClick.Invoke(); yield return null;
            Button("main-action-0").onClick.Invoke();
            Assert.That(ground.BotDriver, Is.Null);Assert.That(ground.LocalSeatCount,Is.EqualTo(1));Assert.That(ground.SetupBotCount,Is.EqualTo(1));
            Assert.That(ground.SetupComposition().Participant(0).Kind,Is.EqualTo(NativeParticipantKind.LocalHuman));Assert.That(ground.SetupComposition().Participant(1).Kind,Is.EqualTo(NativeParticipantKind.Bot));
        }

        [UnityTest] public IEnumerator ChangingSeatKindReleasesDeviceAndSwitchesTheNextHumanOwner()
        {
            yield return Load();
            while (ground.LocalSeatCount > 2) Button("seats-minus").onClick.Invoke();
            pads = new[] { InputSystem.AddDevice<Gamepad>(), InputSystem.AddDevice<Gamepad>() };
            yield return Join(pads[0]);
            ground.SetSeatAi(0, true);
            ground.SetSeatAi(1, false);
            yield return Join(pads[1]);
            Button("Начать — четыре игрока").onClick.Invoke(); yield return new WaitForFixedUpdate(); yield return null;
            yield return NativeLoadingTestScene.Wait(ground);Assert.That(ground.Running, Is.True, "device released from an AI switch can serve the remaining human");
            Assert.That(ground.Composition.Participant(0).Kind, Is.EqualTo(NativeParticipantKind.Bot));
            Assert.That(ground.Composition.Participant(1).Kind, Is.EqualTo(NativeParticipantKind.LocalHuman));
            var before = ground.Session.Pose(1).Position;
            InputSystem.QueueStateEvent(pads[1], new GamepadState { leftStick = Vector2.right });
            yield return null; // Capture the new input on a rendered frame before stepping simulation.
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.That(Vector3.Distance(ground.Session.Pose(1).Position, before), Is.GreaterThan(.01f), "replacement device owns the switched human seat");
            var disconnected = pads[1];
            InputSystem.RemoveDevice(disconnected);
            yield return null; yield return null;
            Assert.That(ground.Running, Is.False, "human disconnect pauses the mixed match");
            Assert.That(Button("Продолжить").interactable, Is.False, "disconnect blocks Resume until reconnect or exit");
            InputSystem.AddDevice(disconnected);
            yield return null; yield return null;
            Assert.That(Button("Продолжить").interactable, Is.True, "the same human device reconnect enables explicit Resume");
            Button("Продолжить").onClick.Invoke(); yield return new WaitForFixedUpdate();
            yield return NativeLoadingTestScene.Wait(ground);Assert.That(ground.Running, Is.True, "a reconnecting human explicitly resumes the paused match");
        }
    }
}
