using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    // Session-only shell state. Gameplay still receives the existing detached composition.
    public sealed partial class ProvingGround
    {
        bool lobbyInitialized;
        LobbyTeamMaterial lobbyTeamMaterial;
        public ProvingProfile LobbyProfile=ProvingProfile.CreateLobbyDefault();
        readonly int[] lobbyPositions={0,1,2,3};
        Button lobbyMap,lobbyMode,lobbyTime,lobbyTarget,lobbyBotOpener;
        ArenaPreviewGraphic lobbyPreview,lobbyDialogPreview;
        GameObject lobbyDialog;
        Transform lobbyBody;
        readonly List<Button> lobbyChoices=new List<Button>();
        Action lobbyBack;
        Button lobbyOpener;
        int lobbyDialogFrame;
        InputDevice lobbyJoinDevice;
        string lobbyJoinProfile;
        int lobbyBotDifficulty;
        bool lobbyJoinFromButton;
        bool LobbyModal=>InlineAny||rosterEditing>=0||pendingSeat>=0||(lobbyDialog!=null&&lobbyDialog.activeSelf)||textEntryPanel;

        int LobbyPosition(int seat,int count)=>lobbyPositions.Take(count).Distinct().Count()==count&&lobbyPositions.Take(count).All(p=>p>=0&&p<count)?lobbyPositions[seat]:seat;
        void RemoveLobbyPosition(int seat,int count)
        {
            int removed=LobbyPosition(seat,count);var before=Enumerable.Range(0,count).Select(i=>LobbyPosition(i,count)).ToArray();
            for(int i=0,j=0;i<count;i++)if(i!=seat)lobbyPositions[j++]=before[i]>removed?before[i]-1:before[i];
            lobbyPositions[count-1]=count-1;
        }
        void CreateSingleScreenLobby(Transform parent)
        {
            // Existing hidden legacy controls remain available to diagnostic fixtures, not navigation.
            foreach(var tab in setupSteps)tab.gameObject.SetActive(false);
            setupNext.gameObject.SetActive(false);setupMapPage.SetActive(false);setupRulesPage.SetActive(false);
            setupScreen.transform.Find("setup-description").gameObject.SetActive(false);
            setupSummary.transform.parent.gameObject.SetActive(false);
            rosterPageTitle.gameObject.SetActive(false);
            lobbyMap=RosterButton(setupScreen.transform,"lobby-map","",new Vector2(.05f,.68f),new Vector2(.35f,.89f),OpenLobbyMaps);
            lobbyMap.transform.Find("label").GetComponent<Text>().alignment=TextAnchor.LowerLeft;
            var mapHint=lobbyMap.GetComponent<MenuActionHint>().GlyphLabel;mapHint.rectTransform.anchorMin=new Vector2(1,.04f);mapHint.rectTransform.anchorMax=new Vector2(1,.27f);
            Layout(lobbyMap.transform.Find("label").GetComponent<Text>().rectTransform,new Vector2(.05f,.06f),new Vector2(.86f,.32f));
            var artwork=Panel(lobbyMap.transform,"lobby-map-artwork",Vector2.zero,Vector2.one,Color.white);RoundRosterPanel(artwork);
            artwork.AddComponent<Mask>().showMaskGraphic=false;
            var preview=new GameObject("lobby-map-preview",typeof(RectTransform),typeof(ArenaPreviewGraphic));preview.transform.SetParent(artwork.transform,false);
            Layout((RectTransform)preview.transform,Vector2.zero,Vector2.one);lobbyPreview=preview.GetComponent<ArenaPreviewGraphic>();lobbyPreview.raycastTarget=false;
            var scrim=Panel(artwork.transform,"map-title-scrim",Vector2.zero,new Vector2(1,.60f),Color.white).GetComponent<Image>();scrim.sprite=LobbyVisualAssets.Scrim;scrim.raycastTarget=false;
            lobbyMap.transform.Find("gamepad-action-button").SetAsLastSibling();
            lobbyMap.transform.Find("label").SetAsLastSibling();mapHint.transform.SetAsLastSibling();
            lobbyMode=RosterButton(setupScreen.transform,"lobby-mode","",new Vector2(.38f,.735f),new Vector2(.61f,.805f),()=>CycleLobbyRule("mode",1));
            lobbyTime=RosterButton(setupScreen.transform,"lobby-time","",new Vector2(.64f,.735f),new Vector2(.77f,.805f),()=>CycleLobbyRule("time",1));
            lobbyTarget=RosterButton(setupScreen.transform,"lobby-target","",new Vector2(.80f,.735f),new Vector2(.95f,.805f),()=>CycleLobbyRule("target",1));
            foreach(var tile in new[]{lobbyMap,lobbyMode,lobbyTime,lobbyTarget}){tile.GetComponent<MenuPresentation>().Compact=false;var r=(RectTransform)tile.transform;r.offsetMin=r.offsetMax=Vector2.zero;}
            foreach(var rule in new[]{lobbyMode,lobbyTime,lobbyTarget})
            {
                CreateRuleArrow(rule,"rule-left","‹",new Vector2(.0f,0),new Vector2(.14f,1),-1);
                CreateRuleArrow(rule,"rule-right","›",new Vector2(.86f,0),Vector2.one,1);
                var label=rule.transform.Find("label").GetComponent<Text>();label.alignment=TextAnchor.MiddleCenter;
                Layout(label.rectTransform,new Vector2(.14f,0),new Vector2(.86f,1));
                rule.GetComponent<MenuActionHint>().ReservedRightInset=()=>34;
            }
            Label(setupScreen.transform,"lobby-mode-label","РЕЖИМ",22,new Vector2(.38f,.815f),new Vector2(.61f,.87f),TextAnchor.MiddleLeft,RosterMuted);
            Label(setupScreen.transform,"lobby-time-label","ВРЕМЯ",22,new Vector2(.64f,.815f),new Vector2(.77f,.87f),TextAnchor.MiddleLeft,RosterMuted);
            Label(setupScreen.transform,"lobby-target-label","ЦЕЛЬ",22,new Vector2(.80f,.815f),new Vector2(.95f,.87f),TextAnchor.MiddleLeft,RosterMuted);
            foreach(var button in new[]{setupPrevious,rosterAddBot,rosterAddHuman,start})button.transform.SetParent(setupScreen.transform,false);
            var footerDivider=Panel(setupScreen.transform,"lobby-footer-divider",new Vector2(.645f,.06f),new Vector2(.646f,.13f),new Color32(116,135,148,165)).GetComponent<Image>();footerDivider.raycastTarget=false;
            rosterAddBot.onClick.RemoveAllListeners();rosterAddBot.onClick.AddListener(BeginLobbyBot);
            BindMenuShortcut(rosterAddBot,MenuPadButton.West,()=>phase==Phase.Setup&&!LobbyModal);
            BindMenuShortcut(rosterAddHuman,MenuPadButton.North,()=>phase==Phase.Setup&&!LobbyModal);
            setupPrevious.onClick.RemoveAllListeners();setupPrevious.onClick.AddListener(ToMainMenu);
            BindMenuShortcut(setupPrevious,MenuPadButton.East,()=>phase==Phase.Setup&&!LobbyModal);
            setupMessage.transform.SetParent(setupScreen.transform,false);
            Layout(setupMessage.rectTransform,new Vector2(.05f,.14f),new Vector2(.95f,.19f));
            Layout((RectTransform)setupPlayersPage.transform.parent,new Vector2(.05f,.20f),new Vector2(.95f,.65f));
            Layout((RectTransform)setupPlayersPage.transform,Vector2.zero,Vector2.one);
            rosterFull.gameObject.SetActive(false);
            Layout((RectTransform)rosterFfaScroll.transform,new Vector2(.01f,.015f),new Vector2(.99f,.985f));
            Layout((RectTransform)rosterBluePanel.transform,new Vector2(0,0),new Vector2(.5f,1));
            Layout((RectTransform)rosterRedPanel.transform,new Vector2(.5f,0),Vector2.one);
        }
        void CycleLobbyRule(string rule,int direction)
        {
            if(phase!=Phase.Setup||LobbyModal)return;
            if(rule=="mode")SetMatchMode(SetupMode==NativeMatchMode.Ffa?NativeMatchMode.Teams:NativeMatchMode.Ffa);
            else
            {
                var c=Configuration;
                if(rule=="time")c.DurationMinutes=1+((c.DurationMinutes-1+direction+10)%10);
                else
                {
                    int index=c.TargetEnabled?Mathf.Clamp((c.TargetPoints-1000)/500,0,8):9;
                    index=(index+direction+10)%10;c.TargetEnabled=index!=9;
                    if(c.TargetEnabled)c.TargetPoints=1000+index*500;
                }
                c.Validate(MatchProfile);Configuration=c;RefreshInterface();
            }
        }
        void CreateRuleArrow(Button rule,string name,string text,Vector2 min,Vector2 max,int direction)
        {
            // A pointer-only child intercepts its own click; the parent remains the sole focus stop.
            var label=Label(rule.transform,name,text,36,min,max,TextAnchor.MiddleCenter,Color.white);label.raycastTarget=true;
            label.gameObject.AddComponent<LobbyRuleArrow>().Choose=()=>CycleLobbyRule(rule==lobbyMode?"mode":rule==lobbyTime?"time":"target",direction);
        }
        void StyleLobbySurface(Button button)
        {
            var presentation=button.GetComponent<MenuPresentation>();presentation.Compact=false;presentation.LobbySurface=true;button.transition=Selectable.Transition.None;
            var hint=button.GetComponent<MenuActionHint>();if(hint){hint.ColoredLobbyGlyph=true;hint.HideFocusedConfirm=true;}
            // MenuPresentation owns the material and tint at render time.
            var outline=button.GetComponent<Outline>();if(outline)outline.effectColor=new Color32(109,130,145,180);
        }
        void StyleLobbySurfaces()
        {
            setupPlayersPage.GetComponent<Image>().color=Color.clear;
            setupPlayersPage.transform.parent.GetComponent<Image>().color=Color.clear;
            rosterFfaScroll.GetComponent<Image>().color=Color.clear;
            rosterBluePanel.GetComponent<Image>().color=Color.clear;
            rosterRedPanel.GetComponent<Image>().color=Color.clear;
            if(!lobbyTeamMaterial)
            {
                var material=new GameObject("team-continuous-material",typeof(RectTransform),typeof(CanvasRenderer),typeof(LobbyTeamMaterial));
                material.transform.SetParent(setupPlayersPage.transform,false);material.transform.SetAsFirstSibling();Layout((RectTransform)material.transform,Vector2.zero,Vector2.one);
                lobbyTeamMaterial=material.GetComponent<LobbyTeamMaterial>();lobbyTeamMaterial.raycastTarget=false;
            }
            lobbyTeamMaterial.gameObject.SetActive(SetupMode==NativeMatchMode.Teams);
            lobbyTeamMaterial.Left=RosterTeamColor(NativeTeam.TeamA);lobbyTeamMaterial.Right=RosterTeamColor(NativeTeam.TeamB);lobbyTeamMaterial.SetVerticesDirty();
            foreach(var button in new[]{lobbyMap,lobbyMode,lobbyTime,lobbyTarget,setupPrevious,rosterAddBot,rosterAddHuman})StyleLobbySurface(button);
            start.GetComponent<MenuActionHint>().ColoredLobbyGlyph=true;start.GetComponent<MenuActionHint>().HideFocusedConfirm=true;
            foreach(var button in rosterCards.Concat(rosterEmpty))StyleLobbySurface(button);
        }
        void RefreshSingleScreenLobby()
        {
            if(lobbyMap==null)return;
            setupScreen.SetActive(phase==Phase.Setup);setupPlayersPage.SetActive(true);
            setupMapPage.SetActive(false);setupRulesPage.SetActive(false);setupStep=setupMaxStep=2;
            foreach(var tab in setupSteps)tab.gameObject.SetActive(false);
            setupNext.gameObject.SetActive(false);setupSummary.transform.parent.gameObject.SetActive(false);
            seatPanel.SetActive(false);botPanel.SetActive(false);teamRow.SetActive(false);
            foreach(var b in new[]{seatsMinus,seatsPlus,rebind,setupBack,diagnosticButton})b.gameObject.SetActive(false);
            foreach(var b in seatIdentityButtons)if(b)b.gameObject.SetActive(false);
            var title=setupScreen.transform.Find("setup-brand").GetComponent<Text>();title.text="Подготовка матча";title.fontSize=40;title.fontStyle=FontStyle.Bold;title.color=Color.white;Layout(title.rectTransform,new Vector2(.05f,.915f),new Vector2(.95f,.97f));
            lobbyPreview.Show(SelectedMapId);lobbyMap.transform.Find("label").GetComponent<Text>().text=AuthoredArenaCatalog.Name(SelectedMapId);
            lobbyMode.GetComponentInChildren<Text>().text=(SetupMode==NativeMatchMode.Teams?"Командный бой":"Каждый за себя");
            lobbyTime.GetComponentInChildren<Text>().text=Configuration.DurationMinutes+" мин";
            lobbyTarget.GetComponentInChildren<Text>().text=(Configuration.TargetEnabled?Configuration.TargetPoints.ToString():"Без цели");
            RefreshRosterUi();StyleLobbySurfaces();
            var footer=new[]{setupPrevious,rosterAddBot,rosterAddHuman,start};var labels=new[]{"Назад","Добавить бота","Присоединиться","Начать матч"};
            var footerMin=new[]{new Vector2(.025f,.055f),new Vector2(.18f,.055f),new Vector2(.38f,.055f),new Vector2(.695f,.055f)};
            var footerMax=new[]{new Vector2(.16f,.13f),new Vector2(.37f,.13f),new Vector2(.60f,.13f),new Vector2(.975f,.13f)};
            for(int i=0;i<footer.Length;i++){footer[i].gameObject.SetActive(phase==Phase.Setup);Layout((RectTransform)footer[i].transform,footerMin[i],footerMax[i]);footer[i].GetComponent<MenuPresentation>().Compact=false;var t=footer[i].GetComponentInChildren<Text>();t.text=labels[i];t.fontSize=23;t.alignment=TextAnchor.MiddleCenter;Layout(t.rectTransform,Vector2.zero,Vector2.one);t.rectTransform.offsetMin=new Vector2(58,0);t.rectTransform.offsetMax=new Vector2(-58,0);}
            start.interactable=SetupBlockingReason()=="";start.GetComponent<Image>().color=MenuGold;start.GetComponentInChildren<Text>().color=MenuInk;
            setupMessage.text=setupError??SetupBlockingReason();
            if(lobbyDialog&&lobbyDialog.activeSelf)lobbyDialog.transform.SetAsLastSibling();
            ConfigureLobbyNavigation();
            // The screen owns caption refresh; finish its layout transaction with the
            // opt-in text+hint group so a later refresh cannot restore obsolete insets.
            foreach(var hint in setupScreen.GetComponentsInChildren<MenuActionHint>().Where(h=>h.enabled))hint.Refresh();
        }
        void ConfigureLobbyNavigation()
        {
            if(lobbyMap==null||LobbyModal)return;
            var top=new[]{lobbyMap,lobbyMode,lobbyTime,lobbyTarget};var foot=new[]{setupPrevious,rosterAddBot,rosterAddHuman,start}.Where(b=>b.interactable).ToArray();
            var groups=lobbyRosterGroups??new[]{new[]{rosterCards[0]}};
            var all=groups.SelectMany(g=>g).ToArray();
            for(int i=0;i<top.Length;i++)SetupNavigation(top[i],top[i],all.Length>0?all[Math.Min(i,all.Length-1)]:foot[0],top[Math.Max(0,i-1)],top[Math.Min(3,i+1)]);
            foreach(var group in groups)for(int i=0;i<group.Length;i++)
            {
                int cols=groups.Length==2?2:4;var card=group[i];int flat=Array.IndexOf(all,card);
                SetupNavigation(card,i>=cols?group[i-cols]:top[Math.Min(3,i%cols+(groups.Length==2&&group==groups[1]?2:0))],i+cols<group.Length?group[i+cols]:foot[Math.Min(i%cols,foot.Length-1)],all[Math.Max(0,flat-1)],all[Math.Min(all.Length-1,flat+1)]);
            }
            for(int i=0;i<foot.Length;i++)SetupNavigation(foot[i],all.Length>0?all[Math.Max(0,all.Length-foot.Length+i)]:top[i],foot[i],foot[Math.Max(0,i-1)],foot[Math.Min(foot.Length-1,i+1)]);
        }
        void BeginLobbyDialog(string title,Action back,Button opener=null)
        {
            if(lobbyDialog){lobbyDialog.SetActive(false);Destroy(lobbyDialog);}
            if(opener!=null)lobbyOpener=opener;
            lobbyDialogFrame=setupTransitionFrame=Time.frameCount;lobbyBack=back;lobbyChoices.Clear();
            lobbyDialog=Panel(setupScreen.transform.parent,"lobby-dialog",Vector2.zero,Vector2.one,new Color32(4,12,19,220));
            var card=Panel(lobbyDialog.transform,"lobby-dialog-card",new Vector2(.20f,.14f),new Vector2(.80f,.88f),MenuCard);StyleSelectorCard(card);
            Label(card.transform,"lobby-dialog-title",title,32,new Vector2(.06f,.88f),new Vector2(.94f,.98f),TextAnchor.MiddleLeft,MenuGold);
            lobbyBody=RosterScroll(card.transform,"lobby-options",new Vector2(.05f,.19f),new Vector2(.95f,.82f),out lobbyScroll);
            var backButton=RosterButton(card.transform,"lobby-dialog-back","НАЗАД",new Vector2(.05f,.035f),new Vector2(.30f,.12f),()=>{if(Time.frameCount>lobbyDialogFrame)lobbyBack?.Invoke();});
            BindMenuShortcut(backButton,MenuPadButton.East,()=>lobbyDialog&&lobbyDialog.activeSelf);
            lobbyDialogBack=backButton;
            StartCoroutine(FadeLobbyPanel(card,backButton.colors.fadeDuration));
        }
        System.Collections.IEnumerator FadeLobbyPanel(GameObject panel,float duration)
        {
            var group=panel.GetComponent<CanvasGroup>();if(!group)group=panel.AddComponent<CanvasGroup>();
            for(float elapsed=0;panel&&panel.activeInHierarchy&&elapsed<duration;elapsed+=Time.unscaledDeltaTime)
            {group.alpha=Mathf.SmoothStep(0,1,elapsed/Mathf.Max(.001f,duration));yield return null;}
            if(group)group.alpha=1;
        }
        ScrollRect lobbyScroll;Button lobbyDialogBack;
        Button LobbyChoice(string name,string text,Action choose,bool enabled=true)
        {
            int row=lobbyChoices.Count;
            var b=RosterButton(lobbyBody,name,text,Vector2.zero,Vector2.one,()=>{if(Time.frameCount>lobbyDialogFrame){setupTransitionFrame=Time.frameCount;choose();}});
            var r=(RectTransform)b.transform;r.anchorMin=new Vector2(0,1);r.anchorMax=Vector2.one;r.pivot=new Vector2(.5f,1);r.offsetMin=new Vector2(7,-(row+1)*56+3);r.offsetMax=new Vector2(-7,-row*56-3);
            b.GetComponentInChildren<Text>().fontSize=24;b.GetComponent<MenuPresentation>().FilledFocus=true;b.interactable=enabled;b.GetComponent<RosterFocus>().UseScroll=()=>lobbyScroll;lobbyChoices.Add(b);return b;
        }
        void FinishLobbyChoices(int selected=0)
        {
            ((RectTransform)lobbyBody).sizeDelta=new Vector2(0,lobbyChoices.Count*56);
            var controls=lobbyChoices.Where(b=>b.interactable).Cast<Selectable>().Concat(new[]{lobbyDialogBack}).ToArray();RosterVerticalNavigation(controls);
            Select(selected<lobbyChoices.Count&&lobbyChoices[selected].interactable?lobbyChoices[selected]:lobbyChoices.FirstOrDefault(b=>b.interactable)??lobbyDialogBack);
        }
        void CloseLobbyDialog()
        {
            if(lobbyDialog)lobbyDialog.SetActive(false);lobbyBack=null;setupTransitionFrame=Time.frameCount;RefreshInterface();Select(lobbyOpener??lobbyMap);
        }
        void OpenLobbyMaps()
        {
            BeginLobbyDialog("ВЫБЕРИТЕ КАРТУ",CloseLobbyDialog,lobbyMap);
            lobbyScroll.gameObject.SetActive(false);
            var panel=lobbyScroll.transform.parent;
            foreach(Transform child in panel)if(child.name=="selector-rule")child.gameObject.SetActive(false);
            Layout((RectTransform)panel,new Vector2(.13f,.12f),new Vector2(.87f,.90f));
            for(int i=0;i<SetupMapIds.Length;i++)
            {
                string id=SetupMapIds[i];int col=i%2,row=i/2;
                var button=RosterButton(panel,"lobby-map-"+id,"",new Vector2(.045f+col*.47f,.49f-row*.34f),new Vector2(.485f+col*.47f,.81f-row*.34f),()=>{if(Time.frameCount<=lobbyDialogFrame)return;SelectAuthoredMap(id);CloseLobbyDialog();});
                button.GetComponent<MenuPresentation>().Compact=false;((RectTransform)button.transform).offsetMin=((RectTransform)button.transform).offsetMax=Vector2.zero;button.GetComponent<MenuActionHint>().HideFocusedConfirm=true;
                var image=new GameObject("map-art",typeof(RectTransform),typeof(ArenaPreviewGraphic));image.transform.SetParent(button.transform,false);Layout((RectTransform)image.transform,new Vector2(.012f,.02f),new Vector2(.988f,.98f));image.GetComponent<ArenaPreviewGraphic>().Show(id);image.GetComponent<ArenaPreviewGraphic>().raycastTarget=false;
                var shade=Panel(button.transform,"map-caption-backing",new Vector2(.012f,.02f),new Vector2(.988f,.22f),new Color32(6,15,24,230));shade.GetComponent<Image>().raycastTarget=false;
                var caption=button.transform.Find("label").GetComponent<Text>();caption.text=AuthoredArenaCatalog.Name(id);caption.fontSize=26;caption.alignment=TextAnchor.MiddleLeft;Layout(caption.rectTransform,new Vector2(.04f,.02f),new Vector2(.96f,.22f));caption.transform.SetAsLastSibling();
                lobbyChoices.Add(button);
            }
            for(int i=0;i<4;i++)SetupNavigation(lobbyChoices[i],i>=2?lobbyChoices[i-2]:lobbyChoices[i],i<2?lobbyChoices[i+2]:lobbyDialogBack,lobbyChoices[i%2==1?i-1:i],lobbyChoices[i%2==0?i+1:i]);
            SetupNavigation(lobbyDialogBack,lobbyChoices[2],lobbyDialogBack,lobbyDialogBack,lobbyDialogBack);
            Select(lobbyChoices[Mathf.Max(0,Array.IndexOf(SetupMapIds,SelectedMapId))]);
            RouteInlineInput();
        }
        void UpdateLobbyDialogInput()
        {
            if(Time.frameCount<=lobbyDialogFrame)return;
            if(TryTakeMenuBack())lobbyBack?.Invoke();
        }
        void BeginLobbyBot()
        {
            if(LobbyModal||RosterTotal>=8)return;var selected=EventSystem.current?.currentSelectedGameObject; lobbyBotOpener=selected&&selected.name.StartsWith("roster-empty-")?selected.GetComponent<Button>():rosterAddBot;lobbyBotDifficulty=lastBotDifficulty;ShowLobbyBotDifficulty();
        }
        void ShowLobbyBotDifficulty()
        {
            BeginLobbyDialog("СЛОЖНОСТЬ БОТА",CloseLobbyDialog,lobbyBotOpener);
            for(int i=0;i<3;i++){int d=i;LobbyChoice("lobby-add-difficulty-"+i,RosterDifficultyNames[i],()=>{lobbyBotDifficulty=d;if(SetupMode==NativeMatchMode.Teams)ShowLobbyTeams(CommitLobbyBot,ShowLobbyBotDifficulty);else CommitLobbyBot(NativeTeam.TeamA);});}
            FinishLobbyChoices(lobbyBotDifficulty);
        }
        void ShowLobbyTeams(Action<NativeTeam> commit,Action back)
        {
            BeginLobbyDialog("КОМАНДА",back);
            foreach(var team in new[]{NativeTeam.TeamA,NativeTeam.TeamB}){var t=team;int count=Enumerable.Range(0,RosterTotal).Count(p=>RosterTeamAt(p)==t);LobbyChoice("lobby-add-team-"+(int)t,RosterTeamName(t)+" · "+count,()=>commit(t));}
            FinishLobbyChoices(SmallerRosterTeam(LocalSeatCount)==NativeTeam.TeamA?0:1);
        }
        void CommitLobbyBot(NativeTeam team)
        {
            if(!botSetup.CanAdd(LocalSeatCount)){setupError="Состав заполнен";CloseLobbyDialog();return;}
            botSetup.Add(LocalSeatCount);int i=botSetup.Count-1;botSetup.SetDifficulty(i,lobbyBotDifficulty);botSetup.SetTeam(i,team);lastBotDifficulty=lobbyBotDifficulty;
            setupError=null;CloseLobbyDialog();FocusRosterCard(LocalSeatCount+i);
        }
        bool LobbyDeviceFree(InputDevice device)=>device!=null&&device.added&&device.enabled&&GamepadCompatibility.CanAssign(device)&&!(device is Keyboard&&Mouse.current==null)&&!Enumerable.Range(0,LocalSeatCount).Any(s=>input.DeviceAt(s)==device);
        void BeginLobbyDevices()
        {
            if(LobbyModal||LocalSeatCount>=4||RosterTotal>=8)return;
            ShowLobbyDevices();
        }
        void ShowLobbyDevices()
        {
            lobbyJoinFromButton=true;lobbyJoinDevice=null;
            BeginLobbyDialog("УСТРОЙСТВО",CloseLobbyDialog,rosterAddHuman);
            foreach(var device in GamepadCompatibility.ListedDevices().Where(LobbyDeviceFree)){var d=device;LobbyChoice("lobby-device-"+d.deviceId,GamepadCompatibility.Name(d)+" #"+d.deviceId,()=>{lobbyJoinDevice=d;ShowLobbyJoinProfiles();});}
            if(lobbyChoices.Count==0)LobbyChoice("lobby-no-devices","Нет свободных устройств",()=>{},false);
            FinishLobbyChoices();
        }
        void BeginLobbyJoin(InputDevice device)
        {
            if(LobbyModal||!LobbyDeviceFree(device))return;
            // Explicit Y recovery retains the old identity/team; it never steals a connected device.
            int lost=Enumerable.Range(0,LocalSeatCount).Where(s=>input.IsHumanSeat(s)&&input.DeviceAt(s)!=null&&!input.IsConnected(s)).DefaultIfEmpty(-1).First();
            if(lost>=0){if(input.Replace(lost,device)){identities.RememberBinding(lost,device.deviceId);setupError=null;RefreshInterface();FocusRosterCard(lost);}return;}
            if(LocalSeatCount>=4||RosterTotal>=8){setupError="Нет свободного экрана";RefreshInterface();return;}
            lobbyJoinFromButton=false;lobbyJoinDevice=device;ShowLobbyJoinProfiles();
        }
        void ShowLobbyJoinProfiles()
        {
            ShowLobbyProfiles(id=>{lobbyJoinProfile=id;if(SetupMode==NativeMatchMode.Teams)ShowLobbyTeams(CommitLobbyHuman,ShowLobbyJoinProfiles);else CommitLobbyHuman(NativeTeam.TeamA);},()=>{if(lobbyJoinFromButton)ShowLobbyDevices();else CloseLobbyDialog();});
        }
        void ShowLobbyProfiles(Action<string> choose,Action back)
        {
            BeginLobbyDialog("ПРОФИЛЬ",back,rosterEditing>=0?rosterIdentity:rosterAddHuman);
            LobbyChoice("lobby-profile-none","Без профиля",()=>choose(null));
            foreach(var profile in playerProfiles.ByUsage())
            {
                string id=profile.Id;bool busy=identities.InUse(id,rosterEditing);
                LobbyChoice("lobby-profile-"+id,profile.Name+(busy?" · занят":""),()=>choose(id),!busy);
            }
            // Separate action below the scrolling list; eight rows fit Full HD.
            var create=RosterButton(lobbyScroll.transform.parent,"lobby-create-profile","СОЗДАТЬ ПРОФИЛЬ",new Vector2(.50f,.035f),new Vector2(.95f,.12f),()=>ShowLobbyCreateProfile(()=>ShowLobbyProfiles(choose,back)));
            FinishLobbyChoices();var available=lobbyChoices.Where(b=>b.interactable).Cast<Selectable>().Concat(new Selectable[]{create,lobbyDialogBack}).ToArray();RosterVerticalNavigation(available);
        }
        void ShowLobbyCreateProfile(Action back)
        {
            BeginLobbyDialog("НОВЫЙ ПРОФИЛЬ",back);lobbyScroll.gameObject.SetActive(false);
            var card=lobbyScroll.transform.parent;var name=CreateNameInput(card);name.text="";
            var error=Label(card,"lobby-name-error","",22,new Vector2(.08f,.43f),new Vector2(.92f,.60f),TextAnchor.MiddleLeft,MenuGold);
            var create=RosterButton(card,"lobby-save-profile","СОЗДАТЬ",new Vector2(.55f,.25f),new Vector2(.92f,.36f),()=>{if(string.IsNullOrWhiteSpace(name.text)){error.text="Введите имя";return;}CreateProfileWithGeneralSettings(name.text);back();});
            RosterVerticalNavigation(new Selectable[]{name,create,lobbyDialogBack});Select(name);
            if(menuDevice is Gamepad pad)OpenTextEntry(name,pad,false);
        }
        void CommitLobbyHuman(NativeTeam team)
        {
            if(LocalSeatCount>=4||RosterTotal>=8||!LobbyDeviceFree(lobbyJoinDevice)||(lobbyJoinProfile!=null&&(playerProfiles.Find(lobbyJoinProfile)==null||identities.InUse(lobbyJoinProfile))))
            {setupError="Устройство, профиль или место уже недоступны";CloseLobbyDialog();return;}
            int seat=LocalSeatCount;input.SetActiveSeatCount(seat+1);botSetup.SetAi(seat,false);SyncHumanSeats();
            if(!input.Assign(seat,lobbyJoinDevice)){input.SetActiveSeatCount(seat);setupError="Устройство недоступно";CloseLobbyDialog();return;}
            identities.ClearSeat(seat);lobbyPositions[seat]=seat;teamAssignments[seat]=team;
            if(lobbyJoinProfile==null)identities.ChooseGuest(seat,lobbyJoinDevice.deviceId,MouseSensitivityPreference.Resolve(Profile),fps.Visible);
            else identities.ChooseProfile(seat,lobbyJoinDevice.deviceId,lobbyJoinProfile,playerProfiles);
            setupError=null;ApplyLayout(LocalSeatCount);CloseLobbyDialog();FocusRosterCard(seat);
        }
        void OpenLobbyScreens()
        {
            int p=rosterEditing;if(p<0)return;
            BeginLobbyDialog("РАСПОЛОЖЕНИЕ ЭКРАНА",()=>{CloseLobbyDialog();Select(rosterDevice);},rosterDevice);
            lobbyScroll.gameObject.SetActive(false);var card=lobbyScroll.transform.parent;
            bool extra=p>=LocalSeatCount;int count=extra&&LocalSeatCount<4?LocalSeatCount+1:LocalSeatCount;
            var screenButtons=new List<Selectable>();
            for(int pos=0;pos<count;pos++)
            {
                int position=pos;int occupant=Enumerable.Range(0,LocalSeatCount).Where(s=>LobbyPosition(s,LocalSeatCount)==position).DefaultIfEmpty(-1).First();
                bool enabled=!extra||occupant<0||RosterIsBot(occupant)||LocalSeatCount<4;
                var v=LocalSeatLayout.Viewport(count,pos);var b=RosterButton(card,"lobby-screen-"+pos,(pos+1)+" · "+(occupant<0?"Свободно":RosterIsBot(occupant)?"Бот":IdentityLabel(occupant))+(enabled?"":"\nНет свободной позиции"),new Vector2(.08f+v.xMin*.84f,.30f+v.yMin*.53f),new Vector2(.08f+v.xMax*.84f-.01f,.30f+v.yMax*.53f-.01f),()=>{AssignLobbyScreen(p,position);CloseLobbyDialog();Select(rosterDevice);});b.GetComponent<MenuPresentation>().Compact=false;((RectTransform)b.transform).offsetMin=((RectTransform)b.transform).offsetMax=Vector2.zero;b.interactable=enabled;screenButtons.Add(b);
            }
            var blank=LocalSeatLayout.PersistentStandings(count);if(blank.HasValue){var v=blank.Value;Label(card,"lobby-screen-standings","ТАБЛИЦА СЧЁТА",22,new Vector2(.08f+v.xMin*.84f,.30f+v.yMin*.53f),new Vector2(.08f+v.xMax*.84f,.30f+v.yMax*.53f),TextAnchor.MiddleCenter,RosterMuted);}
            if(RosterIsBot(p))
            {
                var off=RosterButton(card,"lobby-screen-off","БЕЗ ЭКРАНА",new Vector2(.40f,.17f),new Vector2(.92f,.25f),()=>{SetRosterBotView(false);CloseLobbyDialog();Select(rosterDevice);});off.interactable=extra||LocalSeatCount>1;screenButtons.Add(off);
            }
            screenButtons.Add(lobbyDialogBack);
            var enabledButtons=screenButtons.Where(b=>b.interactable).ToArray();
            Canvas.ForceUpdateCanvases();
            foreach(var button in enabledButtons)
            {
                Vector2 center=((RectTransform)button.transform).TransformPoint(((RectTransform)button.transform).rect.center);
                Func<Vector2,Selectable> nearest=direction=>enabledButtons.Where(b=>b!=button).Select(b=>new{Button=b,Delta=(Vector2)((RectTransform)b.transform).TransformPoint(((RectTransform)b.transform).rect.center)-center})
                    .Where(x=>Vector2.Dot(x.Delta,direction)>1&&Mathf.Abs(Vector2.Dot(x.Delta,new Vector2(-direction.y,direction.x)))<=Vector2.Dot(x.Delta,direction)*1.5f)
                    .OrderBy(x=>x.Delta.sqrMagnitude/Vector2.Dot(x.Delta,direction)).Select(x=>x.Button).FirstOrDefault()??button;
                SetupNavigation(button,nearest(Vector2.up),nearest(Vector2.down),nearest(Vector2.left),nearest(Vector2.right));
            }
            Select(enabledButtons[0]);
        }
        void AssignLobbyScreen(int participant,int position)
        {
            int count=LocalSeatCount;
            int occupant=Enumerable.Range(0,count).Where(s=>LobbyPosition(s,count)==position).DefaultIfEmpty(-1).First();
            if(participant<count)
            {
                if(occupant<0)return;int previous=lobbyPositions[participant];lobbyPositions[participant]=position;lobbyPositions[occupant]=previous;
            }
            else if(occupant>=0&&RosterIsBot(occupant))
            {
                int botIndex=participant-count;var incoming=botSetup.At(botIndex);var outgoing=botSetup.SeatBot(occupant,teamAssignments[occupant]);
                botSetup.SetSeatBot(occupant,incoming);teamAssignments[occupant]=incoming.Team;botSetup.ReplaceBot(botIndex,outgoing);rosterEditing=occupant;
            }
            else if(count<4)
            {
                PromoteRosterBot(participant-count);lobbyPositions[count]=position;
                if(occupant>=0)lobbyPositions[occupant]=count;
            }
            ApplyLayout(LocalSeatCount);RefreshInterface();
        }
    }
    public sealed class LobbyPreviewFocus : MonoBehaviour,ISelectHandler,IPointerEnterHandler
    {
        public Action Show;
        public void OnSelect(BaseEventData e)=>Show?.Invoke();
        public void OnPointerEnter(PointerEventData e)=>Show?.Invoke();
    }
    public sealed partial class ProvingProfile
    {
        internal ProvingProfile BeforeLobbyRemovalHold()
        {
            if(id!="single-screen-lobby-v1"||version<2)return this;
            var old=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(this));
            old.descriptors.RemoveAll(d=>d.Path=="lobby.removeHoldSeconds");old.values.RemoveAll(v=>v.Path=="lobby.removeHoldSeconds");old.version=1;return old;
        }
        public static ProvingProfile CreateLobbyDefault()
        {
            var p=new ProvingProfile{id="single-screen-lobby-v1",version=2};
            p.Add("lobby.durationMinutes","lobby","Начальное время","Время нового лобби после запуска; редактор использует этот шаг.","minutes",1,30,1,3);
            p.Add("lobby.removeHoldSeconds","lobby","Удержание удаления","Время West/X до удаления выбранного участника; отпускание отменяет.","seconds",.3f,3f,.1f,1f);
            p.Add("lobby.targetPoints","lobby","Начальная цель","Цель нового лобби после запуска; редактор использует этот шаг.","points",1000,20000,500,2000);
            return p;
        }
    }
}

