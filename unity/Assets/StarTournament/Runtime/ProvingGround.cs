using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

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
        readonly float[] nextShot = new float[SeatInputCoordinator.SeatCount];
        readonly string[] hit = new string[SeatInputCoordinator.SeatCount];
        enum Phase { Setup, Running, Paused }
        Phase phase;
        bool diagnostic;
        ProvingArena arena;
        GameObject overlay;
        Text status;
        Button start, resume, rebind;
        Font font;
        FpsDisplay fps;
        string pauseReason = "";
        float gameplayTime;
        public bool Running => phase == Phase.Running;
        void Start()
        {
            if (Profile.Validate().Count != 0) throw new InvalidOperationException("Invalid proving profile");
            Time.fixedDeltaTime=1f/Profile.Get("simulation.fixedTickHz");
            AudioListener.volume = 0; // QA is muted, including all Player diagnostics.
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var light = new GameObject("arena-light").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = Profile.Get("presentation.lightIntensity");
            light.transform.rotation = Quaternion.Euler(50, -30, 0); // Authored fixture light orientation.
            arena = new GameObject("unity-two-level-fixture-v1").AddComponent<ProvingArena>(); arena.Build(Profile);
            for (int i = 0; i < motors.Length; i++) CreateSeat(i);
            var args=Environment.GetCommandLineArgs(); int countFlag=Array.IndexOf(args,"-probeCameras");
            if(args.Contains("-diagnostic") && countFlag>=0 && countFlag+1<args.Length && int.TryParse(args[countFlag+1],out int count) && (count==1||count==2||count==4))
            {
                for(int i=0;i<cameras.Length;i++) { cameras[i].enabled=i<count; if(i<count && count<4) cameras[i].rect=new Rect(i/(float)count,0,1f/count,1); }
            }
            CreateInterface();
            gameObject.AddComponent<ProvingTelemetry>();
            bool requestedDiagnostic = Environment.GetCommandLineArgs().Contains("-diagnostic");
            if (requestedDiagnostic) Begin(true);
        }
        void CreateSeat(int index)
        {
            var player = new GameObject("player-" + (index+1)); player.layer = ProvingArena.ParticipantLayer;
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
            SetLayer(body, 11+index);
            var cameraObject = new GameObject("seat-camera-" + (index+1));
            cameras[index] = cameraObject.AddComponent<Camera>();
            cameras[index].rect = new Rect(index%2/2f, index<2 ? .5f : 0, .5f, .5f);
            cameras[index].fieldOfView = Profile.Get("camera.fieldOfViewDegrees");
            cameras[index].nearClipPlane = Profile.Get("camera.nearClipPlane"); cameras[index].farClipPlane = Profile.Get("camera.farClipPlane");
            cameras[index].cullingMask = ~((1 << (11+index)) | (15 << 15)) | (1 << (15+index));
            cameras[index].backgroundColor = new Color32(15, 22, 35, 255);
            var view = Instantiate(ViewWeaponPrefab, cameraObject.transform); SetLayer(view, 15+index);
            view.transform.localPosition=new Vector3(Profile.Get("presentation.viewWeaponX"),Profile.Get("presentation.viewWeaponY"),Profile.Get("presentation.viewWeaponZ"));
            if (index == 0) cameraObject.AddComponent<AudioListener>();
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
                else { input.Capture(Profile, Time.deltaTime); if (input.ConsumePause()) Pause("Пауза"); }
            }
            if (diagnostic && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Pause("Диагностическая пауза");
            RefreshInterface();
        }
        void FixedUpdate()
        {
            if (phase != Phase.Running) return;
            gameplayTime+=Time.fixedDeltaTime;
            for (int i = 0; i < motors.Length; i++)
            {
                var action = diagnostic ? default : input.Consume(i);
                motors[i].Tick(action, Time.fixedDeltaTime);
                if (action.Fire && gameplayTime >= nextShot[i])
                {
                    var state = motors[i].State;
                    var origin = state.Position + Vector3.up * Profile.Get("camera.eyeHeight");
                    var direction = Quaternion.Euler(state.Pitch, state.Yaw, 0) * Vector3.forward;
                    hit[i] = Physics.Raycast(origin, direction, out var impact, Profile.Get("weapon.probeRange"), ProvingArena.ShotMask, QueryTriggerInteraction.Ignore)
                        ? impact.collider.name : "мимо";
                    nextShot[i] = gameplayTime + Profile.Get("weapon.probeCooldown");
                }
                if(hud[i]) hud[i].text = "ИГРОК " + (i+1) + "  ·  " + (diagnostic ? "DIAGNOSTIC" : input.Label(i)) + "\nПОЛИГОН · " + (hit[i] ?? "проверка движения") +
                    (action.ShowRoster ? "\n" + string.Join("\n", Enumerable.Range(0, 4).Select(n => "Игрок " + (n+1) + ": " + input.Label(n))) : "");
            }
        }
        void LateUpdate()
        {
            for (int i = 0; i < motors.Length; i++) if (motors[i])
            {
                var state = motors[i].State;
                cameras[i].transform.SetPositionAndRotation(state.Position + Vector3.up*Profile.Get("camera.eyeHeight"), Quaternion.Euler(state.Pitch,state.Yaw,0));
            }
        }
        void OnApplicationFocus(bool focus) { if (!focus && phase == Phase.Running && !diagnostic) Pause("Окно потеряло фокус"); }
        void Begin(bool diagnostics)
        {
            if (!diagnostics && !input.Ready) return;
            diagnostic = diagnostics; input.Clear(); phase = Phase.Running; SetCursor(!diagnostics && input.HasKeyboard);
        }
        void Pause(string reason) { phase = Phase.Paused; pauseReason = reason; input.Clear(); SetCursor(false); RefreshInterface(); Select(diagnostic||input.Ready ? resume : rebind); }
        void Resume() { if (diagnostic || input.Ready) { input.Clear(); phase = Phase.Running; SetCursor(!diagnostic && input.HasKeyboard); } }
        void ResetSetup() { phase = Phase.Setup; diagnostic = false; input.Reset(); SetCursor(false); RefreshInterface(); Select(rebind); }
        static void Select(Button button) { if(EventSystem.current) { EventSystem.current.firstSelectedGameObject=button.gameObject; EventSystem.current.SetSelectedGameObject(button.gameObject); } }
        static void SetCursor(bool capture) { Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !capture; }
        void OnDestroy() { SetCursor(false); }
        void CreateInterface()
        {
            var eventSystem = new GameObject("input-events"); eventSystem.AddComponent<EventSystem>(); eventSystem.AddComponent<InputSystemUIInputModule>();
            var canvasObject = new GameObject("native-ui", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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
                var cross = TextElement(canvasObject.transform,"crosshair-"+i,"+",(int)Profile.Get("ui.headingFontSize"));
                Layout(cross.rectTransform,new Vector2(rect.xMin,rect.yMin),new Vector2(rect.xMax,rect.yMax)); cross.alignment=TextAnchor.MiddleCenter;
            }
            overlay = new GameObject("setup-pause",typeof(RectTransform),typeof(Image)); overlay.transform.SetParent(canvasObject.transform,false);
            Layout((RectTransform)overlay.transform,Vector2.zero,Vector2.one); overlay.GetComponent<Image>().color=new Color32(10,18,30,245);
            var column = new GameObject("menu",typeof(RectTransform),typeof(VerticalLayoutGroup)); column.transform.SetParent(overlay.transform,false);
            Layout((RectTransform)column.transform,new Vector2(.15f,.15f),new Vector2(.85f,.85f));
            var layout=column.GetComponent<VerticalLayoutGroup>(); layout.childControlHeight=true; layout.childForceExpandHeight=true;
            status=TextElement(column.transform,"status","",(int)Profile.Get("ui.headingFontSize")); status.alignment=TextAnchor.MiddleCenter;
            start=ButtonElement(column.transform,"Начать — четыре игрока",()=>Begin(false));
            resume=ButtonElement(column.transform,"Продолжить",Resume);
            rebind=ButtonElement(column.transform,"Назначить устройства заново",ResetSetup);
            ButtonElement(column.transform,"Диагностика четырёх камер · без управления",()=>Begin(true));
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
            status.text="STAR TOURNAMENT · UNITY ПОЛИГОН\n"+(phase==Phase.Paused ? pauseReason : "Space — клавиатура/мышь · Start — присоединить геймпад")+"\n"+
                string.Join("\n",Enumerable.Range(0,4).Select(i=>"Игрок "+(i+1)+": "+input.Label(i)+(input.IsConnected(i)?"":" · нет устройства")));
        }
    }
}
