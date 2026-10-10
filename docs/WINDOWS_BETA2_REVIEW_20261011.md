# Windows beta.2 最新版の再検証（2026-10-11）

## 結論

Windowsで問題だった短いマイク入力の長文化は、最新版では再現しなかった。利用者の約3.8秒の実録音、録音済み日本語・英語音声、実際の2B/4B推論と音声生成で正常動作を確認した。今回の修正は原因に対応するガード・実行環境の分離として妥当。確認した範囲で新しい重大なアプリ動作不良は発見していない。

再検証時点では、既存Backendへ配布ファイルを上書きする更新手順に、古いPythonパッケージ情報が残る問題があった。この端末では旧情報をバックアップへ退避して解消。その後の共通コード・手順の修正については末尾の追記を参照。すべての条件でバグがないという結論ではない。

## 対象と取得方法

- GitHub: <https://github.com/Tsubame-chan/YuiVRMAIStudio>
- main / `v0.2.5-beta.2`: `dc6db20df87caef9779d112118c0bb7b393d5002`。検証終了時の再fetchでも同一。
- 配布: <https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/tag/v0.2.5-beta.2>
- 前回の共有修正 `733dac4` 以後の31ファイル、追加341行・削除88行を対象に差分を確認。
- 元のチェックアウトには利用者のdocs移動・削除等があるため、その変更を触らず別worktree `YuiVRMAIStudio-latest` で確認。検証時の `fix/windows-beta2-validation` は修正を追加して `fix/backend-venv-update` へ改名。
- ソースだけでなく、公開WindowsアプリZIPとWindows Backend ZIPを取得し、GitHub公開SHA-256と照合して実行した。

| 配布物 | バイト数 | SHA-256 |
| --- | ---: | --- |
| Windows app ZIP | 57,967,261 | `719564715dd838729c6db91cafae4ec0bc473d02595c773bd3e145aabac90d20` |
| Windows Backend ZIP | 60,956,134 | `198568bbcb0a3e5fbb5c8c08f488768cb6441885f49ad233ddb397f771481ffe` |

公開manifest・SHA256SUMSも取得した。大きなモデルとIrodori追加ZIPは再ダウンロードせず、前回導入済みのものを使用した。したがって「Irodori追加ZIP beta.2を新規インストールした」検証には該当しない。

## 今回の確認結果

| 確認 | 結果と範囲 |
| --- | --- |
| Python自動テスト | `backend/tests` と `tests`：326 passed / 14 skipped / failures・errors 0、46.8秒。anyioのasyncio側を実行し、trio側は選択対象外。 |
| 日本語ネイティブSTT回帰テスト | opt-in実行：1 passed / 18 deselected。最新版workerと配布Python、導入済みE2Bを実際に使用。 |
| 実マイク入力 | 利用者が最新版のMicから「こんにちは、お元気ですか」を録音・送信し、「全く問題ない」「TTSもうまく動作」と回答。画面上の認識結果も「こんにちは、お元気ですか？」。 |
| 実録音ログ | elapsed 3,794ms、WAV duration 3,760ms、180,480 samples / 48kHz、361,004 bytes。認識13文字、STT完了3,291ms。短い発声が60秒に膨らんでいない。 |
| 英語ネイティブSTT | 既存fixtureを最新版の配布worker/Pythonで処理。「Hello, how are you?」、約1.68秒。 |
| E2B実推論 | 最新版の配布worker/Pythonで17+25を質問し「42」、約5.25秒。 |
| E4B実推論 | 最新版Playerで「かくにんできました」と指定し、同じ回答。モデル選択済み状態を維持して実会話を確認。 |
| 標準VOICEVOX Core | 最新版Playerの実会話で音声生成。実マイク後の応答も音声生成され、利用者が正常と確認。 |
| Backendの保存済み3声 | 新Backendからbright / gentle / calmすべて音声生成に成功。brightはGUIから実行、gentle約2.99秒、calm約2.43秒。後者のWAVは48kHz mono、4.16秒 / 3.84秒。導入済みの温まったIrodoriサーバーを使用。 |
| Backend GUI | 新版をreloadし、モデル一覧・保存した声・試聴の結果を確認。エンドポイント設定を保存し直す操作は不要だった。 |
| CSVナビ変更 | 通常ナビにCSV項目なし。旧 `#work` に直接移動すると実験機能の説明と、前回の完了済み集計・保存ボタンが表示される。 |
| アバター・履歴の互換性 | 日本語・空白を含むAliciaの保存状態と会話が復元。利用者が手動で閉じた後に最新版を再起動し、同じアバター・会話の復元を再確認。手動終了は障害に数えない。 |
| WAV・停止位置の境界 | 下記13チェックに成功。Unity全体のEditorテストをWindowsで実行した結果ではない。 |
| Backend更新後 | 差分43ファイルのSHA一致、0 mismatches。旧メタデータ退避後も `/health` はok。 |

