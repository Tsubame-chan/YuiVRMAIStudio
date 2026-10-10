# Backend更新時のPython環境 / Updating the Backend Python environment

WindowsとmacOSの共通事項です。古い `backend/.venv` へ新版のファイルを足すだけでは、旧バージョンの `.dist-info` と配布から削除されたモジュールが残ります。Windowsでは、実際にimportされたjson5が0.17.3なのに `importlib.metadata.version("json5")` が0.15.0になることを確認しました。macOSにも同じ上書き経路があり、OSに依存する原因ではありません。

## 手動で既存Backendを更新する

1. アプリ・Backend・追加音声のサービスを終了し、使用中のYuiBackendを非公開の場所へバックアップします。設定・DB・APIキーを含むバックアップをGitHubへ共有しないでください。
2. 同じRelease・同じOSのBackend ZIPを、空の新しいフォルダーへ展開します。古いZIPの展開先へ重ねず、新しいPython実行ファイルと `backend/.venv` が揃っていることを確認します。
3. 新版の `backend/.venv` を別の空フォルダーへ丸ごとコピーして準備します。旧 `.venv` は同じディスク上の非公開バックアップへ移動し、新しい `.venv` を元の場所へ移動します。**フォルダーの内容をマージしないでください。** Windowsでは `Scripts/python.exe`、macOSでは `bin/python` を含む環境全体を置き換えます。Macの隠しフォルダーとシンボリックリンク、実行権限も保持します。
4. その他の配布ファイルを上書き更新します。既存の `.env`、`backend/data`、導入済み `YuiIrodoriV4` 等の追加エンジン、ローカルAIモデルは保持します。`.venv` は手順3で既に置換済みです。
5. 再起動して短い会話・文字起こし・保存した声を確認します。失敗した場合はサービスを終了し、旧配布ファイルと旧 `.venv` を同じ版の組として戻します。新しい環境へ旧ファイルを足して戻す方法は使いません。

旧 `.venv` を同じディスク内で移動する際に、その全内容をもう一度コピーする必要はありません。復旧確認後に不要なバックアップを整理できます。追加音声の巨大なモデルをこのPython環境の置換のために再ダウンロードする必要もありません。

通常のPythonでソースから作ったvenvと、配布に含まれる自己完結したruntimeは別の構成です。上記は配布Backend同士の更新手順であり、任意の開発用venvを混ぜる手順ではありません。

## アプリ内インストーラー

共通インストーラーはBackend bundleに完全な `.venv` が含まれる場合、その環境を一単位として切り替えます。切り替え前に必須ファイルを確認し、ファイル更新が失敗・取り消しとなった場合は旧環境を戻します。復旧自体が失敗した場合は、エラーに示すstaging内のバックアップを残します。リンクされたディレクトリは置換せずエラーにし、リンク先を削除しません。

Pythonを含まないソースのみのbundleや、Backend以外のモデル・音声アセットでは、この環境置換を行いません。

この変更は次のPlayerビルドへのソース修正です。公開済みv0.2.5-beta.2のアプリZIPをGitHubのソース更新だけで変更した扱いにはしません。既存配布版を手動更新する場合は上の手順を使用してください。

## English

This affects both Windows and macOS: copying a new bundle over an existing environment retains obsolete versioned `.dist-info` directories and removed Python modules. The mismatch was reproduced on Windows; the shared installer and both filesystem layouts were tested separately from a Unity Editor or a Mac hardware run.

Stop the app and services, and privately back up the active Backend. Extract the matching OS/release ZIP into an empty folder. Prepare a complete new `backend/.venv`, move the old environment to a private backup on the same volume, and move the new environment into place. Never merge their contents. Preserve Mac links and permissions. Copy the remaining distribution files while retaining `.env`, `backend/data`, optional engines and models. Test after restarting; on failure restore the old distribution files and environment together.

The shared installer now replaces a bundled environment as a unit, validates required files before switching, and restores the previous environment on failure or cancellation. A failed rollback retains its backup. Source-only bundles and non-Backend assets preserve existing environments. This requires a new Player build; existing beta.2 ZIPs are unchanged.
