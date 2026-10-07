#if UNITY_EDITOR || DEVELOPMENT_BUILD || STAR_TOURNAMENT_DEVELOPMENT_QA
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace StarTournament.ProvingGround
{
    public sealed class NativeDesignLabReview:MonoBehaviour
    {
        ProvingGround ground;string directory;
        Button Button(string name)=>ground.GetComponentsInChildren<Button>().Single(b=>b.name==name);
        InputField Input(string name)=>ground.GetComponentsInChildren<InputField>().Single(b=>b.name==name);
        void Click(string name){var b=Button(name);if(!b.interactable)throw new InvalidOperationException("Disabled: "+name);b.onClick.Invoke();}
        IEnumerator Capture(string name)
        {yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"));yield return new WaitForSecondsRealtime(.3f);}
        IEnumerator Start()
        {
            ground=GetComponent<ProvingGround>();Application.runInBackground=true;var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"-labReview");directory=args[flag+1];Directory.CreateDirectory(directory);
            yield return new WaitForSecondsRealtime(1);Click("main-action-2");yield return null;
            Canvas.ForceUpdateCanvases();
            var fields=ground.GetComponentsInChildren<ScrollRect>().Single(r=>r.name=="lab-fields");
            var row=(RectTransform)Input("input-rifle.damage").transform.parent;
            if(fields.viewport.rect.height<row.rect.height*6)throw new InvalidOperationException("Compact Lab must fit six complete parameter rows");
            yield return Capture("01-editor-base");
            if(Input("input-rifle.damage").interactable||Button("lab-rename").interactable||Button("lab-save").interactable||Button("plus-rifle.damage").interactable)throw new InvalidOperationException("Shipped profile must be read-only");
            if(args.Contains("-labCatalogReview")){yield return CatalogReview();yield break;}
            if(File.Exists(Path.Combine(directory,"first-run-complete.json")))
            {
                yield return Capture("10-restarted-history");Click("lab-revision-select");yield return null;yield return Capture("11-restarted-revisions");
                File.WriteAllText(Path.Combine(directory,"restart-complete.txt"),ground.LabSavedIdentity);Application.Quit();yield break;
            }
            Click("lab-create");yield return null;yield return Capture("00-create-profile");Input("lab-profile-name").text="QA профиль";Click("submit");yield return null;
            EventSystem.current.SetSelectedGameObject(Input("input-rifle.damage").gameObject);Input("input-rifle.damage").text="31";yield return null;yield return Capture("02-direct-input-draft");
            Input("lab-search").text="armor.pickupAmount";yield return null;Input("input-armor.pickupAmount").text="55";Input("lab-search").text="";yield return null;
            Click("lab-changes");yield return null;yield return Capture("03-all-changes");Click("jump-rifle.damage");yield return null;
            Input("input-rifle.damage").text="999";yield return null;if(Button("lab-save").interactable)throw new InvalidOperationException("Invalid draft save enabled");yield return Capture("04-local-errors");Click("lab-errors");yield return null;yield return Capture("04b-error-summary");Click("cancel");yield return null;
            Click("lab-back");yield return null;yield return Capture("05-dirty-exit-guard");Click("cancel");yield return null;
            Input("input-rifle.damage").text="31";yield return null;Click("lab-save");yield return null;string v2=ground.LabSavedIdentity;
            Click("lab-revision-select");yield return null;yield return Capture("06-history");Click("revision-1");yield return null;
            Click("lab-release");yield return null;yield return Capture("06b-local-release-mark");
            if(!Button("lab-release").GetComponentInChildren<Text>().text.Contains("да · локально"))throw new InvalidOperationException("Local release mark missing");
            Input("input-rifle.damage").text="32";yield return null;Click("lab-save");yield return null;if(!ground.LabSavedIdentity.Contains("v3-"))throw new InvalidOperationException("Save from older base did not append v3");
            Click("lab-rename");yield return null;Input("lab-profile-name").text="QA профиль · переименован";Click("submit");yield return null;
            Input("input-rifle.damage").text="33";Click("lab-revision-select");yield return null;Click("revision-1");yield return null;yield return Capture("07a-dirty-revision-guard");Click("cancel");yield return null;Click("lab-clear");yield return null;
            Click("lab-create");yield return null;Input("lab-profile-name").text="QA удалить";Click("submit");yield return null;Click("lab-delete");yield return null;yield return Capture("07b-delete-confirmation");Click("confirm");yield return null;
            if(Button("lab-delete").interactable)throw new InvalidOperationException("Protected release deletion enabled");
            Click("lab-profile-select");yield return null;var qaProfile=ground.GetComponentsInChildren<Button>().Single(b=>b.name.StartsWith("profile-")&&b.GetComponentInChildren<Text>().text=="QA профиль · переименован");qaProfile.onClick.Invoke();yield return null;
            Input("lab-search").text="jumpSpeed";yield return null;yield return Capture("07-global-search");Input("lab-search").text="";yield return null;
            if(ground.GetComponentsInChildren<Button>().Any(b=>b.name.StartsWith("group-map-")||b.name=="group-layout"||b.name=="group-ring-layout"||b.name=="group-details"||b.name=="group-ring"||b.name=="group-world-query"||b.name=="group-navigation"||b.name=="group-simulation"))throw new InvalidOperationException("Level authoring group is visible");
            foreach(var hidden in new[]{"layout.fill-0.x","wayfinding.sign-0.yaw","broadcast.screenWidth","ring.spaceCenterY"}.Concat(LabBundle.AuditedExcludedPaths))
            {
                Input("lab-search").text=hidden;yield return null;
                if(ground.GetComponentsInChildren<InputField>().Any(f=>f.name=="input-"+hidden))throw new InvalidOperationException("Level authoring search result: "+hidden);
            }
            yield return Capture("07c-level-and-audit-excluded");Input("lab-search").text="";Click("group-lighting");yield return null;yield return Capture("07d-lighting-tuning");Click("group-trooper-view");yield return null;yield return Capture("07e-animation-without-placement");Click("group-rifle");yield return null;
            EventSystem.current.SetSelectedGameObject(Input("input-rifle.damage").gameObject);yield return null;yield return Capture("08-keyboard-focus-hint");
            Click("lab-back");yield return null;Click("main-action-0");yield return null;yield return Capture("09-setup-revision-hash");
            if(AudioListener.volume!=0)throw new InvalidOperationException("QA not muted");
            File.WriteAllText(Path.Combine(directory,"first-run-complete.json"),"{\"classification\":\"AUTOMATED_NATIVE_PLAYER_NOT_HUMAN_ACCEPTANCE\",\"muted\":true,\"v2\":\""+v2.Replace("\n"," ")+"\",\"selected\":\""+ground.LabSavedIdentity.Replace("\n"," ")+"\"}");Application.Quit();
        }
        IEnumerator CatalogReview()
        {
            var catalogue=LabReleaseCatalog.Load();
            var primary=catalogue.Entries.Where(e=>e.ProfileId==DesignLabHistory.ReleaseId).OrderBy(e=>e.Sequence).Last();
            if(!ground.LabSavedIdentity.Contains(primary.Hash)||!ground.LabSavedIdentity.Contains(primary.ProfileId))
                throw new InvalidOperationException("Startup did not select latest released Default");
            if(File.Exists(Path.Combine(directory,"catalogue-first-complete.json")))
            {
                Click("lab-profile-select");yield return null;
                if(!ground.GetComponentsInChildren<Button>().Any(b=>b.name.StartsWith("profile-")&&b.GetComponentInChildren<Text>().text=="QA каталог · эксперимент"))
                    throw new InvalidOperationException("Local experiment lost on restart");
                yield return Capture("catalogue-restart-profiles");Click("cancel");yield return null;
                File.WriteAllText(Path.Combine(directory,"catalogue-restart-complete.txt"),ground.LabSavedIdentity);Application.Quit();yield break;
            }
            Click("lab-profile-select");yield return null;yield return Capture("catalogue-profiles");Click("cancel");yield return null;
            foreach(var entry in catalogue.Entries.OrderBy(e=>e.Sequence))
            {
                Click("lab-profile-select");yield return null;Click("profile-"+entry.ProfileId);yield return null;
                Click("lab-revision-select");yield return null;yield return Capture("catalogue-revisions-"+entry.Sequence);
                Click("revision-"+entry.Revision);yield return null;
                if(!ground.LabSavedIdentity.Contains(entry.Hash)||!ground.LabSavedIdentity.Contains(entry.ProfileId)||Button("lab-release").interactable)
                    throw new InvalidOperationException("Shipped catalogue selection mismatch: "+entry.Sequence);
                if(Input("input-rifle.damage").interactable||Button("lab-rename").interactable)throw new InvalidOperationException("Shipped profile is editable: "+entry.Sequence);
                yield return Capture("catalogue-selected-"+entry.Sequence);
            }
            Click("lab-create");yield return null;Input("lab-profile-name").text="QA каталог · эксперимент";Click("submit");yield return null;
            Input("input-rifle.damage").text="31";yield return null;Click("lab-save");yield return null;
            if(Button("lab-release").GetComponentInChildren<Text>().text!="Релизная: нет")throw new InvalidOperationException("Experiment inherited release mark");
            Click("lab-release");yield return null;yield return Capture("catalogue-local-experiment");
            File.WriteAllText(Path.Combine(directory,"catalogue-first-complete.json"),"{\"classification\":\"QA_FIXTURE_NOT_REAL_RELEASE\",\"entries\":"+catalogue.Entries.Count+",\"muted\":true}");
            Application.Quit();
        }
    }
}
#endif

#endif
