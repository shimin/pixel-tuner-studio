# Дизайн приложения: фазы 3–5

Детальная проработка аудио, интерфейса и интеграции поверх `EpicCore`.
Общий план и статусы — в [`porting-plan.md`](porting-plan.md), формат
пакетов — в [`protocol.md`](protocol.md).

Дата: 2026-10-14. Статус: код по этому дизайну написан без компилятора;
сборка, тесты и проверка в сети — последний этап (см. porting-plan.md).

Горячих клавиш нет, как в Windows-версии: пробел как Play/Pause
перехватывался бы в полях Art-Net.

## 1. Цели и границы

Перенести поведение главного окна и двух окон device lookup Windows-версии
один в один, кроме мест, где Windows-поведение — явная ошибка. Каждое
отступление перечислено в разделе 9 и может быть отменено владельцем.

Не переносится:

- поле Multicast (скрыто и не используется в Windows);
- кнопка Close (скрыта);
- `MediaStatusCallback` — перезагрузка файла каждые 500 мс в состоянии
  stop. Обходной путь для `MediaPlayer` WPF, у AVFoundation такой
  проблемы нет;
- «разблокировка» звука через play + stop на нулевой громкости — заменена
  на `prepareToPlay()`.

## 2. Сборка без Xcode-проекта

**Решение:** приложение — исполняемый таргет SwiftPM, `.app` собирается
скриптом. Xcode-проект не заводится.

Причины:

- `project.pbxproj` нельзя надёжно написать без Xcode, а SwiftPM-манифест —
  можно, и он же проверяется `swift build`;
- Xcode открывает `Package.swift` напрямую — отладка, превью SwiftUI,
  тесты доступны и так;
- `Info.plist` с `NSLocalNetworkUsageDescription` нужен только в бандле;
  скрипт кладёт его туда сам.

```
epicplayer-mac/
├── Package.swift
├── Sources/
│   ├── EpicCore/          протокол и сеть (фазы 1–2)
│   ├── EpicPlayerKit/     модели, аудио, настройки — без SwiftUI
│   ├── EpicPlayerMac/     SwiftUI: App и Views
│   └── EpicProbe/         CLI для проверки на устройстве
├── Support/
│   └── Info.plist
├── scripts/
│   └── build-app.sh       swift build → EpicPlayer.app → ad-hoc подпись
└── Tests/
    ├── EpicCoreTests/
    └── EpicPlayerKitTests/
```

`EpicPlayerKit` отделён от `EpicPlayerMac`, чтобы логика проверялась
`swift test` без UI: `@testable import` исполняемого таргета работает
плохо, а SwiftUI в тестах не нужен.

Платформа поднимается до macOS 15 (решение владельца). Для этого
`swift-tools-version:6.0` — `.macOS(.v15)` появился в этой версии
манифеста — и `swiftLanguageModes: [.v5]`, чтобы строгая конкурентность
не блокировала работу. Нужен Xcode 16+.

`build-app.sh`:

1. `swift build -c release --product EpicPlayerMac`
2. `EpicPlayer.app/Contents/MacOS/EpicPlayer` ← бинарник
3. `Contents/Info.plist` ← `Support/Info.plist`
4. `Contents/Resources/AppIcon.icns` ← `EpicPlayer/icon.ico` через `sips`
   и `iconutil`; при ошибке конвертации — сборка без иконки, с
   предупреждением
5. `codesign --force --sign -` — ad-hoc подпись. Entitlements не нужны:
   без песочницы сеть доступна и так. Developer ID, hardened runtime и
   нотаризация — отдельный шаг распространения.

## 3. Слои и потоки

```
SwiftUI Views (EpicPlayerMac)
     │  @Bindable / вызовы методов
PlayerModel, DeviceLookupModel   @MainActor @Observable   (EpicPlayerKit)
     │                     │
AudioEngine (протокол)   ShowTransport (протокол)   ArtNetSource (протокол)
     │                     │                          │
AVAudioPlayer            ShowConnection            ArtNetReceiver   (EpicCore)
```

- **Всё состояние UI — на главном акторе.** Модели помечены `@MainActor`.
- **Сокеты** вызывают колбэки на своих очередях. Адаптер в
  `EpicPlayerKit` перекладывает их на главную очередь
  (`DispatchQueue.main.async`) до того, как они коснутся модели.
