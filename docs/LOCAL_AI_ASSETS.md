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

最新ソースのMac・iOSでは、設定の音声エンジン欄から追加音声を任意に取得できます。日本語Irodoriは3声・約1.96GB、英語KokoroはBella、Nova、Nicole、Heart、Puck、Michaelの6声・約67.4MB（展開後約101.6MB）です。通信と空き容量を確認し、取得を実行してください。進捗バナーから取消・再試行ができ、取得中も別の画面を使えます。会話・音声生成は取得後に端末内で動作します。

日本語の標準VOICEVOXはそのまま利用できます。追加音声は初期アプリに同梱せず、英語の初回案内でも取得を後回しにできます。Irodoriは固定バージョンのHugging Faceモデル、KokoroはGitHubの音声パックを取得し、ファイルのハッシュを検査します。既存の配布版0.2.4・デスクトップbeta.2ではこの追加音声機能を使えません。Windows・Androidの端末内Irodoriには未対応です。

PC Backendの追加音声は別の導入経路です。[Backend音声の導入](BACKEND_TTS_GUIDE.md)。

## 取得できないとき

1. 通信と空き容量を確認します。展開中はダウンロードサイズより多くの容量を使います。
2. アプリ内から再試行します。アプリと異なる版のデータを混ぜないでください。
3. 改善しなければ、OS・アプリ版・表示されたエラーを添えて[不具合を報告](https://github.com/Tsubame-chan/YuiVRMAIStudio/issues)してください。

Releaseにある `.part-*` は大きなデータを分割したファイルです。通常はアプリが結合・検査するため、手動で展開する必要はありません。`.sha256` はダウンロードしたファイルの破損を確認するためのチェックサムです。GitHubのCode ZIPにはモデル・音声データも実行アプリも入っていません。

English: desktop setup downloads about 2.5GB of required AI/voice data; allow additional space for extraction. iOS includes standard data. E4B is optional on Mac/iOS. The latest Mac/iOS source offers three Japanese Irodori voices (about 1.96GB, from a pinned Hugging Face model) and six English Kokoro voices (about 67.4MB ZIP / 101.6MB installed, from GitHub): Bella, Nova, Nicole, Heart, Puck and Michael. Downloads are optional, verified by file hashes, and can be cancelled or retried from the progress banner while other screens remain usable. English first-run setup allows postponing the download. Standard Japanese VOICEVOX remains available. These features are not included in the existing 0.2.4 or desktop beta.2 apps. On-device Irodori is not supported on Windows/Android. Use the app to download and verify data rather than extracting Release parts manually. Retry after checking connectivity and free space, and keep app/data versions together.

WindowsではCore ML版を使わず、Releaseの任意追加V4.1 INT8パッケージをBackendへ接続します。導入条件と実機未確認の範囲は[Windows Irodoriガイド](IRODORI_TTS_WINDOWS_NVIDIA.md)を参照してください。
