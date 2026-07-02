import sqlite3
from pathlib import Path
from urllib.parse import unquote


BACKEND_DIR = Path(__file__).resolve().parents[2]


def sqlite_path_from_url(database_url: str) -> Path:
    prefix = "sqlite:///"
    if not database_url.startswith(prefix):
        raise ValueError("Only sqlite:/// DATABASE_URL values are supported in Phase 1.")

    raw_path = unquote(database_url[len(prefix) :])
    path = Path(raw_path)
    if not path.is_absolute():
        path = BACKEND_DIR / path
    return path


def initialize_database(database_url: str) -> None:
    db_path = sqlite_path_from_url(database_url)
    db_path.parent.mkdir(parents=True, exist_ok=True)

    with sqlite3.connect(db_path) as connection:
        connection.execute("PRAGMA journal_mode=WAL;")
        connection.execute("PRAGMA foreign_keys=ON;")
        connection.execute(
            """
            CREATE TABLE IF NOT EXISTS conversations (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                request_id TEXT UNIQUE,
                user_id TEXT NOT NULL,
                role TEXT NOT NULL,
                message TEXT NOT NULL,
                provider TEXT,
                model TEXT,
                face TEXT,
                animation TEXT,
                metadata_json TEXT,
                created_at TEXT NOT NULL DEFAULT (datetime('now'))
            );
            """
        )
        connection.execute(
            """
            CREATE TABLE IF NOT EXISTS usage_logs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                request_id TEXT,
                user_id TEXT NOT NULL,
                provider TEXT NOT NULL,
                model TEXT,
                operation TEXT NOT NULL,
                input_tokens INTEGER DEFAULT 0,
                output_tokens INTEGER DEFAULT 0,
                cost_estimate REAL DEFAULT 0,
                metadata_json TEXT,
                created_at TEXT NOT NULL DEFAULT (datetime('now'))
            );
            """
        )
        connection.execute(
            """
            CREATE TABLE IF NOT EXISTS chat_responses (
                request_id TEXT PRIMARY KEY,
                user_id TEXT NOT NULL,
                response_json TEXT NOT NULL,
                provider TEXT NOT NULL,
                model TEXT,
                created_at TEXT NOT NULL DEFAULT (datetime('now'))
            );
            """
        )
        connection.execute(
            """
            CREATE TABLE IF NOT EXISTS memories (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id TEXT NOT NULL,
                content TEXT NOT NULL,
                importance INTEGER NOT NULL DEFAULT 3,
                tags_json TEXT NOT NULL DEFAULT '[]',
                created_at TEXT NOT NULL DEFAULT (datetime('now')),
                updated_at TEXT NOT NULL DEFAULT (datetime('now'))
            );
            """
        )
        connection.execute(
            """
            CREATE INDEX IF NOT EXISTS idx_usage_logs_created_at
            ON usage_logs(created_at);
            """
        )
        connection.execute(
            """
            CREATE INDEX IF NOT EXISTS idx_usage_logs_user_created_at
            ON usage_logs(user_id, created_at);
            """
        )
        connection.commit()


def check_database(database_url: str) -> bool:
    try:
        db_path = sqlite_path_from_url(database_url)
        if not db_path.exists():
            initialize_database(database_url)
        with sqlite3.connect(db_path) as connection:
            connection.execute("SELECT 1;").fetchone()
        return True
    except Exception:
        return False
