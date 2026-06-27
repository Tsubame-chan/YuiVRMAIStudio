using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using YuiPhysicalAI.Audio;
using YuiPhysicalAI.Api;
using YuiPhysicalAI.Avatar;
using YuiPhysicalAI.Core;
using YuiPhysicalAI.Platform;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiChatPanel
    {
        private string GetDefaultAvatarSlot()
        {
            return YuiBuildProfile.DefaultAvatarSlot;
        }

        private static string UpgradeDefaultAvatarSlot(string savedAvatarSlot, string defaultAvatarSlot)
        {
            if (PlayerPrefs.GetInt(YuiPrefsKeys.AvatarSlotDefaultUpgraded, 0) == 0
                && string.Equals(defaultAvatarSlot, YuiAvatarSlots.DemoKikyo, StringComparison.OrdinalIgnoreCase)
                && string.Equals(YuiAvatarSlots.Normalize(savedAvatarSlot), YuiAvatarSlots.UnityChanDefault, StringComparison.OrdinalIgnoreCase))
            {
                PlayerPrefs.SetInt(YuiPrefsKeys.AvatarSlotDefaultUpgraded, 1);
                PlayerPrefs.SetString(AvatarSlotPrefsKey, defaultAvatarSlot);
                PlayerPrefs.Save();
                return defaultAvatarSlot;
            }

            return savedAvatarSlot;
        }

        private static string AvatarSlotPrefsKey => $"{AvatarSlotKey}.{GetLocalPrefsScope()}";

        private static string GetLocalPrefsScope()
        {
            var source = string.IsNullOrWhiteSpace(Application.dataPath)
                ? Application.identifier
                : Application.dataPath;
            return StableHash(source ?? "default");
        }

        private static string StableHash(string value)
        {
            unchecked
            {
                var hash = 2166136261u;
                for (var i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }

                return hash.ToString("x8");
            }
        }

        private void ApplyAvatarSlot(bool showStatus)
        {
            if (avatarSwitcher == null)
            {
                return;
            }

            var requestedSlot = avatarSlot;
            var waitForSavedCustomVrm = YuiAvatarSlots.IsCustomVrm(requestedSlot)
                && runtimeVrmImporter != null
                && runtimeVrmImporter.HasRestorableSavedCustomVrm
                && !avatarSwitcher.HasCustomAvatar;
            avatarSwitcher.SetAvatarSlot(avatarSlot, !waitForSavedCustomVrm);
            if (showStatus)
            {
                if (!string.Equals(requestedSlot, avatarSwitcher.ActiveSlot, StringComparison.OrdinalIgnoreCase))
                {
                    SetStatus(YuiAvatarSlots.IsCustomVrm(requestedSlot)
                        ? "Load a Custom VRM first; using the default avatar."
                        : "Selected avatar is not available; using the default avatar.");
                }
                else
                {
                    SetStatus("Avatar setting saved");
                }
            }
        }


    }
}
