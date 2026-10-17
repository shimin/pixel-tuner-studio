# Финальная сессия: отладка на Mac и завершение приложения

План работ для последнего этапа. Код фаз 1–5 написан без компилятора
(в среде разработки не было Swift), поэтому здесь всё, что откладывалось:
сборка, тесты, проверка в сети и на устройстве шоу, релизная сборка.

Связанные документы: [`porting-plan.md`](porting-plan.md) — фазы и
решения, [`app-design.md`](app-design.md) — поведение приложения,
[`protocol.md`](protocol.md) — формат пакетов.

Составлено: 2026-10-14. Обновлено: 2026-10-17 — этапы A и B закрыты,
C, D и F пройдены на Mac без устройства (журнал — раздел 11). Ветка: `master`
(ветка `claude/awesome-goodall-q2l9x4` в неё влита).

### Утром: что осталось и в каком порядке

Всё, что можно проверить на одном Mac без устройства, проверено. Осталось
то, для чего нужны руки, звук или железо:

1. `scripts/build-app.sh && open build/EpicPlayer.app` — разрешить доступ
   к локальной сети (D21), затем руками C2–C8, C12, C15, C16: клики,
   громкость на слух, меню во время воспроизведения, окно Device lookup.
2. Окно Device lookup с имитатором на этом же Mac (D14–D18):
   `python3 scripts/fake-device.py --name "Шоу 1" --same-host --tcp`
   запустить **до** приложения.
3. Устройство шоу — этап E целиком, начиная с `epic-probe lookup en0`.
4. Пульт — F12; разрешения и устойчивость — D22–D24, G1–G7.
5. Релиз — H2–H5 (подпись Developer ID — по решению владельца).

## 0. Критерии готовности

Приложение считается завершённым, когда:

1. `swift build` без ошибок; предупреждения разобраны (раздел 2.4).
2. `swift test` — все тесты зелёные.
3. `WindowsReference` на Windows подтверждает эталонные байты.
4. `EpicPlayer.app` запускается двойным щелчком вне Xcode.
5. Устройство шоу реагирует на play / pause / stop / next / prev,
   позиция не расходится с локальной.
6. Device lookup по UDP находит устройство и показывает имя.
7. Art-Net с пульта запускает, ставит на паузу и останавливает трек.
8. Разрешение на локальную сеть запрашивается и после согласия
   не мешает работе.
9. Журнал результатов (раздел 11) заполнен, открытые вопросы
   `[проверить]` в protocol.md закрыты или явно отложены.

Пункт 3 и TCP device lookup не блокируют релиз: TCP-поиск не работал
и в Windows-версии (protocol.md, расхождение 6).

## 1. Подготовка

### Оборудование и софт

| Что | Зачем |
| --- | --- |
| Mac с macOS 15+ и Xcode 16+ | сборка: tools 6.0, `.macOS(.v15)` |
| Второй компьютер в той же сети (любая ОС, Python 3) | `fake-device.py` до выезда к устройству |
| Устройство шоу | этапы E, F |
| Пульт или софт Art-Net (QLC+ бесплатен) | этап F; без них — `send-artdmx.py` |
| Wireshark (по желанию) | разбор ответа устройства; `tcpdump` есть в системе |
| Windows-машина с .NET Framework 4.5 | `WindowsReference`, по желанию |
| Аудиофайлы: короткий mp3 (≈10 с), длинный (> 1 ч), wav, m4a, файл с кириллицей в имени, битый файл | этап C |

### Окружение

```bash
xcode-select -p              # должен указывать на Xcode 16+, не на CommandLineTools
swift --version              # Swift 6.x
sw_vers                      # macOS 15+
git clone https://github.com/shimin/epicplayer.git
cd epicplayer && git checkout master
cd epicplayer-mac
```

Если `xcode-select` указывает на CommandLineTools:
`sudo xcode-select -s /Applications/Xcode.app`.

Логи приложения и CLI во время всех этапов — в отдельном окне терминала:

```bash
log stream --predicate 'subsystem == "com.epicplayer.mac"' --level debug
```

## 2. Этап A. Компиляция

Собирать по таргетам снизу вверх — так ошибки не маскируют друг друга:

