# 記憶と会話履歴 / Memory and conversation history

## キャラクターごとに覚える

人格・記憶・履歴はキャラクターごとに分かれます。表示名を変えたり、同じキャラクターを「着替え」させたりしても、その記憶は保たれます。別のキャラクターを作ると、別の人格・記憶になります。端末内AIとDirect APIを切り替えても、キャラクターの記憶は使えます。

会話履歴は過去のやり取りです。記憶は、好み・約束・覚えておきたい話など、今後の会話で参照する情報です。毎回すべての記録をAIへ渡すわけではありません。関連する記憶を選ぶため、思い出せないことや読み違いもあります。

## 確認・編集・削除

設定の記憶管理から、記憶を確認・追加・編集・固定・削除できます。履歴の削除と記憶の削除は別です。忘れてほしい内容がある場合は、記憶と履歴の両方を確認してください。人格設定に同じ内容を書いている場合は、その設定も修正します。

## 端末で共有する

デスクトップ版v0.2.4-beta.2では、同じBackendへ端末を登録し、変更を確認して人格・記憶・履歴を共有できます。別々に話した履歴は両方を残し、同じ記憶を両側で編集した場合は内容を比較して選びます。同じ表示名だけで別のキャラクターを自動的に結びつけることはありません。

Backendの通常の会話機能が保存した記録と、端末同期の共有記録は別です。既存のBackend記録を自動で取り込む機能は配布版beta.2にはありません。iOS 0.2.4 (14)も同期に対応しません。[同期の手順と制限](BACKEND_CONSOLE.md#キャラクターと会話を端末間で同期する)。

## シークレットモード

既存の記憶を参照しますが、新しい会話を履歴・記憶へ保存しません。シークレット中は端末同期もできません。自分で回答を保存・コピーした場合は、そのファイルが残ります。

外部APIを選ぶと、会話や必要な人格・記憶はそのサービスへ送信されます。シークレットモードは通信を止めたり、相手のサービスの保存方針を変えたりする機能ではありません。[プライバシー](PRIVACY.md)。

English: character memory persists across appearance changes and local/Direct API switching. History and memory are separate and have separate deletion controls. Retrieval is selective and imperfect. Desktop beta.2 supports confirmed sharing through a paired Backend; iOS 0.2.4 (14) does not. Legacy Backend conversation storage is separate from paired shared records in the distributed beta.2. Secret mode reads existing memories without saving new conversations, but does not prevent API transmission.
