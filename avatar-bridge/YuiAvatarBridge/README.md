# Yui Avatar Bridge

Unity/VCCで使っているアバターを、Yui向けの共通 `.vrm` に書き出すEditor拡張です。現在の開発版は0.2.0。公開中の0.1.1はOS別ZIP方式で、仕様が異なります。

[パッケージの導入・使い方](Packages/jp.tsubamechan.yui-avatar-bridge/README.md)を参照してください。0.2.0ではOS別のBuild Supportは不要です。必要なUniVRMの変換依存をパッケージへまとめる構成です。元のアバターは変更せずコピーで処理します。

独自シェーダー・衣装メニュー・接触ギミックの完全再現は対象外です。書き出し時の診断で基本素材・口パク・まばたき・揺れの変換範囲を確認します。
