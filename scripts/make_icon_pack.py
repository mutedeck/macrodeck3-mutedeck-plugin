#!/usr/bin/env python3
"""Build the bundled MuteDeck icon pack from the PNGs in icon-src/.

Usage: make_icon_pack.py <icon-src-dir> <output.macroDeckIconPack> [--cwebp PATH]

Writes a .macroDeckIconPack archive: pack.json plus icons/<id>/master.webp per icon. Each icon is
named after its file stem, which is the name the plugin addresses it by. Ids are UUIDv5 values derived
from the name, and every zip entry gets a fixed timestamp, so the same sources always produce the same
bytes and a rebuild does not show up as a change.
Python 3 standard library only, plus the cwebp encoder.
"""
import argparse
import json
import pathlib
import shutil
import struct
import subprocess
import sys
import tempfile
import uuid
import zipfile

PACK_NAMESPACE = uuid.uuid5(uuid.NAMESPACE_URL, "https://mutedeck.com/macro-deck/icon-pack")
PACK_NAME = "MuteDeck Icons"
PACK_AUTHOR = "MuteDeck"
PACK_VERSION = "1.0.0"
PACK_DESCRIPTION = "Meeting control icons for the MuteDeck plugin."
FIXED_TIMESTAMP = (2026, 1, 1, 0, 0, 0)
CREATED_AT = "2026-01-01T00:00:00Z"


def png_size(path):
    with path.open("rb") as handle:
        header = handle.read(24)
    if len(header) < 24 or header[:8] != b"\x89PNG\r\n\x1a\n" or header[12:16] != b"IHDR":
        raise ValueError(f"{path} is not a PNG file")
    return struct.unpack(">II", header[16:24])


def encode_webp(cwebp, source, target):
    subprocess.run(
        [cwebp, "-quiet", "-lossless", "-exact", "-metadata", "none", str(source), "-o", str(target)],
        check=True,
    )


def add_entry(archive, name, data):
    info = zipfile.ZipInfo(name, date_time=FIXED_TIMESTAMP)
    info.compress_type = zipfile.ZIP_DEFLATED
    info.external_attr = 0o644 << 16
    archive.writestr(info, data)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("source")
    parser.add_argument("output")
    parser.add_argument("--cwebp", default=shutil.which("cwebp"))
    args = parser.parse_args()

    if not args.cwebp:
        print("cwebp was not found; install it (brew install webp) or pass --cwebp", file=sys.stderr)
        return 2

    sources = sorted(pathlib.Path(args.source).glob("*.png"))
    if not sources:
        print(f"no PNG files in {args.source}", file=sys.stderr)
        return 2

    pack_id = uuid.uuid5(PACK_NAMESPACE, PACK_NAME)
    icons = []
    masters = []
    with tempfile.TemporaryDirectory() as work:
        for source in sources:
            name = source.stem
            icon_id = uuid.uuid5(pack_id, name.lower())
            width, height = png_size(source)
            target = pathlib.Path(work) / f"{icon_id}.webp"
            encode_webp(args.cwebp, source, target)
            masters.append((icon_id, target.read_bytes()))
            icons.append({
                "id": str(icon_id),
                "name": name,
                "width": width,
                "height": height,
                "originalFileName": source.name,
                "originalFormat": "png",
                "createdAt": CREATED_AT,
                "updatedAt": CREATED_AT,
            })

    manifest = {
        "id": str(pack_id),
        "name": PACK_NAME,
        "description": PACK_DESCRIPTION,
        "author": PACK_AUTHOR,
        "version": PACK_VERSION,
        "createdAt": CREATED_AT,
        "updatedAt": CREATED_AT,
        "icons": icons,
        "ai": {"interaction": False, "generatedContent": False, "generatedAssets": False},
    }

    output = pathlib.Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(output, "w") as archive:
        add_entry(archive, "pack.json", json.dumps(manifest, indent=2).encode("utf-8"))
        for icon_id, data in masters:
            add_entry(archive, f"icons/{icon_id}/master.webp", data)

    print(f"Wrote {output} with {len(icons)} icons.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
