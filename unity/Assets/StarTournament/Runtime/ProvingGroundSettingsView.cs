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
        // Each entry point uses this view; only the identity and focus owner differ.
        sealed class SettingsView
        {
            SettingsSection settingsSection;
            readonly Button[] settingsSectionButtons=new Button[4];
            Button settingsBackButton,settingsModeButton,settingsResolutionButton;
            Button resolutionCancelButton,settingsRevertButton;

            readonly Dictionary<Selectable,GameObject> settingsFocusBorders=new Dictionary<Selectable,GameObject>();
            readonly List<Button> settingsFocusButtons=new List<Button>();
            GameObject settingsImagePage,settingsControlPage,settingsInterfacePage,settingsAudioPage,settingsConfirmation,resolutionPicker;
            Slider settingsMusicSlider,settingsEffectsSlider;
            Text settingsDescription,settingsDisplayMode,settingsResolution,settingsShadows,settingsConfirmationText;
            Button settingsConfirmButton;
            Button[] resolutionChoices;
            ScrollRect resolutionScroll,deviceScroll; RectTransform deviceContent;
            RectTransform helpDiagram;
            bool resolutionPickerOpen;
            Slider settingsMouseSlider,settingsHorizontalSlider,settingsVerticalSlider,settingsReturnDelaySlider;
            Toggle settingsAutoLevelToggle,settingsFpsToggle,settingsShadowsToggle,settingsBotTextToggle,settingsBotVoiceToggle;
            GameObject settingsHelpPage;
            Button settingsHelpButton,settingsHelpBack;
            bool settingsHelpOpen;
            GameObject settingsDevicesPage;
            Button settingsDevicesButton,settingsDevicesBack;
            readonly List<Text> settingsDeviceRows=new List<Text>();
            Text settingsDevicesPageLabel;
            bool settingsDevicesOpen;

            GameObject settingsScreen;
            Text settingsHeading;

            readonly ProvingGround owner;
            public int Seat=-1;
            public string ProfileId;
            public bool InMatch;
            public GameObject Root=>settingsScreen;
            public Selectable Selected { get; private set; }
            readonly UnityEngine.Events.UnityAction close;
            ScrollRect settingsHelpScroll;
            Text settingsHelpText;
            Button settingsHelpKeyboard,settingsHelpGamepad;
            int settingsSeat=>Seat;
            string settingsProfileId=>ProfileId;
            bool displayConfirmationActive=>owner.displayConfirmationActive&&owner.displayOwner==this;
            bool shadowsEnabled=>owner.shadowsEnabled;
            int displayChangeFrame=>owner.displayChangeFrame;
            float displayConfirmationDeadline=>owner.displayConfirmationDeadline;
            int pendingDisplayWidth=>owner.pendingDisplayWidth;
            int pendingDisplayHeight=>owner.pendingDisplayHeight;
            FullScreenMode pendingDisplayMode=>owner.pendingDisplayMode;
            Button displayReturnButton;
            public SettingsView(ProvingGround owner,bool inMatch,int seat,UnityEngine.Events.UnityAction close)
            {this.owner=owner;InMatch=inMatch;Seat=seat;this.close=close;}
            public void Open(int seat=-1,string profileId=null)
            {Seat=seat;ProfileId=profileId;settingsSection=SettingsSection.Image;settingsHelpOpen=false;settingsDevicesOpen=false;resolutionPickerOpen=false;RefreshSettingsUi();ConfigureSettingsNavigation();Select(settingsSectionButtons[0]);}
            public void Close()
            {if(displayConfirmationActive)owner.RollbackDisplay();resolutionPickerOpen=false;settingsHelpOpen=false;settingsDevicesOpen=false;close();}
            void CloseSettings()=>Close();
            ProvingProfile Profile=>owner.Profile;
            PlayerProfileCatalog playerProfiles=>owner.playerProfiles;
            string IdentityLabel(int seat)=>owner.IdentityLabel(seat);
            void Select(Selectable control)
            {Selected=control;control?.GetComponent<MenuScrollFocus>()?.Reveal();for(int i=0;i<settingsSectionButtons.Length;i++)if(control==settingsSectionButtons[i])PreviewSettingsSection((SettingsSection)i);if(!InMatch)ProvingGround.Select(control);RefreshSettingsFocus();}
            public void Navigate(int x,int y,bool submit,bool back)
            {
                if(back){Back();return;}
                if(Selected==null||!Selected.IsActive()||!Selected.IsInteractable())Select(displayConfirmationActive?settingsRevertButton:FirstSettingsAction());
                if(settingsDevicesOpen&&x!=0&&Selected==settingsDevicesBack){deviceScroll.verticalNormalizedPosition=Mathf.Clamp01(deviceScroll.verticalNormalizedPosition-x*.25f);return;}
                if(Selected is SettingsSlider slider&&x!=0)slider.value+=x*slider.Step;
                else if(x!=0||y!=0)
                {
                    var nav=Selected.navigation;var next=y>0?nav.selectOnUp:y<0?nav.selectOnDown:x<0?nav.selectOnLeft:nav.selectOnRight;
                    if(next!=null&&next.IsActive()&&next.IsInteractable()){Select(next);owner.gameAudio?.MenuMove();}
                }
                if(submit&&Selected.IsInteractable())
                {if(Selected is Button button)button.onClick.Invoke();else if(Selected is Toggle toggle)toggle.isOn=!toggle.isOn;owner.gameAudio?.MenuConfirm();}
            }
            public void Back()
            {
                owner.gameAudio?.MenuBack();
                if(displayConfirmationActive)owner.RollbackDisplay();
                else if(resolutionPickerOpen)CloseResolutionPicker();
                else if(settingsHelpOpen)SetHelp(false);
                else if(settingsDevicesOpen)SetDevices(false);
                else
                {
                    var current=InMatch?Selected:EventSystem.current?EventSystem.current.currentSelectedGameObject?.GetComponent<Selectable>():null;
                    bool inNavigation=current==settingsBackButton;
                    foreach(var button in settingsSectionButtons)inNavigation|=current==button;
                    if(inNavigation)Close();else Select(settingsSectionButtons[(int)settingsSection]);
                }
            }
            public void FinishDisplay()
            {RefreshSettingsUi();Select(displayConfirmationActive?settingsConfirmButton:displayReturnButton??settingsModeButton);}
            void BeginDisplayChange(int width,int height,FullScreenMode mode)=>owner.BeginDisplayChange(this,width,height,mode);
            void ConfirmDisplay()=>owner.ConfirmDisplay(this);
            void RollbackDisplay(){if(displayConfirmationActive)owner.RollbackDisplay();}
            void ToggleShadows()=>owner.ToggleShadows();
            GameObject Panel(Transform p,string n,Vector2 min,Vector2 max,Color color)=>owner.Panel(p,n,min,max,color);
            Text Label(Transform p,string n,string t,int f,Vector2 min,Vector2 max,TextAnchor a,Color color)=>owner.Label(p,n,t,f,min,max,a,color);
            Button MenuButton(Transform p,string n,string t,Vector2 min,Vector2 max,UnityEngine.Events.UnityAction action)=>owner.MenuButton(p,n,t,min,max,action);
            Slider NumericSetting(Transform p,string n,string t,Vector2 min,Vector2 max,NumericDescriptor d,UnityEngine.Events.UnityAction<float> action)=>owner.NumericSetting(p,n,t,min,max,d,action);
            Toggle BooleanSetting(Transform p,string n,string t,Vector2 min,Vector2 max,UnityEngine.Events.UnityAction<bool> action)=>owner.BooleanSetting(p,n,t,min,max,action);
            GamepadLookSettings MenuGamepadLook()=>ProfileId!=null?GamepadLookSettings.Resolve(Profile,playerProfiles.Find(ProfileId)):Seat<0?GamepadLookSettings.General(Profile):owner.PersonalGamepadLook(Seat);
            float MenuMouseSensitivity()=>ProfileId!=null?playerProfiles.Find(ProfileId).MouseDegreesPerPixel:Seat<0?MouseSensitivityPreference.Resolve(Profile):owner.PersonalSensitivity(Seat);
            bool MenuFps()=>ProfileId!=null?playerProfiles.Find(ProfileId).ShowFps:Seat<0?owner.fps.Visible:owner.PersonalFps(Seat);
            void SetMenuGamepad(int axis,float value,bool enabled=false)
            {var look=MenuGamepadLook();if(axis==0)look.Horizontal=value;else if(axis==1)look.Vertical=value;else if(axis==3)look.ReturnDelay=value;else look.AutoLevel=enabled;
             if(ProfileId!=null)playerProfiles.SetGamepad(ProfileId,look);else if(Seat<0)GamepadLookSettings.SaveGeneral(look);else owner.SavePersonalGamepad(Seat,look);owner.RefreshSettingsUi();}
            void SetMenuMouse(float value)
            {if(ProfileId!=null){var p=playerProfiles.Find(ProfileId);playerProfiles.SetPersonal(p.Id,value,p.ShowFps);}else if(Seat<0)MouseSensitivityPreference.Set(Profile,value);else owner.SetPersonalMouse(Seat,value);owner.RefreshSettingsUi();}
            void SetMenuFps(bool value)
            {if(value==MenuFps())return;if(ProfileId!=null){var p=playerProfiles.Find(ProfileId);playerProfiles.SetPersonal(p.Id,p.MouseDegreesPerPixel,value);}else if(Seat<0)owner.fps.Toggle();else owner.TogglePersonalFps(Seat);owner.RefreshSettingsUi();}
            void SetHelp(bool open)
            {settingsHelpOpen=open;if(open)SetHelpText(KeyboardHelp);RefreshSettingsUi();Select(open?settingsHelpBack:settingsHelpButton);}
            void SetHelpText(string text)
            {settingsHelpText.text=text;owner.DrawHelpDiagram(helpDiagram,text==GamepadHelp);Canvas.ForceUpdateCanvases();settingsHelpScroll.verticalNormalizedPosition=1;}
            void SetDevices(bool open)
            {settingsDevicesOpen=open;deviceScroll.verticalNormalizedPosition=1;RefreshSettingsUi();Select(open?settingsDevicesBack:settingsDevicesButton);}
            void RefreshConnectedDevices()
            {
                if(settingsDevicesPage==null || !settingsDevicesOpen)return;
                var devices=new List<InputDevice>(GamepadCompatibility.ListedDevices());
                while(settingsDeviceRows.Count<devices.Count)
                {
                    int i=settingsDeviceRows.Count;
                    var row=Label(deviceContent,"device-row-"+i,"",20,Vector2.zero,Vector2.one,TextAnchor.MiddleLeft,Color.white);
                    row.rectTransform.anchorMin=new Vector2(0,1);row.rectTransform.anchorMax=Vector2.one;row.rectTransform.pivot=new Vector2(.5f,1);
                    row.rectTransform.anchoredPosition=new Vector2(0,-i*48);row.rectTransform.sizeDelta=new Vector2(0,44);
                    settingsDeviceRows.Add(row);
                }
                deviceContent.sizeDelta=new Vector2(0,Mathf.Max(48,devices.Count*48));
                for(int i=0;i<settingsDeviceRows.Count;i++)
                {settingsDeviceRows[i].gameObject.SetActive(i<devices.Count);if(i<devices.Count)settingsDeviceRows[i].text=GamepadCompatibility.Name(devices[i])+" #"+devices[i].deviceId+"   ·   "+GamepadCompatibility.Status(devices[i]);}
                settingsDevicesPageLabel.text=devices.Count==0?"Контроллеры не обнаружены":"";
            }
            ScrollRect ListViewport(Transform parent,string name,Vector2 min,Vector2 max,out RectTransform content)
            {
                var view=Panel(parent,name,min,max,Color.clear);view.AddComponent<RectMask2D>();var scroll=view.AddComponent<ScrollRect>();scroll.viewport=(RectTransform)view.transform;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
                var go=new GameObject(name+"-content",typeof(RectTransform));go.transform.SetParent(view.transform,false);content=(RectTransform)go.transform;content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.sizeDelta=Vector2.zero;scroll.content=content;return scroll;
            }

            public void CreateSettingsUi(Transform parent)
            {
                settingsScreen=Panel(parent,"settings-screen",Vector2.zero,Vector2.one,MenuInk);
                Label(settingsScreen.transform,"settings-brand","НАСТРОЙКИ",30,new Vector2(.18f,.91f),new Vector2(.95f,.96f),TextAnchor.MiddleLeft,MenuGold);
                var navigation=Panel(settingsScreen.transform,"settings-navigation",new Vector2(.05f,.11f),new Vector2(.25f,.80f),MenuCard);
                var content=Panel(settingsScreen.transform,"settings-content",new Vector2(.27f,.11f),new Vector2(.95f,.80f),new Color32(20,33,48,245));
                string[] names={"ИЗОБРАЖЕНИЕ","УПРАВЛЕНИЕ","ИНТЕРФЕЙС","ЗВУК"};
                for(int i=0;i<names.Length;i++)
                {
                    int section=i;
                    float top=.88f-i*.14f;
                    settingsSectionButtons[i]=MenuButton(navigation.transform,"settings-section-"+i,names[i],new Vector2(.06f,top-.10f),new Vector2(.94f,top),()=>SelectSettingsSection((SettingsSection)section));
                    settingsSectionButtons[i].gameObject.AddComponent<SettingsSectionFocus>().Focused=()=>PreviewSettingsSection((SettingsSection)section);
                }
                settingsBackButton=MenuButton(settingsScreen.transform,"settings-back","‹ НАЗАД · B",new Vector2(.05f,.91f),new Vector2(.15f,.96f),CloseSettings);
                settingsHeading=Label(content.transform,"settings-heading","НАСТРОЙКИ ИГРЫ",35,new Vector2(.05f,.85f),new Vector2(.95f,.98f),TextAnchor.MiddleLeft,MenuGold);
                settingsDescription=Label(content.transform,"settings-description","",21,new Vector2(.05f,.77f),new Vector2(.95f,.87f),TextAnchor.MiddleLeft,new Color32(153,173,187,255));

                settingsImagePage=Panel(content.transform,"settings-image",new Vector2(.04f,.06f),new Vector2(.96f,.76f),new Color32(20,33,48,245));
                Panel(settingsImagePage.transform,"display-mode-card",new Vector2(.02f,.75f),new Vector2(.98f,.98f),MenuCard);
                settingsDisplayMode=Label(settingsImagePage.transform,"display-mode-label","",25,new Vector2(.04f,.78f),new Vector2(.96f,.96f),TextAnchor.MiddleLeft,Color.white);
                settingsModeButton=MenuButton(settingsImagePage.transform,"settings-display-mode","СМЕНИТЬ РЕЖИМ",new Vector2(.59f,.78f),new Vector2(.96f,.96f),ChangeDisplayMode);
                Panel(settingsImagePage.transform,"resolution-card-row",new Vector2(.02f,.47f),new Vector2(.98f,.73f),MenuCard);
                settingsResolution=Label(settingsImagePage.transform,"resolution-label","",25,new Vector2(.04f,.50f),new Vector2(.96f,.72f),TextAnchor.MiddleLeft,Color.white);
                settingsResolutionButton=MenuButton(settingsImagePage.transform,"settings-resolution","ВЫБРАТЬ",new Vector2(.59f,.51f),new Vector2(.96f,.69f),OpenResolutionPicker);
                Panel(settingsImagePage.transform,"shadows-card",new Vector2(.02f,.19f),new Vector2(.98f,.45f),MenuCard);
                settingsShadows=Label(settingsImagePage.transform,"shadows-label","",25,new Vector2(.04f,.22f),new Vector2(.96f,.44f),TextAnchor.MiddleLeft,Color.white);
                settingsShadowsToggle=BooleanSetting(settingsImagePage.transform,"settings-shadows","Тени",new Vector2(.59f,.24f),new Vector2(.96f,.42f),v=>{if(v!=shadowsEnabled)ToggleShadows();});
                Label(settingsImagePage.transform,"graphics-help","Графика общая для всех экранов. Новое разрешение нужно подтвердить.",20,new Vector2(.04f,.03f),new Vector2(.96f,.18f),TextAnchor.MiddleLeft,new Color32(153,173,187,255));

                settingsControlPage=Panel(content.transform,"settings-control",new Vector2(.04f,.06f),new Vector2(.96f,.76f),new Color32(20,33,48,245));
                settingsMouseSlider=NumericSetting(settingsControlPage.transform,"settings-mouse-sensitivity","Мышь",new Vector2(.02f,.82f),new Vector2(.98f,.99f),MouseSensitivityPreference.Descriptor(Profile),SetMenuMouse);
                settingsHorizontalSlider=NumericSetting(settingsControlPage.transform,"settings-gamepad-horizontal","Геймпад: горизонталь",new Vector2(.02f,.64f),new Vector2(.98f,.81f),Profile.Descriptor(GamepadLookSettings.HorizontalPath),v=>SetMenuGamepad(0,v));
                settingsVerticalSlider=NumericSetting(settingsControlPage.transform,"settings-gamepad-vertical","Геймпад: вертикаль",new Vector2(.02f,.46f),new Vector2(.98f,.63f),Profile.Descriptor(GamepadLookSettings.VerticalPath),v=>SetMenuGamepad(1,v));
                settingsReturnDelaySlider=NumericSetting(settingsControlPage.transform,"settings-gamepad-return-delay","Задержка автовыравнивания",new Vector2(.02f,.28f),new Vector2(.98f,.45f),Profile.Descriptor(GamepadLookSettings.DelayPath),v=>SetMenuGamepad(3,v));
                settingsAutoLevelToggle=BooleanSetting(settingsControlPage.transform,"settings-auto-level","Выравнивать взгляд по полу / лестнице",new Vector2(.02f,.15f),new Vector2(.98f,.27f),v=>SetMenuGamepad(2,0,v));
                settingsHelpButton=MenuButton(settingsControlPage.transform,"settings-controls-help","СПРАВКА ПО КНОПКАМ",new Vector2(.02f,.01f),new Vector2(.49f,.14f),()=>SetHelp(true));
                settingsDevicesButton=MenuButton(settingsControlPage.transform,"settings-controls-devices","УСТРОЙСТВА",new Vector2(.51f,.01f),new Vector2(.98f,.14f),()=>SetDevices(true));
                settingsInterfacePage=Panel(content.transform,"settings-interface",new Vector2(.04f,.06f),new Vector2(.96f,.76f),new Color32(20,33,48,245));
                settingsFpsToggle=BooleanSetting(settingsInterfacePage.transform,"settings-fps","Показывать FPS",new Vector2(.02f,.78f),new Vector2(.98f,.96f),SetMenuFps);
                settingsBotTextToggle=BooleanSetting(settingsInterfacePage.transform,"settings-bot-reaction-text","Реакции ботов: текст",new Vector2(.02f,.53f),new Vector2(.98f,.71f),v=>{NativeBotReactionPreferences.SetText(v);owner.RefreshSettingsUi();});
                settingsBotVoiceToggle=BooleanSetting(settingsInterfacePage.transform,"settings-bot-reaction-voice","Реакции ботов: голос",new Vector2(.02f,.28f),new Vector2(.98f,.46f),v=>{NativeBotReactionPreferences.SetVoice(v);if(!v)owner.gameAudio?.StopBotReaction();owner.RefreshSettingsUi();});
                Label(settingsInterfacePage.transform,"settings-bot-reactions-help","Реакции ботов общие для всех игроков",20,new Vector2(.04f,.06f),new Vector2(.96f,.20f),TextAnchor.MiddleLeft,new Color32(153,173,187,255));

                settingsAudioPage=Panel(content.transform,"settings-audio",new Vector2(.04f,.06f),new Vector2(.96f,.76f),new Color32(20,33,48,245));
                settingsMusicSlider=NumericSetting(settingsAudioPage.transform,"settings-music","Музыка",new Vector2(.02f,.66f),new Vector2(.98f,.94f),Profile.Descriptor("audio.musicDefaultPercent"),v=>{NativeAudioPreferences.SetMusic(Mathf.RoundToInt(v));owner.RefreshSettingsUi();});
                settingsEffectsSlider=NumericSetting(settingsAudioPage.transform,"settings-effects","Эффекты",new Vector2(.02f,.31f),new Vector2(.98f,.59f),Profile.Descriptor("audio.effectsDefaultPercent"),v=>{NativeAudioPreferences.SetEffects(Mathf.RoundToInt(v));owner.RefreshSettingsUi();});

                settingsHelpPage=Panel(content.transform,"settings-help-page",Vector2.zero,Vector2.one,new Color32(20,33,48,245));
                settingsHelpScroll=ListViewport(settingsHelpPage.transform,"help-viewport",new Vector2(.015f,.12f),new Vector2(.985f,.985f),out helpDiagram);
                settingsHelpScroll.vertical=false;helpDiagram.pivot=new Vector2(.5f,.5f);
                Layout(helpDiagram,Vector2.zero,Vector2.one);
                settingsHelpText=Label(settingsHelpPage.transform,"keyboard-help",KeyboardHelp,18,Vector2.zero,Vector2.zero,TextAnchor.UpperLeft,Color.white);settingsHelpText.gameObject.SetActive(false);
                settingsHelpKeyboard=MenuButton(settingsHelpPage.transform,"settings-help-keyboard","‹",new Vector2(.40f,.02f),new Vector2(.48f,.10f),()=>SetHelpText(settingsHelpText.text==KeyboardHelp?GamepadHelp:KeyboardHelp));
                settingsHelpGamepad=MenuButton(settingsHelpPage.transform,"settings-help-gamepad","›",new Vector2(.52f,.02f),new Vector2(.60f,.10f),()=>SetHelpText(settingsHelpText.text==KeyboardHelp?GamepadHelp:KeyboardHelp));
                settingsHelpBack=MenuButton(settingsScreen.transform,"settings-help-back","‹ НАЗАД · B",new Vector2(.05f,.91f),new Vector2(.15f,.96f),()=>SetHelp(false));
                settingsDevicesPage=Panel(content.transform,"settings-devices-page",new Vector2(.04f,.06f),new Vector2(.96f,.84f),new Color32(20,33,48,245));
                deviceScroll=ListViewport(settingsDevicesPage.transform,"device-list",new Vector2(.03f,.10f),new Vector2(.97f,.90f),out deviceContent);
                settingsDevicesPageLabel=Label(settingsDevicesPage.transform,"devices-page-label","",19,new Vector2(.03f,.10f),new Vector2(.97f,.20f),TextAnchor.MiddleLeft,Color.white);
                settingsDevicesBack=MenuButton(settingsScreen.transform,"devices-back","‹ НАЗАД · B",new Vector2(.05f,.91f),new Vector2(.15f,.96f),()=>SetDevices(false));
                resolutionPicker=Panel(settingsImagePage.transform,"resolution-picker",new Vector2(.57f,.04f),new Vector2(.98f,.51f),MenuCard);
                var pickerCard=Panel(resolutionPicker.transform,"resolution-card",Vector2.zero,Vector2.one,MenuCard);
                Label(pickerCard.transform,"resolution-title","ВЫБЕРИТЕ РАЗРЕШЕНИЕ",30,new Vector2(.06f,.84f),new Vector2(.94f,.96f),TextAnchor.MiddleLeft,MenuGold);
                resolutionScroll=ListViewport(pickerCard.transform,"resolution-list",new Vector2(.06f,.08f),new Vector2(.94f,.78f),out var resolutionContent);
                resolutionChoices=new Button[SupportedResolutions().Count];resolutionContent.sizeDelta=new Vector2(0,resolutionChoices.Length*56);
                for(int i=0;i<resolutionChoices.Length;i++)
                {
                    int row=i;var button=MenuButton(resolutionContent,"resolution-choice-"+i,"",Vector2.zero,Vector2.one,()=>ChooseResolution(row));resolutionChoices[i]=button;
                    var rect=(RectTransform)button.transform;rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=new Vector2(0,-i*56);rect.sizeDelta=new Vector2(0,46);
                    button.gameObject.AddComponent<MenuScrollFocus>().Scroll=resolutionScroll;
                }
                resolutionCancelButton=MenuButton(pickerCard.transform,"resolution-cancel","‹ НАЗАД · B",new Vector2(.06f,.86f),new Vector2(.22f,.94f),CloseResolutionPicker);
                Layout(pickerCard.transform.Find("resolution-title").GetComponent<RectTransform>(),new Vector2(.26f,.84f),new Vector2(.94f,.96f));
                settingsConfirmation=Panel(settingsScreen.transform,"display-confirmation",Vector2.zero,Vector2.one,new Color32(8,17,25,246));
                var confirmCard=Panel(settingsConfirmation.transform,"confirmation-card",new Vector2(.12f,.24f),new Vector2(.88f,.76f),MenuCard);
                settingsConfirmationText=Label(confirmCard.transform,"confirmation-text","",29,new Vector2(.07f,.37f),new Vector2(.93f,.88f),TextAnchor.MiddleCenter,Color.white);
                settingsConfirmButton=MenuButton(confirmCard.transform,"settings-confirm","СОХРАНИТЬ",new Vector2(.07f,.10f),new Vector2(.47f,.29f),ConfirmDisplay);
                settingsRevertButton=MenuButton(confirmCard.transform,"settings-revert","ВЕРНУТЬ",new Vector2(.53f,.10f),new Vector2(.93f,.29f),RollbackDisplay);
                foreach(var button in settingsScreen.GetComponentsInChildren<Selectable>(true))
                {
                    if(button is Button)button.GetComponent<Image>().color=MenuCard;
                    var colors=button.colors;
                    colors.normalColor=button is Button?Color.white:MenuCard;
                    colors.highlightedColor=button is Button?Color.white:new Color32(61,96,112,255);
                    colors.selectedColor=button is Button?Color.white:MenuCard;
                    colors.pressedColor=button is Button?new Color(.8f,.8f,.8f,1):new Color32(176,125,66,255);
                    colors.disabledColor=button is Button?new Color(.52f,.57f,.62f,1):new Color32(50,62,69,255);
                    button.colors=colors;
                    if(button is Button focusButton){settingsFocusButtons.Add(focusButton);continue;}
                    var border=new GameObject("controller-focus",typeof(RectTransform));
                    border.transform.SetParent(button.transform,false);
                    Layout((RectTransform)border.transform,Vector2.zero,Vector2.one);
                    AddFocusEdge(border.transform,"top",new Vector2(0,.94f),Vector2.one);
                    AddFocusEdge(border.transform,"bottom",Vector2.zero,new Vector2(1,.06f));
                    AddFocusEdge(border.transform,"left",Vector2.zero,new Vector2(.015f,1));
                    AddFocusEdge(border.transform,"right",new Vector2(.985f,0),Vector2.one);
                    border.SetActive(false);
                    settingsFocusBorders[button]=border;
                }
                foreach(var text in settingsScreen.GetComponentsInChildren<Text>(true))
                {
                    if(text==settingsHelpText)continue;
                    text.resizeTextForBestFit=true;text.resizeTextMinSize=14;text.resizeTextMaxSize=text.fontSize;
                }
                if(InMatch)
                {
                    // Leave a shared telemetry gutter above each split-screen navigation header.
                    foreach(var name in new[]{"settings-brand","settings-back","settings-help-back","devices-back"})
                    {var header=(RectTransform)settingsScreen.transform.Find(name);var min=header.anchorMin;var max=header.anchorMax;min.y=.85f;max.y=.90f;Layout(header,min,max);}
                    // Keep the widget-internal paths used by common refresh helpers stable.
                    foreach(var node in settingsScreen.GetComponentsInChildren<Transform>(true))
                        if(node.name!="value"&&node.name!="box"&&node.name!="check"&&node.name!="check-fill")node.name="seat-settings-"+settingsSeat+"-"+node.name;
                    foreach(var control in settingsScreen.GetComponentsInChildren<Selectable>(true))
                    {
                        var captured=control;control.transition=Selectable.Transition.None;
                        // Per-seat focus bypasses Unity color transitions, so keep the same dark button surface explicitly.
                        if(control is Button)control.GetComponent<Image>().color=MenuCard;
                        var trigger=control.GetComponent<EventTrigger>()??control.gameObject.AddComponent<EventTrigger>();
                        var entry=new EventTrigger.Entry{eventID=EventTriggerType.PointerEnter};
                        entry.callback.AddListener(_=>{Select(captured);});trigger.triggers.Add(entry);
                    }
                }
                ConfigureSettingsNavigation();
            }
            void AddFocusEdge(Transform parent,string name,Vector2 min,Vector2 max)
            {
                var edge=new GameObject(name,typeof(RectTransform),typeof(Image));
                edge.transform.SetParent(parent,false);
                var rect=(RectTransform)edge.transform;
                // Fixed Canvas-pixel focus strokes do not become wide bars on long controls.
                bool horizontal=name=="top"||name=="bottom";
                Vector2 at=name=="top"?new Vector2(0,1):name=="right"?new Vector2(1,0):Vector2.zero;
                Layout(rect,at,horizontal?new Vector2(1,at.y):new Vector2(at.x,1));
                rect.sizeDelta=horizontal?new Vector2(0,2):new Vector2(2,0);
                var image=edge.GetComponent<Image>();image.color=MenuGold;image.raycastTarget=false;
            }
            void RefreshSettingsFocus()
            {
                var selected=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
                foreach(var button in settingsFocusButtons)
                    button.GetComponent<MenuPresentation>().SetFocused(button.gameObject==(InMatch?Selected?.gameObject:selected));
                foreach(var pair in settingsFocusBorders)
                {
                    bool focused=pair.Key.gameObject==(InMatch?Selected?.gameObject:selected) && pair.Key.IsActive() && pair.Key.IsInteractable();
                    if(pair.Value.activeSelf!=focused)pair.Value.SetActive(focused);
                }
            }
            static void Link(Selectable button,Selectable up=null,Selectable down=null,Selectable left=null,Selectable right=null)
            {
                button.navigation=new Navigation { mode=Navigation.Mode.Explicit,selectOnUp=up,selectOnDown=down,
                    selectOnLeft=left,selectOnRight=right };
            }
            Selectable FirstSettingsAction() => settingsSection==SettingsSection.Image?settingsModeButton:
                settingsSection==SettingsSection.Control?settingsMouseSlider:settingsSection==SettingsSection.Interface?settingsFpsToggle:settingsMusicSlider;
            void ConfigureSettingsNavigation()
            {
                Link(settingsSectionButtons[0],settingsBackButton,settingsSectionButtons[1],null,settingsModeButton);
                Link(settingsSectionButtons[1],settingsSectionButtons[0],
                    settingsSectionButtons[2],null,settingsMouseSlider);
                Link(settingsSectionButtons[2],settingsSectionButtons[1],settingsSectionButtons[3],null,settingsFpsToggle);
                Link(settingsSectionButtons[3],settingsSectionButtons[2],settingsBackButton,null,settingsMusicSlider);
                Link(settingsBackButton,settingsSectionButtons[3],settingsSectionButtons[0],
                    null,FirstSettingsAction());
                Link(settingsModeButton,settingsShadowsToggle,settingsResolutionButton,settingsSectionButtons[0]);
                Link(settingsResolutionButton,settingsModeButton,settingsShadowsToggle,settingsSectionButtons[0]);
                Link(settingsShadowsToggle,settingsResolutionButton,settingsModeButton,settingsSectionButtons[0]);
                Link(settingsMouseSlider,settingsSectionButtons[1],settingsHorizontalSlider);
                Link(settingsHorizontalSlider,settingsMouseSlider,settingsVerticalSlider);
                Link(settingsVerticalSlider,settingsHorizontalSlider,settingsReturnDelaySlider);
                Link(settingsReturnDelaySlider,settingsVerticalSlider,settingsAutoLevelToggle);
                Link(settingsAutoLevelToggle,settingsReturnDelaySlider,settingsHelpButton,settingsSectionButtons[1]);
                Link(settingsHelpButton,settingsAutoLevelToggle,settingsSectionButtons[1],settingsSectionButtons[1],settingsDevicesButton);
                Link(settingsDevicesButton,settingsAutoLevelToggle,settingsSectionButtons[1],settingsHelpButton);
                Link(settingsHelpKeyboard,settingsHelpBack,settingsHelpBack,null,settingsHelpGamepad);
                Link(settingsHelpGamepad,settingsHelpBack,settingsHelpBack,settingsHelpKeyboard);
                Link(settingsHelpBack,settingsHelpKeyboard,settingsHelpKeyboard);
                Link(settingsDevicesBack,settingsDevicesBack,settingsDevicesBack);
                if(settingsDevicesBack.GetComponent<SettingsHelpScrollNavigation>()==null)settingsDevicesBack.gameObject.AddComponent<SettingsHelpScrollNavigation>().Scroll=deviceScroll;
                Link(settingsFpsToggle,settingsSectionButtons[2],settingsBotTextToggle,settingsSectionButtons[2]);
                Link(settingsBotTextToggle,settingsFpsToggle,settingsBotVoiceToggle,settingsSectionButtons[2]);
                Link(settingsBotVoiceToggle,settingsBotTextToggle,settingsBackButton,settingsSectionButtons[2]);
                Link(settingsMusicSlider,settingsSectionButtons[3],settingsEffectsSlider);
                Link(settingsEffectsSlider,settingsMusicSlider,settingsBackButton);
                for(int i=0;i<resolutionChoices.Length;i++)
                    Link(resolutionChoices[i],i==0?resolutionCancelButton:resolutionChoices[i-1],
                        i==resolutionChoices.Length-1?resolutionCancelButton:resolutionChoices[i+1]);
                Link(resolutionCancelButton,resolutionChoices[resolutionChoices.Length-1],resolutionChoices[0]);
                Link(settingsConfirmButton,null,null,null,settingsRevertButton);
                Link(settingsRevertButton,null,null,settingsConfirmButton);
            }
            void PreviewSettingsSection(SettingsSection section)
            {
                if(displayConfirmationActive||resolutionPickerOpen)return;
                if(settingsSection==section&&!settingsHelpOpen&&!settingsDevicesOpen)return;
                settingsHelpOpen=false;settingsDevicesOpen=false;settingsSection=section;
                RefreshSettingsUi();ConfigureSettingsNavigation();
            }
            public void SelectSettingsSection(SettingsSection section)
            {
                PreviewSettingsSection(section);Select(FirstSettingsAction());
            }
            public void RefreshSettingsUi()
            {
                if(settingsImagePage==null)return;
                settingsSectionButtons[0].interactable=!owner.displayConfirmationActive && !resolutionPickerOpen;
                for(int i=1;i<settingsSectionButtons.Length;i++)settingsSectionButtons[i].interactable=!displayConfirmationActive && !resolutionPickerOpen;
                settingsImagePage.SetActive(settingsSection==SettingsSection.Image && !settingsHelpOpen && !settingsDevicesOpen);
                settingsControlPage.SetActive(settingsSection==SettingsSection.Control && !settingsHelpOpen && !settingsDevicesOpen);
                settingsHelpPage.SetActive(settingsHelpOpen);settingsHelpBack.gameObject.SetActive(settingsHelpOpen);settingsDevicesBack.gameObject.SetActive(settingsDevicesOpen);settingsBackButton.gameObject.SetActive(!settingsHelpOpen&&!settingsDevicesOpen);
                settingsDevicesPage.SetActive(settingsDevicesOpen);
                settingsInterfacePage.SetActive(settingsSection==SettingsSection.Interface && !settingsHelpOpen && !settingsDevicesOpen);
                settingsAudioPage.SetActive(settingsSection==SettingsSection.Audio && !settingsHelpOpen && !settingsDevicesOpen);
                resolutionPicker.SetActive(resolutionPickerOpen && !displayConfirmationActive);
                settingsConfirmation.SetActive(displayConfirmationActive);
                for(int i=0;i<settingsSectionButtons.Length;i++)
                {
                    bool selected=i==(int)settingsSection;
                    settingsSectionButtons[i].GetComponentInChildren<Text>().text=(selected?"› ":"")+
                        (i==0?"ИЗОБРАЖЕНИЕ":i==1?"УПРАВЛЕНИЕ":i==2?"ИНТЕРФЕЙС":"ЗВУК");
                }
                settingsHeading.text=settingsProfileId!=null?"НАСТРОЙКИ · "+playerProfiles.Find(settingsProfileId).Name:settingsSeat<0?"НАСТРОЙКИ ИГРЫ":"НАСТРОЙКИ · "+IdentityLabel(settingsSeat);
                settingsHeading.gameObject.SetActive(!settingsHelpOpen);
                settingsDescription.gameObject.SetActive(!settingsHelpOpen && !settingsDevicesOpen && settingsSection!=SettingsSection.Interface);
                if(settingsHelpOpen)settingsHeading.text="УПРАВЛЕНИЕ · СПРАВКА";
                if(settingsDevicesOpen)settingsHeading.text="УПРАВЛЕНИЕ · УСТРОЙСТВА";
                settingsDescription.text=settingsSection==SettingsSection.Image?"Режим вывода и тени применяются ко всем игровым экранам":
                    settingsSection==SettingsSection.Control?"Личная чувствительность сохраняется в профиле; общая служит начальным значением":
                    settingsSection==SettingsSection.Interface?"Один счётчик справа сверху; виден, если включён у одного из игроков":
                    "Громкость общая для всех игроков";
                if(settingsSection==SettingsSection.Image&&owner.displayConfirmationActive&&!displayConfirmationActive)settingsDescription.text="Другой игрок подтверждает общий режим экрана";
                settingsDisplayMode.text="РЕЖИМ ЭКРАНА\n"+(EffectiveDisplayMode()==FullScreenMode.Windowed?"Окно":"Полный экран");
                settingsResolution.text="РАЗРЕШЕНИЕ\n"+Screen.width+" × "+Screen.height;
                settingsShadows.text="ТЕНИ\n"+(shadowsEnabled?"Вкл":"Выкл");
                var sensitivity=MenuMouseSensitivity();var look=MenuGamepadLook();
                RefreshNumericSetting(settingsMouseSlider,"Чувствительность мыши",sensitivity,"°/px");
                RefreshNumericSetting(settingsHorizontalSlider,"Геймпад: горизонталь",look.Horizontal,"°/с");
                RefreshNumericSetting(settingsVerticalSlider,"Геймпад: вертикаль",look.Vertical,"°/с");
                RefreshNumericSetting(settingsReturnDelaySlider,"Задержка автовыравнивания",look.ReturnDelay,"с");
                RefreshToggle(settingsAutoLevelToggle,look.AutoLevel);RefreshToggle(settingsShadowsToggle,shadowsEnabled);
                RefreshToggle(settingsFpsToggle,MenuFps());
                RefreshToggle(settingsBotTextToggle,NativeBotReactionPreferences.TextEnabled);
                RefreshToggle(settingsBotVoiceToggle,NativeBotReactionPreferences.VoiceEnabled);
                RefreshNumericSetting(settingsMusicSlider,"Музыка",NativeAudioPreferences.Music(Profile),"%");
                RefreshNumericSetting(settingsEffectsSlider,"Эффекты",NativeAudioPreferences.Effects(Profile),"%");
                RefreshConnectedDevices();
                if(displayConfirmationActive)
                    settingsConfirmationText.text="Подтвердить режим?\n"+pendingDisplayWidth+" × "+pendingDisplayHeight+" · "+
                        (pendingDisplayMode==FullScreenMode.Windowed?"Окно":"Полный экран")+"\nВозврат через "+
                        Mathf.CeilToInt(Mathf.Max(0,displayConfirmationDeadline-Time.unscaledTime))+" с";
                settingsModeButton.interactable=!owner.displayConfirmationActive;settingsResolutionButton.interactable=!owner.displayConfirmationActive;
                settingsConfirmButton.interactable=displayConfirmationActive && Time.frameCount>displayChangeFrame;
                RefreshResolutionChoices();
                RefreshSettingsFocus();
            }
            void ChangeDisplayMode()
            {
                if(owner.displayConfirmationActive)return;
                var mode=EffectiveDisplayMode()==FullScreenMode.Windowed?FullScreenMode.FullScreenWindow:FullScreenMode.Windowed;
                displayReturnButton=settingsModeButton;
                BeginDisplayChange(Screen.width,Screen.height,mode);
            }
            void OpenResolutionPicker()
            {
                if(owner.displayConfirmationActive)return;
                var sizes=SupportedResolutions();
                resolutionPickerOpen=true;RefreshSettingsUi();Select(resolutionChoices[0]);
            }
            void CloseResolutionPicker()
            {
                resolutionPickerOpen=false;RefreshSettingsUi();Select(settingsResolutionButton);
            }
            void RefreshResolutionChoices()
            {
                if(resolutionPicker==null || !resolutionPickerOpen)return;
                var sizes=SupportedResolutions();
                for(int i=0;i<resolutionChoices.Length;i++)
                {resolutionChoices[i].gameObject.SetActive(i<sizes.Count);if(i>=sizes.Count)continue;var size=sizes[i];resolutionChoices[i].GetComponentInChildren<Text>().text=size.x+" × "+size.y+(size.x==Screen.width&&size.y==Screen.height?"   ·   СЕЙЧАС":"");Link(resolutionChoices[i],i==0?resolutionCancelButton:resolutionChoices[i-1],i==resolutionChoices.Length-1?resolutionCancelButton:resolutionChoices[i+1]);}
                Link(resolutionCancelButton,resolutionChoices[resolutionChoices.Length-1],resolutionChoices[0]);
            }

            void ChooseResolution(int row)
            {
                if(owner.displayConfirmationActive)return;
                var sizes=SupportedResolutions();int index=row;
                if(index<0||index>=sizes.Count)return;
                var size=sizes[index];resolutionPickerOpen=false;
                if(size.x==Screen.width&&size.y==Screen.height){RefreshSettingsUi();Select(settingsResolutionButton);return;}
                displayReturnButton=settingsResolutionButton;
                BeginDisplayChange(size.x,size.y,EffectiveDisplayMode());
            }
        }
    }
    // Shell focus is owned by EventSystem; seat views invoke the same preview directly.
    sealed class SettingsSectionFocus : MonoBehaviour, ISelectHandler
    {
        public Action Focused;
        public void OnSelect(BaseEventData data)=>Focused?.Invoke();
    }
    // A quarter-page is a UI scrolling increment; it does not tune gameplay.
    sealed class SettingsHelpScrollNavigation : MonoBehaviour, IMoveHandler
    {
        public ScrollRect Scroll;
        public void OnMove(AxisEventData data)
        {
            if(data.moveDir!=MoveDirection.Left&&data.moveDir!=MoveDirection.Right)return;
            Scroll.verticalNormalizedPosition=Mathf.Clamp01(Scroll.verticalNormalizedPosition+(data.moveDir==MoveDirection.Left?.25f:-.25f));
            data.Use();
        }
    }
}
