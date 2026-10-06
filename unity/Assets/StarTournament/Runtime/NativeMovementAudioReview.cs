#if UNITY_EDITOR || DEVELOPMENT_BUILD || STAR_TOURNAMENT_DEVELOPMENT_QA
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace StarTournament.ProvingGround
{
    // Synthetic native event/routing smoke. Muted by default; never human audio acceptance.
    public sealed class NativeMovementAudioReview : MonoBehaviour
    {
        ProvingGround ground;string directory;AudioSource[] voices;
        const string EffectsKey="StarTournament.Audio.EffectsPercent";
        bool hadEffects,preferencesChanged;int priorEffects;
        InputSettings.BackgroundBehavior priorBackground;bool backgroundChanged;
        void OnDestroy()
        {
            if(backgroundChanged)InputSystem.settings.backgroundBehavior=priorBackground;
            if(!preferencesChanged)return;
            if(hadEffects)PlayerPrefs.SetInt(EffectsKey,priorEffects);else PlayerPrefs.DeleteKey(EffectsKey);
            PlayerPrefs.Save();
        }
        readonly Dictionary<AudioSource,AudioClip> previous=new Dictionary<AudioSource,AudioClip>();
        readonly List<string> events=new List<string>();
        void Update()
        {
            if(voices==null)return;
            foreach(var voice in voices)
            {
                if(!voice||!voice.isPlaying)continue;
                if(previous.TryGetValue(voice,out var clip)&&clip==voice.clip)continue;
                previous[voice]=voice.clip;
                if(voice.clip&&new[]{"step-","jump","land"}.Any(p=>voice.clip.name.StartsWith(p)))
                    events.Add(voice.clip.name+" pitch="+voice.pitch.ToString("F4",System.Globalization.CultureInfo.InvariantCulture));
            }
        }
        IEnumerator Start()
        {
            Application.runInBackground=true;ground=GetComponent<ProvingGround>();
            // Synthetic review devices must continue receiving queued events without OS focus.
            priorBackground=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;backgroundChanged=true;
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-movementAudioReview");
            if(at<0||at+1>=args.Length||!Path.IsPathFullyQualified(args[at+1]))throw new ArgumentException("Absolute movement audio review directory required");
            directory=args[at+1];Directory.CreateDirectory(directory);
            hadEffects=PlayerPrefs.HasKey(EffectsKey);priorEffects=PlayerPrefs.GetInt(EffectsKey);
            NativeAudioPreferences.SetEffects(80);preferencesChanged=true;
            yield return new WaitForSecondsRealtime(1);
            var audio=(NativeGameAudio)typeof(ProvingGround).GetField("gameAudio",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ground);
            // Exercise real audio routing while keeping the system output muted unless explicitly requested.
            audio.SetDiagnosticMute(false);
            voices=ground.transform.Find("native-game-audio").GetComponentsInChildren<AudioSource>().Where(s=>s.name.StartsWith("voice-")).ToArray();
            var pads=new List<Gamepad>();
            foreach(int seats in new[]{1,4})
            {
                while(pads.Count<seats)pads.Add(InputSystem.AddDevice<Gamepad>());
                ground.StartCombatReview(pads.Take(seats).ToArray(),ensureOpponent:seats==1);
                for(int i=0;i<seats;i++)ground.PlaceCombatReviewSeat(i,new Vector3(-8+i*3,0,2),0);
                yield return new WaitForSeconds(.4f);
                int beginning=events.Count;
                for(int cycle=0;cycle<3;cycle++)
                {
                    for(int i=0;i<seats;i++)InputSystem.QueueStateEvent(pads[i],new GamepadState{leftStick=new Vector2(0,cycle%2==0?1:-1)});
                    yield return new WaitForSeconds(.8f);
                    for(int i=0;i<seats;i++)InputSystem.QueueStateEvent(pads[i],new GamepadState().WithButton(GamepadButton.South));
                    yield return new WaitForSeconds(.1f);
                    foreach(var p in pads)InputSystem.QueueStateEvent(p,new GamepadState());
                    yield return new WaitForSeconds(1);
                }
                var observed=events.Skip(beginning).ToArray();
                File.WriteAllLines(Path.Combine(directory,seats+"-seats-events.txt"),observed);
                foreach(var kind in new[]{"step-","jump","land"})
                    if(!observed.Any(e=>e.StartsWith(kind)))throw new InvalidOperationException("Missing actual motion audio: "+seats+" seats "+kind);
                yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,seats+"-seats.png"));
                yield return new WaitForSecondsRealtime(.4f);
                audio.StopEffects();
            }
            foreach(var p in pads)InputSystem.RemoveDevice(p);
            AudioListener.volume=0;
            File.WriteAllText(Path.Combine(directory,"result.txt"),"PASS: actual step/jump/land routing in 1 and 4 viewports; synthetic devices; human listening pending\n");
            Debug.Log("NATIVE_MOVEMENT_AUDIO_REVIEW_PASS");Application.Quit();
        }
    }
}
#endif

#endif
