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
    /// <summary>Ordinary-menu color/lifecycle QA plus labelled placement/boost fixtures; never human or physical acceptance.</summary>
    public sealed class NativeColorIdentityReview:MonoBehaviour
    {
        ProvingGround ground;string directory;Gamepad pad;
        [Serializable] sealed class Evidence
        {
            public string state,status="NATIVE_PLAYER_COLOR_QA_SYNTHETIC_DEVICE_PLACEMENT_BOOST_AND_ROSTER_ACTION_FIXTURES_NOT_HUMAN_ACCEPTANCE";
            public bool focused,muted,running;public int width,height;public uint botSeed;
            public NativeCompositionSnapshot composition;public NativeCombatSessionSnapshot session;public NativeMatchSnapshot match;
        }
        Button B(string name)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        void Check(bool value,string message){if(!value)throw new InvalidOperationException("COLOR_IDENTITY_REVIEW "+message);}
        IEnumerator Configure(int views,int total,bool allAi,NativeMatchMode mode=NativeMatchMode.Ffa,bool swap=false)
        {
            Time.timeScale=1;
            if(ground.Session.Match!=null){ground.SendMessage("Pause","QA transition");B("В главное меню").onClick.Invoke();}
            B("main-action-0").onClick.Invoke();B("setup-next").onClick.Invoke();B("setup-next").onClick.Invoke();
            while(ground.SetupBotCount>0)ground.RemoveBot(0);
            while(ground.LocalSeatCount>views)B("seats-minus").onClick.Invoke();
            while(ground.LocalSeatCount<views)B("seats-plus").onClick.Invoke();
            for(int s=0;s<views;s++)ground.SetSeatAi(s,allAi||s>0);
            for(int p=views;p<total;p++)ground.AddBot();
            ground.SetMatchMode(mode);
            if(swap)B("team-colors").onClick.Invoke();
            if(!allAi)
            {
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.North));yield return null;yield return null;
                InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;yield return null;
                // Joining a human requires an explicit guest identity in the ordinary roster editor.
                var guest=ground.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.name=="roster-choice-guest");
                if(guest!=null&&guest.gameObject.activeInHierarchy){guest.onClick.Invoke();B("roster-done").onClick.Invoke();}
                else
                {
                    B("roster-card-0").onClick.Invoke();B("roster-identity").onClick.Invoke();B("roster-choice-guest").onClick.Invoke();B("roster-done").onClick.Invoke();
                }
            }
            Check(B("Начать — четыре игрока").interactable,"ordinary start unavailable");
            B("Начать — четыре игрока").onClick.Invoke();yield return null;
            Check(ground.Running,"ordinary start failed");Time.timeScale=0;
            Check(ground.Composition.ParticipantCount==total,"roster size");
            if(mode==NativeMatchMode.Ffa)Check(ground.Composition.Read().Participants.Select(p=>p.Color).Distinct().Count()==total,"duplicate colors");
        }
        void Lineup(float yaw)
        {
            ground.PlaceCombatReviewSeat(0,new Vector3(0,4,-3),0,0);
            for(int p=1;p<ground.Composition.ParticipantCount;p++)
                ground.PlaceCombatReviewSeat(p,new Vector3((p-(ground.Composition.ParticipantCount/2f))*1.3f,4,0),0,yaw);
        }
        IEnumerator Capture(string state)
        {
            while(!Application.isFocused)yield return null;
            yield return null;yield return new WaitForEndOfFrame();
            File.WriteAllText(Path.Combine(directory,state+".json"),JsonUtility.ToJson(new Evidence{state=state,focused=Application.isFocused,muted=AudioListener.volume==0,running=ground.Running,width=Screen.width,height=Screen.height,botSeed=ground.BotDriver?.Seed??0,composition=ground.Composition.Read(),session=ground.Session.Capture(),match=ground.Session.Match?.Read()},true));
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,state+".png"));yield return new WaitForSecondsRealtime(.25f);
        }
        IEnumerator Results(string state)
        {
            // A labelled direct-kill fixture breaks a tied clock; the session still owns score and results.
            ground.Session.ApplyDamage(1,ground.Session.Life(1).Life,500,0,ground.Session.Life(0).Life);
            var actions=new LocalAction[ground.Composition.ParticipantCount];
            int budget=(int)Math.Ceiling(ground.Session.Match.Configuration.DurationMinutes*60/Time.fixedDeltaTime)+2;
            for(int tick=0;tick<budget&&ground.Session.Match.Phase!=NativeMatchPhase.Finished;tick++)
            {ground.Session.Tick(actions,Time.fixedDeltaTime);if(tick%500==0)yield return null;}
            Check(ground.Session.Match.Phase==NativeMatchPhase.Finished,"results timeout");
            Time.timeScale=1;yield return new WaitForFixedUpdate();Time.timeScale=0;yield return null;yield return Capture(state);
        }
        IEnumerator Start()
        {
            ground=GetComponent<ProvingGround>();var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-colorIdentityEvidence");
            directory=at>=0&&at+1<args.Length?args[at+1]:Path.Combine(Application.persistentDataPath,"color-identity-review");Directory.CreateDirectory(directory);AudioListener.volume=0;
            while(!Application.isFocused||!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
            B("main-action-2").onClick.Invoke();yield return null;
            B("group-participant-colors").onClick.Invoke();yield return null;yield return new WaitForEndOfFrame();
            Check(ground.GetComponentsInChildren<InputField>().Count(f=>f.name.StartsWith("input-participant.color."))==24,"palette Lab fields missing");
            File.WriteAllText(Path.Combine(directory,"00-palette-lab.json"),JsonUtility.ToJson(new Evidence{state="00-palette-lab",focused=Application.isFocused,muted=AudioListener.volume==0,width=Screen.width,height=Screen.height},true));
            File.WriteAllText(Path.Combine(directory,"palette-profile.json"),JsonUtility.ToJson(ground.ParticipantPaletteProfile,true));
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"00-palette-lab.png"));yield return new WaitForSecondsRealtime(.25f);
            B("lab-back").onClick.Invoke();yield return null;
            pad=InputSystem.AddDevice<Gamepad>();
            // AI-only composition uses the ordinary menu path and needs no artificial human identity.
            yield return Configure(1,2,true);Lineup(180);yield return Capture("01-ffa-two-front");
            Lineup(90);yield return Capture("02-ffa-two-side");Lineup(0);yield return Capture("03-ffa-two-rear");
            string frozen=JsonUtility.ToJson(ground.Composition.Read());
            var boost=ground.Session.Capture();boost.DamageRemaining[0]=5;boost.DamageRemaining[1]=5;ground.Session.Restore(boost);
            Lineup(180);yield return Capture("04-ffa-two-boost-fixture");
            for(int tick=0;tick<300;tick++)ground.Session.Tick(new LocalAction[2],.02f);
            Check(ground.Session.DamageBoostRemaining(0)==0&&ground.Session.DamageBoostRemaining(1)==0,"boost expiry");Lineup(180);yield return Capture("05-boost-expired");
            ground.SendMessage("Pause","Color identity QA");yield return Capture("06-pause");B("Продолжить").onClick.Invoke();
            Check(JsonUtility.ToJson(ground.Composition.Read())==frozen,"pause changed colors");
            ground.Session.ApplyDamage(1,ground.Session.Life(1).Life,500,0,ground.Session.Life(0).Life);yield return Capture("07-combat-feedback-death-fixture");
            for(int tick=0;tick<500&&ground.Session.Life(1).Dead;tick++)ground.Session.Tick(new LocalAction[2],.02f);
            Check(!ground.Session.Life(1).Dead,"respawn timeout");Lineup(180);yield return Capture("08-respawn");
            ground.SendMessage("Pause","Repeat QA");B("Повторить матч").onClick.Invoke();yield return null;
            Check(JsonUtility.ToJson(ground.Composition.Read())==frozen,"Repeat changed colors");Lineup(180);yield return Capture("09-repeat");
            yield return Configure(1,8,true);Lineup(180);yield return Capture("10-ffa-eight-one-view-front");Lineup(90);yield return Capture("11-ffa-eight-side");Lineup(0);yield return Capture("12-ffa-eight-rear");
            yield return Configure(4,8,true);Lineup(180);yield return Capture("13-ffa-eight-four-views-ai-only");
            yield return Configure(4,8,false);Lineup(180);yield return Capture("14-ffa-eight-four-views-mixed");
            // Labelled action fixture: exercise the actual live table while placement remains frozen.
            // Device/control acceptance is a separate gate from this color presentation review.
            var frameActions=(LocalAction[])typeof(ProvingGround).GetField("actions",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(ground);
            frameActions[0].ShowRoster=true;yield return null;yield return null;
            Check(ground.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="standings-0").gameObject.activeInHierarchy,"live standings unavailable");
            yield return Capture("15-ffa-live-standings-action-fixture");frameActions[0].ShowRoster=false;yield return null;
            yield return Results("15b-ffa-results");B("Повторить матч").onClick.Invoke();yield return null;
            yield return Configure(4,8,true,NativeMatchMode.Teams);Lineup(180);yield return Capture("16-teams-four-views");
            yield return Configure(4,8,true,NativeMatchMode.Teams,true);Lineup(180);yield return Capture("17-teams-swapped-four-views");
            yield return Results("18-teams-results");
            B("Повторить матч").onClick.Invoke();yield return null;Check(ground.Running,"results Repeat failed");
            yield return Configure(1,2,true);Lineup(180);yield return Capture("19-new-ffa-from-menu");
            File.WriteAllText(Path.Combine(directory,"complete.json"),"{\"complete\":true,\"muted\":true,\"humanAcceptance\":false,\"physicalAcceptance\":false}");Debug.Log("COLOR_IDENTITY_REVIEW_COMPLETE "+directory);
            Time.timeScale=1;Application.Quit();
        }
        void OnDestroy(){Time.timeScale=1;if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);}
    }
}
#endif

#endif
