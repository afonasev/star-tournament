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
        public ProvingProfile Profile = ProvingProfile.CreateDefault();
        readonly SeatInputCoordinator input = new SeatInputCoordinator();
        readonly CharacterMotor[] motors = new CharacterMotor[SeatInputCoordinator.SeatCount];
        readonly Camera[] cameras = new Camera[SeatInputCoordinator.SeatCount];
        readonly Text[] hud = new Text[SeatInputCoordinator.SeatCount];
        public ProvingProfile LifeProfile = ProvingProfile.CreateCombatDefault();
        public ProvingProfile CombatProfile = ProvingProfile.CreateNativeCombatDefault();
        readonly GameObject[] bodies = new GameObject[SeatInputCoordinator.SeatCount], views = new GameObject[SeatInputCoordinator.SeatCount];
        readonly Text[] health = new Text[SeatInputCoordinator.SeatCount], ammo = new Text[SeatInputCoordinator.SeatCount], crosshair = new Text[SeatInputCoordinator.SeatCount];
        readonly LocalAction[] actions = new LocalAction[SeatInputCoordinator.SeatCount];
        public ProvingProfile MatchProfile = ProvingProfile.CreateMatchDefault();
        public NativeMatchConfiguration Configuration { get; private set; }
        ProvingProfile frozenMovement, frozenLife, frozenCombat, frozenMatch;
        NativeMatchConfiguration frozenConfiguration;
        readonly Text[] timers = new Text[SeatInputCoordinator.SeatCount];
        readonly NativeStandingsView[] standings = new NativeStandingsView[SeatInputCoordinator.SeatCount];
        NativeStandingsView results;
        Button repeat, menu, diagnosticButton, durationButton, targetButton, targetMinus, targetPlus;
        public NativeCombatSession Session { get; private set; }
        CombatPresentation presentation;
        float previousFixedDelta;
        readonly string[] hit = new string[SeatInputCoordinator.SeatCount];
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
            if (Profile.Validate().Count != 0) throw new InvalidOperationException("Invalid proving profile");
            Configuration=NativeMatchConfiguration.Default(MatchProfile);
            previousFixedDelta = Time.fixedDeltaTime;
            Time.fixedDeltaTime=1f/Profile.Get("simulation.fixedTickHz");
            AudioListener.volume = 0; // QA is muted, including all Player diagnostics.
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var light = Owned("arena-light").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = Profile.Get("presentation.lightIntensity");
            light.transform.rotation = Quaternion.Euler(50, -30, 0); // Authored fixture light orientation.
            arena = Owned("unity-two-level-fixture-v1").AddComponent<ProvingArena>(); arena.Build(Profile);
            for (int i = 0; i < motors.Length; i++) CreateSeat(i);
            Session = new NativeCombatSession(motors, arena, gameObject.scene.GetPhysicsScene(), Profile, LifeProfile, CombatProfile);
            presentation = new CombatPresentation(Session, Profile, CombatProfile, transform, gameObject.scene.GetPhysicsScene(), cameras, bodies, views);
            Session.Fired += (seat, damage) => hit[seat] = damage > 0 ? "Попадание · " + Mathf.RoundToInt(damage) : "Выстрел · мимо";
            var args=Environment.GetCommandLineArgs(); int countFlag=Array.IndexOf(args,"-probeCameras");
            if(args.Contains("-diagnostic") && countFlag>=0 && countFlag+1<args.Length && int.TryParse(args[countFlag+1],out int count) && (count==1||count==2||count==4))
            {
                for(int i=0;i<cameras.Length;i++) { cameras[i].enabled=i<count; if(i<count && count<4) cameras[i].rect=new Rect(i/(float)count,0,1f/count,1); }
            }
            CreateInterface();
            gameObject.AddComponent<ProvingTelemetry>();
            bool requestedDiagnostic = Environment.GetCommandLineArgs().Contains("-diagnostic");
            if (requestedDiagnostic) Begin(true);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (args.Contains("-matchReview")) gameObject.AddComponent<NativeMatchReview>();
            if (args.Contains("-combatReview")) gameObject.AddComponent<NativeCombatReview>();
