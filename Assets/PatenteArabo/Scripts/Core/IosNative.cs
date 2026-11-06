#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;

namespace PatenteArabo
{
    /// <summary>Bridge to Plugins/iOS/PatenteNative.swift. Results come back through one callback (request id, text).</summary>
    public static class IosNative
    {
        delegate void Callback(int id, IntPtr text);

        [DllImport("__Internal")] static extern void pa_set_callback(Callback cb);
        [DllImport("__Internal")] static extern IntPtr pa_locale();
        [DllImport("__Internal")] static extern void pa_speak(string text, float rate, int id);
        [DllImport("__Internal")] static extern void pa_stop();
        [DllImport("__Internal")] static extern bool pa_voice_available();
        [DllImport("__Internal")] static extern void pa_translate(string source, string text, string target, int id);
        [DllImport("__Internal")] static extern void pa_share(string text);
        [DllImport("__Internal")] static extern void pa_open_settings();
        [DllImport("__Internal")] static extern void pa_pick_photo(int id);
        [DllImport("__Internal")] static extern void pa_reminder(bool on, int hour, string title, string text);
        [DllImport("__Internal")] static extern void pa_store_init(string ids);
        [DllImport("__Internal")] static extern void pa_store_buy(string id);
        [DllImport("__Internal")] static extern void pa_store_restore();

        static readonly Dictionary<int, Action<string>> Pending = new Dictionary<int, Action<string>>();
        static int _next = 1;
        static bool _ready;
        public static Action<string> StoreEvents;   // id 0

        static void Ensure()
        {
            if (_ready) return;
            _ready = true;
            pa_set_callback(OnNative);
        }

        [MonoPInvokeCallback(typeof(Callback))]
        static void OnNative(int id, IntPtr text)
        {
            string s = text == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(text);
            if (id == 0) { var se = StoreEvents; MainThread.Run(() => se?.Invoke(s)); return; }
            Action<string> a;
            lock (Pending) { if (!Pending.TryGetValue(id, out a)) return; Pending.Remove(id); }
            MainThread.Run(() => a?.Invoke(s));
        }

        static int Register(Action<string> a)
        {
            Ensure();
            lock (Pending) { int id = _next++; Pending[id] = a; return id; }
        }

        public static string Locale()
        {
            var p = pa_locale();
            if (p == IntPtr.Zero) return null;
            var s = Marshal.PtrToStringUTF8(p);
            Marshal.FreeHGlobal(p);
            return s;
        }

        public static void Speak(string text, float rate, Action<string> done) => pa_speak(text, rate, Register(done));
        public static void Stop() => pa_stop();
        public static bool VoiceAvailable() => pa_voice_available();
        public static void Translate(string source, string text, string target, Action<string> done) => pa_translate(source, text, target, Register(done));
        public static void Share(string text) => pa_share(text);
        public static void OpenSettings() => pa_open_settings();
        public static void PickPhoto(Action<string> done) => pa_pick_photo(Register(done));
        public static void Reminder(bool on, int hour, string title, string text) => pa_reminder(on, hour, title, text);
        public static void StoreInit(string ids, Action<string> events) { Ensure(); StoreEvents = events; pa_store_init(ids); }
        public static void StoreBuy(string id) => pa_store_buy(id);
        public static void StoreRestore() => pa_store_restore();
    }
}
#endif
