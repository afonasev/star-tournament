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
    /// <summary>Muted diagnostic scenes and synthetic devices, never physical input acceptance.</summary>
    public sealed class NativeBloodReview:MonoBehaviour
    {
        ProvingGround ground;string directory;Gamepad[] pads;InputSettings.BackgroundBehavior previousBackground;bool changedBackground;
        void Click(string name)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name).onClick.Invoke();
        void Render(){ground.SendMessage("RefreshInterface");ground.SendMessage("LateUpdate");}
        IEnumerator Capture(string name){Render();yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"));yield return new WaitForSecondsRealtime(.2f);}
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();directory=args[Array.IndexOf(args,"-bloodReview")+1];Directory.CreateDirectory(directory);ground=GetComponent<ProvingGround>();AudioListener.volume=0;Application.runInBackground=true;
            previousBackground=InputSystem.settings.backgroundBehavior;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;changedBackground=true;
            yield return new WaitForSecondsRealtime(1);ground.enabled=false;
            Click("main-action-2");yield return null;
            foreach(var group in new[]{"blood-drops","blood-marks","blood-color"}){Click("group-"+group);yield return null;yield return Capture("01-lab-"+group);}
            Click("lab-back");pads=Enumerable.Range(0,4).Select(_=>InputSystem.AddDevice<Gamepad>()).ToArray();
            int bursts=0,marks=0;
            foreach(int seats in new[]{1,4})
            {
                ground.StartCombatReview(pads.Take(seats).ToArray(),backgroundDiagnostic:true,ensureOpponent:true);
                for(int i=0;i<ground.Session.ParticipantCount;i++)ground.PlaceCombatReviewSeat(i,new Vector3(i<2?0:i==2?-.9f:.9f,-1.2f,i==0?2:i==1?5:1),0,i==1?180:0);
                var snapshot=ground.Session.Capture();snapshot.Lives[1].Armor=0;ground.Session.Restore(snapshot);
                ground.Session.Tick(new LocalAction[ground.Session.ParticipantCount],.02f); // Restore requires an input release before a fresh shot.
                DamageNotice notice=default;ground.Session.Damaged+=n=>notice=n;
                var actions=new LocalAction[ground.Session.ParticipantCount];actions[0].Fire=true;ground.Session.Tick(actions,.02f);
                for(int tick=0;tick<40&&notice.HealthLost<=0;tick++){ground.Session.Tick(new LocalAction[ground.Session.ParticipantCount],.01f);Render();}
                if(notice.HealthLost<=0||!notice.Impact.Valid)throw new InvalidOperationException("Blood review missed actual rifle health impact");
                bursts++;ground.Session.Tick(new LocalAction[ground.Session.ParticipantCount ],.14f);yield return Capture("02-hit-"+seats+"-views");
                for(int tick=0;tick<100;tick++){ground.Session.Tick(new LocalAction[ground.Session.ParticipantCount],.02f);Render();}
                marks=ground.GetComponentsInChildren<MeshRenderer>().Count(r=>r.name=="blood-surface-mark");if(marks==0)throw new InvalidOperationException("Blood did not leave surface marks");
                var surfaceMark=ground.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r=>r.name=="blood-surface-mark");
                if(seats==1&&surfaceMark){var spot=surfaceMark.bounds.center;var feet=spot-Vector3.forward*2.5f;feet.y=-1.2f;var delta=spot-(feet+Vector3.up*ground.Profile.Get("camera.eyeHeight"));float pitch=-Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude)*Mathf.Rad2Deg;ground.PlaceCombatReviewSeat(0,feet,pitch,Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg);}
                yield return Capture("03-marks-"+seats+"-views");
                var before=ground.GetComponentsInChildren<Transform>().Where(t=>t.name=="blood-surface-mark").Select(t=>t.position).ToArray();yield return new WaitForSecondsRealtime(.3f);Render();
                if(ground.GetComponentsInChildren<MeshRenderer>().Count(r=>r.name=="blood-surface-mark")!=before.Length)throw new InvalidOperationException("Paused clock changed marks");
                for(int tick=0;tick<1600;tick++){ground.Session.Tick(new LocalAction[ground.Session.ParticipantCount],.02f);if(tick%10==0)Render();}
                Render();if(ground.GetComponentsInChildren<MeshRenderer>().Any(r=>r.name=="blood-surface-mark"))throw new InvalidOperationException("Marks did not expire");yield return Capture("04-expired-"+seats+"-views");
            }
            File.WriteAllText(Path.Combine(directory,"complete.json"),JsonUtility.ToJson(new Result{bursts=bursts,marks=marks,muted=AudioListener.volume==0,arena=ground.Session.ArenaIdentity,profile=ground.LabSavedIdentity,diagnostic="synthetic devices; physical acceptance not performed"},true));
            Debug.Log("BLOOD_NATIVE_REVIEW_COMPLETE");Application.Quit();
        }
        [Serializable] class Result{public int bursts,marks;public bool muted;public string arena,profile,diagnostic;}
        void OnDestroy(){if(changedBackground)InputSystem.settings.backgroundBehavior=previousBackground;if(pads!=null)foreach(var pad in pads)if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);}
    }
}

#endif
