# macOS Public Alpha

このページは、macOS版のYui VRM AI Studioを試す人向けの入口です。

macOS版はまだ実験的です。最新のmacOS公開alphaは、main branchではなく以下のブランチを見てください。

- GitHub branch: [`macos-public-alpha`](https://github.com/Tsubame-chan/YuiVRMAIStudio/tree/macos-public-alpha)
- English guide: [`docs/MAC_PUBLIC_ALPHA.en.md`](MAC_PUBLIC_ALPHA.en.md)

## 現在の位置づけ

| 項目 | 状態 |
| --- | --- |
| 公開状態 | Public Alpha branch |
| 対象 | 主にApple Silicon Mac |
| ローカルサービス | FastAPI |
| AI provider | OpenAI BYOK |
| TTS | ローカルVOICEVOX Engine |
| VRM | `.vrm` 読み込み |
| iOSとの関係 | iOS Personal buildとは別扱い |

macOS版は、Windows版と同じローカルサービス設計を使います。つまり、OpenAI APIキーやVOICEVOX、会話DB、音声キャッシュはローカル環境側で管理します。

## 必要なもの

- Apple Silicon Mac
- Homebrew
- Python 3.12+
- OpenAI APIキー
- VOICEVOX.app、またはVOICEVOX Engine
- Unity Hub / Unity Editor 2022.3 LTS

開発・ビルド検証ではUnity `2022.3.62f3` を使っています。古いstatus文書には `2022.3.6f1` の記録も残っていますが、現在のcanonical Unity projectは `2022.3.62f3` を前提に扱ってください。

## セットアップ

まず、macOS public alpha branchを取得します。

```bash
git clone https://github.com/Tsubame-chan/YuiVRMAIStudio.git
cd YuiVRMAIStudio
git checkout macos-public-alpha
```

すでにclone済みの場合:

```bash
git fetch origin
git checkout macos-public-alpha
```

HomebrewとPythonを用意します。

```bash
brew install python@3.12 git git-lfs
git lfs install
```

ローカルサービスを初期化します。

```bash
PYTHON_BIN=/opt/homebrew/bin/python3.12 ./scripts/setup_backend_byok_macos.sh
```

`.env` を開き、OpenAI APIキーを設定します。

```bash
open -e .env
```

最低限必要な設定:

```env
OPENAI_API_KEY=sk-...
```

## 起動

ローカルサービスだけ起動:

```bash
./scripts/run_backend_macos.sh
```

ローカルサービスとVOICEVOXをまとめて起動:

```bash
./scripts/start_local_services_macos.sh
```

Finderから起動したい場合:

```text
Start_Yui_Local_Services.command
```

停止:

```bash
./scripts/stop_local_services_macos.sh
```

または:

```text
Stop_Yui_Local_Services.command
```

## VOICEVOX

macOSの起動スクリプトは、主に以下を探します。

```text
/Applications/VOICEVOX.app/Contents/Resources/vv-engine/run
~/Applications/VOICEVOX.app/Contents/Resources/vv-engine/run
```

別の場所にある場合は、`VOICEVOX_ENGINE_EXE` を指定してください。

```bash
export VOICEVOX_ENGINE_EXE="/path/to/VOICEVOX.app/Contents/Resources/vv-engine/run"
```

VOICEVOXがなくてもテキストチャットは動きますが、日本語音声再生にはVOICEVOX Engineが必要です。

## アプリ本体

macOS Public Alpha branchには、macOS向けのUnity build / packaging pathがあります。ビルド済み `.app` が配布物に含まれている場合は、それを起動してください。

開発環境でUnityから開く場合は、リポジトリ内の `unity/` をUnity Hubで開きます。

```text
unity/
```

## 配布前チェック

macOS Public Alphaの配布物を作ったら、公開用の安全確認とmacOS artifact確認を同じ監査で実行します。

```bash
./backend/.venv/bin/python scripts/audit_distribution_release.py --require-builds --platform macos
```

この監査は、公開してはいけないローカルDB、音声キャッシュ、私的アバター関連ファイル、Unity生成物に加えて、以下のどちらかが存在することを確認します。

```text
builds/YuiVRMAIStudio_MacOSAlpha_v0.1.0-alpha.1/Yui VRM AI Studio.app
builds/YuiVRMAIStudio_MacOSAlpha_v0.1.0-alpha.1_macos.zip
```

WindowsとmacOSの配布物を同時に確認する場合:

```bash
./backend/.venv/bin/python scripts/audit_distribution_release.py --require-builds --platform all
```

## 現在の注意点

- macOS版はまだ実験的な配布です。
- 署名・notarizationは今後整備が必要です。現時点の監査は、公開安全性とartifact有無の確認です。
- VOICEVOXは同梱していません。別途インストールしてください。
- iOS Personal向けの接続設定や個人用初期値をmacOS Public版へ混ぜないでください。
- Public版には私的アバターや個人用bundle IDを含めない方針です。

## 関連ドキュメント

- Main README: [`../README.md`](../README.md)
- English README: [`../README.en.md`](../README.en.md)
- Build variants: [`BUILD_VARIANTS.md`](BUILD_VARIANTS.md)
- macOS setup history: [`MAC_SETUP.md`](MAC_SETUP.md)
- API: [`api.md`](api.md)
- External info / web search policy: [`LLM_EXTERNAL_INFO.md`](LLM_EXTERNAL_INFO.md)
