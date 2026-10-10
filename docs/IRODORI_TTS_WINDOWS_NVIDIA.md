# WindowsでIrodoriをBackendに接続する

Irodori-TTS-Serverを導入して起動し、Yui Backendから接続する手順です。必要なGPU・CUDA・モデルは[公式の導入案内](https://github.com/Aratako/Irodori-TTS-Server#readme)を確認してください。

Backend Consoleがある版では、[音声導入ガイドのWindows手順](BACKEND_TTS_GUIDE.md#irodori-windows)から設定できます。管理画面のない版や設定ファイルを使う場合は、次の手順で接続します。

<a id="settings-file"></a>

## V4.1の任意追加パッケージ

Desktop v0.2.5-beta.1のReleaseにある `YuiVRMAIStudio_IrodoriV41_WindowsNVIDIA_v0.2.5-beta.1.zip` は、公式V4.1 Small INT8（約914MB）、音声コーデック（約430MB）、公式サーバーの固定版、導入ツールと3つの合成参照音声を含みます。モデルの再取得は不要ですが、初回のPython/PyTorch CUDA依存導入にはインターネットと追加の空き容量が必要です。NVIDIA GPU・対応ドライバー・Git for Windowsが必要です。CPU・AMD・Android対応のパッケージではありません。

1. ZIPを展開し、`YuiIrodoriV4` フォルダーを既存の `YuiBackend` 内へ置きます。
2. Backendを終了し、`Install_Irodori_V4.bat` を実行します。ファイルのSHA-256を検査してから依存を導入し、元の `.env` をバックアップして接続設定を追加します。
3. Backendを再起動します。標準VOICEVOXは維持され、Irodoriは別途選択できます。必要なら `Start_Irodori_V4.bat` で音声サーバーのみ起動できます。
4. 新規の音声ライブラリには「明るく元気」「親しみやすく優しい」「落ち着いたトーン」の3声が表示されます。既存の保存済み音声は保持します。保存済みConsole設定がある場合は、そちらの接続設定が `.env` より優先されるため、ConsoleでV4.1接続に変更してください。

参照はPCのV4.1 FP16で生成したものを使用します。Windows生成はV4.1 INT8/BF16、40 steps、参照音声と文分割ありです。Mac/iPhoneとは実行方式が異なります。2026-10-11にRTX5070 Tiで起動・3声のWAV生成・Backend Consoleの試聴プレイヤー表示を実機確認しました。温まった短文生成は約2.5〜4.7秒でしたが、初回は追加データ取得とロードを伴い、約50.7秒かかりました。一般的な速度・主観的な音質・完全オフライン初回を保証する試験ではありません。[検証記録](WINDOWS_VALIDATION_20261011.md)。

公開済みv0.2.5-beta.1パッケージの旧installerはPython3.12を選び、SentencePiece依存の導入に失敗する場合があります。最新ソースの `scripts/setup_irodori_v4_windows.ps1` はPython3.11を指定し、uvのjunction作成失敗後も実体を検査して復帰します。既存Release ZIPはこの修正で再発行されていません。既存パッケージで修正版を使う場合は、同梱ファイルを上書きせず、最新ソースのinstallerを `-PackRoot` と `-BackendRoot` を指定して実行してください。

公式モデル: [V4.1 Small Quantized](https://huggingface.co/Aratako/Irodori-TTS-v4.1-Small-Quantized)。実行条件・MITライセンスと利用制限は同梱のモデルREADMEと[公式サーバー](https://github.com/Aratako/Irodori-TTS-Server)を参照してください。

## 設定ファイルから接続する

Backendの `.env` に以下を設定します。例のURL・モデル名は、起動したIrodoriサーバーの設定に合わせて変更してください。

```env
TTS_PROVIDER=http
HTTP_TTS_BASE_URL=http://127.0.0.1:8088
HTTP_TTS_ENDPOINT=/v1/audio/speech
HTTP_TTS_HEALTH_ENDPOINT=/health
HTTP_TTS_PROVIDER_ID=irodori-server
HTTP_TTS_PAYLOAD_FORMAT=irodori_openai_speech
HTTP_TTS_VOICE=bright_natural
HTTP_TTS_MODEL=irodori-tts
HTTP_TTS_INSTRUCT=明るく、聞き取りやすい自然な声。
HTTP_TTS_FORMAT=wav
HTTP_TTS_AUDIO_PROCESSOR=none
HTTP_TTS_IRODORI_NUM_STEPS=40
HTTP_TTS_IRODORI_CHUNKING_ENABLED=true
HTTP_TTS_IRODORI_CHUNK_MIN_CHARS=1
```

設定後にBackendを再起動します。`HTTP_TTS_VOICE=bright_natural` は同梱の「明るく元気」参照音声を使う登録済みの声IDです。ほかの登録済み声を使う場合は、その声IDを指定してください。声の説明から自由生成する設定とは区別します。

アプリのBackend URLには、Yui BackendのURL（同じPCなら通常 `http://127.0.0.1:8000`）を設定します。IrodoriのURLは `.env` の `HTTP_TTS_BASE_URL` へ設定してください。

<a id="connection-check"></a>

## 接続を確認する

Irodoriサーバーを起動した後、PowerShellで確認します。異なるポートを使っている場合はURLを変更してください。

```powershell
curl.exe http://127.0.0.1:8088/health
curl.exe http://127.0.0.1:8088/v1/models
curl.exe http://127.0.0.1:8000/providers/status
```

`/health` はサーバーの起動状態、`/v1/models` は使用できるモデルを確認するためのURLです。Yuiの `/providers/status` では `providers.http_tts.status` が `ok` または `configured` になっているかを確認します。その後、短い文章で音声生成を試してください。

接続できない場合は、サーバーとBackendの起動状態・URL・ポートを確認します。接続できても生成が失敗する場合は、モデル名・登録した声の名前・GPUの空きメモリを確認してください。

## 起動をまとめる場合

起動するたびにIrodoriを手動で開く代わりに、Backendのサービス起動ファイルから起動できます。`.env` に以下を追加してください。

```env
IRODORI_ENABLE=auto
IRODORI_BASE_URL=http://127.0.0.1:8088
IRODORI_START_COMMAND=
```

`IRODORI_START_COMMAND` に、導入した環境でサーバーを起動するコマンドを設定します。Dockerを使っている場合はDockerの起動コマンドです。空欄の場合は、Irodoriを別途起動してください。

音声モデルの利用条件は、使用するモデルの配布ページを確認してください。
