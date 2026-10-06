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
                File.WriteAllLines(Path.Combine(directory,"failure.txt"),observations.Concat(new[]{"FAIL "+message,MusicDiagnostic()}));
                AudioListener.volume=0;Application.Quit(1);throw new InvalidOperationException(message);
            }
            observations.Add("PASS "+message);
        }
        string MusicDiagnostic()
        {
            if(audio==null)return "music not initialized";
            var scene=audio.MusicScene;
            return "STATE "+ground.CombatReviewDiagnostic()+" match="+ground.Session.Match?.Phase+
                " boundCurrent="+(scene.BoundMatch==ground.Session.Match)+" menu="+scene.MenuWeight+
                " round="+scene.RoundWeight+" progress="+scene.TransitionProgress+" suspended="+scene.Suspended+
                " roundPaused="+audio.Music.Paused+" roundOutput="+audio.Music.HasOutput+" gainSum="+audio.Music.GainSum+" finishSeconds="+
                typeof(NativeRoundMusic).GetField("finishSeconds",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(audio.Music)+
                " finishing="+typeof(NativeRoundMusic).GetField("finishing",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(audio.Music)+
                " menuPaused="+scene.Menu.Paused+" menuGainSum="+scene.Menu.GainSum+
                " finishElapsed="+
                typeof(NativeRoundMusic).GetField("finishElapsed",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(audio.Music)+
                " voices="+string.Join(";",sources.Select(s=>s.name+":playing="+s.isPlaying+",volume="+s.volume+",clip="+(s.clip?s.clip.name:"null")+",time="+s.time));
        }
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-musicReview");
            if(at<0||at+1>=args.Length||!Path.IsPathFullyQualified(args[at+1]))throw new ArgumentException("Absolute music review evidence directory required");
            directory=args[at+1];Directory.CreateDirectory(directory);ground=GetComponent<ProvingGround>();
            Application.runInBackground=true;oldBackground=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            // This explicitly synthetic review keeps presentation clocks running in the background.
            typeof(ProvingGround).GetField("nativeInputReview",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ground,true);
            hadMusic=PlayerPrefs.HasKey(MusicKey);hadEffects=PlayerPrefs.HasKey(EffectsKey);
            oldMusic=PlayerPrefs.GetInt(MusicKey);oldEffects=PlayerPrefs.GetInt(EffectsKey);restore=true;
            audio=(NativeGameAudio)typeof(ProvingGround).GetField("gameAudio",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ground);
            audio.SetDiagnosticMute(false);
            NativeAudioPreferences.SetMusic(100);NativeAudioPreferences.SetEffects(0);
            yield return new WaitForSecondsRealtime(.2f);
            sources=ground.transform.Find("native-game-audio").GetComponentsInChildren<AudioSource>().Where(s=>s.name.Contains("-music-")).ToArray();
            Check(sources.Length==4&&sources.Where(s=>s.name.StartsWith("menu-music-")).Sum(s=>s.volume)>0,"main menu: one Arena theme, four shared transport voices");
            yield return Capture("main-menu");
            var menuCapture=ground.GetComponentInChildren<AudioListener>().gameObject.AddComponent<NativeMusicCapture>();
            menuCapture.Begin();AudioListener.volume=1;yield return new WaitForSecondsRealtime(60);
            Check(audio.MusicScene.MenuWeight==1,"menu: uninterrupted two-loop native capture");
            try{observations.Add("PASS "+menuCapture.Save(Path.Combine(directory,"menu-loop-native.wav")));}
            catch(Exception error){Check(false,error.Message);throw;}
            AudioListener.volume=0;Destroy(menuCapture);yield return null;
            for(int seat=0;seat<4;seat++)pads.Add(InputSystem.AddDevice<Gamepad>());
            foreach(int seats in new[]{1,4})
            foreach(int concept in new[]{0,1,2,3})
            {
                ground.StartCombatReview(pads.Take(seats).ToArray(),ensureOpponent:seats==1);
                audio.MusicScene.Tick(false,ground.Session.Match,ground.Profile,false,0);
                audio.Music.ReviewTrack(concept);
                string label=seats+"-track-"+concept;
                // Keep this presentation fixture neutral: bot scoring must not finish a synthetic overtime.
                typeof(ProvingGround).GetProperty("BotDriver").SetValue(ground,null);
                var capture=ground.GetComponentInChildren<AudioListener>().gameObject.AddComponent<NativeMusicCapture>();
                capture.Begin();AudioListener.volume=1;
                NativeAudioPreferences.SetMusic(100);NativeAudioPreferences.SetEffects(0);
                yield return new WaitForSecondsRealtime(.4f);
                sources=ground.transform.Find("native-game-audio").GetComponentsInChildren<AudioSource>().Where(s=>s.name.Contains("-music-")).ToArray();
                Check(sources.Length==4&&sources.Sum(s=>s.volume)>0,seats+" viewports: music plays with Effects=0, four shared transport voices");
                Check(audio.MusicScene.MenuWeight>0&&audio.MusicScene.RoundWeight>0,seats+" viewports: actual menu/match overlap");
                Check(audio.Music.Phase==0,label+" viewports: calm start");yield return Capture(label+"-calm");
                var match=ground.Session.Match;var snapshot=match.Read();
                double hz=ground.Profile.Get("simulation.fixedTickHz");snapshot.Tick=(long)(match.Configuration.DurationMinutes*60*hz*.48);
                match.Restore(snapshot);yield return new WaitForSecondsRealtime(3);
                Check(audio.Music.Phase==1,label+" viewports: middle phase from duration progress");yield return Capture(label+"-drive");
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
                Check(audio.Music.Phase==2,label+" viewports: overtime retains final phase");yield return Capture(label+"-climax");
                var play=typeof(NativeGameAudio).GetMethod("Play",BindingFlags.NonPublic|BindingFlags.Instance);
                for(int participant=0;participant<seats;participant++)play.Invoke(audio,new object[]{"rifle",1f,participant});
                audio.UpdateVolume();
                float sum=ground.transform.Find("native-game-audio").GetComponentsInChildren<AudioSource>().Where(s=>s.isPlaying).Sum(s=>s.volume);
                Check(sum<=1.0001f,seats+" viewports: conservative Music+SFX peak budget <=1");
                Check(sources.Sum(s=>s.volume)>0,seats+" viewports: music remains audible with concurrent SFX=100");
                yield return new WaitForSecondsRealtime(.25f);
                snapshot=match.Read();snapshot.Phase=NativeMatchPhase.Finished;snapshot.Winner=0;match.Restore(snapshot);
                yield return new WaitForSecondsRealtime(2.5f);
                observations.Add(MusicDiagnostic());
                Check(sources.All(s=>!s.isPlaying&&s.volume==0),seats+" viewports: finish fades out");
                string previous=audio.Music.TrackId;
                typeof(ProvingGround).GetMethod("Repeat",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(ground,null);yield return new WaitForSecondsRealtime(.3f);
                Check(audio.Music.Phase==0&&audio.Music.TrackId!=previous,seats+" viewports: Repeat starts a different random concept");
                try{observations.Add("PASS "+label+" viewports: "+capture.Save(Path.Combine(directory,label+"-native-mix.wav")));}
                catch(Exception error){Check(false,error.Message);throw;}
                AudioListener.volume=0;
                Destroy(capture);yield return null;
                // Cover return from an audible late phrase of this exact concept as well as
                // Results/Repeat. Keep the old listener muted across camera replacement.
                audio.Music.ReviewTrack(concept);
                var repeated=ground.Session.Match;var late=repeated.Read();late.Phase=NativeMatchPhase.Overtime;
                late.Tick=(long)(repeated.Configuration.DurationMinutes*60*hz);late.RemainingTicks=0;late.Trigger="time-limit";
                for(int i=0;i<late.Standings.Length;i++)late.Standings[i].Score=0;
                repeated.Restore(late);yield return new WaitForSecondsRealtime(4.5f);
                Check(audio.Music.Phase==2&&audio.MusicScene.RoundWeight==1,label+": active late theme before return");
                typeof(ProvingGround).GetMethod("Menu",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(ground,null);
                typeof(ProvingGround).GetField("nativeInputReview",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ground,true);
                var returnCapture=ground.GetComponentInChildren<AudioListener>().gameObject.AddComponent<NativeMusicCapture>();
                returnCapture.Begin();AudioListener.volume=1;yield return new WaitForSecondsRealtime(8);
                Check(sources.Where(s=>s.name.StartsWith("round-music-")).All(s=>!s.isPlaying&&s.clip==null),seats+" viewports: outgoing round released after return");
                Check(sources.Where(s=>s.name.StartsWith("menu-music-")).Sum(s=>s.volume)>0,seats+" viewports: menu Arena theme returns");
                try{observations.Add("PASS "+returnCapture.Save(Path.Combine(directory,label+"-return-native.wav")));}
                catch(Exception error){Check(false,error.Message);throw;}
                AudioListener.volume=0;Destroy(returnCapture);yield return null;
                yield return Capture(label+"-menu-return");
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

#endif
