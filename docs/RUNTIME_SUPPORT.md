# 実行経路と受入範囲

2026-09-21の開発ソース。公開バイナリbeta.5とは区別してください。

| OS | 端末内文章生成 | 音声入力 | 音声出力 | 検証の限界 |
|---|---|---|---|---|
| macOS | E2B / LiteRT-LM CLI 0.17.0。CLI経路はChatのみ | Backend STT | VOICEVOX CoreまたはBackend Engine | 長時間・新規端末の受入は別途必要 |
| Windows | 埋込Gemma未対応。API / 設定済みBackendを使用 | Backend STT | Backend VOICEVOX Engine | Windows上の起動・導入・発話受入が必要 |
| iOS | Swift LiteRT-LM 0.13.1指定 | OS Speech | VOICEVOX Core / OS音声 | 依存解決・実機のメモリ、権限、中断復帰を要確認 |
| Android arm64 | Kotlin LiteRT-LM 0.13.1指定 | OS Speech（常時オフライン保証なし） | VOICEVOX Core呼出しを追加、検証候補。VOICEVOX選択時に既存の経路選択へ接続 | Native ABIコンパイルと端末発話は別。ローカルBackend優先、なければnativeを試行。実機受入は未完了 |

Mac直接CLIとBackend用LiteRT-LMサーバーは別経路です。サーバーの標準モデル別名は `gemma4-e2b,gpu`。既存E4B利用者は環境変数で明示指定できます。

画像は選択・撮影時に添付として保持し、送信時に解析します。添付欄に画像と処理先を示します。API/Backend経由は設定したサービスへの送信を伴います。自動fallbackが有効なら処理先が代替される場合があります。

停止は通常チャットの要求を取消し、返答の表示・後続再生を抑止します。ネイティブ同期推論の途中計算を即座に終了する保証はありません。Realtimeの録音停止はマイク操作、再生停止は停止ボタンです。接続・合成・録音をまたぐ統一停止は実機受入が必要です。

ダウンロードはSHA-256確認、別領域への展開、必須ファイル確認後に入れ替えます。展開失敗・取消し時は既存ファイルを保持/復元します。十分な空き容量が必要で、OSによる強制終了や停電中のファイル入替えを完全なトランザクションとは扱いません。

Avatar Bridge 0.1.1は利用者のUnity/VCCプロジェクトからOS別ZIPを作るツールです。VRChatサーバーから取得しません。VRM/ZIP読込成功と衣装・表情・PhysBone互換は別で、OSごとの受入が必要です。

Aivis Native、Irodori/Kokoro、遠隔Docker運用は実験対象です。遠隔認証・ペアリング・同期は未完成であり、ローカルBackendをそのままインターネット公開する構成は提供しません。ストア受入は未完了です。