- **DMX** приходит ~40 раз в секунду на universe. Перекладывать каждый
  пакет на главный поток дёшево, но бессмысленно: `DmxTrigger` работает
  на очереди сокета, на главный поток уходит только сработавшая
  команда. Триггер живёт в адаптере, его настройки обновляются под
  замком.
- **Протоколы над EpicCore** нужны для тестов: модели получают фабрики
  и в тестах подставляются фейки без сокетов и без звука.

## 4. Аудио — `AudioEngine`

```swift
@MainActor protocol AudioEngine: AnyObject {
    var isLoaded: Bool { get }
    var duration: TimeInterval { get }
    var currentTime: TimeInterval { get }
    var isPlaying: Bool { get }
    var volume: Float { get set }
    var onFinish: (() -> Void)? { get set }
    func load(_ url: URL) throws
    func play()
    func pause()
    func stop()          // останавливает И сбрасывает позицию в 0
}
```

Реализация `AVAudioEngineAdapter` поверх `AVAudioPlayer`:

- `load` — `AVAudioPlayer(contentsOf:)`, `prepareToPlay()`. Файл читается
  потоково, в память целиком не грузится — замена на `AVPlayer` не нужна.
  Если окажется иначе на длинных записях, меняется только этот класс.
- `stop` — `AVAudioPlayer.stop()` **не** сбрасывает `currentTime`, в отличие
  от `MediaPlayer.Stop()` в WPF. Сброс делается явно.
- Окончание трека — `AVAudioPlayerDelegate.audioPlayerDidFinishPlaying`,
  делегат — отдельный `NSObject`, вызывает `onFinish`.
- Громкость 0…1, как слайдер Windows.

## 5. `PlayerModel` — поведение главного окна

### Состояние

| Поле | Тип | Источник в Windows |
| --- | --- | --- |
| `status` | `.stopped / .playing / .paused` | `_playbackStatus` |
| `trackTitle` | `String?` | `songTitle` |
| `position`, `duration` | `TimeInterval` | `player.Position`, `NaturalDuration` |
| `volume` | `Double`, 0.5 по умолчанию | `volumeSlider` |
| `interfaces`, `selectedInterface` | `[NetworkInterface]`, `NetworkInterface?` | `boxInterfaces` |
| `networkError` | `String?` | `playStatus.Content = ex.Message` |
| `isLookupOpen` | `Bool` | `_suppressPlaybackCommands` |
| `artNet` | `ArtNetSettings` | панель ArtNet settings |

### Команды

| Действие | Локально | В сеть | Windows |
| --- | --- | --- | --- |
| Open audio | загрузить, статус stop | — | `loadFile` ставит статус «Play», не играя — ошибка, не переносится |
| Play | `play()`, статус play | play @ позиция | `Play` |
| Pause | только если не stop; `pause()` | pause @ позиция | `Pause` |
| Stop | `stop()`, позиция 0 | stop @ позиция после сброса = 0 | `Stop` |
| Previous / Next | ничего | prev / next @ 0 | `Back`, `Forward` |
| Громкость | `volume` | — | `volumeSlider_ValueChanged` |
| Конец трека | статус stop, позиция 0 | stop @ 0 | не обрабатывается, см. 9.3 |

Без загруженного трека Play/Pause/Stop недоступны (`playbackPanel.IsEnabled`).
Previous/Next в Windows тоже в этой панели — тоже недоступны.

### Таймеры

Два таймера в `RunLoop.main`, режим `.common` (подводный камень 4):

- **0,25 с — `refresh()`**: обновить `position` для прогресса и времени.
  Только UI, в сеть ничего.
- **1 с — `networkTick()`**:
  - `playing` → play @ текущая позиция (каноническое поведение
    protocol.md, раздел 2);
  - `stopped` → stop @ 0 (повтор stop, protocol.md, раздел 2);
  - `paused` → ничего;
  - при открытом окне device lookup — ничего.

Таймеры принадлежат слою приложения (`PlaybackClock`), модель
предоставляет `refresh()` и `networkTick()`. Тесты вызывают их напрямую.

### Отправка

Каждая команда идёт через `send(_:position:)` модели:

- нет интерфейса или канал не открыт → ничего не отправляется,
  `networkError = "No network interface"`;
