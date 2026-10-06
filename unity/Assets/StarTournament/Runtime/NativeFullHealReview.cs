#if UNITY_EDITOR || DEVELOPMENT_BUILD || STAR_TOURNAMENT_DEVELOPMENT_QA
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace StarTournament.ProvingGround
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public sealed partial class ProvingGround
    {
        internal bool FullHealReviewManualTick;
        internal void FullHealReviewPause(){Pause("Full heal timer QA");}
        internal void FullHealReviewResume(){phase=Phase.Running;RefreshInterface();}
        internal void FullHealReviewRepeat(){Repeat();}
        internal void FullHealReviewSetup(){OpenSetup();}
        internal void FullHealReviewLab(string path)
        {
            phase=Phase.MainMenu;OpenLab();labGroup="full-heal";labQuery=path;labSearch.SetTextWithoutNotify(path);
            if(!labDraft.Descriptors.Any(x=>x.Path==path))throw new InvalidOperationException("Missing Lab path "+path);RefreshLabWorkspace();SelectLabControl(labViews[path].Input);
        }
    }
    /// <summary>Scripted, muted native Player evidence. It does not establish human acceptance.</summary>
    public sealed class NativeFullHealReview : MonoBehaviour
    {
        ProvingGround ground;Gamepad[] pads;string directory;
        IEnumerator Capture(string name)
        {
            yield return null;yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"));
            File.WriteAllText(Path.Combine(directory,name+".json"),JsonUtility.ToJson(ground.Session.Capture(),true));
            yield return new WaitForSecondsRealtime(.3f);
        }
        void Advance(int ticks){for(int i=0;i<ticks;i++)ground.Session.Tick(new LocalAction[2],.02f);}
        void Place(float distance,bool rear=false)
        {
            ground.PlaceCombatReviewSeat(0,new Vector3(0,-1.2f,rear?distance:-distance),9,rear?180:0);
            ground.PlaceCombatReviewSeat(1,new Vector3(0,4,rear?distance:-distance),9,rear?180:0);
        }
        void ViewMarker(int seat,Vector3 anchor)
        {
            bool south=anchor.z<0;
            ground.PlaceCombatReviewSeat(seat,anchor+Vector3.forward*(south?5:-5),18,south?180:0);
        }
        IEnumerator CornerMarkers()
        {
            var pickups=CombatBowlCatalog.Build().Pickups;
            foreach(var kind in new[]{ArenaPickupKind.Armor,ArenaPickupKind.Speed})
            {
                if(kind==ArenaPickupKind.Speed)Advance((int)Math.Ceiling(ground.LifeProfile.Get("speed.initialDelaySeconds")/.02f));
                var pair=pickups.Where(x=>x.Kind==kind).ToArray();
                for(int i=0;i<2;i++)ViewMarker(i,pair[i].Anchor);
                bool ready=kind==ArenaPickupKind.Armor?ground.Session.ArmorPickups.All(x=>x.Available):ground.Session.SpeedPickups.All(x=>x.Available);
                if(!ready)throw new InvalidOperationException("Marker available capture preceded "+kind+" spawn");
                yield return Capture("00-marker-"+kind+"-available");
                for(int i=0;i<2;i++)ground.PlaceCombatReviewSeat(i,pair[i].Anchor,18,0);
                Advance(1);
                bool collected=kind==ArenaPickupKind.Armor?ground.Session.ArmorPickups.All(x=>!x.Available):ground.Session.SpeedPickups.All(x=>!x.Available);
                if(!collected)throw new InvalidOperationException("Marker cooldown capture preceded "+kind+" collection");
                for(int i=0;i<2;i++)ViewMarker(i,pair[i].Anchor);
                yield return Capture("00-marker-"+kind+"-cooldown");
            }
            ground.FullHealReviewRepeat();
        }
        Button Button(string name)=>ground.GetComponentsInChildren<Button>(true).Single(x=>x.name==name);
        InputField Input(string name)=>ground.GetComponentsInChildren<InputField>(true).Single(x=>x.name==name);
        IEnumerator Start()
        {
            AudioListener.volume=0;Application.runInBackground=true;
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-fullHealReview");
            if(at<0||at+1>=args.Length||!Path.IsPathFullyQualified(args[at+1]))throw new ArgumentException("Absolute heal review directory required");
            directory=args[at+1];Directory.CreateDirectory(directory);ground=GetComponent<ProvingGround>();yield return new WaitForSecondsRealtime(.5f);
            pads=new[]{InputSystem.AddDevice<Gamepad>(),InputSystem.AddDevice<Gamepad>()};ground.StartCombatReview(pads,true);ground.FullHealReviewManualTick=true;
            if(!ground.Running)throw new InvalidOperationException("Heal review did not start");
            if(args.Contains("-pickupMarkerReview"))yield return CornerMarkers();
            Place(5);ground.Session.ApplyDamage(0,1,50);
            var initial=ground.Session.Capture();initial.Lives[0].Armor=35;ground.Session.Restore(initial);
            Advance(999);yield return Capture("01-before-first-spawn");Advance(1);yield return Capture("02-distant-heart-and-flame");
            Advance(125);Place(2.6f);yield return Capture("03-near-front");Place(2.6f,true);yield return Capture("04-near-rear");Place(2.6f);
            // Actual authoritative time rotates both renderer models; no renderer-only timer override.
            for(int i=0;i<8;i++){Advance(28);yield return Capture("05-rotation-"+i);}
            ground.PlaceCombatReviewSeat(0,new Vector3(0,-1.2f,0),0,0);Advance(1);
            if(ground.Session.Life(0).Health!=100||ground.Session.Life(0).Armor!=35)throw new InvalidOperationException("Heal/armor QA failed");yield return Capture("06-healed-hud");
            Place(5);Advance(1450);yield return Capture("07-cooldown-29-seconds");
            ground.FullHealReviewPause();ground.FullHealReviewManualTick=false;var paused=JsonUtility.ToJson(ground.Session.Capture());yield return new WaitForSecondsRealtime(1);
            if(paused!=JsonUtility.ToJson(ground.Session.Capture()))throw new InvalidOperationException("Pause advanced state");yield return Capture("08-paused-timer");
            ground.FullHealReviewManualTick=true;ground.FullHealReviewResume();Advance(50);yield return Capture("09-respawn-30-seconds");
            ground.FullHealReviewRepeat();Place(5);yield return Capture("10-repeat-initial-delay");
            foreach(var path in new[]{"heal.targetHealth","heal.initialDelaySeconds","heal.respawnSeconds","heal.pickupRadius","presentation.healPickupHoverHeight","presentation.healPickupRotationDegreesPerSecond","presentation.healPickupScale"})
            {ground.FullHealReviewLab(path);yield return Capture("11-lab-"+path);}
            ground.FullHealReviewLab("heal.targetHealth");Button("lab-create").onClick.Invoke();yield return null;
            Input("lab-profile-name").text="Full heal QA";Button("submit").onClick.Invoke();yield return null;
            ground.FullHealReviewLab("heal.targetHealth");yield return null;Input("input-heal.targetHealth").text="80";yield return Capture("14-lab-edited-target");
            if(!Button("lab-save").interactable)throw new InvalidOperationException("Valid heal draft cannot save");Button("lab-save").onClick.Invoke();yield return Capture("15-lab-saved-target");
            var savedIdentity=ground.LabSavedIdentity;
            // Repeat retains the original frozen profile; a new match applies the newly saved revision.
            ground.FullHealReviewRepeat();Place(5);ground.Session.ApplyDamage(0,1,50);Advance(1000);ground.PlaceCombatReviewSeat(0,new Vector3(0,-1.2f,0),0,0);Advance(1);
            if(ground.Session.Life(0).Health!=100)throw new InvalidOperationException("Repeat lost its original profile");yield return Capture("16-repeat-retains-base-target");
            ground.FullHealReviewSetup();ground.StartCombatReview(pads,true);Place(5);ground.Session.ApplyDamage(0,1,50);Advance(1000);ground.PlaceCombatReviewSeat(0,new Vector3(0,-1.2f,0),0,0);Advance(1);
            if(ground.Session.Life(0).Health!=80)throw new InvalidOperationException("Saved heal target did not apply to new match");yield return Capture("17-new-match-saved-target");
            File.WriteAllText(Path.Combine(directory,"saved-profile-identity.txt"),savedIdentity);
            File.WriteAllText(Path.Combine(directory,"review.json"),JsonUtility.ToJson(new Result{map=ground.Session.ArenaIdentity,movement=ground.Profile.Id+"@"+ground.Profile.Version,lifecycle=ground.LifeProfile.Id+"@"+ground.LifeProfile.Version,muted=AudioListener.volume==0,width=Screen.width,height=Screen.height},true));
            Debug.Log("FULL_HEAL_NATIVE_REVIEW_COMPLETE "+directory);Application.Quit();
        }
        [Serializable] sealed class Result {public string classification="SCRIPTED_NATIVE_PLAYER_NOT_HUMAN_ACCEPTANCE",map,movement,lifecycle;public bool muted;public int width,height;}
        void OnDestroy(){if(pads!=null)foreach(var pad in pads)if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);}
    }
#endif
}

#endif
