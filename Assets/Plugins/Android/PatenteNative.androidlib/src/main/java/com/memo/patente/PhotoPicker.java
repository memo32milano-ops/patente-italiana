package com.memo.patente;

import android.app.Activity;
import android.content.Intent;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.net.Uri;
import android.os.Bundle;

import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;

/** Transparent activity that lets the user pick a photo and returns a 96x96 JPEG path. */
public class PhotoPicker extends Activity {
    private static Native.Callback callback;

    public static void pick(Activity from, Native.Callback cb) {
        callback = cb;
        from.startActivity(new Intent(from, PhotoPicker.class));
    }

    @Override
    protected void onCreate(Bundle b) {
        super.onCreate(b);
        Intent i = new Intent(Intent.ACTION_GET_CONTENT);
        i.setType("image/*");
        try { startActivityForResult(Intent.createChooser(i, null), 42); }
        catch (Throwable e) { finishWith(null); }
    }

    @Override
    protected void onActivityResult(int req, int res, Intent data) {
        if (req != 42 || res != RESULT_OK || data == null || data.getData() == null) { finishWith(null); return; }
        try {
            Uri uri = data.getData();
            InputStream in = getContentResolver().openInputStream(uri);
            Bitmap src = BitmapFactory.decodeStream(in);
            if (in != null) in.close();
            int s = Math.min(src.getWidth(), src.getHeight());
            Bitmap sq = Bitmap.createBitmap(src, (src.getWidth() - s) / 2, (src.getHeight() - s) / 2, s, s);
            Bitmap small = Bitmap.createScaledBitmap(sq, 96, 96, true);
            File f = new File(getCacheDir(), "avatar_pick.jpg");
            FileOutputStream out = new FileOutputStream(f);
            small.compress(Bitmap.CompressFormat.JPEG, 72, out);
            out.close();
            finishWith(f.getAbsolutePath());
        } catch (Throwable e) {
            finishWith(null);
        }
    }

    private void finishWith(String path) {
        Native.Callback cb = callback;
        callback = null;
        if (cb != null) cb.done(path);
        finish();
    }
}
