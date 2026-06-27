# Yui VRM AI Studio

[English README](README.en.md)

**お気に入りのVRMキャラクターを、話し、見て、調べて、覚えるAIエージェントへ。**

Yui VRM AI Studioは、Unity製のVRMアバターアプリとローカルAI補助サービスを組み合わせた、BYOK方式のAIアバタースタジオです。ユーザー自身のAPIキーを使い、会話履歴や設定は基本的に自分のPC/Mac上で管理します。

WindowsとmacOSのDesktop Public Alphaを案内しています。iOS版は現在、個人実機検証用のPersonal Alphaであり、公開配布対象ではありません。

## どこから始めるか

| 対象 | 状態 | 入口 |
| --- | --- | --- |
| Windows Desktop Public Alpha | 公開BYOK alpha | [`docs/SETUP_GUIDE.md`](docs/SETUP_GUIDE.md) |
| macOS Desktop Public Alpha | 実験的な公開alpha | [`macos-public-alpha` branch](https://github.com/Tsubame-chan/YuiVRMAIStudio/tree/macos-public-alpha) / [`docs/MAC_PUBLIC_ALPHA.md`](docs/MAC_PUBLIC_ALPHA.md) |
| iOS Personal Alpha | 個人利用・実機検証中 | 公開配布対象外。方針は [`docs/BUILD_VARIANTS.md`](docs/BUILD_VARIANTS.md) |
| Android | 未実装 | 今後の候補 |

Windows版はこのmainブランチに手順と配布物を置いています。macOS版はまだ実験的なため、専用ブランチで分けて管理しています。

## できること

- VRM 1.0 / VRM 0.x の `.vrm` アバターをAIキャラクターとして表示・会話
- テキストチャット、音声入力、日本語音声応答
- 画像入力 / Vision、画面コンテキスト
- 会話履歴とローカルメモリ
- 天気、イベント、ニュース、場所などの現在情報に対するweb search支援
- OpenAI Realtime APIを使う低遅延会話の実験
- OpenAI Realtime STTとVOICEVOX TTSを組み合わせるRealtime VOICEVOXモード
- リアルタイム翻訳モード

## 仕組み

アプリ本体はUnityで動きます。AI providerとの通信、会話DB、音声生成、画像処理などは、同じマシン上で起動するローカル補助サービスが担当します。

通常の利用では、OS別のセットアップガイドに従って起動スクリプトを使えば十分です。接続先やポートは環境に合わせて変更できるため、このREADMEでは特定の開発環境のURLやIPアドレスを前提にしていません。

## Provider Status

### 主なprovider

- OpenAI: chat / STT / vision / realtime / translation / hosted web search
- VOICEVOX Engine: Japanese TTS runtime

### 実装済み・検証中

- 汎用HTTP TTS adapter: Irodori TTSなど、JSON-in/audio-out型の外部TTSを検証するための実験的な接続口
- Open-Meteo current weather API: web searchとは別に、構造化された現在天気を取得する実験的な接続口
- LM Studio local chat provider: OpenAI互換のローカル `/chat/completions` を使う実験的な接続口
- Grok / xAI chat provider: xAIのOpenAI互換 `/chat/completions` を使う実験的な接続口

### 今後対応候補

- Ollama local LLM
- provider選択UI
- OS標準TTS、LLM内蔵TTS、モバイル向け軽量TTS
- 専用の地図・カレンダー等のAPI連携

## 必要なもの

共通:

- OpenAI APIキー
- `.vrm` アバターを使う場合はVRMファイル
- 日本語音声を使う場合はVOICEVOXまたはVOICEVOX Engine

OS別の詳細:

- Windows: [`docs/SETUP_GUIDE.md`](docs/SETUP_GUIDE.md)
- macOS: [`docs/MAC_PUBLIC_ALPHA.md`](docs/MAC_PUBLIC_ALPHA.md)

### TTS / Irodori検証

日本語音声の標準はVOICEVOXです。Irodori TTSは追加検証中で、OS別に候補を分けています。

- macOS Apple Silicon: [`docs/IRODORI_TTS_PACKAGING.md`](docs/IRODORI_TTS_PACKAGING.md) の MLX VoiceDesign 経路
- Windows NVIDIA: [`docs/IRODORI_TTS_WINDOWS_NVIDIA.md`](docs/IRODORI_TTS_WINDOWS_NVIDIA.md) の Irodori-TTS-Server 経路
- Windows CPU / GPUなし: VOICEVOX推奨

モデル本体やTTSサーバー本体はこのリポジトリには同梱していません。手順と設定例だけを置き、必要なTTS runtimeはユーザー環境で導入します。Irodoriが失敗した場合は `TTS_FALLBACK_PROVIDER=voicevox` でVOICEVOXへ戻せる構成にしています。

## 自分のVRMキャラクターを使う

このalpha版が直接読み込めるのは `.vrm` ファイルです。VRChat SDKのアバター、Unityシーン、Unity prefab、`.unitypackage`、VRChatにアップロード済みのアバターそのものは直接読み込めません。

VRChat用のUnityプロジェクトで管理しているアバターを使いたい場合は、元のBOOTH/配布パッケージに `.vrm` が含まれていないか確認してください。ない場合は、Unity/UniVRMやBlender/VRMのワークフローで別途VRMとして書き出してから読み込んでください。

## Personal / Public / Platformの切り分け

このプロジェクトには、公開用のPublicビルドと、所有者向けのPersonalビルドがあります。

- Public: 外部ユーザー向け。BYOK、公開可能なアバター、公開安全な設定。
- Personal: 個人デバイス向け。私的アバター、個人用bundle ID、所有者向けの初期設定を使う場合があります。

詳しいルールは [`docs/BUILD_VARIANTS.md`](docs/BUILD_VARIANTS.md) にまとめています。Public配布物にPersonal用のアバター、個人用設定、bundle ID、秘密情報を混ぜないことを基本方針にしています。

## Privacy / Data Flow

Yui VRM AI StudioはBYOK方式です。APIキーはユーザー自身のPC/Mac上の `.env` に保存されます。

有効にした機能によって、以下の情報が設定済みの外部AI providerへ送信される可能性があります。

- チャット本文
- 音声入力
- アップロード画像
- スクリーンショット / 画面コンテキスト
- 翻訳対象の音声・テキスト
- web searchが必要な質問内容

以下はローカルに保存されます。

- `.env`
- SQLiteの会話DB
- VOICEVOX生成音声キャッシュ
- ログ

画面コンテキストやリアルタイム翻訳を使う場合は、画面上・音声経路上の機密情報に注意してください。

## Roadmap

### Public Alpha 0.1

- Windows Desktop Public Alphaの安定化
- macOS Desktop Public Alpha branchの整備
- OpenAI chat / STT / vision / web search
- VOICEVOX TTS
- 会話履歴・メモリ
- 画像/画面コンテキスト
- Realtime系の実験機能

### Next

- macOS配布手順の改善
- 鑑賞モードの操作性改善をWindows / macOS / iOSで継続検証
- 汎用HTTP TTS adapterを使ったIrodori TTS等の検証
- provider選択UI
- Grok / xAI API providerの実キー疎通
- LM Studio providerの実機疎通、Ollama local LLM provider
- 専用の天気APIのチャット統合、地図・カレンダー等のAPI連携

### Future

- iOS / Androidの公開版検討
- 外部アプリ音声ブリッジ
- YouTube / ゲーム / 配信 / 通話音声のリアルタイム翻訳
- フィジカルAI / 外部デバイス連携

## 詳細ドキュメント

- Windowsセットアップ: [`docs/SETUP_GUIDE.md`](docs/SETUP_GUIDE.md)
- macOS Public Alpha: [`docs/MAC_PUBLIC_ALPHA.md`](docs/MAC_PUBLIC_ALPHA.md)
- Irodori Windows NVIDIA検証: [`docs/IRODORI_TTS_WINDOWS_NVIDIA.md`](docs/IRODORI_TTS_WINDOWS_NVIDIA.md)
- Irodori optional backend packaging: [`docs/IRODORI_TTS_PACKAGING.md`](docs/IRODORI_TTS_PACKAGING.md)
- TTS benchmark notes: [`docs/IRODORI_TTS_BENCHMARK_20260626.md`](docs/IRODORI_TTS_BENCHMARK_20260626.md)
- API仕様: [`docs/api.md`](docs/api.md)
- 外部情報 / web search方針: [`docs/LLM_EXTERNAL_INFO.md`](docs/LLM_EXTERNAL_INFO.md)
- ビルド種別: [`docs/BUILD_VARIANTS.md`](docs/BUILD_VARIANTS.md)
- リリース確認項目: [`docs/ALPHA_RELEASE_CHECKLIST.md`](docs/ALPHA_RELEASE_CHECKLIST.md)

## トラブルシューティング

初回起動で問題がある場合は、まず使っているOSのセットアップガイドを確認してください。

- Windows: [`docs/SETUP_GUIDE.md#troubleshooting`](docs/SETUP_GUIDE.md#troubleshooting)
- macOS: [`docs/MAC_PUBLIC_ALPHA.md`](docs/MAC_PUBLIC_ALPHA.md)

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
- VOICEVOX/VOICEVOX Engine is not bundled. Install it separately and follow the VOICEVOX terms.
- If you publish generated speech, include the required VOICEVOX credit for the selected voice. The default alpha voice is `VOICEVOX:冥鳴ひまり`.
- ChatdollKit, lilToon, UniVRM, and other Unity packages remain under their respective licenses.
