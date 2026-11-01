using System;
using System.Collections.Generic;
using UnityEngine;

namespace PatenteArabo
{
    /// <summary>Runs callbacks from Android threads on Unity's main thread.</summary>
    public class MainThread : MonoBehaviour
    {
        static MainThread _i;
        static readonly Queue<Action> Q = new Queue<Action>();

        public static void Init()
        {
            if (_i != null) return;
            var go = new GameObject("MainThread");
            DontDestroyOnLoad(go);
            _i = go.AddComponent<MainThread>();
        }

        public static void Run(Action a) { lock (Q) Q.Enqueue(a); }

        void Update()
        {
            while (true)
            {
                Action a;
                lock (Q) { if (Q.Count == 0) break; a = Q.Dequeue(); }
                try { a(); } catch (Exception e) { Debug.LogException(e); }
            }
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    class NativeCallback : AndroidJavaProxy
    {
        readonly Action<string> _a;
        public NativeCallback(Action<string> a) : base("com.memo.patente.Native$Callback") { _a = a; }
        void done(string result) { var a = _a; MainThread.Run(() => a?.Invoke(result)); }
    }
#endif

    /// <summary>Reads Italian text aloud with the phone's text-to-speech.</summary>
    public static class ItalianTts
    {
        public static float Rate
        {
            get => PlayerPrefs.GetFloat("pa_tts_rate", 0.9f);
            set { PlayerPrefs.SetFloat("pa_tts_rate", value); PlayerPrefs.Save(); }
        }

        /// <summary>Speaks the text; onDone is called when reading ends (or immediately where speech is unavailable).</summary>
        public static void Speak(string text, Action onDone = null)
        {
            if (string.IsNullOrEmpty(text)) { onDone?.Invoke(); return; }
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var native = new AndroidJavaClass("com.memo.patente.Native"))
                {
                    var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    native.CallStatic("speak", activity, text, Rate, new NativeCallback(_ => onDone?.Invoke()));
                }
            }
            catch (Exception e) { Debug.LogWarning("TTS: " + e.Message); onDone?.Invoke(); }
#else
            Debug.Log("[TTS] " + text);
            Toast.Show(Lang.T("tts_phone_only"));
            if (onDone != null) MainThread.Run(onDone);
#endif
        }

        public static void Stop()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { using (var native = new AndroidJavaClass("com.memo.patente.Native")) native.CallStatic("stop"); } catch { }
#endif
        }
    }

    /// <summary>On-device machine translation Italian → user's language (Google ML Kit, free, works offline after a one-time model download).</summary>
    public static class Translator
    {
        public static void Translate(string italian, string target, Action<string> done) => TranslateFrom("it", italian, target, done);

        public static void TranslateFrom(string source, string text, string target, Action<string> done)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var native = new AndroidJavaClass("com.memo.patente.Native"))
                    native.CallStatic("translate", source, text, target, new NativeCallback(done));
            }
            catch (Exception e) { Debug.LogWarning("MT: " + e.Message); done?.Invoke(null); }
#else
            done?.Invoke(null);
#endif
        }
    }
}