```bash
swift build --target EpicCore
swift build --target EpicProbe
swift build --target EpicPlayerKit
swift build --target EpicPlayerMac
swift build                      # всё вместе
```

Ошибки исправлять в этом же порядке. После каждой правки — повторить
сборку этого таргета.

### 2.1. Места риска в EpicCore

Код писался без компилятора; ниже — места, в которых я не уверен,
с вероятной правкой.

| Файл | Что | Если не собирается |
| --- | --- | --- |
| `SocketSupport.swift` | `ipBoundInterface = 25` — значение `IP_BOUND_IF` | сверить: `grep -n IP_BOUND_IF "$(xcrun --show-sdk-path)/usr/include/netinet/in.h"` |
| `SocketSupport.swift`, `BroadcastSocket.swift`, `TCPCallbackListener.swift` | `withUnsafeMutablePointer` + `withMemoryRebound` вокруг `recvfrom` / `accept` / `bind` | ошибки «overlapping access» — вынести `length` и буфер в отдельные переменные до вызова |
| `BroadcastSocket.swift`, `TCPCallbackListener.swift` | интерполяция `IPv4Address` в `Logger` (`\(address, privacy: .public)`) | заменить на `\(address.description, privacy: .public)` |
| `InterfaceEnumerator.swift` | `UInt32(IFF_UP \| IFF_RUNNING \| IFF_BROADCAST)`, `entry.ifa_flags` | привести типы явно: `UInt32(bitPattern: …)` или `Int32(entry.ifa_flags)` |
| `InterfaceTypeProbe.swift` | `import Network` рядом с нашим `IPv4Address` | неоднозначность имени — писать `EpicCore.IPv4Address`; лучше сразу переименовать тип (раздел 10) |
| `TCPCallbackListener.swift` | `Darwin.send(fd, buffer.baseAddress! + offset, …)` | `buffer.baseAddress!.advanced(by: offset)` |
| `DeviceDiscovery.swift` | `init?(b0:…)` — лишний failable-инициализатор | удалить, он не используется |

### 2.2. Места риска в EpicPlayerKit

| Файл | Что | Если не собирается |
| --- | --- | --- |
| `PlayerModel.swift`, `DeviceLookupModel.swift` | `@Observable` + `@ObservationIgnored` + `private(set)` | ошибки макроса — убрать `@ObservationIgnored` с проблемного свойства или сделать его `let` |
| `PlayerModel.swift` | `Task.detached { provider() }.value` | при ошибке Sendable — `await withCheckedContinuation` + `DispatchQueue.global().async` |
| `AppSettings.swift` | `init(from:)` у `ArtNetSettings` в расширении | перенести в тело структуры |
| `Abstractions.swift` | `MainActor.assumeIsolated(body)` в `onMain` | `MainActor.assumeIsolated { body() }` |
| `LiveAdapters.swift` | замыкания `{ @Sendable [weak self] … }` для колбэков сокетов | при ошибке — вынести создание замыкания в `nonisolated static func`. Убирать `@Sendable` нельзя: замыкание унаследует `@MainActor`, а вызывается с очереди сокета |
| `SystemAudioPlayer.swift` | `FinishDelegate: AVAudioPlayerDelegate` и изоляция | при ошибке — `@preconcurrency AVAudioPlayerDelegate` или пометить метод `nonisolated` |

### 2.3. Места риска в EpicPlayerMac

| Файл | Что | Если не собирается |
| --- | --- | --- |
| `EpicPlayerApp.swift` | `Window` + `@NSApplicationDelegateAdaptor` + `@State` с `PlayerModel.live()` | при ошибке инициализации `@State` — создавать модель в `AppDelegate` |
| `MainView.swift` | `.sheet(item:)` с `DeviceLookupModel` (класс, `Identifiable`) | явное `public var id: ObjectIdentifier { ObjectIdentifier(self) }` |
| `MainView.swift` | `Picker` с тегами `String?` | все теги должны быть `String?`: `.tag(Optional(id))`, `.tag(String?.none)` |
| `ArtNetSettingsView.swift` | `TextField(value:format: .number)` с `Binding<Int>` | `format: .number` требует `Int` — уже так; при ошибке — `IntegerFormatStyle<Int>()` |
| `PlaybackClock.swift` | `Timer` с `@Sendable`-блоком и `assumeIsolated` | предупреждение — допустимо в Swift 5 |