namespace StarTournament.ProvingGround
{
    public sealed class LobbyRuleArrow : UnityEngine.MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
    {
        public System.Action Choose;
        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData e)
        { if(e.button==UnityEngine.EventSystems.PointerEventData.InputButton.Left){Choose?.Invoke();e.Use();} }
    }
    public sealed class LobbyTeamMaterial : UnityEngine.UI.MaskableGraphic
    {
        public UnityEngine.Color Left,Right;
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();var rect=rectTransform.rect;
            const int strips=32;
            for(int i=0;i<strips;i++)
            {
                float a=i/(float)strips,b=(i+1)/(float)strips;
                var ca=UnityEngine.Color.Lerp(Left,Right,UnityEngine.Mathf.SmoothStep(0,1,(a-.3f)/.4f));
                var cb=UnityEngine.Color.Lerp(Left,Right,UnityEngine.Mathf.SmoothStep(0,1,(b-.3f)/.4f));
                int n=mesh.currentVertCount;
                mesh.AddVert(new UnityEngine.Vector3(rect.xMin+a*rect.width,rect.yMin),ca*.12f+new UnityEngine.Color(.015f,.02f,.025f,.88f),UnityEngine.Vector2.zero);
                mesh.AddVert(new UnityEngine.Vector3(rect.xMin+a*rect.width,rect.yMax),ca*.24f+new UnityEngine.Color(.025f,.03f,.035f,.76f),UnityEngine.Vector2.zero);
                mesh.AddVert(new UnityEngine.Vector3(rect.xMin+b*rect.width,rect.yMax),cb*.24f+new UnityEngine.Color(.025f,.03f,.035f,.76f),UnityEngine.Vector2.zero);
                mesh.AddVert(new UnityEngine.Vector3(rect.xMin+b*rect.width,rect.yMin),cb*.12f+new UnityEngine.Color(.015f,.02f,.025f,.88f),UnityEngine.Vector2.zero);
                mesh.AddTriangle(n,n+1,n+2);mesh.AddTriangle(n,n+2,n+3);
            }
        }
    }
}
