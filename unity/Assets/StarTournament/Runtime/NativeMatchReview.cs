#if UNITY_EDITOR || DEVELOPMENT_BUILD || STAR_TOURNAMENT_DEVELOPMENT_QA
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    /// <summary>CLI-only synthetic Player journey. Accelerated idle ticks are explicitly diagnostic.</summary>
    public sealed class NativeMatchReview : MonoBehaviour
    {
        readonly Gamepad[] pads=new Gamepad[4];
        ProvingGround ground;
        string directory;
        [Serializable] sealed class State
        {
            public string scenario="native-match-review-v1", acceptance="DIAGNOSTIC_NOT_PHYSICAL_OR_PERFORMANCE_ACCEPTANCE";
            public string state, profile="unity-native-match-v1@1";
            public int width,height,shots,eventSystems,listeners,arenas,corpses;
            public bool running,focused,muted;
            public double combatClock;
            public NativeMatchConfiguration configuration;
            public NativeMatchSnapshot match;
            public CombatLifeState[] lives;
            public string[] killNotices;
        }
        Button Button(string name) => ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        IEnumerator Start()
        {
            ground=GetComponent<ProvingGround>(); var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-matchEvidence");
            directory=index>=0 && index+1<args.Length?args[index+1]:Path.Combine(Application.persistentDataPath,"match-review");Directory.CreateDirectory(directory);
            while(!Application.isFocused)yield return null;
            yield return new WaitForSecondsRealtime(1);
            if(args.Contains("-killNoticeReview"))
            {
                Button("main-action-0").onClick.Invoke();
                while(ground.SetupBotCount>0)ground.RemoveBot(0);
                while(ground.LocalSeatCount<4)Button("seats-plus").onClick.Invoke();
                ground.SetMatchMode(NativeMatchMode.Teams);
                ground.SetTeam(0,NativeTeam.TeamA);ground.SetTeam(1,NativeTeam.TeamA);
                ground.SetTeam(2,NativeTeam.TeamB);ground.SetTeam(3,NativeTeam.TeamB);
                for(int i=0;i<4;i++)pads[i]=InputSystem.AddDevice<Gamepad>();
                ground.StartCombatReview(pads);yield return new WaitForSeconds(.3f);
                ground.Session.ApplyDamage(2,ground.Session.Life(2).Life,10000,0,ground.Session.Life(0).Life);
                yield return null;yield return Capture("enemy-kill-red");
                ground.Session.ApplyDamage(1,ground.Session.Life(1).Life,10000,0,ground.Session.Life(0).Life);
                yield return null;yield return Capture("ally-kill-red");
                ground.Session.ApplyDamage(0,ground.Session.Life(0).Life,10000,0,ground.Session.Life(0).Life);
                yield return null;yield return Capture("self-kill-red");
                Debug.Log("KILL_NOTICE_REVIEW_COMPLETE "+directory);
                Application.Quit();yield break;
            }
            // Setup buttons use actual descriptor-backed constraints. No profile override.
            while(ground.Configuration.DurationMinutes>1)Button("duration-minus").onClick.Invoke();
            Button("target-toggle").onClick.Invoke();
            for(int i=0;i<20;i++)Button("Цель −").onClick.Invoke();
            yield return Capture("setup");
            for(int i=0;i<4;i++)pads[i]=InputSystem.AddDevice<Gamepad>();
            ground.StartCombatReview(pads);yield return new WaitForSeconds(.3f);
            yield return Capture("live");
            Place();yield return new WaitForSeconds(.1f);
            yield return KillTarget();
            yield return Capture("kill-notice-and-pair-score");
            yield return new WaitForSeconds(.8f);yield return Capture("killer-orbit");
            InputSystem.QueueStateEvent(pads[1],new GamepadState().WithButton(GamepadButton.Select));
            yield return new WaitForSeconds(.1f);yield return Capture("killcam-standings");
            InputSystem.QueueStateEvent(pads[1],new GamepadState());
            InputSystem.QueueStateEvent(pads[0],new GamepadState().WithButton(GamepadButton.Start));yield return new WaitForSecondsRealtime(.2f);
            InputSystem.QueueStateEvent(pads[0],new GamepadState());yield return Capture("pause");
            yield return new WaitForSecondsRealtime(.3f);yield return Capture("paused-clock");
            // Submit activates the selected Resume via InputSystemUIInputModule.
            InputSystem.QueueStateEvent(pads[0],new GamepadState().WithButton(GamepadButton.South));yield return new WaitForSecondsRealtime(.2f);
            InputSystem.QueueStateEvent(pads[0],new GamepadState());
            for(int kill=1;kill<5;kill++)
            {
                while(ground.Session.Life(1).Dead)yield return null;
                Place();yield return new WaitForSeconds(.1f);yield return KillTarget();
                if(kill==1)yield return Capture("double-kill");
                if(kill==2)yield return Capture("three-kills");
            }
            yield return new WaitForSeconds(.2f);yield return Capture("target-results");
            if(ground.Session.Match.Phase!=NativeMatchPhase.Finished)throw new InvalidOperationException("Review target did not finish");
            InputSystem.QueueStateEvent(pads[0],new GamepadState().WithButton(GamepadButton.South));yield return new WaitForSecondsRealtime(.2f);
            InputSystem.QueueStateEvent(pads[0],new GamepadState());yield return Capture("repeat");
            // Accelerate empty native ticks (all four motors/core); not a timing or physical measurement.
            var session=ground.Session;var empty=new LocalAction[4];
            while(session.Match.Phase==NativeMatchPhase.Running) session.Tick(empty,1f/ground.Profile.Get("simulation.fixedTickHz"));
            yield return Capture("overtime");
            Place();yield return new WaitForSeconds(.1f);
            yield return KillTarget();yield return Capture("time-results");
            if(session.Match.Read().Trigger!="time-limit" || session.Match.Phase!=NativeMatchPhase.Finished)throw new InvalidOperationException("Review overtime did not finish");
            Button("В главное меню").onClick.Invoke();yield return Capture("menu");
            Button("Начать — четыре игрока").onClick.Invoke();yield return new WaitForSeconds(.2f);yield return Capture("new-match");
            Debug.Log("NATIVE_MATCH_REVIEW_COMPLETE "+directory);
        }
        IEnumerator KillTarget()
        {
            // Diagnostic lethal event isolates killcam/HUD review from weapon spread and arena cover.
            var victim=ground.Session.Life(1);var killer=ground.Session.Life(0);
            var result=ground.Session.ApplyDamage(1,victim.Life,victim.Health,0,killer.Life);
            if(!result.Killed)throw new InvalidOperationException("Review lethal event failed");
            yield return null;
        }
        void Place()
        {
            ground.PlaceCombatReviewSeat(0,new Vector3(-8,0,2),0);
            ground.PlaceCombatReviewSeat(1,new Vector3(-8,0,3.5f),0);
            ground.PlaceCombatReviewSeat(2,new Vector3(0,0,3),0);
            ground.PlaceCombatReviewSeat(3,new Vector3(8,0,3),0);
        }
        IEnumerator Capture(string label)
        {
            yield return new WaitForEndOfFrame();
            var state=new State { state=label,width=Screen.width,height=Screen.height,running=ground.Running,focused=Application.isFocused,
                muted=AudioListener.volume==0,combatClock=ground.Session.Time,shots=ground.Session.ShotCount,
                configuration=ground.Session.Match?.Configuration??ground.Configuration,match=ground.Session.Match?.Read(),lives=new CombatLifeState[4],
                killNotices=Enumerable.Range(0,4).Select(i=>ground.GetComponentsInChildren<Text>(true).Single(t=>t.name=="kill-notice-"+i).text).ToArray(),
                eventSystems=ground.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true).Length,
                listeners=ground.GetComponentsInChildren<AudioListener>(true).Length,arenas=ground.GetComponentsInChildren<ProvingArena>(true).Length,
                corpses=ground.GetComponentsInChildren<Transform>(true).Count(t=>t.name.StartsWith("corpse-")&&t.gameObject.activeSelf) };
            for(int i=0;i<4;i++)state.lives[i]=ground.Session.Life(i);
            File.WriteAllText(Path.Combine(directory,label+".json"),JsonUtility.ToJson(state,true));
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,label+".png"));
            yield return null;
        }
        void OnDestroy() { foreach(var p in pads)if(p!=null&&p.added)InputSystem.RemoveDevice(p); }
    }
}
#endif

#endif
