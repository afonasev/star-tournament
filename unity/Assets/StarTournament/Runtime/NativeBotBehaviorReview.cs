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
    public sealed class NativeBotBehaviorReview:MonoBehaviour
    {
        ProvingGround ground;string directory;Text banner;bool collect;double previous=-1;int lastTick=-1;
        readonly HashSet<string> lifecycleCaptures=new HashSet<string>();
        readonly List<double> frames=new List<double>(),cpu=new List<double>();readonly List<Run> runs=new List<Run>();Run run;
        // Evidence fixtures/seed/durations are test protocol, not shipping balance.
        const uint Seed=20260920;
        [Serializable] sealed class Run {public string name;public uint seed;public double firstDamage=-1,damage,seconds;public int shots,deaths,respawns,jumps,fireAttempts,maxTacticalQueries;}
        [Serializable] sealed class Evidence {public string state,status="ACTUAL_BOT_BEHAVIOR_DEVELOPMENT_DIAGNOSTIC";public int width,height,shots;public bool focused,muted,running;public double time;public uint seed;public NativeCompositionSnapshot composition;public ParticipantState[] poses;public CombatLifeState[] lives;public NativeMatchSnapshot match;public NativeBotPlannerState[] bots;}
        [Serializable] sealed class Summary {public string status="PILOT_NOT_DIFFICULTY_OR_TARGET_PERFORMANCE_ACCEPTANCE",device,graphics;public int width,height,frameSamples,cpuSamples;public double frameP95,frameP99,frameWorst,cpuP95,cpuP99,cpuWorst;public bool gpuAvailable=false,allocationsAvailable=false;public Run[] runs;}
        public static NativeMatchComposition Mixed(int humans,bool teams,int count=8)
        {
            var local=Enumerable.Range(count-humans,humans).Reverse().ToArray();
            var roster=teams?new NativeMatchRoster(NativeMatchMode.Teams,Enumerable.Range(0,count).Select(p=>p%2==0?NativeTeam.TeamA:NativeTeam.TeamB).ToArray()):NativeMatchRoster.Ffa(count);
            var people=Enumerable.Range(0,count).Select(p=>new NativeParticipantInfo(local.Contains(p)?NativeParticipantKind.LocalHuman:NativeParticipantKind.Bot,
                local.Contains(p)?"Игрок "+(Array.IndexOf(local,p)+1):"Bot "+(p+1),NativeStandingsView.Identity(roster.Read(),p,false),local.Contains(p)?-1:p%3)).ToArray();
            return new NativeMatchComposition(roster,people,local);
        }
        Button Button(string name)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        void Check(bool okay,string reason){if(!okay)throw new InvalidOperationException("BOT_BEHAVIOR_REVIEW "+reason);}
        void Update()
        {
            if(collect&&ground&&ground.Running&&Application.isFocused)
            {
                double now=Time.realtimeSinceStartupAsDouble;if(previous>=0)frames.Add((now-previous)*1000);previous=now;
                var driver=ground.BotDriver;if(driver!=null&&driver.Ticks!=lastTick){lastTick=driver.Ticks;cpu.Add(driver.LastMilliseconds);if(run!=null)run.maxTacticalQueries=Math.Max(run.maxTacticalQueries,driver.LastQueries);}
            }else previous=-1;
        }
        void BeginRun(string name,int humans,bool teams,int count=8,InputDevice[] devices=null)
        {
            ground.StartBotReview(Mixed(humans,teams,count),Seed,devices);Check(ground.Running&&ground.BotDriver!=null,"start failed");
            run=new Run{name=name,seed=Seed};runs.Add(run);lastTick=-1;
            ground.Session.Fired+=(p,damage)=>{run.damage+=damage;if(damage>0&&run.firstDamage<0)run.firstDamage=ground.Session.Time;};
            ground.Session.RifleBulletHit+=e=>{run.damage+=e.AppliedDamage;if(e.AppliedDamage>0&&run.firstDamage<0)run.firstDamage=e.Time;};
            var activeRun=run;var local=Enumerable.Range(0,ground.Composition.LocalCount).Select(ground.Composition.ParticipantAt).ToArray();
            ground.Session.Died+=notice=>{activeRun.deaths++;if(collect&&local.Contains(notice.Seat)&&lifecycleCaptures.Add(name+"-local-death"))StartCoroutine(Capture(name+"-local-death"));};
            ground.Session.Respawned+=p=>{activeRun.respawns++;if(collect&&local.Contains(p)&&lifecycleCaptures.Add(name+"-local-respawn"))StartCoroutine(Capture(name+"-local-respawn"));};
        }
        void FinishRun()
        {
            run.seconds=ground.Session.Time;run.shots=ground.Session.ShotCount;
            for(int p=0;p<ground.Session.ParticipantCount;p++){var bot=ground.BotDriver.Planner(p);if(bot!=null){run.jumps+=bot.Jumps;run.fireAttempts+=bot.FireAttempts;}}
        }
        IEnumerator Start()
        {
            ground=GetComponent<ProvingGround>();var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-botBehaviorEvidence");directory=at>=0&&at+1<args.Length?args[at+1]:Path.Combine(Application.persistentDataPath,"bot-behavior-review");Directory.CreateDirectory(directory);
            while(!Application.isFocused||!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;Banner();collect=true;
            if(args.Contains("-botControls"))
            {
                BeginRun("keyboard-vs-seven",1,false,8,new InputDevice[]{Keyboard.current});yield return Capture("controls-start");int captures=0;double nextCapture=5;
                while(true){if(Keyboard.current.f9Key.wasPressedThisFrame||ground.Session.Time>=nextCapture){nextCapture=ground.Session.Time+5;FinishRun();yield return Capture("controls-"+(++captures));WriteSummary();}yield return null;}
            }
            BeginRun("one-versus-one",1,false,2);yield return new WaitForSeconds(20);FinishRun();yield return Capture("one-versus-one");
            foreach(int humans in new[]{1,2,3,4})
            {
                BeginRun("ffa-"+humans+"-views",humans,false);yield return new WaitForSeconds(12);FinishRun();yield return Capture(run.name);
                ground.SetParticipantReviewStandings(true);yield return Capture(run.name+"-standings");ground.SetParticipantReviewStandings(false);
            }
            BeginRun("teams-three",3,true);yield return new WaitForSeconds(30);FinishRun();yield return Capture("teams-active");
            ground.SetParticipantReviewStandings(true);yield return Capture("teams-standings");ground.SetParticipantReviewStandings(false);
            ground.SendMessage("Pause","AI review pause");double time=ground.Session.Time;int ticks=ground.BotDriver.Ticks;yield return new WaitForSecondsRealtime(.3f);
            Check(time==ground.Session.Time&&ticks==ground.BotDriver.Ticks,"AI advanced in pause");yield return Capture("paused");Button("Продолжить").onClick.Invoke();yield return new WaitForSeconds(1);yield return Capture("resumed");
            var old=ground.BotDriver;Button("Повторить матч").onClick.Invoke();yield return new WaitForSeconds(1);Check(ground.BotDriver!=old,"Repeat reused driver");yield return Capture("repeat");
            collect=false;ground.enabled=false;
            // Same real bot/session runtime accelerated; no scripted damage or forced results.
            var actions=new LocalAction[ground.Session.ParticipantCount];int budget=(int)(600/Time.fixedDeltaTime);
            for(int t=0;t<budget&&ground.Session.Match.Phase!=NativeMatchPhase.Finished;t++)
            {Array.Clear(actions,0,actions.Length);ground.BotDriver.ProduceActions(actions,Time.fixedDeltaTime);ground.Session.Tick(actions,Time.fixedDeltaTime);if(t%500==0)yield return null;}
            ground.enabled=true;Check(ground.Session.Match.Phase==NativeMatchPhase.Finished,"No natural terminal result within budget");yield return new WaitForFixedUpdate();yield return null;yield return Capture("natural-results");
            Check(runs.Sum(r=>r.damage)>0&&runs.Sum(r=>r.deaths)>0,"No actual combat");WriteSummary();Button("В главное меню").onClick.Invoke();yield return null;yield return Capture("human-setup");Debug.Log("NATIVE_BOT_BEHAVIOR_REVIEW_COMPLETE "+directory);
        }
        IEnumerator Capture(string label)
        {
            while(!Application.isFocused)yield return null;banner.text="NATIVE AI · ACTUAL BOT ACTIONS · SEED "+Seed+"\n"+label+" · development scenario / pilot evidence";
            yield return new WaitForEndOfFrame();var data=new Evidence{state=label,width=Screen.width,height=Screen.height,shots=ground.Session.ShotCount,focused=Application.isFocused,muted=AudioListener.volume==0,running=ground.Running,time=ground.Session.Time,seed=Seed,composition=ground.Composition.Read(),
                poses=Enumerable.Range(0,ground.Session.ParticipantCount).Select(ground.Session.Pose).ToArray(),lives=Enumerable.Range(0,ground.Session.ParticipantCount).Select(ground.Session.Life).ToArray(),match=ground.Session.Match?.Read(),bots=Enumerable.Range(0,ground.Session.ParticipantCount).Select(p=>ground.BotDriver?.Planner(p)?.Capture()).ToArray()};
            File.WriteAllText(Path.Combine(directory,label+".json"),JsonUtility.ToJson(data,true));ScreenCapture.CaptureScreenshot(Path.Combine(directory,label+".png"));yield return null;
        }
        void WriteSummary()
        {
            double Percent(List<double> values,int p){var sorted=values.OrderBy(x=>x).ToArray();return sorted.Length==0?0:sorted[Math.Min(sorted.Length-1,sorted.Length*p/100)];}
            var summary=new Summary{device=SystemInfo.deviceModel,graphics=SystemInfo.graphicsDeviceName,width=Screen.width,height=Screen.height,frameSamples=frames.Count,cpuSamples=cpu.Count,
                frameP95=Percent(frames,95),frameP99=Percent(frames,99),frameWorst=Percent(frames,100),cpuP95=Percent(cpu,95),cpuP99=Percent(cpu,99),cpuWorst=Percent(cpu,100),runs=runs.ToArray()};
            File.WriteAllText(Path.Combine(directory,"summary.json"),JsonUtility.ToJson(summary,true));
            File.WriteAllLines(Path.Combine(directory,"frames-ms.txt"),frames.Select(v=>v.ToString("R",System.Globalization.CultureInfo.InvariantCulture)));
        }
        void Banner()
        {
            var root=new GameObject("bot-review-banner",typeof(Canvas),typeof(CanvasScaler));root.transform.SetParent(transform,false);root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;root.GetComponent<Canvas>().sortingOrder=200;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);
            var go=new GameObject("text",typeof(RectTransform),typeof(Text));go.transform.SetParent(root.transform,false);banner=go.GetComponent<Text>();banner.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");banner.fontSize=24;banner.color=Color.white;banner.alignment=TextAnchor.UpperCenter;banner.raycastTarget=false;
            banner.rectTransform.anchorMin=new Vector2(0,.9f);banner.rectTransform.anchorMax=Vector2.one;banner.rectTransform.offsetMin=banner.rectTransform.offsetMax=Vector2.zero;
        }
    }
}
#endif

#endif
