import json
import sqlite3
from datetime import date, timedelta
from typing import Any

from app.db.sqlite import sqlite_path_from_url
from app.models.chat import ChatResponse, ConversationItem
from app.models.memory import MemoryItem, MemorySaveRequest, MemorySearchRequest
from app.models.usage import UsageLimits, UsageResponse


class ChatRepository:
    def __init__(self, database_url: str):
        self.database_url = database_url

    def _connect(self) -> sqlite3.Connection:
        connection = sqlite3.connect(sqlite_path_from_url(self.database_url))
        connection.row_factory = sqlite3.Row
        return connection

    def get_cached_response(self, request_id: str) -> ChatResponse | None:
        with self._connect() as connection:
            row = connection.execute(
                "SELECT response_json FROM chat_responses WHERE request_id = ?",
                (request_id,),
            ).fetchone()

        if row is None:
            return None
        return ChatResponse.model_validate_json(row["response_json"])

    def list_recent_messages(self, user_id: str, limit: int = 12) -> list[dict[str, str]]:
        with self._connect() as connection:
            rows = connection.execute(
                """
                SELECT role, message
                FROM conversations
                WHERE user_id = ?
                ORDER BY id DESC
                LIMIT ?
                """,
                (user_id, limit),
            ).fetchall()

        return [
            {"role": row["role"], "content": row["message"]}
            for row in reversed(rows)
        ]

    def list_recent_conversations(
        self,
        user_id: str,
        limit: int = 20,
    ) -> list[ConversationItem]:
        with self._connect() as connection:
            rows = connection.execute(
                """
                SELECT role, message, created_at
                FROM conversations
                WHERE user_id = ?
                ORDER BY id DESC
                LIMIT ?
                """,
                (user_id, limit),
            ).fetchall()

        return [
            ConversationItem(
                role=row["role"],
                message=row["message"],
                created_at=row["created_at"],
            )
            for row in reversed(rows)
        ]

    def save_chat_turn(
        self,
        *,
        request_id: str,
        user_id: str,
        user_message: str,
        response: ChatResponse,
        provider: str,
        model: str,
        usage_metadata: dict[str, Any] | None = None,
    ) -> None:
        response_json = response.model_dump_json()
        metadata_json = json.dumps(usage_metadata or {}, ensure_ascii=False)

        with self._connect() as connection:
            connection.execute("PRAGMA foreign_keys=ON;")
            try:
                connection.execute(
                    """
                    INSERT INTO conversations (
                        request_id, user_id, role, message, provider, model, metadata_json
                    )
                    VALUES (?, ?, 'user', ?, ?, ?, ?)
                    """,
                    (request_id, user_id, user_message, provider, model, metadata_json),
                )
            except sqlite3.IntegrityError:
                pass

            connection.execute(
                """
                INSERT INTO conversations (
                    request_id, user_id, role, message, provider, model, face, animation, metadata_json
                )
                VALUES (?, ?, 'assistant', ?, ?, ?, ?, ?, ?)
                """,
                (
                    None,
                    user_id,
                    response.text,
                    provider,
                    model,
                    response.face,
                    response.animation,
                    metadata_json,
                ),
            )
            connection.execute(
                """
                INSERT OR REPLACE INTO chat_responses (
                    request_id, user_id, response_json, provider, model
                )
                VALUES (?, ?, ?, ?, ?)
                """,
                (request_id, user_id, response_json, provider, model),
            )
            connection.execute(
                """
                INSERT INTO usage_logs (
                    request_id, user_id, provider, model, operation, metadata_json
                )
                VALUES (?, ?, ?, ?, 'chat', ?)
                """,
                (request_id, user_id, provider, model, metadata_json),
            )
            connection.commit()

    def clear_user_cache(self, user_id: str) -> dict[str, int]:
        with self._connect() as connection:
            response_count = connection.execute(
                "SELECT COUNT(*) AS count FROM chat_responses WHERE user_id = ?",
                (user_id,),
            ).fetchone()["count"]
            conversation_count = connection.execute(
                "SELECT COUNT(*) AS count FROM conversations WHERE user_id = ?",
                (user_id,),
            ).fetchone()["count"]
            connection.execute("DELETE FROM chat_responses WHERE user_id = ?", (user_id,))
            connection.execute("DELETE FROM conversations WHERE user_id = ?", (user_id,))
            connection.commit()

        return {
            "chat_responses": int(response_count),
            "conversations": int(conversation_count),
        }


