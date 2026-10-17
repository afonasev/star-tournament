using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
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
    public sealed class DesignLabWorkspaceTests
    {
        Scene scene;ProvingGround ground;Gamepad pad;string directory,oldEnvironment;
        ManualResetEventSlim selectionEntered,selectionRelease;
        Button Button(string name)=>ground.GetComponentsInChildren<Button>().Single(b=>b.name==name);
        InputField Input(string name)=>ground.GetComponentsInChildren<InputField>().Single(b=>b.name==name);
        void Click(string name)=>Button(name).onClick.Invoke();
        [UnityTest] public IEnumerator DraftValidationFocusAndSavedSelectionRemainIndependentOfLiveMatch()
        {
            directory=Path.Combine(Path.GetTempPath(),"st-lab-ui-"+Guid.NewGuid());Directory.CreateDirectory(directory);
            oldEnvironment=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_HISTORY");Environment.SetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_HISTORY",Path.Combine(directory,"history.json"));
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();pad=InputSystem.AddDevice<Gamepad>();
            yield return NativeLoadingTestScene.WaitLabHistory(ground);Click("main-action-2");yield return null;
            Assert.That(ground.GetComponentsInChildren<Text>().Single(t=>t.name=="lab-release-reason").text,Does.Contain("Профиль пакета"));
            Assert.That(Button("lab-release").interactable,Is.False);
            Assert.That(Input("input-rifle.damage").interactable,Is.False);
            foreach(var name in new[]{"lab-save","lab-rename","plus-rifle.damage","minus-rifle.damage","reset-rifle.damage"})Assert.That(Button(name).interactable,Is.False);
            Assert.That(Button("lab-create").GetComponentInChildren<Text>().text,Is.EqualTo("Создать копию"));
            Click("lab-create");yield return null;Input("lab-profile-name").text="Локальная копия";Click("submit");yield return null;
            Assert.That(Input("input-rifle.damage").interactable,Is.True);
            Assert.That(ground.GetComponentsInChildren<Button>().Any(b=>b.name.StartsWith("group-map-")||b.name=="group-layout"||b.name=="group-ring-layout"||b.name=="group-details"||b.name=="group-ring"||b.name=="group-world-query"||b.name=="group-navigation"||b.name=="group-simulation"),Is.False);
            foreach(var hidden in new[]{"layout.fill-0.x","wayfinding.sign-0.yaw","broadcast.screenWidth","ring.spaceCenterY"}.Concat(LabBundle.AuditedExcludedPaths))
            {Input("lab-search").text=hidden;yield return null;Assert.That(ground.GetComponentsInChildren<InputField>().Any(f=>f.name=="input-"+hidden),Is.False);}
            Input("lab-search").text="";yield return null;
            float original=ground.CombatProfile.Get("rifle.damage");int seats=ground.LocalSeatCount;
            Input("input-rifle.damage").text="31";yield return null;Assert.That(ground.CombatProfile.Get("rifle.damage"),Is.EqualTo(original));Assert.That(Button("lab-save").interactable,Is.True);
            var scroll=ground.GetComponentsInChildren<ScrollRect>().Single(s=>s.name=="lab-fields");scroll.verticalNormalizedPosition=.5f;var selected=Input("input-rifle.damage").gameObject;EventSystem.current.SetSelectedGameObject(selected);float position=scroll.verticalNormalizedPosition;
            Input("input-rifle.damage").text="32";yield return null;Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(selected));Assert.That(scroll.verticalNormalizedPosition,Is.EqualTo(position).Within(.001));
            Input("input-rifle.damage").text="no number";yield return null;Assert.That(Button("lab-save").interactable,Is.False);Assert.That(Input("input-rifle.damage").text,Is.EqualTo("no number"));
            Click("lab-back");yield return null;Assert.That(Button("cancel"),Is.Not.Null);Assert.That(Button("lab-back").interactable,Is.False);
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.East));yield return null;yield return null;InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
            Assert.That(Input("input-rifle.damage").text,Is.EqualTo("no number"));Assert.That(ground.LocalSeatCount,Is.EqualTo(seats));
            Click("reset-rifle.damage");yield return null;Assert.That(Button("lab-save").interactable,Is.False);
            EventSystem.current.SetSelectedGameObject(Button("plus-rifle.damage").gameObject);
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.South));yield return null;yield return null;InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
            Assert.That(Input("input-rifle.damage").text,Is.EqualTo("11"));Assert.That(ground.GetComponentsInChildren<Text>().Single(t=>t.name=="lab-hint").text,Does.Contain("rifle.damage"));
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadRight));yield return null;yield return null;InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject.name,Is.EqualTo("reset-rifle.damage"));
            var history=(DesignLabHistory)typeof(ProvingGround).GetField("labHistory",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(ground);
            int nextRevision=history.SelectedProfile.Revisions.Max(r=>r.Number)+1;
            Input("input-rifle.damage").text="33";Click("lab-save");yield return null;Assert.That(history.Selected.Number,Is.EqualTo(nextRevision));Assert.That(ground.LabSavedIdentity,Does.Contain("v"+nextRevision+"-"));Assert.That(ground.CombatProfile.Get("rifle.damage"),Is.EqualTo(original));
            string savedIdentity=ground.LabSavedIdentity;Assert.That(Button("lab-release").interactable,Is.True);Click("lab-release");yield return null;
            Assert.That(Button("lab-release").transform.Find("state-box/state-check").gameObject.activeSelf,Is.True);Assert.That(ground.GetComponentsInChildren<Text>().Single(t=>t.name=="lab-release-reason").text,Does.Contain("Локальная отметка"));Assert.That(ground.LabSavedIdentity,Is.EqualTo(savedIdentity));
            Input("input-rifle.damage").text="34";yield return null;Assert.That(Button("lab-release").interactable,Is.False);
            Assert.That(Input("input-rifle.damage").interactable,Is.True,"A local mark does not lock the separate local profile");Click("lab-clear");yield return null;
            Assert.That(Button("confirm"),Is.Not.Null,"Clearing a changed draft requires confirmation");Click("confirm");yield return null;
            Click("lab-back");Click("main-action-0");yield return null;Assert.That(ground.CombatProfile.Get("rifle.damage"),Is.EqualTo(33));
            Assert.That(ground.GetComponentsInChildren<Text>().Any(t=>t.name=="setup-lab-identity"),Is.False,"Setup does not duplicate the Lab summary");Assert.That(ground.LabSavedIdentity,Is.EqualTo(savedIdentity));
            ground.StartCombatReview(new[]{pad},true,true);yield return null;yield return NativeLoadingTestScene.Wait(ground);Assert.That(ground.Running,Is.True);
            var oldSession=ground.Session;var frozen=oldSession.Capture().DesignProfile;Assert.That(frozen,Is.Not.Null);
            typeof(ProvingGround).GetMethod("OpenDesignLab",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(ground,null);yield return NativeLoadingTestScene.Wait(ground);Assert.That(ground.Running,Is.True,"Lab cannot open while match is running");
            var copy=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(ground.CombatProfile));
            typeof(ProvingGround).GetMethod("ToMainMenu",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(ground,null);yield return NativeLoadingTestScene.WaitLabHistory(ground);Click("main-action-2");yield return null;Input("input-rifle.damage").text="34";Click("lab-save");yield return null;
            Assert.That(oldSession.Capture().DesignProfile.Hash,Is.EqualTo(frozen.Hash));
            var wrong=oldSession.Capture();wrong.DesignProfile.Hash="wrong";Assert.Throws<ArgumentException>(()=>oldSession.Restore(wrong));
            Assert.That(copy.Get("rifle.damage"),Is.EqualTo(33));Assert.That(ground.CombatProfile.Get("rifle.damage"),Is.EqualTo(33));
            Input("lab-search").text="jumpSpeed";yield return null;Assert.That(Input("input-player.movement.jumpSpeed"),Is.Not.Null);
            EventSystem.current.SetSelectedGameObject(Input("input-player.movement.jumpSpeed").gameObject);
            Input("lab-search").text="rifle.damage";yield return null;Input("lab-search").text="";yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.Not.Null,"Search restores focus when the previous row is removed");
            Click("lab-clear");yield return null;Assert.That(Button("lab-save").interactable,Is.False);
        }
        [UnityTest]public IEnumerator SavedHudTuningAppliesAtNextMatchWithoutRecreatingTheInterface()
        {
            directory=Path.Combine(Path.GetTempPath(),"st-lab-hud-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            oldEnvironment=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_HISTORY");Environment.SetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_HISTORY",Path.Combine(directory,"history.json"));
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();yield return NativeLoadingTestScene.WaitLabHistory(ground);
            Text T(string name)=>ground.GetComponentsInChildren<Text>(true).Single(t=>t.name==name);
            var originalName=T("seat-name-0");int originalSize=originalName.fontSize;
            var keys=new[]{"ui.fontSize","ui.headingFontSize","ui.indicatorVisualHeight","ui.nameFontSize","ui.killNoticeFontSize","ui.killNoticeBottom","ui.killNoticeTop","ui.standingsFontSize"};
            var desired=keys.ToDictionary(k=>k,k=>ground.Profile.Get(k)+ground.Profile.Descriptor(k).Step*4);
            Click("main-action-2");yield return null;Click("lab-create");yield return null;Input("lab-profile-name").text="HUD tuning QA";Click("submit");yield return null;
            foreach(var key in keys){Input("lab-search").text=key;yield return null;Input("input-"+key).text=desired[key].ToString(System.Globalization.CultureInfo.InvariantCulture);yield return null;}
            Assert.That(Button("lab-save").interactable,Is.True);Click("lab-save");yield return null;
            string saved=ground.LabSavedIdentity;Assert.That(originalName.fontSize,Is.EqualTo(originalSize),"Saving must not immediately retune the existing game UI");
            Click("lab-back");yield return null;Click("main-action-0");yield return null;
            pad=InputSystem.AddDevice<Gamepad>();ground.StartCombatReview(new[]{pad},true,true);yield return NativeLoadingTestScene.Wait(ground);yield return null;
            Assert.That(ground.Running,Is.True);Assert.That(ground.Profile.Get("ui.nameFontSize"),Is.EqualTo(desired["ui.nameFontSize"]));
            Assert.That(ground.FrozenLabIdentity,Is.EqualTo(saved));Assert.That(T("seat-name-0"),Is.SameAs(originalName));
            {
                Assert.That(T("seat-name-0").fontSize,Is.EqualTo((int)desired["ui.nameFontSize"]),"name size");
                Assert.That(T("seat-hud-0").fontSize,Is.EqualTo((int)desired["ui.fontSize"]),"HUD text size");
                Assert.That(T("crosshair-0").fontSize,Is.EqualTo((int)desired["ui.headingFontSize"]),"crosshair size");
                Assert.That(T("match-timer").fontSize,Is.EqualTo((int)desired["ui.headingFontSize"]),"timer size");
                Assert.That(T("fps-value").fontSize,Is.EqualTo((int)desired["ui.fontSize"]),"FPS size");
                var fpsPanel=(RectTransform)T("fps-value").transform.parent;Assert.That(fpsPanel.sizeDelta.x,Is.EqualTo(desired["ui.fontSize"]*7),"FPS panel grows with text");
                Assert.That(T("kill-notice-0").fontSize,Is.EqualTo((int)desired["ui.killNoticeFontSize"]),"kill notice size");
                Assert.That(T("kill-notice-0").rectTransform.anchorMin.y,Is.EqualTo(desired["ui.killNoticeBottom"]).Within(.00001),"kill notice bottom");
                Assert.That(T("kill-notice-0").rectTransform.anchorMax.y,Is.EqualTo(desired["ui.killNoticeTop"]).Within(.00001),"kill notice top");
                foreach(var field in new[]{"health-0","armor-0","ammo-0"})
                {
                    var group=(RectTransform)T(field).transform.parent;Assert.That(group.sizeDelta.y,Is.EqualTo(desired["ui.indicatorVisualHeight"]),field+" height");
                    Assert.That(group.GetComponentInChildren<HudIndicatorIcon>().rectTransform.sizeDelta.y,Is.EqualTo(desired["ui.indicatorVisualHeight"]),field+" icon height");
                }
            }
            foreach(var label in ground.GetComponentsInChildren<Text>(true).Where(t=>t.name.StartsWith("seat-name-")))Assert.That(label.fontSize,Is.EqualTo((int)desired["ui.nameFontSize"]),label.name);
            var tables=(NativeStandingsView[])typeof(ProvingGround).GetField("standings",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ground);
            using(EditorUiCapture.FixedViewport(ground,1920,1080))
            {
                yield return null;Canvas.ForceUpdateCanvases();
                foreach(var icon in ground.GetComponentsInChildren<HudIndicatorIcon>(true))Assert.That(icon.GetComponent<CanvasRenderer>(),Is.Not.Null,icon.name);
                var healthIcon=T("health-0").transform.parent.GetComponentInChildren<HudIndicatorIcon>();var iconMesh=healthIcon.canvasRenderer.GetMesh();
                var meshOutput=Path.GetFullPath(Path.Combine(Application.dataPath,"../../docs/evidence/standard-design-lab/hud-lifecycle"));Directory.CreateDirectory(meshOutput);
                File.WriteAllText(Path.Combine(meshOutput,"icon-render-state.txt"),"viewport=1920x1080\nvertices="+(iconMesh?iconMesh.vertexCount:0)+"\nculled="+healthIcon.canvasRenderer.cull+"\n");
                Assert.That(iconMesh,Is.Not.Null);
                Assert.That(iconMesh.vertexCount,Is.GreaterThan(0),"Health icon must have render geometry after resizing");
                Assert.That(healthIcon.canvasRenderer.cull,Is.False,"Resized health icon must not be culled");
            }
            tables[0].Show(true,ground.Session.Match.Read(),true);Canvas.ForceUpdateCanvases();
            Assert.That(tables[0].Root.transform.Find("row-1/cell-7").GetComponent<Text>().fontSize,Is.EqualTo((int)desired["ui.standingsFontSize"]),"Existing table reads current saved tuning at Show");
            var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../docs/evidence/standard-design-lab/hud-lifecycle"));Directory.CreateDirectory(output);
            yield return EditorUiCapture.Capture(ground,Path.Combine(output,"saved-hud-tuning-1080p.png"),1920,1080);
        }
        static Rect Bounds(RectTransform transform)
        {
            var corners=new Vector3[4];transform.GetWorldCorners(corners);return Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);
        }
        [UnityTest]public IEnumerator BooleanSwitchesKeepDraftSeparateAndSaveOnlyOffOrOn()
        {
            directory=Path.Combine(Path.GetTempPath(),"st-lab-bool-ui-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            string path=Path.Combine(directory,"history.json");oldEnvironment=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_HISTORY");Environment.SetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_HISTORY",path);
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();yield return NativeLoadingTestScene.WaitLabHistory(ground);
            Click("main-action-2");yield return null;
            Input("lab-search").text="blood.enabled";yield return null;
            Assert.That(Button("bool-toggle-blood.enabled").interactable,Is.False,"Package control is protected");
            Assert.That(ground.GetComponentsInChildren<Button>().Count(b=>b.name=="bool-toggle-blood.enabled"),Is.EqualTo(1));
            Click("lab-create");yield return null;Input("lab-profile-name").text="Очень длинное название профиля для проверки";Click("submit");yield return null;
            Assert.That(File.Exists(path),Is.True,"Valid profile creation must persist before editing");
            foreach(string key in new[]{"blood.enabled","hit.enabled"})
            {
                Input("lab-search").text=key;yield return null;
                Assert.That(ground.GetComponentsInChildren<InputField>().Any(f=>f.name=="input-"+key),Is.False,"Boolean fields cannot accept fractional text");
                var before=ground.ActiveLabReference.Clone();var bytes=File.ReadAllBytes(path);
                Click("bool-toggle-"+key);yield return null;Assert.That(Button("lab-save").interactable,Is.True);
                Assert.That(ground.ActiveLabReference.Matches(before),Is.True);CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));
                Assert.That(Button("bool-toggle-"+key).transform.Find("state-box/state-check").gameObject.activeSelf,Is.False);Assert.That(Button("bool-toggle-"+key).GetComponentInChildren<Text>().text,Is.EqualTo("Выкл"));
                Click("bool-toggle-"+key);yield return null;Assert.That(Button("lab-save").interactable,Is.False,"Returning to baseline clears the draft change");
                if(key=="blood.enabled")
                {
                    pad=InputSystem.AddDevice<Gamepad>();EventSystem.current.SetSelectedGameObject(Button("bool-toggle-"+key).gameObject);
                    InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadRight));yield return null;yield return null;
                    InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
                    Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(Button("reset-"+key).gameObject));
                    InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.South));yield return null;yield return null;
                    InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;Assert.That(Button("lab-save").interactable,Is.False);
                }
                if(key=="blood.enabled")
                {
                    EventSystem.current.SetSelectedGameObject(Button("bool-toggle-"+key).gameObject);
                    for(int press=0;press<2;press++)
                    {InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.South));yield return null;yield return null;InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;Assert.That(Button("lab-save").interactable,Is.EqualTo(press==0));}
                }
                Click("bool-toggle-"+key);yield return null;EventSystem.current.SetSelectedGameObject(Button("bool-toggle-"+key).gameObject);
                if(key=="blood.enabled")
                {
                    foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720),new Vector2Int(1920,650)})
                    {
                        using(EditorUiCapture.FixedViewport(ground,size.x,size.y))
                        {
                            Canvas.ForceUpdateCanvases();
                            var toolbar=new[]{"lab-profile-select","lab-revision-select","lab-save","lab-create","lab-rename","lab-delete"}.Select(n=>(RectTransform)Button(n).transform).ToArray();
                            var search=(RectTransform)Input("lab-search").transform;
                            for(int j=1;j<toolbar.Length;j++)Assert.That(Bounds(toolbar[j]).xMin,Is.GreaterThan(Bounds(toolbar[j-1]).xMax),"Toolbar controls must not overlap");
                            Assert.That(Bounds(search).width,Is.GreaterThan(Bounds(toolbar[0]).width*3),"Search spans the workspace");
                            Assert.That(Bounds(search).yMax,Is.LessThan(Bounds(toolbar[0]).yMin));
                            Assert.That(Bounds((RectTransform)Button("bool-toggle-"+key).transform).xMax,Is.LessThan(Bounds((RectTransform)Button("reset-"+key).transform).xMin));
                        }
                        var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../docs/evidence/standard-design-lab/approved-ui"));Directory.CreateDirectory(output);
                        yield return EditorUiCapture.Capture(ground,Path.Combine(output,"18-switches-"+size.x+"x"+size.y+".png"),size.x,size.y);
                    }
                }
                Click("lab-save");yield return null;Assert.That(ground.ActiveLabReference.Revision,Is.EqualTo(before.Revision+1));
                var baseline=(LabBundle)typeof(ProvingGround).GetField("historyStartupBaseline",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ground);
                var saved=new DesignLabHistory(path,baseline,releases:LabReleaseCatalog.Load());Assert.That(saved.StorageError,Is.Null);Assert.That(saved.Selected.Snapshot.Get(key),Is.Zero);
            }
            Assert.That(ground.Running,Is.False);
        }
        [UnityTest] public IEnumerator RevisionSelectionKeepsFramesRunningUntilAtomicPublication()
        {
            directory=Path.Combine(Path.GetTempPath(),"st-lab-async-"+Guid.NewGuid());Directory.CreateDirectory(directory);
            string historyPath=Path.Combine(directory,"history.json");
            oldEnvironment=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_HISTORY");
            Environment.SetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_HISTORY",historyPath);
            yield return NativeLoadingTestScene.Load();
            scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();
            yield return NativeLoadingTestScene.WaitLabHistory(ground);
            var field=typeof(ProvingGround).GetField("labHistory",BindingFlags.Instance|BindingFlags.NonPublic);
            var seed=(DesignLabHistory)field.GetValue(ground);seed.Create("Async UI");
            var draft=seed.Selected.Snapshot;draft.Set("rifle.damage",31);seed.Save(draft);
            selectionEntered=new ManualResetEventSlim();selectionRelease=new ManualResetEventSlim();
            var baseline=(LabBundle)typeof(DesignLabHistory).GetField("shipped",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(seed);
            var catalogue=(LabReleaseCatalog)typeof(DesignLabHistory).GetField("releases",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(seed);
            var history=new DesignLabHistory(historyPath,baseline,(temp,destination)=>
            {
                selectionEntered.Set();
                if(!selectionRelease.Wait(TimeSpan.FromSeconds(60)))throw new TimeoutException("Test did not release writer");
                File.Replace(temp,destination,null);
            },releases:catalogue);
            Assert.That(history.StorageError,Is.Null);
            field.SetValue(ground,history);yield return NativeLoadingTestScene.WaitLabHistory(ground);Click("main-action-2");yield return null;
            string identity=ground.LabSavedIdentity;
            Click("lab-revision-select");yield return null;Click("revision-1");yield return null;Click("revision-select");
            float deadline=Time.realtimeSinceStartup+45;
            while(!selectionEntered.IsSet&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(selectionEntered.IsSet,Is.True);
            Assert.That(ground.LabSelectionPending,Is.True);
            int frame=Time.frameCount;
            for(int i=0;i<5;i++)yield return null;
            Assert.That(Time.frameCount-frame,Is.GreaterThanOrEqualTo(5),"Disk publication must not block the frame loop");
            Assert.That(ground.LabSavedIdentity,Is.EqualTo(identity));
            Assert.That(Button("cancel").interactable,Is.False);
            typeof(ProvingGround).GetMethod("CloseLabDialog",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ground,null);
            Assert.That(Button("cancel").gameObject.activeInHierarchy,Is.True,"Back cannot dismiss an in-flight selection");
            Assert.That(typeof(ProvingGround).GetMethod("ProtectLabQuit",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ground,null),Is.False);
            selectionRelease.Set();deadline=Time.realtimeSinceStartup+45;
            while(ground.LabSelectionPending&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(ground.LabSelectionPending,Is.False);yield return null;
            Assert.That(history.Selected.Number,Is.EqualTo(1));
            Assert.That(ground.LabSavedIdentity,Is.Not.EqualTo(identity));
            Assert.That(Button("lab-back").interactable,Is.True);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {selectionRelease?.Set();
            float deadline=Time.realtimeSinceStartup+45;while(ground&&ground.LabSelectionPending&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(ground&&ground.LabSelectionPending,Is.False,"Selection worker must exit before teardown");
            selectionEntered?.Dispose();selectionRelease?.Dispose();
            if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);Environment.SetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_HISTORY",oldEnvironment);if(directory!=null&&Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}
