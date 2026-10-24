#if UNITY_EDITOR || DEVELOPMENT_BUILD || STAR_TOURNAMENT_DEVELOPMENT_QA
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace StarTournament.ProvingGround
{
    // Opt-in QA only; ordinary release builds do not contain this component.
    public sealed class NativeWellPlayerReview : MonoBehaviour
    {
        [Serializable] sealed class Sample
        {
            public string view,variant; public int width,height,frames,unfocusedFrames,nonRunningFrames; public double medianMs,p95Ms,gpuMedianMs,botMedianMs,botP95Ms,simulationSeconds; public int botSamples;
        }
        [Serializable] sealed class Report
        {
            public string status="DIAGNOSTIC_NOT_ACCEPTANCE",gpu,unity,pipeline; public int requestedWidth,requestedHeight,humans,bots;
            public string protocol="Frozen seed20261003,8bots,onecamera;5s warmup+8s sample per variant, baseline repeated; unchanged resolution/pose/settings; temporary renderer toggles restored.";
            public List<Sample> samples=new List<Sample>();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {if(Environment.GetCommandLineArgs().Contains("-wellPlayerReview"))new GameObject("Well Player diagnostic").AddComponent<NativeWellPlayerReview>();}
        ProvingGround ground;string directory;Report report;MeshRenderer garden;Light[] lamps;LightShadows[] shadows;ShadowCastingMode gardenShadow;
        Camera camera;UniversalAdditionalCameraData cameraData;bool postprocess;readonly FrameTiming[] timing=new FrameTiming[1];
        IEnumerator Start()
        {
            Invoke(nameof(Timeout),600);Application.runInBackground=true;
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-wellPlayerEvidence");
            if(at<0)throw new InvalidOperationException("Owned -wellPlayerEvidence directory required");
            directory=args[at+1];Directory.CreateDirectory(directory);
            ground=FindFirstObjectByType<ProvingGround>();while(!ground||!ground.IsReady||!SplashScreen.isFinished){yield return null;if(!ground)ground=FindFirstObjectByType<ProvingGround>();}
            ground.SendMessage("OpenSetup");ground.SelectAuthoredMap(WellArenaCatalog.Id);
            bool oneBot=args.Contains("-wellPlayerOneBot");int participants=oneBot?2:8;
            var roster=Enumerable.Range(0,participants).Select(n=>oneBot&&n==0?
                new NativeParticipantInfo(NativeParticipantKind.LocalHuman,"Player",NativeParticipantColors.For(n)):
                new NativeParticipantInfo(NativeParticipantKind.Bot,"Bot "+n,NativeParticipantColors.For(n),(int)NativeBotDifficulty.Hard)).ToArray();
            ground.StartBotReview(new NativeMatchComposition(NativeMatchRoster.Ffa(participants),roster,new[]{0}),20261003);
            bool live=args.Contains("-wellPlayerLive");
            ground.FullHealReviewManualTick=!live;Application.runInBackground=true;QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
            yield return null;yield return null;
            int widthAt=Array.IndexOf(args,"-screen-width"),heightAt=Array.IndexOf(args,"-screen-height");
            int requestedWidth=widthAt>=0?int.Parse(args[widthAt+1]):Screen.width,requestedHeight=heightAt>=0?int.Parse(args[heightAt+1]):Screen.height;
            // Startup auto-resolution can override CLI dimensions. Apply the owned QA window after startup.
            Screen.SetResolution(requestedWidth,requestedHeight,FullScreenMode.Windowed);yield return new WaitForSecondsRealtime(1);
            camera=ground.GetComponentsInChildren<Camera>().First(c=>c.enabled);cameraData=camera.GetUniversalAdditionalCameraData();postprocess=cameraData.renderPostProcessing;
            garden=ground.CameraReviewArena.GetComponentsInChildren<MeshRenderer>().Single(r=>r.name=="Well natural garden");gardenShadow=garden.shadowCastingMode;
            lamps=ground.CameraReviewArena.GetComponentsInChildren<Light>();shadows=lamps.Select(l=>l.shadows).ToArray();
            report=new Report{humans=oneBot?1:0,bots=oneBot?1:8,requestedWidth=requestedWidth,requestedHeight=requestedHeight,gpu=SystemInfo.graphicsDeviceName,unity=Application.unityVersion,pipeline=GraphicsSettings.currentRenderPipeline.name};
            if(live)report.protocol="Live seed20261003,8bots,onecamera; ordinary AI/physics;5s warmup+8s sample, baseline repeated; diagnostic only, moving camera follows bot0.";
            if(oneBot)report.protocol=report.protocol.Replace("8bots","1local human+1Hardbot").Replace("moving camera follows bot0","stationary human0 camera; live bot1 AI/physics");
            foreach(string view in new[]{"garden","room"})
            {
                ground.PlaceCombatReviewSeat(0,view=="garden"?new Vector3(0,.1f,-7):new Vector3(17.5f,.02f,20.5f),view=="garden"?12:0,view=="garden"?0:75);
                foreach(string variant in live?new[]{"baseline","baseline-repeat"}:new[]{"baseline","garden-off","garden-shadows-off","local-shadows-off","postprocess-off","baseline-repeat"})
                {
                    Restore();
                    if(Screen.width!=requestedWidth||Screen.height!=requestedHeight){report.status="INCOMPLETE_RESOLUTION_MISMATCH";Save();Application.Quit(2);yield break;}
                    if(variant=="garden-off")garden.enabled=false;
                    if(variant=="garden-shadows-off")garden.shadowCastingMode=ShadowCastingMode.Off;
                    if(variant=="local-shadows-off")foreach(var lamp in lamps)lamp.shadows=LightShadows.None;
                    if(variant=="postprocess-off")cameraData.renderPostProcessing=false;
                    double warm=Time.realtimeSinceStartupAsDouble+5;while(Time.realtimeSinceStartupAsDouble<warm)yield return null;
                    int unfocused=0,nonRunning=0;
                    var frame=new List<double>();var gpu=new List<double>();var bot=new List<double>();ulong timestamp=0;int botTick=ground.BotDriver?.Ticks??0;double simulationStart=ground.Session.Time;
                    double previous=Time.realtimeSinceStartupAsDouble,until=previous+8;
                    while(Time.realtimeSinceStartupAsDouble<until)
                    {
                        FrameTimingManager.CaptureFrameTimings();yield return null;
                        if(ground.BotDriver!=null&&ground.BotDriver.Ticks!=botTick){botTick=ground.BotDriver.Ticks;bot.Add(ground.BotDriver.LastMilliseconds);}
                        if(!Application.isFocused)unfocused++;if(!ground.Running)nonRunning++;
                        double now=Time.realtimeSinceStartupAsDouble;frame.Add((now-previous)*1000);previous=now;
                        if(FrameTimingManager.GetLatestTimings(1,timing)>0&&timing[0].frameStartTimestamp!=timestamp)
                        {timestamp=timing[0].frameStartTimestamp;if(timing[0].gpuFrameTime>0)gpu.Add(timing[0].gpuFrameTime);}
                    }
                    frame.Sort();gpu.Sort();bot.Sort();
                    report.samples.Add(new Sample{view=view,variant=variant,width=Screen.width,height=Screen.height,frames=frame.Count,unfocusedFrames=unfocused,nonRunningFrames=nonRunning,botSamples=bot.Count,simulationSeconds=ground.Session.Time-simulationStart,botMedianMs=bot.Count>0?bot[bot.Count/2]:-1,botP95Ms=bot.Count>0?bot[Math.Min(bot.Count-1,(int)(bot.Count*.95))]:-1,medianMs=frame[frame.Count/2],p95Ms=frame[Math.Min(frame.Count-1,(int)(frame.Count*.95))],gpuMedianMs=gpu.Count>0?gpu[gpu.Count/2]:-1});
                    Save();yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory,view+"-"+variant+".png"));yield return new WaitForSecondsRealtime(.3f);
                }
            }
            Restore();Save();Debug.Log("WELL_PLAYER_DIAGNOSTIC_COMPLETE");Application.Quit(0);
        }
        void Restore(){if(garden){garden.enabled=true;garden.shadowCastingMode=gardenShadow;}if(lamps!=null)for(int i=0;i<lamps.Length;i++)if(lamps[i])lamps[i].shadows=shadows[i];if(cameraData)cameraData.renderPostProcessing=postprocess;}
        void Save()=>File.WriteAllText(Path.Combine(directory,"report.json"),JsonUtility.ToJson(report,true));
        void Timeout(){Restore();if(report!=null){report.status="INCOMPLETE_TIMEOUT";Save();}Application.Quit(2);}
        void OnDestroy()=>Restore();
    }
}
#endif
