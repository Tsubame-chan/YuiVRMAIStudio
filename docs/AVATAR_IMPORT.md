# 自分のアバターをYuiへ

## VRMを持っている場合

Yuiの「添付 → VRM / ZIPを追加」から選択します。読み込み成功後はアプリ内に保管され、「添付 → アバター一覧」で切替・名称変更ができます。転送元ファイルを残し続ける必要はありません。

## Unity / VRChat用アバターの場合

1. 自分の元アバターが入ったUnity 2022.3プロジェクトを開きます。
2. VCCのSettings → Packages → Add Repositoryへ次のURLを追加します。
   `https://raw.githubusercontent.com/Tsubame-chan/YuiVRMAIStudio/main/vpm/index.json`
3. 対象プロジェクトのManage Projectから **Yui Avatar Bridge** を追加します。VCCなしでは[配布ZIP](https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/tag/avatar-bridge-v0.1.1)を展開し、Unity Package ManagerのAdd package from diskでpackage.jsonを指定できます。
4. Hierarchyのアバタールートを選択し、`Yui > Avatar Bridge > Export Avatar for Yui` を開きます。
5. 使う端末のOSを選び、診断と利用権を確認してZIPを保存します。VRChatへのログイン／アップロードは不要です。
6. ZIPをYuiが動く端末へ移し、「添付 → VRM / ZIPを追加」で開きます。解凍せず、そのまま選びます。

## スマホへ送る

- **iPhone／iPad**：AirDropや自分のファイルストレージで「ファイル」へ保存し、Yui内のファイル選択から開きます。USB接続ではMac Finderのデバイス → ファイル → Yuiへコピーする方法も利用できます（ファイル共有設定を含む新しいiOSビルドが必要）。
- **Android**：USBのファイル転送モードでDownloadへコピーするか、自分のファイルストレージから端末へ保存します。その後Yui内のファイル選択でZIPを指定します。
- **WindowsからMac等**：自分で選んだ転送方法でZIPを渡します。Unityプロジェクト全体を端末へコピーする必要はありません。

書き出すときにAndroid／iOSを選択しないと、その端末用payloadはZIPに入りません。Unity Hubに対応Build Supportがない場合は先に追加してください。各モバイル端末での実行検証はまだ完了していません。配布中のBridgeは実験版です。

Macでの描画と母音mappingは検証済みですが、あらゆるShader・衣装切替・表情・PhysBoneの完全再現を意味しません。対応していない機能は元Unityプロジェクトを保持して将来の再exportに備えてください。VRChatアカウントから他人や自分のサーバー上のアバターをダウンロードする機能ではありません。

## 保存した回答

メッセージのSaveでアプリ内のSavedResultsへMarkdownを保存します。デスクトップでは保存先を開きます。iOSでは上記ファイル共有を有効にしたビルドの「ファイル」から参照できます。Androidで他アプリへ書き出す共有シートは今後の対応です。
