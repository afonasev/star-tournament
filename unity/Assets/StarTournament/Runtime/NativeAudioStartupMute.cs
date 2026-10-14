using System;
using System.Linq;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    /// <summary>Resolves launch audio policy before scene behaviours can create or play game audio.</summary>
    public static class NativeAudioStartupMute
    {
        static bool initialized;
        static bool muted;
        static bool silentCaptureActive;
        static bool sceneCallbackRan;

        public static bool SceneCallbackRan => sceneCallbackRan;

        public static bool IsMuted
        {
            get
            {
                if (!initialized) Initialize();
                return muted;
            }
        }

        public static void SetListenerVolume(float value)
        {
            AudioListener.volume = IsMuted && !(silentCaptureActive && value > 0) ? 0 : Mathf.Clamp01(value);
        }

        public static void SetSilentCaptureActive(bool active)
        {
            silentCaptureActive = active;
            if (!active && IsMuted) AudioListener.volume = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InitializeBeforeScene()
        {
            Initialize();
            sceneCallbackRan = true;
        }

        static void Initialize()
        {
            if (initialized) return;
            var args = Environment.GetCommandLineArgs();
            muted = ShouldMute(args, Application.isEditor, Application.isBatchMode);
            initialized = true;
            if (muted) AudioListener.volume = 0;
            else AudioListener.volume = 1;
        }

        public static bool ShouldMute(string[] args, bool isEditor, bool isBatchMode)
        {
            args = args ?? Array.Empty<string>();
            if (args.Contains("-noaudio") || args.Contains("-muted")) return true;
            if (isBatchMode || args.Contains("-runTests") || args.Contains("-diagnostic") ||
                !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_NAVIGATION_EVIDENCE")) ||
                args.Any(a => a.EndsWith("Review", StringComparison.Ordinal))) return true;
            if (args.Contains("-humanAudio")) return false;
            return isEditor;
        }
    }
}
