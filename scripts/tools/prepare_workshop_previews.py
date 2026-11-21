#!/usr/bin/env python3
"""Prepare Workshop screenshots for the BetterRewards workspace.

Drop raw screenshots into any folder (by default ``BetterRewards-workshop/raw_screenshots``),
run this script, and it will:

* pick up every png/jpg/webp it finds (recursively),
* crop each to 16:9 (centre crop, keeping the full frame width when possible),
* downscale anything wider than the target,
* re-encode as PNG and step the quality down until the file is under 1 MB,
* write them to ``BetterRewards-workshop/previews`` as ``NN-name.png`` with stable names.

Ordering is by the existing filename sort, so name your raws ``01-neow.png``,
``02-sacrifice.png`` and so on. Re-running is safe: the previews folder is rebuilt
from scratch each time, which is what the uploader expects (it deletes backend
images that are no longer in the folder).

Usage:
    python scripts/tools/prepare_workshop_previews.py [source_dir] [--width 1920] [--dry-run]
"""

from __future__ import annotations

import argparse
import io
import shutil
import sys
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    sys.exit("Pillow is required: pip install Pillow")

# Steam's per-file limit for Workshop preview images. Stay under it with headroom.
MAX_BYTES = 950_000
ASPECT_W, ASPECT_H = 16, 9
DEFAULT_WIDTH = 1920
EXTS = {".png", ".jpg", ".jpeg", ".webp", ".bmp"}


def workspace_root() -> Path:
    """Walk up from this script to the repo root (the folder holding dist/)."""
    here = Path(__file__).resolve()
    for parent in here.parents:
        if (parent / "mod_manifest.json").is_file() or (parent / "BetterRewards.csproj").is_file():
            return parent
    # Fall back to two levels up from scripts/tools/
    return here.parents[2]


def crop_to_aspect(im: Image.Image) -> Image.Image:
    """Centre-crop to 16:9, trimming the short side only."""
    w, h = im.size
    target = ASPECT_W / ASPECT_H
    current = w / h
    if abs(current - target) < 1e-6:
        return im

    if current > target:
        # Too wide: trim left/right.
        new_w = int(round(h * target))
        left = (w - new_w) // 2
        return im.crop((left, 0, left + new_w, h))

    # Too tall: trim top/bottom.
    new_h = int(round(w / target))
    top = (h - new_h) // 2
    return im.crop((0, top, w, top + new_h))


def encode_under_limit(im: Image.Image) -> tuple[bytes, str]:
    """Encode under MAX_BYTES, preferring full resolution over lossless format.

    Game screenshots contain a lot of smooth gradients and small text; PNG stays
    large there while high-quality JPEG is far smaller at the same size. Keeping
    every preview at the same resolution matters more for the Workshop gallery
    than keeping PNG, so try PNG once, then step JPEG quality down, and only
    downscale as a genuine last resort.
    """
    rgb = im.convert("RGB") if im.mode != "RGB" else im

    # 1. Lossless first, in case it already fits at full size.
    buf = io.BytesIO()
    im.save(buf, format="PNG", optimize=True)
    png = buf.getvalue()
    if len(png) <= MAX_BYTES:
        return png, f"{im.width}x{im.height} PNG"

    # 2. High-quality JPEG at full resolution.
    for quality in (95, 92, 90, 87, 84, 80, 76, 72, 68):
        buf = io.BytesIO()
        rgb.save(buf, format="JPEG", quality=quality, subsampling=0, optimize=True)
        data = buf.getvalue()
        if len(data) <= MAX_BYTES:
            return data, f"{im.width}x{im.height} JPEG{quality}"

    # 3. Still too big: downscale, then retry JPEG at the reduced size.
    width = im.width
    while width > 960:
        width = int(width * 0.9)
        height = int(round(width * ASPECT_H / ASPECT_W))
        canvas = rgb.resize((width, height), Image.LANCZOS)
        for quality in (92, 88, 84, 80):
            buf = io.BytesIO()
            canvas.save(buf, format="JPEG", quality=quality, subsampling=0, optimize=True)
            data = buf.getvalue()
            if len(data) <= MAX_BYTES:
                return data, f"{canvas.width}x{canvas.height} JPEG{quality}"

    # 4. Give up on the limit and hand back the smallest attempt.
    buf = io.BytesIO()
    rgb.save(buf, format="JPEG", quality=68, optimize=True)
    return buf.getvalue(), f"{rgb.width}x{rgb.height} JPEG68 (over limit)"


