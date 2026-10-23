#!/usr/bin/env bash
# Bundle a self-contained linux-x64 publish directory as an AppImage.
# Usage: make-pintora-appimage.sh <publish-dir> <output-dir>
set -euo pipefail

PUBLISH_DIR="$1"
OUTPUT_DIR="$2"
APPDIR="$OUTPUT_DIR/Pintora.AppDir"
APPIMAGETOOL="${APPIMAGETOOL:-appimagetool}"
mkdir -p "$APPDIR/usr/bin" "$OUTPUT_DIR"
cp -R "$PUBLISH_DIR"/. "$APPDIR/usr/bin/"
chmod +x "$APPDIR/usr/bin/Pintora"
cp installer/avalonia/pintora.desktop "$APPDIR/Pintora.desktop"
cp Pinta.Resources/icons/hicolor/96x96/apps/com.github.PintaProject.Pinta.png "$APPDIR/pintora.png"
cat > "$APPDIR/AppRun" <<'APPRUN'
#!/usr/bin/env bash
HERE="$(dirname "$(readlink -f "$0")")"
exec "$HERE/usr/bin/Pintora" "$@"
APPRUN
chmod +x "$APPDIR/AppRun"
ARCH=x86_64 "$APPIMAGETOOL" "$APPDIR" "$OUTPUT_DIR/Pintora-x86_64.AppImage"
