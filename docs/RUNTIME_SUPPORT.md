# 端末ごとの対応 / Device compatibility

| 端末 | 端末内AI | 標準の読み上げ | 利用条件 |
| --- | --- | --- | --- |
| Mac | E2B、任意でE4B | 日本語VOICEVOX・5声 | Apple Silicon向け。初回に必要データを取得 |
| Windows | E2B、任意でE4B | 日本語VOICEVOX・5声 | 64-bit版。初回に必要データを取得 |
| iPhone / iPad | E2B、任意でE4B | 日本語VOICEVOX・OS音声 | iOS 26以降。標準データ同梱。日本向けApp Store版を配信中 |
| Android | 公開アプリなし | — | ダウンロードできるアプリは提供していません |

OpenAI APIを選ぶ場合はインターネット接続とAPIキーが必要で、API利用料金がかかります。AIと読み上げの選択は独立しています。Mac・iOS版0.2.5以降ではIrodori日本語3声とKokoro英語6声を設定から任意取得できます。以前のiOS 0.2.4・デスクトップv0.2.4-beta.2にはこの追加音声機能は含まれません。Windows・Androidの端末内Irodoriには未対応です。

E4BはE2Bより多くの空き容量とメモリを使い、回答にも時間がかかります。機種、メモリ、他のアプリの使用状況によって動作は変わります。動作が重い場合はE2Bへ戻してください。

2026-10-11のWindows検証では、v0.2.5-beta.1アプリへ最新のモデル登録JSONを配置してE4Bの設定選択と実会話を確認しました。v0.2.5-beta.2では変更を含むWindows/Mac Playerを再ビルドしましたが、新候補のWindows実機受入は未完了です。[検証と未確認事項](WINDOWS_VALIDATION_20261011.md)。Windows向けIrodoriは端末内音声ではなく、[Backend Consoleの追加取得](BACKEND_ASSET_DOWNLOADS.md)で利用します。手動用NVIDIA追加ZIPはv0.2.5-beta.2の固定資産を再利用します。beta.3のWindows自動取得は実機での追加確認を継続中です。

現在のデスクトップ版はv0.2.5-beta.3、Unity 6000.3.25f1でビルドしています。iOS 0.2.5は公開済みです。0.2.6 (20)は2026-10-11に審査提出済みで、まだ公開版ではありません。新しい進捗表示・ダイアログ修正はその提出版に含まれます。ソースを取得してもインストール済みアプリやBackendは更新されません。

## Backendと同期

デスクトップ版v0.2.5-beta.3では、PCのBackendを起動してAI・追加音声を設定し、端末登録後にキャラクターの人格・記憶・履歴を共有できます。端末内AIを使うだけならBackendを起動する必要はありません。

公開済みiOS版はBackendへの会話接続と端末同期に対応します。以前のTestFlight版で同期ボタンが表示されない場合は、対応版への更新が必要です。アプリのDirect APIキーとBackend側のキーは別々に設定します。[Backendの使い方](BACKEND_CONSOLE.md)。

Aivis・Irodori等の追加音声には別途導入が必要です。Realtimeなどの追加機能は実験的です。すべての端末や組み合わせでの動作を保証するものではありません。

## アバター

VRM 0.x / 1.0を読み込めます。VRChatの衣装メニュー・独自シェーダー・PhysBoneなどが、そのまま再現されるわけではありません。[VRMの準備](AVATAR_IMPORT.md)。

English: the Mac app targets Apple Silicon; the Windows app targets 64-bit Windows. Both download required AI/voice data during setup. iOS requires iOS 26 and includes standard data; the free Japan App Store version is available. E4B is optional on Mac, Windows and iOS. Mac/iOS 0.2.5 and later offer optional Japanese Irodori and English Kokoro voice downloads; iOS 0.2.4 and desktop v0.2.4-beta.2 apps do not include them. On-device Irodori is not supported on Windows/Android. Desktop v0.2.5-beta.3 and the published iOS version support paired character sync. Earlier TestFlight versions without a sync button need an update. No public Android app is available. API keys and charges apply when using OpenAI API. See [Help](HELP.md).

English release status (2026-10-11): Desktop v0.2.5-beta.3 is published; iOS 0.2.5 is public and 0.2.6 (20) is awaiting review. Windows NVIDIA Irodori runs through the Backend, with [Console acquisition](BACKEND_ASSET_DOWNLOADS.md#english); it does not use the mobile CoreML model. Windows native acceptance of the new automatic installer remains pending.
