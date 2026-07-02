cmake_minimum_required(VERSION 3.22)

project(YuiAivisNativeBridge LANGUAGES CXX)

add_library(voicevox_core SHARED IMPORTED)
set_target_properties(voicevox_core PROPERTIES
    IMPORTED_LOCATION
        "${CMAKE_CURRENT_LIST_DIR}/Voicevox/voicevox_core-android-arm64-0.16.4/lib/libvoicevox_core.so"
)

add_library(onnxruntime SHARED IMPORTED)
set_target_properties(onnxruntime PROPERTIES
    IMPORTED_LOCATION
        "${CMAKE_CURRENT_LIST_DIR}/ONNXRuntime/arm64-v8a/libonnxruntime.so"
)

add_library(YuiAivisNativeBridge SHARED
    YuiAivisNativeBridge.cpp
    ../NativeAivis/YuiAivisRuntime.cpp
    ../NativeAivis/YuiAivisStyleBertRuntime.cpp
)

target_compile_features(YuiAivisNativeBridge PRIVATE cxx_std_17)

target_include_directories(YuiAivisNativeBridge PRIVATE
    ../NativeAivis
    Voicevox/voicevox_core-android-arm64-0.16.4/include
    ../../StreamingAssets/YuiLocalAI/Aivis/Runtime/ONNXRuntime/include
)

target_link_libraries(YuiAivisNativeBridge PRIVATE
    voicevox_core
    onnxruntime
    log
)
