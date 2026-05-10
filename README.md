# Yui VRM AI Studio

[English README](README.en.md)

**あなたのお気に入りのVRMキャラクターを、話し、見て、覚えて、翻訳するAIエージェントへ。**

Yui VRM AI Studioは、Unity製のVRMアバター表示アプリとローカルPythonバックエンドを組み合わせた、Windows向けのローカルAIアバタースタジオです。

UnityChanまたは自分の `.vrm` モデルを表示しながら、テキスト会話、音声入力、VOICEVOX Engineによる日本語音声再生、画像入力、画面コンテキスト、会話メモリ、リアルタイム会話モードを試せます。

このalpha版はBYOK方式です。利用者が自分のOpenAI APIキーを用意し、バックエンドを自分のPC上で起動します。

## 30秒でわかること

- お気に入りのVRMキャラクターを読み込んで、AIエージェントとして会話できます。
- テキスト・音声・画像・画面コンテキストを使ってやり取りできます。
- 日本語音声はVOICEVOX Engineで再生します。
- OpenAI APIキーを自分で用意するBYOK方式です。
- まだalpha版なので、セットアップや一部機能は実験的です。

## 現在できること

- UnityChan Default avatarの表示
- VRM 1.0 / VRM 0.x の読み込み
- テキストチャット
- キャラクター名・性格・口調・プロンプト設定
- OpenAIによる音声入力
- VOICEVOX Engineによる日本語読み上げ
- 画像入力・画面コンテキスト
- 会話履歴・メモリ
- `.env` によるローカルBYOK backend

## 実験機能

- OpenAI Realtime APIによる低遅延音声会話モード
- OpenAI Realtimeのテキスト出力をVOICEVOXに渡す日本語キャラ声会話モード
- リアルタイム翻訳モード

## Provider Status

### 現在の主なprovider

- OpenAI: chat / STT / vision / realtime / translation
- VOICEVOX Engine: local Japanese TTS

### 実装済み・検証中

- Gemini Vision provider はbackend上に実装済みですが、このalpha版では十分に検証できていません。

### 今後対応予定

- Grok / xAI API
- Ollama / LM Studio
- provider選択UI

## 現在のalpha版

- Version: `0.1.0-alpha.1`
- Platform: Windows 10/11
- Bundled avatar: UnityChan Default
- Custom avatar support: local VRM 1.0 and VRM 0.x `.vrm` import
- Speech: local VOICEVOX Engine, installed separately
- Backend: FastAPI on `127.0.0.1:8000`

Windowsアプリ本体:

```text
builds\YuiVRMAIStudio_PublicAlpha_v0.1.0-alpha.1\Yui VRM AI Studio.exe
```

ファイル選択用ヘルパーも同じフォルダに置いてください:

```text
builds\YuiVRMAIStudio_PublicAlpha_v0.1.0-alpha.1\YuiFilePickerHelper.exe
```

## 必要なもの

- Windows 10 または Windows 11
- Python 3.12+
  - https://www.python.org/downloads/windows/
- OpenAI APIキー
  - https://platform.openai.com/api-keys
- VOICEVOX Engine / VOICEVOXに含まれる `vv-engine\run.exe`
  - https://voicevox.hiroshiba.jp/
  - Engine単体: https://github.com/VOICEVOX/voicevox_engine/releases
- PowerShell

API利用量は、実験的なリアルタイム機能を常時使わなければかなり軽めです。通常のチャット、音声入力、画像入力、翻訳などを機能確認として試す程度なら、5ドル分のAPIクレジットでも十分遊べます。長時間のリアルタイム会話、画像や音声の大量利用では費用が増えるため、OpenAIのUsageページで確認しながら使ってください。

## Quick Start

1. GitHubからこのリポジトリを取得します。
   - `Code` -> `Download ZIP` でZIPをダウンロードして展開します。
   - Gitを使う場合は `git clone <repository-url>` でもOKです。
2. 展開したフォルダを、できればシンプルな場所に置きます。

```text
C:\YuiVRMAIStudio
```

