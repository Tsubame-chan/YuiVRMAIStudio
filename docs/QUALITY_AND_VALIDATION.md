# 品質と検証方針

2026-09-22: 公開版はbeta.5のままです。現在の改修版はMac検証とiOS候補の検証段階で、App Store公開の合格判定は出していません。[ストア公開の受入基準](STORE_RELEASE_GATE.md)を満たしてから配布します。

Yui VRM AI StudioはDesktop Public Betaです。まず「Release ZIPを落とした人が、余計な準備なしで起動して試せること」を最優先にしています。その上で、バックエンド、OpenAI API、追加TTS runtimeを入れるほど機能が増える設計です。

## Betaで重視していること

- ReleaseアプリZIPを起動すると、初回ダウンローダーが最小構成のLocal Gemma SLM、Local VOICEVOX、OS別Backend bundleを取得・検証・展開できること。
- GitHubの `Code > Download ZIP` が完成アプリではなくソースコードだと明確に案内すること。
- SettingsとHelpの接続状態を同じCapability判定で表示すること。
- `.env`、会話DB、音声キャッシュ、ローカルアセット、巨大モデルをGit履歴へ混ぜないこと。
- Windows/macOSで同じAI/TTS選択思想を保つこと。

## Release時点で確認する項目

- Publication Guard: 公開してはいけないローカル情報や秘密情報が混ざっていないこと。
- Distribution Audit: 公開コピーに必要なREADME、セットアップガイド、backend source、Unity baseline assetsが揃っていること。
- Desktop build audit: Windows/macOSのアプリ成果物、Windows file picker helper、初回取得manifestが揃っていること。
- GitHub Release assets: アプリZIP、初回取得用Local AI/TTS/Backend bundle、sha256、Release本文の案内が揃っていること。

## 自動テストと実機確認

Backendには接続状態・設定・会話ID・音声ファイル・入力検査などの回帰テストがあります。Unityでは経路選択、履歴、アバター、ファイル選択、同意拒否・取消、モバイル必須データの検査を行います。これらの成功を、新規端末でのインストール・権限・音声品質・長時間動作の確認に読み替えないでください。

公開用ソースにはモデルやBackend実行環境が同梱されません。クリーンなソースのテストは、開発者の端末にインストール済みのモデルやworkerがあることを前提にしません。配布時には、必要なデータを別途準備した実Playerでも確認します。

## Provider/modelについて

外部AI providerやモデル名は、サービス側の提供状況によって動作が変わることがあります。Betaでは、設定画面とHelp画面の接続状態を優先して確認してください。接続状態やAPIキーの存在だけで、実際の応答やモデル利用権が確認済みとは扱いません。
