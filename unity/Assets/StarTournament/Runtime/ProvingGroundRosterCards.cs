using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingGround
    {
        // Authored uGUI layout in the existing Full HD canvas; these sizes do not tune gameplay/readability profiles.
        readonly Button[] rosterCards=new Button[NativeMatchRoster.MaximumParticipants];
        readonly Text[] rosterNames=new Text[NativeMatchRoster.MaximumParticipants],rosterDetails=new Text[NativeMatchRoster.MaximumParticipants],rosterViews=new Text[NativeMatchRoster.MaximumParticipants],rosterIcons=new Text[NativeMatchRoster.MaximumParticipants];
        readonly Image[] rosterStripes=new Image[NativeMatchRoster.MaximumParticipants];
        GameObject rosterEditor,rosterPicker;
        Toggle rosterBotView;
        Transform rosterFfa,rosterBlue,rosterRed,rosterPickerContent;
        ScrollRect rosterFfaScroll,rosterBlueScroll,rosterRedScroll,rosterChoiceScroll;
        GameObject rosterBluePanel,rosterRedPanel;
        Text rosterPageTitle,rosterCount,rosterFull,rosterEditorName,rosterEditorSubtitle,rosterEditorHelp,rosterPickerHeading;
        Text rosterBlueHeading,rosterRedHeading,rosterBlueCount,rosterRedCount;
        Button rosterAddHuman,rosterAddBot,rosterIdentity,rosterDevice,rosterTeam,rosterRemove,rosterDone,rosterPickerBack;
        Text rosterIdentityLabel,rosterDeviceLabel,rosterTeamLabel;
        int rosterEditing=-1;
        Button rosterPickerReturn;
        readonly List<Button> rosterChoices=new List<Button>();
        string rosterLastBlocking;
        static readonly string[] RosterDifficultyNames={"Новичок","Боец","Ветеран"};
        static readonly Color32 RosterMuted=new Color32(153,173,187,255);
        static readonly Color32 RosterBlue=new Color32(106,204,239,255),RosterRed=new Color32(242,128,147,255);

        static Sprite rosterRounded;
        void RoundRosterPanel(GameObject panel)
        {
            if(rosterRounded==null)
            {
                // Authored corner mask for sliced uGUI panels, not a gameplay tuning value.
                const int size=64,radius=14;
                var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);texture.name="roster-panel-corners";
                var pixels=new Color[size*size];
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {
                    float dx=Mathf.Max(radius-x-.5f,x+.5f-(size-radius)),dy=Mathf.Max(radius-y-.5f,y+.5f-(size-radius));
                    float distance=new Vector2(Mathf.Max(0,dx),Mathf.Max(0,dy)).magnitude;
                    pixels[y*size+x]=new Color(1,1,1,Mathf.Clamp01(radius+.5f-distance));
                }
                texture.SetPixels(pixels);texture.Apply();texture.wrapMode=TextureWrapMode.Clamp;
                rosterRounded=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(radius,radius,radius,radius));
            }
            var image=panel.GetComponent<Image>();image.sprite=rosterRounded;image.type=Image.Type.Sliced;
        }
        Button RosterButton(Transform parent,string name,string title,Vector2 min,Vector2 max,UnityEngine.Events.UnityAction action)
        {
            var b=MenuButton(parent,name,title,min,max,action);RoundRosterPanel(b.gameObject);
            var text=b.GetComponentInChildren<Text>();text.fontSize=26;text.fontStyle=FontStyle.Bold;
            var colors=b.colors;colors.highlightedColor=colors.selectedColor=Color.white;b.colors=colors;
            AddRosterFocus(b);
            return b;
        }
        void AddRosterFocus(Button button)
        {
            var focus=button.gameObject.AddComponent<RosterFocus>();
            focus.Initialize(MenuGold);
        }
        Transform RosterScroll(Transform parent,string name,Vector2 min,Vector2 max,out ScrollRect scroll)
        {
            var shell=Panel(parent,name,min,max,new Color32(23,37,52,255));
            var viewport=Panel(shell.transform,"viewport",Vector2.zero,Vector2.one,new Color(0,0,0,0));viewport.AddComponent<RectMask2D>();
            var content=new GameObject("content",typeof(RectTransform));content.transform.SetParent(viewport.transform,false);
            var rect=(RectTransform)content.transform;rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,1);rect.sizeDelta=Vector2.zero;
            scroll=shell.AddComponent<ScrollRect>();scroll.viewport=(RectTransform)viewport.transform;scroll.content=rect;
            scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;
            return content.transform;
        }
        void CreateRosterUi(Transform parent)
        {
            AddRosterFocus(start);RoundRosterPanel(start.gameObject);
            rosterPageTitle=Label(setupScreen.transform,"roster-page-title","СОБЕРИТЕ УЧАСТНИКОВ",40,new Vector2(.05f,.865f),new Vector2(.95f,.92f),TextAnchor.MiddleLeft,Color.white);
            rosterPageTitle.fontStyle=FontStyle.Bold;
            var startColors=start.colors;startColors.normalColor=startColors.highlightedColor=startColors.selectedColor=Color.white;startColors.disabledColor=new Color(.45f,.50f,.55f);start.colors=startColors;
            Label(setupPlayersPage.transform,"players-title","СОСТАВ МАТЧА",30,new Vector2(.017f,.88f),new Vector2(.62f,.98f),TextAnchor.MiddleLeft,Color.white).fontStyle=FontStyle.Bold;
            RoundRosterPanel(setupPlayersPage);RoundRosterPanel(setupSummary.transform.parent.gameObject);
            rosterCount=Label(setupPlayersPage.transform,"roster-count","",24,new Vector2(.017f,.80f),new Vector2(.65f,.89f),TextAnchor.MiddleLeft,RosterMuted);
            rosterAddHuman=RosterButton(setupPlayersPage.transform,"roster-add-human","+ ИГРОК",new Vector2(.70f,.865f),new Vector2(.835f,.97f),AddRosterHuman);
            rosterAddBot=RosterButton(setupPlayersPage.transform,"roster-add-bot","+ БОТ",new Vector2(.845f,.865f),new Vector2(.98f,.97f),()=>{int p=LocalSeatCount+botSetup.Count;AddBot();if(LocalSeatCount+botSetup.Count>p){OpenRosterEditor(p);Select(rosterDone);}});
            rosterFull=Label(setupPlayersPage.transform,"roster-full","",20,new Vector2(.68f,.795f),new Vector2(.98f,.862f),TextAnchor.MiddleCenter,RosterMuted);
            rosterFfa=RosterScroll(setupPlayersPage.transform,"roster-ffa",new Vector2(.017f,.025f),new Vector2(.98f,.77f),out rosterFfaScroll);
            rosterBluePanel=Panel(setupPlayersPage.transform,"roster-team-blue",new Vector2(.017f,.025f),new Vector2(.49f,.77f),new Color32(23,37,52,255));
            rosterRedPanel=Panel(setupPlayersPage.transform,"roster-team-red",new Vector2(.51f,.025f),new Vector2(.98f,.77f),new Color32(23,37,52,255));
            rosterBlueHeading=Label(rosterBluePanel.transform,"team-blue-name","",26,new Vector2(.02f,.88f),new Vector2(.76f,1),TextAnchor.MiddleLeft,RosterBlue);
            rosterRedHeading=Label(rosterRedPanel.transform,"team-red-name","",26,new Vector2(.02f,.88f),new Vector2(.76f,1),TextAnchor.MiddleLeft,RosterRed);
            rosterBlueCount=Label(rosterBluePanel.transform,"team-blue-count","",22,new Vector2(.76f,.88f),new Vector2(.98f,1),TextAnchor.MiddleRight,RosterMuted);
            rosterRedCount=Label(rosterRedPanel.transform,"team-red-count","",22,new Vector2(.76f,.88f),new Vector2(.98f,1),TextAnchor.MiddleRight,RosterMuted);
            rosterBlue=RosterScroll(rosterBluePanel.transform,"team-blue-scroll",new Vector2(.02f,.02f),new Vector2(.98f,.88f),out rosterBlueScroll);
            rosterRed=RosterScroll(rosterRedPanel.transform,"team-red-scroll",new Vector2(.02f,.02f),new Vector2(.98f,.88f),out rosterRedScroll);
            for(int i=0;i<rosterCards.Length;i++)
            {
                int p=i;var b=RosterButton(rosterFfa,"roster-card-"+i,"",Vector2.zero,Vector2.one,()=>OpenRosterEditor(p));rosterCards[i]=b;b.GetComponent<MenuPresentation>().Compact=false;
                b.GetComponentInChildren<Text>().gameObject.SetActive(false);
                b.GetComponent<Image>().color=new Color32(27,43,59,255);
                rosterStripes[i]=Panel(b.transform,"stripe",new Vector2(.005f,.075f),new Vector2(.02f,.925f),MenuGold).GetComponent<Image>();
                var icon=Panel(b.transform,"icon",new Vector2(.05f,.60f),new Vector2(.195f,.89f),new Color32(21,44,59,255));
                RoundRosterPanel(icon);
                rosterIcons[i]=Label(icon.transform,"kind","",30,Vector2.zero,Vector2.one,TextAnchor.MiddleCenter,MenuGold);
                rosterNames[i]=Label(b.transform,"participant-name","",30,new Vector2(.235f,.70f),new Vector2(.94f,.91f),TextAnchor.MiddleLeft,Color.white);
                rosterNames[i].fontStyle=FontStyle.Bold;
                rosterNames[i].resizeTextForBestFit=true;rosterNames[i].resizeTextMinSize=20;rosterNames[i].resizeTextMaxSize=30;
                rosterDetails[i]=Label(b.transform,"participant-detail","",22,new Vector2(.235f,.54f),new Vector2(.96f,.72f),TextAnchor.MiddleLeft,RosterMuted);
                Panel(b.transform,"rule",new Vector2(.055f,.49f),new Vector2(.945f,.495f),new Color32(62,88,102,255));
                rosterDetails[i].resizeTextForBestFit=true;rosterDetails[i].resizeTextMinSize=18;rosterDetails[i].resizeTextMaxSize=22;
                rosterViews[i]=Label(b.transform,"participant-view","",23,new Vector2(.06f,.22f),new Vector2(.88f,.45f),TextAnchor.MiddleLeft,Color.white);
                Label(b.transform,"open","›",32,new Vector2(.87f,.16f),new Vector2(.96f,.46f),TextAnchor.MiddleCenter,RosterMuted);
                foreach(var graphic in b.GetComponentsInChildren<Graphic>())if(graphic.gameObject!=b.gameObject)graphic.raycastTarget=false;
                b.GetComponent<RosterFocus>().UseScroll=()=>SetupMode==NativeMatchMode.Teams? (RosterTeamAt(p)==NativeTeam.TeamA?rosterBlueScroll:rosterRedScroll):rosterFfaScroll;
            }
            rosterEditor=Panel(parent,"roster-editor",Vector2.zero,Vector2.one,new Color32(4,12,19,215));
            var card=Panel(rosterEditor.transform,"roster-editor-card",new Vector2(.305f,.16f),new Vector2(.695f,.84f),new Color32(24,38,54,255));
            RoundRosterPanel(card);Frame(card.transform,MenuGold);
            Label(card.transform,"editor-title","НАСТРОЙКА УЧАСТНИКА",25,new Vector2(.055f,.90f),new Vector2(.945f,.97f),TextAnchor.MiddleLeft,MenuGold);
            rosterEditorName=Label(card.transform,"editor-name","",40,new Vector2(.055f,.80f),new Vector2(.945f,.90f),TextAnchor.MiddleLeft,Color.white);
            rosterEditorSubtitle=Label(card.transform,"editor-subtitle","",24,new Vector2(.055f,.74f),new Vector2(.945f,.81f),TextAnchor.MiddleLeft,RosterMuted);
            rosterIdentityLabel=Label(card.transform,"editor-identity-label","",23,new Vector2(.055f,.655f),new Vector2(.945f,.72f),TextAnchor.MiddleLeft,RosterMuted);
            rosterIdentity=RosterButton(card.transform,"roster-identity","",new Vector2(.055f,.57f),new Vector2(.945f,.65f),OpenRosterIdentityChoices);
            rosterTeamLabel=Label(card.transform,"editor-team-label","Команда",23,new Vector2(.055f,.515f),new Vector2(.945f,.57f),TextAnchor.MiddleLeft,RosterMuted);
            rosterTeam=RosterButton(card.transform,"roster-team","",new Vector2(.055f,.43f),new Vector2(.945f,.51f),OpenRosterTeamChoices);
            rosterDeviceLabel=Label(card.transform,"editor-device-label","",23,new Vector2(.055f,.375f),new Vector2(.945f,.43f),TextAnchor.MiddleLeft,RosterMuted);
            rosterDevice=RosterButton(card.transform,"roster-device","",new Vector2(.055f,.29f),new Vector2(.945f,.37f),OpenRosterDeviceChoices);
            rosterBotView=BooleanSetting(card.transform,"roster-bot-view","Собственный экран",new Vector2(.055f,.29f),new Vector2(.945f,.37f),SetRosterBotView);
            foreach(var field in new[]{rosterIdentity,rosterTeam,rosterDevice})
            {
                var value=field.GetComponentInChildren<Text>();value.fontStyle=FontStyle.Normal;value.alignment=TextAnchor.MiddleLeft;
                value.rectTransform.offsetMin=new Vector2(24,0);value.rectTransform.offsetMax=new Vector2(-55,0);
                Label(field.transform,"dropdown-arrow","▾",26,new Vector2(.90f,0),new Vector2(.98f,1),TextAnchor.MiddleCenter,RosterMuted);
            }
            rosterEditorHelp=Label(card.transform,"editor-help","",21,new Vector2(.055f,.19f),new Vector2(.945f,.285f),TextAnchor.MiddleLeft,RosterMuted);
            rosterRemove=RosterButton(card.transform,"roster-remove","УДАЛИТЬ ИЗ МАТЧА",new Vector2(.055f,.16f),new Vector2(.945f,.23f),RemoveRosterParticipant);
            rosterDone=RosterButton(card.transform,"roster-done","ГОТОВО",new Vector2(.055f,.06f),new Vector2(.945f,.14f),CloseRosterEditor);
            rosterDone.GetComponent<Image>().color=MenuGold;rosterDone.GetComponentInChildren<Text>().color=MenuInk;
            rosterPicker=Panel(card.transform,"roster-picker",Vector2.zero,Vector2.one,new Color32(24,38,54,255));
            rosterPickerHeading=Label(rosterPicker.transform,"picker-title","",28,new Vector2(.30f,.85f),new Vector2(.945f,.97f),TextAnchor.MiddleLeft,MenuGold);
            rosterPickerContent=RosterScroll(rosterPicker.transform,"roster-picker-scroll",new Vector2(.055f,.16f),new Vector2(.945f,.83f),out rosterChoiceScroll);
            rosterPickerBack=RosterButton(rosterPicker.transform,"roster-picker-back","‹ НАЗАД",new Vector2(.055f,.86f),new Vector2(.28f,.95f),CloseRosterPicker);
            rosterPicker.SetActive(false);rosterEditor.SetActive(false);
        }
        void Frame(Transform parent,Color color)
        {
            // Two canvas pixels: a focus affordance, independent of simulation tuning.
            foreach(var edge in new[]{0,1,2,3})
            {
                var image=Panel(parent,"frame-"+edge,edge==0?Vector2.zero:edge==1?new Vector2(0,1):edge==2?Vector2.zero:new Vector2(1,0),edge==0?new Vector2(1,0):edge==1?Vector2.one:edge==2?new Vector2(0,1):Vector2.one,color).GetComponent<Image>();
                var r=image.rectTransform;r.sizeDelta=edge<2?new Vector2(0,2):new Vector2(2,0);image.raycastTarget=false;
            }
        }
        int RosterTotal=>LocalSeatCount+botSetup.Count;
        bool RosterIsBot(int p)=>p>=LocalSeatCount||botSetup.IsAi(p);
        NativeTeam RosterTeamAt(int p)=>p<LocalSeatCount?teamAssignments[p]:botSetup.At(p-LocalSeatCount).Team;
        int RosterDifficultyAt(int p)=>p<LocalSeatCount?botSetup.SeatDifficulty(p):botSetup.At(p-LocalSeatCount).Difficulty;
        string RosterTeamName(NativeTeam team)=>(team==NativeTeam.TeamA)^swappedTeamColors?"Синяя":"Красная";
        Color RosterTeamColor(NativeTeam team)=>(team==NativeTeam.TeamA)^swappedTeamColors?RosterBlue:RosterRed;
        void LayoutRosterGrid(Transform content,int[] participants,int columns,float height)
        {
            var rect=(RectTransform)content;int rows=Math.Max(2,Mathf.CeilToInt(participants.Length/(float)columns));
            rect.sizeDelta=new Vector2(0,rows*height);
            for(int i=0;i<participants.Length;i++)
            {
                var card=rosterCards[participants[i]];card.transform.SetParent(content,false);
                var r=(RectTransform)card.transform;r.anchorMin=new Vector2((i%columns)/(float)columns,1);r.anchorMax=new Vector2((i%columns+1)/(float)columns,1);r.pivot=new Vector2(.5f,1);
                r.offsetMin=new Vector2(7,-(i/columns+1)*height+8);r.offsetMax=new Vector2(-7,-(i/columns)*height-8);
            }
        }
        void RefreshRosterUi()
        {
            if(rosterEditor==null)return;
            bool teams=SetupMode==NativeMatchMode.Teams;
            rosterFfaScroll.gameObject.SetActive(!teams);rosterBluePanel.SetActive(teams);rosterRedPanel.SetActive(teams);
            for(int i=0;i<rosterCards.Length;i++)
            {
                bool active=i<RosterTotal;rosterCards[i].gameObject.SetActive(active);if(!active)continue;
                bool bot=RosterIsBot(i);rosterNames[i].text=bot?"Бот":IdentityLabel(i);
                string deviceLabel=bot?"":input.DeviceAt(i) is Gamepad?"Геймпад #"+input.DeviceAt(i).deviceId:input.Label(i);
                rosterDetails[i].text=bot?RosterDifficultyNames[RosterDifficultyAt(i)]:deviceLabel+(input.DeviceAt(i)!=null&&!input.IsConnected(i)?" · отключено":"");
                rosterDetails[i].color=!bot&&!input.IsConnected(i)?new Color32(255,171,142,255):RosterMuted;
                rosterViews[i].text=i<LocalSeatCount?"Экран "+(i+1):"";rosterViews[i].gameObject.SetActive(i<LocalSeatCount);
                rosterIcons[i].text=bot?"Б":"И";var color=teams?RosterTeamColor(RosterTeamAt(i)):(Color)MenuGold;
                rosterIcons[i].color=color;rosterStripes[i].color=color;
            }
            var all=Enumerable.Range(0,RosterTotal).ToArray();
            if(teams)
            {
                var blue=all.Where(i=>RosterTeamAt(i)==NativeTeam.TeamA).ToArray();var red=all.Where(i=>RosterTeamAt(i)==NativeTeam.TeamB).ToArray();
                LayoutRosterGrid(rosterBlue,blue,2,185);LayoutRosterGrid(rosterRed,red,2,185);
                rosterBlueHeading.text=RosterTeamName(NativeTeam.TeamA).ToUpperInvariant()+" КОМАНДА";rosterRedHeading.text=RosterTeamName(NativeTeam.TeamB).ToUpperInvariant()+" КОМАНДА";
                rosterBlueHeading.color=RosterTeamColor(NativeTeam.TeamA);rosterRedHeading.color=RosterTeamColor(NativeTeam.TeamB);
                rosterBlueCount.text=blue.Length+" игр.";rosterRedCount.text=red.Length+" игр.";
            }
            else LayoutRosterGrid(rosterFfa,all,4,215);
            int humans=botSetup.HumanCount(LocalSeatCount);
            rosterCount.text=humans+" чел.  ·  "+(RosterTotal-humans)+" ботов  ·  "+RosterTotal+"/8 участников  ·  "+LocalSeatCount+" экр.";
            rosterAddHuman.interactable=RosterTotal<8 && LocalSeatCount<4;rosterAddBot.interactable=RosterTotal<8;
            rosterAddHuman.GetComponentInChildren<Text>().color=rosterAddHuman.interactable?Color.white:(Color)new Color32(127,153,165,255);
            rosterAddBot.GetComponentInChildren<Text>().color=rosterAddBot.interactable?Color.white:(Color)new Color32(127,153,165,255);
            rosterFull.text=RosterTotal==8?"Состав заполнен":LocalSeatCount==4?"Все 4 экрана назначены":"";
            rosterEditor.SetActive(phase==Phase.Setup&&setupStep==2&&rosterEditing>=0);
            if(rosterEditing>=0 && rosterEditing<RosterTotal)RefreshRosterEditor();
            ConfigureRosterNavigation();
        }
        void OpenRosterEditor(int p)
        {
            if(phase!=Phase.Setup||setupStep!=2||p<0||p>=RosterTotal)return;
            pendingSeat=-1;rosterEditing=p;rosterPicker.SetActive(false);RefreshInterface();Select(rosterIdentity);
        }
        void CloseRosterEditor()
        {
            int p=rosterEditing;rosterEditing=-1;rosterPicker.SetActive(false);RefreshInterface();FocusRosterCard(p);
        }
        void FocusRosterCard(int p){if(RosterTotal>0)Select(rosterCards[Mathf.Clamp(p,0,RosterTotal-1)]);else Select(rosterAddHuman);}
        void RefreshRosterEditor()
        {
            int p=rosterEditing;bool bot=RosterIsBot(p),teams=SetupMode==NativeMatchMode.Teams;
            rosterEditorName.text=bot?"Бот":IdentityLabel(p);
            rosterEditorSubtitle.text=teams?RosterTeamName(RosterTeamAt(p))+" команда":"Каждый за себя";
            rosterIdentityLabel.text=bot?"Сложность":"Профиль / гость";
            rosterIdentity.GetComponentInChildren<Text>().text=(bot?RosterDifficultyNames[RosterDifficultyAt(p)]:IdentityLabel(p));
            rosterTeam.gameObject.SetActive(teams);rosterTeamLabel.gameObject.SetActive(teams);
            rosterTeam.GetComponentInChildren<Text>().text=RosterTeamName(RosterTeamAt(p));
            rosterDevice.gameObject.SetActive(!bot);rosterBotView.gameObject.SetActive(bot);RefreshToggle(rosterBotView,p<LocalSeatCount);
            rosterBotView.interactable=p<LocalSeatCount?LocalSeatCount>1:LocalSeatCount<4;
            rosterDeviceLabel.text=bot?"":"Устройство";
            rosterDevice.GetComponentInChildren<Text>().text=(bot?(p<LocalSeatCount?"Вкл · Экран "+(p+1):"Выкл"):input.Label(p));
            rosterEditorHelp.text=bot?"Экран бота занимает одну из 4 частей split-screen.":input.IsConnected(p)?"Профиль сохраняется при смене устройства.":"Подключите устройство или выберите другое.";
            rosterRemove.interactable=RosterTotal>1&&(p>=LocalSeatCount||LocalSeatCount>1||botSetup.Count>0);
            if(!rosterRemove.interactable)rosterEditorHelp.text="Нужен хотя бы один участник с экраном.";
        }
        void AddRosterHuman()
        {
            if(phase!=Phase.Setup||RosterTotal>=8||LocalSeatCount>=4)return;
            int seat=LocalSeatCount;botSetup.SetAi(seat,false);identities.ClearSeat(seat);SetSeatCount(seat+1);OpenRosterEditor(seat);
        }
        void BeginRosterPicker(string title,Button source)
        {
            rosterPickerReturn=source;rosterPickerHeading.text=title;rosterChoices.Clear();
            foreach(Transform child in rosterPickerContent){child.gameObject.SetActive(false);child.name="retired-"+child.name;Destroy(child.gameObject);}
            rosterPicker.SetActive(true);
            var sourceRect=(RectTransform)source.transform;var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds((RectTransform)rosterPicker.transform.parent,sourceRect);
            var popup=(RectTransform)rosterPicker.transform;popup.anchorMin=popup.anchorMax=new Vector2(.5f,.5f);popup.pivot=new Vector2(.5f,1);popup.anchoredPosition=new Vector2(bounds.center.x,bounds.min.y);popup.sizeDelta=new Vector2(bounds.size.x,230);
            rosterPickerHeading.gameObject.SetActive(false);rosterPickerBack.gameObject.SetActive(false);Layout((RectTransform)rosterChoiceScroll.transform,new Vector2(0,0),new Vector2(1,1));
            rosterChoiceScroll.verticalNormalizedPosition=1;
        }
        void AddRosterChoice(string name,string title,UnityEngine.Events.UnityAction choose,bool enabled=true)
        {
            int row=rosterChoices.Count;var b=RosterButton(rosterPickerContent,name,title,Vector2.zero,Vector2.one,()=>{choose();CloseRosterPicker();});
            var r=(RectTransform)b.transform;r.anchorMin=new Vector2(0,1);r.anchorMax=Vector2.one;r.pivot=new Vector2(.5f,1);r.offsetMin=new Vector2(6,-(row+1)*70+7);r.offsetMax=new Vector2(-6,-row*70-7);
            b.interactable=enabled;rosterChoices.Add(b);b.GetComponent<RosterFocus>().UseScroll=()=>rosterChoiceScroll;
        }
        void FinishRosterPicker()
        {
            ((RectTransform)rosterPickerContent).sizeDelta=new Vector2(0,Math.Max(1,rosterChoices.Count)*70);
            ConfigureRosterNavigation();Select(rosterChoices.FirstOrDefault(b=>b.interactable)??rosterPickerBack);
        }
        void CloseRosterPicker()
        {
            rosterPicker.SetActive(false);RefreshInterface();Select(rosterPickerReturn??rosterIdentity);
        }
        void OpenRosterIdentityChoices()
        {
            if(rosterEditing<0)return;int p=rosterEditing;bool bot=RosterIsBot(p);
            BeginRosterPicker(bot?"СЛОЖНОСТЬ":"ПРОФИЛЬ / ГОСТЬ",rosterIdentity);
            if(bot)for(int d=0;d<3;d++)
            {
                int difficulty=d;AddRosterChoice("roster-choice-difficulty-"+d,RosterDifficultyNames[d],()=>{if(p<LocalSeatCount)SetSeatDifficulty(p,difficulty);else SetBotDifficulty(p-LocalSeatCount,difficulty);});
            }
            else
            {
                foreach(var profile in playerProfiles.Profiles)
                {
                    var id=profile.Id;bool busy=identities.InUse(id,p);
                    AddRosterChoice("roster-choice-profile-"+id,profile.Name+(busy?" · занят":""),()=>ChooseRosterProfile(p,id),!busy);
                }
                AddRosterChoice("roster-choice-guest","Гость",()=>{identities.ChooseGuest(p,input.DeviceAt(p)?.deviceId??(-100-p),MouseSensitivityPreference.Resolve(Profile),fps.Visible);setupError=null;});
                AddRosterChoice("roster-choice-create-profile","+ Создать профиль",()=>{var profile=CreateProfileWithGeneralSettings("Игрок "+(playerProfiles.Profiles.Count+1));ChooseRosterProfile(p,profile.Id);});
            }
            FinishRosterPicker();
        }
        void ChooseRosterProfile(int seat,string id){identities.ChooseProfile(seat,input.DeviceAt(seat)?.deviceId??(-100-seat),id,playerProfiles);setupError=null;}
        void OpenRosterTeamChoices()
        {
            if(rosterEditing<0)return;int p=rosterEditing;BeginRosterPicker("КОМАНДА",rosterTeam);
            foreach(var team in new[]{NativeTeam.TeamA,NativeTeam.TeamB})
            { var choice=team;AddRosterChoice("roster-choice-team-"+(int)team,RosterTeamName(team),()=>{if(p<LocalSeatCount)SetTeam(p,choice);else SetBotTeam(p-LocalSeatCount,choice);}); }
            FinishRosterPicker();
        }
        void OpenRosterDeviceChoices()
        {
            if(rosterEditing<0)return;int p=rosterEditing;bool bot=RosterIsBot(p);BeginRosterPicker(bot?"СОБСТВЕННЫЙ ЭКРАН":"УСТРОЙСТВО",rosterDevice);
            if(bot)
            {
                AddRosterChoice("roster-choice-view-off","Выкл"+(p<LocalSeatCount&&LocalSeatCount==1?" · нужен хотя бы один экран":""),()=>SetRosterBotView(false),p>=LocalSeatCount||LocalSeatCount>1);
                AddRosterChoice("roster-choice-view-on","Вкл"+(p>=LocalSeatCount&&LocalSeatCount==4?" · все 4 экрана заняты":""),()=>SetRosterBotView(true),p<LocalSeatCount||LocalSeatCount<4);
            }
            else
            {
                foreach(var device in GamepadCompatibility.ListedDevices())
                {
                    var choice=device;bool busy=Enumerable.Range(0,LocalSeatCount).Any(s=>s!=p&&input.DeviceAt(s)==device);
                    bool usable=GamepadCompatibility.CanAssign(device)&&!busy;
                    string label=GamepadCompatibility.Name(device)+" #"+device.deviceId;
                    string status=busy?"Занято":device is Joystick && !(device is Gamepad)?"Нужен XInput или профиль":GamepadCompatibility.Status(device);
                    AddRosterChoice("roster-choice-device-"+device.deviceId,label+"\n"+status,()=>{if(input.Replace(p,choice)){identities.RememberBinding(p,choice.deviceId);setupError=null;}},usable);
                    var caption=rosterChoices[rosterChoices.Count-1].GetComponentInChildren<Text>();caption.fontSize=19;caption.horizontalOverflow=HorizontalWrapMode.Wrap;
                }
                if(rosterChoices.Count==0)AddRosterChoice("roster-choice-no-device","Подключите клавиатуру или геймпад",()=>{},false);
            }
            FinishRosterPicker();
        }
        void CompactRosterSeat(int seat)
        {
            int count=LocalSeatCount;identities.RemoveSeat(seat,count);botSetup.RemoveSeat(seat,count);
            for(int i=seat;i<count-1;i++)teamAssignments[i]=teamAssignments[i+1];
            input.RemoveSeat(seat);ApplyLayout(LocalSeatCount);
        }
        void PromoteRosterBot(int index)
        {
            var bot=botSetup.At(index);int seat=LocalSeatCount;botSetup.Remove(index);
            input.SetActiveSeatCount(seat+1);botSetup.SetAi(seat,true);botSetup.SetSeatDifficulty(seat,bot.Difficulty);teamAssignments[seat]=bot.Team;identities.ClearSeat(seat);SyncHumanSeats();ApplyLayout(LocalSeatCount);rosterEditing=seat;
        }
        void SetRosterBotView(bool ownView)
        {
            int p=rosterEditing;if(p<0||!RosterIsBot(p))return;
            if(ownView && p>=LocalSeatCount && LocalSeatCount<4)PromoteRosterBot(p-LocalSeatCount);
            else if(!ownView && p<LocalSeatCount && LocalSeatCount>1)
            {
                int difficulty=botSetup.SeatDifficulty(p);var team=teamAssignments[p];CompactRosterSeat(p);botSetup.Add(LocalSeatCount);
                int index=botSetup.Count-1;botSetup.SetDifficulty(index,difficulty);botSetup.SetTeam(index,team);rosterEditing=LocalSeatCount+index;
            }
            setupError=null;RefreshInterface();
        }
        void RemoveRosterParticipant()
        {
            int p=rosterEditing;if(p<0||!rosterRemove.interactable)return;
            rosterEditing=-1;rosterPicker.SetActive(false);
            if(p>=LocalSeatCount)botSetup.Remove(p-LocalSeatCount);
            else
            {
                // Keep one view by first moving an existing additional bot into an unused view.
                if(LocalSeatCount==1)PromoteRosterBot(0);
                CompactRosterSeat(p);rosterEditing=-1;
            }
            setupError=null;RefreshInterface();FocusRosterCard(p);
        }
        void ConfigureRosterNavigation()
        {
            if(phase!=Phase.Setup)return;
            if(rosterEditing>=0)
            {
                if(rosterPicker.activeSelf)RosterVerticalNavigation(rosterChoices.Where(b=>b.interactable).Cast<Selectable>().ToArray());
                else RosterVerticalNavigation(new Selectable[]{rosterIdentity,rosterTeam,rosterDevice,rosterBotView,rosterRemove,rosterDone}.Where(b=>b.gameObject.activeSelf&&b.interactable).ToArray());
                return;
            }
            if(setupStep!=2)return;
            var cards=Enumerable.Range(0,RosterTotal).Select(p=>rosterCards[p]).ToArray();
            var top=new[]{rosterAddHuman,rosterAddBot}.Where(b=>b.interactable).ToArray();
            if(!setupGamepadNavigation)
            {
                var steps=setupSteps.Where(b=>b.interactable).ToArray();
                for(int i=0;i<steps.Length;i++)steps[i].navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnLeft=steps[Math.Max(0,i-1)],selectOnRight=steps[Math.Min(steps.Length-1,i+1)],selectOnDown=top.FirstOrDefault()??cards[0]};
            }
            for(int i=0;i<top.Length;i++)
            {var n=new Navigation{mode=Navigation.Mode.Explicit,selectOnLeft=top[System.Math.Max(0,i-1)],selectOnRight=top[System.Math.Min(top.Length-1,i+1)],selectOnUp=setupGamepadNavigation?top[i]:setupSteps[2],selectOnDown=cards[0]};top[i].navigation=n;}
            // Authored grid coordinates keep offscreen cards reachable; select scrolls their rect into view.
            int[] a=Enumerable.Range(0,RosterTotal).Where(p=>SetupMode!=NativeMatchMode.Teams||RosterTeamAt(p)==NativeTeam.TeamA).ToArray();
            int[] bteam=Enumerable.Range(0,RosterTotal).Where(p=>SetupMode==NativeMatchMode.Teams&&RosterTeamAt(p)==NativeTeam.TeamB).ToArray();
            foreach(var group in new[]{a,bteam})for(int i=0;i<group.Length;i++)
            {
                int cols=SetupMode==NativeMatchMode.Teams?2:4;
                var n=new Navigation{mode=Navigation.Mode.Explicit,selectOnUp=i>=cols?rosterCards[group[i-cols]]:top.FirstOrDefault()??(setupGamepadNavigation?rosterCards[group[i]]:setupSteps[2]),selectOnDown=i+cols<group.Length?rosterCards[group[i+cols]]:setupGamepadNavigation?rosterCards[group[i]]:start.interactable?start:setupPrevious,
                    selectOnLeft=i%cols>0?rosterCards[group[i-1]]:rosterCards[group[i]],selectOnRight=i%cols<cols-1&&i+1<group.Length?rosterCards[group[i+1]]:rosterCards[group[i]]};
                if(SetupMode==NativeMatchMode.Teams)
                {
                    var other=group==a?bteam:a;int row=i/2;
                    if(group==a&&i%2==1&&other.Length>0)n.selectOnRight=rosterCards[other[Math.Min(row*2,other.Length-1)]];
                    if(group==bteam&&i%2==0&&other.Length>0)n.selectOnLeft=rosterCards[other[Math.Min(row*2+1,other.Length-1)]];
                }
                rosterCards[group[i]].navigation=n;
            }
            if(setupGamepadNavigation){NoSetupNavigation(setupPrevious);NoSetupNavigation(start);}
            else
            {
                setupPrevious.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnUp=cards.Last(),selectOnRight=start.interactable?start:setupPrevious};
                start.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnUp=cards.Last(),selectOnLeft=setupPrevious};
            }
        }
        void RosterVerticalNavigation(Selectable[] buttons)
        {
            for(int i=0;i<buttons.Length;i++)buttons[i].navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnUp=buttons[Math.Max(0,i-1)],selectOnDown=buttons[Math.Min(buttons.Length-1,i+1)]};
        }
        void UpdateRosterInput()
        {
            if(phase!=Phase.Setup)return;
            bool back=Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame;
            foreach(var pad in Gamepad.all)back|=pad.buttonEast.wasPressedThisFrame;
            if(back)
            {
                setupTransitionFrame=Time.frameCount;
                if(rosterEditing>=0){if(rosterPicker.activeSelf)CloseRosterPicker();else CloseRosterEditor();}
                else if(pendingSeat>=0){pendingSeat=-1;RefreshInterface();FocusRosterCard(0);}
                else PreviousSetupStep();
            }
            if(phase!=Phase.Setup)return;
            string blocking=SetupBlockingReason();
            if(blocking!=rosterLastBlocking){rosterLastBlocking=blocking;RefreshInterface();}
        }
    }

    // Frame stays visible for both pointer hover and EventSystem focus. Selection also reveals clipped cards.
    public sealed class RosterFocus : MonoBehaviour,ISelectHandler,IDeselectHandler,IPointerEnterHandler,IPointerExitHandler
    {
        public Func<ScrollRect> UseScroll;
        public void Initialize(Color color) { /* Focus is rendered by the shared MenuPresentation. */ }
        public void OnSelect(BaseEventData e){Reveal();}
        public void OnDeselect(BaseEventData e){}
        public void OnPointerEnter(PointerEventData e){}
        public void OnPointerExit(PointerEventData e){}
        void Reveal()
        {
            var scroll=UseScroll?.Invoke();if(scroll==null||!scroll.gameObject.activeInHierarchy)return;
            Canvas.ForceUpdateCanvases();var corners=new Vector3[4];((RectTransform)transform).GetWorldCorners(corners);
            float bottom=scroll.viewport.InverseTransformPoint(corners[0]).y,top=scroll.viewport.InverseTransformPoint(corners[1]).y;
            var viewport=scroll.viewport.rect;float shift=bottom<viewport.yMin?viewport.yMin-bottom:top>viewport.yMax?viewport.yMax-top:0;
            if(shift!=0){var position=scroll.content.anchoredPosition;position.y+=shift;scroll.content.anchoredPosition=position;scroll.StopMovement();}
        }
    }
}