3. Python 3.12+ をインストールします。インストール時に `Add python.exe to PATH` を有効にしてください。
4. VOICEVOX Engineを用意します。
   - 通常のVOICEVOXアプリをインストールした場合でも、内部の `vv-engine\run.exe` を使います。
   - Engine単体を使う場合は、VOICEVOX EngineのReleasesからWindows版を取得してください。
5. リポジトリのフォルダでPowerShellを開き、バックエンドの初期セットアップを実行します。

```powershell
.\scripts\setup_backend_byok.ps1
```

PowerShellでスクリプト実行が無効と言われた場合は、一度だけこれを実行してから、もう一度セットアップしてください。

```powershell
Set-ExecutionPolicy RemoteSigned -Scope CurrentUser
```

6. `.env` を開き、OpenAI APIキーを設定します。

```text
OPENAI_API_KEY=your_api_key_here
```

`.env` が開きにくい場合は、リポジトリのフォルダで以下を実行するとメモ帳で開けます。

```powershell
notepad .env
```

7. 次のバッチファイルをダブルクリックします。

```text
Start_Yui_Backend_And_VOICEVOX.bat
```

このファイル1つで、ローカルバックエンドとVOICEVOX Engineの起動をまとめて行います。VOICEVOXのGUIを操作するのではなく、`vv-engine\run.exe` を直接起動して `http://127.0.0.1:50021` のローカルAPIサーバーとして使います。アプリを使っている間は、この起動ウィンドウを開いたままにしてください。

終了するときは、その起動ウィンドウで Enter を押してください。通常はこれだけでバックエンドとVOICEVOX Engineを終了できます。

`Stop_Yui_Backend_And_VOICEVOX.bat` は、起動ウィンドウを閉じてしまった場合や、プロセスが残って終了できない場合の強制終了用です。普段は使わなくて大丈夫です。

8. アプリを起動します。

```text
builds\YuiVRMAIStudio_PublicAlpha_v0.1.0-alpha.1\Yui VRM AI Studio.exe
```

WindowsがSmartScreen警告を出すことがあります。署名なしのalpha版exeなので、信頼して実行する場合は `詳細情報` -> `実行` を選んでください。VOICEVOX起動時にUACが出る場合もあります。

## 自分のVRMキャラクターを使う

このalpha版が直接読み込めるのは `.vrm` ファイルです。VRChat SDKのアバター、Unityシーン、Unity prefab、`.unitypackage`、VRChatにアップロード済みのアバターそのものは直接読み込めません。

VRChat用のUnityプロジェクトで管理しているアバターを使いたい場合は、元のBOOTH/配布パッケージに `.vrm` が含まれていないか確認してください。ない場合は、Unity/UniVRMやBlender/VRMのワークフローで別途VRMとして書き出してから読み込んでください。

手順:

1. `Start_Yui_Backend_And_VOICEVOX.bat` を起動します。
2. `Yui VRM AI Studio.exe` を起動します。
3. Settingsを開きます。
4. `Custom VRM` のインポートボタンを押します。
5. 自分の `.vrm` ファイルを選びます。

読み込みに成功すると、アプリはすぐに `Custom VRM` へ切り替え、選択したパスをローカルに保存します。次回起動時にも同じVRMを復元しようとします。

高度なマテリアル、表情、特殊なリグは、このalpha版では完全に再現されない場合があります。VRChat固有のExpression Menu、FX Controller、PhysBone、Contact、Constraint、Avatar DescriptorなどはVRChat用のデータとして扱ってください。

## Privacy / Data Flow

Yui VRM AI StudioはBYOK方式です。APIキーはユーザー自身のPC上の `.env` に保存されます。

有効にした機能によって、以下の情報が設定済みの外部AI providerへ送信される可能性があります。

- チャット本文
- 音声入力
- アップロード画像
- スクリーンショット / 画面コンテキスト
- 翻訳対象の音声・テキスト

以下はローカルに保存されます。

- `.env`
- SQLiteの会話DB
- VOICEVOX生成音声キャッシュ
- ログ

