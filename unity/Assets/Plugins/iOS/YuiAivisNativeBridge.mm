#import <Foundation/Foundation.h>
#include <stdlib.h>
#include <string>

#include "../NativeAivis/YuiAivisRuntime.h"

namespace {

const char *YuiAivisCopyCString(const std::string& value)
{
    char *output = static_cast<char *>(malloc(value.size() + 1));
    if (output == nullptr) {
        return nullptr;
    }
    memcpy(output, value.c_str(), value.size() + 1);
    return output;
}

std::string YuiAivisRequestString(const char *requestJsonPointer)
{
    return requestJsonPointer == nullptr ? std::string() : std::string(requestJsonPointer);
}

}  // namespace

extern "C" const char *YuiAivisNativeBridge_GetStatus(const char *requestJsonPointer)
{
    @autoreleasepool {
        return YuiAivisCopyCString(
            yui::aivis::GetStatusJson(YuiAivisRequestString(requestJsonPointer), true));
    }
}

extern "C" const char *YuiAivisNativeBridge_Synthesize(const char *requestJsonPointer)
{
    @autoreleasepool {
        return YuiAivisCopyCString(
            yui::aivis::SynthesizeJson(YuiAivisRequestString(requestJsonPointer)));
    }
}

extern "C" void YuiAivisNativeBridge_Free(const char *pointer)
{
    if (pointer != nullptr) {
        free((void *)pointer);
    }
}
