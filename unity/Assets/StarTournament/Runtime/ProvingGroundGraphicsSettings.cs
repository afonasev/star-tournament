using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingGround
    {
        NativeVisualPresentation visualPresentation;
        void SetVisualEnabled(VisualEffect effect,bool value){NativeVisualPreferences.SetEnabled(effect,value);visualPresentation?.Apply();RefreshSettingsUi();}
        void SetVisualStrength(VisualEffect effect,float value){NativeVisualPreferences.SetStrength(Profile,effect,value);visualPresentation?.Apply();RefreshSettingsUi();}
        void SetColorAdjustment(string path,float value){NativeVisualPreferences.SetAdjustment(Profile,path,value);visualPresentation?.Apply();RefreshSettingsUi();}
        void SetAntialiasingLevel(int value){NativeVisualPreferences.SetAntialiasingLevel(value);visualPresentation?.Apply();RefreshSettingsUi();}
        void SetGraphicsQuality(int value){NativeVisualPreferences.SetQuality(value);ApplyQualityPreference();RefreshSettingsUi();}
        void ApplyQualityPreference()
        {NativeVisualPreferences.ApplyQuality();ApplyVSyncPreference();ApplyShadowPreference();Application.targetFrameRate=NativeUserPreferences.FrameCap;visualPresentation?.Apply();}
        enum SettingsSection { Image, Control, Interface, Audio, Graphics }
        const string DisplayWidthKey="StarTournament.Graphics.Width";
        const string DisplayHeightKey="StarTournament.Graphics.Height";
        const string DisplayModeKey="StarTournament.Graphics.Fullscreen";
        const string VSyncKey="StarTournament.Graphics.VSync";
        const string ShadowsKey="StarTournament.Graphics.Shadows";
        const float DisplayConfirmationSeconds=12f;
        SettingsView settingsView,displayOwner;
        readonly SettingsView[] seatSettingsViews=new SettingsView[SeatInputCoordinator.SeatCount];
        readonly Dictionary<Light,LightShadows> originalLightShadows=new Dictionary<Light,LightShadows>();
        ShadowQuality originalShadowQuality;
        int originalVSyncCount;
        int originalFrameCap;
        int originalQualityLevel,originalQualityAa;
        bool originalRealtimeReflections;
        bool vSyncEnabled;
        bool shadowsEnabled=true,graphicsPreferencesInitialized,displayConfirmationActive;
        float displayConfirmationDeadline;
        int displayChangeFrame,oldDisplayWidth,oldDisplayHeight,pendingDisplayWidth,pendingDisplayHeight;
        bool pendingDisplayAuto;
        FullScreenMode oldDisplayMode,pendingDisplayMode;

        void CreateSettingsUi(Transform parent)
        {settingsView=new SettingsView(this,false,-1,CloseSettings);settingsView.CreateSettingsUi(parent);settingsScreen=settingsView.Root;}
        void RefreshSettingsUi()
        {settingsView?.RefreshSettingsUi();foreach(var view in seatSettingsViews)view?.RefreshSettingsUi();}
        static FullScreenMode EffectiveDisplayMode() => Screen.fullScreenMode==FullScreenMode.Windowed?FullScreenMode.Windowed:FullScreenMode.FullScreenWindow;
        static List<Vector2Int> SupportedResolutions()
        {
            var result=new List<Vector2Int>();
            foreach(var mode in Screen.resolutions)
            {
                var size=new Vector2Int(mode.width,mode.height);
                if(size.x>0&&size.y>0&&!result.Contains(size))result.Add(size);
            }
            if(result.Count==0)result.Add(new Vector2Int(Screen.width,Screen.height));
            result.Sort((a,b)=>a.x*a.y==b.x*b.y?a.x.CompareTo(b.x):(long)a.x*a.y<(long)b.x*b.y?-1:1);
            return result;
        }
        void BeginDisplayChange(SettingsView view,int width,int height,FullScreenMode mode,bool auto=false)
        {
            if(displayConfirmationActive)return;
            displayOwner=view;
            oldDisplayWidth=Screen.width;oldDisplayHeight=Screen.height;oldDisplayMode=Screen.fullScreenMode;
            pendingDisplayWidth=width;pendingDisplayHeight=height;pendingDisplayMode=mode;
            pendingDisplayAuto=auto;
            displayChangeFrame=Time.frameCount;displayConfirmationDeadline=Time.unscaledTime+DisplayConfirmationSeconds;
            displayConfirmationActive=true;Screen.SetResolution(width,height,mode);
            RefreshSettingsUi();view.FinishDisplay();
        }
        void TickDisplayConfirmation()
        {if(!displayConfirmationActive)return;if(Time.unscaledTime>=displayConfirmationDeadline){RollbackDisplay();return;}displayOwner.RefreshSettingsUi();}
        void ConfirmDisplay(SettingsView view)
        {
            if(!displayConfirmationActive||displayOwner!=view||Time.frameCount<=displayChangeFrame)return;
            displayConfirmationActive=false;
            PlayerPrefs.SetInt(DisplayWidthKey,Screen.width);PlayerPrefs.SetInt(DisplayHeightKey,Screen.height);
            PlayerPrefs.SetInt(NativeUserPreferences.AutoKey,pendingDisplayAuto?1:0);
            PlayerPrefs.SetInt(DisplayModeKey,Screen.fullScreenMode==FullScreenMode.Windowed?0:1);PlayerPrefs.Save();
            RefreshSettingsUi();view.FinishDisplay();displayOwner=null;
        }
        void RollbackDisplay()
        {
            if(!displayConfirmationActive)return;
            var view=displayOwner;displayConfirmationActive=false;
            Screen.SetResolution(oldDisplayWidth,oldDisplayHeight,oldDisplayMode);
            RefreshSettingsUi();view?.FinishDisplay();displayOwner=null;
        }
        void UpdateSettingsBackInput()
        {
            if(phase!=Phase.Settings)return;
            bool back=TryTakeMenuBack(input.DeviceAt(settingsSeat) as Gamepad,settingsSeat>=0);
            if(back){settingsView.Back();lastAudioSelection=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;}
            else if(settingsSeat>=0){if(input.DeviceAt(settingsSeat) is Gamepad assigned&&assigned.buttonNorth.wasPressedThisFrame)settingsView.ResetShortcut();}
            else if(MenuUiDevice is Gamepad pad&&pad.buttonNorth.wasPressedThisFrame)settingsView.ResetShortcut();
        }
        void InitializeGraphicsPreferences()
        {
            originalShadowQuality=QualitySettings.shadows;
            originalVSyncCount=QualitySettings.vSyncCount;
            originalFrameCap=Application.targetFrameRate;
            originalQualityLevel=QualitySettings.GetQualityLevel();originalQualityAa=QualitySettings.antiAliasing;originalRealtimeReflections=QualitySettings.realtimeReflectionProbes;
            int savedVSync=PlayerPrefs.GetInt(VSyncKey,-1);
            vSyncEnabled=savedVSync!=0;
            ApplyVSyncPreference();
            graphicsPreferencesInitialized=true;
            shadowsEnabled=PlayerPrefs.GetInt(ShadowsKey,1)!=0;
            ApplyQualityPreference();
            Application.targetFrameRate=NativeUserPreferences.FrameCap;
            int width=PlayerPrefs.GetInt(DisplayWidthKey,0),height=PlayerPrefs.GetInt(DisplayHeightKey,0);
            if(NativeUserPreferences.AutoResolution){var desktop=Screen.currentResolution;width=desktop.width;height=desktop.height;}
            if(width<=0||height<=0)return;
            if(!SupportedResolutions().Contains(new Vector2Int(width,height)))return;
            var mode=PlayerPrefs.GetInt(DisplayModeKey,1)==0?FullScreenMode.Windowed:FullScreenMode.FullScreenWindow;
            if(Screen.width!=width||Screen.height!=height||EffectiveDisplayMode()!=mode)Screen.SetResolution(width,height,mode);
        }
        void SetVSync(bool enabled)
        {
            vSyncEnabled=enabled;PlayerPrefs.SetInt(VSyncKey,enabled?1:0);PlayerPrefs.Save();ApplyVSyncPreference();RefreshSettingsUi();
        }
        void ApplyVSyncPreference()
        {
            // API modes: zero disables synchronization; one presents every display refresh.
            // These are technical mode identifiers, not artistic Balance Lab tuning.
            QualitySettings.vSyncCount=vSyncEnabled?1:0;
        }
        void ToggleShadows()
        {
            shadowsEnabled=!shadowsEnabled;
            PlayerPrefs.SetInt(ShadowsKey,shadowsEnabled?1:0);PlayerPrefs.Save();
            ApplyShadowPreference();RefreshSettingsUi();
        }
        void ApplyShadowPreference()
        {
            if(!graphicsPreferencesInitialized)return;
            QualitySettings.shadows=shadowsEnabled?ShadowQuality.All:ShadowQuality.Disable;
            foreach(var light in GetComponentsInChildren<Light>(true))
            {
                if(!originalLightShadows.ContainsKey(light))originalLightShadows[light]=light.shadows;
                light.shadows=shadowsEnabled?originalLightShadows[light]:LightShadows.None;
            }
        }
        void RestoreGraphicsQuality()
        {
            if(graphicsPreferencesInitialized){QualitySettings.SetQualityLevel(originalQualityLevel,true);QualitySettings.antiAliasing=originalQualityAa;QualitySettings.realtimeReflectionProbes=originalRealtimeReflections;QualitySettings.shadows=originalShadowQuality;QualitySettings.vSyncCount=originalVSyncCount;Application.targetFrameRate=originalFrameCap;}
        }
    }
}
