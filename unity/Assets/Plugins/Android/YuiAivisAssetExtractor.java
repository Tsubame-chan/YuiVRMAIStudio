package jp.tsubamechan.yuivrm.localai;

import android.content.Context;
import android.content.res.AssetManager;
import com.unity3d.player.UnityPlayer;
import org.json.JSONArray;
import org.json.JSONObject;
import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;

public final class YuiAivisAssetExtractor {
    private YuiAivisAssetExtractor() {}

    public static String ensureExtracted(String targetRoot) {
        try {
            Context context = UnityPlayer.currentActivity.getApplicationContext();
            AssetManager assets = context.getAssets();
            File root = new File(targetRoot);
            JSONArray copied = new JSONArray();

            copyTree(assets, "YuiLocalAI/Aivis", new File(root, "Aivis"), copied);
            copyTree(assets, "YuiLocalAI/Voicevox", new File(root, "Voicevox"), copied);

            return new JSONObject()
                .put("ok", true)
                .put("target_root", root.getAbsolutePath())
                .put("copied", copied)
                .toString();
        } catch (Throwable ex) {
            try {
                return new JSONObject()
                    .put("ok", false)
                    .put("error_code", "aivis_asset_extraction_failed")
                    .put("error_message", ex.getMessage() == null ? ex.getClass().getSimpleName() : ex.getMessage())
                    .toString();
            } catch (Throwable ignored) {
                return "{\"ok\":false,\"error_code\":\"aivis_asset_extraction_failed\"}";
            }
        }
    }

    private static void copyTree(
        AssetManager assets,
        String assetPath,
        File target,
        JSONArray copied
    ) throws Exception {
        String[] children = assets.list(assetPath);
        if (children != null && children.length > 0) {
            if (!target.isDirectory() && !target.mkdirs()) {
                throw new IllegalStateException("Failed to create directory: " + target.getAbsolutePath());
            }
            for (String child : children) {
                copyTree(assets, assetPath + "/" + child, new File(target, child), copied);
            }
            return;
        }

        if (target.isFile() && target.length() > 0) {
            return;
        }
        File parent = target.getParentFile();
        if (parent != null && !parent.isDirectory() && !parent.mkdirs()) {
            throw new IllegalStateException("Failed to create directory: " + parent.getAbsolutePath());
        }

        File tmp = new File(target.getAbsolutePath() + ".tmp");
        try (InputStream input = assets.open(assetPath);
             FileOutputStream output = new FileOutputStream(tmp, false)) {
            byte[] buffer = new byte[1024 * 256];
            int read;
            while ((read = input.read(buffer)) >= 0) {
                if (read > 0) {
                    output.write(buffer, 0, read);
                }
            }
        }
        if (target.exists() && !target.delete()) {
            throw new IllegalStateException("Failed to replace file: " + target.getAbsolutePath());
        }
        if (!tmp.renameTo(target)) {
            throw new IllegalStateException("Failed to move extracted file: " + target.getAbsolutePath());
        }
        copied.put(assetPath);
    }
}
