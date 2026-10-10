# macOSセットアップ

[English](MAC_PUBLIC_BETA.en.md) · [FAQ](HELP.md)

## アプリを使う

1. [v0.2.5-beta.2](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/tag/v0.2.5-beta.2)の `YuiVRMAIStudio_MacOSPublicBeta_v0.2.5-beta.2_macos.zip` をダウンロードします。
2. 展開した `Yui VRM AI Studio.app` を開きます。配布版のローカルruntimeはApple Silicon向けです。
3. 初回の案内を読んでダウンロードを開始します。標準E2B、日本語5声、辞書、Mac runtimeを取得します。合計約2.5GB、展開には追加の空き容量が必要です。
4. メッセージを送り、設定から性格・声・VRMを変更します。ヘルプからチュートリアルを再表示できます。

署名・notarizationは未整備です。macOSが起動を止める場合は、配布元を確認してからOSの「プライバシーとセキュリティ」の案内で許可してください。セキュリティ機能を全体で無効にする必要はありません。`.sha256` はZIPの破損確認用です。

通常はアプリZIPだけを取得すればよく、データの `.part-*` やBackend bundleを手動で展開する必要はありません。Code ZIPには実行アプリやweightが入っていません。

## 会話の方法

- 端末内AI: 初回取得後はオフライン利用可。E2Bが標準、設定からE4Bを任意取得できます。E4Bは容量・負荷・待ち時間が増えます。
- OpenAI API: 設定にAPIキーを入力します。通信とAPI利用料金が必要です。高品質な会話に推奨します。
- Backend: 任意の拡張経路。アプリのDirect APIキーとBackend `.env` のキーは別です。

AIと音声の選択は独立しています。標準は日本語VOICEVOXの5声です。端末内AIを使うだけならBackendの起動は不要です。追加音声には別途導入が必要です。

## 自分のVRMを使う

設定のキャラクター項目から `.vrm` を選び、読み込み完了まで待ちます。マイキャラクターの「着替え」は人格・音声・記憶を保って外見を変えます。VRChat/Unityアバターは先にVRMへ書き出します。[アバター導入ガイド](AVATAR_IMPORT.md)。

## Backendを使う場合

初回取得した `YuiBackend` 内の `Start_Yui_Backend.command` / `Stop_Yui_Backend.command` で起動・停止できます。Startで開く管理画面から提供元・保存した声・端末同期を設定できます。[Backend Console](BACKEND_CONSOLE.md) / [追加TTS導入](BACKEND_TTS_GUIDE.md)。OpenAIや追加TTS/STTはBackend側の設定も必要です。別端末から使う場合は、両方の端末をTailscale等へ接続し、アプリにPCのVPNアドレスとポートを指定します。例: `http://100.x.x.x:8000`。新しいBackendでは `scripts/start_mobile_backend_macos.sh` で起動すると、Tailscaleのアドレスを取得し、PC内とVPNからの接続を受け付けます。PCをスリープさせないでください。

以前のv0.2.4-beta.2のBackendにはこの起動ファイルがありません。VPNに接続しただけでは、PC内だけで待ち受けるBackendへ別端末から到達できません。

ソースから準備する場合:

```bash
PYTHON_BIN=/opt/homebrew/bin/python3.12 ./scripts/setup_backend_byok_macos.sh
open -e .env
./scripts/start_local_services_macos.sh
# 終了時
./scripts/stop_local_services_macos.sh
```

Python 3.12などの前提はセットアップスクリプトの案内を参照してください。Backend `.env` にAPIキーを保存してもアプリのDirect APIキーを設定したことにはなりません。Backend VOICEVOX等を選ぶ場合は対象エンジンも必要です。


[端末ごとの対応 / Compatibility](RUNTIME_SUPPORT.md)

## 既存Backendを更新する

アプリZIPの更新だけでは、保存領域の既存YuiBackendは置き換わりません。旧Backendが優先されるため、今回の音声認識・TTS修正にはBackendも更新してください。

1. アプリとBackend、Irodoriを終了し、使用中のYuiBackendフォルダーを非公開の場所へ丸ごとバックアップします。
2. 同じReleaseの `YuiVRMAIStudio_BackendBundle_v0.2.5-beta.2_macos.zip` を別の空フォルダーへ展開します。
3. `backend/.venv` は旧環境をバックアップへ移動してから、新版の環境全体へ置き換えます。内容を上書きマージすると古いパッケージ情報・モジュールが残ります。[共通更新手順](BACKEND_UPDATE.md)に従い、リンクと実行権限を保持して残りの配布ファイルを更新します。既存の `.env`、`backend/data`、追加エンジンとモデルは保持します。
4. 追加TTSの更新が必要な場合は、その提供元・OS向けの導入手順に従います。MacでWindows NVIDIA追加ZIPを使用しません。
5. 再起動して音声認識・会話・保存した声を短文で確認します。不具合があれば終了し、バックアップへ戻してください。
