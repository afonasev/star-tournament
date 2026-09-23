using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace StarTournament.ProvingGround
{
    /// <summary>Explicit development capture. Diagnostic framing constants never enter gameplay state.</summary>
    public sealed class NativeCombatBowlReview : MonoBehaviour
    {
        ProvingGround ground; string directory;Gamepad[] pads;float reviewTimeScale=1;
        IEnumerator Start()
        {
            ground=GetComponent<ProvingGround>();directory=ground.CombatBowlReviewDirectory;Directory.CreateDirectory(directory);AudioListener.volume=0;Application.runInBackground=true;
            while(!Application.isFocused||!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
            yield return Capture("01-setup");
            pads=Enumerable.Range(0,4).Select(_=>InputSystem.AddDevice<Gamepad>()).ToArray();var roster=NativeMatchRoster.Ffa(8);
            var metadata=Enumerable.Range(0,8).Select(i=>new NativeParticipantInfo(i<4?NativeParticipantKind.LocalHuman:NativeParticipantKind.Bot,i<4?"Игрок "+(i+1):"Бот "+(i-3),NativeParticipantColors.For(i),i<4?-1:1)).ToArray();
            ground.StartBotReview(new NativeMatchComposition(roster,metadata,new[]{0,1,2,3}),20260923,pads);
            if(!ground.Running)throw new InvalidOperationException("R7 review could not start match");
            yield return Capture("02-eight-participants-four-viewports");
            ground.PlaceCombatReviewSeat(0,new Vector3(-4.5f,4,0),0,90);
            ground.PlaceCombatReviewSeat(1,new Vector3(0,-1.2f,-5),0,0);
            ground.PlaceCombatReviewSeat(2,new Vector3(0,0,13),10,180);
            ground.PlaceCombatReviewSeat(3,new Vector3(-32,4,-26),15,90);
            yield return new WaitForSeconds(16);if(!ground.Running)throw new InvalidOperationException("Review paused before gameplay capture");yield return Capture("03-hall-basement-entrances");
            var playerCameras=ground.GetComponentsInChildren<Camera>();
            var oldRects=playerCameras.Select(c=>c.rect).ToArray();
            for(int i=1;i<playerCameras.Length;i++)playerCameras[i].enabled=false;
            playerCameras[0].rect=new Rect(0,0,1,1);yield return Capture("03a-single-viewport");
            for(int i=0;i<playerCameras.Length;i++){playerCameras[i].rect=oldRects[i];playerCameras[i].enabled=true;}
            var samples=new System.Collections.Generic.List<float>();
            for(int frame=0;frame<360;frame++){yield return null;if(!ground.Running)throw new InvalidOperationException("Review paused during timing");samples.Add(Time.unscaledDeltaTime*1000);}
            samples.Sort();
            File.WriteAllText(Path.Combine(directory,"performance.json"),JsonUtility.ToJson(new Performance{samples=samples.Count,medianMs=samples[samples.Count/2],p95Ms=samples[(int)(samples.Count*.95f)],allocatedMemoryBytes=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong(),width=Screen.width,height=Screen.height,localLights=6,note="Diagnostic on current Mac; four native cameras, scripted stationary views, vsync/frame cap included. Not reference hardware acceptance."},true));
            // Full-screen architectural views are real Player renders; no wall or roof is hidden.
            var reviewCameras=ground.GetComponentsInChildren<Camera>();var reviewCanvases=ground.GetComponentsInChildren<Canvas>();
            foreach(var c in reviewCameras)c.enabled=false;foreach(var c in reviewCanvases)c.enabled=false;
            var artCameraObject=new GameObject("Orbital architectural capture");var artCamera=artCameraObject.AddComponent<Camera>();artCamera.fieldOfView=72;artCamera.nearClipPlane=.05f;
            while(ground.Session.Life(1).Dead)yield return null;
            reviewTimeScale=Time.timeScale;Time.timeScale=0;
            // Park other live participants outside this paired diagnostic frame; the eight-player gameplay capture above is untouched.
            for(int seat=0;seat<ground.Session.ParticipantCount;seat++)
                if(seat!=1)ground.PlaceCombatReviewSeat(seat,new Vector3(30,4,20-seat*1.5f),0,180);
            // Corpses are independent presentation clones, so moving their participant cannot clear the diagnostic frame.
            foreach(var corpse in ground.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("corpse-",StringComparison.Ordinal)))
                corpse.gameObject.SetActive(false);
            ground.PlaceCombatReviewSeat(1,new Vector3(0,4,-3),0,180);
            yield return null;
            artCamera.transform.position=new Vector3(0,7,-7);artCamera.transform.LookAt(new Vector3(0,4.75f,-3));yield return Capture("16-live-hall-opponent");
            var shadowLights=ground.GetComponentInChildren<OrbitalLeaguePresentation>().GetComponentsInChildren<Light>();
            var renderers=ground.GetComponentsInChildren<Renderer>(true);
            File.WriteAllText(Path.Combine(directory,"lighting-diagnostic.json"),JsonUtility.ToJson(new LightingDiagnostic{
                pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline?.name,
                qualityShadows=QualitySettings.shadows.ToString(),
                casterRenderers=renderers.Count(renderer=>renderer.shadowCastingMode!=UnityEngine.Rendering.ShadowCastingMode.Off),
                receiverRenderers=renderers.Count(renderer=>renderer.receiveShadows),
                skinnedShadowPasses=renderers.OfType<SkinnedMeshRenderer>().Select(renderer=>{
                    var material=renderer.sharedMaterial;
                    return renderer.name+" | "+(material?material.shader.name:"missing")+" | "+(material?material.FindPass("ShadowCaster"):-1);
                }).Distinct().ToArray(),
                lights=shadowLights.Select(light=>new LightSnapshot{name=light.name,position=light.transform.position,range=light.range,spotAngle=light.spotAngle,intensity=light.intensity,shadows=light.shadows.ToString(),strength=light.shadowStrength}).ToArray()
            },true));
            var shadowModes=shadowLights.Select(light=>light.shadows).ToArray();
            foreach(var light in shadowLights)light.shadows=LightShadows.None;
            yield return Capture("16a-hall-shadow-disabled-diagnostic");
            for(int light=0;light<shadowLights.Length;light++)shadowLights[light].shadows=shadowModes[light];
            foreach(var light in shadowLights)light.enabled=false;
            yield return Capture("16b-hall-ceiling-lights-disabled-diagnostic");
            foreach(var light in shadowLights)light.enabled=true;
            // Isolate one real spotlight to distinguish a missing shadow pass from excess fill.
            var studio=ground.GetComponentsInChildren<Light>().First(light=>light.type==LightType.Directional);
            var oldStudio=studio.intensity;var oldAmbient=RenderSettings.ambientLight;
            var oldSpotIntensity=shadowLights[0].intensity;var oldSpotRange=shadowLights[0].range;
            studio.intensity=0;RenderSettings.ambientLight=Color.white*.05f;
            for(int light=1;light<shadowLights.Length;light++)shadowLights[light].enabled=false;
            shadowLights[0].intensity=30;shadowLights[0].range=15;
            yield return Capture("16c-isolated-spot-shadow-on-diagnostic");
            shadowLights[0].shadows=LightShadows.None;
            yield return Capture("16d-isolated-spot-shadow-off-diagnostic");
            var proof=new GameObject("Diagnostic shadow caster without collider");
            proof.transform.SetParent(ground.transform,false);proof.transform.position=new Vector3(-3.1f,5,0);proof.transform.localScale=new Vector3(1,2,1);
            proof.AddComponent<MeshFilter>().sharedMesh=ground.GetComponentInChildren<ProvingArena>().GetComponentInChildren<MeshFilter>().sharedMesh;
            var proofMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            proof.AddComponent<MeshRenderer>().sharedMaterial=proofMaterial;
            artCamera.transform.position=new Vector3(-3,6.5f,-5);artCamera.transform.LookAt(new Vector3(-3.1f,5,0));
            shadowLights[0].shadows=shadowModes[0];
            yield return Capture("16e-proof-cube-shadow-on-diagnostic");
            shadowLights[0].shadows=LightShadows.None;
            yield return Capture("16f-proof-cube-shadow-off-diagnostic");
            Destroy(proof);Destroy(proofMaterial);
            shadowLights[0].shadows=shadowModes[0];shadowLights[0].intensity=oldSpotIntensity;shadowLights[0].range=oldSpotRange;
            for(int light=1;light<shadowLights.Length;light++)shadowLights[light].enabled=true;
            RenderSettings.ambientLight=oldAmbient;studio.intensity=oldStudio;
            Time.timeScale=reviewTimeScale;
            while(ground.Session.Life(1).Dead)yield return null;
            ground.PlaceCombatReviewSeat(1,new Vector3(0,-1.2f,-3),0,180);
            Time.timeScale=0;
            artCamera.transform.position=new Vector3(0,1,-7);artCamera.transform.LookAt(new Vector3(0,0,-3));yield return Capture("17-live-basement-opponent");
            Time.timeScale=reviewTimeScale;
            // Park actors outside the architectural shots after the gameplay readability capture.
            for(int seat=0;seat<4;seat++)ground.PlaceCombatReviewSeat(seat,new Vector3(26,.6f,-11-seat*2),0,180);
            while(ground.Session.Life(1).Dead)yield return null;
            ground.PlaceCombatReviewSeat(1,new Vector3(-25,0,0),0,0);
            float priorTimeScale=Time.timeScale;Time.timeScale=0; // Freeze only staged architectural shots so combat cannot move/remove the lower-floor subject.
            var positions=new[]{new Vector3(0,1.6f,32),new Vector3(32,5.6f,30),new Vector3(-34,1.6f,0),new Vector3(-32,5.6f,26),new Vector3(-25,5.6f,2),new Vector3(0,5.6f,17),new Vector3(0,5.6f,-17),new Vector3(-30,5.6f,30),new Vector3(30,5.6f,30),new Vector3(-30,5.6f,-30),new Vector3(30,5.6f,-30),new Vector3(0,.4f,-5)};
            var targets=new[]{new Vector3(-16,1.6f,32),new Vector3(36,5.6f,30),new Vector3(-24,1.6f,0),new Vector3(-24,5.6f,18),new Vector3(-25,.6f,0),new Vector3(0,5.6f,4),new Vector3(0,5.6f,-4),new Vector3(-32,4.6f,32),new Vector3(32,4.6f,32),new Vector3(-32,4.6f,-32),new Vector3(32,4.6f,-32),new Vector3(0,.4f,4)};
            var names=new[]{"06-lowered-outer-ring","07-panoramic-window","08-wide-west-gateway","09-wide-diagonal","10-grating-lower-player","11-north-bridge","12-south-bridge","13-speed-nw","14-armor-ne","15-armor-sw","16-speed-se","17-basement"};
            for(int i=0;i<positions.Length;i++){artCamera.transform.position=positions[i];artCamera.transform.LookAt(targets[i]);yield return Capture(names[i]);}
            var windowPositions=new[]{new Vector3(31,5.8f,30),new Vector3(-31,5.8f,-30),new Vector3(0,1.7f,-31),new Vector3(0,1.7f,31),new Vector3(32,5.8f,27)};
            var windowTargets=new[]{new Vector3(38,5.8f,30),new Vector3(-38,5.8f,-30),new Vector3(0,1.7f,-38),new Vector3(0,1.7f,38),new Vector3(38,5.8f,30)};
            var windowNames=new[]{"18-window-blue-planet","19-window-rock-planet","20-window-sun","21-window-stars","22-window-oblique-depth"};
            for(int i=0;i<windowPositions.Length;i++){artCamera.transform.position=windowPositions[i];artCamera.transform.LookAt(windowTargets[i]);yield return Capture(windowNames[i]);}
            var routePositions=new[]{new Vector3(0,1.7f,30),new Vector3(0,1.7f,-30),new Vector3(-33,5.7f,18),new Vector3(33,5.7f,-18),new Vector3(0,.4f,-4),new Vector3(0,5.7f,18),new Vector3(0,5.7f,-18)};
            var routeTargets=new[]{new Vector3(0,2.1f,24),new Vector3(0,2.1f,-24),new Vector3(-28,6,16),new Vector3(28,6,-16),new Vector3(2.6f,1,0),new Vector3(0,5.8f,24),new Vector3(0,5.8f,-24)};
            var routeNames=new[]{"23-north-red","24-south-yellow","25-west-violet","26-east-violet","27-central-blue","28-north-upper-red","29-south-upper-yellow"};
            for(int i=0;i<routePositions.Length;i++){artCamera.transform.position=routePositions[i];artCamera.transform.LookAt(routeTargets[i]);yield return Capture(routeNames[i]);}
            // F4: unhidden native views looking down both orientations of the smooth ceiling.
            artCamera.transform.position=new Vector3(-32,5.65f,17);
            artCamera.transform.LookAt(new Vector3(-32,3.8f,7));yield return Capture("32-smooth-west-ceiling");
            artCamera.transform.position=new Vector3(31,5.65f,32);
            artCamera.transform.LookAt(new Vector3(21,3.8f,32));yield return Capture("33-smooth-north-ceiling");
            // F3 diagnostic views of the restored open flanks, with all runtime walls/ceilings visible.
            foreach(int side in new[]{-1,1})
            {
                artCamera.transform.position=new Vector3(-18*side,1.65f,15*side);
                artCamera.transform.LookAt(new Vector3(-6*side,1.4f,15*side));
                yield return Capture(side>0?"30-open-north-spawn-flank":"31-open-south-spawn-flank");
            }
            Time.timeScale=priorTimeScale;Destroy(artCameraObject);foreach(var c in reviewCameras)c.enabled=true;foreach(var c in reviewCanvases)c.enabled=true;
            // Real player projection, camera-only cutaway: roof/wall renderers hidden only for the labelled overview.
            var arena=ground.GetComponentInChildren<ProvingArena>();var cameras=ground.GetComponentsInChildren<Camera>();var canvases=ground.GetComponentsInChildren<Canvas>();
            foreach(var camera in cameras)camera.enabled=false;foreach(var canvas in canvases)canvas.enabled=false;
            var hidden=arena.GetComponentsInChildren<Renderer>().Where(r=>r.name.Contains("roof")||r.name.Contains("ceiling")).ToArray();foreach(var r in hidden)r.enabled=false;
            var go=new GameObject("R7-cutaway-camera");var overview=go.AddComponent<Camera>();overview.transform.position=new Vector3(62,78,-68);overview.transform.LookAt(new Vector3(0,0,0));overview.fieldOfView=52;overview.backgroundColor=new Color(.1f,.14f,.2f);overview.clearFlags=CameraClearFlags.SolidColor;
            yield return Capture("04-diagnostic-cutaway");
            foreach(var r in hidden)r.enabled=true;Destroy(go);foreach(var camera in cameras)camera.enabled=true;foreach(var canvas in canvases)canvas.enabled=true;
            var old=ground.Session;var repeat=ground.GetComponentsInChildren<Button>(true).Single(b=>b.GetComponentInChildren<Text>()?.text=="Повторить матч");repeat.onClick.Invoke();yield return null;
            if(ground.Session==old||ground.Session.ArenaIdentity!=CombatBowlCatalog.Identity)throw new InvalidOperationException("R7 Repeat identity mismatch");
            yield return Capture("05-repeat");
            File.WriteAllText(Path.Combine(directory,"review.json"),JsonUtility.ToJson(new Result{arena=ground.Session.ArenaIdentity,authoring=ground.CombatBowlAuthoring.Id+"@"+ground.CombatBowlAuthoring.Version,presentation=ground.OrbitalLeagueProfile.Id+"@"+ground.OrbitalLeagueProfile.Version,participants=ground.Session.ParticipantCount,seed=20260923,release=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_RELEASE_ID"),muted=AudioListener.volume==0,width=Screen.width,height=Screen.height,repeat=true,player="native macOS Development Player",note="Scripted live diagnostic match; architectural views freeze time for staging; physical controllers and human playable acceptance remain open."},true));
            Debug.Log("COMBAT_BOWL_R7_REVIEW_COMPLETE "+directory);
        }
        IEnumerator Capture(string name){yield return new WaitForSecondsRealtime(.35f);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"));yield return new WaitForSecondsRealtime(.5f);}
        [Serializable] class Performance{public int samples,width,height,localLights;public long allocatedMemoryBytes;public float medianMs,p95Ms;public string note;}
        [Serializable] class Result{public string arena,authoring,presentation,release,player,note;public int participants;public uint seed;public bool muted,repeat;public int width,height;}
        [Serializable] class LightingDiagnostic{public string pipeline,qualityShadows;public int casterRenderers,receiverRenderers;public string[] skinnedShadowPasses;public LightSnapshot[] lights;}
        [Serializable] class LightSnapshot{public string name,shadows;public Vector3 position;public float range,spotAngle,intensity,strength;}
        void OnDestroy(){Time.timeScale=reviewTimeScale;if(pads!=null)foreach(var pad in pads)if(pad.added)InputSystem.RemoveDevice(pad);}
    }
}
