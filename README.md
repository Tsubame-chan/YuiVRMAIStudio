# Yui VRM AI Studio

[English](README.en.md)

VRMアバターと、文字・音声・画像で会話するUnityアプリです。公開用のデフォルトアバターを使うほか、自分のVRMを読み込めます。

## 配布版と現在のソース

| 対象 | 状態 |
| --- | --- |
| Windows / macOSの実行アプリ | [v0.2.0-beta.5](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/tag/v0.2.0-beta.5)を配布中 |
| このリポジトリのmain | 2026-09-23時点の改修ソース。beta.5より新しく、同じ動作・画面ではありません |
| [ソース検証用スナップショット](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/tag/dev-snapshot-20260923) | 追加の標準VOICEVOXモデルと規約。完成アプリの配布ではありません |
| iOS / Android | 実装・検証用ソースあり。ストア公開版はありません |

`Code → Download ZIP`には実行アプリや大型AIモデルは入りません。アプリを試す場合はbeta.5のReleaseを利用してください。最新ソースの実機検証は継続中です。[確認済み範囲と既知の制限](docs/SOURCE_STATUS.md)を参照してください。

## アプリを試す

beta.5のReleaseから、OSに合うファイルを展開して起動します。

- macOS: `YuiVRMAIStudio_MacOSPublicBeta_v0.2.0-beta.5_macos.zip`
- Windows: `YuiVRMAIStudio_WindowsPublicBeta_v0.2.0-beta.5_windows.zip`

初回ダウンロードでLocal AI / VOICEVOX / OS別Backend bundleを取得します。Releaseのmanifest・分割ZIP・SHA-256はその配布版の組み合わせです。別版のファイルを混ぜないでください。

詳しくは[Windows設定](docs/SETUP_GUIDE.md)、[macOS設定](docs/MAC_PUBLIC_BETA.md)、[大型データの扱い](docs/LOCAL_AI_ASSETS.md)へ。

## 現在のソースにある機能

- VRM 0.x / 1.0の読み込み、キャラクターと衣装の管理、口パク・まばたき・Humanoid待機姿勢の補正。
- `Talk`の短い会話、`Work`の詳しい画面出力と要点の読み上げ。
- テキスト、マイク、画像の入力。モバイルのクリップは写真選択を開きます。撮影は標準カメラで行ってから添付します。PCには画像/カメラ選択があります。
- 端末内Gemma、Direct OpenAI API、Backend経由のAI。AIと音声エンジンは独立して選べます。
- 端末会話履歴、回答の保存・読み上げ、Secret Mode。
- 日本語/English、共通フォント、Soft Gradient背景、接続状態の確認。
- Backend経由のweb検索、音声・画像処理。Realtime会話/翻訳は詳細設定にある実験機能です。

すべての機能がすべてのOS・端末で検証済みという意味ではありません。必要なモデル・ネイティブ実行環境・サービスの有無によって選択肢が変わります。

## AIと音声

| 経路 | 必要なもの |
| --- | --- |
| 端末内AI | 対応するGemmaモデルとOS別ランタイム |
| Direct OpenAI | 設定画面のOpenAI APIキー。PC Backend不要、API利用料あり |
| Backend AI | 起動済みのYui Backendと、そのサービス側の設定 |
| 端末内VOICEVOX | Core・辞書・音声モデル |
| Backend VOICEVOX / AivisSpeech / HTTP TTS | 接続先に導入・設定された音声エンジン |

アプリ内のOpenAIキーは直接接続用です。Backendの`.env`のキーとは独立し、アプリのキーをBackendへ転送しません。端末内AIでも、接続可能なBackendがあればBackendの音声を使えます。

beta.5の初回音声モデルは冥鳴ひまりです。最新ソース用の標準5声データは別スナップショットにあります。追加手順は[ソースの状態](docs/SOURCE_STATUS.md#音声データ)。macOSのAivisSpeech HDはbeta.5 manifestが参照するbeta.3の追加パックとして公開済みで、追加音声のダウンロードから取得できます。IrodoriとWindows用追加音声は同manifestにありません。

## 自分のアバター

最新ソースでは「設定 → キャラクター → アバターを読み込む」からVRMを選択します。「マイキャラクター → 着替え」は人格を保ったまま別の外見へ切り替える機能です。

VRChat用のアバターは、衣装・体型・シェーダーを設定したUnityプロジェクトから既存の変換ツールでVRMへ書き出します。VRChatの独自シェーダー、衣装メニュー、接触ギミック等の完全再現には対応しません。`.unitypackage`・購入ZIP・FBXをそのまま読み込むこともできません。[アバター導入ガイド](docs/AVATAR_IMPORT.md)を参照してください。

## 保存とプライバシー

最新ソースのApple向け実装はAPIキーをKeychainへ保存し、外部AI / Backendへの初回送信前に確認を表示します。Secret Modeも、選んだ外部サービスへの通信自体を停止するものではありません。beta.5へこれらの変更が遡って反映されるわけではありません。[プライバシー説明](docs/PRIVACY.md)を確認してください。

## 開発・検証

Unity 2022.3.62f3を使用しています。モデル重みや生成済みビルド、利用者のアバター・履歴・秘密値はGit管理に含めません。

- [ソースの状態・データ・検証上の制限](docs/SOURCE_STATUS.md)
- [実行環境の対応状況](docs/RUNTIME_SUPPORT.md)
- [品質と検証](docs/QUALITY_AND_VALIDATION.md)
- [公開Playerのアセット検査](docs/PUBLIC_PLAYER_ASSET_VALIDATION.md)
- [API仕様](docs/api.md)

ソース取得だけで全OSの完成アプリが生成できるとは限りません。OS別SDK、ネイティブライブラリ、大型モデル、署名設定を揃えてビルド・実機確認してください。