- ошибка `sendto` → `networkError = описание`, локальное воспроизведение
  не прерывается (как в Windows: исключение ловится и показывается);
- успешная отправка → `networkError = nil`.

Позиция в мс — `UInt32(clamping: Int(currentTime * 1000))`: отрицательных
и переполненных значений не бывает (расхождение 5 protocol.md).

### Интерфейс

- При старте: `InterfaceEnumerator.available()` в фоне (вызов блокирует
  до 1 с), затем выбор по сохранённому **имени** (`en0`), не по индексу
  в списке, как Windows: порядок интерфейсов на Mac меняется.
- Смена интерфейса → закрыть старый `ShowConnection`, открыть новый.
- Кнопка «обновить список» — рядом с выбором; в Windows список строится
  один раз при старте.

## 6. Device lookup — `DeviceLookupModel`

Один экран с режимом `.udp` / `.tcp`, открывается как sheet (модальный,
как `ShowDialog`). Пока открыт — `PlayerModel.isLookupOpen = true`.

| | UDP | TCP |
| --- | --- | --- |
| При открытии | `49 00 0D` на broadcast | `TCPCallbackListener` на 3331, затем `49 00 0D` и 9-байтовое объявление |
| Строка списка | IP, имя (Tag), сырые байты в подсказке | IP:порт подключения |
| Play / Stop в строке | unicast play/stop @ 0 на IP:333 | unicast play/stop @ 0 на IP:**порт подключения** (как `WifiInfoDevice` в Windows) `[проверить]` |
| Refresh | повтор `49 00 0D` | повтор `49 00 0D` |
| Повторный ответ | обновляет сырые байты существующей строки | — |
| Счётчик | «Total: N» | «Total: N» |

Ответы устройств приходят в `ShowConnection.onDeviceResponse`, общий с
главной моделью канал. Lookup-модель подписывается на время открытия.

В TCP-режиме адрес объявления (`TCPAnnounceAddress`) выбирается
переключателем в окне — до проверки на устройстве (расхождение 4).

## 7. Art-Net

Настройки (`ArtNetSettings`), правая панель как Expander в Windows:

| Поле | Windows | Mac |
| --- | --- | --- |
| Use Art-Net | чекбокс, не сохраняется | переключатель, сохраняется |
| Local IP, Subnet | для биндинга | заменены выбором интерфейса или «любой» |
| Local Port | не используется | не переносится, 6454 фиксирован |
| Universe | не используется | «любой» по умолчанию, можно задать |
| Channel | индекс с 0 | **канал DMX 1–512** (см. 9.4) |
| Start, Pause, Stop | 50, 100, 150 | 50, 100, 150 |

Сработавшая команда вызывает те же `play()`, `pause()`, `stop()` модели —
и, значит, те же пакеты в сеть. Без загруженного трека команды
игнорируются (`player.HasAudio`).

Ошибка открытия порта 6454 (занят другим софтом без `SO_REUSEPORT`) —
показывается в панели, переключатель возвращается в «выкл».

## 8. Настройки — `SettingsStore`

`AppSettings: Codable` целиком хранится в `UserDefaults` одним JSON под
ключом `settings.v1`. Версия в ключе — чтобы при смене формата не читать
старые данные как новые.

Сохранение — **при каждом изменении**, а не при выходе, как в Windows:
приложение на шоу могут закрыть принудительно, и настройки не должны
теряться. Запись дебаунсится на 0,5 с.

| Поле | По умолчанию |
| --- | --- |
| `audioFilePath` | `nil` |
| `interfaceName` | `nil` — первый в списке |
| `volume` | 0.5 |
| `artNet.enabled` | `false` |
| `artNet.interfaceName` | `nil` — любой |
| `artNet.universe` | `nil` — любой |
| `artNet.channel` | 1 |
| `artNet.playValue / pauseValue / stopValue` | 50 / 100 / 150 |

Файл трека, которого больше нет, при старте не загружается, путь
остаётся в настройках, в UI — «File not found».

Приложение без песочницы (raw-сокеты, см. раздел «Распространение»
в porting-plan.md), поэтому достаточно пути, security-scoped закладки
не нужны.

## 9. Отступления от Windows-версии

Пункты 1, 3 и 4 подтверждены владельцем (2026-10-14), остальные — решения
по умолчанию, владелец может отменить.

