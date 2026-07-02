package jp.tsubamechan.yuivrm.localai;

import android.app.Activity;
import android.content.ContentResolver;
import android.content.Intent;
import android.database.Cursor;
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

        Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT);
        intent.addCategory(Intent.CATEGORY_OPENABLE);
        intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_GRANT_PERSISTABLE_URI_PERMISSION);
        if ("vrm".equals(mode)) {
            intent.setType("*/*");
            intent.putExtra(Intent.EXTRA_MIME_TYPES, new String[] {
                "application/octet-stream",
                "application/x-vrm",
                "model/gltf-binary"
            });
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
        try {
            int flags = data.getFlags() & Intent.FLAG_GRANT_READ_URI_PERMISSION;
            getContentResolver().takePersistableUriPermission(uri, flags);
        } catch (Throwable ignored) {
            // Some providers do not support persistable permissions. The file is copied immediately below.
        }

        try {
            send(copyToAppStorage(uri, mode));
        } catch (Throwable ex) {
            send(ERROR_PREFIX + "選択したファイルをコピーできませんでした: " + message(ex));
        } finally {
            finish();
        }
    }

    private String copyToAppStorage(Uri uri, String mode) throws Exception {
        File root = "vrm".equals(mode)
            ? new File(getFilesDir(), "YuiImportedFiles/VRM")
            : new File(getCacheDir(), "YuiPickedFiles/Image");
        if (!root.isDirectory() && !root.mkdirs()) {
            throw new IllegalStateException("Failed to create directory: " + root.getAbsolutePath());
        }

        String extension = extensionFor(uri, mode);
        String prefix = "vrm".equals(mode) ? "yui-imported-vrm" : "yui-picked-image";
        File target = new File(root, prefix + "-" + UUID.randomUUID().toString() + "." + extension);
        try (InputStream input = getContentResolver().openInputStream(uri);
             FileOutputStream output = new FileOutputStream(target, false)) {
            if (input == null) {
                throw new IllegalStateException("Content resolver returned null stream.");
            }
            byte[] buffer = new byte[256 * 1024];
            int read;
            while ((read = input.read(buffer)) >= 0) {
                if (read > 0) {
                    output.write(buffer, 0, read);
                }
            }
        }
        return target.getAbsolutePath();
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

        return "vrm".equals(mode) ? "vrm" : "jpg";
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
