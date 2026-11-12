package com.memo.patente;

import android.app.AlarmManager;
import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.os.Build;

import java.util.Calendar;

/** Daily study reminder (local notification, no server). */
public class ReminderReceiver extends BroadcastReceiver {
    private static final String PREFS = "patente_reminder";
    private static final String CHANNEL = "study";

    @Override
    public void onReceive(Context ctx, Intent intent) {
        SharedPreferences sp = ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
        if (intent != null && Intent.ACTION_BOOT_COMPLETED.equals(intent.getAction())) {
            // phone restarted: alarms are lost, schedule again if the reminder is on
            if (sp.getBoolean("on", false))
                schedule(ctx, sp.getInt("hour", 19), sp.getInt("minute", 0), sp.getString("title", "Patente Italiana"), sp.getString("text", ""));
            return;
        }
        String title = sp.getString("title", "Patente Italiana");
        String text = sp.getString("text", "");
        NotificationManager nm = (NotificationManager) ctx.getSystemService(Context.NOTIFICATION_SERVICE);
        if (Build.VERSION.SDK_INT >= 26) {
            nm.createNotificationChannel(new NotificationChannel(CHANNEL, title, NotificationManager.IMPORTANCE_DEFAULT));
        }
        Intent launch = ctx.getPackageManager().getLaunchIntentForPackage(ctx.getPackageName());
        PendingIntent pi = PendingIntent.getActivity(ctx, 0, launch, PendingIntent.FLAG_IMMUTABLE | PendingIntent.FLAG_UPDATE_CURRENT);
        Notification.Builder b = Build.VERSION.SDK_INT >= 26 ? new Notification.Builder(ctx, CHANNEL) : new Notification.Builder(ctx);
        b.setSmallIcon(ctx.getApplicationInfo().icon).setContentTitle(title).setContentText(text).setAutoCancel(true).setContentIntent(pi);
        try { nm.notify(1, b.build()); } catch (SecurityException ignored) { }
        schedule(ctx, sp.getInt("hour", 19), sp.getInt("minute", 0), title, text); // next day
    }

    private static PendingIntent pending(Context ctx) {
        Intent i = new Intent(ctx, ReminderReceiver.class);
        return PendingIntent.getBroadcast(ctx, 7, i, PendingIntent.FLAG_IMMUTABLE | PendingIntent.FLAG_UPDATE_CURRENT);
    }

    public static void schedule(Context ctx, int hour, int minute, String title, String text) {
        ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit()
                .putInt("hour", hour).putInt("minute", minute).putString("title", title).putString("text", text).putBoolean("on", true).apply();
        Calendar c = Calendar.getInstance();
        c.set(Calendar.HOUR_OF_DAY, hour);
        c.set(Calendar.MINUTE, minute);
        c.set(Calendar.SECOND, 0);
        if (c.getTimeInMillis() <= System.currentTimeMillis() + 1000) c.add(Calendar.DAY_OF_YEAR, 1);
        AlarmManager am = (AlarmManager) ctx.getSystemService(Context.ALARM_SERVICE);
        // allowed while the phone dozes (no exact-alarm permission needed)
        if (android.os.Build.VERSION.SDK_INT >= 23) am.setAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, c.getTimeInMillis(), pending(ctx));
        else am.set(AlarmManager.RTC_WAKEUP, c.getTimeInMillis(), pending(ctx));
    }

    public static void cancel(Context ctx) {
        ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit().putBoolean("on", false).apply();
        AlarmManager am = (AlarmManager) ctx.getSystemService(Context.ALARM_SERVICE);
        am.cancel(pending(ctx));
    }
}
