# Backendに音声エンジンを追加する

VOICEVOX、AivisSpeech、Irodoriを使うには、音声エンジンと必要なモデルを導入して起動します。その後、Backend Consoleで接続先を設定し、声の選択・調整・試聴を行います。

Backend ConsoleはGitHubの最新ソースで利用できます。v0.2.4-beta.1のBackend Bundleには管理画面は含まれません。

## 最初から含まれるもの

| 入手方法 | 含まれるもの | 音声を使うために追加するもの |
| --- | --- | --- |
| GitHubのソース（clone / Download ZIP） | Backendの接続機能と起動ファイル | Python依存関係、音声エンジン、音声モデル |
| v0.2.4-beta.1 Backend Bundle / Mac | BackendとPython実行環境 | 音声エンジン、音声モデル |
| v0.2.4-beta.1 Backend Bundle / Windows | Backend、Python実行環境、端末内音声用の実行ファイル | Backendで使う音声エンジン、音声モデル |
| アプリ用Desktop Minimum追加データ | 標準VOICEVOX 5声と辞書 | Aivis/Irodoriを使う場合は、それぞれのエンジンとモデル |

アプリの「端末内の声」と、Backendに接続して使う音声は別です。アプリが標準音声で話せても、Backendで使う音声エンジンは別途起動する必要があります。

## 選べる音声エンジン

| 音声エンジン・接続形式 | 設定できること |
| --- | --- |
| VOICEVOX Engine | 話者・スタイル、速度、ピッチ、抑揚、音量など |
| AivisSpeech Engine | 導入済みモデルの話者・スタイル、速度、ピッチなど |
| Irodori / mlx-audio（Apple Silicon Mac） | モデル、声の説明、性別、言語。速度・ピッチは生成後に調整 |
| Irodori-TTS-Server（Windows NVIDIA等） | モデル、声の説明、サーバーに登録した声。GPUなどの動作要件は公式手順を確認 |
| 共通JSON / Speech API互換 | 提供元が指定するモデル名・voice名を入力して使用 |

一覧を取得できない接続先でも、モデル名・voice名を手動入力して使える場合があります。独自のAPI形式には対応していないため、提供元の仕様が選択した接続形式と一致するかを確認してください。

<a id="voicevox"></a>

## VOICEVOX Engine

1. [公式VOICEVOX](https://voicevox.hiroshiba.jp/)からOSに合う版を導入し、起動します。
2. Consoleの「AIと音声 → 読み上げ」でVOICEVOXを選びます。通常の接続先は `http://127.0.0.1:50021`。異なる場合は「提供元と接続」で設定します。
3. 話者とスタイルを選択し、試聴して名前を付けて保存します。アプリでは「Backendの保存した声を選ぶ」から指定します。

利用条件は[VOICEVOXの規約](https://voicevox.hiroshiba.jp/term/)と各声の規約を確認してください。

<a id="aivis"></a>

## AivisSpeech

1. [公式AivisSpeech](https://github.com/Aivis-Project/AivisSpeech#readme)の案内からOSに合う版を導入し、起動します。
2. AivisSpeechで使いたい音声モデル（.aivmx）を追加します。[AivisHub](https://hub.aivis-project.com/)でモデルを探せます。
3. Consoleの「AIと音声 → 読み上げ → 新しい声」でAivisSpeechを選びます。通常の接続先は `http://127.0.0.1:10101`。一覧は自動取得します。
4. 話者とスタイルを選び、短い文章で試聴して保存します。別PC・別ポートへ接続する場合は「提供元と接続」で接続先を追加してください。

一覧が空なら、接続先のAivisSpeechに音声モデルが追加されているかを確認します。接続できない場合は、エンジンの起動状態・URL・ポートを確認してください。

声によって利用条件が異なります。使うモデルの配布ページで、クレジット表記や商用利用などの条件を確認してください。

<a id="irodori-mac"></a>

## Irodori：Apple Silicon Mac

1. [mlx-audioの導入手順](https://github.com/Blaizzy/mlx-audio#readme)と[Irodoriの案内](https://github.com/Blaizzy/mlx-audio/tree/main/mlx_audio/tts/models/irodori_tts)に従ってインストールします。
2. Irodoriモデルを導入します。モデルの例は[Irodori-TTS-600M-v3-VoiceDesign-8bit](https://huggingface.co/mlx-community/Irodori-TTS-600M-v3-VoiceDesign-8bit)。必要なファイルと読込方法は配布ページ・mlx-audioの手順を確認してください。
3. 導入したPython環境で `python -m mlx_audio.server --host 127.0.0.1 --port 41080` を実行します。
4. Consoleの「AIと音声 → 読み上げ → 提供元と接続」で「Irodori / Mac MLX」を追加します。接続先は `http://127.0.0.1:41080`、生成パスは `/v1/audio/speech`。モデル名はサーバーで使える名前に合わせます。
5. 「新しい声」でその接続先とモデルを選びます。声の説明（例：「明るく、やや高めの女性の声。自然な会話調で」）・性別・言語を指定し、試聴して保存します。

モデル一覧が空の場合は、サーバーへモデルが読み込まれているかを確認してください。録音ファイルをアップロードして声を指定する操作には対応していません。

モデルの利用条件は[配布ページ](https://huggingface.co/mlx-community/Irodori-TTS-600M-v3-VoiceDesign-8bit)と[原モデルの案内](https://huggingface.co/Aratako/Irodori-TTS-600M-v3-VoiceDesign)を確認してください。

<a id="irodori-windows"></a>

## Irodori：Windows NVIDIA等

1. [公式Irodori-TTS-Server](https://github.com/Aratako/Irodori-TTS-Server#readme)のWindows/CUDAまたはDocker手順に従って導入します。使用するサーバーの版に対応したモデルとGPU環境を用意してください。
2. サーバーを起動します。接続先の例は `http://127.0.0.1:8088`。起動確認の方法は[Windows向け接続手順](IRODORI_TTS_WINDOWS_NVIDIA.md#connection-check)にあります。
3. Consoleの「AIと音声 → 読み上げ → 提供元と接続」で「Irodori-TTS-Server」を追加します。生成パスは `/v1/audio/speech`。モデル名の例は `irodori-tts`、声の説明で生成する場合のvoiceは `none` です。サーバーの設定に合わせて変更してください。
4. 「新しい声」で接続先を選び、声の説明を入力するか、サーバーに登録した声を選択します。一覧が取得できない版では、登録した声の名前を手動入力できます。
5. 短い文章で試聴して保存します。生成ステップ数・seedなどは接続先の詳細設定で調整できます。

モデルと付属ファイルの利用条件は、公式サーバーの案内から各配布ページで確認してください。

<a id="other-tts"></a>

## その他のTTSを追加する

「提供元と接続」で共通JSONまたはSpeech API互換を選び、提供元のURL・生成パス・モデル・voice・必要なAPIキーを入力します。

一覧を取得できないAPIでは、モデル名・voice名を手動入力してください。入力する値は提供元の案内にある識別子です。短い文章で試聴して、接続先で音声を生成できるか確認します。

生成できない場合は、エンジンの起動状態、URL・生成パス、APIキー、モデル名・voice名を確認してください。設定値が正しくても失敗する場合は、提供元のAPIが選択した接続形式に対応しているかを確認します。

Console全体の操作は[Backend Consoleの使い方](BACKEND_CONSOLE.md)を参照してください。