### 2.4. Предупреждения

Swift 5 language mode: предупреждения о конкурентности (Sendable,
изоляция) допустимы, но их нужно просмотреть — каждое может оказаться
настоящей гонкой. Список сохранить в журнал (раздел 11). Переход на
Swift 6 language mode — после релиза, отдельной задачей.

## 3. Этап B. Модульные тесты

```bash
swift test 2>&1 | tee test.log
swift test --filter ProtocolVectorTests       # эталонные байты
swift test --filter NetworkLogicTests         # broadcast, фильтр, ArtDmx
swift test --filter PlayerModelTests          # поведение главного окна
swift test --filter DeviceLookupModelTests
swift test --filter SettingsAndFormatTests
```

| Набор | Что означает падение |
| --- | --- |
| `ProtocolVectorTests` | **Блокирует всё.** Байты пакетов расходятся с Windows. Править код, не эталон: эталон выведен из Windows-кода |
| `NetworkLogicTests` | ошибка в чистой логике сети; править код |
| `PlayerModelTests`, `DeviceLookupModelTests` | расхождение с app-design.md. Сверить с разделами 5–6 и решить, что неверно — тест или модель |
| `SettingsAndFormatTests` | хранение настроек или формат времени |

Если тест падает из-за точности чисел с плавающей точкой (позиция в мс) —
брать значения, точно представимые в двоичном виде (0.5, 2.5), а не
менять округление в `TimeFormat.milliseconds`.

### WindowsReference (по желанию, на Windows)

```
cd epicplayer-mac
dotnet run --project Tests\WindowsReference -- Tests\Fixtures\protocol-vectors.json
```

Нужен .NET SDK и targeting pack .NET Framework 4.5 (как у основного
проекта); можно собрать и в Visual Studio.

Подтверждает, что `protocol-vectors.json` совпадает с тем, что
собирает исходный C#-код. Если совпадает — оба теста смотрят на одну
правду.

## 4. Этап C. Приложение без сети

```bash
scripts/build-app.sh
open build/EpicPlayer.app
```

Если сборка бандла падает на иконке — скрипт продолжит без неё; разобрать
отдельно (`sips -s format png ../EpicPlayer/icon.ico --out /tmp/i.png`).

Сеть на этом этапе может быть любой, устройство не нужно.

| # | Действие | Ожидается |
| --- | --- | --- |
| C1 | Первый запуск | окно 800×340, тёмное; «No media available»; кнопки транспорта неактивны |
| C2 | Open audio → mp3 | имя файла, длительность справа, «Status: Stop» |
| C3 | Play | звук, время идёт, прогресс растёт, «Status: Play» |
| C4 | Pause, затем Play | звук продолжается с того же места |
| C5 | Stop | позиция 00:00, прогресс 0; Play начинает с начала |
| C6 | Pause в остановленном состоянии | ничего не происходит |
| C7 | Громкость 0 % / 100 % | звук пропадает / максимальный; подпись «Volume: N%» |
| C8 | Удерживать открытым меню 5 секунд во время воспроизведения | время продолжает идти (таймер в режиме `.common`) |
| C9 | Короткий трек доиграть до конца | «Status: Stop», позиция 0 (решение 9.3) |
| C10 | wav, m4a, файл с кириллицей в имени | открываются и играют |
| C11 | Битый файл (переименованный .txt) | «Cannot open: …», приложение работает |
| C12 | Трек больше часа | время `1:02:03`, позиция не обнуляется после 59:59 |
| C13 | Выйти (⌘Q) и запустить снова | тот же трек загружен, громкость та же, интерфейс тот же |
| C14 | Переместить файл трека, запустить | «File not found», путь не потерян: вернуть файл — снова грузится |
| C15 | Закрыть окно крестиком | приложение завершается |
| C16 | Панель «Art-Net settings» | раскрывается и сворачивается, поля редактируются |

Настройки можно посмотреть: `defaults read com.epicplayer.mac settings.v1`
(данные в JSON, хранятся как `Data`). При запуске через `swift run`
домен другой — по имени процесса `EpicPlayerMac`.

