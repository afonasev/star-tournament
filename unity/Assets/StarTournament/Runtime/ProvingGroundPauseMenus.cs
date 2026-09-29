using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingGround
    {
        enum SeatPausePage { Closed, Actions, Settings, Help, Audio, ConfirmRepeat, ConfirmExit }
        readonly SeatPausePage[] seatPausePages = new SeatPausePage[SeatInputCoordinator.SeatCount];
        readonly int[] seatPauseCursor = new int[SeatInputCoordinator.SeatCount];
        readonly GameObject[] seatPauseRoots = new GameObject[SeatInputCoordinator.SeatCount];
        readonly GameObject[] seatPauseCards = new GameObject[SeatInputCoordinator.SeatCount];
        readonly GameObject[] seatPauseWaiting = new GameObject[SeatInputCoordinator.SeatCount];
        readonly GameObject[] seatPauseActions = new GameObject[SeatInputCoordinator.SeatCount];
        readonly GameObject[] seatPauseSettings = new GameObject[SeatInputCoordinator.SeatCount];
        readonly GameObject[] seatPauseAudio = new GameObject[SeatInputCoordinator.SeatCount];
        readonly GameObject[] seatPauseRepeat = new GameObject[SeatInputCoordinator.SeatCount];
        readonly GameObject[] seatPauseExit = new GameObject[SeatInputCoordinator.SeatCount];
        readonly Text[] seatPauseSettingsValue = new Text[SeatInputCoordinator.SeatCount];
        readonly Text[] seatPauseAudioValue = new Text[SeatInputCoordinator.SeatCount];
        readonly Text[] seatPauseReason = new Text[SeatInputCoordinator.SeatCount];
        readonly Text[] seatPauseFps = new Text[SeatInputCoordinator.SeatCount];
        readonly Button[,] seatPauseActionButtons = new Button[SeatInputCoordinator.SeatCount,4];
        readonly Selectable[,] seatPauseSettingButtons = new Selectable[SeatInputCoordinator.SeatCount,8];
        readonly Selectable[,] seatPauseAudioButtons = new Selectable[SeatInputCoordinator.SeatCount,3];
        readonly GameObject[] seatPauseHelp=new GameObject[SeatInputCoordinator.SeatCount];
        readonly Text[] seatPauseHelpText=new Text[SeatInputCoordinator.SeatCount];
        readonly ScrollRect[] seatPauseHelpScroll=new ScrollRect[SeatInputCoordinator.SeatCount];
        readonly Button[,] seatPauseHelpButtons=new Button[SeatInputCoordinator.SeatCount,3];
        readonly Vector2[] seatPauseLastStick=new Vector2[SeatInputCoordinator.SeatCount];
        readonly float[] seatPauseRepeatAt=new float[SeatInputCoordinator.SeatCount];
        readonly Button[,] seatPauseRepeatButtons = new Button[SeatInputCoordinator.SeatCount,2];
        readonly Button[,] seatPauseExitButtons = new Button[SeatInputCoordinator.SeatCount,2];

        bool UseSeatPauseMenus => !diagnostic && !combatReview && reviewComposition==null &&
            Enumerable.Range(0,LocalSeatCount).Any(input.IsHumanSeat);
        // Headless test runners have no focused GameView; native Player still requires focus to resume.
        bool FocusAllowsResume => Application.isFocused || Application.isBatchMode;

        void CreateSeatPauseUi()
        {
            for(int seat=0;seat<SeatInputCoordinator.SeatCount;seat++)
            {
                if(viewportRoots[seat]==null)continue;
                int localSeat=seat;
                var root=Panel(viewportRoots[seat],"seat-pause-"+seat,Vector2.zero,Vector2.one,Color.clear);
                root.GetComponent<Image>().raycastTarget=false;
                seatPauseRoots[seat]=root;
                var dim=Panel(root.transform,"dim",Vector2.zero,Vector2.one,new Color32(4,12,21,175));
                dim.GetComponent<Image>().raycastTarget=false;
                var wait=Label(root.transform,"pause-wait","МАТЧ НА ПАУЗЕ",24,new Vector2(.08f,.42f),new Vector2(.92f,.58f),TextAnchor.MiddleCenter,Color.white);
                seatPauseWaiting[seat]=wait.gameObject;
                var card=Panel(root.transform,"pause-card",new Vector2(.11f,.09f),new Vector2(.89f,.91f),new Color32(20,39,53,248));
                seatPauseCards[seat]=card;
                card.AddComponent<CanvasGroup>();
                seatPauseActions[seat]=PausePage(card.transform,"actions");
                Label(seatPauseActions[seat].transform,"title","ПАУЗА",31,new Vector2(.08f,.85f),new Vector2(.92f,.97f),TextAnchor.MiddleLeft,MenuGold);
                seatPauseReason[seat]=Label(seatPauseActions[seat].transform,"reason","",18,new Vector2(.08f,.73f),new Vector2(.92f,.85f),TextAnchor.MiddleLeft,Color.white);
                string[] actionLabels={"Продолжить","Настройки","Начать заново","Выйти в главное меню"};
                for(int item=0;item<4;item++)
                {
                    int localItem=item;
                    float top=.70f-item*.155f;
                    seatPauseActionButtons[seat,item]=PauseButton(seatPauseActions[seat].transform,"pause-action-"+seat+"-"+item,actionLabels[item],
                        new Vector2(.08f,top-.11f),new Vector2(.92f,top),()=>RunSeatPauseAction(localSeat,localItem),seat,item);
                }
                seatPauseSettings[seat]=PausePage(card.transform,"settings");
                Label(seatPauseSettings[seat].transform,"title","НАСТРОЙКИ",30,new Vector2(.08f,.84f),new Vector2(.92f,.97f),TextAnchor.MiddleLeft,MenuGold);
                seatPauseSettingsValue[seat]=Label(seatPauseSettings[seat].transform,"values","",18,new Vector2(.08f,.78f),new Vector2(.92f,.85f),TextAnchor.MiddleLeft,Color.white);
                var d=MouseSensitivityPreference.Descriptor(Profile);
                seatPauseSettingButtons[seat,0]=NumericSetting(seatPauseSettings[seat].transform,"pause-setting-"+seat+"-0","Мышь",new Vector2(.08f,.61f),new Vector2(.92f,.77f),d,v=>SetPersonalMouse(localSeat,v),18);
                seatPauseSettingButtons[seat,1]=NumericSetting(seatPauseSettings[seat].transform,"pause-setting-"+seat+"-1","Геймпад: горизонталь",new Vector2(.08f,.44f),new Vector2(.92f,.60f),Profile.Descriptor(GamepadLookSettings.HorizontalPath),v=>SetSeatLookAxis(localSeat,0,v),18);
                seatPauseSettingButtons[seat,2]=NumericSetting(seatPauseSettings[seat].transform,"pause-setting-"+seat+"-2","Геймпад: вертикаль",new Vector2(.08f,.27f),new Vector2(.92f,.43f),Profile.Descriptor(GamepadLookSettings.VerticalPath),v=>SetSeatLookAxis(localSeat,1,v),18);
                seatPauseSettingButtons[seat,3]=BooleanSetting(seatPauseSettings[seat].transform,"pause-setting-"+seat+"-3","Выравнивать взгляд",new Vector2(.08f,.20f),new Vector2(.92f,.26f),v=>{var look=PersonalGamepadLook(localSeat);look.AutoLevel=v;SavePersonalGamepad(localSeat,look);},18);
                seatPauseSettingButtons[seat,4]=BooleanSetting(seatPauseSettings[seat].transform,"pause-setting-"+seat+"-4","Показывать FPS",new Vector2(.08f,.13f),new Vector2(.92f,.19f),v=>{if(v!=PersonalFps(localSeat))TogglePersonalFps(localSeat);},18);
                seatPauseSettingButtons[seat,5]=PauseButton(seatPauseSettings[seat].transform,"pause-setting-"+seat+"-5","Справка по кнопкам",new Vector2(.08f,.065f),new Vector2(.92f,.125f),()=>RunSeatPauseAction(localSeat,5),seat,5);
                seatPauseSettingButtons[seat,6]=PauseButton(seatPauseSettings[seat].transform,"pause-setting-"+seat+"-6","Звук ›",new Vector2(.08f,0),new Vector2(.48f,.06f),()=>RunSeatPauseAction(localSeat,6),seat,6);
                seatPauseSettingButtons[seat,7]=PauseButton(seatPauseSettings[seat].transform,"pause-setting-"+seat+"-7","‹ Назад",new Vector2(.52f,0),new Vector2(.92f,.06f),()=>RunSeatPauseAction(localSeat,7),seat,7);
                seatPauseAudio[seat]=PausePage(card.transform,"audio");
                Label(seatPauseAudio[seat].transform,"title","ЗВУК",30,new Vector2(.08f,.84f),new Vector2(.92f,.97f),TextAnchor.MiddleLeft,MenuGold);
                seatPauseAudioValue[seat]=Label(seatPauseAudio[seat].transform,"values","Громкость общая для всех игроков",17,new Vector2(.08f,.74f),new Vector2(.92f,.84f),TextAnchor.MiddleLeft,Color.white);
                seatPauseAudioButtons[seat,0]=NumericSetting(seatPauseAudio[seat].transform,"pause-audio-"+seat+"-0","Музыка",new Vector2(.08f,.48f),new Vector2(.92f,.73f),Profile.Descriptor("audio.musicDefaultPercent"),v=>{NativeAudioPreferences.SetMusic(Mathf.RoundToInt(v));RefreshSeatPauseUi();},18);
                seatPauseAudioButtons[seat,1]=NumericSetting(seatPauseAudio[seat].transform,"pause-audio-"+seat+"-1","Эффекты",new Vector2(.08f,.20f),new Vector2(.92f,.45f),Profile.Descriptor("audio.effectsDefaultPercent"),v=>{NativeAudioPreferences.SetEffects(Mathf.RoundToInt(v));RefreshSeatPauseUi();},18);
                seatPauseAudioButtons[seat,2]=PauseButton(seatPauseAudio[seat].transform,"pause-audio-"+seat+"-2","‹ Назад",new Vector2(.08f,.04f),new Vector2(.92f,.16f),()=>BackSeatPause(localSeat),seat,2);
                seatPauseHelp[seat]=PausePage(card.transform,"help");
                var helpViewport=Panel(seatPauseHelp[seat].transform,"help-viewport",new Vector2(.04f,.24f),new Vector2(.96f,.97f),Color.clear);
                helpViewport.AddComponent<RectMask2D>();var scroll=helpViewport.AddComponent<ScrollRect>();seatPauseHelpScroll[seat]=scroll;
                scroll.viewport=(RectTransform)helpViewport.transform;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
                seatPauseHelpText[seat]=Label(helpViewport.transform,"controls-help",GamepadHelp,17,new Vector2(0,1),Vector2.one,TextAnchor.UpperLeft,Color.white);
                var textRect=seatPauseHelpText[seat].rectTransform;textRect.pivot=new Vector2(.5f,1);textRect.sizeDelta=Vector2.zero;
                seatPauseHelpText[seat].verticalOverflow=VerticalWrapMode.Overflow;
                textRect.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;scroll.content=textRect;
                Label(seatPauseHelp[seat].transform,"scroll-help","← / → — прокрутка; колесо мыши",14,new Vector2(.04f,.19f),new Vector2(.96f,.24f),TextAnchor.MiddleLeft,MenuGold);
                seatPauseHelpButtons[seat,0]=PauseButton(seatPauseHelp[seat].transform,"pause-help-pad-"+seat,"Геймпад",new Vector2(.04f,.10f),new Vector2(.48f,.18f),()=>SetSeatHelp(localSeat,GamepadHelp),seat,0);
                seatPauseHelpButtons[seat,1]=PauseButton(seatPauseHelp[seat].transform,"pause-help-keys-"+seat,"Клавиатура / мышь",new Vector2(.52f,.10f),new Vector2(.96f,.18f),()=>SetSeatHelp(localSeat,KeyboardHelp),seat,1);
                seatPauseHelpButtons[seat,2]=PauseButton(seatPauseHelp[seat].transform,"pause-help-back-"+seat,"‹ Назад",new Vector2(.04f,.01f),new Vector2(.96f,.09f),()=>BackSeatPause(localSeat),seat,2);
                // Independent per-seat focus: pause bypasses the global EventSystem's navigation.
                for(int j=0;j<8;j++)
                {
                    int localItem=j;var control=seatPauseSettingButtons[seat,j];control.transition=Selectable.Transition.None;control.navigation=new Navigation{mode=Navigation.Mode.None};
                    if(j<5){var trigger=control.gameObject.AddComponent<EventTrigger>();var entry=new EventTrigger.Entry{eventID=EventTriggerType.PointerEnter};entry.callback.AddListener(_=>{seatPauseCursor[localSeat]=localItem;RefreshSeatPauseUi();});trigger.triggers.Add(entry);}
                }
                for(int j=0;j<2;j++)
                {
                    int localItem=j;var control=seatPauseAudioButtons[seat,j];control.transition=Selectable.Transition.None;control.navigation=new Navigation{mode=Navigation.Mode.None};
                    var trigger=control.gameObject.AddComponent<EventTrigger>();var entry=new EventTrigger.Entry{eventID=EventTriggerType.PointerEnter};entry.callback.AddListener(_=>{seatPauseCursor[localSeat]=localItem;RefreshSeatPauseUi();});trigger.triggers.Add(entry);
                }
                seatPauseRepeat[seat]=ConfirmationPage(card.transform,seat,"repeat","НАЧАТЬ ЗАНОВО?",out var repeatYes,out var repeatNo);
                seatPauseRepeatButtons[seat,0]=repeatYes;seatPauseRepeatButtons[seat,1]=repeatNo;
                seatPauseExit[seat]=ConfirmationPage(card.transform,seat,"exit","ВЫЙТИ В ГЛАВНОЕ МЕНЮ?",out var exitYes,out var exitNo);
                seatPauseExitButtons[seat,0]=exitYes;seatPauseExitButtons[seat,1]=exitNo;
                seatPauseFps[seat]=Label(viewportRoots[seat],"seat-fps-"+seat,"FPS —",17,
                    new Vector2(.71f,.91f),new Vector2(.98f,.98f),TextAnchor.MiddleRight,Color.white);
                seatPauseFps[seat].raycastTarget=false;
                root.SetActive(false);
            }
        }

        GameObject PausePage(Transform parent,string name)
        {
            var page=Panel(parent,name,Vector2.zero,Vector2.one,Color.clear);
            page.GetComponent<Image>().raycastTarget=false;
            return page;
        }

        GameObject ConfirmationPage(Transform parent,int seat,string name,string title,out Button yes,out Button no)
        {
            var page=PausePage(parent,name);
            Label(page.transform,"title",title,28,new Vector2(.08f,.78f),new Vector2(.92f,.96f),TextAnchor.MiddleCenter,MenuGold);
            Label(page.transform,"scope","Это действие затронет всех игроков.",20,new Vector2(.08f,.46f),new Vector2(.92f,.72f),TextAnchor.MiddleCenter,Color.white);
            yes=PauseButton(page.transform,"pause-"+name+"-yes-"+seat,"Подтвердить",new Vector2(.08f,.23f),new Vector2(.92f,.37f),
                ()=>RunSeatPauseAction(seat,0),seat,0);
            no=PauseButton(page.transform,"pause-"+name+"-no-"+seat,"Отмена",new Vector2(.08f,.07f),new Vector2(.92f,.21f),
                ()=>RunSeatPauseAction(seat,1),seat,1);
            return page;
        }

        Button PauseButton(Transform parent,string name,string value,Vector2 min,Vector2 max,UnityEngine.Events.UnityAction action,int seat,int index)
        {
            var button=MenuButton(parent,name,value,min,max,action);
            button.transition=Selectable.Transition.None;
            var trigger=button.GetComponent<EventTrigger>()??button.gameObject.AddComponent<EventTrigger>();
            var entry=new EventTrigger.Entry { eventID=EventTriggerType.PointerEnter };
            entry.callback.AddListener(_=>{if(seatPauseCursor[seat]!=index)gameAudio?.MenuMove();seatPauseCursor[seat]=index;RefreshSeatPauseUi();});
            trigger.triggers.Add(entry);
            return button;
        }

        int DefaultPauseSeat()
        {
            for(int i=0;i<LocalSeatCount;i++)if(input.IsHumanSeat(i)&&input.DeviceAt(i) is Keyboard)return i;
            for(int i=0;i<LocalSeatCount;i++)if(input.IsHumanSeat(i))return i;
            return -1;
        }

        int SeatPausePressedMask()
        {
            int mask=0;
            for(int i=0;i<LocalSeatCount;i++)
            {
                if(!input.IsHumanSeat(i)||!input.IsConnected(i))continue;
                var device=input.DeviceAt(i);
                if(device is Keyboard keyboard && keyboard.escapeKey.wasPressedThisFrame ||
                    device is Gamepad pad && pad.startButton.wasPressedThisFrame)mask|=1<<i;
            }
            return mask;
        }

        void OpenSeatPause(int seat)
        {
            if(seat<0||seat>=LocalSeatCount||!input.IsHumanSeat(seat))return;
            seatPausePages[seat]=SeatPausePage.Actions;seatPauseCursor[seat]=0;seatPauseLastStick[seat]=Vector2.zero;
            RefreshSeatPauseUi();
        }

        void UpdateSeatPauseInput()
        {
            if(!FocusAllowsResume)return;
            int mask=SeatPausePressedMask();
            for(int seat=0;seat<LocalSeatCount;seat++)
            {
                if(!input.IsHumanSeat(seat)||!input.IsConnected(seat))continue;
                if((mask&(1<<seat))!=0)
                {
                    if(seatPausePages[seat]==SeatPausePage.Closed)OpenSeatPause(seat);
                    else CloseSeatPause(seat);
                    continue;
                }
                if(seatPausePages[seat]==SeatPausePage.Closed)continue;
                var device=input.DeviceAt(seat);
                bool up=false,down=false,left=false,right=false,submit=false,back=false;
                if(device is Gamepad pad)
                {
                    var nav=pad.dpad.ReadValue();if(nav==Vector2.zero)nav=pad.leftStick.ReadValue();
                    // UI activation threshold is a focus invariant; repeat uses the shared UI module policy.
                    nav=new Vector2(Mathf.Abs(nav.x)>.5f?Mathf.Sign(nav.x):0,Mathf.Abs(nav.y)>.5f?Mathf.Sign(nav.y):0);
                    if(RepeatSeatNavigation(seat,nav)){up=nav.y>0;down=nav.y<0;left=nav.x<0;right=nav.x>0;}
                    submit=pad.buttonSouth.wasPressedThisFrame;back=pad.buttonEast.wasPressedThisFrame;
                }
                else if(device is Keyboard keyboard)
                {
                    var nav=new Vector2((keyboard.rightArrowKey.isPressed?1:0)-(keyboard.leftArrowKey.isPressed?1:0),(keyboard.upArrowKey.isPressed?1:0)-(keyboard.downArrowKey.isPressed?1:0));
                    if(RepeatSeatNavigation(seat,nav)){up=nav.y>0;down=nav.y<0;left=nav.x<0;right=nav.x>0;}
                    submit=keyboard.enterKey.wasPressedThisFrame;back=keyboard.backspaceKey.wasPressedThisFrame;
                }
                if(up)MoveSeatPauseCursor(seat,-1);
                if(down)MoveSeatPauseCursor(seat,1);
                if(left||right)StepSeatPauseSlider(seat,right?1:-1);
                if(back){gameAudio?.MenuBack();BackSeatPause(seat);}
                else if(submit)
                {
                    int item=seatPauseCursor[seat];var page=seatPausePages[seat];
                    if(page==SeatPausePage.Settings&&item==7||page==SeatPausePage.Help&&item==2||page==SeatPausePage.Audio&&item==2)gameAudio?.MenuBack();
                    else gameAudio?.MenuConfirm();
                    RunSeatPauseAction(seat,item);
                }
            }
        }

        bool RepeatSeatNavigation(int seat,Vector2 direction)
        {
            if(direction!=seatPauseLastStick[seat])
            {seatPauseLastStick[seat]=direction;seatPauseRepeatAt[seat]=Time.unscaledTime+menuInputModule.moveRepeatDelay;return direction!=Vector2.zero;}
            if(direction==Vector2.zero||Time.unscaledTime<seatPauseRepeatAt[seat])return false;
            seatPauseRepeatAt[seat]=Time.unscaledTime+menuInputModule.moveRepeatRate;return true;
        }
        int SeatPauseChoiceCount(int seat) => seatPausePages[seat]==SeatPausePage.Settings?8:seatPausePages[seat]==SeatPausePage.Help||seatPausePages[seat]==SeatPausePage.Audio?3:seatPausePages[seat]==SeatPausePage.Actions?4:2;
        void MoveSeatPauseCursor(int seat,int direction)
        {
            int count=SeatPauseChoiceCount(seat);
            seatPauseCursor[seat]=(seatPauseCursor[seat]+direction+count)%count;
            gameAudio?.MenuMove();
            RefreshSeatPauseUi();
        }
        void BackSeatPause(int seat)
        {
            if(seatPausePages[seat]==SeatPausePage.Actions)CloseSeatPause(seat);
            else if(seatPausePages[seat]==SeatPausePage.Help){seatPausePages[seat]=SeatPausePage.Settings;seatPauseCursor[seat]=5;RefreshSeatPauseUi();}
            else if(seatPausePages[seat]==SeatPausePage.Audio){seatPausePages[seat]=SeatPausePage.Settings;seatPauseCursor[seat]=6;RefreshSeatPauseUi();}
            else {seatPausePages[seat]=SeatPausePage.Actions;seatPauseCursor[seat]=1;RefreshSeatPauseUi();}
        }
        void CloseSeatPause(int seat)
        {
            if(seatPausePages[seat]==SeatPausePage.Closed)return;
            bool last=Enumerable.Range(0,LocalSeatCount).All(i=>i==seat||seatPausePages[i]==SeatPausePage.Closed);
            if(last&&(!input.Ready||!FocusAllowsResume))
            {
                seatPausePages[seat]=SeatPausePage.Actions;seatPauseCursor[seat]=0;RefreshSeatPauseUi();
                return;
            }
            seatPausePages[seat]=SeatPausePage.Closed;
            if(last)
            {
                EventSystem.current.sendNavigationEvents=true;
                Resume();
            }
            RefreshInterface();
        }
        void ClearSeatPauseMenus()
        {
            for(int i=0;i<seatPausePages.Length;i++){seatPausePages[i]=SeatPausePage.Closed;seatPauseCursor[i]=0;}
            if(EventSystem.current)EventSystem.current.sendNavigationEvents=true;
        }

        void RunSeatPauseAction(int seat,int item)
        {
            if(phase!=Phase.Paused||!UseSeatPauseMenus||seat<0||seat>=LocalSeatCount||!input.IsHumanSeat(seat))return;
            switch(seatPausePages[seat])
            {
                case SeatPausePage.Actions:
                    if(item==0)CloseSeatPause(seat);
                    else if(item==1)SetSeatPausePage(seat,SeatPausePage.Settings);
                    else if(item==2)SetSeatPausePage(seat,SeatPausePage.ConfirmRepeat);
                    else if(item==3)SetSeatPausePage(seat,SeatPausePage.ConfirmExit);
                    break;
                case SeatPausePage.Settings:
                    if(item==3||item==4){var toggle=(Toggle)seatPauseSettingButtons[seat,item];toggle.isOn=!toggle.isOn;}
                    else if(item==5)SetSeatPausePage(seat,SeatPausePage.Help);
                    else if(item==6)SetSeatPausePage(seat,SeatPausePage.Audio);
                    else if(item==7)BackSeatPause(seat);
                    break;
                case SeatPausePage.Help:
                    if(item==2)BackSeatPause(seat);else SetSeatHelp(seat,item==0?GamepadHelp:KeyboardHelp);
                    break;
                case SeatPausePage.Audio:
                    if(item==2)BackSeatPause(seat);
                    break;
                case SeatPausePage.ConfirmRepeat:
                    if(item==0)
                    {
                        if(!input.Ready)return;
                        ClearSeatPauseMenus();Repeat();RefreshInterface();
                    }
                    else SetSeatPausePage(seat,SeatPausePage.Actions);
                    break;
                case SeatPausePage.ConfirmExit:
                    if(item==0){ClearSeatPauseMenus();ToMainMenu();}
                    else SetSeatPausePage(seat,SeatPausePage.Actions);
                    break;
            }
            RefreshSeatPauseUi();
        }
        void SetSeatLookAxis(int seat,int axis,float value)
        {var look=PersonalGamepadLook(seat);if(axis==0)look.Horizontal=value;else look.Vertical=value;SavePersonalGamepad(seat,look);RefreshSeatPauseUi();}
        void StepSeatPauseSlider(int seat,int direction)
        {
            // A quarter-page is a UI navigation increment, independent of gameplay tuning.
            if(seatPausePages[seat]==SeatPausePage.Help){seatPauseHelpScroll[seat].verticalNormalizedPosition=Mathf.Clamp01(seatPauseHelpScroll[seat].verticalNormalizedPosition-direction*.25f);return;}
            if(seatPausePages[seat]==SeatPausePage.Settings && seatPauseSettingButtons[seat,seatPauseCursor[seat]] is SettingsSlider settingsSlider)
                settingsSlider.value+=direction*settingsSlider.Step;
            else if(seatPausePages[seat]==SeatPausePage.Audio && seatPauseAudioButtons[seat,seatPauseCursor[seat]] is SettingsSlider audioSlider)
                audioSlider.value+=direction*audioSlider.Step;
        }
        void SetSeatHelp(int seat,string text)
        {seatPauseHelpText[seat].text=text;Canvas.ForceUpdateCanvases();seatPauseHelpScroll[seat].verticalNormalizedPosition=1;}
        void SetSeatPausePage(int seat,SeatPausePage page)
        {
            seatPausePages[seat]=page;seatPauseCursor[seat]=0;RefreshSeatPauseUi();
        }

        void RefreshSeatPauseUi()
        {
            for(int seat=0;seat<SeatInputCoordinator.SeatCount;seat++)
            {
                if(seatPauseRoots[seat]==null)continue;
                bool active=phase==Phase.Paused&&UseSeatPauseMenus&&seat<LocalSeatCount;
                seatPauseRoots[seat].SetActive(active);
                bool human=active&&input.IsHumanSeat(seat);
                var page=seatPausePages[seat];
                seatPauseCards[seat].SetActive(human&&page!=SeatPausePage.Closed);
                seatPauseCards[seat].GetComponent<CanvasGroup>().blocksRaycasts=human&&input.DeviceAt(seat) is Keyboard;
                seatPauseWaiting[seat].SetActive(active&&(!human||page==SeatPausePage.Closed));
                seatPauseActions[seat].SetActive(human&&page==SeatPausePage.Actions);
                seatPauseSettings[seat].SetActive(human&&page==SeatPausePage.Settings);
                seatPauseHelp[seat].SetActive(human&&page==SeatPausePage.Help);
                seatPauseAudio[seat].SetActive(human&&page==SeatPausePage.Audio);
                seatPauseRepeat[seat].SetActive(human&&page==SeatPausePage.ConfirmRepeat);
                seatPauseExit[seat].SetActive(human&&page==SeatPausePage.ConfirmExit);
                if(human)
                {
                    seatPauseReason[seat].text=pauseReason;
                    seatPauseSettingsValue[seat].text=IdentityLabel(seat);
                    var look=PersonalGamepadLook(seat);
                    RefreshNumericSetting((Slider)seatPauseSettingButtons[seat,0],"Мышь",PersonalSensitivity(seat),"°/px");
                    RefreshNumericSetting((Slider)seatPauseSettingButtons[seat,1],"Геймпад: горизонталь",look.Horizontal,"°/с");
                    RefreshNumericSetting((Slider)seatPauseSettingButtons[seat,2],"Геймпад: вертикаль",look.Vertical,"°/с");
                    RefreshToggle((Toggle)seatPauseSettingButtons[seat,3],look.AutoLevel);RefreshToggle((Toggle)seatPauseSettingButtons[seat,4],PersonalFps(seat));
                    RefreshNumericSetting((Slider)seatPauseAudioButtons[seat,0],"Музыка",NativeAudioPreferences.Music(Profile),"%");
                    RefreshNumericSetting((Slider)seatPauseAudioButtons[seat,1],"Эффекты",NativeAudioPreferences.Effects(Profile),"%");
                    seatPauseActionButtons[seat,0].interactable=input.Ready&&FocusAllowsResume;
                    PaintSeatButtons(seat,page);
                }
                bool showFps=(phase==Phase.Running||phase==Phase.Paused)&&UseSeatPauseMenus&&seat<LocalSeatCount&&input.IsHumanSeat(seat)&&PersonalFps(seat);
                seatPauseFps[seat].gameObject.SetActive(showFps);
                if(showFps)seatPauseFps[seat].text=fps.CurrentText;
            }
        }
        void PaintSeatButtons(int seat,SeatPausePage page)
        {
            int count=SeatPauseChoiceCount(seat);
            for(int item=0;item<count;item++)
            {
                Selectable button=page==SeatPausePage.Actions?seatPauseActionButtons[seat,item]:
                    page==SeatPausePage.Settings?seatPauseSettingButtons[seat,item]:
                    page==SeatPausePage.Help?seatPauseHelpButtons[seat,item]:
                    page==SeatPausePage.Audio?seatPauseAudioButtons[seat,item]:
                    page==SeatPausePage.ConfirmRepeat?seatPauseRepeatButtons[seat,item]:seatPauseExitButtons[seat,item];
                var image=button.GetComponent<Image>();
                if(image)image.color=!button.interactable?new Color32(46,57,63,255):item==seatPauseCursor[seat]?new Color32(61,96,112,255):MenuCard;
                if(button is Slider){button.transform.parent.GetComponent<Image>().color=item==seatPauseCursor[seat]?new Color32(61,96,112,255):MenuCard;}

            }
        }
    }
}
