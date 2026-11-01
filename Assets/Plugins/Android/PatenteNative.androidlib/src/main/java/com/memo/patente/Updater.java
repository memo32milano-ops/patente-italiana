package com.memo.patente;

import android.app.Activity;

import com.google.android.gms.tasks.OnSuccessListener;
import com.google.android.play.core.appupdate.AppUpdateInfo;
import com.google.android.play.core.appupdate.AppUpdateManager;
import com.google.android.play.core.appupdate.AppUpdateManagerFactory;
import com.google.android.play.core.appupdate.AppUpdateOptions;
import com.google.android.play.core.install.InstallState;
import com.google.android.play.core.install.InstallStateUpdatedListener;
import com.google.android.play.core.install.model.AppUpdateType;
import com.google.android.play.core.install.model.InstallStatus;
import com.google.android.play.core.install.model.UpdateAvailability;

/**
 * Google Play in-app updates. When a newer version is published on Google Play, every user sees the
 * update screen as soon as the app opens (or comes back to the foreground):
 * - FLEXIBLE: Google Play shows an "Update" dialog, downloads in the background, then the app restarts to install;
 * - IMMEDIATE (full screen, must update) when the release has in-app update priority 4-5 or the user is 7+ days behind.
 * Works only for installs that come from Google Play (not for a side-loaded test APK).
 */
public class Updater {
    private static final int REQUEST = 4711;
    private static AppUpdateManager manager;
    private static boolean listening;
    private static int askedFor = -1;   // version already offered in this session

    public static void check(final Activity activity, final Native.Callback cb) {
        try {
            if (manager == null) manager = AppUpdateManagerFactory.create(activity.getApplicationContext());
            manager.getAppUpdateInfo().addOnSuccessListener(new OnSuccessListener<AppUpdateInfo>() {
                @Override public void onSuccess(AppUpdateInfo info) {
                    try {
                        int avail = info.updateAvailability();
                        if (info.installStatus() == InstallStatus.DOWNLOADED) {
                            if (cb != null) cb.done("downloaded");
                            manager.completeUpdate();
                            return;
                        }
                        if (avail == UpdateAvailability.DEVELOPER_TRIGGERED_UPDATE_IN_PROGRESS) {
                            manager.startUpdateFlowForResult(info, activity, AppUpdateOptions.newBuilder(AppUpdateType.IMMEDIATE).build(), REQUEST);
                            return;
                        }
                        if (avail != UpdateAvailability.UPDATE_AVAILABLE) { if (cb != null) cb.done("none"); return; }
                        if (askedFor == info.availableVersionCode()) return;
                        askedFor = info.availableVersionCode();
                        if (cb != null) cb.done("available|" + info.availableVersionCode());
                        Integer stale = info.clientVersionStalenessDays();
                        boolean urgent = info.updatePriority() >= 4 || (stale != null && stale >= 7);
                        if (urgent && info.isUpdateTypeAllowed(AppUpdateType.IMMEDIATE)) {
                            manager.startUpdateFlowForResult(info, activity, AppUpdateOptions.newBuilder(AppUpdateType.IMMEDIATE).build(), REQUEST);
                        } else if (info.isUpdateTypeAllowed(AppUpdateType.FLEXIBLE)) {
                            if (!listening) {
                                listening = true;
                                manager.registerListener(new InstallStateUpdatedListener() {
                                    @Override public void onStateUpdate(InstallState state) {
                                        if (state.installStatus() == InstallStatus.DOWNLOADED) {
                                            if (cb != null) cb.done("downloaded");
                                            manager.completeUpdate();
                                        }
                                    }
                                });
                            }
                            manager.startUpdateFlowForResult(info, activity, AppUpdateOptions.newBuilder(AppUpdateType.FLEXIBLE).build(), REQUEST);
                        }
                    } catch (Exception e) { if (cb != null) cb.done("error|" + e.getMessage()); }
                }
            });
        } catch (Exception e) { if (cb != null) cb.done("error|" + e.getMessage()); }
    }
}
