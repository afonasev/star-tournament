using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class NativeMenuMusicTests
    {
        [UnityTest] public IEnumerator MenuLoopsTransitionsFreezeReverseAndCoalesceWithoutAdditionalVoices()
        {
            float listener=AudioListener.volume;AudioListener.volume=0;
            var root=new GameObject("menu-music-lifecycle-test");var profile=ProvingProfile.CreateDefault();
            var music=new NativeMusicCoordinator(root.transform,profile);
            try
            {
                var mp=ProvingProfile.CreateMatchDefault();var config=NativeMatchConfiguration.Default(mp);
                NativeMatchState Match()=>new NativeMatchState(2,config,mp,60);
                void Tick(bool menu,NativeMatchState match,bool suspended,float dt)
                {
                    music.Tick(menu,match,profile,suspended,dt);music.UpdateVolume(1,1);
                    Assert.That(root.GetComponentsInChildren<AudioSource>().Sum(s=>s.volume),Is.LessThanOrEqualTo(.55001f));
                }
                Tick(true,null,false,.02f);yield return new WaitForSecondsRealtime(.15f);
                var sources=root.GetComponentsInChildren<AudioSource>();Assert.That(sources.Length,Is.EqualTo(4));
                var menuSources=sources.Where(s=>s.name.StartsWith("menu-music-")).ToArray();
                Assert.That(menuSources.Sum(s=>s.volume),Is.GreaterThan(0));
                float initialPosition=menuSources.Single(s=>s.isPlaying).time;
                Tick(true,null,false,.02f);yield return null;
                Assert.That(menuSources.Single(s=>s.isPlaying).time,Is.GreaterThanOrEqualTo(initialPosition));
                var first=Match();Tick(false,first,false,1);yield return new WaitForSecondsRealtime(.1f);
                string firstTrack=music.Round.TrackId;Assert.That(music.MenuWeight,Is.GreaterThan(0));Assert.That(music.RoundWeight,Is.GreaterThan(0));
                float progress=music.TransitionProgress;Tick(false,first,true,20);
                var positions=sources.Select(s=>s.time).ToArray();yield return new WaitForSecondsRealtime(.1f);
                Assert.That(music.TransitionProgress,Is.EqualTo(progress));Assert.That(sources.Sum(s=>s.volume),Is.EqualTo(0));
                for(int i=0;i<sources.Length;i++)Assert.That(sources[i].time,Is.EqualTo(positions[i]).Within(.025f));
                Tick(false,first,false,.02f);Assert.That(music.Round.TrackId,Is.EqualTo(firstTrack));
                Tick(true,null,false,.2f);Assert.That(music.TransitionProgress,Is.LessThan(progress));
                var discarded=Match();var latest=Match();Tick(false,discarded,false,.02f);
                Assert.That(music.BoundMatch,Is.SameAs(first),"Audible round is not rebound during reversal");
                Tick(false,latest,false,4);Assert.That(music.BoundMatch,Is.Null);
                var random=Random.state;Tick(false,latest,false,.02f);
                Assert.That(music.BoundMatch,Is.SameAs(latest));Assert.That(music.Round.TrackId,Is.Not.EqualTo(firstTrack));
                Assert.That(JsonUtility.ToJson(Random.state),Is.EqualTo(JsonUtility.ToJson(random)));
                string latestTrack=music.Round.TrackId;music.Restore(first);Assert.That(music.Round.TrackId,Is.EqualTo(latestTrack));
                Tick(false,latest,false,4);Assert.That(music.MenuWeight,Is.EqualTo(0));
                Assert.That(menuSources.All(s=>!s.isPlaying&&s.volume==0),Is.True);
                Tick(true,null,false,4);Assert.That(music.BoundMatch,Is.Null);
                Assert.That(sources.Where(s=>s.name.StartsWith("round-music-")).All(s=>!s.isPlaying&&s.clip==null),Is.True);
                var manifest=JsonUtility.FromJson<MenuMusicManifest>(Resources.Load<TextAsset>("Audio/Music/menu-manifest").text);
                menuSources.Single(s=>s.isPlaying).time=manifest.loopEnd-manifest.loopFadeSeconds*.5f;
                Tick(true,null,false,.02f);Assert.That(menuSources.Count(s=>s.isPlaying),Is.EqualTo(2));
                Tick(true,null,true,8);float[] loopPositions=menuSources.Select(s=>s.time).ToArray();
                yield return new WaitForSecondsRealtime(.1f);
                for(int i=0;i<2;i++)Assert.That(menuSources[i].time,Is.EqualTo(loopPositions[i]).Within(.025f));
                Tick(true,null,false,manifest.loopFadeSeconds);Assert.That(menuSources.Count(s=>s.isPlaying),Is.EqualTo(1));
                Assert.That(root.GetComponentsInChildren<AudioSource>().Length,Is.EqualTo(4));
                Assert.That(sources.All(s=>s.pitch==1),Is.True);
            }
            finally{music.Dispose();Object.Destroy(root);AudioListener.volume=listener;}
        }
        [UnityTest] public IEnumerator PausedOrFinishedRoundReturnsFromSilenceAndNewMatchNeverInheritsOldEnvelope()
        {
            float listener=AudioListener.volume;AudioListener.volume=0;
            var root=new GameObject("menu-silent-origin-test");var profile=ProvingProfile.CreateDefault();
            var music=new NativeMusicCoordinator(root.transform,profile);
            try
            {
                var mp=ProvingProfile.CreateMatchDefault();var config=NativeMatchConfiguration.Default(mp);
                NativeMatchState Match()=>new NativeMatchState(2,config,mp,60);
                var first=Match();music.Tick(false,first,profile,false,4);music.UpdateVolume(1,1);yield return null;
                var sources=root.GetComponentsInChildren<AudioSource>();
                // The focus callback must freeze immediately, even without another Update.
                music.SetSuspended(true);music.UpdateVolume(1,1);
                var positions=sources.Select(s=>s.time).ToArray();yield return new WaitForSecondsRealtime(.1f);
                for(int i=0;i<sources.Length;i++)Assert.That(sources[i].time,Is.EqualTo(positions[i]).Within(.025f));
                music.Tick(true,null,profile,false,.02f);music.UpdateVolume(1,1);
                Assert.That(music.RoundWeight,Is.EqualTo(0));Assert.That(music.MenuWeight,Is.LessThan(.02f));
                Assert.That(sources.Where(s=>s.name.StartsWith("round-music-")).All(s=>!s.isPlaying&&s.volume==0),Is.True);
                var second=Match();music.Tick(false,second,profile,false,4);music.UpdateVolume(1,1);
                var snapshot=second.Read();snapshot.Phase=NativeMatchPhase.Finished;second.Restore(snapshot);
                music.Tick(false,second,profile,false,3);music.UpdateVolume(1,1);
                Assert.That(music.Round.HasOutput,Is.False);
                music.Tick(true,null,profile,false,.1f);music.UpdateVolume(1,1);
                Assert.That(music.MenuWeight,Is.LessThan(.05f));
                music.Tick(false,Match(),profile,false,.02f);music.UpdateVolume(1,1);
                Assert.That(music.RoundWeight,Is.LessThan(.02f),"New match fades from silence, not the old finished weight");
                music.Tick(true,null,profile,false,4);music.UpdateVolume(1,1);
                profile.Set("audio.music.gain",0);music.Tick(true,null,profile,false,.02f);music.UpdateVolume(1,1);
                Assert.That(sources.Sum(s=>s.volume),Is.EqualTo(0),"Applied Lab mix also changes persistent menu gain");
            }
            finally{music.Dispose();Object.Destroy(root);AudioListener.volume=listener;}
        }
    }
}
