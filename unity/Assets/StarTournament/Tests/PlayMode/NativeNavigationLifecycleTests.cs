using StarTournament.ProvingGround.Tests.PlayMode;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace StarTournament.ProvingGround.Tests
{
    public sealed class NativeNavigationLifecycleTests
    {
        Scene scene; ProvingGround ground;Gamepad[] pads;
        Button Button(string name)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(pads!=null)foreach(var p in pads)if(p!=null&&p.added)InputSystem.RemoveDevice(p);
            if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);
        }
        [UnityTest] public IEnumerator SessionTickPauseDeathAndFrozenRepeatOwnNavigationLifecycle()
        {
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ProvingGround>()).Single();
            pads=Enumerable.Range(0,3).Select(_=>InputSystem.AddDevice<Gamepad>()).ToArray();ground.StartCombatReview(pads);ground.EnableNavigationReview();
            var driver=ground.NavigationReviewDriver;var configuration=driver.Controller.Capture().Configuration;
            driver.Controller.SetStaticGoal(new Vector3(-13,4,2));yield return new WaitForFixedUpdate();yield return null;
            Assert.That(driver.Controller.Capture().Time,Is.GreaterThanOrEqualTo(0));
            InputSystem.QueueStateEvent(pads[0],new GamepadState().WithButton(GamepadButton.Start));yield return null;yield return null;
            InputSystem.QueueStateEvent(pads[0],new GamepadState());yield return null;
            Assert.That(ground.Running,Is.False);var paused=JsonUtility.ToJson(driver.Controller.Capture());var time=ground.Session.Time;
            yield return new WaitForSecondsRealtime(.1f);Assert.That(ground.Session.Time,Is.EqualTo(time));Assert.That(JsonUtility.ToJson(driver.Controller.Capture()),Is.EqualTo(paused));
            ground.BotNavigationProfile.Set("bots.navigation.stuckSeconds",4);
            ground.BotPerceptionProfile.Set("bots.normal.memorySeconds",10);
            Button("Повторить матч").onClick.Invoke();yield return null;yield return NativeLoadingTestScene.Wait(ground);
            Assert.That(ground.NavigationReviewDriver,Is.Not.SameAs(driver));Assert.That(ground.NavigationReviewDriver.Controller.Capture().Configuration,Is.EqualTo(configuration));
            Assert.That(ground.NavigationReviewDriver.Controller.Capture().HasGoal,Is.False);
            ground.NavigationReviewDriver.Controller.SetStaticGoal(new Vector3(-13,4,2));
            ground.Session.ApplyDamage(0,ground.Session.Life(0).Life,500);yield return new WaitForFixedUpdate();yield return null;
            Assert.That(ground.NavigationReviewDriver.Controller.Capture().HasGoal,Is.False);
            Button("В главное меню").onClick.Invoke();Button("main-action-0").onClick.Invoke();Assert.That(ground.NavigationReviewDriver,Is.Null);
        }
    }
}
