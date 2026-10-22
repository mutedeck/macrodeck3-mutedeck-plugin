#!/usr/bin/env bash
# Renders the vector icons in icon-src/vector/ to icon-src/*.png, then rebuilds
# src/MuteDeckPlugin/icon-packs/mutedeck.macroDeckIconPack from icon-src/*.png and registers it in
# manifest.json under the key "mutedeck", which the plugin's live button icons address.
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/_env.sh"

CWEBP="${CWEBP:-$(command -v cwebp || true)}"
[[ -n "$CWEBP" ]] || CWEBP=/opt/homebrew/bin/cwebp
[[ -x "$CWEBP" ]] || die "cwebp not found; install it with 'brew install webp' or set CWEBP"

CHROME="${CHROME:-/Applications/Google Chrome.app/Contents/MacOS/Google Chrome}"
[[ -x "$CHROME" ]] || die "Chrome not found at $CHROME; set CHROME to a Chrome or Chromium binary"

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

python3 -I "$REPO_ROOT/scripts/render_icons.py" "$REPO_ROOT/icon-src" --chrome "$CHROME"
python3 -I "$REPO_ROOT/scripts/make_icon_pack.py" "$REPO_ROOT/icon-src" "$work/mutedeck.macroDeckIconPack" --cwebp "$CWEBP"
mdp icon-pack add "$work/mutedeck.macroDeckIconPack" --key mutedeck --force --source "$PROJECT_DIR"
mdp icon-pack list --source "$PROJECT_DIR"
