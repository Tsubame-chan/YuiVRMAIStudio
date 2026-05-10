# Yui VRM AI Studio

[English README](README.en.md)

Yui VRM AI Studio は、Unity製のアバター表示アプリとローカルPythonバックエンドを組み合わせた、Windows向けのローカルAIアバタースタジオです。

UnityChanまたは自分のVRMモデルを表示しながら、テキスト会話、音声入力、VOICEVOXによる日本語音声再生、画像入力、画面を見る実験機能、リアルタイム会話や翻訳の実験ができます。

このalpha版はBYOK方式です。利用者が自分のOpenAI APIキーを用意し、バックエンドを自分のPC上で起動します。

## 特徴

- UnityChan Default またはローカルの `.vrm` アバターを表示できます。
- VRM 1.0 と VRM 0.x のインポートに対応しています。
- テキストチャットができます。
- OpenAIの音声認識を使った音声入力ができます。
- VOICEVOX Engineを使って日本語音声を再生できます。
- 画像入力と、画面を見る実験機能を使えます。
- 実験的なリアルタイム機能があります。
  - リアルタイム会話
  - VOICEVOX版リアルタイム会話
  - リアルタイム翻訳
  - 画像/画面コンテキストを使った会話実験
- キャラクター名を設定できます。

現時点の音声出力はVOICEVOXの都合上、日本語音声が中心です。UIやテキスト会話は英語でも使えますが、まずは日本語利用を主な想定にしています。

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
- VOICEVOX Engine
  - https://voicevox.hiroshiba.jp/
- PowerShell

`.env.example` に入っているモデル名は、このalpha版の初期値です。自分のアカウントや地域で使えないモデルがある場合は、OpenAIの現在のドキュメントを確認して `.env` のモデル名を変更してください。

API利用量は、実験的なリアルタイム機能を常時使わなければかなり軽めです。通常のチャット、音声入力、画像入力、翻訳などを機能確認として試す程度なら、5ドル分のAPIクレジットでも十分遊べます。長時間のリアルタイム会話、画像や音声の大量利用では費用が増えるため、OpenAIのUsageページで確認しながら使ってください。

## クイックスタート

1. GitHubからこのリポジトリを取得します。
   - `Code` -> `Download ZIP` でZIPをダウンロードして展開します。
   - Gitを使う場合は `git clone <repository-url>` でもOKです。
2. 展開したフォルダを、できればシンプルな場所に置きます。

```text
C:\YuiVRMAIStudio
```

3. Python 3.12+ をインストールします。インストール時に `Add python.exe to PATH` を有効にしてください。
4. VOICEVOXをインストールします。
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

このファイル1つで、ローカルバックエンドとVOICEVOX Engineの起動をまとめて行います。アプリを使っている間は、この起動ウィンドウを開いたままにしてください。

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

- Backend does not start:
  - `.\scripts\setup_backend_byok.ps1` をもう一度実行してください。
- VOICEVOX is not found:
  - VOICEVOXを通常の場所にインストールしてください。
  - または `VOICEVOX_ENGINE_EXE` に `vv-engine\run.exe` のフルパスを設定してください。
- Chat does not respond:
  - `.env` があるか確認してください。
  - `OPENAI_API_KEY` が空でないか確認してください。
- Voice does not play:
  - VOICEVOXが起動しているか確認してください。
  - http://127.0.0.1:50021/version をブラウザで開けるか確認してください。
- File picker does not open:
  - `YuiFilePickerHelper.exe` が `Yui VRM AI Studio.exe` と同じフォルダにあるか確認してください。
- 終了できない:
  - 通常は `Start_Yui_Backend_And_VOICEVOX.bat` のウィンドウで Enter を押します。
  - それでも残る場合だけ `Stop_Yui_Backend_And_VOICEVOX.bat` を使ってください。

## ライセンスとクレジット

プロジェクトコードはMIT Licenseです。詳細は `LICENSE` を見てください。

サードパーティのアセットやライブラリは、それぞれのライセンスに従います。

- UnityChan assets are distributed under the Unity-Chan License Terms.
- VOICEVOXは同梱していません。別途インストールし、VOICEVOXの利用規約とクレジット表記に従ってください。
- 生成音声を公開する場合は、選択したVOICEVOX話者に必要なクレジットを記載してください。alpha版のデフォルト音声は `VOICEVOX:冥鳴ひまり` です。
- ChatdollKit, lilToon, UniVRM, and other Unity packages remain under their respective licenses.
