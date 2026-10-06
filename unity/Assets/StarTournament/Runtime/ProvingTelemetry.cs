using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    // Bounded diagnostic protocol. Durations control measurement only, not gameplay or balance.
    public sealed class ProvingTelemetry : MonoBehaviour
    {
        [Serializable] public sealed class Report
        {
            public string status="DIAGNOSTIC_NOT_ACCEPTANCE", unity, profile, profileSha256, arenaProfile, arenaFamily, arenaIdentity, device, graphics, operatingSystem;
            public string workload="four stationary participants, no physical input", state;
            public string focusPolicy="foreground warmup required; focus recorded throughout measurement";
            public bool focusedForEntireMeasurement=true;
            public int width,height,cameras,samples;
            public float warmupSeconds=3, measurementSeconds=10, frameP50Ms,frameP95Ms,frameP99Ms,worstFrameMs,cpuP50Ms,cpuP95Ms,cpuP99Ms,cpuWorstMs,gpuP50Ms,gpuP95Ms,gpuP99Ms,gpuWorstMs;
            public bool cpuTimingAvailable,gpuTimingAvailable;
            public long allocatedMemoryBytes;
            public string presentationProfile;
            public int skinInstances,uniqueSkinnedMeshes,uniqueMaterials,uniqueTextures;
            public long textureRuntimeBytes;
        }
        readonly List<float> frames=new List<float>(),cpu=new List<float>(),gpu=new List<float>();
        readonly FrameTiming[] timings=new FrameTiming[1];
        string directory;
        float elapsed;
        bool finished;
        bool measuring;
        bool focusedThroughout=true;
        void Start()
        {
            var args=Environment.GetCommandLineArgs();
            int flag=Array.IndexOf(args,"-probeEvidence");
            if(flag<0 || flag+1>=args.Length) { enabled=false;return; }
            directory=Path.GetFullPath(args[flag+1]);Directory.CreateDirectory(directory);
            Application.runInBackground=true;
        }
        void Update()
        {
            if(finished)return;
            FrameTimingManager.CaptureFrameTimings();
            // Window activation must precede warmup; automation launch latency is not foreground evidence.
            if(!measuring && !Application.isFocused) { elapsed=0; return; }
            elapsed+=Time.unscaledDeltaTime;
            if(!measuring)
            {
                // The frame crossing the warmup boundary belongs wholly to warmup.
                // Do not count startup/window activation latency as a measured frame.
                if(elapsed>=3) { measuring=true;elapsed=0; }
                return;
            }
            focusedThroughout &= Application.isFocused;
            frames.Add(Time.unscaledDeltaTime*1000);
            if(FrameTimingManager.GetLatestTimings(1,timings)>0)
            {
                if(timings[0].cpuFrameTime>0)cpu.Add((float)timings[0].cpuFrameTime);
                if(timings[0].gpuFrameTime>0)gpu.Add((float)timings[0].gpuFrameTime);
            }
            if(elapsed<10)return;
            finished=true;
            var ground=GetComponent<ProvingGround>();
            var builtArena=ground.GetComponentInChildren<ProvingArena>();
            var report=new Report {unity=Application.unityVersion,profile=ground.Profile.Id+"@"+ground.Profile.Version,arenaProfile=ground.CombatBowlAuthoring.Id+"@"+ground.CombatBowlAuthoring.Version,
                profileSha256=Hash(JsonUtility.ToJson(ground.Profile)),arenaFamily=builtArena?.Definition?.MapId,arenaIdentity=builtArena?.Definition?.Identity,
                device=SystemInfo.deviceModel,graphics=SystemInfo.graphicsDeviceName,operatingSystem=SystemInfo.operatingSystem,
                width=Screen.width,height=Screen.height,cameras=Camera.allCamerasCount,samples=frames.Count,
                state=ground.Running?"running-diagnostic":"setup-or-pause",frameP50Ms=Percentile(frames,.5f),frameP95Ms=Percentile(frames,.95f),
                frameP99Ms=Percentile(frames,.99f),worstFrameMs=Percentile(frames,1),cpuP50Ms=Percentile(cpu,.5f),cpuP95Ms=Percentile(cpu,.95f),cpuP99Ms=Percentile(cpu,.99f),cpuWorstMs=Percentile(cpu,1),
                gpuP50Ms=Percentile(gpu,.5f),gpuP95Ms=Percentile(gpu,.95f),gpuP99Ms=Percentile(gpu,.99f),gpuWorstMs=Percentile(gpu,1),
                cpuTimingAvailable=cpu.Count>0,gpuTimingAvailable=gpu.Count>0,allocatedMemoryBytes=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()};
            report.focusedForEntireMeasurement=focusedThroughout;
            report.presentationProfile=ground.TrooperProfile.Id+"@"+ground.TrooperProfile.Version;
            report.workload="four stationary animated troopers, session-clock idle and first-person aim, no physical input";
            var skins=ground.GetComponentsInChildren<SkinnedMeshRenderer>();
            var materials=ground.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Where(m=>m).Distinct().ToArray();
            var textures=materials.SelectMany(m=>m.GetTexturePropertyNames().Select(n=>m.GetTexture(n))).Where(t=>t).Distinct().ToArray();
            report.skinInstances=skins.Length;report.uniqueSkinnedMeshes=skins.Select(s=>s.sharedMesh).Distinct().Count();
            report.uniqueMaterials=materials.Length;report.uniqueTextures=textures.Length;
            report.textureRuntimeBytes=textures.Sum(t=>UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t));
            File.WriteAllText(Path.Combine(directory,"report.json"),JsonUtility.ToJson(report,true));
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"player.png"));
            Invoke(nameof(Quit),1); // Allow the asynchronous screenshot to finish before exiting the diagnostic Player.
        }
        static float Percentile(List<float> values,float percentile)
        {
            if(values.Count==0)return 0;values.Sort();return values[Mathf.Clamp(Mathf.CeilToInt(values.Count*percentile)-1,0,values.Count-1)];
        }
        static string Hash(string data)
        {
            using(var sha=System.Security.Cryptography.SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(data))).Replace("-","").ToLowerInvariant();
        }
        void Quit() { Application.Quit(); }
    }
}
