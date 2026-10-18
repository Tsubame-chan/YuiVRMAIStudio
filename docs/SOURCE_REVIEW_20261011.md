# 2026-10-11 ソース横断レビュー / Source review

> 過去の検証記録です。記載の版・成否・未確認事項は検証当時のものです。現在の[配布ファイル](DOWNLOADS.md)と[機能対応](RUNTIME_SUPPORT.md)は別途確認してください。

main `2f86bfc198a82009fdb1c4dbc579bc8c31c790d7`に対する外部レビューの5項目を実装と照合し、正本で修正しました。この文書は次回配布候補のソースを説明します。既に配布済みのDesktop v0.2.5-beta.3、審査提出済みiOS 0.2.6 (20)には、この後続修正は含まれません。

| 対象 | 修正と影響 |
| --- | --- |
| Android音声認識 | 取消時にJavaの結果通知を待たず、C#の待機を完了。要求IDで古い結果を遮断し、権限確認中の二重開始・取消にも対応 |
| iOS OS音声認識・読み上げ | 要求IDとネイティブ取消APIを追加。実行中のOS処理へ停止を届け、待機を解除。完了後のコールバックによる書換えを遮断 |
| Mac/Windows Backend HTTP TTS | 音声後処理をworkerへ移動。SoundStretch等の待機で非同期イベントループ全体を止めない。取消後の出力ファイル公開も防止 |
| Desktop Backend起動 | health期限切れを明示的な失敗にし、未起動を成功済みとして固定しない。呼出元の取消も維持 |
| ビルド・配布工程 | Unity・Backend・パッケージ・監査の既定番号を[共通メタデータ](RELEASE_VERSIONING.md)へ集約。公開ソース用テンプレートも同じ値を参照 |

追加確認では、取消と開始の順序、古い要求の遅延通知、OS権限確認中の状態、ネイティブの完了/取消競合を扱いました。README日英と導入・対応環境・音声取得・記憶/プライバシー文書の古い記載も更新しています。モデル資産の固定URL、互換性最低バージョン、過去の実機試験の証拠は現在のリリース番号へ一律置換していません。

## 検証範囲

Mac上のUnity EditMode全体は637成功・5 ignored・失敗0。Backendとroot Python試験は390成功・6 skipped・失敗0（追加したSwift状態試験を含む）。新しく生成したsanitized公開ソースでも、取消・起動待機・リリース番号の14試験が成功し、公開ソース監査に合格。公開文書29件のローカル参照133件はすべて有効です。iOSの実機向けSwift型チェックは成功。ネイティブ要求状態を実コードで動かし、取消前後・競合500回・ID隔離・期限切れを検証しました。

Android/iPhoneの実機で、今回変更したOS音声APIの取消を再受入した結果ではありません。Windowsの新版自動取得の実機受入も引き続き別の確認です。外部API・モデル推論等のopt-in試験は通常の自動試験から除外されます。第三者依存の全内部コードや全設定組合せの無欠陥を証明するレビューではありません。

## iOSへの反映方針

2026-10-11の判断: 今回のOS音声取消修正は次回アップデートへまとめます。停止時にネイティブの完了/期限を待つ問題への修正であり、通常の会話・音声生成の品質を変更するものではありません。既存の審査提出済み0.2.6 (20)を差し替えず、次回は新しいbuild番号で作成し、iPhone上で録音/合成中の取消・直後の再実行・権限確認中の取消を確認してから提出します。

## English

Five findings from the review of main `2f86bfc` were confirmed and fixed in the canonical source: Android STT cancellation, iOS native STT/TTS cancellation, blocking Backend audio postprocessing, silent Backend health timeout, and inconsistent release defaults. Additional fixes isolate stale Android results by request ID and serialize permission/start/cancel state. Shared release metadata also reaches sanitized public templates.

These are subsequent source fixes for a future distribution; published Desktop v0.2.5-beta.3 and already-submitted iOS 0.2.6 (20) do not contain them. Japanese and English README/setup/support/download/privacy documentation was reconciled without rewriting historical test evidence or pinned model URLs.

Validation on Mac: 637 Unity EditMode passes, 5 ignored; 390 Python passes, 6 skipped; no failures. The fresh sanitized public tree passed 14 focused Unity tests and the source distribution audit; 133 local links across 29 public documents resolve. Swift device-SDK type checking passed. Production native request-state tests cover early/active cancellation, 500 completion races, request isolation and timeouts. Native Android/iPhone acceptance of changed OS speech cancellation and Windows acceptance of the new automatic installer remain separate, pending checks.

The iOS cancellation fixes are planned for the next update, after iPhone acceptance of cancel/retry and permission cancellation. Already-submitted build 20 is retained; a future submission requires a new build number.
