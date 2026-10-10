using UnityEngine;

namespace StarTournament.ProvingGround
{
    /// <summary>Installation-wide presentation preferences, independent of participant profiles.</summary>
    public static class NativeBotReactionPreferences
    {
        public const string TextKey="StarTournament.BotReactions.Text";
        public const string VoiceKey="StarTournament.BotReactions.Voice";
        public static bool TextEnabled=>PlayerPrefs.GetInt(TextKey,1)!=0;
        public static bool VoiceEnabled=>PlayerPrefs.GetInt(VoiceKey,0)!=0;
        public static void SetText(bool value){PlayerPrefs.SetInt(TextKey,value?1:0);PlayerPrefs.Save();}
        public static void SetVoice(bool value){PlayerPrefs.SetInt(VoiceKey,value?1:0);PlayerPrefs.Save();}
    }
}
