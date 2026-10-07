using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingGround
    {
        enum SeatPausePage { Closed, Actions, Settings, ConfirmRepeat, ConfirmExit }
        readonly SeatPausePage[] seatPausePages = new SeatPausePage[SeatInputCoordinator.SeatCount];
        readonly int[] seatPauseCursor = new int[SeatInputCoordinator.SeatCount];
        readonly GameObject[] seatPauseRoots = new GameObject[SeatInputCoordinator.SeatCount];
        readonly GameObject[] seatPauseCards = new GameObject[SeatInputCoordinator.SeatCount];
        readonly GameObject[] seatPauseWaiting = new GameObject[SeatInputCoordinator.SeatCount];
        readonly GameObject[] seatPauseActions = new GameObject[SeatInputCoordinator.SeatCount];
        readonly GameObject[] seatPauseRepeat = new GameObject[SeatInputCoordinator.SeatCount];
        readonly GameObject[] seatPauseExit = new GameObject[SeatInputCoordinator.SeatCount];
        readonly Text[] seatPauseReason = new Text[SeatInputCoordinator.SeatCount];
        readonly Text[] seatPauseFps = new Text[SeatInputCoordinator.SeatCount];
        readonly Button[,] seatPauseActionButtons = new Button[SeatInputCoordinator.SeatCount,4];
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
                var card=Panel(root.transform,"pause-card",new Vector2(.11f,.09f),new Vector2(.89f,.91f),new Color32(20,33,48,248));
                seatPauseCards[seat]=card;
                card.AddComponent<CanvasGroup>();
                seatPauseActions[seat]=PausePage(card.transform,"actions");
                Label(seatPauseActions[seat].transform,"title","Пауза",40,new Vector2(.08f,.83f),new Vector2(.92f,.96f),TextAnchor.MiddleCenter,MenuGold);
                seatPauseReason[seat]=Label(seatPauseActions[seat].transform,"reason","",30,new Vector2(.08f,.72f),new Vector2(.92f,.83f),TextAnchor.MiddleCenter,Color.white);
                string[] actionLabels={"Продолжить","Настройки","Начать заново","Выйти в главное меню"};
                for(int item=0;item<4;item++)
                {
                    int localItem=item;
                    float top=.70f-item*.155f;
                    seatPauseActionButtons[seat,item]=PauseButton(seatPauseActions[seat].transform,"pause-action-"+seat+"-"+item,actionLabels[item],
                        new Vector2(.08f,top-.11f),new Vector2(.92f,top),()=>RunSeatPauseAction(localSeat,localItem),seat,item);
                }
                seatSettingsViews[seat]=new SettingsView(this,true,seat,()=>BackSeatPause(localSeat));
                seatSettingsViews[seat].CreateSettingsUi(card.transform);
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
            entry.callback.AddListener(_=>{seatPauseCursor[seat]=index;RefreshSeatPauseUi();});
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
                    else if(seatPausePages[seat]==SeatPausePage.Settings&&input.DeviceAt(seat) is Keyboard)seatSettingsViews[seat].Back();
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
                if(seatPausePages[seat]==SeatPausePage.Settings)
                {seatSettingsViews[seat].Navigate(right?1:left?-1:0,up?1:down?-1:0,submit,back);continue;}
                if(up)MoveSeatPauseCursor(seat,-1);
                if(down)MoveSeatPauseCursor(seat,1);
                if(back){gameAudio?.MenuBack();BackSeatPause(seat);}
                else if(submit)
                {
                    int item=seatPauseCursor[seat];var page=seatPausePages[seat];
                    gameAudio?.MenuConfirm();
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
        int SeatPauseChoiceCount(int seat) => seatPausePages[seat]==SeatPausePage.Actions?4:2;
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
            else {seatPausePages[seat]=SeatPausePage.Actions;seatPauseCursor[seat]=1;RefreshSeatPauseUi();}
        }

        void CloseSeatPause(int seat)
        {
            if(seatPausePages[seat]==SeatPausePage.Closed)return;
            if(displayConfirmationActive&&displayOwner==seatSettingsViews[seat])RollbackDisplay();
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
            if(displayConfirmationActive&&displayOwner!=settingsView)RollbackDisplay();
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
        void SetSeatPausePage(int seat,SeatPausePage page)
        {
            seatPausePages[seat]=page;seatPauseCursor[seat]=0;RefreshSeatPauseUi();if(page==SeatPausePage.Settings)seatSettingsViews[seat].Open(seat);
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
                seatSettingsViews[seat].Root.SetActive(human&&page==SeatPausePage.Settings);
                // A settings view follows the same full-panel proportions as the shell, within this viewport.
                Layout((RectTransform)seatPauseCards[seat].transform,page==SeatPausePage.Settings?Vector2.zero:new Vector2(.11f,.09f),page==SeatPausePage.Settings?Vector2.one:new Vector2(.89f,.91f));
                if(page!=SeatPausePage.Settings)
                {
                    // Keep action dialogs compact on a full viewport; retain proportional sizing in split-screen.
                    var cardRect=(RectTransform)seatPauseCards[seat].transform;
                    var viewport=(RectTransform)cardRect.parent;
                    float width=Mathf.Min(viewport.rect.width*.78f,760),height=Mathf.Min(viewport.rect.height*.82f,650);
                    cardRect.anchorMin=cardRect.anchorMax=new Vector2(.5f,.5f);cardRect.sizeDelta=new Vector2(width,height);cardRect.anchoredPosition=Vector2.zero;
                }
                seatPauseRepeat[seat].SetActive(human&&page==SeatPausePage.ConfirmRepeat);
                seatPauseExit[seat].SetActive(human&&page==SeatPausePage.ConfirmExit);
                if(human)
                {
                    seatPauseReason[seat].text=IdentityLabel(seat);
                    if(page==SeatPausePage.Settings)seatSettingsViews[seat].RefreshSettingsUi();
                    seatPauseActionButtons[seat,0].interactable=input.Ready&&FocusAllowsResume;
                    if(page!=SeatPausePage.Settings&&page!=SeatPausePage.Closed)PaintSeatButtons(seat,page);
                }
                bool showFps=(phase==Phase.Running||phase==Phase.Paused)&&UseSeatPauseMenus&&seat<LocalSeatCount&&input.IsHumanSeat(seat)&&PersonalFps(seat);
                seatPauseFps[seat].gameObject.SetActive(false);
                if(showFps)seatPauseFps[seat].text=fps.CurrentText;
            }
        }
        void PaintSeatButtons(int seat,SeatPausePage page)
        {
            int count=SeatPauseChoiceCount(seat);
            for(int item=0;item<count;item++)
            {
                Selectable button=page==SeatPausePage.Actions?seatPauseActionButtons[seat,item]:
                    page==SeatPausePage.ConfirmRepeat?seatPauseRepeatButtons[seat,item]:seatPauseExitButtons[seat,item];
                var image=button.GetComponent<Image>();
                if(image)image.color=!button.interactable?new Color32(46,57,63,255):MenuCard;
                button.GetComponent<MenuPresentation>()?.SetFocused(item==seatPauseCursor[seat]);
                if(button is Slider){button.transform.parent.GetComponent<Image>().color=item==seatPauseCursor[seat]?new Color32(61,96,112,255):MenuCard;}

            }
        }
    }
}
