# Windowsセットアップ / Windows setup

[FAQ](HELP.md) · [macOS](MAC_PUBLIC_BETA.md)

現在のWindows配布版は **v0.2.5-beta.2**（ベータ版）です。

## アプリを使う

1. [Release](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/tag/v0.2.5-beta.2)から `YuiVRMAIStudio_WindowsPublicBeta_v0.2.5-beta.2_windows.zip` を取得します。
2. フォルダ全体を展開して `Yui VRM AI Studio.exe` を起動します。`YuiFilePickerHelper.exe` とDataフォルダ等を移動・削除しないでください。
3. 初回案内を確認して、AI/音声とWindows runtimeを取得します。約2.5GBに加え展開用空き容量が必要です。通常Pythonを別に導入する必要はありません。
4. 端末内AI、または設定したOpenAI APIで会話します。高品質なAPI会話にはAPIキー・通信・API利用料金が必要です。

SmartScreenが出る場合は配布元を確認し、OSの案内で許可します。`.sha256` はZIPの破損確認用。Code ZIPはソースで実行アプリではありません。

標準の端末内AIはE2B、標準音声は日本語5声です。機種やGPU、空きメモリによって動作は変わります。

## アバター・設定

設定から `.vrm` を選びます。Unity/VRChatアバターは先にVRM化してください。「着替え」は人格と記憶を維持します。[導入ガイド](AVATAR_IMPORT.md)。性格はキャラクター設定、ローカルAIの上限・温度等は詳細設定から変更できます。案内はHelpから再表示できます。

## Backendを使う場合

初回取得した `YuiBackend` の `Start_Yui_Backend.bat` / `Stop_Yui_Backend.bat` を使います。端末内AIで会話するだけなら、このBackendを起動する必要はありません。Startで開く管理画面から、提供元・保存した声・端末同期を設定できます。[Backend Console](BACKEND_CONSOLE.md) / [追加TTS導入](BACKEND_TTS_GUIDE.md)。対象STT/TTSエンジンは別途導入・起動します。アプリのDirect APIキーとは別です。

ソースからのセットアップではPowerShellで:

```powershell
.\scripts\setup_backend_byok.ps1
notepad .env
.\scripts\start_local_services.ps1
# 停止
.\scripts\stop_local_services.ps1
```

前提ツール・実行ポリシーの案内はスクリプトを参照してください。別端末から接続する場合は、PCとモバイルの両方をTailscale等へ接続し、PCのVPNアドレスで待ち受ける設定が必要です。[接続先の設定](BACKEND_CONSOLE.md#初めて使うとき)。

## 困ったとき

- ファイル選択が出ない: `YuiFilePickerHelper.exe` が本体と同じフォルダか確認。
- データ取得が失敗: 接続・空き容量を確認し、同じReleaseのmanifest/データを使って再試行。
- ローカルAI/音声が失敗: 設定で選んだエンジンとインストール状態を確認。エラー全文、OS、GPU、版、モデルと再現手順を[Issue](https://github.com/Tsubame-chan/YuiVRMAIStudio/issues)へ。APIキー・私的な会話は載せないでください。
- APIが失敗: アプリのキーとモデル利用権、通信・利用枠を確認。

English: download and extract the entire Windows app ZIP, keep the helper/Data files together, and confirm first-run data installation. Python is bundled. Local E2B/STT/VOICEVOX run through the bundled worker. API keys and charges apply to Direct API. See [HELP](HELP.md) for the English FAQ and [runtime support](RUNTIME_SUPPORT.md) for boundaries.

## 既存Backendを更新する

アプリZIPの更新だけでは、保存領域の既存YuiBackendは置き換わりません。旧Backendが優先されるため、今回の音声認識・TTS修正にはBackendも更新してください。

1. アプリとBackend、Irodoriを終了し、使用中のYuiBackendフォルダーを非公開の場所へ丸ごとバックアップします。
2. 同じReleaseの `YuiVRMAIStudio_BackendBundle_v0.2.5-beta.2_windows.zip` を別の空フォルダーへ展開します。
3. `backend/.venv` は旧環境をバックアップへ移動してから、新版の環境全体へ置き換えます。内容を上書きマージすると古いパッケージ情報・モジュールが残ります。[共通更新手順](BACKEND_UPDATE.md)に従い、残りの配布ファイルを更新します。既存の `.env`、`backend/data`、導入済み `YuiIrodoriV4` とモデルは保持します。
4. WindowsのIrodori利用者は同じReleaseの新しいNVIDIA追加ZIPを別の場所へ展開し、そこからinstallerを使います。旧パッケージ内のハッシュ対象スクリプトだけを上書きしないでください。[手順](IRODORI_TTS_WINDOWS_NVIDIA.md)。
5. 再起動して音声認識・会話・保存した声を短文で確認します。不具合があれば終了し、バックアップへ戻してください。
