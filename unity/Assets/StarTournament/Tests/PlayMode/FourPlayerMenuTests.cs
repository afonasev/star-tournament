using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class FourPlayerMenuTests
    {
        readonly Gamepad[] pads=new Gamepad[4];
        Scene scene;
        [UnityTest]
        public IEnumerator FramesStayWithAssignedSeatAndEdgesAreConsumedOnce()
        {
            var seats=new SeatInputCoordinator();
            for(int i=0;i<4;i++) { pads[i]=InputSystem.AddDevice<Gamepad>();seats.Assign(i,pads[i]); }
            yield return null;
            seats.Capture(ProvingProfile.CreateDefault(),Time.fixedDeltaTime);
            InputSystem.QueueStateEvent(pads[2],new GamepadState{leftStick=Vector2.right,rightTrigger=1});
            yield return null;
            seats.Capture(ProvingProfile.CreateDefault(),Time.fixedDeltaTime);
            for(int i=0;i<4;i++)
            {
                var frame=seats.Consume(i);
                Assert.That(frame.Move.x,Is.EqualTo(i==2?1:0));Assert.That(frame.Fire,Is.EqualTo(i==2));
            }
            Assert.That(seats.Consume(2).Fire,Is.False);
            seats.Clear();Assert.That(seats.Consume(2).Move,Is.EqualTo(Vector2.zero));
        }
        IEnumerator PressBotMenu(Gamepad pad,GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(button));yield return null;yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
        }
        [UnityTest] public IEnumerator NewBotsConfirmDefaultDifficultyWithOneSubmit()
        {
            yield return SceneManager.LoadSceneAsync("ProvingGround",LoadSceneMode.Additive);
            scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            var ground=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<ProvingGround>()).Single();
            var pad=pads[0]=InputSystem.AddDevice<Gamepad>();
            System.Func<string,UnityEngine.UI.Button> button=name=>ground.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b=>b.name==name);
            var events=UnityEngine.EventSystems.EventSystem.current;
            events.SetSelectedGameObject(button("main-action-0").gameObject);yield return PressBotMenu(pad,GamepadButton.South);
            button("setup-next").onClick.Invoke();button("setup-next").onClick.Invoke();
            for(int i=0;i<2;i++)
            {
                int before=ground.SetupBotCount;
                events.SetSelectedGameObject(button("roster-add-bot").gameObject);yield return PressBotMenu(pad,GamepadButton.South);
                Assert.That(ground.SetupBotCount,Is.EqualTo(before+1));
                Assert.That(events.currentSelectedGameObject.name,Is.EqualTo("roster-done"));
                Assert.That(ground.SetupComposition().Read().Participants.Last().Difficulty,Is.EqualTo((int)NativeBotDifficulty.Normal));
                yield return PressBotMenu(pad,GamepadButton.South);
                Assert.That(button("roster-done").gameObject.activeInHierarchy,Is.False);
                Assert.That(events.currentSelectedGameObject.name,Is.EqualTo("roster-card-"+(ground.LocalSeatCount+before)));
            }
            button("roster-card-"+(ground.LocalSeatCount+ground.SetupBotCount-1)).onClick.Invoke();
            Assert.That(events.currentSelectedGameObject.name,Is.EqualTo("roster-identity"));
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach(var pad in pads) if(pad!=null && pad.added)InputSystem.RemoveDevice(pad);
            if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);
        }
        [UnityTest]
        public IEnumerator FpsSettingPersistsAndKeepsSamplingWhilePaused()
        {
            bool hadPreference=PlayerPrefs.HasKey(FpsDisplay.PreferenceKey);
            int original=PlayerPrefs.GetInt(FpsDisplay.PreferenceKey);
            GameObject root=null;
            try
            {
                PlayerPrefs.DeleteKey(FpsDisplay.PreferenceKey);
                root=new GameObject("fps-test",typeof(RectTransform));
                var label=root.AddComponent<UnityEngine.UI.Text>();
                var display=root.AddComponent<FpsDisplay>();
                display.Initialize(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"),24,label);
                Assert.That(display.Visible,Is.True);
                var overlay=root.transform.Find("fps-overlay").gameObject;
                Assert.That(overlay.activeSelf,Is.False,"Menus hide FPS without disabling the preference");
                display.SetMatchActive(true);
                Assert.That(overlay.activeSelf,Is.True);
                display.SetMatchActive(false);
                display.Toggle();
                display.Toggle();
                Assert.That(display.Visible,Is.True);
                Assert.That(overlay.activeSelf,Is.False,"Enabling the setting in menus must not reveal FPS");
                display.SetMatchActive(true);
                Assert.That(overlay.activeSelf,Is.True,"The next match restores the enabled counter");
                display.Toggle();
                Assert.That(display.Visible,Is.False);
                Assert.That(root.transform.Find("fps-overlay").gameObject.activeSelf,Is.False);
                Object.Destroy(root); yield return null;
                root=new GameObject("fps-test-reloaded",typeof(RectTransform));
                label=root.AddComponent<UnityEngine.UI.Text>();
                display=root.AddComponent<FpsDisplay>();
                display.Initialize(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"),24,label);
                Assert.That(display.Visible,Is.False);
                display.Toggle();
                Time.timeScale=0;
                yield return new WaitForSecondsRealtime(0.7f);
                var counter=root.transform.Find("fps-overlay/fps-value").GetComponent<UnityEngine.UI.Text>();
                Assert.That(counter.text,Does.StartWith("FPS "));
                Assert.That(counter.text,Does.Not.Contain("—"));
            }
            finally
            {
                Time.timeScale=1;
                if(root)Object.Destroy(root);
                if(hadPreference)PlayerPrefs.SetInt(FpsDisplay.PreferenceKey,original);
                else PlayerPrefs.DeleteKey(FpsDisplay.PreferenceKey);
                PlayerPrefs.Save();
            }
        }
        [UnityTest]
        public IEnumerator FourGamepadsCanJoinStartPauseAndResumeWithoutMouse()
        {
            yield return SceneManager.LoadSceneAsync("ProvingGround",LoadSceneMode.Additive);
            scene=SceneManager.GetSceneByName("ProvingGround");
            yield return null;
            var ground=Object.FindFirstObjectByType<ProvingGround>(); Assert.That(ground,Is.Not.Null);
            pads[0]=InputSystem.AddDevice<Gamepad>();
            InputSystem.QueueStateEvent(pads[0],new GamepadState().WithButton(GamepadButton.South));yield return null;yield return null;
            InputSystem.QueueStateEvent(pads[0],new GamepadState());yield return null;
            Assert.That(ground.LocalSeatCount,Is.EqualTo(1));ground.RemoveBot(0);
            Assert.That(ground.Running,Is.False);
            var next=ground.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b=>b.name=="setup-next");
            next.onClick.Invoke();next.onClick.Invoke();
            for(int i=1;i<4;i++)
            {
                pads[i]=InputSystem.AddDevice<Gamepad>();
                InputSystem.QueueStateEvent(pads[i],new GamepadState().WithButton(GamepadButton.North));
                yield return null;yield return null;
                InputSystem.QueueStateEvent(pads[i],new GamepadState());yield return null;
                ground.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b=>b.name=="roster-identity").onClick.Invoke();
                ground.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b=>b.name=="roster-choice-guest"&&b.gameObject.activeInHierarchy).onClick.Invoke();
                ground.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b=>b.name=="roster-done").onClick.Invoke();
            }
            Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.name,Is.EqualTo("roster-card-3"));
            InputSystem.QueueStateEvent(pads[0],new GamepadState().WithButton(GamepadButton.DpadDown));yield return null;yield return null;
            InputSystem.QueueStateEvent(pads[0],new GamepadState());yield return null;
            Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.name,Is.EqualTo("Начать — четыре игрока"));
            InputSystem.QueueStateEvent(pads[0],new GamepadState().WithButton(GamepadButton.South));
            yield return null;yield return null;
            Assert.That(ground.Running,Is.True,"Gamepad Submit must activate the selected four-player Start button");
            InputSystem.QueueStateEvent(pads[0],new GamepadState());yield return null;
            InputSystem.QueueStateEvent(pads[0],new GamepadState().WithButton(GamepadButton.Start));
            yield return null;yield return null;Assert.That(ground.Running,Is.False);
            InputSystem.QueueStateEvent(pads[0],new GamepadState());yield return null;
            InputSystem.QueueStateEvent(pads[0],new GamepadState().WithButton(GamepadButton.South));
            yield return null;yield return null;Assert.That(ground.Running,Is.True,"Resume must be reachable without a mouse");
            InputSystem.RemoveDevice(pads[3]);yield return null;yield return null;
            Assert.That(ground.Running,Is.False,"Disconnect pauses the whole proving ground");
        }
    }
}
