#import <AVFoundation/AVFoundation.h>
#import <Foundation/Foundation.h>

#include <algorithm>
#include <math.h>
#include <mutex>
#include <stdlib.h>
#include <string.h>

static AVAudioEngine* gEngine = nil;
static float* gAudioBuffer = NULL;
static float* gSnapshotBuffer = NULL;
static int gBufferSize = 0;
static int gSampleRate = 0;
static int gWritePosition = 0;
static int gIsRecording = 0;
static double gResamplePhase = 0.0;
static float gPreviousInputSample = 0.0f;
static bool gHasPreviousInputSample = false;
static std::mutex gAudioMutex;

static void FreeBuffers()
{
    if (gAudioBuffer != NULL)
    {
        free(gAudioBuffer);
        gAudioBuffer = NULL;
    }

    if (gSnapshotBuffer != NULL)
    {
        free(gSnapshotBuffer);
        gSnapshotBuffer = NULL;
    }
}

static void ResetStateWithoutLock()
{
    gBufferSize = 0;
    gSampleRate = 0;
    gWritePosition = 0;
    gIsRecording = 0;
    gResamplePhase = 0.0;
    gPreviousInputSample = 0.0f;
    gHasPreviousInputSample = false;
}

static void StopEngine()
{
    if (gEngine == nil)
    {
        return;
    }

    AVAudioInputNode* inputNode = [gEngine inputNode];
    [inputNode removeTapOnBus:0];
    [gEngine stop];
    gEngine = nil;

    AVAudioSession* session = [AVAudioSession sharedInstance];
    [session setActive:NO
           withOptions:AVAudioSessionSetActiveOptionNotifyOthersOnDeactivation
                 error:nil];
}

static bool ConfigureAudioSession(bool enableVoiceProcessing, int frequency)
{
    AVAudioSession* session = [AVAudioSession sharedInstance];
    NSError* error = nil;

    AVAudioSessionCategoryOptions options =
        AVAudioSessionCategoryOptionDefaultToSpeaker |
        AVAudioSessionCategoryOptionAllowBluetoothHFP |
        AVAudioSessionCategoryOptionAllowBluetoothA2DP;

    NSString* mode = enableVoiceProcessing ? AVAudioSessionModeVoiceChat : AVAudioSessionModeMeasurement;
    if (![session setCategory:AVAudioSessionCategoryPlayAndRecord
                         mode:mode
                      options:options
                        error:&error])
    {
        NSLog(@"[iOSNativeMicrophonePlugin] setCategory failed: %@", error);
        return false;
    }

    if (frequency > 0)
    {
        [session setPreferredSampleRate:(double)frequency error:nil];
    }

    [session setPreferredIOBufferDuration:0.02 error:nil];

    if (![session setActive:YES error:&error])
    {
        NSLog(@"[iOSNativeMicrophonePlugin] setActive failed: %@", error);
        return false;
    }

    return true;
}

extern "C" void iOSNativeMicrophonePlugin_ForcePlaybackSpeakerOutput()
{
    AVAudioSession* session = [AVAudioSession sharedInstance];
    NSError* error = nil;

    AVAudioSessionCategoryOptions options =
        AVAudioSessionCategoryOptionAllowBluetoothA2DP |
        AVAudioSessionCategoryOptionAllowAirPlay;

    if (![session setCategory:AVAudioSessionCategoryPlayback
                         mode:AVAudioSessionModeDefault
                      options:options
                        error:&error])
    {
        NSLog(@"[iOSNativeMicrophonePlugin] force playback speaker setCategory failed: %@", error);
        return;
    }

    [session setActive:YES error:nil];
}

static float SampleInputFrame(AVAudioPCMBuffer* buffer, AVAudioFramePosition frameIndex)
{
    const float* const* channelData = buffer.floatChannelData;
    AVAudioChannelCount channels = buffer.format.channelCount;

    if (channelData == NULL || channels == 0)
    {
        return 0.0f;
    }

    float sample = 0.0f;
    for (AVAudioChannelCount channel = 0; channel < channels; channel++)
    {
        sample += channelData[channel][frameIndex];
    }

    return sample / (float)channels;
}

static void WriteRingSampleWithoutLock(float sample)
{
    if (gAudioBuffer == NULL || gBufferSize <= 0)
    {
        return;
    }

    gAudioBuffer[gWritePosition] = std::max(-1.0f, std::min(1.0f, sample));
    gWritePosition = (gWritePosition + 1) % gBufferSize;
}

