using UnityEngine;

namespace StarTournament.ProvingGround
{
    // The same shared headroom as the legacy mix; user channels have independent gains.
    public readonly struct NativeAudioChannelMix
    {
        public readonly float EffectsScale,VoiceScale,MusicBudget;
        NativeAudioChannelMix(float effects,float voice,float music)
        {EffectsScale=effects;VoiceScale=voice;MusicBudget=music;}

        public static NativeAudioChannelMix Calculate(float effectsSum,float voiceSum,float effects,float voice,float music,float master)
        {
            float limiter=1/Mathf.Max(1,effectsSum+voiceSum);
            float effectScale=effects*limiter,voiceScale=voice*limiter;
            float attenuation=music>0?1/Mathf.Max(1,effectsSum*effectScale+voiceSum*voiceScale+music):1;
            float output=attenuation*Mathf.Clamp01(master);
            return new NativeAudioChannelMix(effectScale*output,voiceScale*output,music*output);
        }
    }
}
