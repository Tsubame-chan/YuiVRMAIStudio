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
        private void SendCurrentInput()
        {
            if (inputField == null || isSending)
            {
                return;
            }

            var message = inputField.text.Trim();
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            inputField.text = string.Empty;
            _ = SendMessageAsync(message);
        }

        private void ToggleRecording()
        {
            if (isSending)
            {
                return;
            }

            if (isRecording)
            {
                _ = StopRecordingAndSendAsync();
                return;
            }

            StartRecording();
        }

        private void CaptureScreenAndAnalyze()
        {
            if (isSending)
            {
                return;
            }

            _ = CaptureScreenAndAnalyzeAsync();
        }

        private void ImportImageAndAnalyze()
        {
            if (isSending)
            {
                return;
            }

            _ = ImportImageAndAnalyzeFromPickerAsync();
        }


    }
}
