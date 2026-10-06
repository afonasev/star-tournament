#if UNITY_EDITOR || DEVELOPMENT_BUILD || STAR_TOURNAMENT_DEVELOPMENT_QA
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    /// <summary>Muted native visual evidence using a labelled shortened-countdown fixture.</summary>
    public sealed class NativeDamageBonusReview : MonoBehaviour
    {
        ProvingGround ground;Gamepad[] pads;string directory;
        Text[] Labels()=>ground.GetComponentsInChildren<Text>().Where(t=>t.name.StartsWith("damage-bonus-notice-")).ToArray();
        void Check(bool ok,string message){if(!ok)throw new InvalidOperationException("DAMAGE_BONUS_REVIEW "+message);}
        IEnumerator Capture(string state)
        {
            yield return null;yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,state+".png"));
            File.WriteAllText(Path.Combine(directory,state+".json"),JsonUtility.ToJson(ground.Session.Capture(),true));
            yield return new WaitForSecondsRealtime(.3f);
        }
        IEnumerator State(int views)
        {
            // This is visual QA with synthetic devices, not physical input acceptance.
            ground.StartCombatReview(pads.Take(views).ToArray(),backgroundDiagnostic:true,ensureOpponent:views==1);ground.FullHealReviewManualTick=true;
            yield return null;
            int count=ground.Session.ParticipantCount;
            var snapshot=ground.Session.Capture();snapshot.DamagePickup.Available=false;snapshot.DamagePickup.Remaining=.01;
            ground.Session.Restore(snapshot);
            var anchor=ground.Session.DamagePickup.Anchor;
            for(int p=0;p<count;p++)ground.PlaceCombatReviewSeat(p,anchor+Vector3.back*(6+p*3),8);
            int spawns=0;ground.Session.DamageBonusAppeared+=()=>spawns++;
            ground.Session.Tick(new LocalAction[count],.02f);yield return null;
            Check(spawns==1,"one spawn event");Check(Labels().Length==views&&Labels().All(t=>t.text=="ПОЯВИЛСЯ БОНУС УРОНА"&&t.color==Color.red),"spawn HUD in all views");
            yield return Capture(views+"-views-01-spawn-countdown-fixture");
            // The final participant is a bot in the one-view fixture. Neither case reveals its identity.
            ground.PlaceCombatReviewSeat(count-1,anchor,8);ground.Session.Tick(new LocalAction[count],.02f);yield return null;
            Check(ground.Session.DamageBoostRemaining(count-1)>0,"collected by remote participant");
            Check(Labels().All(t=>t.text=="БОНУС УРОНА ПОДОБРАН"),"anonymous pickup HUD");
            yield return Capture(views+"-views-02-pickup-position-fixture");
            ground.FullHealReviewPause();yield return Capture(views+"-views-03-pause");
            ground.FullHealReviewResume();yield return null;Check(Labels().All(t=>t.text=="БОНУС УРОНА ПОДОБРАН"),"resume preserves simulation lifetime");
            for(int i=0;i<160;i++)ground.Session.Tick(new LocalAction[count],.02f);yield return null;
            Check(Labels().All(t=>t.text==""),"notice expires");yield return Capture(views+"-views-04-expired");
            var available=ground.Session.Capture();available.DamagePickup.Available=true;available.DamagePickup.Remaining=0;ground.Session.Restore(available);yield return null;
            ground.Session.Tick(new LocalAction[count],.02f);yield return null;
            Check(spawns==1&&Labels().All(t=>t.text==""),"restore does not fabricate appearance");
        }
        IEnumerator Start()
        {
            ground=GetComponent<ProvingGround>();var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-damageBonusEvidence");
            directory=at>=0&&at+1<args.Length?args[at+1]:Path.Combine(Application.persistentDataPath,"damage-bonus-review");Directory.CreateDirectory(directory);
            Application.runInBackground=true;AudioListener.volume=0;while(!Application.isFocused||!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
            pads=Enumerable.Range(0,4).Select(_=>InputSystem.AddDevice<Gamepad>()).ToArray();
            yield return State(1);yield return State(4);
            File.WriteAllText(Path.Combine(directory,"complete.json"),"{\"complete\":true,\"muted\":true,\"humanAcceptance\":false,\"fixture\":\"shortened-countdown-and-participant-placement\"}");
            Debug.Log("DAMAGE_BONUS_REVIEW_COMPLETE "+directory);Application.Quit();
        }
        void OnDestroy(){if(pads!=null)foreach(var pad in pads)if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);}
    }
}
#endif

#endif
