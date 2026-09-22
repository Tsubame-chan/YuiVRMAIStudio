using System;
using System.IO;
using UnityEngine;
using YuiPhysicalAI.Avatar;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiChatPanel
    {
        private string activeCharacterProfileId;
        private bool characterProfileWritable;
        private YuiCharacterDialogueStore DialogueStore => new YuiCharacterDialogueStore(Path.Combine(Application.persistentDataPath, "CharacterDialogue"));
        private YuiCharacterProfileStore ProfileStore => new YuiCharacterProfileStore(Path.Combine(Application.persistentDataPath, "CharacterProfiles"));
        private YuiCharacterProfile CaptureCharacterProfile() => new YuiCharacterProfile {
            Name = characterName, Instruction = customInstruction, TtsMode = ttsMode, SpeakerId = speakerId,
            Speed = speedScale, Pitch = pitchScale, Intonation = intonationScale, SynthesisVolume = synthesisVolumeScale,
            PrePhoneme = prePhonemeLength, PostPhoneme = postPhonemeLength,
            VoiceGender = irodoriVoiceGender, VoiceInstruction = irodoriVoiceInstruct };
        private void SaveCharacterProfile()
        {
            if (activeCharacterProfileId == null || !characterProfileWritable) return;
            try { ProfileStore.Save(activeCharacterProfileId, CaptureCharacterProfile()); }
            catch (Exception ex) { SetStatus("キャラクター設定を保存できません: " + ex.Message); Debug.LogWarning(ex.Message); }
        }
        private void SelectCharacterProfile(bool initial = false)
        {
            if (runtimeVrmImporter == null) runtimeVrmImporter = GetComponent<YuiRuntimeVrmImporter>() ?? YuiSceneObjectFinder.FindFirst<YuiRuntimeVrmImporter>();
            var id = ChatCharacterId();
            if (id == activeCharacterProfileId) return;
            if (!initial) SaveCharacterProfile();
            activeCharacterProfileId = id;
            characterProfileWritable = false;
            try
            {
                var profile = ProfileStore.Read(id);
                if (profile == null)
                {
                    profile = CaptureCharacterProfile();
                    if (!initial)
                    {
                        profile.Name = "Yui"; profile.Instruction = "";
                        foreach (var entry in YuiAvatarLibrary.Read()) if (entry.id == id) { profile.Name = entry.name; break; }
                    }
                    ProfileStore.Save(id, profile);
                }
                characterName = string.IsNullOrWhiteSpace(profile.Name) ? "Yui" : profile.Name;
                customInstruction = profile.Instruction ?? "";
                ttsMode = NormalizeTtsMode(profile.TtsMode);
                var tuning = YuiTtsTuningPrefs.Sanitize(ttsMode, new YuiSavedTtsTuning(profile.SpeakerId, profile.Speed, profile.Pitch,
                    profile.Intonation, profile.SynthesisVolume, profile.PrePhoneme, profile.PostPhoneme));
                speakerId = tuning.SpeakerId; speedScale = tuning.SpeedScale; pitchScale = tuning.PitchScale;
                intonationScale = tuning.IntonationScale; synthesisVolumeScale = tuning.SynthesisVolumeScale;
                prePhonemeLength = tuning.PrePhonemeLength; postPhonemeLength = tuning.PostPhonemeLength;
                irodoriVoiceGender = NormalizeIrodoriVoiceGender(profile.VoiceGender);
                irodoriVoiceInstruct = NormalizeIrodoriVoiceInstruct(profile.VoiceInstruction);
                characterProfileWritable = true;
                if (!initial)
                {
                    MigrateRecentDialogue();
                    _=RestoreConversationViewAsync();
                    latestVision = null; retryChatMessage = null;
                    pendingVisionImageAttachment.MarkConsumedAfterSuccessfulChat();
                    ConfigureAiRuntimeRouter(); ConfigureChatdollKitVoicevoxTts();
                }
            }
            catch (Exception ex)
            {
                // Do not overwrite a damaged profile using whichever character happened to be active.
                characterName = "Yui"; customInstruction = "";
                SetStatus("キャラクター設定を読み込めません。元ファイルは保持されています。"); Debug.LogWarning(ex.Message);
            }
        }
        private bool CanChangeCharacter()
        {
            if (runtimeVrmImporter != null && runtimeVrmImporter.IsImporting)
            { SetStatus("アバターの読み込みが終わるまでお待ちください。"); return false; }
            if (isSending || isRecording || realtimeStreamActive || realtimeWaitingForResponse || realtimeSocket != null)
            { SetStatus("会話を停止してからキャラクターを切り替えてください。"); return false; }
            StopRealtimeAudioPlayback();
            return true;
        }
    }
}
