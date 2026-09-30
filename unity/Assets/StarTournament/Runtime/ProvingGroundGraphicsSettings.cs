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
        SettingsSection settingsSection;
        readonly Button[] settingsSectionButtons=new Button[4];
        Button settingsBackButton,settingsModeButton,settingsResolutionButton,settingsShadowsButton;
        Button settingsSensitivityMinus,settingsSensitivityPlus,settingsFpsButton;
        Button musicMinus,musicPlus,effectsMinus,effectsPlus;
        Button resolutionCancelButton,settingsRevertButton;
        Button displayReturnButton;
        readonly Dictionary<Button,GameObject> settingsFocusBorders=new Dictionary<Button,GameObject>();
        GameObject settingsImagePage,settingsControlPage,settingsInterfacePage,settingsAudioPage,settingsConfirmation,resolutionPicker;
        Text musicValue,effectsValue;
        Text settingsDescription,settingsDisplayMode,settingsResolution,settingsShadows,settingsSensitivity,settingsFps,settingsConfirmationText;
        Button settingsConfirmButton;
        readonly Button[] resolutionChoices=new Button[5];
        Button resolutionPagePrevious,resolutionPageNext;
        Text resolutionPageLabel;
        int resolutionPage;
        bool resolutionPickerOpen;
        readonly Dictionary<Light,LightShadows> originalLightShadows=new Dictionary<Light,LightShadows>();
        ShadowQuality originalShadowQuality;
        bool shadowsEnabled=true,graphicsPreferencesInitialized,displayConfirmationActive;
        float displayConfirmationDeadline;
        int displayChangeFrame,oldDisplayWidth,oldDisplayHeight,pendingDisplayWidth,pendingDisplayHeight;
        FullScreenMode oldDisplayMode,pendingDisplayMode;

        void CreateSettingsUi(Transform parent)
        {
            settingsScreen=Panel(parent,"settings-screen",Vector2.zero,Vector2.one,MenuInk);
            Label(settingsScreen.transform,"settings-brand","STAR TOURNAMENT  /  НАСТРОЙКИ",35,new Vector2(.05f,.90f),new Vector2(.95f,.98f),TextAnchor.MiddleLeft,MenuGold);
            var navigation=Panel(settingsScreen.transform,"settings-navigation",new Vector2(.05f,.11f),new Vector2(.25f,.87f),MenuCard);
            var content=Panel(settingsScreen.transform,"settings-content",new Vector2(.27f,.11f),new Vector2(.95f,.87f),new Color32(20,39,53,255));
            string[] names={"ИЗОБРАЖЕНИЕ","УПРАВЛЕНИЕ","ИНТЕРФЕЙС","ЗВУК"};
            for(int i=0;i<names.Length;i++)
            {
                int section=i;
                float top=.88f-i*.14f;
                settingsSectionButtons[i]=MenuButton(navigation.transform,"settings-section-"+i,names[i],new Vector2(.06f,top-.10f),new Vector2(.94f,top),()=>SelectSettingsSection((SettingsSection)section));
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
            settingsShadowsButton=MenuButton(settingsImagePage.transform,"settings-shadows","ВКЛ / ВЫКЛ",new Vector2(.59f,.24f),new Vector2(.96f,.42f),ToggleShadows);
            Label(settingsImagePage.transform,"graphics-help","Графика общая для всех экранов. Новое разрешение нужно подтвердить.",20,new Vector2(.04f,.03f),new Vector2(.96f,.18f),TextAnchor.MiddleLeft,new Color32(177,199,201,255));

            settingsControlPage=Panel(content.transform,"settings-control",new Vector2(.04f,.06f),new Vector2(.96f,.76f),new Color32(20,39,53,255));
            Panel(settingsControlPage.transform,"sensitivity-card",new Vector2(.02f,.26f),new Vector2(.98f,.94f),MenuCard);
            settingsSensitivity=Label(settingsControlPage.transform,"sensitivity-label","",27,new Vector2(.04f,.65f),new Vector2(.96f,.90f),TextAnchor.MiddleLeft,Color.white);
            Label(settingsControlPage.transform,"sensitivity-help","Поворот камеры за пиксель движения мыши.",20,new Vector2(.04f,.52f),new Vector2(.96f,.67f),TextAnchor.MiddleLeft,new Color32(177,199,201,255));
            settingsSensitivityMinus=MenuButton(settingsControlPage.transform,"settings-sensitivity-minus","−",new Vector2(.04f,.30f),new Vector2(.40f,.49f),()=>StepMenuSensitivity(-1));
            settingsSensitivityPlus=MenuButton(settingsControlPage.transform,"settings-sensitivity-plus","+",new Vector2(.60f,.30f),new Vector2(.96f,.49f),()=>StepMenuSensitivity(1));
            settingsInterfacePage=Panel(content.transform,"settings-interface",new Vector2(.04f,.06f),new Vector2(.96f,.76f),new Color32(20,39,53,255));
            Panel(settingsInterfacePage.transform,"fps-card",new Vector2(.02f,.25f),new Vector2(.98f,.94f),MenuCard);
            settingsFps=Label(settingsInterfacePage.transform,"fps-label","",27,new Vector2(.04f,.65f),new Vector2(.96f,.90f),TextAnchor.MiddleLeft,Color.white);
            Label(settingsInterfacePage.transform,"fps-help","Индикатор частоты кадров без изменения правил матча.",20,new Vector2(.04f,.50f),new Vector2(.96f,.67f),TextAnchor.MiddleLeft,new Color32(177,199,201,255));
            settingsFpsButton=MenuButton(settingsInterfacePage.transform,"settings-fps","ВКЛ / ВЫКЛ",new Vector2(.55f,.28f),new Vector2(.96f,.47f),ToggleMenuFps);

            settingsAudioPage=Panel(content.transform,"settings-audio",new Vector2(.04f,.06f),new Vector2(.96f,.76f),new Color32(20,39,53,255));
            Panel(settingsAudioPage.transform,"music-card",new Vector2(.02f,.54f),new Vector2(.98f,.95f),MenuCard);
            musicValue=Label(settingsAudioPage.transform,"music-value","",27,new Vector2(.05f,.74f),new Vector2(.95f,.93f),TextAnchor.MiddleLeft,Color.white);
            musicMinus=MenuButton(settingsAudioPage.transform,"music-minus","−",new Vector2(.05f,.57f),new Vector2(.43f,.73f),()=>StepAudio(false,-1));
            musicPlus=MenuButton(settingsAudioPage.transform,"music-plus","+",new Vector2(.57f,.57f),new Vector2(.95f,.73f),()=>StepAudio(false,1));
            Panel(settingsAudioPage.transform,"effects-card",new Vector2(.02f,.07f),new Vector2(.98f,.48f),MenuCard);
            effectsValue=Label(settingsAudioPage.transform,"effects-value","",27,new Vector2(.05f,.27f),new Vector2(.95f,.46f),TextAnchor.MiddleLeft,Color.white);
            effectsMinus=MenuButton(settingsAudioPage.transform,"effects-minus","−",new Vector2(.05f,.10f),new Vector2(.43f,.26f),()=>StepAudio(true,-1));
            effectsPlus=MenuButton(settingsAudioPage.transform,"effects-plus","+",new Vector2(.57f,.10f),new Vector2(.95f,.26f),()=>StepAudio(true,1));

            resolutionPicker=Panel(settingsScreen.transform,"resolution-picker",Vector2.zero,Vector2.one,new Color32(8,17,25,246));
            var pickerCard=Panel(resolutionPicker.transform,"resolution-card",new Vector2(.26f,.15f),new Vector2(.74f,.85f),MenuCard);
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
            var confirmCard=Panel(settingsConfirmation.transform,"confirmation-card",new Vector2(.27f,.30f),new Vector2(.73f,.70f),MenuCard);
            settingsConfirmationText=Label(confirmCard.transform,"confirmation-text","",29,new Vector2(.07f,.37f),new Vector2(.93f,.88f),TextAnchor.MiddleCenter,Color.white);
            settingsConfirmButton=MenuButton(confirmCard.transform,"settings-confirm","СОХРАНИТЬ",new Vector2(.07f,.10f),new Vector2(.47f,.29f),ConfirmDisplay);
            settingsRevertButton=MenuButton(confirmCard.transform,"settings-revert","ВЕРНУТЬ",new Vector2(.53f,.10f),new Vector2(.93f,.29f),RollbackDisplay);
            foreach(var button in settingsScreen.GetComponentsInChildren<Button>(true))
            {
                button.GetComponent<Image>().color=Color.white;
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
                bool focused=pair.Key.gameObject==selected && pair.Key.IsActive() && pair.Key.IsInteractable();
                if(pair.Value.activeSelf!=focused)pair.Value.SetActive(focused);
            }
        }
        static void Link(Button button,Button up=null,Button down=null,Button left=null,Button right=null)
        {
            button.navigation=new Navigation { mode=Navigation.Mode.Explicit,selectOnUp=up,selectOnDown=down,
                selectOnLeft=left,selectOnRight=right };
        }
        Button FirstSettingsAction() => settingsSection==SettingsSection.Image?settingsModeButton:
            settingsSection==SettingsSection.Control?settingsSensitivityMinus:settingsSection==SettingsSection.Interface?settingsFpsButton:musicMinus;
        void ConfigureSettingsNavigation()
        {
            Link(settingsSectionButtons[0],settingsBackButton,settingsSectionButtons[1],null,settingsModeButton);
            Link(settingsSectionButtons[1],settingsSeat<0?settingsSectionButtons[0]:settingsBackButton,
                settingsSectionButtons[2],null,settingsSensitivityMinus);
            Link(settingsSectionButtons[2],settingsSectionButtons[1],settingsSectionButtons[3],null,settingsFpsButton);
            Link(settingsSectionButtons[3],settingsSectionButtons[2],settingsBackButton,null,musicMinus);
            Link(settingsBackButton,settingsSectionButtons[3],settingsSeat<0?settingsSectionButtons[0]:settingsSectionButtons[1],
                null,FirstSettingsAction());
            Link(settingsModeButton,settingsShadowsButton,settingsResolutionButton,settingsSectionButtons[0]);
            Link(settingsResolutionButton,settingsModeButton,settingsShadowsButton,settingsSectionButtons[0]);
            Link(settingsShadowsButton,settingsResolutionButton,settingsModeButton,settingsSectionButtons[0]);
            Link(settingsSensitivityMinus,settingsSectionButtons[1],settingsBackButton,settingsSectionButtons[1],settingsSensitivityPlus);
            Link(settingsSensitivityPlus,settingsSectionButtons[1],settingsBackButton,settingsSensitivityMinus);
            Link(settingsFpsButton,settingsSectionButtons[2],settingsBackButton,settingsSectionButtons[2]);
            Link(musicMinus,settingsSectionButtons[3],effectsMinus,settingsSectionButtons[3],musicPlus);
            Link(musicPlus,settingsSectionButtons[3],effectsPlus,musicMinus);
            Link(effectsMinus,musicMinus,settingsBackButton,settingsSectionButtons[3],effectsPlus);
            Link(effectsPlus,musicPlus,settingsBackButton,effectsMinus);
            for(int i=0;i<resolutionChoices.Length;i++)
                Link(resolutionChoices[i],i==0?resolutionCancelButton:resolutionChoices[i-1],
                    i==resolutionChoices.Length-1?resolutionPagePrevious:resolutionChoices[i+1]);
            Link(resolutionPagePrevious,resolutionChoices[resolutionChoices.Length-1],resolutionCancelButton,null,resolutionPageNext);
            Link(resolutionPageNext,resolutionChoices[resolutionChoices.Length-1],resolutionCancelButton,resolutionPagePrevious);
            Link(resolutionCancelButton,resolutionPagePrevious,resolutionChoices[0]);
            Link(settingsConfirmButton,null,null,null,settingsRevertButton);
            Link(settingsRevertButton,null,null,settingsConfirmButton);
        }
        void SelectSettingsSection(SettingsSection section)
        {
            if(settingsSeat>=0 && section==SettingsSection.Image)return;
            settingsSection=section;RefreshSettingsUi();ConfigureSettingsNavigation();Select(FirstSettingsAction());
        }
        void RefreshSettingsUi()
        {
            if(settingsImagePage==null)return;
            settingsSectionButtons[0].interactable=settingsSeat<0 && !displayConfirmationActive && !resolutionPickerOpen;
            for(int i=1;i<settingsSectionButtons.Length;i++)settingsSectionButtons[i].interactable=!displayConfirmationActive && !resolutionPickerOpen;
            settingsImagePage.SetActive(settingsSection==SettingsSection.Image && settingsSeat<0);
            settingsControlPage.SetActive(settingsSection==SettingsSection.Control);
            settingsInterfacePage.SetActive(settingsSection==SettingsSection.Interface);
            settingsAudioPage.SetActive(settingsSection==SettingsSection.Audio);
            resolutionPicker.SetActive(resolutionPickerOpen && !displayConfirmationActive);
            settingsConfirmation.SetActive(displayConfirmationActive);
            for(int i=0;i<settingsSectionButtons.Length;i++)
            {
                bool selected=i==(int)settingsSection;
                settingsSectionButtons[i].GetComponentInChildren<Text>().text=(selected?"› ":"")+
                    (i==0?"ИЗОБРАЖЕНИЕ":i==1?"УПРАВЛЕНИЕ":i==2?"ИНТЕРФЕЙС":"ЗВУК");
            }
            settingsHeading.text=settingsSeat<0?"НАСТРОЙКИ ИГРЫ":"НАСТРОЙКИ · "+IdentityLabel(settingsSeat);
            settingsDescription.text=settingsSection==SettingsSection.Image?"Режим вывода и тени применяются ко всем игровым экранам":
                settingsSection==SettingsSection.Control?"Личная чувствительность сохраняется в профиле; общая служит начальным значением":
                settingsSection==SettingsSection.Interface?"Индикатор FPS можно настроить отдельно для каждого игрока":
                "Общая громкость для всех игроков. Музыкальная дорожка появится позже";
            musicValue.text="МУЗЫКА  "+NativeAudioPreferences.Music(Profile)+" %";
            effectsValue.text="ЭФФЕКТЫ  "+NativeAudioPreferences.Effects(Profile)+" %";
            settingsDisplayMode.text="РЕЖИМ ЭКРАНА\n"+(EffectiveDisplayMode()==FullScreenMode.Windowed?"Окно":"Полный экран");
            settingsResolution.text="РАЗРЕШЕНИЕ\n"+Screen.width+" × "+Screen.height;
            settingsShadows.text="ТЕНИ\n"+(shadowsEnabled?"Вкл":"Выкл");
            float sensitivity=settingsSeat<0?MouseSensitivityPreference.Resolve(Profile):PersonalSensitivity(settingsSeat);
            settingsSensitivity.text="ЧУВСТВИТЕЛЬНОСТЬ МЫШИ\n"+sensitivity.ToString("0.00")+" °/px";
            settingsFps.text="ПОКАЗЫВАТЬ FPS\n"+((settingsSeat<0?fps.Visible:PersonalFps(settingsSeat))?"Вкл":"Выкл");
            if(displayConfirmationActive)
                settingsConfirmationText.text="Подтвердить режим?\n"+pendingDisplayWidth+" × "+pendingDisplayHeight+" · "+
                    (pendingDisplayMode==FullScreenMode.Windowed?"Окно":"Полный экран")+"\nВозврат через "+
                    Mathf.CeilToInt(Mathf.Max(0,displayConfirmationDeadline-Time.unscaledTime))+" с";
            settingsConfirmButton.interactable=displayConfirmationActive && Time.frameCount>displayChangeFrame;
            RefreshResolutionChoices();
            RefreshSettingsFocus();
        }
        static FullScreenMode EffectiveDisplayMode() => Screen.fullScreenMode==FullScreenMode.Windowed?FullScreenMode.Windowed:FullScreenMode.FullScreenWindow;
        void StepAudio(bool effects,int direction)
        {
            if(effects)NativeAudioPreferences.SetEffects(NativeAudioPreferences.Effects(Profile)+direction*5);
            else NativeAudioPreferences.SetMusic(NativeAudioPreferences.Music(Profile)+direction*5);
            RefreshSettingsUi();
        }
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
        void ChangeDisplayMode()
        {
            if(displayConfirmationActive)return;
            var mode=EffectiveDisplayMode()==FullScreenMode.Windowed?FullScreenMode.FullScreenWindow:FullScreenMode.Windowed;
            displayReturnButton=settingsModeButton;
            BeginDisplayChange(Screen.width,Screen.height,mode);
        }
        void OpenResolutionPicker()
        {
            if(displayConfirmationActive)return;
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
            var sizes=SupportedResolutions();int index=resolutionPage*resolutionChoices.Length+row;
            if(index<0||index>=sizes.Count)return;
            var size=sizes[index];resolutionPickerOpen=false;
            if(size.x==Screen.width&&size.y==Screen.height){RefreshSettingsUi();Select(settingsResolutionButton);return;}
            displayReturnButton=settingsResolutionButton;
            BeginDisplayChange(size.x,size.y,EffectiveDisplayMode());
        }
        void BeginDisplayChange(int width,int height,FullScreenMode mode)
        {
            oldDisplayWidth=Screen.width;oldDisplayHeight=Screen.height;oldDisplayMode=Screen.fullScreenMode;
            pendingDisplayWidth=width;pendingDisplayHeight=height;pendingDisplayMode=mode;
            displayChangeFrame=Time.frameCount;displayConfirmationDeadline=Time.unscaledTime+DisplayConfirmationSeconds;
            resolutionPickerOpen=false;displayConfirmationActive=true;
            Screen.SetResolution(width,height,mode);
            RefreshSettingsUi();Select(settingsConfirmButton);
        }
        void TickDisplayConfirmation()
        {
            if(!displayConfirmationActive)return;
            if(Time.unscaledTime>=displayConfirmationDeadline){RollbackDisplay();return;}
            if(settingsConfirmationText)settingsConfirmationText.text="Подтвердить режим?\n"+pendingDisplayWidth+" × "+pendingDisplayHeight+
                "\nВозврат через "+Mathf.CeilToInt(displayConfirmationDeadline-Time.unscaledTime)+" с";
            if(settingsConfirmButton && Time.frameCount>displayChangeFrame && !settingsConfirmButton.interactable)
            {
                settingsConfirmButton.interactable=true;
                Select(settingsConfirmButton);
            }
        }
        void ConfirmDisplay()
        {
            if(!displayConfirmationActive || Time.frameCount<=displayChangeFrame)return;
            displayConfirmationActive=false;
            PlayerPrefs.SetInt(DisplayWidthKey,Screen.width);PlayerPrefs.SetInt(DisplayHeightKey,Screen.height);
            PlayerPrefs.SetInt(DisplayModeKey,Screen.fullScreenMode==FullScreenMode.Windowed?0:1);PlayerPrefs.Save();
            RefreshSettingsUi();Select(displayReturnButton??settingsModeButton);
        }
        void RollbackDisplay()
        {
            if(!displayConfirmationActive)return;
            displayConfirmationActive=false;
            Screen.SetResolution(oldDisplayWidth,oldDisplayHeight,oldDisplayMode);
            if(settingsScreen)RefreshSettingsUi();
            if(phase==Phase.Settings)Select(displayReturnButton??settingsModeButton);
        }
        void UpdateSettingsBackInput()
        {
            if(phase!=Phase.Settings)return;
            bool back=Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame;
            if(settingsSeat>=0)
            {
                if(input.DeviceAt(settingsSeat) is Gamepad pad)back|=pad.buttonEast.wasPressedThisFrame;
            }
            else foreach(var pad in Gamepad.all)back|=pad.buttonEast.wasPressedThisFrame;
            if(!back)return;
            gameAudio?.MenuBack();
            if(displayConfirmationActive)RollbackDisplay();
            else if(resolutionPickerOpen)CloseResolutionPicker();
            else CloseSettings();
            lastAudioSelection=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
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
