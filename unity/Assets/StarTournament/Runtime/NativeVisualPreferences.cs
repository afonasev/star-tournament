using System;
using UnityEngine;
namespace StarTournament.ProvingGround
{
    public enum VisualEffect { Lighting, Materials, AmbientOcclusion, Bloom, Reflections }
    public static class NativeVisualPreferences
    {
        public static readonly string[] Names={"Объёмный свет","Отклик материалов","Затемнение стыков (AO)","Свечение (Bloom)","Отражения окружения"};
        public static string Path(VisualEffect effect)=>"visual."+effect+"Percent";
        public static string Key(VisualEffect effect)=>"StarTournament.Graphics.Visual."+effect;
        public static bool Enabled(VisualEffect effect)=>PlayerPrefs.GetInt(Key(effect)+".Enabled",1)!=0;
        public static float Strength(ProvingProfile profile,VisualEffect effect)
        {
            var d=profile.Descriptor(Path(effect));var value=PlayerPrefs.GetFloat(Key(effect)+".Strength",profile.Get(d.Path));
            if(float.IsNaN(value)||float.IsInfinity(value))value=profile.Get(d.Path);
            return Mathf.Clamp(Mathf.Round(value/d.Step)*d.Step,d.Minimum,d.Maximum);
        }
        public static float Effective(ProvingProfile profile,VisualEffect effect)=>Enabled(effect)?Strength(profile,effect)/100f:0;
        public static void SetEnabled(VisualEffect effect,bool value){PlayerPrefs.SetInt(Key(effect)+".Enabled",value?1:0);PlayerPrefs.Save();}
        public static void SetStrength(ProvingProfile profile,VisualEffect effect,float value)
        {
            var d=profile.Descriptor(Path(effect));if(float.IsNaN(value)||float.IsInfinity(value))value=profile.Get(d.Path);
            PlayerPrefs.SetFloat(Key(effect)+".Strength",Mathf.Clamp(Mathf.Round(value/d.Step)*d.Step,d.Minimum,d.Maximum));PlayerPrefs.Save();
        }
    }
}
