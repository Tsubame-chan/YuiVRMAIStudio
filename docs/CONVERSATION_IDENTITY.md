# キャラクター・会話・記憶 / Character identity and memory

2026-10-03。通常チャットの端末内AIとDirect APIで共通の記憶を使います。

## キャラクターを分ける

キャラクターのIDで人格、音声、最近の対話、会話履歴、長期記憶を分離します。表示名を変えても同じキャラクターです。「着替え」は外見だけを変え、人格と記憶を維持します。同じ外見で別のキャラクターを作る場合は、別の人格・記憶として扱えます。アバターファイルのIDだけで人格を決める設計ではありません。

選択中のキャラクターの性格指示、共通の回答方針、モデル別の追加指示が回答に反映されます。ローカル/API切替で人格や記憶を初期化しません。APIでは必要な指示・会話・検索された記憶をそのサービスへ送信します。

## 保存と検索

最近の対話と長期記憶は別です。通常のユーザー発言をキャラクター別の端末内記憶として保持し、質問に関連するものを上限付きで検索して渡します。固定した記憶や新しい訂正を考慮します。記憶は「ユーザーが話した参考情報」として扱い、システム指示やAI自身の体験に昇格させません。

設定の記憶管理で確認・追加・編集・削除・一括削除ができます。会話履歴の削除と長期記憶の削除は別の操作です。記憶を消しても、残っている最近の会話に同じ情報があれば回答に現れる場合があります。完全に忘れさせたい場合は、該当キャラクターの記憶と会話履歴の両方を確認してください。

記憶の容量と検索には上限があります。全発言を毎回プロンプトへ入れず、関連情報を選ぶ構成です。検索漏れ・モデルの読み違いがあり、必ず思い出す保証はありません。再起動で保存は維持されますが、端末間同期・クラウドバックアップは実装していません。

## シークレットモード

選択中のキャラクターの既存記憶を参照しつつ、新しい会話を履歴・記憶へ保存しません。解除後に内緒話を引き継がず、別キャラクターとも共有しません。モードやキャラクターの切替をまたいだ処理結果の保存も抑止します。外部APIを使う場合の通信を止める機能ではありません。利用者が明示的に保存・コピーしたファイルは別です。

## Backendと保存結果

Backendはuser_id / character_id / session_idで会話を分離し、memoriesにもcharacter_idを持ちます。旧共有データは推測でキャラクターへ割り当てません。ID指定は認証の代わりではありません。Backend管理と端末内記憶は異なる保存領域で、相互同期はありません。

保存した回答のMarkdownと隣接JSONには、生成時のcharacter/session/task/request ID、時刻、modeを記録します。生成後にキャラクターを変えて保存しても元のIDを保持します。旧Markdownも一覧表示できますが、過去のIDを推測して付けません。

English: normal local/API chat shares persistent, character-scoped retrieval. Appearance changes retain identity; separate characters do not share memory. Secret mode reads existing memories without recording new exchanges. History and memory deletion are separate. Retrieval is bounded and imperfect, and Backend storage/device storage are not synchronized.
