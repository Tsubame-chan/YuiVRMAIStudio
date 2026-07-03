# Windows Desktop Public Beta セットアップ

このページはWindows版を試す人向けの入口です。まずはGitHub ReleasesのWindows Beta配布物を使ってください。GitHubの `Code > Download ZIP` はソースコード用で、実行ファイルや大型AI/TTSデータを含まないため、そのままでは完成アプリとして動きません。

現在のWindows実行用ZIPは `v0.2.0-beta.3` です。この版には初回ダウンローダーも含まれており、不足しているLocal AI/TTSデータがあればGitHub Releasesのmanifestから取得します。

macOS版は [`MAC_PUBLIC_BETA.md`](MAC_PUBLIC_BETA.md) を見てください。

## まず動かす

1. GitHub Releasesの `v0.2.0-beta.3` で、名前に `WindowsPublicBeta` が入っている次のファイルを同じフォルダへダウンロードします。
   - `YuiVRMAIStudio_WindowsPublicBeta_v0.2.0-beta.3_windows.zip`
   - `YuiVRMAIStudio_WindowsPublicBeta_v0.2.0-beta.3_windows.zip.sha256`
2. PowerShellでそのフォルダを開き、sha256を確認します。
3. 作成されたZIPを展開します。
4. `Yui VRM AI Studio.exe` を起動します。

```powershell
$expected = (Get-Content .\YuiVRMAIStudio_WindowsPublicBeta_v0.2.0-beta.3_windows.zip.sha256).Split()[0]
$actual = (Get-FileHash .\YuiVRMAIStudio_WindowsPublicBeta_v0.2.0-beta.3_windows.zip -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actual -ne $expected) { throw "SHA256 mismatch" }
```

WindowsのSmartScreenが表示された場合は、信頼できる配布物であることを確認してから `詳細情報` -> `実行` を選びます。

同じフォルダに `YuiFilePickerHelper.exe` が必要です。これは画像やVRMファイルを選ぶための補助ツールです。

初回起動時にLocal AI/TTSデータが不足している場合は、アプリ内のダウンローダーがGitHub Releasesから必要なデータを取得します。

`MacOSPublicBeta` はmacOS用、`YuiVRMAIStudio_LocalAIAssets_DesktopMinimum` / `LocalAIAssets_Minimum` はソースからUnityでビルドする人向けです。Windowsアプリを試すだけならダウンロード不要です。

## ダウンロードの違い

| 入手方法 | 用途 |
| --- | --- |
| ReleaseのWindows ZIP / `.part-*` | すぐ使う人向け。実行ファイルと最小ローカルAI/TTSを含みます。分割されている場合は結合してから展開します。 |
| `Code > Download ZIP` | ソースを読む/改造する人向け。実行ファイルや大型モデルは含みません。 |
| `YuiVRMAIStudio_LocalAIAssets_DesktopMinimum` / `LocalAIAssets_Minimum` | ソースからUnityビルドする人向け、または初回ダウンローダー検証用の最小asset packです。 |
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

- Python 3.12+
- OpenAI APIキー
- VOICEVOX Engine、AivisSpeech HD、Irodori TTSなど、使いたい外部TTS runtime

Pythonは公式サイトからインストールし、`Add python.exe to PATH` を有効にしてください。

```text
https://www.python.org/downloads/windows/
```

PowerShellでリポジトリフォルダを開き、初期化します。

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
Start_Yui_Backend_And_VOICEVOX.bat
```

このウィンドウはアプリ使用中は開いたままにします。終了するときは、起動ウィンドウでEnterを押します。

うまく止まらない場合:

```text
Stop_Yui_Backend_And_VOICEVOX.bat
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
- バックエンド機能を使う場合は `Start_Yui_Backend_And_VOICEVOX.bat` が起動中か確認してください。

音声が出ない:

- Voice EngineをLocal VOICEVOXにして試してください。
- Backend VOICEVOX/Aivis/Irodoriを使う場合は、バックエンドと対象runtimeが起動しているか確認してください。

ファイル選択が開かない:

- `YuiFilePickerHelper.exe` を `Yui VRM AI Studio.exe` と同じフォルダに置いてください。

Release ZIPではなくCode ZIPを落としてしまった:

- それはソースコードです。すぐ使う場合はGitHub ReleasesのWindows Beta配布物を落としてください。

## ソースからビルドする場合

Releaseの `YuiVRMAIStudio_LocalAIAssets_DesktopMinimum` または旧 `LocalAIAssets_Minimum` をリポジトリ直下へ展開してからUnityで開きます。詳しくは [`LOCAL_AI_ASSETS.md`](LOCAL_AI_ASSETS.md) を参照してください。
