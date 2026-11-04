using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class NativeUpdateMenuTests
    {
        string directory, status, command, oldStatus, oldCommand, oldRelease;
        Scene scene;
        ProvingGround ground;
        Gamepad pad;
        const string Sentinel="{\"action\":\"restart\"}";
        [UnitySetUp] public IEnumerator Setup()
        {
            oldStatus=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_UPDATE_STATUS");
            oldCommand=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_UPDATE_COMMAND");
            oldRelease=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_RELEASE_LABEL");
            directory=Path.Combine(Path.GetTempPath(),"star-no-update-menu-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);status=Path.Combine(directory,"status.json");command=Path.Combine(directory,"command.json");
            File.WriteAllText(status,"{\"state\":\"downloading\",\"matchPaused\":false,\"progress\":41}");
            File.WriteAllText(command,Sentinel);
            Environment.SetEnvironmentVariable("STAR_TOURNAMENT_UPDATE_STATUS",status);
            Environment.SetEnvironmentVariable("STAR_TOURNAMENT_UPDATE_COMMAND",command);
            Environment.SetEnvironmentVariable("STAR_TOURNAMENT_RELEASE_LABEL","0.1.0 (28.09.2026)");
            yield return NativeLoadingTestScene.Load();
            scene=SceneManager.GetSceneByName("ProvingGround");
            ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();
        }
        void AssertNoUpdateUi()
        {
            var names=ground.GetComponentsInChildren<Transform>(true).Select(t=>t.name).ToArray();
            Assert.That(names.Any(n=>n.StartsWith("update-",StringComparison.Ordinal)||n.StartsWith("release-history",StringComparison.Ordinal)),Is.False);
            Assert.That(ground.GetComponents<MonoBehaviour>().Any(c=>c.GetType().Name=="NativeUpdateBridge"),Is.False);
            Assert.That(File.ReadAllText(command),Is.EqualTo(Sentinel),"Legacy launcher commands must remain untouched");
            Assert.That(ground.GetComponentsInChildren<Text>(true).Single(t=>t.name=="installed-release").text,Is.EqualTo("0.1.0 (28.09.2026)"));
        }
        [UnityTest] public IEnumerator LegacyUpdateStatesCannotCreateInGameUi()
        {
            foreach(var state in new[]{"available","downloading","verifying","staged","error","apply-error","unavailable"})
            {
                File.WriteAllText(status,"{\"state\":\""+state+"\",\"version\":\"9.0.0\",\"error\":\"Update failed\"}");
                yield return new WaitForSecondsRealtime(.3f);
                AssertNoUpdateUi();
            }
            var captures=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_MENU_QA_SCREENSHOTS");
            if(!string.IsNullOrEmpty(captures))
            {
                Directory.CreateDirectory(captures);
                yield return EditorUiCapture.Capture(ground,Path.Combine(captures,"main-no-updates.png"));
                yield return EditorUiCapture.Capture(ground,Path.Combine(captures,"main-no-updates-720.png"),1280,720);
            }
        }
        [UnityTest] public IEnumerator MatchStartsWithoutLegacyLauncherAcknowledgement()
        {
            pad=InputSystem.AddDevice<Gamepad>();
            typeof(ProvingGround).GetField("menuDevice",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ground,pad);
            ground.GetComponentsInChildren<Button>(true).Single(b=>b.name=="main-action-0").onClick.Invoke();
            ground.AddBot();
            typeof(ProvingGround).GetMethod("QueueMatch",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ground,new object[]{true,false});
            yield return NativeLoadingTestScene.Wait(ground);
            Assert.That(ground.Running,Is.True,"Match must not wait for an updater IPC acknowledgement");
            AssertNoUpdateUi();
            ground.SendMessage("ToMainMenu");yield return null;
            AssertNoUpdateUi();
        }
        [UnityTest] public IEnumerator DirectLaunchHasNoReleaseHistoryAction()
        {
            Environment.SetEnvironmentVariable("STAR_TOURNAMENT_UPDATE_STATUS",null);
            Environment.SetEnvironmentVariable("STAR_TOURNAMENT_UPDATE_COMMAND",null);
            yield return SceneManager.UnloadSceneAsync(scene);
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");
            ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();
            AssertNoUpdateUi();
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);
            if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);
            Environment.SetEnvironmentVariable("STAR_TOURNAMENT_UPDATE_STATUS",oldStatus);
            Environment.SetEnvironmentVariable("STAR_TOURNAMENT_UPDATE_COMMAND",oldCommand);
            Environment.SetEnvironmentVariable("STAR_TOURNAMENT_RELEASE_LABEL",oldRelease);
            if(directory!=null&&Directory.Exists(directory))Directory.Delete(directory,true);
        }
    }
}
