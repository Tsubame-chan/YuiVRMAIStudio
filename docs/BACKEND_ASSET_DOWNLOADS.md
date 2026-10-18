# Backendの追加音声を取得する

[現在のBackend](DOWNLOADS.md)で利用できます。Consoleに取得操作がない場合はBackendを更新するか、Windowsの[手動導入](IRODORI_TTS_WINDOWS_NVIDIA.md)を利用します。

対応版のBackendを起動し、同じPCでConsoleの「AIと音声 → 読み上げ → Irodoriの音声を追加」を開きます。開始前にモデルの取得容量と必要な空き容量を確認し、「取得する」を選んでください。GitHubやHugging Faceを手動で開く必要はありません。

取得中の表示はパーセントです。ファイル検証・依存ソフトの導入・音声の準備は別の工程として表示します。画面を移動しても処理を続け、下部のバナーから詳細・中止・再試行を選べます。対応するアプリにもBackend側の状況を表示します。中止・再試行は同じPCのConsoleで行います。

取得後は実際の音声生成を確認してから「準備できました」と表示します。「声の一覧を更新」で、明るく元気・親しみやすく優しい・落ち着いたトーンの3声を追加します。アプリの「Backendの保存した声を選ぶ」で割り当てられます。未保存の声の編集は自動で破棄しません。既存の提供元・選択した声・`.env`・APIキーは保持します。

## 対応する実行環境

- Apple Silicon Mac：Python 3.12のBackend、V4.1 Small MLX 8bit、モデル約1.39 GB、空き容量目安5 GiB。音声エンジンの依存関係を別の仮想環境へ導入します。対応するmlx-audioは0.5.8に固定しています。
- Windows NVIDIA：V4.1 Small INT8、追加ZIP約1.21 GB、空き容量目安10 GiB。Git for WindowsとCUDA 12.8対応のNVIDIAドライバが必要です。専用Python 3.11環境へ依存を導入します。CPU/AMD GPU向けの追加データではありません。

モデルと各構成要素の条件は開始画面の配布元リンクと配布パッケージのREADME/ライセンスを確認してください。参照音声はPCのV4.1 FP16で作成した合成3声です。モバイル用CoreMLの重みはBackendの導入には使用しません。

## 中断・失敗した場合

取得済みのファイルは再試行で再利用します。未取得の残りを取得し、SHA-256検証に失敗したファイルは再取得します。Backend終了時はこの機能が起動した音声サーバーだけを終了し、次の起動時に導入済み音声をバックグラウンドで準備します。

導入後に生成できない場合は「データを再検証・修復」を使います。既に追加音声を使用している場合、修復中はその音声エンジンの再起動が必要になります。声の選択・編集は保持します。

管理対象は通常 `backend/data/addons` です。管理画面の設定保存先を変更した場合は、その隣の `addons` に保存します。モデル・キャッシュ・個別の依存環境は公開GitやBackend配布ビルドへ含めません。

## English

The [current Backend](DOWNLOADS.md) supports background Irodori acquisition from Backend Console → AI and speech → Read-aloud → Add Irodori voices. Review the download size and required disk space before starting. Progress is shown as a percentage; verification, dependency installation and voice preparation are separate phases. You can use other screens while downloading. Open the bottom banner for details, cancellation or retry. Connected compatible apps display Backend progress; manage cancellation and retry in the Console on that PC.

Apple Silicon Mac uses V4.1 Small MLX 8bit (about 1.39 GB), a separate Python 3.12 environment and mlx-audio 0.5.8; allow about 5 GiB free. Windows NVIDIA uses V4.1 Small INT8 (about 1.21 GB archive), a separate Python 3.11 environment, Git for Windows and a CUDA 12.8-compatible driver; allow about 10 GiB free. This Windows option does not support CPU/AMD GPUs. Its fixed package is reused from v0.2.5-beta.2; native Windows acceptance of the new automatic installer remains pending.

Readiness includes actual synthesis. Refresh the voice list to add the three synthetic reference presets (bright, gentle and calm), then assign a saved Backend voice in the app. Existing settings, saved voices and API keys are preserved. Retry reuses verified files. Repair rechecks the installation and may restart an active added TTS engine. The Backend stops only servers it owns. User-installed models and environments stay under `backend/data/addons` (or beside a custom settings store) and are excluded from source and distribution bundles. These Backend weights are separate from the mobile CoreML download.
