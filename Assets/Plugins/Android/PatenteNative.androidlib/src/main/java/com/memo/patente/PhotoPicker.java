package com.memo.patente;

import android.app.Activity;
import android.content.Intent;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.provider.MediaStore;

import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;

/**
 * Transparent activity that lets the user pick a photo and returns a 256x256 JPEG path.
 * Result to Unity: a file path, "cancel" (user closed the gallery) or "error" (the photo could not be read).
 * Uses the system photo picker on Android 13+ (no permission needed), the gallery or a file chooser before.
 */
public class PhotoPicker extends Activity {
    private static Native.Callback callback;
    private static final int REQ = 42;

    public static void pick(Activity from, Native.Callback cb) {
        callback = cb;
        from.startActivity(new Intent(from, PhotoPicker.class));
    }

    @Override
    protected void onCreate(Bundle b) {
        super.onCreate(b);
        if (b != null) return;   // recreated after rotation: the picker is already open
        Intent[] tries;
        if (Build.VERSION.SDK_INT >= 33) {
            tries = new Intent[] { new Intent(MediaStore.ACTION_PICK_IMAGES).setType("image/*"),
                                   new Intent(Intent.ACTION_GET_CONTENT).setType("image/*") };
        } else {
            tries = new Intent[] { new Intent(Intent.ACTION_PICK, MediaStore.Images.Media.EXTERNAL_CONTENT_URI),
                                   new Intent(Intent.ACTION_GET_CONTENT).setType("image/*").addCategory(Intent.CATEGORY_OPENABLE) };
        }
        for (Intent i : tries) {
            try { startActivityForResult(i, REQ); return; } catch (Throwable ignored) { }
        }
        finishWith("error");
    }

    @Override
    protected void onActivityResult(int req, int res, Intent data) {
        if (req != REQ) return;
        if (res != RESULT_OK || data == null || data.getData() == null) { finishWith("cancel"); return; }
        try {
            Uri uri = data.getData();
            // read the size first, then decode a reduced copy (big camera photos would run out of memory)
            BitmapFactory.Options o = new BitmapFactory.Options();
            o.inJustDecodeBounds = true;
            InputStream in = getContentResolver().openInputStream(uri);
            BitmapFactory.decodeStream(in, null, o);
            if (in != null) in.close();
            int sample = 1;
            while (Math.min(o.outWidth, o.outHeight) / (sample * 2) >= 256) sample *= 2;
            BitmapFactory.Options o2 = new BitmapFactory.Options();
            o2.inSampleSize = sample;
            in = getContentResolver().openInputStream(uri);
            Bitmap src = BitmapFactory.decodeStream(in, null, o2);
            if (in != null) in.close();
            if (src == null) { finishWith("error"); return; }
            int s = Math.min(src.getWidth(), src.getHeight());
            Bitmap sq = Bitmap.createBitmap(src, (src.getWidth() - s) / 2, (src.getHeight() - s) / 2, s, s);
            Bitmap small = Bitmap.createScaledBitmap(sq, 256, 256, true);
            File f = new File(getCacheDir(), "avatar_pick.jpg");
            FileOutputStream out = new FileOutputStream(f);
            small.compress(Bitmap.CompressFormat.JPEG, 80, out);
            out.close();
            finishWith(f.getAbsolutePath());
        } catch (Throwable e) {
            finishWith("error");
        }
    }

    private void finishWith(String result) {
        Native.Callback cb = callback;
        callback = null;
        if (cb != null) cb.done(result);
        finish();
    }
}
