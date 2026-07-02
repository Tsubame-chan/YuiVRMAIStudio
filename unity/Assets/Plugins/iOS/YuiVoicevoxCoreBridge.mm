#import <Foundation/Foundation.h>
#include <stdint.h>
#include <string.h>
#include <stdlib.h>

#include "Voicevox/voicevox_core.xcframework/ios-arm64/voicevox_core.framework/Headers/voicevox_core.h"

static const VoicevoxOnnxruntime *yuiVoicevoxOrt = nullptr;
static OpenJtalkRc *yuiVoicevoxOpenJtalk = nullptr;
static VoicevoxSynthesizer *yuiVoicevoxSynthesizer = nullptr;
static NSString *yuiVoicevoxLoadedModelPath = nil;

static const char *YuiVoicevoxCString(NSString *text)
{
    return strdup([text UTF8String]);
}

static const char *YuiVoicevoxJson(NSDictionary *value)
{
    NSData *data = [NSJSONSerialization dataWithJSONObject:value options:0 error:nil];
    NSString *text = data == nil
        ? @"{\"ok\":false,\"error_code\":\"invalid_json\",\"error_message\":\"Failed to encode VOICEVOX Core bridge JSON.\"}"
        : [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
    return YuiVoicevoxCString(text);
}

static const char *YuiVoicevoxError(NSString *code, NSString *message)
{
    return YuiVoicevoxJson(@{
        @"ok": @NO,
        @"error_code": code ?: @"voicevox_error",
        @"error_message": message ?: @"VOICEVOX Core bridge failed."
    });
}

static NSString *YuiVoicevoxResultMessage(VoicevoxResultCode code)
{
    return [NSString stringWithFormat:@"VOICEVOX Core error code: %d", (int)code];
}

static NSString *YuiVoicevoxString(NSDictionary *request, NSString *key)
{
    id value = request[key];
    return [value isKindOfClass:[NSString class]] ? (NSString *)value : @"";
}

static double YuiVoicevoxDouble(NSDictionary *request, NSString *key, double fallback)
{
    id value = request[key];
    return [value respondsToSelector:@selector(doubleValue)] ? [value doubleValue] : fallback;
}

static uint32_t YuiVoicevoxUInt32(NSDictionary *request, NSString *key, uint32_t fallback)
{
    id value = request[key];
    return [value respondsToSelector:@selector(unsignedIntValue)] ? [value unsignedIntValue] : fallback;
}

static BOOL YuiVoicevoxEnsureSynthesizer(NSString *dictPath)
{
    if (yuiVoicevoxSynthesizer != nullptr) {
        return YES;
    }

    VoicevoxResultCode result = voicevox_onnxruntime_init_once(&yuiVoicevoxOrt);
    if (result != VOICEVOX_RESULT_OK || yuiVoicevoxOrt == nullptr) {
        return NO;
    }

    result = voicevox_open_jtalk_rc_new([dictPath UTF8String], &yuiVoicevoxOpenJtalk);
    if (result != VOICEVOX_RESULT_OK || yuiVoicevoxOpenJtalk == nullptr) {
        return NO;
    }

    VoicevoxInitializeOptions options = voicevox_make_default_initialize_options();
    options.acceleration_mode = VOICEVOX_ACCELERATION_MODE_CPU;
    options.cpu_num_threads = 0;
    result = voicevox_synthesizer_new(
        yuiVoicevoxOrt,
        yuiVoicevoxOpenJtalk,
        options,
        &yuiVoicevoxSynthesizer);
    return result == VOICEVOX_RESULT_OK && yuiVoicevoxSynthesizer != nullptr;
}

static VoicevoxResultCode YuiVoicevoxEnsureModelLoaded(NSString *modelPath)
{
    if (yuiVoicevoxLoadedModelPath != nil && [yuiVoicevoxLoadedModelPath isEqualToString:modelPath]) {
        return VOICEVOX_RESULT_OK;
    }

    VoicevoxVoiceModelFile *model = nullptr;
    VoicevoxResultCode result = voicevox_voice_model_file_open([modelPath UTF8String], &model);
    if (result != VOICEVOX_RESULT_OK || model == nullptr) {
        return result;
    }

    result = voicevox_synthesizer_load_voice_model(yuiVoicevoxSynthesizer, model);
    voicevox_voice_model_file_delete(model);
    if (result == VOICEVOX_RESULT_OK || result == VOICEVOX_RESULT_MODEL_ALREADY_LOADED_ERROR) {
        yuiVoicevoxLoadedModelPath = [modelPath copy];
        return VOICEVOX_RESULT_OK;
    }

    return result;
}

static NSString *YuiVoicevoxPatchAudioQuery(
    char *audioQueryJson,
    double speedScale,
    double pitchScale,
    double intonationScale,
    double volumeScale,
    double prePhonemeLength,
    double postPhonemeLength)
{
    NSData *data = [[NSData alloc] initWithBytes:audioQueryJson length:strlen(audioQueryJson)];
    NSError *error = nil;
    NSMutableDictionary *query = [[NSJSONSerialization JSONObjectWithData:data options:NSJSONReadingMutableContainers error:&error] mutableCopy];
    if (query == nil) {
        return [[NSString alloc] initWithBytes:audioQueryJson length:strlen(audioQueryJson) encoding:NSUTF8StringEncoding];
    }

    query[@"speedScale"] = @(MAX(0.5, MIN(2.0, speedScale)));
    query[@"pitchScale"] = @(MAX(-0.15, MIN(0.15, pitchScale)));
    query[@"intonationScale"] = @(MAX(0.0, MIN(2.0, intonationScale)));
    query[@"volumeScale"] = @(MAX(0.0, MIN(2.0, volumeScale)));
    query[@"prePhonemeLength"] = @(MAX(0.0, MIN(1.5, prePhonemeLength)));
    query[@"postPhonemeLength"] = @(MAX(0.0, MIN(1.5, postPhonemeLength)));

    NSData *patched = [NSJSONSerialization dataWithJSONObject:query options:0 error:nil];
    return patched == nil
        ? [[NSString alloc] initWithBytes:audioQueryJson length:strlen(audioQueryJson) encoding:NSUTF8StringEncoding]
        : [[NSString alloc] initWithData:patched encoding:NSUTF8StringEncoding];
}

extern "C" const char *YuiVoicevoxCoreBridge_Synthesize(const char *requestJsonPointer)
{
    @autoreleasepool {
        if (requestJsonPointer == nullptr) {
            return YuiVoicevoxError(@"invalid_request", @"VOICEVOX Core request is null.");
        }

        NSString *requestJson = [NSString stringWithUTF8String:requestJsonPointer];
        NSData *requestData = [requestJson dataUsingEncoding:NSUTF8StringEncoding];
        NSError *error = nil;
        NSDictionary *request = [NSJSONSerialization JSONObjectWithData:requestData options:0 error:&error];
        if (![request isKindOfClass:[NSDictionary class]]) {
            return YuiVoicevoxError(@"invalid_request", @"VOICEVOX Core request is not valid JSON.");
        }

        NSString *text = [YuiVoicevoxString(request, @"text") stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceAndNewlineCharacterSet]];
        if (text.length == 0) {
            return YuiVoicevoxError(@"invalid_request", @"VOICEVOX Core text is empty.");
        }

        NSString *dictPath = YuiVoicevoxString(request, @"open_jtalk_dict_path");
        NSString *modelPath = YuiVoicevoxString(request, @"model_path");
        if (![[NSFileManager defaultManager] fileExistsAtPath:dictPath]) {
            return YuiVoicevoxError(@"dict_missing", [NSString stringWithFormat:@"OpenJTalk dictionary was not found: %@", dictPath]);
        }
        if (![[NSFileManager defaultManager] fileExistsAtPath:modelPath]) {
            return YuiVoicevoxError(@"model_missing", [NSString stringWithFormat:@"VOICEVOX model was not found: %@", modelPath]);
        }

        if (!YuiVoicevoxEnsureSynthesizer(dictPath)) {
            return YuiVoicevoxError(@"init_failed", @"Failed to initialize VOICEVOX Core synthesizer.");
        }

        VoicevoxResultCode result = YuiVoicevoxEnsureModelLoaded(modelPath);
        if (result != VOICEVOX_RESULT_OK) {
            return YuiVoicevoxError(@"model_load_failed", YuiVoicevoxResultMessage(result));
        }

        uint32_t styleId = YuiVoicevoxUInt32(request, @"style_id", 14);
        char *audioQueryJson = nullptr;
        result = voicevox_synthesizer_create_audio_query(
            yuiVoicevoxSynthesizer,
            [text UTF8String],
            styleId,
            &audioQueryJson);
        if (result != VOICEVOX_RESULT_OK || audioQueryJson == nullptr) {
            if (audioQueryJson != nullptr) {
                voicevox_json_free(audioQueryJson);
            }
            return YuiVoicevoxError(@"audio_query_failed", YuiVoicevoxResultMessage(result));
        }

        NSString *patchedQuery = YuiVoicevoxPatchAudioQuery(
            audioQueryJson,
            YuiVoicevoxDouble(request, @"speed_scale", 1.0),
            YuiVoicevoxDouble(request, @"pitch_scale", 0.0),
            YuiVoicevoxDouble(request, @"intonation_scale", 1.0),
            YuiVoicevoxDouble(request, @"volume_scale", 1.0),
            YuiVoicevoxDouble(request, @"pre_phoneme_length", 0.1),
            YuiVoicevoxDouble(request, @"post_phoneme_length", 0.1));
        voicevox_json_free(audioQueryJson);
        if (patchedQuery.length == 0) {
            return YuiVoicevoxError(@"audio_query_failed", @"VOICEVOX Core audio query could not be encoded as UTF-8 JSON.");
        }

        VoicevoxSynthesisOptions synthesisOptions = voicevox_make_default_synthesis_options();
        synthesisOptions.enable_interrogative_upspeak = true;
        uintptr_t wavLength = 0;
        uint8_t *wav = nullptr;
        result = voicevox_synthesizer_synthesis(
            yuiVoicevoxSynthesizer,
            [patchedQuery UTF8String],
            styleId,
            synthesisOptions,
            &wavLength,
            &wav);
        if (result != VOICEVOX_RESULT_OK || wav == nullptr || wavLength == 0) {
            if (wav != nullptr) {
                voicevox_wav_free(wav);
            }
            return YuiVoicevoxError(@"synthesis_failed", YuiVoicevoxResultMessage(result));
        }

        NSData *wavData = [[NSData alloc] initWithBytes:wav length:(NSUInteger)wavLength];
        voicevox_wav_free(wav);
        NSString *audioBase64 = [wavData base64EncodedStringWithOptions:0];
        return YuiVoicevoxJson(@{
            @"ok": @YES,
            @"audio_base64": audioBase64,
            @"sample_rate": @24000,
            @"duration_ms": @0
        });
    }
}

extern "C" void YuiVoicevoxCoreBridge_Free(const char *pointer)
{
    if (pointer != nullptr) {
        free((void *)pointer);
    }
}
