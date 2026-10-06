#if UNITY_EDITOR || DEVELOPMENT_BUILD || STAR_TOURNAMENT_DEVELOPMENT_QA
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
            return (DamageResult)typeof(NativeCombatSession).GetMethod("ApplyDamagePolicy",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ground.Session,new object[]{victim,ground.Session.Life(victim).Life,damage,source,source<0?0:ground.Session.Life(source).Life,true,default(FatalImpact)});
        }
        void Step(int count){for(int i=0;i<count;i++)ground.Session.Tick(new LocalAction[ground.Composition.ParticipantCount],.02f);}
        IEnumerator Capture(string label)
        {
            do
            {
                while(!Application.isFocused)yield return null;
                yield return null;yield return new WaitForEndOfFrame();
            }while(!Application.isFocused);
            Check(AudioListener.volume==0,"unmuted");
            File.WriteAllText(Path.Combine(directory,label+".json"),JsonUtility.ToJson(new Evidence{state=label,focused=Application.isFocused,muted=AudioListener.volume==0,running=ground.Running,width=Screen.width,height=Screen.height,composition=ground.Composition.Read(),session=ground.Session.Capture()},true));
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,label+".png"));yield return new WaitForSecondsRealtime(.2f);
        }
        IEnumerator Held(bool held)
        {
            // Focus loss can reset a synthetic device before the queued state is processed.
            // Requeue only after focus returns; the projection assertion remains mandatory.
            for(int attempt=0;attempt<3;attempt++)
            {
                Time.timeScale=0;
                while(!Application.isFocused)yield return null;
                if(keyboardView)InputSystem.QueueStateEvent(keyboard,held?new KeyboardState(Key.Tab):new KeyboardState());
                else InputSystem.QueueStateEvent(pads[0],held?new GamepadState().WithButton(GamepadButton.Select):new GamepadState());
                Time.timeScale=1;yield return new WaitForSecondsRealtime(.06f);Time.timeScale=0;yield return null;
                if(Application.isFocused&&ground.transform.Find("native-ui/standings-0").gameObject.activeSelf==held)
                    yield break;
            }
            throw new InvalidOperationException("SCOREBOARD_REVIEW View held projection after focused input retries");
        }
        IEnumerator Finish(string label)
        {
            int guard=20000;while(ground.Session.Match.Phase!=NativeMatchPhase.Finished&&guard-->0){Step(1);if(guard%500==0)yield return null;}
            Check(ground.Session.Match.Phase==NativeMatchPhase.Finished,"finish");Time.timeScale=1;yield return new WaitForFixedUpdate();Time.timeScale=0;
            var actions=ground.transform.Find("native-ui/setup-pause/menu").GetComponentsInChildren<Button>().Select(b=>b.name).ToArray();
            Check(actions.SequenceEqual(new[]{"Повторить матч","В главное меню"}),"results actions: "+string.Join(",",actions));
            yield return Capture(label);
        }
        void SeedAccuracy()
        {
            var match=ground.Session.Match;
            match.RecordAccuracy(0,WeaponId.Shotgun,8,2);match.RecordAccuracy(0,WeaponId.Rifle,4,3);
            for(int i=1;i<ground.Composition.ParticipantCount;i++)
            {
                match.RecordAccuracy(i,WeaponId.Shotgun,8,i);
                match.RecordAccuracy(i,WeaponId.Rifle,4,i%5);
                match.RecordAccuracy(i,WeaponId.Cutter,2,i*.25);
                match.RecordAccuracy(i,WeaponId.RocketLauncher,3,i%4);
            }
            Check(Math.Abs(match.Read().Standings.Single(r=>r.Seat==0).AccuracyPercent-50)<.000001,"mixed weapon accuracy");
        }
        void CheckAccuracyTable(string path,bool teams)
        {
            var table=ground.transform.Find(path);
            Check(table!=null&&table.gameObject.activeInHierarchy,"accuracy table visible "+path);
            var row=table.GetComponentsInChildren<Text>().Single(t=>t.name=="cell-0"&&t.text=="AFONASEV").transform.parent;
            Check(row.Find("cell-6").GetComponent<Text>().text=="50%","accuracy before score");
            Check(!row.Find("identity-accent").gameObject.activeSelf,"no player leader stripe");
            Check(table.Find("row-0/header-6").GetComponent<StandingsIcon>().Kind==StandingsIcon.Symbol.Percent,"percent header");
            if(teams)foreach(var team in table.GetComponentsInChildren<Text>().Where(t=>t.name=="cell-0"&&t.text.StartsWith("Team ")))
                Check(team.transform.parent.Find("cell-6").GetComponent<Text>().text=="","no fabricated team accuracy");
        }
        IEnumerator Start()
        {
            Application.runInBackground=true;
            ground=GetComponent<ProvingGround>();var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-scoreboardEvidence");directory=at>=0&&at+1<args.Length?args[at+1]:Path.Combine(Application.persistentDataPath,"scoreboard-review");Directory.CreateDirectory(directory);AudioListener.volume=0;
            while(!Application.isFocused||!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
            for(int i=0;i<4;i++)pads[i]=InputSystem.AddDevice<Gamepad>();keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();
            if(args.Contains("-accuracyOnly"))
            {
                yield return Configure(1,8,false,true);SeedAccuracy();Hit(7,0,500);Step(1);
                yield return Held(true);yield return Capture("01-ffa-eight-accuracy-live");CheckAccuracyTable("native-ui/standings-0",false);yield return Held(false);
                yield return Finish("02-ffa-eight-accuracy-results");CheckAccuracyTable("native-ui/setup-pause/results-table",false);
                yield return Configure(3,8,true);SeedAccuracy();Hit(7,0,500);Step(1);
                yield return Capture("03-teams-eight-accuracy-persistent");CheckAccuracyTable("native-ui/persistent-standings",true);
                yield return Finish("04-teams-eight-accuracy-results");CheckAccuracyTable("native-ui/setup-pause/results-table",true);
                Debug.Log("NATIVE_ACCURACY_REVIEW_COMPLETE "+directory);Application.Quit();yield break;
            }
            if(args.Contains("-resultsActionsOnly"))
            {
                var roster=NativeMatchRoster.Ffa(4);
                var info=Enumerable.Range(0,4).Select(i=>new NativeParticipantInfo(NativeParticipantKind.Bot,new[]{"Orion","Vega","Nova","Atlas"}[i],NativeStandingsView.Identity(roster.Read(),i,false),1)).ToArray();
                var resultComposition=new NativeMatchComposition(roster,info,new[]{0});
                var config=NativeMatchConfiguration.Default(ground.MatchProfile);config.DurationMinutes=1;config.TargetEnabled=true;config.TargetPoints=1000;ground.ConfigureBotTacticsReview(config);
                ground.StartBotReview(resultComposition,17029);
                yield return null;Time.timeScale=0;
                Check(ground.Running,"results fixture: "+ground.CombatReviewDiagnostic());
                Debug.Log("RESULTS_ACTIONS_STARTED");
                ground.Session.Match.BeginTick();
                for(int i=0;i<20;i++)ground.Session.Match.RecordDamage(1,0,new DamageResult(100,true));
                ground.Session.Match.EndTick();
                Debug.Log("RESULTS_ACTIONS_FINISHED "+ground.Session.Match.Phase);
                yield return Finish("results-two-actions");
                B("Повторить матч").onClick.Invoke();yield return null;Time.timeScale=0;Check(ground.Running,"repeat");
                yield return Capture("repeat");
                ground.SendMessage("Pause","Results actions review");yield return null;
                var pauseActions=ground.transform.Find("native-ui/setup-pause/menu").GetComponentsInChildren<Button>().Select(b=>b.name).ToArray();
                Check(pauseActions.SequenceEqual(new[]{"Продолжить","fallback-settings","Повторить матч","В главное меню"}),"pause actions: "+string.Join(",",pauseActions));
                yield return Capture("pause-four-actions");
                B("fallback-settings").onClick.Invoke();yield return null;
                Check(B("settings-section-0").gameObject.activeInHierarchy && !B("fps-setting").gameObject.activeInHierarchy && !B("Продолжить").gameObject.activeInHierarchy,"unified pause settings page");
                yield return Capture("pause-settings");
                B("settings-back").onClick.Invoke();yield return null;
                Check(B("Продолжить").gameObject.activeInHierarchy && !B("fps-setting").gameObject.activeInHierarchy,"pause settings back");
                B("В главное меню").onClick.Invoke();yield return null;Check(!ground.Running,"main menu");
                yield return Capture("main-menu");
                Debug.Log("NATIVE_SCOREBOARD_REVIEW_COMPLETE "+directory);yield break;
            }
            yield return Configure(1,2,false);yield return Held(true);yield return Capture("01-ffa-two-tie");yield return Held(false);
            Hit(1,0,500);Step(1);yield return Held(true);yield return Capture("02-ffa-live-leader");Hit(0,-1,500);yield return Capture("03-ffa-dead-leader");Step(450);yield return Capture("04-ffa-respawn");yield return Held(false);yield return Finish("05-ffa-final");
            yield return Configure(2,8,false);Hit(7,0,500);Hit(0,0,500);Step(1);yield return Held(true);yield return Capture("06-ffa-eight-negative-suffix");yield return Held(false);
            yield return Configure(3,8,true);yield return Capture("07-three-views-eight-uneven-teams-tie");
            // The open west loop avoids the solid central plinth in the native Combat Bowl.
            // An equipped, positioned diagnostic fixture fires a real rocket: source + two allies.
            ground.PlaceCombatReviewSeat(0,new Vector3(-20,0,0),0,0);ground.PlaceCombatReviewSeat(1,new Vector3(-20,0,2),0,0);ground.PlaceCombatReviewSeat(2,new Vector3(-19,0,2),0,0);
            for(int i=3;i<8;i++)ground.PlaceCombatReviewSeat(i,new Vector3(20,0,i),0,0);
            Physics.SyncTransforms();
            var rocket=ground.Session.Capture();rocket.Lives[0].RocketOwned=true;rocket.Lives[0].RocketAmmo=20;
            for(int i=0;i<3;i++)rocket.Lives[i].Health=1;ground.Session.Restore(rocket);
            var select=new LocalAction[8];select[0].SelectWeapon=WeaponSelection.RocketLauncher;ground.Session.Tick(select,.02f);Step(51);
            Check(ground.Session.Life(0).SelectedWeapon==WeaponId.RocketLauncher,"rocket selected");
            int shotsBefore=ground.Session.ShotCount;
            var fire=new LocalAction[8];fire[0].Fire=true;ground.Session.Tick(fire,.02f);Check(ground.Session.ShotCount==shotsBefore+1,"rocket fired");Step(8);
            var shooter=ground.Session.Match.Read().Standings.Single(r=>r.Seat==0);
            Check(shooter.SelfKills==1&&shooter.AllyKills==2&&shooter.AccumulatedPenalty==600,
                $"multi-victim rocket penalty self={shooter.SelfKills} ally={shooter.AllyKills} penalty={shooter.AccumulatedPenalty}");
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

#endif
