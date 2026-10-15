#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace StarTournament.ProvingGround
{
    // Opt-in standalone regression evidence; never part of ordinary startup or release builds.
    public sealed class NativeLabStartupReview:MonoBehaviour
    {
        [Serializable] sealed class Evidence
        {
            public bool noaudio,menuReady,historyPendingAtMenu,historyReady,profileUnchanged,audioPreferencesUnchanged;
            public float menuSeconds,historySeconds;public string identity,runtimeHash,error;
            public LabRevisionReference reference;public LabHistoryStartupDiagnostics diagnostics;
            public long managedMemoryAfterGc;public int frameSamples;public double p50FrameMs,p95FrameMs,maxFrameMs;
            public string performanceScope="Diagnostic shared-run frame intervals; no performance/device acceptance";
        }
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"-labStartupEvidence");
            if(flag<0||flag+1>=args.Length)yield break;
            string directory=Path.GetFullPath(args[flag+1]);Directory.CreateDirectory(directory);
            var ground=GetComponent<ProvingGround>();var evidence=new Evidence{noaudio=args.Contains("-noaudio")};
            if(!evidence.noaudio)throw new InvalidOperationException("Startup evidence requires -noaudio from process start");
            int music=PlayerPrefs.GetInt("StarTournament.Audio.MusicPercent",-1),effects=PlayerPrefs.GetInt("StarTournament.Audio.EffectsPercent",-1);
            float deadline=Time.realtimeSinceStartup+1700;
            while(!ground.IsReady){if(Time.realtimeSinceStartup>=deadline)throw new InvalidOperationException("Menu startup timeout");yield return null;}
            evidence.menuReady=true;evidence.menuSeconds=Time.realtimeSinceStartup;evidence.historyPendingAtMenu=!ground.IsLabHistoryReady;
            evidence.runtimeHash=ground.CaptureLabBundle().Hash();evidence.identity=ground.LabSavedIdentity;evidence.reference=ground.ActiveLabReference;
            File.WriteAllText(Path.Combine(directory,"menu-ready.json"),JsonUtility.ToJson(evidence,true));
            yield return null;ScreenCapture.CaptureScreenshot(Path.Combine(directory,"menu.png"));yield return null;
            if(!ground.IsLabHistoryReady)
            {
                ground.SendMessage("OpenDesignLab");yield return null;
                ScreenCapture.CaptureScreenshot(Path.Combine(directory,"lab-waiting.png"));yield return null;
                ground.GetComponentsInChildren<Button>(true).Single(b=>b.name=="lab-back").onClick.Invoke();yield return null;
            }
            var frames=new List<double>();
            while(!ground.IsLabHistoryReady&&ground.LabHistoryLoadError==null)
            {
                if(Time.realtimeSinceStartup>=deadline)throw new InvalidOperationException("History startup timeout");
                if(frames.Count<10000)frames.Add(Time.unscaledDeltaTime*1000d);
                yield return null;
            }
            evidence.historyReady=ground.IsLabHistoryReady;evidence.historySeconds=Time.realtimeSinceStartup;evidence.error=ground.LabHistoryLoadError;
            evidence.profileUnchanged=ground.CaptureLabBundle().Hash()==evidence.runtimeHash&&ground.LabSavedIdentity==evidence.identity&&JsonUtility.ToJson(ground.ActiveLabReference)==JsonUtility.ToJson(evidence.reference);
            evidence.audioPreferencesUnchanged=music==PlayerPrefs.GetInt("StarTournament.Audio.MusicPercent",-1)&&effects==PlayerPrefs.GetInt("StarTournament.Audio.EffectsPercent",-1);
            evidence.diagnostics=ground.LabHistoryDiagnostics;
            evidence.managedMemoryAfterGc=GC.GetTotalMemory(true); // Opt-in diagnostic fixture only, never ordinary runtime.
            frames.Sort();evidence.frameSamples=frames.Count;if(frames.Count>0){evidence.p50FrameMs=frames[(frames.Count-1)/2];evidence.p95FrameMs=frames[(int)((frames.Count-1)*.95)];evidence.maxFrameMs=frames[frames.Count-1];}
            File.WriteAllText(Path.Combine(directory,"complete.json"),JsonUtility.ToJson(evidence,true));
            if(!evidence.historyReady||!evidence.profileUnchanged||!evidence.audioPreferencesUnchanged)throw new InvalidOperationException("Startup review failed; inspect complete.json");
            Debug.Log("LAB_STARTUP_REVIEW_PASS "+directory);
            yield return new WaitForSecondsRealtime(1);Application.Quit();
        }
    }
}
#endif
