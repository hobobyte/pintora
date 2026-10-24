#!/usr/bin/env bash
# Package a self-contained linux-x64 publish directory as a Debian .deb.
# Usage: make-pintora-linux-deb.sh <publish-dir> <version> <output-dir>
set -euo pipefail

PUBLISH_DIR="$1"
VERSION="$2"
OUTPUT_DIR="$3"
STAGING="$(mktemp -d "${TMPDIR:-/tmp}/pintora-deb.XXXXXX")"
trap 'rm -rf "$STAGING"' EXIT

mkdir -p "$STAGING/usr/lib/pintora" "$STAGING/usr/bin" \
    "$STAGING/usr/share/applications" "$STAGING/usr/share/icons/hicolor/96x96/apps" \
    "$STAGING/usr/share/doc/pintora" "$STAGING/DEBIAN" "$OUTPUT_DIR"
cp -R "$PUBLISH_DIR"/. "$STAGING/usr/lib/pintora/"
chmod +x "$STAGING/usr/lib/pintora/Pintora"
ln -s /usr/lib/pintora/Pintora "$STAGING/usr/bin/pintora"
cp installer/avalonia/pintora.desktop "$STAGING/usr/share/applications/"
cp Pinta.Resources/icons/hicolor/96x96/apps/com.github.PintaProject.Pinta.png \
    "$STAGING/usr/share/icons/hicolor/96x96/apps/pintora.png"
cp license-mit.txt license-pdn.txt CHANGELOG.md \
    "$STAGING/usr/share/doc/pintora/"
sed "s/__VERSION__/$VERSION/" installer/avalonia/pintora.control > "$STAGING/DEBIAN/control"
echo "Installed-Size: $(du -sk "$STAGING/usr" | cut -f1)" >> "$STAGING/DEBIAN/control"

echo '2.0' > "$STAGING/debian-binary"
tar -czf "$STAGING/control.tar.gz" --owner=root --group=root --numeric-owner -C "$STAGING/DEBIAN" .
tar -czf "$STAGING/data.tar.gz" --owner=root --group=root --numeric-owner -C "$STAGING" usr
DEB_PATH="$OUTPUT_DIR/pintora_${VERSION}_amd64.deb"
(cd "$STAGING" && ar rc "$DEB_PATH" debian-binary control.tar.gz data.tar.gz)
echo "Built $DEB_PATH"
