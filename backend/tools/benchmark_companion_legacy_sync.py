"""Synthetic, temporary-DB timing for one atomic legacy→v2 sync plan."""
from __future__ import annotations

import argparse
import json
import tempfile
import time
from pathlib import Path

from app.core.companion_legacy_adapter import LegacyV2Adapter
from app.core.companion_migration import preview_character
from app.core.companion_shadow_migration import shadow_import_character
from app.core.device_sync import Item, PlanRequest, SyncStore


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--items", type=int, default=1000)
    args = parser.parse_args()
    if not 1 <= args.items <= 18000:
        parser.error("--items must be between 1 and 18000 (legacy snapshot limit)")
    with tempfile.TemporaryDirectory(prefix="yui-legacy-v2-bench-") as folder:
        url = "sqlite:///" + str(Path(folder) / "synthetic.db")
        sync = SyncStore(url)
        pairing = sync.pairing_code()
        principal = sync.pair("Synthetic benchmark", pairing["code"])["device_id"]
        character = sync.create_character("Synthetic")["id"]
        digest = preview_character(url, character)["legacy_digest"]
        shadow_import_character(url, character, digest)
        items = [Item(kind="history", id=f"h{index}:user",
                      value={"text": f"synthetic line {index}", "speaker": "You",
                             "mode": "talk", "recorded_utc": "2026-10-04T01:00:00Z",
                             "conversation_id": "synthetic-conversation",
                             "turn_id": f"synthetic-turn-{index}"})
                 for index in range(args.items)]
        adapter = LegacyV2Adapter(url)
        start = time.perf_counter()
        plan = adapter.plan(principal, PlanRequest(character_id=character, items=items))
        after_plan = time.perf_counter()
        snapshot = adapter.commit(principal, plan["plan_id"], {})
        after_commit = time.perf_counter()
        print(json.dumps({"items": args.items, "projected_items": len(snapshot["items"]),
                          "plan_seconds": round(after_plan - start, 3),
                          "commit_seconds": round(after_commit - after_plan, 3)},
                         sort_keys=True))


if __name__ == "__main__":
    main()
