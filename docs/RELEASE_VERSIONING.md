# Release version metadata / リリース番号

ビルド・パッケージ・Backendの既定番号は `backend/app/core/release_versions.json` にまとめています。DesktopのGitタグ、iOSの販売バージョン、iOSのビルド番号は別の値です。既存公開物の番号を変更する仕組みではありません。現在の値はJSONを確認してください。ソース上の次期版番号と、公開済みアプリの版は一致するとは限りません。利用者向けの入手先は[配布一覧](DOWNLOADS.md)にまとめます。

1. 次のリリース用に `desktop_tag` または `ios_version` / `ios_build` を更新します。Appleへ再アップロードするビルド番号は、既にアップロードした番号より増やします。
2. Unityの公開ビルド、macOS上のパッケージスクリプト、Python/PowerShell配布監査、Backend Settingsがこのファイルを参照します。公開ソース生成でもJSONとEditorの読込ヘルパーを保持します。
3. 環境変数で明示的に上書きする場合、Desktopのビルド・パッケージ・監査には同じ `YUI_RELEASE_VERSION` を渡します。iOSは `YUI_IOS_VERSION` / `YUI_IOS_BUILD_NUMBER`、Backend表示だけの特別な配置は `APP_VERSION` です。通常の `.env.example` は表示番号を固定しません。古い `.env` に配布時の `APP_VERSION` が残っている場合は、その行を削除すると共通値に戻ります。更新処理は既存 `.env` を上書きしません。
4. 互換性を示す `minimum_app_version` や、既存モデル配布URLのタグ・SHA-256はリリース番号とは別です。同じ検証済みモデルを再利用する場合、過去の資産URLを新タグへ書き換えません。

新しい配布を公開したら、GitHub Releaseの実在するasset名で `docs/DOWNLOADS.md` と両言語READMEのアプリZIPリンクを確認します。Backendの再梱包には同じタグでも異なるファイル名が付く場合があります。最新のベータ版は `releases/latest` だけで判定しません。旧モデル資産の固定タグ・SHAや、日付付き検証記録の対象版は一括置換しません。

確認例: `python3 scripts/release_version.py`、`python3 scripts/release_version.py --field ios_build`。不正な共通メタデータはエラーにします。リリース時は通常のソース整合性・公開資産監査も必要です。

## English

`backend/app/core/release_versions.json` supplies defaults for Unity builds, packaging, distribution audits and Backend Settings. Desktop tags, iOS marketing versions and iOS build numbers are independent. Read the current defaults from that JSON. Candidate build metadata can be ahead of published binaries; user-facing acquisition links belong in [Downloads](DOWNLOADS.md). Changing source does not modify previously published binaries.

Before a new release, update the applicable fields; each new Apple upload needs a higher build number. Use the same explicit `YUI_RELEASE_VERSION` for a Desktop build, its package and audit. iOS overrides are `YUI_IOS_VERSION` and `YUI_IOS_BUILD_NUMBER`. `APP_VERSION` only overrides the Backend's displayed version for a custom deployment and is not fixed in the sample environment file. Remove an obsolete release-specific `APP_VERSION` line from an older `.env` to restore the common default; updates preserve existing `.env` files.

Compatibility minimums and pinned model asset URLs/hashes have separate purposes. Reusing a verified older asset does not require reuploading it under a new app tag. Sanitized public generation retains both the metadata and its Unity Editor reader. Invalid common metadata fails explicitly; release source and public-asset audits remain mandatory.
