#!/bin/bash
# DMG для переноса на другой Mac (plans/final-session.md, H2 и H4).
#
#   scripts/make-dmg.sh   → build/EpicPlayer-<версия>.dmg
#
# Внутри — EpicPlayer.app с ad-hoc подписью и ссылка на /Applications.
# На чужом Mac Gatekeeper предупредит: «Открыть всё равно» в Системных
# настройках → Конфиденциальность и безопасность. Developer ID и
# нотаризация — отдельный шаг (porting-plan.md, «Распространение»).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
APP="$ROOT/build/EpicPlayer.app"

# Всегда пересобирать: иначе в образ попадёт устаревший бандл.
"$ROOT/scripts/build-app.sh"

VERSION="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' "$APP/Contents/Info.plist")"
DMG="$ROOT/build/EpicPlayer-$VERSION.dmg"

STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT
cp -R "$APP" "$STAGE/"
ln -s /Applications "$STAGE/Applications"

echo "→ $DMG"
rm -f "$DMG"
hdiutil create -volname "E-pic player" -srcfolder "$STAGE" -ov -format UDZO "$DMG" >/dev/null

echo "Готово: $DMG"
