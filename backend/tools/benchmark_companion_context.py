"""Measure protocol-2 Context retrieval with synthetic, source-linked data.

Run from backend/: ./.venv/bin/python -m tools.benchmark_companion_context
No user database or external service is accessed.
"""
from __future__ import annotations

import argparse
import json
import math
import statistics
import tempfile
import time
from pathlib import Path
from uuid import uuid4

from app.core.companion_context import ContextRequest, owner_context
from app.core.companion_v2 import CompanionStore


def measure(records: int, memories: int, runs: int, query: str) -> dict:
    if records < 1 or memories < 0 or runs < 2:
        raise ValueError("records >= 1, memories >= 0 and runs >= 2 are required")
    now = "2026-10-04T00:00:00+00:00"
    with tempfile.TemporaryDirectory(prefix="yui-context-bench-") as folder:
        store = CompanionStore("sqlite:///" + str(Path(folder) / "synthetic.db"))
        principal, character, conversation = uuid4().hex, uuid4().hex, uuid4().hex
        with store.connect() as db:
            db.execute("INSERT INTO sync_devices VALUES(?,?,?,?,?,0)",
                       (principal, "Synthetic benchmark", "unused", 0, 0))
            db.execute("INSERT INTO sync_characters(id,name) VALUES(?,?)", (character, "Synthetic"))
            db.execute("INSERT INTO companion_v2_conversations VALUES(?,?,?,?,?,?)",
                       (conversation, character, 1, "talk", None, "{}"))
            record_rows, revision_rows = [], []
            for index in range(records):
                record_id = f"{index:032x}"
                payload = json.dumps({"kind": "user_utterance", "conversation_id": conversation,
                                      "text": f"synthetic note {index} about topic {index % 100}",
                                      "recorded_at": now, "realm": "real"})
                record_rows.append((record_id, character, 1, 0, payload, "device:" + principal, now))
                revision_rows.append((record_id, 1, payload, "device:" + principal, now))
            db.executemany("INSERT INTO companion_v2_records VALUES(?,?,?,?,?,?,?)", record_rows)
            db.executemany("INSERT INTO companion_v2_record_revisions VALUES(?,?,?,?,?)", revision_rows)
            memory_rows, source_rows = [], []
            for index in range(memories):
                memory_id = f"{index + records:032x}"
                record_id = f"{index % records:032x}"
                payload = json.dumps({"type": "episode", "subject": "user",
                                      "text": f"synthetic memory {index} about topic {index % 100}",
                                      "basis": "user_statement", "pinned": False,
                                      "source_refs": [{"record_id": record_id, "revision": 1}]})
                memory_rows.append((memory_id, character, 1, "active", payload))
                source_rows.append((memory_id, record_id, 1, None))
            db.executemany("INSERT INTO companion_v2_memories VALUES(?,?,?,?,?)", memory_rows)
            db.executemany("INSERT INTO companion_v2_sources VALUES(?,?,?,?)", source_rows)
        request = ContextRequest(conversation_id=conversation, purpose="talk", query=query)
        durations = []
        for _ in range(runs):
            started = time.perf_counter()
            packet = owner_context(store, principal, character, request)
            durations.append((time.perf_counter() - started) * 1000)
    ranked = sorted(durations)
    return {"records": records, "memories": memories, "runs": runs, "query": query,
            "items": len(packet["items"]), "median_ms": round(statistics.median(durations), 2),
            "p95_ms": round(ranked[math.ceil(0.95 * runs) - 1], 2),
            "max_ms": round(ranked[-1], 2)}


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--records", type=int, default=10000)
    parser.add_argument("--memories", type=int, default=2000)
    parser.add_argument("--runs", type=int, default=20)
    parser.add_argument("--query", default="topic 42")
    args = parser.parse_args()
    print(json.dumps(measure(args.records, args.memories, args.runs, args.query)))