## 5. Этап D. Сеть без устройства

Цель — убедиться, что пакеты уходят в правильный интерфейс с правильными
байтами, до выезда к устройству.

### D.1. Интерфейсы

```bash
swift run epic-probe interfaces --all
```

| # | Конфигурация | Ожидается в списке «для выбора» |
| --- | --- | --- |
| D1 | Только Wi-Fi | `en0 · Wi-Fi · …` |
| D2 | Wi-Fi + USB/Thunderbolt Ethernet | оба; Ethernet первым |
| D3 | Включён VPN | `utun*` отсутствует |
| D4 | Thunderbolt Bridge / Internet Sharing | `bridge*`, `ap*` отсутствуют |
| D5 | Ethernet без DHCP (самоназначенный 169.254.x.x) | присутствует (тип может быть `?`) |

Если в списке лишний интерфейс — дополнить `virtualPrefixes` в
`InterfaceEnumerator.swift` и тест `testVirtualInterfacesRejected`.
Если нужного нет — посмотреть его флаги в `ifconfig` и тип в
`--all`.

### D.2. Байты в сети

В отдельном окне:

```bash
sudo tcpdump -i en0 -n -X 'udp port 333 or udp port 6454 or tcp port 3331'
```

| # | Действие | Ожидается в tcpdump |
| --- | --- | --- |
| D6 | `swift run epic-probe send en0 play 1000` | на `x.x.x.255.333`: `5005 02e8 0300 000d` |
| D7 | То же при включённом VPN | пакет на `en0`, не на `utun*` (подводный камень 1) |
| D8 | Подсеть /16 или /22, если есть возможность | broadcast по маске, не `.255` последнего октета |
| D9 | Приложение: Play и подождать | play с растущей позицией раз в секунду, с первой секунды |
| D10 | Приложение: Stop и подождать | stop @ 0 раз в секунду |
| D11 | Приложение: Pause и подождать | тишина |
| D12 | Приложение: открыть Device lookup | `49 00 0d`; повторы play/stop прекращаются, пока окно открыто |
| D13 | Приложение: next / prev | `05` / `06`, позиция 0 |

### D.3. Имитатор устройства

На втором компьютере в той же сети или на этом же Mac. На том же Mac —
с `--same-host` и **до** запуска приложения: unicast на свой адрес:333
ядро отдаёт только сокету, открытому первым, поэтому имитатор отвечает
на broadcast, а команды из строки lookup получает сам.

```bash
python3 scripts/fake-device.py --name "Шоу 1" --tcp               # другой компьютер
python3 scripts/fake-device.py --name "Шоу 1" --tcp --same-host   # этот же Mac
```

| # | Действие | Ожидается |
| --- | --- | --- |
| D14 | Приложение: Device lookup | строка `IP: <второй компьютер>`, `Tag: Шоу 1`; подсказка — сырые байты |
| D15 | Refresh несколько раз | строка одна, «Total: 1» |
| D16 | Play / Stop в строке | имитатор: `play @ 0`, `stop @ 0` с адреса Mac (unicast) |
| D17 | Play в главном окне | имитатор: `play @ N мс  +1.00 с` каждую секунду |
| D18 | TCP device lookup | имитатор: `TCP-объявление: <IP Mac>:3331`, `подключился`; в окне строка с портом |
| D19 | TCP: переключить «Broadcast (as Windows)» | имитатор пытается подключиться к `x.x.x.255` и не может — это ожидаемо |
| D20 | `swift run epic-probe lookup en0` | то же, что D14, в консоли |

### D.4. Разрешения macOS

| # | Что | Ожидается |
| --- | --- | --- |
| D21 | Первый запуск `.app` и первая отправка | запрос «найти устройства в локальной сети»; после согласия пакеты уходят |
| D22 | Отказ в разрешении | `networkError` в окне (`sendto: No route to host` или похожее), приложение не падает. Включить: Системные настройки → Конфиденциальность → Локальная сеть |
| D23 | Firewall включён | запрос на входящие подключения; без согласия не приходят ответы устройств и Art-Net |
| D24 | Пересборка `.app` | ad-hoc подпись меняется — macOS может спросить разрешение заново. Это ожидаемо до подписи Developer ID |

