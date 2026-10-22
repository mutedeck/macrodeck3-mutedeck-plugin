#!/usr/bin/env python3
"""Render the vector-based pack icons in icon-src/ from the SVG sources in icon-src/vector/.

Usage: render_icons.py <icon-src-dir> --chrome PATH [--size 512]

Each icon in ICONS is one source glyph, centered in a square canvas with the same padding as the
original Macro Deck 2 artwork, filled white (or grey for the disabled variants), optionally crossed out
with a red slash. Headless Chrome rasterizes it to <name>.png with a transparent background. Icons that
have no vector source (custom artwork) are not listed here and stay as hand-made PNGs.
Python 3 standard library only, plus a Chrome or Chromium binary.
"""
import argparse
import pathlib
import re
import subprocess
import sys
import tempfile

WHITE = "#ffffff"
GREY = "#585858"
RED = "#ff0000"

# Fraction of the canvas the glyph's longer side may take, and the slash's extent, measured on the
# Macro Deck 2 icons so the redrawn set looks the same, just sharper.
GLYPH_FRACTION = 0.72
SLASH_FRACTION = 0.68
SLASH_WIDTH_FRACTION = 0.058

# name -> (source relative to icon-src/vector, colour, crossed out)
ICONS = {
    "unmuted": ("fontawesome/solid-microphone.svg", WHITE, False),
    "muted": ("fontawesome/solid-microphone.svg", WHITE, True),
    "microphone-disabled": ("fontawesome/solid-microphone.svg", GREY, False),
    "video-started": ("fontawesome/solid-video.svg", WHITE, False),
    "video-stopped": ("fontawesome/solid-video.svg", WHITE, True),
    "video-disabled": ("fontawesome/solid-video.svg", GREY, False),
    # MD2 names: "started" shows the slash because pressing it stops sharing.
    "sharing-stopped": ("fontawesome/solid-share.svg", WHITE, False),
    "sharing-started": ("fontawesome/solid-share.svg", WHITE, True),
    "sharing-disabled": ("fontawesome/solid-share.svg", GREY, False),
    "recording-started": ("fontawesome/solid-circle-stop.svg", WHITE, False),
    "recording-stopped": ("fontawesome/regular-circle-stop.svg", WHITE, False),
    "recording-disabled": ("fontawesome/regular-circle-stop.svg", GREY, False),
    "leave-meeting": ("mutedeck/portal-exit.svg", WHITE, False),
    "leave-meeting-disabled": ("mutedeck/portal-exit.svg", GREY, False),
    "live-caption": ("fontawesome/regular-closed-captioning.svg", WHITE, False),
    "raise-hand": ("fontawesome/solid-hand.svg", WHITE, False),
    "react-heart": ("fontawesome/solid-heart.svg", WHITE, False),
    "react-love": ("fontawesome/solid-heart.svg", WHITE, False),
    "react-like": ("fontawesome/solid-thumbs-up.svg", WHITE, False),
    "react-thumbs-up": ("fontawesome/solid-thumbs-up.svg", WHITE, False),
    "react-laugh": ("fontawesome/solid-face-laugh.svg", WHITE, False),
    "react-wow": ("fontawesome/solid-face-surprise.svg", WHITE, False),
    "toggle-chat": ("fontawesome/solid-comment-dots.svg", WHITE, False),
    "toggle-participants": ("fontawesome/solid-people-arrows.svg", WHITE, False),
}


def read_glyph(path):
    text = path.read_text(encoding="utf-8")
    text = re.sub(r"<\?xml[^>]*\?>", "", text)
    text = re.sub(r"<!--.*?-->", "", text, flags=re.S)
    match = re.search(r"<svg\b[^>]*\bviewBox=\"([^\"]+)\"[^>]*>(.*)</svg>", text, flags=re.S)
    if not match:
        raise ValueError(f"{path} has no root <svg> with a viewBox")
    _, _, width, height = (float(value) for value in match.group(1).replace(",", " ").split())
    return match.group(1), width, height, match.group(2).strip()


def compose(source, colour, crossed, size):
    view_box, width, height, body = read_glyph(source)
    scale = size * GLYPH_FRACTION / max(width, height)
    glyph_width, glyph_height = width * scale, height * scale
    x, y = (size - glyph_width) / 2, (size - glyph_height) / 2
    # A nested <svg> maps the source viewBox onto the glyph box, and currentColor covers sources that
    # use it instead of a fill.
    parts = [
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{size}" height="{size}" viewBox="0 0 {size} {size}">',
        f'<svg x="{x:.2f}" y="{y:.2f}" width="{glyph_width:.2f}" height="{glyph_height:.2f}" '
        f'viewBox="{view_box}" fill="{colour}" color="{colour}">{body}</svg>',
    ]
    if crossed:
        inset = size * (1 - SLASH_FRACTION) / 2
        parts.append(
            f'<line x1="{inset:.2f}" y1="{size - inset:.2f}" x2="{size - inset:.2f}" y2="{inset:.2f}" '
            f'stroke="{RED}" stroke-width="{size * SLASH_WIDTH_FRACTION:.2f}"/>')
    parts.append("</svg>")
    return "".join(parts)


def rasterize(chrome, svg_path, png_path, size):
    subprocess.run(
        [chrome, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--no-first-run",
            "--default-background-color=00000000", f"--window-size={size},{size}",
            f"--screenshot={png_path}", svg_path.as_uri()],
        check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    if not png_path.is_file():
        raise RuntimeError(f"Chrome did not write {png_path}")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("icon_src", type=pathlib.Path)
    parser.add_argument("--chrome", required=True)
    parser.add_argument("--size", type=int, default=512)
    args = parser.parse_args()

    vector_dir = args.icon_src / "vector"
    with tempfile.TemporaryDirectory() as work:
        for name, (source, colour, crossed) in ICONS.items():
            svg_path = pathlib.Path(work) / f"{name}.svg"
            svg_path.write_text(compose(vector_dir / source, colour, crossed, args.size), encoding="utf-8")
            rasterize(args.chrome, svg_path, args.icon_src / f"{name}.png", args.size)
            print(f"rendered {name}.png")
    return 0


if __name__ == "__main__":
    sys.exit(main())
