using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class NativeMusicTests
    {
        [Test] public void DurationProgressAndOvertimeSelectIntensityWithoutScoreOrSimulationMutation()
        {
            Assert.That(NativeRoundMusic.Intensity(60,60,NativeMatchPhase.Running,.33f,.67f),Is.EqualTo(0));
            Assert.That(NativeRoundMusic.Intensity(35,60,NativeMatchPhase.Running,.33f,.67f),Is.EqualTo(1));
            Assert.That(NativeRoundMusic.Intensity(10,60,NativeMatchPhase.Running,.33f,.67f),Is.EqualTo(2));
            Assert.That(NativeRoundMusic.Intensity(900,1800,NativeMatchPhase.Running,.33f,.67f),Is.EqualTo(1));
            Assert.That(NativeRoundMusic.Intensity(0,1800,NativeMatchPhase.Overtime,.33f,.67f),Is.EqualTo(2));
        }
        [Test] public void AllFourTracksHaveThreeStreamingPhrasesAndIncreasingPreparedTempo()
        {
            var manifest=JsonUtility.FromJson<RoundMusicManifest>(Resources.Load<TextAsset>("Audio/Music/manifest").text);
            Assert.That(manifest.tracks.Length,Is.EqualTo(4));
            Assert.That(manifest.tracks.Select(t=>t.id),Is.Unique);
            foreach(var track in manifest.tracks)
            {
                Assert.That(track.phases.Length,Is.EqualTo(3));
                float last=0;
                foreach(var phrase in track.phases)
                {
                    Assert.That(phrase.bpm,Is.GreaterThan(last));last=phrase.bpm;
                    var clip=Resources.Load<AudioClip>(phrase.resource);Assert.That(clip,Is.Not.Null);
                    Assert.That(clip.loadType,Is.EqualTo(AudioClipLoadType.Streaming));
                    Assert.That(phrase.loopStart,Is.GreaterThan(0));Assert.That(phrase.loopEnd,Is.LessThan(clip.length));
                    Assert.That(phrase.loopEnd-phrase.loopStart,Is.GreaterThan(8*60/phrase.bpm));
                }
            }
        }
        [Test] public void SelectedMenuSourceHasStreamingBeatAlignedLoopAndFourSecondProfile()
        {
            var manifest=JsonUtility.FromJson<MenuMusicManifest>(Resources.Load<TextAsset>("Audio/Music/menu-manifest").text);
            var clip=Resources.Load<AudioClip>(manifest.resource);
            Assert.That(manifest.id,Is.EqualTo("menu-arena"));Assert.That(clip,Is.Not.Null);
            Assert.That(clip.loadType,Is.EqualTo(AudioClipLoadType.Streaming));
            Assert.That(manifest.loopStart,Is.GreaterThan(manifest.loopFadeSeconds));
            Assert.That(manifest.loopEnd,Is.LessThan(clip.length));
            Assert.That((manifest.loopEnd-manifest.loopStart)*manifest.bpm/60,Is.EqualTo(48).Within(.01));
            var profile=ProvingProfile.CreateDefault();Assert.That(profile.Get("audio.music.menuTransitionSeconds"),Is.EqualTo(4));
            Assert.That(profile.Descriptor("audio.music.menuTransitionSeconds").Group,Is.EqualTo("music"));
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] public void PreviousLabRevisionMigratesWithoutRewritingOriginalHashOrChosenValues(int pulseGeneration)
        {
            var shipped=DesignLabHistoryTests.Shipped();
            var prior=typeof(ProvingProfile).GetMethod("BeforeRoundMusic",BindingFlags.NonPublic|BindingFlags.Instance);
            var before=new LabBundle{Profiles=shipped.Profiles.Select(p=>(ProvingProfile)prior.Invoke(p,null)).Select(p=>pulseGeneration==1?p.BeforePulseVisibility():pulseGeneration==2?p.BeforePulseRefinement():p).ToList()};
            string path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".json");
            try
            {
                var history=new DesignLabHistory(path,before);history.Create("Saved music predecessor");
                var draft=history.Selected.Snapshot;draft.Set("audio.effectsDefaultPercent",45);draft.Set("pulseFx.fireCount",13);history.Save(draft);
                string hash=history.Selected.Hash;
                var migrated=new DesignLabHistory(path,shipped);Assert.That(migrated.StorageError,Is.Null);
                Assert.That(migrated.Selected.Snapshot.Get("audio.effectsDefaultPercent"),Is.EqualTo(45));
                Assert.That(migrated.Selected.Snapshot.Get("audio.music.gain"),Is.EqualTo(.55f));
                Assert.That(migrated.Selected.Snapshot.Get("pulseFx.fireCount"),Is.EqualTo(13));
                Assert.That(migrated.Profiles.SelectMany(p=>p.Revisions).Any(r=>r.Hash==hash),Is.True);
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
        [Test] public void MusicBalanceMigrationPreservesHistoricalHashAndCustomMusicGain()
        {
            var shipped=DesignLabHistoryTests.Shipped();
            var method=typeof(ProvingProfile).GetMethod("BeforeMusicBalance",BindingFlags.NonPublic|BindingFlags.Instance);
            var before=new LabBundle{Profiles=shipped.Profiles.Select(p=>(ProvingProfile)method.Invoke(p,null)).ToList()};
            string path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".json");
            try
            {
                var history=new DesignLabHistory(path,before);history.Create("Saved audio balance predecessor");
                var draft=history.Selected.Snapshot;draft.Set("audio.music.gain",.35f);draft.Set("audio.effectsDefaultPercent",45);history.Save(draft);
                string hash=history.Selected.Hash;
                var migrated=new DesignLabHistory(path,shipped);Assert.That(migrated.StorageError,Is.Null);
                Assert.That(migrated.Selected.Snapshot.Get("audio.music.balanceGain"),Is.EqualTo(.32f));
                Assert.That(migrated.Selected.Snapshot.Get("audio.music.gain"),Is.EqualTo(.35f));
                Assert.That(migrated.Selected.Snapshot.Get("audio.effectsDefaultPercent"),Is.EqualTo(45));
                Assert.That(migrated.Profiles.SelectMany(p=>p.Revisions).Any(r=>r.Hash==hash),Is.True);
                var reopened=new DesignLabHistory(path,shipped);Assert.That(reopened.StorageError,Is.Null);
                Assert.That(reopened.Selected.Hash,Is.EqualTo(migrated.Selected.Hash));
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
        [Test] public void RoundMusicPredecessorMigratesMenuSettingAndPreservesSavedMixAndHash()
        {
            var shipped=DesignLabHistoryTests.Shipped();
            var method=typeof(ProvingProfile).GetMethod("BeforeMenuMusic",BindingFlags.NonPublic|BindingFlags.Instance);
            var before=new LabBundle{Profiles=shipped.Profiles.Select(p=>(ProvingProfile)method.Invoke(p,null)).ToList()};
            string path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".json");
            try
            {
                var history=new DesignLabHistory(path,before);history.Create("Saved round-only music");
                var draft=history.Selected.Snapshot;draft.Set("audio.music.gain",.35f);draft.Set("audio.music.transitionBeats",6);history.Save(draft);
                string hash=history.Selected.Hash;
                var migrated=new DesignLabHistory(path,shipped);Assert.That(migrated.StorageError,Is.Null);
                Assert.That(migrated.Selected.Snapshot.Get("audio.music.gain"),Is.EqualTo(.35f));
                Assert.That(migrated.Selected.Snapshot.Get("audio.music.transitionBeats"),Is.EqualTo(6));
                Assert.That(migrated.Selected.Snapshot.Get("audio.music.menuTransitionSeconds"),Is.EqualTo(4));
                Assert.That(migrated.Profiles.SelectMany(p=>p.Revisions).Any(r=>r.Hash==hash),Is.True);
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
    }
}
