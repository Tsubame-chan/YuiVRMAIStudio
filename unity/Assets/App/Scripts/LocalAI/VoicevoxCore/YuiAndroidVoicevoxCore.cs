#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.IO;
using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;

namespace YuiPhysicalAI.LocalAI
{
    // ABI follows the bundled VOICEVOX Core 0.16.4 header (Android arm64).
    internal static class YuiAndroidVoicevoxCore
    {
        private const string Library = "voicevox_core";
        private static readonly object Sync = new object();
        [StructLayout(LayoutKind.Sequential)] private struct LoadOptions { public IntPtr Filename; }
        [StructLayout(LayoutKind.Sequential)] private struct InitializeOptions { public int Acceleration; public ushort Threads; }
        [StructLayout(LayoutKind.Sequential)] private struct SynthesisOptions { public byte Upspeak; }
        [DllImport(Library)] private static extern int voicevox_onnxruntime_load_once(LoadOptions options, out IntPtr runtime);
        [DllImport(Library)] private static extern int voicevox_open_jtalk_rc_new([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out IntPtr dictionary);
        [DllImport(Library)] private static extern void voicevox_open_jtalk_rc_delete(IntPtr dictionary);
        [DllImport(Library)] private static extern int voicevox_synthesizer_new(IntPtr runtime, IntPtr dictionary, InitializeOptions options, out IntPtr synthesizer);
        [DllImport(Library)] private static extern void voicevox_synthesizer_delete(IntPtr synthesizer);
        [DllImport(Library)] private static extern int voicevox_voice_model_file_open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out IntPtr model);
        [DllImport(Library)] private static extern void voicevox_voice_model_file_delete(IntPtr model);
        [DllImport(Library)] private static extern int voicevox_synthesizer_load_voice_model(IntPtr synthesizer, IntPtr model);
        [DllImport(Library)] private static extern int voicevox_synthesizer_create_audio_query(IntPtr synthesizer, [MarshalAs(UnmanagedType.LPUTF8Str)] string text, uint style, out IntPtr query);
        [DllImport(Library)] private static extern int voicevox_synthesizer_synthesis(IntPtr synthesizer, [MarshalAs(UnmanagedType.LPUTF8Str)] string query, uint style, SynthesisOptions options, out UIntPtr length, out IntPtr wav);
        [DllImport(Library)] private static extern void voicevox_json_free(IntPtr query);
        [DllImport(Library)] private static extern void voicevox_wav_free(IntPtr wav);
        private static void Check(int code) { if (code != 0) throw new InvalidOperationException("VOICEVOX Core error: " + code); }

        internal static YuiVoicevoxCoreSynthesisResult Synthesize(string payload)
        {
            lock (Sync)
            {
                IntPtr dictionary = IntPtr.Zero, synthesizer = IntPtr.Zero, model = IntPtr.Zero, query = IntPtr.Zero, wav = IntPtr.Zero;
                var libraryName = Marshal.StringToHGlobalAnsi("libonnxruntime.so");
                try
                {
                    var request = JObject.Parse(payload);
                    var dictionaryPath = (string)request["open_jtalk_dict_path"];
                    var modelPath = (string)request["model_path"];
                    if (!Directory.Exists(dictionaryPath) || !File.Exists(modelPath))
                        return YuiVoicevoxCoreSynthesisResult.Error("assets_missing", "VOICEVOX dictionary/model is not installed. Retry the initial download.");
                    var text = (string)request["text"];
                    if (string.IsNullOrWhiteSpace(text)) return YuiVoicevoxCoreSynthesisResult.Error("invalid_request", "Speech text is empty.");
                    var style = checked((uint)(int)request["style_id"]);
                    Check(voicevox_onnxruntime_load_once(new LoadOptions { Filename = libraryName }, out var runtime));
                    Check(voicevox_open_jtalk_rc_new(dictionaryPath, out dictionary));
                    Check(voicevox_synthesizer_new(runtime, dictionary, new InitializeOptions { Acceleration = 1 }, out synthesizer));
                    Check(voicevox_voice_model_file_open(modelPath, out model));
                    Check(voicevox_synthesizer_load_voice_model(synthesizer, model));
                    Check(voicevox_synthesizer_create_audio_query(synthesizer, text, style, out query));
                    var audioQuery = JObject.Parse(Marshal.PtrToStringUTF8(query));
                    audioQuery["speedScale"] = Math.Max(.5, Math.Min(2, (double)request["speed_scale"]));
                    audioQuery["pitchScale"] = Math.Max(-.15, Math.Min(.15, (double)request["pitch_scale"]));
                    audioQuery["intonationScale"] = Math.Max(0, Math.Min(2, (double)request["intonation_scale"]));
                    audioQuery["volumeScale"] = Math.Max(0, Math.Min(2, (double)request["volume_scale"]));
                    audioQuery["prePhonemeLength"] = Math.Max(0, Math.Min(1.5, (double)request["pre_phoneme_length"]));
                    audioQuery["postPhonemeLength"] = Math.Max(0, Math.Min(1.5, (double)request["post_phoneme_length"]));
                    Check(voicevox_synthesizer_synthesis(synthesizer, audioQuery.ToString(), style, new SynthesisOptions { Upspeak = 1 }, out var length, out wav));
                    var bytes = new byte[checked((int)length.ToUInt64())];
                    Marshal.Copy(wav, bytes, 0, bytes.Length);
                    return new YuiVoicevoxCoreSynthesisResult { Ok = true, AudioBase64 = Convert.ToBase64String(bytes), SampleRate = 24000 };
                }
                catch (Exception ex) { return YuiVoicevoxCoreSynthesisResult.Error("native_synthesis_failed", ex.Message); }
                finally
                {
                    if (wav != IntPtr.Zero) voicevox_wav_free(wav);
                    if (query != IntPtr.Zero) voicevox_json_free(query);
                    if (model != IntPtr.Zero) voicevox_voice_model_file_delete(model);
                    if (synthesizer != IntPtr.Zero) voicevox_synthesizer_delete(synthesizer);
                    if (dictionary != IntPtr.Zero) voicevox_open_jtalk_rc_delete(dictionary);
                    Marshal.FreeHGlobal(libraryName);
                }
            }
        }
    }
}
#endif
