#if UNITY_EDITOR || DEVELOPMENT_BUILD || STAR_TOURNAMENT_DEVELOPMENT_QA
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingGround
    {
        internal int ProjectilePerformanceVisualCount(WeaponId weapon)
        {
            if(weapon==WeaponId.Cutter){for(int i=0;i<4;i++)if(!Session.Beam(i).Active)return 0;return 1;}
            return weapon==WeaponId.RocketLauncher?presentation.RocketEffectsForReview.ActiveBursts+presentation.RocketEffectsForReview.ActiveFlights:presentation.ActiveShotEffectCount;
        }
    }
    // Derived from NativeModelPerformanceReview @6951a008 (2026-10-29).
    // Opt-in isolated Player diagnostic; never changes persisted graphics or product models.
    [DefaultExecutionOrder(10000)]
    public sealed class NativeProjectilePerformanceReview:MonoBehaviour
    {
        [Serializable] sealed class Distribution
        {
            public int count,over16,over33,over50;public double mean,p50,p95,p99,max;
            public Distribution(IEnumerable<double> values){var a=values.OrderBy(v=>v).ToArray();count=a.Length;if(count==0)return;mean=a.Average();p50=a[count/2];p95=a[Math.Min(count-1,(int)(count*.95))];p99=a[Math.Min(count-1,(int)(count*.99))];max=a[count-1];over16=a.Count(v=>v>16.667);over33=a.Count(v=>v>33.333);over50=a.Count(v=>v>50);}
        }
        [Serializable] sealed class Sample
        {
            public Distribution hitObserveMs;
            public long contactProjections,contactFindCalls,contactReuses,contactRequests;
            public string weapon,variant;public int views,width,height,unfocused,nonrunning;public long allocatedBytes,reservedBytes,monoBytes;public double simulationSeconds;
            public Distribution frame,cpu,gpu,main,render,present,draws,triangles,setpass,tickMs,gcBytes,cutterMs,rocketMs,plasmaMs;public int shots,damageNotices,rocketHits,plasmaHits,visualActiveFrames,focusTransitions;
        }
        [Serializable] sealed class MeshRow{public string path,kind;public int triangles,vertices,materials;public bool active;}
        [Serializable] sealed class Report
        {
            public string hitPoseReuse;
            public string status="RUNNING",body,unity,cpu,gpu,os,pipeline,protocol="seed 20261029; 4 shooters/4 shielded targets 6m apart in open south yard; health/armor 100 replenished; rocket/shotgun press edges every tick subject to unchanged cooldown; real fixed-step firing with resource replenishment, cooldowns retained; opt-in synthetic-input owner ignores device-focus resets, ordinary gameplay unchanged; each frozen sample resets canonical initial snapshot then runs exactly 30 ordinary .02s firing ticks; effects must remain active on every frame; 5s warmup+8s idle/20s firing/8s frozen-contact; same F23 body and product defaults; actual Player, no capture during timed samples. Sustained diagnostic fire is not an ordinary match average.";
            public int views,width,height,quality,systemMemoryMB;public bool frameTiming,muted;public float renderScale;public List<Sample> samples=new List<Sample>();
        }
        ProvingGround ground;string directory,variant="full";int views;int damageNotices,rocketHits,plasmaHits;Report report;GameObject currentBody;
        Renderer[] costume=Array.Empty<Renderer>(),weapons=Array.Empty<Renderer>();readonly FrameTiming[] timings=new FrameTiming[1];
        ProfilerRecorder draws,triangles,passes,gcCost,cutterCost,rocketCost,plasmaCost;
        double tickMilliseconds;
        bool firing; NativeCombatSessionSnapshot initialSnapshot; LocalAction[] fireActions; CombatLife[] refill; CombatLifeState[] reserves;
        static NativeProjectilePerformanceReview activeDiagnostic;
        internal static bool OwnsInput(ProvingGround instance)=>activeDiagnostic&&activeDiagnostic.ground==instance;
        void Awake(){activeDiagnostic=this;Application.logMessageReceived+=Failure;}
        void Failure(string message,string trace,LogType type){if(type!=LogType.Exception)return;if(report!=null){report.status="INVALID_DIAGNOSTIC";Save();}Application.Quit(2);}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Bootstrap()
        {if(Environment.GetCommandLineArgs().Contains("-projectilePerformance"))new GameObject("Projectile performance diagnostic").AddComponent<NativeProjectilePerformanceReview>();}
        static string Arg(string key,string fallback=null){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,key);return i>=0&&i+1<a.Length?a[i+1]:fallback;}
        IEnumerator Start()
        {
            directory=Arg("-projectilePerformance");if(!Path.IsPathFullyQualified(directory??""))throw new InvalidOperationException("Owned absolute evidence path required");Directory.CreateDirectory(directory);
            Invoke(nameof(Timeout),900);Application.runInBackground=true;views=int.Parse(Arg("-projectileViews","1"));if(views!=1&&views!=4)throw new InvalidOperationException("Expected 1 or 4 views");
            while(!(ground=FindFirstObjectByType<ProvingGround>())||!ground.IsReady||!SplashScreen.isFinished)yield return null;
            Screen.SetResolution(1920,1080,FullScreenMode.Windowed);QualitySettings.SetQualityLevel(NativeVisualPreferences.QualityTier(NativeVisualPreferences.DefaultQuality),true);QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
            currentBody=ground.TrooperBodyPrefab;
            ground.SendMessage("OpenSetup");ground.SelectAuthoredMap(LunarLaboratoryCatalog.Id);Begin();yield return null;yield return new WaitForSecondsRealtime(1);initialSnapshot=ground.Session.Capture();
            string hitMode=Arg("-hitPoseReuse","current");if(hitMode!="current"&&hitMode!="baseline")throw new InvalidOperationException("Expected hitPoseReuse baseline or current");
            ground.HitFeedbackForReview.SetPoseReuseForDiagnostics(hitMode=="current");
            SkinnedContactPatch.OriginalTransformLookupForDiagnostics=hitMode=="baseline";
            var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            report=new Report{body="F23-887439e0",hitPoseReuse=hitMode,views=views,width=Screen.width,height=Screen.height,quality=QualitySettings.GetQualityLevel(),unity=Application.unityVersion,cpu=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName,os=SystemInfo.operatingSystem,systemMemoryMB=SystemInfo.systemMemorySize,pipeline=pipeline?pipeline.name:"none",renderScale=pipeline?pipeline.renderScale:1,frameTiming=FrameTimingManager.IsFeatureEnabled(),muted=AudioListener.volume==0};
            if(Screen.width!=1920||Screen.height!=1080||!report.muted)throw new InvalidOperationException("Resolution/silent admission failed");
            draws=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count");triangles=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Triangles Count");passes=ProfilerRecorder.StartNew(ProfilerCategory.Render,"SetPass Calls Count");
            gcCost=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame");cutterCost=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"ST.Cutter.Render");rocketCost=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"ST.Rocket.Render");plasmaCost=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"ST.Plasma.Render");
            ground.Session.Damaged+=_=>damageNotices++;ground.Session.RocketExploded+=_=>rocketHits++;ground.Session.PlasmaBoltHit+=_=>plasmaHits++;
            foreach(WeaponId weapon in Arg("-projectileWeapons","Cutter,RocketLauncher,Plasma,Rifle,Shotgun").Split(',').Select(w=>(WeaponId)Enum.Parse(typeof(WeaponId),w)))
            {
                Equip(weapon);yield return null;yield return null;Inventory(weapon);
                firing=false;yield return Measure(weapon,"idle",8);
                firing=true;yield return Measure(weapon,"firing",20);firing=false;
                var measured=report.samples[report.samples.Count-1];if(measured.damageNotices==0||(weapon!=WeaponId.Cutter&&measured.shots==0))throw new InvalidOperationException("Invalid firing workload: "+weapon+" shots="+measured.shots+" damage="+measured.damageNotices);
                // Prepare a matched ordinary-fire state, outside the timed section. No new
                // damage callbacks during frozen timing: isolate visual rendering from F23 projection.
                PrepareFrozen(weapon);yield return Measure(weapon,"frozen-contact",8);
                // Capture an explicit ordinary firing state outside the timed interval.
                yield return Capture(weapon);
            }
            report.status="COMPLETE_DIAGNOSTIC_NOT_ACCEPTANCE";Save();Debug.Log("PROJECTILE_PERFORMANCE_COMPLETE "+directory);Application.Quit(0);
        }
        void Begin()
        {
            ground.StartBotReview(new NativeMatchComposition(NativeMatchRoster.Ffa(8),Enumerable.Range(0,8).Select(n=>new NativeParticipantInfo(n<views?NativeParticipantKind.LocalHuman:NativeParticipantKind.Bot,n<views?"Player "+n:"Bot "+n,NativeParticipantColors.For(n),n<views?-1:(int)NativeBotDifficulty.Hard)).ToArray(),Enumerable.Range(0,views).ToArray()),20261029);
            ground.FullHealReviewManualTick=true;
            for(int i=0;i<8;i++)ground.PlaceCombatReviewSeat(i,new Vector3((i%4-1.5f)*6,0.02f,i<4?-26:-16),0,i<4?0:180);
        }
        void Equip(WeaponId weapon)
        {
            if(initialSnapshot!=null)ground.Session.Restore(initialSnapshot);
            refill=(CombatLife[])typeof(NativeCombatSession).GetField("lives",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(ground.Session);
            foreach(var life in refill){life.CollectWeapon(WeaponId.Cutter);life.CollectWeapon(WeaponId.RocketLauncher);life.CollectWeapon(WeaponId.Plasma);life.CollectWeapon(WeaponId.Shotgun);}
            var snapshot=ground.Session.Capture();
            for(int i=0;i<snapshot.Lives.Length;i++){var l=snapshot.Lives[i];l.ShotgunOwned=l.RocketOwned=l.CutterOwned=l.PlasmaOwned=true;l.Health=100;l.Armor=100;l.SelectedWeapon=weapon;l.PendingWeapon=default;l.SwitchRemaining=0;l.Ammo=CombatLife.AmmoFor(l,weapon);snapshot.Lives[i]=l;}
            ground.Session.Restore(snapshot);ground.Session.Tick(new LocalAction[8],.02f);reserves=ground.Session.Capture().Lives;fireActions=new LocalAction[8];for(int i=0;i<4;i++)fireActions[i].FireHeld=true;
        }
        void FixedUpdate()
        {
            if(!firing||refill==null)return;TickFire();
        }
        void PrepareFrozen(WeaponId weapon)
        {
            Equip(weapon);ground.SendMessage("LateUpdate");
            for(int tick=0;tick<30;tick++)TickFire();
        }
        void TickFire()
        {
            // Controlled sustained-contact workload: replenish resources only, retain cooldowns,
            // ordinary session collision/damage/projectiles and presentation lifecycle.
            for(int i=0;i<refill.Length;i++){var l=refill[i].Read();var r=reserves[i];l.Health=r.Health;l.Armor=r.Armor;l.RifleAmmo=r.RifleAmmo;l.ShotgunAmmo=r.ShotgunAmmo;l.RocketAmmo=r.RocketAmmo;l.PlasmaAmmo=r.PlasmaAmmo;l.CutterEnergy=r.CutterEnergy;l.Ammo=CombatLife.AmmoFor(l,l.SelectedWeapon);refill[i].Restore(l);}
            for(int i=0;i<4;i++)fireActions[i].Fire=reserves[i].SelectedWeapon==WeaponId.RocketLauncher||reserves[i].SelectedWeapon==WeaponId.Shotgun;
            long began=System.Diagnostics.Stopwatch.GetTimestamp();ground.Session.Tick(fireActions,Time.fixedDeltaTime);tickMilliseconds+=(System.Diagnostics.Stopwatch.GetTimestamp()-began)*1000.0/System.Diagnostics.Stopwatch.Frequency;
        }
        static bool IsWeapon(Renderer r)=>r.name=="weapon:joined"||r.transform.GetComponentsInParent<Transform>(true).Any(t=>t.name=="automatic-rifle"||t.name=="pulse-launcher"||t.name=="cutter"||t.name=="forge-plasma"||t.name.EndsWith("ammo-display"));
        void RefreshRenderers()
        {
            weapons=ground.GetComponentsInChildren<WeaponModelPresentation>(true).SelectMany(w=>w.GetComponentsInChildren<Renderer>(true)).Where(IsWeapon).Distinct().ToArray();
            costume=ground.GetComponentsInChildren<TrooperVisual>(true).Where(t=>t.name=="trooper-presentation").SelectMany(t=>t.GetComponentsInChildren<SkinnedMeshRenderer>(true)).Where(r=>!IsWeapon(r)).Cast<Renderer>().ToArray();
        }
        void LateUpdate(){foreach(var r in weapons)if(r)r.forceRenderingOff=variant=="weapons-hidden";foreach(var r in costume)if(r)r.forceRenderingOff=variant=="world-costume-hidden";}
        void Inventory(WeaponId weapon)
        {
            RefreshRenderers();var rows=new List<MeshRow>();
            foreach(var r in ground.GetComponentsInChildren<Renderer>(true))
            {
                if(!r.GetComponentInParent<TrooperVisual>())continue;var mesh=r is SkinnedMeshRenderer skin?skin.sharedMesh:r.GetComponent<MeshFilter>()?.sharedMesh;if(!mesh)continue;
                rows.Add(new MeshRow{path=PathOf(r.transform),kind=IsWeapon(r)?"weapon":r.GetComponentInParent<Camera>()?"fps-costume":"world-costume",triangles=(int)Enumerable.Range(0,mesh.subMeshCount).Sum(i=>(long)mesh.GetIndexCount(i)/3),vertices=mesh.vertexCount,materials=r.sharedMaterials.Length,active=r.enabled&&r.gameObject.activeInHierarchy});
            }
            File.WriteAllText(Path.Combine(directory,weapon+"-inventory.json"),JsonUtility.ToJson(new MeshRows{items=rows},true));
        }
        [Serializable] sealed class MeshRows{public List<MeshRow> items;}
        static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
        IEnumerator Measure(WeaponId weapon,string mode,float seconds)
        {
            variant=mode;RefreshRenderers();double warm=Time.realtimeSinceStartupAsDouble+5;while(Time.realtimeSinceStartupAsDouble<warm){FrameTimingManager.CaptureFrameTimings();yield return null;}
            var observe=new List<double>();var hitEffects=ground.HitFeedbackForReview;long previousObserve=hitEffects.ObserveTicks;var cacheBefore=hitEffects.PoseReuseDiagnostics();
            var ticks=new List<double>();tickMilliseconds=0;var gc=new List<double>();var cc=new List<double>();var rc=new List<double>();var pc=new List<double>();int shotStart=ground.Session.ShotCount,damageStart=damageNotices,rocketStart=rocketHits,plasmaStart=plasmaHits;
            var frame=new List<double>();var cpu=new List<double>();var gpu=new List<double>();var main=new List<double>();var render=new List<double>();var present=new List<double>();var dc=new List<double>();var tr=new List<double>();var sp=new List<double>();
            bool focused=Application.isFocused;
            var s=new Sample{weapon=weapon.ToString(),variant=mode,views=views,width=Screen.width,height=Screen.height};double simulation=ground.Session.Time,previous=Time.realtimeSinceStartupAsDouble,until=previous+seconds;ulong stamp=0;
            var csv=new StringBuilder("frame_ms,cpu_ms,gpu_ms,draw_calls,triangles,setpass,focused,cutter_ms,rocket_ms,plasma_ms,tick_ms,hit_observe_ms\n");
            while(Time.realtimeSinceStartupAsDouble<until)
            {
                FrameTimingManager.CaptureFrameTimings();yield return null;double now=Time.realtimeSinceStartupAsDouble;frame.Add((now-previous)*1000);previous=now;
                if(!Application.isFocused)s.unfocused++;if(Application.isFocused!=focused){s.focusTransitions++;focused=Application.isFocused;}if(ground.ProjectilePerformanceVisualCount(weapon)>0)s.visualActiveFrames++;if(!ground.Running)s.nonrunning++;
                bool fresh=FrameTimingManager.GetLatestTimings(1,timings)>0&&timings[0].frameStartTimestamp!=stamp;var t=timings[0];
                if(fresh){stamp=t.frameStartTimestamp;if(t.cpuFrameTime>0)cpu.Add(t.cpuFrameTime);if(t.gpuFrameTime>0)gpu.Add(t.gpuFrameTime);if(t.cpuMainThreadFrameTime>0)main.Add(t.cpuMainThreadFrameTime);if(t.cpuRenderThreadFrameTime>0)render.Add(t.cpuRenderThreadFrameTime);present.Add(t.cpuMainThreadPresentWaitTime);}
                ticks.Add(tickMilliseconds);tickMilliseconds=0;if(gcCost.Valid)gc.Add(gcCost.LastValue);if(cutterCost.Valid)cc.Add(cutterCost.LastValue/1e6);if(rocketCost.Valid)rc.Add(rocketCost.LastValue/1e6);if(plasmaCost.Valid)pc.Add(plasmaCost.LastValue/1e6);
                long nowObserve=hitEffects.ObserveTicks;double observeDelta=(nowObserve-previousObserve)*1000.0/System.Diagnostics.Stopwatch.Frequency;observe.Add(observeDelta);previousObserve=nowObserve;
                if(draws.Valid)dc.Add(draws.LastValue);if(triangles.Valid)tr.Add(triangles.LastValue);if(passes.Valid)sp.Add(passes.LastValue);
                csv.AppendLine(string.Join(",",new double[]{frame[frame.Count-1],fresh?t.cpuFrameTime:double.NaN,fresh?t.gpuFrameTime:double.NaN,draws.Valid?draws.LastValue:-1,triangles.Valid?triangles.LastValue:-1,passes.Valid?passes.LastValue:-1,Application.isFocused?1:0,cutterCost.Valid?cutterCost.LastValue/1e6:double.NaN,rocketCost.Valid?rocketCost.LastValue/1e6:double.NaN,plasmaCost.Valid?plasmaCost.LastValue/1e6:double.NaN,ticks[ticks.Count-1],observeDelta}.Select(v=>v.ToString("R",CultureInfo.InvariantCulture))));
            }
            var cacheAfter=hitEffects.PoseReuseDiagnostics();s.hitObserveMs=new Distribution(observe);s.contactProjections=cacheAfter[0]-cacheBefore[0];s.contactFindCalls=cacheAfter[1]-cacheBefore[1];s.contactReuses=cacheAfter[2]-cacheBefore[2];s.contactRequests=cacheAfter[3]-cacheBefore[3];
            s.tickMs=new Distribution(ticks);s.gcBytes=new Distribution(gc);s.shots=ground.Session.ShotCount-shotStart;s.damageNotices=damageNotices-damageStart;s.rocketHits=rocketHits-rocketStart;s.plasmaHits=plasmaHits-plasmaStart;s.cutterMs=new Distribution(cc);s.rocketMs=new Distribution(rc);s.plasmaMs=new Distribution(pc);s.frame=new Distribution(frame);s.cpu=new Distribution(cpu);s.gpu=new Distribution(gpu);s.main=new Distribution(main);s.render=new Distribution(render);s.present=new Distribution(present);s.draws=new Distribution(dc);s.triangles=new Distribution(tr);s.setpass=new Distribution(sp);s.allocatedBytes=Profiler.GetTotalAllocatedMemoryLong();s.reservedBytes=Profiler.GetTotalReservedMemoryLong();s.monoBytes=Profiler.GetMonoUsedSizeLong();s.simulationSeconds=ground.Session.Time-simulation;
            if(mode=="frozen-contact"&&s.visualActiveFrames!=s.frame.count)throw new InvalidOperationException("Inactive frozen effects: "+weapon+" active="+s.visualActiveFrames+" frames="+s.frame.count);
            report.samples.Add(s);File.WriteAllText(Path.Combine(directory,weapon+"-"+mode+".csv"),csv.ToString());Save();Debug.Log("MODEL_SAMPLE "+report.body+" "+views+" "+weapon+" "+mode+" p50="+s.frame.p50+" p95="+s.frame.p95+" GPU="+s.gpu.p50);
        }
        IEnumerator Capture(WeaponId weapon){variant="full";yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,weapon+"-fps.png"));File.WriteAllText(Path.Combine(directory,weapon+"-capture.json"),JsonUtility.ToJson(ground.Session.Capture(),true));yield return new WaitForSecondsRealtime(.5f);}
        void Save()=>File.WriteAllText(Path.Combine(directory,"performance.json"),JsonUtility.ToJson(report,true));
        void Timeout(){if(report!=null){report.status="INCOMPLETE_TIMEOUT";Save();}Application.Quit(2);}
        void OnDestroy(){Application.logMessageReceived-=Failure;if(activeDiagnostic==this)activeDiagnostic=null;gcCost.Dispose();cutterCost.Dispose();rocketCost.Dispose();plasmaCost.Dispose();draws.Dispose();triangles.Dispose();passes.Dispose();foreach(var r in weapons)if(r)r.forceRenderingOff=false;foreach(var r in costume)if(r)r.forceRenderingOff=false;if(ground&&currentBody)ground.TrooperBodyPrefab=currentBody;}
    }
}
#endif
