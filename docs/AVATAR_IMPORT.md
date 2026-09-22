# 自分のアバターをYuiへ

VRMを持っていれば、そのまま読み込めます。VRChat用のUnityプロジェクトから持ち込む場合は、既存の変換ツールでVRMを書き出します。Yui専用の変換ツールは必須ではありません。

この案内は現在のmainの操作画面を対象にしています。配布済みbeta.5とは画面が異なる場合があります。VRM変換後の表示や動作は、使うアバターと端末で確認してください。

## VRMを持っている場合

「設定 → キャラクター → アバターを読み込む」で `.vrm` を選びます。VRM 0.x / 1.0に対応します。成功後はアプリ内へコピーするため、元ファイルを移動する必要はありません。

購入ZIPは先に解凍し、中の `.vrm` を選んでください。購入ZIP・`.unitypackage`・FBXをそのままYuiに読み込むことはできません。

新しく読み込むと別のキャラクターになります。同じキャラクターの衣装替えには「マイキャラクター → 着替え」を使います。名前・性格・声・記憶を引き継げます。

## VRChat用アバターの場合

### まず、普段使う姿をUnityで用意する

- **すでに改変している方:** いつものUnityプロジェクトの、衣装・アクセサリー・体型を調整したアバターを使います。
- **購入したばかりの方:** 作者の説明に従ってSDK・指定シェーダー・アバターを導入します。YuiのためにVRChatへアップロードする必要はありません。

追加ツールを入れる前にプロジェクトをバックアップしてください。既存のシェーダーやツールを無条件で入れ替える必要はありません。

### 既存ツールで書き出す

lilToonとModular Avatarを使うプロジェクトでは、[NDMF VRM Exporter](https://github.com/hkrn/ndmf-vrm-exporter)を評価中です。以下は試験手順であり、見た目まで受入済みの正式推奨手順ではありません。独自シェーダーをそのまま持ち込む方式ではありません。下の「見た目と動作の違い」も確認してください。

**初回の導入・設定**

1. [公式の導入手順](https://github.com/hkrn/ndmf-vrm-exporter/blob/main/docs~/usage.md)に従い、VCC / ALCOMに作者のリポジトリ `https://hkrn.github.io/vpm.json` を登録し、対象プロジェクトへNDMF VRM Exporterを追加します。
2. UnityのHierarchyでアバター本体（VRC Avatar Descriptorがあるオブジェクト）を選び、`Add Component`から`VRM Export Description`を追加します。
3. `Authors`に作者名を設定し、元モデルの利用条件に合わせてメタデータを確認します。VRChat APIからの取得やアバターのアップロードは不要です。
4. このコンポーネントのチェックを外し、表示される`Open NDMF Console to export VRM file`を押します。`Avatar platform`で`VRM 1.0 (NDMF VRM Exporter)`を選びます。

**2回目以降の書き出し**

1. 使いたい衣装・色・体型に調整します。VRChat内だけで変えたメニューの状態ではなく、元Unityプロジェクト側で設定します。
2. `Tools → NDM Framework → Show NDMF Console`を開き、対象アバターと上記のVRMプラットフォームを確認し、`Export`で `.vrm` を保存します。
3. Yuiの「設定 → キャラクター → アバターを読み込む」で選びます。スマホの場合は先にファイルを転送します。

Macでの確認構成はUnity 2022.3.62f3、VRChat SDK 3.9.0、NDMF VRM Exporter 1.4.0、lilToon 2.3.4です。lilToonの自動変換には、Exporterが認識する対応版・パッケージ構成が必要です。古いAssets形式のlilToonを置くだけで同じ変換になるとは限りません。必要な更新は作者の手順で行ってください。

既存の[VRM Converter for VRChat](https://github.com/esperecyan/VRMConverterForVRChat)で作ったVRM 0.xもYuiで読み込めます。すでに使い慣れた変換手順がある場合、Yui用に作り直す必要はありません。同ツールの今回の書き出し検証の対象外です。

## 見た目と動作の違い

VRM化はVRChatのすべての機能を移す処理ではありません。

- **確認するもの:** 衣装・アクセサリー・体型、髪や目の色、透明な部分、口パク、まばたき、髪や衣服の揺れ。
- **変わり得るもの:** 陰影・光沢・輪郭、物理演算の強さや衝突。NDMF VRM ExporterはlilToonの対応項目をMToonへ変換し、一部の色調整や重ね合わせをテクスチャに焼き込みます。完全に同じ見た目にはなりません。
- **対象外:** VRChatの衣装メニューや接触・掴む操作、任意のAnimator/FXの再実行、独自スクリプト。特殊なラメ・屈折・ファー等も同じ描画にはなりません。変換ツールの[互換性説明](https://github.com/hkrn/ndmf-vrm-exporter/blob/main/docs~/compatibility.md)を参照してください。

書き出したVRMをYuiで確認してから使い始めてください。色や衣装が欠けた場合は「変換できた」とせず、元のシェーダー・変換設定を見直します。テクスチャの焼き込みでファイル容量が増えることもあります。今回のMacでの成功は、そのまま全アバター・全スマホの性能保証にはなりません。

## スマホへコピーする

- **Windows → iPhone:** iCloud Driveへ保存し、iPhoneの「ファイル」から選択。USBを使う場合は[Appleデバイスのファイル共有](https://support.apple.com/en-au/guide/devices-windows/mchl4bd77d3a/windows)でYuiへコピーします。
- **Mac → iPhone:** AirDropで送り、「ファイル」に保存してYuiから選択します。
- **Android:** USBのファイル転送でDownloadへコピーし、Yuiから選択します。

iPhoneのUSB共有には共有設定を有効にした新しいYuiビルドが必要です。Windows → iPhoneの実機一周は未確認です。Yuiを含むPC Backendをスマホから常時使う必要はありません。

## 以前のYui Avatar Bridge ZIPを持っている場合

互換読み込みを残しています。そのZIPに使用端末用のデータが必要です。旧ZIPもカスタムシェーダーを完全保存する形式ではありません。新たに持ち込むために旧ZIPを用意する必要はありません。[旧方式の仕様](YUI_AVATAR_BRIDGE_ARCHITECTURE.md)を参照してください。
