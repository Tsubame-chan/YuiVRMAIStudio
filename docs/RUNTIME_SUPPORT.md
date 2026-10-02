# 実行経路と受入範囲 / Runtime support

2026-10-03。desktop v0.2.4-beta.2 / iOS 0.2.4 (14)候補。旧beta.5の制限と混同しないでください。

| OS | 端末内文章生成 | 音声入力・出力 | 検証の限界 |
| --- | --- | --- | --- |
| macOS | LiteRT-LM Python worker。標準E2B、任意E4B | workerでローカルSTT / native VOICEVOX Core。Direct API / Backendも選択可 | Apple Silicon向けruntime。Intel・別端末・長時間の保証なし |
| Windows | 同じworker経路を実装、E2BデータとWindows Python/runtimeを配布 | ローカルSTT / VOICEVOXのworker経路、Direct API / Backend | Windows 64-bit runtime。GPU/driver・端末性能によって動作が異なります |
| iOS 26+ | Swift LiteRT-LM。E2B同梱、E4BはApple配信の任意取得 | OS Speech / VOICEVOX Core / OS音声。Direct APIも選択可 | iPhone 16 Proで本人確認。通常モデル・低メモリ端末全般の保証なし。App Store審査中 |
| Android arm64 | Kotlin LiteRT-LMのソース経路 | OS Speech / VOICEVOX Coreのソース経路 | 公開アプリ・実機受入なし。OS Speechの完全オフライン保証なし |

Windowsの新しいruntime起動・髪の揺れ・同期認証情報の保持と、両OSの新しいPlayerのアバター読込は実機確認待ちです。

Desktop workerとHTTP Backendサーバーは別です。初回に取得する `YuiBackend` bundleには両方のコードが入りますが、端末内会話のためにサーバーを起動する必要はありません。APIキーはDirect接続とBackendで別々に設定します。

通常チャットでは人格・履歴・検索された記憶をローカル/APIに共通で渡します。秘密モードは既存記憶を参照し、新しい会話を保存しません。キャラクター・秘密モードが変わった後に古い要求の結果を保存しない保護があります。Realtimeなどの実験的経路を通常チャットと同じ受入水準とは扱いません。desktop beta.2では、登録済みの端末間で人格・記憶・履歴を確認付きで同期できます。iOS 0.2.4 (14)は同期非対応です。[操作方法](BACKEND_CONSOLE.md#キャラクターと会話を端末間で同期する)。

画像は添付として保持し、送信時に選択した経路で処理します。API/Backendでは外部送信となります。fallbackを有効にした場合、表示する処理先も確認してください。

停止は要求を取消し、後続の表示・再生を抑止します。Desktop workerは親プロセスが終了させますが、すべてのネイティブ同期推論が即座に中断できる保証はありません。端末ごとの中断・復帰受入は別途必要です。

データ取得はSHA-256確認、別領域への展開、必須ファイル検査後に入れ替えます。取消し・展開失敗では既存データを保護します。展開用の空き容量が必要で、OS強制終了中の入替えを完全なトランザクションとは扱いません。

Avatar Bridgeは利用者のUnity/VCCプロジェクトからOS別ZIPを作る別経路です。VRChatサーバーからアバターを取得しません。VRM/ZIP読込成功は衣装メニュー・独自シェーダー・PhysBoneの完全互換を意味しません。

Aivis、Irodori/Kokoro、Realtime、遠隔運用は実験的です。今回の標準データに追加TTSパックは含みません。ローカルBackendを認証なしでインターネット公開する構成は提供しません。

English summary: macOS uses the Apple Silicon Python worker; Windows uses the same worker route with its own bundled runtime. iOS requires iOS 26 and has owner acceptance on iPhone 16 Pro, with store review pending. Android and experimental Backend integrations are not equivalently accepted. See [HELP](HELP.md).
