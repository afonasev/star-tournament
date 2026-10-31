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
        readonly Text[] rosterNames=new Text[NativeMatchRoster.MaximumParticipants],rosterDetails=new Text[NativeMatchRoster.MaximumParticipants];
        readonly GameObject[] rosterDeviceIcons=new GameObject[NativeMatchRoster.MaximumParticipants],rosterScreenIcons=new GameObject[NativeMatchRoster.MaximumParticipants];
        readonly Button[] rosterEmpty=new Button[8];
        Button[][] lobbyRosterGroups;
        int rosterEmptyUsed;
        readonly Image[] rosterPortraits=new Image[NativeMatchRoster.MaximumParticipants];
        GameObject rosterEditor,rosterPicker;
        Toggle rosterBotView;
        Transform rosterFfa,rosterBlue,rosterRed,rosterPickerContent;
        ScrollRect rosterFfaScroll,rosterBlueScroll,rosterRedScroll,rosterChoiceScroll;
        GameObject rosterBluePanel,rosterRedPanel;
        Text rosterPageTitle,rosterFull,rosterEditorName,rosterEditorSubtitle,rosterEditorHelp,rosterPickerHeading;
        Button rosterAddHuman,rosterAddBot,rosterIdentity,rosterDevice,rosterTeam,rosterRemove,rosterDone,rosterPickerBack;
        Text rosterIdentityLabel,rosterDeviceLabel,rosterTeamLabel;
        int rosterEditing=-1;
        Button rosterPickerReturn;
        readonly List<Button> rosterChoices=new List<Button>();
        string rosterLastBlocking;
        float rosterChoiceRowHeight=70;
        static readonly string[] RosterDifficultyNames={"Рекрут","Боец","Ветеран"};
        static readonly Color32 RosterMuted=new Color32(153,173,187,255);
        static readonly Color32 RosterBlue=new Color32(106,204,239,255),RosterRed=new Color32(242,128,147,255);

        readonly Dictionary<string,int> lobbyColors=new Dictionary<string,int>();
        readonly System.Random lobbyColorRandom=new System.Random();
        readonly Button[] rosterColorSwatches=new Button[8];
        GameObject rosterColorRow;Text rosterColorLabel;
        string LobbyColorIdentity(int p)=>RosterIsBot(p)?"bot:"+(p<LocalSeatCount?botSetup.SeatBot(p,teamAssignments[p]).Id:botSetup.At(p-LocalSeatCount).Id):"human:"+(input.DeviceAt(p)?.deviceId.ToString()??identities.GuestAt(p)?.Id??("seat:"+p));
        void EnsureLobbyColors()
        {
            var keys=Enumerable.Range(0,RosterTotal).Select(LobbyColorIdentity).ToArray();
            foreach(var key in lobbyColors.Keys.Except(keys).ToArray())lobbyColors.Remove(key);
            foreach(var key in keys)if(!lobbyColors.ContainsKey(key))
            {
                var free=Enumerable.Range(0,8).Except(lobbyColors.Values).ToArray();lobbyColors[key]=free[lobbyColorRandom.Next(free.Length)];
            }
        }
        Color LobbyColor(int p)
        {
            if(SetupMode==NativeMatchMode.Teams)return RosterTeamColor(RosterTeamAt(p));
            EnsureLobbyColors();return NativeParticipantColors.Read(ParticipantPaletteProfile)[lobbyColors[LobbyColorIdentity(p)]];
        }
        void ChooseLobbyColor(int index)
        {
            if(rosterEditing<0||SetupMode!=NativeMatchMode.Ffa||!rosterColorSwatches[index].interactable)return;
            lobbyColors[LobbyColorIdentity(rosterEditing)]=index;RefreshInterface();
        }
        NativeMatchComposition FreezeLobbyColors(NativeMatchComposition draft)
        {
            if(draft.Roster.Mode==NativeMatchMode.Training)
            {var training=draft.Read();EnsureLobbyColors();for(int i=0;i<draft.LocalCount;i++)training.Participants[i].Color=NativeParticipantColors.Read(ParticipantPaletteProfile)[lobbyColors[LobbyColorIdentity(i)]];return NativeMatchComposition.Restore(training);}
            if(draft.Roster.Mode!=NativeMatchMode.Ffa)return NativeParticipantColors.FreezeNewMatch(draft,profile:ParticipantPaletteProfile);
            EnsureLobbyColors();var snapshot=draft.Read();
            for(int p=0;p<snapshot.Participants.Length;p++)snapshot.Participants[p].Color=LobbyColor(p);
            return NativeMatchComposition.Restore(snapshot);
        }
        static Sprite rosterRounded;
        static Sprite rosterHumanPortrait,rosterBotPortrait;
        static Sprite Portrait(string resource,ref Sprite cached)
        {
            if(cached)return cached;
            var texture=Resources.Load<Texture2D>("LobbyPortraits/"+resource);
            if(texture)
            {
                // Bilinear samples at the transparent top must not wrap to the opaque bottom edge.
                texture.wrapMode=TextureWrapMode.Clamp;
                cached=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
            }
            return cached;
        }
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
            b.GetComponent<MenuPresentation>().LobbySurface=true;b.transition=Selectable.Transition.None;
            b.GetComponent<MenuActionHint>().ColoredLobbyGlyph=true;
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

            RoundRosterPanel(setupPlayersPage);RoundRosterPanel(setupSummary.transform.parent.gameObject);
            rosterAddHuman=RosterButton(setupPlayersPage.transform,"roster-add-human","+ ИГРОК",new Vector2(.70f,.865f),new Vector2(.835f,.97f),AddRosterHuman);
            rosterAddBot=RosterButton(setupPlayersPage.transform,"roster-add-bot","+ БОТ",new Vector2(.845f,.865f),new Vector2(.98f,.97f),()=>{int p=LocalSeatCount+botSetup.Count;AddBot();if(LocalSeatCount+botSetup.Count>p){OpenRosterEditor(p);}});
            rosterFull=Label(setupPlayersPage.transform,"roster-full","",20,new Vector2(.68f,.795f),new Vector2(.98f,.862f),TextAnchor.MiddleCenter,RosterMuted);
            rosterFfa=RosterScroll(setupPlayersPage.transform,"roster-ffa",new Vector2(.017f,.025f),new Vector2(.98f,.77f),out rosterFfaScroll);
            rosterBluePanel=Panel(setupPlayersPage.transform,"roster-team-blue",new Vector2(.017f,.025f),new Vector2(.49f,.77f),new Color32(23,37,52,255));
            rosterRedPanel=Panel(setupPlayersPage.transform,"roster-team-red",new Vector2(.51f,.025f),new Vector2(.98f,.77f),new Color32(23,37,52,255));
            rosterBlue=RosterScroll(rosterBluePanel.transform,"team-blue-scroll",new Vector2(.02f,.02f),new Vector2(.98f,.98f),out rosterBlueScroll);
            rosterRed=RosterScroll(rosterRedPanel.transform,"team-red-scroll",new Vector2(.02f,.02f),new Vector2(.98f,.98f),out rosterRedScroll);
            rosterBluePanel.GetComponent<Image>().color=new Color32(22,53,72,190);
            rosterRedPanel.GetComponent<Image>().color=new Color32(68,34,43,190);
            rosterBlueScroll.GetComponent<Image>().color=Color.clear;
            rosterRedScroll.GetComponent<Image>().color=Color.clear;
            for(int i=0;i<rosterCards.Length;i++)
            {
                int p=i;var b=RosterButton(rosterFfa,"roster-card-"+i,"",Vector2.zero,Vector2.one,()=>OpenRosterEditor(p));rosterCards[i]=b;b.GetComponent<MenuPresentation>().Compact=false;
                b.GetComponentInChildren<Text>().gameObject.SetActive(false);
                var hint=b.GetComponent<MenuActionHint>();hint.ReservedRightInset=()=>24;
                hint.GlyphLabel.rectTransform.anchorMin=new Vector2(1,.18f);hint.GlyphLabel.rectTransform.anchorMax=new Vector2(1,.40f);
                b.GetComponent<Image>().color=new Color32(4,9,15,185);
                var portrait=Panel(b.transform,"portrait",new Vector2(.025f,.04f),new Vector2(.49f,.96f),Color.white);
                rosterPortraits[i]=portrait.GetComponent<Image>();rosterPortraits[i].preserveAspect=true;rosterPortraits[i].raycastTarget=false;
                rosterNames[i]=Label(b.transform,"participant-name","",28,new Vector2(.52f,.69f),new Vector2(.95f,.94f),TextAnchor.MiddleLeft,Color.white);
                hint.CaptionOverride=rosterNames[i];
                hint.CardHint=true;
                rosterNames[i].fontStyle=FontStyle.Bold;
                rosterNames[i].resizeTextForBestFit=true;rosterNames[i].resizeTextMinSize=20;rosterNames[i].resizeTextMaxSize=30;
                rosterDetails[i]=Label(b.transform,"participant-detail","",20,new Vector2(.52f,.47f),new Vector2(.95f,.65f),TextAnchor.MiddleLeft,RosterMuted);
                rosterDetails[i].resizeTextForBestFit=true;rosterDetails[i].resizeTextMinSize=18;rosterDetails[i].resizeTextMaxSize=22;
                var device=new GameObject("participant-device-glyph",typeof(RectTransform),typeof(CanvasRenderer),typeof(LobbyRosterGlyph));device.transform.SetParent(b.transform,false);
                Layout((RectTransform)device.transform,new Vector2(.52f,.44f),new Vector2(.76f,.66f));rosterDeviceIcons[i]=device;
                var screen=new GameObject("participant-screen-slot",typeof(RectTransform));screen.transform.SetParent(b.transform,false);
                Layout((RectTransform)screen.transform,new Vector2(.79f,.44f),new Vector2(.93f,.66f));
                for(int slot=0;slot<4;slot++)
                {
                    var square=new GameObject("slot-"+slot,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));square.transform.SetParent(screen.transform,false);
                    int x=slot%2,y=slot/2;var squareRect=(RectTransform)square.transform;squareRect.anchorMin=squareRect.anchorMax=new Vector2(.5f,.5f);squareRect.sizeDelta=new Vector2(15,15);squareRect.anchoredPosition=new Vector2(x==0?-10:10,y==0?10:-10);
                    var squareImage=square.GetComponent<Image>();squareImage.sprite=LobbyVisualAssets.ScreenCell(false);squareImage.color=new Color32(93,220,225,255);squareImage.raycastTarget=false;
                }
                rosterScreenIcons[i]=screen.gameObject;
                foreach(var graphic in b.GetComponentsInChildren<Graphic>())if(graphic.gameObject!=b.gameObject)graphic.raycastTarget=false;
                b.GetComponent<RosterFocus>().UseScroll=()=>SetupMode==NativeMatchMode.Teams? (RosterTeamAt(p)==NativeTeam.TeamA?rosterBlueScroll:rosterRedScroll):rosterFfaScroll;
            }
            for(int i=0;i<rosterEmpty.Length;i++)
            {
                var empty=RosterButton(rosterFfa,"roster-empty-"+i,"+",Vector2.zero,Vector2.one,BeginLobbyBot);
                rosterEmpty[i]=empty;empty.GetComponent<MenuPresentation>().Compact=false;
                empty.GetComponentInChildren<Text>().fontSize=52;
                var ring=Panel(empty.transform,"empty-slot-ring",new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Color32(112,151,171,155)).GetComponent<Image>();
                ring.sprite=LobbyVisualAssets.EmptySlot;ring.raycastTarget=false;ring.rectTransform.sizeDelta=new Vector2(96,96);ring.transform.SetAsFirstSibling();
                empty.GetComponent<RosterFocus>().UseScroll=()=>empty.GetComponentInParent<ScrollRect>();
                empty.gameObject.SetActive(false);
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
                field.GetComponent<MenuActionHint>().ReservedRightInset=()=>-value.rectTransform.offsetMax.x;
                Label(field.transform,"dropdown-arrow","▾",26,new Vector2(.90f,0),new Vector2(.98f,1),TextAnchor.MiddleCenter,RosterMuted);
            }
            rosterColorLabel=Label(card.transform,"editor-color-label","Цвет",23,new Vector2(.055f,.375f),new Vector2(.945f,.43f),TextAnchor.MiddleLeft,RosterMuted);
            rosterColorRow=Panel(card.transform,"editor-colors",new Vector2(.055f,.29f),new Vector2(.945f,.37f),Color.clear);
            for(int n=0;n<8;n++)
            {
                int index=n;var swatch=RosterButton(rosterColorRow.transform,"roster-color-"+n,"",new Vector2(n/8f,0),new Vector2((n+1)/8f,1),()=>ChooseLobbyColor(index));
                var rect=(RectTransform)swatch.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(46,46);rect.anchoredPosition=new Vector2((n-3.5f)*60,0);
                swatch.GetComponent<MenuPresentation>().LobbySurface=false;swatch.transition=Selectable.Transition.ColorTint;
                swatch.GetComponent<MenuActionHint>().enabled=false;rosterColorSwatches[n]=swatch;
            }
            rosterEditorHelp=Label(card.transform,"editor-help","",21,new Vector2(.055f,.19f),new Vector2(.945f,.285f),TextAnchor.MiddleLeft,RosterMuted);
            rosterRemove=RosterButton(card.transform,"roster-remove","УДАЛИТЬ ИЗ МАТЧА",new Vector2(.055f,.16f),new Vector2(.945f,.23f),RemoveRosterParticipant);
            rosterDone=RosterButton(card.transform,"roster-done","ГОТОВО",new Vector2(.055f,.06f),new Vector2(.945f,.14f),CloseRosterEditor);
            BindMenuShortcut(rosterDone,MenuPadButton.East,()=>phase==Phase.Setup&&rosterEditing>=0&&!rosterPicker.activeSelf&&!(lobbyDialog&&lobbyDialog.activeSelf));
            rosterDone.GetComponent<MenuPresentation>().LobbySurface=false;
            rosterDone.GetComponent<Image>().color=MenuGold;rosterDone.GetComponentInChildren<Text>().color=MenuInk;
            rosterPicker=Panel(rosterEditor.transform,"roster-picker",Vector2.zero,Vector2.one,SelectorBackdrop);
            var pickerCard=Panel(rosterPicker.transform,"roster-picker-card",new Vector2(.30f,.18f),new Vector2(.70f,.82f),MenuCard);StyleSelectorCard(pickerCard);
            rosterPickerHeading=Label(pickerCard.transform,"picker-title","",28,new Vector2(.055f,.85f),new Vector2(.945f,.98f),TextAnchor.MiddleCenter,MenuGold);
            rosterPickerContent=RosterScroll(pickerCard.transform,"roster-picker-scroll",new Vector2(.055f,.19f),new Vector2(.945f,.82f),out rosterChoiceScroll);
            rosterPickerBack=RosterButton(pickerCard.transform,"roster-picker-back","‹ НАЗАД",new Vector2(.055f,.04f),new Vector2(.945f,.14f),CloseRosterPicker);
            rosterPickerBack.GetComponentInChildren<Text>().fontSize=22;rosterPickerBack.GetComponentInChildren<Text>().fontStyle=FontStyle.Normal;
            BindMenuShortcut(rosterPickerBack,MenuPadButton.East,()=>phase==Phase.Setup&&rosterEditing>=0&&rosterPicker.activeSelf);
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
        Color RosterTeamColor(NativeTeam team)=>NativeParticipantColors.TeamColor(team,swappedTeamColors,ParticipantPaletteProfile);
        Button[] LayoutRosterGrid(Transform content,int[] participants,int slots,int columns,float height)
        {
            var cards=participants.Select(i=>rosterCards[i]).ToList();
            while(cards.Count<slots){var empty=rosterEmpty[rosterEmptyUsed++];empty.gameObject.SetActive(true);cards.Add(empty);}
            var rect=(RectTransform)content;int rows=Math.Max(1,Mathf.CeilToInt(cards.Count/(float)columns));
            float available=((RectTransform)content.parent).rect.height;
            float rowHeight=Mathf.Min(height,available/2f),gridHeight=rows*rowHeight;
            float inset=Mathf.Max(0,(available-gridHeight)*.5f);
            rect.sizeDelta=new Vector2(0,Mathf.Max(available,gridHeight));
            for(int i=0;i<cards.Count;i++)
            {
                var card=cards[i];card.transform.SetParent(content,false);
                float rowInset=(columns-Math.Min(columns,cards.Count-(i/columns)*columns))*.5f;
                var r=(RectTransform)card.transform;r.anchorMin=new Vector2((rowInset+i%columns)/columns,1);r.anchorMax=new Vector2((rowInset+i%columns+1)/columns,1);r.pivot=new Vector2(.5f,1);
                r.offsetMin=new Vector2(8,-inset-(i/columns+1)*rowHeight+8);r.offsetMax=new Vector2(-8,-inset-(i/columns)*rowHeight-8);
            }
            return cards.ToArray();
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
                bool disconnected=!bot&&input.DeviceAt(i)!=null&&!input.IsConnected(i);
                rosterDetails[i].text=bot?RosterDifficultyNames[RosterDifficultyAt(i)]:disconnected?"Устройство отключено":"";
                rosterDetails[i].gameObject.SetActive(bot||disconnected);rosterDetails[i].color=disconnected?new Color32(255,171,142,255):RosterMuted;
                rosterPortraits[i].sprite=bot?Portrait("Bot",ref rosterBotPortrait):Portrait("Human",ref rosterHumanPortrait);
                rosterDeviceIcons[i].SetActive(!bot);rosterScreenIcons[i].SetActive(i<LocalSeatCount);
                rosterDeviceIcons[i].GetComponent<LobbyRosterGlyph>().color=disconnected?new Color(.5f,.5f,.5f,1):Color.white;
                Layout(rosterDetails[i].rectTransform,new Vector2(.52f,disconnected?.10f:.47f),new Vector2(.95f,disconnected?.42f:.65f));
                if(i<LocalSeatCount)
                {
                    int slot=LobbyPosition(i,LocalSeatCount);var squares=rosterScreenIcons[i].GetComponentsInChildren<Image>(true);
                    for(int n=0;n<squares.Length;n++)squares[n].sprite=LobbyVisualAssets.ScreenCell(n==slot);
                }
                if(!bot)rosterDeviceIcons[i].GetComponent<LobbyRosterGlyph>().Kind=input.DeviceAt(i) is Gamepad?LobbyRosterGlyph.DeviceKind.Gamepad:LobbyRosterGlyph.DeviceKind.KeyboardMouse;
                rosterCards[i].GetComponent<MenuPresentation>().SurfaceTint=LobbyColor(i);
            }
            foreach(var empty in rosterEmpty)empty.gameObject.SetActive(false);rosterEmptyUsed=0;
            Canvas.ForceUpdateCanvases();
            var all=Enumerable.Range(0,RosterTotal).ToArray();
            if(teams)
            {
                var blue=all.Where(i=>RosterTeamAt(i)==NativeTeam.TeamA).ToArray();var red=all.Where(i=>RosterTeamAt(i)==NativeTeam.TeamB).ToArray();
                int blueSlots=Mathf.Clamp(4,blue.Length,8-red.Length);
                lobbyRosterGroups=new[]{LayoutRosterGrid(rosterBlue,blue,blueSlots,2,250),LayoutRosterGrid(rosterRed,red,8-blueSlots,2,250)};
            }
            else lobbyRosterGroups=new[]{LayoutRosterGrid(rosterFfa,all,8,4,250)};
            // Reparenting between FFA/team grids can disable a card without a new
            // EventSystem selection event. Restore the shared visual focus explicitly.
            foreach(var card in rosterCards.Concat(rosterEmpty))card.GetComponent<MenuPresentation>().SetFocused(EventSystem.current&&EventSystem.current.currentSelectedGameObject==card.gameObject);
            rosterAddHuman.interactable=RosterTotal<8 && LocalSeatCount<4;rosterAddBot.interactable=RosterTotal<8;
            rosterAddHuman.GetComponentInChildren<Text>().color=rosterAddHuman.interactable?Color.white:(Color)new Color32(127,153,165,255);
            rosterAddBot.GetComponentInChildren<Text>().color=rosterAddBot.interactable?Color.white:(Color)new Color32(127,153,165,255);
            rosterFull.text=RosterTotal==8?"Состав заполнен":LocalSeatCount==4?"Все 4 экрана назначены":"";
            rosterEditor.SetActive(phase==Phase.Setup&&setupStep==2&&rosterEditing>=0);
            if(rosterEditing>=0 && rosterEditing<RosterTotal)RefreshRosterEditor();
            RefreshInlineCards();
            ConfigureRosterNavigation();
        }
        void OpenRosterEditor(int p)
        {
            if(phase!=Phase.Setup||setupStep!=2||p<0||p>=RosterTotal)return;
            OpenInlineEditor(p);
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
            rosterEditorSubtitle.text=bot?"":input.Label(p);
            rosterIdentityLabel.text=bot?"Сложность":"Профиль";
            rosterIdentity.GetComponentInChildren<Text>().text=bot?RosterDifficultyNames[RosterDifficultyAt(p)]:identities.ProfileId(p)==null?"Без профиля":IdentityLabel(p);
            rosterColorRow.SetActive(!teams);rosterColorLabel.gameObject.SetActive(!teams);
            if(!teams)
            {
                var palette=NativeParticipantColors.Read(ParticipantPaletteProfile);EnsureLobbyColors();
                var selected=LobbyColor(p);
                for(int n=0;n<8;n++)
                {
                    var swatch=rosterColorSwatches[n];swatch.GetComponent<Image>().color=palette[n];
                    swatch.interactable=!Enumerable.Range(0,RosterTotal).Any(i=>i!=p&&LobbyColor(i)==palette[n]);
                    swatch.GetComponentInChildren<Text>().text=selected==palette[n]?"✓":"";
                }
            }
            rosterTeam.gameObject.SetActive(teams);rosterTeamLabel.gameObject.SetActive(teams);
            rosterTeam.GetComponentInChildren<Text>().text=RosterTeamName(RosterTeamAt(p));
            rosterDevice.gameObject.SetActive(true);rosterBotView.gameObject.SetActive(false);
            Layout(rosterDeviceLabel.rectTransform,new Vector2(.055f,teams?.375f:.515f),new Vector2(.945f,teams?.43f:.57f));
            Layout((RectTransform)rosterDevice.transform,new Vector2(.055f,teams?.29f:.43f),new Vector2(.945f,teams?.37f:.51f));
            rosterDeviceLabel.text="Экран";rosterDevice.GetComponentInChildren<Text>().text=p<LocalSeatCount?"Экран "+(LobbyPosition(p,LocalSeatCount)+1):"Без экрана";
            rosterEditorHelp.text=!bot&&!input.IsConnected(p)?"Устройство отключено. Верните его или удалите участника.":"";
            rosterRemove.interactable=RosterTotal>1&&(p>=LocalSeatCount||LocalSeatCount>1||botSetup.Count>0);
            if(!rosterRemove.interactable)rosterEditorHelp.text="Нужен хотя бы один участник с экраном.";
        }
        void AddRosterHuman() { BeginLobbyDevices(); }
        void BeginRosterPicker(string title,Button source,bool compact=false)
        {
            rosterChoiceRowHeight=compact?42:70;
            rosterPickerReturn=source;rosterPickerHeading.text=title;rosterChoices.Clear();
            foreach(Transform child in rosterPickerContent){child.gameObject.SetActive(false);child.name="retired-"+child.name;Destroy(child.gameObject);}
            rosterPicker.transform.SetAsLastSibling();rosterPicker.SetActive(true);
            rosterChoiceScroll.verticalNormalizedPosition=1;
        }
        void AddRosterChoice(string name,string title,UnityEngine.Events.UnityAction choose,bool enabled=true)
        {
            int row=rosterChoices.Count;var b=RosterButton(rosterPickerContent,name,title,Vector2.zero,Vector2.one,()=>{choose();CloseRosterPicker();});
            var r=(RectTransform)b.transform;r.anchorMin=new Vector2(0,1);r.anchorMax=Vector2.one;r.pivot=new Vector2(.5f,1);float gap=rosterChoiceRowHeight<70?3:7;r.offsetMin=new Vector2(6,-(row+1)*rosterChoiceRowHeight+gap);r.offsetMax=new Vector2(-6,-row*rosterChoiceRowHeight-gap);
            if(rosterChoiceRowHeight<70){var caption=b.GetComponentInChildren<Text>();caption.fontSize=22;caption.fontStyle=FontStyle.Normal;}
            b.GetComponent<MenuPresentation>().FilledFocus=true;
            b.interactable=enabled;rosterChoices.Add(b);b.GetComponent<RosterFocus>().UseScroll=()=>rosterChoiceScroll;
        }
        void FinishRosterPicker()
        {
            ((RectTransform)rosterPickerContent).sizeDelta=new Vector2(0,Math.Max(1,rosterChoices.Count)*rosterChoiceRowHeight);
            ConfigureRosterNavigation();Select(rosterChoices.FirstOrDefault(b=>b.interactable)??rosterPickerBack);
        }
        void CloseRosterPicker()
        {
            rosterPicker.SetActive(false);RefreshInterface();Select(rosterPickerReturn??rosterIdentity);
        }
        void OpenRosterIdentityChoices()
        {
            if(rosterEditing<0)return;int p=rosterEditing;
            if(!RosterIsBot(p)){ShowLobbyProfiles(id=>{if(id==null)identities.ChooseGuest(p,input.DeviceAt(p)?.deviceId??(-100-p),MouseSensitivityPreference.Resolve(Profile),fps.Visible);else ChooseRosterProfile(p,id);CloseLobbyDialog();RefreshInterface();},()=>{CloseLobbyDialog();Select(rosterIdentity);});return;}
            BeginRosterPicker("СЛОЖНОСТЬ",rosterIdentity);
            for(int d=0;d<3;d++){int difficulty=d;AddRosterChoice("roster-choice-difficulty-"+d,RosterDifficultyNames[d],()=>{if(p<LocalSeatCount)SetSeatDifficulty(p,difficulty);else SetBotDifficulty(p-LocalSeatCount,difficulty);});}
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
        void OpenRosterDeviceChoices() { OpenLobbyScreens(); }
        void CompactRosterSeat(int seat)
        {
            int count=LocalSeatCount;RemoveLobbyPosition(seat,count);identities.RemoveSeat(seat,count);botSetup.RemoveSeat(seat,count);
            for(int i=seat;i<count-1;i++)teamAssignments[i]=teamAssignments[i+1];
            input.RemoveSeat(seat);ApplyLayout(LocalSeatCount);
        }
        void PromoteRosterBot(int index)
        {
            var bot=botSetup.At(index);int seat=LocalSeatCount;botSetup.Remove(index);
            lobbyPositions[seat]=seat;input.SetActiveSeatCount(seat+1);botSetup.SetSeatBot(seat,bot);teamAssignments[seat]=bot.Team;identities.ClearSeat(seat);SyncHumanSeats();ApplyLayout(LocalSeatCount);rosterEditing=seat;
        }
        void SetRosterBotView(bool ownView)
        {
            int p=rosterEditing;if(p<0||!RosterIsBot(p))return;
            if(ownView && p>=LocalSeatCount && LocalSeatCount<4)PromoteRosterBot(p-LocalSeatCount);
            else if(!ownView && p<LocalSeatCount && LocalSeatCount>1)
            {
                var bot=botSetup.SeatBot(p,teamAssignments[p]);CompactRosterSeat(p);botSetup.AddExisting(LocalSeatCount,bot);
                int index=botSetup.Count-1;rosterEditing=LocalSeatCount+index;
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
                if(rosterPicker.activeSelf)RosterVerticalNavigation(rosterChoices.Where(b=>b.interactable).Cast<Selectable>().Concat(new Selectable[]{rosterPickerBack}).ToArray());
                else
                {
                    RosterVerticalNavigation(new Selectable[]{rosterIdentity,rosterTeam,rosterDevice}.Concat(rosterColorSwatches).Concat(new Selectable[]{rosterRemove,rosterDone}).Where(b=>b.gameObject.activeInHierarchy&&b.interactable).ToArray());
                    if(rosterColorRow.activeSelf)
                    {
                        var row=rosterColorSwatches.Where(b=>b.interactable).ToArray();
                        for(int i=0;i<row.Length;i++)row[i].navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnUp=rosterDevice,selectOnDown=rosterRemove,selectOnLeft=row[Math.Max(0,i-1)],selectOnRight=row[Math.Min(row.Length-1,i+1)]};
                        var selected=row.FirstOrDefault(b=>b.GetComponentInChildren<Text>().text=="✓")??row[0];
                        var nav=rosterDevice.navigation;nav.selectOnDown=selected;rosterDevice.navigation=nav;
                        nav=rosterRemove.navigation;nav.selectOnUp=selected;rosterRemove.navigation=nav;
                    }
                }
            }
            else ConfigureLobbyNavigation();
        }
        void RosterVerticalNavigation(Selectable[] buttons)
        {
            for(int i=0;i<buttons.Length;i++)buttons[i].navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnUp=buttons[Math.Max(0,i-1)],selectOnDown=buttons[Math.Min(buttons.Length-1,i+1)]};
        }
        void UpdateRosterInput()
        {
            if(phase!=Phase.Setup)return;
            if(lobbyDialog!=null&&lobbyDialog.activeSelf){UpdateLobbyDialogInput();return;}
            if(UpdateInlineInput())return;
            bool back=TryTakeMenuBack();
            if(back)
            {
                setupTransitionFrame=Time.frameCount;
                if(rosterEditing>=0){if(rosterPicker.activeSelf)CloseRosterPicker();else CloseRosterEditor();}
                else if(pendingSeat>=0)CloseIdentityPicker();
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
