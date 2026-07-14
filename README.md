# Yui VRM AI Studio

[English README](README.en.md)

**お気に入りのVRMキャラクターを、話し、見て、調べて、覚えるAIエージェントへ。**

Yui VRM AI Studioは、自分のVRMキャラクターをデスクトップ上のAIアバターとして動かし、テキスト・音声・画像・カメラを使って会話や作業を頼めるアプリです。VRChatで使っているキャラクター、創作キャラ、推しアバターを、ただ眺めるだけではなく、話し、覚え、調べ、PCでの作業を手伝い、外出時もモバイルから同じ環境へつながる存在にすることを目指しています。

現在公開しているのは、Windows / macOS向けのデスクトップ版ベータです。アプリ本体ZIPを展開して起動すると、初回ダウンロードでLocal Gemma SLM、Local VOICEVOX、Yui Backend bundleが揃います。手動でバックエンドサーバーやPython環境を作らなくても試せます。より高品質な会話、リアルタイム会話/翻訳、会話DB、追加TTSを使いたい場合は、OpenAI APIキーやアプリ内の追加音声ダウンロードを使います。

## 主な特徴

- 自分の `.vrm` キャラクターを画面に表示し、そのキャラと会話できます。
- メッセージ入力、音声入力、画像入力、選択中のカメラ画像を会話に使えます。
- `Talk` は短く自然に会話し、`Work` は詳しい作業結果を画面に出して結論だけを読み上げます。
- ローカルメモリと会話履歴を使い、継続的に会話できるAIアバターとして扱えます。
- バックエンドを用意しなくても、基本的な会話と日本語音声を試せます。
- 日本語音声はVOICEVOXを標準の音声エンジンとして扱い、AivisSpeech HDやIrodori TTSなどを任意で追加できます。

## 対応状況

| 版 | 状態 | 案内 |
| --- | --- | --- |
| Windows デスクトップ版 | 公開ベータ | [`docs/SETUP_GUIDE.md`](docs/SETUP_GUIDE.md) |
| macOS デスクトップ版 | 公開ベータ | [`docs/MAC_PUBLIC_BETA.md`](docs/MAC_PUBLIC_BETA.md) |
| iOS / Android | 公開候補として検証中 | 現時点ではデスクトップ版を優先 |

実行ファイルと大型Local AI/TTSデータは [GitHub Releases](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases) で配布します。GitHubの `Code > Download ZIP` はソースコード用で、実行ファイルや大型AI/TTSデータは含まれません。

現在の配布版は `v0.2.0-beta.3` です。通常はWindows/macOSのアプリ本体ZIPだけをダウンロードしてください。その他の大きなデータは、初回起動時にアプリが自動で取得します。

## インストール

GitHub Releasesから、お使いのOSに対応したアプリ本体ZIPをダウンロードして展開し、起動してください。

- macOS: `YuiVRMAIStudio_MacOSPublicBeta_v0.2.0-beta.3_macos.zip`
- Windows: `YuiVRMAIStudio_WindowsPublicBeta_v0.2.0-beta.3_windows.zip`

初回起動時にダウンロード画面が表示されます。開始すると、基本動作に必要なLocal AI/TTSデータとYui Backend bundleが自動で揃います。ユーザーがPythonやバックエンドを手動で構築する必要はありません。

Releaseにある `YuiVRMAIStudio_LocalAIAssets_DesktopMinimum` や `YuiVRMAIStudio_BackendBundle_*` は、通常はアプリが自動取得するデータです。手動で落とす必要があるのは、ソースからビルドする場合、個別検証する場合、またはダウンロード済みデータを手動復旧したい場合だけです。

`.sha256` はダウンロード破損を確認したい人向けです。ソースコードを読みたい、または自分でビルドしたい場合は `Code > Download ZIP` や `git clone` を使います。ソースZIPには実行ファイルや大型モデルは含まれません。