14 skippedのうち13件はmacOS/POSIX、Swift、macOSパッケージツールに依存するもの。残り1件は通常スイートで未指定のopt-inネイティブSTTで、別途Windowsで実行し成功した。Mac固有の13件をWindowsで成功扱いしていない。

### 境界条件の確認方法

最新版C#ソースから次の純粋メソッドだけを抽出・コンパイルし、WindowsのPowerShellから実行した。`TryReadWavInfo`、`ChunkIs`、`ReadWavUInt32`、`ResolveStoppedSamplePosition`。

- 正常な3秒・30秒・31秒WAVで、サンプルレートと実時間を正しく算出。
- 途中で切れたWAV、`uint.MaxValue`のチャンク長、不整合なbyte rateを拒否。
- 3秒で停止したposition=0を60秒分に置き換えない。
- 完了した30秒クリップは正しく全長扱い。
- 録音中のposition=0を全長扱いしない。
- 位置がクリップ長を超えた場合の上限を確認。

さらに、公開Playerの `Assembly-CSharp.dll` をMono.Cecilで読み、WAV時間パーサー・停止ガード・30,000ms制限が収録されていることを3チェックで確認した。これは配布DLLの収録確認であり、そのUnity依存メソッドをDLLから直接実行したテストではない。

## 修正の妥当性

### 音声認識と録音

- Windows STT専用のCPU実行、文字起こし専用の指示、greedy decodingを維持。チャットのGPU利用を一律に止めていない。
- ローカルSTTの30秒制限を、申告durationだけでなく実際のWAV長にも適用。Backend障害時のローカルfallbackにも適用される。
- チャンクサイズをunsignedで読み、長いオフセットで境界を確認する修正は、符号オーバーフロー・破損ファイルへの対処として妥当。
- 停止済み・position=0だけを根拠に全クリップを送らない修正は、WindowsとmacOSの共通経路にも反映されている。
- Direct APIへ正常に送る経路には、ローカル専用30秒制限を押し付けていない。

元のWindows GPU挙動の根本原因（特定ドライバー、GPU演算、LiteRT-LM側の不具合等）は依然未特定。CPUに切り替えたら正常になったことと、その低層原因を断定できることは別である。今回の再検証でもGPU STTを再有効化して原因特定したわけではない。

### 実行環境とTTS

- worker子プロセスの外来 `PYTHONHOME` / `PYTHONPATH` 除去と、macOSの必要な自前Python homeだけ設定する構成は、別サービスのPythonを混ぜないために妥当。
- Windows Irodoriの実インタープリターが3.11か確認する修正と、既存venvのPythonを直接起動する変更を確認。Windowsのresolver異常ケースは自動テストにも含まれる。
- IrodoriのBackend read timeout 180秒・connect 10秒、アプリTTS待機210秒、HttpClient fallbackで既定100秒に切られない対処を確認。
- 保存した声のServer/MLX変換は、未調整の既定プリセットだけを対象とし、利用者の調整と保存ファイルを勝手に書き換えない。双方の変換・保存不変・調整済み維持のテストが通った。
- LM Studio回答先頭のthinkブロック除去と空回答拒否のテストが通った。通常本文にあるタグ等を一律削除していない。

タイムアウト値は全処理が必ず210秒以内に終わるという保証ではない。UnityWebRequestからHttpClientへの再試行は別の待機枠を持ち、httpxのread timeoutも全工程の絶対期限ではない。無応答・遅いストリーム・取り消しの実機ネットワーク試験は今回未実施。

