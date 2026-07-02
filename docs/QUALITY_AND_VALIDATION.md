# 品質と検証方針

Yui VRM AI StudioはDesktop Public Betaです。まず「Release ZIPを落とした人が、余計な準備なしで起動して試せること」を最優先にしています。その上で、バックエンド、OpenAI API、追加TTS runtimeを入れるほど機能が増える設計です。

## Betaで重視していること

- ReleaseアプリZIPに最小構成のLocal Gemma SLMとLocal VOICEVOXを含めること。
- GitHubの `Code > Download ZIP` が完成アプリではなくソースコードだと明確に案内すること。
- SettingsとHelpの接続状態を同じCapability判定で表示すること。
- `.env`、会話DB、音声キャッシュ、ローカルアセット、巨大モデルをGit履歴へ混ぜないこと。
- Windows/macOSで同じAI/TTS選択思想を保つこと。

## Release時点で確認する項目

- Publication Guard: 公開してはいけないローカル情報や秘密情報が混ざっていないこと。
- Distribution Audit: 公開コピーに必要なREADME、セットアップガイド、backend source、Unity baseline assetsが揃っていること。
- Desktop build audit: Windows/macOSのアプリ成果物、Windows file picker helper、最小Local AI/TTS assetが揃っていること。
- GitHub Release assets: 分割ZIP、sha256、Release本文の復元手順が揃っていること。

## 今後増やしたいテスト

Public Betaとして配布範囲が広がるほど、以下の自動テストを増やしていきます。

| 領域 | 追加したい検証 |
| --- | --- |
| `/health`, `/config`, `/providers/status` | APIレスポンス基本形、秘密情報を出さないこと |
| `/chat` | APIキーなし、provider failure、重複request_id |
| `/audio/{filename}` | traversal拒否、拡張子拒否、存在しないファイル |
| `/vision`, `/stt` | 空ファイル、サイズ超過、content-type拒否 |
| setup scripts | dry-runまたは最低限の存在確認 |
| Unity UI | AI/TTS選択肢のReady/Unavailable表示、Auto Select fallback |

## Provider/modelについて

外部AI providerやモデル名は、サービス側の提供状況によって動作が変わることがあります。Betaでは、設定画面とHelp画面の接続状態を優先して確認してください。将来的には「APIキーありだが疎通未確認」「モデル名未検証」「Backend未接続だが選択肢として案内中」のような状態表示をさらに細かくしていきます。
