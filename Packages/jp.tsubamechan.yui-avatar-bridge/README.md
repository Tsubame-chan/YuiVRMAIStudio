# Yui Avatar Bridge

UnityのアバターをYuiで使うための書き出し拡張です。新しくアバターを用意する方は、[VRMの準備と読み込み](https://github.com/Tsubame-chan/YuiVRMAIStudio/blob/main/docs/AVATAR_IMPORT.md)を参照してください。

## 配布版とこのソースの違い

VCCの公開リポジトリで入手できる **0.1.1はOS別のZIPを書き出す版** です。このソースの0.2.0はVRMを書き出す版で、VCCの公開索引からはまだインストールできません。VRMを用意したい場合、0.1.1の導入は不要です。

既存のZIPを使う場合は[旧ZIPの読み込み](https://github.com/Tsubame-chan/YuiVRMAIStudio/blob/main/docs/YUI_AVATAR_BRIDGE_USER_TEST_GUIDE.md)を参照してください。ZIPは対象OSとUnity版に依存し、別の版のアプリで使えるとは限りません。

## ソースのVRM書き出し機能

UniVRM/UniGLTF 0.127.2を使うUnity Editor拡張です。導入済みのプロジェクトでは、アバターを選択し `Yui > Avatar Bridge > Export Avatar for Yui` から、利用許可を確認して書き出します。生成した `.vrm` をYuiへ読み込んでください。

元Prefab・Scene・Materialを変更せず、コピーで処理します。対応するModular Avatarの加工もコピー側で実行します。独自シェーダー、衣装メニュー、VRChat独自のギミックが完全に再現されるわけではありません。書き出し後の案内で口パク・まばたきなどの変換結果を確認してください。

本パッケージにアバターやVRChat SDKは含まれません。[旧ZIPのファイル仕様](Documentation~/FORMAT.md)。