## 新たに確認した問題：Backend上書き更新で古いメタデータが残る

分類：更新経路の整合性問題。今回、実際の会話・STT・TTSの故障にはつながっていない。

再現条件：`docs/SETUP_GUIDE.md` の「既存Backendを更新する」に従い、新配布のファイルを旧Backendの同じ場所へ上書きコピーする。コピーは既存の不要ファイルを取り除かないため、名前にバージョンが入る古い `.dist-info` ディレクトリが残る。

実測例：

| パッケージ | 上書き後のimportlib.metadata | 実際にimportされたモジュール |
| --- | --- | --- |
| json5 | 0.15.0 | 0.17.3 |
| absl-py | 2.5.0 | 2.5.1 |

同様の重複はabsl-py、fastapi、json5、opentelemetry-api、pydantic、pydantic_core、SQLAlchemy、tomli、wcwidthの9パッケージ、旧メタデータ10ディレクトリで確認した。

対照実験：公開beta.2 Backend ZIPを空の場所に展開した環境にはメタデータの重複がなく、json5とabslのモジュール版・メタデータ版も一致した。配布ZIPの新規展開自体は正常であり、旧環境へファイルを足す更新方法に原因がある。

### この端末への対処

新配布にある現行メタデータのSHA一致を確認し、新配布に存在しない同一パッケージの旧メタデータだけを非公開バックアップへ移動した。`.env`、DB、保存した声、モデル、Irodori環境、現行パッケージを削除していない。移動なので元に戻せる。

対処後は重複0。json5 / absl / fastapi / pydantic / SQLAlchemyのモジュール版とメタデータ版の一致を確認し、Backend healthも正常。

### GitHub側に残る課題と推奨方針

今回のローカル退避は一般向けアップデーターの実装ではない。GitHubの更新案内のまま別端末で上書きすれば再発し得る。また、メタデータ以外の旧ファイルが残るケースまで解決したものではない。

利用者の設定・DB・任意導入の音声環境を保持しつつ、配布Python runtimeと配布venvを整合した単位で入れ替える更新方法を用意するのが望ましい。まず空フォルダーへ展開・検証し、サービス終了後に既存runtimeをバックアップへ退避し、起動失敗時に戻せる方式を検討する。未知の利用者追加ファイルを名前だけで一括削除する方法は採らない。

Windowsで実証した問題であり、macOSの上書き更新で同じ重複が発生することは今回の端末では実証していない。ただし、フォルダーへ足すだけの更新なら同じ原因があり得るため、Mac側でも旧版から新版への移行とパッケージ情報の一致を確認する。

アプリのソース修正・GitHubへの追加pushは今回実施していない。今回の追加変更は検証用ファイル、この報告、インストール済みBackendへの差分適用と旧メタデータ退避。

## 未検証・引き継ぐ範囲

- このWindows端末ではUnity 6.3 Editorの全テストを実行していない。Mac側の記録にある608 passed / 5 ignoredは、今回Windowsで再実行した数字ではない。
- Macアプリ、iOS/Android実機、App Store版への反映を今回再確認していない。共通コードの変更だけで配布済みモバイル版に入ったとは扱わない。
- 新しいIrodori追加ZIPの新規導入、完全にキャッシュを消した状態のcold start、初回SilentCipher取得・完全オフライン初回起動は今回未検証。3声は既存導入・warm状態の確認。
- 実マイクの即停止・30秒自動停止・取り消し・録音途中のデバイス切断は、新PlayerのUIで一式再試験していない。停止位置とWAVの境界は上記のコード検証で確認。
- VRM1、新しいVRMのファイルピッカー操作、大容量・破損VRM、pickerキャンセル後の再選択は今回未再試験。前回Windowsで成功したVRM0の読み込み状態は新Playerでも復元した。
- Direct APIへの課金を伴う実リクエスト、外部LM Studio/Ollamaとの新規接続、全認証・通信エラー条件、長時間の安定性を網羅していない。
- 任意LLM登録・インポートは従来どおり拡張案。今回使えるようになった機能と混同しない。

## ローカル証跡

workspace直下の `.windows-validation/`：

