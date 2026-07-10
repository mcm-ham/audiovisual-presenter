#!/bin/bash
# Packages the Avalonia operator UI into a self-contained "Audiovisual Presenter.app".
# Usage: build/package-macos.sh [runtime-id]   (default osx-arm64; pass osx-x64 for Intel)
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
RID="${1:-osx-arm64}"
CONFIG=Release
OUT="$ROOT/artifacts/macos/$RID"
PUBLISH="$OUT/publish"
APP="$OUT/Audiovisual Presenter.app"
VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$ROOT/src/Presenter.App/Presenter.App.csproj")"

rm -rf "$OUT"
dotnet publish "$ROOT/src/Presenter.App" -c "$CONFIG" -r "$RID" --self-contained -o "$PUBLISH"

mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R "$PUBLISH/." "$APP/Contents/MacOS/"

# icns from the shared 64x64 Projector.ico; sizes above 64 are upscaled, which is
# fine on screen until a larger master icon exists
ICONSET="$OUT/Projector.iconset"
mkdir -p "$ICONSET"
for SIZE in 16 32 64 128 256 512; do
  sips -s format png -z $SIZE $SIZE "$ROOT/src/Presenter.App/Icons/Projector.ico" \
    --out "$ICONSET/icon_${SIZE}x${SIZE}.png" >/dev/null
done
for SIZE in 16 32 128 256; do
  DOUBLE=$((SIZE * 2))
  cp "$ICONSET/icon_${DOUBLE}x${DOUBLE}.png" "$ICONSET/icon_${SIZE}x${SIZE}@2x.png"
done
iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/Projector.icns"
rm -rf "$ICONSET"

cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>Audiovisual Presenter</string>
    <key>CFBundleDisplayName</key>
    <string>Audiovisual Presenter</string>
    <key>CFBundleIdentifier</key>
    <string>org.minsoft.audiovisualpresenter</string>
    <key>CFBundleExecutable</key>
    <string>Presenter</string>
    <key>CFBundleIconFile</key>
    <string>Projector.icns</string>
    <key>CFBundleShortVersionString</key>
    <string>${VERSION}</string>
    <key>CFBundleVersion</key>
    <string>${VERSION}</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>LSMinimumSystemVersion</key>
    <string>12.0</string>
    <key>LSApplicationCategoryType</key>
    <string>public.app-category.presentation</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
PLIST

# ad-hoc signature so Gatekeeper lets it launch locally; replace "-" with a
# Developer ID identity for distribution
codesign --force --deep --sign - "$APP"

echo "Packaged: $APP"
