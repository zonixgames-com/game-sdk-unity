using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace FantosiaGames
{
    // The Fantosia games protocol, version 1, for a Unity WebGL game framed by the Fantosia platform.
    //
    // One page load is one run. The game calls RequestRunStart when the player starts, waits for
    // RunStarted before the run begins, and calls EndRun with the final score. The platform saves
    // the run and shows its own result screen. The game makes no network request and receives
    // nothing about the player. See README.md.
    //
    // Outside an iframe (the Unity editor, a standalone build) Embedded is false: RequestRunStart
    // raises RunStarted at once and the other calls do nothing, so the game plays on its own.
    public static class Fantosia
    {
        // The platform page allowed to frame the build, chosen at build time with a scripting define.
#if FANTOSIA_PRODUCTION
        public const string PlatformOrigin = "https://platform.fantosia.com";
#elif FANTOSIA_STAGING
        public const string PlatformOrigin = "https://platform.fantosia.net";
#else
        public const string PlatformOrigin = "https://platform.fantosia.app";
#endif

        // technical: protocol limits (README.md)
        const int ErrorMaxLength = 200;
        const float ScoreInterval = 1f;

        // True when the build runs inside the platform's iframe.
        public static bool Embedded { get; private set; }

        // The platform answered RequestRunStart: start the run now.
        public static event Action RunStarted;
        // The platform refused the run (time up, signed out). It explains why to the player.
        public static event Action RunRefused;
        // The player pressed pause or resume in the platform's header.
        public static event Action Paused;
        public static event Action Resumed;
        // The player pressed the sound button in the platform's header.
        public static event Action Muted;
        public static event Action Unmuted;
        // The page does not speak this protocol version: the build is out of date, ask to reload.
        public static event Action Unsupported;

        // When true (the default) the SDK sets AudioListener.volume on mute and unmute itself.
        public static bool HandleSound = true;

        static bool runRequested;
        static bool runStarted;
        static bool runEnded;
        static float lastScoreAt = float.NegativeInfinity;

        public static bool IsRunStarted => runStarted;
        public static bool IsRunEnded => runEnded;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int FantosiaIsEmbedded();
        [DllImport("__Internal")] static extern void FantosiaConnect(string origin);
        [DllImport("__Internal")] static extern void FantosiaSend(string type, string dataJson);
#else
        static int FantosiaIsEmbedded() => 0;
        static void FantosiaConnect(string origin) { }
        static void FantosiaSend(string type, string dataJson) { }
#endif

        // Says ready before the first scene, so the platform knows the build has loaded.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            Embedded = FantosiaIsEmbedded() == 1;
            if (!Embedded) return;

            // The .jslib delivers the platform's messages to this object by name, in every scene.
            var bridge = new GameObject(FantosiaBridge.ObjectName);
            UnityEngine.Object.DontDestroyOnLoad(bridge);
            bridge.AddComponent<FantosiaBridge>();
            FantosiaConnect(PlatformOrigin);
        }

        // The player starts the run. Begin it only on RunStarted.
        public static void RequestRunStart()
        {
            if (runRequested || runEnded) return;
            runRequested = true;
            if (!Embedded)
            {
                runStarted = true;
                RunStarted?.Invoke();
                return;
            }
            FantosiaSend("run:start", "{}");
        }

        // Optional, during the run: the score shown in the platform's header. At most once a
        // second; never trusted by the platform.
        public static void ReportScore(long score)
        {
            if (!runStarted || runEnded || score < 0) return;
            if (Time.unscaledTime - lastScoreAt < ScoreInterval) return;
            lastScoreAt = Time.unscaledTime;
            FantosiaSend("run:score", "{\"score\":" + score + "}");
        }

        // The run is over and the score is final: a whole number, 0 or more. Call it once; after
        // it the game shows no Play again, menu, leaderboard or sign-in of its own.
        public static void EndRun(long score)
        {
            if (!runStarted || runEnded) return;
            runEnded = true;
            FantosiaSend("run:end", "{\"score\":" + Math.Max(0L, score) + "}");
        }

        // The build cannot continue. A short English text for the platform's logs.
        public static void Error(string message)
        {
            message = (message ?? "").Length > ErrorMaxLength ? message.Substring(0, ErrorMaxLength) : (message ?? "");
            FantosiaSend("error", JsonUtility.ToJson(new ErrorData { message = message }));
        }

        [Serializable]
        class ErrorData { public string message; }

        internal static void Receive(string type)
        {
            switch (type)
            {
                case "run:started":
                    if (!runRequested || runStarted) return;
                    runStarted = true;
                    RunStarted?.Invoke();
                    break;
                case "run:refused":
                    if (runStarted) return;
                    runRequested = false; // the player may try again
                    RunRefused?.Invoke();
                    break;
                case "pause": Paused?.Invoke(); break;
                case "resume": Resumed?.Invoke(); break;
                case "mute":
                    if (HandleSound) AudioListener.volume = 0f;
                    Muted?.Invoke();
                    break;
                case "unmute":
                    if (HandleSound) AudioListener.volume = 1f;
                    Unmuted?.Invoke();
                    break;
                case "unsupported": Unsupported?.Invoke(); break;
            }
        }
    }

    // Receives the platform's messages from the .jslib (SendMessage by object name).
    sealed class FantosiaBridge : MonoBehaviour
    {
        internal const string ObjectName = "FantosiaBridge";

        void OnFantosiaMessage(string type) => Fantosia.Receive(type);
    }
}
