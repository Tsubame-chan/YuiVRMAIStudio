# Windowsを基準にした修正・横断検証（2026-10-11）

Windows ZIPの文書とPR42を起点に、正本へ選択的に取り込んで追加修正した。WindowsのRTX5070 Ti/LiteRT-LM 0.17.0での記録を基準とし、Macの結果は共有処理の副作用確認として区別する。ZIPには実際の修正コード・pytest XML・認識音声がなく、実機結果はWindowsセッションの文書による報告である。

## 修正内容

- Windows STTのCPU化、決定的な認識設定、書き起こし専用指示、WAV・無音・過剰出力検査を維持。会話GPU、MacのGPU→CPUフォールバックは維持。
- ローカル録音30秒、API入力60秒を維持。実際のWAV時間でもローカル上限を検査。負の/過大なchunkサイズで循環・領域外読み取りしない。Macの代替録音を使う時も実WAV時間を送信する。
- 録音停止位置0を短時間の録音全体として送信しない保護を全OSへ適用。30秒・60秒到達の正当な全バッファは維持。
- Unityの追加STTテストでVision delegateの引数が1つ欠けたコンパイルエラーを修正。
- WindowsのE4B対応をJSONとソース既定値の両方へ反映。
- IrodoriのWindows Python3.11選択・junction失敗からの復旧を維持し、既存検出時にも実インタープリターを検査。TTSとBackendの起動、端末内AI workerが別のPYTHONHOME/PYTHONPATHを継承しないよう修正。TTS起動は同期済みvenvのpython.exeを直接使う。
- IrodoriのServer/MLX形式に合う初期値を生成。保存済みの未編集な標準声だけを接続方式へ適応し、調整済みの声と保存ファイルを読み取り時に変更しない。
- Irodoriの初回ロード対策としてBackend read180秒/connect10秒、アプリ側TTS210秒。HTTP fallbackの既定100秒が先に切れないよう、既存の各要求の取消・期限へ統一。
- SoundStretchをOSとUnix実行権限で選択。LM Studioの空本文・thinkingのみ・tool_callsのみ・不正形式を成功会話にしない。content先頭のthink領域を除き、本文がなければ既存502へ渡す。
- 声ID bright_naturalの説明を「説明による自由生成」から「登録済みの参照声」に訂正。Windowsの検証・将来LLM案のMDを公開ツリー生成対象へ追加。

## 確認結果

- Mac Python全体: 355成功、5skip、3subtests成功。skipはWindows PowerShell native4件とWindows native STT1件。Trioは既存環境に合わせて対象外。audioopのPython3.13廃止警告あり、今回のPython3.12で失敗ではない。
- Unity6000.3.25f1 EditMode: 608成功、5ignored、0失敗。子プロセスへ不正なPython環境を継承させない実起動試験も成功。
- 私有資産を除いた新しいツリーでWindowsとMac Playerをビルド。packed asset privacy監査PASS。Windowsはコンパイル/資産検査であり新候補をWindows実機で操作した意味ではない。
- Mac MLX V4.1実合成: 3声の48kHz mono WAV生成、約5.6〜6.8秒。音質の今回の主観的採点ではない。
- MacのE2B GPU STT: 合成した通常文を約3.46秒で書き起こし。命令を含む試験では計算に回答せず「2 + 2 は いくつ です か ?」を出力。ただし先頭句「計算してください」が抜けており、完全な逐語精度の保証にはしない。Windowsの設定をMacの出力に合わせて変更していない。
- Windows BackendのPython依存は既存0.2.5配布の主要バージョンと一致（LiteRT0.17.0等）。Irodoriは別のPython3.11と公式uv.lockを使う。
- 修正版Windows NVIDIA追加ZIPを作成。モデル・codec・公式固定server・3参照声を保持し、変更したinstaller/start scriptのfiles.jsonとZIP SHAを再作成。未公開。

## 公開前・Windowsでの確認

ソースの修正はレビュー可能。既存Release ZIPをソースpushだけで更新した扱いにはしない。新規配布はWindows/MacアプリだけでなくBackend実行データ、Irodori追加ZIP、manifestのURL/サイズ/SHA/バージョンを同時に更新する。新規タグへmanifestをpinして再ビルドし、現候補の検証用v0.2.5-beta.1表示とは区別する。

Windowsでは新候補の新規導入と既存データ更新、即停止/30秒/取り消し、E2B/E4B再選択、3声のコールド/ウォーム合成、STT日本語/英語を確認する。Windows GPU固有の根本原因は未特定。PowerShellの追加ケースはMacでnative実行できない。Irodoriの初回SilentCipher取得は残り、インストール完了だけで完全オフライン初回を保証しない。Serverの固定版は同機能を任意取得する実装なので、透かしを独自に無効化していない。[固定版の取得処理](https://github.com/SesameAILabs/silentcipher/blob/d46d7d0893a583d8968ab3a6626e2289faec9152/src/silentcipher/server.py#L458-L470)。

VRM pickerは共通の二重起動guardがあり、インポート中の二重要求も拒否する。Windows ShowDialog所有者、VRM1/大容量ZIP/キャンセル後の実操作は候補実機の試験項目とする。今回の資料だけから障害と断定して挙動を置換しない。任意GGUF取り込み、Ollama専用UI、CSV機能削除は未確定の拡張案として据え置く。接続時の空本文・思考漏れ防止を先に直した。

iOS/Androidにも共通の録音・WAV・Backend TTS待機修正を反映したが、新しいiPhone/Android Playerの実機受入やApp Store再提出は実施していない。既存承認済みiOS0.2.5(19)は今回の修正を含む版ではない。

## v0.2.5-beta.2への反映

本記録の修正をデスクトップ新版へ反映。BackendのCSVを通常ナビから外し、旧URLと成果物を維持する。任意LLMは次の大型更新の方針として文書へ記録し、今回の実装済み機能と区別する。初期の検証候補v0.2.5-beta.1表示とは別に、新規タグへmanifestをpinしてWindows/Macをビルドする。iOSの再提出はこのGitHub更新の対象外。
