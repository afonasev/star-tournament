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
        Button B(string name)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        [UnityTearDown] public IEnumerator Cleanup(){if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);}
        [UnityTest] public IEnumerator OrdinarySoloStartMixedTeamsRepeatPauseAndExit()
        {
            yield return SceneManager.LoadSceneAsync("ProvingGround",LoadSceneMode.Additive);scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ProvingGround>()).Single();
            B("main-action-0").onClick.Invoke();NativeSetupFixture.UnboundHumans(ground);
            B("setup-next").onClick.Invoke();B("setup-next").onClick.Invoke();
            for(int i=0;i<3;i++)B("seats-minus").onClick.Invoke();
            Assert.That(ground.LocalSeatCount,Is.EqualTo(1));Assert.That(B("Начать — четыре игрока").interactable,Is.False);
            pad=InputSystem.AddDevice<Gamepad>();InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.North));yield return null;yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
            B("roster-identity").onClick.Invoke();B("roster-choice-guest").onClick.Invoke();B("roster-done").onClick.Invoke();
            Assert.That(B("Начать — четыре игрока").interactable,Is.False,"One participant is invalid even with a device");
            for(int i=0;i<7;i++)B("bot-add").onClick.Invoke();
            B("bot-difficulty-0").onClick.Invoke();ground.SetMatchMode(NativeMatchMode.Teams);
            for(int i=0;i<7;i++)ground.SetBotTeam(i,NativeTeam.TeamA);
            Assert.That(B("Начать — четыре игрока").interactable,Is.False);ground.SetBotTeam(0,NativeTeam.TeamB);
            Assert.That(B("bot-add").interactable,Is.False);Assert.That(B("seats-plus").interactable,Is.False);
            Assert.That(B("Начать — четыре игрока").interactable,Is.True);B("Начать — четыре игрока").onClick.Invoke();yield return null;
            Assert.That(ground.Running,Is.True);Assert.That(ground.BotDriver,Is.Not.Null);Assert.That(ground.Session.ParticipantCount,Is.EqualTo(8));
            Assert.That(ground.GetComponentsInChildren<Camera>().Count(c=>c.enabled),Is.EqualTo(1));
            Assert.That(ground.GetComponentsInChildren<TrooperVisual>().Length,Is.EqualTo(9));
            var frozen=JsonUtility.ToJson(ground.Composition.Read());var session=ground.Session;var driver=ground.BotDriver;
            ground.SendMessage("Pause","Test pause");var time=session.Time;var ticks=driver.Ticks;yield return new WaitForFixedUpdate();
            ground.RemoveBot(0);ground.SetBotDifficulty(0,0);Assert.That(ground.SetupBotCount,Is.EqualTo(7));
            Assert.That(session.Time,Is.EqualTo(time));Assert.That(driver.Ticks,Is.EqualTo(ticks));
            B("Повторить матч").onClick.Invoke();yield return null;
            Assert.That(ground.BotDriver,Is.Not.SameAs(driver));Assert.That(ground.Session,Is.Not.SameAs(session));Assert.That(JsonUtility.ToJson(ground.Composition.Read()),Is.EqualTo(frozen));
            ground.SendMessage("Pause","Exit");B("В главное меню").onClick.Invoke();yield return null;
            B("main-action-0").onClick.Invoke();
            B("setup-next").onClick.Invoke();B("setup-next").onClick.Invoke();
            Assert.That(ground.BotDriver,Is.Null);Assert.That(ground.LocalSeatCount,Is.EqualTo(1));Assert.That(ground.SetupBotCount,Is.EqualTo(1),"New match resets to one bot");
            for(int i=1;i<7;i++)ground.AddBot();
            for(int i=0;i<7;i++){B("bot-remove-0").onClick.Invoke();Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.activeInHierarchy,Is.True,"Removal keeps a navigable control selected");}Assert.That(B("Начать — четыре игрока").interactable,Is.False);
            B("seats-plus").onClick.Invoke();ground.SetMatchMode(NativeMatchMode.Ffa);B("Диагностика четырёх камер · без управления").onClick.Invoke();yield return null;
            Assert.That(ground.Running,Is.True);Assert.That(ground.BotDriver,Is.Null);Assert.That(ground.Session.ParticipantCount,Is.EqualTo(2));
        }
    }
}