#endif
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void StartCombatReview(Gamepad[] pads)
        {
            input.Reset();
            for(int i=0;i<pads.Length;i++) input.Assign(i,pads[i]);
            combatReview=true; Begin(false);
        }
        public void PlaceCombatReviewSeat(int seat, Vector3 feet, float pitch)
        {
            motors[seat].Initialize(Profile,feet);
            motors[seat].Tick(new LocalAction{LookDegrees=new Vector2(0,-pitch)},Time.fixedDeltaTime);
        }
#endif
        GameObject Owned(string name, params Type[] components)
        {
            var go = new GameObject(name, components);
            SceneManager.MoveGameObjectToScene(go, gameObject.scene);
            go.transform.SetParent(transform, false);
            return go;
        }
        void CreateSeat(int index)
        {
            var player = Owned("player-" + (index+1)); player.layer = ProvingArena.ParticipantLayer;
            motors[index] = player.AddComponent<CharacterMotor>(); motors[index].Initialize(Profile, arena.Spawns[index]);
            if (!RobotPrefab || !WorldWeaponPrefab || !ViewWeaponPrefab) throw new InvalidOperationException("Required GLB prefab missing; run Prepare in Editor.");
            var body = Instantiate(RobotPrefab, player.transform); body.name = "robot-presentation";
            // Normalize the authored bounds to the profile capsule height, keeping a feet anchor.
            // Scaling is derived from content, not another independent balance parameter.
            var renderers=body.GetComponentsInChildren<Renderer>();
            var bounds=renderers[0].bounds; foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float scale=Profile.Get("player.capsule.height")/bounds.size.y;
            body.transform.localScale=Vector3.one*scale;
            body.transform.localPosition=Vector3.up*((player.transform.position.y-bounds.min.y)*scale);
            var mount = body.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "robot:rig:weapon-mount");
            if (!mount) throw new InvalidOperationException("Robot semantic weapon mount missing");
            var weapon=Instantiate(WorldWeaponPrefab, mount); weapon.transform.localPosition=Vector3.up*Profile.Get("presentation.worldWeaponMountY");
            ApplyIdentity(body,index);
            SetLayer(body, 11+index); bodies[index] = body;
            foreach(var collider in body.GetComponentsInChildren<Collider>()) { collider.enabled=false; Destroy(collider); }
            var cameraObject = Owned("seat-camera-" + (index+1));
            cameras[index] = cameraObject.AddComponent<Camera>();
            cameras[index].rect = new Rect(index%2/2f, index<2 ? .5f : 0, .5f, .5f);
            cameras[index].fieldOfView = Profile.Get("camera.fieldOfViewDegrees");
            cameras[index].nearClipPlane = Profile.Get("camera.nearClipPlane"); cameras[index].farClipPlane = Profile.Get("camera.farClipPlane");
            cameras[index].cullingMask = ~((1 << (11+index)) | (15 << 15)) | (1 << (15+index));
            cameras[index].backgroundColor = new Color32(15, 22, 35, 255);
            var view = Instantiate(ViewWeaponPrefab, cameraObject.transform); SetLayer(view, 15+index); views[index] = view;
            ApplyIdentity(view,index);
            view.transform.localPosition=new Vector3(Profile.Get("presentation.viewWeaponX"),Profile.Get("presentation.viewWeaponY"),Profile.Get("presentation.viewWeaponZ"));
            if (index == 0) cameraObject.AddComponent<AudioListener>();
        }
        static void ApplyIdentity(GameObject root,int seat)
        {
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++) if(materials[i] && materials[i].name.StartsWith("identity",StringComparison.OrdinalIgnoreCase))
                {
                    var block=new MaterialPropertyBlock(); renderer.GetPropertyBlock(block,i);
                    // glTFast and URP material property aliases; preserve authored textures and shared ownership.
                    foreach(string property in new[]{"_BaseColor","baseColorFactor","_EmissionColor","emissiveFactor"})
                        if(materials[i].HasProperty(property)) block.SetColor(property,NativeStandingsView.Palette[seat]);
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
                if (!input.Ready) Pause("Назначенное устройство отключено");
                else if(input.HasKeyboard && Cursor.lockState!=CursorLockMode.Locked) Pause("Захват курсора потерян");
                else { input.Capture(frozenMovement ?? Profile, Time.deltaTime); if (input.ConsumePause()) Pause("Пауза"); }
            }
            if (diagnostic && phase==Phase.Running && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Pause("Диагностическая пауза");
            RefreshInterface();
        }
        void FixedUpdate()
        {
            if (phase != Phase.Running) return;
            for(int i=0;i<actions.Length;i++) actions[i] = diagnostic ? default : input.Consume(i);
            try
            {
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
            for(int i=0;i<hud.Length;i++) if(hud[i])
            {
                var life=Session.Life(i);
                health[i].text="ЗДОРОВЬЕ  "+Mathf.CeilToInt(life.Health);
                ammo[i].text="ДВУСТВОЛКА  ·  "+life.Ammo;
                crosshair[i].text=life.Dead ? presentation.DeathMessage(i) : "+";
                var match=Session.Match;
                timers[i].text=match==null ? "" : match.Phase==NativeMatchPhase.Overtime ? "ОВЕРТАЙМ" : TimeLabel(match.RemainingSeconds);
                standings[i].Show(phase==Phase.Running && actions[i].ShowRoster,match?.Read(),diagnostic||combatReview);
                hud[i].text="ИГРОК "+(i+1)+(diagnostic || combatReview ? " · DIAGNOSTIC" : "")+"\n"+(hit[i] ?? "");
            }
        }
        void OnApplicationFocus(bool focus) { if (!focus && phase == Phase.Running && !diagnostic) Pause("Окно потеряло фокус"); }
        void Begin(bool diagnostics)
        {
            if (!diagnostics && !input.Ready) return;
            diagnostic = diagnostics; CreateMatch(false); input.Clear(); Session.ClearInput(); phase = Phase.Running; SetCursor(!diagnostics && input.HasKeyboard);
        }
        static ProvingProfile Copy(ProvingProfile p) => JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(p));
        static string TimeLabel(double seconds) { int value=(int)Math.Ceiling(seconds); return (value/60).ToString("00")+":"+(value%60).ToString("00"); }
        void CreateMatch(bool repeating)
        {
            Session?.Stop(); presentation?.Dispose();
            if(!repeating)
            {
                Configuration.Validate(MatchProfile);
                frozenMovement=Copy(Profile); frozenLife=Copy(LifeProfile); frozenCombat=Copy(CombatProfile); frozenMatch=Copy(MatchProfile);
                frozenConfiguration=Configuration;
            }
            for(int i=0;i<motors.Length;i++) { motors[i].SetAlive(false); bodies[i].SetActive(true); views[i].SetActive(true); }
            for(int i=0;i<motors.Length;i++) motors[i].Initialize(frozenMovement,arena.Spawns[i]);
            Array.Clear(actions,0,actions.Length); Array.Clear(hit,0,hit.Length);
            var match=new NativeMatchState(motors.Length,frozenConfiguration,frozenMatch,frozenMovement.Get("simulation.fixedTickHz"));
            Session=new NativeCombatSession(motors,arena,gameObject.scene.GetPhysicsScene(),frozenMovement,frozenLife,frozenCombat,match);
            presentation=new CombatPresentation(Session,frozenMovement,frozenCombat,transform,gameObject.scene.GetPhysicsScene(),cameras,bodies,views);
            Session.Fired+=(seat,damage)=>hit[seat]=damage>0?"Попадание · "+Mathf.RoundToInt(damage):"Выстрел · мимо";
            input.Clear(); Session.ClearInput();
        }
        void Repeat()
        {
            if(!diagnostic && !input.Ready) return;
            CreateMatch(true); phase=Phase.Running; SetCursor(!diagnostic && input.HasKeyboard); RefreshInterface();
        }
        void Menu()
        {
            phase=Phase.Setup; diagnostic=false; combatReview=false; Session.Stop(); presentation.Dispose();
            input.Clear(); Array.Clear(actions,0,actions.Length); SetCursor(false); RefreshInterface(); Select(input.Ready?start:rebind);
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
        void Pause(string reason) { phase = Phase.Paused; pauseReason = reason; input.Clear(); Session.ClearInput(); Array.Clear(actions,0,actions.Length); SetCursor(false); RefreshInterface(); Select(diagnostic||input.Ready ? resume : menu); }
        void Resume() { if (diagnostic || input.Ready) { input.Clear(); Session.ClearInput(); phase = Phase.Running; SetCursor(!diagnostic && input.HasKeyboard); } }
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
                var rect = cameras[i].rect;
                hud[i] = TextElement(canvasObject.transform, "seat-hud-"+i, "", (int)Profile.Get("ui.fontSize"));
                Layout(hud[i].rectTransform,new Vector2(rect.xMin,rect.yMin),new Vector2(rect.xMax,rect.yMax));
                float padding=Profile.Get("ui.fontSize");
                hud[i].rectTransform.offsetMin=Vector2.one*padding;hud[i].rectTransform.offsetMax=-Vector2.one*padding;
                hud[i].alignment = TextAnchor.UpperLeft;
                health[i] = TextElement(canvasObject.transform,"health-"+i,"",(int)Profile.Get("ui.fontSize"));
                ammo[i] = TextElement(canvasObject.transform,"ammo-"+i,"",(int)Profile.Get("ui.fontSize"));
                foreach(var label in new[]{health[i],ammo[i]})
                {
                    Layout(label.rectTransform,new Vector2(rect.xMin,rect.yMin),new Vector2(rect.xMax,rect.yMax));
                    label.rectTransform.offsetMin=Vector2.one*padding; label.rectTransform.offsetMax=-Vector2.one*padding;
                }
                timers[i]=TextElement(canvasObject.transform,"timer-"+i,"",(int)Profile.Get("ui.fontSize"));
                Layout(timers[i].rectTransform,new Vector2(rect.xMin,rect.yMin),new Vector2(rect.xMax,rect.yMax));
                timers[i].rectTransform.offsetMax=-Vector2.one*padding; timers[i].alignment=TextAnchor.UpperCenter;
                health[i].alignment=TextAnchor.LowerLeft; ammo[i].alignment=TextAnchor.LowerRight;
                var cross = TextElement(canvasObject.transform,"crosshair-"+i,"+",(int)Profile.Get("ui.headingFontSize"));
                Layout(cross.rectTransform,new Vector2(rect.xMin,rect.yMin),new Vector2(rect.xMax,rect.yMax)); cross.alignment=TextAnchor.MiddleCenter; crosshair[i]=cross;
            }
            for(int i=0;i<standings.Length;i++)
            {
                var r=cameras[i].rect;
                standings[i]=new NativeStandingsView(canvasObject.transform,font,(int)Profile.Get("ui.fontSize"),"standings-"+i,
                    new Vector2(r.xMin,r.yMin+r.height*.25f),new Vector2(r.xMax,r.yMin+r.height*.75f));
                standings[i].Root.SetActive(false);
            }
            overlay = new GameObject("setup-pause",typeof(RectTransform),typeof(Image)); overlay.transform.SetParent(canvasObject.transform,false);
            Layout((RectTransform)overlay.transform,Vector2.zero,Vector2.one); overlay.GetComponent<Image>().color=new Color32(10,18,30,245);
            var column = new GameObject("menu",typeof(RectTransform),typeof(VerticalLayoutGroup)); column.transform.SetParent(overlay.transform,false);
            Layout((RectTransform)column.transform,new Vector2(.15f,.15f),new Vector2(.85f,.85f));
            var layout=column.GetComponent<VerticalLayoutGroup>(); layout.childControlHeight=true; layout.childForceExpandHeight=true;
            status=TextElement(column.transform,"status","",(int)Profile.Get("ui.headingFontSize")); status.alignment=TextAnchor.MiddleCenter;
            start=ButtonElement(column.transform,"Начать — четыре игрока",()=>Begin(false));
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
            results=new NativeStandingsView(overlay.transform,font,(int)Profile.Get("ui.headingFontSize"),"results-table",new Vector2(.06f,.45f),new Vector2(.94f,.77f));
            results.Root.SetActive(false);
            var settings = ButtonElement(column.transform,"Настройки · Показывать FPS: вкл",()=>fps.Toggle());
            settings.gameObject.name = "fps-setting";
            fps = canvasObject.AddComponent<FpsDisplay>();
            fps.Initialize(font, (int)Profile.Get("ui.fontSize"), settings.GetComponentInChildren<Text>());
            ButtonElement(column.transform,"Выйти",()=>Application.Quit());
            RefreshInterface(); Select(rebind);
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
            start.gameObject.SetActive(phase==Phase.Setup); start.interactable=input.Ready;
            resume.gameObject.SetActive(phase==Phase.Paused); resume.interactable=diagnostic||input.Ready;
            repeat.gameObject.SetActive(phase==Phase.Paused||phase==Phase.Results); repeat.interactable=diagnostic||input.Ready;
            menu.gameObject.SetActive(phase==Phase.Paused||phase==Phase.Results);
            rebind.gameObject.SetActive(phase==Phase.Setup);
            diagnosticButton.gameObject.SetActive(phase==Phase.Setup);
            durationButton.gameObject.SetActive(phase==Phase.Setup);
            durationButton.transform.parent.Find("duration-minus").gameObject.SetActive(phase==Phase.Setup);
            targetButton.gameObject.SetActive(phase==Phase.Setup);
            targetMinus.gameObject.SetActive(phase==Phase.Setup && Configuration.TargetEnabled);
            targetPlus.gameObject.SetActive(phase==Phase.Setup && Configuration.TargetEnabled);
            durationButton.GetComponentInChildren<Text>().text="Длительность: "+Configuration.DurationMinutes+" мин   +";
            targetButton.GetComponentInChildren<Text>().text="Цель: "+(Configuration.TargetEnabled?Configuration.TargetPoints+" очков":"выключена");
            results.Show(phase==Phase.Results,Session.Match?.Read(),diagnostic||combatReview);
            var menuRect=(RectTransform)status.transform.parent;
            Layout(menuRect,new Vector2(.15f,phase==Phase.Results?.06f:.08f),new Vector2(.85f,phase==Phase.Results?.40f:.92f));
            status.text="STAR TOURNAMENT · UNITY ПОЛИГОН\n"+(phase==Phase.Paused ? pauseReason : "Space — клавиатура/мышь · Start — присоединить геймпад")+"\n"+
                string.Join("  |  ",Enumerable.Range(0,4).Select(i=>"P"+(i+1)+": "+(input.IsConnected(i)?"готов":"ожидает")));
            if(phase==Phase.Results)
            {
                var result=Session.Match.Read();
                status.text="ПОБЕДИЛ ИГРОК "+(result.Winner+1)+" · "+(result.Trigger=="score-limit"?"ЦЕЛЬ ПО ОЧКАМ":"ВРЕМЯ")+(input.Ready||diagnostic?"":"\nПодключите устройства для повтора или вернитесь в меню");
            }
        }
    }
}