CLI `epic-probe` получает разрешение от терминала (Terminal, iTerm),
а не своё.

## 6. Этап E. Устройство шоу

Порядок — из protocol.md, «Порядок реализации». Сначала CLI, затем
приложение: CLI проще и показывает сырые байты.

| # | Действие | Записать в журнал |
| --- | --- | --- |
| E1 | `epic-probe lookup en0` | находится ли устройство; имя; **сырые байты ответа целиком** (закрывает открытый вопрос 5) |
| E2 | `epic-probe send en0 play 0` → `pause 5000` → `play 5000` → `stop` | реакция устройства на каждую команду |
| E3 | `epic-probe send en0 next`, `prev` | что устройство считает «следующим» |
| E4 | `epic-probe send en0 play 0 --to <IP устройства>` | реагирует ли на unicast |
| E5 | `epic-probe listen en0 120` во время работы устройства | шлёт ли устройство что-то само; формат |
| E6 | `epic-probe tcp en0`, затем `--legacy-address` | какой адрес в объявлении устройство принимает (расхождение 4) и что идёт по соединению |
| E7 | Приложение: трек, Play, 2–3 минуты | позиция на устройстве не расходится с локальной |
| E8 | Приложение: Pause / Play / Stop в середине трека | устройство следует |
| E9 | Приложение: перемотки нет — проверить, что Stop → Play стартует с 0 и устройство тоже | синхронно |
| E10 | Приложение: доиграть трек до конца | устройство получает stop (решение 9.3); как реагирует |
| E11 | Приложение: Device lookup → Play / Stop в строке | устройство реагирует на unicast |
| E12 | Отключить кабель / Wi-Fi на 10 с во время воспроизведения | приложение показывает ошибку и продолжает играть; после возврата сети — догоняет позицией в течение секунды |

Если устройство не реагирует ни на что, проверить по порядку:
1. tcpdump на Mac — уходят ли пакеты и на какой адрес;
2. совпадает ли подсеть Mac и устройства;
3. сравнить с Windows-версией tcpdump/Wireshark на той же сети:
   байты команд должны совпадать с выводом `epic-probe send`.

Результаты E1 и E6 перенести в protocol.md: разделы 4 и 5, снять
пометки `[проверить]`.

## 7. Этап F. Art-Net

Без пульта — `send-artdmx.py` со второго компьютера или с самого Mac
(Art-Net-пакеты со своего адреса не отбрасываются):

```bash
python3 scripts/send-artdmx.py <broadcast> --channel 1 --values 50 0 100 0 150 0
swift run epic-probe artnet en0 --channel 0   # CLI: индекс с нуля
```

| # | Действие | Ожидается |
| --- | --- | --- |
| F1 | Включить «Use Art-Net», интерфейс Any | «Listening on UDP 6454» |
| F2 | Значение 50 на канале 1 | Play |
| F3 | Удерживать 50 (`--values 50 --hold 5`) | один Play, без перезапусков |
| F4 | 100, затем 150 | Pause, затем Stop |
| F5 | 49 → 50 → 51 (фейдер) | Play только на 50 |
| F6 | Канал 12 в настройках, слать на канал 1 | ничего; слать на 12 — реагирует |
| F7 | Universe = 1, слать на universe 0 | ничего; на 1 — реагирует |
| F8 | Без загруженного трека | ничего |
| F9 | Запустить QLC+ (держит 6454) до включения Art-Net | работает (`SO_REUSEPORT`) или понятная ошибка, переключатель выключается |
| F10 | Интерфейс Art-Net = en0, слать на broadcast en0 | реагирует; на другом интерфейсе — нет |
| F11 | Перезапуск приложения с включённым Art-Net | слушает сразу после старта |
| F12 | Настоящий пульт | то же, что F2–F4 |

## 8. Этап G. Устойчивость

