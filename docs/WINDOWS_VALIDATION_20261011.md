# Windows動作確認・修正記録

対象：2026-10-10〜11、Desktop v0.2.5-beta.1とGitHubのソース。Windows、RTX 5070 Ti、ドライバー32.0.16.1714、LiteRT-LM 0.17.0。特定の1台での実機確認と自動テストであり、Windows全機種・全設定を通した保証ではない。

修正ブランチ：`fix/windows-stt-cpu-decoding`。[PR #42](https://github.com/Tsubame-chan/YuiVRMAIStudio/pull/42)。mainは保護されているためPR経由。push、マージ、新しい配布バイナリの発行は別の状態として扱う。

## 更新から検証までの経緯

1. 旧v0.2.4系からv0.2.5-beta.1へアプリとBackendを更新。取得ファイルをハッシュ確認し、モデル・人格・履歴などの永続データを保持した。
2. 利用者の約3秒の「こんにちは、お元気ですか」が長い不正な認識になったため、同じWAVを使ったGPU/CPU、精度、サンプラー、キャッシュの比較を実施。
3. GPU STTの不安定さを確認。FLOAT32やgreedy単独では改善する例があっても十分でなかった。CPU固定に加え、書き起こし専用指示、入力・出力検査を追加。利用者の実マイクで「こんにちは、お元気ですか」が正しく認識された。
4. 短い挨拶だけでなく、長い文章、命令・計算を含む発声、英語、ステレオ、低音量、末尾の無音、30秒などを試験。ローカル入力を30秒へ限定するソース変更を追加。API入力は同じ上限へ変更していない。
5. Backend GUIのGitHub収録を確認し、実際に起動。設定・記憶・履歴・作業・音声などの画面を確認。CSVの実集計と成果物保存を確認。
6. 日本語・空白を含む名前の公式サンプルVRMを取り込み、標準／追加アバター切り替え、保存、アプリ終了・再起動後の復元を確認。
7. Windows向けIrodori V4.1を追加導入。Python依存導入失敗と初期音声の422を実際に発見し修正。CUDAで3声を合成し、Backend GUIの試聴まで確認。
8. 任意4Bを公式取得元とハッシュで確認し、実行。利用者の要望によりWindows設定でも選択可能に変更。実アプリから選択・保存して会話に使えることを確認。
9. Windows固有条件だけでなく共通Backendコードを調査。SoundStretchのOS混同とLM Studioの空本文成功扱いを回帰テストで確認・修正。Macへの影響と未確認条件を別MDへ整理。

## 実機で確認できた範囲

| 項目 | 実施と結果 |
| --- | --- |
| 2B会話・標準読み上げ | 日本語の指定文「かくにんできました。」を応答。GPU会話約4.5秒、VOICEVOX Core生成約1.3秒。STTのCPU化で会話GPUを無効にしていない |
| 音声認識 | 利用者の実マイク再試験成功。合成音声による比較・回帰結果をSTT fixtureへ保存。内容一致と句読点揺れを区別 |
| VRM0取り込み・切り替え | UniVRM公式Alicia。日本語・空白のパス、約7.9MB。選択は利用者がファイルダイアログで実施。表示とロードログ、一覧保存、標準↔追加切り替え、再起動復元を確認 |
| Backend GUI | `backend/app/static/admin/` に既に収録済み。今回のWindowsインストールでも管理画面を使用。Macローカル未push変更の有無は未確認 |
| CSV作業 | `分類,金額` のA100/B200/A50を集計。A:件数2・合計150、B:件数1・合計200。Backend再起動後も成果物を表示し、GUIから保存したCSVの値を確認。入力を外部LLMへ送らない |
| Irodori V4.1 | 正式パッケージのSHA確認、Python3.11・CUDA依存導入、RTX5070 Tiで起動。bright_natural/gentle_friend/calm_naturalの3声がHTTP200、48kHz・monoの有効WAV。温まった要求約2.5〜4.7秒。Backend GUIに試聴プレイヤー表示 |
| E4B | 公式モデル3,659,530,240bytes、SHA-256 `0b2a8980ce155fd97673d8e820b4d29d9c7d99b8fa6806f425d969b145bd52e0`。worker初回約34.5秒、後続約6.4〜8.5秒。Windowsアプリで4B選択・設定保存後「17＋25」→「42」、ログの `model=core_text` が成功、約6.9秒 |

## 修正内容と根拠

### ローカルSTT

`scripts/yui_desktop_inference.py`：Windows STTをCPUへ固定。書き起こし専用system指示、決定的サンプラー、WAVの実フレーム・PCM16・長さ確認、無音拒否、過剰出力・出力上限到達の拒否、日本語空白整理を追加。単にCPUに替えるだけでは、音声中の命令へ回答する別の問題が残ることを確認した。

Unity側：ローカル音声30秒、自動停止と表示、録音位置0の拒否、APIからローカルへフォールバックする場合の30秒検査。Windows実行workerへ修正を適用し、実マイク成功。**GPU内部の精度破綻の原因は未確定**であり、「GPUは常に壊れる」「ドライバーのこの処理が原因」と断定していない。

### Irodori導入と初期音声

`scripts/setup_irodori_v4_windows.ps1`：固定依存SentencePiece0.1.99にWindows Python3.12 wheelがなく、ソースビルド失敗。Python3.11を指定し、既存／実体の実行ファイルを明示して同期。uvが実体展開後にminor版junctionの作成に失敗する条件にも、完全版ディレクトリのPython3.11を検査して復帰する。依存導入失敗時はBackend設定を変更しない。実導入成功に加え、ダウンロードなしの実プロセス回帰テストで既存・junction失敗後の復帰・不適切な版の拒否を確認。

同梱ファイルを直接変更するとパッケージ整合性検査が正しく拒否することも確認し、同梱の元ファイルを復元した。今回の導入は更新したリポジトリのinstallerから、ハッシュ確認済みパッケージを指定して実行した。既存Release ZIP自体を再発行したわけではない。

導入後、展開済みパッケージ36ファイルのSHAを再検査し、取得ZIPのSHAも一致したため、重複していた一時ZIPだけを削除した。**1,214,515,046bytes（約1.13GiB）を回収**。導入済みIrodori・E4B・標準モデル・利用者の永続データは保持した。以前拒否された旧ステージング／実行キャッシュの削除とは別の対象である。

`backend/app/core/irodori_presets.py`：サーバー形式へMLX専用パラメータを渡す422を修正。形式ごとに初期値を作り、3声とも能力解決できるテストを追加。MLXの従来パラメータは保持。Windowsのインストール済みBackendにも適用し、GUIの保存済み音声試聴成功を確認。

### 共通Backendの境界条件

- `backend/app/providers/http_tts.py`：SoundStretchをOSに合った実行ファイルから自動探索。Mac条件も模擬したテストで、別OSのバイナリを選ばないことを確認。
- `backend/app/providers/lmstudio_chat.py`：空本文・choicesなし・thinkingのみ・tool_callsのみ等を成功扱いせず、既存の提供元エラー処理へ渡す。生の応答JSONを会話へ代用しない。正規化で本文が消える場合も拒否。通常の日本語応答・テキストブロックは維持。
- 自動テストのWindows移植性：日本語fixtureを明示UTF-8で読み、固定Unix一時パスを隔離tmpへ変更。POSIX chmod検査とMac shell実行をWindowsへ無理に適用しない。これはWindows ACLやMac実機を検証済みにする変更ではない。

この3つの共通Backend修正は現在のインストールにも適用し、Backend再起動後のreadyを確認した。外部LM Studioの実サーバー接続は未実施であり、そこは回帰テストの確認結果と区別する。

### Windowsの4B選択

`local_ai_model_packs.json` と `YuiLocalAiModelRegistry.cs` の標準登録でE4BをWindowsにも許可。現在のインストール済みアプリは外部StreamingAssetsの登録JSONを読めるため、そこへ同じJSONを配置して設定画面に表示し、実会話まで確認した。ソースのfallbackも変更済みだが、Unity Playerを再ビルドして配布したわけではない。

## 自動テストと証跡

Backend・scriptsのWindows実行テストは、隔離したPython3.12環境でpytestを実行。MCPのHTTP／stdio等の任意依存もテスト環境だけに追加した。実アプリの依存環境をテスト用パッケージで置き換えていない。

実行例（repo root、必要な依存を準備した環境）：

```powershell
$env:PYTEST_DISABLE_PLUGIN_AUTOLOAD = '1'
$env:PYTHONPATH = Join-Path $PWD 'backend'
python -m pytest backend/tests tests -p anyio.pytest_plugin -k 'not trio' -q
```

最終全体実行は **316 passed、14 skipped、1 warning（45.35秒）**。Trioは今回の環境では実行対象外。Mac shell 12件、Mac配布処理1件、opt-in native STT1件は通常のWindows全体実行ではスキップ。STT nativeは別途実行した結果が既存fixtureにある。警告はPython3.12のaudioop非推奨。ローカル `.windows-validation/pytest.xml` に全体実行の結果を保持。

ローカルの `.windows-validation/` にE4B結果、Irodoriの3声の結果・WAV、pytest XML、GUIスクリーンショットを保持。利用者の絶対パス・実データ・大きなモデル・鍵・環境ファイルは公開リポジトリへ追加しない。

## 未解決・未実施の範囲

| 項目 | 理由・次の作業 |
| --- | --- |
| Unity C#変更のコンパイル、30秒UIの新版配布、Editorテスト | 一致するUnity6.3 Editorがこの端末にない。現行配布アプリの旧60秒表示とworker30秒拒否の差が残る。正しいEditorでビルド・録音境界試験が必要 |
| GPU STTの内部原因 | CPU回避は実証済みだが、GPUカーネル／ドライバーの特定には未到達。別GPU・別版を比較するまでGPU復帰しない |
| VRM1、Avatar Bridge ZIP、画像ファイル取り込み全条件 | VRM0実経路は成功。その他の形式・破損・キャンセル・二重操作・巨大ファイルを実機で網羅していない。既存EditorテストをMac/Unity環境でも実行する |
| ファイルダイアログの所有者・操作性 | Windows helperのダイアログを操作ツールが取得できず、選択は利用者が実施。helperのShowDialogにownerを渡していない点は改善候補。ファイル選択失敗とは断定しない |
| Irodori初回オフライン・合成速度・音質全般 | 初回にSilentCipher用データ取得あり、コールドロード約44.5秒、初回短文合成約50.7秒。現行HTTP TTSの60秒timeoutに余裕が少ない。完全オフライン初回や低速回線、長文、主観的音質の保証は未実施 |
| 保存済み音声の形式変更、独立TTSの環境変数 | 初期声の修正は既存データの自動移行ではない。Windows起動スクリプトのuv版指定警告と、PYTHONHOME/PYTHONPATHを継承した環境は追加確認候補 |
| 外部APIの実会話・STT・Vision・Realtime | APIキー未設定。モック試験を実接続成功と扱わない |
| モバイル同期、Mac実機、LM Studio/Ollama等の実サーバー | 現在の端末だけでは全経路の実機確認不可。共通テストの成功と実サーバー互換を区別する |
| 古いステージングと実行キャッシュの削除 | 一部の以前の削除操作は自動承認レビューに拒否されたため未実施。拒否対象を別経路で削除していない。モデル・ユーザーデータと一時取得物を区別する |

Backend単独VOICEVOX engineのポート50021は今回起動していないが、アプリのVOICEVOX Coreは実合成成功。両者を混同しない。新規任意モデルの取り込み機能や外部LLM互換拡張は今回の実装範囲ではなく、[別の検討メモ](LOCAL_LLM_EXTENSION_NOTES.md)に提案を記録した。

Macに渡す確認箇所は [macOS引き継ぎ](MACOS_VALIDATION_HANDOFF_20261011.md) を参照。テストで未発見の条件も疑いとして記載しているが、未再現の条件を確定バグとして数えていない。