AivisSpeech HDやIrodori TTSなどの高品質音声は任意の追加ランタイムです。なくてもアプリは動きますが、バックエンド接続時の声の選択肢を増やせます。リアルタイム会話/翻訳、会話DB、Backend TTSを使う場合、PC版は同梱または初回取得したYui Backendを優先して使います。

TTS配布の方針として、初回必須データはVOICEVOXの最小構成に絞り、AivisSpeech HDやIrodori TTSは高品質な追加音声パックとして扱います。対応している追加音声は、Settingsの `Additional Voices` からアプリ内で取得できます。現時点のReleaseではmacOS向けAivisSpeech HD add-onを配布します。このパックにはAivis本体、選定済みAIVMXモデル、Aivisが必要とする日本語BERT依存が含まれるため大きめです。Windows向け追加音声やIrodori TTSは検証済みランタイムが揃い次第、同じ導線へ追加する方針です。

## 主な機能

- VRM 1.0 / VRM 0.x の `.vrm` アバターをAIキャラクターとして表示・会話
- テキストチャット、音声入力、日本語音声応答
- 画像入力 / Vision、選択中カメラの画像理解
- 短い会話用の `Talk` と、詳しい画面出力を作る `Work`
- 会話履歴とローカルメモリ
- 天気、イベント、ニュース、場所などの現在情報に対するweb search支援
- OpenAI Realtime APIを使う低遅延会話の実験
- OpenAI Realtime STTとVOICEVOX TTSを組み合わせるRealtime VOICEVOXモード
- Auto SelectによるBackend優先 / Localへの自動切り替え
- Local Gemma SLMによるオフライン・低通信環境向けの軽量会話
- ローカルVOICEVOX、Backend VOICEVOX、AivisSpeech HD、Irodori TTSの状態表示と選択
- リアルタイム翻訳モード

## アプリの構成

アプリ本体はUnityで動きます。AIプロバイダーとの通信、会話DB、音声生成、画像処理などは、同じマシン上で起動するローカル補助サービス、またはアプリ内蔵のローカル実行基盤が担当します。

バックエンドを起動すると、高品質な会話、リアルタイム会話/翻訳、会話DB、Backend TTSを使えます。PC版は完全版の母艦として、同じマシン上のYui Backendを自動起動する方向です。モバイル版は単体でも動く companion client ですが、VPNなどで自宅PCのYui Backendへ接続すると、外出先でも自宅のAI環境を使えます。VPN自体にYuiの追加利用料はありませんが、携帯回線を使う場合はSIM側のデータ通信量は発生します。

## 開発状況と今後の予定

### 現在のPublic Beta

- Windows / macOS向けのデスクトップ版ベータを公開中
- OpenAI chat / STT / vision / web search
- VOICEVOX TTS
- Local Gemma SLMとDirect APIによる代替経路
- Auto SelectによるBackend優先 / Localへの自動切り替え
- Backend VOICEVOX / AivisSpeech HD / Irodori TTSの選択
- 会話履歴・メモリ
- 画像入力 / Vision、選択中カメラの画像理解
- Realtime系の実験機能

### 検証中の機能

- 汎用HTTP TTS adapter: Irodori TTSなど、JSON-in/audio-out型の外部TTSを検証するための実験的な接続口
- Open-Meteo current weather API: web searchとは別に、構造化された現在天気を取得する実験的な接続口
- LM Studio local chat provider: OpenAI互換のローカル `/chat/completions` を使う実験的な接続口
- Grok / xAI chat provider: xAIのOpenAI互換 `/chat/completions` を使う実験的な接続口
- 共通Capability判定: Help画面と設定画面でBackend / Local / Direct APIの利用可否を同じ基準で表示

### 次に進めたいこと

- VCCのUnityプロジェクトから自分のVRChatアバターを簡単に書き出す `Yui Avatar Bridge`
- 画像、画面範囲、ウィンドウ、ファイルを一つの入力欄へ渡し、説明・翻訳・要約・下書きを完成させる作業導線
- PCに頼んだ長い処理をモバイルで確認し、同じキャラクター・記憶・作業を引き継ぐ端末連続性
- 記憶した内容をユーザーが確認・修正・固定・削除できるメモリ管理
- 初回ダウンロードの復旧性、アプリ更新確認、Windows / macOS配布品質の継続改善

