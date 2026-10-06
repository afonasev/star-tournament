#if UNITY_EDITOR || DEVELOPMENT_BUILD || STAR_TOURNAMENT_DEVELOPMENT_QA
#if UNITY_EDITOR || DEVELOPMENT_BUILD || STAR_TOURNAMENT_DEVELOPMENT_QA
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    /// <summary>Real session-tick navigation, opt-in fixture goals; not a full combat planner.</summary>
    [UnityEngine.Scripting.Preserve]
    public sealed class NativeBotNavigationReview : MonoBehaviour
    {
        ProvingGround ground; Gamepad[] pads; string directory; Text banner;
        readonly List<double> frames=new List<double>();double started,lastFrameWall=-1;bool collect;
        [Serializable] sealed class Measurements
        {
            public string status="DIAGNOSTIC_NOT_TARGET_60FPS_ACCEPTANCE",unity,device,graphics,os,workload;
            public int width,height,samples,routeQueries;public bool focused,muted,gpuTimingAvailable=false,allocationCounterAvailable;
            public double durationSeconds,p50ms,p95ms,p99ms,worstms,routeP50ms,routeP95ms,routeP99ms,routeWorstMs;
            public long allocatedBytes;public string profile="unity-bot-navigation-v1@1";
        }
        void Update()
        {
            // A delta read on the first collected frame includes time before collection (e.g. startup/focus).
            // Measure only intervals bounded by two observed foreground Running updates; retain raw intervals.
            if(collect&&ground!=null&&ground.Running&&Application.isFocused)
            {
                double now=Time.realtimeSinceStartupAsDouble;
                if(lastFrameWall>=0)frames.Add((now-lastFrameWall)*1000d);
                lastFrameWall=now;
            }
            else lastFrameWall=-1;
        }
        [Serializable] sealed class Evidence
        {
            public string state, status="NAVIGATION_DIAGNOSTIC_NOT_PLAYABLE_BOTS",profile="unity-bot-navigation-v1@1", arenaFamily, arenaIdentity;
            public int width,height; public bool focused,muted,running;public double sessionTime;
            public ParticipantState pose;public NativeNavigationState navigation;
        }
        Button Button(string name)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        void Check(bool condition,string message){if(!condition)throw new InvalidOperationException("NAVIGATION_REVIEW "+message);}
        IEnumerator Start()
        {
            ground=GetComponent<ProvingGround>();var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-navigationEvidence");
            directory=index>=0&&index+1<args.Length?args[index+1]:Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_NAVIGATION_EVIDENCE")??Path.Combine(Application.persistentDataPath,"navigation-review");Directory.CreateDirectory(directory);
            // Do not request ScreenCapture while the native splash presentation is still pending.
            while(!Application.isFocused || !UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
            yield return new WaitForEndOfFrame();
            pads=Enumerable.Range(0,3).Select(_=>InputSystem.AddDevice<Gamepad>()).ToArray();
            CreateBanner();ground.StartCombatReview(pads);ground.EnableNavigationReview();
            started=Time.realtimeSinceStartupAsDouble;collect=true;
            yield return Capture("start-empty");
            var arena=ground.GetComponentInChildren<ProvingArena>();
            // Definition endpoints establish starting conditions; every route is executed by session actions.
            ground.PlaceCombatReviewSeat(1,new Vector3(0,0,-12),0);ground.PlaceCombatReviewSeat(2,new Vector3(8,0,-12),0);
            foreach(var transition in arena.ReadNavigationTransitions())
            {
                var lower=transition.OrderedFeet[0];var upper=transition.OrderedFeet[transition.OrderedFeet.Length-1];var forward=upper-lower;forward.y=0;forward.Normalize();
                var bottom=lower-forward;var top=upper+forward;
                ground.PlaceCombatReviewSeat(0,bottom,0);ground.NavigationReviewDriver.Controller.SetStaticGoal(top);
                string kind=transition.Id.Replace("transition:","");
                yield return new WaitForSeconds(.8f);yield return Capture(kind+"-ascending");
                yield return Arrive();yield return Capture(kind+"-upper");
                ground.NavigationReviewDriver.Controller.SetStaticGoal(bottom);yield return new WaitForSeconds(.8f);yield return Capture(kind+"-descending");
                yield return Arrive();yield return Capture(kind+"-lower");
            }
            ground.PlaceCombatReviewSeat(0,new Vector3(1,0,-8),0,90);ground.PlaceCombatReviewSeat(2,new Vector3(3,0,-8),0);
            ground.NavigationReviewDriver.Controller.FollowEnemy(2);yield return new WaitForSeconds(.1f);
            Check(ground.NavigationReviewDriver.Controller.Capture().HasGoal,"direct goal missing");yield return Capture("direct-observed-goal");
            // Visibility, remembered positions, and expiry are independently exercised by NativeBotPerceptionReview.
            // This diagnostic keeps its contract to generated-arena navigation routes only.
            ground.NavigationReviewDriver.Controller.SetStaticGoal(arena.UpperRoutePoint);yield return new WaitForSeconds(.4f);
            InputSystem.QueueStateEvent(pads[0],new GamepadState().WithButton(GamepadButton.Start));yield return new WaitForSecondsRealtime(.2f);
            InputSystem.QueueStateEvent(pads[0],new GamepadState());yield return new WaitForSecondsRealtime(.2f);
            var paused=JsonUtility.ToJson(ground.NavigationReviewDriver.Controller.Capture());double time=ground.Session.Time;
            yield return new WaitForSecondsRealtime(.5f);
            Check(!ground.Running&&ground.Session.Time==time&&JsonUtility.ToJson(ground.NavigationReviewDriver.Controller.Capture())==paused,"pause advanced navigation");
            yield return Capture("paused-route");Button("Продолжить").onClick.Invoke();yield return new WaitForSeconds(.2f);
            Check(ground.Session.Time>time,"resume did not advance");yield return Capture("resumed-route");
            var old=ground.NavigationReviewDriver;Button("Повторить матч").onClick.Invoke();yield return null;
            Check(ground.NavigationReviewDriver!=old&&!ground.NavigationReviewDriver.Controller.Capture().HasGoal,"Repeat retained intent");yield return Capture("repeat-empty");
            collect=false;Measure();
            Button("В главное меню").onClick.Invoke();Check(ground.NavigationReviewDriver==null,"menu retained driver");yield return Capture("return-setup");
            Debug.Log("NATIVE_BOT_NAVIGATION_REVIEW_COMPLETE "+directory);
        }
        void Measure()
        {
            var arena=ground.GetComponentInChildren<ProvingArena>();var navigation=new NativeNavigationProvider(arena,ground.Profile,ground.BotNavigationProfile);
            const int count=1000; // Fixed diagnostic sample count, not balance.
            var times=new double[count];var watch=new Stopwatch();long before=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<count;i++)
            {
                watch.Restart();Check(navigation.TryRoute(i%2==0?arena.LowerRoutePoint:arena.UpperRoutePoint,i%2==0?arena.UpperRoutePoint:arena.LowerRoutePoint,out _,out var error),error);
                watch.Stop();times[i]=watch.Elapsed.TotalMilliseconds;
            }
            var bytes=GC.GetAllocatedBytesForCurrentThread()-before;
            File.WriteAllLines(Path.Combine(directory,"foreground-frame-intervals-ms.txt"),frames.Select(value=>value.ToString("R",System.Globalization.CultureInfo.InvariantCulture)));
            Array.Sort(times);frames.Sort();
            Check(frames.Count>0,"no foreground frame samples");
            var report=new Measurements{unity=Application.unityVersion,device=SystemInfo.deviceModel,graphics=SystemInfo.graphicsDeviceName,os=SystemInfo.operatingSystem,
                workload="bounded foreground Running Update intervals, includes screenshot/UI cost; one navigator with perception/motor and three-seat trooper viewports; separate route-query microbenchmark",
                width=Screen.width,height=Screen.height,samples=frames.Count,routeQueries=count,focused=Application.isFocused,muted=AudioListener.volume==0,
                durationSeconds=Time.realtimeSinceStartupAsDouble-started,p50ms=frames[frames.Count/2],p95ms=frames[frames.Count*95/100],p99ms=frames[frames.Count*99/100],worstms=frames[frames.Count-1],
                routeP50ms=times[count/2],routeP95ms=times[count*95/100],routeP99ms=times[count*99/100],routeWorstMs=times[count-1],
                allocationCounterAvailable=bytes>0,allocatedBytes=bytes>0?bytes:-1}; // Mono may expose a nonfunctional zero allocation counter.
            File.WriteAllText(Path.Combine(directory,"navigation-performance.json"),JsonUtility.ToJson(report,true));
        }
        IEnumerator Arrive()
        {
            double deadline=ground.Session.Time+20; // Diagnostic safety budget, not game tuning.
            while(ground.NavigationReviewDriver.Controller.Status!=NativeNavigationStatus.Arrived&&ground.Session.Time<deadline)
            {
                Check(ground.NavigationReviewDriver.Controller.Status!=NativeNavigationStatus.Blocked,ground.NavigationReviewDriver.Controller.Capture().Failure);
                yield return null;
            }
            Check(ground.NavigationReviewDriver.Controller.Status==NativeNavigationStatus.Arrived,"route timeout");
            Check(ground.NavigationReviewDriver.Controller.Capture().Recoveries==0,"free route used recovery");
        }
        IEnumerator Capture(string label)
        {
            while(!Application.isFocused)yield return null;banner.text="NATIVE BOT NAVIGATION · DEVELOPMENT DIAGNOSTIC\n"+label+" · ordinary actions / CharacterMotor · no combat planner";
            yield return new WaitForEndOfFrame();
            var arena=ground.GetComponentInChildren<ProvingArena>();
            var data=new Evidence{state=label,arenaFamily=arena.Definition.MapId,arenaIdentity=arena.Definition.Identity,width=Screen.width,height=Screen.height,focused=Application.isFocused,muted=AudioListener.volume==0,
                running=ground.Running,sessionTime=ground.Session.Time,pose=ground.Session.Pose(0),navigation=ground.NavigationReviewDriver?.Controller.Capture()};
            File.WriteAllText(Path.Combine(directory,label+".json"),JsonUtility.ToJson(data,true));ScreenCapture.CaptureScreenshot(Path.Combine(directory,label+".png"));yield return null;
        }
        void CreateBanner()
        {
            // Diagnostic overlay layout is test protocol, never a shipping readability profile.
            var canvas=new GameObject("navigation-diagnostic",typeof(Canvas),typeof(CanvasScaler));canvas.transform.SetParent(transform,false);
            canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;canvas.GetComponent<Canvas>().sortingOrder=200;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);
            var panel=new GameObject("banner",typeof(RectTransform),typeof(Image));panel.transform.SetParent(canvas.transform,false);panel.GetComponent<Image>().color=new Color(.02f,.04f,.08f,.94f);
            var rect=panel.GetComponent<RectTransform>();rect.anchorMin=new Vector2(0,.88f);rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var text=new GameObject("text",typeof(RectTransform),typeof(Text));text.transform.SetParent(panel.transform,false);banner=text.GetComponent<Text>();
            banner.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");banner.fontSize=24;banner.color=Color.white;banner.alignment=TextAnchor.MiddleCenter;banner.raycastTarget=false;
            banner.rectTransform.anchorMin=Vector2.zero;banner.rectTransform.anchorMax=Vector2.one;banner.rectTransform.offsetMin=banner.rectTransform.offsetMax=Vector2.zero;
        }
        void OnDestroy(){if(pads!=null)foreach(var p in pads)if(p!=null&&p.added)InputSystem.RemoveDevice(p);}
    }
}
#endif

#endif
