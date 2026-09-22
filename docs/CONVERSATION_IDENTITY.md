# 会話と保存結果のID（schema 1）

2026-09-21。最小のローカル継続利用を実装。端末間同期・認証・タスク一覧はこの仕様の範囲外。

- character_id: アバター一覧のSHA-256 ID。組込アバターは `builtin:<slot>`。キャラクターの名前変更で変えない。元資産が変わる場合は別ID。未登録のカスタム入力はslot識別に留まる。
- session_id: キャラクターとTalk/WorkごとにUUIDを発行し、端末設定に保存。再起動後に同じIDを送る。
- task_id: Work送信ごとにUUID。複数送信を一つのタスクとして編集する画面は未実装。
- request_id: 一つの生成要求のUUID。
- 保存結果: SavedResultsのMarkdownと隣接する `.md.json` に上記ID・作成日時・modeを保存。吹き出しの生成時のIDを保持するため、後からアバターを切り替えて保存しても変わらない。

Backendはconversations/chat_responsesにnullableのID列を追加する冪等な移行を行う。既存の本文・時刻・request_idを保持し、過去データへ推測のキャラクターを割り当てない。旧クライアントはIDなしの旧履歴を参照、新クライアントはuser_id/character_id/session_idで履歴を分離する。キャッシュ読出しもuser・IDに限定する。同じrequest_idの保存再試行は二重のassistant行や利用記録を増やさない。

memoriesにもnullable character_idを追加。通常Backend Chatの記憶と、一覧・編集・削除をuser_id/character_idで分離する。旧共有記憶はnullのまま保持し、専用画面で確認できる。範囲指定は認証ではない。端末内AI/Direct APIの履歴復元、RealtimeへのID適用、旧MarkdownへのID付与は行っていない。移行はSQLiteスキーマの追加のみで、session/task表や実在しない外部キーは定義しない。公開APIの追加フィールドは任意で、旧クライアントとの接続を維持する。

保存結果の一覧・再表示・コピー・入力への再利用は添付メニューから可能。旧Markdownも対象。記憶管理はBackend接続が必要。詳細と受入範囲は [継続開発報告](reports/development_20260921_followup/SUMMARY.md)。