### 将来的な構想

- iOS / Androidの公開版検討
- OS標準STT/TTSの横展開と品質検証
- 外部アプリ音声ブリッジ
- YouTube / ゲーム / 配信 / 通話音声のリアルタイム翻訳
- フィジカルAI / 外部デバイス連携

## 必要なもの

最小構成:

- GitHub ReleasesのWindows / macOS向けベータ配布物
- `.vrm` アバターを使う場合はVRMファイル

追加機能:

- OpenAI APIや高品質な画像理解/STTを使う場合はOpenAI APIキー
- リアルタイム会話/翻訳、会話DB、Backend TTSを使う場合は初回ダウンロード済みのYui Backend bundle
- 日本語音声を拡張する場合はVOICEVOX Engine、AivisSpeech HD、Irodori TTSなどの外部TTSランタイム

OS別の詳細:

- Windows: [`docs/SETUP_GUIDE.md`](docs/SETUP_GUIDE.md)
- macOS: [`docs/MAC_PUBLIC_BETA.md`](docs/MAC_PUBLIC_BETA.md)

### Git cloneとRelease配布物の違い

GitHubの通常リポジトリには、巨大なGemmaモデル本体、音声モデル、音声辞書、生成済みビルドを入れません。これらは容量とライセンス境界の都合で、GitHub Releasesのベータ配布物側に分けます。Releaseのアプリ配布物は最小構成だけで動きます。追加音声やソースビルド用アセットは必要な人だけ取得してください。詳しくは [`docs/LOCAL_AI_ASSETS.md`](docs/LOCAL_AI_ASSETS.md) を参照してください。

### TTS / Irodori検証

日本語音声ではVOICEVOXを標準の音声エンジンとして扱います。PC版では、バックエンド未接続でも使えるローカルVOICEVOXを優先し、バックエンド接続時はより細かく調整できるVOICEVOX EngineやAivisSpeech HD、Irodori TTSを選べる方針です。Irodori TTSは追加検証中で、OS別に候補を分けています。

- macOS Apple Silicon: [`docs/IRODORI_TTS_PACKAGING.md`](docs/IRODORI_TTS_PACKAGING.md) の MLX VoiceDesign 経路
- Windows NVIDIA: [`docs/IRODORI_TTS_WINDOWS_NVIDIA.md`](docs/IRODORI_TTS_WINDOWS_NVIDIA.md) の Irodori-TTS-Server 経路
- Windows CPU / GPUなし: VOICEVOX推奨

大型モデル本体やTTSサーバー本体は、ライセンスと容量の都合でGit管理には入れません。必要なランタイムはユーザー環境で導入するか、GitHub Releasesの配布物として分けて扱います。Irodoriが失敗した場合は `TTS_FALLBACK_PROVIDER=voicevox` でVOICEVOXへ戻せる構成にしています。

Unityアプリ側のBackend URLは、VOICEVOXやIrodoriのURLではなく、常にYui backendを指定します。通常は `http://127.0.0.1:8000`、iPhoneなど別端末から同じPC/Macのバックエンドへ接続する場合は `http://<PCまたはMacのLAN/VPN IP>:8000` です。VOICEVOXやIrodoriのURLは `.env` と起動スクリプト側で管理します。

## 自分のVRMキャラクターを使う

このベータ版が直接読み込めるのは `.vrm` ファイルです。VRChat SDKのアバター、Unityシーン、Unity prefab、`.unitypackage`、VRChatにアップロード済みのアバターそのものは、現在の配布版では直接読み込めません。

