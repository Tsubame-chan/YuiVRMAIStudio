# macOS Desktop Public Beta セットアップ

このページはmacOS版を試す人向けの入口です。まずはGitHub ReleasesのmacOS Beta配布物を使ってください。GitHubの `Code > Download ZIP` はソースコード用で、実行済みアプリや大型AI/TTSデータは含まれません。

現在のmacOS実行用ZIPは `v0.2.0-beta.3` です。この版では、初回起動時に不足しているLocal AI/TTSデータとmacOS Backend bundleをアプリが自動で取得します。

- English guide: [`MAC_PUBLIC_BETA.en.md`](MAC_PUBLIC_BETA.en.md)

## まず動かす

1. GitHub Releasesの `v0.2.0-beta.3` で、名前に `MacOSPublicBeta` が入っているアプリ本体ZIPをダウンロードします。
   - `YuiVRMAIStudio_MacOSPublicBeta_v0.2.0-beta.3_macos.zip`
2. ZIPを展開し、`Yui VRM AI Studio.app` を起動します。
3. 初回ダウンロード画面で開始すると、基本動作に必要なLocal AI/TTSデータとmacOS Backend bundleが自動で入ります。

このBetaはまだ署名・notarizationの整備前です。macOSで警告が出た場合は、信頼できる配布物であることを確認してからシステム設定または右クリックメニューから許可してください。

`.sha256` はダウンロード破損を確認したい場合だけ使います。通常はアプリ本体ZIPだけで始められます。AivisSpeech HDなどの追加音声は、Settingsの `Additional Voices` から必要な人だけ取得します。

`WindowsPublicBeta` はWindows用、`YuiVRMAIStudio_LocalAIAssets_DesktopMinimum` / `LocalAIAssets_Minimum` は通常アプリ内の初回ダウンローダーが取得します。macOSアプリを試すだけなら手動ダウンロード不要です。

## ダウンロードの違い

| 入手方法 | 用途 |
| --- | --- |
| ReleaseのmacOSアプリZIP | すぐ使う人向け。通常はこれだけ落として展開します。 |
| `.sha256` | ダウンロード破損を確認したい人向けです。 |
| `Code > Download ZIP` | ソースを読む/改造する人向け。`.app` や大型モデルは含みません。 |
| `YuiVRMAIStudio_LocalAIAssets_DesktopMinimum` / `LocalAIAssets_Minimum` | 初回起動時にアプリが取得する最小ローカルAI/TTSデータです。手動取得は通常不要です。 |
| `YuiVRMAIStudio_BackendBundle_*_macos` | 初回起動時にアプリが取得するmacOS Backend bundleです。手動取得は通常不要です。 |
| Optional voice / 外部runtime | AivisSpeech HDやIrodori TTSなど、声の選択肢を増やすための任意追加です。対応パックはSettingsの `Additional Voices` から取得します。 |

## できることの目安

- バックエンドなし: Local Gemma SLM、Local VOICEVOX、VRM表示、基本チャット。
- OpenAI APIキーあり: Direct OpenAI API、より高品質な会話/画像理解/STT。
- バックエンドあり: リアルタイム会話、リアルタイム翻訳、会話DB、Backend VOICEVOX、AivisSpeech HD、Irodori TTS。

初回は `Auto Select` のままで大丈夫です。バックエンドが動いていればBackendを優先し、なければLocal/Directへ戻ります。

## バックエンドを使う場合

フル機能を使いたい場合、通常は初回ダウンローダーが取得したYui Backend bundleをアプリが自動起動します。手動で起動・停止したい場合は、ユーザーデータ領域に展開された `YuiBackend` 内のコマンドを使います。

必要なもの:

- Apple Silicon Mac
- 初回ダウンロード済みの `YuiBackend`
- OpenAI APIキー
- VOICEVOX Engine、AivisSpeech HD、Irodori TTSなど、使いたい外部TTS runtime

macOS Backend bundleには実行用 `.venv` が同梱されています。ソースから起動する場合やBackend bundleにvenvがない場合だけ、HomebrewとPythonを用意します。

```bash
brew install python@3.12 git git-lfs
git lfs install
```

ソース版ではローカルサービスを初期化します。

```bash
PYTHON_BIN=/opt/homebrew/bin/python3.12 ./scripts/setup_backend_byok_macos.sh
```

`.env` を開き、OpenAI APIキーを入れます。

```bash
open -e .env
```

```env
OPENAI_API_KEY=sk-...
```

## バックエンドの起動と停止

初回ダウンロード済みBackend bundleを手動起動する場合:

```text
YuiBackend/Start_Yui_Backend.command
```

停止:

```text
YuiBackend/Stop_Yui_Backend.command
```

ソース版の起動:

```bash
./scripts/start_local_services_macos.sh
```

Finderから起動する場合:

```text
Start_Yui_Local_Services.command
```

ソース版の停止:

```bash
./scripts/stop_local_services_macos.sh
```

または:

```text
Stop_Yui_Local_Services.command
```

## VOICEVOX

Release ZIP内のLocal VOICEVOXだけでも最低限の日本語音声は使えます。Backend VOICEVOXで細かく調整したい場合や、自分のVOICEVOX環境を使いたい場合はVOICEVOX Engineを追加します。

macOSの起動スクリプトは主に以下を探します。

```text
/Applications/VOICEVOX.app/Contents/Resources/vv-engine/run
~/Applications/VOICEVOX.app/Contents/Resources/vv-engine/run
```

別の場所にある場合は `VOICEVOX_ENGINE_EXE` を指定してください。

```bash
export VOICEVOX_ENGINE_EXE="/path/to/VOICEVOX.app/Contents/Resources/vv-engine/run"
```

## 自分のVRMを使う

読み込めるのは `.vrm` ファイルです。VRChat SDKアバター、Unity prefab、Unityシーン、`.unitypackage`、VRChatにアップロード済みのアバターそのものは直接読み込めません。

アプリ内でSettingsを開き、Custom VRMから `.vrm` を選びます。読み込みに成功すると、その場でアバターが切り替わります。

## ソースからビルドする場合

Releaseの `YuiVRMAIStudio_LocalAIAssets_DesktopMinimum` または旧 `LocalAIAssets_Minimum` をリポジトリ直下へ展開してからUnityで開きます。詳しくは [`LOCAL_AI_ASSETS.md`](LOCAL_AI_ASSETS.md) を参照してください。

開発・ビルド検証ではUnity `2022.3.62f3` を使っています。

## 関連ドキュメント

- Main README: [`../README.md`](../README.md)
- English README: [`../README.en.md`](../README.en.md)
- API: [`api.md`](api.md)
- 外部情報 / web search方針: [`LLM_EXTERNAL_INFO.md`](LLM_EXTERNAL_INFO.md)
