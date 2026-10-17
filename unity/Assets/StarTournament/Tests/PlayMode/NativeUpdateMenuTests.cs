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
        [UnityTest] public IEnumerator HistorySelectsInstalledReleaseScrollsAndRestoresMenuFocus()
        {
            oldStatus=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_UPDATE_STATUS");
            oldCommand=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_UPDATE_COMMAND");
            oldRelease=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_RELEASE_LABEL");
            directory=Path.Combine(Path.GetTempPath(),"star-update-menu-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            var status=Path.Combine(directory,"status.json");var command=Path.Combine(directory,"command.json");
            File.WriteAllText(status,"{\"state\":\"current\"}");
            Environment.SetEnvironmentVariable("STAR_TOURNAMENT_UPDATE_STATUS",status);
            Environment.SetEnvironmentVariable("STAR_TOURNAMENT_UPDATE_COMMAND",command);
            Environment.SetEnvironmentVariable("STAR_TOURNAMENT_RELEASE_LABEL","0.1.0 (28.09.2026)");
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            var ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();
            using(var fixedViewport=EditorUiCapture.FixedViewport(ground,1920,650))
            {
            yield return null;yield return null;
            pad=InputSystem.AddDevice<Gamepad>();
            typeof(ProvingGround).GetField("menuDevice",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ground,pad);
            var open=ground.GetComponentsInChildren<Button>(true).Single(b=>b.name=="release-history-action");
            Assert.That(open.gameObject.activeInHierarchy,Is.True,"History remains available at current version");
            EventSystem.current.SetSelectedGameObject(open.gameObject);
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.South));yield return null;yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
            float end=Time.realtimeSinceStartup+5;while(!File.Exists(command)&&Time.realtimeSinceStartup<end)yield return null;
            Assert.That(ground.GetComponentsInChildren<Button>(true).Single(x=>x.name=="release-history-back").gameObject.activeInHierarchy,Is.True,"Native Submit opens history");
            Assert.That(File.Exists(command),Is.True,"Opening history queues a history request");
            Assert.That(File.ReadAllText(command),Does.Contain("history"));Assert.That(File.ReadAllText(command),Does.Not.Contain("download"));
            File.Delete(command);
            var data=new NativeUpdateBridge.Snapshot{state="current",history=new NativeUpdateBridge.History{state="ready",items=new[]{
                new NativeUpdateBridge.HistoryItem{version="1.1.0",date="2026-10-16",russian=string.Join("\n",Enumerable.Repeat("Улучшено управление и чтение настроек.",1200)),english="English only"},
                new NativeUpdateBridge.HistoryItem{version="0.1.0",date="2026-09-28",russian="",english="Older release",installed=true}}}};
            data.history.items=new[]{data.history.items[0]}.Concat(Enumerable.Range(1,9).Reverse().Select(i=>new NativeUpdateBridge.HistoryItem{version="1.0."+i,date="2026-10-15",russian="Исправления управления."})).Concat(new[]{data.history.items[1]}).ToArray();
            var json=JsonUtility.ToJson(data);
            Assert.That(System.Text.Encoding.UTF8.GetByteCount(json),Is.GreaterThan(65536),"History may exceed the old small status-only IPC limit");
            File.WriteAllText(status,json);yield return new WaitForSecondsRealtime(.4f);
            Assert.That(EventSystem.current.currentSelectedGameObject.name,Is.EqualTo("release-history-back"),"Background completion must not steal focus");
            var scroll=ground.GetComponentsInChildren<ScrollRect>(true).Single(x=>x.name=="release-history-scroll");
            var body=ground.GetComponentsInChildren<Text>(true).Single(x=>x.name=="release-history-description");
            Assert.That(body.text,Does.Contain("Улучшено"));Assert.That(body.text,Does.Not.Contain("English only"));
            Assert.That(body.supportRichText,Is.False,"Release descriptions are untrusted plain text");
            var chunks=scroll.content.GetComponentsInChildren<Text>().OrderBy(x=>x.transform.GetSiblingIndex()).ToArray();
            Assert.That(chunks.Length,Is.GreaterThan(1),"Long descriptions use bounded text meshes");
            Assert.That(string.Concat(chunks.Select(x=>x.text)),Is.EqualTo(data.history.items[0].russian),"All notes remain readable without truncation");
            Assert.That(scroll.content.rect.height,Is.GreaterThan(scroll.viewport.rect.height));
            InputSystem.QueueStateEvent(pad,new GamepadState{rightStick=Vector2.down});
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(scroll.content.anchoredPosition.y,Is.GreaterThan(10),"Owned right stick scrolls inside long description");
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
            yield return Capture(ground,"history-long");
            var installed=ground.GetComponentsInChildren<Button>(true).Single(x=>x.name=="release-history-version-10");
            var installedBadge=installed.transform.Find("history-installed-badge");
            Assert.That(installedBadge,Is.Not.Null);
            Assert.That(installedBadge.GetComponentInChildren<Text>().text,Is.EqualTo("УСТАНОВЛЕНА"));
            Assert.That(ground.GetComponentsInChildren<Text>(true).Count(t=>t.name=="history-installed-label"),Is.EqualTo(1),"Only the running release receives this badge");
            var versions=ground.GetComponentsInChildren<ScrollRect>(true).Single(x=>x.name=="release-history-versions");
            Assert.That(versions.viewport.rect.height,Is.LessThan(700),"Fixture holds a short viewport during navigation");
            Assert.That(versions.content.rect.height,Is.GreaterThan(versions.viewport.rect.height));
            var first=ground.GetComponentsInChildren<Button>(true).Single(x=>x.name=="release-history-version-0");
            EventSystem.current.SetSelectedGameObject(first.gameObject);yield return null;
            for(int row=1;row<=10;row++)
            {
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadDown));yield return null;yield return null;
                InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
                Assert.That(EventSystem.current.currentSelectedGameObject.name,Is.EqualTo("release-history-version-"+row),"D-pad navigates all releases including offscreen rows");
            }
            Assert.That(versions.content.anchoredPosition.y,Is.GreaterThan(10),"Navigation reveals installed release below the viewport");
            var installedHint=installed.GetComponent<MenuActionHint>();Assert.That(installedHint.ReservedRightInset,Is.Not.Null);
            if(installedHint.GlyphLabel.enabled)
            {
                var hintBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(installed.transform,installedHint.GlyphLabel.rectTransform);
                var badgeBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(installed.transform,(RectTransform)installedBadge);
                Assert.That(hintBounds.max.x,Is.LessThan(badgeBounds.min.x),"A controller hint must not cover the installed-version badge");
            }
            var rowBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(versions.viewport,(RectTransform)installed.transform);
            Assert.That(rowBounds.min.y,Is.GreaterThanOrEqualTo(versions.viewport.rect.yMin-1));
            Assert.That(rowBounds.max.y,Is.LessThanOrEqualTo(versions.viewport.rect.yMax+1));
            foreach(var entry in ground.GetComponentsInChildren<Button>(true).Where(x=>x.name.StartsWith("release-history-version-")))
            {
                var text=entry.GetComponentInChildren<Text>();
                Assert.That(text.preferredHeight,Is.LessThanOrEqualTo(text.rectTransform.rect.height),"Version and publication date fit every row");
            }
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.South));yield return null;yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
            Assert.That(body.text,Does.Contain("не опубликовано"),"Missing Russian is explicit, not invented or replaced with English");
            Assert.That(scroll.content.anchoredPosition.y,Is.EqualTo(0).Within(1),"Short description is placed at its top edge");
            var other=ground.GetComponentsInChildren<Button>(true).Single(x=>x.name=="release-history-version-9");
            EventSystem.current.SetSelectedGameObject(other.gameObject);yield return null;
            Assert.That(installed.transform.Find("history-installed-frame").gameObject.activeInHierarchy,Is.True,"Installed frame persists without focus");
            Assert.That(installedBadge.gameObject.activeInHierarchy,Is.True);
            data.state="staged";data.version="1.1.0";File.WriteAllText(status,JsonUtility.ToJson(data));yield return new WaitForSecondsRealtime(.3f);
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(other.gameObject),"Update completion cannot steal history focus");
            Assert.That(installedBadge.GetComponentInChildren<Text>().text,Is.EqualTo("УСТАНОВЛЕНА"),"Prepared candidate never replaces installed badge");
            yield return Capture(ground,"history-installed-no-translation");
            yield return null;yield return null;
            var focusedBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(versions.viewport,(RectTransform)other.transform);
            Assert.That(focusedBounds.min.y,Is.GreaterThanOrEqualTo(versions.viewport.rect.yMin-1),"Viewport resize keeps focused version visible");
            Assert.That(focusedBounds.max.y,Is.LessThanOrEqualTo(versions.viewport.rect.yMax+1));
            EventSystem.current.SetSelectedGameObject(installed.gameObject);yield return null;
            EventSystem.current.SetSelectedGameObject(other.gameObject);yield return null;
            var images=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_MENU_QA_SCREENSHOTS");
            if(!string.IsNullOrEmpty(images))yield return EditorUiCapture.Capture(ground,Path.Combine(images,"history-installed-short.png"),1920,650);
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.East));yield return null;yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
            Assert.That(open.gameObject.activeInHierarchy,Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(open.gameObject));
            Assert.That(File.Exists(command),Is.False,"Reading/closing history never grants update consent");
            }
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
