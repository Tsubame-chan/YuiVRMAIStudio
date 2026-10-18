# ダウンロード / Downloads

2026-10-18確認。公開中のデスクトップベータ版は [v0.2.6-beta.1](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/tag/v0.2.6-beta.1) です。以下は、そのReleaseに実在する配布ファイルへのリンクです。GitHubの「Code → Download ZIP」はソースコードで、実行アプリではありません。

## macOS

- [アプリZIP / App ZIP](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/download/v0.2.6-beta.1/YuiVRMAIStudio_MacOSPublicBeta_v0.2.6-beta.1_macos.zip)（Apple Silicon）
- [Backend ZIP（修正版r1）/ Corrected Backend ZIP](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/download/v0.2.6-beta.1/YuiVRMAIStudio_BackendBundle_v0.2.6-beta.1_macos-r1.zip)
- [起動手順](MAC_PUBLIC_BETA.md) / [English setup](MAC_PUBLIC_BETA.en.md)

Mac BackendはPython実行ファイルの梱包を修正した `macos-r1.zip` を使ってください。修正前の同名ReleaseのBackendを導入済みなら、アプリを再起動してBackendを更新します。アプリ本体とモデルの重みはこの再梱包で変更されていません。[更新手順](BACKEND_UPDATE.md)。

## Windows

- [アプリZIP / App ZIP](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/download/v0.2.6-beta.1/YuiVRMAIStudio_WindowsPublicBeta_v0.2.6-beta.1_windows.zip)（64-bit）
- [Backend ZIP](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/download/v0.2.6-beta.1/YuiVRMAIStudio_BackendBundle_v0.2.6-beta.1_windows.zip)
- [起動手順 / Setup](SETUP_GUIDE.md)

## iPhone / iPad

[App Store（日本向け無料版）](https://apps.apple.com/jp/app/id6815341780)。必要OSと、公開版・開発版の機能の違いは[対応表](RUNTIME_SUPPORT.md)を参照してください。Androidの公開アプリはありません。

## データとチェックサム / Data and checksums

通常はアプリZIPを取得し、初回案内から必要なデータをダウンロードします。Backendを手動で更新する場合は、上の同じRelease・同じOSのBackend ZIPを使います。

- [SHA256SUMS.txt](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/download/v0.2.6-beta.1/SHA256SUMS.txt)
- [アセットmanifest / Asset manifest](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/download/v0.2.6-beta.1/YuiVRMAIStudio_AssetManifest.json)
- [AI・音声データの取得](LOCAL_AI_ASSETS.md) / [Backend追加音声](BACKEND_ASSET_DOWNLOADS.md)

manifestは変更のない大容量モデルを旧Releaseの固定URL・SHA-256で再利用します。旧タグが付いたデータが必ず古いアプリ向けとは限りません。URLのタグだけを新しいものへ置き換えないでください。手動用の[Irodori Windows NVIDIA追加ZIP](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/download/v0.2.5-beta.2/YuiVRMAIStudio_IrodoriV41_WindowsNVIDIA_v0.2.5-beta.2.zip)も、この固定データです。通常は[Consoleから取得](BACKEND_ASSET_DOWNLOADS.md)します。

## 更新履歴 / Release notes

[今回のRelease notes](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/download/v0.2.6-beta.1/RELEASE_NOTES.md) / [すべてのReleases](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases)。ベータ版を含めて確認してください。`releases/latest` は最新ベータを指すとは限りません。

English: the links above are the published desktop beta apps and matching Backend bundles. Use **macos-r1** for the corrected Mac Backend; that repackaging did not change the app or model weights. Setup normally downloads the required data for you. Older model/voice asset tags can be intentional checksum-pinned reuse. Source/build version metadata is not proof that a binary has been published. See [device compatibility](RUNTIME_SUPPORT.md) for released versus development-only features.
