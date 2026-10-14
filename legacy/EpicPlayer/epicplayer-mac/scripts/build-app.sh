#!/bin/bash
# Сборка EpicPlayer.app из SwiftPM без Xcode-проекта.
# См. plans/app-design.md, раздел 2.
#
#   scripts/build-app.sh            → build/EpicPlayer.app (release)
#   CONFIG=debug scripts/build-app.sh
#
# Подпись ad-hoc: запускается на этой машине, на чужой — через
# «Open Anyway». Developer ID и нотаризация — отдельный шаг.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CONFIG="${CONFIG:-release}"
OUT="$ROOT/build"
APP="$OUT/EpicPlayer.app"
ICON_SOURCE="$ROOT/../EpicPlayer/icon.ico"

cd "$ROOT"

echo "→ swift build ($CONFIG)"
swift build -c "$CONFIG" --product EpicPlayerMac
BIN_DIR="$(swift build -c "$CONFIG" --show-bin-path)"

echo "→ $APP"
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp "$BIN_DIR/EpicPlayerMac" "$APP/Contents/MacOS/EpicPlayer"
cp "$ROOT/Support/Info.plist" "$APP/Contents/Info.plist"
printf 'APPL????' > "$APP/Contents/PkgInfo"

# Иконка из icon.ico Windows-версии: самый большой кадр (256×256) → icns.
# Без иконки приложение работает, поэтому ошибка здесь не фатальна.
make_icon() {
    local work
    work="$(mktemp -d)"
    trap 'rm -rf "$work"' RETURN

    sips -s format png "$ICON_SOURCE" --out "$work/icon.png" >/dev/null
    local set="$work/AppIcon.iconset"
    mkdir "$set"
    for size in 16 32 128 256; do
        sips -z $size $size "$work/icon.png" --out "$set/icon_${size}x${size}.png" >/dev/null
        local double=$((size * 2))
        sips -z $double $double "$work/icon.png" --out "$set/icon_${size}x${size}@2x.png" >/dev/null
    done
    iconutil -c icns "$set" -o "$APP/Contents/Resources/AppIcon.icns"
}

if [[ -f "$ICON_SOURCE" ]] && make_icon; then
    echo "→ иконка из $(basename "$ICON_SOURCE")"
else
    echo "! иконка не создана, сборка продолжается без неё" >&2
fi

echo "→ ad-hoc подпись"
codesign --force --sign - "$APP"

echo "Готово: $APP"
echo "Запуск: open \"$APP\""
