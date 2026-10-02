# Yui VRM AI Studio

[English](README.en.md) · [ヘルプ・FAQ](docs/HELP.md) · [不具合報告](https://github.com/Tsubame-chan/YuiVRMAIStudio/issues)

**自分のVRMアバターとAIチャット。**

お気に入りのアバターを会話相手にして、雑談や相談、ちょっとした作業を一緒に。性格や話し方は文章で自由に設定できます。会話の合間には、好きな角度からアバターを鑑賞できます。

<p>
  <img src="docs/images/avatar-chat.jpg" width="300" alt="自分のアバターとAIチャット：実際に発話するUnityちゃん">
  <img src="docs/images/avatar-viewer.jpg" width="300" alt="360度回転・拡大でアバターを鑑賞">
</p>

画像は付属Unityちゃんを使った実アプリのMac描画です。端末・ウィンドウサイズによって画面配置が変わります。

## ダウンロード

| 対象 | 入手先・状態 |
| --- | --- |
| macOS / Windows | [v0.2.4-beta.1](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/tag/v0.2.4-beta.1) |
| iPhone / iPad | 0.2.4 (14)をApp Store審査へ提出済み。承認後、日本で無料公開予定。[App Store予定ページ](https://apps.apple.com/jp/app/id6815341780)（公開後に利用可） |
| Android | 開発用ソースあり。配布・実機受入は未完了 |

デスクトップ版はReleaseからOSに合うアプリZIPを展開して起動します。初回は確認画面からAI・音声・実行環境データを取得します。Wi-Fiと十分な空き容量をご用意ください。取得後の端末内AIとの会話にインターネット接続は不要です。

**`Code → Download ZIP`は開発者向けのソースです。実行アプリや大型モデルは含みません。**

導入手順: [macOS](docs/MAC_PUBLIC_BETA.md) / [Windows](docs/SETUP_GUIDE.md)。macOS配布物はApple Silicon向けの実行環境を使用し、署名・公証は未整備です。Windows版もベータとして提供します。

## あなたの会話相手を作る

- **好きな外見へ。** VRM 0.x / 1.0を読み込み、キャラクターや衣装を切り替えられます。まばたき・口パク・対応する揺れを表示します。
- **性格と応答を調整。** キャラクターごとの性格・口調、AI共通の回答方針、ローカルモデル別の追加指示を組み合わせます。ローカルAIではコンテキスト、生成・推論上限、温度などもTalk/Work別に設定できます。
- **会話を重ねる。** 記憶はキャラクターごとに端末へ保存。再起動や端末内AI／APIの切替後も参照し、確認・編集・削除できます。別キャラクターとは共有しません。
- **用途に合わせる。** Talkは短い会話、Workは詳しい説明や作業支援。テキスト・マイク・画像入力、回答の保存・読み上げに対応します。
- **デジタルフィギュアとして。** 鑑賞モードで360°回転・拡大。日英UIと、ヘルプから再表示できる4ページのチュートリアルがあります。

<details>
<summary>性格・モデル設定とオフライン会話の画面を見る</summary>

<p>
  <img src="docs/images/customization.jpg" width="300" alt="キャラクターの性格とAIの応答設定">
  <img src="docs/images/offline-chat.jpg" width="300" alt="標準2Bと任意4Bモデルの選択">
</p>

</details>

## AIと音声を選ぶ

| 会話の方法 | 特徴・必要なもの |
| --- | --- |
| 端末内AI | 標準Gemma 4 E2B。macOS / iOSではE4Bを設定から任意取得でき、回答品質と引き換えに容量・負荷・応答時間が増えます |
| OpenAI API | より高品質な会話に推奨。APIキー、通信、API利用料金が必要です。ChatGPTの有料プランとは別です |
| PC Backend | 任意の拡張経路。設定済みのTTS/STT、検索などを利用できます。CLIでの管理と実験的機能を含みます |

AIと音声エンジンは独立して選べます。端末内音声は日本語VOICEVOXの標準5声。英語UIはありますが、英語専用TTSはまだ同梱していません。Backendで設定した追加の音声エンジンも利用できます。

アプリのOpenAIキーは直接接続用で、Backendの`.env`とは別です。アプリのキーをBackendへ転送しません。デスクトップの初回取得にはローカル推論workerを含む実行環境も入りますが、端末内AIはBackendサーバーの起動を必要としません。

## アバターを持ち込む

「設定 → キャラクター → アバターを読み込む」からVRMを選びます。「マイキャラクター → 着替え」は、人格を保ったまま外見だけ変える機能です。

Unity／VRChat向けアバターは、設定済みのUnityプロジェクトからVRMへ書き出してください。購入ZIP、`.unitypackage`、FBXは直接読み込めません。独自シェーダー・衣装メニュー・PhysBone等の完全再現は保証しません。利用権のあるモデルをご使用ください。[アバター導入ガイド](docs/AVATAR_IMPORT.md)へ。

## 記憶とプライバシー

シークレットモードでは選択中のキャラクターの既存記憶を参照できますが、新しい会話は履歴・記憶へ保存しません。解除後に内緒話を引き継ぐこともありません。記憶は端末間では同期しません。

外部AIを選んだ場合は、会話や関連する性格設定・記憶などを送信します。シークレットモードも外部送信を止める機能ではありません。Apple版のAPIキーはKeychainに保存します。詳しくは[プライバシー](docs/PRIVACY.md)。

**AIの回答と記憶の参照には誤りがあります。** ローカルモデルは主語の混同や知識・計算の誤答が残ります。大きいモデルや設定の変更も正確さを保証しません。重要な内容は確認してください。

## 開発・詳しい仕様

Unity **2022.3.62f3** / UniVRM **0.131.2**。Unity 6への移行はまだ行っていません。公開版の標準アバターはUnityちゃんで、私用アバター・キー・会話データを配布しません。モデル、生成ビルド、端末別SDK・署名設定はソースとは別に必要です。

- [ソースと検証の現在地](docs/SOURCE_STATUS.md)
- [OS別の対応と制限](docs/RUNTIME_SUPPORT.md)
- [モデル・音声データ](docs/LOCAL_AI_ASSETS.md)
- [会話・キャラクター・記憶](docs/CONVERSATION_IDENTITY.md)
- [品質と公開アセット監査](docs/QUALITY_AND_VALIDATION.md)
- [情報・データの配信方針](docs/DISTRIBUTION.md)
- [API仕様](docs/api.md)

すべてのOS・端末で同じ受入が完了しているわけではありません。配布物とソースの検証範囲は上記の資料で区別しています。
