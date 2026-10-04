#if UNITY_EDITOR || DEVELOPMENT_BUILD || STAR_TOURNAMENT_DEVELOPMENT_QA
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace StarTournament.ProvingGround
{
    /// <summary>Explicit development capture. Diagnostic framing constants never enter gameplay state.</summary>
    public sealed class NativeCombatBowlReview : MonoBehaviour
    {
        ProvingGround ground; string directory;float reviewTimeScale=1;
        IEnumerator Start()
        {
            ground=GetComponent<ProvingGround>();directory=ground.CombatBowlReviewDirectory;Directory.CreateDirectory(directory);AudioListener.volume=0;Application.runInBackground=true;
            while(!Application.isFocused||!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
            yield return Capture("01-setup");
            var roster=NativeMatchRoster.Ffa(8);
            var metadata=Enumerable.Range(0,8).Select(i=>new NativeParticipantInfo(i<4?NativeParticipantKind.LocalHuman:NativeParticipantKind.Bot,i<4?"Игрок "+(i+1):"Бот "+(i-3),NativeParticipantColors.For(i),i<4?-1:1)).ToArray();
            // A capture owns its scripted camera poses; diagnostic mode keeps the review independent
            // of macOS cursor focus while another native Player is open.
            ground.StartBotReview(new NativeMatchComposition(roster,metadata,new[]{0,1,2,3}),20260923);
            if(!ground.Running)throw new InvalidOperationException("R7 review could not start match");
            if(Environment.GetCommandLineArgs().Contains("-veteranReview")){yield return VeteranReview();yield break;}
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
            int unfocusedFrames=0;
            for(int frame=0;frame<360;frame++){yield return null;if(!ground.Running)throw new InvalidOperationException("Review paused during timing");if(!Application.isFocused)unfocusedFrames++;samples.Add(Time.unscaledDeltaTime*1000);}
            samples.Sort();
            File.WriteAllText(Path.Combine(directory,"performance.json"),JsonUtility.ToJson(new Performance{samples=samples.Count,unfocusedFrames=unfocusedFrames,medianMs=samples[samples.Count/2],p95Ms=samples[(int)(samples.Count*.95f)],allocatedMemoryBytes=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong(),width=Screen.width,height=Screen.height,localLights=ground.GetComponentInChildren<OrbitalLeaguePresentation>().LocalLightCount,note="Diagnostic on current Mac; four native cameras, scripted stationary views, vsync/frame cap included. Not reference hardware acceptance."},true));
            var budgetFixtureLights=ground.GetComponentInChildren<OrbitalLeaguePresentation>().GetComponentsInChildren<Light>()
                .Where(light=>light.name.StartsWith("League fixture light /",StringComparison.Ordinal)).ToArray();
            foreach(var light in budgetFixtureLights)light.enabled=false;
            for(int frame=0;frame<30;frame++)yield return null; // Let renderer settle after changing the light set.
            samples.Clear();unfocusedFrames=0;
            for(int frame=0;frame<360;frame++){yield return null;if(!ground.Running)throw new InvalidOperationException("Review paused during fixture budget timing");if(!Application.isFocused)unfocusedFrames++;samples.Add(Time.unscaledDeltaTime*1000);}
            samples.Sort();
            File.WriteAllText(Path.Combine(directory,"performance-fixtures-disabled.json"),JsonUtility.ToJson(new Performance{samples=samples.Count,unfocusedFrames=unfocusedFrames,medianMs=samples[samples.Count/2],p95Ms=samples[(int)(samples.Count*.95f)],allocatedMemoryBytes=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong(),width=Screen.width,height=Screen.height,localLights=ground.GetComponentInChildren<OrbitalLeaguePresentation>().LocalLightCount-budgetFixtureLights.Length,note="Same Player session, four native cameras and scripted poses as performance.json; only fixture lights disabled for paired diagnostic."},true));
            foreach(var light in budgetFixtureLights)light.enabled=true;
            // Full-screen architectural views are real Player renders; no wall or roof is hidden.
            var reviewCameras=ground.GetComponentsInChildren<Camera>();var reviewCanvases=ground.GetComponentsInChildren<Canvas>();
            foreach(var c in reviewCameras)c.enabled=false;foreach(var c in reviewCanvases)c.enabled=false;
            var artCameraObject=new GameObject("Orbital architectural capture");var artCamera=artCameraObject.AddComponent<Camera>();artCamera.fieldOfView=72;artCamera.nearClipPlane=.05f;
            while(ground.Session.Life(0).Dead||ground.Session.Life(1).Dead)yield return null;
            reviewTimeScale=Time.timeScale;Time.timeScale=0;
            var ownCamera=ground.transform.Find("seat-camera-1").GetComponent<Camera>();
            var ownShadowProxies=ground.transform.Find("player-1/trooper-presentation")
                .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer=>renderer.name=="own-shadow-proxy").ToArray();
            artCamera.cullingMask=ownCamera.cullingMask;artCamera.fieldOfView=ownCamera.fieldOfView;
            foreach(int yaw in new[]{0,90,180,270})
            {
                ground.PlaceCombatReviewSeat(0,new Vector3(0,4,-3),70,yaw);
                yield return null;
                artCamera.transform.SetPositionAndRotation(ownCamera.transform.position,ownCamera.transform.rotation);
                yield return Capture("03b-own-shadow-yaw-"+yaw);
                foreach(var proxy in ownShadowProxies)proxy.enabled=false;
                yield return Capture("03c-own-shadow-disabled-yaw-"+yaw);
                foreach(var proxy in ownShadowProxies)proxy.enabled=true;
            }
            artCamera.cullingMask=~0;artCamera.fieldOfView=72;
            // Park other live participants outside this paired diagnostic frame; the eight-player gameplay capture above is untouched.
            for(int seat=0;seat<ground.Session.ParticipantCount;seat++)
                if(seat!=1)ground.PlaceCombatReviewSeat(seat,new Vector3(30,4,20-seat*1.5f),0,180);
            // Corpses are independent presentation clones, so moving their participant cannot clear the diagnostic frame.
            foreach(var corpse in ground.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("corpse-",StringComparison.Ordinal)))
                corpse.gameObject.SetActive(false);
            ground.PlaceCombatReviewSeat(1,new Vector3(0,4,-3),0,180);
            yield return null;
            artCamera.transform.position=new Vector3(0,7,-7);artCamera.transform.LookAt(new Vector3(0,4.75f,-3));yield return Capture("16-live-hall-opponent");
            foreach(int side in new[]{-1,1})
            {
                artCamera.transform.position=new Vector3(side*16,3.8f,-side*4);
                artCamera.transform.LookAt(new Vector3(side*12,5.74f,-side*2));
                yield return Capture(side<0?"16f-west-rise-wall-label":"16g-east-rise-wall-label");
            }
            var shadowLights=ground.GetComponentInChildren<OrbitalLeaguePresentation>().GetComponentsInChildren<Light>().Where(light=>light.name.StartsWith("League ",StringComparison.Ordinal))
                .OrderBy(light=>light.name.StartsWith("League ceiling light /",StringComparison.Ordinal)?0:1)
                .ThenBy(light=>int.Parse(light.name.Split('/').Last().Trim())).ToArray();
            var studio=ground.GetComponentsInChildren<Light>().First(light=>light.type==LightType.Directional);
            var renderers=ground.GetComponentsInChildren<Renderer>(true);
            var lighting=ground.GetComponentInChildren<OrbitalLeaguePresentation>();
            var auxiliaryLights=shadowLights.Where(light=>light.name.StartsWith("League fixture light /",StringComparison.Ordinal)).ToArray();
            // A lower-floor lamp needs its own visible pool, not only a ray that reaches a collider.
            // Select the west gateway fixture by position: generated fixture indices change with authored map revisions.
            var lowerFloorLamp=auxiliaryLights.OrderBy(light=>Vector3.Distance(light.transform.position,new Vector3(-20.36f,7.87f,15f))).First();
            var lampPosition=lowerFloorLamp.transform.position;
            artCamera.transform.position=new Vector3(lampPosition.x,2,lampPosition.z-8);
            artCamera.transform.LookAt(new Vector3(lampPosition.x,2.8f,lampPosition.z));
            yield return Capture("08b-lower-floor-fixture-on");
            lowerFloorLamp.enabled=false;
            yield return Capture("08c-lower-floor-fixture-off");
            lowerFloorLamp.enabled=true;
            int surfaceMask=(1<<ProvingArena.WorldLayer)|(1<<ProvingArena.MovementOnlyLayer);
            File.WriteAllText(Path.Combine(directory,"lighting-diagnostic.json"),JsonUtility.ToJson(new LightingDiagnostic{
                pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline?.name,
                qualityShadows=QualitySettings.shadows.ToString(),
                fixtures=lighting.FixtureCount,fixtureLights=lighting.FixtureLightCount,
                fixtureRaysHit=auxiliaryLights.Count(light=>Physics.Raycast(light.transform.position,light.transform.forward,
                    light.range,surfaceMask,QueryTriggerInteraction.Ignore)),
                casterRenderers=renderers.Count(renderer=>renderer.shadowCastingMode!=UnityEngine.Rendering.ShadowCastingMode.Off),
                receiverRenderers=renderers.Count(renderer=>renderer.receiveShadows),
                skinnedShadowPasses=renderers.OfType<SkinnedMeshRenderer>().Select(renderer=>{
                    var material=renderer.sharedMaterial;
                    return renderer.name+" | "+(material?material.shader.name:"missing")+" | "+(material?material.FindPass("ShadowCaster"):-1);
                }).Distinct().ToArray(),
                lights=shadowLights.Concat(new[]{studio}).Select(light=>new LightSnapshot{name=light.name,position=light.transform.position,direction=light.transform.forward,range=light.range,spotAngle=light.spotAngle,intensity=light.intensity,shadows=light.shadows.ToString(),strength=light.shadowStrength}).ToArray()
            },true));
            var shadowModes=shadowLights.Select(light=>light.shadows).ToArray();
            var studioShadowMode=studio.shadows;studio.shadows=LightShadows.None;
            foreach(var light in shadowLights)light.shadows=LightShadows.None;
            yield return Capture("16a-hall-shadow-disabled-diagnostic");
            for(int light=0;light<shadowLights.Length;light++)shadowLights[light].shadows=shadowModes[light];
            studio.shadows=studioShadowMode;
            foreach(var light in shadowLights)light.enabled=false;
            yield return Capture("16b-hall-ceiling-lights-disabled-diagnostic");
            foreach(var light in shadowLights)light.enabled=true;
            // Isolate one real spotlight to distinguish a missing shadow pass from excess fill.
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
            foreach(var light in shadowLights)light.enabled=false;
            yield return Capture("17a-basement-ceiling-lights-disabled-diagnostic");
            foreach(var light in shadowLights)light.enabled=true;
            ground.PlaceCombatReviewSeat(1,new Vector3(-12,0,28),0,90);
            artCamera.transform.position=new Vector3(0,1.6f,28);artCamera.transform.LookAt(new Vector3(-12,1.1f,28));
            yield return Capture("17b-live-outer-ring-opponent");
            ground.PlaceCombatReviewSeat(1,new Vector3(0,4,13),0,180);
            artCamera.transform.position=new Vector3(0,6,5);artCamera.transform.LookAt(new Vector3(0,5,13));
            yield return Capture("17c-live-north-bridge-opponent");
            Time.timeScale=reviewTimeScale;
            // Park actors outside the architectural shots after the gameplay readability capture.
            for(int seat=0;seat<4;seat++)ground.PlaceCombatReviewSeat(seat,new Vector3(26,.6f,-11-seat*2),0,180);
            while(ground.Session.Life(1).Dead)yield return null;
            ground.PlaceCombatReviewSeat(1,new Vector3(-25,0,0),0,0);
            float priorTimeScale=Time.timeScale;Time.timeScale=0; // Freeze only staged architectural shots so combat cannot move/remove the lower-floor subject.
            var positions=new[]{new Vector3(0,1.6f,28),new Vector3(32,5.6f,30),new Vector3(-34,1.6f,0),new Vector3(-32,5.6f,26),new Vector3(-25,5.6f,2),new Vector3(0,5.6f,17),new Vector3(0,5.6f,-17),new Vector3(-30,5.6f,30),new Vector3(30,5.6f,30),new Vector3(-30,5.6f,-30),new Vector3(30,5.6f,-30),new Vector3(0,.4f,-5)};
            var targets=new[]{new Vector3(-16,1.6f,28),new Vector3(36,5.6f,30),new Vector3(-24,1.6f,0),new Vector3(-24,5.6f,18),new Vector3(-25,.6f,0),new Vector3(0,5.6f,4),new Vector3(0,5.6f,-4),new Vector3(-32,4.6f,32),new Vector3(32,4.6f,32),new Vector3(-32,4.6f,-32),new Vector3(32,4.6f,-32),new Vector3(0,.4f,4)};
            var names=new[]{"06-lowered-outer-ring","07-panoramic-window","08-wide-west-gateway","09-wide-diagonal","10-grating-lower-player","11-north-bridge","12-south-bridge","13-speed-nw","14-armor-ne","15-armor-sw","16-speed-se","17-basement"};
            for(int i=0;i<positions.Length;i++)
            {
                artCamera.transform.position=positions[i];artCamera.transform.LookAt(targets[i]);yield return Capture(names[i]);
                if(i==2)
                {
                    var fixtureLights=shadowLights.Where(light=>light.name.StartsWith("League fixture light /",StringComparison.Ordinal)).ToArray();
                    foreach(var light in fixtureLights)light.enabled=false;
                    yield return Capture("08a-wide-west-gateway-fixture-lights-disabled");
                    foreach(var light in fixtureLights)light.enabled=true;
                }
                if(i!=0)continue;
                foreach(var light in shadowLights)light.enabled=false;
                yield return Capture("06a-outer-ring-ceiling-lights-disabled-diagnostic");
                foreach(var light in shadowLights)light.enabled=true;
                var outerAmbient=RenderSettings.ambientLight;var outerStudio=studio.intensity;
                studio.intensity=0;RenderSettings.ambientLight=Color.white*.05f;
                foreach(var light in shadowLights)light.enabled=false;
                shadowLights[12].enabled=true;
                yield return Capture("06b-outer-ring-isolated-spot-diagnostic");
                foreach(var light in shadowLights)light.enabled=true;
                studio.intensity=outerStudio;RenderSettings.ambientLight=outerAmbient;
            }
            var windowPositions=new[]{new Vector3(31,5.8f,27),new Vector3(-31,5.8f,-27),new Vector3(0,1.7f,-31),new Vector3(0,1.7f,31),new Vector3(32,5.8f,27)};
            var windowTargets=new[]{new Vector3(38,5.8f,27),new Vector3(-38,5.8f,-27),new Vector3(0,1.7f,-38),new Vector3(0,1.7f,38),new Vector3(38,5.8f,27)};
            var windowNames=new[]{"18-window-blue-planet","19-window-rock-planet","20-window-sun","21-window-stars","22-window-oblique-depth"};
            for(int i=0;i<windowPositions.Length;i++){artCamera.transform.position=windowPositions[i];artCamera.transform.LookAt(windowTargets[i]);yield return Capture(windowNames[i]);}
            var routePositions=new[]{new Vector3(0,1.7f,30),new Vector3(0,1.7f,-30),new Vector3(-33,5.7f,18),new Vector3(33,5.7f,-18),new Vector3(0,.4f,-4),new Vector3(0,5.7f,18),new Vector3(0,5.7f,-18)};
            var routeTargets=new[]{new Vector3(0,2.1f,24),new Vector3(0,2.1f,-24),new Vector3(-28,6,16),new Vector3(28,6,-16),new Vector3(2.6f,1,0),new Vector3(0,5.8f,24),new Vector3(0,5.8f,-24)};
            var routeNames=new[]{"23-north-red","24-south-yellow","25-west-violet","26-east-violet","27-central-blue","28-north-upper-red","29-south-upper-yellow"};
            for(int i=0;i<routePositions.Length;i++){artCamera.transform.position=routePositions[i];artCamera.transform.LookAt(routeTargets[i]);yield return Capture(routeNames[i]);}
            // F4: unhidden native views looking down both orientations of the smooth ceiling.
            artCamera.transform.position=new Vector3(-32,5.65f,17);
            artCamera.transform.LookAt(new Vector3(-32,3.8f,7));yield return Capture("32-smooth-west-ceiling");
            artCamera.transform.position=new Vector3(31,5.65f,28);
            artCamera.transform.LookAt(new Vector3(21,3.8f,28));yield return Capture("33-smooth-north-ceiling");
            // F3 diagnostic views of the restored open flanks, with all runtime walls/ceilings visible.
            foreach(int side in new[]{-1,1})
            {
                artCamera.transform.position=new Vector3(-18*side,1.65f,15*side);
                artCamera.transform.LookAt(new Vector3(-6*side,1.4f,15*side));
                yield return Capture(side>0?"30-open-north-spawn-flank":"31-open-south-spawn-flank");
            }
            var f7Positions=new[]{new Vector3(0,1.65f,14),new Vector3(-13,1.65f,10),new Vector3(13,1.65f,-10),new Vector3(0,.45f,6),new Vector3(-17,1.65f,28)};
            var f7Targets=new[]{new Vector3(0,3,0),new Vector3(-5,3,0),new Vector3(5,3,0),new Vector3(0,.45f,-7),new Vector3(-28,5,28)};
            for(int i=0;i<f7Positions.Length;i++){artCamera.transform.position=f7Positions[i];artCamera.transform.LookAt(f7Targets[i]);yield return Capture("40-f7-architecture-"+i);}
            yield return CaptureDirectDiagonalWalks(artCamera);
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
        IEnumerator VeteranReview()
        {
            ground.PlaceCombatReviewSeat(0,new Vector3(0,4,3),0,0);
            ground.PlaceCombatReviewSeat(1,new Vector3(0,4,10),0,180);
            ground.PlaceCombatReviewSeat(2,new Vector3(-3.5f,4,5),0,0);
            ground.PlaceCombatReviewSeat(3,new Vector3(2,4,5),0,40);
            yield return new WaitForSecondsRealtime(12);
            yield return Capture("veteran-01-four-views");
            var samples=new System.Collections.Generic.List<float>();int unfocused=0;
            for(int frame=0;frame<180;frame++){yield return null;samples.Add(Time.unscaledDeltaTime*1000);if(!Application.isFocused)unfocused++;}
            samples.Sort();File.WriteAllText(Path.Combine(directory,"veteran-performance.json"),JsonUtility.ToJson(new Performance{samples=samples.Count,unfocusedFrames=unfocused,medianMs=samples[samples.Count/2],p95Ms=samples[(int)(samples.Count*.95f)],width=Screen.width,height=Screen.height,note="Warmed four-camera current-Mac diagnostic; not reference hardware acceptance."},true));
            float prior=Time.timeScale;Time.timeScale=0;
            // Keep the architectural sample visible; the earlier four-view capture is live staging.
            for(int seat=0;seat<ground.Session.ParticipantCount;seat++)ground.PlaceCombatReviewSeat(seat,new Vector3(-32,4,16+seat),0,0);
            ground.PlaceCombatReviewSeat(1,new Vector3(0,4,14),0,180);
            var cameras=ground.GetComponentsInChildren<Camera>();var canvases=ground.GetComponentsInChildren<Canvas>();
            foreach(var c in cameras)c.enabled=false;foreach(var c in canvases)c.enabled=false;
            var go=new GameObject("Veteran review camera");var camera=go.AddComponent<Camera>();camera.fieldOfView=72;camera.nearClipPlane=.05f;
            camera.transform.position=new Vector3(0,5.65f,1);camera.transform.LookAt(new Vector3(0,5.9f,8));yield return Capture("veteran-02-player-height");
            camera.transform.position=new Vector3(5.35f,7.1f,-7.1f);camera.transform.LookAt(new Vector3(-.5f,5.5f,2));yield return Capture("veteran-03-overview");
            camera.transform.position=new Vector3(-2.6f,5.6f,5.4f);camera.transform.LookAt(new Vector3(-4.4f,5.75f,7.8f));yield return Capture("veteran-04-service");
            camera.transform.position=new Vector3(1.3f,5.5f,4.4f);camera.transform.LookAt(new Vector3(4.5f,4.9f,6.8f));yield return Capture("veteran-05-cover");
            camera.transform.position=new Vector3(0,5.65f,5.8f);camera.transform.LookAt(new Vector3(-.4f,5.8f,-7));yield return Capture("veteran-07-south-player-height");
            camera.transform.position=new Vector3(4.8f,5.65f,.8f);camera.transform.LookAt(new Vector3(-5.9f,6.1f,-2.5f));yield return Capture("veteran-08-west-broadcast-wall");
            Time.timeScale=prior;Destroy(go);foreach(var c in cameras)c.enabled=true;foreach(var c in canvases)c.enabled=true;
            var previous=ground.Session;var repeat=ground.GetComponentsInChildren<Button>(true).Single(b=>b.GetComponentInChildren<Text>()?.text=="Повторить матч");repeat.onClick.Invoke();yield return null;
            if(previous==ground.Session||ground.Session.ArenaIdentity!=CombatBowlCatalog.Identity)throw new InvalidOperationException("Veteran Repeat identity mismatch");
            yield return Capture("veteran-06-repeat");
            File.WriteAllText(Path.Combine(directory,"veteran-review.json"),JsonUtility.ToJson(new Result{arena=ground.Session.ArenaIdentity,authoring=ground.CombatBowlAuthoring.Id+"@"+ground.CombatBowlAuthoring.Version,presentation=ground.OrbitalLeagueProfile.Id+"@"+ground.OrbitalLeagueProfile.Version,participants=ground.Session.ParticipantCount,seed=20260923,release=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_RELEASE_ID"),muted=AudioListener.volume==0,width=Screen.width,height=Screen.height,repeat=true,player="native macOS Development Player",note="Veteran F2 entire central hall, real Player renders; staged architectural views. Physical/human acceptance pending."},true));
            Debug.Log("VETERAN_REVIEW_COMPLETE");Application.Quit();
        }
        IEnumerator CaptureDirectDiagonalWalks(Camera camera)
        {
            // Diagnostic route coordinates and time bounds; actual movement uses the gameplay capsule profile.
            var walks=new System.Collections.Generic.List<Walk>();float eye=ground.Profile.Get("camera.eyeHeight");
            foreach(int sx in new[]{-1,1})foreach(int sz in new[]{-1,1})foreach(bool reverse in new[]{false,true})
            {
                var route=new[]{new Vector3(sx*32,4,sz*17),new Vector3(sx*32,4,sz*26),new Vector3(sx*24,4,sz*18)};
                if(reverse)Array.Reverse(route);
                var walker=new GameObject("F3 diagnostic walking capsule");walker.layer=ProvingArena.ParticipantLayer;
                walker.AddComponent<CharacterController>();var motor=walker.AddComponent<CharacterMotor>();motor.Initialize(ground.Profile,route[0]);
                string label=$"34-diagonal-{sx}-{sz}-{(reverse?"out":"in")}";
                camera.transform.position=motor.State.Position+Vector3.up*eye;camera.transform.LookAt(route[1]+Vector3.up*eye);
                yield return Capture(label+"-start");
                foreach(var goal in route.Skip(1))
                {
                    for(int tick=0;tick<300;tick++)
                    {
                        var delta=goal-motor.State.Position;var move=new Vector2(delta.x,delta.z);if(move.magnitude<.2f)break;
                        motor.Tick(new LocalAction{Move=move.normalized},1f/60);Physics.SyncTransforms();
                        camera.transform.position=motor.State.Position+Vector3.up*eye;camera.transform.LookAt(goal+Vector3.up*eye);yield return null;
                    }
                    if(Vector3.Distance(motor.State.Position,goal)>.3f)throw new InvalidOperationException($"F3 direct walk blocked {label}: {motor.State.Position} -> {goal}");
                }
                walks.Add(new Walk{label=label,start=route[0],end=motor.State.Position,arrived=true});yield return Capture(label+"-end");Destroy(walker);
            }
            File.WriteAllText(Path.Combine(directory,"f3-walks.json"),JsonUtility.ToJson(new Walks{routes=walks.ToArray(),note="Native CharacterMotor direct input, frozen match and diagnostic camera, no pathfinding; human playtest remains open."},true));
        }
        [Serializable] class Walk{public string label;public Vector3 start,end;public bool arrived;}
        [Serializable] class Walks{public Walk[] routes;public string note;}
        IEnumerator Capture(string name){yield return new WaitForSecondsRealtime(.35f);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"));yield return new WaitForSecondsRealtime(.5f);}
        [Serializable] class Performance{public int samples,unfocusedFrames,width,height,localLights;public long allocatedMemoryBytes;public float medianMs,p95Ms;public string note;}
        [Serializable] class Result{public string arena,authoring,presentation,release,player,note;public int participants;public uint seed;public bool muted,repeat;public int width,height;}
        [Serializable] class LightingDiagnostic{public string pipeline,qualityShadows;public int fixtures,fixtureLights,fixtureRaysHit,casterRenderers,receiverRenderers;public string[] skinnedShadowPasses;public LightSnapshot[] lights;}
        [Serializable] class LightSnapshot{public string name,shadows;public Vector3 position,direction;public float range,spotAngle,intensity,strength;}
        void OnDestroy(){Time.timeScale=reviewTimeScale;}
    }
}

#endif
