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
    // Opt-in isolated Player diagnostic; never changes persisted graphics or product models.
    [DefaultExecutionOrder(10000)]
    public sealed class NativeModelPerformanceReview:MonoBehaviour
    {
        [Serializable] sealed class Distribution
        {
            public int count,over16,over33,over50;public double mean,p50,p95,p99,max;
            public Distribution(IEnumerable<double> values){var a=values.OrderBy(v=>v).ToArray();count=a.Length;if(count==0)return;mean=a.Average();p50=a[count/2];p95=a[Math.Min(count-1,(int)(count*.95))];p99=a[Math.Min(count-1,(int)(count*.99))];max=a[count-1];over16=a.Count(v=>v>16.667);over33=a.Count(v=>v>33.333);over50=a.Count(v=>v>50);}
        }
        [Serializable] sealed class Sample
        {
            public string weapon,variant;public int views,width,height,unfocused,nonrunning;public long allocatedBytes,reservedBytes,monoBytes;public double simulationSeconds;
            public Distribution frame,cpu,gpu,main,render,present,draws,triangles,setpass,contactFind,contactSnapshot,contactCreate;
        }
        [Serializable] sealed class MeshRow{public string path,kind,shadowMode;public int triangles,vertices,materials;public bool active;}
        [Serializable] sealed class Report
        {
            public string status="RUNNING",body,unity,cpu,gpu,os,pipeline,protocol="5s warmup+8s timed frozen placement; current weapon/material/animation code for both bodies; actual Player, no capture during sample. Renderer forceRenderingOff isolates drawing cost only. Both assets packaged; memory is not standalone asset-loading delta.";
            public int views,width,height,quality,systemMemoryMB;public bool frameTiming,muted,contactRecordersValid;public float renderScale;public List<Sample> samples=new List<Sample>();
        }
        ProvingGround ground;string directory,variant="full";int views;bool baseline;Report report;GameObject currentBody;
        Renderer[] costume=Array.Empty<Renderer>(),weapons=Array.Empty<Renderer>();readonly FrameTiming[] timings=new FrameTiming[1];
        ProfilerRecorder draws,triangles,passes,contactFind,contactSnapshot,contactCreate;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Bootstrap()
        {if(Environment.GetCommandLineArgs().Contains("-modelPerformance"))new GameObject("Model performance diagnostic").AddComponent<NativeModelPerformanceReview>();}
        static string Arg(string key,string fallback=null){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,key);return i>=0&&i+1<a.Length?a[i+1]:fallback;}
        IEnumerator Start()
        {
            directory=Arg("-modelPerformance");if(!Path.IsPathFullyQualified(directory??""))throw new InvalidOperationException("Owned absolute evidence path required");Directory.CreateDirectory(directory);
            Invoke(nameof(Timeout),900);Application.runInBackground=true;baseline=Arg("-modelBody","current")=="baseline";views=int.Parse(Arg("-modelViews","1"));if(views!=1&&views!=4)throw new InvalidOperationException("Expected 1 or 4 views");
            while(!(ground=FindFirstObjectByType<ProvingGround>())||!ground.IsReady||!SplashScreen.isFinished)yield return null;
            Screen.SetResolution(1920,1080,FullScreenMode.Windowed);QualitySettings.SetQualityLevel(NativeVisualPreferences.QualityTier(NativeVisualPreferences.DefaultQuality),true);QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
            currentBody=ground.TrooperBodyPrefab;if(baseline){ground.TrooperBodyPrefab=Resources.Load<GameObject>("ModelPerformanceBaseline");if(!ground.TrooperBodyPrefab)throw new InvalidOperationException("Missing baseline body");}
            ground.SendMessage("OpenSetup");ground.SelectAuthoredMap(LunarLaboratoryCatalog.Id);Begin();yield return null;yield return new WaitForSecondsRealtime(1);
            var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            report=new Report{body=Arg("-modelLabel",baseline?"F23-original":"F23-optimized"),views=views,width=Screen.width,height=Screen.height,quality=QualitySettings.GetQualityLevel(),unity=Application.unityVersion,cpu=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName,os=SystemInfo.operatingSystem,systemMemoryMB=SystemInfo.systemMemorySize,pipeline=pipeline?pipeline.name:"none",renderScale=pipeline?pipeline.renderScale:1,frameTiming=FrameTimingManager.IsFeatureEnabled(),muted=AudioListener.volume==0};
            if(Screen.width!=1920||Screen.height!=1080||!report.muted)throw new InvalidOperationException("Resolution/silent admission failed");
            draws=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count");triangles=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Triangles Count");passes=ProfilerRecorder.StartNew(ProfilerCategory.Render,"SetPass Calls Count");
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(SkinnedContactPatch).TypeHandle);
            contactFind=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"F23.Contact.Find");
            contactSnapshot=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"F23.Contact.Snapshot");
            contactCreate=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"F23.Contact.Create");
            report.contactRecordersValid=contactFind.Valid&&contactSnapshot.Valid&&contactCreate.Valid;
            foreach(WeaponId weapon in new[]{WeaponId.Rifle,WeaponId.Shotgun,WeaponId.RocketLauncher,WeaponId.Cutter,WeaponId.Plasma})
            {
                Equip(weapon);yield return null;yield return null;Inventory(weapon);
                yield return Measure(weapon,"full",8);
                if(views==1){yield return Capture(weapon);yield return Measure(weapon,"weapons-hidden",8);}
                if(weapon==WeaponId.Rifle){yield return Measure(weapon,"world-costume-hidden",8);if(views==4)yield return Measure(weapon,"weapons-hidden",8);}
            }
            Equip(WeaponId.Rifle);yield return Measure(WeaponId.Rifle,"full-repeat",8);
            // Independent ordinary-runtime witness, not a pose-matched A/B: bots may move/die.
            variant="full";Begin();yield return null;Equip(WeaponId.Rifle);ground.FullHealReviewManualTick=false;
            yield return Measure(WeaponId.Rifle,"live-bots",20);
            yield return CaptureWorld();
            report.status="COMPLETE_DIAGNOSTIC_NOT_ACCEPTANCE";Save();Debug.Log("MODEL_PERFORMANCE_COMPLETE "+directory);Application.Quit(0);
        }
        void Begin()
        {
            ground.StartBotReview(new NativeMatchComposition(NativeMatchRoster.Ffa(8),Enumerable.Range(0,8).Select(n=>new NativeParticipantInfo(n<views?NativeParticipantKind.LocalHuman:NativeParticipantKind.Bot,n<views?"Player "+n:"Bot "+n,NativeParticipantColors.For(n),n<views?-1:(int)NativeBotDifficulty.Hard)).ToArray(),Enumerable.Range(0,views).ToArray()),20261029);
            ground.FullHealReviewManualTick=true;
            if(baseline)foreach(var proxy in ground.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s=>s.name=="own-shadow-proxy"))
                proxy.transform.parent.GetComponent<SkinnedMeshRenderer>().shadowCastingMode=ShadowCastingMode.On;
            for(int i=0;i<8;i++)ground.PlaceCombatReviewSeat(i,i<views?new Vector3((i-(views-1)*.5f)*1.5f,4,-6):new Vector3(((i-views)%4-1.5f)*1.5f,4,-1+(i-views)/4*3),0,i<views?0:180);
        }
        void Equip(WeaponId weapon)
        {
            var snapshot=ground.Session.Capture();
            for(int i=0;i<snapshot.Lives.Length;i++){var l=snapshot.Lives[i];l.ShotgunOwned=l.RocketOwned=l.CutterOwned=l.PlasmaOwned=true;l.PlasmaAmmo=100;l.SelectedWeapon=weapon;l.PendingWeapon=default;l.SwitchRemaining=0;l.Ammo=CombatLife.AmmoFor(l,weapon);snapshot.Lives[i]=l;}
            ground.Session.Restore(snapshot);
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
                rows.Add(new MeshRow{shadowMode=r.shadowCastingMode.ToString(),path=PathOf(r.transform),kind=IsWeapon(r)?"weapon":r.GetComponentInParent<Camera>()?"fps-costume":"world-costume",triangles=(int)Enumerable.Range(0,mesh.subMeshCount).Sum(i=>(long)mesh.GetIndexCount(i)/3),vertices=mesh.vertexCount,materials=r.sharedMaterials.Length,active=r.enabled&&r.gameObject.activeInHierarchy});
            }
            File.WriteAllText(Path.Combine(directory,weapon+"-inventory.json"),JsonUtility.ToJson(new MeshRows{items=rows},true));
        }
        [Serializable] sealed class MeshRows{public List<MeshRow> items;}
        static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
        IEnumerator Measure(WeaponId weapon,string mode,float seconds)
        {
            variant=mode;RefreshRenderers();double warm=Time.realtimeSinceStartupAsDouble+5;while(Time.realtimeSinceStartupAsDouble<warm){FrameTimingManager.CaptureFrameTimings();yield return null;}
            var frame=new List<double>();var cpu=new List<double>();var gpu=new List<double>();var main=new List<double>();var render=new List<double>();var present=new List<double>();var dc=new List<double>();var tr=new List<double>();var sp=new List<double>();
            var s=new Sample{weapon=weapon.ToString(),variant=mode,views=views,width=Screen.width,height=Screen.height};double simulation=ground.Session.Time,previous=Time.realtimeSinceStartupAsDouble,until=previous+seconds;ulong stamp=0;
            var cf=new List<double>();var cs=new List<double>();var cc=new List<double>();
            var csv=new StringBuilder("frame_ms,cpu_ms,gpu_ms,draw_calls,triangles,setpass,focused,contact_find_ms,contact_snapshot_ms,contact_create_ms\n");
            while(Time.realtimeSinceStartupAsDouble<until)
            {
                FrameTimingManager.CaptureFrameTimings();yield return null;double now=Time.realtimeSinceStartupAsDouble;frame.Add((now-previous)*1000);previous=now;
                if(!Application.isFocused)s.unfocused++;if(!ground.Running)s.nonrunning++;
                bool fresh=FrameTimingManager.GetLatestTimings(1,timings)>0&&timings[0].frameStartTimestamp!=stamp;var t=timings[0];
                if(fresh){stamp=t.frameStartTimestamp;if(t.cpuFrameTime>0)cpu.Add(t.cpuFrameTime);if(t.gpuFrameTime>0)gpu.Add(t.gpuFrameTime);if(t.cpuMainThreadFrameTime>0)main.Add(t.cpuMainThreadFrameTime);if(t.cpuRenderThreadFrameTime>0)render.Add(t.cpuRenderThreadFrameTime);present.Add(t.cpuMainThreadPresentWaitTime);}
                if(draws.Valid)dc.Add(draws.LastValue);if(triangles.Valid)tr.Add(triangles.LastValue);if(passes.Valid)sp.Add(passes.LastValue);
                double findMs=contactFind.Valid?contactFind.LastValue/1e6:0,snapshotMs=contactSnapshot.Valid?contactSnapshot.LastValue/1e6:0,createMs=contactCreate.Valid?contactCreate.LastValue/1e6:0;
                cf.Add(findMs);cs.Add(snapshotMs);cc.Add(createMs);
                csv.AppendLine(string.Join(",",new double[]{frame[frame.Count-1],fresh?t.cpuFrameTime:double.NaN,fresh?t.gpuFrameTime:double.NaN,draws.Valid?draws.LastValue:-1,triangles.Valid?triangles.LastValue:-1,passes.Valid?passes.LastValue:-1,Application.isFocused?1:0,findMs,snapshotMs,createMs}.Select(v=>v.ToString("R",CultureInfo.InvariantCulture))));
            }
            s.frame=new Distribution(frame);s.cpu=new Distribution(cpu);s.gpu=new Distribution(gpu);s.main=new Distribution(main);s.render=new Distribution(render);s.present=new Distribution(present);s.draws=new Distribution(dc);s.triangles=new Distribution(tr);s.setpass=new Distribution(sp);s.allocatedBytes=Profiler.GetTotalAllocatedMemoryLong();s.reservedBytes=Profiler.GetTotalReservedMemoryLong();s.monoBytes=Profiler.GetMonoUsedSizeLong();s.simulationSeconds=ground.Session.Time-simulation;
            s.contactFind=new Distribution(cf);s.contactSnapshot=new Distribution(cs);s.contactCreate=new Distribution(cc);
            report.samples.Add(s);File.WriteAllText(Path.Combine(directory,weapon+"-"+mode+".csv"),csv.ToString());Save();Debug.Log("MODEL_SAMPLE "+report.body+" "+views+" "+weapon+" "+mode+" p50="+s.frame.p50+" p95="+s.frame.p95+" GPU="+s.gpu.p50);
        }
        IEnumerator Capture(WeaponId weapon){variant="full";yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,weapon+"-fps.png"));yield return new WaitForSecondsRealtime(.5f);}
        // Actual native imported model and animation, captured after all timed sections.
        IEnumerator CaptureWorld()=>CaptureWorld(ground,ground.TrooperBodyPrefab,directory,!baseline,true);
        public static IEnumerator CaptureWorld(ProvingGround ground,GameObject prefab,string directory,bool immutableTopology,bool contactCpu)
        {
            Directory.CreateDirectory(directory);
            var root=Instantiate(prefab,new Vector3(1000,1000,1000),Quaternion.identity);
            var origin=root.transform.position;
            TrooperIdentityPresentation.ApplyBody(root,NativeStandingsView.Palette[0],ground.ParticipantPaletteProfile);
            foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
            var visual=root.AddComponent<TrooperVisual>();visual.EquippedWeapon=WeaponId.Shotgun;
            visual.Initialize(ground.TrooperClips,ProvingProfile.CreateTrooperDefault(),false);
            var cameraObject=new GameObject("F23 evidence camera");var camera=cameraObject.AddComponent<Camera>();
            camera.enabled=false;camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.085f,.095f);
            camera.orthographic=true;camera.orthographicSize=1.04f;camera.nearClipPlane=.01f;camera.farClipPlane=20;
            var target=new RenderTexture(2000,2400,24);target.Create();camera.targetTexture=target;
            var pixels=new Texture2D(2000,2400,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            var lightObject=new GameObject("F23 evidence key");var key=lightObject.AddComponent<Light>();key.type=LightType.Directional;key.intensity=2;key.cullingMask=1<<30;key.transform.rotation=Quaternion.Euler(35,150,0);
            var oldSun=RenderSettings.sun;var oldAmbientMode=RenderSettings.ambientMode;var oldAmbient=RenderSettings.ambientLight;float oldAmbientIntensity=RenderSettings.ambientIntensity;
            RenderSettings.sun=key;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.35f,.35f,.35f);RenderSettings.ambientIntensity=1;
            try
            {
                foreach(var state in new[]{"idle","run"})
                {
                    float speed=state=="run"?ProvingProfile.CreateTrooperDefault().Get("animation.runThreshold")+1:0;
                    for(int i=0;i<=35;i++){root.transform.position=origin+Vector3.forward*speed*i*.01f;visual.Render(i*.01,new Vector3(0,0,speed),100,1);}
                    root.transform.position=origin;
                    if(visual.State!=state)throw new InvalidOperationException("Unexpected world evidence state "+visual.State+" instead of "+state);
                    if(state=="idle"&&contactCpu)CaptureContactWork(root,origin,Path.Combine(directory,"contact-work.json"),immutableTopology);
                    foreach(var view in new[]{("front",new Vector3(2,1.2f,4)),("side",new Vector3(4,.3f,0)),("back",new Vector3(-2,1,-4))})
                    {
                        var center=origin+Vector3.up*.9f;camera.transform.position=center+view.Item2;camera.transform.LookAt(center);
                        yield return null;if(GraphicsSettings.currentRenderPipeline!=null)RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});else camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,2000,2400),0,0);pixels.Apply();
                        File.WriteAllBytes(Path.Combine(directory,"world-"+state+"-"+view.Item1+".png"),pixels.EncodeToPNG());
                    }
                }
            }
            finally{RenderSettings.sun=oldSun;RenderSettings.ambientMode=oldAmbientMode;RenderSettings.ambientLight=oldAmbient;RenderSettings.ambientIntensity=oldAmbientIntensity;RenderTexture.active=previous;target.Release();Destroy(target);Destroy(pixels);visual.Release();Destroy(root);Destroy(cameraObject);Destroy(lightObject);}
        }
        [Serializable] sealed class ContactWork { public float x,y;public bool hit;public Vector3 point,normal;public double findMs,createMs;public int patchTriangles; }
        [Serializable] sealed class ContactWorkReport { public string protocol="24 synchronous independent shots at fixed F23 idle pose; Stopwatch CPU, no renderer/GPU attribution; Find includes snapshot; Create excludes destruction.";public bool immutableTopology;public ContactWork[] samples; }
        internal static void CaptureContactWork(GameObject body,Vector3 origin,string path,bool immutableTopology)
        {
            var samples=new List<ContactWork>();
            var material=body.GetComponentsInChildren<SkinnedMeshRenderer>().First().sharedMaterial;
            for(int repeat=0;repeat<2;repeat++)foreach(float y in new[]{.62f,.95f,1.18f,1.36f})foreach(float x in new[]{-.12f,0,.12f})
            {
                var row=new ContactWork{x=x,y=y};var watch=System.Diagnostics.Stopwatch.StartNew();
                var contact=new SkinnedContactPatch.Projection(body,immutableTopology).Find(origin+new Vector3(x,y,.2f),Vector3.back);
                watch.Stop();row.findMs=watch.Elapsed.TotalMilliseconds;row.hit=contact!=null;if(contact!=null){row.point=contact.Point;row.normal=contact.Normal;}
                if(contact!=null)
                {
                    watch.Restart();var patch=SkinnedContactPatch.Create(contact,.13f,material,"hit-contact-diagnostic",out var mesh);watch.Stop();
                    row.createMs=watch.Elapsed.TotalMilliseconds;row.patchTriangles=mesh?mesh.triangles.Length/3:0;
                    if(patch)DestroyImmediate(patch);if(mesh)DestroyImmediate(mesh);
                }
                samples.Add(row);
            }
            File.WriteAllText(path,JsonUtility.ToJson(new ContactWorkReport{immutableTopology=immutableTopology,samples=samples.ToArray()},true));
        }
        void Save()=>File.WriteAllText(Path.Combine(directory,"performance.json"),JsonUtility.ToJson(report,true));
        void Timeout(){if(report!=null){report.status="INCOMPLETE_TIMEOUT";Save();}Application.Quit(2);}
        void OnDestroy(){draws.Dispose();triangles.Dispose();passes.Dispose();contactFind.Dispose();contactSnapshot.Dispose();contactCreate.Dispose();foreach(var r in weapons)if(r)r.forceRenderingOff=false;foreach(var r in costume)if(r)r.forceRenderingOff=false;if(ground&&currentBody)ground.TrooperBodyPrefab=currentBody;}
    }
}
#endif
