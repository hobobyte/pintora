#!/usr/bin/env bash
# Create a drag-and-drop macOS disk image from a published app bundle.
# Usage: make-pintora-macos-dmg.sh <app-bundle> <output.dmg>
set -euo pipefail
APP="${1%/}"
OUTPUT="$2"
[[ -d "$APP" && -f "$APP/Contents/Info.plist" ]] || { echo "Invalid app bundle: $APP" >&2; exit 1; }
STAGE="$(mktemp -d "${TMPDIR:-/tmp}/pintora-dmg.XXXXXX")"
trap 'rm -rf "$STAGE"' EXIT
mkdir "$STAGE/Pintora"
ditto "$APP" "$STAGE/Pintora/Pintora.app"
ln -s /Applications "$STAGE/Pintora/Applications"
cp LICENSE.md CHANGELOG.md \
    Pinta.Resources/icons/pinta-icons.md "$STAGE/Pintora/"
cp third-party/pinta/LICENSE.md "$STAGE/Pintora/LICENSE-PINTA.md"
cp third-party/paint.net/LICENSE.md "$STAGE/Pintora/LICENSE-PAINT.NET.md"
hdiutil create -quiet -volname "Pintora" -srcfolder "$STAGE/Pintora" -fs HFS+ -format UDZO "$OUTPUT"
hdiutil verify -quiet "$OUTPUT"
echo "Built $OUTPUT (unsigned and not notarized)"
