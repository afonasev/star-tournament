using System;
using System.Collections;
using System.IO;
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
            File.WriteAllText(status,"{\"state\":\"available\",\"version\":\"1.1.0\"}");
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
            File.Delete(command);File.WriteAllText(status,"{\"state\":\"downloading\",\"version\":\"1.1.0\",\"progress\":41}");
            var message=ground.GetComponentsInChildren<Text>(true).Single(t=>t.name=="update-status");
            end=Time.realtimeSinceStartup+5;while(!message.text.Contains("41%")&&Time.realtimeSinceStartup<end)yield return null;
            Assert.That(message.text,Does.Contain("41%"));Assert.That(message.text,Does.Contain("1.1.0"));
            Assert.That(button.interactable,Is.True);Assert.That(button.GetComponentInChildren<Text>().text,Is.EqualTo("Отменить"));
            var fill=ground.GetComponentsInChildren<Image>(true).Single(i=>i.name=="update-progress-fill");
            Assert.That(fill.rectTransform.anchorMax.x,Is.EqualTo(.41f).Within(.001f));
            Assert.That(fill.transform.parent.gameObject.activeSelf,Is.True);
            Assert.That(message.text,Does.Contain("продолжится в меню"));
            yield return Capture(ground,"downloading");
            button.onClick.Invoke();
            end=Time.realtimeSinceStartup+5;while(!File.Exists(command)&&Time.realtimeSinceStartup<end)yield return null;
            Assert.That(File.ReadAllText(command),Does.Contain("cancel"));File.Delete(command);
            File.WriteAllText(status,"{\"state\":\"verifying\",\"version\":\"1.1.0\",\"progress\":100}");
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(message.text,Does.Contain("проверка пакета"));Assert.That(button.interactable,Is.True);
            yield return Capture(ground,"verifying");
            Assert.That(message.gameObject.activeSelf,Is.True,"Download progress remains visible");
            Assert.That(label.text,Is.EqualTo("0.1.0 (28.09.2026)"));
            File.WriteAllText(status,"{\"state\":\"staged\",\"version\":\"1.1.0\",\"progress\":100}");
            end=Time.realtimeSinceStartup+5;while(button.GetComponentInChildren<Text>().text!="Перезапустить"&&Time.realtimeSinceStartup<end)yield return null;
            Assert.That(button.GetComponentInChildren<Text>().text,Is.EqualTo("Перезапустить"));
            Assert.That(message.text,Does.Contain("1.1.0"));Assert.That(message.text,Does.Contain("после выхода из игры"));
            Assert.That(fill.transform.parent.gameObject.activeSelf,Is.False);
            yield return Capture(ground,"staged");
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
                File.WriteAllText(status,"{\"state\":\""+state+"\",\"version\":\"1.1.0\"}");
                var expected=state=="unavailable"?"Не удалось проверить обновления. Проверьте подключение. Следующая проверка — при следующем запуске.":state=="available"?"Доступна версия 1.1.0":"";
                var hasAction=state=="available";
                float end=Time.realtimeSinceStartup+5;
                // Allow the IPC polling loop to observe even transitions with identical empty messages.
                yield return new WaitForSecondsRealtime(.25f);
                while((message.text!=expected||button.gameObject.activeSelf!=hasAction)&&Time.realtimeSinceStartup<end)yield return null;
                Assert.That(message.text,Is.EqualTo(expected));
                Assert.That(message.gameObject.activeSelf,Is.EqualTo(state=="unavailable"||state=="available"),"Failure and the selected available version remain visible");
                Assert.That(button.gameObject.activeSelf,Is.EqualTo(hasAction));
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
        [UnityTest]
        public IEnumerator ErrorsRemainVisibleAndOfferTheRightRetry()
        {
            Assert.That(NativeUpdateBridge.Describe(new NativeUpdateBridge.Snapshot{state="apply-error"}).Message,Does.Contain("Предыдущая установка"));
            oldStatus=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_UPDATE_STATUS");
            oldCommand=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_UPDATE_COMMAND");
            oldRelease=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_RELEASE_LABEL");
            directory=Path.Combine(Path.GetTempPath(),"star-update-menu-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            var status=Path.Combine(directory,"status.json");var command=Path.Combine(directory,"command.json");
            Environment.SetEnvironmentVariable("STAR_TOURNAMENT_UPDATE_STATUS",status);
            Environment.SetEnvironmentVariable("STAR_TOURNAMENT_UPDATE_COMMAND",command);
            Environment.SetEnvironmentVariable("STAR_TOURNAMENT_RELEASE_LABEL","0.1.0 (28.09.2026)");
            File.WriteAllText(status,"{\"state\":\"error\",\"version\":\"1.1.0\",\"error\":\"Нет доступа к папке обновления.\"}");
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            var ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();
            var button=ground.GetComponentsInChildren<Button>(true).Single(b=>b.name=="update-action");
            var message=ground.GetComponentsInChildren<Text>(true).Single(t=>t.name=="update-status");
            foreach(var state in new[]{"error","unavailable","apply-error"})
            {
                File.WriteAllText(status,"{\"state\":\""+state+"\",\"version\":\"1.1.0\",\"error\":\"Нет доступа к папке обновления.\"}");
                yield return new WaitForSecondsRealtime(.4f);
                Assert.That(message.text,Is.EqualTo("Нет доступа к папке обновления."+(state=="unavailable"?" Следующая проверка — при следующем запуске.":"")));
                Assert.That(message.gameObject.activeSelf,Is.True);
                Assert.That(button.interactable,Is.EqualTo(state!="unavailable"));
                Assert.That(button.gameObject.activeSelf,Is.EqualTo(state!="unavailable"));
                Assert.That(button.GetComponentInChildren<Text>().text,Is.EqualTo(state=="apply-error"?"Повторить установку":state=="unavailable"?"":"Повторить"));
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(message.text,Does.StartWith("Нет доступа к папке обновления."),"Polling must not hide the failure");
                Assert.That(File.Exists(command),Is.False,"An error never grants installation consent");
            }
            const string longError="Установка отменена: Windows не получила разрешение администратора. Повторите установку и подтвердите запрос Windows.";
            File.WriteAllText(status,"{\"state\":\"apply-error\",\"error\":\""+longError+"\"}");
            yield return new WaitForSecondsRealtime(.3f);
            Canvas.ForceUpdateCanvases();
            Assert.That(message.preferredHeight,Is.LessThanOrEqualTo(message.rectTransform.rect.height),"Readable errors must fit without clipping");
            var captures=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_MENU_QA_SCREENSHOTS");
            if(!string.IsNullOrEmpty(captures))
            {
                Directory.CreateDirectory(captures);
                yield return EditorUiCapture.Capture(ground,Path.Combine(captures,"apply-error.png"));
            }
            File.WriteAllText(status,"{\"state\":\"unavailable\",\"error\":\"Проверьте подключение.\"}");
            yield return new WaitForSecondsRealtime(.3f);button.onClick.Invoke();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(File.Exists(command),Is.False,"No second metadata check within this session");
        }
        [Serializable] sealed class Command { public string action,id; }
        IEnumerator Capture(ProvingGround ground,string name)
        {
            var captures=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_MENU_QA_SCREENSHOTS");
            if(!string.IsNullOrEmpty(captures))
            {Directory.CreateDirectory(captures);yield return EditorUiCapture.Capture(ground,Path.Combine(captures,name+".png"));}
        }
        [UnityTest] public IEnumerator MatchAdmissionWaitsForMatchingDrainAcknowledgementAndReturnsSameRequest()
        {
            oldStatus=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_UPDATE_STATUS");
            oldCommand=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_UPDATE_COMMAND");
            oldRelease=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_RELEASE_LABEL");
            directory=Path.Combine(Path.GetTempPath(),"star-update-menu-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            var status=Path.Combine(directory,"status.json");var command=Path.Combine(directory,"command.json");
            File.WriteAllText(status,"{\"state\":\"available\",\"version\":\"1.1.0\"}");
            Environment.SetEnvironmentVariable("STAR_TOURNAMENT_UPDATE_STATUS",status);
            Environment.SetEnvironmentVariable("STAR_TOURNAMENT_UPDATE_COMMAND",command);
            Environment.SetEnvironmentVariable("STAR_TOURNAMENT_RELEASE_LABEL","0.1.0 (28.09.2026)");
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");
            var ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();
            var bridge=ground.GetComponent<NativeUpdateBridge>();
            pad=InputSystem.AddDevice<Gamepad>();
            typeof(ProvingGround).GetField("menuDevice",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ground,pad);
            ground.GetComponentsInChildren<Button>(true).Single(b=>b.name=="main-action-0").onClick.Invoke();
            // An unconsumed command must survive the match request.
            File.WriteAllText(command,"{\"action\":\"download\"}");
            typeof(ProvingGround).GetMethod("QueueMatch",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ground,new object[]{true,false});
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(bridge.IsWaitingForMatchPause,Is.True);
            Assert.That(File.ReadAllText(command),Does.Contain("download"),"FIFO must not overwrite unconsumed consent");
            Assert.That(ground.Running,Is.False);
            File.Delete(command);
            float end=Time.realtimeSinceStartup+5;
            while(!File.Exists(command)&&Time.realtimeSinceStartup<end)yield return null;
            var request=JsonUtility.FromJson<Command>(File.ReadAllText(command));File.Delete(command);
            Assert.That(request.action,Is.EqualTo("match"));Assert.That(request.id,Has.Length.EqualTo(32));
            File.WriteAllText(status,"{\"state\":\"paused\",\"matchPaused\":true,\"matchRequest\":\"stale\"}");
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(bridge.IsWaitingForMatchPause,Is.True,"A previous match acknowledgement cannot admit the next match");
            Assert.That(ground.Running,Is.False);
            File.WriteAllText(status,"{\"state\":\"paused\",\"matchPaused\":true,\"matchRequest\":\""+request.id+"\"}");
            yield return NativeLoadingTestScene.Wait(ground);
            Assert.That(ground.Running,Is.True);Assert.That(bridge.IsWaitingForMatchPause,Is.False);
            ground.SendMessage("ToMainMenu");
            end=Time.realtimeSinceStartup+5;while(!File.Exists(command)&&Time.realtimeSinceStartup<end)yield return null;
            var returned=JsonUtility.FromJson<Command>(File.ReadAllText(command));
            Assert.That(returned.action,Is.EqualTo("menu"));Assert.That(returned.id,Is.EqualTo(request.id));
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
