#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace StarTournament.ProvingGround
{
    /// <summary>Opt-in controlled native damage/UI evidence. Synthetic input is not physical acceptance.</summary>
    public sealed class NativeScoreboardReview:MonoBehaviour
    {
        ProvingGround ground;string directory;readonly Gamepad[] pads=new Gamepad[4];Keyboard keyboard;Mouse mouse;bool keyboardView;
        [Serializable] sealed class Evidence
        {
            public string state,scope="NATIVE_PLAYER_CONTROLLED_DAMAGE_SYNTHETIC_INPUT_NOT_HUMAN_OR_PHYSICAL_ACCEPTANCE";
            public bool focused,muted,running;public int width,height;
            public NativeCompositionSnapshot composition;public NativeCombatSessionSnapshot session;
        }
        Button B(string name)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        void Check(bool value,string message){if(!value)throw new InvalidOperationException("SCOREBOARD_REVIEW "+message);}
        IEnumerator Configure(int views,int participants,bool teams,bool useKeyboard=false)
        {
            Time.timeScale=1;
            var roster=teams?new NativeMatchRoster(NativeMatchMode.Teams,Enumerable.Range(0,participants).Select(i=>i<participants-3?NativeTeam.TeamA:NativeTeam.TeamB).ToArray()):NativeMatchRoster.Ffa(participants);
            string[] names={"AFONASEV","VEGA","NOVA","ATLAS","ECHO","ORION","RIFT","ZENITH"};
            var info=Enumerable.Range(0,participants).Select(i=>new NativeParticipantInfo(i==0?NativeParticipantKind.LocalHuman:NativeParticipantKind.Bot,names[i],NativeStandingsView.Identity(roster.Read(),i,false),i==0?-1:i%3)).ToArray();
            var composition=new NativeMatchComposition(roster,info,Enumerable.Range(0,views).ToArray());
            var config=NativeMatchConfiguration.Default(ground.MatchProfile);config.DurationMinutes=1;ground.ConfigureBotTacticsReview(config);
            keyboardView=useKeyboard;var devices=pads.Take(views).Cast<InputDevice>().ToArray();if(useKeyboard)devices[0]=keyboard;
            ground.StartBotReview(composition,17029,devices);
            yield return new WaitForSecondsRealtime(.15f);Time.timeScale=0;ground.SetParticipantReviewStandings(false);
            Check(ground.Running,"start");
        }
        DamageResult Hit(int victim,int source,float damage,bool rocket=false)
        {
            if(!rocket)return ground.Session.ApplyDamage(victim,ground.Session.Life(victim).Life,damage,source,source<0?0:ground.Session.Life(source).Life);
            return (DamageResult)typeof(NativeCombatSession).GetMethod("ApplyDamagePolicy",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ground.Session,new object[]{victim,ground.Session.Life(victim).Life,damage,source,source<0?0:ground.Session.Life(source).Life,true});
        }
        void Step(int count){for(int i=0;i<count;i++)ground.Session.Tick(new LocalAction[ground.Composition.ParticipantCount],.02f);}
        IEnumerator Capture(string label)
        {
            while(!Application.isFocused)yield return null;
            yield return null;yield return new WaitForEndOfFrame();
            Check(AudioListener.volume==0,"unmuted");
            File.WriteAllText(Path.Combine(directory,label+".json"),JsonUtility.ToJson(new Evidence{state=label,focused=Application.isFocused,muted=AudioListener.volume==0,running=ground.Running,width=Screen.width,height=Screen.height,composition=ground.Composition.Read(),session=ground.Session.Capture()},true));
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,label+".png"));yield return new WaitForSecondsRealtime(.2f);
        }
        IEnumerator Held(bool held)
        {
            if(keyboardView)InputSystem.QueueStateEvent(keyboard,held?new KeyboardState(Key.Tab):new KeyboardState());
            else InputSystem.QueueStateEvent(pads[0],held?new GamepadState().WithButton(GamepadButton.Select):new GamepadState());
            Time.timeScale=1;yield return new WaitForSecondsRealtime(.06f);Time.timeScale=0;yield return null;
            Check(ground.transform.Find("native-ui/standings-0").gameObject.activeSelf==held,"View held projection");
        }
        IEnumerator Finish(string label)
        {
            int guard=20000;while(ground.Session.Match.Phase!=NativeMatchPhase.Finished&&guard-->0){Step(1);if(guard%500==0)yield return null;}
            Check(ground.Session.Match.Phase==NativeMatchPhase.Finished,"finish");Time.timeScale=1;yield return new WaitForFixedUpdate();Time.timeScale=0;yield return Capture(label);
        }
        IEnumerator Start()
        {
            ground=GetComponent<ProvingGround>();var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-scoreboardEvidence");directory=at>=0&&at+1<args.Length?args[at+1]:Path.Combine(Application.persistentDataPath,"scoreboard-review");Directory.CreateDirectory(directory);AudioListener.volume=0;
            while(!Application.isFocused||!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
            for(int i=0;i<4;i++)pads[i]=InputSystem.AddDevice<Gamepad>();keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();
            yield return Configure(1,2,false);yield return Held(true);yield return Capture("01-ffa-two-tie");yield return Held(false);
            Hit(1,0,500);Step(1);yield return Held(true);yield return Capture("02-ffa-live-leader");Hit(0,-1,500);yield return Capture("03-ffa-dead-leader");Step(450);yield return Capture("04-ffa-respawn");yield return Held(false);yield return Finish("05-ffa-final");
            yield return Configure(2,8,false);Hit(7,0,500);Hit(0,0,500);Step(1);yield return Held(true);yield return Capture("06-ffa-eight-negative-suffix");yield return Held(false);
            yield return Configure(3,8,true);yield return Capture("07-three-views-eight-uneven-teams-tie");
            // An equipped, positioned diagnostic fixture fires a real native rocket: source + two allies.
            ground.PlaceCombatReviewSeat(0,new Vector3(-8,0,0),0,0);ground.PlaceCombatReviewSeat(1,new Vector3(-8,0,2),0,0);ground.PlaceCombatReviewSeat(2,new Vector3(-7,0,2),0,0);
            for(int i=3;i<8;i++)ground.PlaceCombatReviewSeat(i,new Vector3(8,0,i),0,0);
            var rocket=ground.Session.Capture();rocket.Lives[0].RocketOwned=true;rocket.Lives[0].RocketAmmo=20;
            for(int i=0;i<3;i++)rocket.Lives[i].Health=1;ground.Session.Restore(rocket);
            var select=new LocalAction[8];select[0].SelectWeapon=WeaponSelection.RocketLauncher;ground.Session.Tick(select,.02f);Step(51);
            var fire=new LocalAction[8];fire[0].Fire=true;ground.Session.Tick(fire,.02f);Step(8);
            var shooter=ground.Session.Match.Read().Standings.Single(r=>r.Seat==0);Check(shooter.SelfKills==1&&shooter.AllyKills==2&&shooter.AccumulatedPenalty==600,"multi-victim rocket penalty");
            yield return Capture("07a-real-rocket-self-and-two-allies");
            // Reset the controlled fixture so the V4 table examples have independent evidence.
            yield return Configure(3,8,true);
            // Unified policy: half friendly damage before armor, then actual shield plus HP.
            Check(Hit(1,0,50).Applied==25,"half friendly damage");var state=ground.Session.Capture();state.Lives[7].Armor=50;ground.Session.Restore(state);
            Check(Hit(7,0,500).Applied==150,"actual shield and HP without overkill");Hit(6,0,500);Hit(5,0,500);Hit(1,0,30,true);Hit(2,0,500,true);Step(1);
            yield return Capture("08-teams-red-suffix-live-leader");Hit(0,-1,500);yield return Capture("09-teams-dead-row-and-team-leader");Step(450);yield return Capture("10-teams-respawn");
            var before=JsonUtility.ToJson(ground.Session.Match.Read());ground.SendMessage("Pause","Scoreboard QA");yield return Capture("11-pause");Check(JsonUtility.ToJson(ground.Session.Match.Read())==before,"pause changed state");B("Продолжить").onClick.Invoke();Time.timeScale=0;yield return Capture("12-resumed");
            // Restore through the actual native session; retain immutable net counters and composition.
            var saved=JsonUtility.FromJson<NativeCombatSessionSnapshot>(JsonUtility.ToJson(ground.Session.Capture()));ground.Session.Restore(saved);yield return Capture("13-restored");
            yield return Finish("14-teams-final");string composition=JsonUtility.ToJson(ground.Composition.Read());B("Повторить матч").onClick.Invoke();Time.timeScale=0;yield return null;
            Check(ground.Session.Match.Read().Standings.All(r=>r.Score==0&&r.AccumulatedPenalty==0&&r.AllyKills==0&&r.SelfKills==0&&r.AllyDamageDealt==0),"Repeat counters");Check(JsonUtility.ToJson(ground.Composition.Read())==composition,"Repeat composition");yield return Capture("15-teams-repeat-cleared");
            yield return Configure(4,8,true);Hit(7,0,500);Step(1);yield return Held(true);yield return Capture("16-four-views-eight-teams-held");yield return Held(false);
            yield return Configure(1,8,false,true);
            // Controlled health refill keeps one target alive while actual capped hits build a four-digit group.
            for(int i=0;i<20;i++){var refill=ground.Session.Capture();refill.Lives[7].Health=100;ground.Session.Restore(refill);Hit(7,0,80);}
            Hit(7,0,500);Hit(6,0,500);Hit(5,0,500);Hit(0,0,500);Step(1);
            yield return Held(true);yield return Capture("17-ffa-eight-wide-values-tab");yield return Held(false);yield return Finish("18-ffa-eight-final");
            Debug.Log("NATIVE_SCOREBOARD_REVIEW_COMPLETE "+directory);
        }
        void OnDestroy(){Time.timeScale=1;foreach(var p in pads)if(p!=null&&p.added)InputSystem.RemoveDevice(p);if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);}
    }
}
#endif
