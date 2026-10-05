#!/bin/bash
# Сборка приложения EpicPlayerMac как .app бандла.
#
# SwiftPM собирает исполняемый файл, но не собирает бандл: без него
# приложение не запустится двойным щелчком, не покажет меню macOS
# и не получит доступ к локальной сети. Здесь недостающие шаги
# делаются вручную.
#
# Запуск: ./Scripts/build-app.sh
# Результат: epicplayer-mac/.build/EpicPlayerMac.app

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PACKAGE_DIR="$(dirname "$SCRIPT_DIR")"
APP_NAME="EpicPlayerMac"
BUNDLE_ID="com.epicplayer.mac"
MARKETING_VERSION="0.1.0"
BUILD_NUMBER="1"
MIN_MACOS="15.0"

cd "$PACKAGE_DIR"

echo "==> Сборка Release"
swift build -c release --package-path "$PACKAGE_DIR"

BIN_DIR="$(swift build -c release --package-path "$PACKAGE_DIR" --show-bin-path)"
APP_DIR="$PACKAGE_DIR/.build/$APP_NAME.app"

echo "==> Сборка бандла $APP_DIR"
rm -rf "$APP_DIR"
mkdir -p "$APP_DIR/Contents/MacOS" "$APP_DIR/Contents/Resources"

cp "$BIN_DIR/$APP_NAME" "$APP_DIR/Contents/MacOS/$APP_NAME"

# Info.plist из исходников: в нём стоят переменные сборки.
sed -e "s/\$(EXECUTABLE_NAME)/$APP_NAME/g" \
    -e "s/\$(PRODUCT_BUNDLE_IDENTIFIER)/$BUNDLE_ID/g" \
    -e "s/\$(PRODUCT_NAME)/$APP_NAME/g" \
    -e "s/\$(MARKETING_VERSION)/$MARKETING_VERSION/g" \
    -e "s/\$(CURRENT_PROJECT_VERSION)/$BUILD_NUMBER/g" \
    -e "s/\$(MACOSX_DEPLOYMENT_TARGET)/$MIN_MACOS/g" \
    "$PACKAGE_DIR/Sources/$APP_NAME/Resources/Info.plist" \
    > "$APP_DIR/Contents/Info.plist"

printf 'APPL????' > "$APP_DIR/Contents/PkgInfo"

# Подпись для локального запуска. Без Developer ID на чужом компьютере
# потребуется нотарификация, см. plans/porting-plan.md, раздел
# «Распространение».
echo "==> Локальная подпись (ad-hoc)"
codesign --force --sign - "$APP_DIR" 2>/dev/null \
    || echo "   codesign недоступен, приложение собрано без подписи"

echo
echo "Готово: $APP_DIR"
echo "Запуск: open $APP_DIR"