- `beta2-release.json`：GitHub Releaseの取得情報。
- `beta2-pytest.xml`：326 passed / 14 skippedの全スイート結果。
- `Test-Beta2Boundaries.ps1`：C#境界と公開DLL収録確認の13チェック。
- `check_beta2_native.py` / `beta2-native-results.json`：英語STTと2Bの実推論。
- `beta2-live-tts.json` / `beta2-yui-irodori-*.wav`：保存した声の実生成結果。
- `beta2-tts.png`：最新Backend GUIで試聴結果が表示された画面。
- `beta2-update-plan.json`：差分43ファイル / 631,857 bytes。適用後全43件SHA一致。
- `beta2-metadata-duplicates.json`：対処前の9パッケージの重複。
- `Repair-Beta2Metadata.ps1` / `beta2-metadata-repair-plan.json`：今回の限定的な退避処理と移動先。
- `beta2-backup/`：利用者設定・DB・変更前ファイル・旧メタデータの非公開バックアップ。GitHubへアップロードしない。

起動中の最新版Playerは `.windows-validation/beta2-app/YuiVRMAIStudio_WindowsPublicBeta_v0.2.5-beta.2/` のもの。通常利用していた旧アプリのインストールフォルダーを上書きしたわけではない。Backendは既存の保存領域を差分更新して使用している。

## 追記：両OSの共通更新経路を修正

利用者の追加依頼に基づき、手動更新の案内だけでなく `YuiLocalAiAssetDownloader.VerifyAndInstallArchive` に同じファイル単位の上書き経路があることを確認した。Macの更新案内も同じ手順だった。Windows固有の原因ではない。

- Backend bundleの `.venv` を環境全体として切り替える共通トランザクションを追加。旧版の `.dist-info` と廃止モジュールが残らない。
- 必須ファイルとWindowsネイティブライブラリを、旧環境を動かす前に検査。
- 他の配布ファイルの更新失敗・キャンセル時は旧環境と既存ファイルを復元。復旧に失敗した場合はstagingのバックアップを消さない。
- `.env`、DB、保存した声、追加音声環境、別フォルダーのモデルは環境置換の対象外。
- Pythonを含まないソースのみのbundleと、Backend以外のアセットは従来の環境を保持。ディレクトリのリンクを検出した場合は置換を拒否してリンク先を保護する。
- Windows/Mac日本語・Mac英語ガイドと同梱READMEを、環境を混ぜない[共通更新手順](BACKEND_UPDATE.md)へ修正。Mac向け案内に混入していたWindows NVIDIA向けの手順も除いた。

回帰確認：旧 `dc6db20` のインストーラーは、Windows形式・Mac形式のどちらでも旧メタデータが残るため、新しい回帰テストに失敗した。修正後は同じアーカイブ処理・manifest・ledger・回帰fixtureをコンパイルし、新しい14件と既存のモデル導入4件、計18件のNUnitテストをWindows上の独立実行で成功させた。Unityネットワーククライアントを外し、OS判定・ネイティブファイル一覧の参照をテスト用に置き換えており、Unity EditorやMac実機のテスト結果とは区別する。

対象は、両形式の通常更新と途中失敗の復旧、不完全な環境、必須ファイル欠落、Windows DLL欠落、ソースのみのbundle、Backend以外のアセット、復旧失敗後の再試行、事前キャンセル、Windowsのルート・入れ子junctionのリンク先保護。日本語・空白のある一時パスで実行し、設定・DB・保存した声・モデルの内容も保持した。既存の単一ZIP・分割ZIP・SHA検査・ledger記録・失敗時のモデル保持も成功。

加えて、SHA照合済みの公開Windows beta.2 Backend ZIPを修正後のインストーラーで一時環境へ実際に導入した。旧json5メタデータが消え、配布Pythonからjson5・fastapi・pydanticをimportでき、モジュール版とメタデータ版が一致した。既存 `.env` とテスト用DBは保持した。検証用の展開先は終了後に整理し、実際の利用者の環境には追加の入れ替えを行っていない。

公開済みbeta.2アプリZIPの実行コードは変更していない。この共通インストーラー修正を使うには、GitHubの修正を取り込んだWindows/Mac Playerの再ビルド・新配布が必要。手動の安全な更新手順は既存配布にも適用できる。Mac実機とUnity Editor全スイートでの最終受入は引き継ぐ。
