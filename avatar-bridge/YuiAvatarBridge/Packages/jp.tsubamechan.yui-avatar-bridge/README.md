> 2026-09-22: この独自変換器の公開は保留中です。既存の[NDMF VRM Exporter](https://github.com/hkrn/ndmf-vrm-exporter)を使う導線を優先して評価しています。以下は試作の使い方で、正式な推奨手順ではありません。

# Yui Avatar Bridge 0.2.0（公開前の候補）

自分のVRChat用アバターを、Yuiで開ける共通 `.vrm` 1ファイルへ書き出すUnity拡張です。ユーザー自身で先にVRMへ変換する必要はありません。

VCCへYuiのリポジトリを登録し、対象プロジェクトのManage ProjectからYui Avatar Bridgeを追加します。変換に必要なUniVRM/UniGLTF 0.127.2も依存パッケージとして追加する構成です。0.2.0はまだ公開されておらず、現在の公開索引は旧0.1.1です。

`https://raw.githubusercontent.com/Tsubame-chan/YuiVRMAIStudio/main/vpm/index.json`

1. いつものUnity 2022.3プロジェクトで、使いたい姿のアバターを選びます。
2. `Yui > Avatar Bridge > Export Avatar for Yui` を開きます。
3. 利用許可を確認し「Yui用ファイルを書き出す」を押します。
4. 保存されたファイルを端末へコピーしてYuiで開きます。

OS別の書出しやBuild Support追加は不要です。元Prefab・Scene・Materialは変更しません。対応するModular Avatarの加工はコピー側で実行します。表情や衣装のアニメーション、独自Shader、VRChat独自ギミックを完全再現するものではありません。変換できない口やまばたき等は完了時に知らせます。

生成したパッケージからVRChat SDK 3.9.0の公式Robot、Modular AvatarのBone Proxy/Merge ArmatureをMac Editorで検証しています。Windows VCCの導入操作・iPhone実機・全アバターの見た目は未受入です。

[導入と転送](https://github.com/Tsubame-chan/YuiVRMAIStudio/blob/main/docs/AVATAR_IMPORT.md)。`Documentation~/FORMAT.md`は従来ZIPの互換仕様です。本パッケージにアバターやVRChat SDKは含めません。
