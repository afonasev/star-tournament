using System;
using UnityEngine;
namespace StarTournament.ProvingGround
{
    public enum VisualEffect { Lighting, Materials, AmbientOcclusion, Bloom, Reflections, ColorGrading }
    public static class NativeVisualPreferences
    {
        public const string QualityKey="StarTournament.Graphics.Quality";
        public const int DefaultQuality=2;
        public static readonly string[] QualityNames={"Низкое","Среднее","Высокое"};
        static readonly int[] QualityTiers={0,2,5};
        public static int Quality {get{int value=PlayerPrefs.GetInt(QualityKey,DefaultQuality);return value>=0&&value<QualityTiers.Length?value:DefaultQuality;}}
        public static int QualityTier(int value)=>QualityTiers[value>=0&&value<QualityTiers.Length?value:DefaultQuality];
        public static void SetQuality(int value){PlayerPrefs.SetInt(QualityKey,value>=0&&value<QualityTiers.Length?value:DefaultQuality);PlayerPrefs.Save();}
        public static void ApplyQuality()
        {
            int tier=QualityTier(Quality);
            if(tier>=QualitySettings.names.Length)throw new InvalidOperationException("Required native quality tier is missing");
            int aa=QualitySettings.antiAliasing;bool reflections=QualitySettings.realtimeReflectionProbes;
            QualitySettings.SetQualityLevel(tier,true);
            QualitySettings.antiAliasing=aa;QualitySettings.realtimeReflectionProbes=reflections;
        }
        public const string AntialiasingKey="StarTournament.Graphics.Antialiasing";
        public const int AntialiasingOff=0,AntialiasingLow=1,AntialiasingMedium=2,AntialiasingHigh=3;
        public const int DefaultAntialiasing=AntialiasingMedium;
        public static readonly string[] AntialiasingNames={"Выкл.","SMAA · низкое","SMAA · среднее","SMAA · высокое"};
        public static readonly string[] Names={"Объёмный свет","Отклик материалов","Затемнение стыков (AO)","Свечение (Bloom)","Отражения окружения","Нейтральная цветокоррекция"};
        public const string ExposurePath="visual.ColorGradingExposure";
        public const string SaturationPath="visual.ColorGradingSaturation";
        public static readonly string[] ColorAdjustmentPaths={ExposurePath,SaturationPath};
        public static int AntialiasingLevel
        {
            get{int value=PlayerPrefs.GetInt(AntialiasingKey,DefaultAntialiasing);return value>=AntialiasingOff&&value<=AntialiasingHigh?value:DefaultAntialiasing;}
        }
        public static void SetAntialiasingLevel(int value)
        {PlayerPrefs.SetInt(AntialiasingKey,value>=AntialiasingOff&&value<=AntialiasingHigh?value:DefaultAntialiasing);PlayerPrefs.Save();}
        public static float Adjustment(ProvingProfile profile,string path)
        {
            var d=profile.Descriptor(path);float value=PlayerPrefs.GetFloat("StarTournament.Graphics."+path,profile.Get(path));
            return Validated(profile,d,value);
        }
        public static void SetAdjustment(ProvingProfile profile,string path,float value)
        {
            PlayerPrefs.SetFloat("StarTournament.Graphics."+path,Validated(profile,profile.Descriptor(path),value));PlayerPrefs.Save();
        }
        static float Validated(ProvingProfile profile,NumericDescriptor d,float value)
        {
            if(float.IsNaN(value)||float.IsInfinity(value))value=profile.Get(d.Path);
            return Mathf.Clamp(Mathf.Round(value/d.Step)*d.Step,d.Minimum,d.Maximum);
        }
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