| # | Сценарий | Ожидается |
| --- | --- | --- |
| G1 | Сон и пробуждение Mac во время Stop | после пробуждения повтор stop идёт; при смене адреса — кнопка обновления списка интерфейсов восстанавливает связь |
| G2 | Сменить Wi-Fi-сеть на ходу | ошибка в окне; ↻ и выбор интерфейса восстанавливают |
| G3 | Два экземпляра приложения (`open -n`) | оба запускаются; оба шлют пакеты — проверить, не путает ли это устройство |
| G4 | Device lookup открыт 10 минут | нет повторов play/stop; после закрытия — возобновляются |
| G5 | Час воспроизведения | память стабильна (Activity Monitor), позиция без дрейфа |
| G6 | Принудительное завершение (`kill -9`) после смены настроек | настройки сохранились (запись через 0,5 с) |
| G7 | Удалить настройки: `defaults delete com.epicplayer.mac` | старт со значениями по умолчанию |

Падение приложения — отчёт из Console.app → Crash Reports, приложить
в журнал целиком.

## 9. Этап H. Релиз

| # | Шаг |
| --- | --- |
| H1 | `scripts/build-app.sh` — release-сборка |
| H2 | Скопировать `build/EpicPlayer.app` на **другой** Mac, запустить: Gatekeeper предупредит; «Открыть всё равно» в Системных настройках → Конфиденциальность и безопасность |
| H3 | Проверить на этом Mac C2–C5 и D21 |
| H4 | DMG (по желанию): `hdiutil create -volname "E-pic player" -srcfolder build/EpicPlayer.app -ov -format UDZO build/EpicPlayer.dmg` |
| H5 | Developer ID и нотаризация — по разделу «Распространение» в porting-plan.md, если решено покупать аккаунт ($99/год). Добавит в `build-app.sh`: `codesign --options runtime --sign "Developer ID Application: …"`, `xcrun notarytool submit … --wait`, `xcrun stapler staple` |
| H6 | Версия в `Support/Info.plist` синхронна с Windows (сейчас 1.0.0) |
| H7 | README: раздел про macOS-версию — сборка, запуск, отличия от Windows (app-design.md, раздел 9) |
| H8 | Обновить статусы в porting-plan.md: фазы 1–6 закрыты |

## 10. Технический долг — после релиза

Не блокирует релиз, но записано, чтобы не потерялось:

1. Переименовать `EpicCore.IPv4Address` (например, `IPv4Octets`):
   пересекается с типом из `Network.framework`.
2. Swift 6 language mode — включить строгую конкурентность, разобрать
   предупреждения.
3. Удалить `IPv4Address.init?(b0:…)`.
4. Исправить README Windows-версии: позиция раз в секунду, а не в пять
   (protocol.md, расхождение 1).
5. Локализация интерфейса (сейчас английский, как в Windows).
6. Sparkle для автообновлений — после подписи Developer ID.

## 11. Журнал результатов

Заполнять по ходу. Каждая строка — один пункт из этапов выше.

