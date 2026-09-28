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
        Button Button(string name)=>ground.GetComponentsInChildren<Button>(true).Single(button=>button.name==name);
        GameObject Object(string name)=>ground.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name).gameObject;
        IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("ProvingGround",LoadSceneMode.Additive);
            scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<ProvingGround>()).Single();
            Button("main-action-0").onClick.Invoke();NativeSetupFixture.UnboundHumans(ground);
            while(ground.LocalSeatCount>2)Button("seats-minus").onClick.Invoke();
            for(int seat=0;seat<2;seat++)
            {
                pads[seat]=InputSystem.AddDevice<Gamepad>();
                InputSystem.QueueStateEvent(pads[seat],new GamepadState().WithButton(GamepadButton.North));
                yield return null;yield return null;
                InputSystem.QueueStateEvent(pads[seat],new GamepadState());yield return null;
                Button("identity-guest").onClick.Invoke();
            }
            Button("Начать — четыре игрока").onClick.Invoke();yield return new WaitForFixedUpdate();yield return null;
            Assert.That(ground.Running,Is.True);
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
        }
        [UnityTest] public IEnumerator TwoSeatMenusKeepOneFrozenMatchUntilTheLastCloses()
        {
            yield return Load();
            yield return Press(0,GamepadButton.Start);
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
            Assert.That(ground.Running,Is.True);
            Assert.That(Object("seat-pause-0").activeSelf,Is.False);
        }
        [UnityTest] public IEnumerator PersonalSettingsAndGlobalConfirmationStayScoped()
        {
            yield return Load();yield return Press(0,GamepadButton.Start);yield return Press(1,GamepadButton.Start);
            Button("pause-action-0-1").onClick.Invoke();Button("pause-action-1-1").onClick.Invoke();
            var before0=Object("seat-pause-0").GetComponentsInChildren<Text>(true).Single(t=>t.name=="values").text;
            var before1=Object("seat-pause-1").GetComponentsInChildren<Text>(true).Single(t=>t.name=="values").text;
            Button("pause-setting-0-0").onClick.Invoke();
            var after0=Object("seat-pause-0").GetComponentsInChildren<Text>(true).Single(t=>t.name=="values").text;
            var after1=Object("seat-pause-1").GetComponentsInChildren<Text>(true).Single(t=>t.name=="values").text;
            Assert.That(after0,Is.Not.EqualTo(before0));Assert.That(after1,Is.EqualTo(before1));
            Button("pause-setting-0-3").onClick.Invoke();Button("pause-setting-1-3").onClick.Invoke();
            Button("pause-action-1-3").onClick.Invoke();
            Assert.That(Object("pause-exit-yes-1").activeInHierarchy,Is.True);
            Button("pause-exit-no-1").onClick.Invoke();
            Assert.That(Object("pause-action-0-0").activeInHierarchy,Is.True);
            Assert.That(ground.Running,Is.False);
            var original=ground.Session;
            Button("pause-action-1-2").onClick.Invoke();Button("pause-repeat-yes-1").onClick.Invoke();yield return new WaitForFixedUpdate();
            Assert.That(ground.Running,Is.True);Assert.That(ground.Session,Is.Not.SameAs(original));
            Assert.That(Object("seat-pause-0").activeSelf,Is.False);
        }
        [UnityTest] public IEnumerator GamepadNavigationSubmitsOnlyItsOwnViewport()
        {
            yield return Load();yield return Press(0,GamepadButton.Start);yield return Press(1,GamepadButton.Start);
            yield return Press(0,GamepadButton.DpadDown);yield return Press(0,GamepadButton.South);
            Assert.That(Object("pause-setting-0-0").activeInHierarchy,Is.True);
            Assert.That(Object("pause-action-1-0").activeInHierarchy,Is.True);
            yield return Press(1,GamepadButton.DpadDown);yield return Press(1,GamepadButton.DpadDown);
            yield return Press(1,GamepadButton.South);
            Assert.That(Object("pause-repeat-yes-1").activeInHierarchy,Is.True);
            Assert.That(Object("pause-setting-0-0").activeInHierarchy,Is.True);
            Assert.That(ground.Running,Is.False);
        }
        [UnityTest] public IEnumerator DisconnectedSeatCannotResumeThroughTheOtherMenu()
        {
            yield return Load();yield return Press(0,GamepadButton.Start);
            InputSystem.RemoveDevice(pads[1]);yield return null;
            Button("pause-action-0-0").onClick.Invoke();
            Assert.That(ground.Running,Is.False);
            InputSystem.AddDevice(pads[1]);yield return null;
            Button("pause-action-0-0").onClick.Invoke();yield return new WaitForFixedUpdate();
            Assert.That(ground.Running,Is.True);
        }
    }
}