1. **Повтор позиции раз в секунду с первой секунды**, а не после пятой.
   В Windows из-за ошибки с `_lastMsSent` первые 5 секунд позиция не
   повторяется. Решено владельцем ранее.
2. **Broadcast по маске**, не заменой последнего октета.
3. **Конец трека → stop.** Windows не обрабатывает конец: статус
   остаётся play, раз в секунду уходит play с позицией конца трека.
   Mac переходит в stop и шлёт stop @ 0. **Подтверждено владельцем.**
4. **Канал DMX с единицы.** Windows хранит и показывает индекс массива:
   0 — первый канал (значение по умолчанию из настроек перекрывает «1»
   из XAML). На Mac пользователь вводит номер канала, как на пульте:
   канал 1 = индекс 0, то есть тот же канал, что у Windows по умолчанию.
   Пользователю Windows-версии с настройкой N нужно ввести N + 1.
   **Подтверждено владельцем.**
5. **Universe применяется**, если задан. По умолчанию — любой, как в
   Windows.
6. **Интерфейс сохраняется по имени**, а не по индексу.
7. **Настройки сохраняются сразу**, а не при выходе.
8. **Строки UI — на английском**, как в Windows-версии: операторы знают
   эти подписи. Локализация — отдельная задача.

## 10. Интерфейс (SwiftUI)

Главное окно, тёмное (`#202020` как в Windows), минимум 600×320:

```
┌──────────────────────────────┬──────────────┬─────────────┐
│ [Open audio]                 │ [   Play   ] │ ▸ Art-Net   │
│ [en0 · Wi-Fi · 192.…  ▾] [↻] │ [  Pause   ] │   settings  │
│ [Device lookup]              │ [   Stop   ] │  (сворачи-  │
│ [TCP device lookup]          │ [prev][next] │   вается)   │
│ track.mp3                    │ Status: Play │             │
│ ███████████░░░░░░░░░░░       │ ──●──── 50%  │             │
│ 01:23                  04:56 │ network err  │             │
└──────────────────────────────┴──────────────┴─────────────┘
```

- Device lookup — `.sheet` с `DeviceLookupView`.
- Открытие файла — `.fileImporter`, типы `.audio` (mp3, wav, m4a, aiff).

## 11. Тесты `EpicPlayerKitTests`

Фейки: `FakeAudioEngine`, `FakeShowTransport` (записывает отправленные
пакеты), `FakeArtNetSource`. Всё на главном акторе, без сети и звука.

| Область | Проверки |
| --- | --- |
| Команды | play/pause/stop/prev/next шлют нужный пакет и позицию; pause в stop игнорируется; stop сбрасывает позицию |
| Таймер сети | play раз в тик с текущей позицией; stop @ 0 в stop; в pause тишина; при открытом lookup тишина |
| Конец трека | статус stop, отправлен stop @ 0 |
| Без трека | команды не исполняются и не уходят в сеть |
| Без интерфейса | отправки нет, `networkError` задан, воспроизведение идёт |
| Ошибка сети | `networkError` задан и сбрасывается при успешной отправке |
| Интерфейс | восстановление по имени; смена закрывает старый канал |
| DMX | канал 1 = индекс 0; команды только с треком; настройки применяются |
| Lookup | дубликаты по IP обновляют строку; счётчик; unicast play/stop; TCP — порт подключения |
| Настройки | значения по умолчанию; round-trip через `UserDefaults(suiteName:)`; битый JSON → значения по умолчанию |
| Формат времени | `mm:ss`, после часа — `h:mm:ss` (Windows `mm\:ss` после 59:59 начинал с 00) |

## 12. Порядок написания

1. `Package.swift`: таргеты, macOS 15, tools 6.0.
2. `EpicPlayerKit`: протоколы, `AppSettings` + `SettingsStore`,
   `TimeFormat`, `PlayerModel`, `DeviceLookupModel`, адаптеры EpicCore,
   `AVAudioEngineAdapter`.
3. Тесты `EpicPlayerKitTests`.
4. `EpicPlayerMac`: App, `PlaybackClock`, Views.
5. `Support/`, `scripts/build-app.sh`.
6. Обновить статусы в porting-plan.md.

Затем — сборка и исправление ошибок компиляции, `swift test`,
проверка на устройстве (последний этап).
