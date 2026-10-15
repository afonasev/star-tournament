using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    // Existing fixtures predate these keys. Protect real user values across focused runs.
    [SetUpFixture]
    public sealed class AudioChannelPreferenceIsolation
    {
        readonly Dictionary<string,int?> saved=new Dictionary<string,int?>();
        [OneTimeSetUp] public void Isolate()
        {
            foreach(var key in new[]{NativeAudioPreferences.MasterKey,NativeAudioPreferences.VoiceKey})
            {saved[key]=PlayerPrefs.HasKey(key)?PlayerPrefs.GetInt(key):(int?)null;PlayerPrefs.SetInt(key,100);}
        }
        [OneTimeTearDown] public void Restore()
        {
            foreach(var entry in saved)if(entry.Value.HasValue)PlayerPrefs.SetInt(entry.Key,entry.Value.Value);else PlayerPrefs.DeleteKey(entry.Key);
            PlayerPrefs.Save();
        }
    }
}
