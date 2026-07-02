# macOS Desktop Public Beta セットアップ

このページはmacOS版を試す人向けの入口です。まずはGitHub ReleasesのmacOS Beta配布物を使ってください。GitHubの `Code > Download ZIP` はソースコード用で、実行済みアプリや大型AI/TTSデータは含まれません。

- English guide: [`MAC_PUBLIC_BETA.en.md`](MAC_PUBLIC_BETA.en.md)

## まず動かす

1. GitHub Releasesで、名前に `MacOSPublicBeta` が入っている次の3ファイルだけを同じフォルダへダウンロードします。
   - `YuiVRMAIStudio_MacOSPublicBeta_..._macos.zip.part-000`
   - `YuiVRMAIStudio_MacOSPublicBeta_..._macos.zip.part-001`
   - `YuiVRMAIStudio_MacOSPublicBeta_..._macos.zip.sha256`
2. Terminalでそのフォルダを開き、ZIPを結合してsha256を確認します。
3. ZIPを展開し、`Yui VRM AI Studio.app` を起動します。

```bash
cat YuiVRMAIStudio_MacOSPublicBeta_v0.2.0-beta.1_macos.zip.part-* > YuiVRMAIStudio_MacOSPublicBeta_v0.2.0-beta.1_macos.zip
shasum -a 256 -c YuiVRMAIStudio_MacOSPublicBeta_v0.2.0-beta.1_macos.zip.sha256
```

このBetaはまだ署名・notarizationの整備前です。macOSで警告が出た場合は、信頼できる配布物であることを確認してからシステム設定または右クリックメニューから許可してください。

Releaseアプリには、最小構成のLocal Gemma SLMとLocal VOICEVOXを含めています。追加データなしでも、まずテキスト会話と日本語音声応答を試せます。

`WindowsPublicBeta` はWindows用、`LocalAIAssets_Minimum` はソースからUnityでビルドする人向けです。macOSアプリを試すだけならダウンロード不要です。

## ダウンロードの違い

| 入手方法 | 用途 |
| --- | --- |
| ReleaseのmacOS ZIP / `.part-*` | すぐ使う人向け。`.app` と最小ローカルAI/TTSを含みます。分割されている場合は結合してから展開します。 |
| `Code > Download ZIP` | ソースを読む/改造する人向け。`.app` や大型モデルは含みません。 |
| `LocalAIAssets_Minimum` | ソースからUnityビルドする人向けの最小asset packです。 |
| Optional voice / 外部runtime | AivisSpeech HDやIrodori TTSなど、声の選択肢を増やすための任意追加です。 |

## できることの目安

- バックエンドなし: Local Gemma SLM、Local VOICEVOX、VRM表示、基本チャット。
- OpenAI APIキーあり: Direct OpenAI API、より高品質な会話/画像理解/STT。
- バックエンドあり: リアルタイム会話、リアルタイム翻訳、会話DB、Backend VOICEVOX、AivisSpeech HD、Irodori TTS。

初回は `Auto Select` のままで大丈夫です。バックエンドが動いていればBackendを優先し、なければLocal/Directへ戻ります。

## バックエンドを使う場合

フル機能を使いたい場合だけ、以下をセットアップします。

バックエンドの起動スクリプトはアプリZIPではなく、このリポジトリのソース側に入っています。Releaseアプリだけを試す場合、この章は飛ばして構いません。

必要なもの:

- Apple Silicon Mac
- Homebrew
- Python 3.12+
- OpenAI APIキー
- VOICEVOX Engine、AivisSpeech HD、Irodori TTSなど、使いたい外部TTS runtime

HomebrewとPythonを用意します。

```bash
brew install python@3.12 git git-lfs
git lfs install
```

ローカルサービスを初期化します。

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

起動:

```bash
./scripts/start_local_services_macos.sh
```

Finderから起動する場合:

```text
Start_Yui_Local_Services.command
```

停止:

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

Releaseの `LocalAIAssets_Minimum` をリポジトリ直下へ展開してからUnityで開きます。詳しくは [`LOCAL_AI_ASSETS.md`](LOCAL_AI_ASSETS.md) を参照してください。

開発・ビルド検証ではUnity `2022.3.62f3` を使っています。

## 関連ドキュメント

- Main README: [`../README.md`](../README.md)
- English README: [`../README.en.md`](../README.en.md)
- API: [`api.md`](api.md)
- 外部情報 / web search方針: [`LLM_EXTERNAL_INFO.md`](LLM_EXTERNAL_INFO.md)
