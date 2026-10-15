using System;
using UnityEngine;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    // UserPreferenceCatalog v1: preference bounds, deliberately outside match/Lab state.
    public static class NativeUserPreferences
    {
        public const string ScaleKey="StarTournament.Settings.UiScalePercent",ContrastKey="StarTournament.Settings.HighContrast";
        public const string FrameCapKey="StarTournament.Graphics.FrameCap",AutoKey="StarTournament.Graphics.AutoResolution";
        public static readonly NumericDescriptor ScaleDescriptor=new NumericDescriptor {Path="preferences.uiScalePercent",Label="Масштаб интерфейса",Group="interface",Unit="%",Minimum=75,Maximum=150,Step=5,DefaultValue=100};
        // Discrete presentation rates, not a simulation rate or gameplay balance parameter.
        public static readonly int[] FrameCaps={-1,30,60,90,120,144,165,240};
        public static int Scale=>Mathf.RoundToInt(Snap(ScaleDescriptor,PlayerPrefs.GetInt(ScaleKey,100)));
        public static bool HighContrast=>PlayerPrefs.GetInt(ContrastKey,0)!=0;
        public static bool AutoResolution=>PlayerPrefs.GetInt(AutoKey,PlayerPrefs.HasKey("StarTournament.Graphics.Width")?0:1)!=0;
        public static int FrameCap {get {int v=PlayerPrefs.GetInt(FrameCapKey,-1);return Array.IndexOf(FrameCaps,v)>=0?v:-1;}}
        public static float Snap(NumericDescriptor d,float value)
        {if(float.IsNaN(value)||float.IsInfinity(value))value=d.DefaultValue;return Mathf.Clamp(d.Minimum+Mathf.Round((Mathf.Clamp(value,d.Minimum,d.Maximum)-d.Minimum)/d.Step)*d.Step,d.Minimum,d.Maximum);}
        public static NumericDescriptor Deadzone(ProvingProfile p)
        {var d=p.Descriptor("input.deadzone");return new NumericDescriptor {Path="preferences.deadzonePercent",Label="Мёртвая зона стика",Group="input",Unit="%",Minimum=d.Minimum*100,Maximum=d.Maximum*100,Step=d.Step*100,DefaultValue=p.Get(d.Path)*100};}
        public static void SetScale(float v){PlayerPrefs.SetInt(ScaleKey,Mathf.RoundToInt(Snap(ScaleDescriptor,v)));PlayerPrefs.Save();}
        public static void SetContrast(bool v){PlayerPrefs.SetInt(ContrastKey,v?1:0);PlayerPrefs.Save();}
        public static void CycleFrameCap(){int i=Array.IndexOf(FrameCaps,FrameCap);PlayerPrefs.SetInt(FrameCapKey,FrameCaps[(i+1)%FrameCaps.Length]);PlayerPrefs.Save();Application.targetFrameRate=FrameCap;}
        public static void Reset(Action<string> delete=null,Action save=null)
        {
            delete=delete??PlayerPrefs.DeleteKey;save=save??PlayerPrefs.Save;
            // Explicit allow-list: never erase identities, progress, Lab or statistics consent.
            foreach(var k in new[]{ScaleKey,ContrastKey,FrameCapKey,MouseSensitivityPreference.Key,FpsDisplay.PreferenceKey,
                "StarTournament.Graphics.VSync","StarTournament.Graphics.Shadows",NativeVisualPreferences.AntialiasingKey,
                "StarTournament.Audio.MusicPercent","StarTournament.Audio.EffectsPercent",NativeAudioPreferences.MasterKey,NativeAudioPreferences.VoiceKey,
                NativeBotReactionPreferences.TextKey,NativeBotReactionPreferences.VoiceKey})delete(k);
            GamepadLookSettings.ResetGeneral(delete);
            foreach(VisualEffect e in Enum.GetValues(typeof(VisualEffect)))
            {delete(NativeVisualPreferences.Key(e)+".Enabled");delete(NativeVisualPreferences.Key(e)+".Strength");}
            foreach(var path in NativeVisualPreferences.ColorAdjustmentPaths)delete("StarTournament.Graphics."+path);
            save();Application.targetFrameRate=FrameCap;
        }
    }

    // Keeps team/status colors intact: accessibility adds dark edges instead of recoloring them.
    public sealed class NativeInterfacePreferences : MonoBehaviour
    {
        Vector2 originalReference;CanvasScaler scaler;int scale=-1;bool contrast,initialized;float nextRefresh;
        void Awake(){scaler=GetComponent<CanvasScaler>();if(scaler)originalReference=scaler.referenceResolution;}
        void LateUpdate()
        {
            if(scaler&&scale!=NativeUserPreferences.Scale){scale=NativeUserPreferences.Scale;scaler.referenceResolution=originalReference/(scale/100f);}
            bool requested=NativeUserPreferences.HighContrast;
            if(initialized&&requested==contrast&&(!contrast||Time.unscaledTime<nextRefresh))return;
            contrast=requested;initialized=true;nextRefresh=Time.unscaledTime+.5f;
            var texts=GetComponentsInChildren<Text>(true);
            foreach(var text in texts)
            {
                var outline=text.GetComponent<NativeContrastOutline>();
                if(contrast&&!outline)outline=text.gameObject.AddComponent<NativeContrastOutline>();
                if(outline){outline.enabled=contrast;outline.effectColor=Color.black;outline.effectDistance=new Vector2(1,-1);}
            }
        }
    }
    public sealed class NativeContrastOutline : Outline { }
}
