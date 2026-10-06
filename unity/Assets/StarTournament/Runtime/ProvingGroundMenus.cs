using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingGround
    {
        static readonly Color32 MenuInk = new Color32(17,30,41,255);
        static readonly Color32 MenuCard = new Color32(28,49,60,255);
        static readonly Color32 MenuGold = new Color32(255,189,98,255);
        GameObject oldMenuColumn, mainMenuScreen, setupScreen, setupMapPage, setupRulesPage, setupPlayersPage, profilesScreen, settingsScreen, labScreen, identityScreen;
        Transform profileList, identityList;
        Text profileDetail, identityHeading, labDetail, setupSummary, setupMessage, setupLabIdentity;
        InputField profileNameInput;
        Button mainBattleButton, profilesBackButton, setupNext, setupPrevious;
        readonly Button[] setupSteps = new Button[3];
        int setupStep, setupMaxStep;
        InputSystemUIInputModule menuInputModule;
        InputAction menuSubmitAction,menuMoveAction,menuClickAction;
        InputDevice menuDevice;
        Button identitySettingsButton,operatorSettingsButton;
        Button[] seatIdentityButtons = new Button[SeatInputCoordinator.SeatCount];
        string selectedProfileId;
        int pendingSeat = -1, settingsSeat = -1;
        Phase settingsReturn = Phase.MainMenu;

        GameObject Panel(Transform parent,string name,Vector2 min,Vector2 max,Color color)
        {
            var panel=new GameObject(name,typeof(RectTransform),typeof(Image));panel.transform.SetParent(parent,false);
            Layout((RectTransform)panel.transform,min,max);panel.GetComponent<Image>().color=color;
            return panel;
        }
        Text Label(Transform parent,string name,string value,int size,Vector2 min,Vector2 max,TextAnchor align,Color color)
        {
            var text=TextElement(parent,name,value,size);Layout(text.rectTransform,min,max);text.alignment=align;text.color=color;return text;
        }
        Button MenuButton(Transform parent,string name,string value,Vector2 min,Vector2 max,UnityEngine.Events.UnityAction action,bool enabled=true)
        {
            var button=ButtonElement(parent,value,action);button.name=name;Layout((RectTransform)button.transform,min,max);
            var image=button.GetComponent<Image>();image.color=MenuCard;
            var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=MenuGold;
            colors.selectedColor=MenuGold;colors.pressedColor=new Color(.75f,.57f,.32f);colors.disabledColor=new Color(.43f,.50f,.52f);
            button.colors=colors;button.interactable=enabled;
            return button;
        }
        void CreateModernMenuUi(Transform parent)
        {
            oldMenuColumn=parent.Find("menu").gameObject;
            mainMenuScreen=Panel(parent,"main-menu",Vector2.zero,Vector2.one,MenuInk);
            var left=Panel(mainMenuScreen.transform,"actions",new Vector2(.06f,.13f),new Vector2(.53f,.87f),new Color32(20,39,53,255));
            var right=Panel(mainMenuScreen.transform,"broadcast",new Vector2(.56f,.13f),new Vector2(.94f,.87f),MenuCard);
            Label(left.transform,"brand","STAR TOURNAMENT",38,new Vector2(.07f,.85f),new Vector2(.95f,.97f),TextAnchor.MiddleLeft,MenuGold);
            string[] labels={"Битва","Сетевая игра · скоро","Лаборатория геймдизайна","Профили игроков","Настройки игры","Выход"};
            UnityEngine.Events.UnityAction[] actions={OpenSetup,()=>{},OpenLab,OpenProfiles,()=>OpenSettings(-1),()=>Application.Quit()};
            for(int i=0;i<labels.Length;i++)
            {
                float top=.79f-i*.115f;
                var button=MenuButton(left.transform,"main-action-"+i,labels[i],new Vector2(.07f,top-.095f),new Vector2(.93f,top),actions[i],i!=1);
                if(i==0)mainBattleButton=button;
            }
            Label(right.transform,"bowl","КОМБАТ БОУЛ",42,new Vector2(.08f,.73f),new Vector2(.93f,.88f),TextAnchor.MiddleLeft,Color.white);
            Label(right.transform,"league","ОРБИТАЛЬНАЯ ЛИГА",26,new Vector2(.08f,.65f),new Vector2(.93f,.74f),TextAnchor.MiddleLeft,MenuGold);
            Label(right.transform,"route","01 / ЛОКАЛЬНЫЙ МАТЧ\n\nВыберите состав, профили и устройства",26,new Vector2(.08f,.36f),new Vector2(.93f,.61f),TextAnchor.UpperLeft,Color.white);
            Label(right.transform,"controls","МЫШЬ: выбор   ·   ГЕЙМПАД: крестовина / A",22,new Vector2(.08f,.07f),new Vector2(.95f,.19f),TextAnchor.MiddleLeft,new Color32(177,199,201,255));
            var releaseLabel=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_RELEASE_LABEL");
            if(!string.IsNullOrEmpty(releaseLabel))
            {
                Label(mainMenuScreen.transform,"installed-release",releaseLabel,22,new Vector2(.06f,.025f),new Vector2(.52f,.10f),TextAnchor.MiddleLeft,new Color32(177,199,201,255));
                var updateStatus=Label(mainMenuScreen.transform,"update-status","Проверка обновлений…",19,new Vector2(.56f,.02f),new Vector2(.78f,.11f),TextAnchor.MiddleLeft,new Color32(177,199,201,255));
                var updateButton=MenuButton(mainMenuScreen.transform,"update-action","Проверить",new Vector2(.79f,.025f),new Vector2(.94f,.105f),()=>{});
                gameObject.AddComponent<NativeUpdateBridge>().Bind(updateStatus,updateButton);
            }

            CreateSetupUi(parent);

            profilesScreen=Panel(parent,"profiles-screen",Vector2.zero,Vector2.one,MenuInk);
            var listCard=Panel(profilesScreen.transform,"profile-list-card",new Vector2(.06f,.12f),new Vector2(.57f,.89f),new Color32(20,39,53,255));
            var detailCard=Panel(profilesScreen.transform,"profile-detail-card",new Vector2(.59f,.12f),new Vector2(.94f,.89f),MenuCard);
            Label(listCard.transform,"profiles-title","ПРОФИЛИ ИГРОКОВ",35,new Vector2(.06f,.86f),new Vector2(.94f,.98f),TextAnchor.MiddleLeft,Color.white);
            var list=Panel(listCard.transform,"profile-list",new Vector2(.06f,.20f),new Vector2(.94f,.84f),new Color32(20,39,53,255));profileList=list.transform;
            MenuButton(listCard.transform,"profile-create","+ СОЗДАТЬ ПРОФИЛЬ",new Vector2(.06f,.10f),new Vector2(.94f,.18f),CreateNamedProfile);
            profilesBackButton=MenuButton(listCard.transform,"profiles-back","‹ НАЗАД",new Vector2(.06f,.01f),new Vector2(.94f,.08f),ToMainMenu);
            profileDetail=Label(detailCard.transform,"profile-detail","Выберите профиль",27,new Vector2(.08f,.77f),new Vector2(.95f,.97f),TextAnchor.MiddleLeft,MenuGold);
            profileNameInput=CreateNameInput(detailCard.transform);
            MenuButton(detailCard.transform,"profile-rename","Переименовать",new Vector2(.08f,.57f),new Vector2(.92f,.64f),RenameSelectedProfile);
            MenuButton(detailCard.transform,"profile-settings","Личные настройки",new Vector2(.08f,.28f),new Vector2(.92f,.48f),OpenProfileSettings);
            MenuButton(detailCard.transform,"profile-delete","Удалить профиль",new Vector2(.08f,.06f),new Vector2(.92f,.16f),DeleteSelectedProfile);

            CreateSettingsUi(parent);

            CreateDesignLabUi(parent);

            identityScreen=Panel(parent,"identity-picker",Vector2.zero,Vector2.one,new Color32(8,17,25,248));
            var identityCard=Panel(identityScreen.transform,"identity-card",new Vector2(.17f,.12f),new Vector2(.83f,.88f),new Color32(20,39,53,255));
            identityHeading=Label(identityCard.transform,"identity-heading","КТО ИГРАЕТ?",35,new Vector2(.06f,.85f),new Vector2(.94f,.97f),TextAnchor.MiddleLeft,MenuGold);
            var options=Panel(identityCard.transform,"identity-options",new Vector2(.06f,.19f),new Vector2(.94f,.82f),new Color32(20,39,53,255));identityList=options.transform;
            MenuButton(identityCard.transform,"identity-cancel","‹ НАЗАД",new Vector2(.06f,.06f),new Vector2(.48f,.16f),()=>{pendingSeat=-1;RefreshInterface();});
            identitySettingsButton=MenuButton(identityCard.transform,"identity-settings","Личные настройки",new Vector2(.52f,.06f),new Vector2(.94f,.16f),()=>{int seat=pendingSeat;pendingSeat=-1;OpenSettings(seat);});
            for(int seat=0;seat<seatIdentityButtons.Length;seat++)
            {
                int localSeat=seat;
                seatIdentityButtons[seat]=MenuButton(parent,"seat-identity-"+seat,"Выбрать профиль",new Vector2(.53f,.57f-seat*.07f),new Vector2(.97f,.63f-seat*.07f),()=>OpenIdentityPicker(localSeat));
                seatIdentityButtons[seat].transform.SetParent(setupPlayersPage.transform,false);
                float top=.42f-seat*.055f;
                Layout((RectTransform)seatIdentityButtons[seat].transform,new Vector2(.065f,top-.047f),new Vector2(.49f,top));
            }
        }
        void MoveSetupControl(Button button,GameObject page,Vector2 min,Vector2 max)
        {
            button.transform.SetParent(page.transform,false);
            Layout((RectTransform)button.transform,min,max);
            button.GetComponent<Image>().color=MenuCard;
        }
        void CreateSetupUi(Transform parent)
        {
            setupScreen=Panel(parent,"setup-screen",Vector2.zero,Vector2.one,MenuInk);
            Label(setupScreen.transform,"setup-brand","STAR TOURNAMENT  /  БИТВА",35,new Vector2(.05f,.91f),new Vector2(.95f,.98f),TextAnchor.MiddleLeft,MenuGold);
            Label(setupScreen.transform,"setup-description","Выберите карту, задайте правила, соберите игроков и начните матч.",22,new Vector2(.05f,.86f),new Vector2(.95f,.92f),TextAnchor.MiddleLeft,Color.white);
            var steps=new[]{"1  КАРТА","2  ПРАВИЛА","3  ИГРОКИ"};
            for(int i=0;i<3;i++)
            {
                int selected=i;float left=.05f+i*.305f;
                setupSteps[i]=MenuButton(setupScreen.transform,"setup-step-"+i,steps[i],new Vector2(left,.78f),new Vector2(left+.29f,.85f),()=>ShowSetupStep(selected));
            }
            var content=Panel(setupScreen.transform,"setup-content",new Vector2(.05f,.12f),new Vector2(.69f,.76f),new Color32(20,39,53,255));
            var summary=Panel(setupScreen.transform,"setup-summary-card",new Vector2(.71f,.12f),new Vector2(.95f,.76f),MenuCard);
            Label(summary.transform,"summary-title","ПЕРЕД СТАРТОМ",26,new Vector2(.07f,.85f),new Vector2(.93f,.96f),TextAnchor.MiddleLeft,MenuGold);
            setupSummary=Label(summary.transform,"setup-summary","",20,new Vector2(.07f,.53f),new Vector2(.93f,.83f),TextAnchor.UpperLeft,Color.white);
            setupLabIdentity=Label(summary.transform,"setup-lab-identity","",16,new Vector2(.07f,.34f),new Vector2(.93f,.52f),TextAnchor.UpperLeft,Color.white);setupLabIdentity.supportRichText=false;
            setupMessage=Label(summary.transform,"setup-message","",18,new Vector2(.07f,.22f),new Vector2(.93f,.34f),TextAnchor.UpperLeft,new Color32(255,213,157,255));
            setupPrevious=MenuButton(summary.transform,"setup-previous","‹ НАЗАД",new Vector2(.07f,.12f),new Vector2(.93f,.20f),PreviousSetupStep);
            setupNext=MenuButton(summary.transform,"setup-next","ДАЛЕЕ ›",new Vector2(.07f,.02f),new Vector2(.93f,.10f),NextSetupStep);
            setupMapPage=Panel(content.transform,"setup-map",Vector2.zero,Vector2.one,new Color32(20,39,53,255));
            Label(setupMapPage.transform,"map-title","ВЫБЕРИТЕ КАРТУ",30,new Vector2(.05f,.86f),new Vector2(.95f,.98f),TextAnchor.MiddleLeft,Color.white);
            Label(setupMapPage.transform,"map-help","Опубликованные карты с точной ревизией. Выбор не меняет геометрию.",20,new Vector2(.05f,.77f),new Vector2(.95f,.86f),TextAnchor.MiddleLeft,new Color32(177,199,201,255));
            var mapCard=Panel(setupMapPage.transform,"selected-map",new Vector2(.05f,.36f),new Vector2(.95f,.73f),MenuCard);
            Label(mapCard.transform,"map-name","COMBAT BOWL",31,new Vector2(.05f,.59f),new Vector2(.95f,.90f),TextAnchor.MiddleLeft,MenuGold);
            Label(mapCard.transform,"map-revision","Орбитальная арена · "+CombatBowlCatalog.Identity+"\nФиксированная карта · 2–8 участников",21,new Vector2(.05f,.14f),new Vector2(.95f,.58f),TextAnchor.MiddleLeft,Color.white);
            MoveSetupControl(arenaButton,setupMapPage,new Vector2(.05f,.15f),new Vector2(.95f,.30f));
            MenuButton(setupMapPage.transform,"setup-map-exit","‹ ГЛАВНОЕ МЕНЮ",new Vector2(.05f,.03f),new Vector2(.45f,.12f),ToMainMenu);

            setupRulesPage=Panel(content.transform,"setup-rules",Vector2.zero,Vector2.one,new Color32(20,39,53,255));
            Label(setupRulesPage.transform,"rules-title","ПРАВИЛА МАТЧА",30,new Vector2(.05f,.87f),new Vector2(.95f,.98f),TextAnchor.MiddleLeft,Color.white);
            MoveSetupControl(modeButton,setupRulesPage,new Vector2(.05f,.68f),new Vector2(.95f,.82f));
            Label(setupRulesPage.transform,"mode-help","Каждый за себя или две команды с отдельным счётом.",19,new Vector2(.05f,.61f),new Vector2(.95f,.67f),TextAnchor.MiddleLeft,new Color32(177,199,201,255));
            MoveSetupControl(durationButton,setupRulesPage,new Vector2(.05f,.47f),new Vector2(.69f,.59f));
            var durationMinus=oldMenuColumn.transform.Find("duration-minus").GetComponent<Button>();
            MoveSetupControl(durationMinus,setupRulesPage,new Vector2(.71f,.47f),new Vector2(.95f,.59f));
            Label(setupRulesPage.transform,"duration-help","Матч закончится по времени или раньше по цели.",19,new Vector2(.05f,.40f),new Vector2(.95f,.46f),TextAnchor.MiddleLeft,new Color32(177,199,201,255));
            MoveSetupControl(targetButton,setupRulesPage,new Vector2(.05f,.27f),new Vector2(.69f,.39f));
            MoveSetupControl(targetMinus,setupRulesPage,new Vector2(.71f,.27f),new Vector2(.82f,.39f));
            MoveSetupControl(targetPlus,setupRulesPage,new Vector2(.84f,.27f),new Vector2(.95f,.39f));
            Label(setupRulesPage.transform,"target-help","Необязательный порог по очкам.",19,new Vector2(.05f,.20f),new Vector2(.95f,.26f),TextAnchor.MiddleLeft,new Color32(177,199,201,255));
            teamRow.transform.SetParent(setupRulesPage.transform,false);
            Layout((RectTransform)teamRow.transform,new Vector2(.05f,.09f),new Vector2(.95f,.20f));
            MoveSetupControl(colorButton,setupRulesPage,new Vector2(.05f,.01f),new Vector2(.95f,.08f));

            setupPlayersPage=Panel(content.transform,"setup-players",Vector2.zero,Vector2.one,new Color32(20,39,53,255));
            MoveSetupControl(start,summary,new Vector2(.07f,.02f),new Vector2(.93f,.10f));
            CreateRosterUi(parent);

        }
        void ShowSetupStep(int step)
        {
            if(phase!=Phase.Setup || step<0 || step>setupMaxStep || step>2)return;
            rosterEditing=-1;setupStep=step;RefreshInterface();if(step==2)FocusRosterCard(0);else Select(setupNext);
        }
        void NextSetupStep() { setupMaxStep=Mathf.Max(setupMaxStep,Mathf.Min(2,setupStep+1));ShowSetupStep(Mathf.Min(2,setupStep+1)); }
        void PreviousSetupStep() { if(setupStep==0)ToMainMenu();else ShowSetupStep(setupStep-1); }
        string SetupBlockingReason()
        {
            if(LocalSeatCount+botSetup.Count>AuthoredArenaCatalog.Maximum(SelectedMapId))return "Для этой карты нужно 2–"+AuthoredArenaCatalog.Maximum(SelectedMapId)+" участника";
            if(!ValidSetup())return LocalSeatCount+botSetup.Count<2?"Добавьте игрока или бота":"Для команд нужны обе команды";
            if(!input.Ready)return "Подключите устройства людей";
            if(!IdentitiesReady())return "Выберите профиль или гостя каждому человеку";
            return "";
        }
        void RefreshSetupUi()
        {
            if(setupScreen==null)return;
            setupScreen.SetActive(phase==Phase.Setup && pendingSeat<0);
            setupMapPage.SetActive(setupStep==0);setupRulesPage.SetActive(setupStep==1);setupPlayersPage.SetActive(setupStep==2);
            for(int i=0;i<setupSteps.Length;i++)
            {
                setupSteps[i].interactable=i<=setupMaxStep;
                float left=.05f+i*.305f;Layout((RectTransform)setupSteps[i].transform,new Vector2(left,setupStep==2?.745f:.78f),new Vector2(left+.29f,setupStep==2?.815f:.85f));
                setupSteps[i].GetComponent<Image>().color=i==setupStep?MenuGold:MenuCard;
                setupSteps[i].GetComponentInChildren<Text>().color=i==setupStep?MenuInk:Color.white;
            }
            setupPrevious.GetComponentInChildren<Text>().text=setupStep==0?"‹ МЕНЮ":"‹ НАЗАД";
            setupNext.gameObject.SetActive(setupStep<2);
            start.gameObject.SetActive(phase==Phase.Setup && setupStep==2);
            start.interactable=input.Ready && IdentitiesReady() && ValidSetup();
            start.GetComponentInChildren<Text>().text="НАЧАТЬ МАТЧ";
            start.GetComponentInChildren<Text>().fontSize=26;
            start.GetComponentInChildren<Text>().color=MenuInk;
            start.GetComponent<Image>().color=MenuGold;
            seatPanel.SetActive(false);botPanel.SetActive(false);
            seatsMinus.gameObject.SetActive(false);seatsPlus.gameObject.SetActive(false);rebind.gameObject.SetActive(false);setupBack.gameObject.SetActive(false);diagnosticButton.gameObject.SetActive(false);teamRow.SetActive(false);
            for(int i=0;i<seatIdentityButtons.Length;i++)seatIdentityButtons[i].gameObject.SetActive(
                false);
            setupSummary.text="КАРТА\n"+AuthoredArenaCatalog.Name(SelectedMapId)+"\n\nПРАВИЛА\n"+
                (SetupMode==NativeMatchMode.Teams?"Команды":"Каждый сам за себя")+" · "+Configuration.DurationMinutes+" мин"+
                (Configuration.TargetEnabled?"\nЦель: "+Configuration.TargetPoints:"\nБез цели по очкам")+"\n\nСОСТАВ\n"+
                botSetup.HumanCount(LocalSeatCount)+" человек · "+(LocalSeatCount-botSetup.HumanCount(LocalSeatCount)+botSetup.Count)+" AI";
            bool roster=setupStep==2;
            var content=(RectTransform)setupPlayersPage.transform.parent;
            Layout(content,new Vector2(.05f,roster?.185f:.12f),new Vector2(roster?.95f:.69f,roster?.73f:.76f));
            var summary=(RectTransform)setupSummary.transform.parent;
            Layout(summary,new Vector2(roster?.05f:.71f,roster?.05f:.12f),new Vector2(.95f,roster?.165f:.76f));
            summary.Find("summary-title").gameObject.SetActive(!roster);
            setupLabIdentity.gameObject.SetActive(!roster);
            Layout(setupSummary.rectTransform,roster?new Vector2(.02f,.38f):new Vector2(.07f,.53f),roster?new Vector2(.70f,.94f):new Vector2(.93f,.83f));
            setupSummary.fontSize=roster?24:20;
            if(roster)setupSummary.text=AuthoredArenaCatalog.Name(SelectedMapId)+"\n"+(SetupMode==NativeMatchMode.Teams?"Две команды":"Каждый за себя")+" · "+Configuration.DurationMinutes+" мин · "+(Configuration.TargetEnabled?"Цель: "+Configuration.TargetPoints:"Без цели по очкам");
            Layout(setupMessage.rectTransform,roster?new Vector2(.17f,.015f):new Vector2(.07f,.22f),roster?new Vector2(.70f,.36f):new Vector2(.93f,.34f));
            Layout((RectTransform)start.transform,roster?new Vector2(.74f,.16f):new Vector2(.07f,.02f),roster?new Vector2(.98f,.84f):new Vector2(.93f,.10f));
            Layout((RectTransform)setupPrevious.transform,roster?new Vector2(.02f,.015f):new Vector2(.07f,.12f),roster?new Vector2(.15f,.33f):new Vector2(.93f,.20f));
            var brand=setupScreen.transform.Find("setup-brand").GetComponent<Text>();brand.text=roster?"STAR TOURNAMENT":"STAR TOURNAMENT  /  БИТВА";
            brand.fontSize=roster?25:35;brand.fontStyle=roster?FontStyle.Bold:FontStyle.Normal;
            Layout(brand.rectTransform,roster?new Vector2(.05f,.915f):new Vector2(.05f,.91f),roster?new Vector2(.95f,.965f):new Vector2(.95f,.98f));
            rosterPageTitle.gameObject.SetActive(roster);
            var description=setupScreen.transform.Find("setup-description").GetComponent<Text>();
            Layout(description.rectTransform,roster?new Vector2(.05f,.83f):new Vector2(.05f,.86f),roster?new Vector2(.95f,.87f):new Vector2(.95f,.92f));
            description.color=roster?RosterMuted:Color.white;
            start.GetComponentInChildren<Text>().fontStyle=FontStyle.Bold;
            setupScreen.transform.Find("setup-description").GetComponent<Text>().text=roster?"Выберите карточку, чтобы изменить участника. Свободный геймпад: Y — присоединиться.":"Выберите карту, задайте правила, соберите игроков и начните матч.";
            setupLabIdentity.text=LabSavedIdentity;
            RefreshRosterUi();
            var mapCard=setupMapPage.transform.Find("selected-map");
            mapCard.Find("map-name").GetComponent<Text>().text=AuthoredArenaCatalog.Name(SelectedMapId).ToUpperInvariant();
            mapCard.Find("map-revision").GetComponent<Text>().text=(SelectedMapId==LunarLaboratoryCatalog.Id?"Лунная база · два этажа и двор":SelectedMapId==IndustrialTunnelsCatalog.Id?"Промышленный ярус · кольцо и центр":"Орбитальная арена")+"\n2–"+AuthoredArenaCatalog.Maximum(SelectedMapId)+" участников";
            setupMessage.text=setupError??(setupStep==2?SetupBlockingReason():"Значения сохраняются при переходе между шагами");
        }
        InputField CreateNameInput(Transform parent)
        {
            var shell=Panel(parent,"profile-name-input",new Vector2(.08f,.67f),new Vector2(.92f,.76f),new Color32(38,61,72,255));
            var input=shell.AddComponent<InputField>();
            var value=Label(shell.transform,"name-value","",26,new Vector2(.04f,0),new Vector2(.96f,1),TextAnchor.MiddleLeft,Color.white);
            input.textComponent=value;input.characterLimit=24;input.contentType=InputField.ContentType.Standard;
            return input;
        }
        void RememberMenuDevice(InputAction.CallbackContext context)
        {
            if(phase!=Phase.MainMenu && phase!=Phase.Profiles && phase!=Phase.Settings && phase!=Phase.Lab)return;
            var device=context.control?.device;
            if(device is Mouse)device=Keyboard.current;
            if(device is Keyboard || device is Gamepad)menuDevice=device;
        }
        void OpenSetup()
        {
            ApplySavedLabRevision();Configuration=NativeMatchConfiguration.Default(MatchProfile);fps.RestoreGeneralPreference();
            phase=Phase.Setup;setupStep=setupMaxStep=0;setupError=null;pendingSeat=-1;rosterEditing=-1;
            input.Reset();input.SetActiveSeatCount(1);
            for(int seat=0;seat<SeatInputCoordinator.SeatCount;seat++){identities.ClearSeat(seat);botSetup.SetAi(seat,false);botSetup.SetSeatDifficulty(seat,(int)NativeBotDifficulty.Normal);teamAssignments[seat]=seat%2==0?NativeTeam.TeamA:NativeTeam.TeamB;}
            while(botSetup.Count>0)botSetup.Remove(botSetup.Count-1);
            botSetup.Add(1);SyncHumanSeats();
            var device=menuDevice;
            if(device==null || !device.added || !device.enabled)device=(InputDevice)Keyboard.current??Gamepad.all.FirstOrDefault(p=>p.added&&p.enabled);
            if(input.Assign(0,device) && !identities.TryRestore(0,device.deviceId,playerProfiles))
                identities.ChooseGuest(0,device.deviceId,MouseSensitivityPreference.Resolve(Profile),fps.Visible);
            ApplyLayout(1);RefreshInterface();Select(setupNext);
        }
        void ToMainMenu()
        {
            rosterEditing=-1;
            if(phase==Phase.Lab && LabDirty){ConfirmLabDiscard(ToMainMenu);return;}
            ClearSeatPauseMenus();
            if(phase==Phase.Running||phase==Phase.Paused||phase==Phase.Results)Menu();
            pendingSeat=-1;fps.RestoreGeneralPreference();phase=Phase.MainMenu;SetCursor(false);RefreshInterface();Select(mainBattleButton);
        }
        void OpenProfiles() { phase=Phase.Profiles;RefreshProfilesUi();RefreshInterface();Select(profilesBackButton); }
        void OpenLab() { OpenDesignLab(); }
        void OpenSettings(int seat)
        {
            settingsReturn=phase;settingsSeat=seat;
            phase=Phase.Settings;RefreshInterface();settingsView.Open(seat);
        }
        void CloseSettings()
        {
            if(displayConfirmationActive&&displayOwner==settingsView)RollbackDisplay();
            int seat=settingsSeat;phase=settingsReturn;settingsSeat=-1;RefreshInterface();
            if(phase==Phase.Profiles){RefreshProfileDetail();Select(profilesBackButton);}
            if(phase==Phase.MainMenu)Select(mainMenuScreen.transform.Find("actions/main-action-4").GetComponent<Button>());
            else if(phase==Phase.Setup&&seat>=0&&seat<seatIdentityButtons.Length)FocusRosterCard(seat);
            else if(phase==Phase.Paused)Select(operatorSettingsButton);
        }

        void OnDeviceJoined(int seat,InputDevice device)
        {
            setupError=null;
            if(identities.HasIdentity(seat)){identities.RememberBinding(seat,device.deviceId);RefreshInterface();return;}
            if(!identities.TryRestore(seat,device.deviceId,playerProfiles))OpenIdentityPicker(seat);
            else RefreshInterface();
        }
        void OpenIdentityPicker(int seat)
        {
            if(phase!=Phase.Setup || !input.IsHumanSeat(seat))return;
            if(setupStep==2){OpenRosterEditor(seat);return;}
            pendingSeat=seat;RefreshIdentityOptions();RefreshInterface();
        }
        void RefreshIdentityOptions()
        {
            if(identityList==null||pendingSeat<0)return;
            foreach(Transform child in identityList){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            identityHeading.text="БИТВА  /  ИГРОК "+(pendingSeat+1)+"  /  "+
                (input.IsConnected(pendingSeat)?input.Label(pendingSeat):"УСТРОЙСТВО ОТКЛЮЧЕНО");
            identitySettingsButton.interactable=identities.HasIdentity(pendingSeat);
            var profiles=playerProfiles.Profiles.ToArray();
            int rows=profiles.Length+2;
            for(int i=0;i<profiles.Length;i++)
            {
                var record=profiles[i];bool occupied=identities.InUse(record.Id,pendingSeat);
                float top=1f-i/(float)rows;
                MenuButton(identityList,"identity-profile-"+record.Id,record.Name+(occupied?" · Занят другим игроком":" · Свободен"),
                    new Vector2(0,top-1f/rows+.015f),new Vector2(1,top-.015f),()=>ChooseIdentity(record.Id),!occupied);
            }
            float createTop=1f-profiles.Length/(float)rows;
            MenuButton(identityList,"identity-create","+ Создать профиль",new Vector2(0,createTop-1f/rows+.015f),new Vector2(1,createTop-.015f),CreateAndChooseProfile);
            float guestTop=createTop-1f/rows;
            MenuButton(identityList,"identity-guest","Без профиля · гость до выхода",new Vector2(0,guestTop-1f/rows+.015f),new Vector2(1,guestTop-.015f),ChooseGuest);
        }
        void ChooseIdentity(string id)
        {
            var device=input.DeviceAt(pendingSeat);
            if(!input.IsConnected(pendingSeat)||device==null||!identities.ChooseProfile(pendingSeat,device.deviceId,id,playerProfiles))
            {setupError="Профиль занят или устройство отключено";RefreshIdentityOptions();return;}
            pendingSeat=-1;RefreshInterface();Select(setupStep==2?start:setupNext);
        }
        void CreateAndChooseProfile()
        {
            var profile=CreateProfileWithGeneralSettings("Игрок "+(playerProfiles.Profiles.Count+1));
            ChooseIdentity(profile.Id);
        }
        void ChooseGuest()
        {
            var device=input.DeviceAt(pendingSeat);
            if(!input.IsConnected(pendingSeat)||device==null){setupError="Устройство отключено";RefreshInterface();return;}
            identities.ChooseGuest(pendingSeat,device.deviceId,MouseSensitivityPreference.Resolve(Profile),fps.Visible);
            pendingSeat=-1;RefreshInterface();Select(setupStep==2?start:setupNext);
        }
        bool IdentitiesReady()
        {
            if(reviewComposition!=null||combatReview)return true;
            for(int i=0;i<LocalSeatCount;i++)if(input.IsHumanSeat(i)&&!identities.HasIdentity(i))return false;
            return true;
        }
        string IdentityLabel(int seat)
        {
            var id=identities.ProfileId(seat);
            if(id!=null)return playerProfiles.Find(id)?.Name??"Профиль удалён";
            return identities.GuestAt(seat)!=null?"Гость":"выберите профиль";
        }
        NativeMatchComposition WithSelectedNames(NativeMatchComposition original)
        {
            var snapshot=original.Read();
            for(int seat=0;seat<original.LocalCount;seat++)
            {
                int participant=original.ParticipantAt(seat);
                if(snapshot.Participants[participant].Kind!=NativeParticipantKind.LocalHuman)continue;
                var info=snapshot.Participants[participant];info.Name=IdentityLabel(seat);snapshot.Participants[participant]=info;
            }
            return NativeMatchComposition.Restore(snapshot);
        }
        void RefreshProfilesUi()
        {
            if(profileList==null)return;
            foreach(Transform child in profileList){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            var profiles=playerProfiles.Profiles.ToArray();
            for(int i=0;i<profiles.Length;i++)
            {
                var record=profiles[i];float top=1f-i/Mathf.Max(4f,profiles.Length);
                MenuButton(profileList,"profile-"+record.Id,record.Name,new Vector2(0,top-1f/Mathf.Max(4f,profiles.Length)+.012f),new Vector2(1,top-.012f),()=>SelectProfile(record.Id));
            }
            if(selectedProfileId==null||playerProfiles.Find(selectedProfileId)==null)selectedProfileId=profiles.FirstOrDefault()?.Id;
            RefreshProfileDetail();
        }
        void SelectProfile(string id) { selectedProfileId=id;RefreshProfileDetail(); }
        void RefreshProfileDetail()
        {
            var selected=playerProfiles.Find(selectedProfileId);
            profileDetail.text=selected==null?"Создайте профиль":selected.Name+"\nМышь: "+selected.MouseDegreesPerPixel.ToString("0.00")+"°/px\nFPS: "+(selected.ShowFps?"вкл":"выкл");
            profileNameInput.text=selected?.Name??"";
        }
        void CreateNamedProfile()
        {
            var name=(profileNameInput.text??"").Trim();
            if(name.Length==0||playerProfiles.Find(selectedProfileId)!=null&&name==playerProfiles.Find(selectedProfileId).Name)name="Игрок "+(playerProfiles.Profiles.Count+1);
            var selected=CreateProfileWithGeneralSettings(name);
            selectedProfileId=selected.Id;RefreshProfilesUi();
        }
        void RenameSelectedProfile()
        {
            if(selectedProfileId==null)return;
            try {playerProfiles.Rename(selectedProfileId,profileNameInput.text);RefreshProfilesUi();}
            catch(ArgumentException){profileDetail.text="Имя должно содержать 1–24 символа";}
        }
        void DeleteSelectedProfile()
        {
            if(selectedProfileId==null)return;
            playerProfiles.Delete(selectedProfileId);identities.ForgetProfile(selectedProfileId);selectedProfileId=null;
            RefreshProfilesUi();RefreshInterface();
        }
        void StepSelectedProfile(int direction)
        {
            var selected=playerProfiles.Find(selectedProfileId);if(selected==null)return;
            var descriptor=MouseSensitivityPreference.Descriptor(Profile);
            float value=Mathf.Clamp(selected.MouseDegreesPerPixel+direction*descriptor.Step,descriptor.Minimum,descriptor.Maximum);
            playerProfiles.SetPersonal(selected.Id,value,selected.ShowFps);RefreshProfileDetail();
        }
        void ToggleSelectedProfileFps()
        {
            var selected=playerProfiles.Find(selectedProfileId);if(selected==null)return;
            playerProfiles.SetPersonal(selected.Id,selected.MouseDegreesPerPixel,!selected.ShowFps);RefreshProfileDetail();
        }
        void StepMenuSensitivity(int direction)
        {
            if(settingsSeat<0)MouseSensitivityPreference.Step(Profile,direction);
            else StepPersonalSensitivity(settingsSeat,direction);
            RefreshInterface();
        }
        void ToggleMenuFps()
        {
            if(settingsSeat<0)fps.Toggle();
            else TogglePersonalFps(settingsSeat);
            RefreshInterface();
        }
        void StepPersonalSensitivity(int seat,int direction)
        {
            var id=identities.ProfileId(seat);
            if(id!=null) {selectedProfileId=id;StepSelectedProfile(direction);}
            else
            {
                var guest=identities.GuestAt(seat);
                if(guest!=null){var d=MouseSensitivityPreference.Descriptor(Profile);guest.MouseDegreesPerPixel=Mathf.Clamp(guest.MouseDegreesPerPixel+direction*d.Step,d.Minimum,d.Maximum);}
            }
            RefreshSeatPauseUi();
        }
        void TogglePersonalFps(int seat)
        {
            var id=identities.ProfileId(seat);
            if(id!=null){selectedProfileId=id;ToggleSelectedProfileFps();}
            else {var guest=identities.GuestAt(seat);if(guest!=null)guest.ShowFps=!guest.ShowFps;}
            RefreshSeatPauseUi();
        }
        float ActiveMouseSensitivity()
        {
            for(int i=0;i<LocalSeatCount;i++)if(input.DeviceAt(i) is Keyboard)
            {
                var id=identities.ProfileId(i);if(id!=null)return playerProfiles.Find(id)?.MouseDegreesPerPixel??MouseSensitivityPreference.Resolve(Profile);
                var guest=identities.GuestAt(i);if(guest!=null)return guest.MouseDegreesPerPixel;
            }
            return MouseSensitivityPreference.Resolve(Profile);
        }
        void ApplyMatchFpsPreference()
        {
            if(UseSeatPauseMenus){fps.SetSessionVisibility(false);RefreshSeatPauseUi();return;}
            int operatorSeat=-1;
            for(int i=0;i<LocalSeatCount;i++)if(input.IsHumanSeat(i) && input.DeviceAt(i) is Keyboard){operatorSeat=i;break;}
            if(operatorSeat<0)for(int i=0;i<LocalSeatCount;i++)if(input.IsHumanSeat(i)){operatorSeat=i;break;}
            fps.SetSessionVisibility(operatorSeat<0?PlayerPrefs.GetInt(FpsDisplay.PreferenceKey,1)!=0:PersonalFps(operatorSeat));
        }
        void RefreshModernMenuUi()
        {
            if(mainMenuScreen==null)return;
            bool shell=phase==Phase.MainMenu||phase==Phase.Profiles||phase==Phase.Settings||phase==Phase.Lab;
            oldMenuColumn.SetActive(!shell && phase!=Phase.Setup && pendingSeat<0);
            mainMenuScreen.SetActive(phase==Phase.MainMenu);
            profilesScreen.SetActive(phase==Phase.Profiles);
            settingsScreen.SetActive(phase==Phase.Settings);
            labScreen.SetActive(phase==Phase.Lab);
            identityScreen.SetActive(phase==Phase.Setup && pendingSeat>=0);
            for(int seat=0;seat<seatIdentityButtons.Length;seat++)
            {
                seatIdentityButtons[seat].gameObject.SetActive(phase==Phase.Setup && pendingSeat<0 && seat<LocalSeatCount && input.IsHumanSeat(seat));
                seatIdentityButtons[seat].GetComponentInChildren<Text>().text="Игрок "+(seat+1)+" · "+IdentityLabel(seat)+" · "+
                    (input.DeviceAt(seat)==null?"Ожидает устройство":input.IsConnected(seat)?input.Label(seat):"Устройство отключено — назначьте заново");
            }
            RefreshSetupUi();
            if(phase==Phase.Settings)RefreshSettingsUi();
        }
        float PersonalSensitivity(int seat)
        {
            var id=identities.ProfileId(seat);
            return id!=null?playerProfiles.Find(id)?.MouseDegreesPerPixel??MouseSensitivityPreference.Resolve(Profile):
                identities.GuestAt(seat)?.MouseDegreesPerPixel??MouseSensitivityPreference.Resolve(Profile);
        }
        bool PersonalFps(int seat)
        {
            var id=identities.ProfileId(seat);
            return id!=null?playerProfiles.Find(id)?.ShowFps??fps.Visible:identities.GuestAt(seat)?.ShowFps??fps.Visible;
        }
    }
}
