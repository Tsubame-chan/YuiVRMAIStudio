# Backendの音声エンジンを導入する

Backend Consoleは、導入済みの音声エンジン/APIへ接続して、声の選択・調整・試聴をまとめる画面です。エンジンのインストーラーそのものではありません。

## 最初から含まれるもの

2026-10-03にソース生成設定とv0.2.4-beta.1の実ZIPを確認した内容です。

| 入手方法 | 含まれるもの | 別途必要なもの |
| --- | --- | --- |
| GitHubのソース（clone / Download ZIP） | Backendコード、VOICEVOX/Aivis/HTTP TTSの接続adapter、起動スクリプト | HTTP音声エンジン、声モデル、Python依存関係 |
| v0.2.4-beta.1 Backend Bundle / Mac | BackendとPython実行環境 | VOICEVOX Engine、Aivis、IrodoriのHTTPエンジン・モデル |
| v0.2.4-beta.1 Backend Bundle / Windows | Backend、Python実行環境、端末内合成用VOICEVOX Core DLL | Backendから呼ぶHTTPエンジン、Aivis/Irodoriの実行環境・モデル |
| アプリ用Desktop Minimum追加データ | E2Bと標準VOICEVOX 5声・辞書・許諾文書 | Aivis/Irodori、HTTPエンジン |

アプリの「端末内の声」、VOICEVOX Core、ブラウザのBackendが呼ぶVOICEVOX Engineは異なる経路です。アプリが標準音声で喋れても、50021番のHTTPエンジンが起動しているとは限りません。ローカル開発機に導入済みの追加モデルを、一般ユーザーの初期環境と扱いません。

## 対応する接続形式

| 形式 | 一覧と主な操作 | 対応範囲 |
| --- | --- | --- |
| VOICEVOX Engine | /speakersの話者・スタイル一覧、速度・ピッチ・抑揚等 | VOICEVOX合成API。Core DLLへの直接接続ではない |
| AivisSpeech Engine | 導入済み話者・スタイル一覧、VOICEVOX互換の調整 | AivisSpeechのエンジン。任意のAI音声モデルファイルを直接実行しない |
| Irodori / mlx-audio（Mac Apple Silicon） | /v1/models、声の説明・性別・言語、速度/ピッチの後処理 | MLX Speech API拡張。ローカルでは生成済み参照音声を一時ファイルで共有 |
| Irodori-TTS-Server（Windows CUDA等） | /v1/models、対応版では/v1/audio/voices、caption、登録済みのvoice | 専用API形式。MLXと同じリクエストではない。今回のWindows実機GUI経路は未検証 |
| 共通JSON / Speech API互換 | モデル名・voice名を手動入力、試聴で確認 | adapterと同じリクエスト/音声応答形式であることが必要 |

一覧が返らないことと、合成APIが使えないことは別です。手動入力で使える互換APIもあります。一方、独自の認証、WebSocket、独自JSON応答、参照音声アップロード等はURLだけで対応できません。新しいadapterと能力定義が必要です。「対応形式」も実機受入済みという意味ではありません。

<a id="voicevox"></a>

## VOICEVOX Engine

