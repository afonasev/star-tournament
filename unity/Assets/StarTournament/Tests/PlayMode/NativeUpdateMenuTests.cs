using System;
using System.Collections;
using System.IO;
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
    public sealed class NativeUpdateMenuTests
    {
        string directory,oldStatus,oldCommand,oldRelease;
        Scene scene;
        Gamepad pad;
        [UnityTest]
        public IEnumerator ConsentUsesNativeSubmitAndStagingDoesNotChangeInstalledIdentity()
        {
            oldStatus=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_UPDATE_STATUS");
            oldCommand=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_UPDATE_COMMAND");
            oldRelease=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_RELEASE_LABEL");
            directory=Path.Combine(Path.GetTempPath(),"star-update-menu-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            var status=Path.Combine(directory,"status.json");var command=Path.Combine(directory,"command.json");
            File.WriteAllText(status,"{\"state\":\"available\"}");
            Environment.SetEnvironmentVariable("STAR_TOURNAMENT_UPDATE_STATUS",status);
            Environment.SetEnvironmentVariable("STAR_TOURNAMENT_UPDATE_COMMAND",command);
            Environment.SetEnvironmentVariable("STAR_TOURNAMENT_RELEASE_LABEL","0.1.0 (28.09.2026)");
            yield return NativeLoadingTestScene.Load();
            scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            var ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();
            var button=ground.GetComponentsInChildren<Button>(true).Single(b=>b.name=="update-action");
            var label=ground.GetComponentsInChildren<Text>(true).Single(t=>t.name=="installed-release");
            float end=Time.realtimeSinceStartup+5;
            while(button.GetComponentInChildren<Text>().text!="Обновить"&&Time.realtimeSinceStartup<end)yield return null;
            Assert.That(button.GetComponentInChildren<Text>().text,Is.EqualTo("Обновить"));
            Assert.That(File.Exists(command),Is.False,"Metadata availability is not download consent");
            pad=InputSystem.AddDevice<Gamepad>();EventSystem.current.SetSelectedGameObject(button.gameObject);
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.South));yield return null;yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState());
            end=Time.realtimeSinceStartup+5;while(!File.Exists(command)&&Time.realtimeSinceStartup<end)yield return null;
            Assert.That(File.ReadAllText(command),Does.Contain("download"));
            File.Delete(command);File.WriteAllText(status,"{\"state\":\"downloading\",\"progress\":41}");
            var message=ground.GetComponentsInChildren<Text>(true).Single(t=>t.name=="update-status");
            end=Time.realtimeSinceStartup+5;while(!message.text.Contains("41%")&&Time.realtimeSinceStartup<end)yield return null;
            Assert.That(message.text,Does.Contain("41%"));Assert.That(button.interactable,Is.False);
            Assert.That(message.gameObject.activeSelf,Is.True,"Download progress remains visible");
            Assert.That(label.text,Is.EqualTo("0.1.0 (28.09.2026)"));
            File.WriteAllText(status,"{\"state\":\"staged\",\"progress\":100}");
            end=Time.realtimeSinceStartup+5;while(button.GetComponentInChildren<Text>().text!="Перезапустить"&&Time.realtimeSinceStartup<end)yield return null;
            Assert.That(button.GetComponentInChildren<Text>().text,Is.EqualTo("Перезапустить"));
            Assert.That(label.text,Is.EqualTo("0.1.0 (28.09.2026)"),"Staged metadata never replaces the running version/date");
            Assert.That(File.Exists(command),Is.False,"No implicit restart command");
        }
        [UnityTest]
        public IEnumerator AutomaticCheckShowsActionOnlyWhenUpdateIsAvailable()
        {
            oldStatus=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_UPDATE_STATUS");
            oldCommand=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_UPDATE_COMMAND");
            oldRelease=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_RELEASE_LABEL");
            directory=Path.Combine(Path.GetTempPath(),"star-update-menu-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            var status=Path.Combine(directory,"status.json");var command=Path.Combine(directory,"command.json");
            File.WriteAllText(status,"{\"state\":\"checking\"}");
            Environment.SetEnvironmentVariable("STAR_TOURNAMENT_UPDATE_STATUS",status);
            Environment.SetEnvironmentVariable("STAR_TOURNAMENT_UPDATE_COMMAND",command);
            Environment.SetEnvironmentVariable("STAR_TOURNAMENT_RELEASE_LABEL","0.1.0-test.10 (08.10.2026)");
            yield return NativeLoadingTestScene.Load();
            scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            var ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();
            var button=ground.GetComponentsInChildren<Button>(true).Single(b=>b.name=="update-action");
            var message=ground.GetComponentsInChildren<Text>(true).Single(t=>t.name=="update-status");
            Assert.That(button.gameObject.activeSelf,Is.False,"No check button or startup flash");
            Assert.That(message.gameObject.activeSelf,Is.False,"No checking status flash");
            var label=ground.GetComponentsInChildren<Text>(true).Single(t=>t.name=="installed-release");
            Assert.That(label.alignment,Is.EqualTo(TextAnchor.MiddleRight));
            Assert.That(label.rectTransform.anchorMax.x,Is.GreaterThan(.95f));
            Assert.That(label.rectTransform.anchorMax.y,Is.LessThan(.075f));
            Assert.That(((RectTransform)button.transform).anchorMin.y,Is.GreaterThan(label.rectTransform.anchorMax.y),"Action stays above the version");
            foreach(var state in new[]{"current","unavailable","available","checking","available","current"})
            {
                File.WriteAllText(status,"{\"state\":\""+state+"\"}");
                var expected="";
                float end=Time.realtimeSinceStartup+5;
                // Allow the IPC polling loop to observe even transitions with identical empty messages.
                yield return new WaitForSecondsRealtime(.25f);
                while((message.text!=expected||button.gameObject.activeSelf!=(state=="available"))&&Time.realtimeSinceStartup<end)yield return null;
                Assert.That(message.text,Is.EqualTo(expected));
                Assert.That(message.gameObject.activeSelf,Is.False,"Idle updates show only the release identity and conditional action");
                Assert.That(button.gameObject.activeSelf,Is.EqualTo(state=="available"));
                if(state=="available")
                {
                    Assert.That(button.GetComponentInChildren<Text>().text,Is.EqualTo("Обновить"));
                    Assert.That(button.interactable,Is.True);
                }
                Assert.That(File.Exists(command),Is.False,"Automatic metadata check grants no download consent");
                var captureDirectory=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_MENU_QA_SCREENSHOTS");
                if(!string.IsNullOrEmpty(captureDirectory)&&(state=="current"||state=="available"))
                {
                    Directory.CreateDirectory(captureDirectory);
                    yield return EditorUiCapture.Capture(ground,Path.Combine(captureDirectory,state+".png"));
                    File.WriteAllText(Path.Combine(captureDirectory,state+".txt"),"EDITOR_OFFSCREEN_SYNTHETIC_NOT_PLAYER_ACCEPTANCE\nrevision="+Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_SOURCE_REVISION")+"\nstate="+state+"\nidentity="+label.text+"\n");
                }
            }
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
