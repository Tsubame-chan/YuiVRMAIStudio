# macOSセットアップ

[English](MAC_PUBLIC_BETA.en.md) · [FAQ](HELP.md)

## アプリを使う

1. [v0.2.4-beta.1](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/tag/v0.2.4-beta.1)の `YuiVRMAIStudio_MacOSPublicBeta_v0.2.4-beta.1_macos.zip` をダウンロードします。
2. 展開した `Yui VRM AI Studio.app` を開きます。配布版のローカルruntimeはApple Silicon向けです。
3. 初回の案内を読んでダウンロードを開始します。標準E2B、日本語5声、辞書、Mac runtimeを取得します。合計約2.5GB、展開には追加の空き容量が必要です。
4. メッセージを送り、設定から性格・声・VRMを変更します。ヘルプからチュートリアルを再表示できます。

署名・notarizationは未整備です。macOSが起動を止める場合は、配布元を確認してからOSの「プライバシーとセキュリティ」の案内で許可してください。セキュリティ機能を全体で無効にする必要はありません。`.sha256` はZIPの破損確認用です。

通常はアプリZIPだけを取得すればよく、データの `.part-*` やBackend bundleを手動で展開する必要はありません。Code ZIPには実行アプリやweightが入っていません。

## 会話の方法

- 端末内AI: 初回取得後はオフライン利用可。E2Bが標準、設定からE4Bを任意取得できます。E4Bは容量・負荷・待ち時間が増えます。
- OpenAI API: 設定にAPIキーを入力します。通信とAPI利用料金が必要です。高品質な会話に推奨します。
- Backend: 任意の拡張経路。アプリのDirect APIキーとBackend `.env` のキーは別です。

AIと音声の選択は独立しています。標準は日本語VOICEVOXの5声。端末内AI/音声のworkerは初回取得した `YuiBackend` のruntimeを使いますが、HTTP Backendサーバーの起動は必要ありません。追加TTSの実験的adapterが存在しても、今回のmanifestにそのパックが入っているとは限りません。

## 自分のVRMを使う

設定のキャラクター項目から `.vrm` を選び、読み込み完了まで待ちます。マイキャラクターの「着替え」は人格・音声・記憶を保って外見を変えます。VRChat/Unityアバターは先にVRMへ書き出します。[アバター導入ガイド](AVATAR_IMPORT.md)。

## Backendを使う場合

初回取得した `YuiBackend` 内の `Start_Yui_Backend.command` / `Stop_Yui_Backend.command` で起動・停止できます。OpenAIや追加TTS/STTはBackend側の設定も必要です。遠隔PCへ接続する際は、双方から到達できるVPNアドレスとBackendのlisten設定を使います。Macの起動スクリプトは既定で `BACKEND_HOST=127.0.0.1`、`BACKEND_PORT=8000` です。VPN側へlistenさせる場合はそのPCのVPN IPを `BACKEND_HOST` に指定し、アプリには `http://<VPN IP>:8000` を設定します。VPNだけで、localhostに限定したサーバーへ他端末から接続できるわけではありません。

ソースから準備する場合:

```bash
PYTHON_BIN=/opt/homebrew/bin/python3.12 ./scripts/setup_backend_byok_macos.sh
open -e .env
./scripts/start_local_services_macos.sh
# 終了時
./scripts/stop_local_services_macos.sh
```

Python 3.12などの前提はセットアップスクリプトの案内を参照してください。Backend `.env` にAPIキーを保存してもアプリのDirect APIキーを設定したことにはなりません。Backend VOICEVOX等を選ぶ場合は対象エンジンも必要です。

## ソースをビルドする場合

Unity **2022.3.62f3** / UniVRM **0.131.2**。[データ復元](LOCAL_AI_ASSETS.md)とOS別SDK/runtimeを準備してください。公開ビルドでは[アセット監査](PUBLIC_PLAYER_ASSET_VALIDATION.md)が必要です。

[OS別対応](RUNTIME_SUPPORT.md) · [ソースの現在地](SOURCE_STATUS.md) · [API仕様](api.md)