static void WriteInputBuffer(AVAudioPCMBuffer* buffer, double inputSampleRate)
{
    if (buffer == nil || buffer.frameLength == 0 || inputSampleRate <= 0.0)
    {
        return;
    }

    std::lock_guard<std::mutex> lock(gAudioMutex);
    if (!gIsRecording || gAudioBuffer == NULL || gSampleRate <= 0)
    {
        return;
    }

    const AVAudioFrameCount frameCount = buffer.frameLength;
    const double inputPerOutput = inputSampleRate / (double)gSampleRate;

    while (gResamplePhase < (double)frameCount)
    {
        int frameIndex = (int)floor(gResamplePhase);
        double fraction = gResamplePhase - (double)frameIndex;

        float sampleA = 0.0f;
        float sampleB = 0.0f;

        if (frameIndex <= 0)
        {
            sampleA = gHasPreviousInputSample ? gPreviousInputSample : SampleInputFrame(buffer, 0);
        }
        else
        {
            sampleA = SampleInputFrame(buffer, frameIndex - 1);
        }

        if (frameIndex < (int)frameCount)
        {
            sampleB = SampleInputFrame(buffer, frameIndex);
        }
        else
        {
            sampleB = SampleInputFrame(buffer, frameCount - 1);
        }

        float outputSample = sampleA + (sampleB - sampleA) * (float)fraction;
        WriteRingSampleWithoutLock(outputSample);
        gResamplePhase += inputPerOutput;
    }

    gResamplePhase -= (double)frameCount;
    gPreviousInputSample = SampleInputFrame(buffer, frameCount - 1);
    gHasPreviousInputSample = true;
}

extern "C" int iOSNativeMicrophonePlugin_GetDeviceCount()
{
    return 1;
}

extern "C" const char* iOSNativeMicrophonePlugin_GetDeviceName(int index)
{
    return index == 0 ? "iPhone Microphone" : "";
}

static int StartRecording(int lengthSec, int frequency, bool enableVoiceProcessing)
{
    if (lengthSec <= 0 || frequency <= 0)
    {
        return 0;
    }

    StopEngine();

    {
        std::lock_guard<std::mutex> lock(gAudioMutex);
        FreeBuffers();
        ResetStateWithoutLock();

        gBufferSize = lengthSec * frequency;
        gSampleRate = frequency;
        gAudioBuffer = (float*)calloc((size_t)gBufferSize, sizeof(float));
        gSnapshotBuffer = (float*)calloc((size_t)gBufferSize, sizeof(float));

        if (gAudioBuffer == NULL || gSnapshotBuffer == NULL)
        {
            FreeBuffers();
            ResetStateWithoutLock();
            return 0;
        }
    }

    if (!ConfigureAudioSession(enableVoiceProcessing, frequency))
    {
        std::lock_guard<std::mutex> lock(gAudioMutex);
        FreeBuffers();
        ResetStateWithoutLock();
        return 0;
    }

    gEngine = [[AVAudioEngine alloc] init];
    AVAudioInputNode* inputNode = [gEngine inputNode];
    AVAudioFormat* inputFormat = [inputNode outputFormatForBus:0];
    double inputSampleRate = inputFormat.sampleRate;

    [inputNode installTapOnBus:0
                    bufferSize:1024
                        format:inputFormat
                         block:^(AVAudioPCMBuffer* buffer, AVAudioTime* when) {
        (void)when;
        WriteInputBuffer(buffer, inputSampleRate);
    }];

    NSError* error = nil;
    if (![gEngine startAndReturnError:&error])
    {
        NSLog(@"[iOSNativeMicrophonePlugin] AVAudioEngine start failed: %@", error);
        StopEngine();
        std::lock_guard<std::mutex> lock(gAudioMutex);
        FreeBuffers();
        ResetStateWithoutLock();
        return 0;
    }

    {
        std::lock_guard<std::mutex> lock(gAudioMutex);
        gIsRecording = 1;
    }

    return 1;
}

extern "C" int iOSNativeMicrophonePlugin_Start(const char* deviceName, int lengthSec, int frequency)
{
    (void)deviceName;
    return StartRecording(lengthSec, frequency, false);
}

extern "C" int iOSNativeMicrophonePlugin_StartWithVoiceProcessing(const char* deviceName, int lengthSec, int frequency, int enableVoiceProcessing)
{
    (void)deviceName;
    return StartRecording(lengthSec, frequency, enableVoiceProcessing == 1);
}

extern "C" void iOSNativeMicrophonePlugin_End()
{
    StopEngine();
    std::lock_guard<std::mutex> lock(gAudioMutex);
    gIsRecording = 0;
}

extern "C" int iOSNativeMicrophonePlugin_IsRecording()
{
    std::lock_guard<std::mutex> lock(gAudioMutex);
    return gIsRecording;
}

extern "C" int iOSNativeMicrophonePlugin_GetPosition()
{
    std::lock_guard<std::mutex> lock(gAudioMutex);
    return gIsRecording ? gWritePosition : 0;
}

extern "C" float* iOSNativeMicrophonePlugin_GetAudioData()
{
    std::lock_guard<std::mutex> lock(gAudioMutex);
    if (gAudioBuffer == NULL || gSnapshotBuffer == NULL || gBufferSize <= 0)
    {
        return NULL;
    }

    memcpy(gSnapshotBuffer, gAudioBuffer, (size_t)gBufferSize * sizeof(float));
    return gSnapshotBuffer;
}

extern "C" void iOSNativeMicrophonePlugin_ForceReset()
{
    StopEngine();
    std::lock_guard<std::mutex> lock(gAudioMutex);
    FreeBuffers();
    ResetStateWithoutLock();
}
