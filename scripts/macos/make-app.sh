#!/usr/bin/env bash
# Wraps a published osx-* folder into "Pixel Tuner Studio.app" with an ad-hoc signature.
# Usage: scripts/macos/make-app.sh <publish-dir> <output-dir>
# Developer ID signing and notarization come later, once the Apple account is set up.
set -euo pipefail

publish_dir="$1"
out_dir="$2"
root="$(cd "$(dirname "$0")/../.." && pwd)"
app="$out_dir/Pixel Tuner Studio.app"

rm -rf "$app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
cp -R "$publish_dir"/. "$app/Contents/MacOS/"
cp "$root/src/Heads/PixelTuner.Desktop/macOS/Info.plist" "$app/Contents/Info.plist"
chmod +x "$app/Contents/MacOS/PixelTunerStudio"

if command -v codesign >/dev/null; then
  codesign --force --deep --sign - "$app"
fi
echo "Created $app"
