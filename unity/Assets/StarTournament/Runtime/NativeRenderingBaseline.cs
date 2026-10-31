#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
    // Development-only, opt-in evidence adapter. No asset/settings persistence.
    public sealed class NativeRenderingBaseline : MonoBehaviour
    {
        public const uint Seed=20261025;
        public static readonly string[] Maps={CombatBowlCatalog.Id,IndustrialTunnelsCatalog.Id,LunarLaboratoryCatalog.Id,WellArenaCatalog.Id};
        public static bool Requested(string[] args)=>args.Contains("-renderingBaseline");
        public static NativeMatchComposition Composition(int views)=>Composition(CombatBowlCatalog.Id,views);
        public static NativeMatchComposition Composition(string map,int views)
        {
            if(views!=1&&views!=4)throw new ArgumentOutOfRangeException(nameof(views));
            int participants=AuthoredArenaCatalog.Maximum(map);
            if(views>participants||!AuthoredArenaCatalog.Supports(map,participants))throw new ArgumentException("Unsupported map composition");
            return new NativeMatchComposition(NativeMatchRoster.Ffa(participants),Enumerable.Range(0,participants).Select(n=>new NativeParticipantInfo(NativeParticipantKind.Bot,"Bot "+n,NativeParticipantColors.For(n),(int)NativeBotDifficulty.Hard)).ToArray(),Enumerable.Range(0,views).ToArray());
        }
        public static NativeMatchConfiguration Configuration(ProvingProfile profile)=>new NativeMatchConfiguration{DurationMinutes=5,TargetEnabled=false,TargetPoints=(int)profile.Get("match.ffaTargetDefault")};
        public static bool ReadyForReview(bool ready,bool splashFinished,bool batchMode)=>ready&&(batchMode||splashFinished);
        public static void RestoreReference(NativeCombatSession session,string json)
        {
            session.Restore(JsonUtility.FromJson<NativeCombatSessionSnapshot>(json));Physics.SyncTransforms();
            if(JsonUtility.ToJson(session.Capture())!=json)throw new InvalidOperationException("Reference snapshot round-trip mismatch");
        }
        [Serializable] sealed class MotorInfo{public string name;public Vector3 position,euler;public ParticipantState state;}
        [Serializable] sealed class RenderInfo{public string name,path;public Vector3 position,euler,scale,boundsCenter,boundsSize;public bool enabled;}
        [Serializable] sealed class VisualState{public MotorInfo[] motors;public RenderInfo[] renderers;public int particleSystems,liveParticles,lights;}
        [Serializable] public sealed class Distribution
        {
            public int count;public double mean,p50,p95,p99,max;
            public Distribution(IEnumerable<double> values){var a=values.OrderBy(x=>x).ToArray();count=a.Length;if(count==0)return;mean=a.Average();p50=a[(count-1)/2];p95=a[Math.Min(count-1,(int)(count*.95))];p99=a[Math.Min(count-1,(int)(count*.99))];max=a[count-1];}
        }
        [Serializable] sealed class CameraSet{public CameraInfo[] items;}
        [Serializable] sealed class CameraInfo{public string name;public Vector3 position,euler;public Rect viewport;public float fov,near,far;public bool postProcessing;public int antialiasing;}
        [Serializable] sealed class MeshInfo{public string renderer,mesh,material;public int vertices;public long indices,bytes;public bool readable;public Vector3 bounds;}
        [Serializable] public sealed class AssetScope
        {
            public long inclusiveOwnerMeshBytes,unreferencedOwnerMeshBytes,loadedMaterialBytes,loadedTextureBytes;
            public int inclusiveOwnerMeshCount,unreferencedOwnerMeshCount,loadedTextureCount;
        }
        // Values only escape this call: no retained Unity asset references, mutation or lifetime changes.
        public static AssetScope ReadAssetScope(Transform arena)
        {
            var inclusive=ArenaMeshResidency.ReadInclusiveOwnedMeshes(arena);
            var unused=ArenaMeshResidency.ReadUnreferencedOwnedMeshes(arena);
            var materials=Resources.FindObjectsOfTypeAll<Material>();var textures=Resources.FindObjectsOfTypeAll<Texture>();
            return new AssetScope{inclusiveOwnerMeshCount=inclusive.Length,inclusiveOwnerMeshBytes=inclusive.Sum(m=>Profiler.GetRuntimeMemorySizeLong(m)),unreferencedOwnerMeshCount=unused.Length,unreferencedOwnerMeshBytes=unused.Sum(m=>Profiler.GetRuntimeMemorySizeLong(m)),loadedMaterialBytes=materials.Sum(m=>Profiler.GetRuntimeMemorySizeLong(m)),loadedTextureCount=textures.Length,loadedTextureBytes=textures.Sum(t=>Profiler.GetRuntimeMemorySizeLong(t))};
        }
        [Serializable] public sealed class PresentationClock
        {
            public double realtime,scaled,unscaled;public int frame;
        }
        static PresentationClock ReadClock()=>new PresentationClock{realtime=Time.realtimeSinceStartupAsDouble,scaled=Time.timeAsDouble,unscaled=Time.unscaledTimeAsDouble,frame=Time.frameCount};
        [Serializable] sealed class Sample
        {
            public string name,status="DIAGNOSTIC_NOT_ACCEPTANCE",stateStart,stateEnd,referenceState,actualMap,visualStart,visualEnd,gpuStatus="NotRun",cpuStatus="NotRun";
            public int views,width,height,quality,aa,vsync,frameCap,unfocusedFrames,nonRunningFrames;public string qualityName,shadows;public float renderScale;public double simulationStart,simulationEnd;
            public NativeCompositionSnapshot composition;public float shadowDistance;public int shadowCascades;public string pipelineSettings;public CameraInfo[] camerasStart,camerasEnd;public long allocated,reserved,mono,workingSet,meshBytes;public int meshCount,materialCount;
            public AssetScope assetScope;public PresentationClock presentationStart,presentationEnd;public string cpuProfile;
            public Distribution frameMs,cpuMs,gpuMs;public string[] counterNames;public bool[] counterAvailable;public Distribution[] counters;public List<MeshInfo> meshes=new List<MeshInfo>();
        }
        [Serializable] sealed class Report
        {
            public string status="INCOMPLETE",unity,device,cpu,gpu,os,graphicsApi,map,pipeline,protocol="v2: authored max Hard bots (Tunnels4, Bowl/Lunar/Well8)/FFA seed20261025, 1920x1080, uncapped; ordinary1 warmup5/sample15; fresh frozen1 and frozen4 warmup5/sample8; no asset/light/quality perturbations",source;
            public List<string> nativeBackgroundEvents=new List<string>();public string focusRoute="ordinary-windowed";public int ownBackgroundWindows;public bool batchMode;public string referenceInput;public uint seed=Seed;public int cpuThreads,systemMemoryMB,graphicsMemoryMB;public bool frameTimingEnabled;public List<Sample> samples=new List<Sample>();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Bootstrap(){if(Requested(Environment.GetCommandLineArgs()))new GameObject("Rendering baseline QA").AddComponent<NativeRenderingBaseline>();}
        ProvingGround ground;Report report,reference;string directory,map,cpuProfileDirectory;bool background;readonly FrameTiming[] timings=new FrameTiming[1];
        static readonly string[] Names={"Draw Calls Count","SetPass Calls Count","Triangles Count","Vertices Count","Batches Count","Main Thread","Render Thread","GC Allocated In Frame"};
        static readonly ProfilerCategory[] Categories={ProfilerCategory.Render,ProfilerCategory.Render,ProfilerCategory.Render,ProfilerCategory.Render,ProfilerCategory.Render,ProfilerCategory.Internal,ProfilerCategory.Internal,ProfilerCategory.Memory};
        ProfilerRecorder[] recorders;
        IEnumerator Start()
        {
            Invoke(nameof(Timeout),300);Application.runInBackground=true;
            var args=Environment.GetCommandLineArgs();string Arg(string flag){int at=Array.IndexOf(args,flag);if(at<0||at+1>=args.Length)throw new ArgumentException("Required "+flag);return args[at+1];}
            directory=Arg("-baselineEvidence");if(!Path.IsPathFullyQualified(directory))throw new ArgumentException("Absolute owned evidence required");Directory.CreateDirectory(directory);
            if(args.Contains("-baselineCpuProfile"))cpuProfileDirectory=Arg("-baselineCpuProfile");
            RenderingBaselineCpuCapture.Output(cpuProfileDirectory,"frozen4");
            map=Arg("-baselineMap");if(!Maps.Contains(map))throw new ArgumentException("Unsupported map");
            var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            report=new Report{batchMode=Application.isBatchMode,unity=Application.unityVersion,device=SystemInfo.deviceModel,cpu=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName,os=SystemInfo.operatingSystem,graphicsApi=SystemInfo.graphicsDeviceType.ToString(),map=map,pipeline=pipeline?pipeline.name:"none",cpuThreads=SystemInfo.processorCount,systemMemoryMB=SystemInfo.systemMemorySize,graphicsMemoryMB=SystemInfo.graphicsMemorySize,frameTimingEnabled=FrameTimingManager.IsFeatureEnabled(),source=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_BASELINE_SOURCE")};Save();
            while(!ground||!ReadyForReview(ground.IsReady,SplashScreen.isFinished,Application.isBatchMode)){ground=FindFirstObjectByType<ProvingGround>();yield return null;}
            Screen.SetResolution(1920,1080,FullScreenMode.Windowed);yield return new WaitForSecondsRealtime(1);
            background=args.Contains("-baselineBackground");if(background){report.focusRoute="macOS-own-window-background";report.ownBackgroundWindows=RenderingBaselineMacBackground.Apply();report.nativeBackgroundEvents.Add(RenderingBaselineMacBackground.LastEvidence);Save();}
            int refAt=Array.IndexOf(args,"-baselineReference");if(refAt>=0){report.referenceInput=args[refAt+1];reference=JsonUtility.FromJson<Report>(File.ReadAllText(report.referenceInput));if(reference.map!=map)throw new InvalidOperationException("Reference map mismatch");}
            recorders=Names.Select((name,n)=>ProfilerRecorder.StartNew(Categories[n],name)).ToArray();
            yield return Measure("ordinary1",1,true,15);yield return Measure("frozen1",1,false,8);yield return Measure("frozen4",4,false,8);
            report.status=report.samples.All(s=>s.status=="DIAGNOSTIC_NOT_ACCEPTANCE")?"COMPLETE_DIAGNOSTIC_NOT_ACCEPTANCE":"FAIL";Save();Debug.Log("RENDERING_BASELINE_"+report.status);Application.Quit(report.status=="FAIL"?1:0);
        }
        CameraInfo[] Cameras()=>ground.GetComponentsInChildren<Camera>().Where(c=>c.enabled).OrderBy(c=>c.name).Select(c=>new CameraInfo{name=c.name,position=c.transform.position,euler=c.transform.eulerAngles,viewport=c.rect,fov=c.fieldOfView,near=c.nearClipPlane,far=c.farClipPlane,postProcessing=c.GetUniversalAdditionalCameraData().renderPostProcessing,antialiasing=(int)c.GetUniversalAdditionalCameraData().antialiasing}).ToArray();
        string Visuals()
        {
            string Hierarchy(Transform t)=>t==ground.transform?t.name:Hierarchy(t.parent)+"/"+t.name;
            var particles=ground.GetComponentsInChildren<ParticleSystem>(true);
            return JsonUtility.ToJson(new VisualState{motors=Enumerable.Range(0,ground.Session.ParticipantCount).Select(p=>{var m=ground.CameraReviewMotor(p);return new MotorInfo{name=m.name,position=m.transform.position,euler=m.transform.eulerAngles,state=m.State};}).ToArray(),renderers=ground.GetComponentsInChildren<Renderer>(true).Select(r=>new RenderInfo{name=r.name,path=Hierarchy(r.transform),position=r.transform.position,euler=r.transform.eulerAngles,scale=r.transform.lossyScale,boundsCenter=r.bounds.center,boundsSize=r.bounds.size,enabled=r.enabled&&r.gameObject.activeInHierarchy}).ToArray(),particleSystems=particles.Length,liveParticles=particles.Sum(p=>p.particleCount),lights=ground.GetComponentsInChildren<Light>(true).Count(l=>l.enabled)});
        }
        bool MotorsMatch()=>Enumerable.Range(0,ground.Session.ParticipantCount).All(p=>ground.CameraReviewMotor(p).transform.position==ground.Session.Pose(p).Position);
        IEnumerator Measure(string name,int views,bool live,int duration)
        {
            UnityEngine.Random.InitState((int)Seed);ground.FullHealReviewManualTick=true;ground.SendMessage("OpenSetup");ground.SelectAuthoredMap(map);
            var composition=Composition(map,views);if(!AuthoredArenaCatalog.Supports(map,composition.ParticipantCount))throw new InvalidOperationException("Unsupported review participants");
            ground.ConfigureBotTacticsReview(Configuration(ground.MatchProfile));ground.StartBotReview(composition,Seed);
            if(!ground.Running)throw new InvalidOperationException("Review start failed: "+ground.CombatReviewDiagnostic());
            if(ground.CameraReviewArena.Definition.MapId!=map||ground.Session.ArenaIdentity!=ground.CameraReviewArena.Definition.Identity)throw new InvalidOperationException("Actual map mismatch");
            string expected=null;Sample referenceSample=null;
            if(!live){referenceSample=reference?.samples.Single(s=>s.name==name);expected=referenceSample?.stateStart??JsonUtility.ToJson(ground.Session.Capture());RestoreReference(ground.Session,expected);}
            ground.FullHealReviewManualTick=!live;QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
            if(background){report.ownBackgroundWindows=RenderingBaselineMacBackground.Apply();report.nativeBackgroundEvents.Add(RenderingBaselineMacBackground.LastEvidence);Save();}
            double warm=Time.realtimeSinceStartupAsDouble+5;while(Time.realtimeSinceStartupAsDouble<warm){FrameTimingManager.CaptureFrameTimings();yield return null;}
            var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if(background&&Application.isFocused)throw new InvalidOperationException("QA Player did not enter background focus condition");
            var sample=new Sample{presentationStart=ReadClock(),actualMap=ground.CameraReviewArena.Definition.Identity,referenceState=expected,visualStart=Visuals(),composition=ground.Composition.Read(),shadowDistance=QualitySettings.shadowDistance,shadowCascades=QualitySettings.shadowCascades,pipelineSettings=pipeline?JsonUtility.ToJson(pipeline):"none",name=name,views=views,width=Screen.width,height=Screen.height,quality=QualitySettings.GetQualityLevel(),qualityName=QualitySettings.names[QualitySettings.GetQualityLevel()],aa=QualitySettings.antiAliasing,shadows=QualitySettings.shadows.ToString(),vsync=QualitySettings.vSyncCount,frameCap=Application.targetFrameRate,renderScale=pipeline?pipeline.renderScale:1,stateStart=JsonUtility.ToJson(ground.Session.Capture()),simulationStart=ground.Session.Time,camerasStart=Cameras(),counterNames=Names,counterAvailable=recorders.Select(r=>r.Valid).ToArray()};
            if(!live&&(sample.stateStart!=expected||!MotorsMatch()))throw new InvalidOperationException("Restored state/physics transform mismatch");
            if(referenceSample!=null&&JsonUtility.ToJson(new CameraSet{items=sample.camerasStart})!=JsonUtility.ToJson(new CameraSet{items=referenceSample.camerasStart}))throw new InvalidOperationException("Restored camera mismatch");
            var frame=new List<double>();var cpu=new List<double>();var gpu=new List<double>();var counters=Names.Select(_=>new List<double>()).ToArray();
            var csv=new StringBuilder("frame_ms,cpu_ms,gpu_ms,draw_calls,setpass,triangles,vertices,batches,main_ns,render_ns,gc_bytes,focused,running,timestamp\n");var cpuRows=new List<double>();var gpuRows=new List<double>();var focusedRows=new List<bool>();var runningRows=new List<bool>();var timestamps=new List<ulong>();ulong timestamp=0;
            sample.cpuProfile=RenderingBaselineCpuCapture.Output(cpuProfileDirectory,name);
            using(var capture=RenderingBaselineCpuCapture.Begin(sample.cpuProfile))
            {
                double until=Time.realtimeSinceStartupAsDouble+duration,previous=Time.realtimeSinceStartupAsDouble;
                while(Time.realtimeSinceStartupAsDouble<until)
                {
                    FrameTimingManager.CaptureFrameTimings();yield return null;double now=Time.realtimeSinceStartupAsDouble,ms=(now-previous)*1000;previous=now;frame.Add(ms);
                    bool fresh=FrameTimingManager.GetLatestTimings(1,timings)>0&&timings[0].frameStartTimestamp!=timestamp;var t=timings[0];if(fresh){timestamp=t.frameStartTimestamp;if(t.cpuFrameTime>0)cpu.Add(t.cpuFrameTime);if(t.gpuFrameTime>0)gpu.Add(t.gpuFrameTime);}
                    cpuRows.Add(fresh&&t.cpuFrameTime>0?t.cpuFrameTime:double.NaN);gpuRows.Add(fresh&&t.gpuFrameTime>0?t.gpuFrameTime:double.NaN);
                    for(int n=0;n<recorders.Length;n++)if(recorders[n].Valid)counters[n].Add(recorders[n].LastValue);
                    if(!Application.isFocused)sample.unfocusedFrames++;if(!ground.Running)sample.nonRunningFrames++;
                    focusedRows.Add(Application.isFocused);runningRows.Add(ground.Running);timestamps.Add(fresh?timestamp:0);
                }
                sample.presentationEnd=ReadClock();
            }
            sample.frameMs=new Distribution(frame);sample.cpuMs=new Distribution(cpu);sample.gpuMs=new Distribution(gpu);sample.cpuStatus=cpu.Count>0?"AVAILABLE":"NotRun";sample.gpuStatus=gpu.Count>0?"AVAILABLE":"NotRun";sample.counters=counters.Select(v=>new Distribution(v)).ToArray();sample.simulationEnd=ground.Session.Time;sample.stateEnd=JsonUtility.ToJson(ground.Session.Capture());sample.camerasEnd=Cameras();sample.visualEnd=Visuals();
            sample.allocated=Profiler.GetTotalAllocatedMemoryLong();sample.reserved=Profiler.GetTotalReservedMemoryLong();sample.mono=Profiler.GetMonoUsedSizeLong();sample.workingSet=System.Diagnostics.Process.GetCurrentProcess().WorkingSet64;
            sample.assetScope=ReadAssetScope(ground.CameraReviewArena.transform);
            var meshes=Resources.FindObjectsOfTypeAll<Mesh>();sample.meshCount=meshes.Length;sample.meshBytes=meshes.Sum(m=>Profiler.GetRuntimeMemorySizeLong(m));sample.materialCount=Resources.FindObjectsOfTypeAll<Material>().Length;
            foreach(var r in ground.CameraReviewArena.GetComponentsInChildren<MeshRenderer>()){var m=r.GetComponent<MeshFilter>()?.sharedMesh;if(!m)continue;sample.meshes.Add(new MeshInfo{renderer=r.name,mesh=m.name,material=string.Join(";",r.sharedMaterials.Select(x=>x?x.name:"null")),vertices=m.vertexCount,indices=Enumerable.Range(0,m.subMeshCount).Sum(i=>(long)m.GetIndexCount(i)),bytes=Profiler.GetRuntimeMemorySizeLong(m),readable=m.isReadable,bounds=r.bounds.size});}
            if(frame.Count==0||(background&&sample.unfocusedFrames!=frame.Count)||sample.nonRunningFrames>0||sample.camerasStart.Length!=views||sample.width!=1920||sample.height!=1080||(!live&&(sample.simulationEnd!=sample.simulationStart||sample.stateEnd!=expected||!MotorsMatch()))||(live&&sample.simulationEnd<=sample.simulationStart))sample.status="FAIL_CONTRACT";
            // Serialize only after the timed section so CSV string allocations do not distort frames.
            string Number(double v)=>double.IsNaN(v)?"":v.ToString("R",CultureInfo.InvariantCulture);
            for(int i=0;i<frame.Count;i++)
            {var row=new List<string>{Number(frame[i]),Number(cpuRows[i]),Number(gpuRows[i])};for(int n=0;n<counters.Length;n++)row.Add(counters[n].Count==frame.Count?Number(counters[n][i]):"");row.Add(focusedRows[i]?"1":"0");row.Add(runningRows[i]?"1":"0");row.Add(timestamps[i]==0?"":timestamps[i].ToString());csv.AppendLine(string.Join(",",row));}
            report.samples.Add(sample);File.WriteAllText(Path.Combine(directory,name+".csv"),csv.ToString());Save();yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"));yield return new WaitForSecondsRealtime(.5f);
        }
        void Save()=>File.WriteAllText(Path.Combine(directory,"report.json"),JsonUtility.ToJson(report,true));
        void Timeout(){if(report!=null){report.status="INCOMPLETE_TIMEOUT";Save();}Application.Quit(2);}
        void OnDestroy(){if(recorders!=null)foreach(var recorder in recorders)recorder.Dispose();}
    }
}
#endif
