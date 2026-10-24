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
    public sealed class NativeBotSetupTests
    {
        Scene scene;ProvingGround ground;Gamepad pad;
        InputSettings.EditorInputBehaviorInPlayMode priorInput;InputSettings.BackgroundBehavior priorBackground;
        [SetUp] public void Setup()
        {
            priorInput=InputSystem.settings.editorInputBehaviorInPlayMode;priorBackground=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        }
        Button B(string name)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        [UnityTearDown] public IEnumerator Cleanup(){if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);InputSystem.settings.editorInputBehaviorInPlayMode=priorInput;InputSystem.settings.backgroundBehavior=priorBackground;}
        [UnityTest] public IEnumerator OrdinarySoloStartMixedTeamsRepeatPauseAndExit()
        {
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ProvingGround>()).Single();
            pad=InputSystem.AddDevice<Gamepad>();yield return null;
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(B("main-action-0").gameObject);
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.South));yield return null;yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
            Assert.That(ground.LocalSeatCount,Is.EqualTo(1));Assert.That(ground.SetupBotCount,Is.Zero);
            Assert.That(B("Начать — четыре игрока").interactable,Is.False,"One participant is invalid even with a device");
            for(int i=0;i<7;i++)B("bot-add").onClick.Invoke();
            B("bot-difficulty-0").onClick.Invoke();ground.SetMatchMode(NativeMatchMode.Teams);
            for(int i=0;i<7;i++)ground.SetBotTeam(i,NativeTeam.TeamA);
            Assert.That(B("Начать — четыре игрока").interactable,Is.False);ground.SetBotTeam(0,NativeTeam.TeamB);
            Assert.That(B("bot-add").interactable,Is.False);Assert.That(B("seats-plus").interactable,Is.False);
            Assert.That(B("Начать — четыре игрока").interactable,Is.True);B("Начать — четыре игрока").onClick.Invoke();yield return null;
            yield return NativeLoadingTestScene.Wait(ground);Assert.That(ground.Running,Is.True);Assert.That(ground.BotDriver,Is.Not.Null);Assert.That(ground.Session.ParticipantCount,Is.EqualTo(8));
            Assert.That(ground.GetComponentsInChildren<Camera>().Count(c=>c.enabled),Is.EqualTo(1));
            Assert.That(ground.GetComponentsInChildren<TrooperVisual>().Length,Is.EqualTo(9));
            var frozen=JsonUtility.ToJson(ground.Composition.Read());var names=ground.Composition.Read().Participants.Select(p=>p.Name).ToArray();var session=ground.Session;var driver=ground.BotDriver;
            ground.SendMessage("Pause","Test pause");var time=session.Time;var ticks=driver.Ticks;yield return new WaitForFixedUpdate();
            ground.RemoveBot(0);ground.SetBotDifficulty(0,0);Assert.That(ground.SetupBotCount,Is.EqualTo(7));
            Assert.That(session.Time,Is.EqualTo(time));Assert.That(driver.Ticks,Is.EqualTo(ticks));
            B("Повторить матч").onClick.Invoke();yield return null;yield return NativeLoadingTestScene.Wait(ground);
            Assert.That(ground.BotDriver,Is.Not.SameAs(driver));Assert.That(ground.Session,Is.Not.SameAs(session));Assert.That(JsonUtility.ToJson(ground.Composition.Read()),Is.EqualTo(frozen));
            ground.SendMessage("Pause","Exit");B("В главное меню").onClick.Invoke();yield return null;
            B("main-action-0").onClick.Invoke();
            yield return null;
            Assert.That(ground.BotDriver,Is.Null);Assert.That(ground.LocalSeatCount,Is.EqualTo(1));Assert.That(ground.SetupBotCount,Is.EqualTo(7),"Session draft survives match/Repeat/menu");
            Assert.That(ground.SetupMode,Is.EqualTo(NativeMatchMode.Teams));
            var setup=(NativeBotSetup)typeof(ProvingGround).GetField("botSetup",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(ground);
            Assert.That(setup.Build(1,NativeMatchMode.Teams,new[]{NativeTeam.TeamA},false).Read().Participants.Skip(1).Select(p=>p.Name),Is.EqualTo(names.Skip(1)));
            for(int i=0;i<7;i++){B("bot-remove-0").onClick.Invoke();Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.activeInHierarchy,Is.True,"Removal keeps a navigable control selected");}Assert.That(B("Начать — четыре игрока").interactable,Is.False);
            B("seats-plus").onClick.Invoke();ground.SetMatchMode(NativeMatchMode.Ffa);B("Диагностика четырёх камер · без управления").onClick.Invoke();yield return null;yield return NativeLoadingTestScene.Wait(ground);
            yield return NativeLoadingTestScene.Wait(ground);Assert.That(ground.Running,Is.True);Assert.That(ground.BotDriver,Is.Null);Assert.That(ground.Session.ParticipantCount,Is.EqualTo(2));
        }
    }
}