1. [公式VOICEVOX](https://voicevox.hiroshiba.jp/)からOSに合う版を導入し、起動します。
2. Consoleの「AIと音声 → 読み上げ」でVOICEVOXを選びます。通常の接続先は `http://127.0.0.1:50021`。異なる場合は「提供元と接続」で設定します。
3. 話者とスタイルを選択し、試聴して名前を付けて保存します。アプリでは「Backendの声を選ぶ」から使う声を指定します。

利用条件は[VOICEVOXの規約](https://voicevox.hiroshiba.jp/term/)と各声の規約を確認してください。アプリの端末内VOICEVOX Core音声とは別経路です。

<a id="aivis"></a>

## AivisSpeech

1. [公式AivisSpeech](https://github.com/Aivis-Project/AivisSpeech#readme)の案内からOSに合うインストーラーを導入し、アプリ/エンジンを起動します。
2. AivisSpeechで必要な.aivmxモデルを導入します。[AivisHub](https://hub.aivis-project.com/)の各モデルページで利用条件を確認してください。インストール操作や許諾確認はAivis側で行います。
3. Backend Console → AIと音声 → 読み上げ → 新しい声でAivisSpeechを選びます。通常の接続先は http://127.0.0.1:10101 。一覧は自動取得します。
4. 話者とスタイルを選び、短い文章で試聴して保存します。別PC/別ポートなら「提供元と接続」でAivis接続先を追加してください。

一覧が空なら、モデルがそのエンジンに導入されているかを確認します。到達不能なら起動/ポート/接続先を確認します。保存済みIDが別エンジンに存在しなくても、他の声へ自動で置換しません。

[AivisSpeech Engine](https://github.com/Aivis-Project/AivisSpeech-Engine#ライセンス)はLGPL-3.0。モデルは別ライセンスです。[ACML 1.0](https://github.com/Aivis-Project/ACML/blob/master/ACML-1.0.md)は条件付きの再配布を認めていますが、ACML-NCや独自規約もあります。全モデルを同じ条件とみなしません。現在の非同梱を「Aivis全体が再配布禁止だから」と説明する根拠はありません。

<a id="irodori-mac"></a>

## Irodori：Apple Silicon Mac

1. [mlx-audioの導入手順](https://github.com/Blaizzy/mlx-audio#readme)に従い、別のPython環境へインストールします。[Irodori固有の案内](https://github.com/Blaizzy/mlx-audio/tree/main/mlx_audio/tts/models/irodori_tts)も確認します。WindowsではMLX版を使用しません。
2. 使うモデルを導入します。今回の接続確認対象は [Irodori-TTS-600M-v3-VoiceDesign-8bit](https://huggingface.co/mlx-community/Irodori-TTS-600M-v3-VoiceDesign-8bit)。モデル・DACVAE・tokenizer等を揃え、サーバーから読める場所へ置きます。起動だけではモデル一覧が空の場合があります。
3. 導入した環境で `python -m mlx_audio.server --host 127.0.0.1 --port 41080` を起動します。モデルの登録/読込方法は使用版のmlx-audio手順に従います。
4. Console → 提供元と接続 → 追加する形式「Irodori / Mac MLX」で接続先を追加します。既定例は41080番、生成パス/v1/audio/speech。モデル名はそのサーバーで使えるモデルID/ローカルパスと合わせます。
5. 新しい声でその接続先を選び、モデル一覧から選択します。声の説明（例:「明るく、やや高めの女性の声。自然な会話調で」）・性別・言語を指定して試聴、保存します。

同じPCのMLXでは、Backendが生成した参照音声を短時間の共有ファイルで渡します。本人の録音を自動送信する機能ではありません。別PCのMLXへBackendのローカルパスを送らないため、遠隔接続では説明による生成になります。参照音声アップロードのGUIは未実装です。

<a id="irodori-windows"></a>

## Irodori：Windows NVIDIA等

1. [公式Irodori-TTS-Server](https://github.com/Aratako/Irodori-TTS-Server#readme)のWindows/CUDAまたはDocker手順に従い導入します。モデル・必要なGPU依存は別途取得します。公式mainは進化するため、使用サーバー版とモデルの対応を確認してください。古いモデルを最新版へ無条件に置換しません。
2. 例として8088番で起動し、/healthと/v1/modelsの応答を確認します。
3. Consoleで「Irodori-TTS-Server」形式の接続先を追加します。例は生成パス/v1/audio/speech、モデルirodori-tts、voice=none。サーバーでモデル名を変えた場合はそれに合わせます。
4. 声の説明で生成する場合はnone、声を固定する場合はサーバーで登録済みのvoiceを選びます。対応版の登録済み一覧は自動取得します。未対応版ではサーバーのvoicesフォルダーで登録した名前を手動指定できます。
5. 試聴で生成を確認します。生成ステップ数・seed等は接続先の詳細設定で扱います。MLX専用の性別/言語指定や、Backendのファイルパスをこのサーバーへ送りません。

追加の検証手順は[Irodori Windows NVIDIA](IRODORI_TTS_WINDOWS_NVIDIA.md)。CPUのみの性能、全GPU、全サーバー版の動作を保証していません。

[Irodori-TTS-Serverのコード](https://github.com/Aratako/Irodori-TTS-Server#license)と今回の[MLX 8bitモデル](https://huggingface.co/mlx-community/Irodori-TTS-600M-v3-VoiceDesign-8bit)はMITの表示です。[原VoiceDesignモデルの利用条件](https://huggingface.co/Aratako/Irodori-TTS-600M-v3-VoiceDesign)には追加の利用上の制約も示されています。別モデルや付属資産を一括でMITと推定しません。再配布する場合は、正確な対象/version、各許諾文書・出典を確認してから配布構成へ追加します。今回エンジンやモデルを新しく同梱する変更はありません。

<a id="other-tts"></a>

## その他のTTSを追加する

「提供元と接続」で共通JSONまたはSpeech API互換を選び、提供元のURL・生成パス・モデル・voice・必要なキーを指定します。キーは保存後に値を再表示しません。接続確認パスがない場合は未確認のまま、短い文章の試聴で実生成を確認します。

一覧に未対応のAPIではvoice/modelは手動入力です。提供元の説明にある識別子を使います。合成できなければ/api契約とadapterの形式を照合し、独自APIを無理にVOICEVOXやIrodori形式へ当てはめません。[Backend API](api.md)と[Console案内](BACKEND_CONSOLE.md)も参照してください。

公式情報の確認日: 2026-10-03。エンジン・モデルのダウンロードや利用条件への同意は、この案内を読んだだけでは実行されません。
