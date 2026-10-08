// Yui Kokoro v1.0 ONNX bridge. No phonemizer or eSpeak dependency.
// ONNX Runtime is dynamically resolved from the app's existing bundled runtime.
#include "Kokoro/onnxruntime_c_api.h"
#include <algorithm>
#include <cmath>
#include <cstdlib>
#include <cstring>
#include <fstream>
#include <mutex>
#include <stdexcept>
#include <string>
#include <vector>
#if defined(_WIN32)
#include <windows.h>
#define YUI_EXPORT extern "C" __declspec(dllexport)
#else
#include <dlfcn.h>
#define YUI_EXPORT extern "C" __attribute__((visibility("default")))
#endif

struct YuiKokoroResult {
    int32_t ok;
    int32_t sampleRate;
    int64_t count;
    float *samples;
    char *error;
};

namespace {
std::mutex synthesisMutex, cancellationMutex;
const OrtApi *api = nullptr;
OrtEnv *env = nullptr;
OrtSession *session = nullptr;
OrtRunOptions *activeRun = nullptr;
std::string loadedModel;
void *runtimeHandle = nullptr;

void check(OrtStatus *status) {
    if (!status) return;
    std::string message = api->GetErrorMessage(status);
    api->ReleaseStatus(status);
    throw std::runtime_error(message);
}
void initialize(const char *libraryPath, const char *modelPath) {
    if (!api) {
#if defined(_WIN32)
        runtimeHandle = LoadLibraryA(libraryPath);
        auto getApi = runtimeHandle ? reinterpret_cast<const OrtApiBase *(ORT_API_CALL *)()>(
            GetProcAddress(static_cast<HMODULE>(runtimeHandle), "OrtGetApiBase")) : nullptr;
#else
        runtimeHandle = dlopen(libraryPath, RTLD_NOW | RTLD_LOCAL);
        auto getApi = runtimeHandle ? reinterpret_cast<const OrtApiBase *(ORT_API_CALL *)()>(
            dlsym(runtimeHandle, "OrtGetApiBase")) : nullptr;
#endif
        if (!getApi) throw std::runtime_error("The bundled ONNX Runtime could not be loaded.");
        api = getApi()->GetApi(17);
        if (!api) throw std::runtime_error("The bundled ONNX Runtime does not provide API 17.");
    }
    if (!env) {
        check(api->CreateEnv(ORT_LOGGING_LEVEL_WARNING, "YuiKokoro", &env));
    }
    if (session && loadedModel == modelPath) return;
    if (session) { api->ReleaseSession(session); session = nullptr; loadedModel.clear(); }
    OrtSessionOptions *options = nullptr;
    check(api->CreateSessionOptions(&options));
    try {
        check(api->SetIntraOpNumThreads(options, 2));
        check(api->SetInterOpNumThreads(options, 1));
        check(api->DisableCpuMemArena(options));
        check(api->DisableMemPattern(options));
        check(api->SetSessionGraphOptimizationLevel(options, ORT_ENABLE_ALL));
#if defined(_WIN32)
        int length = MultiByteToWideChar(CP_UTF8, 0, modelPath, -1, nullptr, 0);
        std::vector<wchar_t> path(length);
        MultiByteToWideChar(CP_UTF8, 0, modelPath, -1, path.data(), length);
        check(api->CreateSession(env, path.data(), options, &session));
#else
        check(api->CreateSession(env, modelPath, options, &session));
#endif
        loadedModel = modelPath;
    } catch (...) { api->ReleaseSessionOptions(options); throw; }
    api->ReleaseSessionOptions(options);
}
}

YUI_EXPORT void YuiKokoro_Cancel() {
    std::lock_guard<std::mutex> lock(cancellationMutex);
    if (activeRun) {
        auto status = api->RunOptionsSetTerminate(activeRun);
        if (status) api->ReleaseStatus(status);
    }
}

YUI_EXPORT void YuiKokoro_Release() {
    std::lock_guard<std::mutex> lock(synthesisMutex);
    if (session) { api->ReleaseSession(session); session = nullptr; }
    loadedModel.clear();
}

