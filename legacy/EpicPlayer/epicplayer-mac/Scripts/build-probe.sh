#!/bin/bash
# Сборка интеграционного зонда SocketProbe как .app бандла.
#
# Почему нужен бандл: в процессе `swift test` настоящие сокеты не
# работают. Бинарник, который собирает SwiftPM, не имеет Info.plist,
# а macOS 15+ ограничивает такие процессы — bind на loopback возвращает
# "Can't assign requested address". Собранный как .app зонд имеет
# Info.plist с NSLocalNetworkUsageDescription и с сокетами работает.
#
# Запуск: ./Scripts/build-probe.sh
# Результат: epicplayer-mac/.build/SocketProbe.app
# Потом:    open epicplayer-mac/.build/SocketProbe.app
#
# Либо разово из терминала, чтобы увидеть вывод:
#   epicplayer-mac/.build/SocketProbe.app/Contents/MacOS/SocketProbe

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PACKAGE_DIR="$(dirname "$SCRIPT_DIR")"
APP_NAME="SocketProbe"
BUNDLE_ID="com.epicplayer.socketprobe"
VERSION="0.1.0"
BUILD_NUMBER="1"
MIN_MACOS="15.0"

cd "$PACKAGE_DIR"

echo "==> Сборка Release"
swift build -c release --package-path "$PACKAGE_DIR" --product "$APP_NAME"

BIN_DIR="$(swift build -c release --package-path "$PACKAGE_DIR" --show-bin-path)"
APP_DIR="$PACKAGE_DIR/.build/$APP_NAME.app"

echo "==> Сборка бандла $APP_DIR"
rm -rf "$APP_DIR"
mkdir -p "$APP_DIR/Contents/MacOS"

cp "$BIN_DIR/$APP_NAME" "$APP_DIR/Contents/MacOS/$APP_NAME"

# Тот же Info.plist, что у приложения: объявление о локальной сети нужно,
# чтобы сокеты работали.
sed -e "s/\$(EXECUTABLE_NAME)/$APP_NAME/g" \
    -e "s/\$(PRODUCT_BUNDLE_IDENTIFIER)/$BUNDLE_ID/g" \
    -e "s/\$(PRODUCT_NAME)/$APP_NAME/g" \
    -e "s/\$(MARKETING_VERSION)/$VERSION/g" \
    -e "s/\$(CURRENT_PROJECT_VERSION)/$BUILD_NUMBER/g" \
    -e "s/\$(MACOSX_DEPLOYMENT_TARGET)/$MIN_MACOS/g" \
    "$PACKAGE_DIR/Sources/EpicPlayerMac/Resources/Info.plist" \
    > "$APP_DIR/Contents/Info.plist"

printf 'APPL????' > "$APP_DIR/Contents/PkgInfo"

echo "==> Локальная подпись (ad-hoc)"
codesign --force --sign - "$APP_DIR" 2>/dev/null \
    || echo "   codesign недоступен, бандл собран без подписи"

echo
echo "Готово: $APP_DIR"
echo "Вывод в терминале:"
echo "  $APP_DIR/Contents/MacOS/$APP_NAME"