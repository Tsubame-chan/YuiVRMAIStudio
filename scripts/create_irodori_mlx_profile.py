#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import os
import shutil
from pathlib import Path


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Create a lightweight MLX Irodori model profile by copying config.json "
            "and linking the large model files from an existing local install."
        )
    )
    parser.add_argument("--source", required=True, type=Path, help="Source Irodori MLX model directory.")
    parser.add_argument("--output", required=True, type=Path, help="Output profile directory.")
    parser.add_argument("--num-steps", required=True, type=int, help="sampler.num_steps value for this profile.")
    parser.add_argument(
        "--link-mode",
        choices=("symlink", "copy"),
        default="symlink",
        help="How to reuse model files. Use copy only when symlinks are unavailable.",
    )
    return parser.parse_args()


def require_file(path: Path) -> None:
    if not path.is_file():
        raise SystemExit(f"Required file not found: {path}")


def replace_path(path: Path, source: Path, *, link_mode: str) -> None:
    if path.exists() or path.is_symlink():
        if path.is_dir() and not path.is_symlink():
            shutil.rmtree(path)
        else:
            path.unlink()

    if link_mode == "copy":
        if source.is_dir():
            shutil.copytree(source, path)
        else:
            shutil.copy2(source, path)
        return

    os.symlink(source, path, target_is_directory=source.is_dir())


def main() -> int:
    args = parse_args()
    source = args.source.expanduser().resolve()
    output = args.output.expanduser()
    require_file(source / "config.json")
    require_file(source / "model.safetensors")
    require_file(source / "dacvae" / "config.json")
    require_file(source / "dacvae" / "model.safetensors")

    output.mkdir(parents=True, exist_ok=True)
    config = json.loads((source / "config.json").read_text(encoding="utf-8"))
    sampler = config.setdefault("sampler", {})
    sampler["num_steps"] = args.num_steps
    (output / "config.json").write_text(
        json.dumps(config, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )

    replace_path(output / "model.safetensors", source / "model.safetensors", link_mode=args.link_mode)
    dacvae = output / "dacvae"
    dacvae.mkdir(exist_ok=True)
    replace_path(dacvae / "config.json", source / "dacvae" / "config.json", link_mode=args.link_mode)
    replace_path(dacvae / "model.safetensors", source / "dacvae" / "model.safetensors", link_mode=args.link_mode)

    print(f"Created Irodori MLX profile: {output}")
    print(f"sampler.num_steps={args.num_steps}")
    print(f"link_mode={args.link_mode}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