def main() -> int:
    parser = argparse.ArgumentParser(description="Prepare Steam Workshop preview images.")
    parser.add_argument("source", nargs="?", help="folder with raw screenshots")
    parser.add_argument("--width", type=int, default=DEFAULT_WIDTH, help="target width (default 1920)")
    parser.add_argument("--dry-run", action="store_true", help="report what would be written")
    args = parser.parse_args()

    root = workspace_root()
    workspace = root.parent / "BetterRewards-workshop"
    if not workspace.is_dir():
        workspace = root / "BetterRewards-workshop"
    if not workspace.is_dir():
        return fail(f"Could not find the BetterRewards-workshop folder next to {root}")

    source = Path(args.source).expanduser().resolve() if args.source else workspace / "raw_screenshots"
    if not source.is_dir():
        return fail(
            f"No screenshot folder at {source}\n"
            "Create it and drop your raw screenshots in, or pass a folder as the first argument."
        )

    raws = sorted(p for p in source.rglob("*") if p.is_file() and p.suffix.lower() in EXTS)
    if not raws:
        return fail(f"No images found in {source} (looked for {', '.join(sorted(EXTS))}).")

    previews = workspace / "previews"
    print(f"Source : {source}")
    print(f"Target : {previews}")
    print(f"Found  : {len(raws)} image(s), target width {args.width}px, limit {MAX_BYTES / 1_000_000:.2f} MB\n")

    if args.dry_run:
        for i, raw in enumerate(raws, 1):
            with Image.open(raw) as im:
                print(f"  {i:02d}  {raw.name}  {im.width}x{im.height} -> previews/{i:02d}-{slug(raw.stem)}.png")
        return 0

    # Rebuild the folder so the uploader's "delete missing" behaviour never leaves stale images.
    if previews.exists():
        shutil.rmtree(previews)
    previews.mkdir(parents=True, exist_ok=True)

    for i, raw in enumerate(raws, 1):
        try:
            with Image.open(raw) as im:
                im.load()
                if im.mode not in ("RGB", "RGBA"):
                    im = im.convert("RGBA" if "A" in im.getbands() else "RGB")

                cropped = crop_to_aspect(im)
                if cropped.width > args.width:
                    h = int(round(args.width * ASPECT_H / ASPECT_W))
                    cropped = cropped.resize((args.width, h), Image.LANCZOS)

                data, note = encode_under_limit(cropped)
        except Exception as exc:  # noqa: BLE001 - report and continue with the rest
            print(f"  !!  {raw.name}: {exc}")
            continue

        ext = ".jpg" if "JPEG" in note else ".png"
        out = previews / f"{i:02d}-{slug(raw.stem)}{ext}"
        out.write_bytes(data)
        print(f"  ok  {raw.name:<32} -> {out.name:<28} {note:<12} {len(data) / 1000:.0f} KB")

    written = list(previews.glob("*.png")) + list(previews.glob("*.jpg"))
    print(f"\nDone. {len(written)} preview(s) in {previews}")
    print("Next: rerun the ModUploader upload command; the previews attach to the same item.")
    return 0


def slug(text: str) -> str:
    """ASCII slug for a file name; non-latin names fall back to 'shot'."""
    raw = text.strip()

    # Drop a leading order prefix like "01-" first, so "01-Neow" -> "neow"
    # while a purely non-latin name still has its digits left to work with.
    parts = raw.split("-", 1)
    if len(parts) == 2 and parts[0].strip().isdigit():
        raw = parts[1]

    keep = [c.lower() if c.isalnum() and c.isascii() else "-" for c in raw]
    out = "".join(keep).strip("-")
    while "--" in out:
        out = out.replace("--", "-")
    # A name that reduced to bare digits carries no meaning; fall back to "shot".
    if not out or out.isdigit():
        return "shot"
    return out


def fail(message: str) -> int:
    print(message, file=sys.stderr)
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
