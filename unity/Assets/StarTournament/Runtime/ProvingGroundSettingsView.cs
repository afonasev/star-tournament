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
            GameObject settingsImagePage,settingsControlPage,settingsInterfacePage,settingsAudioPage,settingsConfirmation,resolutionPicker;
            Slider settingsMusicSlider,settingsEffectsSlider;
            Text settingsDescription,settingsDisplayMode,settingsResolution,settingsShadows,settingsFps,settingsConfirmationText;
            Button settingsConfirmButton;
            readonly Button[] resolutionChoices=new Button[5];
            Button resolutionPagePrevious,resolutionPageNext;
            Text resolutionPageLabel;
            int resolutionPage;
            bool resolutionPickerOpen;
            Slider settingsMouseSlider,settingsHorizontalSlider,settingsVerticalSlider;
            Toggle settingsAutoLevelToggle,settingsFpsToggle,settingsShadowsToggle;
            GameObject settingsHelpPage;
            Button settingsHelpButton,settingsHelpBack;
            bool settingsHelpOpen;
            GameObject settingsDevicesPage;
            Button settingsDevicesButton,settingsDevicesBack,settingsDevicesPrevious,settingsDevicesNext;
            readonly Text[] settingsDeviceRows=new Text[5];
            Text settingsDevicesPageLabel;
            bool settingsDevicesOpen;
            int settingsDevicesPageIndex;

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
            {Selected=control;for(int i=0;i<settingsSectionButtons.Length;i++)if(control==settingsSectionButtons[i])PreviewSettingsSection((SettingsSection)i);if(!InMatch)ProvingGround.Select(control);RefreshSettingsFocus();}
            public void Navigate(int x,int y,bool submit,bool back)
            {
                if(back){Back();return;}
                if(Selected==null||!Selected.IsActive()||!Selected.IsInteractable())Select(displayConfirmationActive?settingsRevertButton:FirstSettingsAction());
                if(settingsHelpOpen&&x!=0&&Selected==settingsHelpBack)
                {settingsHelpScroll.verticalNormalizedPosition=Mathf.Clamp01(settingsHelpScroll.verticalNormalizedPosition-x*.25f);return;}
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
            {var look=MenuGamepadLook();if(axis==0)look.Horizontal=value;else if(axis==1)look.Vertical=value;else look.AutoLevel=enabled;
             if(ProfileId!=null)playerProfiles.SetGamepad(ProfileId,look);else if(Seat<0)GamepadLookSettings.SaveGeneral(look);else owner.SavePersonalGamepad(Seat,look);owner.RefreshSettingsUi();}
            void SetMenuMouse(float value)
            {if(ProfileId!=null){var p=playerProfiles.Find(ProfileId);playerProfiles.SetPersonal(p.Id,value,p.ShowFps);}else if(Seat<0)MouseSensitivityPreference.Set(Profile,value);else owner.SetPersonalMouse(Seat,value);owner.RefreshSettingsUi();}
            void SetMenuFps(bool value)
            {if(value==MenuFps())return;if(ProfileId!=null){var p=playerProfiles.Find(ProfileId);playerProfiles.SetPersonal(p.Id,p.MouseDegreesPerPixel,value);}else if(Seat<0)owner.fps.Toggle();else owner.TogglePersonalFps(Seat);owner.RefreshSettingsUi();}
            void SetHelp(bool open)
            {settingsHelpOpen=open;if(open)SetHelpText(KeyboardHelp);RefreshSettingsUi();Select(open?settingsHelpBack:settingsHelpButton);}
            void SetHelpText(string text)
            {settingsHelpText.text=text;Canvas.ForceUpdateCanvases();settingsHelpScroll.verticalNormalizedPosition=1;}
            void SetDevices(bool open)
            {settingsDevicesOpen=open;settingsDevicesPageIndex=0;RefreshSettingsUi();Select(open?settingsDevicesBack:settingsDevicesButton);}
            void ChangeDevicesPage(int direction)
            {settingsDevicesPageIndex+=direction;RefreshSettingsUi();Select(direction<0?settingsDevicesPrevious:settingsDevicesNext);}
            void RefreshConnectedDevices()
            {
                if(settingsDevicesPage==null || !settingsDevicesOpen)return;
                var devices=new List<InputDevice>(GamepadCompatibility.ListedDevices());
                int pages=Mathf.Max(1,(devices.Count+settingsDeviceRows.Length-1)/settingsDeviceRows.Length);
                settingsDevicesPageIndex=Mathf.Clamp(settingsDevicesPageIndex,0,pages-1);
                int offset=settingsDevicesPageIndex*settingsDeviceRows.Length;
                for(int i=0;i<settingsDeviceRows.Length;i++)
                {
                    int index=offset+i;
                    settingsDeviceRows[i].gameObject.SetActive(index<devices.Count);
                    if(index<devices.Count)
                    {
                        var device=devices[index];
                        settingsDeviceRows[i].text=GamepadCompatibility.Name(device)+" #"+device.deviceId+"\n"+GamepadCompatibility.Status(device);
                    }
                }
                settingsDevicesPageLabel.text=devices.Count==0?"Контроллеры не обнаружены":
                    "Устройства "+(settingsDevicesPageIndex+1)+" / "+pages;
                settingsDevicesPrevious.interactable=settingsDevicesPageIndex>0;
                settingsDevicesNext.interactable=settingsDevicesPageIndex<pages-1;
            }
            public void CreateSettingsUi(Transform parent)
            {
                settingsScreen=Panel(parent,"settings-screen",Vector2.zero,Vector2.one,MenuInk);
                Label(settingsScreen.transform,"settings-brand","STAR TOURNAMENT  /  НАСТРОЙКИ",35,new Vector2(.05f,.84f),new Vector2(.95f,.90f),TextAnchor.MiddleLeft,MenuGold);
                var navigation=Panel(settingsScreen.transform,"settings-navigation",new Vector2(.05f,.11f),new Vector2(.25f,.80f),MenuCard);
                var content=Panel(settingsScreen.transform,"settings-content",new Vector2(.27f,.11f),new Vector2(.95f,.80f),new Color32(20,39,53,255));
                string[] names={"ИЗОБРАЖЕНИЕ","УПРАВЛЕНИЕ","ИНТЕРФЕЙС","ЗВУК"};
                for(int i=0;i<names.Length;i++)
                {
                    int section=i;
                    float top=.88f-i*.14f;
                    settingsSectionButtons[i]=MenuButton(navigation.transform,"settings-section-"+i,names[i],new Vector2(.06f,top-.10f),new Vector2(.94f,top),()=>SelectSettingsSection((SettingsSection)section));
                    settingsSectionButtons[i].gameObject.AddComponent<SettingsSectionFocus>().Focused=()=>PreviewSettingsSection((SettingsSection)section);
                }
                settingsBackButton=MenuButton(navigation.transform,"settings-back","‹ НАЗАД",new Vector2(.06f,.05f),new Vector2(.94f,.15f),CloseSettings);
                settingsHeading=Label(content.transform,"settings-heading","НАСТРОЙКИ ИГРЫ",35,new Vector2(.05f,.85f),new Vector2(.95f,.98f),TextAnchor.MiddleLeft,MenuGold);
                settingsDescription=Label(content.transform,"settings-description","",21,new Vector2(.05f,.77f),new Vector2(.95f,.87f),TextAnchor.MiddleLeft,new Color32(177,199,201,255));

                settingsImagePage=Panel(content.transform,"settings-image",new Vector2(.04f,.06f),new Vector2(.96f,.76f),new Color32(20,39,53,255));
                Panel(settingsImagePage.transform,"display-mode-card",new Vector2(.02f,.75f),new Vector2(.98f,.98f),MenuCard);
                settingsDisplayMode=Label(settingsImagePage.transform,"display-mode-label","",25,new Vector2(.04f,.78f),new Vector2(.96f,.96f),TextAnchor.MiddleLeft,Color.white);
                settingsModeButton=MenuButton(settingsImagePage.transform,"settings-display-mode","СМЕНИТЬ РЕЖИМ",new Vector2(.59f,.78f),new Vector2(.96f,.96f),ChangeDisplayMode);
                Panel(settingsImagePage.transform,"resolution-card-row",new Vector2(.02f,.47f),new Vector2(.98f,.73f),MenuCard);
                settingsResolution=Label(settingsImagePage.transform,"resolution-label","",25,new Vector2(.04f,.50f),new Vector2(.96f,.72f),TextAnchor.MiddleLeft,Color.white);
                settingsResolutionButton=MenuButton(settingsImagePage.transform,"settings-resolution","ВЫБРАТЬ",new Vector2(.59f,.51f),new Vector2(.96f,.69f),OpenResolutionPicker);
                Panel(settingsImagePage.transform,"shadows-card",new Vector2(.02f,.19f),new Vector2(.98f,.45f),MenuCard);
                settingsShadows=Label(settingsImagePage.transform,"shadows-label","",25,new Vector2(.04f,.22f),new Vector2(.96f,.44f),TextAnchor.MiddleLeft,Color.white);
                settingsShadowsToggle=BooleanSetting(settingsImagePage.transform,"settings-shadows","Тени",new Vector2(.59f,.24f),new Vector2(.96f,.42f),v=>{if(v!=shadowsEnabled)ToggleShadows();});
                Label(settingsImagePage.transform,"graphics-help","Графика общая для всех экранов. Новое разрешение нужно подтвердить.",20,new Vector2(.04f,.03f),new Vector2(.96f,.18f),TextAnchor.MiddleLeft,new Color32(177,199,201,255));

                settingsControlPage=Panel(content.transform,"settings-control",new Vector2(.04f,.06f),new Vector2(.96f,.76f),new Color32(20,39,53,255));
                settingsMouseSlider=NumericSetting(settingsControlPage.transform,"settings-mouse-sensitivity","Мышь",new Vector2(.02f,.77f),new Vector2(.98f,.99f),MouseSensitivityPreference.Descriptor(Profile),SetMenuMouse);
                settingsHorizontalSlider=NumericSetting(settingsControlPage.transform,"settings-gamepad-horizontal","Геймпад: горизонталь",new Vector2(.02f,.54f),new Vector2(.98f,.76f),Profile.Descriptor(GamepadLookSettings.HorizontalPath),v=>SetMenuGamepad(0,v));
                settingsVerticalSlider=NumericSetting(settingsControlPage.transform,"settings-gamepad-vertical","Геймпад: вертикаль",new Vector2(.02f,.31f),new Vector2(.98f,.53f),Profile.Descriptor(GamepadLookSettings.VerticalPath),v=>SetMenuGamepad(1,v));
                settingsAutoLevelToggle=BooleanSetting(settingsControlPage.transform,"settings-auto-level","Выравнивать взгляд по полу / лестнице",new Vector2(.02f,.16f),new Vector2(.98f,.29f),v=>SetMenuGamepad(2,0,v));
                settingsHelpButton=MenuButton(settingsControlPage.transform,"settings-controls-help","СПРАВКА ПО КНОПКАМ",new Vector2(.02f,.01f),new Vector2(.49f,.14f),()=>SetHelp(true));
                settingsDevicesButton=MenuButton(settingsControlPage.transform,"settings-controls-devices","УСТРОЙСТВА",new Vector2(.51f,.01f),new Vector2(.98f,.14f),()=>SetDevices(true));
                settingsInterfacePage=Panel(content.transform,"settings-interface",new Vector2(.04f,.06f),new Vector2(.96f,.76f),new Color32(20,39,53,255));
                Panel(settingsInterfacePage.transform,"fps-card",new Vector2(.02f,.25f),new Vector2(.98f,.94f),MenuCard);
                settingsFps=Label(settingsInterfacePage.transform,"fps-label","",27,new Vector2(.04f,.65f),new Vector2(.96f,.90f),TextAnchor.MiddleLeft,Color.white);
                Label(settingsInterfacePage.transform,"fps-help","Индикатор частоты кадров без изменения правил матча.",20,new Vector2(.04f,.50f),new Vector2(.96f,.67f),TextAnchor.MiddleLeft,new Color32(177,199,201,255));
                settingsFpsToggle=BooleanSetting(settingsInterfacePage.transform,"settings-fps","Показывать FPS",new Vector2(.05f,.28f),new Vector2(.96f,.47f),SetMenuFps);

                settingsAudioPage=Panel(content.transform,"settings-audio",new Vector2(.04f,.06f),new Vector2(.96f,.76f),new Color32(20,39,53,255));
                settingsMusicSlider=NumericSetting(settingsAudioPage.transform,"settings-music","Музыка",new Vector2(.02f,.55f),new Vector2(.98f,.95f),Profile.Descriptor("audio.musicDefaultPercent"),v=>{NativeAudioPreferences.SetMusic(Mathf.RoundToInt(v));owner.RefreshSettingsUi();});
                settingsEffectsSlider=NumericSetting(settingsAudioPage.transform,"settings-effects","Эффекты",new Vector2(.02f,.10f),new Vector2(.98f,.50f),Profile.Descriptor("audio.effectsDefaultPercent"),v=>{NativeAudioPreferences.SetEffects(Mathf.RoundToInt(v));owner.RefreshSettingsUi();});

                settingsHelpPage=Panel(content.transform,"settings-help-page",new Vector2(.04f,.01f),new Vector2(.96f,.84f),new Color32(20,39,53,255));
                var helpViewport=Panel(settingsHelpPage.transform,"help-viewport",new Vector2(.02f,.33f),new Vector2(.98f,.98f),Color.clear);
                helpViewport.AddComponent<RectMask2D>();settingsHelpScroll=helpViewport.AddComponent<ScrollRect>();
                settingsHelpScroll.viewport=(RectTransform)helpViewport.transform;settingsHelpScroll.horizontal=false;settingsHelpScroll.movementType=ScrollRect.MovementType.Clamped;
                settingsHelpText=Label(helpViewport.transform,"keyboard-help",KeyboardHelp,22,new Vector2(0,1),Vector2.one,TextAnchor.UpperLeft,Color.white);
                settingsHelpText.rectTransform.pivot=new Vector2(.5f,1);settingsHelpText.rectTransform.sizeDelta=Vector2.zero;
                settingsHelpText.verticalOverflow=VerticalWrapMode.Overflow;
                settingsHelpText.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
                settingsHelpScroll.content=settingsHelpText.rectTransform;
                Label(settingsHelpPage.transform,"settings-help-scroll-hint","← / → — прокрутка · колесо мыши",18,new Vector2(.02f,.25f),new Vector2(.98f,.32f),TextAnchor.MiddleLeft,MenuGold);
                settingsHelpKeyboard=MenuButton(settingsHelpPage.transform,"settings-help-keyboard","КЛАВИАТУРА / МЫШЬ",new Vector2(.02f,.14f),new Vector2(.49f,.24f),()=>SetHelpText(KeyboardHelp));
                settingsHelpGamepad=MenuButton(settingsHelpPage.transform,"settings-help-gamepad","ГЕЙМПАД",new Vector2(.51f,.14f),new Vector2(.98f,.24f),()=>SetHelpText(GamepadHelp));
                settingsHelpBack=MenuButton(settingsHelpPage.transform,"settings-help-back","‹ К НАСТРОЙКАМ",new Vector2(.02f,.02f),new Vector2(.98f,.12f),()=>SetHelp(false));
                settingsDevicesPage=Panel(content.transform,"settings-devices-page",new Vector2(.04f,.01f),new Vector2(.96f,.84f),new Color32(20,39,53,255));
                Label(settingsDevicesPage.transform,"devices-title","ПОДКЛЮЧЁННЫЕ УСТРОЙСТВА",27,new Vector2(.03f,.88f),new Vector2(.97f,.98f),TextAnchor.MiddleLeft,MenuGold);
                for(int i=0;i<settingsDeviceRows.Length;i++)
                {
                    float top=.86f-i*.13f;
                    settingsDeviceRows[i]=Label(settingsDevicesPage.transform,"device-row-"+i,"",19,new Vector2(.04f,top-.12f),new Vector2(.96f,top),TextAnchor.MiddleLeft,Color.white);
                }
                settingsDevicesPageLabel=Label(settingsDevicesPage.transform,"devices-page-label","",19,new Vector2(.25f,.14f),new Vector2(.75f,.21f),TextAnchor.MiddleCenter,Color.white);
                settingsDevicesPrevious=MenuButton(settingsDevicesPage.transform,"devices-previous","‹",new Vector2(.04f,.14f),new Vector2(.23f,.22f),()=>ChangeDevicesPage(-1));
                settingsDevicesNext=MenuButton(settingsDevicesPage.transform,"devices-next","›",new Vector2(.77f,.14f),new Vector2(.96f,.22f),()=>ChangeDevicesPage(1));
                settingsDevicesBack=MenuButton(settingsDevicesPage.transform,"devices-back","‹ К НАСТРОЙКАМ",new Vector2(.02f,.02f),new Vector2(.98f,.12f),()=>SetDevices(false));
                resolutionPicker=Panel(settingsScreen.transform,"resolution-picker",Vector2.zero,Vector2.one,new Color32(8,17,25,246));
                var pickerCard=Panel(resolutionPicker.transform,"resolution-card",new Vector2(.12f,.10f),new Vector2(.88f,.90f),MenuCard);
                Label(pickerCard.transform,"resolution-title","ВЫБЕРИТЕ РАЗРЕШЕНИЕ",30,new Vector2(.06f,.84f),new Vector2(.94f,.96f),TextAnchor.MiddleLeft,MenuGold);
                for(int i=0;i<resolutionChoices.Length;i++)
                {
                    int row=i;float top=.81f-i*.115f;
                    resolutionChoices[i]=MenuButton(pickerCard.transform,"resolution-choice-"+i,"",new Vector2(.06f,top-.10f),new Vector2(.94f,top),()=>ChooseResolution(row));
                }
                resolutionPagePrevious=MenuButton(pickerCard.transform,"resolution-page-prev","‹",new Vector2(.06f,.11f),new Vector2(.27f,.20f),()=>ChangeResolutionPage(-1));
                resolutionPageLabel=Label(pickerCard.transform,"resolution-page-label","",21,new Vector2(.31f,.11f),new Vector2(.69f,.20f),TextAnchor.MiddleCenter,Color.white);
                resolutionPageNext=MenuButton(pickerCard.transform,"resolution-page-next","›",new Vector2(.73f,.11f),new Vector2(.94f,.20f),()=>ChangeResolutionPage(1));
                resolutionCancelButton=MenuButton(pickerCard.transform,"resolution-cancel","‹ К НАСТРОЙКАМ",new Vector2(.06f,.02f),new Vector2(.94f,.10f),CloseResolutionPicker);
                settingsConfirmation=Panel(settingsScreen.transform,"display-confirmation",Vector2.zero,Vector2.one,new Color32(8,17,25,246));
                var confirmCard=Panel(settingsConfirmation.transform,"confirmation-card",new Vector2(.12f,.24f),new Vector2(.88f,.76f),MenuCard);
                settingsConfirmationText=Label(confirmCard.transform,"confirmation-text","",29,new Vector2(.07f,.37f),new Vector2(.93f,.88f),TextAnchor.MiddleCenter,Color.white);
                settingsConfirmButton=MenuButton(confirmCard.transform,"settings-confirm","СОХРАНИТЬ",new Vector2(.07f,.10f),new Vector2(.47f,.29f),ConfirmDisplay);
                settingsRevertButton=MenuButton(confirmCard.transform,"settings-revert","ВЕРНУТЬ",new Vector2(.53f,.10f),new Vector2(.93f,.29f),RollbackDisplay);
                foreach(var button in settingsScreen.GetComponentsInChildren<Selectable>(true))
                {
                    if(button is Button)button.GetComponent<Image>().color=Color.white;
                    var colors=button.colors;
                    colors.normalColor=MenuCard;
                    colors.highlightedColor=new Color32(61,96,112,255);
                    colors.selectedColor=MenuCard;
                    colors.pressedColor=new Color32(176,125,66,255);
                    colors.disabledColor=new Color32(50,62,69,255);
                    button.colors=colors;
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
                if(!InMatch)settingsHelpBack.gameObject.AddComponent<SettingsHelpScrollNavigation>().Scroll=settingsHelpScroll;
                ConfigureSettingsNavigation();
            }
            void AddFocusEdge(Transform parent,string name,Vector2 min,Vector2 max)
            {
                var edge=new GameObject(name,typeof(RectTransform),typeof(Image));
                edge.transform.SetParent(parent,false);
                Layout((RectTransform)edge.transform,min,max);
                var image=edge.GetComponent<Image>();image.color=MenuGold;image.raycastTarget=false;
            }
            void RefreshSettingsFocus()
            {
                var selected=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
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
                Link(settingsVerticalSlider,settingsHorizontalSlider,settingsAutoLevelToggle);
                Link(settingsAutoLevelToggle,settingsVerticalSlider,settingsHelpButton,settingsSectionButtons[1]);
                Link(settingsHelpButton,settingsAutoLevelToggle,settingsSectionButtons[1],settingsSectionButtons[1],settingsDevicesButton);
                Link(settingsDevicesButton,settingsAutoLevelToggle,settingsSectionButtons[1],settingsHelpButton);
                Link(settingsHelpKeyboard,settingsHelpBack,settingsHelpBack,null,settingsHelpGamepad);
                Link(settingsHelpGamepad,settingsHelpBack,settingsHelpBack,settingsHelpKeyboard);
                Link(settingsHelpBack,settingsHelpKeyboard,settingsHelpKeyboard);
                Link(settingsDevicesPrevious,settingsDevicesBack,settingsDevicesBack,null,settingsDevicesNext);
                Link(settingsDevicesNext,settingsDevicesBack,settingsDevicesBack,settingsDevicesPrevious);
                Link(settingsDevicesBack,settingsDevicesPrevious,settingsDevicesPrevious);
                Link(settingsFpsToggle,settingsSectionButtons[2],settingsBackButton,settingsSectionButtons[2]);
                Link(settingsMusicSlider,settingsSectionButtons[3],settingsEffectsSlider);
                Link(settingsEffectsSlider,settingsMusicSlider,settingsBackButton);
                for(int i=0;i<resolutionChoices.Length;i++)
                    Link(resolutionChoices[i],i==0?resolutionCancelButton:resolutionChoices[i-1],
                        i==resolutionChoices.Length-1?resolutionPagePrevious:resolutionChoices[i+1]);
                Link(resolutionPagePrevious,resolutionChoices[resolutionChoices.Length-1],resolutionCancelButton,null,resolutionPageNext);
                Link(resolutionPageNext,resolutionChoices[resolutionChoices.Length-1],resolutionCancelButton,resolutionPagePrevious);
                Link(resolutionCancelButton,resolutionPagePrevious,resolutionChoices[0]);
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
                settingsHelpPage.SetActive(settingsHelpOpen);
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
                settingsDescription.gameObject.SetActive(!settingsHelpOpen && !settingsDevicesOpen);
                if(settingsHelpOpen)settingsHeading.text="УПРАВЛЕНИЕ · СПРАВКА";
                if(settingsDevicesOpen)settingsHeading.text="УПРАВЛЕНИЕ · УСТРОЙСТВА";
                settingsDescription.text=settingsSection==SettingsSection.Image?"Режим вывода и тени применяются ко всем игровым экранам":
                    settingsSection==SettingsSection.Control?"Личная чувствительность сохраняется в профиле; общая служит начальным значением":
                    settingsSection==SettingsSection.Interface?"Индикатор FPS можно настроить отдельно для каждого игрока":
                    "Громкость общая для всех игроков";
                if(settingsSection==SettingsSection.Image&&owner.displayConfirmationActive&&!displayConfirmationActive)settingsDescription.text="Другой игрок подтверждает общий режим экрана";
                settingsDisplayMode.text="РЕЖИМ ЭКРАНА\n"+(EffectiveDisplayMode()==FullScreenMode.Windowed?"Окно":"Полный экран");
                settingsResolution.text="РАЗРЕШЕНИЕ\n"+Screen.width+" × "+Screen.height;
                settingsShadows.text="ТЕНИ\n"+(shadowsEnabled?"Вкл":"Выкл");
                var sensitivity=MenuMouseSensitivity();var look=MenuGamepadLook();
                RefreshNumericSetting(settingsMouseSlider,"Чувствительность мыши",sensitivity,"°/px");
                RefreshNumericSetting(settingsHorizontalSlider,"Геймпад: горизонталь",look.Horizontal,"°/с");
                RefreshNumericSetting(settingsVerticalSlider,"Геймпад: вертикаль",look.Vertical,"°/с");
                RefreshToggle(settingsAutoLevelToggle,look.AutoLevel);RefreshToggle(settingsShadowsToggle,shadowsEnabled);
                RefreshToggle(settingsFpsToggle,MenuFps());
                RefreshNumericSetting(settingsMusicSlider,"Музыка",NativeAudioPreferences.Music(Profile),"%");
                RefreshNumericSetting(settingsEffectsSlider,"Эффекты",NativeAudioPreferences.Effects(Profile),"%");
                settingsFps.text="ПОКАЗЫВАТЬ FPS";
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
                int current=sizes.FindIndex(s=>s.x==Screen.width&&s.y==Screen.height);
                resolutionPage=Mathf.Max(0,current)/resolutionChoices.Length;
                resolutionPickerOpen=true;RefreshSettingsUi();Select(resolutionChoices[0]);
            }
            void CloseResolutionPicker()
            {
                resolutionPickerOpen=false;RefreshSettingsUi();Select(settingsResolutionButton);
            }
            void ChangeResolutionPage(int direction)
            {
                int pageCount=Mathf.CeilToInt(SupportedResolutions().Count/(float)resolutionChoices.Length);
                resolutionPage=Mathf.Clamp(resolutionPage+direction,0,Mathf.Max(0,pageCount-1));
                RefreshResolutionChoices();Select(resolutionChoices[0]);
            }
            void RefreshResolutionChoices()
            {
                if(resolutionPicker==null || !resolutionPickerOpen)return;
                var sizes=SupportedResolutions();
                int pageCount=Mathf.CeilToInt(sizes.Count/(float)resolutionChoices.Length);
                resolutionPage=Mathf.Clamp(resolutionPage,0,Mathf.Max(0,pageCount-1));
                for(int i=0;i<resolutionChoices.Length;i++)
                {
                    int index=resolutionPage*resolutionChoices.Length+i;
                    resolutionChoices[i].gameObject.SetActive(index<sizes.Count);
                    resolutionChoices[i].interactable=!owner.displayConfirmationActive;
                    if(index>=sizes.Count)continue;
                    var size=sizes[index];
                    resolutionChoices[i].GetComponentInChildren<Text>().text=size.x+" × "+size.y+
                        (size.x==Screen.width&&size.y==Screen.height?"   ·   СЕЙЧАС":"");
                }
                resolutionPagePrevious.interactable=resolutionPage>0;
                resolutionPageNext.interactable=resolutionPage+1<pageCount;
                resolutionPageLabel.text=(resolutionPage+1)+" / "+pageCount;
                int last=Mathf.Min(resolutionChoices.Length-1,sizes.Count-resolutionPage*resolutionChoices.Length-1);
                Button pageAction=resolutionPagePrevious.interactable?resolutionPagePrevious:
                    resolutionPageNext.interactable?resolutionPageNext:resolutionCancelButton;
                for(int i=0;i<=last;i++)
                    Link(resolutionChoices[i],i==0?resolutionCancelButton:resolutionChoices[i-1],
                        i==last?pageAction:resolutionChoices[i+1]);
                Link(resolutionPagePrevious,resolutionChoices[last],resolutionCancelButton,null,
                    resolutionPageNext.interactable?resolutionPageNext:resolutionCancelButton);
                Link(resolutionPageNext,resolutionChoices[last],resolutionCancelButton,
                    resolutionPagePrevious.interactable?resolutionPagePrevious:resolutionCancelButton);
                Link(resolutionCancelButton,pageAction,resolutionChoices[0]);
            }
            void ChooseResolution(int row)
            {
                if(owner.displayConfirmationActive)return;
                var sizes=SupportedResolutions();int index=resolutionPage*resolutionChoices.Length+row;
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
