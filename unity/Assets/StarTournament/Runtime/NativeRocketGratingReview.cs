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
    /// <summary>Explicit bounded native Player fixture; no physical/human acceptance implied.</summary>
    public sealed class NativeRocketGratingReview:MonoBehaviour
    {
        ProvingGround ground;Gamepad pad;string directory;int captures;
        [Serializable] sealed class ContactEvidence {public string surface,side;public Vector3 launch,contact;public int explosions,remainingRockets;}
        [Serializable] sealed class Result {public string classification="AUTOMATED_NATIVE_PLAYER_NOT_HUMAN_ACCEPTANCE",arena;public bool muted;public int width,height;public ContactEvidence[] contacts;}
        IEnumerator Capture(string name)
        {
            ground.SendMessage("RefreshInterface");ground.SendMessage("LateUpdate");
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,(++captures).ToString("D2")+"-"+name+".png"));
            File.WriteAllText(Path.Combine(directory,captures.ToString("D2")+"-"+name+".json"),JsonUtility.ToJson(ground.Session.Capture(),true));
            yield return new WaitForSecondsRealtime(.15f);
        }
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"-rocketGratingReview");directory=args[flag+1];Directory.CreateDirectory(directory);
            ground=GetComponent<ProvingGround>();AudioListener.volume=0;Application.runInBackground=true;
            yield return new WaitForSecondsRealtime(1);ground.enabled=false;pad=InputSystem.AddDevice<Gamepad>();var evidence=new List<ContactEvidence>();
            foreach(string id in new[]{"balcony-west","bridge-north","bridge-south"})foreach(int side in new[]{1,-1})
            {
                ground.StartCombatReview(new[]{pad},backgroundDiagnostic:true,ensureOpponent:true);
                var arena=ground.GetComponentInChildren<ProvingArena>();var grate=arena.Definition.Solids.Single(s=>s.Id==id);
                // Fixture chooses the gap nearest the middle; production parameters remain unchanged.
                var art=ProvingProfile.CreateCombatBowlRingPresentationDefault();float pitch=art.Get("ring.gratingPitch"),bar=art.Get("ring.gratingBar");
                float Gap(float size)=>-size/2+bar/2+(Mathf.Floor((size/2-bar/2)/pitch)+.5f)*pitch;
                var point=grate.Position+new Vector3(Gap(grate.Size.x),0,Gap(grate.Size.z));
                ground.PlaceCombatReviewSeat(0,new Vector3(point.x,side>0?4:0,point.z-2),side>0?45:-45);
                ground.PlaceCombatReviewSeat(1,new Vector3(32,4,28),0);
                var snapshot=ground.Session.Capture();snapshot.Lives[0].RocketOwned=true;snapshot.Lives[0].RocketAmmo=20;ground.Session.Restore(snapshot);
                var actions=new LocalAction[ground.Session.ParticipantCount];actions[0].SelectWeapon=WeaponSelection.RocketLauncher;ground.Session.Tick(actions,.02f);
                for(int i=0;i<60;i++)ground.Session.Tick(new LocalAction[actions.Length],.02f);
                actions=new LocalAction[actions.Length];actions[0].Fire=true;ground.Session.Tick(actions,.02f);
                if(ground.Session.Rockets.Length!=1)throw new InvalidOperationException("Missing actual Pulse launch");
                // Bind the real launched identity to a reproducible vertical gap crossing in serialized gameplay state.
                snapshot=ground.Session.Capture();var rocket=snapshot.Rockets[0];rocket.Position=point+Vector3.up*side*(grate.Size.y/2+.8f);rocket.Direction=Vector3.down*side;snapshot.Rockets[0]=rocket;ground.Session.Restore(snapshot);
                var item=new ContactEvidence{surface=id,side=side>0?"above":"below",launch=rocket.Position};
                Action<RocketExplosion> observe=e=>{item.explosions++;item.contact=e.Position;};ground.Session.RocketExploded+=observe;
                yield return Capture(id+"-"+item.side+"-flight");
                for(int i=0;i<10&&item.explosions==0;i++)ground.Session.Tick(new LocalAction[actions.Length],.02f);
                ground.Session.Tick(new LocalAction[actions.Length],.04f);item.remainingRockets=ground.Session.Rockets.Length;
                if(item.explosions!=1||item.remainingRockets!=0||Mathf.Abs(item.contact.y-(grate.Position.y+side*(grate.Size.y/2+ground.Profile.Get("player.capsule.skinWidth"))))>.001f)throw new InvalidOperationException("Invalid grating contact "+id+" "+item.side);
                yield return Capture(id+"-"+item.side+"-explosion");
                ground.Session.RocketExploded-=observe;evidence.Add(item);
            }
            File.WriteAllText(Path.Combine(directory,"complete.json"),JsonUtility.ToJson(new Result{arena=ground.Session.ArenaIdentity,muted=AudioListener.volume==0,width=Screen.width,height=Screen.height,contacts=evidence.ToArray()},true));
            Debug.Log("ROCKET_GRATING_NATIVE_REVIEW_COMPLETE "+directory);Application.Quit();
        }
        void OnDestroy(){if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);}
    }
}
#endif

#endif
