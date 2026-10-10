using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingGround : MonoBehaviour
    {
        public GameObject RobotPrefab, WorldWeaponPrefab, ViewWeaponPrefab, ArmorPickupPrefab, SpeedPickupPrefab, DamagePickupPrefab, HealPickupPrefab;
        public GameObject TrooperBodyPrefab, TrooperArmsPrefab, RiflePrefab, PulsePrefab, CutterPrefab, RocketProjectilePrefab;
        public AnimationClip[] TrooperClips;
        public ProvingProfile HitFeedbackProfile = ProvingProfile.CreateHitFeedbackDefault();
        public ProvingProfile BloodProfile = ProvingProfile.CreateBloodDefault();
        public ProvingProfile DeathProfile = ProvingProfile.CreateDeathDefault();
        public ProvingProfile TrooperProfile = ProvingProfile.CreateTrooperDefault();
        public ProvingProfile Profile = ProvingProfile.CreateDefault();
        readonly SeatInputCoordinator input = new SeatInputCoordinator();
        CharacterMotor[] motors = Array.Empty<CharacterMotor>();
        readonly Camera[] cameras = new Camera[SeatInputCoordinator.SeatCount];
        readonly Text[] hud = new Text[SeatInputCoordinator.SeatCount], names = new Text[SeatInputCoordinator.SeatCount];
        public HitFeedbackPresentation HitFeedbackForReview=>presentation?.HitFeedbackForReview;
        internal RocketPresentation RocketEffectsForReview=>presentation?.RocketEffectsForReview;
        public ProvingProfile RocketEffectsProfile = ProvingProfile.CreateRocketEffectsDefault();
        public ProvingProfile CutterProfile = ProvingProfile.CreateCutterDefault();
        public ProvingProfile LifeProfile = ProvingProfile.CreateCombatDefault();
        public ProvingProfile CombatProfile = ProvingProfile.CreateNativeCombatDefault();
        GameObject[] bodies = Array.Empty<GameObject>();
        readonly GameObject[] views = new GameObject[SeatInputCoordinator.SeatCount];
        Light studioLight;
        readonly Text[] health = new Text[SeatInputCoordinator.SeatCount], armor = new Text[SeatInputCoordinator.SeatCount], ammo = new Text[SeatInputCoordinator.SeatCount], crosshair = new Text[SeatInputCoordinator.SeatCount];
        readonly Text[] killNotice = new Text[SeatInputCoordinator.SeatCount];
        readonly string[] killNoticeText = new string[SeatInputCoordinator.SeatCount];
        readonly bool[] killNoticeAllied = new bool[SeatInputCoordinator.SeatCount];
        readonly double[] killNoticeUntil = new double[SeatInputCoordinator.SeatCount];
        readonly GameObject[] armorGroups = new GameObject[SeatInputCoordinator.SeatCount];
        readonly HudIndicatorIcon[] ammoIcons = new HudIndicatorIcon[SeatInputCoordinator.SeatCount];
        LocalAction[] actions = new LocalAction[SeatInputCoordinator.SeatCount];
        LocalAction[] localActions = new LocalAction[SeatInputCoordinator.SeatCount];
        public ProvingProfile BotPerceptionProfile = ProvingProfile.CreateBotPerceptionDefault();
        public ProvingProfile BotNavigationProfile = ProvingProfile.CreateNavigationDefault();
        public ProvingProfile BotBehaviorProfile = ProvingProfile.CreateBotBehaviorDefault();
        public NativeBotMatchDriver BotDriver {get;private set;}
        bool botReviewEnabled; uint botSeed; ProvingProfile frozenBehavior;
        public NativeNavigationDriver NavigationReviewDriver { get; private set; }
        bool navigationReviewEnabled;
        ProvingProfile frozenNavigation, frozenPerception;
        public ProvingProfile ParticipantPaletteProfile = ProvingProfile.CreateParticipantPaletteDefault();
        public ProvingProfile RosterProfile = ProvingProfile.CreateRosterDefault();
        ProvingProfile frozenRosterProfile;
        public NativeMatchComposition Composition { get; private set; }
        NativeMatchComposition reviewComposition;
        bool reviewShowStandings;
        public ProvingProfile TeamProfile = ProvingProfile.CreateTeamDefault();
        public ProvingProfile MatchProfile = ProvingProfile.CreateMatchDefault();
        public NativeMatchConfiguration Configuration { get; private set; }
        ProvingProfile frozenRocketEffects, frozenCutter, frozenMovement, frozenLife, frozenCombat, frozenMatch, frozenTrooper, frozenDeath, frozenBlood, frozenHitFeedback;
        ProvingProfile previewMovement,previewLife,previewCombat,previewTrooper;
        NativeMatchConfiguration frozenConfiguration;
        NativeMatchRoster frozenRoster;
        ProvingProfile frozenTeam;
        bool swappedTeamColors, frozenSwappedColors;
        public NativeMatchMode SetupMode { get; private set; }
        readonly NativeTeam[] teamAssignments={NativeTeam.TeamA,NativeTeam.TeamB,NativeTeam.TeamA,NativeTeam.TeamB};
        Button modeButton;
        GameObject teamRow;
        readonly Button[] teamButtons=new Button[SeatInputCoordinator.SeatCount];
        string setupError;
        readonly NativeBotSetup botSetup=new NativeBotSetup();
        GameObject botPanel;
        Button addBot;
        GameObject seatPanel;
        readonly Button[] seatKinds=new Button[SeatInputCoordinator.SeatCount], seatDifficulties=new Button[SeatInputCoordinator.SeatCount];
        readonly GameObject[] botRows=new GameObject[NativeMatchRoster.MaximumParticipants-1];
        readonly Button[] botDifficulty=new Button[NativeMatchRoster.MaximumParticipants-1], botTeams=new Button[NativeMatchRoster.MaximumParticipants-1];
        public int SetupBotCount=>botSetup.Count;
        public void SetSeatAi(int seat,bool ai)
        {
            if(phase!=Phase.Setup)return;
            if(seat<0||seat>=LocalSeatCount)throw new ArgumentOutOfRangeException(nameof(seat));
            botSetup.SetAi(seat,ai);input.SetHumanSeat(seat,!ai);if(ai)identities.ClearSeat(seat);setupError=null;RefreshInterface();
        }
        public void SetSeatDifficulty(int seat,int difficulty)
        {
            if(phase!=Phase.Setup)return;
            botSetup.SetSeatDifficulty(seat,difficulty);lastBotDifficulty=difficulty;RefreshInterface();
        }
        void SyncHumanSeats()
        {
            for(int seat=0;seat<LocalSeatCount;seat++)input.SetHumanSeat(seat,!botSetup.IsAi(seat));
        }
        public NativeMatchComposition SetupComposition()=>botSetup.Build(LocalSeatCount,SetupMode,teamAssignments,swappedTeamColors);
        public void AddBot(){if(phase!=Phase.Setup||!botSetup.CanAdd(LocalSeatCount))return;botSetup.Add(LocalSeatCount);botSetup.SetDifficulty(botSetup.Count-1,lastBotDifficulty);setupError=null;RefreshInterface();}
        public void RemoveBot(int index){if(phase!=Phase.Setup)return;botSetup.Remove(index);setupError=null;RefreshInterface();if(setupStep==2)FocusRosterCard(LocalSeatCount+Math.Min(index,botSetup.Count-1));else Select(setupNext);}
        public void SetBotDifficulty(int index,int value){if(phase!=Phase.Setup)return;botSetup.SetDifficulty(index,value);lastBotDifficulty=value;RefreshInterface();}
        public void SetBotTeam(int index,NativeTeam value){if(phase!=Phase.Setup)return;botSetup.SetTeam(index,value);setupError=null;RefreshInterface();}
        public void SetMatchMode(NativeMatchMode mode)
        {
            if(phase!=Phase.Setup) return;
            if(mode!=NativeMatchMode.Ffa && mode!=NativeMatchMode.Teams) throw new ArgumentException("Invalid mode");
            bool changed=SetupMode!=mode;SetupMode=mode;if(changed)ApplyModeTarget();setupError=null;RefreshInterface();
        }
        public void SetTeam(int seat,NativeTeam team)
        {
            if(phase!=Phase.Setup) return;
            if(seat<0 || seat>=LocalSeatCount || (team!=NativeTeam.TeamA && team!=NativeTeam.TeamB)) throw new ArgumentException("Invalid assignment");
            teamAssignments[seat]=team;setupError=null;RefreshInterface();
        }
        public NativeMatchRoster SetupRoster() => SetupComposition().Roster;
        bool ValidSetup()
        {
            try { return AuthoredArenaCatalog.Supports(SelectedMapId,(reviewComposition??SetupComposition()).ParticipantCount); } catch(ArgumentException) { return false; }
        }
        Text matchTimer;
        RectTransform timerPanel;
        RectTransform verticalDivider, horizontalDivider;
        readonly NativeStandingsView[] standings = new NativeStandingsView[SeatInputCoordinator.SeatCount];
        NativeStandingsView results, persistentStandings;
        NativeAchievementsView achievementsView;
        readonly DamageVignette[] damageVignettes=new DamageVignette[SeatInputCoordinator.SeatCount];
        readonly RectTransform[] viewportRoots=new RectTransform[SeatInputCoordinator.SeatCount];
        Button seatsMinus, seatsPlus;
        Button fallbackSettings;
        int frozenSeatCount=SeatInputCoordinator.SeatCount;
        int probeCameraCount;
        public int LocalSeatCount => input.ActiveSeatCount;
        Button repeat, menu, diagnosticButton, durationButton, targetButton, targetMinus, targetPlus, mouseSensitivityMinus, mouseSensitivityPlus;
        public NativeCombatSession Session { get; private set; }
        CombatPresentation presentation;
        bool[] damageTinted=Array.Empty<bool>();
        readonly List<GameObject> armorPickupVisuals=new List<GameObject>(),speedPickupVisuals=new List<GameObject>();
        GameObject damagePickupVisual;
        float previousFixedDelta;
        string[] hit = Array.Empty<string>();
        enum Phase { Loading, Setup, Running, Paused, Results, MainMenu, Profiles, Settings, Lab }
        Phase phase;
        bool diagnostic;
        bool combatReview;
        bool nativeInputReview;
        ProvingArena arena;
        GameObject overlay;
        RectTransform resultsFrame;
        Text status;
        Button start, resume, rebind, setupBack;
        public string CombatBowlReviewDirectory;
        public ProvingProfile OrbitalLeagueProfile=ProvingProfile.CreateCombatBowlRingPresentationDefault();
        public ProvingProfile CombatBowlAuthoring=CombatBowlCatalog.AuthoringProfile();
        public ProvingProfile LunarPresentation=ProvingProfile.CreateLunarPresentation();
        [NonSerialized] public ProvingProfile LunarAuthoring=LunarLaboratoryCatalog.AuthoringProfile();
        public ProvingProfile TunnelsPresentation=ProvingProfile.CreateIndustrialTunnelsPresentation();
        public ProvingProfile TunnelsAuthoring=IndustrialTunnelsCatalog.AuthoringProfile();
        public string SelectedMapId {get;private set;}=CombatBowlCatalog.Id;
        ArenaFreezeSnapshot frozenMatchArena;
        public void SelectAuthoredMap(string id)
        {
            if(phase!=Phase.Setup)throw new InvalidOperationException("Map selection is setup-only");
            AuthoredArenaCatalog.Name(id);SelectedMapId=id;setupError=null;RefreshInterface();
        }
        Button arenaButton;

        Font font;
        NativeGameAudio gameAudio;
        bool musicFocused=true;
        GameObject lastAudioSelection;
        FpsDisplay fps;
        string pauseReason = "";
        PlayerProfileCatalog playerProfiles;
        readonly LocalIdentitySession identities = new LocalIdentitySession();

        public bool Running => phase == Phase.Running;
        void Awake()
        {
            phase=Phase.Loading;
            loadingScreen=NativeLoadingScreen.Create(transform);
            loadingScreen.ShowStartup();
        }
        IEnumerator Start()
        {
            yield return null;
            yield return RunLoadingSteps(InitializeGame(),false);
        }
        IEnumerator InitializeGame()
        {
            playerProfiles = new PlayerProfileCatalog(PlayerProfilesPath());
            input.Joined += OnDeviceJoined;
            input.JoinRejected += device => {
                if(phase==Phase.Setup && setupStep==2 && LocalSeatCount<4 && RosterTotal<8)
                {
                    int seat=LocalSeatCount;botSetup.SetAi(seat,false);SetSeatCount(seat+1);
                    if(input.Assign(seat,device)){OnDeviceJoined(seat,device);return;}
                }
                setupError="Нет свободного места для "+device.displayName; RefreshInterface();
            };
            MatchProfile.EnsureMatchDescriptors();
            TrooperProfile.EnsureTrooperDescriptors();
            ParticipantPaletteProfile.EnsureIdentitySurfaceDescriptors();
            RocketEffectsProfile.EnsureRocketEffectsDescriptors();
            Profile.EnsureDefaultDescriptors();LifeProfile.EnsureCombatDescriptors();CombatProfile.EnsureNativeCombatDescriptors();
            if (Profile.Validate().Count != 0 || LifeProfile.Validate().Count != 0 || CombatProfile.Validate().Count != 0 || TrooperProfile.Validate().Count != 0) throw new InvalidOperationException("Invalid proving profile");
            yield return InitializeDesignLabAsync();
            previewMovement=Copy(Profile);previewLife=Copy(LifeProfile);previewCombat=Copy(CombatProfile);previewTrooper=Copy(TrooperProfile);
            Configuration=NativeMatchConfiguration.Default(MatchProfile);
            previousFixedDelta = Time.fixedDeltaTime;
            Time.fixedDeltaTime=1f/Profile.Get("simulation.fixedTickHz");
            // Automated reviews are silent. Ordinary Player sessions use the user's effects preference.
            var soundArgs=Environment.GetCommandLineArgs();
            bool muteReview=(Application.isEditor||Application.isBatchMode||
                !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_NAVIGATION_EVIDENCE"))||
                soundArgs.Any(a=>a.EndsWith("Review",StringComparison.Ordinal)||a=="-diagnostic"))&&!soundArgs.Contains("-audioReview");
            AudioListener.volume=muteReview?0:1;
            gameAudio=new NativeGameAudio(transform,Profile,muteReview);
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            studioLight = Owned("arena-light").AddComponent<Light>();
            studioLight.type = LightType.Directional; studioLight.intensity = Profile.Get("presentation.lightIntensity");
            studioLight.transform.rotation = Quaternion.Euler(50, -30, 0); // Authored fixture light orientation.
            var args=Environment.GetCommandLineArgs();int familyFlag=Array.IndexOf(args,"-arenaFamily");
            if(familyFlag>=0||args.Contains("-procedural"))throw new ArgumentException("Procedural arena source is unsupported.");
            bool environmentReview=DevelopmentNavigationReviewHandoff.TryParse(
                Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_NAVIGATION_EVIDENCE"),
                Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_ARENA_FAMILY"), out int environmentFamily);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int bowlReviewFlag=Array.IndexOf(args,"-combatBowlReview");
            if(bowlReviewFlag>=0)
            {
                if(bowlReviewFlag+1>=args.Length||!System.IO.Path.IsPathFullyQualified(args[bowlReviewFlag+1]))throw new ArgumentException("Absolute Combat Bowl review directory required");
                CombatBowlReviewDirectory=args[bowlReviewFlag+1];
            }
#endif
            arena = Owned("unity-arena").AddComponent<ProvingArena>();
            yield return BuildSelectedArenaSteps(true);
            InitializeGraphicsPreferences();
            Composition=HumanComposition(SetupRoster(),false);
            yield return RebuildActorsSteps(Composition);
            Session = new NativeCombatSession(motors, arena, gameObject.scene.GetPhysicsScene(), Profile, LifeProfile, CombatProfile,cutterProfile:CutterProfile);
            gameAudio.Bind(Session,Composition);BindDamageBonusAlerts();
            RebuildPickupVisuals();
            presentation = new CombatPresentation(Session, Profile, CombatProfile, transform, gameObject.scene.GetPhysicsScene(), cameras, bodies, views,deathProfile:DeathProfile,bloodProfile:BloodProfile,rocketPrefab:RocketProjectilePrefab,rocketEffects:RocketEffectsProfile,hitProfile:HitFeedbackProfile);
            BindShotFeedback();
            int countFlag=Array.IndexOf(args,"-probeCameras");
            if(args.Contains("-diagnostic") && countFlag>=0 && countFlag+1<args.Length && int.TryParse(args[countFlag+1],out int count) && (count==1||count==2||count==4))
            {
                probeCameraCount=count;
            }
            CreateInterface();
            phase=Phase.MainMenu;RefreshInterface();Select(mainBattleButton);
            worldReady=true;IsReady=true;loadingScreen.Hide();
            Debug.Log("STAR_TOURNAMENT_MAIN_MENU_READY");
#if UNITY_EDITOR || DEVELOPMENT_BUILD || STAR_TOURNAMENT_DEVELOPMENT_QA
            if(!string.IsNullOrEmpty(CombatBowlReviewDirectory))gameObject.AddComponent<NativeCombatBowlReview>();
#endif
            gameObject.AddComponent<ProvingTelemetry>();
            if(args.Contains("-teamsDiagnostic")) SetMatchMode(NativeMatchMode.Teams);
            bool requestedDiagnostic = Environment.GetCommandLineArgs().Contains("-diagnostic");
            if (requestedDiagnostic) Begin(true);
#if UNITY_EDITOR || DEVELOPMENT_BUILD || STAR_TOURNAMENT_DEVELOPMENT_QA
            if (args.Contains("-botSeatsReview")) gameObject.AddComponent<NativeBotSeatsReview>();
            if (args.Contains("-botSetupReview")) gameObject.AddComponent<NativeBotSetupReview>();
            if (args.Contains("-botPerceptionReview")) gameObject.AddComponent<NativeBotPerceptionReview>();
            if (args.Contains("-botTacticsReview")) gameObject.AddComponent<NativeBotTacticsReview>();
            if (args.Contains("-botBehaviorReview")) gameObject.AddComponent<NativeBotBehaviorReview>();
            if (args.Contains("-participantReview")) gameObject.AddComponent<NativeParticipantReview>();
            if (args.Contains("-botNavigationReview")||environmentReview)
            {
                // The review class is compiled only into the Development QA Player. Reflection keeps this ordinary bootstrap
                // independent from that conditional assembly while making the documented CLI activation observable.
                var reviewType=Type.GetType("StarTournament.ProvingGround.NativeBotNavigationReview, StarTournament.ProvingGround");
                if(reviewType==null) throw new InvalidOperationException("Development navigation review is not packaged");
                gameObject.AddComponent(reviewType); Debug.Log("NATIVE_BOT_NAVIGATION_REVIEW_BOOTSTRAPPED");
            }
            if (args.Contains("-shotOriginReview")) gameObject.AddComponent<NativeShotOriginReview>();
            if (args.Contains("-rocketEffectsReview")) gameObject.AddComponent<NativeRocketEffectsReview>();
            if (args.Contains("-rocketGratingReview")) gameObject.AddComponent<NativeRocketGratingReview>();
            if (args.Contains("-vectorReview")) gameObject.AddComponent<VectorVisualReview>();
            if (args.Contains("-trooperReview")) gameObject.AddComponent<TrooperReview>();
            if (args.Contains("-teamReview")) gameObject.AddComponent<NativeTeamsReview>();
            if (args.Contains("-layoutReview")) gameObject.AddComponent<NativeLayoutReview>();
            if (args.Contains("-matchReview")) gameObject.AddComponent<NativeMatchReview>();
            if (args.Contains("-combatReview") || args.Contains("-shotFeedbackReview")) gameObject.AddComponent<NativeCombatReview>();
            if (args.Contains("-bonusVisualReview"))
            {
                Application.runInBackground = true;
                gameObject.AddComponent<NativeBonusVisualReview>();
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (args.Contains("-musicReview")) gameObject.AddComponent<NativeMusicReview>();
            if (args.Contains("-movementAudioReview")) gameObject.AddComponent<NativeMovementAudioReview>();
            if (args.Contains("-unifiedDamageReview")) gameObject.AddComponent<NativeUnifiedDamageReview>();
            if (args.Contains("-scoreboardReview")) gameObject.AddComponent<NativeScoreboardReview>();
            if (args.Contains("-achievementReview")) gameObject.AddComponent<NativeAchievementReview>();
            if (args.Contains("-labReview")) gameObject.AddComponent<NativeDesignLabReview>();
            if (args.Contains("-fullHealReview")) gameObject.AddComponent<NativeFullHealReview>();
            if (args.Contains("-weaponBalanceReview")) gameObject.AddComponent<NativeWeaponBalanceReview>();
            if (args.Contains("-bloodReview")) gameObject.AddComponent<NativeBloodReview>();
            if (args.Contains("-cutterReview")) gameObject.AddComponent<NativeCutterReview>();
            if (args.Contains("-lunarPerformance")) gameObject.AddComponent<NativeLunarPerformanceReview>();
            if (args.Contains("-lunarReview")) gameObject.AddComponent<NativeLunarLaboratoryReview>();
            if (args.Contains("-industrialTunnelsReview")) gameObject.AddComponent<NativeIndustrialTunnelsReview>();
            if (args.Contains("-weaponPickupReview")) gameObject.AddComponent<NativeWeaponPickupReview>();
            if (args.Contains("-deathRagdollReview")) gameObject.AddComponent<NativeDeathRagdollReview>();
            if (args.Contains("-damageVignetteReview")) gameObject.AddComponent<NativeDamageVignetteReview>();
            if (args.Contains("-weaponSwitchReview")) gameObject.AddComponent<NativeWeaponSwitchReview>();
            if (args.Contains("-pulseReview")) gameObject.AddComponent<NativePulseReview>();
            if (args.Contains("-damageBonusReview")) gameObject.AddComponent<NativeDamageBonusReview>();
            if (args.Contains("-colorIdentityReview")) gameObject.AddComponent<NativeColorIdentityReview>();
            if (args.Contains("-colorSurfaceReview")) gameObject.AddComponent<NativeColorSurfaceReview>();
            if (args.Contains("-weaponColorReview")) gameObject.AddComponent<NativeWeaponColorReview>();
            if (args.Contains("-profilesReview")) gameObject.AddComponent<NativeProfilesReview>();
            if (args.Contains("-pauseReview")) gameObject.AddComponent<NativePauseMenuReview>();
            if (args.Contains("-rosterCardsReview")) gameObject.AddComponent<NativeRosterCardsReview>();
            if (args.Contains("-gamepadCameraReview"))gameObject.AddComponent<NativeGamepadCameraReview>();
            if (args.Contains("-menuStyleReview")) gameObject.AddComponent<NativeMenuStyleReview>();
            if (args.Contains("-setupSettingsReview")) gameObject.AddComponent<NativeSetupSettingsReview>();
#endif
#endif
        }
        static string PlayerProfilesPath()
        {
            string path=System.IO.Path.Combine(Application.persistentDataPath,"player-profiles-v1.json");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-profilesPath");
            if(index>=0)
            {
                if(index+1>=args.Length || !System.IO.Path.IsPathFullyQualified(args[index+1]))throw new ArgumentException("Absolute profile QA path required");
                path=args[index+1];
            }
#endif
            return path;
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void ConfigureBotTacticsReview(NativeMatchConfiguration config){config.Validate(MatchProfile);Configuration=config;}
        public void StartBotReview(NativeMatchComposition composition,uint seed,InputDevice[] devices=null)
        {
            if(phase!=Phase.Setup)Menu();
            composition.RequireSupportedSources(true,false);if(seed==0)throw new ArgumentException("Seed required");
            if(devices!=null&&devices.Length!=composition.LocalCount)throw new ArgumentException("Local device count mismatch");
            botReviewEnabled=true;botSeed=seed;reviewComposition=NativeMatchComposition.Restore(composition.Read());
            input.Reset();input.SetActiveSeatCount(composition.LocalCount);
            for(int seat=0;seat<composition.LocalCount;seat++)input.SetHumanSeat(seat,composition.Participant(composition.ParticipantAt(seat)).Kind==NativeParticipantKind.LocalHuman);
            if(devices!=null)for(int i=0;i<devices.Length;i++)if(input.IsHumanSeat(i)&&!input.Assign(i,devices[i]))throw new ArgumentException("Invalid device");
            Begin(devices==null);
        }
        public void SetParticipantReviewStandings(bool show) { reviewShowStandings=show; }
        public void StartParticipantReview(NativeMatchComposition composition,InputDevice[] devices=null)
        {
            if(phase!=Phase.Setup)Menu();
            composition.RequireSupportedSources(false,true);
            if(devices!=null&&devices.Length!=composition.LocalCount)throw new ArgumentException("Local device count mismatch");
            reviewComposition=NativeMatchComposition.Restore(composition.Read());
            input.Reset();input.SetActiveSeatCount(composition.LocalCount);
            for(int seat=0;seat<composition.LocalCount;seat++)input.SetHumanSeat(seat,composition.Participant(composition.ParticipantAt(seat)).Kind==NativeParticipantKind.LocalHuman);
            if(devices!=null)for(int seat=0;seat<devices.Length;seat++)if(input.IsHumanSeat(seat)&&!input.Assign(seat,devices[seat]))throw new ArgumentException("Invalid local device");
            Begin(devices==null);
        }
        public void StartCombatReview(Gamepad[] pads,bool backgroundDiagnostic=false,bool ensureOpponent=false)
        {
            if(phase!=Phase.Setup) Menu();
            SetSeatCount(pads.Length);
            if(ensureOpponent && pads.Length==1 && LocalSeatCount+botSetup.Count<2)AddBot();
            input.Reset();
            for(int i=0;i<pads.Length;i++)
                if(!input.Assign(i,pads[i]))throw new InvalidOperationException("Combat review could not assign pad "+i+": added="+pads[i].added+", enabled="+pads[i].enabled+", human="+input.IsHumanSeat(i));
            // Native review fixtures use real per-seat preference owners before entering ordinary pause/UI paths.
            for(int i=0;i<pads.Length;i++)if(!identities.HasIdentity(i))identities.ChooseGuest(i,pads[i].deviceId,MouseSensitivityPreference.Resolve(Profile),fps.Visible);
            combatReview=true; Begin(backgroundDiagnostic);
        }
        public string CombatReviewDiagnostic() => "phase="+phase+", ready="+input.Ready+", validSetup="+ValidSetup()+", seats="+LocalSeatCount+", error="+setupError;
        public void EnableNativeInputReview()
        {
            if(phase!=Phase.Running||!combatReview)throw new InvalidOperationException("Native input review requires a running combat fixture");
            nativeInputReview=true;combatReview=false;ApplyMatchFpsPreference();RefreshInterface();
        }
        public void OpenPauseReviewSeat(int seat)
        {
            if(phase==Phase.Running)Pause("Пауза",seat);
            else if(phase==Phase.Paused)OpenSeatPause(seat);
        }
        public void ResumeNativeInputReview()
        {
            if(!nativeInputReview||phase!=Phase.Paused)return;
            ClearSeatPauseMenus();Resume();RefreshInterface();
        }
        public CharacterMotor CameraReviewMotor(int seat)=>motors[seat];
        public ProvingArena CameraReviewArena=>arena;
        public void PlaceCombatReviewSeat(int seat, Vector3 feet, float pitch,float yaw=0)
        {
            motors[seat].Initialize(frozenMovement??Profile,feet,arena);
            motors[seat].Tick(new LocalAction{LookDegrees=new Vector2(yaw,-pitch)},Time.fixedDeltaTime);
        }
        public void EnableNavigationReview()
        {
            navigationReviewEnabled = true;
            frozenNavigation = Copy(BotNavigationProfile); frozenPerception = Copy(BotPerceptionProfile);
            NavigationReviewDriver = new NativeNavigationDriver(Session,arena,gameObject.scene.GetPhysicsScene(),
                frozenMovement??Profile,frozenCombat??CombatProfile,frozenPerception,frozenNavigation,0);
        }
#endif
        GameObject Owned(string name, params Type[] components)
        {
            var go = new GameObject(name, components);
            SceneManager.MoveGameObjectToScene(go, gameObject.scene);
            go.transform.SetParent(transform, false);
            return go;
        }
        void CreateActor(int index,int ownerSeat,ProvingProfile movement,ProvingProfile trooper)
        {
            var player = Owned("player-" + (index+1));player.SetActive(false); player.layer = ProvingArena.ParticipantLayer;
            motors[index] = player.AddComponent<CharacterMotor>(); motors[index].Initialize(movement, arena.Spawns[index%arena.Spawns.Length],arena);
            if (!TrooperBodyPrefab || !TrooperArmsPrefab) throw new InvalidOperationException("Trooper assets missing; run Prepare in Editor.");
            var body = Instantiate(TrooperBodyPrefab, player.transform); body.name = "trooper-presentation";
            // Shipping recipe has a measured 1.8m rest height and feet origin; never normalize animated bounds.
            body.transform.localScale=Vector3.one*(movement.Get("player.capsule.height")/1.8f);
            body.AddComponent<TrooperVisual>().Initialize(TrooperClips,trooper);
            ApplyIdentity(body,index); SetLayer(body,ownerSeat>=0?11+ownerSeat:0);
            if(ownerSeat>=0)CreateOwnShadowProxy(body);
            bodies[index]=body;
            body.AddComponent<VectorShotPresentation>().Initialize(movement);
            body.AddComponent<WeaponModelPresentation>().Initialize(RiflePrefab,movement,PulsePrefab,CutterPrefab,frozenCutter??CutterProfile);
        }
        static void CreateOwnShadowProxy(GameObject body)
        {
            // Owner cameras hide the third-person body layer. A shadow-only skin on the world
            // layer reuses its animated bones, so the player sees the real silhouette on the floor.
            // This is renderer routing only: no duplicate motor, animator or collider is created.
            foreach(var source in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if(!source.sharedMesh)continue;
                var go=new GameObject("own-shadow-proxy");go.transform.SetParent(source.transform,false);go.layer=0;
                var proxy=go.AddComponent<SkinnedMeshRenderer>();
                proxy.sharedMesh=source.sharedMesh;proxy.sharedMaterials=source.sharedMaterials;
                proxy.bones=source.bones;proxy.rootBone=source.rootBone;proxy.localBounds=source.localBounds;
                proxy.updateWhenOffscreen=true;
                proxy.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                proxy.receiveShadows=false;
            }
        }
        void CreateView(int index,int participant,ProvingProfile movement,ProvingProfile trooper)
        {
            var cameraObject = Owned("seat-camera-" + (index+1));
            cameras[index] = cameraObject.AddComponent<Camera>();
            cameras[index].rect = LocalSeatLayout.Viewport(SeatInputCoordinator.SeatCount,index);
            cameras[index].fieldOfView = movement.Get("camera.fieldOfViewDegrees");
            cameras[index].nearClipPlane = movement.Get("camera.nearClipPlane"); cameras[index].farClipPlane = movement.Get("camera.farClipPlane");
            cameras[index].cullingMask = ~((1 << (11+index)) | (15 << 15)) | (1 << (15+index));
            cameras[index].backgroundColor = new Color32(15, 22, 35, 255);
            var view = Instantiate(TrooperArmsPrefab,cameraObject.transform); view.name="trooper-view";
            SetLayer(view,15+index); views[index]=view;
            // First-person arms are camera-local presentation, never a world silhouette.
            foreach(var renderer in view.GetComponentsInChildren<Renderer>(true))renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            view.AddComponent<TrooperVisual>().Initialize(TrooperClips,trooper,true);
            ApplyIdentity(view,participant);
            view.AddComponent<VectorShotPresentation>().Initialize(movement);
            view.AddComponent<WeaponModelPresentation>().Initialize(RiflePrefab,movement,PulsePrefab,CutterPrefab,frozenCutter??CutterProfile);
            view.transform.localPosition=new Vector3(trooper.Get("view.x"),trooper.Get("view.y"),trooper.Get("view.z"));
            var fillObject=new GameObject("equipment-fill");fillObject.transform.SetParent(cameraObject.transform,false);
            var fill=fillObject.AddComponent<Light>();fill.type=LightType.Point;
            fill.intensity=trooper.Get("view.fillIntensity");fill.range=trooper.Get("view.fillRange");
            fill.cullingMask=1<<(15+index);fill.shadows=LightShadows.None;
            if (index == 0) cameraObject.AddComponent<AudioListener>();
        }
        NativeMatchComposition HumanComposition(NativeMatchRoster roster,bool swapped)
        {
            var metadata=Enumerable.Range(0,roster.Count).Select(i=>new NativeParticipantInfo(NativeParticipantKind.LocalHuman,
                "Игрок "+(i+1),NativeStandingsView.Identity(roster.Read(),i,swapped))).ToArray();
            return new NativeMatchComposition(roster,metadata,Enumerable.Range(0,roster.Count).ToArray());
        }
        void RebuildActors(NativeMatchComposition composition,ProvingProfile movement=null,ProvingProfile trooper=null,bool activate=true)
        { var steps=RebuildActorsSteps(composition,movement,trooper,activate);while(steps.MoveNext()){} }
        IEnumerator RebuildActorsSteps(NativeMatchComposition composition,ProvingProfile movement=null,ProvingProfile trooper=null,bool activate=true)
        {
            movement=movement??Profile;trooper=trooper??TrooperProfile;
            localActions=new LocalAction[composition.LocalCount];
            foreach(var motor in motors)if(motor){motor.gameObject.SetActive(false);Destroy(motor.gameObject);}
            for(int s=0;s<cameras.Length;s++)if(cameras[s]){cameras[s].gameObject.SetActive(false);Destroy(cameras[s].gameObject);cameras[s]=null;views[s]=null;}
            damageTinted=new bool[composition.ParticipantCount];motors=new CharacterMotor[composition.ParticipantCount];bodies=new GameObject[motors.Length];hit=new string[motors.Length];
            for(int p=0;p<motors.Length;p++){CreateActor(p,composition.SeatOf(p),movement,trooper);yield return null;}
            for(int seat=0;seat<composition.LocalCount;seat++){CreateView(seat,composition.ParticipantAt(seat),movement,trooper);yield return null;}
            if(activate)foreach(var motor in motors)motor.gameObject.SetActive(true);
        }
        void ApplyIdentity(GameObject root,int seat) => ApplyIdentity(root,NativeStandingsView.Palette[seat]);
        void ApplyIdentity(GameObject root,Color color)
        {
            TrooperIdentityPresentation.ApplyBody(root,color,ParticipantPaletteProfile);
            root.GetComponent<WeaponModelPresentation>()?.Tint(color,false);
        }
        static void SetLayer(GameObject root, int layer) { foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer; }
        void Update()
        {
            if(phase==Phase.Loading)return;
            TickDisplayConfirmation();
            UpdateSettingsBackInput();
            UpdateRosterInput();
            if (phase == Phase.Setup)
            {
                PollRosterShortcuts();if(setupStep==2)input.PollSetup();PollSetupStart();
            }
            else if (phase == Phase.Running && !diagnostic)
            {
                if (!input.Ready) Pause("Устройство отключено: "+string.Join(", ",Enumerable.Range(0,LocalSeatCount).Where(i=>input.IsHumanSeat(i)&&!input.IsConnected(i)).Select(i=>"P"+(i+1))));
                else if(input.HasKeyboard && Cursor.lockState!=CursorLockMode.Locked) Pause("Захват курсора потерян");
                else
                {
                    var activeProfile=frozenMovement??Profile; input.Capture(activeProfile,Time.deltaTime,ActiveMouseSensitivity(),PersonalGamepadLook);
                    if(UseSeatPauseMenus)
                    {
                        input.ConsumePause();
                        int mask=SeatPausePressedMask();
                        if(mask!=0)
                        {
                            int first=Enumerable.Range(0,LocalSeatCount).First(i=>(mask&(1<<i))!=0);
                            Pause("Пауза",first);
                            for(int i=0;i<LocalSeatCount;i++)if(i!=first&&(mask&(1<<i))!=0)OpenSeatPause(i);
                        }
                    }
                    else if (input.ConsumePause()) Pause("Пауза");
                }
            }
            else if(phase==Phase.Paused && UseSeatPauseMenus)UpdateSeatPauseInput();
            UpdateDesignLab();
            if (diagnostic && phase==Phase.Running && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Pause("Диагностическая пауза");
            RefreshInterface();
            UpdateMenuCursor();
            UpdateMenuSelectionAudio();
            bool musicMatch=phase==Phase.Running||phase==Phase.Paused||phase==Phase.Results||
                (phase==Phase.Settings&&(settingsReturn==Phase.Running||settingsReturn==Phase.Paused||settingsReturn==Phase.Results));
            bool musicPaused=phase==Phase.Paused||(phase==Phase.Settings&&(settingsReturn==Phase.Running||settingsReturn==Phase.Paused));
            bool musicBackgroundAllowed=diagnostic||combatReview||nativeInputReview||Application.isBatchMode;
            gameAudio?.TickMusic(!musicMatch,musicPaused||(!musicFocused&&!musicBackgroundAllowed),Time.unscaledDeltaTime);
        }
        void UpdateMenuSelectionAudio()
        {
            var selected=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
            if(selected==lastAudioSelection)return;
            if(selected!=null&&lastAudioSelection!=null&&phase!=Phase.Running)gameAudio?.MenuMove();
            lastAudioSelection=selected;
        }
        void FixedUpdate()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(FullHealReviewManualTick)return;
#endif
            if (phase != Phase.Running) return;
            for(int seat=0;seat<localActions.Length;seat++)localActions[seat]=diagnostic?default:input.Consume(seat);
            Composition.AssembleLocalActions(localActions,actions);
            try
            {
                NavigationReviewDriver?.ProduceActions(actions);
                BotDriver?.ProduceActions(actions,Time.fixedDeltaTime);
                Session.Tick(actions, Time.fixedDeltaTime);
                gameAudio?.Tick(true);
                if(Session.Match?.Phase==NativeMatchPhase.Finished)
                {
                    gameAudio?.StopEffects();
                    phase=Phase.Results; input.Clear(); Session.ClearInput(); Array.Clear(actions,0,actions.Length);
                    SetCursor(false); RefreshInterface(); Select(repeat.interactable?repeat:menu);
                }
            }
            catch(InvalidOperationException error) { Pause(error.Message); Debug.LogError(error); }
        }
        void LateUpdate()
        {
            if(Session == null || phase==Phase.Loading || !worldReady) return;
            RenderArmorPickup();RenderSpeedPickup();RenderDamagePickup();RenderHealPickup();RenderWeaponPickups();
            for(int p=0;p<Composition.ParticipantCount;p++)
            {
                bool boosted=Session.DamageBoostRemaining(p)>0;if(damageTinted[p]==boosted)continue;damageTinted[p]=boosted;
                var color=Composition.Participant(p).Color;
                bodies[p].GetComponent<WeaponModelPresentation>()?.Tint(color,boosted);
                int seat=Composition.SeatOf(p);if(seat>=0)views[seat].GetComponent<WeaponModelPresentation>()?.Tint(color,boosted);
            }
            if(phase!=Phase.Setup) presentation.Render();
            var snapshot=Session.Match?.Read();var lifeStates=Session.LifeStates;
            persistentStandings.Show(phase!=Phase.Setup && frozenSeatCount==3,snapshot,diagnostic||combatReview,Composition,lifeStates);
            var match=Session.Match;
            if(timerPanel)timerPanel.gameObject.SetActive(phase==Phase.Running && match!=null);
            if(matchTimer && match!=null)
            {
                matchTimer.text=match.Phase==NativeMatchPhase.Overtime ? "ОВЕРТАЙМ" : TimeLabel(match.RemainingSeconds);
                float padding=Profile.Get("ui.fontSize");
                timerPanel.sizeDelta=new Vector2(matchTimer.preferredWidth+padding*2,Profile.Get("ui.headingFontSize")+padding);
            }
            for(int i=0;i<Composition.LocalCount;i++) if(hud[i])
            {
                int participant=Composition.ParticipantAt(i);var life=Session.Life(participant);
                if(damageBonusNotice[i])damageBonusNotice[i].text=phase==Phase.Running&&Session.Time<damageBonusUntil?damageBonusText:"";
                damageVignettes[i]?.Render(phase==Phase.Running&&!life.Dead);
                health[i].text=Mathf.CeilToInt(life.Health).ToString();
                armorGroups[i].SetActive(life.Armor>0); if(life.Armor>0) armor[i].text=Mathf.CeilToInt(life.Armor).ToString();
                ammo[i].text=(life.SelectedWeapon==WeaponId.Cutter?life.CutterEnergy.ToString("0.0"):life.Ammo.ToString())+(Session.DamageBoostRemaining(participant)>0&&life.SelectedWeapon!=WeaponId.RocketLauncher?" ×2":"");
                ammoIcons[i].Kind=life.SelectedWeapon==WeaponId.Cutter?HudIndicatorIcon.IconKind.Cutter:life.SelectedWeapon==WeaponId.Rifle?HudIndicatorIcon.IconKind.Rifle:life.SelectedWeapon==WeaponId.Shotgun?HudIndicatorIcon.IconKind.Shotgun:HudIndicatorIcon.IconKind.Rocket;
                ResizeHudIndicator(health[i]);if(life.Armor>0)ResizeHudIndicator(armor[i]);ResizeHudIndicator(ammo[i]);

                ammo[i].color=Session.DamageBoostRemaining(participant)>0?Color.red:Color.white;
                ammoIcons[i].color=ammo[i].color;
                crosshair[i].text=life.Dead ? "" : "+";
                if(killNotice[i])
                {
                    killNotice[i].text=phase==Phase.Setup || phase==Phase.Results ? "" :
                        life.Dead ? DeathNoticeText(participant,snapshot) :
                        Session.Time<killNoticeUntil[i] ? killNoticeText[i] : "";
                    killNotice[i].color=!life.Dead && Session.Time<killNoticeUntil[i] && killNoticeAllied[i] ? Color.red : Color.white;
                }
                standings[i].PerspectiveParticipant=participant;
                standings[i].Show(phase==Phase.Running && (actions[participant].ShowRoster || (reviewComposition!=null&&reviewShowStandings)),snapshot,diagnostic||combatReview,Composition,lifeStates);
                names[i].color=Composition.Participant(participant).Color;
                names[i].text=Composition.Participant(participant).Name+(snapshot?.Roster.Mode==NativeMatchMode.Teams ? " · "+NativeStandingsView.TeamName(snapshot.Roster.Teams[participant]) : "")+(diagnostic || combatReview ? " · DIAGNOSTIC" : "");
                hud[i].text=hit[participant] ?? "";
            }
        }
        void OnApplicationFocus(bool focus)
        {
            musicFocused=focus;
            if(!focus){input.Clear();Session?.ClearInput();}
            if(!focus&&!diagnostic&&!combatReview&&!nativeInputReview&&!Application.isBatchMode)gameAudio?.SuspendMusic();
            if(!focus && displayConfirmationActive)RollbackDisplay();
            if (!focus && phase == Phase.Running && !diagnostic && !combatReview && !nativeInputReview) Pause("Окно потеряло фокус");
        }
        string ColoredName(int participant)
        {
            var info=Composition.Participant(participant);
            // Participant names are plain text even though the notice uses Unity rich text for color.
            string safe=info.Name.Replace('<','‹').Replace('>','›');
            return "<color=#"+ColorUtility.ToHtmlStringRGB(info.Color)+">"+safe+"</color>";
        }
        int ParticipantWithId(string id)
        {
            for(int p=0;p<Session.ParticipantCount;p++)if(Session.Life(p).ParticipantId==id)return p;
            return -1;
        }
        void BindShotFeedback()
        {
            var lastContact=Enumerable.Repeat(double.NegativeInfinity,Session.ParticipantCount).ToArray();
            Session.Fired+=(seat,damage)=>{if(lastContact[seat]==Session.Time)return;hit[seat]=damage>0?"Попадание · "+Mathf.RoundToInt(damage):Session.Life(seat).SelectedWeapon==WeaponId.Rifle?"Выстрел":"Выстрел · мимо";};
            Session.RifleBulletHit+=e=>{lastContact[e.Bullet.Owner]=e.Time;hit[e.Bullet.Owner]=e.AppliedDamage>0?"Попадание · "+Mathf.RoundToInt(e.AppliedDamage):"Контакт · без урона";};
        }
        string DeathNoticeText(int participant,NativeMatchSnapshot snapshot)
        {
            if(snapshot==null)return presentation.DeathMessage(participant);
            var life=Session.Life(participant);
            int killer=life.KillerId==null?-1:ParticipantWithId(life.KillerId);
            string message=killer==participant?"Ты убил себя":killer<0?"Вы погибли":"Вас убил "+ColoredName(killer)+" · "+
                snapshot.DirectKills(participant,killer)+":"+snapshot.DirectKills(killer,participant);
            return message+"\nВозрождение через "+Mathf.CeilToInt((float)life.RespawnRemaining);
        }
        void OnMatchDeath(DeathNotice notice)
        {
            int localSeat=Composition==null?-1:Composition.SeatOf(notice.Seat);
            if(localSeat>=0)input.SetSeatAlive(localSeat,false);
            if(Session.Match==null || notice.Life.KillerId==null)return;
            int killer=ParticipantWithId(notice.Life.KillerId);
            if(killer<0 || killer==notice.Seat)return;
            int view=Composition.SeatOf(killer);
            if(view<0)return;
            var snapshot=Session.Match.Read();
            int chain=snapshot.KillChain(killer);
            killNoticeAllied[view]=Session.Match.Roster.AreAllies(killer,notice.Seat);
            killNoticeText[view]=killNoticeAllied[view]?"Ты убил союзника "+Composition.Participant(notice.Seat).Name.Replace('<','‹').Replace('>','›'):"Убит "+ColoredName(notice.Seat)+" · "+
                snapshot.DirectKills(killer,notice.Seat)+":"+snapshot.DirectKills(notice.Seat,killer)+
                (chain==2?"\nDouble kill!":chain>2?"\n"+chain+" kills!":"");
            killNoticeUntil[view]=Session.Time+(frozenMovement??Profile).Get("ui.killNoticeSeconds");
        }
        void OnMatchRespawn(int participant)
        {
            int localSeat=Composition==null?-1:Composition.SeatOf(participant);
            if(localSeat>=0)input.SetSeatAlive(localSeat,true);
        }
        void Begin(bool diagnostics)
        {
            if ((!diagnostics && (!input.Ready || !IdentitiesReady())) || !ValidSetup()) return;
            diagnostic = diagnostics;
            try { if(!diagnostics&&!combatReview&&!botReviewEnabled&&reviewComposition==null)ApplySavedLabRevision(); BuildSelectedArena(); CreateMatch(false); } catch(Exception error) when(error is InvalidOperationException || error is ArgumentException) { Menu();setupError=error.Message;RefreshInterface();return; }
            worldReady=true;setupError=null; input.Clear(); Session.ClearInput(); phase = Phase.Running; SetCursor(!diagnostics && input.HasKeyboard);
            ApplyMatchFpsPreference();
            if(!diagnostics&&!combatReview&&!botReviewEnabled&&reviewComposition==null&&!nativeInputReview)RememberPlayedMap();
        }
        static ProvingProfile Copy(ProvingProfile p) => JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(p));
        void BuildSelectedArena() { var steps=BuildSelectedArenaSteps(false);while(steps.MoveNext()){} }
        IEnumerator BuildSelectedArenaSteps(bool asynchronous)
        {
            if(OrbitalLeagueProfile.Id=="orbital-league-v1")OrbitalLeagueProfile=ProvingProfile.CreateCombatBowlRingPresentationDefault();
            OrbitalLeagueProfile.EnsureOrbitalLeagueDescriptors();
            studioLight.intensity=OrbitalLeagueProfile.Get("light.studioKey");
            studioLight.shadowStrength=OrbitalLeagueProfile.Get("light.keyShadowStrength");
            studioLight.shadows=studioLight.shadowStrength>0?LightShadows.Soft:LightShadows.None;
            studioLight.shadowBias=OrbitalLeagueProfile.Get("light.shadowBias");
            studioLight.shadowNormalBias=OrbitalLeagueProfile.Get("light.shadowNormalBias");
            var frozen=AuthoredArenaCatalog.Freeze(SelectedMapId,Profile);
            bool tunnels=SelectedMapId==IndustrialTunnelsCatalog.Id;
            if(tunnels)studioLight.intensity=TunnelsPresentation.Get("tunnels.light.key");
            bool lunar=SelectedMapId==LunarLaboratoryCatalog.Id;
            if(lunar)studioLight.intensity=LunarPresentation.Get("lunar.sun");
            var steps=arena.BuildSteps(frozen,Profile,lunar?LunarPresentation:tunnels?TunnelsPresentation:OrbitalLeagueProfile,asynchronous);
            while(steps.MoveNext())yield return steps.Current;
            ApplyShadowPreference();
        }
        static string TimeLabel(double seconds) { int value=(int)Math.Ceiling(seconds); return (value/60).ToString("00")+":"+(value%60).ToString("00"); }
        void CreateMatch(bool repeating) { DrainLoadingSteps(CreateMatchSteps(repeating)); }
        IEnumerator CreateMatchSteps(bool repeating)
        {
            for(int seat=0;seat<SeatInputCoordinator.SeatCount;seat++)input.SetSeatAlive(seat,true);
            if(!repeating)
                foreach(var profile in new[]{Profile,LifeProfile,CombatProfile,MatchProfile,TrooperProfile,TeamProfile,RosterProfile,ParticipantPaletteProfile,CutterProfile,DeathProfile,RocketEffectsProfile})
                {
                    if(profile==null||profile.Validate().Count!=0)throw new ArgumentException("Invalid match profile");
                    foreach(var descriptor in profile.Descriptors)NativeMatchConfiguration.ValidateValue(profile,descriptor.Path,profile.Get(descriptor.Path));
                }
            Session?.Stop(); presentation?.Dispose();
            if(!repeating)
            {
                Configuration.Validate(MatchProfile);
                if(TeamProfile.Id!="unity-native-team-v1" || TeamProfile.Version!=1 || TeamProfile.Validate().Count!=0) throw new InvalidOperationException("Invalid team profile");
                foreach(var descriptor in TeamProfile.Descriptors) NativeMatchConfiguration.ValidateValue(TeamProfile,descriptor.Path,TeamProfile.Get(descriptor.Path));
                Composition=reviewComposition!=null?NativeMatchComposition.Restore(reviewComposition.Read()):
                    (combatReview||diagnostic?SetupComposition():WithSelectedNames(SetupComposition()));
                if(reviewComposition==null&&!combatReview&&!diagnostic)Composition=NativeParticipantColors.FreezeNewMatch(Composition,profile:ParticipantPaletteProfile);
                if(!AuthoredArenaCatalog.Supports(arena.Definition.MapId,Composition.ParticipantCount))throw new ArgumentException("Карта поддерживает 2–"+AuthoredArenaCatalog.Maximum(arena.Definition.MapId)+" участников");
                frozenMatchArena=arena.FrozenSnapshot;
                bool hasBots=Composition.Read().Participants.Any(p=>p.Kind==NativeParticipantKind.Bot);
                if(hasBots&&!botReviewEnabled)botSeed=(uint)UnityEngine.Random.Range(1,int.MaxValue);
                Composition.RequireSupportedSources(true,reviewComposition!=null&&!botReviewEnabled);
                frozenRoster=Composition.Roster;frozenSwappedColors=swappedTeamColors;frozenTeam=Copy(TeamProfile);
                if(RosterProfile.Id!="unity-native-roster-v1" || RosterProfile.Version!=1 || RosterProfile.Validate().Count!=0)throw new InvalidOperationException("Invalid roster profile");
                foreach(var descriptor in RosterProfile.Descriptors)NativeMatchConfiguration.ValidateValue(RosterProfile,descriptor.Path,RosterProfile.Get(descriptor.Path));
                frozenRosterProfile=Copy(RosterProfile);
                if(hasBots){frozenBehavior=Copy(BotBehaviorProfile);frozenPerception=Copy(BotPerceptionProfile);frozenNavigation=Copy(BotNavigationProfile);}
                frozenRocketEffects=Copy(RocketEffectsProfile);frozenCutter=Copy(CutterProfile);frozenMovement=Copy(Profile); frozenLife=Copy(LifeProfile); frozenCombat=Copy(CombatProfile); frozenMatch=Copy(MatchProfile); frozenTrooper=Copy(TrooperProfile); frozenDeath=Copy(DeathProfile); frozenBlood=Copy(BloodProfile);frozenHitFeedback=Copy(HitFeedbackProfile);
                FrozenLabIdentity=LabSavedIdentity;
                var selectedLab=labHistory?.Selected;
                frozenLabReference=labHistory==null?null:new LabRevisionReference{ProfileId=diagnostic||combatReview||botReviewEnabled||reviewComposition!=null?"diagnostic-fixture":labHistory.SelectedProfileId,Revision=diagnostic||combatReview||botReviewEnabled||reviewComposition!=null?0:selectedLab.ReleaseSequence>0?selectedLab.ReleaseNumber:selectedLab.Number,Hash=CurrentLabBundle().Hash()};
                frozenConfiguration=Configuration; frozenSeatCount=LocalSeatCount;
            }
            yield return null;
            Time.fixedDeltaTime=1/frozenMovement.Get("simulation.fixedTickHz");
            yield return RebuildActorsSteps(Composition,frozenMovement,frozenTrooper,false);
            yield return null;
            foreach(var motor in motors)motor.SetAlive(false);
            Physics.SyncTransforms();
            var selector=new SafeSpawnSelector(gameObject.scene.GetPhysicsScene(),arena,frozenMovement,frozenCombat);
            Vector3[] initial=null;bool placed=false;
            yield return selector.AllocateInitial(frozenRoster,frozenTeam.Get("spawn.initialOpponentSeparation"),(success,positions)=>{placed=success;initial=positions;},
                (int)frozenRosterProfile.Get("spawn.initialSearchBudgetNodes"),(int)frozenRosterProfile.Get("spawn.initialCandidateBudget"));
            if(!placed)throw new InvalidOperationException("Нет безопасного размещения: "+selector.InitialFailure);
            yield return null;
            for(int p=0;p<motors.Length;p++)
            {
                motors[p].Initialize(frozenMovement,initial[p]);
                ApplyIdentity(bodies[p],Composition.Participant(p).Color);
                bodies[p].GetComponent<TrooperVisual>().Initialize(TrooperClips,frozenTrooper);
            }
            foreach(var motor in motors)motor.gameObject.SetActive(true);
            Physics.SyncTransforms();
            for(int seat=0;seat<frozenSeatCount;seat++)
            {
                ApplyIdentity(views[seat],Composition.Participant(Composition.ParticipantAt(seat)).Color);
                views[seat].GetComponent<TrooperVisual>().Initialize(TrooperClips,frozenTrooper,true);
                views[seat].transform.localPosition=new Vector3(frozenTrooper.Get("view.x"),frozenTrooper.Get("view.y"),frozenTrooper.Get("view.z"));
            }
            actions=new LocalAction[Composition.ParticipantCount];
            ApplyLayout(frozenSeatCount);
            Array.Clear(actions,0,actions.Length); Array.Clear(hit,0,hit.Length);
            yield return null;
            var match=new NativeMatchState(frozenRoster,frozenConfiguration,frozenMatch,frozenMovement.Get("simulation.fixedTickHz"));
            match.ConfigureAchievementRecipients(Enumerable.Range(0,Composition.ParticipantCount)
                .Select(p=>Composition.Participant(p).Kind==NativeParticipantKind.LocalHuman).ToArray());
            Session=new NativeCombatSession(motors,arena,gameObject.scene.GetPhysicsScene(),frozenMovement,frozenLife,frozenCombat,match,frozenLabReference,frozenCutter);
            yield return null;
            RebuildPickupVisuals();
            gameAudio?.Bind(Session,Composition,frozenMovement);BindDamageBonusAlerts();
            NavigationReviewDriver = navigationReviewEnabled ? new NativeNavigationDriver(Session,arena,gameObject.scene.GetPhysicsScene(),
                frozenMovement,frozenCombat,frozenPerception,frozenNavigation,0) : null;
            BotDriver=Composition.Read().Participants.Any(p=>p.Kind==NativeParticipantKind.Bot)?new NativeBotMatchDriver(Session,Composition,arena,gameObject.scene.GetPhysicsScene(),frozenMovement,frozenLife,frozenCombat,frozenPerception,frozenNavigation,frozenBehavior,botSeed):null;
            yield return null;
            presentation=new CombatPresentation(Session,frozenMovement,frozenCombat,transform,gameObject.scene.GetPhysicsScene(),cameras.Take(frozenSeatCount).ToArray(),bodies,views.Take(frozenSeatCount).ToArray(),Composition,deathProfile:frozenDeath,bloodProfile:frozenBlood,rocketPrefab:RocketProjectilePrefab,rocketEffects:frozenRocketEffects,hitProfile:frozenHitFeedback);
            Array.Clear(killNoticeText,0,killNoticeText.Length);Array.Clear(killNoticeUntil,0,killNoticeUntil.Length);
            Array.Clear(killNoticeAllied,0,killNoticeAllied.Length);
            for(int seat=0;seat<damageVignettes.Length;seat++)
                damageVignettes[seat]?.Bind(seat<frozenSeatCount?Session:null,seat<frozenSeatCount?Composition.ParticipantAt(seat):-1,frozenMovement);
            Session.Died+=OnMatchDeath;
            Session.Respawned+=OnMatchRespawn;
            BindShotFeedback();
            foreach(var view in standings) view.SwapTeamColors=frozenSwappedColors;
            persistentStandings.SwapTeamColors=results.SwapTeamColors=frozenSwappedColors;
            input.Clear(); Session.ClearInput();
        }
        void Repeat() { QueueMatch(diagnostic,true); }
        void Menu()
        {
            NavigationReviewDriver = null; navigationReviewEnabled = false; BotDriver=null;botReviewEnabled=false;
            foreach(var vignette in damageVignettes)vignette?.Bind(null,-1,Profile);
            phase=Phase.Setup; diagnostic=false; combatReview=false; nativeInputReview=false; probeCameraCount=0; Session?.Stop(); presentation?.Dispose();
            if(!worldReady){setupStep=setupMaxStep=2;input.Clear();SetCursor(false);RefreshInterface();FocusSetupStep();return;}
            bool wasReview=reviewComposition!=null;
            reviewComposition=null;reviewShowStandings=false;if(wasReview&&LocalSeatCount<2)input.SetActiveSeatCount(2);SyncHumanSeats();
            // Setup preview is not a playable roster; preserve invalid drafts for editing.
            if(LocalSeatCount+botSetup.Count>=NativeMatchRoster.MinimumParticipants)
                Composition=botSetup.Build(LocalSeatCount,NativeMatchMode.Ffa,teamAssignments,false);
            else
                Composition=new NativeMatchComposition(NativeMatchRoster.Ffa(2),new[]{
                    new NativeParticipantInfo(NativeParticipantKind.LocalHuman,"Игрок 1",NativeStandingsView.Palette[0]),
                    new NativeParticipantInfo(NativeParticipantKind.DiagnosticFixture,"Preview",NativeStandingsView.Palette[1])},new[]{0});
            var safeMovement=frozenMovement??previewMovement;var safeLife=frozenLife??previewLife;var safeCombat=frozenCombat??previewCombat;
            RebuildActors(Composition,safeMovement,frozenTrooper??previewTrooper);
            Session=new NativeCombatSession(motors,arena,gameObject.scene.GetPhysicsScene(),safeMovement,safeLife,safeCombat,cutterProfile:CutterProfile);
            RebuildPickupVisuals();
            gameAudio?.Bind(Session,Composition);BindDamageBonusAlerts();
            presentation=new CombatPresentation(Session,safeMovement,safeCombat,transform,gameObject.scene.GetPhysicsScene(),cameras.Take(LocalSeatCount).ToArray(),bodies,views.Take(LocalSeatCount).ToArray(),Composition,deathProfile:DeathProfile,bloodProfile:BloodProfile,rocketPrefab:RocketProjectilePrefab,rocketEffects:RocketEffectsProfile,hitProfile:HitFeedbackProfile);
            actions=new LocalAction[Composition.ParticipantCount];ApplyLayout(LocalSeatCount);
            setupStep=setupMaxStep=2;
            input.Clear(); Array.Clear(actions,0,actions.Length); SetCursor(false); RefreshInterface(); FocusSetupStep();
        }
        void SetSeatCount(int count)
        {
            // Structural product limits; never silently discard configured bots.
            if(phase!=Phase.Setup || count<1 || count>SeatInputCoordinator.SeatCount || count+botSetup.Count>NativeMatchRoster.MaximumParticipants) return;
            int previous=LocalSeatCount;
            // Extra-bot indices follow the view seats; preserve the participant being edited on Y join.
            if(rosterEditing>=previous)rosterEditing+=count-previous;
            else if(rosterEditing>=count)rosterEditing=-1;
            for(int i=count;i<previous;i++)identities.ClearSeat(i);
            input.SetActiveSeatCount(count);SyncHumanSeats();setupError=null; ApplyLayout(count); RefreshInterface();
            // Picker callbacks capture indices. Discard them after a structural roster change.
            if(count!=previous && rosterPicker!=null && rosterPicker.activeSelf)CloseRosterPicker();
        }
        void ApplyLayout(int count)
        {
            int renderedCount=diagnostic && probeCameraCount>0 ? Math.Min(probeCameraCount,count) : count;
            for(int i=0;i<cameras.Length;i++)
            {
                bool active=i<renderedCount;
                if(cameras[i])cameras[i].enabled=active&&(worldReady||phase!=Phase.Setup);
                if(viewportRoots[i]) viewportRoots[i].gameObject.SetActive(active);
                standings[i]?.Root.SetActive(false);
                if(!active || !cameras[i]) continue;
                var r=LocalSeatLayout.Viewport(renderedCount,i); cameras[i].rect=r;
                if(viewportRoots[i]) Layout(viewportRoots[i],new Vector2(r.xMin,r.yMin),new Vector2(r.xMax,r.yMax));
                if(standings[i]!=null) Layout((RectTransform)standings[i].Root.transform,
                    new Vector2(r.xMin,r.yMin+r.height*.12f),new Vector2(r.xMax,r.yMin+r.height*.88f));
            }
            if(persistentStandings!=null)
            {
                persistentStandings.Root.SetActive(false);
                var r=LocalSeatLayout.PersistentStandings(count);
                if(r.HasValue)
                {
                    var table=(RectTransform)persistentStandings.Root.transform;
                    Layout(table,new Vector2(r.Value.xMin,r.Value.yMin),new Vector2(r.Value.xMax,r.Value.yMax));
                    float seamGap=Profile.Get("ui.centerTimerSeamGap");
                    persistentStandings.TopInset=Profile.Get("ui.headingFontSize")+Profile.Get("ui.fontSize")+2*seamGap;
                }
            }
            float dividerWidth=Profile.Get("ui.viewportDividerWidth");
            if(verticalDivider)
            {
                verticalDivider.gameObject.SetActive(renderedCount>=2);
                verticalDivider.anchorMin=new Vector2(.5f,0);
                verticalDivider.anchorMax=new Vector2(.5f,1);
                verticalDivider.sizeDelta=new Vector2(dividerWidth,0);
                verticalDivider.anchoredPosition=Vector2.zero;
            }
            if(horizontalDivider)
            {
                horizontalDivider.gameObject.SetActive(renderedCount>=3);
                horizontalDivider.anchorMin=new Vector2(0,.5f);
                horizontalDivider.anchorMax=new Vector2(1,.5f);
                horizontalDivider.sizeDelta=new Vector2(0,dividerWidth);
                horizontalDivider.anchoredPosition=Vector2.zero;
            }
            if(timerPanel)
            {
                bool center=renderedCount>=3;
                timerPanel.anchorMin=timerPanel.anchorMax=center?new Vector2(.5f,.5f):new Vector2(.5f,1f);
                timerPanel.pivot=new Vector2(.5f,1f);
                // The four-seat timer touches the lower edge of the centered divider.
                float topInset=renderedCount==4?dividerWidth*.5f:Profile.Get(center?"ui.centerTimerSeamGap":"ui.fontSize");
                timerPanel.anchoredPosition=new Vector2(0,-topInset);
            }
        }
        void StepConfiguration(string path,int direction)
        {
            if(phase!=Phase.Setup) return;
            var d=MatchProfile.Descriptors.First(x=>x.Path==path); var config=Configuration;
            int value=path=="match.durationMinutes"?config.DurationMinutes:config.TargetPoints;
            value=(int)Mathf.Clamp(value+direction*d.Step,d.Minimum,d.Maximum);
            if(path=="match.durationMinutes") config.DurationMinutes=value; else config.TargetPoints=value;
            config.Validate(MatchProfile); Configuration=config; RefreshInterface();
        }
        void StepMouseSensitivity(int direction)
        {
            if(phase==Phase.Running)return;
            MouseSensitivityPreference.Step(Profile,direction);RefreshInterface();
        }
        void Pause(string reason) => Pause(reason,-1);
        void Pause(string reason,int seat)
        {
            BotDriver?.Release(); phase = Phase.Paused; pauseReason = reason; input.Clear(); Session.ClearInput(); gameAudio?.StopEffects(); Array.Clear(actions,0,actions.Length); SetCursor(false);
            if(UseSeatPauseMenus)
            {
                OpenSeatPause(seat>=0?seat:DefaultPauseSeat());
                EventSystem.current.sendNavigationEvents=false;
            }
            RefreshInterface();
            if(!UseSeatPauseMenus)Select(diagnostic||input.Ready ? resume : menu);
        }
        void Resume() { if (diagnostic || input.Ready) { BotDriver?.Release(); input.Clear(); Session.ClearInput(); phase = Phase.Running; SetCursor(!diagnostic && input.HasKeyboard); } }
        void ResetSetup() { Menu(); phase = Phase.Setup; diagnostic = false; input.Reset(); for(int i=0;i<SeatInputCoordinator.SeatCount;i++)identities.ClearSeat(i); Session.ClearInput(); Array.Clear(actions,0,actions.Length); SetCursor(false); RefreshInterface(); FocusSetupStep(); }
        static void Select(Selectable button) { if(EventSystem.current) { EventSystem.current.firstSelectedGameObject=button.gameObject; EventSystem.current.SetSelectedGameObject(button.gameObject); } }
        bool menuGamepadCursor;
        void UpdateMenuCursor()
        {
            if(phase==Phase.Running || phase==Phase.Loading)return;
            foreach(var pad in Gamepad.all)
            {
                if(!pad.enabled)continue;
                if(pad.leftStick.ReadValue().sqrMagnitude>=.25f || pad.dpad.ReadValue()!=Vector2.zero)
                    menuGamepadCursor=true;
                foreach(var control in pad.allControls)
                    if(control is UnityEngine.InputSystem.Controls.ButtonControl button && button.wasPressedThisFrame)
                        menuGamepadCursor=true;
            }
            // Mouse movement wins when both devices are used in the same frame.
            // Button releases and small stick drift do not change the cursor mode.
            var mouse=Mouse.current;
            if(mouse!=null && mouse.enabled && (mouse.delta.ReadValue()!=Vector2.zero || mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.scroll.ReadValue()!=Vector2.zero))
                menuGamepadCursor=false;
            Cursor.visible=!menuGamepadCursor;
        }
        void SetCursor(bool capture) { Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !capture && (phase==Phase.Running || !menuGamepadCursor); }
        void OnDestroy() { CancelHistoryLoading(); UnbindDamageBonusAlerts(); if(menuSubmitAction!=null)menuSubmitAction.performed-=RememberMenuDevice;if(menuMoveAction!=null)menuMoveAction.performed-=RememberMenuDevice;if(menuClickAction!=null)menuClickAction.performed-=RememberMenuDevice; Application.wantsToQuit-=ProtectLabQuit; gameAudio?.Dispose(); Session?.Stop(); presentation?.Dispose();foreach(var mesh in pickupMeshes)if(mesh)Destroy(mesh); if(damagePickupVisual)Destroy(damagePickupVisual);foreach(var item in healPickupVisuals)if(item)Destroy(item);healPickupVisuals.Clear();foreach(var item in armorPickupVisuals)if(item)Destroy(item);foreach(var item in speedPickupVisuals)if(item)Destroy(item); menuGamepadCursor=false; SetCursor(false); RestoreShadowQuality(); if(previousFixedDelta > 0) Time.fixedDeltaTime=previousFixedDelta; }
        readonly List<GameObject> healPickupVisuals=new List<GameObject>();
        void RebuildPickupVisuals()
        {
            foreach(var collection in new[]{healPickupVisuals,armorPickupVisuals,speedPickupVisuals,weaponPickupVisuals})
            {foreach(var visual in collection)if(visual){visual.SetActive(false);Destroy(visual);}collection.Clear();}
            foreach(var mesh in pickupMeshes)if(mesh)Destroy(mesh);pickupMeshes.Clear();
            if(damagePickupVisual){damagePickupVisual.SetActive(false);Destroy(damagePickupVisual);}
            CreateArmorPickupVisual();CreateSpeedPickupVisual();CreateDamagePickupVisual();CreateHealPickupVisual();CreateWeaponPickupVisuals();
        }
        void CreateHealPickupVisual()
        {
            if(!HealPickupPrefab)throw new InvalidOperationException("Missing manifest-owned heal prefab");
            foreach(var pickup in Session.HealPickups)
            {
                var visual=Instantiate(HealPickupPrefab,transform);visual.name=pickup.InstanceId+"-presentation";
                foreach(var collider in visual.GetComponentsInChildren<Collider>())Destroy(collider);
                healPickupVisuals.Add(visual);
            }
        }
        void RenderHealPickup()
        {
            if(Session==null)return;var states=Session.HealPickups;
            for(int i=0;i<healPickupVisuals.Count;i++)
            {
                var visual=healPickupVisuals[i];var pickup=states[i];visual.SetActive(pickup.Available);if(!pickup.Available)continue;
                visual.transform.position=pickup.Anchor+Vector3.up*Profile.Get("presentation.healPickupHoverHeight");
                visual.transform.rotation=Quaternion.Euler(0,(float)(Session.Time*Profile.Get("presentation.healPickupRotationDegreesPerSecond")),0);
                visual.transform.localScale=Vector3.one*Profile.Get("presentation.healPickupScale");
            }
        }
        void CreateArmorPickupVisual()
        {
            if(!ArmorPickupPrefab)throw new InvalidOperationException("Missing manifest-owned armor prefab");
            foreach(var pickup in Session.ArmorPickups)
            {
                var visual=Instantiate(ArmorPickupPrefab,transform);visual.name=pickup.InstanceId+"-presentation";
                foreach(var collider in visual.GetComponentsInChildren<Collider>())Destroy(collider);
                armorPickupVisuals.Add(visual);
            }
        }
        void RenderArmorPickup()
        {
            if(Session==null)return;var states=Session.ArmorPickups;
            for(int i=0;i<armorPickupVisuals.Count;i++)
            {
                var visual=armorPickupVisuals[i];var pickup=states[i];visual.SetActive(pickup.Available);if(!pickup.Available)continue;
                visual.transform.position=pickup.Anchor+Vector3.up*Profile.Get("presentation.armorPickupHoverHeight");
                visual.transform.rotation=Quaternion.Euler(0,(float)(Session.Time*Profile.Get("presentation.armorPickupRotationDegreesPerSecond")),0);
            }
        }

        void CreateDamagePickupVisual()
        {
            if(!DamagePickupPrefab) throw new InvalidOperationException("Missing manifest-owned damage pickup prefab");
            damagePickupVisual=Instantiate(DamagePickupPrefab,transform);damagePickupVisual.name="damage-pickup-presentation";
            foreach(var collider in damagePickupVisual.GetComponentsInChildren<Collider>())Destroy(collider);
        }
        void RenderDamagePickup()
        {
            if(!damagePickupVisual||Session==null)return;var pickup=Session.DamagePickup;
            damagePickupVisual.SetActive(Session.HasDamagePickup&&pickup.Available);if(!Session.HasDamagePickup||!pickup.Available)return;
            damagePickupVisual.transform.position=pickup.Anchor+Vector3.up*Profile.Get("presentation.armorPickupHoverHeight");
            damagePickupVisual.transform.rotation=Quaternion.Euler(0,(float)(Session.Time*Profile.Get("presentation.armorPickupRotationDegreesPerSecond")),0);
        }
        void CreateSpeedPickupVisual()
        {
            if(!SpeedPickupPrefab)throw new InvalidOperationException("Missing manifest-owned speed prefab");
            foreach(var pickup in Session.SpeedPickups)
            {
                var visual=Instantiate(SpeedPickupPrefab,transform);visual.name=pickup.InstanceId+"-presentation";
                foreach(var collider in visual.GetComponentsInChildren<Collider>())Destroy(collider);
                speedPickupVisuals.Add(visual);
            }
        }
        void RenderSpeedPickup()
        {
            if(Session==null)return;var states=Session.SpeedPickups;
            for(int i=0;i<speedPickupVisuals.Count;i++)
            {
                var visual=speedPickupVisuals[i];var pickup=states[i];visual.SetActive(pickup.Available);if(!pickup.Available)continue;
                visual.transform.position=pickup.Anchor+Vector3.up*Profile.Get("presentation.speedPickupHoverHeight");
                visual.transform.rotation=Quaternion.Euler(0,(float)(Session.Time*Profile.Get("presentation.speedPickupRotationDegreesPerSecond")),0);
            }
        }

        void CreateInterface()
        {
            var eventSystem = Owned("input-events"); eventSystem.AddComponent<EventSystem>();menuInputModule=eventSystem.AddComponent<InputSystemUIInputModule>();
            // Only intentional UI actions select the menu operator; pointer motion is not ownership.
            menuSubmitAction=menuInputModule.submit.action;menuMoveAction=menuInputModule.move.action;menuClickAction=menuInputModule.leftClick.action;
            menuSubmitAction.performed+=RememberMenuDevice;
            menuMoveAction.performed+=RememberMenuDevice;
            menuClickAction.performed+=RememberMenuDevice;
            var canvasObject = Owned("native-ui", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080); // Full HD UI design coordinate system, independent of gameplay/output resolution.
            for (int i=0;i<hud.Length;i++)
            {
                if(!cameras[i].enabled) continue;
                var root=new GameObject("seat-viewport-"+i,typeof(RectTransform)); root.transform.SetParent(canvasObject.transform,false);
                viewportRoots[i]=(RectTransform)root.transform;
                var effect=new GameObject("damage-vignette-"+i,typeof(RectTransform),typeof(CanvasRenderer),typeof(DamageVignette));
                effect.transform.SetParent(viewportRoots[i],false);
                damageVignettes[i]=effect.GetComponent<DamageVignette>();
                Layout(damageVignettes[i].rectTransform,Vector2.zero,Vector2.one);
                var rect = new Rect(0,0,1,1);
                hud[i] = TextElement(viewportRoots[i], "seat-hud-"+i, "", (int)Profile.Get("ui.fontSize"));
                Layout(hud[i].rectTransform,new Vector2(rect.xMin,rect.yMin),new Vector2(rect.xMax,rect.yMax));
                float padding=Profile.Get("ui.fontSize");
                hud[i].rectTransform.offsetMin=Vector2.one*padding;hud[i].rectTransform.offsetMax=-Vector2.one*padding;
                hud[i].alignment = TextAnchor.UpperLeft;
                CreateDamageBonusNotice(i);
                int indicatorHeight=(int)Profile.Get("ui.indicatorVisualHeight");
                health[i]=CreateHudIndicator(viewportRoots[i],"health-"+i,HudIndicatorIcon.IconKind.Heart,new Color32(255,72,72,255),false,padding,indicatorHeight,out _);
                armor[i]=CreateHudIndicator(viewportRoots[i],"armor-"+i,HudIndicatorIcon.IconKind.Shield,new Color32(55,234,255,255),false,padding*2+indicatorHeight,indicatorHeight,out _);
                armorGroups[i]=armor[i].transform.parent.gameObject;
                ammo[i]=CreateHudIndicator(viewportRoots[i],"ammo-"+i,HudIndicatorIcon.IconKind.Cartridges,Color.white,true,padding,indicatorHeight,out ammoIcons[i]);
                names[i]=TextElement(viewportRoots[i],"seat-name-"+i,"",(int)Profile.Get("ui.nameFontSize"));
                Layout(names[i].rectTransform,Vector2.zero,Vector2.one);
                names[i].rectTransform.offsetMin=Vector2.one*padding;
                names[i].rectTransform.offsetMax=-Vector2.one*padding;
                names[i].alignment=TextAnchor.LowerCenter;
                var cross = TextElement(viewportRoots[i],"crosshair-"+i,"+",(int)Profile.Get("ui.headingFontSize"));
                Layout(cross.rectTransform,new Vector2(rect.xMin,rect.yMin),new Vector2(rect.xMax,rect.yMax)); cross.alignment=TextAnchor.MiddleCenter; crosshair[i]=cross;
                var notice=TextElement(viewportRoots[i],"kill-notice-"+i,"",(int)Profile.Get("ui.killNoticeFontSize"));
                Layout(notice.rectTransform,new Vector2(0,Profile.Get("ui.killNoticeBottom")),new Vector2(1,Profile.Get("ui.killNoticeTop")));
                notice.alignment=TextAnchor.MiddleCenter;notice.color=Color.white;notice.supportRichText=true;killNotice[i]=notice;
                var outline=notice.gameObject.AddComponent<Outline>();outline.effectColor=new Color(0,0,0,.9f);
            }
            for(int i=0;i<standings.Length;i++)
            {
                var r=cameras[i].rect;
                standings[i]=new NativeStandingsView(canvasObject.transform,font,(int)Profile.Get("ui.fontSize"),"standings-"+i,
                    new Vector2(r.xMin,r.yMin+r.height*.12f),new Vector2(r.xMax,r.yMin+r.height*.88f),Profile);
                standings[i].FitToContent=true;standings[i].Root.SetActive(false);
            }
            persistentStandings=new NativeStandingsView(canvasObject.transform,font,(int)Profile.Get("ui.fontSize"),"persistent-standings",new Vector2(.5f,0),new Vector2(1,.5f),Profile);
            persistentStandings.Root.SetActive(false);
            verticalDivider=CreateViewportDivider(canvasObject.transform,"viewport-divider-vertical");
            horizontalDivider=CreateViewportDivider(canvasObject.transform,"viewport-divider-horizontal");
            var timerObject=new GameObject("match-timer-panel",typeof(RectTransform),typeof(Image));
            timerObject.transform.SetParent(canvasObject.transform,false);timerPanel=(RectTransform)timerObject.transform;
            timerObject.GetComponent<Image>().color=new Color32(7,21,37,255);
            timerObject.GetComponent<Image>().raycastTarget=false;
            matchTimer=TextElement(timerObject.transform,"match-timer","",(int)Profile.Get("ui.headingFontSize"));
            Layout(matchTimer.rectTransform,Vector2.zero,Vector2.one);
            matchTimer.alignment=TextAnchor.MiddleCenter;
            timerObject.SetActive(false);
            overlay = new GameObject("setup-pause",typeof(RectTransform),typeof(Image)); overlay.transform.SetParent(canvasObject.transform,false);
            Layout((RectTransform)overlay.transform,Vector2.zero,Vector2.one); overlay.GetComponent<Image>().color=new Color32(10,18,30,245);
            var column = new GameObject("menu",typeof(RectTransform),typeof(VerticalLayoutGroup)); column.transform.SetParent(overlay.transform,false);
            Layout((RectTransform)column.transform,new Vector2(.15f,.15f),new Vector2(.85f,.85f));
            var layout=column.GetComponent<VerticalLayoutGroup>(); layout.childControlHeight=true; layout.childForceExpandHeight=true;
            status=TextElement(column.transform,"status","",(int)Profile.Get("ui.headingFontSize")); status.alignment=TextAnchor.MiddleCenter;
            start=ButtonElement(column.transform,"Начать — четыре игрока",()=>QueueMatch(false));
            seatsMinus=ButtonElement(column.transform,"Игроки −",()=>SetSeatCount(LocalSeatCount-1)); seatsMinus.gameObject.name="seats-minus";
            seatsPlus=ButtonElement(column.transform,"Экраны +",()=>SetSeatCount(LocalSeatCount+1)); seatsPlus.gameObject.name="seats-plus";
            modeButton=ButtonElement(column.transform,"Режим",()=>SetMatchMode(SetupMode==NativeMatchMode.Ffa?NativeMatchMode.Teams:NativeMatchMode.Ffa));modeButton.gameObject.name="match-mode";
            arenaButton=ButtonElement(column.transform,"Арена",()=>SelectAuthoredMap(AuthoredArenaCatalog.Next(SelectedMapId)));arenaButton.gameObject.name="authored-map";
            teamRow=new GameObject("team-assignments",typeof(RectTransform),typeof(HorizontalLayoutGroup));teamRow.transform.SetParent(column.transform,false);
            var teamLayout=teamRow.GetComponent<HorizontalLayoutGroup>();teamLayout.childControlWidth=teamLayout.childControlHeight=true;teamLayout.childForceExpandWidth=teamLayout.childForceExpandHeight=true;
            for(int i=0;i<teamButtons.Length;i++) { int seat=i;teamButtons[i]=ButtonElement(teamRow.transform,"Команда P"+(i+1),()=>SetTeam(seat,teamAssignments[seat]==NativeTeam.TeamA?NativeTeam.TeamB:NativeTeam.TeamA));teamButtons[i].gameObject.name="team-seat-"+i; }
            resume=ButtonElement(column.transform,"Продолжить",Resume);
            fallbackSettings=ButtonElement(column.transform,"Настройки",()=>OpenSettings(-1));operatorSettingsButton=fallbackSettings;
            fallbackSettings.gameObject.name="fallback-settings";
            repeat=ButtonElement(column.transform,"Повторить матч",Repeat);
            menu=ButtonElement(column.transform,"В главное меню",ToMainMenu);
            rebind=ButtonElement(column.transform,"Назначить устройства заново",ResetSetup);
            diagnosticButton=ButtonElement(column.transform,"Диагностика четырёх камер · без управления",()=>QueueMatch(true));
            durationButton=ButtonElement(column.transform,"Длительность",()=>StepConfiguration("match.durationMinutes",1));
            durationButton.gameObject.name="duration-plus";
            var durationMinus=ButtonElement(column.transform,"−",()=>StepConfiguration("match.durationMinutes",-1)); durationMinus.gameObject.name="duration-minus";
            targetButton=ButtonElement(column.transform,"Цель",()=>{ if(phase!=Phase.Setup)return; var c=Configuration;c.TargetEnabled=true;Configuration=c;RefreshInterface(); });
            targetButton.gameObject.name="target-toggle";
            targetMinus=ButtonElement(column.transform,"Цель −",()=>StepConfiguration("match.targetPoints",-1));
            targetPlus=ButtonElement(column.transform,"Цель +",()=>StepConfiguration("match.targetPoints",1));
            resultsFrame=new GameObject("results-frame",typeof(RectTransform)).GetComponent<RectTransform>();
            resultsFrame.SetParent(overlay.transform,false);resultsFrame.anchorMin=resultsFrame.anchorMax=resultsFrame.pivot=new Vector2(.5f,.5f);
            resultsFrame.sizeDelta=new Vector2(1920,1080);
            results=new NativeStandingsView(resultsFrame,font,(int)Profile.Get("ui.fontSize"),"results-table",new Vector2(.06f,.42f),new Vector2(.94f,.94f),Profile){AllowSelection=true};
            results.Root.SetActive(false);
            achievementsView=new NativeAchievementsView(resultsFrame,font,(int)Profile.Get("ui.fontSize"));
            var settings = ButtonElement(column.transform,"Настройки · Показывать FPS: вкл",()=>fps.Toggle());
            settings.gameObject.name = "fps-setting";settings.gameObject.SetActive(false);
            fps = canvasObject.AddComponent<FpsDisplay>();
            fps.Initialize(font, (int)Profile.Get("ui.fontSize"), settings.GetComponentInChildren<Text>());
            mouseSensitivityMinus=ButtonElement(column.transform,"Чувствительность мыши −",()=>StepMouseSensitivity(-1));mouseSensitivityMinus.gameObject.name="mouse-sensitivity-minus";
            mouseSensitivityPlus=ButtonElement(column.transform,"Чувствительность мыши +",()=>StepMouseSensitivity(1));mouseSensitivityPlus.gameObject.name="mouse-sensitivity-plus";
            setupBack=ButtonElement(column.transform,"Назад к главному меню",ToMainMenu);
            CreateBotSetupInterface();
            CreateSeatSetupInterface();
            CreateModernMenuUi(overlay.transform);
            CreateSeatPauseUi();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // One global UI label replaces Unity's per-camera watermark while retaining Development Build diagnostics.
            UnityEngine.Rendering.Watermark.showDeveloperWatermark=false;
            int buildFontSize=(int)Profile.Get("ui.fontSize");
            var buildLabel=TextElement(canvasObject.transform,"development-build-label","Development Build",buildFontSize);
            buildLabel.fontStyle=FontStyle.Bold;
            buildLabel.alignment=TextAnchor.UpperLeft;
            buildLabel.horizontalOverflow=HorizontalWrapMode.Overflow;buildLabel.verticalOverflow=VerticalWrapMode.Overflow;
            var buildRect=buildLabel.rectTransform;
            buildRect.anchorMin=buildRect.anchorMax=buildRect.pivot=new Vector2(0,1);
            buildRect.anchoredPosition=new Vector2(buildFontSize,-buildFontSize);
            buildRect.sizeDelta=new Vector2(buildLabel.preferredWidth,buildLabel.preferredHeight);
#endif
            ApplyLayout(LocalSeatCount); RefreshInterface(); Select(rebind);
        }
        void CreateSeatSetupInterface()
        {
            seatPanel=new GameObject("seat-setup",typeof(RectTransform),typeof(VerticalLayoutGroup));seatPanel.transform.SetParent(overlay.transform,false);
            Layout((RectTransform)seatPanel.transform,new Vector2(.53f,.63f),new Vector2(.97f,.94f));
            var layout=seatPanel.GetComponent<VerticalLayoutGroup>();layout.childControlHeight=layout.childForceExpandHeight=true;
            var title=TextElement(seatPanel.transform,"seat-title","ЭКРАНЫ · ЧЕЛОВЕК / AI",(int)Profile.Get("ui.headingFontSize"));title.alignment=TextAnchor.MiddleCenter;
            for(int i=0;i<seatKinds.Length;i++)
            {
                int seat=i;var row=new GameObject("seat-choice-"+i,typeof(RectTransform),typeof(HorizontalLayoutGroup));row.transform.SetParent(seatPanel.transform,false);
                var horizontal=row.GetComponent<HorizontalLayoutGroup>();horizontal.childControlHeight=horizontal.childControlWidth=horizontal.childForceExpandHeight=horizontal.childForceExpandWidth=true;
                seatKinds[i]=ButtonElement(row.transform,"",()=>SetSeatAi(seat,!botSetup.IsAi(seat)));seatKinds[i].name="seat-kind-"+i;
                seatDifficulties[i]=ButtonElement(row.transform,"",()=>SetSeatDifficulty(seat,(botSetup.SeatDifficulty(seat)+1)%3));seatDifficulties[i].name="seat-difficulty-"+i;
            }
        }
        void CreateBotSetupInterface()
        {
            botPanel=new GameObject("bot-setup",typeof(RectTransform),typeof(VerticalLayoutGroup));botPanel.transform.SetParent(overlay.transform,false);
            Layout((RectTransform)botPanel.transform,new Vector2(.53f,.04f),new Vector2(.97f,.60f));
            var layout=botPanel.GetComponent<VerticalLayoutGroup>();layout.childControlHeight=true;layout.childForceExpandHeight=true;
            var title=TextElement(botPanel.transform,"bot-title","ДОПОЛНИТЕЛЬНЫЕ БОТЫ · лимит 8",(int)Profile.Get("ui.headingFontSize"));title.alignment=TextAnchor.MiddleCenter;
            addBot=ButtonElement(botPanel.transform,"Добавить бота",AddBot);addBot.name="bot-add";
            for(int i=0;i<botRows.Length;i++)
            {
                int index=i;var row=new GameObject("bot-row-"+i,typeof(RectTransform),typeof(VerticalLayoutGroup));row.transform.SetParent(botPanel.transform,false);botRows[i]=row;
                var vertical=row.GetComponent<VerticalLayoutGroup>();vertical.childControlHeight=vertical.childForceExpandHeight=true;
                botDifficulty[i]=ButtonElement(row.transform,"Сложность",()=>SetBotDifficulty(index,(botSetup.At(index).Difficulty+1)%3));botDifficulty[i].name="bot-difficulty-"+i;
                var controls=new GameObject("controls",typeof(RectTransform),typeof(HorizontalLayoutGroup));controls.transform.SetParent(row.transform,false);
                var horizontal=controls.GetComponent<HorizontalLayoutGroup>();horizontal.childControlHeight=horizontal.childControlWidth=horizontal.childForceExpandHeight=horizontal.childForceExpandWidth=true;
                botTeams[i]=ButtonElement(controls.transform,"Команда",()=>SetBotTeam(index,botSetup.At(index).Team==NativeTeam.TeamA?NativeTeam.TeamB:NativeTeam.TeamA));botTeams[i].name="bot-team-"+i;
                ButtonElement(controls.transform,"Удалить",()=>RemoveBot(index)).name="bot-remove-"+i;
            }
        }
        Text TextElement(Transform parent,string name,string value,int size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Text)); go.transform.SetParent(parent,false);
            var text=go.GetComponent<Text>(); text.text=value; text.font=font; text.fontSize=size; text.color=Color.white; text.raycastTarget=false; return text;
        }
        Text CreateHudIndicator(Transform parent,string name,HudIndicatorIcon.IconKind kind,Color tint,bool right,float bottom,int height,out HudIndicatorIcon icon)
        {
            var group=new GameObject(name+"-group",typeof(RectTransform));group.transform.SetParent(parent,false);
            var rect=(RectTransform)group.transform;
            rect.anchorMin=rect.anchorMax=right?new Vector2(1,0):Vector2.zero;
            rect.pivot=right?new Vector2(1,0):Vector2.zero;
            rect.anchoredPosition=new Vector2(right?-Profile.Get("ui.fontSize"):Profile.Get("ui.fontSize"),bottom);
            rect.sizeDelta=new Vector2(height*5,height);
            var iconObject=new GameObject(name+"-icon",typeof(RectTransform),typeof(HudIndicatorIcon));
            iconObject.transform.SetParent(group.transform,false);
            icon=iconObject.GetComponent<HudIndicatorIcon>();icon.Kind=kind;icon.color=tint;icon.raycastTarget=false;
            var iconRect=(RectTransform)iconObject.transform;
            iconRect.anchorMin=iconRect.anchorMax=iconRect.pivot=Vector2.zero;
            iconRect.anchoredPosition=Vector2.zero;iconRect.sizeDelta=Vector2.one*height;
            // LegacyRuntime.ttf's digit cap height is smaller than its nominal point size;
            // this font-metric compensation makes digit ink match the profile-owned icon height.
            var value=TextElement(group.transform,name,"",Mathf.RoundToInt(height*1.1f));
            var valueRect=value.rectTransform;
            valueRect.anchorMin=valueRect.anchorMax=valueRect.pivot=Vector2.zero;
            valueRect.anchoredPosition=new Vector2(height+Profile.Get("ui.fontSize")*.5f,0);
            valueRect.sizeDelta=new Vector2(height*4,height);
            value.alignment=TextAnchor.MiddleLeft;value.verticalOverflow=VerticalWrapMode.Overflow;
            value.horizontalOverflow=HorizontalWrapMode.Overflow;value.color=tint;
            return value;
        }
        void ResizeHudIndicator(Text value)
        {
            var rect=(RectTransform)value.transform.parent;
            rect.sizeDelta=new Vector2(value.rectTransform.anchoredPosition.x+value.preferredWidth,rect.sizeDelta.y);
        }
        Button ButtonElement(Transform parent,string title,UnityEngine.Events.UnityAction action)
        {
            var go=new GameObject(title,typeof(RectTransform),typeof(Image),typeof(Button)); go.transform.SetParent(parent,false);
            go.GetComponent<Image>().color=MenuCard;RoundRosterPanel(go);
            var button=go.GetComponent<Button>(); button.onClick.AddListener(()=>{if(go.name.Contains("back")||go.name.Contains("cancel")||title.StartsWith("‹"))gameAudio?.MenuBack();else gameAudio?.MenuConfirm();action();lastAudioSelection=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;});
            var label=TextElement(go.transform,"label",title,(int)Profile.Get("ui.fontSize")); Layout(label.rectTransform,Vector2.zero,Vector2.one); label.alignment=TextAnchor.MiddleCenter;
            label.rectTransform.offsetMin=new Vector2(12,4);label.rectTransform.offsetMax=new Vector2(-12,-4);
            var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=colors.selectedColor=Color.white;colors.pressedColor=new Color(.8f,.8f,.8f,1);colors.disabledColor=new Color(.52f,.57f,.62f,1);colors.fadeDuration=.12f;button.colors=colors;
            go.AddComponent<MenuPresentation>();return button;
        }
        static void Layout(RectTransform rect,Vector2 min,Vector2 max) { rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero; }
        static RectTransform CreateViewportDivider(Transform parent,string name)
        {
            var divider=new GameObject(name,typeof(RectTransform),typeof(Image));
            divider.transform.SetParent(parent,false);
            var image=divider.GetComponent<Image>();image.color=Color.black;image.raycastTarget=false;
            divider.SetActive(false);
            return (RectTransform)divider.transform;
        }
        void CompactMatchMenu(RectTransform menuRect)
        {
            if(phase!=Phase.Paused && phase!=Phase.Results)return;
            float width=Profile.Get("ui.matchMenuWidth"),height=Profile.Get("ui.matchMenuButtonHeight"),spacing=Profile.Get("ui.matchMenuSpacing");
            var group=menuRect.GetComponent<VerticalLayoutGroup>();
            if(phase==Phase.Results)
            {
                group.enabled=false;bool reconnectNotice=!repeat.interactable;
                status.gameObject.SetActive(reconnectNotice);
                float noticeHeight=reconnectNotice?height*.8f:0;
                float totalHeight=height+noticeHeight;
                if(reconnectNotice)
                {
                    status.text="Подключите устройства для повтора или вернитесь в меню";
                    Layout(status.rectTransform,new Vector2(0,height/totalHeight),Vector2.one);
                }
                foreach(var button in new[]{repeat,menu})
                {
                    var element=button.GetComponent<LayoutElement>()??button.gameObject.AddComponent<LayoutElement>();element.ignoreLayout=true;
                    var rect=(RectTransform)button.transform;
                    float x=button==repeat?0:.5f;
                    Layout(rect,new Vector2(x,0),new Vector2(x+.5f,height/totalHeight));rect.offsetMin=new Vector2(button==repeat?0:spacing*.5f,0);rect.offsetMax=new Vector2(button==repeat?-spacing*.5f:0,0);
                }
                menuRect.sizeDelta=new Vector2(width*1.8f,totalHeight);
                NativeAchievementsView.PlaceResultsActionsAtBottom(menuRect);
                results.ConfigureResultNavigation(repeat,menu);return;
            }
            status.gameObject.SetActive(true);group.enabled=true;
            foreach(var button in new[]{repeat,menu})if(button.GetComponent<LayoutElement>())button.GetComponent<LayoutElement>().ignoreLayout=false;
            repeat.navigation=menu.navigation=new Navigation{mode=Navigation.Mode.Automatic};
            group.childForceExpandHeight=false;group.spacing=spacing;
            int buttons=0;
            foreach(var button in menuRect.GetComponentsInChildren<Button>())
            {
                var element=button.GetComponent<LayoutElement>()??button.gameObject.AddComponent<LayoutElement>();
                element.minHeight=element.preferredHeight=height;element.flexibleHeight=0;buttons++;
            }
            status.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,width);
            float statusHeight=status.preferredHeight;
            var title=status.GetComponent<LayoutElement>()??status.gameObject.AddComponent<LayoutElement>();
            title.minHeight=title.preferredHeight=statusHeight;title.flexibleHeight=0;
            // The title uses its measured wrapped text height; action sizes come from the UI profile.
            menuRect.sizeDelta=new Vector2(width,statusHeight+buttons*(height+spacing));
            if(phase==Phase.Results)NativeAchievementsView.PlaceResultsActionsAtBottom(menuRect);
            else Layout(menuRect,new Vector2(.5f,.5f),new Vector2(.5f,.5f));
        }
        void RefreshInterface()
        {
            if (!overlay) return;
            fps.SetMatchActive(phase==Phase.Running || phase==Phase.Paused || phase==Phase.Settings&&settingsReturn==Phase.Paused);
            RefreshModernMenuUi();
            overlay.SetActive(phase!=Phase.Running && !(phase==Phase.Paused && UseSeatPauseMenus));
            start.gameObject.SetActive(phase==Phase.Setup); start.interactable=input.Ready && IdentitiesReady() && ValidSetup();
            start.GetComponentInChildren<Text>().text="Начать · людей: "+botSetup.HumanCount(LocalSeatCount)+" · AI: "+(LocalSeatCount-botSetup.HumanCount(LocalSeatCount)+botSetup.Count);
            seatsMinus.gameObject.SetActive(phase==Phase.Setup); seatsMinus.interactable=LocalSeatCount>1;
            seatsPlus.gameObject.SetActive(phase==Phase.Setup); seatsPlus.interactable=LocalSeatCount<SeatInputCoordinator.SeatCount && LocalSeatCount+botSetup.Count<NativeMatchRoster.MaximumParticipants;
            seatsMinus.GetComponentInChildren<Text>().text="Экранов: "+LocalSeatCount+"   −";
            diagnosticButton.GetComponentInChildren<Text>().text="Диагностика: "+LocalSeatCount+" камеры · без управления";
            fallbackSettings.gameObject.SetActive(phase==Phase.Paused);
            repeat.GetComponentInChildren<Text>().text=phase==Phase.Paused?"Начать заново":"Повторить матч";
            menu.GetComponentInChildren<Text>().text=phase==Phase.Paused?"Выйти в главное меню":"В главное меню";
            resume.gameObject.SetActive(phase==Phase.Paused); resume.interactable=diagnostic||input.Ready;
            repeat.gameObject.SetActive((phase==Phase.Paused)||phase==Phase.Results); repeat.interactable=diagnostic||input.Ready;
            menu.gameObject.SetActive((phase==Phase.Paused)||phase==Phase.Results);
            modeButton.gameObject.SetActive(phase==Phase.Setup);
            modeButton.GetComponentInChildren<Text>().text="Режим: "+(SetupMode==NativeMatchMode.Teams?"Team A / Team B":"Каждый сам за себя");
            arenaButton.gameObject.SetActive(phase==Phase.Setup);
            arenaButton.GetComponentInChildren<Text>().text="Сменить карту · "+AuthoredArenaCatalog.Name(SelectedMapId)+" · 2–"+AuthoredArenaCatalog.Maximum(SelectedMapId);
            teamRow.SetActive(phase==Phase.Setup && SetupMode==NativeMatchMode.Teams);
            for(int i=0;i<teamButtons.Length;i++)
            {
                teamButtons[i].gameObject.SetActive(i<LocalSeatCount);
                teamButtons[i].GetComponentInChildren<Text>().text="P"+(i+1)+" · "+NativeStandingsView.TeamName(teamAssignments[i]);
                teamButtons[i].GetComponent<Image>().color=NativeStandingsView.TeamColor(teamAssignments[i],swappedTeamColors);
            }
            rebind.gameObject.SetActive(phase==Phase.Setup);
            setupBack.gameObject.SetActive(phase==Phase.Setup);
            diagnosticButton.gameObject.SetActive(phase==Phase.Setup);diagnosticButton.interactable=ValidSetup();
            durationButton.gameObject.SetActive(phase==Phase.Setup);
            durationButton.transform.parent.Find("duration-minus").gameObject.SetActive(phase==Phase.Setup);
            targetButton.gameObject.SetActive(phase==Phase.Setup);
            targetMinus.gameObject.SetActive(phase==Phase.Setup && Configuration.TargetEnabled);
            targetPlus.gameObject.SetActive(phase==Phase.Setup && Configuration.TargetEnabled);
            var sensitivityDescriptor=MouseSensitivityPreference.Descriptor(Profile);var sensitivity=MouseSensitivityPreference.Resolve(Profile);
            mouseSensitivityMinus.gameObject.SetActive(false);mouseSensitivityPlus.gameObject.SetActive(false);
            status.transform.parent.Find("fps-setting").gameObject.SetActive(false);
            mouseSensitivityMinus.interactable=sensitivity>sensitivityDescriptor.Minimum;
            mouseSensitivityPlus.interactable=sensitivity<sensitivityDescriptor.Maximum;
            mouseSensitivityMinus.GetComponentInChildren<Text>().text=sensitivityDescriptor.Label+": "+sensitivity.ToString("0.00")+" "+sensitivityDescriptor.Unit+"   −";
            mouseSensitivityPlus.GetComponentInChildren<Text>().text=sensitivityDescriptor.Label+": "+sensitivity.ToString("0.00")+" "+sensitivityDescriptor.Unit+"   +";
            durationButton.GetComponentInChildren<Text>().text="Длительность: "+Configuration.DurationMinutes+" мин   +";
            targetButton.GetComponentInChildren<Text>().text="Цель: "+(Configuration.TargetEnabled?Configuration.TargetPoints+" очков":"выключена");
            var resultMenu=(RectTransform)status.transform.parent;
            Transform resultParent=phase==Phase.Results?resultsFrame:overlay.transform;
            if(resultMenu.parent!=resultParent)resultMenu.SetParent(resultParent,false);
            results.Show(phase==Phase.Results,Session.Match?.Read(),diagnostic||combatReview,Composition,phase==Phase.Results?Session.LifeStates:null);
            achievementsView.ContentLayout=true;
            achievementsView.Show(phase==Phase.Results,Session.Match?.Read(),Composition);
            var menuRect=(RectTransform)status.transform.parent;
            Layout(menuRect,new Vector2(phase==Phase.Setup?.04f:.15f,phase==Phase.Results?.11f:.08f),new Vector2(phase==Phase.Setup?.51f:.85f,phase==Phase.Results?.11f:.92f));
            if(seatPanel)
            {
                seatPanel.SetActive(phase==Phase.Setup);
                for(int i=0;i<seatKinds.Length;i++)
                {
                    seatKinds[i].transform.parent.gameObject.SetActive(i<LocalSeatCount);
                    seatKinds[i].GetComponentInChildren<Text>().text="Экран "+(i+1)+" · "+(botSetup.IsAi(i)?"AI":"Человек")+"  ›";
                    seatDifficulties[i].gameObject.SetActive(botSetup.IsAi(i));
                    seatDifficulties[i].GetComponentInChildren<Text>().text=new[]{"Салага","Боец","Ветеран"}[botSetup.SeatDifficulty(i)]+"  ›";
                }
            }
            if(botPanel)
            {
                botPanel.SetActive(phase==Phase.Setup);addBot.interactable=botSetup.CanAdd(LocalSeatCount);
                for(int i=0;i<botRows.Length;i++)
                {
                    botRows[i].SetActive(i<botSetup.Count);if(i>=botSetup.Count)continue;
                    var bot=botSetup.At(i);botDifficulty[i].GetComponentInChildren<Text>().text=bot.Name+" · "+new[]{"Салага","Боец","Ветеран"}[bot.Difficulty]+"  ›";
                    botTeams[i].gameObject.SetActive(SetupMode==NativeMatchMode.Teams);
                    botTeams[i].GetComponentInChildren<Text>().text=NativeStandingsView.TeamName(bot.Team);
                    botTeams[i].GetComponent<Image>().color=NativeStandingsView.TeamColor(bot.Team,swappedTeamColors);
                }
            }
            status.text=(phase==Phase.Paused ? "Пауза" : "Space / Y — присоединить человека · Esc — пауза")+"\n"+
                string.Join("  |  ",Enumerable.Range(0,LocalSeatCount).Select(i=>"P"+(i+1)+": "+(!input.IsHumanSeat(i)?"AI":input.IsConnected(i)?IdentityLabel(i):"ожидает")));
            if(phase==Phase.Paused)status.text=DefaultPauseSeat()<0?"Пауза":"Пауза\n"+IdentityLabel(DefaultPauseSeat());
            if(phase==Phase.Setup && !ValidSetup()) status.text+=LocalSeatCount+botSetup.Count<2?"\nДобавьте бота или второго игрока":"\nНужны обе команды: Team A и Team B";
            if(phase==Phase.Setup && setupError!=null) status.text+="\n"+setupError;
            if(phase==Phase.Results)
            {
                var result=Session.Match.Read();
                status.text=(result.Roster.Mode==NativeMatchMode.Teams ? "ПОБЕДИЛА "+NativeStandingsView.TeamName(result.WinnerTeam) : "ПОБЕДИЛ "+Composition.Participant(result.Winner).Name)+" · "+(result.Trigger=="score-limit"?"ЦЕЛЬ ПО ОЧКАМ":"ВРЕМЯ")+(input.Ready||diagnostic?"":"\nПодключите устройства для повтора или вернитесь в меню");
            }
            CompactMatchMenu(menuRect);
            RefreshModernMenuUi();
            RefreshSeatPauseUi();
            StyleOperatorPause();
            if(phase==Phase.Results)
            {
                achievementsView.CenterResultsContent(menuRect,results);
                results.Show(true,Session.Match.Read(),diagnostic||combatReview,Composition,Session.LifeStates);
            }
            else achievementsView.PositionResultsTable(results);
        }
    }
}
