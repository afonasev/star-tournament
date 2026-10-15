using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class NativeAudioPreferenceTests
    {
        readonly Dictionary<string,int?> saved=new Dictionary<string,int?>();
        const string Music="StarTournament.Audio.MusicPercent",Effects="StarTournament.Audio.EffectsPercent";
        ProvingProfile profile;
        [SetUp] public void Setup()
        {
            profile=ProvingProfile.CreateDefault();
            foreach(var key in new[]{Music,Effects,NativeAudioPreferences.MasterKey,NativeAudioPreferences.VoiceKey})
            {saved[key]=PlayerPrefs.HasKey(key)?PlayerPrefs.GetInt(key):(int?)null;PlayerPrefs.DeleteKey(key);}
        }
        [TearDown] public void Restore()
        {
            foreach(var p in saved)if(p.Value.HasValue)PlayerPrefs.SetInt(p.Key,p.Value.Value);else PlayerPrefs.DeleteKey(p.Key);
            PlayerPrefs.Save();
        }
        [Test] public void ReadingDefaultsDoesNotMigrateOrWritePreferences()
        {
            Assert.That(NativeAudioPreferences.Master,Is.EqualTo(100));
            Assert.That(NativeAudioPreferences.Voice(profile),Is.EqualTo(NativeAudioPreferences.Effects(profile)));
            foreach(var key in saved.Keys)Assert.That(PlayerPrefs.HasKey(key),Is.False,key);
        }
        [Test] public void FirstEffectsChangePreservesThePreviousSharedVoiceLevel()
        {
            PlayerPrefs.SetInt(Effects,35);
            Assert.That(NativeAudioPreferences.Voice(profile),Is.EqualTo(35));
            NativeAudioPreferences.SetEffects(60,profile);
            Assert.That(NativeAudioPreferences.Effects(profile),Is.EqualTo(60));
            Assert.That(NativeAudioPreferences.Voice(profile),Is.EqualTo(35));
            Assert.That(PlayerPrefs.GetInt(NativeAudioPreferences.VoiceKey),Is.EqualTo(35));
            NativeAudioPreferences.SetEffects(0,profile);
            Assert.That(NativeAudioPreferences.Voice(profile),Is.EqualTo(35));
        }
        [Test] public void ZeroAndClampingAreIndependentPersistedPreferences()
        {
            NativeAudioPreferences.SetVoice(0);NativeAudioPreferences.SetMaster(0);NativeAudioPreferences.SetMusic(0);NativeAudioPreferences.SetEffects(0,profile);
            Assert.That(NativeAudioPreferences.Master,Is.Zero);
            Assert.That(NativeAudioPreferences.Voice(profile),Is.Zero);Assert.That(NativeAudioPreferences.Music(profile),Is.Zero);Assert.That(NativeAudioPreferences.Effects(profile),Is.Zero);
            NativeAudioPreferences.SetMaster(200);NativeAudioPreferences.SetVoice(-5);
            Assert.That(NativeAudioPreferences.Master,Is.EqualTo(100));Assert.That(NativeAudioPreferences.Voice(profile),Is.Zero);
            Assert.That(PlayerPrefs.GetInt(NativeAudioPreferences.VoiceKey),Is.Zero);
        }
        [TestCase(0f,0f,0f)] [TestCase(.4f,.3f,.6f)] [TestCase(3f,2f,.8f)]
        public void EqualChannelsPreserveThePreviousNormalizedMix(float sfx,float voice,float music)
        {
            float oldScale=.8f/Mathf.Max(1,sfx+voice);
            float attenuation=music>0?1/Mathf.Max(1,(sfx+voice)*oldScale+music):1;
            var mix=NativeAudioChannelMix.Calculate(sfx,voice,.8f,.8f,music,1);
            Assert.That(mix.EffectsScale,Is.EqualTo(oldScale*attenuation).Within(.000001));
            Assert.That(mix.VoiceScale,Is.EqualTo(oldScale*attenuation).Within(.000001));
            Assert.That(mix.MusicBudget,Is.EqualTo(music*attenuation).Within(.000001));
        }
        [Test] public void ZeroEffectsLeavesSpeechAndMusicAndMasterScalesTheEntireOutput()
        {
            var mix=NativeAudioChannelMix.Calculate(0,.8f,0,.65f,.2f,1);
            Assert.That(mix.EffectsScale,Is.Zero);Assert.That(mix.VoiceScale,Is.GreaterThan(0));Assert.That(mix.MusicBudget,Is.GreaterThan(0));
            var half=NativeAudioChannelMix.Calculate(0,.8f,0,.65f,.2f,.5f);
            Assert.That(half.VoiceScale,Is.EqualTo(mix.VoiceScale*.5f));Assert.That(half.MusicBudget,Is.EqualTo(mix.MusicBudget*.5f));
            var silent=NativeAudioChannelMix.Calculate(2,3,1,1,1,0);
            Assert.That(silent.EffectsScale+silent.VoiceScale+silent.MusicBudget,Is.Zero);
            Assert.That(mix.VoiceScale*.8f+mix.MusicBudget,Is.LessThanOrEqualTo(1));
        }
    }
}
