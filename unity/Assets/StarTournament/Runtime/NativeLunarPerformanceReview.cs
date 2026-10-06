#if UNITY_EDITOR || DEVELOPMENT_BUILD || STAR_TOURNAMENT_DEVELOPMENT_QA
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Text;
using System.Linq;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace StarTournament.ProvingGround
{
    // Opt-in diagnostic perturbations only; never alters the shipped map, profiles or saved settings.
    public sealed class NativeLunarPerformanceReview : MonoBehaviour
    {
        [Serializable] sealed class Distribution
        {
            public int count,hitches16,hitches25,hitches50; public double mean,p50,p95,p99,max;
            public Distribution(IEnumerable<double> source) { var a=source.OrderBy(x=>x).ToArray();count=a.Length;if(count==0)return;mean=a.Average();max=a[count-1];hitches16=a.Count(x=>x>16.67);hitches25=a.Count(x=>x>25);hitches50=a.Count(x=>x>50);p50=a[(count-1)/2];p95=a[Math.Min(count-1,(int)(count*.95))];p99=a[Math.Min(count-1,(int)(count*.99))]; }
        }
        [Serializable] sealed class Sample
        {
            public string name,variant,state; public Vector3 feet;public float yaw,pitch,matchTime,fieldOfView,renderScale; public int width,height,quality,lights,shadowLights,cameras;public bool focused=true;public int focusedFrames,unfocusedFrames;
            public Distribution frameMs,cpuMs,gpuMs,mainMs,renderMs,presentWaitMs,drawCalls,setPass,triangles,vertices;
        }
        [Serializable] sealed class LightingOverride {public string group; public bool disable;public float range,intensity,innerAngle;public int stride=1;public bool checkerboard;public Vector3 offset;}
        [Serializable] sealed class Step {public string name,variant="baseline";public Vector3 feet;public float yaw,pitch,warmup=5,duration=8;public bool live;public LightingOverride[] lighting;}
        [Serializable] sealed class Study {public Step[] steps;}
        [Serializable] sealed class MeshInfo {public string name;public int vertices,triangles;public Vector3 bounds;public bool shadows;}
        [Serializable] sealed class LightInfo {public string name,type,shadows,group;public Vector3 position;public float range,intensity,spotAngle,innerSpotAngle;}
        [Serializable] sealed class Report
        {
            public string status="DIAGNOSTIC_NOT_ACCEPTANCE",map=LunarLaboratoryCatalog.Identity,unity,device,cpu,gpu,os,pipeline;
            public string protocol="Frozen seed 20261003, 8 hard bots, one camera; identical yaw/pitch/FOV, quality and resolution for spatial baselines (default 1920x1080). 5s warmup + 8s samples; reverse repeat. Diagnostic light/shadow/render-scale toggles restored after each sample. Simulation timed separately, no rendering in timed section.";
            public int seed=20261003,originalVsync,originalTargetRate,cpuThreads,systemMemoryMB,vsync;public double refreshHz;public float renderScale;public bool frameTimingEnabled;
            public List<LightInfo> lights=new List<LightInfo>();
            public List<Sample> samples=new List<Sample>();public List<MeshInfo> meshes=new List<MeshInfo>();public Distribution botMs,tickMs;public string frozenState;
        }
        ProvingGround ground;string directory;Report report;readonly FrameTiming[] timing=new FrameTiming[1];
        ProfilerRecorder draws,passes,triangles,vertices;
        Light[] lights;bool[] lightEnabled;LightShadows[] shadows;float[] ranges,intensities,innerAngles;Vector3[] positions;Step step;UniversalRenderPipelineAsset pipeline;
        IEnumerator Start()
        {
            Invoke(nameof(TimedOut),1200);
            Application.runInBackground=true;AudioListener.volume=0;var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-lunarEvidence");directory=i>=0?args[i+1]:Path.Combine(Application.persistentDataPath,"lunar-performance");Directory.CreateDirectory(directory);
            ground=GetComponent<ProvingGround>();while(!SplashScreen.isFinished)yield return null;
            int Dimension(string flag,int fallback){int at=Array.IndexOf(args,flag);return at>=0&&at+1<args.Length&&int.TryParse(args[at+1],out int value)&&value>=320&&value<=7680?value:fallback;}
            Screen.SetResolution(Dimension("-screen-width",1920),Dimension("-screen-height",1080),args.Contains("-lunarFullscreen")?FullScreenMode.FullScreenWindow:FullScreenMode.Windowed);yield return new WaitForSecondsRealtime(1);
            report=new Report{unity=Application.unityVersion,device=SystemInfo.deviceModel,cpu=SystemInfo.processorType,cpuThreads=SystemInfo.processorCount,systemMemoryMB=SystemInfo.systemMemorySize,gpu=SystemInfo.graphicsDeviceName,os=SystemInfo.operatingSystem,originalVsync=QualitySettings.vSyncCount,originalTargetRate=Application.targetFrameRate,frameTimingEnabled=FrameTimingManager.IsFeatureEnabled()};
            if(args.Contains("-lunarUncapped")){QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;}
            report.vsync=QualitySettings.vSyncCount;report.refreshHz=Screen.currentResolution.refreshRateRatio.value;
            ground.SendMessage("OpenSetup");ground.SelectAuthoredMap(LunarLaboratoryCatalog.Id);
            ground.StartBotReview(new NativeMatchComposition(NativeMatchRoster.Ffa(8),Enumerable.Range(0,8).Select(n=>new NativeParticipantInfo(NativeParticipantKind.Bot,"Bot "+n,NativeParticipantColors.For(n),(int)NativeBotDifficulty.Hard)).ToArray(),new[]{0}),(uint)report.seed);
            ground.FullHealReviewManualTick=true;
            report.frozenState=JsonUtility.ToJson(ground.Session.Capture());
            pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;report.pipeline=pipeline?pipeline.name:"none";report.renderScale=pipeline?pipeline.renderScale:1;
            lights=FindObjectsByType<Light>(FindObjectsSortMode.None);lightEnabled=lights.Select(l=>l.enabled).ToArray();shadows=lights.Select(l=>l.shadows).ToArray();ranges=lights.Select(l=>l.range).ToArray();intensities=lights.Select(l=>l.intensity).ToArray();positions=lights.Select(l=>l.transform.position).ToArray();innerAngles=lights.Select(l=>l.innerSpotAngle).ToArray();
            report.lights=lights.Select(l=>new LightInfo{name=l.name,group=Group(l),type=l.type.ToString(),shadows=l.shadows.ToString(),position=l.transform.position,range=l.range,intensity=l.intensity,spotAngle=l.spotAngle,innerSpotAngle=l.innerSpotAngle}).ToList();
            foreach(var r in ground.CameraReviewArena.GetComponentsInChildren<MeshRenderer>())if(r.enabled)
            {var mesh=r.GetComponent<MeshFilter>()?.sharedMesh;if(mesh)report.meshes.Add(new MeshInfo{name=r.name,vertices=mesh.vertexCount,triangles=(int)mesh.GetIndexCount(0)/3,bounds=r.bounds.size,shadows=r.shadowCastingMode!=ShadowCastingMode.Off});}
            draws=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count");passes=ProfilerRecorder.StartNew(ProfilerCategory.Render,"SetPass Calls Count");triangles=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Triangles Count");vertices=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Vertices Count");
            int planAt=Array.IndexOf(args,"-lunarStudy");
            if(planAt>=0)
            {
                var study=JsonUtility.FromJson<Study>(File.ReadAllText(args[planAt+1]));
                report.protocol="Explicit lighting study plan: "+File.ReadAllText(args[planAt+1]);
                foreach(var planned in study.steps){step=planned;yield return Measure(step.name,step.feet,step.variant);}
                RestoreRender();Save();Application.Quit(0);yield break;
            }
            // Camera translation is the only spatial-baseline variable; all other participants stay frozen.
            var hall=new Vector3(0,.02f,-9);var yard=new Vector3(0,.02f,-25);var upper=new Vector3(0,4.5f,-9);
            yield return Measure("yard-a",yard,"baseline");yield return Measure("hall-a",hall,"baseline");yield return Measure("upper-a",upper,"baseline");
            yield return Measure("hall-b",hall,"baseline");yield return Measure("yard-b",yard,"baseline");
            foreach(string variant in new[]{"no-shadows","no-local-lights","no-upper-lights","half-render-scale"})
            {yield return Measure("hall-"+variant,hall,variant);yield return Measure("yard-"+variant,yard,variant);}
            yield return Measure("hall-final",hall,"baseline");yield return Measure("yard-final",yard,"baseline");
            RestoreRender();
            ground.PlaceCombatReviewSeat(0,hall,0,0);
            var botTimes=new List<double>();var tickTimes=new List<double>();var actions=new LocalAction[8];float dt=1/ground.Profile.Get("simulation.fixedTickHz");var clock=new System.Diagnostics.Stopwatch();
            for(int tick=0;tick<600;tick++)
            {
                Array.Clear(actions,0,actions.Length);clock.Restart();ground.BotDriver.ProduceActions(actions,dt);clock.Stop();botTimes.Add(clock.Elapsed.TotalMilliseconds);
                clock.Restart();ground.Session.Tick(actions,dt);clock.Stop();tickTimes.Add(clock.Elapsed.TotalMilliseconds);if(tick%10==0)yield return null;
            }
            report.botMs=new Distribution(botTimes);report.tickMs=new Distribution(tickTimes);Save();Application.Quit(0);
        }
        void RestoreRender()
        {for(int n=0;n<lights.Length;n++)if(lights[n]){lights[n].enabled=lightEnabled[n];lights[n].shadows=shadows[n];lights[n].range=ranges[n];lights[n].intensity=intensities[n];lights[n].innerSpotAngle=innerAngles[n];lights[n].transform.position=positions[n];}if(pipeline)pipeline.renderScale=report.renderScale;}
        static string Group(Light l)
        {
            if(l.type==LightType.Directional)return "directional";
            if(l.name=="equipment-fill")return "equipment";
            if(Mathf.Abs(l.transform.position.x)>15&&Mathf.Abs(l.transform.position.x)<22)return "stairs-"+(l.type==LightType.Point?"point":"spot");
            if(l.name.StartsWith("Office")||l.name.StartsWith("Neutral")&&Mathf.Abs(l.transform.position.x)<12&&Mathf.Abs(l.transform.position.z)<12)
                return (l.transform.position.y>4.5f?"upper-":"lower-")+(l.type==LightType.Point?"point":"spot");
            return "exterior";
        }
        IEnumerator Measure(string name,Vector3 feet,string variant)
        {
            RestoreRender();
            for(int n=0;n<lights.Length;n++)
            {var l=lights[n];if(variant=="no-shadows")l.shadows=LightShadows.None;if(variant=="no-local-lights"&&l.type!=LightType.Directional)l.enabled=false;if(variant=="no-upper-lights"&&l.type!=LightType.Directional&&l.transform.position.y>LunarLaboratoryCatalog.Upper)l.enabled=false;}
            if(variant=="half-render-scale"&&pipeline)pipeline.renderScale=report.renderScale*.5f;
            if(step?.lighting!=null)foreach(var setting in step.lighting)
            {
                // Stable spatial ordering prevents FindObjects order from changing the selected fixture subset.
                var group=lights.Where(l=>Group(l)==setting.group).OrderBy(l=>l.transform.position.x).ThenBy(l=>l.transform.position.z).ToArray();
                var xs=group.Select(l=>l.transform.position.x).Distinct().OrderBy(x=>x).ToArray();var zs=group.Select(l=>l.transform.position.z).Distinct().OrderBy(z=>z).ToArray();
                for(int n=0;n<group.Length;n++)
                {
                    var l=group[n];int ordinal=setting.checkerboard?Array.IndexOf(xs,l.transform.position.x)+Array.IndexOf(zs,l.transform.position.z):n;
                    if(setting.disable||ordinal%Math.Max(1,setting.stride)!=0)l.enabled=false;
                    if(setting.innerAngle>0&&l.type==LightType.Spot)l.innerSpotAngle=setting.innerAngle;if(setting.range>0)l.range=setting.range;if(setting.intensity>0)l.intensity=setting.intensity;l.transform.position+=setting.offset;
                }
            }
            ground.FullHealReviewManualTick=!(step?.live??false);
            ground.PlaceCombatReviewSeat(0,feet,step?.pitch??0,step?.yaw??0);
            double warm=Time.realtimeSinceStartupAsDouble+(step?.warmup??5);while(Time.realtimeSinceStartupAsDouble<warm){FrameTimingManager.CaptureFrameTimings();yield return null;}
            var s=new Sample{name=name,variant=variant,feet=feet,yaw=step?.yaw??0,pitch=step?.pitch??0,state=JsonUtility.ToJson(ground.Session.Capture()),fieldOfView=ground.GetComponentsInChildren<Camera>().First(c=>c.enabled).fieldOfView,renderScale=pipeline?pipeline.renderScale:1,matchTime=(float)ground.Session.Time,width=Screen.width,height=Screen.height,quality=QualitySettings.GetQualityLevel(),lights=lights.Count(l=>l.enabled),shadowLights=lights.Count(l=>l.enabled&&l.shadows!=LightShadows.None),cameras=Camera.allCamerasCount};
            var frame=new List<double>();var cpu=new List<double>();var gpu=new List<double>();var main=new List<double>();var render=new List<double>();var wait=new List<double>();var dc=new List<double>();var sp=new List<double>();var tr=new List<double>();var ve=new List<double>();ulong timestamp=0;
            var csv=new StringBuilder("frame_ms,cpu_ms,gpu_ms,main_ms,render_ms,present_wait_ms,draw_calls,setpass,triangles,vertices,focused,frame_timestamp,timing_fresh\n");
            double until=Time.realtimeSinceStartupAsDouble+(step?.duration??8),previous=Time.realtimeSinceStartupAsDouble;
            while(Time.realtimeSinceStartupAsDouble<until)
            {
                FrameTimingManager.CaptureFrameTimings();yield return null;double now=Time.realtimeSinceStartupAsDouble;frame.Add((now-previous)*1000);previous=now;s.focused&=Application.isFocused;if(Application.isFocused)s.focusedFrames++;else s.unfocusedFrames++;
                bool fresh=FrameTimingManager.GetLatestTimings(1,timing)>0&&timing[0].frameStartTimestamp!=timestamp;
                if(fresh)
                {var t=timing[0];timestamp=t.frameStartTimestamp;if(t.cpuFrameTime>0)cpu.Add(t.cpuFrameTime);if(t.gpuFrameTime>0)gpu.Add(t.gpuFrameTime);if(t.cpuMainThreadFrameTime>0)main.Add(t.cpuMainThreadFrameTime);if(t.cpuRenderThreadFrameTime>0)render.Add(t.cpuRenderThreadFrameTime);wait.Add(t.cpuMainThreadPresentWaitTime);}
                var ft=timing[0];csv.AppendLine(string.Join(",",new double[]{frame[frame.Count-1],fresh?ft.cpuFrameTime:double.NaN,fresh&&ft.gpuFrameTime>0?ft.gpuFrameTime:double.NaN,fresh?ft.cpuMainThreadFrameTime:double.NaN,fresh?ft.cpuRenderThreadFrameTime:double.NaN,fresh?ft.cpuMainThreadPresentWaitTime:double.NaN,draws.Valid?draws.LastValue:-1,passes.Valid?passes.LastValue:-1,triangles.Valid?triangles.LastValue:-1,vertices.Valid?vertices.LastValue:-1,Application.isFocused?1:0}.Select(v=>double.IsNaN(v)?"":v.ToString("R",CultureInfo.InvariantCulture)))+","+(fresh?ft.frameStartTimestamp.ToString():"")+","+(fresh?"1":"0"));
                if(draws.Valid)dc.Add(draws.LastValue);if(passes.Valid)sp.Add(passes.LastValue);if(triangles.Valid)tr.Add(triangles.LastValue);if(vertices.Valid)ve.Add(vertices.LastValue);
            }
            File.WriteAllText(Path.Combine(directory,name+".csv"),csv.ToString());
            s.frameMs=new Distribution(frame);s.cpuMs=new Distribution(cpu);s.gpuMs=new Distribution(gpu);s.mainMs=new Distribution(main);s.renderMs=new Distribution(render);s.presentWaitMs=new Distribution(wait);s.drawCalls=new Distribution(dc);s.setPass=new Distribution(sp);s.triangles=new Distribution(tr);s.vertices=new Distribution(ve);report.samples.Add(s);Save();
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"));yield return new WaitForSecondsRealtime(.3f);
        }
        void TimedOut(){if(report!=null){report.status="INCOMPLETE_TIMEOUT";Save();}Application.Quit(2);}
        void Save()=>File.WriteAllText(Path.Combine(directory,"performance.json"),JsonUtility.ToJson(report,true));
        void OnDestroy(){draws.Dispose();passes.Dispose();triangles.Dispose();vertices.Dispose();if(report!=null&&lights!=null)RestoreRender();}
    }
}
#endif

#endif