YUI_EXPORT YuiKokoroResult *YuiKokoro_Synthesize(const char *libraryPath,
    const char *modelPath, const char *voicePath, const int64_t *tokens,
    int32_t tokenCount, float speed) {
    auto result = new YuiKokoroResult{0, 24000, 0, nullptr, nullptr};
    std::lock_guard<std::mutex> lock(synthesisMutex);
    OrtMemoryInfo *memory = nullptr;
    OrtValue *inputs[3] = {nullptr, nullptr, nullptr};
    OrtValue *audio = nullptr;
    OrtTensorTypeAndShapeInfo *shape = nullptr;
    OrtRunOptions *run = nullptr;
    try {
        if (!libraryPath || !modelPath || !voicePath || !tokens || tokenCount < 3 || tokenCount > 242)
            throw std::runtime_error("Invalid or oversized Kokoro input.");
        if (!std::isfinite(speed) || speed < 0.5f || speed > 2.0f)
            throw std::runtime_error("Kokoro speed must be between 0.5 and 2.");
        initialize(libraryPath, modelPath);
        // Packed voice files are raw little-endian float32 [510, 256]. The style
        // row is selected by phoneme count, excluding the two boundary tokens.
        std::ifstream voice(voicePath, std::ios::binary);
        std::vector<float> style(256);
        voice.seekg(static_cast<std::streamoff>(tokenCount - 2) * 256 * sizeof(float));
        if (!voice.read(reinterpret_cast<char *>(style.data()), 256 * sizeof(float)))
            throw std::runtime_error("Kokoro voice data is missing or damaged.");
        check(api->CreateCpuMemoryInfo(OrtArenaAllocator, OrtMemTypeDefault, &memory));
        int64_t tokenShape[] = {1, tokenCount}, styleShape[] = {1, 256}, speedShape[] = {1};
        check(api->CreateTensorWithDataAsOrtValue(memory, const_cast<int64_t *>(tokens),
            tokenCount * sizeof(int64_t), tokenShape, 2, ONNX_TENSOR_ELEMENT_DATA_TYPE_INT64, &inputs[0]));
        check(api->CreateTensorWithDataAsOrtValue(memory, style.data(), 256 * sizeof(float),
            styleShape, 2, ONNX_TENSOR_ELEMENT_DATA_TYPE_FLOAT, &inputs[1]));
        check(api->CreateTensorWithDataAsOrtValue(memory, &speed, sizeof(float),
            speedShape, 1, ONNX_TENSOR_ELEMENT_DATA_TYPE_FLOAT, &inputs[2]));
        check(api->CreateRunOptions(&run));
        { std::lock_guard<std::mutex> cancelLock(cancellationMutex); activeRun = run; }
        const char *names[] = {"tokens", "style", "speed"}, *outputs[] = {"audio"};
        check(api->Run(session, run, names, const_cast<const OrtValue *const *>(inputs), 3, outputs, 1, &audio));
        check(api->GetTensorTypeAndShape(audio, &shape));
        ONNXTensorElementDataType elementType;
        check(api->GetTensorElementType(shape, &elementType));
        if (elementType != ONNX_TENSOR_ELEMENT_DATA_TYPE_FLOAT) throw std::runtime_error("Kokoro returned an invalid audio type.");
        size_t count = 0;
        check(api->GetTensorShapeElementCount(shape, &count));
        if (!count || count > 24000 * 60) throw std::runtime_error("Kokoro returned an invalid audio length.");
        float *data = nullptr;
        check(api->GetTensorMutableData(audio, reinterpret_cast<void **>(&data)));
        result->samples = new float[count];
        for (size_t i = 0; i < count; ++i) {
            if (!std::isfinite(data[i])) throw std::runtime_error("Kokoro returned non-finite audio.");
            result->samples[i] = std::max(-1.0f, std::min(1.0f, data[i]));
        }
        result->count = static_cast<int64_t>(count);
        result->ok = 1;
    } catch (const std::exception &ex) {
        result->error = new char[std::strlen(ex.what()) + 1];
        std::strcpy(result->error, ex.what());
    }
    { std::lock_guard<std::mutex> cancelLock(cancellationMutex); activeRun = nullptr; }
    if (run) api->ReleaseRunOptions(run);
    if (shape) api->ReleaseTensorTypeAndShapeInfo(shape);
    if (audio) api->ReleaseValue(audio);
    for (auto input : inputs) if (input) api->ReleaseValue(input);
    if (memory) api->ReleaseMemoryInfo(memory);
    return result;
}

YUI_EXPORT void YuiKokoro_Free(YuiKokoroResult *result) {
    if (!result) return;
    delete[] result->samples;
    delete[] result->error;
    delete result;
}
