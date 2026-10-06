#if UNITY_EDITOR || DEVELOPMENT_BUILD || STAR_TOURNAMENT_DEVELOPMENT_QA
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace StarTournament.ProvingGround
{
    /// <summary>Opt-in sensor diagnostic. Existing human bodies, no bot actions or gameplay AI.</summary>
    public sealed class NativeBotPerceptionReview : MonoBehaviour
    {
        ProvingGround ground; NativeBotPerception knowledge; NativeBotObservationProvider provider;
        Gamepad[] pads; string directory; Text banner; GameObject canvasRoot;
        [Serializable] sealed class Evidence
        {
            public string state, acceptance="PERCEPTION_DIAGNOSTIC_NOT_PLAYABLE_BOTS", profile="unity-bot-perception-v1@1";
            public string clock="explicit diagnostic observation stream; not match/replay time";
            public int width,height;public bool focused,muted;
            public ParticipantState[] actualPoses; public NativeBotPerceptionSnapshot knowledge;
        }
        [Serializable] sealed class Timing
        {
            public string status="DIAGNOSTIC_NOT_60FPS_ACCEPTANCE",workload="three observers; actual PhysicsScene FOV/LOS plus memory/report; stationary human bodies";
            public string unity,device,graphics,operatingSystem,profile="unity-bot-perception-v1@1";
            public int samples,width,height;public double p50ms,p95ms,p99ms,worstms,totalMs;
            public bool focused,muted,gpuTimingAvailable=false;public long allocatedMemoryBytes;
        }
        Button Button(string name)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        void NewKnowledge()
        {
            knowledge=new NativeBotPerception(ground.Session.Match.Roster,new[]{NativeBotDifficulty.Easy,NativeBotDifficulty.Normal,NativeBotDifficulty.Hard},ground.BotPerceptionProfile);
            provider=new NativeBotObservationProvider(ground.Session,ground.Session.Match.Roster,gameObject.scene.GetPhysicsScene(),ground.Profile,ground.CombatProfile);
        }
        void Sample(double time)=>knowledge.Sample(time,provider.Observe(knowledge));
        void Place()
        {
            ground.PlaceCombatReviewSeat(0,new Vector3(1,0,-8),0,90);
            ground.PlaceCombatReviewSeat(1,new Vector3(1,0,-11),0,180);
            ground.PlaceCombatReviewSeat(2,new Vector3(3,0,-8),0,90);
            Physics.SyncTransforms();
        }
        void Check(bool pass,string message){if(!pass)throw new InvalidOperationException("PERCEPTION_REVIEW: "+message);}
        NativeBotMemoryEntry Enemy(int observer)=>knowledge.Read(observer).Enemies.Single(e=>e.Sighting.Participant==2);
        IEnumerator Start()
        {
            ground=GetComponent<ProvingGround>();var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"-botEvidence");
            directory=flag>=0&&flag+1<args.Length?args[flag+1]:Path.Combine(Application.persistentDataPath,"bot-perception-review");Directory.CreateDirectory(directory);
            while(!Application.isFocused)yield return null;
            pads=Enumerable.Range(0,3).Select(_=>InputSystem.AddDevice<Gamepad>()).ToArray();
            while(ground.LocalSeatCount>3)Button("seats-minus").onClick.Invoke();
            ground.SetMatchMode(NativeMatchMode.Teams);ground.SetTeam(0,NativeTeam.TeamA);ground.SetTeam(1,NativeTeam.TeamA);ground.SetTeam(2,NativeTeam.TeamB);
            CreateBanner();banner.text="BOT PERCEPTION · DEVELOPMENT DIAGNOSTIC · human roster, no bot actions";
            yield return Capture("setup-human-only");
            ground.StartCombatReview(pads);yield return new WaitForSeconds(.2f);Place();NewKnowledge();Sample(0);
            Check(Enemy(0).Visible&&knowledge.Read(1).Enemies.Length==0,"direct sight / delayed receiver");var original=Enemy(0).Sighting.Position;
            yield return Capture("direct-sight");
            ground.PlaceCombatReviewSeat(2,new Vector3(6,0,-8),0,90);Physics.SyncTransforms();Sample(.2);
            Check(!Enemy(0).Visible&&Enemy(0).Sighting.Position==original&&knowledge.Read(1).Enemies.Length==0,"hidden move leaked");
            yield return Capture("hidden-movement");Sample(.31);
            Check(!Enemy(1).Visible&&Enemy(1).ObservedAt==0&&Enemy(1).Sighting.Position==original,"delayed report changed origin");
            yield return Capture("delayed-team-report");
            var saved=JsonUtility.ToJson(knowledge.Capture());
            InputSystem.QueueStateEvent(pads[0],new GamepadState().WithButton(GamepadButton.Start));yield return new WaitForSecondsRealtime(.2f);
            InputSystem.QueueStateEvent(pads[0],new GamepadState());yield return new WaitForSecondsRealtime(.2f);
            Check(!ground.Running&&JsonUtility.ToJson(knowledge.Capture())==saved,"pause altered memory");yield return Capture("paused-memory");
            Button("Продолжить").onClick.Invoke();yield return null;
            Sample(2.01);Check(knowledge.Read(0).Enemies.Length==0&&knowledge.Read(1).Enemies.Length==1,"difficulty expiry");yield return Capture("easy-memory-expired");
            Sample(4.01);Check(knowledge.Read(0).Enemies.Length==0&&knowledge.Read(1).Enemies.Length==0,"report extended memory");yield return Capture("all-memory-expired");
            ground.PlaceCombatReviewSeat(2,new Vector3(3,0,-8),0,90);Physics.SyncTransforms();Sample(4.2);Check(Enemy(0).Visible,"reacquisition");yield return Capture("reacquired");
            var restored=new NativeBotPerception(ground.Session.Match.Roster,new[]{NativeBotDifficulty.Easy,NativeBotDifficulty.Normal,NativeBotDifficulty.Hard},ground.BotPerceptionProfile);
            restored.Restore(JsonUtility.FromJson<NativeBotPerceptionSnapshot>(JsonUtility.ToJson(knowledge.Capture())));
            Check(JsonUtility.ToJson(restored.Capture())==JsonUtility.ToJson(knowledge.Capture()),"snapshot roundtrip");
            Button("Повторить матч").onClick.Invoke();yield return null;NewKnowledge();Check(knowledge.Capture().Pending.Length==0,"repeat clear");yield return Capture("repeat-empty-knowledge");
            Button("В главное меню").onClick.Invoke();ground.SetMatchMode(NativeMatchMode.Ffa);ground.StartCombatReview(pads);yield return null;Place();NewKnowledge();Sample(0);Sample(.31);
            Check(knowledge.Read(1).Enemies.Length==0&&knowledge.Capture().Pending.Length==0,"FFA report leak");yield return Capture("ffa-no-reports");
            Benchmark();
            Button("В главное меню").onClick.Invoke();yield return Capture("return-setup");
            Debug.Log("NATIVE_BOT_PERCEPTION_REVIEW_COMPLETE "+directory);
        }
        void Benchmark()
        {
            // Fixed sample counts are measurement protocol, not designer/gameplay parameters.
            const int warmup=500,count=10000;var samples=new double[count];double time=knowledge.Time;
            var timer=new Stopwatch();
            for(int i=-warmup;i<count;i++)
            {
                time+=1d/ground.Profile.Get("simulation.fixedTickHz");timer.Restart();Sample(time);timer.Stop();
                if(i>=0)samples[i]=timer.Elapsed.TotalMilliseconds;
            }
            Array.Sort(samples);var report=new Timing{unity=Application.unityVersion,device=SystemInfo.deviceModel,graphics=SystemInfo.graphicsDeviceName,operatingSystem=SystemInfo.operatingSystem,
                samples=count,width=Screen.width,height=Screen.height,p50ms=samples[count/2],p95ms=samples[count*95/100],p99ms=samples[count*99/100],worstms=samples[count-1],totalMs=samples.Sum(),
                focused=Application.isFocused,muted=AudioListener.volume==0,allocatedMemoryBytes=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()};
            File.WriteAllText(Path.Combine(directory,"perception-overhead.json"),JsonUtility.ToJson(report,true));
        }
        IEnumerator Capture(string label)
        {
            while(!Application.isFocused)yield return null;
            if(knowledge!=null)banner.text="BOT PERCEPTION · DIAGNOSTIC ONLY · "+label+" · t="+knowledge.Time.ToString("F2")+"\n"+
                string.Join("   |   ",Enumerable.Range(0,knowledge.Count).Select(i=>"P"+(i+1)+": "+string.Join(", ",knowledge.Read(i).Enemies.Select(e=>"P"+(e.Sighting.Participant+1)+(e.Visible?" seen":" remembered")+" @"+e.ObservedAt.ToString("F2")))));
            yield return new WaitForEndOfFrame();
            var data=new Evidence{state=label,width=Screen.width,height=Screen.height,focused=Application.isFocused,muted=AudioListener.volume==0,
                actualPoses=Enumerable.Range(0,ground.Session.ParticipantCount).Select(ground.Session.Pose).ToArray(),knowledge=knowledge?.Capture()};
            File.WriteAllText(Path.Combine(directory,label+".json"),JsonUtility.ToJson(data,true));ScreenCapture.CaptureScreenshot(Path.Combine(directory,label+".png"));yield return null;
        }
        void CreateBanner()
        {
            // Development-only diagnostic layout, not a shipping readability/balance parameter.
            canvasRoot=new GameObject("perception-diagnostic",typeof(Canvas),typeof(CanvasScaler));canvasRoot.transform.SetParent(transform,false);
            var canvas=canvasRoot.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=200;
            var scaler=canvasRoot.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);
            var panel=new GameObject("banner",typeof(RectTransform),typeof(Image));panel.transform.SetParent(canvasRoot.transform,false);panel.GetComponent<Image>().color=new Color(.02f,.04f,.08f,.94f);
            var rect=panel.GetComponent<RectTransform>();rect.anchorMin=new Vector2(0,.86f);rect.anchorMax=new Vector2(1,1);rect.offsetMin=rect.offsetMax=Vector2.zero;
            var text=new GameObject("text",typeof(RectTransform),typeof(Text));text.transform.SetParent(panel.transform,false);banner=text.GetComponent<Text>();banner.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");banner.fontSize=24;banner.color=Color.white;banner.alignment=TextAnchor.MiddleCenter;banner.raycastTarget=false;
            banner.rectTransform.anchorMin=Vector2.zero;banner.rectTransform.anchorMax=Vector2.one;banner.rectTransform.offsetMin=banner.rectTransform.offsetMax=Vector2.zero;
        }
        void OnDestroy(){if(pads!=null)foreach(var pad in pads)if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);if(canvasRoot)Destroy(canvasRoot);}
    }
}
#endif

#endif
