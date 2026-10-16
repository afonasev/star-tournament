using System.Collections;
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
    public sealed class SettingsGamepadNavigationTests
    {
        Scene scene;
        Gamepad pad;
        Joystick unsupported;
        Mouse cursorMouse;
        InputSettings.EditorInputBehaviorInPlayMode priorCursorInput;
        InputSettings.BackgroundBehavior priorCursorBackground;
        bool cursorInputCaptured;
        ProvingGround ground;
        GamepadLookSettings originalGamepad;
        float originalMouse;
        bool preferencesCaptured;
        Button Button(string name)=>ground.GetComponentsInChildren<Button>(true).Single(button=>button.name==name);
        string Focus=>EventSystem.current.currentSelectedGameObject?.name;

        IEnumerator Load()
        {
            yield return NativeLoadingTestScene.Load();
            scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<ProvingGround>()).Single();
            originalGamepad=GamepadLookSettings.General(ground.Profile);originalMouse=MouseSensitivityPreference.Resolve(ground.Profile);preferencesCaptured=true;
            pad=InputSystem.AddDevice<Gamepad>();yield return null;
            Button("main-action-4").onClick.Invoke();yield return null;
            Assert.That(Focus,Is.EqualTo("settings-section-0"));
        }
        IEnumerator Press(GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(button));
            yield return null;yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
        }
        IEnumerator MoveStick(Vector2 direction)
        {
            InputSystem.QueueStateEvent(pad,new GamepadState { leftStick=direction });
            yield return null;yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
        }
        static void CaptureLtHelp(Canvas canvas,string filename="gamepad-lt-help.png",string variable="STAR_TOURNAMENT_LT_QA_IMAGES")
        {
            var directory=System.Environment.GetEnvironmentVariable(variable);
            if(string.IsNullOrEmpty(directory))return;
            System.IO.Directory.CreateDirectory(directory);
            var owner=new GameObject("LT help evidence camera");var camera=owner.AddComponent<Camera>();camera.enabled=false;
            var target=new RenderTexture(1920,1080,24);target.Create();camera.targetTexture=target;
            var mode=canvas.renderMode;var previousCamera=canvas.worldCamera;float distance=canvas.planeDistance;
            var active=RenderTexture.active;var pixels=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            try
            {
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,1920,1080),0,0);pixels.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory,filename),pixels.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode=mode;canvas.worldCamera=previousCamera;canvas.planeDistance=distance;
                RenderTexture.active=active;camera.targetTexture=null;target.Release();
                Object.Destroy(pixels);Object.Destroy(target);Object.Destroy(owner);Canvas.ForceUpdateCanvases();
            }
        }

        [UnityTest] public IEnumerator ConfirmGlobalResetPreservesIdentitiesAndRequiresDisplayConfirmation()
        {
            var keys=new System.Collections.Generic.List<string>();
            int cap=Application.targetFrameRate;
            NativeUserPreferences.Reset(keys.Add,()=>{});Application.targetFrameRate=cap;
            var floats=new System.Collections.Generic.HashSet<string>(NativeVisualPreferences.ColorAdjustmentPaths.Select(p=>"StarTournament.Graphics."+p));
            foreach(VisualEffect e in System.Enum.GetValues(typeof(VisualEffect)))floats.Add(NativeVisualPreferences.Key(e)+".Strength");
            foreach(string k in new[]{MouseSensitivityPreference.Key,"StarTournament.Settings.Gamepad.X","StarTournament.Settings.Gamepad.Y","StarTournament.Settings.Gamepad.Delay","StarTournament.Settings.Gamepad.Deadzone"})floats.Add(k);
            var saved=keys.Distinct().Select(k=>new {Key=k,Had=PlayerPrefs.HasKey(k),Float=PlayerPrefs.GetFloat(k),Int=PlayerPrefs.GetInt(k)}).ToArray();
            string path=System.IO.Path.Combine(Application.temporaryCachePath,"reset-test-"+System.Guid.NewGuid()+".json");
            try
            {
                yield return Load();
                var catalog=new PlayerProfileCatalog(path);var record=catalog.Create("Reset identity",.5f,false);
                var look=GamepadLookSettings.Default(ground.Profile);look.InvertY=true;catalog.SetGamepad(record.Id,look);
                typeof(ProvingGround).GetField("playerProfiles",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(ground,catalog);
                NativeUserPreferences.SetScale(125);NativeUserPreferences.SetContrast(true);NativeAudioPreferences.SetMaster(25);
                Button("settings-reset-all").onClick.Invoke();yield return null;
                Assert.That(Focus,Is.EqualTo("settings-reset-cancel"));
                Button("settings-reset-confirm").onClick.Invoke();yield return null;yield return null;
                Assert.That(NativeUserPreferences.Scale,Is.EqualTo(100));Assert.That(NativeUserPreferences.HighContrast,Is.False);
                Assert.That(NativeAudioPreferences.Master,Is.EqualTo(100));
                Assert.That(GamepadLookSettings.General(ground.Profile).InvertY,Is.False);
                var reloaded=new PlayerProfileCatalog(path);Assert.That(reloaded.Profiles.Count,Is.EqualTo(1));
                Assert.That(reloaded.Find(record.Id).Name,Is.EqualTo("Reset identity"));Assert.That(reloaded.Find(record.Id).GamepadLookVersion,Is.EqualTo(0));
                Assert.That(Button("settings-confirm").gameObject.activeInHierarchy,Is.True,"Display reset must still require a separate confirmation");
                Assert.That(Focus,Is.EqualTo("settings-revert"),"Risky display changes start on the safe rollback action");
                Button("settings-revert").onClick.Invoke();yield return null;
                Assert.That(Button("settings-confirm").gameObject.activeInHierarchy,Is.False);
            }
            finally
            {
                foreach(var v in saved)if(!v.Had)PlayerPrefs.DeleteKey(v.Key);else if(floats.Contains(v.Key))PlayerPrefs.SetFloat(v.Key,v.Float);else PlayerPrefs.SetInt(v.Key,v.Int);
                PlayerPrefs.Save();Application.targetFrameRate=cap;
                if(System.IO.File.Exists(path))System.IO.File.Delete(path);
            }
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(preferencesCaptured){GamepadLookSettings.SaveGeneral(originalGamepad);MouseSensitivityPreference.Set(ground.Profile,originalMouse);preferencesCaptured=false;}
            if(pad!=null && pad.added)InputSystem.RemoveDevice(pad);
            if(unsupported!=null && unsupported.added)InputSystem.RemoveDevice(unsupported);
            if(cursorMouse!=null && cursorMouse.added)InputSystem.RemoveDevice(cursorMouse);
            if(cursorInputCaptured)
            {
                InputSystem.settings.editorInputBehaviorInPlayMode=priorCursorInput;
                InputSystem.settings.backgroundBehavior=priorCursorBackground;
                cursorInputCaptured=false;
            }
            if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest] public IEnumerator MenuCursorFollowsGamepadAndMouseAcrossScreens()
        {
            yield return Load();
            priorCursorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            priorCursorBackground=InputSystem.settings.backgroundBehavior;cursorInputCaptured=true;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            cursorMouse=InputSystem.AddDevice<Mouse>();yield return null;
            Assert.That(Cursor.visible,Is.True,"Connecting a pad does not hide the cursor");
            yield return Press(GamepadButton.DpadDown);
            Assert.That(Cursor.visible,Is.False);
            Assert.That(Cursor.lockState,Is.EqualTo(CursorLockMode.None));
            yield return null;Assert.That(Cursor.visible,Is.False,"Idle frames keep gamepad mode");
            InputSystem.QueueStateEvent(cursorMouse,new MouseState { position=new Vector2(40,40),delta=new Vector2(10,5) });
            yield return null;yield return null;
            Assert.That(Cursor.visible,Is.True,"Mouse movement restores the cursor");
            InputSystem.QueueStateEvent(pad,new GamepadState { leftStick=new Vector2(.05f,0) });
            yield return null;yield return null;
            Assert.That(Cursor.visible,Is.True,"Stick drift does not take over from the mouse");
            yield return MoveStick(Vector2.up);
            Assert.That(Cursor.visible,Is.False,"Stick navigation hides the cursor again");
            yield return Press(GamepadButton.East);
            Assert.That(Focus,Is.EqualTo("main-action-4"));
            Assert.That(Cursor.visible,Is.False,"Returning to the main menu keeps gamepad mode");
            Button("main-action-0").onClick.Invoke();yield return null;
            Assert.That(Cursor.visible,Is.False,"Opening setup keeps gamepad mode");
            InputSystem.QueueStateEvent(cursorMouse,new MouseState { position=new Vector2(45,45),delta=new Vector2(5,5) });
            yield return null;yield return null;
            Assert.That(Cursor.visible,Is.True,"Setup also responds to mouse movement");
            yield return Press(GamepadButton.Start);
            Assert.That(Cursor.visible,Is.False,"Setup shortcuts hide the cursor");
        }

        [UnityTest] public IEnumerator SettingsAreFullyNavigableWithGamepad()
        {
            yield return Load();
            yield return Press(GamepadButton.DpadRight);Assert.That(Focus,Is.EqualTo("settings-display-mode"));
            yield return Press(GamepadButton.DpadDown);Assert.That(Focus,Is.EqualTo("settings-resolution"));
            yield return Press(GamepadButton.South);Assert.That(Focus,Is.EqualTo("resolution-choice-0"));
            yield return Press(GamepadButton.DpadDown);Assert.That(Focus,Is.EqualTo("resolution-choice-1"));
            yield return Press(GamepadButton.East);Assert.That(Focus,Is.EqualTo("settings-resolution"));
            yield return Press(GamepadButton.DpadDown);Assert.That(Focus,Is.EqualTo("settings-resolution-auto"));
            yield return Press(GamepadButton.DpadDown);Assert.That(Focus,Is.EqualTo("settings-vsync"));
            var vsync=ground.GetComponentsInChildren<Toggle>(true).Single(t=>t.name=="settings-vsync");bool priorVSync=vsync.isOn;
            const string vsyncKey="StarTournament.Graphics.VSync";bool hadVSync=PlayerPrefs.HasKey(vsyncKey);int savedVSync=PlayerPrefs.GetInt(vsyncKey),qualityVSync=QualitySettings.vSyncCount;
            try
            {
                yield return Press(GamepadButton.South);Assert.That(vsync.isOn,Is.EqualTo(!priorVSync));Assert.That(QualitySettings.vSyncCount,Is.EqualTo(priorVSync?0:1));
                yield return Press(GamepadButton.South);Assert.That(vsync.isOn,Is.EqualTo(priorVSync));
            }
            finally{if(hadVSync)PlayerPrefs.SetInt(vsyncKey,savedVSync);else PlayerPrefs.DeleteKey(vsyncKey);PlayerPrefs.Save();QualitySettings.vSyncCount=qualityVSync;}
            yield return Press(GamepadButton.DpadUp);Assert.That(Focus,Is.EqualTo("settings-resolution-auto"));
            yield return Press(GamepadButton.DpadLeft);Assert.That(Focus,Is.EqualTo("settings-section-0"));
            yield return MoveStick(Vector2.down);Assert.That(Focus,Is.EqualTo("settings-section-4"));
            yield return Press(GamepadButton.South);Assert.That(Focus,Is.EqualTo("settings-antialiasing"));
            yield return Press(GamepadButton.DpadDown);Assert.That(Focus,Is.EqualTo("settings-shadows"));
            yield return Press(GamepadButton.East);Assert.That(Focus,Is.EqualTo("settings-section-4"));
            yield return Press(GamepadButton.DpadDown);Assert.That(Focus,Is.EqualTo("settings-section-3"));
            yield return Press(GamepadButton.DpadDown);Assert.That(Focus,Is.EqualTo("settings-section-1"));
            yield return Press(GamepadButton.South);Assert.That(Focus,Is.EqualTo("settings-mouse-sensitivity"));
            var slider=ground.GetComponentsInChildren<Slider>(true).Single(x=>x.name=="settings-mouse-sensitivity");
            slider.value=.5f;float before=slider.value;
            yield return Press(GamepadButton.DpadRight);Assert.That(Focus,Is.EqualTo("settings-mouse-sensitivity"));Assert.That(slider.value,Is.GreaterThan(before));
            yield return Press(GamepadButton.DpadDown);Assert.That(Focus,Is.EqualTo("settings-gamepad-horizontal"));
            yield return Press(GamepadButton.DpadDown);Assert.That(Focus,Is.EqualTo("settings-gamepad-vertical"));
            yield return Press(GamepadButton.DpadDown);Assert.That(Focus,Is.EqualTo("settings-deadzone"));
            yield return Press(GamepadButton.DpadDown);Assert.That(Focus,Is.EqualTo("settings-invert-x"));
            yield return Press(GamepadButton.DpadDown);Assert.That(Focus,Is.EqualTo("settings-invert-y"));
            yield return Press(GamepadButton.DpadDown);Assert.That(Focus,Is.EqualTo("settings-gamepad-return-delay"));
            var delay=ground.GetComponentsInChildren<Slider>(true).Single(x=>x.name=="settings-gamepad-return-delay");
            delay.value=.8f;yield return Press(GamepadButton.DpadRight);
            Assert.That(GamepadLookSettings.General(ground.Profile).ReturnDelay,Is.EqualTo(delay.value).Within(.001));
            if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                string evidence=System.Environment.GetEnvironmentVariable("STAR_TOURNAMENT_UI_EVIDENCE");
                if(!string.IsNullOrWhiteSpace(evidence))
                {System.IO.Directory.CreateDirectory(evidence);yield return EditorUiCapture.Capture(ground,System.IO.Path.Combine(evidence,"control-settings.png"));}
            }
            yield return Press(GamepadButton.DpadDown);Assert.That(Focus,Is.EqualTo("settings-auto-level"));
            var toggle=ground.GetComponentsInChildren<Toggle>(true).Single(x=>x.name=="settings-auto-level");var checkedBefore=toggle.isOn;
            yield return Press(GamepadButton.South);Assert.That(toggle.isOn,Is.EqualTo(!checkedBefore));
            yield return Press(GamepadButton.DpadDown);yield return Press(GamepadButton.South);Assert.That(Focus,Is.EqualTo("settings-help-back"));
            var scroll=ground.GetComponentsInChildren<ScrollRect>(true).Single(x=>x.name=="help-viewport");
            // The complete raster fits the viewport; navigation switches illustrations without cropping.
            Canvas.ForceUpdateCanvases();
            Assert.That(scroll.vertical,Is.False);
            Assert.That(scroll.content.rect.height,Is.EqualTo(scroll.viewport.rect.height).Within(.1f));
            Assert.That(scroll.content.rect.width,Is.EqualTo(scroll.viewport.rect.width).Within(.1f));
            Assert.That(scroll.content.GetComponentInChildren<RawImage>().texture.name,Is.EqualTo("help-keyboard"));
            yield return Press(GamepadButton.DpadDown);Assert.That(Focus,Is.EqualTo("settings-help-keyboard"));
            yield return Press(GamepadButton.DpadRight);Assert.That(Focus,Is.EqualTo("settings-help-gamepad"));
            yield return Press(GamepadButton.South);
            Assert.That(scroll.content.GetComponentInChildren<RawImage>().texture.name,Is.EqualTo("help-gamepad"));
            Assert.That(scroll.content.GetComponentsInChildren<Text>(true).Single(t=>t.name=="help-lt-aim-text").text,
                Does.Contain("короткое нажатие — сброс к горизонту"));
            CaptureLtHelp(scroll.content.GetComponentInParent<Canvas>());
            yield return Press(GamepadButton.DpadLeft);yield return Press(GamepadButton.South);
            Assert.That(scroll.content.GetComponentInChildren<RawImage>().texture.name,Is.EqualTo("help-keyboard"));
            yield return Press(GamepadButton.South);
            Assert.That(scroll.content.GetComponentInChildren<RawImage>().texture.name,Is.EqualTo("help-gamepad"),"Previous arrow wraps from keyboard to gamepad");
            yield return Press(GamepadButton.South);
            Assert.That(scroll.content.GetComponentInChildren<RawImage>().texture.name,Is.EqualTo("help-keyboard"));
            yield return Press(GamepadButton.East);Assert.That(Focus,Is.EqualTo("settings-controls-help"));
            yield return Press(GamepadButton.DpadLeft);Assert.That(Focus,Is.EqualTo("settings-section-1"));
            yield return Press(GamepadButton.DpadDown);Assert.That(Focus,Is.EqualTo("settings-section-2"));
            yield return Press(GamepadButton.South);Assert.That(Focus,Is.EqualTo("settings-ui-scale"));
            yield return Press(GamepadButton.DpadDown);Assert.That(Focus,Is.EqualTo("settings-high-contrast"));
            yield return Press(GamepadButton.DpadDown);Assert.That(Focus,Is.EqualTo("settings-fps"));
            Assert.That(ground.GetComponentsInChildren<Text>().Count(t=>t.text.ToUpperInvariant().Contains("ПОКАЗЫВАТЬ FPS")),Is.EqualTo(1));
            yield return Press(GamepadButton.East);Assert.That(Focus,Is.EqualTo("settings-section-2"));
            yield return Press(GamepadButton.DpadUp);Assert.That(Focus,Is.EqualTo("settings-section-1"));
            yield return Press(GamepadButton.South);Assert.That(Focus,Is.EqualTo("settings-mouse-sensitivity"));
            yield return Press(GamepadButton.East);Assert.That(Focus,Is.EqualTo("settings-section-1"));
            yield return Press(GamepadButton.East);Assert.That(Focus,Is.EqualTo("main-action-4"));
        }
        [UnityTest] public IEnumerator ShoulderButtonsCycleOnlyAssignedWeaponOnPress()
        {
            pad=InputSystem.AddDevice<Gamepad>();yield return null;
            var seats=new SeatInputCoordinator();
            Assert.That(seats.Assign(0,pad),Is.True);
            var profile=ProvingProfile.CreateDefault();
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.LeftShoulder));yield return null;
            seats.Capture(profile,.016f);
            Assert.That(seats.Consume(0).SelectWeapon,Is.EqualTo(WeaponSelection.Previous));
            Assert.That(seats.Consume(1).SelectWeapon,Is.EqualTo(WeaponSelection.None));
            yield return null;seats.Capture(profile,.016f);
            Assert.That(seats.Consume(0).SelectWeapon,Is.EqualTo(WeaponSelection.None),"Holding LB must not repeat");
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.RightShoulder));yield return null;
            seats.Capture(profile,.016f);
            Assert.That(seats.Consume(0).SelectWeapon,Is.EqualTo(WeaponSelection.Next));
        }
        [UnityTest] public IEnumerator AllAiOperatorOpensTheSameSettingsAndReturnsToPause()
        {
            yield return Load();Button("settings-back").onClick.Invoke();Button("main-action-0").onClick.Invoke();
            for(int seat=0;seat<ground.LocalSeatCount;seat++)ground.SetSeatAi(seat,true);
            Button("Начать — четыре игрока").onClick.Invoke();yield return new WaitForFixedUpdate();yield return null;
            yield return NativeLoadingTestScene.Wait(ground);Assert.That(ground.Running,Is.True);
            yield return Press(GamepadButton.Start);Assert.That(ground.Running,Is.False);
            Button("fallback-settings").onClick.Invoke();yield return null;
            Assert.That(Button("settings-section-0").gameObject.activeInHierarchy,Is.True);
            Assert.That(Focus,Is.EqualTo("settings-section-0"));
            Button("settings-section-1").onClick.Invoke();Button("settings-controls-help").onClick.Invoke();
            Assert.That(Button("settings-help-back").gameObject.activeInHierarchy,Is.True);
            yield return Press(GamepadButton.East);yield return Press(GamepadButton.East);
            Assert.That(Focus,Is.EqualTo("settings-section-1"));yield return Press(GamepadButton.East);
            Assert.That(Focus,Is.EqualTo("fallback-settings"));Assert.That(ground.Running,Is.False);
            Button("Продолжить").onClick.Invoke();yield return new WaitForFixedUpdate();yield return NativeLoadingTestScene.Wait(ground);Assert.That(ground.Running,Is.True);
        }
        [UnityTest] public IEnumerator DevicePageShowsSupportedAndUnsupportedControllers()
        {
            yield return Load();unsupported=InputSystem.AddDevice<Joystick>();
            Button("settings-section-1").onClick.Invoke();
            Button("settings-controls-devices").onClick.Invoke();yield return null;
            bool foundPad=false,foundUnsupported=false;
            {
                var rows=ground.GetComponentsInChildren<Text>(true).Where(t=>t.name.StartsWith("device-row-")&&t.gameObject.activeInHierarchy).Select(t=>t.text).ToArray();
                foundPad|=rows.Any(t=>t.Contains(pad.deviceId.ToString())&&t.Contains("Готов к игре"));
                foundUnsupported|=rows.Any(t=>t.Contains(unsupported.deviceId.ToString())&&t.Contains("XInput"));
            }
            Assert.That(foundPad,Is.True);
            Assert.That(foundUnsupported,Is.True);
            Button("devices-back").onClick.Invoke();
            Assert.That(Focus,Is.EqualTo("settings-controls-devices"));
            Button("settings-controls-devices").onClick.Invoke();Assert.That(Focus,Is.EqualTo("devices-back"));
            yield return Press(GamepadButton.East);
            Assert.That(Focus,Is.EqualTo("settings-controls-devices"));
            yield return Press(GamepadButton.East);Assert.That(Focus,Is.EqualTo("settings-section-1"));
        }

        [UnityTest] public IEnumerator GamepadCanCancelDisplayConfirmation()
        {
            yield return Load();
            yield return Press(GamepadButton.DpadRight);Assert.That(Focus,Is.EqualTo("settings-display-mode"));
            yield return Press(GamepadButton.South);
            Assert.That(ground.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="display-confirmation").gameObject.activeInHierarchy,Is.True);
            yield return Press(GamepadButton.East);
            Assert.That(ground.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="display-confirmation").gameObject.activeInHierarchy,Is.False);
            Assert.That(Focus,Is.EqualTo("settings-display-mode"));
        }
        [UnityTest] public IEnumerator SectionFocusShowsParametersWithoutSubmit()
        {
            yield return Load();
            string[] pages={"settings-image","settings-control","settings-interface","settings-audio","settings-graphics"};
            foreach(int section in new[]{4,3,1,2})
            {
                yield return Press(GamepadButton.DpadDown);
                Assert.That(Focus,Is.EqualTo("settings-section-"+section));
                for(int page=0;page<5;page++)
                    Assert.That(ground.GetComponentsInChildren<Transform>(true).Single(x=>x.name==pages[page]).gameObject.activeInHierarchy,Is.EqualTo(page==section));
            }
            yield return Press(GamepadButton.DpadRight);Assert.That(Focus,Is.EqualTo("settings-ui-scale"));
            yield return Press(GamepadButton.East);Assert.That(Focus,Is.EqualTo("settings-section-2"));
            yield return MoveStick(Vector2.up);Assert.That(Focus,Is.EqualTo("settings-section-1"));
            yield return MoveStick(Vector2.down);Assert.That(Focus,Is.EqualTo("settings-section-2"));
            Assert.That(ground.GetComponentsInChildren<Toggle>(true).Single(x=>x.name=="settings-fps").gameObject.activeInHierarchy,Is.True);
        }
        [UnityTest] public IEnumerator AdditionalPagesKeepPreferencesAndGuardResetShortcut()
        {
            bool hadScale=PlayerPrefs.HasKey(NativeUserPreferences.ScaleKey),hadContrast=PlayerPrefs.HasKey(NativeUserPreferences.ContrastKey);
            int oldScale=PlayerPrefs.GetInt(NativeUserPreferences.ScaleKey),oldContrast=PlayerPrefs.GetInt(NativeUserPreferences.ContrastKey);
            try
            {
                yield return Load();
                yield return Press(GamepadButton.North);Assert.That(Focus,Is.EqualTo("settings-reset-cancel"));
                Assert.That(Button("settings-section-0").interactable,Is.False);
                yield return Press(GamepadButton.North);Assert.That(Focus,Is.EqualTo("settings-reset-cancel"));
                yield return Press(GamepadButton.South);Assert.That(Focus,Is.EqualTo("settings-section-0"));
                Assert.That(ground.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="settings-reset-dialog").gameObject.activeSelf,Is.False);
                Button("settings-resolution").onClick.Invoke();yield return null;
                yield return Press(GamepadButton.North);Assert.That(Focus,Is.EqualTo("resolution-choice-0"));
                yield return Press(GamepadButton.East);
                Button("settings-section-1").onClick.Invoke();
                var zone=ground.GetComponentsInChildren<Slider>(true).Single(s=>s.name=="settings-deadzone");zone.value=25;
                Assert.That(GamepadLookSettings.General(ground.Profile).Deadzone,Is.EqualTo(.25f).Within(.001));
                var invert=ground.GetComponentsInChildren<Toggle>(true).Single(s=>s.name=="settings-invert-y");invert.isOn=true;
                Assert.That(GamepadLookSettings.General(ground.Profile).InvertY,Is.True);
                NativeUserPreferences.SetScale(100);NativeUserPreferences.SetContrast(false);yield return null;
                var directory=System.Environment.GetEnvironmentVariable("STAR_TOURNAMENT_SETTINGS_QA_IMAGES");
                foreach(var section in new[]{0,4,3,1,2})
                {
                    Button("settings-section-"+section).onClick.Invoke();yield return null;
                    if(!string.IsNullOrEmpty(directory))CaptureLtHelp(zone.GetComponentInParent<Canvas>(),"settings-page-"+section+".png","STAR_TOURNAMENT_SETTINGS_QA_IMAGES");
                }
                var scale=ground.GetComponentsInChildren<Slider>(true).Single(s=>s.name=="settings-ui-scale");scale.value=125;
                var contrast=ground.GetComponentsInChildren<Toggle>(true).Single(s=>s.name=="settings-high-contrast");contrast.isOn=true;yield return null;yield return null;
                Assert.That(NativeUserPreferences.Scale,Is.EqualTo(125));Assert.That(NativeUserPreferences.HighContrast,Is.True);
                Assert.That(scale.GetComponentInParent<Canvas>().GetComponent<CanvasScaler>().referenceResolution.x,Is.EqualTo(1920/1.25f).Within(.1));
                if(!string.IsNullOrEmpty(directory))CaptureLtHelp(scale.GetComponentInParent<Canvas>(),"interface-125-contrast.png","STAR_TOURNAMENT_SETTINGS_QA_IMAGES");
                Button("settings-reset-all").onClick.Invoke();yield return null;
                Assert.That(Focus,Is.EqualTo("settings-reset-cancel"));
                if(!string.IsNullOrEmpty(directory))CaptureLtHelp(scale.GetComponentInParent<Canvas>(),"reset-dialog.png","STAR_TOURNAMENT_SETTINGS_QA_IMAGES");
                yield return Press(GamepadButton.East);Assert.That(NativeUserPreferences.Scale,Is.EqualTo(125),"Cancel does not reset preferences");
            }
            finally
            {
                if(hadScale)PlayerPrefs.SetInt(NativeUserPreferences.ScaleKey,oldScale);else PlayerPrefs.DeleteKey(NativeUserPreferences.ScaleKey);
                if(hadContrast)PlayerPrefs.SetInt(NativeUserPreferences.ContrastKey,oldContrast);else PlayerPrefs.DeleteKey(NativeUserPreferences.ContrastKey);
                PlayerPrefs.Save();
            }
        }
        [UnityTest] public IEnumerator AudioSettingsPersistAndRemainGamepadNavigable()
        {
            const string musicKey="StarTournament.Audio.MusicPercent",effectsKey="StarTournament.Audio.EffectsPercent";
            bool hadMusic=PlayerPrefs.HasKey(musicKey),hadEffects=PlayerPrefs.HasKey(effectsKey);
            int oldMusic=PlayerPrefs.GetInt(musicKey),oldEffects=PlayerPrefs.GetInt(effectsKey);
            int oldMaster=NativeAudioPreferences.Master,oldVoice=NativeAudioPreferences.Voice(ProvingProfile.CreateDefault());
            try
            {
                yield return Load();
                yield return Press(GamepadButton.DpadDown);
                yield return Press(GamepadButton.DpadDown);
                Assert.That(Focus,Is.EqualTo("settings-section-3"));
                yield return Press(GamepadButton.South);Assert.That(Focus,Is.EqualTo("settings-master"));
                var masterSlider=ground.GetComponentsInChildren<Slider>(true).Single(x=>x.name=="settings-master");
                masterSlider.value=40;
                yield return Press(GamepadButton.DpadRight);Assert.That(Focus,Is.EqualTo("settings-master"));
                Assert.That(NativeAudioPreferences.Master,Is.EqualTo(45));
                yield return Press(GamepadButton.DpadDown);Assert.That(Focus,Is.EqualTo("settings-music"));
                var musicSlider=ground.GetComponentsInChildren<Slider>(true).Single(x=>x.name=="settings-music");
                musicSlider.value=50;
                yield return Press(GamepadButton.DpadRight);Assert.That(Focus,Is.EqualTo("settings-music"));
                Assert.That(NativeAudioPreferences.Music(ground.Profile),Is.EqualTo(55));
                yield return Press(GamepadButton.DpadDown);Assert.That(Focus,Is.EqualTo("settings-effects"));
                var effectsSlider=ground.GetComponentsInChildren<Slider>(true).Single(x=>x.name=="settings-effects");
                effectsSlider.value=50;
                yield return Press(GamepadButton.DpadRight);Assert.That(Focus,Is.EqualTo("settings-effects"));
                Assert.That(NativeAudioPreferences.Effects(ground.Profile),Is.EqualTo(55));
                yield return Press(GamepadButton.DpadDown);Assert.That(Focus,Is.EqualTo("settings-voice"));
                var voiceSlider=ground.GetComponentsInChildren<Slider>(true).Single(x=>x.name=="settings-voice");
                voiceSlider.value=60;
                yield return Press(GamepadButton.DpadRight);Assert.That(Focus,Is.EqualTo("settings-voice"));
                Assert.That(NativeAudioPreferences.Voice(ground.Profile),Is.EqualTo(65));
                Assert.That(NativeAudioPreferences.Effects(ground.Profile),Is.EqualTo(55));
                CaptureLtHelp(voiceSlider.GetComponentInParent<Canvas>(),"audio-settings.png","STAR_TOURNAMENT_AUDIO_QA_IMAGES");
                masterSlider.value=0;voiceSlider.value=0;
                Assert.That(PlayerPrefs.GetInt(NativeAudioPreferences.MasterKey),Is.Zero);
                Assert.That(PlayerPrefs.GetInt(NativeAudioPreferences.VoiceKey),Is.Zero);
                yield return Press(GamepadButton.DpadUp);Assert.That(Focus,Is.EqualTo("settings-effects"));
                yield return Press(GamepadButton.DpadLeft);Assert.That(Focus,Is.EqualTo("settings-effects"));
                Assert.That(NativeAudioPreferences.Effects(ground.Profile),Is.EqualTo(50));
                yield return Press(GamepadButton.East);Assert.That(Focus,Is.EqualTo("settings-section-3"));
            }
            finally
            {
                if(hadMusic)PlayerPrefs.SetInt(musicKey,oldMusic);else PlayerPrefs.DeleteKey(musicKey);
                if(hadEffects)PlayerPrefs.SetInt(effectsKey,oldEffects);else PlayerPrefs.DeleteKey(effectsKey);
                NativeAudioPreferences.SetMaster(oldMaster);NativeAudioPreferences.SetVoice(oldVoice);
                PlayerPrefs.Save();
            }
        }
    }
}
