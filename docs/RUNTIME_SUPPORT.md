# 端末ごとの対応 / Device compatibility

公開中のアプリ・Backendは[配布一覧](DOWNLOADS.md)から取得してください。ソースのビルド番号や日付付き検証記録は、現在の配信状況そのものではありません。

| 端末 | 標準の端末内AI | 任意モデルの取り込み | 利用条件 |
| --- | --- | --- | --- |
| Mac | E2B、任意でE4B | 単一GGUF・互換LiteRT-LM | Apple Silicon向け。初回に必要データを取得 |
| Windows | E2B、任意でE4B | 互換LiteRT-LM | 64-bit版。GGUF実行エンジンは配布版に未同梱 |
| iPhone / iPad（App Store版） | E2B、任意でE4B | 公開版には未搭載 | iOS 26以降。標準データ同梱。日本向け無料版 |
| Android | 公開アプリなし | — | ソースの存在は配布済みアプリを意味しません |

iOS向けの任意モデル取り込みは開発ビルドで実装・実機検証されています。App Storeで公開中の版と区別してください。GGUFのWindows準備経路やAndroid対応も、配布済みの実行機能とは別です。[対応形式・使い方・実行環境の制限](LOCAL_MODEL_IMPORT.md)。

追加モデルは実験的です。形式が同じでも、実行エンジン、チャットテンプレート、量子化、メモリなどによって動作が変わります。E4Bや大きな追加モデルは容量・メモリ・待ち時間が増えます。問題があれば標準E2Bへ戻してください。

## 音声とAPI

標準の読み上げは日本語VOICEVOXの5声です。iOSではOS音声も使えます。Mac・iOSは設定から日本語Irodoriの3声と英語Kokoroの6声を任意取得できます。[AI・音声データ](LOCAL_AI_ASSETS.md)。Windows・Androidの端末内Irodoriは未対応です。

Windows NVIDIAのIrodoriは端末内音声ではなく、[Backend Consoleから取得](BACKEND_ASSET_DOWNLOADS.md)して利用します。手動導入用の固定追加ZIPは旧タグの検証済み資産を再利用しています。[配布一覧](DOWNLOADS.md) / [Windows Irodori](IRODORI_TTS_WINDOWS_NVIDIA.md)。

OpenAI APIにはインターネット接続、APIキー、API利用料金が必要です。AIと読み上げの選択は独立しています。

## Backendと同期

PCのBackendでAI・追加音声を設定し、端末登録後にキャラクターの人格・記憶・履歴を共有できます。公開iOS版もBackend接続と端末同期に対応します。必要な操作が見当たらない旧アプリ・Backendは更新してください。アプリのDirect APIキーとBackendのキーは別々に設定します。[Backendの使い方](BACKEND_CONSOLE.md) / [安全な更新](BACKEND_UPDATE.md)。

端末内AI・端末内音声を使うだけならBackendの起動は不要です。Aivis・Irodori等の追加音声エンジンには別途導入が必要です。Realtimeなどの追加機能は実験的です。

## アバター

VRM 0.x / 1.0を読み込めます。VRChatの衣装メニュー・独自シェーダー・PhysBoneなどが、そのまま再現されるわけではありません。[VRMの準備](AVATAR_IMPORT.md)。

## 検証範囲

現在の公開Releaseの検証範囲は[Release notes](DOWNLOADS.md#更新履歴--release-notes)を確認してください。Windowsの追加モデル取り込みやBackend自動取得には、実機受入が未完了の経路があります。過去の成功例やMacの試験を、そのまま全Windows環境・全モデルの保証とは扱いません。Mac版はad-hoc署名で、Developer ID署名・公証は未提供です。

## English

Get published apps and matching Backend bundles from [Downloads](DOWNLOADS.md). Mac targets Apple Silicon; Windows targets 64-bit systems. Both provide E2B and optional E4B. Mac additionally imports single-file GGUF and compatible LiteRT-LM; the published Windows app imports compatible LiteRT-LM and does not bundle GGUF execution. Model import on iOS is implemented and tested in development builds, but is not included in the currently announced App Store release. There is no public Android app. See [model import](LOCAL_MODEL_IMPORT.md) for formats and runtime limitations.

iOS requires iOS 26 and includes standard data. Mac/iOS offer optional Japanese Irodori and English Kokoro downloads. Windows NVIDIA Irodori runs through the Backend, with [Console acquisition](BACKEND_ASSET_DOWNLOADS.md#english). The desktop and published iOS apps support Backend connections and paired character sync. API keys and charges apply to OpenAI API. Additional models are experimental; consult the current release notes for device-validation limits. Source changes do not update installed binaries.
