#!/usr/bin/env python3
"""Shrink the embedded UI icons to the size they are actually drawn at.

The shop buttons draw their icons in an 80x80 control and the event option
icon is a 60x60 control, so the committed 256-832 px sources only cost DLL
bytes. Resizing to 256 px (a 3x margin for scaled displays) and quantising to
a 256-colour palette keeps a 38-52 dB PSNR against the resized original while
dropping the embedded payload by roughly 80%.

Run from the project root:

    python scripts/tools/optimize_icons.py

Pass --check to report sizes without rewriting anything.
"""
from __future__ import annotations

import argparse
import io
import sys
from pathlib import Path

try:
    from PIL import Image
except ImportError:  # pragma: no cover - guidance beats a traceback
    raise SystemExit("Pillow is required: python -m pip install Pillow")

TARGET_SIZE = 256
PALETTE_COLORS = 256


def optimise(path: Path) -> tuple[int, int]:
    before = path.stat().st_size
    with Image.open(path) as raw:
        image = raw.convert("RGBA")
    if max(image.size) > TARGET_SIZE:
        image = image.resize((TARGET_SIZE, TARGET_SIZE), Image.LANCZOS)
    buffer = io.BytesIO()
    image.quantize(colors=PALETTE_COLORS, method=Image.FASTOCTREE).save(
        buffer, "PNG", optimize=True
    )
    data = buffer.getvalue()
    # Never make a file bigger; some small icons are already well compressed.
    if len(data) >= before:
        return before, before
    path.write_bytes(data)
    return before, len(data)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="report without writing")
    parser.add_argument("--dir", default="resources", help="icon directory")
    args = parser.parse_args()

    icon_dir = Path(__file__).resolve().parent.parent.parent / args.dir
    icons = sorted(icon_dir.glob("*.png"))
    if not icons:
        print(f"No PNG icons found in {icon_dir}", file=sys.stderr)
        return 1

    total_before = total_after = 0
    for icon in icons:
        if args.check:
            with Image.open(icon) as raw:
                pixels = f"{raw.width}x{raw.height}"
            size = icon.stat().st_size
            total_before += size
            total_after += size
            print(f"{icon.name:34} {pixels:>9} {size / 1024:7.1f} KB")
            continue
        before, after = optimise(icon)
        total_before += before
        total_after += after
        note = "" if after < before else "  (kept)"
        print(
            f"{icon.name:34} {before / 1024:7.1f} KB -> {after / 1024:6.1f} KB{note}",
            flush=True,
        )

    if args.check:
        print(f"{'total':34} {'':>9} {total_before / 1024:7.1f} KB")
    else:
        print(f"{'total':34} {total_before / 1024:7.1f} KB -> {total_after / 1024:6.1f} KB")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
