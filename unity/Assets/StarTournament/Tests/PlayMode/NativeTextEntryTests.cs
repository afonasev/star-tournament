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
using Object=UnityEngine.Object;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class NativeTextEntryTests
    {
        ProvingGround ground;Scene scene;Gamepad pad,other;Keyboard keyboard;string profiles;
        InputSettings.BackgroundBehavior priorBackground;InputSettings.EditorInputBehaviorInPlayMode priorEditor;bool inputCaptured;
        Button B(string name)=>ground.GetComponentsInChildren<Button>(true).Single(x=>x.name==name&&x.gameObject.activeInHierarchy);
        InputField F(string name)=>ground.GetComponentsInChildren<InputField>(true).Single(x=>x.name==name);
        string Focus=>EventSystem.current.currentSelectedGameObject?.name;
        bool Open=>ground.GetComponentsInChildren<Canvas>(true).Any(x=>x.name=="text-entry"&&x.gameObject.activeInHierarchy);
        string Draft=>ground.GetComponentsInChildren<Text>(true).Single(x=>x.name=="text-entry-value").text.Replace("│","");
        IEnumerator Load()
        {
            priorBackground=InputSystem.settings.backgroundBehavior;priorEditor=InputSystem.settings.editorInputBehaviorInPlayMode;inputCaptured=true;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");
            ground=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<ProvingGround>()).Single();
            profiles=Path.Combine(Application.temporaryCachePath,"text-entry-"+Guid.NewGuid()+".json");
            typeof(ProvingGround).GetField("playerProfiles",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ground,new PlayerProfileCatalog(profiles));
            pad=InputSystem.AddDevice<Gamepad>();other=InputSystem.AddDevice<Gamepad>();keyboard=InputSystem.AddDevice<Keyboard>();
            typeof(ProvingGround).GetField("menuDevice",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ground,pad);yield return null;
        }
        IEnumerator Press(Gamepad device,GamepadButton button)
        {InputSystem.QueueStateEvent(device,new GamepadState().WithButton(button));yield return null;yield return null;InputSystem.QueueStateEvent(device,new GamepadState());yield return null;yield return null;}
        IEnumerator Type(string text)
        {foreach(char c in text)InputSystem.QueueTextEvent(keyboard,c);yield return null;yield return null;}
        IEnumerator Edit(InputField field)
        {EventSystem.current.SetSelectedGameObject(field.gameObject);yield return null;yield return Press(pad,GamepadButton.South);Assert.That(Open,Is.True);}
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);
            foreach(var d in new InputDevice[]{pad,other,keyboard})if(d!=null&&d.added)InputSystem.RemoveDevice(d);
            if(profiles!=null&&File.Exists(profiles))File.Delete(profiles);
            if(inputCaptured){InputSystem.settings.backgroundBehavior=priorBackground;InputSystem.settings.editorInputBehaviorInPlayMode=priorEditor;inputCaptured=false;}
        }
        [UnityTest] public IEnumerator OpeningHoldForeignPadAndCancelDoNotMutateTarget()
        {
            yield return Load();B("main-action-3").onClick.Invoke();var field=F("profile-name-input");field.text="Пилот";
            int changes=0,ends=0;field.onValueChanged.AddListener(_=>changes++);field.onEndEdit.AddListener(_=>ends++);
            EventSystem.current.SetSelectedGameObject(field.gameObject);yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.South));
            for(int i=0;i<6;i++)yield return null;
            Assert.That(Open,Is.True);Assert.That(Draft,Is.EqualTo("Пилот"),"Opening/held submit must not insert a character");
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
            Assert.That(B("text-space").GetComponentInChildren<Text>().text,Is.EqualTo("Пробел"));var hinttextspace=B("text-space").GetComponent<MenuActionHint>();hinttextspace.Refresh();Assert.That(hinttextspace.GlyphLabel.text,Is.EqualTo("Y"));Assert.That(B("text-space").transform.Find("gamepad-action-button").GetComponent<Image>().enabled,Is.True);
            Assert.That(B("text-delete").GetComponentInChildren<Text>().text,Is.EqualTo("Удалить"));var hinttextdelete=B("text-delete").GetComponent<MenuActionHint>();hinttextdelete.Refresh();Assert.That(hinttextdelete.GlyphLabel.text,Is.EqualTo("X"));Assert.That(B("text-delete").transform.Find("gamepad-action-button").GetComponent<Image>().enabled,Is.True);
            Assert.That(B("text-cancel").GetComponentInChildren<Text>().text,Is.EqualTo("Отмена"));var hinttextcancel=B("text-cancel").GetComponent<MenuActionHint>();hinttextcancel.Refresh();Assert.That(hinttextcancel.GlyphLabel.text,Is.EqualTo("B"));Assert.That(B("text-cancel").transform.Find("gamepad-action-button").GetComponent<Image>().enabled,Is.True);
            Assert.That(B("text-apply").GetComponentInChildren<Text>().text,Is.EqualTo("Применить"));var hinttextapply=B("text-apply").GetComponent<MenuActionHint>();hinttextapply.Refresh();Assert.That(hinttextapply.GlyphLabel.text,Is.EqualTo("≡"));Assert.That(B("text-apply").transform.Find("gamepad-action-button").GetComponent<Image>().enabled,Is.True);
            yield return Press(other,GamepadButton.South);yield return Press(other,GamepadButton.Start);yield return Press(other,GamepadButton.East);
            Assert.That(Open,Is.True);Assert.That(Draft,Is.EqualTo("Пилот"));
            yield return Press(pad,GamepadButton.South);Assert.That(Draft,Is.EqualTo("Пилот1"));
            yield return Capture("keyboard-gamepad.png");
            yield return Press(pad,GamepadButton.East);
            Assert.That(Open,Is.False);Assert.That(field.text,Is.EqualTo("Пилот"));Assert.That(changes,Is.Zero);Assert.That(ends,Is.Zero);
            Assert.That(Focus,Is.EqualTo(field.name));Assert.That(EventSystem.current.sendNavigationEvents,Is.True);
        }
        [UnityTest] public IEnumerator TextEntryReleasesCursorOnFocusLossWithoutDroppingDraft()
        {
            yield return Load();B("main-action-3").onClick.Invoke();var field=F("profile-name-input");field.text="Пилот";
            yield return Edit(field);yield return Press(pad,GamepadButton.South);
            Assert.That(Cursor.visible,Is.False);string draft=Draft,focus=Focus;
            var callback=typeof(ProvingGround).GetMethod("OnApplicationFocus",BindingFlags.Instance|BindingFlags.NonPublic);
            callback.Invoke(ground,new object[]{false});
            Assert.That(Cursor.visible,Is.True);Assert.That(Cursor.lockState,Is.EqualTo(CursorLockMode.None));
            for(int i=0;i<3;i++)yield return null;
            Assert.That(Cursor.visible,Is.True,"Text-entry cursor ownership must also yield while the window is unfocused");
            Assert.That(Open,Is.True);Assert.That(Draft,Is.EqualTo(draft));Assert.That(Focus,Is.EqualTo(focus));Assert.That(field.text,Is.EqualTo("Пилот"));
            callback.Invoke(ground,new object[]{true});yield return null;
            Assert.That(Open,Is.True);Assert.That(Draft,Is.EqualTo(draft));Assert.That(Focus,Is.EqualTo(focus));
            yield return Press(pad,GamepadButton.East);Assert.That(Open,Is.False);Assert.That(field.text,Is.EqualTo("Пилот"));
        }
        [UnityTest] public IEnumerator KeyboardHandoverAndApplyUseExistingProfilePersistence()
        {
            yield return Load();B("main-action-3").onClick.Invoke();var field=F("profile-name-input");field.text="";
            int changes=0,ends=0;field.onValueChanged.AddListener(_=>changes++);field.onEndEdit.AddListener(_=>ends++);
            yield return Edit(field);yield return Type("Новый пилот");Assert.That(Draft,Is.EqualTo("Новый пилот"));Assert.That(field.text,Is.Empty);
            Assert.That(B("text-space").GetComponentInChildren<Text>().text,Is.EqualTo("Пробел"),"Keyboard input hides controller shortcuts");
            yield return Press(pad,GamepadButton.Start);
            Assert.That(Open,Is.False);Assert.That(changes,Is.EqualTo(1));Assert.That(ends,Is.EqualTo(1));Assert.That(field.text,Is.EqualTo("Новый пилот"));
            B("profile-create").onClick.Invoke();yield return null;
            Assert.That(new PlayerProfileCatalog(profiles).Profiles.Single().Name,Is.EqualTo("Новый пилот"));
        }
        [UnityTest] public IEnumerator ReconnectPreservesDraftAndConsumesClaimPress()
        {
            yield return Load();B("main-action-3").onClick.Invoke();var field=F("profile-name-input");field.text="Пилот";
            yield return Edit(field);yield return Type("2");InputSystem.RemoveDevice(pad);yield return null;
            Assert.That(ground.GetComponentsInChildren<Text>(true).Single(t=>t.name=="text-entry-status").text,Does.Contain("отключён"));
            yield return Press(other,GamepadButton.South);Assert.That(Draft,Is.EqualTo("Пилот2"),"First press only claims disconnected modal");
            yield return Press(other,GamepadButton.South);Assert.That(Draft,Is.EqualTo("Пилот21"));
            yield return Press(other,GamepadButton.East);Assert.That(field.text,Is.EqualTo("Пилот"));
        }
        [UnityTest] public IEnumerator LabSearchCancelAndNumericValidationStayInTopContext()
        {
            yield return Load();yield return NativeLoadingTestScene.WaitLabHistory(ground);B("main-action-2").onClick.Invoke();yield return null;
            B("lab-create").onClick.Invoke();yield return null;
            B("submit").onClick.Invoke();yield return null;
            var search=F("lab-search");string original=search.text;yield return Edit(search);yield return Type("rifle");
            yield return Press(pad,GamepadButton.East);Assert.That(Open,Is.False);Assert.That(search.text,Is.EqualTo(original));
            Assert.That(search.gameObject.activeInHierarchy,Is.True,"Cancel must not leave Lab");
            yield return Edit(search);yield return Type("rifle");yield return Press(pad,GamepadButton.Start);yield return null;
            Assert.That(search.text,Is.EqualTo("rifle"));
            var numeric=ground.GetComponentsInChildren<InputField>(true).First(x=>x.name.StartsWith("input-")&&x.gameObject.activeInHierarchy&&x.interactable);
            string before=numeric.text;yield return Edit(numeric);yield return Type("x");Assert.That(Draft,Is.EqualTo(before));
            yield return Capture("keyboard-numeric.png");
            while(Draft.Length>0)yield return Press(pad,GamepadButton.West);
            yield return Type("-");Assert.That(Draft,Is.EqualTo("-"));yield return Press(pad,GamepadButton.Start);
            Assert.That(Open,Is.True);Assert.That(B("text-apply").interactable,Is.False);Assert.That(B("text-apply").GetComponentInChildren<Text>().text,Is.EqualTo("Применить"));
            yield return Capture("keyboard-numeric-incomplete.png");
            yield return Press(pad,GamepadButton.West);yield return Type(before);yield return Press(pad,GamepadButton.DpadDown);
            Assert.That(B("text-apply").interactable,Is.True);Assert.That(B("text-apply").GetComponentInChildren<Text>().text,Is.EqualTo("Применить"));
            yield return Press(pad,GamepadButton.East);Assert.That(numeric.text,Is.EqualTo(before));
        }
        [UnityTest] public IEnumerator GridLanguageCaseCaretAndKeyboardCancelAreFunctional()
        {
            yield return Load();B("main-action-3").onClick.Invoke();var field=F("profile-name-input");field.text="";
            yield return Edit(field);yield return Press(pad,GamepadButton.DpadDown);Assert.That(Focus,Is.EqualTo("text-key-1-0"));
            yield return Press(pad,GamepadButton.South);Assert.That(Draft,Is.EqualTo("Й"));
            B("text-language").onClick.Invoke();B("text-case").onClick.Invoke();
            EventSystem.current.SetSelectedGameObject(B("text-key-1-0").gameObject);
            yield return Press(pad,GamepadButton.South);Assert.That(Draft,Is.EqualTo("Йq"));
            B("text-left").onClick.Invoke();yield return Press(pad,GamepadButton.West);Assert.That(Draft,Is.EqualTo("q"));
            yield return Press(pad,GamepadButton.North);Assert.That(Draft,Is.EqualTo(" q"));
            yield return Capture("keyboard-latin.png");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));yield return null;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;
            Assert.That(Open,Is.False);Assert.That(field.text,Is.Empty);Assert.That(Focus,Is.EqualTo(field.name));
        }
        [UnityTest] public IEnumerator HelpPagesShowGameplayArtWithPreservedAspectAndDeviceEquivalents()
        {
            yield return Load();B("main-action-4").onClick.Invoke();B("settings-section-1").onClick.Invoke();B("settings-controls-help").onClick.Invoke();yield return null;
            foreach(bool gamepad in new[]{false,true})
            {
                if(gamepad)B("settings-help-gamepad").onClick.Invoke();yield return null;Canvas.ForceUpdateCanvases();
                var image=ground.GetComponentsInChildren<RawImage>().Single(x=>x.name=="help-illustration");
                Assert.That(image.texture.name,Is.EqualTo(gamepad?"help-gamepad":"help-keyboard"));
                Assert.That(image.texture.width,Is.EqualTo(1536),"Import must preserve source dimensions, without NPOT resampling");Assert.That(image.texture.height,Is.EqualTo(1024));
                var rect=image.rectTransform.rect;Assert.That(rect.width/rect.height,Is.EqualTo((float)image.texture.width/image.texture.height).Within(.01f));
                string text=(string)typeof(ProvingGround).GetField(gamepad?"GamepadHelp":"KeyboardHelp",BindingFlags.Static|BindingFlags.NonPublic).GetRawConstantValue();
                Assert.That(text,Does.Not.Contain("МЕНЮ"));Assert.That(text,Does.Not.Contain("присоединиться"));
                if(gamepad)Assert.That(ground.GetComponentsInChildren<Text>().Single(t=>t.name=="help-device-equivalents").text,Does.Contain("Xbox → PlayStation → Switch"));
                yield return Capture(gamepad?"help-gamepad.png":"help-keyboard.png");
            }
        }
        IEnumerator Capture(string file)
        {
            string dir=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_TEXT_QA_IMAGES");if(string.IsNullOrEmpty(dir))yield break;
            Directory.CreateDirectory(dir);yield return EditorUiCapture.Capture(ground,Path.Combine(dir,file));
        }
        [UnityTest] public IEnumerator NativeKeyboardEscapeEndsFieldEditingBeforeLeavingLab()
        {
            yield return Load();yield return NativeLoadingTestScene.WaitLabHistory(ground);B("main-action-2").onClick.Invoke();yield return null;
            var search=F("lab-search");EventSystem.current.SetSelectedGameObject(search.gameObject);search.ActivateInputField();yield return null;yield return null;
            Assert.That(search.isFocused,Is.True);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));yield return null;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;
            Assert.That(search.gameObject.activeInHierarchy,Is.True,"First Escape exits editing, not Lab");Assert.That(search.isFocused,Is.False);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));yield return null;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            Assert.That(search.gameObject.activeInHierarchy,Is.False,"Fresh Escape can then return to main menu");
        }
    }
}
