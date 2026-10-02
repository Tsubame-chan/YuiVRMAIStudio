# WindowsでIrodoriをBackendに接続する

Irodori-TTS-Serverを導入して起動し、Yui Backendから接続する手順です。必要なGPU・CUDA・モデルは[公式の導入案内](https://github.com/Aratako/Irodori-TTS-Server#readme)を確認してください。

Backend Consoleがある版では、[音声導入ガイドのWindows手順](BACKEND_TTS_GUIDE.md#irodori-windows)から設定できます。管理画面のない版や設定ファイルを使う場合は、次の手順で接続します。

<a id="settings-file"></a>

## 設定ファイルから接続する

Backendの `.env` に以下を設定します。例のURL・モデル名は、起動したIrodoriサーバーの設定に合わせて変更してください。

```env
TTS_PROVIDER=http
HTTP_TTS_BASE_URL=http://127.0.0.1:8088
HTTP_TTS_ENDPOINT=/v1/audio/speech
HTTP_TTS_HEALTH_ENDPOINT=/health
HTTP_TTS_PROVIDER_ID=irodori-server
HTTP_TTS_PAYLOAD_FORMAT=irodori_openai_speech
HTTP_TTS_VOICE=none
HTTP_TTS_MODEL=irodori-tts
HTTP_TTS_INSTRUCT=明るく、聞き取りやすい自然な声。
HTTP_TTS_FORMAT=wav
HTTP_TTS_AUDIO_PROCESSOR=none
HTTP_TTS_IRODORI_NUM_STEPS=24
HTTP_TTS_IRODORI_CHUNKING_ENABLED=false
```

設定後にBackendを再起動します。`HTTP_TTS_VOICE=none` は声の説明から生成する設定です。声を固定する場合は、Irodoriサーバーに登録した声の名前を指定してください。

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
