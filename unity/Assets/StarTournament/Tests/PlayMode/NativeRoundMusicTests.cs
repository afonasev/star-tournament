using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class NativeRoundMusicTests
    {
        [UnityTest] public IEnumerator ProgressLoopsPauseRestoreOvertimeAndFinishPreserveOneTrack()
        {
            float old=AudioListener.volume;AudioListener.volume=0;
            var root=new GameObject("music-lifecycle-test");var music=new NativeRoundMusic(root.transform);
            try
            {
                var profile=ProvingProfile.CreateDefault();var mp=ProvingProfile.CreateMatchDefault();
                var config=NativeMatchConfiguration.Default(mp);config.DurationMinutes=1;
                var match=new NativeMatchState(2,config,mp,60);music.Bind(match,profile);string track=music.TrackId;
                var sources=root.GetComponentsInChildren<AudioSource>();
                music.Tick(true,.02f);music.UpdateVolume(1,1);yield return new WaitForSeconds(.15f);
                // Move near loop end to exercise the real streaming two-source overlap.
                var manifest=JsonUtility.FromJson<RoundMusicManifest>(Resources.Load<TextAsset>("Audio/Music/manifest").text);
                var phrase=manifest.tracks.Single(t=>t.id==track).phases[0];
                sources.Single(s=>s.isPlaying).time=phrase.loopEnd-60/phrase.bpm;
                music.Tick(true,.02f);music.UpdateVolume(1,1);
                for(int i=0;i<100;i++){music.Tick(true,.025f);music.UpdateVolume(1,1);Assert.That(sources.Sum(s=>s.volume),Is.LessThanOrEqualTo(1.00001f));yield return null;}
                Assert.That(music.TrackId,Is.EqualTo(track));Assert.That(music.Phase,Is.EqualTo(0));
                music.Tick(false,.02f);var positions=sources.Select(s=>s.time).ToArray();yield return new WaitForSecondsRealtime(.1f);
                for(int i=0;i<2;i++)Assert.That(sources[i].time,Is.EqualTo(positions[i]).Within(.025f));
                var snapshot=match.Read();snapshot.Tick=1800;match.Restore(snapshot);music.Restore();
                Assert.That(music.TrackId,Is.EqualTo(track));Assert.That(music.Phase,Is.EqualTo(1));
                snapshot=match.Read();snapshot.Phase=NativeMatchPhase.Overtime;snapshot.RemainingTicks=0;snapshot.Trigger="time-limit";match.Restore(snapshot);music.Restore();
                music.Tick(true,.02f);Assert.That(music.Phase,Is.EqualTo(2));
                snapshot=match.Read();snapshot.Phase=NativeMatchPhase.Finished;snapshot.AwardsFrozen=true;match.Restore(snapshot);
                for(int i=0;i<110;i++){music.Tick(false,.025f);music.UpdateVolume(1,1);}
                Assert.That(sources.All(s=>!s.isPlaying&&s.volume==0),Is.True);
            }
            finally{music.Dispose();Object.Destroy(root);AudioListener.volume=old;}
        }
        [UnityTest] public IEnumerator MusicIsIndependentOfEffectsAndPauseMuteMenuAndRepeatHaveOneOutput()
        {
            const string musicKey="StarTournament.Audio.MusicPercent",effectsKey="StarTournament.Audio.EffectsPercent";
            bool hadMusic=PlayerPrefs.HasKey(musicKey),hadEffects=PlayerPrefs.HasKey(effectsKey);
            int oldMusic=PlayerPrefs.GetInt(musicKey),oldEffects=PlayerPrefs.GetInt(effectsKey);float listener=AudioListener.volume;
            var root=new GameObject("music-routing-test");var profile=ProvingProfile.CreateDefault();NativeGameAudio audio=null;
            AudioListener.volume=0;
            try
            {
                NativeAudioPreferences.SetMusic(100);NativeAudioPreferences.SetEffects(0);
                audio=new NativeGameAudio(root.transform,profile,false);
                var matchProfile=ProvingProfile.CreateMatchDefault();
                var config=NativeMatchConfiguration.Default(matchProfile);
                NativeMatchState Match()=>new NativeMatchState(2,config,matchProfile,60);
                var current=Match();
                void Tick(bool running,float dt=.016f){audio.MusicScene.Tick(false,current,profile,!running,dt);audio.UpdateVolume();}
                Tick(true);yield return null;
                audio.UpdateVolume();
                var sources=root.GetComponentsInChildren<AudioSource>().Where(s=>s.name.Contains("-music-")).ToArray();
                Assert.That(sources.Length,Is.EqualTo(4));Assert.That(sources.Sum(s=>s.volume),Is.GreaterThan(0));
                Assert.That(sources.All(s=>s.pitch==1),Is.True,"Tempo is prepared offline without live pitch shift");
                Tick(false);Assert.That(audio.Music.Paused,Is.True);Assert.That(sources.Sum(s=>s.volume),Is.EqualTo(0));
                Tick(true);yield return null;Assert.That(audio.Music.Paused,Is.False);
                NativeAudioPreferences.SetEffects(100);
                var play=typeof(NativeGameAudio).GetMethod("Play",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                for(int i=0;i<4;i++)play.Invoke(audio,new object[]{"rifle",1f,-1});
                audio.UpdateVolume();
                var effects=root.GetComponentsInChildren<AudioSource>().Where(s=>!s.name.Contains("-music-")&&s.isPlaying).ToArray();
                Assert.That(effects.Length,Is.EqualTo(4));
                Assert.That(sources.Sum(s=>s.volume),Is.GreaterThan(0),"Concurrent loud SFX must not completely erase the musical theme");
                Assert.That(effects.Sum(s=>s.volume)+sources.Sum(s=>s.volume),Is.LessThanOrEqualTo(1.00001f));
                NativeAudioPreferences.SetMusic(0);audio.UpdateVolume();
                Assert.That(effects.All(s=>Mathf.Abs(s.volume-.25f)<.00001f),Is.True,"Music=0 preserves the previous SFX-only mix");
                NativeAudioPreferences.SetMusic(100);Tick(false);
                Assert.That(effects.All(s=>Mathf.Abs(s.volume-.25f)<.00001f),Is.True,"Pause preserves the previous SFX-only mix");
                Tick(true);
                NativeAudioPreferences.SetMusic(0);NativeAudioPreferences.SetEffects(100);audio.UpdateVolume();
                Assert.That(sources.Sum(s=>s.volume),Is.EqualTo(0));
                NativeAudioPreferences.SetMusic(100);audio.SetDiagnosticMute(true);Assert.That(sources.Sum(s=>s.volume),Is.EqualTo(0));
                audio.SetDiagnosticMute(false);audio.UpdateVolume();Assert.That(sources.Sum(s=>s.volume),Is.GreaterThan(0));
                string prior=audio.Music.TrackId;var state=Random.state;
                for(int i=0;i<12;i++)
                {
                    current=Match();for(int step=0;step<65;step++)Tick(true,.25f);
                    Assert.That(audio.Music.TrackId,Is.Not.EqualTo(prior));prior=audio.Music.TrackId;
                }
                Assert.That(JsonUtility.ToJson(Random.state),Is.EqualTo(JsonUtility.ToJson(state)));
                audio.MusicScene.Tick(true,null,profile,false,4);audio.UpdateVolume();
                Assert.That(sources.Where(s=>s.name.StartsWith("round-music-")).All(s=>!s.isPlaying&&s.clip==null),Is.True);
                Assert.That(sources.Where(s=>s.name.StartsWith("menu-music-")).Sum(s=>s.volume),Is.GreaterThan(0));
            }
            finally
            {
                audio?.Dispose();Object.Destroy(root);AudioListener.volume=listener;
                if(hadMusic)PlayerPrefs.SetInt(musicKey,oldMusic);else PlayerPrefs.DeleteKey(musicKey);
                if(hadEffects)PlayerPrefs.SetInt(effectsKey,oldEffects);else PlayerPrefs.DeleteKey(effectsKey);PlayerPrefs.Save();
            }
        }
    }
}
