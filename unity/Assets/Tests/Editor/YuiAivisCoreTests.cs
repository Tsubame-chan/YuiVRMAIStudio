using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using YuiPhysicalAI.LocalAI;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiAivisCoreTests
    {
        private string tempRoot;

        [SetUp]
        public void SetUp()
        {
            tempRoot = Path.Combine(Path.GetTempPath(), "yui-aivis-core-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrWhiteSpace(tempRoot) && Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Test]
        public void Probe_ReportsCatalogMissingSeparatelyFromRuntime()
        {
            var status = YuiAivisCoreProbe.Evaluate(tempRoot, nativeRuntimeLinked: false);

            Assert.IsFalse(status.ModelsReady);
            Assert.IsFalse(status.RuntimeReady);
            CollectionAssert.Contains(status.MissingComponents, "aivis_voices.json");
            CollectionAssert.Contains(status.MissingComponents, "style_bert_vits2_runtime");
        }

        [Test]
        public void Probe_ReportsModelsReadyWhenCatalogAndVoiceFilesExist()
        {
            WriteMinimalCatalog(tempRoot);

            var status = YuiAivisCoreProbe.Evaluate(tempRoot, nativeRuntimeLinked: false);

            Assert.IsTrue(status.ModelsReady);
            Assert.IsFalse(status.RuntimeReady);
            CollectionAssert.DoesNotContain(status.MissingComponents, "aivis_voices.json");
            CollectionAssert.DoesNotContain(status.MissingComponents, "Models/female_voice_1.aivmx");
            CollectionAssert.DoesNotContain(status.MissingComponents, "Metadata/female_voice_1.hyper_parameters.json");
            CollectionAssert.DoesNotContain(status.MissingComponents, "Metadata/female_voice_1.style_vectors.npy");
            CollectionAssert.Contains(status.MissingComponents, "style_bert_vits2_runtime");
            CollectionAssert.Contains(status.MissingComponents, "japanese_bert_onnx");
            CollectionAssert.Contains(status.MissingComponents, "japanese_bert_tokenizer");
            CollectionAssert.Contains(status.MissingComponents, "japanese_text_frontend");
        }

        [Test]
        public void Probe_RuntimeReadyRequiresNativeRuntimeAndBertAssets()
        {
            WriteMinimalCatalog(tempRoot);
            WriteAllRuntimeAssets(tempRoot);

            var status = YuiAivisCoreProbe.Evaluate(tempRoot, nativeRuntimeLinked: true);

            Assert.IsTrue(status.ModelsReady);
            Assert.IsTrue(status.RuntimeReady);
            CollectionAssert.IsEmpty(status.MissingComponents);
        }

        [Test]
        public void Probe_ReportsEveryMissingRuntimeAssetWithStableComponentNames()
        {
            WriteMinimalCatalog(tempRoot);

            var status = YuiAivisCoreProbe.Evaluate(tempRoot, nativeRuntimeLinked: true);

            Assert.IsTrue(status.ModelsReady);
            Assert.IsFalse(status.RuntimeReady);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "onnxruntime",
                    "style_bert_vits2_runtime",
                    "japanese_bert_onnx",
                    "japanese_bert_tokenizer",
                    "japanese_text_frontend"
                },
                status.MissingComponents);
        }

        [Test]
        public void RuntimeAssets_ReportReadyOnlyWhenAllFilesExist()
        {
            WriteAllRuntimeAssets(tempRoot);

            var report = YuiAivisRuntimeAssets.Evaluate(tempRoot, nativeRuntimeLinked: true);

            Assert.IsTrue(report.RuntimeReady);
            CollectionAssert.IsEmpty(report.MissingComponents);
        }

        [Test]
        public void RuntimeAssets_AcceptsPlatformReadyManifestsOnlyForMatchingPlatform()
        {
            WriteMinimalCatalog(tempRoot);
            WritePlatformRuntimeAssets(tempRoot, "ios");

            var iosReport = YuiAivisRuntimeAssets.Evaluate(tempRoot, nativeRuntimeLinked: true, platformName: "ios");
            var androidReport = YuiAivisRuntimeAssets.Evaluate(tempRoot, nativeRuntimeLinked: true, platformName: "android");

            Assert.IsTrue(iosReport.RuntimeReady);
            CollectionAssert.IsEmpty(iosReport.MissingComponents);
            Assert.IsFalse(androidReport.RuntimeReady);
            CollectionAssert.Contains(androidReport.MissingComponents, "onnxruntime");
            CollectionAssert.Contains(androidReport.MissingComponents, "style_bert_vits2_runtime");
            CollectionAssert.Contains(androidReport.MissingComponents, "japanese_text_frontend");
        }

        [Test]
        public void RuntimeAssets_RejectsNotReadyManifestEvenWhenReadyAppearsInValue()
        {
            WriteAllRuntimeAssets(tempRoot);
            File.WriteAllText(
                Path.Combine(tempRoot, "Runtime", "StyleBertVits2", "manifest.json"),
                @"{""status"":""not_ready"",""note"":""ready only after device verification""}");

            var report = YuiAivisRuntimeAssets.Evaluate(tempRoot, nativeRuntimeLinked: true);

            Assert.IsFalse(report.RuntimeReady);
            CollectionAssert.Contains(report.MissingComponents, "style_bert_vits2_runtime");
        }

        [Test]
        public void Probe_PackagedAivisVoicesAreModelsReadyButRuntimeIncomplete()
        {
            var root = Path.Combine(Application.dataPath, "StreamingAssets", "YuiLocalAI", "Aivis");
            if (!Directory.Exists(root))
            {
                Assert.Ignore("Packaged Aivis voice assets are not present in this checkout.");
            }

            var status = YuiAivisCoreProbe.Evaluate(root, nativeRuntimeLinked: false);

            Assert.IsTrue(status.ModelsReady);
            Assert.IsFalse(status.RuntimeReady);
            CollectionAssert.Contains(status.MissingComponents, "style_bert_vits2_runtime");
        }

        [Test]
        public void Probe_RejectsVoiceFilesOutsideAivisRoot()
        {
            var outsideModel = Path.Combine(Path.GetDirectoryName(tempRoot) ?? tempRoot, "outside.aivmx");
            File.WriteAllText(outsideModel, "fake");
            Directory.CreateDirectory(Path.Combine(tempRoot, "Metadata"));
            File.WriteAllText(Path.Combine(tempRoot, "Metadata", "female_voice_1.hyper_parameters.json"), "{}");
            File.WriteAllText(Path.Combine(tempRoot, "Metadata", "female_voice_1.style_vectors.npy"), "fake");
            File.WriteAllText(
                Path.Combine(tempRoot, "aivis_voices.json"),
                @"{
  ""schema_version"": ""1"",
  ""default_voice_id"": 1431611904,
  ""voices"": [
    {
      ""id"": 1431611904,
      ""key"": ""female_voice_1"",
      ""display_name"": ""女性ボイス①"",
      ""model_path"": ""../outside.aivmx"",
      ""hyper_parameters_path"": ""Metadata/female_voice_1.hyper_parameters.json"",
      ""style_vectors_path"": ""Metadata/female_voice_1.style_vectors.npy"",
      ""speaker_id"": 0,
      ""default_style_id"": 0,
      ""sampling_rate"": 44100
    }
  ]
}");

            try
            {
                var status = YuiAivisCoreProbe.Evaluate(tempRoot, nativeRuntimeLinked: false);

                Assert.IsFalse(status.ModelsReady);
                CollectionAssert.Contains(status.MissingComponents, "invalid_voice_path");
            }
            finally
            {
                if (File.Exists(outsideModel))
                {
                    File.Delete(outsideModel);
                }
            }
        }

        [Test]
        public void NativeSynthesize_ReturnsRuntimeUnavailableWithMissingComponentsWhenCoreIsIncomplete()
        {
            var root = Path.Combine(Application.dataPath, "StreamingAssets", "YuiLocalAI", "Aivis");
            if (!Directory.Exists(root))
            {
                Assert.Ignore("Packaged Aivis voice assets are not present in this checkout.");
            }

            var result = YuiAivisNativeBridge.Synthesize("こんにちは", 1431611904, 1f, 0f, 1f, 1f, 0.1f, 0.1f);

            Assert.IsFalse(result.Ok);
            Assert.AreEqual("runtime_unavailable", result.ErrorCode);
            CollectionAssert.Contains(result.MissingComponents, "style_bert_vits2_runtime");
        }

        [Test]
        public void NativeWrappers_UseSharedAivisRuntimeHeader()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var iosBridge = File.ReadAllText(Path.Combine(projectRoot, "Assets", "Plugins", "iOS", "YuiAivisNativeBridge.mm"));
            var androidBridge = File.ReadAllText(Path.Combine(projectRoot, "Assets", "Plugins", "Android", "YuiAivisNativeBridge.cpp"));

            StringAssert.Contains("YuiAivisRuntime.h", iosBridge);
            StringAssert.Contains("YuiAivisRuntime.h", androidBridge);
        }

        [Test]
        public void NativeSynthesizeRequest_ProvidesRuntimeDependencyPaths()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var bridge = File.ReadAllText(Path.Combine(projectRoot, "Assets", "App", "Scripts", "LocalAI", "AivisNative", "YuiAivisNativeBridge.cs"));

            StringAssert.Contains("root_path", bridge);
            StringAssert.Contains("bert_model_path", bridge);
            StringAssert.Contains("bert_tokenizer_path", bridge);
            StringAssert.Contains("bert_vocab_path", bridge);
            StringAssert.Contains("open_jtalk_dict_path", bridge);
            StringAssert.Contains("voicevox_model_path", bridge);
            StringAssert.Contains("voicevox_speaker_id", bridge);
        }

        [Test]
        public void LocalAiPathResolver_KnowsUnityIosRawStreamingAssetsFallbacks()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var resolver = File.ReadAllText(Path.Combine(projectRoot, "Assets", "App", "Scripts", "LocalAI", "YuiLocalAiPathResolver.cs"));
            var aivisBridge = File.ReadAllText(Path.Combine(projectRoot, "Assets", "App", "Scripts", "LocalAI", "AivisNative", "YuiAivisNativeBridge.cs"));
            var voicevoxBridge = File.ReadAllText(Path.Combine(projectRoot, "Assets", "App", "Scripts", "LocalAI", "VoicevoxCore", "YuiVoicevoxCoreBridge.cs"));

            StringAssert.Contains(@"Path.Combine(dataPath, ""Raw"", ""YuiLocalAI"")", resolver);
            StringAssert.Contains(@"Path.Combine(parent, ""Data"", ""Raw"", ""YuiLocalAI"")", resolver);
            StringAssert.Contains("YuiLocalAiPathResolver.AivisRootPath()", aivisBridge);
            StringAssert.Contains("YuiLocalAiPathResolver.VoicevoxRootPath()", voicevoxBridge);
        }

        [Test]
        public void AndroidAivisBridge_ExtractsStreamingAssetsBeforePassingNativeFilePaths()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var bridge = File.ReadAllText(Path.Combine(projectRoot, "Assets", "App", "Scripts", "LocalAI", "AivisNative", "YuiAivisNativeBridge.cs"));
            var extractor = Path.Combine(projectRoot, "Assets", "Plugins", "Android", "YuiAivisAssetExtractor.java");

            StringAssert.Contains("EnsureAndroidAivisAssetsExtracted", bridge);
            StringAssert.Contains("AndroidLocalAiRootPath", bridge);
            Assert.IsTrue(File.Exists(extractor));
        }

        [Test]
        public void RuntimeAssetValidator_KnowsAllRequiredComponents()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var validator = File.ReadAllText(Path.Combine(projectRoot, "..", "scripts", "validate_aivis_runtime_assets.py"));

            StringAssert.Contains("onnxruntime", validator);
            StringAssert.Contains("style_bert_vits2_runtime", validator);
            StringAssert.Contains("japanese_bert_onnx", validator);
            StringAssert.Contains("japanese_bert_tokenizer", validator);
            StringAssert.Contains("japanese_text_frontend", validator);
        }

        [Test]
        public void AndroidAivisDependencies_ArePackagedAsUnityPlugins()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var androidPluginRoot = Path.Combine(projectRoot, "Assets", "Plugins", "Android");

            Assert.IsTrue(File.Exists(Path.Combine(androidPluginRoot, "onnxruntime-android-1.23.0.aar")));
            Assert.IsTrue(File.Exists(Path.Combine(androidPluginRoot, "Voicevox", "voicevox_core-android-arm64-0.16.4", "include", "voicevox_core.h")));
            Assert.IsTrue(File.Exists(Path.Combine(androidPluginRoot, "Voicevox", "voicevox_core-android-arm64-0.16.4", "lib", "libvoicevox_core.so")));
        }

        [Test]
        public void AndroidOnnxRuntimeAar_IsKeptAsSourceArchiveButNotPackagedBesideExtractedSo()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var aarMeta = File.ReadAllText(Path.Combine(projectRoot, "Assets", "Plugins", "Android", "onnxruntime-android-1.23.0.aar.meta"));

            StringAssert.Contains("Android: Android", aarMeta);
            StringAssert.Contains("enabled: 0", aarMeta);
        }

        [Test]
        public void FetchScript_InstallsAndroidTextFrontendAndPluginDependencies()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var fetchScript = File.ReadAllText(Path.Combine(projectRoot, "..", "scripts", "fetch_aivis_runtime_assets.py"));

            StringAssert.Contains("--install-voicevox-core-android", fetchScript);
            StringAssert.Contains("--android-voicevox-core-url", fetchScript);
            StringAssert.Contains("ANDROID_ONNXRUNTIME_PLUGIN_PATH", fetchScript);
            StringAssert.Contains("onnxruntime-android-{ONNXRUNTIME_VERSION}.aar", fetchScript);
            StringAssert.Contains("ANDROID_VOICEVOX_CORE_DIR_NAME", fetchScript);
            StringAssert.Contains("voicevox_core-android-arm64-{VOICEVOX_CORE_VERSION}", fetchScript);
        }

        [Test]
        public void AndroidCMake_ReferencesSharedVoicevoxAndOnnxRuntimeLibraries()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var cmake = File.ReadAllText(Path.Combine(projectRoot, "Assets", "Plugins", "Android", "CMakeLists.txt"));

            StringAssert.Contains("voicevox_core", cmake);
            StringAssert.Contains("onnxruntime", cmake);
            StringAssert.Contains("Voicevox/voicevox_core-android-arm64-0.16.4/include", cmake);
        }

        [Test]
        public void NativeStyleBertRuntime_DoesNotTreatEveryAppleTargetAsIosDeviceFrontend()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var source = File.ReadAllText(Path.Combine(projectRoot, "Assets", "Plugins", "NativeAivis", "YuiAivisStyleBertRuntime.cpp"));

            StringAssert.Contains("TARGET_OS_IPHONE && !TARGET_OS_SIMULATOR", source);
            StringAssert.Contains("StyleBertRuntimeHasJapaneseTextFrontend", source);
        }

        [Test]
        public void NativeStyleBertRuntime_ReusesHeavyRuntimeResources()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var source = File.ReadAllText(Path.Combine(projectRoot, "Assets", "Plugins", "NativeAivis", "YuiAivisStyleBertRuntime.cpp"));

            StringAssert.Contains("CachedSession", source);
            StringAssert.Contains("CachedVocab", source);
            StringAssert.Contains("CachedNpyFloat32", source);
            StringAssert.Contains("gVoicevoxMutex", source);
        }

        private static void WriteMinimalCatalog(string root)
        {
            Directory.CreateDirectory(Path.Combine(root, "Models"));
            Directory.CreateDirectory(Path.Combine(root, "Metadata"));
            File.WriteAllText(Path.Combine(root, "Models", "female_voice_1.aivmx"), "fake");
            File.WriteAllText(Path.Combine(root, "Metadata", "female_voice_1.hyper_parameters.json"), "{}");
            File.WriteAllText(Path.Combine(root, "Metadata", "female_voice_1.style_vectors.npy"), "fake");
            File.WriteAllText(
                Path.Combine(root, "aivis_voices.json"),
                @"{
  ""schema_version"": ""1"",
  ""default_voice_id"": 1431611904,
  ""voices"": [
    {
      ""id"": 1431611904,
      ""key"": ""female_voice_1"",
      ""display_name"": ""女性ボイス①"",
      ""model_path"": ""Models/female_voice_1.aivmx"",
      ""hyper_parameters_path"": ""Metadata/female_voice_1.hyper_parameters.json"",
      ""style_vectors_path"": ""Metadata/female_voice_1.style_vectors.npy"",
      ""speaker_id"": 0,
      ""default_style_id"": 0,
      ""sampling_rate"": 44100
    }
  ]
}");
        }

        private static void WriteAllRuntimeAssets(string root)
        {
            Directory.CreateDirectory(Path.Combine(root, "Runtime", "ONNXRuntime"));
            Directory.CreateDirectory(Path.Combine(root, "Runtime", "StyleBertVits2"));
            Directory.CreateDirectory(Path.Combine(root, "Runtime", "JapaneseBert"));
            Directory.CreateDirectory(Path.Combine(root, "Runtime", "JapaneseTextFrontend"));
            File.WriteAllText(Path.Combine(root, "Runtime", "ONNXRuntime", "manifest.json"), @"{""status"":""ready""}");
            File.WriteAllText(Path.Combine(root, "Runtime", "StyleBertVits2", "manifest.json"), @"{""status"":""ready""}");
            File.WriteAllText(Path.Combine(root, "Runtime", "JapaneseBert", "model_fp16.onnx"), "fake");
            File.WriteAllText(Path.Combine(root, "Runtime", "JapaneseBert", "tokenizer.json"), "fake");
            File.WriteAllText(Path.Combine(root, "Runtime", "JapaneseTextFrontend", "manifest.json"), @"{""status"":""ready""}");
        }

        private static void WritePlatformRuntimeAssets(string root, string platform)
        {
            Directory.CreateDirectory(Path.Combine(root, "Runtime", "ONNXRuntime"));
            Directory.CreateDirectory(Path.Combine(root, "Runtime", "StyleBertVits2"));
            Directory.CreateDirectory(Path.Combine(root, "Runtime", "JapaneseBert"));
            Directory.CreateDirectory(Path.Combine(root, "Runtime", "JapaneseTextFrontend"));
            var platformReady = $@"{{""status"":""platform_ready"",""ready_platforms"":[""{platform}""]}}";
            File.WriteAllText(Path.Combine(root, "Runtime", "ONNXRuntime", "manifest.json"), platformReady);
            File.WriteAllText(Path.Combine(root, "Runtime", "StyleBertVits2", "manifest.json"), platformReady);
            File.WriteAllText(Path.Combine(root, "Runtime", "JapaneseBert", "model_fp16.onnx"), "fake");
            File.WriteAllText(Path.Combine(root, "Runtime", "JapaneseBert", "tokenizer.json"), "fake");
            File.WriteAllText(Path.Combine(root, "Runtime", "JapaneseTextFrontend", "manifest.json"), platformReady);
        }
    }
}
