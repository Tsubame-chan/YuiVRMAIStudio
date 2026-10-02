# AI・音声データの取得 / AI and voice downloads

## 通常の導入

[アプリ](../README.md#ダウンロード)を起動し、初回の案内から必要データを取得してください。デスクトップ版は合計約2.5GBに加え、展開用の空き容量が必要です。Pythonやモデルを個別に導入する必要はありません。

iOS版は標準のAI・音声データを同梱しています。Mac・iOSで使えるE4Bは設定から任意に取得する追加モデルです。E4Bを取得しなくても標準E2Bで会話できます。

標準の読み上げは次の日本語5声です。音声の利用条件・必要なクレジットは、各音声のライセンス案内を確認してください。

| 声 | 標準スタイルID |
| --- | --- |
| 冥鳴ひまり | 14 |
| 四国めたん | 2 |
| ずんだもん | 3 |
| 九州そら | 16 |
| 小夜/SAYO | 46 |

Aivis・Irodori等の追加音声は標準データに含まれません。[追加音声の導入](BACKEND_TTS_GUIDE.md)。

## 取得できないとき

1. 通信と空き容量を確認します。展開中はダウンロードサイズより多くの容量を使います。
2. アプリ内から再試行します。アプリと異なる版のデータを混ぜないでください。
3. 改善しなければ、OS・アプリ版・表示されたエラーを添えて[不具合を報告](https://github.com/Tsubame-chan/YuiVRMAIStudio/issues)してください。

Releaseにある `.part-*` は大きなデータを分割したファイルです。通常はアプリが結合・検査するため、手動で展開する必要はありません。`.sha256` はダウンロードしたファイルの破損を確認するためのチェックサムです。GitHubのCode ZIPにはモデル・音声データも実行アプリも入っていません。

English: desktop setup downloads about 2.5GB of required AI/voice data; allow additional space for extraction. iOS includes standard data. E4B is optional on Mac/iOS. Use the app to download and verify data rather than extracting Release parts manually. Retry after checking connectivity and free space, and keep app/data versions together. Standard speech includes five Japanese VOICEVOX voices; optional engines require separate setup.
