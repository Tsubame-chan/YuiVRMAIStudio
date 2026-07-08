#!/usr/bin/env python3
"""Merge one release asset descriptor into a Yui asset manifest."""

from __future__ import annotations

import argparse
import json
from pathlib import Path


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--asset-json", required=True, type=Path)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
    asset = json.loads(args.asset_json.read_text(encoding="utf-8"))

    asset_id = asset.get("id")
    if not asset_id:
        raise ValueError(f"Asset descriptor does not contain an id: {args.asset_json}")

    assets = manifest.setdefault("assets", [])
    for index, existing in enumerate(assets):
        if existing.get("id") == asset_id:
            assets[index] = asset
            break
    else:
        assets.append(asset)

    args.manifest.write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print(f"Merged asset {asset_id} into {args.manifest}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
