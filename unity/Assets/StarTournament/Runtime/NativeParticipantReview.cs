#if UNITY_EDITOR || DEVELOPMENT_BUILD || STAR_TOURNAMENT_DEVELOPMENT_QA
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    /// <summary>Opt-in composition journey: fixtures are never advertised as playable AI.</summary>
    public sealed class NativeParticipantReview:MonoBehaviour
    {
        ProvingGround ground;string directory;Text banner;bool collect;double previous=-1;readonly List<double> intervals=new List<double>();
        [Serializable] sealed class Evidence
        {
            public string state,status="PARTICIPANT_COMPOSITION_DIAGNOSTIC_NOT_PLAYABLE_BOTS";
            public int width,height,cameras,controllers,shots;public bool focused,muted,running;public double sessionTime;
            public NativeCompositionSnapshot composition;public ParticipantState[] poses;public CombatLifeState[] lives;public NativeMatchSnapshot match;
        }
        [Serializable] sealed class Performance
        {
            public string status="DIAGNOSTIC_NOT_TARGET_60FPS_ACCEPTANCE",device,graphics,unity;
            public int width,height,samples;public double p50ms,p95ms,p99ms,worstms;
            public bool gpuTimingAvailable=false,allocationCounterAvailable=false;
            public string workload="Eight native actors, 1/2/3/4 local views, screenshot/UI/lifecycle work included; short foreground intervals only";
        }
        public static NativeMatchComposition Mixed(int humans,bool teams)
        {
            var local=Enumerable.Range(8-humans,humans).Reverse().ToArray();
            var roster=teams?new NativeMatchRoster(NativeMatchMode.Teams,Enumerable.Range(0,8).Select(p=>p%2==0?NativeTeam.TeamA:NativeTeam.TeamB).ToArray()):NativeMatchRoster.Ffa(8);
            var participants=Enumerable.Range(0,8).Select(p=>new NativeParticipantInfo(local.Contains(p)?NativeParticipantKind.LocalHuman:NativeParticipantKind.DiagnosticFixture,
                local.Contains(p)?"Игрок "+(Array.IndexOf(local,p)+1):"Fixture "+(p+1),NativeStandingsView.Identity(roster.Read(),p,false))).ToArray();
            return new NativeMatchComposition(roster,participants,local);
        }
        Button Button(string name)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        void Check(bool condition,string message){if(!condition)throw new InvalidOperationException("PARTICIPANT_REVIEW "+message);}
        void Update()
        {
            if(collect&&ground&&ground.Running&&Application.isFocused)
            {double now=Time.realtimeSinceStartupAsDouble;if(previous>=0)intervals.Add((now-previous)*1000);previous=now;}
            else previous=-1;
        }
        IEnumerator Start()
        {
            ground=GetComponent<ProvingGround>();var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"-participantEvidence");
            directory=flag>=0&&flag+1<args.Length?args[flag+1]:Path.Combine(Application.persistentDataPath,"participant-review");Directory.CreateDirectory(directory);
            while(!Application.isFocused||!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
            CreateBanner();
            if(args.Contains("-participantControls"))
            {
                ground.StartParticipantReview(Mixed(1,false),new InputDevice[]{Keyboard.current});
                banner.text="DEVELOPMENT · one human / seven stationary fixtures · keyboard controls";
                int shot=0;yield return Capture("controls-start");
                while(true)
                {
                    if(Keyboard.current!=null&&Keyboard.current.f9Key.wasPressedThisFrame)yield return Capture("controls-"+(++shot));
                    yield return null;
                }
            }
            int devices=InputSystem.devices.Count;collect=true;
            foreach(int humans in new[]{1,2,3,4})
            {
                ground.StartParticipantReview(Mixed(humans,false));yield return null;yield return new WaitForSeconds(.5f);
                Check(ground.Running&&ground.Session.ParticipantCount==8,"eight participant start failed");
                Check(ground.GetComponentsInChildren<Camera>(true).Length==humans,"extra cameras");
                Check(InputSystem.devices.Count==devices,"synthetic devices created");
                yield return Capture("ffa-"+humans+"-views");ground.SetParticipantReviewStandings(true);yield return Capture("ffa-"+humans+"-standings");ground.SetParticipantReviewStandings(false);
            }
            ground.StartParticipantReview(Mixed(3,true));yield return new WaitForSeconds(.5f);yield return Capture("teams-three-persistent");
            ground.SetParticipantReviewStandings(true);yield return Capture("teams-eight-rows");ground.SetParticipantReviewStandings(false);
            int local=ground.Composition.ParticipantAt(0),killer=0;
            ground.Session.ApplyDamage(local,ground.Session.Life(local).Life,500,killer,ground.Session.Life(killer).Life);
            yield return new WaitForSeconds(.2f);Check(ground.Session.Life(local).Dead,"local death absent");yield return Capture("nonlocal-killer");
            ground.Session.ApplyDamage(killer,ground.Session.Life(killer).Life,500);yield return new WaitForSeconds(.2f);yield return Capture("nonlocal-corpse");
            double deadline=ground.Session.Time+ground.CombatProfile.Get("death.corpseSeconds")+10;
            while(ground.Session.Life(local).Dead&&ground.Session.Time<deadline)yield return null;
            Check(!ground.Session.Life(local).Dead,"respawn missing");yield return Capture("respawn-eight");
            // Use the real setup pause action through the shared owner, then invoke native menu buttons.
            ground.SendMessage("Pause","Development review pause");double time=ground.Session.Time;yield return new WaitForSecondsRealtime(.3f);
            Check(ground.Session.Time==time&&!ground.Running,"pause advanced");yield return Capture("paused-eight");Button("Продолжить").onClick.Invoke();yield return new WaitForSeconds(.2f);yield return Capture("resumed-eight");
            string frozen=JsonUtility.ToJson(ground.Composition.Read());var old=ground.Session;
            Button("Повторить матч").onClick.Invoke();yield return null;Check(ground.Session!=old&&JsonUtility.ToJson(ground.Composition.Read())==frozen,"Repeat composition changed");yield return Capture("repeat-eight");
            // Accelerate the same session with idle fixed actions, outside the frame measurement window.
            collect=false;WritePerformance();
            ground.Session.ApplyDamage(0,ground.Session.Life(0).Life,500,7,ground.Session.Life(7).Life);
            var idle=new LocalAction[8];int ticks=(int)Math.Ceiling(ground.Session.Match.RemainingSeconds/Time.fixedDeltaTime)+3;
            for(int tick=0;tick<ticks&&ground.Session.Match.Phase!=NativeMatchPhase.Finished;tick++)ground.Session.Tick(idle,Time.fixedDeltaTime);
            Check(ground.Session.Match.Phase==NativeMatchPhase.Finished,"bounded results fast-forward failed");
            yield return new WaitForFixedUpdate();yield return null;yield return Capture("results-eight");
            Button("В главное меню").onClick.Invoke();yield return null;yield return Capture("return-human-setup");
            Debug.Log("NATIVE_PARTICIPANT_REVIEW_COMPLETE "+directory);
        }
        IEnumerator Capture(string label)
        {
            while(!Application.isFocused)yield return null;
            banner.text="NATIVE PARTICIPANT COMPOSITION · DEVELOPMENT FIXTURES\n"+label+" · eight actors / local views only · no combat AI";
            yield return new WaitForEndOfFrame();
            var data=new Evidence{state=label,width=Screen.width,height=Screen.height,cameras=ground.GetComponentsInChildren<Camera>(true).Length,
                controllers=ground.GetComponentsInChildren<CharacterController>().Length,shots=ground.Session.ShotCount,focused=Application.isFocused,muted=AudioListener.volume==0,running=ground.Running,sessionTime=ground.Session.Time,
                composition=ground.Composition.Read(),poses=Enumerable.Range(0,ground.Session.ParticipantCount).Select(ground.Session.Pose).ToArray(),
                lives=Enumerable.Range(0,ground.Session.ParticipantCount).Select(ground.Session.Life).ToArray(),match=ground.Session.Match?.Read()};
            File.WriteAllText(Path.Combine(directory,label+".json"),JsonUtility.ToJson(data,true));ScreenCapture.CaptureScreenshot(Path.Combine(directory,label+".png"));yield return null;
        }
        void WritePerformance()
        {
            Check(intervals.Count>0,"no frame samples");File.WriteAllLines(Path.Combine(directory,"foreground-frame-intervals-ms.txt"),intervals.Select(v=>v.ToString("R",System.Globalization.CultureInfo.InvariantCulture)));
            intervals.Sort();var p=new Performance{device=SystemInfo.deviceModel,graphics=SystemInfo.graphicsDeviceName,unity=Application.unityVersion,width=Screen.width,height=Screen.height,samples=intervals.Count,
                p50ms=intervals[intervals.Count/2],p95ms=intervals[intervals.Count*95/100],p99ms=intervals[intervals.Count*99/100],worstms=intervals.Last()};
            File.WriteAllText(Path.Combine(directory,"participant-performance.json"),JsonUtility.ToJson(p,true));
        }
        void CreateBanner()
        {
            // Diagnostic overlay coordinates are test protocol, not shipping UI tuning.
            var root=new GameObject("participant-review-banner",typeof(Canvas),typeof(CanvasScaler));root.transform.SetParent(transform,false);
            root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;root.GetComponent<Canvas>().sortingOrder=200;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);
            var panel=new GameObject("panel",typeof(RectTransform),typeof(Image));panel.transform.SetParent(root.transform,false);panel.GetComponent<Image>().color=new Color(.02f,.04f,.08f,.94f);
            var rect=panel.GetComponent<RectTransform>();rect.anchorMin=new Vector2(0,.9f);rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var text=new GameObject("text",typeof(RectTransform),typeof(Text));text.transform.SetParent(panel.transform,false);banner=text.GetComponent<Text>();banner.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");banner.fontSize=24;banner.color=Color.white;banner.alignment=TextAnchor.MiddleCenter;banner.raycastTarget=false;
            banner.rectTransform.anchorMin=Vector2.zero;banner.rectTransform.anchorMax=Vector2.one;banner.rectTransform.offsetMin=banner.rectTransform.offsetMax=Vector2.zero;
        }
    }
}
#endif

#endif