画面コンテキストやリアルタイム翻訳を使う場合は、画面上・音声経路上の機密情報に注意してください。

## Roadmap

### Public Alpha 0.1

- VRM表示・制御
- ローカルFastAPI backend
- OpenAI chat / STT / vision
- VOICEVOX TTS
- 会話履歴・メモリ
- 画像/画面コンテキスト
- Realtime系の実験機能

### Next

- 外部アプリ音声ブリッジ
- YouTube / ゲーム / 配信 / 通話音声のリアルタイム翻訳
- provider選択UI
- Gemini Vision providerの検証
- Grok / xAI API provider
- Ollama / LM Studio local LLM provider

### Future

- モバイルアプリ展開
- フィジカルAI / 外部デバイス連携
- より豊かなUnityシーン、アニメーション、インタラクション

## Yui Physical AIとの関係

このリポジトリは、Yui Physical AI構想に向けた最初のUnity版コアランタイムとして始まりました。

現在はWindows上でVRMキャラクターをAIエージェントとして動かすことに集中しています。長期的には、外部アプリ連携、モバイルアプリ展開、フィジカルAI / 外部デバイス連携へ広げていく予定です。

## 詳細ドキュメント

- セットアップ詳細: `docs/SETUP_GUIDE.md`
- API仕様: `docs/api.md`
- リリース確認項目: `docs/ALPHA_RELEASE_CHECKLIST.md`

## トラブルシューティング

バックエンドが起動しているか確認:

```powershell
.\scripts\check_backend.ps1
```

バックエンド起動中に使えるローカルURL:

- http://127.0.0.1:8000/health
- http://127.0.0.1:8000/config
- http://127.0.0.1:8000/usage
- http://127.0.0.1:8000/docs

よくある初回トラブル:

- バックエンドが起動しない:
  - `.\scripts\setup_backend_byok.ps1` をもう一度実行してください。
- VOICEVOX Engineが見つからない:
  - 通常のVOICEVOXアプリ、またはVOICEVOX Engine単体をインストールしてください。
  - このアプリが必要とするのは `vv-engine\run.exe` です。
  - または `VOICEVOX_ENGINE_EXE` に `vv-engine\run.exe` のフルパスを設定してください。
- チャットが反応しない:
  - `.env` があるか確認してください。
  - `OPENAI_API_KEY` が空でないか確認してください。
- 新しく展開したのに以前の会話履歴が表示される:
  - 既に別フォルダのbackendが `127.0.0.1:8000` で起動している可能性があります。
  - いったん `Stop_Yui_Backend_And_VOICEVOX.bat` を実行し、新しく展開したフォルダの `Start_Yui_Backend_And_VOICEVOX.bat` から起動し直してください。
  - VRMパスや音声設定など一部のアプリ設定は、同じWindowsユーザー内で引き継がれる場合があります。
- 音声が再生されない:
  - VOICEVOXが起動しているか確認してください。
  - http://127.0.0.1:50021/version をブラウザで開けるか確認してください。
- ファイル選択画面が開かない:
  - `YuiFilePickerHelper.exe` が `Yui VRM AI Studio.exe` と同じフォルダにあるか確認してください。
- 終了できない:
  - 通常は `Start_Yui_Backend_And_VOICEVOX.bat` のウィンドウで Enter を押します。
  - それでも残る場合だけ `Stop_Yui_Backend_And_VOICEVOX.bat` を使ってください。

## ライセンスとクレジット

プロジェクトコードはMIT Licenseです。詳細は `LICENSE` を見てください。

サードパーティのアセットやライブラリは、それぞれのライセンスに従います。

- UnityChan assets are distributed under the Unity-Chan License Terms.
- VOICEVOX/VOICEVOX Engineは同梱していません。別途インストールし、VOICEVOXの利用規約とクレジット表記に従ってください。
- 生成音声を公開する場合は、選択したVOICEVOX話者に必要なクレジットを記載してください。alpha版のデフォルト音声は `VOICEVOX:冥鳴ひまり` です。
- ChatdollKit, lilToon, UniVRM, and other Unity packages remain under their respective licenses.
