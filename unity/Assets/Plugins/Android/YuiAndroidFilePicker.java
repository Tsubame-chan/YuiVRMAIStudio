package jp.tsubamechan.yuivrm.localai;

import android.app.Activity;
import android.content.Intent;
import com.unity3d.player.UnityPlayer;

public final class YuiAndroidFilePicker {
    private YuiAndroidFilePicker() {
    }

    public static void open(String mode, String callbackObjectName) {
        Activity activity = UnityPlayer.currentActivity;
        if (activity == null) {
            UnityPlayer.UnitySendMessage(callbackObjectName, "OnAndroidFilePickerResult", "__YUI_ERROR__:Unity Activity is not available.");
            return;
        }

        Intent intent = new Intent(activity, YuiFilePickerActivity.class);
        intent.putExtra(YuiFilePickerActivity.EXTRA_MODE, mode == null ? "image" : mode);
        intent.putExtra(YuiFilePickerActivity.EXTRA_CALLBACK_OBJECT, callbackObjectName == null ? "" : callbackObjectName);
        activity.startActivity(intent);
    }
}
