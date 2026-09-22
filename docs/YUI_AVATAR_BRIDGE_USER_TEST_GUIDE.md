# Yui Avatar Bridge ユーザー目線テストガイド

更新日: 2026-07-14

このガイドは、Windows上のVCCアバタープロジェクトから標準ZIPを書き出し、クラウド経由でMacへ移し、Yui VRM AI Studioで読み込む実機テスト用です。アバターをVRMへ変換しないため、VRChat用Unityプロジェクトのメッシュ、マテリアル、BlendShape、Humanoid骨格を保ったまま検証できます。

## 現在のテスト範囲

- Windows、macOS、Android、iOS用のUnity AssetBundleを、必要な対象を選んで1つの標準ZIPへ保存します。
- VRC Avatar Descriptorの15 Visemeから、`aa / ih / ou / ee / oh` の5母音を自動取得します。
- AnimatorやDescriptorから表情・アニメーションクリップ候補を記録し、標準表情、表情オプション、ジェスチャー、移動、衣装、その他に分類します。
- MaterialごとにShader名と、書き出し時にShaderを解決できたかを記録します。
- VRC PhysBoneのルート、影響骨数、Collider数、主要パラメーターを記録します。
- Yui側には実行中のOSに合うペイロードを選び、SHA-256とサイズを検証するローダーがあります。

現段階では、生成したmacOS AssetBundleが同一Unity版のYui Editorで互換性エラーになり、runtime-load受入は未合格です。ZIP、payload、ハッシュ、診断情報の生成までは確認済みですが、一般ユーザー向け導線としてはまだ使用しないでください。表情クリップの自動再生と、VRC PhysBoneをYuiランタイム物理へ変換して揺らす処理も未完了です。ZIPに診断・変換元情報が入っていることと、Yui上で動作することは分けて確認してください。

## 0. 事前準備

1. 対象アバターをUnity上で正常に表示できるVCC Avatarプロジェクトを、念のためバックアップします。
2. VCCがそのプロジェクトに指定しているUnity 2022.3系を使用します。検証途中でUnityの版を変更しません。
3. アバターの利用規約を確認します。VRChat外のアプリでの利用・変換・保存が許可されているアバターだけを使用してください。
4. ZIPにはアバター素材そのものが含まれます。公開共有せず、自分のクラウドストレージも共有範囲を限定してください。

## 1. WindowsのVCCプロジェクトへBridgeを入れる

公開VPMリポジトリの提供前は、開発版パッケージをローカル導入します。

1. VCCから対象のAvatarプロジェクトを開きます。
2. Unityの `Window > Package Manager` を開きます。
3. `+ > Add package from disk...` を選びます。
4. 開発版フォルダー `jp.tsubamechan.yui-avatar-bridge` 内の `package.json` を指定します。
5. Unity上部に `Yui > Avatar Bridge > Export Avatar for Yui` が表示されることを確認します。

一般配布時はVCCへYuiのVPMリポジトリを一度登録し、`Manage Project` から追加する方式へ置き換えます。公開URLと画面例はVPMリリース時に本ガイドへ追記します。

## 2. WindowsでアバターZIPを書き出す

1. 普段VRChatへアップロードするときと同じアバターSceneを開きます。
2. Hierarchyで、`VRC Avatar Descriptor` と `Animator` を持つアバター最上位GameObjectを選択します。
3. `Yui > Avatar Bridge > Export Avatar for Yui` を開きます。
4. Compatibility欄を確認します。
   - `VRC Avatar Descriptor: Found`
   - `Humanoid Animator: Valid`
   - `Visemes: 5/5`
   - Mesh、Material、BlendShapeの件数が0ではない
   - PhysBoneを使うアバターなら、PhysBone chainsが想定に近い
5. Display nameを確認します。
6. 読み込ませたい端末のpayloadを有効にします。WindowsからMacへ移して両方で試す場合はWindowsとmacOS、スマートフォンでも試す場合はAndroid/iOSも有効にします。Unityへ対象プラットフォームのBuild Supportが入っていない場合は、そのpayloadを先に外してデスクトップ検証を続行できます。
7. 保存先を選びます。出力名は `アバター名_AvatarPackage.zip` です。
8. VRChat外で使用できる権利・許可があることを確認するチェックを入れます。
9. `Export for Yui` を押し、完了ダイアログの保存先を確認します。

ZIPを展開すると、主に次のファイルが見えます。

```text
manifest.json
FORMAT.txt
payloads/avatar_windows.bundle
payloads/avatar_macos.bundle
payloads/avatar_android.bundle
payloads/avatar_ios.bundle
```

