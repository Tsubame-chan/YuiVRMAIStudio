#include <jni.h>
#include <cstdlib>
#include <cstring>
#include <string>

#include "../NativeAivis/YuiAivisRuntime.h"

namespace {

char* CopyCString(const std::string& value) {
    char* output = static_cast<char*>(std::malloc(value.size() + 1));
    if (output == nullptr) {
        return nullptr;
    }
    std::memcpy(output, value.c_str(), value.size() + 1);
    return output;
}

std::string JStringToUtf8(JNIEnv* env, jstring value) {
    if (env == nullptr || value == nullptr) {
        return "";
    }
    const char* chars = env->GetStringUTFChars(value, nullptr);
    if (chars == nullptr) {
        return "";
    }
    std::string output(chars);
    env->ReleaseStringUTFChars(value, chars);
    return output;
}

}  // namespace

extern "C" char* YuiAivisNativeBridge_GetStatus(const char* requestJsonPointer) {
    const std::string requestJson = requestJsonPointer == nullptr ? "" : requestJsonPointer;
    return CopyCString(yui::aivis::GetStatusJson(requestJson, true));
}

extern "C" char* YuiAivisNativeBridge_Synthesize(const char* requestJsonPointer) {
    const std::string requestJson = requestJsonPointer == nullptr ? "" : requestJsonPointer;
    return CopyCString(yui::aivis::SynthesizeJson(requestJson));
}

extern "C" void YuiAivisNativeBridge_Free(const char* pointer) {
    if (pointer != nullptr) {
        std::free(const_cast<char*>(pointer));
    }
}

extern "C" JNIEXPORT jstring JNICALL
Java_com_yuivrmai_localai_YuiAivisNativeBridge_getStatus(JNIEnv* env, jclass, jstring requestJson) {
    const std::string response = yui::aivis::GetStatusJson(JStringToUtf8(env, requestJson), true);
    return env->NewStringUTF(response.c_str());
}

extern "C" JNIEXPORT jstring JNICALL
Java_com_yuivrmai_localai_YuiAivisNativeBridge_synthesize(JNIEnv* env, jclass, jstring requestJson) {
    const std::string response = yui::aivis::SynthesizeJson(JStringToUtf8(env, requestJson));
    return env->NewStringUTF(response.c_str());
}
