package com.memo.patente;

import android.app.Activity;
import android.os.Bundle;
import android.speech.tts.TextToSpeech;
import android.speech.tts.UtteranceProgressListener;

import com.google.mlkit.common.model.DownloadConditions;
import com.google.mlkit.nl.translate.TranslateLanguage;
import com.google.mlkit.nl.translate.Translation;
import com.google.mlkit.nl.translate.Translator;
import com.google.mlkit.nl.translate.TranslatorOptions;

import java.util.HashMap;
import java.util.Locale;

/** Small bridge used by the Unity app: on-device translation (ML Kit) and Italian text-to-speech. */
public class Native {
    public interface Callback { void done(String result); }

    // ---------------- translation ----------------
    private static final HashMap<String, Translator> translators = new HashMap<>();

    public static boolean supports(String tag) {
        return TranslateLanguage.fromLanguageTag(tag) != null;
    }

    public static void translate(final String source, final String text, final String target, final Callback cb) {
        try {
            String src = TranslateLanguage.fromLanguageTag(source);
            String tgt = TranslateLanguage.fromLanguageTag(target);
            if (src == null || tgt == null) { cb.done(null); return; }
            String key = src + ">" + tgt;
            Translator t = translators.get(key);
            if (t == null) {
                TranslatorOptions o = new TranslatorOptions.Builder()
                        .setSourceLanguage(src)
                        .setTargetLanguage(tgt)
                        .build();
                t = Translation.getClient(o);
                translators.put(key, t);
            }
            final Translator tr = t;
            tr.downloadModelIfNeeded(new DownloadConditions.Builder().build())
                    .addOnSuccessListener(v -> tr.translate(text)
                            .addOnSuccessListener(cb::done)
                            .addOnFailureListener(e -> cb.done(null)))
                    .addOnFailureListener(e -> cb.done(null));
        } catch (Throwable e) {
            cb.done(null);
        }
    }

    // ---------------- Italian speech ----------------
    private static TextToSpeech tts;
    private static boolean ready;
    private static String pendingText;
    private static Callback pendingCb;
    private static float rate = 0.9f;
    private static final HashMap<String, Callback> callbacks = new HashMap<>();
    private static int counter;

    public static void speak(final Activity activity, final String text, final float speechRate, final Callback cb) {
        rate = speechRate;
        activity.runOnUiThread(() -> {
            if (tts == null) {
                pendingText = text;
                pendingCb = cb;
                tts = new TextToSpeech(activity.getApplicationContext(), status -> {
                    if (status != TextToSpeech.SUCCESS) {
                        // no speech engine: try again next time, and tell Unity (it shows how to install one)
                        ready = false; tts = null;
                        Callback c = pendingCb; pendingCb = null; pendingText = null;
                        if (c != null) c.done("novoice");
                        return;
                    }
                    int lr = tts.setLanguage(Locale.ITALY);
                    noItalian = lr < TextToSpeech.LANG_AVAILABLE;
                    tts.setOnUtteranceProgressListener(new UtteranceProgressListener() {
                        @Override public void onStart(String id) { }
                        @Override public void onDone(String id) { fire(id, "done"); }
                        @Override public void onError(String id) { fire(id, "error"); }
                        @Override public void onStop(String id, boolean interrupted) { fire(id, "stopped"); }
                    });
                    ready = true;
                    if (pendingText != null) { say(pendingText, pendingCb); pendingText = null; pendingCb = null; }
                });
            } else if (ready) {
                say(text, cb);
            } else {
                if (pendingCb != null && pendingCb != cb) pendingCb.done("stopped");   // replaced before the engine was ready
                pendingText = text;
                pendingCb = cb;
            }
        });
    }

    private static boolean noItalian;

    private static void fire(String id, String what) {
        Callback c;
        synchronized (callbacks) { c = callbacks.remove(id); }
        if (c != null) c.done(what);
    }

    private static void say(String text, Callback cb) {
        if (noItalian) {
            // the Italian voice data is missing: re-check (the user may have installed it meanwhile)
            noItalian = tts.isLanguageAvailable(Locale.ITALY) < TextToSpeech.LANG_AVAILABLE;
            if (noItalian) { if (cb != null) cb.done("novoice"); return; }
            tts.setLanguage(Locale.ITALY);
        }
        String id = "pa" + (++counter);
        if (cb != null) synchronized (callbacks) { callbacks.put(id, cb); }
        tts.setSpeechRate(rate);
        tts.speak(text, TextToSpeech.QUEUE_FLUSH, new Bundle(), id);
    }

    public static void stop() {
        if (tts != null) tts.stop();
    }

    public static void openTtsSettings(Activity a) {
        try {
            android.content.Intent i = new android.content.Intent("com.android.settings.TTS_SETTINGS");
            i.addFlags(android.content.Intent.FLAG_ACTIVITY_NEW_TASK);
            a.startActivity(i);
        } catch (Throwable e) {
            try {
                android.content.Intent i = new android.content.Intent(TextToSpeech.Engine.ACTION_INSTALL_TTS_DATA);
                i.addFlags(android.content.Intent.FLAG_ACTIVITY_NEW_TASK);
                a.startActivity(i);
            } catch (Throwable ignored) { }
        }
    }

    public static void share(Activity a, String text) {
        android.content.Intent i = new android.content.Intent(android.content.Intent.ACTION_SEND);
        i.setType("text/plain");
        i.putExtra(android.content.Intent.EXTRA_TEXT, text);
        android.content.Intent c = android.content.Intent.createChooser(i, null);
        c.addFlags(android.content.Intent.FLAG_ACTIVITY_NEW_TASK);
        a.startActivity(c);
    }

    public static boolean italianVoiceAvailable() {
        if (tts == null) return true;
        if (noItalian) return false;
        int r = tts.isLanguageAvailable(Locale.ITALY);
        return r >= TextToSpeech.LANG_AVAILABLE;
    }
}
