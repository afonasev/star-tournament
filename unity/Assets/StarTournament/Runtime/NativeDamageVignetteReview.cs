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
    /// <summary>Native rendering evidence with synthetic fixtures; human visual acceptance is separate.</summary>
    public sealed class NativeDamageVignetteReview:MonoBehaviour
    {
        ProvingGround ground;string directory;Gamepad[] pads;
        readonly List<string> checks=new List<string>();
        DamageVignette Effect(int seat)=>ground.GetComponentsInChildren<DamageVignette>(true).Single(x=>x.name=="damage-vignette-"+seat);
        void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);checks.Add(message);}
        void Render(){ground.SendMessage("RefreshInterface");ground.SendMessage("LateUpdate");Canvas.ForceUpdateCanvases();}
        void Tick(float seconds){ground.Session.Tick(new LocalAction[ground.Session.ParticipantCount],seconds);Render();}
        void Hit(int seat,float damage){ground.Session.ApplyDamage(seat,ground.Session.Life(seat).Life,damage);Tick(ground.Profile.Get("ui.damageVignette.attackSeconds"));}
        void Armor(int seat,float amount)
        {var snapshot=ground.Session.Capture();var life=snapshot.Lives[seat];life.Armor=amount;snapshot.Lives[seat]=life;ground.Session.Restore(snapshot);Render();}
        void StartMatch(int count)
        {
            ground.StartCombatReview(pads.Take(count).ToArray(),backgroundDiagnostic:true,ensureOpponent:count==1);
            for(int i=0;i<ground.Session.ParticipantCount;i++)ground.PlaceCombatReviewSeat(i,new Vector3(-5+i*3,-1.2f,-5),0,0);
            Render();
        }
        IEnumerator Capture(string name)
        {
            Render();Require(ground.Running,"active match: "+name);
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"));
            File.WriteAllText(Path.Combine(directory,name+"-state.json"),JsonUtility.ToJson(ground.Session.Capture(),true));
            yield return new WaitForSecondsRealtime(.15f);
        }
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();directory=args[Array.IndexOf(args,"-damageVignetteReview")+1];Directory.CreateDirectory(directory);
            ground=GetComponent<ProvingGround>();AudioListener.volume=0;Application.runInBackground=true;
            yield return new WaitForSecondsRealtime(1);ground.enabled=false;
            pads=Enumerable.Range(0,4).Select(_=>InputSystem.AddDevice<Gamepad>()).ToArray();
            StartMatch(1);yield return Capture("01-clean");Armor(0,40);Hit(0,10);
            Require(!Effect(0).HealthHit&&Effect(0).Opacity>0,"armor-only blue");yield return Capture("02-shield-blue");
            float frozen=Effect(0).Opacity;double time=ground.Session.Time;yield return new WaitForSecondsRealtime(.2f);Render();
            Require(ground.Session.Time==time&&Effect(0).Opacity==frozen,"session pause freezes pulse");
            Hit(0,35);Require(Effect(0).HealthHit,"armor penetration red");yield return Capture("03-penetration-red");
            for(int i=0;i<5;i++){Hit(0,1);Require(Effect(0).Opacity<=ground.Profile.Get("ui.damageVignette.opacity"),"series opacity capped "+i);}
            yield return Capture("04-series-red");Tick(1);Require(Effect(0).Opacity==0,"pulse fades completely");yield return Capture("05-faded");
            var snapshot=ground.Session.Capture();Hit(0,2);ground.Session.Restore(snapshot);Render();Require(Effect(0).Opacity==0,"restore clears impulse");
            for(int count=2;count<=4;count++)
            {
                StartMatch(count);Armor(0,30);Hit(0,5);Hit(1,5);
                Require(!Effect(0).HealthHit&&Effect(0).Opacity>0&&Effect(1).HealthHit&&Effect(1).Opacity>0,"independent blue and red "+count+" views");
                for(int seat=2;seat<count;seat++)Require(Effect(seat).Opacity==0,"untouched viewport "+seat+" of "+count);
                Require(Effect(0).transform.GetSiblingIndex()==0&&!Effect(0).raycastTarget,"under HUD without input interception "+count);
                yield return Capture("0"+(count+4)+"-"+count+"-views");
            }
            ground.Session.ApplyDamage(0,ground.Session.Life(0).Life,1000);Render();Require(Effect(0).Opacity==0,"killcam hides pulse");
            for(int i=0;i<300&&ground.Session.Life(0).Dead;i++)Tick(.02f);
            Require(!ground.Session.Life(0).Dead&&Effect(0).Opacity==0,"respawn clears impulse");
            Hit(0,5);ground.SendMessage("Repeat");Render();Require(Effect(0).Opacity==0,"Repeat clears impulse");
            Hit(0,5);ground.SendMessage("Menu");Render();Require(Effect(0).Opacity==0,"menu clears impulse");
            File.WriteAllText(Path.Combine(directory,"complete.json"),JsonUtility.ToJson(new Result{checks=checks.ToArray(),muted=AudioListener.volume==0,width=Screen.width,height=Screen.height},true));
            Debug.Log("DAMAGE_VIGNETTE_NATIVE_REVIEW_COMPLETE "+directory);Application.Quit();
        }
        void OnDestroy(){if(pads!=null)foreach(var pad in pads)if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);}
        [Serializable]sealed class Result{public string classification="AUTOMATED_NATIVE_PLAYER_NOT_HUMAN_ACCEPTANCE";public bool muted;public int width,height;public string[] checks;}
    }
}
#endif

#endif
