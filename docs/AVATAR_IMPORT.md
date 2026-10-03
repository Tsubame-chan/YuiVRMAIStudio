# 自分のアバターを使う

[English](AVATAR_IMPORT.en.md) · [ヘルプ](HELP.md) · [README](../README.md)

Yuiに読み込むファイルは **VRM（.vrm）** です。VRMをお持ちの方は、そのまま読み込めます。UnityやVRChat用のアバターは、先にUnityからVRMへ書き出します。

## VRMを読み込む

1. 「設定 → キャラクター → アバターを読み込む」を開きます。
2. `.vrm` ファイルを選び、読み込みが終わるまで待ちます。VRM 0.x / 1.0を使えます。
3. アバターが切り替わったら、名前・性格・声を設定します。

ファイルはアプリ内へコピーされます。購入ZIPにVRMが入っている場合は、先にZIPを解凍してください。購入ZIP・FBX・`.unitypackage` は直接読み込めません。

**衣装だけを変える場合**は「マイキャラクター → 着替え」から別のVRMを選びます。名前・性格・声・記憶を保ったまま外見を変えられます。新しいキャラクターとして読み込むと、人格と記憶も別になります。

## Unity・VRChat用アバターをVRMにする

### 1. 使いたい姿をUnityで用意する

改変済みなら、普段使っているUnityプロジェクトを開きます。購入したばかりなら、アバター作者の手順に沿ってSDK・シェーダー・モデルを導入します。衣装、色、アクセサリー、体型をUnity上で調整してください。

VRChatへのアップロードは不要です。追加ツールを入れる前に、プロジェクトをバックアップしておきます。

### 2. VRMを書き出す

lilToonやModular Avatarを使うアバターには [NDMF VRM Exporter](https://github.com/hkrn/ndmf-vrm-exporter)を使えます。

1. [作者の導入手順](https://github.com/hkrn/ndmf-vrm-exporter/blob/main/docs~/usage.md)に沿って、VCC / ALCOMへリポジトリを登録し、対象プロジェクトにExporterを追加します。
2. Hierarchyでアバターの最上位オブジェクトを選び、`Add Component`から `VRM Export Description` を追加します。
3. `Authors`に作者名を設定し、モデルの利用条件に沿ってメタデータを入力します。
4. コンポーネントのチェックを外し、表示されるボタンからNDMF Consoleを開きます。
5. `Avatar platform`で `VRM 1.0 (NDMF VRM Exporter)` を選び、`Export`から保存先を指定します。

次回からは衣装や色を調整し、`Tools → NDM Framework → Show NDMF Console`から同じプラットフォームで書き出せます。VRChat内で一時的に変えた衣装メニューではなく、Unityプロジェクト側の姿を使います。

ツールの対応バージョンやシェーダー構成は[公式の互換性説明](https://github.com/hkrn/ndmf-vrm-exporter/blob/main/docs~/compatibility.md)をご覧ください。独自シェーダーをそのままVRMへ持ち込む方式ではありません。別のツールですでにVRM 0.x / 1.0を書き出せている場合は、そのファイルを使えます。

### 3. Yuiへ読み込む

Mac・Windowsでは、書き出したVRMを設定から選びます。スマートフォンでは先に「ファイル」等へコピーし、Yuiから選んでください。

- **Mac → iPhone:** AirDropで送り、「ファイル」に保存します。
- **Windows → iPhone:** iCloud Driveに保存し、iPhoneの「ファイル」から選びます。USBなら [Appleデバイスのファイル共有](https://support.apple.com/guide/devices-windows/mchl4bd77d3a/windows)でYuiへコピーできます。

クラウド上のファイルはダウンロードが終わってから選んでください。PCのBackendへ接続する必要はありません。

## 見た目や動きが違うとき

VRMへの変換で、陰影・光沢・輪郭、髪や服の揺れ方が変わることがあります。VRChatの衣装メニュー、掴む操作、独自スクリプト等は引き継ぎません。

- **衣装や色が欠ける:** 元のUnityプロジェクトの表示状態と、Exporterのシェーダー・変換設定を確認します。
- **まばたきや口が動かない:** 書き出し元の表情・BlendShape設定を確認します。
- **重い／読み込みが遅い:** テクスチャの焼き込みでVRMが大きくなる場合があります。元モデルのメッシュやテクスチャを軽くして書き出します。

利用権のあるアバターを使ってください。不具合報告にはアプリ版・変換ツール・エラー・再現手順を記載し、購入モデルそのものは公開添付しないでください。

<details>
<summary>以前のYui Avatar Bridge ZIPを使う場合</summary>

旧ZIPの互換読み込みもあります。その端末向けのpayloadが必要です。新たに持ち込む場合は、上記のVRM手順を使ってください。[旧ZIPの手順](YUI_AVATAR_BRIDGE_USER_TEST_GUIDE.md)

</details>

旧Yui Avatar Bridge ZIPはOSとUnity版の両方に依存します。アプリ更新後に読み込めなくなった場合は、元のアバターをVRMへ書き出し直してください。
