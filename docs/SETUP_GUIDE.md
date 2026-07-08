# Windows Desktop Public Beta セットアップ

このページはWindows版を試す人向けの入口です。まずはGitHub ReleasesのWindows Beta配布物を使ってください。GitHubの `Code > Download ZIP` はソースコード用で、実行ファイルや大型AI/TTSデータを含まないため、そのままでは完成アプリとして動きません。

現在のWindows実行用ZIPは `v0.2.0-beta.3` です。この版では、初回起動時に不足しているLocal AI/TTSデータとWindows Backend bundleをアプリが自動で取得します。

macOS版は [`MAC_PUBLIC_BETA.md`](MAC_PUBLIC_BETA.md) を見てください。

## まず動かす

1. GitHub Releasesの `v0.2.0-beta.3` で、名前に `WindowsPublicBeta` が入っているアプリ本体ZIPをダウンロードします。
   - `YuiVRMAIStudio_WindowsPublicBeta_v0.2.0-beta.3_windows.zip`
2. ZIPを展開します。
3. `Yui VRM AI Studio.exe` を起動します。
4. 初回ダウンロード画面で開始すると、基本動作に必要なLocal AI/TTSデータとWindows Backend bundleが自動で入ります。

WindowsのSmartScreenが表示された場合は、信頼できる配布物であることを確認してから `詳細情報` -> `実行` を選びます。

`YuiFilePickerHelper.exe` は画像やVRMファイルを選ぶための補助ツールで、アプリZIPに同梱されています。展開後にファイル構成を崩さず、そのまま使ってください。

`.sha256` はダウンロード破損を確認したい場合だけ使います。通常はアプリ本体ZIPだけで始められます。

`MacOSPublicBeta` はmacOS用、`YuiVRMAIStudio_LocalAIAssets_DesktopMinimum` / `YuiVRMAIStudio_BackendBundle` / `LocalAIAssets_Minimum` は通常アプリ内の初回ダウンローダーが取得します。Windowsアプリを試すだけなら手動ダウンロード不要です。

## ダウンロードの違い

| 入手方法 | 用途 |
| --- | --- |
| ReleaseのWindowsアプリZIP | すぐ使う人向け。通常はこれだけ落として展開します。 |
| `.sha256` | ダウンロード破損を確認したい人向けです。 |
| `YuiVRMAIStudio_LocalAIAssets_DesktopMinimum` | 初回起動時にアプリが取得する最小ローカルAI/TTSデータです。手動取得は通常不要です。 |
| `YuiVRMAIStudio_BackendBundle_*_windows` | 初回起動時にアプリが取得するWindows Backend bundleです。portable Python runtimeを含むため、通常ユーザーがPythonを別途入れる必要はありません。 |
| `Code > Download ZIP` | ソースを読む/改造する人向け。実行ファイルや大型モデルは含みません。 |
| Optional voice / 外部runtime | AivisSpeech HDやIrodori TTSなど、声の選択肢を増やすための任意追加です。対応パックはSettingsの `Additional Voices` から取得します。 |

## できることの目安

- バックエンドなし: Local Gemma SLM、Local VOICEVOX、VRM表示、基本チャット。
- OpenAI APIキーあり: Direct OpenAI API、より高品質な会話/画像理解/STT。
- バックエンドあり: リアルタイム会話、リアルタイム翻訳、会話DB、Backend VOICEVOX、AivisSpeech HD、Irodori TTS。

初回は `Auto Select` のままで大丈夫です。バックエンドが動いていればBackendを優先し、なければLocal/Directへ戻ります。

## バックエンドを使う場合

Release版でフル機能を使う場合、通常は初回ダウンローダーが取得したWindows Backend bundleをアプリが自動起動します。

手動で起動・停止したい場合は、ユーザーデータ領域に展開された `YuiBackend` 内の `Start_Yui_Backend.bat` / `Stop_Yui_Backend.bat` を使います。

必要なもの:

- 初回ダウンロード済みの `YuiBackend`
- OpenAI APIキー
- VOICEVOX Engine、AivisSpeech HD、Irodori TTSなど、使いたい外部TTS runtime

PythonはWindows Backend bundleに同梱されています。以下のPython導入と `setup_backend_byok.ps1` は、ソースから起動する場合や、bundle内の `backend\.venv\Scripts\python.exe` が欠けている場合だけ必要です。

```text
https://www.python.org/downloads/windows/
```

ソースから起動する場合は、PowerShellでリポジトリフォルダを開き、初期化します。

```powershell
.\scripts\setup_backend_byok.ps1
```

スクリプト実行がブロックされる場合は、一度だけ以下を実行します。

```powershell
Set-ExecutionPolicy RemoteSigned -Scope CurrentUser
```

`.env` を開き、OpenAI APIキーを入れます。

```powershell
notepad .env
```

```env
OPENAI_API_KEY=sk-...
```

## バックエンドの起動と停止

起動:

```text
YuiBackend\Start_Yui_Backend.bat
```

このウィンドウはアプリ使用中は開いたままにします。終了するときは、起動ウィンドウでEnterを押します。

うまく止まらない場合:

```text
YuiBackend\Stop_Yui_Backend.bat
```

古いバックエンドが残っていると、別のコピーの会話履歴や設定を見てしまうことがあります。新しく展開したZIPを試す前に、必要ならStop BATで止めてから起動し直してください。

## VOICEVOX Engine

Release ZIP内のLocal VOICEVOXだけでも最低限の日本語音声は使えます。Backend VOICEVOXで細かく調整したい場合や、自分のVOICEVOX環境を使いたい場合はVOICEVOX Engineを追加します。

通常のVOICEVOXアプリにEngineが含まれています。

```text
https://voicevox.hiroshiba.jp/
```

起動スクリプトは主に以下を探します。

```text
%LOCALAPPDATA%\Programs\VOICEVOX\vv-engine\run.exe
%ProgramFiles%\VOICEVOX\vv-engine\run.exe
```

別の場所にある場合は `VOICEVOX_ENGINE_EXE` に `vv-engine\run.exe` のフルパスを指定してください。

## 自分のVRMを使う

読み込めるのは `.vrm` ファイルです。VRChat SDKアバター、Unity prefab、Unityシーン、`.unitypackage`、VRChatにアップロード済みのアバターそのものは直接読み込めません。

アプリ内でSettingsを開き、Custom VRMから `.vrm` を選びます。読み込みに成功すると、その場でアバターが切り替わります。

## よくあるトラブル

チャットが反応しない:

- `Auto Select` のまま再度試してください。
- API機能を使う場合は `.env` の `OPENAI_API_KEY` を確認してください。
- バックエンド機能を使う場合は、アプリが自動起動したYui Backend、または `YuiBackend\Start_Yui_Backend.bat` が起動中か確認してください。

音声が出ない:

- Voice EngineをLocal VOICEVOXにして試してください。
- Backend VOICEVOX/Aivis/Irodoriを使う場合は、バックエンドと対象runtimeが起動しているか確認してください。

ファイル選択が開かない:

- `YuiFilePickerHelper.exe` を `Yui VRM AI Studio.exe` と同じフォルダに置いてください。

Release ZIPではなくCode ZIPを落としてしまった:

- それはソースコードです。すぐ使う場合はGitHub ReleasesのWindows Beta配布物を落としてください。

## ソースからビルドする場合

Releaseの `YuiVRMAIStudio_LocalAIAssets_DesktopMinimum` または旧 `LocalAIAssets_Minimum` をリポジトリ直下へ展開してからUnityで開きます。詳しくは [`LOCAL_AI_ASSETS.md`](LOCAL_AI_ASSETS.md) を参照してください。
