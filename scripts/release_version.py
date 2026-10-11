#!/usr/bin/env python3
"""Print a validated release field. YUI_RELEASE_VERSION overrides desktop_tag."""
import argparse
import os
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "backend"))
from app.core.release_version import read_release_versions


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--field", choices=("desktop_tag", "ios_version", "ios_build"), default="desktop_tag")
    args = parser.parse_args()
    value = read_release_versions()[args.field]
    override = os.environ.get("YUI_RELEASE_VERSION", "").strip()
    if args.field == "desktop_tag" and override:
        if not re.fullmatch(r"v[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?", override):
            parser.error("YUI_RELEASE_VERSION must be a version tag, e.g. v0.2.5-beta.3")
        value = override
    print(value)


if __name__ == "__main__":
    main()
