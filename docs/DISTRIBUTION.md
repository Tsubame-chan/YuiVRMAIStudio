# 情報と大容量データの配信 / Distribution architecture

2026-10-03。GitHubを利用者の入口にしつつ、データの配信先は用途別に分けます。

| 内容 / Content | 現在の置き場所 / Current location |
| --- | --- |
| 紹介・手順・FAQ・仕様・ソース / Intro, guides, FAQ, source | このGitHub repository。入口はREADMEと[HELP](HELP.md) |
| 問い合わせ・不具合 / Bug reports | GitHub Issues。秘密情報は書かないでください |
| デスクトップアプリ・基本データ・runtime / Desktop apps and required data | 版を固定したGitHub Releases、分割ファイル、SHA-256とmanifest |
| 任意のmacOS上位モデル / Optional macOS model | モデルcatalogで指定するHugging Faceのファイル・checksum |
| iOSアプリ・任意E4B / iOS app and optional E4B | App Store / Apple-Hosted Background Assets。desktopのGitHub初回取得とは別 |

## GitHub Releasesを使う理由と制限

ソース履歴に大型weightを入れず、アプリ・モデル・runtimeの組合せを版ごとに固定できます。GitHubの公式仕様ではReleaseの**各ファイルは2GiB未満**で、Release全体の容量・帯域に上限はありません。ただしダウンロード速度の保証ではありません。[公式仕様](https://docs.github.com/en/repositories/releasing-projects-on-github/about-releases)。

desktop v0.2.4-beta.2は約2.46GBの共通データZIPを2分割します。OS runtimeは約76MB（Mac）/67MB（Windows）です。利用者は通常アプリ内の案内だけで導入でき、手動結合は不要です。配布アプリは同じ版のmanifestを参照します。prereleaseが `/latest` の別版manifestを取得することを避けます。古いReleaseは更新せず保持します。

速度は実測で判断します。Wi-Fiの契約速度だけでなく、配信経路、端末ストレージ、checksum検査・展開も待ち時間に影響します。新しい有料サービスを追加しただけで速くなるとは限りません。

## 配信先を変更できる構成

manifestはデータID・版・HTTPS URL・分割順・サイズ・SHA-256・必須ファイルを持ちます。モデルcatalogもモデルID・capability・取得先・checksumを分離します。GitHubだけにモデル名や取得処理を直結させず、同じ検査を通す別の配信先を使える基盤です。

将来CDN/オブジェクトストレージを使う場合は、地域別速度、Range/再開、費用・容量、運用・ライセンス、破損・取消し保護を測ってから切り替えます。未検証のミラーや自動fallbackは追加しません。Hugging Faceのrevision固定も活用候補です。[HF公式のダウンロード仕様](https://huggingface.co/docs/huggingface_hub/en/guides/download)。現状のすべてのURLがcommit SHA固定であるとは主張しません。

GitHub Pages等の独立した紹介サイトは今後の選択肢ですが、現時点ではREADME・HELP・Releases・Issuesで入口を一つに保ちます。

English: GitHub is the documentation/support hub, not a mandatory provider for every large asset. Desktop releases use version-pinned manifests, split files and SHA-256; macOS optional models use the model catalog's Hugging Face source, while iOS uses Apple's asset distribution. GitHub permits files under 2GiB without an aggregate release/bandwidth quota, but does not promise download speed. A future CDN should be selected using measured latency, resume support, integrity, licensing and operating cost. No new paid hosting service is introduced in this release.
