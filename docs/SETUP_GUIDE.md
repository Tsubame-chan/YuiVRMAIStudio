# Windowsセットアップ / Windows setup

[FAQ](HELP.md) · [macOS](MAC_PUBLIC_BETA.md)

現在のWindows配布版は **v0.2.4-beta.1** です。

## アプリを使う

1. [Release](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/tag/v0.2.4-beta.1)から `YuiVRMAIStudio_WindowsPublicBeta_v0.2.4-beta.1_windows.zip` を取得します。
2. フォルダ全体を展開して `Yui VRM AI Studio.exe` を起動します。`YuiFilePickerHelper.exe` とDataフォルダ等を移動・削除しないでください。
3. 初回案内を確認して、AI/音声とWindows runtimeを取得します。約2.5GBに加え展開用空き容量が必要です。通常Pythonを別に導入する必要はありません。
4. 端末内AI、または設定したOpenAI APIで会話します。高品質なAPI会話にはAPIキー・通信・API利用料金が必要です。

SmartScreenが出る場合は配布元を確認し、OSの案内で許可します。`.sha256` はZIPの破損確認用。Code ZIPはソースで実行アプリではありません。

現在のソースにはE2BのLiteRT-LM workerとローカルVOICEVOX workerがあります。古いbeta.5の「WindowsローカルGemma未対応」とは異なりますが、今回のruntimeがすべてのWindows GPU/CPUで動作するという保証ではありません。標準音声は日本語5声です。

## アバター・設定

設定から `.vrm` を選びます。Unity/VRChatアバターは先にVRM化してください。「着替え」は人格と記憶を維持します。[導入ガイド](AVATAR_IMPORT.md)。性格はキャラクター設定、ローカルAIの上限・温度等は詳細設定から変更できます。案内はHelpから再表示できます。

## Backendを使う場合

初回取得した `YuiBackend` の `Start_Yui_Backend.bat` / `Stop_Yui_Backend.bat` を使います。端末内workerはこのHTTPサーバーを必要としません。拡張機能を使う場合はBackend `.env` のキーと対象STT/TTSエンジンを設定します。アプリのDirect APIキーとは別です。

ソースからのセットアップではPowerShellで:

```powershell
.\scripts\setup_backend_byok.ps1
notepad .env
.\scripts\start_local_services.ps1
# 停止
.\scripts\stop_local_services.ps1
```

前提ツール・実行ポリシーの案内はスクリプトを参照してください。リモート接続は双方で到達できるVPNアドレスとlisten設定が必要です。

## 困ったとき

- ファイル選択が出ない: `YuiFilePickerHelper.exe` が本体と同じフォルダか確認。
- データ取得が失敗: 接続・空き容量を確認し、同じReleaseのmanifest/データを使って再試行。
- ローカルAI/音声が失敗: 設定で選んだエンジンとインストール状態を確認。エラー全文、OS、GPU、版、モデルと再現手順を[Issue](https://github.com/Tsubame-chan/YuiVRMAIStudio/issues)へ。APIキー・私的な会話は載せないでください。
- APIが失敗: アプリのキーとモデル利用権、通信・利用枠を確認。

English: download and extract the entire Windows app ZIP, keep the helper/Data files together, and confirm first-run data installation. Python is bundled. Local E2B/STT/VOICEVOX run through the bundled worker. API keys and charges apply to Direct API. See [HELP](HELP.md) for the English FAQ and [runtime support](RUNTIME_SUPPORT.md) for boundaries.
