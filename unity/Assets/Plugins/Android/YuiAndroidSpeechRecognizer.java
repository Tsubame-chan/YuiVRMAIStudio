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

public final class YuiAndroidSpeechRecognizer {
    private static final String CANCELLED = "__YUI_CANCELLED__";
    private static final String ERROR_PREFIX = "__YUI_ERROR__:";
    private static final Handler MAIN = new Handler(Looper.getMainLooper());

    private static SpeechRecognizer recognizer;
    private static String callbackObjectName;

    private YuiAndroidSpeechRecognizer() {
    }

    public static boolean isAvailable() {
        Activity activity = UnityPlayer.currentActivity;
        return activity != null && SpeechRecognizer.isRecognitionAvailable(activity);
    }

    public static void start(String callback, String languageCode) {
        callbackObjectName = callback == null ? "" : callback;
        MAIN.post(() -> startOnMain(languageCode == null || languageCode.trim().isEmpty() ? "ja-JP" : languageCode));
    }

    public static void cancel() {
        MAIN.post(() -> {
            if (recognizer != null) {
                try {
                    recognizer.cancel();
                } catch (Throwable ignored) {
                    // Best effort cancellation.
                }
                destroyRecognizer();
            }
        });
    }

    private static void startOnMain(String languageCode) {
        Activity activity = UnityPlayer.currentActivity;
        if (activity == null) {
            send(ERROR_PREFIX + "Unity Activity is not available.");
            return;
        }

        if (activity.checkSelfPermission(Manifest.permission.RECORD_AUDIO) != PackageManager.PERMISSION_GRANTED) {
            send(ERROR_PREFIX + "Microphone permission is not granted.");
            return;
        }

        if (!SpeechRecognizer.isRecognitionAvailable(activity)) {
            send(ERROR_PREFIX + "Android speech recognition service is not available on this device.");
            return;
        }

        destroyRecognizer();
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
                destroyRecognizer();
                if (error == SpeechRecognizer.ERROR_CLIENT
                    || error == SpeechRecognizer.ERROR_SPEECH_TIMEOUT
                    || error == SpeechRecognizer.ERROR_NO_MATCH) {
                    send(CANCELLED);
                    return;
                }

                send(ERROR_PREFIX + errorMessage(error));
            }

            @Override
            public void onResults(Bundle results) {
                destroyRecognizer();
                ArrayList<String> matches = results == null
                    ? null
                    : results.getStringArrayList(SpeechRecognizer.RESULTS_RECOGNITION);
                if (matches == null || matches.isEmpty() || matches.get(0) == null || matches.get(0).trim().isEmpty()) {
                    send(ERROR_PREFIX + "Android speech recognition returned an empty transcript.");
                    return;
                }

                send(matches.get(0));
            }
        });

        Intent intent = new Intent(RecognizerIntent.ACTION_RECOGNIZE_SPEECH);
        intent.putExtra(RecognizerIntent.EXTRA_LANGUAGE_MODEL, RecognizerIntent.LANGUAGE_MODEL_FREE_FORM);
        intent.putExtra(RecognizerIntent.EXTRA_LANGUAGE, languageCode);
        intent.putExtra(RecognizerIntent.EXTRA_PARTIAL_RESULTS, false);
        intent.putExtra(RecognizerIntent.EXTRA_MAX_RESULTS, 3);
        intent.putExtra(RecognizerIntent.EXTRA_CALLING_PACKAGE, activity.getPackageName());

        try {
            recognizer.startListening(intent);
        } catch (Throwable ex) {
            destroyRecognizer();
            send(ERROR_PREFIX + message(ex));
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

    private static void send(String message) {
        if (callbackObjectName != null && !callbackObjectName.isEmpty()) {
            UnityPlayer.UnitySendMessage(callbackObjectName, "OnAndroidSpeechResult", message == null ? "" : message);
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
