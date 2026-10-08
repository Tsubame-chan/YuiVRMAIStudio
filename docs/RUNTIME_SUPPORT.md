# 端末ごとの対応 / Device compatibility

| 端末 | 端末内AI | 標準の読み上げ | 利用条件 |
| --- | --- | --- | --- |
| Mac | E2B、任意でE4B | 日本語VOICEVOX・5声 | Apple Silicon向け。初回に必要データを取得 |
| Windows | E2B | 日本語VOICEVOX・5声 | 64-bit版。初回に必要データを取得 |
| iPhone / iPad | E2B、任意でE4B | 日本語VOICEVOX・OS音声 | iOS 26以降。標準データ同梱。日本向けApp Store版を配信中 |
| Android | 公開アプリなし | — | ダウンロードできるアプリは提供していません |

OpenAI APIを選ぶ場合はインターネット接続とAPIキーが必要で、API利用料金がかかります。AIと読み上げの選択は独立しています。最新ソースのMac・iOSではIrodori日本語2声とKokoro英語6声を設定から任意取得できます。既存の配布版0.2.4・デスクトップbeta.2にはこの追加音声機能は含まれません。Windows・Androidの端末内Irodoriには未対応です。

E4BはE2Bより多くの空き容量とメモリを使い、回答にも時間がかかります。機種、メモリ、他のアプリの使用状況によって動作は変わります。動作が重い場合はE2Bへ戻してください。

GitHubの最新ソースと、ダウンロードできるデスクトップ版v0.2.4-beta.2では機能が異なります。最新ソースはUnity 6.3 LTSを使いますが、配布済みデスクトップ版は以前のUnity版です。ソースを取得してもインストール済みアプリやBackendは更新されません。

## Backendと同期

デスクトップ版v0.2.4-beta.2では、PCのBackendを起動してAI・追加音声を設定し、端末登録後にキャラクターの人格・記憶・履歴を共有できます。端末内AIを使うだけならBackendを起動する必要はありません。

公開済みiOS版はBackendへの会話接続と端末同期に対応します。以前のTestFlight版で同期ボタンが表示されない場合は、対応版への更新が必要です。アプリのDirect APIキーとBackend側のキーは別々に設定します。[Backendの使い方](BACKEND_CONSOLE.md)。

Aivis・Irodori等の追加音声には別途導入が必要です。Realtimeなどの追加機能は実験的です。すべての端末や組み合わせでの動作を保証するものではありません。

## アバター

VRM 0.x / 1.0を読み込めます。VRChatの衣装メニュー・独自シェーダー・PhysBoneなどが、そのまま再現されるわけではありません。[VRMの準備](AVATAR_IMPORT.md)。

English: the Mac app targets Apple Silicon; the Windows app targets 64-bit Windows. Both download required AI/voice data during setup. iOS requires iOS 26 and includes standard data; the free Japan App Store version is available. E4B is optional on Mac/iOS. The latest Mac/iOS source also offers optional Japanese Irodori and English Kokoro voice downloads; the existing 0.2.4 and desktop beta.2 apps do not include them. On-device Irodori is not supported on Windows/Android. Desktop beta.2 and the published iOS version support paired character sync. Earlier TestFlight versions without a sync button need an update. No public Android app is available. API keys and charges apply when using OpenAI API. See [Help](HELP.md).
