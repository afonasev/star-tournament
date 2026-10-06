#if UNITY_EDITOR || DEVELOPMENT_BUILD || STAR_TOURNAMENT_DEVELOPMENT_QA
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    /// <summary>Focused ordinary-setup evidence for bot-owned views. Synthetic devices are never physical acceptance.</summary>
    public sealed class NativeBotSeatsReview : MonoBehaviour
    {
        const string Acceptance = "ORDINARY_SETUP_SYNTHETIC_INPUT_NOT_PHYSICAL_OR_PERFORMANCE_ACCEPTANCE";
        ProvingGround ground;
        string directory;
        Gamepad pad;
        Keyboard operatorKeyboard;
        readonly List<float> frameMilliseconds = new List<float>();

        [Serializable] sealed class Evidence
        {
            public string state, acceptance = Acceptance;
            public bool focused, muted, running;
            public int width, height, views, participants, shots, botTicks, frameSamples;
            public float frameMeanMilliseconds, frameP95Milliseconds;
            public uint seed;
            public double clock;
            public string movementProfile, lifeProfile, combatProfile, behaviorProfile;
            public NativeCompositionSnapshot composition;
            public NativeMatchSnapshot match;
            public CombatLifeState[] lives;
            public NativeBotPlannerState[] planners;
        }

        Button Button(string name) => ground.GetComponentsInChildren<Button>(true).Single(button => button.name == name);
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException("BOT_SEATS_REVIEW " + message); }
        void Update()
        {
            if (ground != null && ground.Running && Application.isFocused)
                frameMilliseconds.Add(Time.unscaledDeltaTime * 1000f);
        }
        void BeginFrameSamples() => frameMilliseconds.Clear();
        static float Percentile(List<float> values, float percentile)
        {
            if (values.Count == 0) return 0;
            var copy = values.OrderBy(value => value).ToArray();
            return copy[Mathf.Min(copy.Length - 1, Mathf.FloorToInt((copy.Length - 1) * percentile))];
        }
        IEnumerator WaitFocused(float seconds)
        {
            float elapsed = 0;
            while (elapsed < seconds)
            {
                if (Application.isFocused) elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        IEnumerator Join(Gamepad value)
        {
            InputSystem.QueueStateEvent(value, new GamepadState().WithButton(GamepadButton.Start));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(value, new GamepadState());
            yield return null; yield return null;
        }

        IEnumerator Capture(string state)
        {
            while (!Application.isFocused) yield return null;
            yield return new WaitForEndOfFrame();
            var count = ground.Session.ParticipantCount;
            var driver = ground.BotDriver;
            File.WriteAllText(Path.Combine(directory, state + ".json"), JsonUtility.ToJson(new Evidence
            {
                state = state,
                focused = Application.isFocused,
                muted = AudioListener.volume == 0,
                running = ground.Running,
                width = Screen.width,
                height = Screen.height,
                views = ground.LocalSeatCount,
                participants = count,
                shots = ground.Session.ShotCount,
                botTicks = driver?.Ticks ?? 0,
                frameSamples = frameMilliseconds.Count,
                frameMeanMilliseconds = frameMilliseconds.Count == 0 ? 0 : frameMilliseconds.Average(),
                frameP95Milliseconds = Percentile(frameMilliseconds, .95f),
                seed = driver?.Seed ?? 0,
                clock = ground.Session.Time,
                movementProfile = ground.Profile.Id + "@" + ground.Profile.Version,
                lifeProfile = ground.LifeProfile.Id + "@" + ground.LifeProfile.Version,
                combatProfile = ground.CombatProfile.Id + "@" + ground.CombatProfile.Version,
                behaviorProfile = ground.BotBehaviorProfile.Id + "@" + ground.BotBehaviorProfile.Version,
                composition = ground.Session.Match == null ? ground.SetupComposition().Read() : ground.Composition.Read(),
                match = ground.Session.Match?.Read(),
                lives = Enumerable.Range(0, count).Select(ground.Session.Life).ToArray(),
                planners = Enumerable.Range(0, count).Select(participant => driver?.Planner(participant)?.Capture()).ToArray()
            }, true));
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, state + ".png"));
            yield return null;
        }

        void SetViews(int count)
        {
            while (ground.LocalSeatCount > count) Button("seats-minus").onClick.Invoke();
            while (ground.LocalSeatCount < count) Button("seats-plus").onClick.Invoke();
        }

        void Configure(int views, params bool[] ai)
        {
            SetViews(views);
            for (var seat = 0; seat < views; seat++)
            {
                ground.SetSeatAi(seat, ai[seat]);
                if (ai[seat]) ground.SetSeatDifficulty(seat, seat % 3);
            }
        }

        IEnumerator PauseWithEscape()
        {
            Check(operatorKeyboard != null && operatorKeyboard.added, "synthetic operator keyboard unavailable for Escape review");
            InputSystem.QueueStateEvent(operatorKeyboard, new KeyboardState(Key.Escape));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(operatorKeyboard, new KeyboardState());
            yield return null;
            Check(!ground.Running, "operator Escape did not pause an all-AI match");
        }

        IEnumerator ObserveViewedDeathOrFixture(string prefix, int victim, int killer, float seconds)
        {
            float elapsed = 0;
            while (elapsed < seconds)
            {
                if (ground.Session.Life(victim).Dead)
                {
                    yield return Capture(prefix + "-natural-death");
                    yield return WaitFocused(Mathf.Min(.5f, ground.LifeProfile.Get("combat.killcamSeconds") / 2f));
                    yield return Capture(prefix + "-natural-killcam");
                    while (ground.Session.Life(victim).Dead) yield return null;
                    yield return Capture(prefix + "-natural-respawn");
                    yield break;
                }
                if (Application.isFocused) elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            // Separate, explicitly labelled lifecycle fixture: natural AI did not die in this bounded review window.
            ground.Session.ApplyDamage(victim, ground.Session.Life(victim).Life, 500, killer, ground.Session.Life(killer).Life);
            yield return null;
            yield return Capture(prefix + "-lifecycle-fixture-death");
            yield return WaitFocused(Mathf.Min(.5f, ground.LifeProfile.Get("combat.killcamSeconds") / 2f));
            yield return Capture(prefix + "-lifecycle-fixture-killcam");
            while (ground.Session.Life(victim).Dead) yield return null;
            yield return Capture(prefix + "-lifecycle-fixture-respawn");
        }

        IEnumerator Start()
        {
            ground = GetComponent<ProvingGround>();
            var args = Environment.GetCommandLineArgs();
            var evidence = Array.IndexOf(args, "-botSeatsEvidence");
            directory = evidence >= 0 && evidence + 1 < args.Length ? args[evidence + 1] : Path.Combine(Application.persistentDataPath, "bot-seats-review");
            Directory.CreateDirectory(directory);
            while (!Application.isFocused || !UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
            operatorKeyboard = InputSystem.AddDevice<Keyboard>();

            // Two views: a single human on the first view plus an AI, with one synthetic pad only for that human.
            Configure(2, false, true);
            pad = InputSystem.AddDevice<Gamepad>();
            yield return Join(pad);
            Button("Начать — четыре игрока").onClick.Invoke(); BeginFrameSamples(); yield return WaitFocused(4);
            Check(ground.Running && ground.BotDriver != null && ground.LocalSeatCount == 2, "mixed two-view ordinary start failed");
            yield return Capture("01-two-views-mixed-live");
            ground.SendMessage("Pause", "review transition"); Button("В главное меню").onClick.Invoke(); yield return null;
            InputSystem.RemoveDevice(pad); pad = null;

            Configure(2, true, true);
            Button("Начать — четыре игрока").onClick.Invoke(); BeginFrameSamples(); yield return WaitFocused(2);
            Check(ground.Running && ground.BotDriver != null, "all-AI two-view ordinary start failed");
            yield return Capture("02-two-views-all-ai-live");
            yield return PauseWithEscape(); Button("В главное меню").onClick.Invoke(); yield return null;

            // Three views: the only human is deliberately on a non-zero view, proving sparse ownership.
            Configure(3, true, false, true);
            pad = InputSystem.AddDevice<Gamepad>();
            yield return Join(pad);
            Button("Начать — четыре игрока").onClick.Invoke(); BeginFrameSamples(); yield return WaitFocused(5);
            Check(ground.Running && ground.Composition.ParticipantAt(1) >= 0 && ground.Composition.Participant(ground.Composition.ParticipantAt(1)).Kind == NativeParticipantKind.LocalHuman, "sparse human view was not preserved");
            yield return Capture("03-three-views-sparse-human-live");
            ground.SendMessage("Pause", "review transition"); Button("В главное меню").onClick.Invoke(); yield return null;
            InputSystem.RemoveDevice(pad); pad = null;

            Configure(3, true, true, true);
            Button("Начать — четыре игрока").onClick.Invoke(); BeginFrameSamples(); yield return WaitFocused(2);
            Check(ground.Running && ground.BotDriver != null, "all-AI three-view ordinary start failed");
            yield return Capture("04-three-views-all-ai-live");
            yield return PauseWithEscape(); Button("В главное меню").onClick.Invoke(); yield return null;

            Configure(4, true, true, true, false);
            pad = InputSystem.AddDevice<Gamepad>();
            yield return Join(pad);
            Button("Начать — четыре игрока").onClick.Invoke(); BeginFrameSamples(); yield return WaitFocused(3);
            Check(ground.Running && ground.BotDriver != null, "mixed four-view ordinary start failed");
            yield return Capture("05-four-views-mixed-live");
            ground.SendMessage("Pause", "review transition"); Button("В главное меню").onClick.Invoke(); yield return null;
            InputSystem.RemoveDevice(pad); pad = null;

            // Four all-AI views require no gameplay device. Teams prove that ordinary setup retains per-view teams.
            Configure(4, true, true, true, true);
            ground.SetMatchMode(NativeMatchMode.Teams);
            ground.SetTeam(0, NativeTeam.TeamA); ground.SetTeam(1, NativeTeam.TeamB);
            ground.SetTeam(2, NativeTeam.TeamA); ground.SetTeam(3, NativeTeam.TeamB);
            for (var i = 0; i < 4; i++) ground.AddBot();
            Button("Начать — четыре игрока").onClick.Invoke(); BeginFrameSamples(); yield return WaitFocused(7);
            Check(ground.Running && ground.BotDriver != null && ground.LocalSeatCount == 4, "all-AI four-view ordinary start failed");
            Check(ground.Session.ParticipantCount == 8, "all-AI review must retain eight total participants");
            Check(ground.BotDriver.Ticks > 0 && ground.Session.ShotCount > 0, "real AI did not plan and fire");
            yield return Capture("06-four-views-all-ai-teams-live");
            yield return ObserveViewedDeathOrFixture("07-four-views-viewed-bot", 0, 1, 25);

            yield return PauseWithEscape();
            var pausedClock = ground.Session.Time; var pausedTicks = ground.BotDriver.Ticks;
            yield return new WaitForSecondsRealtime(.25f);
            Check(ground.Session.Time == pausedClock && ground.BotDriver.Ticks == pausedTicks, "AI/session clocks advanced while paused");
            yield return Capture("08-all-ai-operator-paused");
            Button("Продолжить").onClick.Invoke(); yield return WaitFocused(1);
            Check(ground.Running, "operator resume failed"); yield return Capture("09-all-ai-resumed");
            var frozen = JsonUtility.ToJson(ground.Composition.Read()); var oldDriver = ground.BotDriver;
            yield return PauseWithEscape(); Button("Повторить матч").onClick.Invoke(); yield return null;
            Check(ground.Running && ground.BotDriver != oldDriver && JsonUtility.ToJson(ground.Composition.Read()) == frozen, "repeat changed frozen composition");
            yield return Capture("10-all-ai-repeat");
            yield return PauseWithEscape(); Button("В главное меню").onClick.Invoke(); yield return null;
            Check(ground.BotDriver == null && JsonUtility.ToJson(ground.SetupComposition().Read()) == frozen, "exit did not retain draft composition");
            yield return Capture("11-return-setup");
            File.WriteAllText(Path.Combine(directory, "complete.txt"), "PASS: ordinary 2/3/4 views, sparse human, all-AI, actual planners/fire, Escape pause, resume, frozen repeat, exit. Physical device, TV and target-performance acceptance remain open.");
            Debug.Log("NATIVE_BOT_SEATS_REVIEW_COMPLETE " + directory);
            Application.Quit();
        }

        void OnDestroy()
        {
            if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
            if (operatorKeyboard != null && operatorKeyboard.added) InputSystem.RemoveDevice(operatorKeyboard);
        }
    }
}
#endif

#endif
