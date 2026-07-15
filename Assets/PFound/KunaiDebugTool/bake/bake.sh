#!/usr/bin/env bash
# Bake Iosevka Nerd Font Mono → BMFont atlas for KunaiDebugTool.
# Output: Assets/GameSpecific/KunaiDebugTool/IosevkaKunai.{fnt,png}
#
# Prerequisites:
#   1. fontbm  → brew install vladimirgamalyan/tap/fontbm   (or build from https://github.com/vladimirgamalyan/fontbm)
#   2. Iosevka Nerd Font Mono Regular .ttf → place at: bake/IosevkaNerdFontMono-Regular.ttf
#      Download: https://github.com/ryanoasis/nerd-fonts/releases/latest (file: Iosevka.zip)
#
# Re-bake: edit chars.txt, rerun this script. Code does not change.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../../.." && pwd)"
FONT_TTF="$SCRIPT_DIR/IosevkaNerdFontMono-Regular.ttf"
CHARS_FILE="$SCRIPT_DIR/chars.txt"
OUT_DIR="$REPO_ROOT/Assets/GameSpecific/KunaiDebugTool"
OUT_NAME="IosevkaKunai"

if [[ ! -f "$FONT_TTF" ]]; then
  echo "ERROR: missing $FONT_TTF"
  echo "Download Iosevka Nerd Font from https://github.com/ryanoasis/nerd-fonts/releases/latest"
  echo "Extract IosevkaNerdFontMono-Regular.ttf into $SCRIPT_DIR/"
  exit 1
fi

FONTBM="${FONTBM_BIN:-}"
if [[ -z "$FONTBM" ]]; then
  if command -v fontbm >/dev/null 2>&1; then
    FONTBM="fontbm"
  elif [[ -x "$HOME/Development/fontbm/build/fontbm" ]]; then
    FONTBM="$HOME/Development/fontbm/build/fontbm"
  else
    echo "ERROR: fontbm not found. Set FONTBM_BIN or add to PATH."
    echo "Build: https://github.com/vladimirgamalyan/fontbm"
    exit 1
  fi
fi

mkdir -p "$OUT_DIR"

"$FONTBM" \
  --font-file "$FONT_TTF" \
  --chars-file "$CHARS_FILE" \
  --font-size 28 \
  --texture-size 512x512 \
  --texture-name-suffix none \
  --color 255,255,255 \
  --padding-up 1 --padding-right 1 --padding-down 1 --padding-left 1 \
  --data-format txt \
  --output "$OUT_DIR/$OUT_NAME"

# Unity treats .txt as TextAsset; .fnt is unrecognized. Rename for direct asset reference.
rm -f "$OUT_DIR/$OUT_NAME.fnt.txt" "$OUT_DIR/$OUT_NAME.fnt.txt.meta"
mv "$OUT_DIR/$OUT_NAME.fnt" "$OUT_DIR/$OUT_NAME.fnt.txt"

# Clean up legacy suffixed PNG from previous bakes (if any).
rm -f "$OUT_DIR/${OUT_NAME}_0.png" "$OUT_DIR/${OUT_NAME}_0.png.meta"

echo "Baked → $OUT_DIR/$OUT_NAME.fnt.txt + $OUT_DIR/$OUT_NAME.png"