VRChat用のUnityプロジェクトで管理しているアバターを使いたい場合は、元のBOOTH/配布パッケージに `.vrm` が含まれていないか確認してください。ない場合は、Unity/UniVRMやBlender/VRMのワークフローで別途VRMとして書き出してから読み込んでください。この手順をVCC内の数クリックへ短縮する `Yui Avatar Bridge` を優先開発項目にしています。設計は [`docs/YUI_AVATAR_BRIDGE_ARCHITECTURE.md`](docs/YUI_AVATAR_BRIDGE_ARCHITECTURE.md) を参照してください。

## Privacy / Data Flow

Yui VRM AI StudioはBYOK方式です。APIキーはユーザー自身のPC/Mac上の `.env` に保存されます。

有効にした機能によって、以下の情報が設定済みの外部AIプロバイダーへ送信される可能性があります。

- チャット本文
- 音声入力
- アップロード画像
- ユーザーが選んだ画像 / カメラ画像
- 翻訳対象の音声・テキスト
- web searchが必要な質問内容

以下はローカルに保存されます。

- `.env`
- SQLiteの会話DB
- VOICEVOX生成音声キャッシュ
- ログ

画像理解やリアルタイム翻訳を使う場合は、画像・カメラ・音声経路上の機密情報に注意してください。

## 詳細ドキュメント

- Windowsセットアップ: [`docs/SETUP_GUIDE.md`](docs/SETUP_GUIDE.md)
- macOS Public Beta: [`docs/MAC_PUBLIC_BETA.md`](docs/MAC_PUBLIC_BETA.md)
- Irodori Windows NVIDIA検証: [`docs/IRODORI_TTS_WINDOWS_NVIDIA.md`](docs/IRODORI_TTS_WINDOWS_NVIDIA.md)
- Irodori optional backend packaging: [`docs/IRODORI_TTS_PACKAGING.md`](docs/IRODORI_TTS_PACKAGING.md)
- API仕様: [`docs/api.md`](docs/api.md)
- 外部情報 / web search方針: [`docs/LLM_EXTERNAL_INFO.md`](docs/LLM_EXTERNAL_INFO.md)
- ローカルAI/TTS asset配布: [`docs/LOCAL_AI_ASSETS.md`](docs/LOCAL_AI_ASSETS.md)
- 品質と検証方針: [`docs/QUALITY_AND_VALIDATION.md`](docs/QUALITY_AND_VALIDATION.md)
- 製品方針と優先機能: [`docs/PRODUCT_DIRECTION_20260714.md`](docs/PRODUCT_DIRECTION_20260714.md)
- VCCアバター導入設計: [`docs/YUI_AVATAR_BRIDGE_ARCHITECTURE.md`](docs/YUI_AVATAR_BRIDGE_ARCHITECTURE.md)

## トラブルシューティング

初回起動で問題がある場合は、まず使っているOSのセットアップガイドを確認してください。

- Windows: [`docs/SETUP_GUIDE.md#troubleshooting`](docs/SETUP_GUIDE.md#troubleshooting)
- macOS: [`docs/MAC_PUBLIC_BETA.md`](docs/MAC_PUBLIC_BETA.md)

よくある原因:

- ローカル補助サービスが起動していない
- `.env` に `OPENAI_API_KEY` が設定されていない
- VOICEVOX Engineが見つからない、または起動していない
- Windows版で `YuiFilePickerHelper.exe` がアプリ本体と同じフォルダにない

開発者向けのAPI詳細や診断手順は [`docs/api.md`](docs/api.md) を参照してください。

## License And Credits

Project code is released under the MIT License. See [`LICENSE`](LICENSE).

Third-party assets and libraries keep their own licenses.

- UnityChan assets are distributed under the Unity-Chan License Terms.
- VOICEVOX/VOICEVOX Engine follows the upstream VOICEVOX terms.
- If you publish generated speech, include the required VOICEVOX credit for the selected voice. The default beta voice is `VOICEVOX:冥鳴ひまり`.
- ChatdollKit, lilToon, UniVRM, and other Unity packages remain under their respective licenses.
