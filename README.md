# Pixel Tuner

Платформа для создания шоу и управления пиксельным реквизитом. Следующее поколение
PixelTuner 2: Windows, macOS, Android и iOS из одного кода. Два приложения:

- **Pixel Tuner Studio 4.0** (Windows и macOS, `com.pixeltuner.studio`) — редактор
  проектов: импорт .ptp, таймлайны, сборка .bin и заливка в устройства.
- **Pixel Tuner Stage 1.0** (iPhone, iPad, Android, `com.pixeltuner.stage`) — открывает
  .pxpkg, играет звук шоу и синхронизирует по нему устройства в сети.

Общий код говорит от имени «Pixel Tuner», без версии и без Studio или Stage. Полное
имя приложения встречается только в его запускалке (`src/Heads`) и в заголовке окна.

Стек: .NET 10, Avalonia 12 (Fluent), CommunityToolkit.Mvvm. План и решения —
в документе «PixelTuner нового поколения».

## Структура

```text
src/
  PixelTuner.Core/        модель проекта (ShowProject) и сборка .bin, без UI и платформенного кода
  PixelTuner.Package/     формат .pxpkg: чтение, запись, проверка целостности, импорт .ptp
  PixelTuner.Sync/        протокол устройств (UDP 333), поиск устройств, Art-Net
  PixelTuner.UI/          общий интерфейс Avalonia: экраны, тема, строки ru/en/de
  PixelTuner.UI/Product.cs  какое приложение запущено: Studio или Stage
  Heads/
    PixelTuner.Studio.Desktop/  Studio на Windows и macOS (+ Info.plist для .app)
    PixelTuner.Stage.Android/   Stage на Android
    PixelTuner.Stage.iOS/       Stage на iPhone и iPad
tests/                    тесты ядра; протокол сверяется с эталонными векторами
scripts/                  сборка .app для macOS, иконки (icons/make-icons.py), эталоны для CI
legacy/                   PixelTuner 2, Common, Communication, WpfTools, EpicPlayer с историей
```

Платформы — это проекты в одной ветке, а не отдельные ветки. Платформенный код
живёт только в `src/Heads`; всё остальное общее. Если общему коду нужна
возможность платформы (звук, файлы, сеть), заводится интерфейс в общем коде и
реализация в каждом head.

## Сборка

Нужен .NET SDK 10 (`global.json`).

```bash
dotnet test tests/PixelTuner.Sync.Tests
dotnet test tests/PixelTuner.Package.Tests
dotnet run --project src/Heads/PixelTuner.Studio.Desktop
```

Мобильные платформы:

```bash
dotnet workload install android ios
dotnet build src/Heads/PixelTuner.Stage.Android -t:Run          # на подключённом устройстве или эмуляторе
dotnet build src/Heads/PixelTuner.Stage.iOS -r iossimulator-arm64  # только на Mac с Xcode
```

Решение целиком: `PixelTunerStudio.slnx` (Visual Studio 2022 17.13+, Rider 2024.3+).

macOS-приложение: `dotnet publish src/Heads/PixelTuner.Studio.Desktop -c Release -r osx-arm64 --self-contained -o out/osx`,
затем `scripts/macos/make-app.sh out/osx out/app`.

PixelTuner 2 по-прежнему собирается из `legacy/PixelTuner2` (см. `legacy/README.md`).

## Формат проекта .pxpkg

Один файл со всем проектом: ZIP-архив, который можно передать через AirDrop,
почту или мессенджер и открыть на любой платформе.

```text
mimetype            первым и без сжатия: application/vnd.pixeltuner.package+zip
manifest.json       версия формата, id проекта, список медиа с SHA-256 и размерами
project.json        проект (ShowProject)
media/audio/<sha256>.mp3
media/images/<sha256>.png
media/video/<sha256>.mp4
cache/...           то, что можно пересчитать: волна звука, миниатюры, превью
export/<device>.bin скомпилированные файлы устройств (необязательно)
```

- Медиа хранятся по хэшу содержимого: один файл, использованный много раз, лежит один раз.
- Уже сжатые форматы (MP3, JPG, PNG, MP4…) не сжимаются повторно и читаются прямо из архива.
- Сохранение атомарное: запись во временный файл рядом и замена.
- `minReaderVersion` в манифесте: старое приложение не откроет слишком новый пакет
  и скажет об этом, новое мигрирует старый.
- Тип файла зарегистрирован в Info.plist (`com.pixeltuner.pxpkg`) и в Android
  intent-filter, чтобы файл открывался сразу в приложении.

## Импорт проектов PixelTuner 2

`PtpImporter.Import("show.ptp", "show.pxpkg")` переносит проект .ptp в пакет:

- таймлайны, элементы, разметку светодиодов и рисунок холста — в `ShowProject`;
- звук, картинки и GIF — в `media/` по хэшу. Файл ищется по сохранённому пути,
  затем рядом с проектом. Если файла нигде нет, вызывается `FindMissing`, один
  раз на каждый путь. Если файл так и не нашёлся, он попадает в `MissingMedia`,
  а элемент остаётся без медиа, но со старым путём в `SourcePath`.

Сборка .bin работает с моделью. Прямой экспорт из .ptp (`Ptp*Export`) тоже идёт
через импорт, поэтому сверка с эталонами PixelTuner 2 проверяет и импорт.
`PixelTuner.Package.Tests` сравнивает .bin из пакета с .bin из исходного .ptp.

## Протокол устройств

Спецификация: `legacy/EpicPlayer/epicplayer-mac/plans/protocol.md`.
Эталонные байты: `legacy/EpicPlayer/epicplayer-mac/Tests/Fixtures/*.json` — их
проверяют тесты `PixelTuner.Sync.Tests`, Swift-версия EpicPlayer и WindowsReference.
Протокол меняется только вместе с этими файлами.

## Платформенные требования

- **iOS:** для UDP broadcast нужен entitlement
  `com.apple.developer.networking.multicast`, Apple выдаёт его по
  [заявке](https://developer.apple.com/contact/request/networking-multicast).
  Без него сборка на устройство не подпишется. Сборка для симулятора работает.
- **iOS и macOS:** при первом запуске система спрашивает доступ к локальной
  сети (`NSLocalNetworkUsageDescription`); без него команды не дойдут до устройств.
- **Android:** приём ответов поиска устройств требует `MulticastLock`
  (разрешение `CHANGE_WIFI_MULTICAST_STATE` уже в манифесте).

## Процесс

- Одна основная ветка `main`. Работа идёт в коротких ветках задач и попадает в
  `main` через PR.
- CI (`.github/workflows/ci.yml`) на каждый PR собирает Windows, macOS, Android и
  iOS и гоняет тесты. Красная платформа = красный PR.
- PR сливаются обычным merge-коммитом (история `legacy/` и перенос кода важны).
- Версии задаются в одном месте, в `Directory.Build.props`: `StudioVersion` и `StageVersion`.