| Пункт | Дата | Результат | Заметки, байты, ошибки |
| --- | --- | --- | --- |
| A EpicCore | 2026-10-17 | ✅ | Xcode 27.0, Swift 6.4, macOS 26.6: собирается без ошибок и предупреждений; места риска из 2.1 правок не потребовали |
| A EpicPlayerKit | 2026-10-17 | ✅ | без ошибок и предупреждений |
| A EpicPlayerMac | 2026-10-17 | ✅ | без ошибок и предупреждений, debug и release |
| B swift test | 2026-10-17 | ✅ | 92 теста, 0 падений: EpicCore 36 (с эталонами Art-Net из второй ветки), EpicPlayerKit 56 (с загрузкой файла настоящим AVAudioPlayer) |
| B WindowsReference | до 2026-10-17 | ✅ | на Windows: 15 проверок, 0 расхождений |
| C1 | 2026-10-17 | ✅ | 800×348, тёмное, «No media available», транспорт неактивен, `en0` выбран |
| C3–C5, C9 | 2026-10-17 | ✅ | через Art-Net (кнопки — утром): Play/Pause/Stop, время и прогресс идут; конец трека → «Status: Stop», 00:00, stop @ 0 |
| C10 | 2026-10-17 | ✅ | m4a с кириллицей в имени открывается, длительность 00:12 |
| C11 | 2026-10-17 | ✅ | «Cannot open: unsupported or damaged audio file», приложение работает |
| C13 | 2026-10-17 | ✅ | трек, громкость и интерфейс восстанавливаются из `settings.v1` |
| C14 | 2026-10-17 | ✅ | «File not found», путь сохраняется |
| C2, C6–C8, C12, C15, C16 | | ⏳ утром | нужны клики и звук: у агента не было Accessibility |
| D1, D3, D4 | 2026-10-17 | ✅ | `epic-probe interfaces --all`: в выборе только `en0 · Wi-Fi · 192.168.1.102/24`; `utun26` (VPN 198.18.0.1/16), `bridge100`, `lo0` отфильтрованы |
| D6, D7 | 2026-10-17 | ✅ | `send en0 play 1000` → `50 05 02 E8 03 00 00 0D` с порта 333 на 192.168.1.255:333 при включённом VPN (приём локальным сокетом, tcpdump без sudo недоступен) |
| D9–D11 | 2026-10-17 | ✅ | приложение: play @ 0, затем play с позицией раз в секунду (221, 1219, 2222, 3220 мс); pause — тишина; stop @ 0 раз в секунду |
| D12, D13 | | ⏳ утром | нужны клики |
| D14, D18, D20 | 2026-10-17 | ✅ | CLI с имитатором на этом же Mac (`--same-host`): найдено `192.168.1.102`, имя «Шоу 1» из CP1251; TCP: объявление `4C 06 C0 A8 01 66 03 0D 0D`, имитатор подключился |
| D15–D17, D19 | | ⏳ утром | окно Device lookup — нужны клики |
| D21–D24 | | ⏳ утром | диалоги разрешений |
| E1 ответ устройства | | ⏳ | сырые байты: |
| E6 TCP-объявление | | ⏳ | рабочий адрес: |
| E2–E12 | | ⏳ | устройство шоу |
| F1–F4 | 2026-10-17 | ✅ | приложение с Art-Net из настроек, `send-artdmx.py` на broadcast: 50 → Play, 100 → Pause, 150 → Stop; в сеть ушли play/pause/stop |
| F5–F11 | | ⏳ утром | логика F5–F8 покрыта тестами `DmxTrigger` |
| F12 | | ⏳ | настоящий пульт |
| G1–G7 | | ⏳ | |
| H1 | 2026-10-17 | ✅ | `scripts/build-app.sh` — release, иконка из `icon.ico`, ad-hoc подпись |
| H6–H8 | 2026-10-17 | ✅ | версия 1.0.0; README — раздел «macOS-версия»; статусы в porting-plan.md |
| H2–H5 | | ⏳ | другой Mac, DMG, Developer ID |

Замечания 2026-10-17:

- При самом первом открытии нового бандла поверх окна один раз появилась
  панель Open. Ни при повторных запусках, ни после
  `defaults delete com.epicplayer.mac` не повторилась. Если повторится
  утром — записать, после какого действия.
- Таймер сети идёт независимо от команд, поэтому сразу после Play может
  уйти второй play @ 0, а после конца трека — два stop @ 0 в одну секунду.
  Так же работает Windows-версия; для устройства это безвредно.

## 12. Как работать с Claude в финальной сессии

Новая сессия начинается без памяти о предыдущих — контекст в этих
файлах. Первое сообщение:

> Финальная сессия EpicPlayer для macOS. Прочитай
> `epicplayer-mac/plans/final-session.md`, `porting-plan.md`,
> `app-design.md`, `protocol.md`. Ветка `master`. Ниже вывод сборки.

Что присылать:

- **Ошибки сборки** — вывод `swift build` целиком, начиная с первой
  ошибки. Первые 20–30 ошибок за раз: остальные часто следствие.
- **Тесты** — `test.log` или строки `error:` с именами тестов.
- **Сеть** — вывод `epic-probe` и выдержку из `tcpdump -X`.
- **Поведение** — номер пункта (C7, E10…), что ожидалось, что произошло,
  строки из `log stream` за это время.
- **Падение** — отчёт из Console.app.

Правки Claude пушит в ветку, вы делаете `git pull` и повторяете пункт.
Если Claude-сессия запущена на самом Mac (Claude Code в терминале),
она может собирать и запускать тесты сама — тогда этапы A и B
выполняются без пересылки логов.
