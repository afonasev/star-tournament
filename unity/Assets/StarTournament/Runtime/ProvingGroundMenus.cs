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
        static readonly Color32 MenuInk = new Color32(16,28,41,255);
        static readonly Color32 MenuCard = new Color32(27,43,59,255);
        static readonly Color32 MenuGold = new Color32(231,187,114,255);
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
            bool outer=name=="main-menu"||name=="setup-screen"||name=="profiles-screen"||name=="lab-screen"||name=="settings-screen"&&parent.name!="pause-card";
            if(outer)AddMenuBackdrop(panel,name=="main-menu");
            else if(color.a>0 && min!=Vector2.zero && max!=Vector2.one)RoundRosterPanel(panel);
            return panel;
        }
        void AddMenuBackdrop(GameObject panel,bool main)
        {
            var texture=Resources.Load<Texture2D>("UI/menu-arena");
            var art=new GameObject("menu-arena-background",typeof(RectTransform),typeof(RawImage),typeof(AspectRatioFitter));
            art.transform.SetParent(panel.transform,false);Layout((RectTransform)art.transform,Vector2.zero,Vector2.one);
            var image=art.GetComponent<RawImage>();image.texture=texture;image.raycastTarget=false;
            var fit=art.GetComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio=texture?(float)texture.width/texture.height:16f/9f;
            var shade=new GameObject("menu-background-shade",typeof(RectTransform),typeof(Image));shade.transform.SetParent(panel.transform,false);
            Layout((RectTransform)shade.transform,Vector2.zero,Vector2.one);shade.GetComponent<Image>().color=new Color32(10,20,32,(byte)(main?20:190));shade.GetComponent<Image>().raycastTarget=false;
        }
        Text Label(Transform parent,string name,string value,int size,Vector2 min,Vector2 max,TextAnchor align,Color color)
        {
            var text=TextElement(parent,name,value,size);Layout(text.rectTransform,min,max);text.alignment=align;text.color=color;return text;
        }
        Button MenuButton(Transform parent,string name,string value,Vector2 min,Vector2 max,UnityEngine.Events.UnityAction action,bool enabled=true)
        {
            var button=ButtonElement(parent,value,action);button.name=name;Layout((RectTransform)button.transform,min,max);
            button.interactable=enabled;
            return button;
        }
        void CreateModernMenuUi(Transform parent)
        {
            oldMenuColumn=parent.Find("menu").gameObject;
            mainMenuScreen=Panel(parent,"main-menu",Vector2.zero,Vector2.one,MenuInk);
            var left=Panel(mainMenuScreen.transform,"actions",new Vector2(.05f,.13f),new Vector2(.325f,.80f),Color.clear);
            Label(left.transform,"brand","<color=#EFF5F5>STAR</color>\nTOURNAMENT",52,new Vector2(0,.76f),new Vector2(1,.99f),TextAnchor.MiddleLeft,MenuGold).fontStyle=FontStyle.Bold;
            string[] labels={"Битва","Сетевая игра · скоро","Лаборатория геймдизайна","Профили игроков","Настройки игры","Выход"};
            UnityEngine.Events.UnityAction[] actions={OpenSetup,()=>{},OpenLab,OpenProfiles,()=>OpenSettings(-1),()=>Application.Quit()};
            for(int i=0;i<labels.Length;i++)
            {
                float top=.69f-i*.112f;
                var button=MenuButton(left.transform,"main-action-"+i,labels[i],new Vector2(0,top-.088f),new Vector2(1,top),actions[i],i!=1);
                button.GetComponent<MenuPresentation>().Height=60;button.SendMessage("OnRectTransformDimensionsChange");
                var label=button.GetComponentInChildren<Text>();label.alignment=TextAnchor.MiddleLeft;
                label.rectTransform.offsetMin=new Vector2(24,0);label.rectTransform.offsetMax=new Vector2(-20,0);
                if(i==0){mainBattleButton=button;button.GetComponent<Image>().color=MenuGold;label.color=MenuInk;label.fontStyle=FontStyle.Bold;}
                if(i==5)button.GetComponent<Image>().color=new Color32(16,28,41,140);
            }
            Label(mainMenuScreen.transform,"controls","↑ ↓  Выбрать     A  Подтвердить     Мышь: выбор",18,new Vector2(.05f,.025f),new Vector2(.53f,.075f),TextAnchor.MiddleLeft,new Color32(153,173,187,255));
            var releaseLabel=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_RELEASE_LABEL");
            if(!string.IsNullOrEmpty(releaseLabel))
            {
                Label(mainMenuScreen.transform,"installed-release",releaseLabel,18,new Vector2(.60f,.02f),new Vector2(.97f,.06f),TextAnchor.MiddleRight,new Color32(153,173,187,255));
                var updateStatus=Label(mainMenuScreen.transform,"update-status","",16,new Vector2(.60f,.135f),new Vector2(.97f,.17f),TextAnchor.MiddleRight,new Color32(153,173,187,255));
                var updateButton=MenuButton(mainMenuScreen.transform,"update-action","Обновить",new Vector2(.80f,.072f),new Vector2(.97f,.13f),()=>{});
                gameObject.AddComponent<NativeUpdateBridge>().Bind(updateStatus,updateButton);
            }

            CreateSetupUi(parent);

            profilesScreen=Panel(parent,"profiles-screen",Vector2.zero,Vector2.one,MenuInk);
            var listCard=Panel(profilesScreen.transform,"profile-list-card",new Vector2(.06f,.12f),new Vector2(.57f,.89f),new Color32(20,33,48,245));
            var detailCard=Panel(profilesScreen.transform,"profile-detail-card",new Vector2(.59f,.12f),new Vector2(.94f,.89f),MenuCard);
            Label(listCard.transform,"profiles-title","ПРОФИЛИ ИГРОКОВ",35,new Vector2(.06f,.86f),new Vector2(.94f,.98f),TextAnchor.MiddleLeft,Color.white);
            var list=Panel(listCard.transform,"profile-list",new Vector2(.06f,.20f),new Vector2(.94f,.84f),new Color32(20,33,48,245));profileList=list.transform;
            MenuButton(listCard.transform,"profile-create","+ СОЗДАТЬ ПРОФИЛЬ",new Vector2(.06f,.08f),new Vector2(.55f,.17f),CreateNamedProfile);
            profilesBackButton=MenuButton(profilesScreen.transform,"profiles-back","‹ Назад",new Vector2(.05f,.91f),new Vector2(.15f,.96f),ToMainMenu);
            profileDetail=Label(detailCard.transform,"profile-detail","Выберите профиль",27,new Vector2(.08f,.77f),new Vector2(.95f,.97f),TextAnchor.MiddleLeft,MenuGold);
            profileNameInput=CreateNameInput(detailCard.transform);
            MenuButton(detailCard.transform,"profile-rename","Переименовать",new Vector2(.08f,.57f),new Vector2(.92f,.64f),RenameSelectedProfile);
            MenuButton(detailCard.transform,"profile-settings","Личные настройки",new Vector2(.08f,.46f),new Vector2(.92f,.53f),OpenProfileSettings);
            MenuButton(detailCard.transform,"profile-delete","Удалить профиль",new Vector2(.08f,.35f),new Vector2(.92f,.42f),DeleteSelectedProfile);

            CreateSettingsUi(parent);

            CreateDesignLabUi(parent);

            identityScreen=Panel(parent,"identity-picker",Vector2.zero,Vector2.one,new Color32(8,17,25,248));
            var identityCard=Panel(identityScreen.transform,"identity-card",new Vector2(.17f,.12f),new Vector2(.83f,.88f),new Color32(20,33,48,245));
            identityHeading=Label(identityCard.transform,"identity-heading","КТО ИГРАЕТ?",35,new Vector2(.06f,.85f),new Vector2(.94f,.97f),TextAnchor.MiddleLeft,MenuGold);
            var options=Panel(identityCard.transform,"identity-options",new Vector2(.06f,.19f),new Vector2(.94f,.82f),new Color32(20,33,48,245));identityList=options.transform;
            MenuButton(identityScreen.transform,"identity-cancel","‹ Назад",new Vector2(.05f,.91f),new Vector2(.15f,.96f),()=>{pendingSeat=-1;RefreshInterface();});
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
            var steps=new[]{"КАРТА","ПРАВИЛА","ИГРОКИ"};
            for(int i=0;i<3;i++)
            {
                int selected=i;float left=.05f+i*.305f;
                setupSteps[i]=MenuButton(setupScreen.transform,"setup-step-"+i,steps[i],new Vector2(left,.78f),new Vector2(left+.29f,.85f),()=>ShowSetupStep(selected));
            }
            var content=Panel(setupScreen.transform,"setup-content",new Vector2(.05f,.12f),new Vector2(.69f,.76f),new Color32(20,33,48,245));
            var summary=Panel(setupScreen.transform,"setup-summary-card",new Vector2(.71f,.12f),new Vector2(.95f,.76f),MenuCard);
            Label(summary.transform,"summary-title","ПЕРЕД СТАРТОМ",26,new Vector2(.07f,.85f),new Vector2(.93f,.96f),TextAnchor.MiddleLeft,MenuGold);
            setupSummary=Label(summary.transform,"setup-summary","",20,new Vector2(.07f,.53f),new Vector2(.93f,.83f),TextAnchor.UpperLeft,Color.white);
            setupLabIdentity=Label(summary.transform,"setup-lab-identity","",16,new Vector2(.07f,.34f),new Vector2(.93f,.52f),TextAnchor.UpperLeft,Color.white);setupLabIdentity.supportRichText=false;
            setupMessage=Label(summary.transform,"setup-message","",18,new Vector2(.07f,.22f),new Vector2(.93f,.34f),TextAnchor.UpperLeft,new Color32(255,213,157,255));
            setupPrevious=MenuButton(setupScreen.transform,"setup-previous","‹ Назад",new Vector2(.05f,.91f),new Vector2(.15f,.96f),PreviousSetupStep);
            setupNext=MenuButton(summary.transform,"setup-next","ДАЛЕЕ ›",new Vector2(.07f,.02f),new Vector2(.93f,.10f),NextSetupStep);
            setupMapPage=Panel(content.transform,"setup-map",Vector2.zero,Vector2.one,new Color32(20,33,48,245));
            Label(setupMapPage.transform,"map-title","ВЫБЕРИТЕ КАРТУ",30,new Vector2(.05f,.86f),new Vector2(.95f,.98f),TextAnchor.MiddleLeft,Color.white);
            Label(setupMapPage.transform,"map-help","Выберите арену для матча.",20,new Vector2(.05f,.77f),new Vector2(.95f,.86f),TextAnchor.MiddleLeft,new Color32(153,173,187,255));
            var mapCard=Panel(setupMapPage.transform,"selected-map",new Vector2(.05f,.36f),new Vector2(.95f,.73f),MenuCard);
            Label(mapCard.transform,"map-name","COMBAT BOWL",25,new Vector2(.50f,.59f),new Vector2(.95f,.90f),TextAnchor.MiddleLeft,MenuGold);
            Label(mapCard.transform,"map-revision","Орбитальная арена · "+CombatBowlCatalog.Identity+"\nФиксированная карта · 2–8 участников",21,new Vector2(.50f,.14f),new Vector2(.95f,.58f),TextAnchor.MiddleLeft,Color.white);
            var preview=new GameObject("map-preview",typeof(RectTransform),typeof(ArenaPreviewGraphic));preview.transform.SetParent(mapCard.transform,false);Layout((RectTransform)preview.transform,new Vector2(.03f,.06f),new Vector2(.46f,.94f));
            MoveSetupControl(arenaButton,setupMapPage,new Vector2(.05f,.19f),new Vector2(.62f,.27f));


            setupRulesPage=Panel(content.transform,"setup-rules",Vector2.zero,Vector2.one,new Color32(20,33,48,245));
            Label(setupRulesPage.transform,"rules-title","ПРАВИЛА МАТЧА",30,new Vector2(.05f,.87f),new Vector2(.95f,.98f),TextAnchor.MiddleLeft,Color.white);
            MoveSetupControl(modeButton,setupRulesPage,new Vector2(.05f,.70f),new Vector2(.62f,.80f));
            Label(setupRulesPage.transform,"mode-help","Каждый за себя или две команды с отдельным счётом.",19,new Vector2(.05f,.61f),new Vector2(.95f,.67f),TextAnchor.MiddleLeft,new Color32(153,173,187,255));
            MoveSetupControl(durationButton,setupRulesPage,new Vector2(.05f,.48f),new Vector2(.48f,.58f));
            var durationMinus=oldMenuColumn.transform.Find("duration-minus").GetComponent<Button>();
            MoveSetupControl(durationMinus,setupRulesPage,new Vector2(.50f,.48f),new Vector2(.62f,.58f));
            Label(setupRulesPage.transform,"duration-help","Матч закончится по времени или раньше по цели.",19,new Vector2(.05f,.40f),new Vector2(.95f,.46f),TextAnchor.MiddleLeft,new Color32(153,173,187,255));
            MoveSetupControl(targetButton,setupRulesPage,new Vector2(.05f,.28f),new Vector2(.48f,.38f));
            MoveSetupControl(targetMinus,setupRulesPage,new Vector2(.50f,.28f),new Vector2(.60f,.38f));
            MoveSetupControl(targetPlus,setupRulesPage,new Vector2(.62f,.28f),new Vector2(.72f,.38f));
            Label(setupRulesPage.transform,"target-help","Необязательный порог по очкам.",19,new Vector2(.05f,.20f),new Vector2(.95f,.26f),TextAnchor.MiddleLeft,new Color32(153,173,187,255));
            teamRow.transform.SetParent(setupRulesPage.transform,false);
            Layout((RectTransform)teamRow.transform,new Vector2(.05f,.09f),new Vector2(.95f,.20f));

            setupPlayersPage=Panel(content.transform,"setup-players",Vector2.zero,Vector2.one,new Color32(20,33,48,245));
            MoveSetupControl(start,summary,new Vector2(.07f,.02f),new Vector2(.93f,.10f));
            CreateRosterUi(parent);
            CreateF2Setup();

        }
        void ShowSetupStep(int step)
        {
            if(phase!=Phase.Setup || step<0 || step>setupMaxStep || step>2)return;
            rosterEditing=-1;setupStep=step;RefreshInterface();FocusSetupStep();
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
            start.GetComponentInChildren<Text>().text="НАЧАТЬ МАТЧ  ·  Start";
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
            Layout((RectTransform)setupPrevious.transform,new Vector2(.05f,.91f),new Vector2(.15f,.96f));
            var brand=setupScreen.transform.Find("setup-brand").GetComponent<Text>();brand.text="ПОДГОТОВКА МАТЧА";
            brand.fontSize=roster?25:35;brand.fontStyle=roster?FontStyle.Bold:FontStyle.Normal;
            Layout(brand.rectTransform,new Vector2(.18f,.91f),roster?new Vector2(.95f,.965f):new Vector2(.95f,.98f));
            rosterPageTitle.gameObject.SetActive(roster);
            var description=setupScreen.transform.Find("setup-description").GetComponent<Text>();
            Layout(description.rectTransform,new Vector2(.05f,.86f),new Vector2(.95f,.90f));
            description.color=roster?RosterMuted:Color.white;
            start.GetComponentInChildren<Text>().fontStyle=FontStyle.Bold;
            setupScreen.transform.Find("setup-description").GetComponent<Text>().text=roster?"Выберите карточку, чтобы изменить участника. Свободный геймпад: Y — присоединиться.":"Выберите карту, задайте правила, соберите игроков и начните матч.";
            setupLabIdentity.text=LabSavedIdentity;
            RefreshRosterUi();
            var mapCard=setupMapPage.transform.Find("selected-map");
            mapCard.GetComponentInChildren<ArenaPreviewGraphic>().Show(SelectedMapId);
            mapCard.Find("map-name").GetComponent<Text>().text=AuthoredArenaCatalog.Name(SelectedMapId).ToUpperInvariant();
            mapCard.Find("map-revision").GetComponent<Text>().text=(SelectedMapId==LunarLaboratoryCatalog.Id?"Лунная база · два этажа и двор":SelectedMapId==IndustrialTunnelsCatalog.Id?"Промышленный ярус · кольцо и центр":"Орбитальная арена")+"\n2–"+AuthoredArenaCatalog.Maximum(SelectedMapId)+" участников";
            setupMessage.text=setupError??(setupStep==2?SetupBlockingReason():"");
            RefreshF2Setup();
            ConfigureSetupNavigation();
        }
        InputField CreateNameInput(Transform parent)
        {
            var shell=Panel(parent,"profile-name-input",new Vector2(.08f,.67f),new Vector2(.92f,.76f),new Color32(35,51,68,255));
            var input=shell.AddComponent<InputField>();
            var value=Label(shell.transform,"name-value","",26,new Vector2(.04f,0),new Vector2(.96f,1),TextAnchor.MiddleLeft,Color.white);
            input.textComponent=value;input.characterLimit=24;input.contentType=InputField.ContentType.Standard;
            return input;
        }
        void RememberMenuDevice(InputAction.CallbackContext context)
        {
            if(phase==Phase.Setup){TrackSetupNavigationInput(context);return;}
            if(phase!=Phase.MainMenu && phase!=Phase.Profiles && phase!=Phase.Settings && phase!=Phase.Lab)return;
            // UI navigation and pointer clicks are PassThrough actions: releases also
            // perform. A release must not replace the device that submitted the menu.
            if(context.action==menuMoveAction && context.ReadValue<Vector2>()==Vector2.zero)return;
            if(context.action==menuClickAction && !context.ReadValueAsButton())return;
            var device=context.control?.device;
            if(device is Mouse)device=Keyboard.current;
            if(device is Keyboard || device is Gamepad)menuDevice=device;
        }
        void OpenSetup()
        {
            ApplySavedLabRevision();Configuration=NativeMatchConfiguration.Default(MatchProfile);fps.RestoreGeneralPreference();
            phase=Phase.Setup;SetupMode=NativeMatchMode.Ffa;ApplyModeTarget();lastBotDifficulty=(int)NativeBotDifficulty.Normal;setupStep=setupMaxStep=0;setupError=null;pendingSeat=-1;rosterEditing=-1;
            input.Reset();input.SetActiveSeatCount(1);
            for(int seat=0;seat<SeatInputCoordinator.SeatCount;seat++){identities.ClearSeat(seat);botSetup.SetAi(seat,false);botSetup.SetSeatDifficulty(seat,(int)NativeBotDifficulty.Normal);teamAssignments[seat]=seat%2==0?NativeTeam.TeamA:NativeTeam.TeamB;}
            while(botSetup.Count>0)botSetup.Remove(botSetup.Count-1);
            botSetup.Add(1);SyncHumanSeats();
            var device=menuDevice;
            if(device==null || !device.added || !device.enabled)device=(InputDevice)Keyboard.current??Gamepad.all.FirstOrDefault(p=>p.added&&p.enabled);
            if(input.Assign(0,device) && !identities.TryRestore(0,device.deviceId,playerProfiles))
                identities.ChooseGuest(0,device.deviceId,MouseSensitivityPreference.Resolve(Profile),fps.Visible);
            setupGamepadNavigation=device is Gamepad;RestoreLastPlayedMap();ApplyLayout(1);RefreshInterface();FocusSetupStep();
        }
        void ToMainMenu()
        {
            if(labHistory!=null&&labHistory.Busy)return;
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
            if(setupStep==2 && device is Gamepad){if(identities.HasIdentity(seat))identities.RememberBinding(seat,device.deviceId);else if(!identities.TryRestore(seat,device.deviceId,playerProfiles))identities.ChooseGuest(seat,device.deviceId,MouseSensitivityPreference.Resolve(Profile),fps.Visible);RefreshInterface();SetSetupNavigationMode(true);if(rosterEditing<0&&pendingSeat<0)Select(rosterCards[seat]);return;}
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
            pendingSeat=-1;RefreshInterface();FocusSetupStep();
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
            pendingSeat=-1;RefreshInterface();FocusSetupStep();
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
            profileDetail.text=selected==null?"Создайте профиль":selected.Name;
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
            ApplyMatchFpsPreference();
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
            if(UseSeatPauseMenus){fps.SetSessionVisibility(Enumerable.Range(0,LocalSeatCount).Any(i=>input.IsHumanSeat(i)&&PersonalFps(i)));RefreshSeatPauseUi();return;}
            int operatorSeat=-1;
            for(int i=0;i<LocalSeatCount;i++)if(input.IsHumanSeat(i) && input.DeviceAt(i) is Keyboard){operatorSeat=i;break;}
            if(operatorSeat<0)for(int i=0;i<LocalSeatCount;i++)if(input.IsHumanSeat(i)){operatorSeat=i;break;}
            fps.SetSessionVisibility(operatorSeat<0?PlayerPrefs.GetInt(FpsDisplay.PreferenceKey,1)!=0:PersonalFps(operatorSeat));
        }
        void RefreshModernMenuUi()
        {
            if(mainMenuScreen==null)return;
            if(EventSystem.current && phase!=Phase.Running && !(phase==Phase.Paused&&UseSeatPauseMenus))EventSystem.current.sendNavigationEvents=true;
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
