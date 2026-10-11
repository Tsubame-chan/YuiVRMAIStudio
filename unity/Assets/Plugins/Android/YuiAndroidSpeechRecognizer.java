package jp.tsubamechan.yuivrm.localai;

import android.Manifest;
import android.app.Activity;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.speech.RecognitionListener;
import android.speech.RecognizerIntent;
import android.speech.SpeechRecognizer;
import com.unity3d.player.UnityPlayer;
import java.util.ArrayList;
import org.json.JSONObject;

public final class YuiAndroidSpeechRecognizer {
    private static final String CANCELLED = "__YUI_CANCELLED__";
    private static final String ERROR_PREFIX = "__YUI_ERROR__:";
    private static final Handler MAIN = new Handler(Looper.getMainLooper());

    private static SpeechRecognizer recognizer;
    private static String activeRequestId;

    private YuiAndroidSpeechRecognizer() {
    }

    public static boolean isAvailable() {
        Activity activity = UnityPlayer.currentActivity;
        return activity != null && SpeechRecognizer.isRecognitionAvailable(activity);
    }

    public static void start(String callback, String requestId, String languageCode) {
        MAIN.post(() -> startOnMain(callback, requestId,
            languageCode == null || languageCode.trim().isEmpty() ? "ja-JP" : languageCode));
    }

    public static void cancel(String requestId) {
        MAIN.post(() -> {
            if (requestId == null || !requestId.equals(activeRequestId)) return;
            activeRequestId = null;
            if (recognizer != null) {
                try { recognizer.cancel(); } catch (Throwable ignored) { }
                destroyRecognizer();
            }
        });
    }

    private static void startOnMain(String callback, String requestId, String languageCode) {
        activeRequestId = requestId;
        destroyRecognizer();
        Activity activity = UnityPlayer.currentActivity;
        if (activity == null) {
            send(callback, requestId, ERROR_PREFIX + "Unity Activity is not available.");
            return;
        }

        if (activity.checkSelfPermission(Manifest.permission.RECORD_AUDIO) != PackageManager.PERMISSION_GRANTED) {
            send(callback, requestId, ERROR_PREFIX + "Microphone permission is not granted.");
            return;
        }

        if (!SpeechRecognizer.isRecognitionAvailable(activity)) {
            send(callback, requestId, ERROR_PREFIX + "Android speech recognition service is not available on this device.");
            return;
        }

        try {
            recognizer = SpeechRecognizer.createSpeechRecognizer(activity);
            recognizer.setRecognitionListener(new RecognitionListener() {
                @Override public void onReadyForSpeech(Bundle params) { }
                @Override public void onBeginningOfSpeech() { }
                @Override public void onRmsChanged(float rmsdB) { }
                @Override public void onBufferReceived(byte[] buffer) { }
                @Override public void onEndOfSpeech() { }
                @Override public void onPartialResults(Bundle partialResults) { }
                @Override public void onEvent(int eventType, Bundle params) { }

                @Override
                public void onError(int error) {
                    if (!requestId.equals(activeRequestId)) return;
                    activeRequestId = null;
                    destroyRecognizer();
                    if (error == SpeechRecognizer.ERROR_CLIENT
                        || error == SpeechRecognizer.ERROR_SPEECH_TIMEOUT
                        || error == SpeechRecognizer.ERROR_NO_MATCH) {
                        send(callback, requestId, CANCELLED);
                        return;
                    }

                    send(callback, requestId, ERROR_PREFIX + errorMessage(error));
                }

                @Override
                public void onResults(Bundle results) {
                    if (!requestId.equals(activeRequestId)) return;
                    activeRequestId = null;
                    destroyRecognizer();
                    ArrayList<String> matches = results == null
                        ? null
                        : results.getStringArrayList(SpeechRecognizer.RESULTS_RECOGNITION);
                    if (matches == null || matches.isEmpty() || matches.get(0) == null || matches.get(0).trim().isEmpty()) {
                        send(callback, requestId, ERROR_PREFIX + "Android speech recognition returned an empty transcript.");
                        return;
                    }

                    send(callback, requestId, matches.get(0));
                }
            });

            Intent intent = new Intent(RecognizerIntent.ACTION_RECOGNIZE_SPEECH);
            intent.putExtra(RecognizerIntent.EXTRA_LANGUAGE_MODEL, RecognizerIntent.LANGUAGE_MODEL_FREE_FORM);
            intent.putExtra(RecognizerIntent.EXTRA_LANGUAGE, languageCode);
            intent.putExtra(RecognizerIntent.EXTRA_PARTIAL_RESULTS, false);
            intent.putExtra(RecognizerIntent.EXTRA_MAX_RESULTS, 3);
            intent.putExtra(RecognizerIntent.EXTRA_CALLING_PACKAGE, activity.getPackageName());

            recognizer.startListening(intent);
        } catch (Throwable ex) {
            activeRequestId = null;
            destroyRecognizer();
            send(callback, requestId, ERROR_PREFIX + message(ex));
        }
    }

    private static void destroyRecognizer() {
        if (recognizer == null) {
            return;
        }

        try {
            recognizer.destroy();
        } catch (Throwable ignored) {
            // Best effort cleanup.
        }
        recognizer = null;
    }

    private static void send(String callback, String requestId, String message) {
        if (callback != null && !callback.isEmpty()) {
            try {
                JSONObject payload = new JSONObject();
                payload.put("request_id", requestId);
                payload.put("message", message == null ? "" : message);
                UnityPlayer.UnitySendMessage(callback, "OnAndroidSpeechResult", payload.toString());
            } catch (Exception ignored) { }
        }
    }

    private static String errorMessage(int error) {
        switch (error) {
            case SpeechRecognizer.ERROR_AUDIO:
                return "Android speech recognition audio error.";
            case SpeechRecognizer.ERROR_INSUFFICIENT_PERMISSIONS:
                return "Android microphone permission is missing.";
            case SpeechRecognizer.ERROR_NETWORK:
            case SpeechRecognizer.ERROR_NETWORK_TIMEOUT:
                return "Android speech recognition network error.";
            case SpeechRecognizer.ERROR_RECOGNIZER_BUSY:
                return "Android speech recognizer is busy.";
            case SpeechRecognizer.ERROR_SERVER:
                return "Android speech recognition server error.";
            default:
                return "Android speech recognition failed: " + error;
        }
    }

    private static String message(Throwable ex) {
        String message = ex.getMessage();
        return message == null || message.isEmpty() ? ex.getClass().getSimpleName() : message;
    }
}