class MemoryRepository:
    def __init__(self, database_url: str):
        self.database_url = database_url

    def _connect(self) -> sqlite3.Connection:
        connection = sqlite3.connect(sqlite_path_from_url(self.database_url))
        connection.row_factory = sqlite3.Row
        return connection

    def save(self, request: MemorySaveRequest) -> MemoryItem:
        tags_json = json.dumps(request.tags, ensure_ascii=False)
        with self._connect() as connection:
            cursor = connection.execute(
                """
                INSERT INTO memories (user_id, content, importance, tags_json)
                VALUES (?, ?, ?, ?)
                """,
                (request.user_id, request.content, request.importance, tags_json),
            )
            connection.commit()
            memory_id = cursor.lastrowid

        return MemoryItem(
            id=str(memory_id),
            content=request.content,
            importance=request.importance,
            tags=request.tags,
        )

    def search(self, request: MemorySearchRequest) -> list[MemoryItem]:
        pattern = f"%{request.query}%"
        with self._connect() as connection:
            rows = connection.execute(
                """
                SELECT id, content, importance, tags_json
                FROM memories
                WHERE user_id = ?
                  AND (content LIKE ? OR tags_json LIKE ?)
                ORDER BY importance DESC, updated_at DESC, id DESC
                LIMIT ?
                """,
                (request.user_id, pattern, pattern, request.limit),
            ).fetchall()

        return [
            MemoryItem(
                id=str(row["id"]),
                content=row["content"],
                importance=row["importance"],
                tags=json.loads(row["tags_json"]),
            )
            for row in rows
        ]

    def list_recent(self, user_id: str, limit: int = 5) -> list[MemoryItem]:
        with self._connect() as connection:
            rows = connection.execute(
                """
                SELECT id, content, importance, tags_json
                FROM memories
                WHERE user_id = ?
                ORDER BY importance DESC, updated_at DESC, id DESC
                LIMIT ?
                """,
                (user_id, limit),
            ).fetchall()

        return [
            MemoryItem(
                id=str(row["id"]),
                content=row["content"],
                importance=row["importance"],
                tags=json.loads(row["tags_json"]),
            )
            for row in rows
        ]

    def clear_user_memories(self, user_id: str) -> int:
        with self._connect() as connection:
            count = connection.execute(
                "SELECT COUNT(*) AS count FROM memories WHERE user_id = ?",
                (user_id,),
            ).fetchone()["count"]
            connection.execute("DELETE FROM memories WHERE user_id = ?", (user_id,))
            connection.commit()

        return int(count)


class UsageRepository:
    def __init__(self, database_url: str):
        self.database_url = database_url

    def _connect(self) -> sqlite3.Connection:
        connection = sqlite3.connect(sqlite_path_from_url(self.database_url))
        connection.row_factory = sqlite3.Row
        return connection

    def log(
        self,
        *,
        request_id: str | None,
        user_id: str,
        provider: str,
        model: str | None,
        operation: str,
        metadata: dict[str, Any] | None = None,
    ) -> None:
        with self._connect() as connection:
            connection.execute(
                """
                INSERT INTO usage_logs (
                    request_id, user_id, provider, model, operation, metadata_json
                )
                VALUES (?, ?, ?, ?, ?, ?)
                """,
                (
                    request_id,
                    user_id,
                    provider,
                    model,
                    operation,
                    json.dumps(metadata or {}, ensure_ascii=False),
                ),
            )
            connection.commit()

    def daily_usage(
        self,
        *,
        target_date: date,
        limits: UsageLimits,
        user_id: str | None = None,
    ) -> UsageResponse:
        start_at = f"{target_date.isoformat()} 00:00:00"
        end_at = f"{(target_date + timedelta(days=1)).isoformat()} 00:00:00"
        params: list[Any] = [start_at, end_at]
        user_clause = ""
        if user_id is not None:
            user_clause = " AND user_id = ?"
            params.append(user_id)

        with self._connect() as connection:
            rows = connection.execute(
                f"""
                SELECT operation, metadata_json
                FROM usage_logs
                WHERE created_at >= ? AND created_at < ?{user_clause}
                """,
                params,
            ).fetchall()

        counts: dict[str, int] = {}
        stt_duration_ms = 0
        for row in rows:
            operation = row["operation"]
            counts[operation] = counts.get(operation, 0) + 1
            if operation == "stt":
                try:
                    metadata = json.loads(row["metadata_json"] or "{}")
                    stt_duration_ms += int(metadata.get("duration_ms") or 0)
                except (TypeError, ValueError, json.JSONDecodeError):
                    pass

        return UsageResponse(
            date=target_date.isoformat(),
            chat_count=counts.get("chat", 0),
            vision_count=counts.get("vision", 0),
            stt_minutes=round(stt_duration_ms / 60000, 2),
            tts_count=counts.get("tts", 0),
            limits=limits,
        )
