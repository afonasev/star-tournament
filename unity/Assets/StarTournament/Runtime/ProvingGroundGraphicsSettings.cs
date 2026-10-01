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
        enum SettingsSection { Image, Control, Interface, Audio }
        const string DisplayWidthKey="StarTournament.Graphics.Width";
        const string DisplayHeightKey="StarTournament.Graphics.Height";
        const string DisplayModeKey="StarTournament.Graphics.Fullscreen";
        const string ShadowsKey="StarTournament.Graphics.Shadows";
        const float DisplayConfirmationSeconds=12f;
        SettingsView settingsView,displayOwner;
        readonly SettingsView[] seatSettingsViews=new SettingsView[SeatInputCoordinator.SeatCount];
        readonly Dictionary<Light,LightShadows> originalLightShadows=new Dictionary<Light,LightShadows>();
        ShadowQuality originalShadowQuality;
        bool shadowsEnabled=true,graphicsPreferencesInitialized,displayConfirmationActive;
        float displayConfirmationDeadline;
        int displayChangeFrame,oldDisplayWidth,oldDisplayHeight,pendingDisplayWidth,pendingDisplayHeight;
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
        void BeginDisplayChange(SettingsView view,int width,int height,FullScreenMode mode)
        {
            if(displayConfirmationActive)return;
            displayOwner=view;
            oldDisplayWidth=Screen.width;oldDisplayHeight=Screen.height;oldDisplayMode=Screen.fullScreenMode;
            pendingDisplayWidth=width;pendingDisplayHeight=height;pendingDisplayMode=mode;
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
            bool back=Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame;
            if(settingsSeat>=0){if(input.DeviceAt(settingsSeat) is Gamepad pad)back|=pad.buttonEast.wasPressedThisFrame;}
            else foreach(var pad in Gamepad.all)back|=pad.buttonEast.wasPressedThisFrame;
            if(back){settingsView.Back();lastAudioSelection=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;}
        }
        void InitializeGraphicsPreferences()
        {
            originalShadowQuality=QualitySettings.shadows;
            graphicsPreferencesInitialized=true;
            shadowsEnabled=PlayerPrefs.GetInt(ShadowsKey,1)!=0;
            ApplyShadowPreference();
            int width=PlayerPrefs.GetInt(DisplayWidthKey,0),height=PlayerPrefs.GetInt(DisplayHeightKey,0);
            if(width<=0||height<=0)return;
            if(!SupportedResolutions().Contains(new Vector2Int(width,height)))return;
            var mode=PlayerPrefs.GetInt(DisplayModeKey,1)==0?FullScreenMode.Windowed:FullScreenMode.FullScreenWindow;
            if(Screen.width!=width||Screen.height!=height||EffectiveDisplayMode()!=mode)Screen.SetResolution(width,height,mode);
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
        void RestoreShadowQuality()
        {
            if(graphicsPreferencesInitialized)QualitySettings.shadows=originalShadowQuality;
        }
    }
}