上級者は`manifest.json`の表示名や将来の感情割当を編集できます。Bundleを差し替えた場合だけ、対応する`sizeBytes`と`sha256`も更新する必要があります。

## 3. ZIPをWindowsからMacへ移す

1. 完成した `.zip` を、iCloud Drive、Google Drive、OneDriveなどへアップロードします。
2. アップロード完了後、ファイルサイズがWindows側と一致することを確認します。
3. Macで同期・ダウンロードが完全に終わるまで待ちます。一時ファイルやクラウドのプレースホルダーを直接Yuiへ渡しません。
4. ZIPは展開・再圧縮・拡張子変更をせず、そのまま移します。

厳密に確認する場合は、Windows PowerShellとMac TerminalでSHA-256を比較します。

```powershell
Get-FileHash .\AvatarName_AvatarPackage.zip -Algorithm SHA256
```

```bash
shasum -a 256 AvatarName_AvatarPackage.zip
```

## 4. Mac版Yuiへ読み込む

1. Mac版Yui VRM AI Studioを起動します。
2. Avatar設定の `Load Avatar` を押します。
3. Windowsから移した `.zip` を選びます。
4. YuiがmacOSペイロードを選択し、サイズとSHA-256を検証してから表示することを確認します。
5. 失敗した場合は、エラーメッセージ、ZIPのSHA-256、BridgeとYuiの版、Unityの版を控えます。アバター本体やZIPはIssueへ公開添付しません。

## 5. 実アバター受入チェック

アバターごとに次を記録します。

- 外観: メッシュ欠落、ピンク色のMaterial、透明・両面表示、輪郭線、表情初期値の異常がない。
- 骨格: 位置、向き、身長、腕・指・首の姿勢が正しい。
- リップシンク: 「あ・い・う・え・お」を含む音声で5母音すべてが動く。似た名前のBlendShapeを誤選択していない。
- 視点: 鑑賞モードで一周し、裏面やアクセサリーの表示崩れがない。
- 表情候補: `manifest.json`の`diagnostics.expressionClips`に、普段使う表情が`facial_expression`または`facial_option`として入り、衣装ギミックが表情に誤分類されていない。
- PhysBone情報: `diagnostics.physBones`のルート、影響骨数、Collider数がUnity Inspectorの認識と大きくずれていない。
- 安全性: 元のVCC Scene、Prefab、Animator Controllerが書き換わっていない。
- 可搬性: 同じZIPから、各端末が自分のOSに一致するpayloadを選んで読み込める。

PhysBoneの実際の揺れと、感情に応じた表情再生は、ランタイム変換機能の実装後に同じZIPで再テストします。

## 6. 比較検証で残す情報

- アバター名の代わりに任意のテスト識別名
- Unity版、VRC SDK Avatars版、Yui Avatar Bridge版、Yui本体版
- WindowsでのZIPサイズとSHA-256
- `Visemes n/5`、Expression clips数、PhysBone chains数
- Windows版YuiとMac版Yuiそれぞれの成功・失敗
- 見た目、5母音、Material、PhysBone、表情候補ごとの結果
- エラー全文と再現手順

この二方向の結果、つまり公式サンプルを使う隔離テストと、実際のVCCアバターを使うユーザー目線テストを突き合わせてから、schema 1を正式確定します。

## Validation update — 2026-09-09

The macOS AssetBundle load blocker was traced to missing built-in module dependencies in the Bridge project. A build could produce a ZIP with valid SHA-256 while logging that AssetBundle support was disabled. The Bridge package now declares assetbundle, animation and jsonserialize modules, checks the required module before export, uses StrictMode and validates a host-platform staged bundle before publishing the ZIP.

A minimal primitive producer/consumer isolated the failure. After the fix, the SDK Robot sample and a privately owned unmodified avatar each loaded in an actual Unity 2022.3.62f3 macOS Yui Player: rendering, supported shaders, humanoid and five writable mapped vowel shapes passed. Initial native-avatar facing now uses the same 180-degree yaw as VRM; existing saved user transforms remain authoritative. Editor tests are not a substitute for this Player gate.

Windows cross-compilation succeeded; native Windows execution remains unverified. Audio-driven lipsync, expressions, clothing toggles, physics and arbitrary avatar compatibility remain separate gates. A customized baked avatar was identified and rendered locally, but that does not establish a VCC source → Bridge ZIP → Player round trip for its modified animation/clothing setup.

ZIP loading also rejects traversal segments, normalized duplicate paths, oversized manifests, invalid declared payload sizes and malformed SHA-256 values. Editor platform detection now uses the host platform even after cross-compiling for Windows.

See `PUBLIC_PLAYER_ASSET_VALIDATION.md` before creating any distributable Player.
