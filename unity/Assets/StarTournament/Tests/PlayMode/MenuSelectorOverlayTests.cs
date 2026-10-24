using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class MenuSelectorOverlayTests
    {
        Scene scene;ProvingGround ground;Gamepad availablePad;bool hadMap;string priorMap;
        Button B(string name)=>ground.GetComponentsInChildren<Button>().Single(b=>b.name==name);
        void Click(string name)=>B(name).onClick.Invoke();
        GameObject Popup(string name)=>ground.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name).gameObject;
        [UnitySetUp] public IEnumerator Load()
        {
            hadMap=PlayerPrefs.HasKey(ProvingGround.LastPlayedMapPreferenceKey);priorMap=PlayerPrefs.GetString(ProvingGround.LastPlayedMapPreferenceKey);
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(availablePad!=null&&availablePad.added)InputSystem.RemoveDevice(availablePad);
            if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);
            if(hadMap)PlayerPrefs.SetString(ProvingGround.LastPlayedMapPreferenceKey,priorMap);else PlayerPrefs.DeleteKey(ProvingGround.LastPlayedMapPreferenceKey);
            PlayerPrefs.Save();
        }
        static PointerEventData PointerAt(RectTransform rect)
        {
            Canvas.ForceUpdateCanvases();var canvas=rect.GetComponentInParent<Canvas>();
            return new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,
                position=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center))};
        }
        public static void AssertPointerTarget(Button button,bool click=false)
        {
            var data=PointerAt((RectTransform)button.transform);var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
            Assert.That(hits.Count,Is.GreaterThan(0),button.name+" is raycastable");
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(),Is.SameAs(button),button.name+" must receive the topmost pointer hit");
            if(click)ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,data,ExecuteEvents.pointerClickHandler);
        }
        IEnumerator Capture(string name)
        {
            var path=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_SELECTOR_QA_DIR");if(string.IsNullOrEmpty(path))yield break;
            Directory.CreateDirectory(path);yield return new WaitForSecondsRealtime(.12f);yield return EditorUiCapture.Capture(ground,Path.Combine(path,name+".png"),1920,1080);
        }
        [UnityTest] public IEnumerator ConfirmationDialogsKeepPointerActionsAboveTheirWorkspace()
        {
            using(EditorUiCapture.FixedViewport(ground,1920,1080))
            {
                Click("main-action-4");yield return null;Click("settings-reset-all");yield return null;
                AssertPointerTarget(B("settings-reset-cancel"));yield return Capture("settings-reset-modal");
                AssertPointerTarget(B("settings-reset-cancel"),true);yield return null;
                Assert.That(Popup("settings-reset-dialog").activeSelf,Is.False);
                Click("settings-back");yield return NativeLoadingTestScene.WaitLabHistory(ground);Click("main-action-2");yield return null;
                Click("lab-create");yield return null;yield return Capture("lab-create-modal");
                ground.GetComponentsInChildren<InputField>().Single(i=>i.name=="lab-profile-name").text="Modal QA";
                AssertPointerTarget(B("submit"),true);yield return null;
                Click("lab-rename");yield return null;AssertPointerTarget(B("cancel"));yield return Capture("lab-rename-modal");AssertPointerTarget(B("cancel"),true);yield return null;
                Click("lab-delete");yield return null;AssertPointerTarget(B("cancel"));yield return Capture("lab-delete-modal");AssertPointerTarget(B("cancel"),true);yield return null;
                typeof(ProvingGround).GetMethod("ShowLabErrors",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(ground,null);
                yield return null;yield return Capture("lab-errors-modal");AssertPointerTarget(B("cancel"),true);yield return null;
                Assert.That(B("lab-create").interactable,Is.True);
            }
        }
        [UnityTest] public IEnumerator ResolutionChoicesOverlayLatePageControlsAndBlockBackgroundClicks()
        {
            using(EditorUiCapture.FixedViewport(ground,1920,1080))
            {
                Click("main-action-4");yield return null;
                var late=new GameObject("late-page-control",typeof(RectTransform),typeof(Image));late.transform.SetParent(Popup("settings-image").transform,false);
                var rect=(RectTransform)late.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;late.GetComponent<Image>().color=Color.clear;
                Click("settings-resolution");yield return null;
                var popup=Popup("resolution-picker");Assert.That(popup.transform.parent.name,Is.EqualTo("settings-screen"));
                Assert.That(popup.transform.GetSiblingIndex(),Is.EqualTo(popup.transform.parent.childCount-1));
                var data=PointerAt((RectTransform)B("settings-display-mode").transform);var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
                Assert.That(hits[0].gameObject,Is.SameAs(popup),"The backdrop blocks controls outside the dropdown card");
                foreach(var choice in popup.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("resolution-choice-")))
                {
                    EventSystem.current.SetSelectedGameObject(choice.gameObject);yield return null;AssertPointerTarget(choice);
                }
                EventSystem.current.SetSelectedGameObject(popup.GetComponentsInChildren<Button>().First(b=>b.name.StartsWith("resolution-choice-")).gameObject);yield return null;
                yield return Capture("resolution-foreground");
                var output=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_SELECTOR_QA_DIR");
                if(!string.IsNullOrEmpty(output))yield return EditorUiCapture.Capture(ground,Path.Combine(output,"resolution-foreground-720.png"),1280,720);
                var choiceToClick=popup.GetComponentsInChildren<Button>().First(b=>b.name.StartsWith("resolution-choice-"));
                EventSystem.current.SetSelectedGameObject(choiceToClick.gameObject);yield return null;AssertPointerTarget(choiceToClick,true);yield return null;
                Assert.That(popup.activeSelf,Is.False);
                // An Editor window may have a size absent from the display's supported modes.
                if(Popup("display-confirmation").activeSelf){AssertPointerTarget(B("settings-revert"),true);yield return null;}
                Assert.That(EventSystem.current.currentSelectedGameObject.name,Is.EqualTo("settings-resolution"));
                UnityEngine.Object.Destroy(late);
            }
        }
        [UnityTest] public IEnumerator RosterSelectorsReceivePointerAboveEditorFields()
        {
            using(EditorUiCapture.FixedViewport(ground,1920,1080))
            {
                availablePad=InputSystem.AddDevice<Gamepad>();yield return null;
                Click("main-action-0");ground.SetMatchMode(NativeMatchMode.Teams);Click("roster-card-0");yield return null;
                foreach(var source in new[]{"roster-identity","roster-device","roster-team"})
                {
                    Click(source);yield return null;yield return null;
                    var popup=Popup(source=="roster-team"?"roster-picker":"lobby-dialog");
                    Assert.That(popup.transform.GetSiblingIndex(),Is.EqualTo(popup.transform.parent.childCount-1));
                    var choice=source=="roster-identity"?B("lobby-profile-none"):source=="roster-device"?B("lobby-screen-0"):popup.GetComponentsInChildren<Button>().First(b=>b.interactable&&b.name.StartsWith("roster-choice-"));
                    EventSystem.current.SetSelectedGameObject(choice.gameObject);yield return null;AssertPointerTarget(choice);
                    yield return Capture(source+"-foreground");AssertPointerTarget(choice,true);yield return null;Assert.That(popup.activeSelf,Is.False);
                }
                Click("roster-done");Click("roster-add-bot");yield return null;yield return null;
                AssertPointerTarget(B("lobby-add-difficulty-1"),true);yield return null;yield return null;
                AssertPointerTarget(B("lobby-add-team-"+(int)NativeTeam.TeamB),true);yield return null;
                Click("roster-card-1");yield return null;Click("roster-identity");yield return null;
                var difficultyPopup=Popup("roster-picker");
                var difficulty=B("roster-choice-difficulty-0");AssertPointerTarget(difficulty);yield return Capture("difficulty-foreground");AssertPointerTarget(difficulty,true);Assert.That(difficultyPopup.activeSelf,Is.False);
            }
        }
        [UnityTest] public IEnumerator LabProfileAndRevisionSelectorsReceivePointerAboveWorkspace()
        {
            yield return NativeLoadingTestScene.WaitLabHistory(ground);
            using(EditorUiCapture.FixedViewport(ground,1920,1080))
            {
                Click("main-action-2");yield return null;Click("lab-profile-select");yield return null;
                var popup=Popup("lab-dialog");Assert.That(popup.transform.GetSiblingIndex(),Is.EqualTo(popup.transform.parent.childCount-1));
                Assert.That(B("lab-save").interactable,Is.False);
                var profile=popup.GetComponentsInChildren<Button>().First(b=>b.name.StartsWith("profile-"));AssertPointerTarget(profile);yield return Capture("lab-profile-foreground");AssertPointerTarget(profile,true);
                // Selection has its own async operation after startup history is already ready.
                float selectionDeadline=Time.realtimeSinceStartup+30;
                while(ground.GetComponentsInChildren<Transform>().Any(t=>t.name=="lab-dialog"))
                {Assert.That(Time.realtimeSinceStartup,Is.LessThan(selectionDeadline),"Profile selection must finish before opening another selector");yield return null;}
                Assert.That(B("lab-revision-select").interactable,Is.True);
                Click("lab-revision-select");yield return null;popup=ground.GetComponentsInChildren<Transform>().Single(t=>t.name=="lab-dialog").gameObject;
                var revision=popup.GetComponentsInChildren<Button>().First(b=>b.name.StartsWith("revision-")&&b.name.Substring(9).All(char.IsDigit));
                AssertPointerTarget(revision);yield return Capture("lab-revision-foreground");AssertPointerTarget(revision,true);yield return null;AssertPointerTarget(B("cancel"),true);yield return null;
                Assert.That(ground.GetComponentsInChildren<Transform>().Any(t=>t.name=="lab-dialog"),Is.False);
            }
        }
    }
}
