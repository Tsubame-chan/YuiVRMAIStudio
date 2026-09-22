package jp.tsubamechan.yuivrm.localai;

import android.app.Activity;
import android.content.ContentResolver;
import android.content.Intent;
import android.database.Cursor;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Matrix;
import android.media.ExifInterface;
import android.net.Uri;
import android.os.Bundle;
import android.provider.OpenableColumns;
import android.webkit.MimeTypeMap;
import com.unity3d.player.UnityPlayer;
import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.util.Locale;
import java.util.UUID;

public final class YuiFilePickerActivity extends Activity {
    public static final String EXTRA_MODE = "jp.tsubamechan.yuivrm.localai.EXTRA_MODE";
    public static final String EXTRA_CALLBACK_OBJECT = "jp.tsubamechan.yuivrm.localai.EXTRA_CALLBACK_OBJECT";

    private static final int REQUEST_OPEN_DOCUMENT = 40071;
    private static final String CANCELLED = "__YUI_CANCELLED__";
    private static final String ERROR_PREFIX = "__YUI_ERROR__:";

    private String mode;
    private String callbackObjectName;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        mode = getIntent().getStringExtra(EXTRA_MODE);
        if (mode == null || mode.trim().isEmpty()) {
            mode = "image";
        }
        callbackObjectName = getIntent().getStringExtra(EXTRA_CALLBACK_OBJECT);
        if (callbackObjectName == null) {
            callbackObjectName = "";
        }

