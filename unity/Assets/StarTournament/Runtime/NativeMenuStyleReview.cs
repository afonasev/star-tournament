#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace StarTournament.ProvingGround
{
    // Synthetic screenshot tour; isolated QA data only. Does not stand in for human/controller acceptance.
    public sealed class NativeMenuStyleReview:MonoBehaviour
    {
        ProvingGround ground;string directory;
        Button B(string name)=>ground.GetComponentsInChildren<Button>(true).Single(x=>x.name==name);
        void Click(string name){var b=B(name);if(!b.gameObject.activeInHierarchy||!b.interactable)throw new InvalidOperationException("Unavailable "+name);b.onClick.Invoke();}
        IEnumerator Capture(string name)
        {
            yield return null;yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"));yield return new WaitForSecondsRealtime(.25f);
        }
        IEnumerator Start()
        {
            Application.runInBackground=true;ground=GetComponent<ProvingGround>();
            var args=Environment.GetCommandLineArgs();directory=args[Array.IndexOf(args,"-menuStyleReview")+1];Directory.CreateDirectory(directory);
            yield return new WaitForSecondsRealtime(1);AudioListener.volume=0;
            yield return Capture("01-main");
            Click("main-action-3");yield return Capture("02-profiles-empty");Click("profile-create");yield return Capture("03-profile-detail");
            Click("profile-settings");yield return Capture("04-profile-settings");Click("settings-back");Click("profiles-back");
            Click("main-action-0");yield return Capture("05-map");Click("arena-choice-1");yield return Capture("05b-tunnels");Click("arena-choice-2");yield return Capture("05c-lunar");Click("arena-choice-0");Click("setup-next");yield return Capture("06-rules");Click("mode-choice-1");yield return Capture("06b-rules-teams");Click("setup-next");yield return Capture("07-players");
            Click("roster-card-0");yield return Capture("08-human-editor");Click("roster-identity");yield return Capture("09-profile-picker");B("roster-picker-back").onClick.Invoke();Click("roster-done");
            Click("roster-card-1");yield return Capture("10-bot-editor");Click("roster-identity");yield return Capture("11-difficulty");B("roster-picker-back").onClick.Invoke();Click("roster-done");
            // Existing hidden setup-back action is the same route used by other native review drivers.
            B("Назад к главному меню").onClick.Invoke();
            Click("main-action-4");yield return Capture("12-image");Click("settings-resolution");yield return Capture("13-resolution");Click("resolution-cancel");
            Click("settings-section-1");yield return Capture("14-controls");Click("settings-controls-help");yield return Capture("15-help-keyboard");ground.GetComponentsInChildren<ScrollRect>().Single(x=>x.name=="help-viewport").verticalNormalizedPosition=0;yield return Capture("15b-help-menu");Click("settings-help-gamepad");yield return Capture("16-help-gamepad");ground.GetComponentsInChildren<ScrollRect>().Single(x=>x.name=="help-viewport").verticalNormalizedPosition=0;yield return Capture("16b-gamepad-menu");Click("settings-help-back");Click("settings-controls-devices");yield return Capture("17-devices");Click("devices-back");
            Click("settings-section-2");yield return Capture("18-interface");Click("settings-section-3");yield return Capture("19-audio");Click("settings-back");
            Click("main-action-2");yield return Capture("20-lab");Click("lab-create");yield return Capture("21-lab-dialog");Click("cancel");Click("lab-back");
            File.WriteAllText(Path.Combine(directory,"complete.json"),"{\"complete\":true,\"muted\":true,\"humanAcceptance\":false,\"screenshots\":26}");
            Debug.Log("MENU_STYLE_REVIEW_COMPLETE");Application.Quit();
        }
    }
}
#endif
