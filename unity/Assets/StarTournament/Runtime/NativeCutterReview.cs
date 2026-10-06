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
    /// <summary>Bounded diagnostic fixture. Timings and poses are test inputs, not balance defaults.</summary>
    public sealed class NativeCutterReview:MonoBehaviour
    {
        ProvingGround ground;string directory;Gamepad[] pads;
        void Click(string name)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name).onClick.Invoke();
        IEnumerator Capture(string name)
        {
            ground.SendMessage("RefreshInterface");if(ground.Session!=null)ground.SendMessage("LateUpdate");
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"));
            if(ground.Session!=null)File.WriteAllText(Path.Combine(directory,name+".json"),JsonUtility.ToJson(ground.Session.Capture(),true));
            yield return new WaitForSecondsRealtime(.3f);
        }
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();directory=args[Array.IndexOf(args,"-cutterReview")+1];Directory.CreateDirectory(directory);
            ground=GetComponent<ProvingGround>();AudioListener.volume=0;Application.runInBackground=true;
            yield return new WaitForSecondsRealtime(1);ground.enabled=false;
            Click("main-action-2");yield return null;Click("group-cutter");yield return null;yield return Capture("01-lab-cutter");Click("group-cutter-effects");yield return null;yield return Capture("01b-lab-effects");
            ground.GetComponentsInChildren<InputField>(true).Single(f=>f.name=="lab-search").text="cutter.width";
            yield return null;yield return Capture("01c-lab-width");Click("lab-back");
            pads=Enumerable.Range(0,4).Select(_=>InputSystem.AddDevice<Gamepad>()).ToArray();
            ground.StartCombatReview(new[]{pads[0]},backgroundDiagnostic:true,ensureOpponent:true);
            Place(0,new Vector3(0,-1.2f,-5));Place(1,new Vector3(0,-1.2f,5));SelectAll();yield return Capture("02-first-person");
            Fire(.075f);yield return Capture("03-growth");Fire(.075f);yield return Capture("04-full-beam");
            ground.Session.Tick(new LocalAction[ground.Session.ParticipantCount],.02f);
            if(ground.Session.Beam(0).Active)throw new Exception("Release left beam active");
            for(int seats=2;seats<=4;seats++)
            {
                ground.StartCombatReview(pads.Take(seats).ToArray(),backgroundDiagnostic:true);
                for(int i=0;i<seats;i++)Place(i,new Vector3(0,-1.2f,-6+i*3));
                SelectAll();Fire(.15f);yield return Capture("0"+(seats+3)+"-seats-"+seats);
            }
            var watch=System.Diagnostics.Stopwatch.StartNew();double maxFrame=0;double previous=0;
            // Bounded diagnostic only: no reference-hardware performance acceptance.
            for(int frame=0;frame<120;frame++){Fire(.01f);ground.SendMessage("LateUpdate");yield return new WaitForEndOfFrame();double now=watch.Elapsed.TotalMilliseconds;maxFrame=Math.Max(maxFrame,now-previous);previous=now;}
            File.WriteAllText(Path.Combine(directory,"four-beam-performance.json"),JsonUtility.ToJson(new Performance{frames=120,totalMilliseconds=watch.Elapsed.TotalMilliseconds,maxFrameMilliseconds=maxFrame},true));
            ground.Session.ClearInput();for(int i=0;i<ground.Session.ParticipantCount;i++)if(ground.Session.Beam(i).Active)throw new Exception("Focus clear left active beam");
            File.WriteAllText(Path.Combine(directory,"complete.json"),JsonUtility.ToJson(new Result{arena=ground.Session.ArenaIdentity,profile=ground.LabSavedIdentity,muted=AudioListener.volume==0,width=Screen.width,height=Screen.height},true));
            Debug.Log("CUTTER_NATIVE_REVIEW_COMPLETE "+directory);Application.Quit();
        }
        void Place(int i,Vector3 position)=>ground.PlaceCombatReviewSeat(i,position,0);
        void Fire(float dt){var a=new LocalAction[ground.Session.ParticipantCount];for(int i=0;i<a.Length;i++)a[i].FireHeld=true;ground.Session.Tick(a,dt);}
        void SelectAll(){var a=new LocalAction[ground.Session.ParticipantCount];for(int i=0;i<a.Length;i++)a[i].SelectWeapon=WeaponSelection.Cutter;ground.Session.Tick(a,.02f);ground.Session.Tick(new LocalAction[a.Length],1);}
        void OnDestroy(){if(pads!=null)foreach(var p in pads)if(p!=null&&p.added)InputSystem.RemoveDevice(p);}
        [Serializable] sealed class Performance {public string classification="DIAGNOSTIC_CURRENT_MAC_NOT_REFERENCE_HARDWARE_ACCEPTANCE";public int frames;public double totalMilliseconds,maxFrameMilliseconds;}
        [Serializable] sealed class Result {public string classification="AUTOMATED_NATIVE_PLAYER_NOT_HUMAN_ACCEPTANCE",arena,profile;public bool muted;public int width,height;}
    }
}

#endif
