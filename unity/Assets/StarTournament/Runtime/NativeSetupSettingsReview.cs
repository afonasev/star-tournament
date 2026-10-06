#if UNITY_EDITOR || DEVELOPMENT_BUILD || STAR_TOURNAMENT_DEVELOPMENT_QA
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    // Synthetic native Player journey. Screenshots are visual evidence, not human acceptance.
    public sealed class NativeSetupSettingsReview : MonoBehaviour
    {
        ProvingGround ground;
        string directory;
        Button Button(string name)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        void ToggleShadows(){var t=ground.GetComponentsInChildren<Toggle>(true).Single(x=>x.name=="settings-shadows");t.isOn=!t.isOn;}
        GameObject Object(string name)=>ground.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name).gameObject;
        void Check(bool value,string message) { if(!value)throw new InvalidOperationException(message); }
        [Serializable] sealed class ReviewState
        {
            public string state,classification="SYNTHETIC_NATIVE_PLAYER_VISUAL_NOT_HUMAN_ACCEPTANCE";
            public int width,height;
            public bool muted,setupMap,setupRules,setupPlayers,settingsImage,settingsAudio,confirmation,shadows;
            public int musicPercent,effectsPercent;
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"));
            File.WriteAllText(Path.Combine(directory,name+".json"),JsonUtility.ToJson(new ReviewState {
                state=name,width=Screen.width,height=Screen.height,muted=AudioListener.volume==0,
                setupMap=Object("setup-map").activeInHierarchy,setupRules=Object("setup-rules").activeInHierarchy,
                setupPlayers=Object("setup-players").activeInHierarchy,settingsImage=Object("settings-image").activeInHierarchy,
                settingsAudio=Object("settings-audio").activeInHierarchy,
                musicPercent=NativeAudioPreferences.Music(ground.Profile),effectsPercent=NativeAudioPreferences.Effects(ground.Profile),
                confirmation=Object("display-confirmation").activeInHierarchy,
                shadows=QualitySettings.shadows!=ShadowQuality.Disable},true));
            yield return new WaitForSecondsRealtime(.5f);
        }
        IEnumerator Press(Gamepad pad,GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(button));
            yield return null;yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
        }
        IEnumerator Start()
        {
            Application.runInBackground=true;ground=GetComponent<ProvingGround>();
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-setupSettingsReview");
            if(at<0||at+1>=args.Length||!Path.IsPathFullyQualified(args[at+1]))throw new ArgumentException("Absolute setup/settings review directory required");
            directory=args[at+1];Directory.CreateDirectory(directory);
            yield return new WaitForSecondsRealtime(1f);
            Button("main-action-0").onClick.Invoke();Check(Object("setup-map").activeInHierarchy,"Map is not first step");
            yield return Capture("01-map");
            Button("setup-next").onClick.Invoke();Check(Object("setup-rules").activeInHierarchy,"Rules did not follow map");
            yield return Capture("02-rules");
            Button("setup-next").onClick.Invoke();Check(Object("setup-players").activeInHierarchy,"Players did not follow rules");
            yield return Capture("03-players");
            Button("setup-previous").onClick.Invoke();Check(Object("setup-rules").activeInHierarchy,"Back did not preserve setup");
            Button("setup-step-2").onClick.Invoke();Check(Object("setup-players").activeInHierarchy,"Visited player step unavailable");
            Button("Назад к главному меню").onClick.Invoke();
            Button("main-action-4").onClick.Invoke();Check(Object("settings-image").activeInHierarchy,"Image section is not default");
            yield return Capture("04-image-settings");
            var pad=InputSystem.AddDevice<Gamepad>();
            yield return Press(pad,GamepadButton.DpadRight);
            Check(EventSystem.current.currentSelectedGameObject.name=="settings-display-mode","Gamepad did not reach image control");
            yield return Capture("04a-gamepad-focus");
            Button("settings-resolution").onClick.Invoke();Check(Object("resolution-picker").activeInHierarchy,"Resolution picker did not open");
            yield return Capture("05-resolution-picker");
            yield return Press(pad,GamepadButton.East);
            Check(EventSystem.current.currentSelectedGameObject.name=="settings-resolution","Gamepad did not close resolution picker");
            var oldMode=Screen.fullScreenMode;int oldWidth=Screen.width,oldHeight=Screen.height;
            Button("settings-display-mode").onClick.Invoke();yield return new WaitForSecondsRealtime(.5f);
            Check(Object("display-confirmation").activeInHierarchy,"Display change did not request confirmation");
            yield return Capture("06-display-confirmation");
            Button("settings-revert").onClick.Invoke();yield return new WaitForSecondsRealtime(.5f);
            Check(!Object("display-confirmation").activeInHierarchy,"Display confirmation did not close");
            Check(Screen.fullScreenMode==oldMode && Screen.width==oldWidth && Screen.height==oldHeight,"Display rollback changed effective mode");
            yield return Capture("07-display-reverted");
            Button("settings-display-mode").onClick.Invoke();
            yield return new WaitForSecondsRealtime(13f);
            Check(!Object("display-confirmation").activeInHierarchy,"Display confirmation did not time out");
            Check(Screen.fullScreenMode==oldMode && Screen.width==oldWidth && Screen.height==oldHeight,"Display timeout did not restore effective mode");
            yield return Capture("07a-display-timeout-reverted");
            bool oldShadows=QualitySettings.shadows!=ShadowQuality.Disable;
            if(!oldShadows)ToggleShadows();
            ToggleShadows();Check(QualitySettings.shadows==ShadowQuality.Disable,"Shadow toggle did not disable shadows");
            yield return Capture("08-shadows-toggled");
            var overlay=Object("setup-pause");overlay.SetActive(false);
            yield return Capture("08a-world-shadows-off");overlay.SetActive(true);
            ToggleShadows();Check(QualitySettings.shadows!=ShadowQuality.Disable,"Shadow toggle did not restore shadows");
            overlay.SetActive(false);yield return Capture("08b-world-shadows-on");overlay.SetActive(true);
            if(!oldShadows)ToggleShadows();
            Button("settings-section-1").onClick.Invoke();yield return Capture("09-control-settings");
            Button("settings-section-2").onClick.Invoke();yield return Capture("10-interface-settings");
            Button("settings-section-3").onClick.Invoke();Check(Object("settings-audio").activeInHierarchy,"Audio settings are unavailable");
            yield return Capture("11-audio-settings");
            InputSystem.RemoveDevice(pad);
            File.WriteAllText(Path.Combine(directory,"complete.txt"),"PASS: map → rules → players, back navigation, native settings sections including audio, supported resolution picker, display rollback, shadows toggle. Muted synthetic Player journey; physical gamepad and human acceptance pending.");
            Debug.Log("NATIVE_SETUP_SETTINGS_REVIEW_COMPLETE "+directory);
        }
    }
}
#endif

#endif
