#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
namespace StarTournament.ProvingGround
{
    // Synthetic native Player smoke, muted by default. Peak sums are conservative
    // source-volume bounds. A listener filter also retains the real native mix before
    // silencing its output; this does not replace physical/listening acceptance.
    public sealed class NativeMusicReview : MonoBehaviour
    {
        ProvingGround ground;NativeGameAudio audio;string directory;
        readonly List<Gamepad> pads=new List<Gamepad>();readonly List<string> observations=new List<string>();
        const string MusicKey="StarTournament.Audio.MusicPercent",EffectsKey="StarTournament.Audio.EffectsPercent";
        bool hadMusic,hadEffects,restore;int oldMusic,oldEffects;
        InputSettings.BackgroundBehavior oldBackground;
        AudioSource[] sources;
        void Check(bool success,string message)
        {
            if(!success)
            {
                File.WriteAllLines(Path.Combine(directory,"failure.txt"),observations.Concat(new[]{"FAIL "+message}));
                AudioListener.volume=0;Application.Quit(1);throw new InvalidOperationException(message);
            }
            observations.Add("PASS "+message);
        }
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-musicReview");
            if(at<0||at+1>=args.Length||!Path.IsPathFullyQualified(args[at+1]))throw new ArgumentException("Absolute music review evidence directory required");
            directory=args[at+1];Directory.CreateDirectory(directory);ground=GetComponent<ProvingGround>();
            Application.runInBackground=true;oldBackground=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            hadMusic=PlayerPrefs.HasKey(MusicKey);hadEffects=PlayerPrefs.HasKey(EffectsKey);
            oldMusic=PlayerPrefs.GetInt(MusicKey);oldEffects=PlayerPrefs.GetInt(EffectsKey);restore=true;
            audio=(NativeGameAudio)typeof(ProvingGround).GetField("gameAudio",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ground);
            audio.SetDiagnosticMute(false);
            for(int seat=0;seat<4;seat++)pads.Add(InputSystem.AddDevice<Gamepad>());
            foreach(int seats in new[]{1,4})
            {
                ground.StartCombatReview(pads.Take(seats).ToArray(),ensureOpponent:seats==1);
                // Keep this presentation fixture neutral: bot scoring must not finish a synthetic overtime.
                typeof(ProvingGround).GetProperty("BotDriver").SetValue(ground,null);
                var capture=ground.GetComponentInChildren<AudioListener>().gameObject.AddComponent<NativeMusicCapture>();
                capture.Begin();AudioListener.volume=1;
                NativeAudioPreferences.SetMusic(100);NativeAudioPreferences.SetEffects(0);
                yield return new WaitForSecondsRealtime(.4f);
                sources=ground.transform.Find("native-game-audio").GetComponentsInChildren<AudioSource>().Where(s=>s.name.StartsWith("round-music-")).ToArray();
                Check(sources.Length==2&&sources.Sum(s=>s.volume)>0,seats+" viewports: music plays with Effects=0, two shared sources");
                Check(audio.Music.Phase==0,seats+" viewports: calm start");yield return Capture(seats+"-calm");
                var match=ground.Session.Match;var snapshot=match.Read();
                double hz=ground.Profile.Get("simulation.fixedTickHz");snapshot.Tick=(long)(match.Configuration.DurationMinutes*60*hz*.48);
                match.Restore(snapshot);yield return new WaitForSecondsRealtime(3);
                Check(audio.Music.Phase==1,seats+" viewports: middle phase from duration progress");yield return Capture(seats+"-drive");
                var pause=typeof(ProvingGround).GetMethods(BindingFlags.NonPublic|BindingFlags.Instance).Single(m=>m.Name=="Pause"&&m.GetParameters().Length==1);
                pause.Invoke(ground,new object[]{"Проверка музыки"});yield return null;
                var positions=sources.Select(s=>s.time).ToArray();yield return new WaitForSecondsRealtime(.2f);
                Check(audio.Music.Paused&&sources.Select((s,i)=>Math.Abs(s.time-positions[i])<.05f).All(v=>v),seats+" viewports: pause freezes music");
                typeof(ProvingGround).GetMethod("Resume",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(ground,null);
                yield return new WaitForSecondsRealtime(.2f);Check(!audio.Music.Paused,seats+" viewports: resume continues");
                NativeAudioPreferences.SetMusic(0);NativeAudioPreferences.SetEffects(100);yield return null;
                Check(sources.Sum(s=>s.volume)==0,seats+" viewports: Music=0 independently mutes");
                NativeAudioPreferences.SetMusic(100);audio.SetDiagnosticMute(true);yield return null;
                Check(sources.Sum(s=>s.volume)==0,seats+" viewports: diagnostic mute includes music");audio.SetDiagnosticMute(false);
                snapshot=match.Read();snapshot.Phase=NativeMatchPhase.Overtime;snapshot.Tick=(long)(match.Configuration.DurationMinutes*60*hz);snapshot.RemainingTicks=0;snapshot.Trigger="time-limit";
                for(int i=0;i<snapshot.Standings.Length;i++)snapshot.Standings[i].Score=0;
                match.Restore(snapshot);yield return new WaitForSecondsRealtime(3);
                Check(audio.Music.Phase==2,seats+" viewports: overtime retains final phase");yield return Capture(seats+"-climax");
                var play=typeof(NativeGameAudio).GetMethod("Play",BindingFlags.NonPublic|BindingFlags.Instance);
                for(int participant=0;participant<seats;participant++)play.Invoke(audio,new object[]{"rifle",1f,participant});
                audio.UpdateVolume();
                float sum=ground.transform.Find("native-game-audio").GetComponentsInChildren<AudioSource>().Where(s=>s.isPlaying).Sum(s=>s.volume);
                Check(sum<=1.0001f,seats+" viewports: conservative Music+SFX peak budget <=1");
                Check(sources.Sum(s=>s.volume)>0,seats+" viewports: music remains audible with concurrent SFX=100");
                yield return new WaitForSecondsRealtime(.25f);
                snapshot=match.Read();snapshot.Phase=NativeMatchPhase.Finished;snapshot.Winner=0;match.Restore(snapshot);
                yield return new WaitForSecondsRealtime(2.5f);
                Check(sources.All(s=>!s.isPlaying&&s.volume==0),seats+" viewports: finish fades out");
                string previous=audio.Music.TrackId;
                typeof(ProvingGround).GetMethod("Repeat",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(ground,null);yield return new WaitForSecondsRealtime(.3f);
                Check(audio.Music.Phase==0&&audio.Music.TrackId!=previous,seats+" viewports: Repeat starts a different random concept");
                try{observations.Add("PASS "+seats+" viewports: "+capture.Save(Path.Combine(directory,seats+"-native-mix.wav")));}
                catch(Exception error){Check(false,error.Message);throw;}
                AudioListener.volume=0;
                typeof(ProvingGround).GetMethod("Menu",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(ground,null);yield return null;
                Check(sources.All(s=>!s.isPlaying&&s.clip==null),seats+" viewports: menu is music-free");
            }
            File.WriteAllLines(Path.Combine(directory,"result.txt"),observations.Concat(new[]{"DIAGNOSTIC: synthetic state progression/devices with bot driver disabled; human listening, physical devices, performance and broader waveform clipping acceptance pending."}));
            Debug.Log("NATIVE_MUSIC_REVIEW_PASS");AudioListener.volume=0;Application.Quit();
        }
        IEnumerator Capture(string label)
        {
            File.WriteAllText(Path.Combine(directory,label+".txt"),"track="+audio.Music.TrackId+" phase="+audio.Music.Phase+" clips="+string.Join(",",sources.Select(s=>s.clip?s.clip.name:"none")));
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,label+".png"));yield return new WaitForSecondsRealtime(.15f);
        }
        void OnDestroy()
        {
            foreach(var pad in pads)if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);
            if(!restore)return;InputSystem.settings.backgroundBehavior=oldBackground;
            if(hadMusic)PlayerPrefs.SetInt(MusicKey,oldMusic);else PlayerPrefs.DeleteKey(MusicKey);
            if(hadEffects)PlayerPrefs.SetInt(EffectsKey,oldEffects);else PlayerPrefs.DeleteKey(EffectsKey);PlayerPrefs.Save();
        }
    }
}
#endif
