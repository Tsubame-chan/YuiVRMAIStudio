using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace YuiPhysicalAI.LocalAI
{
    // Model and lexicons are optional data under persistent storage, never bundled Resources.
    public static class YuiKokoroSpeech
    {
        public static bool Supported {
            get {
#if UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                return true;
#else
                return false;
#endif
            }
        }
#if UNITY_IOS && !UNITY_EDITOR
        private const string Library = "__Internal";
#else
        private const string Library = "YuiKokoroBridge";
#endif
        [StructLayout(LayoutKind.Sequential)] private struct Result
        { public int Ok, SampleRate; public long Count; public IntPtr Samples, Error; }
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr YuiKokoro_Synthesize(
            string runtime, string model, string voice, long[] tokens, int count, float speed);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void YuiKokoro_Free(IntPtr result);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void YuiKokoro_Cancel();
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void YuiKokoro_Release();
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
        public static string Root(string persistent) => Path.Combine(persistent, "YuiLocalAI", "Kokoro");
        public static bool IsInstalled(string persistent) => File.Exists(Path.Combine(Root(persistent), "pack.json"))
            && File.Exists(Path.Combine(Root(persistent), "kokoro-v1.0.int8.onnx"));

        public static readonly string[] SupportedVoices = { "af_bella", "af_nova", "af_nicole", "af_heart", "am_puck", "am_michael" };
        public static string[] InstalledVoices(string persistent)
        {
            if (!IsInstalled(persistent)) return Array.Empty<string>();
            try
            {
                var root = Root(persistent);
                var manifest = JObject.Parse(File.ReadAllText(Path.Combine(root, "pack.json")));
                var declared = manifest["voices"]?.Values<string>().ToArray() ?? Array.Empty<string>();
                var files = manifest["files"] as JObject;
                return SupportedVoices.Where(v => declared.Contains(v) && files?[v + ".bin"] != null
                    && File.Exists(Path.Combine(root, v + ".bin"))
                    && new FileInfo(Path.Combine(root, v + ".bin")).Length == 510 * 256 * sizeof(float)).ToArray();
            }
            catch (Exception e) when (e is IOException || e is JsonException || e is ArgumentException) { return Array.Empty<string>(); }
        }
        public static string ResolveVoice(string saved, string[] installed) => installed.Contains(saved) ? saved
            : installed.Contains("af_bella") ? "af_bella" : installed.FirstOrDefault() ?? "af_bella";

        public static async Task<float[]> SynthesizeAsync(string text, string voice, string persistent,
            string appDataPath, float speed, CancellationToken token)
        {
            var root = Root(persistent);
            if (!IsInstalled(persistent)) throw new InvalidOperationException("Download English voice data in Settings → Voice first.");
            string runtime;
#if UNITY_IOS && !UNITY_EDITOR
            runtime = Path.Combine(Path.GetDirectoryName(appDataPath), "Frameworks", "voicevox_onnxruntime.framework", "voicevox_onnxruntime");
#elif UNITY_ANDROID && !UNITY_EDITOR
            runtime = "libonnxruntime.so";
#elif UNITY_EDITOR_OSX
            runtime = Path.Combine(appDataPath, "Plugins/macOS/Voicevox/libvoicevox_onnxruntime.1.17.3.dylib");
#elif UNITY_STANDALONE_OSX
            runtime = Path.Combine(appDataPath, "PlugIns", "libvoicevox_onnxruntime.1.17.3.dylib");
#else
            throw new PlatformNotSupportedException("Kokoro native runtime is not available in this build.");
#endif
#if UNITY_IOS || UNITY_ANDROID || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            await Gate.WaitAsync(token);
            try
            {
                return await Task.Run(() => {
                    token.ThrowIfCancellationRequested();
                    Validate(root, token);
                    if (!InstalledVoices(persistent).Contains(voice))
                        throw new InvalidDataException("This voice is not in the installed English pack. Download it again.");
                    var phonemizer = new YuiKokoroEnglishPhonemizer(root);
                    var output = new List<float>();
                    using (token.Register(YuiKokoro_Cancel))
                    {
                        foreach (var chunk in phonemizer.Tokenize(text))
                        {
                            token.ThrowIfCancellationRequested();
                            var pointer = YuiKokoro_Synthesize(runtime, Path.Combine(root, "kokoro-v1.0.int8.onnx"),
                                Path.Combine(root, voice + ".bin"), chunk, chunk.Length, speed);
                            try {
                                if (pointer == IntPtr.Zero) throw new InvalidOperationException("Kokoro returned no result.");
                                var result = Marshal.PtrToStructure<Result>(pointer);
                                token.ThrowIfCancellationRequested();
                                if (result.Ok == 0) throw new InvalidOperationException(Marshal.PtrToStringAnsi(result.Error));
                                if (result.SampleRate != 24000 || result.Count <= 0 || result.Count > 1440000)
                                    throw new InvalidDataException("Invalid Kokoro audio result.");
                                var samples = new float[(int)result.Count];
                                Marshal.Copy(result.Samples, samples, 0, samples.Length);
                                output.AddRange(samples);
                                if (output.Count > 24000 * 180) throw new InvalidOperationException("Speech is too long. Use a shorter message.");
                            } finally { if (pointer != IntPtr.Zero) YuiKokoro_Free(pointer); }
                        }
                    }
                    token.ThrowIfCancellationRequested();
                    return output.ToArray();
                }, token);
            }
            finally { try { YuiKokoro_Release(); } finally { Gate.Release(); } }
#endif
        }

        private static void Validate(string root, CancellationToken token)
        {
            var manifest = JObject.Parse(File.ReadAllText(Path.Combine(root, "pack.json")));
            foreach (var property in ((JObject)manifest["files"]).Properties())
            {
                token.ThrowIfCancellationRequested();
                if (Path.GetFileName(property.Name) != property.Name) throw new InvalidDataException("Invalid voice data path.");
                var file = Path.Combine(root, property.Name);
                var expected = property.Value;
                if (!File.Exists(file) || new FileInfo(file).Length != (long)expected["size_bytes"])
                    throw new InvalidDataException("English voice data is incomplete. Download it again.");
                using (var sha = SHA256.Create()) using (var stream = File.OpenRead(file))
                    if (BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() != (string)expected["sha256"])
                        throw new InvalidDataException("English voice data failed verification. Download it again.");
            }
        }
    }

    // Uses Apache-2.0 Misaki US lexicons. Unknown names are spelled aloud, never silently dropped.
    // This compact portable path does not implement Misaki's Python POS/BART model.
    public sealed class YuiKokoroEnglishPhonemizer
    {
        private readonly Dictionary<string, string> lexicon = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly JObject vocab;
        private static readonly Regex Words = new Regex(@"[A-Za-z]+(?:'[A-Za-z]+)*|[0-9]+|[^\w\s]", RegexOptions.CultureInvariant);
        private static readonly string[] Digits = { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine" };
        private static readonly string[] Teens = { "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen" };
        private static readonly string[] Tens = { "", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety" };
        public YuiKokoroEnglishPhonemizer(string root)
        {
            ReadLexicon(Path.Combine(root, "us_gold.json"));
            ReadLexicon(Path.Combine(root, "us_silver.json"), true);
            vocab = (JObject)JObject.Parse(File.ReadAllText(Path.Combine(root, "config.json")))["vocab"];
        }
        private void ReadLexicon(string path, bool fallback = false)
        {
            // Stream entries to retain only pronunciations, not two large JSON object trees.
            using var stream = File.OpenText(path); using var reader = new JsonTextReader(stream);
            reader.Read();
            while (reader.Read() && reader.TokenType != JsonToken.EndObject)
            {
                if (reader.TokenType != JsonToken.PropertyName) throw new InvalidDataException("Invalid English lexicon.");
                var key = (string)reader.Value; reader.Read(); var value = JToken.ReadFrom(reader);
                if (value is JObject obj) value = obj["DEFAULT"] ?? obj.Properties().Select(p => p.Value).FirstOrDefault(v => v.Type == JTokenType.String);
                if (value?.Type == JTokenType.String && !lexicon.ContainsKey(key) && (!fallback || !lexicon.ContainsKey(key.ToLowerInvariant()))) lexicon.Add(key, (string)value);
            }
        }
        public string Phonemes(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Speech is empty.");
            if (text.Length > 6000) throw new ArgumentException("Speech is too long.");
            // Avoid discarding non-English words and accidentally claiming multilingual support.
            if (text.Any(c => c >= '\u3040' && c <= '\u9fff'))
                throw new ArgumentException("English voice requires English text. Select Japanese for Japanese speech.");
            text = text.Replace('’', '\'').Replace('–', '-');
            text = Regex.Replace(text, @"(?<=\d),(?=\d{3}(?:\D|$))", "");
            text = Regex.Replace(text, @"(?<=\d)\.(?=\d)", " point ");
            text = text.Replace("%", " percent ").Replace("&", " and ");
            var pieces = new List<string>();
            foreach (Match match in Words.Matches(text))
            {
                var word = match.Value;
                if (char.IsDigit(word[0]))
                {
                    foreach (var numberWord in NumberWords(word).Split(' ')) pieces.Add(Word(numberWord));
                }
                else if (char.IsLetter(word[0])) pieces.Add(Word(word));
                else if (word == "-") pieces.Add("—");
                else if (vocab[word] != null) pieces.Add(word);
            }
            var result = string.Join(" ", pieces);
            if (result.Length == 0) throw new ArgumentException("No English speech in this message.");
            foreach (var c in result)
                if (vocab[c.ToString()] == null) throw new InvalidDataException("Unsupported phoneme: " + c);
            return result;
        }
        private string Lookup(string word)
        {
            return lexicon.TryGetValue(word, out var value) || lexicon.TryGetValue(word.ToLowerInvariant(), out value) ? value : null;
        }
        private string Word(string word)
        {
            if (word == "a") return "ə";
            if (word.Equals("Yui", StringComparison.OrdinalIgnoreCase)) return "jˈui";
            var found = Lookup(word);
            if (found != null) return found;
            if (word.Length > 2 && word.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            {
                found = Lookup(word.Substring(0, word.Length - 1));
                if (found != null) return found + ("sʃʧzʒʤ".Contains(found[found.Length - 1].ToString()) ? "ɪz" : "ptkfθ".Contains(found[found.Length - 1].ToString()) ? "s" : "z");
            }
            return string.Join(" ", word.Where(char.IsLetter).Select(c => Lookup(char.ToUpperInvariant(c).ToString()) ?? throw new InvalidDataException("Cannot spell " + word)));
        }
        public static string NumberWords(string text)
        {
            if (text.Length > 1 && text[0] == '0' || !int.TryParse(text, out var number) || number > 999999)
                return string.Join(" ", text.Select(c => Digits[c - '0']));
            if (number < 10) return Digits[number];
            if (number < 20) return Teens[number - 10];
            if (number < 100) return Tens[number / 10] + (number % 10 == 0 ? "" : " " + NumberWords((number % 10).ToString()));
            if (number < 1000) return Digits[number / 100] + " hundred" + (number % 100 == 0 ? "" : " " + NumberWords((number % 100).ToString()));
            return NumberWords((number / 1000).ToString()) + " thousand" + (number % 1000 == 0 ? "" : " " + NumberWords((number % 1000).ToString()));
        }
        public IEnumerable<long[]> Tokenize(string text)
        {
            var phonemes = Phonemes(text);
            var current = new List<long> { 0 };
            foreach (var word in phonemes.Split(' '))
            {
                if (word.Length > 230) throw new ArgumentException("English word is too long.");
                if (current.Count + word.Length + 1 > 240) { current.Add(0); yield return current.ToArray(); current = new List<long> { 0 }; }
                if (current.Count > 1) current.Add((long)vocab[" "]);
                foreach (var c in word) current.Add((long)vocab[c.ToString()]);
            }
            if (current.Count > 1) { current.Add(0); yield return current.ToArray(); }
        }
    }
}
