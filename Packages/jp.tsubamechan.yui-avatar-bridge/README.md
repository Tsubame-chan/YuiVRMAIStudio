# Yui Avatar Bridge 0.1.1 (experimental)

VRChat用のUnityプロジェクトに追加して、自分のアバターをYui用の通常ZIPへ書き出すEditorツールです。VRChatアカウントへのログインやアップロードは不要です。

## インストール

VCC: Settings → Packages → Add Repository に次を入力し、追加後に対象プロジェクトのManage Projectから **Yui Avatar Bridge** を追加します。

`https://raw.githubusercontent.com/Tsubame-chan/YuiVRMAIStudio/main/vpm/index.json`

VCCを使わない場合は、GitHub Releaseからpackage ZIPをダウンロード・展開し、UnityのWindow → Package Manager → + → Add package from diskで展開先のpackage.jsonを選びます。VRChat SDKを含むUnity 2022.3プロジェクトで利用してください。

## 書き出し

1. Hierarchyでアバタールートを選択。
2. `Yui > Avatar Bridge > Export Avatar for Yui`。
3. 診断を確認し、利用する端末のOSを選択。必要なUnity build supportがないOSは先にUnity Hubで追加。
4. 利用権の確認、出力先を選択してExport。
5. ZIPをYuiへ渡し、アバター追加で読み込み。

Mac Playerで読み込み・描画・母音mappingを検証した実験版です。Windows／Android／iOSのpayload生成と、各実機での対応は別です。衣装、表情、PhysBone、任意のShaderの完全再現はまだ保証しません。元PrefabやSceneは変更しませんが、一般的なUnityプロジェクトのバックアップ運用は維持してください。

See Documentation~/FORMAT.md. No avatar assets or VRChat SDK are redistributed in this package.
