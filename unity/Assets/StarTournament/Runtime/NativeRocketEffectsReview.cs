#if UNITY_EDITOR || DEVELOPMENT_BUILD || STAR_TOURNAMENT_DEVELOPMENT_QA
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
namespace StarTournament.ProvingGround
{
    /// <summary>Bounded actual Player captures; scripted contacts are diagnostics, not human acceptance.</summary>
    public sealed class NativeRocketEffectsReview:MonoBehaviour
    {
        ProvingGround ground;Gamepad[] pads;string directory;int captures;readonly List<ContactEvidence> contacts=new List<ContactEvidence>();
        InputSettings.BackgroundBehavior previousBackground;bool backgroundChanged;
        // Only the explicit diagnostic flag installs this bounded failure handler.
        void Awake(){Application.logMessageReceived+=Failure;}
        void Failure(string message,string trace,LogType type){if(type==LogType.Exception)Application.Quit(1);}
        [Serializable]sealed class ContactEvidence{public string label;public int explosions,remainingRockets;public Vector3 contact;}
        [Serializable]sealed class Result{public string classification="AUTOMATED_NATIVE_PLAYER_NOT_HUMAN_ACCEPTANCE",arena;public bool muted;public int width,height;public ContactEvidence[] contacts;}
        [Serializable]sealed class VfxEvidence{public float gameplayRadius,flameEnvelope;public int activeLights;}
        IEnumerator Capture(string name)
        {
            ground.SendMessage("RefreshInterface");ground.SendMessage("LateUpdate");
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,(++captures).ToString("D2")+"-"+name+".png"));
            File.WriteAllText(Path.Combine(directory,captures.ToString("D2")+"-"+name+".json"),JsonUtility.ToJson(ground.Session.Capture(),true));
            File.WriteAllText(Path.Combine(directory,captures.ToString("D2")+"-"+name+"-vfx.json"),JsonUtility.ToJson(new VfxEvidence{gameplayRadius=ground.Session.RocketBlastRadius,flameEnvelope=ground.RocketEffectsForReview.FlameEnvelope,activeLights=ground.RocketEffectsForReview.ActiveLights},true));
            yield return new WaitForSecondsRealtime(.15f);
        }
        void Tick(float seconds)
        {for(int i=0;i<Mathf.RoundToInt(seconds/.02f);i++)ground.Session.Tick(new LocalAction[ground.Session.ParticipantCount],.02f);}
        void EquipAndSelect()
        {
            var state=ground.Session.Capture();for(int i=0;i<state.Lives.Length;i++){state.Lives[i].RocketOwned=true;state.Lives[i].RocketAmmo=50;}ground.Session.Restore(state);
            var actions=new LocalAction[ground.Session.ParticipantCount];for(int i=0;i<actions.Length;i++)actions[i].SelectWeapon=WeaponSelection.RocketLauncher;
            ground.Session.Tick(actions,.02f);Tick(1.3f);
        }
        void Begin(int seats)
        {
            ground.StartCombatReview(pads.Take(seats).ToArray(),backgroundDiagnostic:true,ensureOpponent:seats==1);
            for(int i=0;i<ground.Session.ParticipantCount;i++)ground.PlaceCombatReviewSeat(i,new Vector3(-32+i*64,4,28),0);
            EquipAndSelect();
        }
        NativeCombatSessionSnapshot Fire()
        {
            var actions=new LocalAction[ground.Session.ParticipantCount];actions[0].Fire=true;ground.Session.Tick(actions,.02f);
            if(ground.Session.Rockets.Length!=1)throw new InvalidOperationException("Expected confirmed rocket");
            ground.Session.ClearInput();return ground.Session.Capture();
        }
        IEnumerator Contact(string label,Vector3 launch,Vector3 direction)
        {
            var state=Fire();state.Rockets[0].Position=launch;state.Rockets[0].Direction=direction;ground.Session.Restore(state);
            var item=new ContactEvidence{label=label};Action<RocketExplosion> observe=e=>{item.explosions++;item.contact=e.Position;};ground.Session.RocketExploded+=observe;
            yield return Capture(label+"-flight");
            for(int i=0;i<20&&item.explosions==0;i++)Tick(.02f);
            if(item.explosions!=1)throw new InvalidOperationException("Expected one contact: "+label);
            Tick(.08f);yield return Capture(label+"-early");
            // Capture again without advancing session time: actual pause-freeze evidence.
            if(label=="floor")yield return Capture(label+"-paused");
            if(label=="floor"||label=="wall")
            {ground.RocketEffectsForReview.SetDiagnosticLights(false);yield return Capture(label+"-light-off");ground.RocketEffectsForReview.SetDiagnosticLights(true);yield return Capture(label+"-light-on");}
            Tick(.18f);yield return Capture(label+"-fire");Tick(.36f);yield return Capture(label+"-late");
            item.remainingRockets=ground.Session.Rockets.Length;if(item.remainingRockets!=0)throw new InvalidOperationException("Contact retained flight");
            ground.Session.RocketExploded-=observe;contacts.Add(item);
        }
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"-rocketEffectsReview");directory=args[flag+1];Directory.CreateDirectory(directory);
            ground=GetComponent<ProvingGround>();AudioListener.volume=0;Application.runInBackground=true;
            previousBackground=InputSystem.settings.backgroundBehavior;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;backgroundChanged=true;
            yield return new WaitForSecondsRealtime(1);Screen.SetResolution(1920,1080,FullScreenMode.Windowed);yield return new WaitForSecondsRealtime(.3f);
            ground.enabled=false;pads=Enumerable.Range(0,4).Select(_=>InputSystem.AddDevice<Gamepad>()).ToArray();foreach(var pad in pads)if(!pad.enabled)InputSystem.EnableDevice(pad);
            Begin(1);ground.PlaceCombatReviewSeat(0,new Vector3(0,4,-5),18);yield return Contact("floor",new Vector3(0,4.8f,0),Vector3.down);
            Begin(1);var wall=ground.CameraReviewArena.Definition.Solids.Single(s=>s.Id=="hall-north-left");
            var wallPoint=wall.Position+Vector3.back*(wall.Size.z/2+.8f);
            ground.PlaceCombatReviewSeat(0,wallPoint+new Vector3(0,-1.5f,-5),0);yield return Contact("wall",wallPoint,Vector3.forward);
            Begin(1);ground.PlaceCombatReviewSeat(0,new Vector3(0,4,-5),0);ground.PlaceCombatReviewSeat(1,new Vector3(0,4,1),0,180);
            yield return Contact("participant",new Vector3(0,5.2f,-.5f),Vector3.forward);
            foreach(string id in new[]{"balcony-west","bridge-north","bridge-south"})foreach(int side in new[]{1,-1})
            {
                Begin(1);var grate=ground.CameraReviewArena.Definition.Solids.Single(s=>s.Id==id);
                var art=ProvingProfile.CreateCombatBowlRingPresentationDefault();float pitch=art.Get("ring.gratingPitch"),bar=art.Get("ring.gratingBar");
                float Gap(float size)=>-size/2+bar/2+(Mathf.Floor((size/2-bar/2)/pitch)+.5f)*pitch;
                var point=grate.Position+new Vector3(Gap(grate.Size.x),0,Gap(grate.Size.z));
                ground.PlaceCombatReviewSeat(0,new Vector3(point.x,side>0?4:0,point.z-5),side>0?18:-18);
                yield return Contact(id+(side>0?"-above":"-below"),point+Vector3.up*side*(grate.Size.y/2+.8f),Vector3.down*side);
            }
            Begin(4);for(int i=0;i<4;i++)ground.PlaceCombatReviewSeat(i,new Vector3((i-1.5f)*1.1f,4,-5),18);
            var a=new LocalAction[ground.Session.ParticipantCount];for(int i=0;i<4;i++)a[i].Fire=true;ground.Session.Tick(a,.02f);
            var snapshot=ground.Session.Capture();if(snapshot.Rockets.Length!=4)throw new InvalidOperationException("Four launches missing");
            for(int i=0;i<4;i++){snapshot.Rockets[i].Position=new Vector3((i-1.5f)*.8f,4.8f,0);snapshot.Rockets[i].Direction=Vector3.down;}ground.Session.Restore(snapshot);
            yield return Capture("four-flight");Tick(.12f);yield return Capture("four-overlap-early");Tick(.5f);yield return Capture("four-overlap-late");
            // Restore cleans transient particles; Repeat uses the ordinary new-session path.
            ground.Session.Restore(snapshot);yield return Capture("four-restored-flight");Begin(1);yield return Capture("repeat-clean");
            File.WriteAllText(Path.Combine(directory,"complete.json"),JsonUtility.ToJson(new Result{arena=ground.Session.ArenaIdentity,muted=AudioListener.volume==0,width=Screen.width,height=Screen.height,contacts=contacts.ToArray()},true));
            Debug.Log("ROCKET_EFFECTS_NATIVE_REVIEW_COMPLETE "+directory);Application.Quit();
        }
        void OnDestroy(){Application.logMessageReceived-=Failure;if(backgroundChanged)InputSystem.settings.backgroundBehavior=previousBackground;if(pads!=null)foreach(var pad in pads)if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);}
    }
}
#endif

#endif
