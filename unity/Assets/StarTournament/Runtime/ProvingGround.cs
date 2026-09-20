using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace StarTournament.ProvingGround
{
    public sealed class ProvingGround : MonoBehaviour
    {
        public GameObject RobotPrefab, WorldWeaponPrefab, ViewWeaponPrefab;
        public GameObject TrooperBodyPrefab, TrooperArmsPrefab;
        public AnimationClip[] TrooperClips;
        public ProvingProfile TrooperProfile = ProvingProfile.CreateTrooperDefault();
        public ProvingProfile Profile = ProvingProfile.CreateDefault();
        public ProvingProfile ArenaProfile = ProvingProfile.CreateArenaFoundationDefault();
        readonly SeatInputCoordinator input = new SeatInputCoordinator();
        CharacterMotor[] motors = Array.Empty<CharacterMotor>();
        readonly Camera[] cameras = new Camera[SeatInputCoordinator.SeatCount];
        readonly Text[] hud = new Text[SeatInputCoordinator.SeatCount];
        public ProvingProfile LifeProfile = ProvingProfile.CreateCombatDefault();
        public ProvingProfile CombatProfile = ProvingProfile.CreateNativeCombatDefault();
        GameObject[] bodies = Array.Empty<GameObject>();
        readonly GameObject[] views = new GameObject[SeatInputCoordinator.SeatCount];
        readonly Text[] health = new Text[SeatInputCoordinator.SeatCount], ammo = new Text[SeatInputCoordinator.SeatCount], crosshair = new Text[SeatInputCoordinator.SeatCount];
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
        public ProvingProfile RosterProfile = ProvingProfile.CreateRosterDefault();
        ProvingProfile frozenRosterProfile;
        public NativeMatchComposition Composition { get; private set; }
        NativeMatchComposition reviewComposition;
        bool reviewShowStandings;
        public ProvingProfile TeamProfile = ProvingProfile.CreateTeamDefault();
        public ProvingProfile MatchProfile = ProvingProfile.CreateMatchDefault();
        public NativeMatchConfiguration Configuration { get; private set; }
        ProvingProfile frozenMovement, frozenLife, frozenCombat, frozenMatch, frozenTrooper;
        ProvingProfile previewMovement,previewLife,previewCombat,previewTrooper;
        NativeMatchConfiguration frozenConfiguration;
        NativeMatchRoster frozenRoster;
        ProvingProfile frozenTeam;
        bool swappedTeamColors, frozenSwappedColors;
        public NativeMatchMode SetupMode { get; private set; }
        readonly NativeTeam[] teamAssignments={NativeTeam.TeamA,NativeTeam.TeamB,NativeTeam.TeamA,NativeTeam.TeamB};
        Button modeButton, colorButton;
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
            botSetup.SetAi(seat,ai);input.SetHumanSeat(seat,!ai);setupError=null;RefreshInterface();
        }
        public void SetSeatDifficulty(int seat,int difficulty)
        {
            if(phase!=Phase.Setup)return;
            botSetup.SetSeatDifficulty(seat,difficulty);RefreshInterface();
        }
        void SyncHumanSeats()
        {
            for(int seat=0;seat<LocalSeatCount;seat++)input.SetHumanSeat(seat,!botSetup.IsAi(seat));
        }
        public NativeMatchComposition SetupComposition()=>botSetup.Build(LocalSeatCount,SetupMode,teamAssignments,swappedTeamColors);
        public void AddBot(){if(phase!=Phase.Setup||!botSetup.CanAdd(LocalSeatCount))return;botSetup.Add(LocalSeatCount);setupError=null;RefreshInterface();}
        public void RemoveBot(int index){if(phase!=Phase.Setup)return;botSetup.Remove(index);setupError=null;RefreshInterface();Select(botSetup.Count>0?botDifficulty[Math.Min(index,botSetup.Count-1)]:addBot);}
        public void SetBotDifficulty(int index,int value){if(phase!=Phase.Setup)return;botSetup.SetDifficulty(index,value);RefreshInterface();}
        public void SetBotTeam(int index,NativeTeam value){if(phase!=Phase.Setup)return;botSetup.SetTeam(index,value);setupError=null;RefreshInterface();}
        public void SetMatchMode(NativeMatchMode mode)
        {
            if(phase!=Phase.Setup) return;
            if(mode!=NativeMatchMode.Ffa && mode!=NativeMatchMode.Teams) throw new ArgumentException("Invalid mode");
            SetupMode=mode;setupError=null;RefreshInterface();
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
            try { if(reviewComposition==null)SetupRoster();return true; } catch(ArgumentException) { return false; }
        }
        readonly Text[] timers = new Text[SeatInputCoordinator.SeatCount];
        readonly NativeStandingsView[] standings = new NativeStandingsView[SeatInputCoordinator.SeatCount];
        NativeStandingsView results, persistentStandings;
        readonly RectTransform[] viewportRoots=new RectTransform[SeatInputCoordinator.SeatCount];
        Button seatsMinus, seatsPlus;
        int frozenSeatCount=SeatInputCoordinator.SeatCount;
        int probeCameraCount;
        public int LocalSeatCount => input.ActiveSeatCount;
        Button repeat, menu, diagnosticButton, durationButton, targetButton, targetMinus, targetPlus, mouseSensitivityMinus, mouseSensitivityPlus;
        public NativeCombatSession Session { get; private set; }
        CombatPresentation presentation;
        float previousFixedDelta;
        string[] hit = Array.Empty<string>();
        enum Phase { Setup, Running, Paused, Results }
        Phase phase;
        bool diagnostic;
        bool combatReview;
        ProvingArena arena;
        GameObject overlay;
        Text status;
        Button start, resume, rebind;
        Font font;
        FpsDisplay fps;
        string pauseReason = "";

        public bool Running => phase == Phase.Running;
        void Start()
        {
            if (Profile.Validate().Count != 0 || TrooperProfile.Validate().Count != 0) throw new InvalidOperationException("Invalid proving profile");
            previewMovement=Copy(Profile);previewLife=Copy(LifeProfile);previewCombat=Copy(CombatProfile);previewTrooper=Copy(TrooperProfile);
            Configuration=NativeMatchConfiguration.Default(MatchProfile);
            Profile.EnsureDefaultDescriptors();
            previousFixedDelta = Time.fixedDeltaTime;
            Time.fixedDeltaTime=1f/Profile.Get("simulation.fixedTickHz");
            AudioListener.volume = 0; // QA is muted, including all Player diagnostics.
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var light = Owned("arena-light").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = Profile.Get("presentation.lightIntensity");
            light.transform.rotation = Quaternion.Euler(50, -30, 0); // Authored fixture light orientation.
            var args=Environment.GetCommandLineArgs();int familyFlag=Array.IndexOf(args,"-arenaFamily");
            if(familyFlag>=0&&familyFlag+1<args.Length&&int.TryParse(args[familyFlag+1],out int family))ArenaProfile.Set("arena.family",family);
            bool environmentReview=DevelopmentNavigationReviewHandoff.TryParse(
                Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_NAVIGATION_EVIDENCE"),
                Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_ARENA_FAMILY"), out int environmentFamily);
            if(environmentReview) ArenaProfile.Set("arena.family",environmentFamily);
            arena = Owned("unity-arena-families-v1").AddComponent<ProvingArena>();
            arena.Build(ArenaGenerator.Generate(new ArenaGenerationRequest { Seed=1, GeneratorVersion=ArenaGenerator.FamiliesVersion, Profile=ArenaProfile }), Profile);
            Composition=HumanComposition(SetupRoster(),false);
            RebuildActors(Composition);
            Session = new NativeCombatSession(motors, arena, gameObject.scene.GetPhysicsScene(), Profile, LifeProfile, CombatProfile);
            presentation = new CombatPresentation(Session, Profile, CombatProfile, transform, gameObject.scene.GetPhysicsScene(), cameras, bodies, views);
            Session.Fired += (seat, damage) => hit[seat] = damage > 0 ? "Попадание · " + Mathf.RoundToInt(damage) : "Выстрел · мимо";
            int countFlag=Array.IndexOf(args,"-probeCameras");
            if(args.Contains("-diagnostic") && countFlag>=0 && countFlag+1<args.Length && int.TryParse(args[countFlag+1],out int count) && (count==1||count==2||count==4))
            {
                probeCameraCount=count;
            }
            CreateInterface();
            gameObject.AddComponent<ProvingTelemetry>();
            if(args.Contains("-teamsDiagnostic")) SetMatchMode(NativeMatchMode.Teams);
            bool requestedDiagnostic = Environment.GetCommandLineArgs().Contains("-diagnostic");
            if (requestedDiagnostic) Begin(true);
            if (args.Contains("-botSeatsReview")) gameObject.AddComponent<NativeBotSeatsReview>();
            if (args.Contains("-botSetupReview")) gameObject.AddComponent<NativeBotSetupReview>();
            if (args.Contains("-botPerceptionReview")) gameObject.AddComponent<NativeBotPerceptionReview>();
            if (args.Contains("-botBehaviorReview")) gameObject.AddComponent<NativeBotBehaviorReview>();
            if (args.Contains("-participantReview")) gameObject.AddComponent<NativeParticipantReview>();
            // macOS replaces the launcher process and drops its argv/environment. This symbol is supplied only by
            // ProvingGroundBuild.BuildMac's Development QA artifact, never by a shipping Player.
#if STAR_TOURNAMENT_DEVELOPMENT_QA
            const bool packagedNavigationReview = true;
#else
            const bool packagedNavigationReview = false;
#endif
            if (args.Contains("-botNavigationReview")||environmentReview||packagedNavigationReview)
            {
                // The review class is compiled only into the Development QA Player. Reflection keeps this ordinary bootstrap
                // independent from that conditional assembly while making the documented CLI activation observable.
                var reviewType=Type.GetType("StarTournament.ProvingGround.NativeBotNavigationReview, StarTournament.ProvingGround");
                if(reviewType==null) throw new InvalidOperationException("Development navigation review is not packaged");
                gameObject.AddComponent(reviewType); Debug.Log("NATIVE_BOT_NAVIGATION_REVIEW_BOOTSTRAPPED");
            }
            if (args.Contains("-trooperReview")) gameObject.AddComponent<TrooperReview>();
            if (args.Contains("-teamReview")) gameObject.AddComponent<NativeTeamsReview>();
            if (args.Contains("-layoutReview")) gameObject.AddComponent<NativeLayoutReview>();
            if (args.Contains("-matchReview")) gameObject.AddComponent<NativeMatchReview>();
            if (args.Contains("-combatReview") || args.Contains("-shotFeedbackReview")) gameObject.AddComponent<NativeCombatReview>();
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
        public void StartCombatReview(Gamepad[] pads)
        {
            if(phase!=Phase.Setup) Menu();
            SetSeatCount(pads.Length);
            input.Reset();
            for(int i=0;i<pads.Length;i++) input.Assign(i,pads[i]);
            combatReview=true; Begin(false);
        }
        public void PlaceCombatReviewSeat(int seat, Vector3 feet, float pitch,float yaw=0)
        {
            motors[seat].Initialize(Profile,feet);
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
            motors[index] = player.AddComponent<CharacterMotor>(); motors[index].Initialize(movement, arena.Spawns[index%arena.Spawns.Length]);
            if (!TrooperBodyPrefab || !TrooperArmsPrefab) throw new InvalidOperationException("Trooper assets missing; run Prepare in Editor.");
            var body = Instantiate(TrooperBodyPrefab, player.transform); body.name = "trooper-presentation";
            // Shipping recipe has a measured 1.8m rest height and feet origin; never normalize animated bounds.
            body.transform.localScale=Vector3.one*(movement.Get("player.capsule.height")/1.8f);
            body.AddComponent<TrooperVisual>().Initialize(TrooperClips,trooper);
            ApplyIdentity(body,index); SetLayer(body,ownerSeat>=0?11+ownerSeat:0); bodies[index]=body;
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
            view.AddComponent<TrooperVisual>().Initialize(TrooperClips,trooper,true);
            ApplyIdentity(view,participant);
            view.transform.localPosition=new Vector3(trooper.Get("view.x"),trooper.Get("view.y"),trooper.Get("view.z"));
            if (index == 0) cameraObject.AddComponent<AudioListener>();
        }
        NativeMatchComposition HumanComposition(NativeMatchRoster roster,bool swapped)
        {
            var metadata=Enumerable.Range(0,roster.Count).Select(i=>new NativeParticipantInfo(NativeParticipantKind.LocalHuman,
                "Игрок "+(i+1),NativeStandingsView.Identity(roster.Read(),i,swapped))).ToArray();
            return new NativeMatchComposition(roster,metadata,Enumerable.Range(0,roster.Count).ToArray());
        }
        void RebuildActors(NativeMatchComposition composition,ProvingProfile movement=null,ProvingProfile trooper=null,bool activate=true)
        {
            movement=movement??Profile;trooper=trooper??TrooperProfile;
            localActions=new LocalAction[composition.LocalCount];
            foreach(var motor in motors)if(motor){motor.gameObject.SetActive(false);Destroy(motor.gameObject);}
            for(int s=0;s<cameras.Length;s++)if(cameras[s]){cameras[s].gameObject.SetActive(false);Destroy(cameras[s].gameObject);cameras[s]=null;views[s]=null;}
            motors=new CharacterMotor[composition.ParticipantCount];bodies=new GameObject[motors.Length];hit=new string[motors.Length];
            for(int p=0;p<motors.Length;p++)CreateActor(p,composition.SeatOf(p),movement,trooper);
            for(int seat=0;seat<composition.LocalCount;seat++)CreateView(seat,composition.ParticipantAt(seat),movement,trooper);
            if(activate)foreach(var motor in motors)motor.gameObject.SetActive(true);
        }
        static void ApplyIdentity(GameObject root,int seat) => ApplyIdentity(root,NativeStandingsView.Palette[seat]);
        static void ApplyIdentity(GameObject root,Color color)
        {
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++) if(materials[i] && materials[i].name.StartsWith("identity",StringComparison.OrdinalIgnoreCase))
                {
                    var block=new MaterialPropertyBlock(); renderer.GetPropertyBlock(block,i);
                    // glTFast and URP material property aliases; preserve authored textures and shared ownership.
                    foreach(string property in new[]{"_BaseColor","baseColorFactor","_EmissionColor","emissiveFactor"})
                        if(materials[i].HasProperty(property)) block.SetColor(property,color);
                    renderer.SetPropertyBlock(block,i);
                }
            }
        }
        static void SetLayer(GameObject root, int layer) { foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer; }
        void Update()
        {
            if (phase == Phase.Setup)
            {
                bool ready=input.Ready; input.PollSetup();
                if(!ready && input.Ready) { start.interactable=true; Select(start); }
            }
            else if (phase == Phase.Running && !diagnostic)
            {
                if (!input.Ready) Pause("Устройство отключено: "+string.Join(", ",Enumerable.Range(0,LocalSeatCount).Where(i=>input.IsHumanSeat(i)&&!input.IsConnected(i)).Select(i=>"P"+(i+1))));
                else if(input.HasKeyboard && Cursor.lockState!=CursorLockMode.Locked) Pause("Захват курсора потерян");
                else { var activeProfile=frozenMovement??Profile; input.Capture(activeProfile,Time.deltaTime,MouseSensitivityPreference.Resolve(activeProfile)); if (input.ConsumePause()) Pause("Пауза"); }
            }
            if (diagnostic && phase==Phase.Running && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Pause("Диагностическая пауза");
            RefreshInterface();
        }
        void FixedUpdate()
        {
            if (phase != Phase.Running) return;
            for(int seat=0;seat<localActions.Length;seat++)localActions[seat]=diagnostic?default:input.Consume(seat);
            Composition.AssembleLocalActions(localActions,actions);
            try
            {
                NavigationReviewDriver?.ProduceActions(actions);
                BotDriver?.ProduceActions(actions,Time.fixedDeltaTime);
                Session.Tick(actions, Time.fixedDeltaTime);
                if(Session.Match?.Phase==NativeMatchPhase.Finished)
                {
                    phase=Phase.Results; input.Clear(); Session.ClearInput(); Array.Clear(actions,0,actions.Length);
                    SetCursor(false); RefreshInterface(); Select(repeat.interactable?repeat:menu);
                }
            }
            catch(InvalidOperationException error) { Pause(error.Message); Debug.LogError(error); }
        }
        void LateUpdate()
        {
            if(Session == null) return;
            if(phase!=Phase.Setup) presentation.Render();
            var snapshot=Session.Match?.Read();
            persistentStandings.Show(phase!=Phase.Setup && frozenSeatCount==3,snapshot,diagnostic||combatReview,Composition);
            for(int i=0;i<Composition.LocalCount;i++) if(hud[i])
            {
                int participant=Composition.ParticipantAt(i);var life=Session.Life(participant);
                health[i].text="ЗДОРОВЬЕ  "+Mathf.CeilToInt(life.Health);
                ammo[i].text="ДВУСТВОЛКА  ·  "+life.Ammo;
                crosshair[i].text=life.Dead ? presentation.DeathMessage(participant) : "+";
                var match=Session.Match;
                timers[i].text=match==null ? "" : match.Phase==NativeMatchPhase.Overtime ? "ОВЕРТАЙМ" : TimeLabel(match.RemainingSeconds);
                standings[i].Show(phase==Phase.Running && (actions[participant].ShowRoster || (reviewComposition!=null&&reviewShowStandings)),snapshot,diagnostic||combatReview,Composition);
                hud[i].color=Composition.Participant(participant).Color;
                hud[i].text=Composition.Participant(participant).Name+(snapshot?.Roster.Mode==NativeMatchMode.Teams ? " · "+NativeStandingsView.TeamName(snapshot.Roster.Teams[participant]) : "")+(diagnostic || combatReview ? " · DIAGNOSTIC" : "")+"\n"+(hit[participant] ?? "");
            }
        }
        void OnApplicationFocus(bool focus) { if (!focus && phase == Phase.Running && !diagnostic) Pause("Окно потеряло фокус"); }
        void Begin(bool diagnostics)
        {
            if ((!diagnostics && !input.Ready) || !ValidSetup()) return;
            diagnostic = diagnostics;
            try { CreateMatch(false); } catch(Exception error) when(error is InvalidOperationException || error is ArgumentException) { Menu();setupError=error.Message;RefreshInterface();return; }
            setupError=null; input.Clear(); Session.ClearInput(); phase = Phase.Running; SetCursor(!diagnostics && input.HasKeyboard);
        }
        static ProvingProfile Copy(ProvingProfile p) => JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(p));
        static string TimeLabel(double seconds) { int value=(int)Math.Ceiling(seconds); return (value/60).ToString("00")+":"+(value%60).ToString("00"); }
        void CreateMatch(bool repeating)
        {
            if(!repeating)
                foreach(var profile in new[]{Profile,LifeProfile,CombatProfile,MatchProfile,TrooperProfile,TeamProfile,RosterProfile})
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
                Composition=reviewComposition!=null?NativeMatchComposition.Restore(reviewComposition.Read()):SetupComposition();
                bool hasBots=Composition.Read().Participants.Any(p=>p.Kind==NativeParticipantKind.Bot);
                if(hasBots&&!botReviewEnabled)botSeed=(uint)UnityEngine.Random.Range(1,int.MaxValue);
                Composition.RequireSupportedSources(true,reviewComposition!=null&&!botReviewEnabled);
                frozenRoster=Composition.Roster;frozenSwappedColors=swappedTeamColors;frozenTeam=Copy(TeamProfile);
                if(RosterProfile.Id!="unity-native-roster-v1" || RosterProfile.Version!=1 || RosterProfile.Validate().Count!=0)throw new InvalidOperationException("Invalid roster profile");
                foreach(var descriptor in RosterProfile.Descriptors)NativeMatchConfiguration.ValidateValue(RosterProfile,descriptor.Path,RosterProfile.Get(descriptor.Path));
                frozenRosterProfile=Copy(RosterProfile);
                if(hasBots){frozenBehavior=Copy(BotBehaviorProfile);frozenPerception=Copy(BotPerceptionProfile);frozenNavigation=Copy(BotNavigationProfile);}
                frozenMovement=Copy(Profile); frozenLife=Copy(LifeProfile); frozenCombat=Copy(CombatProfile); frozenMatch=Copy(MatchProfile); frozenTrooper=Copy(TrooperProfile);
                frozenConfiguration=Configuration; frozenSeatCount=LocalSeatCount;
            }
            Time.fixedDeltaTime=1/frozenMovement.Get("simulation.fixedTickHz");
            RebuildActors(Composition,frozenMovement,frozenTrooper,false);
            foreach(var motor in motors)motor.SetAlive(false);
            Physics.SyncTransforms();
            var selector=new SafeSpawnSelector(gameObject.scene.GetPhysicsScene(),arena,frozenMovement,frozenCombat);
            if(!selector.TryInitial(frozenRoster,frozenTeam.Get("spawn.initialOpponentSeparation"),out var initial,
                (int)frozenRosterProfile.Get("spawn.initialSearchBudgetNodes"),(int)frozenRosterProfile.Get("spawn.initialCandidateBudget")))
                throw new InvalidOperationException("Нет безопасного размещения: "+selector.InitialFailure);
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
            var match=new NativeMatchState(frozenRoster,frozenConfiguration,frozenMatch,frozenMovement.Get("simulation.fixedTickHz"));
            Session=new NativeCombatSession(motors,arena,gameObject.scene.GetPhysicsScene(),frozenMovement,frozenLife,frozenCombat,match);
            NavigationReviewDriver = navigationReviewEnabled ? new NativeNavigationDriver(Session,arena,gameObject.scene.GetPhysicsScene(),
                frozenMovement,frozenCombat,frozenPerception,frozenNavigation,0) : null;
            BotDriver=Composition.Read().Participants.Any(p=>p.Kind==NativeParticipantKind.Bot)?new NativeBotMatchDriver(Session,Composition,arena,gameObject.scene.GetPhysicsScene(),frozenMovement,frozenLife,frozenCombat,frozenPerception,frozenNavigation,frozenBehavior,botSeed):null;
            presentation=new CombatPresentation(Session,frozenMovement,frozenCombat,transform,gameObject.scene.GetPhysicsScene(),cameras.Take(frozenSeatCount).ToArray(),bodies,views.Take(frozenSeatCount).ToArray(),Composition);
            Session.Fired+=(seat,damage)=>hit[seat]=damage>0?"Попадание · "+Mathf.RoundToInt(damage):"Выстрел · мимо";
            foreach(var view in standings) view.SwapTeamColors=frozenSwappedColors;
            persistentStandings.SwapTeamColors=results.SwapTeamColors=frozenSwappedColors;
            input.Clear(); Session.ClearInput();
        }
        void Repeat()
        {
            if(!diagnostic && !input.Ready) return;
            CreateMatch(true); phase=Phase.Running; SetCursor(!diagnostic && input.HasKeyboard); RefreshInterface();
        }
        void Menu()
        {
            NavigationReviewDriver = null; navigationReviewEnabled = false; BotDriver=null;botReviewEnabled=false;
            phase=Phase.Setup; diagnostic=false; combatReview=false; probeCameraCount=0; Session.Stop(); presentation.Dispose();
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
            Session=new NativeCombatSession(motors,arena,gameObject.scene.GetPhysicsScene(),safeMovement,safeLife,safeCombat);
            presentation=new CombatPresentation(Session,safeMovement,safeCombat,transform,gameObject.scene.GetPhysicsScene(),cameras.Take(LocalSeatCount).ToArray(),bodies,views.Take(LocalSeatCount).ToArray(),Composition);
            actions=new LocalAction[Composition.ParticipantCount];ApplyLayout(LocalSeatCount);
            input.Clear(); Array.Clear(actions,0,actions.Length); SetCursor(false); RefreshInterface(); Select(input.Ready?start:rebind);
        }
        void SetSeatCount(int count)
        {
            // Structural product limits; never silently discard configured bots.
            if(phase!=Phase.Setup || count<1 || count>SeatInputCoordinator.SeatCount || count+botSetup.Count>NativeMatchRoster.MaximumParticipants) return;
            input.SetActiveSeatCount(count);SyncHumanSeats();setupError=null; ApplyLayout(count); RefreshInterface();
        }
        void ApplyLayout(int count)
        {
            int renderedCount=diagnostic && probeCameraCount>0 ? Math.Min(probeCameraCount,count) : count;
            for(int i=0;i<cameras.Length;i++)
            {
                bool active=i<renderedCount;
                if(cameras[i])cameras[i].enabled=active;
                if(viewportRoots[i]) viewportRoots[i].gameObject.SetActive(active);
                standings[i]?.Root.SetActive(false);
                if(!active || !cameras[i]) continue;
                var r=LocalSeatLayout.Viewport(renderedCount,i); cameras[i].rect=r;
                if(viewportRoots[i]) Layout(viewportRoots[i],new Vector2(r.xMin,r.yMin),new Vector2(r.xMax,r.yMax));
                if(standings[i]!=null) Layout((RectTransform)standings[i].Root.transform,
                    new Vector2(r.xMin,r.yMin+r.height*.25f),new Vector2(r.xMax,r.yMin+r.height*.75f));
            }
            if(persistentStandings!=null)
            {
                persistentStandings.Root.SetActive(false);
                var r=LocalSeatLayout.PersistentStandings(count);
                if(r.HasValue) Layout((RectTransform)persistentStandings.Root.transform,new Vector2(r.Value.xMin,r.Value.yMin),new Vector2(r.Value.xMax,r.Value.yMax));
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
        void Pause(string reason) { BotDriver?.Release(); phase = Phase.Paused; pauseReason = reason; input.Clear(); Session.ClearInput(); Array.Clear(actions,0,actions.Length); SetCursor(false); RefreshInterface(); Select(diagnostic||input.Ready ? resume : menu); }
        void Resume() { if (diagnostic || input.Ready) { BotDriver?.Release(); input.Clear(); Session.ClearInput(); phase = Phase.Running; SetCursor(!diagnostic && input.HasKeyboard); } }
        void ResetSetup() { Menu(); phase = Phase.Setup; diagnostic = false; input.Reset(); Session.ClearInput(); Array.Clear(actions,0,actions.Length); SetCursor(false); RefreshInterface(); Select(rebind); }
        static void Select(Button button) { if(EventSystem.current) { EventSystem.current.firstSelectedGameObject=button.gameObject; EventSystem.current.SetSelectedGameObject(button.gameObject); } }
        static void SetCursor(bool capture) { Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !capture; }
        void OnDestroy() { Session?.Stop(); presentation?.Dispose(); SetCursor(false); if(previousFixedDelta > 0) Time.fixedDeltaTime=previousFixedDelta; }
        void CreateInterface()
        {
            var eventSystem = Owned("input-events"); eventSystem.AddComponent<EventSystem>(); eventSystem.AddComponent<InputSystemUIInputModule>();
            var canvasObject = Owned("native-ui", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080); // Full HD UI design coordinate system, independent of gameplay/output resolution.
            for (int i=0;i<hud.Length;i++)
            {
                if(!cameras[i].enabled) continue;
                var root=new GameObject("seat-viewport-"+i,typeof(RectTransform)); root.transform.SetParent(canvasObject.transform,false);
                viewportRoots[i]=(RectTransform)root.transform;
                var rect = new Rect(0,0,1,1);
                hud[i] = TextElement(viewportRoots[i], "seat-hud-"+i, "", (int)Profile.Get("ui.fontSize"));
                Layout(hud[i].rectTransform,new Vector2(rect.xMin,rect.yMin),new Vector2(rect.xMax,rect.yMax));
                float padding=Profile.Get("ui.fontSize");
                hud[i].rectTransform.offsetMin=Vector2.one*padding;hud[i].rectTransform.offsetMax=-Vector2.one*padding;
                hud[i].alignment = TextAnchor.UpperLeft;
                health[i] = TextElement(viewportRoots[i],"health-"+i,"",(int)Profile.Get("ui.fontSize"));
                ammo[i] = TextElement(viewportRoots[i],"ammo-"+i,"",(int)Profile.Get("ui.fontSize"));
                foreach(var label in new[]{health[i],ammo[i]})
                {
                    Layout(label.rectTransform,new Vector2(rect.xMin,rect.yMin),new Vector2(rect.xMax,rect.yMax));
                    label.rectTransform.offsetMin=Vector2.one*padding; label.rectTransform.offsetMax=-Vector2.one*padding;
                }
                timers[i]=TextElement(viewportRoots[i],"timer-"+i,"",(int)Profile.Get("ui.fontSize"));
                Layout(timers[i].rectTransform,new Vector2(rect.xMin,rect.yMin),new Vector2(rect.xMax,rect.yMax));
                timers[i].rectTransform.offsetMax=-Vector2.one*padding; timers[i].alignment=TextAnchor.UpperCenter;
                health[i].alignment=TextAnchor.LowerLeft; ammo[i].alignment=TextAnchor.LowerRight;
                var cross = TextElement(viewportRoots[i],"crosshair-"+i,"+",(int)Profile.Get("ui.headingFontSize"));
                Layout(cross.rectTransform,new Vector2(rect.xMin,rect.yMin),new Vector2(rect.xMax,rect.yMax)); cross.alignment=TextAnchor.MiddleCenter; crosshair[i]=cross;
            }
            for(int i=0;i<standings.Length;i++)
            {
                var r=cameras[i].rect;
                standings[i]=new NativeStandingsView(canvasObject.transform,font,(int)Profile.Get("ui.fontSize"),"standings-"+i,
                    new Vector2(r.xMin,r.yMin+r.height*.25f),new Vector2(r.xMax,r.yMin+r.height*.75f));
                standings[i].Root.SetActive(false);
            }
            persistentStandings=new NativeStandingsView(canvasObject.transform,font,(int)Profile.Get("ui.fontSize"),"persistent-standings",new Vector2(.5f,0),new Vector2(1,.5f));
            persistentStandings.Root.SetActive(false);
            overlay = new GameObject("setup-pause",typeof(RectTransform),typeof(Image)); overlay.transform.SetParent(canvasObject.transform,false);
            Layout((RectTransform)overlay.transform,Vector2.zero,Vector2.one); overlay.GetComponent<Image>().color=new Color32(10,18,30,245);
            var column = new GameObject("menu",typeof(RectTransform),typeof(VerticalLayoutGroup)); column.transform.SetParent(overlay.transform,false);
            Layout((RectTransform)column.transform,new Vector2(.15f,.15f),new Vector2(.85f,.85f));
            var layout=column.GetComponent<VerticalLayoutGroup>(); layout.childControlHeight=true; layout.childForceExpandHeight=true;
            status=TextElement(column.transform,"status","",(int)Profile.Get("ui.headingFontSize")); status.alignment=TextAnchor.MiddleCenter;
            start=ButtonElement(column.transform,"Начать — четыре игрока",()=>Begin(false));
            seatsMinus=ButtonElement(column.transform,"Игроки −",()=>SetSeatCount(LocalSeatCount-1)); seatsMinus.gameObject.name="seats-minus";
            seatsPlus=ButtonElement(column.transform,"Экраны +",()=>SetSeatCount(LocalSeatCount+1)); seatsPlus.gameObject.name="seats-plus";
            modeButton=ButtonElement(column.transform,"Режим",()=>SetMatchMode(SetupMode==NativeMatchMode.Ffa?NativeMatchMode.Teams:NativeMatchMode.Ffa));modeButton.gameObject.name="match-mode";
            teamRow=new GameObject("team-assignments",typeof(RectTransform),typeof(HorizontalLayoutGroup));teamRow.transform.SetParent(column.transform,false);
            var teamLayout=teamRow.GetComponent<HorizontalLayoutGroup>();teamLayout.childControlWidth=teamLayout.childControlHeight=true;teamLayout.childForceExpandWidth=teamLayout.childForceExpandHeight=true;
            for(int i=0;i<teamButtons.Length;i++) { int seat=i;teamButtons[i]=ButtonElement(teamRow.transform,"Команда P"+(i+1),()=>SetTeam(seat,teamAssignments[seat]==NativeTeam.TeamA?NativeTeam.TeamB:NativeTeam.TeamA));teamButtons[i].gameObject.name="team-seat-"+i; }
            colorButton=ButtonElement(column.transform,"Поменять цвета команд",()=>{if(phase!=Phase.Setup)return;swappedTeamColors=!swappedTeamColors;RefreshInterface();});colorButton.gameObject.name="team-colors";
            resume=ButtonElement(column.transform,"Продолжить",Resume);
            repeat=ButtonElement(column.transform,"Повторить матч",Repeat);
            menu=ButtonElement(column.transform,"В главное меню",Menu);
            rebind=ButtonElement(column.transform,"Назначить устройства заново",ResetSetup);
            diagnosticButton=ButtonElement(column.transform,"Диагностика четырёх камер · без управления",()=>Begin(true));
            durationButton=ButtonElement(column.transform,"Длительность",()=>StepConfiguration("match.durationMinutes",1));
            durationButton.gameObject.name="duration-plus";
            var durationMinus=ButtonElement(column.transform,"Длительность −",()=>StepConfiguration("match.durationMinutes",-1)); durationMinus.gameObject.name="duration-minus";
            targetButton=ButtonElement(column.transform,"Цель",()=>{ if(phase!=Phase.Setup)return; var c=Configuration;c.TargetEnabled=!c.TargetEnabled;Configuration=c;RefreshInterface(); });
            targetButton.gameObject.name="target-toggle";
            targetMinus=ButtonElement(column.transform,"Цель −",()=>StepConfiguration("match.targetPoints",-1));
            targetPlus=ButtonElement(column.transform,"Цель +",()=>StepConfiguration("match.targetPoints",1));
            results=new NativeStandingsView(overlay.transform,font,(int)Profile.Get("ui.fontSize"),"results-table",new Vector2(.06f,.45f),new Vector2(.94f,.77f));
            results.Root.SetActive(false);
            var settings = ButtonElement(column.transform,"Настройки · Показывать FPS: вкл",()=>fps.Toggle());
            settings.gameObject.name = "fps-setting";
            fps = canvasObject.AddComponent<FpsDisplay>();
            fps.Initialize(font, (int)Profile.Get("ui.fontSize"), settings.GetComponentInChildren<Text>());
            mouseSensitivityMinus=ButtonElement(column.transform,"Чувствительность мыши −",()=>StepMouseSensitivity(-1));mouseSensitivityMinus.gameObject.name="mouse-sensitivity-minus";
            mouseSensitivityPlus=ButtonElement(column.transform,"Чувствительность мыши +",()=>StepMouseSensitivity(1));mouseSensitivityPlus.gameObject.name="mouse-sensitivity-plus";
            ButtonElement(column.transform,"Выйти",()=>Application.Quit());
            CreateBotSetupInterface();
            CreateSeatSetupInterface();
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
        Button ButtonElement(Transform parent,string title,UnityEngine.Events.UnityAction action)
        {
            var go=new GameObject(title,typeof(RectTransform),typeof(Image),typeof(Button)); go.transform.SetParent(parent,false);
            go.GetComponent<Image>().color=new Color32(32,75,92,255);
            var button=go.GetComponent<Button>(); button.onClick.AddListener(action);
            var label=TextElement(go.transform,"label",title,(int)Profile.Get("ui.fontSize")); Layout(label.rectTransform,Vector2.zero,Vector2.one); label.alignment=TextAnchor.MiddleCenter; return button;
        }
        static void Layout(RectTransform rect,Vector2 min,Vector2 max) { rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero; }
        void RefreshInterface()
        {
            if (!overlay) return;
            overlay.SetActive(phase!=Phase.Running);
            start.gameObject.SetActive(phase==Phase.Setup); start.interactable=input.Ready && ValidSetup();
            start.GetComponentInChildren<Text>().text="Начать · людей: "+botSetup.HumanCount(LocalSeatCount)+" · AI: "+(LocalSeatCount-botSetup.HumanCount(LocalSeatCount)+botSetup.Count);
            seatsMinus.gameObject.SetActive(phase==Phase.Setup); seatsMinus.interactable=LocalSeatCount>1;
            seatsPlus.gameObject.SetActive(phase==Phase.Setup); seatsPlus.interactable=LocalSeatCount<SeatInputCoordinator.SeatCount && LocalSeatCount+botSetup.Count<NativeMatchRoster.MaximumParticipants;
            seatsMinus.GetComponentInChildren<Text>().text="Экранов: "+LocalSeatCount+"   −";
            diagnosticButton.GetComponentInChildren<Text>().text="Диагностика: "+LocalSeatCount+" камеры · без управления";
            resume.gameObject.SetActive(phase==Phase.Paused); resume.interactable=diagnostic||input.Ready;
            repeat.gameObject.SetActive(phase==Phase.Paused||phase==Phase.Results); repeat.interactable=diagnostic||input.Ready;
            menu.gameObject.SetActive(phase==Phase.Paused||phase==Phase.Results);
            modeButton.gameObject.SetActive(phase==Phase.Setup);
            modeButton.GetComponentInChildren<Text>().text="Режим: "+(SetupMode==NativeMatchMode.Teams?"Team A / Team B":"Каждый сам за себя");
            teamRow.SetActive(phase==Phase.Setup && SetupMode==NativeMatchMode.Teams);
            colorButton.gameObject.SetActive(teamRow.activeSelf);
            for(int i=0;i<teamButtons.Length;i++)
            {
                teamButtons[i].gameObject.SetActive(i<LocalSeatCount);
                teamButtons[i].GetComponentInChildren<Text>().text="P"+(i+1)+" · "+NativeStandingsView.TeamName(teamAssignments[i]);
                teamButtons[i].GetComponent<Image>().color=NativeStandingsView.TeamColor(teamAssignments[i],swappedTeamColors);
            }
            rebind.gameObject.SetActive(phase==Phase.Setup);
            diagnosticButton.gameObject.SetActive(phase==Phase.Setup);diagnosticButton.interactable=ValidSetup();
            durationButton.gameObject.SetActive(phase==Phase.Setup);
            durationButton.transform.parent.Find("duration-minus").gameObject.SetActive(phase==Phase.Setup);
            targetButton.gameObject.SetActive(phase==Phase.Setup);
            targetMinus.gameObject.SetActive(phase==Phase.Setup && Configuration.TargetEnabled);
            targetPlus.gameObject.SetActive(phase==Phase.Setup && Configuration.TargetEnabled);
            var sensitivityDescriptor=MouseSensitivityPreference.Descriptor(Profile);var sensitivity=MouseSensitivityPreference.Resolve(Profile);
            mouseSensitivityMinus.gameObject.SetActive(phase!=Phase.Running);mouseSensitivityPlus.gameObject.SetActive(phase!=Phase.Running);
            mouseSensitivityMinus.interactable=sensitivity>sensitivityDescriptor.Minimum;
            mouseSensitivityPlus.interactable=sensitivity<sensitivityDescriptor.Maximum;
            mouseSensitivityMinus.GetComponentInChildren<Text>().text=sensitivityDescriptor.Label+": "+sensitivity.ToString("0.00")+" "+sensitivityDescriptor.Unit+"   −";
            mouseSensitivityPlus.GetComponentInChildren<Text>().text=sensitivityDescriptor.Label+": "+sensitivity.ToString("0.00")+" "+sensitivityDescriptor.Unit+"   +";
            durationButton.GetComponentInChildren<Text>().text="Длительность: "+Configuration.DurationMinutes+" мин   +";
            targetButton.GetComponentInChildren<Text>().text="Цель: "+(Configuration.TargetEnabled?Configuration.TargetPoints+" очков":"выключена");
            results.Show(phase==Phase.Results,Session.Match?.Read(),diagnostic||combatReview,Composition);
            var menuRect=(RectTransform)status.transform.parent;
            Layout(menuRect,new Vector2(phase==Phase.Setup?.04f:.15f,phase==Phase.Results?.06f:.08f),new Vector2(phase==Phase.Setup?.51f:.85f,phase==Phase.Results?.40f:.92f));
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
            status.text="STAR TOURNAMENT · UNITY ПОЛИГОН\n"+(phase==Phase.Paused ? pauseReason : "Space / Start — присоединить человека · Esc — пауза")+"\n"+
                string.Join("  |  ",Enumerable.Range(0,LocalSeatCount).Select(i=>"P"+(i+1)+": "+(!input.IsHumanSeat(i)?"AI":input.IsConnected(i)?"готов":"ожидает")));
            if(phase==Phase.Setup && !ValidSetup()) status.text+=LocalSeatCount+botSetup.Count<2?"\nДобавьте бота или второго игрока":"\nНужны обе команды: Team A и Team B";
            if(phase==Phase.Setup && setupError!=null) status.text+="\n"+setupError;
            if(phase==Phase.Results)
            {
                var result=Session.Match.Read();
                status.text=(result.Roster.Mode==NativeMatchMode.Teams ? "ПОБЕДИЛА "+NativeStandingsView.TeamName(result.WinnerTeam) : "ПОБЕДИЛ "+Composition.Participant(result.Winner).Name)+" · "+(result.Trigger=="score-limit"?"ЦЕЛЬ ПО ОЧКАМ":"ВРЕМЯ")+(input.Ready||diagnostic?"":"\nПодключите устройства для повтора или вернитесь в меню");
            }
        }
    }
}