        if (savedInstanceState != null) return; // The OS restores the existing picker after rotation.

        Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT);
        intent.addCategory(Intent.CATEGORY_OPENABLE);
        intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
        if ("vrm".equals(mode) || "avatar".equals(mode)) {
            intent.setType("*/*");
            // Providers disagree on the MIME type of .vrm. Validate the filename after selection.

        } else {
            intent.setType("image/*");
        }

        try {
            startActivityForResult(intent, REQUEST_OPEN_DOCUMENT);
        } catch (Throwable ex) {
            send(ERROR_PREFIX + "ファイル選択を開始できませんでした: " + message(ex));
            finish();
        }
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode != REQUEST_OPEN_DOCUMENT) {
            finish();
            return;
        }

        if (resultCode != RESULT_OK || data == null || data.getData() == null) {
            send(CANCELLED);
            finish();
            return;
        }

        Uri uri = data.getData();
        new Thread(() -> {
            String result;
            try { result = copyToAppStorage(uri, mode); }
            catch (Throwable ex) { result = ERROR_PREFIX + message(ex); }
            final String message = result;
            runOnUiThread(() -> { send(message); finish(); });
        }, "YuiFileCopy").start();
    }

    private String copyToAppStorage(Uri uri, String mode) throws Exception {
        boolean avatarMode = "avatar".equals(mode);
        boolean documentMode = "vrm".equals(mode) || avatarMode;
        File root = new File(getCacheDir(), "YuiPickedFiles/" + (documentMode ? "Avatar/" : "Image/") + UUID.randomUUID());
        if (!root.isDirectory() && !root.mkdirs()) {
            throw new IllegalStateException("Failed to create directory: " + root.getAbsolutePath());
        }

        String extension = extensionFor(uri, mode);
        String allowed = documentMode ? (avatarMode ? ",vrm,zip," : ",vrm,") : ",png,jpg,jpeg,webp,heic,heif,";
        if (!allowed.contains("," + extension + ","))
            throw new IllegalArgumentException(documentMode ? "Choose a VRM or a ZIP exported by Yui Avatar Bridge." : "Choose a PNG, JPEG, WebP or HEIF image.");
        String name = displayNameFor(uri).replaceAll("[/\\\\\\p{Cntrl}]", "_");
        if (name.isEmpty() || extensionFromName(name).isEmpty()) name = "selected." + extension;
        File target = new File(root, name);
        long total = 0, limit = (documentMode ? 512L : 64L) * 1024 * 1024;
        try (InputStream input = getContentResolver().openInputStream(uri);
             FileOutputStream output = new FileOutputStream(target, false)) {
            if (input == null) {
                throw new IllegalStateException("Content resolver returned null stream.");
            }
            byte[] buffer = new byte[256 * 1024];
            int read;
            while ((read = input.read(buffer)) >= 0) {
                if (read > 0) {
                    total += read;
                    if (total > limit) throw new IllegalArgumentException(documentMode ? "Choose an avatar file smaller than 512 MB." : "Choose an image smaller than 64 MB.");
                    output.write(buffer, 0, read);
                }
            }
        } catch (Exception ex) {
            target.delete();
            throw ex;
        }
        if (!documentMode) {
            try { return normalizedImage(target); }
            finally { target.delete(); }
        }
        return target.getAbsolutePath();
    }

    private String normalizedImage(File source) throws Exception {
        BitmapFactory.Options bounds = new BitmapFactory.Options();
        bounds.inJustDecodeBounds = true;
        BitmapFactory.decodeFile(source.getAbsolutePath(), bounds);
        if (bounds.outWidth <= 0 || bounds.outHeight <= 0)
            throw new IllegalArgumentException("Could not decode this image. Try PNG or JPEG.");
        BitmapFactory.Options options = new BitmapFactory.Options();
        options.inSampleSize = 1;
        while (Math.max(bounds.outWidth, bounds.outHeight) / options.inSampleSize > 1600) options.inSampleSize *= 2;
        Bitmap bitmap = BitmapFactory.decodeFile(source.getAbsolutePath(), options);
        if (bitmap == null) throw new IllegalArgumentException("Could not decode this image. Try PNG or JPEG.");
        File target = new File(source.getParentFile(), UUID.randomUUID() + ".jpg");
        try {
            int orientation = ExifInterface.ORIENTATION_NORMAL;
            try {
                orientation = new ExifInterface(source.getAbsolutePath()).getAttributeInt(ExifInterface.TAG_ORIENTATION, orientation);
            } catch (java.io.IOException ignored) { /* Some valid image formats have no EXIF reader. */ }
            Matrix matrix = new Matrix();
            switch (orientation) {
                case ExifInterface.ORIENTATION_ROTATE_90: matrix.postRotate(90); break;
                case ExifInterface.ORIENTATION_ROTATE_180: matrix.postRotate(180); break;
                case ExifInterface.ORIENTATION_ROTATE_270: matrix.postRotate(270); break;
                case ExifInterface.ORIENTATION_FLIP_HORIZONTAL: matrix.setScale(-1, 1); break;
                case ExifInterface.ORIENTATION_FLIP_VERTICAL: matrix.setScale(1, -1); break;
                case ExifInterface.ORIENTATION_TRANSPOSE: matrix.setScale(-1, 1); matrix.postRotate(270); break;
                case ExifInterface.ORIENTATION_TRANSVERSE: matrix.setScale(-1, 1); matrix.postRotate(90); break;
            }
            if (!matrix.isIdentity()) {
                Bitmap rotated = Bitmap.createBitmap(bitmap, 0, 0, bitmap.getWidth(), bitmap.getHeight(), matrix, true);
                if (rotated != bitmap) { bitmap.recycle(); bitmap = rotated; }
            }
            try (FileOutputStream output = new FileOutputStream(target)) {
                if (!bitmap.compress(Bitmap.CompressFormat.JPEG, 90, output))
                    throw new IllegalArgumentException("Could not decode this image. Try PNG or JPEG.");
            }
            return target.getAbsolutePath();
        } catch (Exception ex) { target.delete(); throw ex; }
        finally { bitmap.recycle(); }
    }

    private String extensionFor(Uri uri, String mode) {
        String displayName = displayNameFor(uri);
        String fromName = extensionFromName(displayName);
        if (!fromName.isEmpty()) {
            return fromName;
        }

        ContentResolver resolver = getContentResolver();
        String mime = resolver.getType(uri);
        String fromMime = mime == null ? "" : MimeTypeMap.getSingleton().getExtensionFromMimeType(mime);
        if (fromMime != null && !fromMime.trim().isEmpty()) {
            return fromMime.toLowerCase(Locale.US);
        }

        return ""; // Never relabel an unknown document as a supported avatar/image.
    }

    private String displayNameFor(Uri uri) {
        try (Cursor cursor = getContentResolver().query(uri, null, null, null, null)) {
            if (cursor != null && cursor.moveToFirst()) {
                int index = cursor.getColumnIndex(OpenableColumns.DISPLAY_NAME);
                if (index >= 0) {
                    String name = cursor.getString(index);
                    return name == null ? "" : name;
                }
            }
        } catch (Throwable ignored) {
            // Fall through to path parsing.
        }
        return uri.getLastPathSegment() == null ? "" : uri.getLastPathSegment();
    }

    private static String extensionFromName(String name) {
        if (name == null) {
            return "";
        }
        int index = name.lastIndexOf('.');
        if (index < 0 || index >= name.length() - 1) {
            return "";
        }
        String extension = name.substring(index + 1).trim().toLowerCase(Locale.US);
        return extension.replaceAll("[^a-z0-9]", "");
    }

    private void send(String message) {
        if (callbackObjectName != null && !callbackObjectName.isEmpty()) {
            UnityPlayer.UnitySendMessage(callbackObjectName, "OnAndroidFilePickerResult", message == null ? "" : message);
        }
    }

    private static String message(Throwable ex) {
        String message = ex.getMessage();
        return message == null || message.isEmpty() ? ex.getClass().getSimpleName() : message;
    }
}
