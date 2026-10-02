# 旧Yui Avatar Bridge ZIPの読み込み

新しくアバターを導入する場合は[VRMの手順](AVATAR_IMPORT.md)を使ってください。このページは、以前のYui Avatar Bridgeで作ったZIPをお持ちの方向けです。

1. ZIPに使うOS向けのpayloadが入っていることを確認します。Windows用のBundleだけが入ったZIPはMacでは使えません。
2. ZIPを展開・再圧縮せず、使う端末へコピーします。クラウド経由ならダウンロード完了まで待ちます。
3. アプリのアバター読み込みからZIPを選びます。対応payloadのサイズ・SHA-256を検査してから表示します。
4. エラーが出る場合は、アプリとBridgeの版、対象OS、エラー全文を控えてIssueへ報告します。購入アバターのZIPは公開添付しないでください。

旧ZIPを新しく作る必要はありません。カスタムシェーダーやVRChatのすべての挙動を保持する形式でもありません。実装詳細は[開発者向け仕様](YUI_AVATAR_BRIDGE_ARCHITECTURE.md)を参照してください。

English: this page covers existing legacy Avatar Bridge ZIPs. Keep the ZIP intact, ensure it includes the payload for your OS, finish transferring/downloading it, and select it from the app's avatar loader. For new avatars, use the [VRM import guide](AVATAR_IMPORT.en.md). Do not publicly attach purchased model data when reporting errors.
