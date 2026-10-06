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
    /// <summary>Bounded scripted Player review. Fixture placement/timings are diagnostics, not gameplay defaults.</summary>
    public sealed class NativePulseReview:MonoBehaviour
    {
        ProvingGround ground;string directory;Gamepad[] pads;
        void Click(string name)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name).onClick.Invoke();
        IEnumerator Capture(string name)
        {
            bool gameplay=int.Parse(name.Substring(0,2))>=3;
            if(gameplay&&!ground.Running)throw new InvalidOperationException("Gameplay capture requires an active match");
            // The fixture freezes Update/FixedUpdate; refresh UI explicitly as the normal Update would.
            ground.SendMessage("RefreshInterface");
            if(gameplay&&ground.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="setup-screen"&&t.gameObject.activeInHierarchy))throw new InvalidOperationException("Setup UI covered gameplay capture");
            if(ground.Session!=null)ground.SendMessage("LateUpdate");
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"));
            if(ground.Session!=null)File.WriteAllText(Path.Combine(directory,name+".json"),JsonUtility.ToJson(ground.Session.Capture(),true));
            yield return new WaitForSecondsRealtime(.3f);
        }
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"-pulseReview");directory=args[flag+1];Directory.CreateDirectory(directory);
            ground=GetComponent<ProvingGround>();AudioListener.volume=0;Application.runInBackground=true;
            yield return new WaitForSecondsRealtime(1);ground.enabled=false;
            Click("main-action-2");yield return null;Click("group-rocket");yield return null;
            foreach(var key in new[]{"rocket.maximumDamage","rocket.speed","rocket.radius","rocket.cooldownSeconds","rocket.startingAmmo"})
                if(!ground.GetComponentsInChildren<InputField>(true).Any(i=>i.name=="input-"+key))throw new InvalidOperationException("Pulse Lab field missing: "+key);
            yield return Capture("01-lab-pulse-gameplay");Click("group-rocket-effects");yield return null;yield return Capture("02-lab-pulse-effects");Click("lab-back");
            pads=Enumerable.Range(0,4).Select(_=>InputSystem.AddDevice<Gamepad>()).ToArray();
            ground.StartCombatReview(new[]{pads[0]},backgroundDiagnostic:true,ensureOpponent:true);
            ground.PlaceCombatReviewSeat(0,new Vector3(0,-1.2f,-5),0);ground.PlaceCombatReviewSeat(1,new Vector3(0,-1.2f,5),0,180);
            SelectAll();yield return Capture("03-first-person-pulse");
            var actions=new LocalAction[ground.Session.ParticipantCount];actions[0].Fire=true;ground.Session.Tick(actions,.02f);
            if(ground.Session.Rockets.Length!=1)throw new InvalidOperationException("No actual Pulse launch");
            for(int i=0;i<4;i++)ground.Session.Tick(new LocalAction[actions.Length],.02f);
            yield return Capture("04-projectile-flight");
            int explosions=0;ground.Session.RocketExploded+=_=>explosions++;
            while(explosions==0&&ground.Session.Time<3)ground.Session.Tick(new LocalAction[actions.Length],.02f);
            if(explosions!=1)throw new InvalidOperationException("Expected one explosion");
            ground.Session.Tick(new LocalAction[actions.Length],.04f);yield return Capture("05-explosion-impact");
            ground.StartCombatReview(new[]{pads[0],pads[1]},backgroundDiagnostic:true);
            ground.PlaceCombatReviewSeat(0,new Vector3(0,-1.2f,-5),0);ground.PlaceCombatReviewSeat(1,new Vector3(1.5f,-1.2f,-2),0,210);
            SelectAll();yield return Capture("06-two-seats-world-and-viewmodel");
            ground.StartCombatReview(pads,backgroundDiagnostic:true);
            for(int i=0;i<4;i++)ground.PlaceCombatReviewSeat(i,new Vector3(0,-1.2f,-6+i*3),0);
            SelectAll();yield return Capture("07-four-seats-pulse");
            File.WriteAllText(Path.Combine(directory,"complete.json"),JsonUtility.ToJson(new Result{arena=ground.Session.ArenaIdentity,profile=ground.LabSavedIdentity,muted=AudioListener.volume==0,explosions=explosions,width=Screen.width,height=Screen.height},true));
            Debug.Log("PULSE_NATIVE_REVIEW_COMPLETE "+directory);Application.Quit();
        }
        void SelectAll()
        {
            var a=new LocalAction[ground.Session.ParticipantCount];for(int i=0;i<a.Length;i++)a[i].SelectWeapon=WeaponSelection.RocketLauncher;
            ground.Session.Tick(a,.02f);for(int i=0;i<51;i++)ground.Session.Tick(new LocalAction[a.Length],.02f);
        }
        void OnDestroy(){if(pads!=null)foreach(var p in pads)if(p!=null&&p.added)InputSystem.RemoveDevice(p);}
        [Serializable] sealed class Result {public string classification="AUTOMATED_NATIVE_PLAYER_NOT_HUMAN_ACCEPTANCE",arena,profile;public bool muted;public int explosions,width,height;}
    }
}
#endif

#endif
