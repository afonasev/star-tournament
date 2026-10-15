using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingGround
    {
        sealed partial class SettingsView
        {
            GameObject settingsGraphicsPage,resetDialog;
            Slider settingsDeadzoneSlider,settingsScaleSlider;
            Toggle settingsInvertX,settingsInvertY,settingsContrast;
            Button settingsAutoButton,settingsFrameCap,resetAllButton,resetCancel,resetConfirm;
            Text settingsWriteError;
            bool resetOpen;
            int settingsOpenedFrame;
            Selectable resetReturn;
            static readonly int[] StandardSectionOrder={0,4,3,1,2};
            string StandardName(string name)=>InMatch?"seat-settings-"+Seat+"-"+name:name;
            void Move(Transform node,Transform parent,Vector2 min,Vector2 max)
            {node.SetParent(parent,false);Layout((RectTransform)node,min,max);}
            void AddStandardFocus(Selectable control)
            {
                if(control is Button b){settingsFocusButtons.Add(b);return;}
                var border=Panel(control.transform,"controller-focus",Vector2.zero,Vector2.one,Color.clear);
                border.GetComponent<Image>().raycastTarget=false;
                AddFocusEdge(border.transform,"top",new Vector2(0,.94f),Vector2.one);
                AddFocusEdge(border.transform,"bottom",Vector2.zero,new Vector2(1,.06f));
                AddFocusEdge(border.transform,"left",Vector2.zero,new Vector2(.015f,1));
                AddFocusEdge(border.transform,"right",new Vector2(.985f,0),Vector2.one);
                border.SetActive(false);settingsFocusBorders[control]=border;
            }
            void SaveStandard(Action action)
            {
                try{action();if(settingsWriteError)settingsWriteError.text="";owner.RefreshSettingsUi();}
                catch(Exception e){if(settingsWriteError)settingsWriteError.text="Настройки не сохранены между запусками. Проверьте доступ к данным и повторите.";Debug.LogWarning("Settings operation failed: "+e.Message);}
            }
            void CreateStandardSettings(Transform content)
            {
                for(int i=0;i<StandardSectionOrder.Length;i++)
                {float top=.91f-i*.16f;Layout((RectTransform)settingsSectionButtons[StandardSectionOrder[i]].transform,new Vector2(.06f,top-.11f),new Vector2(.94f,top));}
                settingsGraphicsPage=Panel(content,StandardName("settings-graphics"),new Vector2(.04f,.06f),new Vector2(.96f,.76f),new Color32(20,33,48,245));
                var viewport=graphicsScroll.viewport;var list=graphicsScroll.content;
                viewport.SetParent(settingsGraphicsPage.transform,false);
                var replacement=settingsGraphicsPage.AddComponent<ScrollRect>();replacement.viewport=viewport;replacement.content=list;replacement.horizontal=false;replacement.movementType=ScrollRect.MovementType.Clamped;replacement.scrollSensitivity=45;
                UnityEngine.Object.Destroy(graphicsScroll);graphicsScroll=replacement;
                foreach(var focus in viewport.GetComponentsInChildren<MenuScrollFocus>(true))focus.Scroll=graphicsScroll;
                list.sizeDelta=new Vector2(0,800);
                Move(settingsDisplayMode.transform,settingsImagePage.transform,new Vector2(.02f,.79f),new Vector2(.55f,.98f));
                Move(settingsModeButton.transform,settingsImagePage.transform,new Vector2(.60f,.81f),new Vector2(.98f,.96f));
                Move(settingsResolution.transform,settingsImagePage.transform,new Vector2(.02f,.57f),new Vector2(.55f,.76f));
                Move(settingsResolutionButton.transform,settingsImagePage.transform,new Vector2(.60f,.63f),new Vector2(.98f,.75f));
                settingsModeButton.GetComponent<MenuScrollFocus>().Scroll=null;settingsResolutionButton.GetComponent<MenuScrollFocus>().Scroll=null;
                settingsAutoButton=MenuButton(settingsImagePage.transform,StandardName("settings-resolution-auto"),"AUTO",new Vector2(.60f,.50f),new Vector2(.98f,.61f),ChooseAutoResolution);
                Move(settingsVSyncToggle.transform,settingsImagePage.transform,new Vector2(.02f,.29f),new Vector2(.98f,.45f));
                settingsVSyncToggle.GetComponent<MenuScrollFocus>().Scroll=null;
                Label(settingsImagePage.transform,StandardName("settings-frame-cap-label"),"Ограничение FPS",23,new Vector2(.02f,.08f),new Vector2(.57f,.23f),TextAnchor.MiddleLeft,Color.white);
                settingsFrameCap=MenuButton(settingsImagePage.transform,StandardName("settings-frame-cap"),"",new Vector2(.60f,.08f),new Vector2(.98f,.23f),()=>SaveStandard(NativeUserPreferences.CycleFrameCap));
                foreach(var hint in settingsImagePage.GetComponentsInChildren<Text>(true).Where(t=>t.name.EndsWith("graphics-help")))hint.gameObject.SetActive(false);
                var aaLabel=list.GetComponentsInChildren<Text>(true).Single(t=>t.name.EndsWith("settings-antialiasing-label"));
                Layout(aaLabel.rectTransform,new Vector2(.02f,.88f),new Vector2(.56f,.99f));
                Layout((RectTransform)settingsAntialiasingButton.transform,new Vector2(.59f,.88f),new Vector2(.96f,.99f));
                Layout(settingsShadows.rectTransform,new Vector2(.02f,.76f),new Vector2(.56f,.87f));
                Layout((RectTransform)settingsShadowsToggle.transform,new Vector2(.59f,.76f),new Vector2(.96f,.87f));
                for(int i=0;i<visualToggles.Length;i++)
                {Layout((RectTransform)visualToggles[i].transform,new Vector2(.02f,.75f-(i+1)*.11f),new Vector2(.98f,.75f-i*.11f));visualSliders[i].transform.parent.gameObject.SetActive(false);}
                foreach(var slider in colorAdjustments)slider.transform.parent.gameObject.SetActive(false);

                var controls=ListViewport(settingsControlPage.transform,StandardName("controls-viewport"),Vector2.zero,Vector2.one,out var body);
                body.sizeDelta=new Vector2(0,1200);controls.scrollSensitivity=45;
                settingsDeadzoneSlider=owner.NumericSetting(body,StandardName("settings-deadzone"),"Мёртвая зона стика",Vector2.zero,Vector2.one,NativeUserPreferences.Deadzone(Profile),v=>SaveStandard(()=>SetMenuGamepad(4,v/100)));
                settingsInvertX=owner.BooleanSetting(body,StandardName("settings-invert-x"),"Инверсия взгляда X",Vector2.zero,Vector2.one,v=>SaveStandard(()=>SetMenuGamepad(5,0,v)));
                settingsInvertY=owner.BooleanSetting(body,StandardName("settings-invert-y"),"Инверсия взгляда Y",Vector2.zero,Vector2.one,v=>SaveStandard(()=>SetMenuGamepad(6,0,v)));
                Selectable[] inputControls={settingsMouseSlider,settingsHorizontalSlider,settingsVerticalSlider,settingsDeadzoneSlider,settingsInvertX,settingsInvertY,settingsReturnDelaySlider,settingsAutoLevelToggle,settingsHelpButton,settingsDevicesButton};
                for(int i=0;i<inputControls.Length;i++)
                {
                    var c=inputControls[i];var row=c is Slider?c.transform.parent:c.transform;
                    Move(row,body,new Vector2(.02f,1-(i+1)*.098f),new Vector2(.98f,1-i*.098f-.008f));
                    var focus=c.GetComponent<MenuScrollFocus>()??c.gameObject.AddComponent<MenuScrollFocus>();focus.Scroll=controls;focus.Row=(RectTransform)row;
                }
                settingsScaleSlider=owner.NumericSetting(settingsInterfacePage.transform,StandardName("settings-ui-scale"),"Масштаб интерфейса",new Vector2(.02f,.81f),new Vector2(.98f,.99f),NativeUserPreferences.ScaleDescriptor,v=>SaveStandard(()=>NativeUserPreferences.SetScale(v)));
                settingsContrast=owner.BooleanSetting(settingsInterfacePage.transform,StandardName("settings-high-contrast"),"Повышенный контраст",new Vector2(.02f,.61f),new Vector2(.98f,.79f),v=>SaveStandard(()=>NativeUserPreferences.SetContrast(v)));
                Layout((RectTransform)settingsFpsToggle.transform,new Vector2(.02f,.41f),new Vector2(.98f,.59f));
                Layout((RectTransform)settingsBotTextToggle.transform,new Vector2(.02f,.21f),new Vector2(.98f,.39f));
                Layout((RectTransform)settingsBotVoiceToggle.transform,new Vector2(.02f,.01f),new Vector2(.98f,.19f));
                foreach(var hint in settingsInterfacePage.GetComponentsInChildren<Text>(true).Where(t=>t.name.EndsWith("settings-bot-reactions-help")))hint.gameObject.SetActive(false);

                resetAllButton=MenuButton(settingsScreen.transform,StandardName("settings-reset-all"),"Сбросить все настройки",new Vector2(.05f,.035f),new Vector2(.34f,.09f),OpenReset);
                settingsWriteError=Label(settingsScreen.transform,StandardName("settings-save-error"),"",17,new Vector2(.36f,.015f),new Vector2(.95f,.09f),TextAnchor.MiddleLeft,new Color32(255,218,184,255));
                resetDialog=Panel(settingsScreen.transform,StandardName("settings-reset-dialog"),Vector2.zero,Vector2.one,new Color32(4,10,17,250));
                var card=Panel(resetDialog.transform,"card",new Vector2(.18f,.20f),new Vector2(.82f,.80f),MenuCard);
                Label(card.transform,"title","Сбросить все настройки?",30,new Vector2(.06f,.76f),new Vector2(.94f,.94f),TextAnchor.MiddleLeft,MenuGold);
                Label(card.transform,"body","Экран, графика, звук, управление и интерфейс, включая личные предпочтения, вернутся к исходным значениям.\n\nПрофили, прогресс, история и лаборатория сохранятся. Согласие на статистику не изменится.",22,new Vector2(.06f,.30f),new Vector2(.94f,.75f),TextAnchor.UpperLeft,Color.white);
                resetCancel=MenuButton(card.transform,StandardName("settings-reset-cancel"),"Отмена",new Vector2(.06f,.10f),new Vector2(.48f,.24f),CloseReset);
                resetConfirm=MenuButton(card.transform,StandardName("settings-reset-confirm"),"Сбросить всё",new Vector2(.52f,.10f),new Vector2(.94f,.24f),ConfirmReset);
                foreach(var c in new Selectable[]{settingsAutoButton,settingsFrameCap,settingsDeadzoneSlider,settingsInvertX,settingsInvertY,settingsScaleSlider,settingsContrast,resetAllButton,resetCancel,resetConfirm})AddStandardFocus(c);
                var canvas=settingsScreen.GetComponentInParent<Canvas>();
                if(canvas&&!canvas.GetComponent<NativeInterfacePreferences>())canvas.gameObject.AddComponent<NativeInterfacePreferences>();
                resetDialog.SetActive(false);
            }
            void ConfigureStandardNavigation()
            {
                for(int i=0;i<StandardSectionOrder.Length;i++)
                {int n=StandardSectionOrder[i];var action=n==0?settingsModeButton:n==4?(Selectable)settingsAntialiasingButton:n==3?settingsMasterSlider:n==1?settingsMouseSlider:settingsScaleSlider;Link(settingsSectionButtons[n],i==0?settingsBackButton:settingsSectionButtons[StandardSectionOrder[i-1]],i==4?resetAllButton:settingsSectionButtons[StandardSectionOrder[i+1]],null,action);}
                Link(settingsBackButton,resetAllButton,settingsSectionButtons[0]);
                Link(resetAllButton,settingsSectionButtons[2],settingsBackButton);
                Selectable[] screen={settingsModeButton,settingsResolutionButton,settingsAutoButton,settingsVSyncToggle,settingsFrameCap};
                Chain(screen,settingsSectionButtons[0]);
                Chain(new Selectable[]{settingsAntialiasingButton,settingsShadowsToggle}.Concat(visualToggles).ToArray(),settingsSectionButtons[4]);
                Chain(new Selectable[]{settingsMouseSlider,settingsHorizontalSlider,settingsVerticalSlider,settingsDeadzoneSlider,settingsInvertX,settingsInvertY,settingsReturnDelaySlider,settingsAutoLevelToggle,settingsHelpButton,settingsDevicesButton},settingsSectionButtons[1]);
                Chain(new Selectable[]{settingsScaleSlider,settingsContrast,settingsFpsToggle,settingsBotTextToggle,settingsBotVoiceToggle},settingsSectionButtons[2]);
            }
            void Chain(Selectable[] controls,Button section)
            {for(int i=0;i<controls.Length;i++)Link(controls[i],i==0?section:controls[i-1],i==controls.Length-1?resetAllButton:controls[i+1],section);}
            void RefreshStandardSettings()
            {
                if(!settingsGraphicsPage)return;
                var look=MenuGamepadLook();
                RefreshNumericSetting(settingsDeadzoneSlider,"Мёртвая зона стика",look.Deadzone*100,"%");
                RefreshNumericSetting(settingsScaleSlider,"Масштаб интерфейса",NativeUserPreferences.Scale,"%");
                RefreshToggle(settingsInvertX,look.InvertX);RefreshToggle(settingsInvertY,look.InvertY);RefreshToggle(settingsContrast,NativeUserPreferences.HighContrast);
                settingsFrameCap.GetComponentInChildren<Text>().text=NativeUserPreferences.FrameCap<0?"Без ограничения":NativeUserPreferences.FrameCap+" FPS";
                resetDialog.SetActive(resetOpen);resetAllButton.gameObject.SetActive(!settingsHelpOpen&&!settingsDevicesOpen);
                for(int i=0;i<settingsSectionButtons.Length;i++)settingsSectionButtons[i].interactable=!resetOpen&&!owner.displayConfirmationActive&&!resolutionPickerOpen;
                resetAllButton.interactable=!resetOpen&&!owner.displayConfirmationActive&&!resolutionPickerOpen;
                var pad=InMatch?owner.input.DeviceAt(Seat) as UnityEngine.InputSystem.Gamepad:owner.menuDevice as UnityEngine.InputSystem.Gamepad;
                string device=(pad?.layout+" "+pad?.description.product).ToLowerInvariant();
                string glyph=device.Contains("dual")||device.Contains("playstation")?"△":device.Contains("switch")||device.Contains("nintendo")?"X":device.Contains("xinput")||device.Contains("xbox")?"Y":"Верх";
                resetAllButton.GetComponentInChildren<Text>().text="Сбросить все настройки"+(owner.menuGamepadCursor?" · "+glyph:"");
            }
            void ChooseAutoResolution()
            {var d=Screen.currentResolution;int w=d.width>0?d.width:Screen.width,h=d.height>0?d.height:Screen.height;displayReturnButton=settingsAutoButton;owner.BeginDisplayChange(this,w,h,EffectiveDisplayMode(),true);}
            public void ResetShortcut()
            {
                var editing=EventSystem.current?.currentSelectedGameObject?.GetComponent<InputField>();
                if(Time.frameCount<=settingsOpenedFrame||resetOpen||displayConfirmationActive||owner.displayConfirmationActive||resolutionPickerOpen||settingsHelpOpen||settingsDevicesOpen||editing&&editing.isFocused)return;
                OpenReset();
            }
            void OpenReset()
            {if(owner.displayConfirmationActive||resetOpen||resolutionPickerOpen)return;resetReturn=InMatch?Selected:EventSystem.current?.currentSelectedGameObject?.GetComponent<Selectable>();resetOpen=true;RefreshSettingsUi();ConfigureSettingsNavigation();Select(resetCancel);}
            void CloseReset()
            {resetOpen=false;RefreshSettingsUi();ConfigureSettingsNavigation();Select(resetReturn&&resetReturn.IsActive()?resetReturn:resetAllButton);}
            void ConfirmReset()
            {
                SaveStandard(()=>
                {
                    playerProfiles.ResetPreferences(Profile);NativeUserPreferences.Reset();owner.identities.ResetGuestPreferences(Profile);
                    owner.vSyncEnabled=true;owner.shadowsEnabled=true;owner.ApplyVSyncPreference();owner.ApplyShadowPreference();owner.fps.RestoreGeneralPreference();
                    owner.visualPresentation?.Apply();owner.gameAudio?.UpdateVolume();CloseReset();
                    var d=Screen.currentResolution;displayReturnButton=resetAllButton;
                    owner.BeginDisplayChange(this,d.width>0?d.width:Screen.width,d.height>0?d.height:Screen.height,FullScreenMode.FullScreenWindow,true);
                });
            }
        }
    }
}
