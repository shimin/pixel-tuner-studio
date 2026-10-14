import EpicCore
import Foundation

// Проверка сетевого слоя на реальном устройстве, до UI (фаза 2).
// Запуск из каталога epicplayer-mac: swift run epic-probe <команда>

let usage = """
epic-probe — проверка протокола EpicPlayer на устройстве

  interfaces [--all]                    список интерфейсов (--all — без фильтра)
  lookup <iface> [сек]                  UDP-поиск устройств, по умолчанию 5 с
  send <iface> <cmd> [мс] [--to IP]     команда: play pause stop next prev
  listen <iface> [сек]                  всё, что приходит на UDP 333
  tcp <iface> [сек] [--legacy-address]  TCP-поиск: слушать 3331 и объявить себя
  artnet [iface] [--channel N] [--universe U]
                                        приём ArtDmx и срабатывания команд

<iface> — имя (en0) или адрес интерфейса. Логи: log stream --predicate 'subsystem == "com.epicplayer.mac"'
"""

func fail(_ message: String) -> Never {
    FileHandle.standardError.write((message + "\n").data(using: .utf8)!)
    exit(1)
}

func say(_ message: String) {
    let time = ISO8601DateFormatter.string(from: Date(), timeZone: .current, formatOptions: [.withTime, .withColonSeparatorInTime])
    print("[\(time)] \(message)")
    fflush(stdout)
}

var arguments = Array(CommandLine.arguments.dropFirst())

func takeFlag(_ name: String) -> Bool {
    guard let index = arguments.firstIndex(of: name) else { return false }
    arguments.remove(at: index)
    return true
}

func takeOption(_ name: String) -> String? {
    guard let index = arguments.firstIndex(of: name) else { return nil }
    guard index + 1 < arguments.count else { fail("\(name): нет значения") }
    let value = arguments[index + 1]
    arguments.removeSubrange(index...(index + 1))
    return value
}

func findInterface(_ query: String) -> NetworkInterface {
    let all = (try? InterfaceEnumerator.ipv4Interfaces(includeVirtual: true)) ?? []
    let typed = InterfaceEnumerator.available()
    let candidates = typed + all.filter { item in !typed.contains { $0.id == item.id } }
    guard let found = candidates.first(where: { $0.name == query || $0.address.description == query }) else {
        fail("Интерфейс \(query) не найден. Список: epic-probe interfaces --all")
    }
    return found
}

func seconds(at position: Int, default value: Double) -> Double {
    guard arguments.count > position else { return value }
    guard let parsed = Double(arguments[position]) else { fail("Ожидалось число секунд: \(arguments[position])") }
    return parsed
}

func finish(after seconds: Double, _ cleanup: @escaping () -> Void = {}) -> Never {
    DispatchQueue.main.asyncAfter(deadline: .now() + seconds) {
        cleanup()
        exit(0)
    }
    dispatchMain()
}

func describe(_ interface: NetworkInterface) -> String {
    "\(interface.displayName), индекс \(interface.index), broadcast \(interface.broadcastAddress)"
        + (interface.broadcastAddress == interface.address.legacyBroadcast
           ? "" : " (Windows-версия послала бы на \(interface.address.legacyBroadcast))")
}

guard let command = arguments.first else {
    print(usage)
    exit(0)
}
arguments.removeFirst()

switch command {
case "interfaces":
    let showAll = takeFlag("--all")
    let typed = InterfaceEnumerator.available()
    print("Для выбора в приложении:")
    if typed.isEmpty { print("  (нет)") }
    for interface in typed { print("  " + describe(interface)) }

    if showAll {
        print("\nВсе IPv4 без фильтра:")
        for interface in (try? InterfaceEnumerator.ipv4Interfaces(includeVirtual: true)) ?? [] {
            print("  \(interface.name) \(interface.address)/\(interface.netmask.prefixLength), индекс \(interface.index)")
        }
    }

case "lookup":
    guard let name = arguments.first else { fail(usage) }
    let interface = findInterface(name)
    let duration = seconds(at: 1, default: 5)
    say("Интерфейс: " + describe(interface))

    let connection: ShowConnection
    do { connection = try ShowConnection(interface: interface) } catch { fail("Не открыть UDP 333: \(error)") }

    var found = Set<String>()
    connection.onDeviceResponse = { response in
        let isNew = found.insert(response.address.description).inserted
        let status = isNew ? "Найдено" : "Повтор"
        let name = response.name.map { "«\($0)»" } ?? "—"
        say("\(status): \(response.address) имя=\(name) \(response.raw.count) байт")
        say("  \(response.rawHex)")
    }
    connection.onOtherDatagram = { datagram in
        say("Не ответ устройства от \(datagram.sender):\(datagram.senderPort): \(datagram.data.hexString)")
    }

    do { try connection.requestDevices() } catch { fail("Запрос не отправлен: \(error)") }
    say("Запрос 49 00 0D → \(connection.broadcastAddress):333, жду \(duration) с")
    finish(after: duration) { say("Готово, устройств: \(found.count)") }

case "send":
    let target = takeOption("--to")
    guard arguments.count >= 2 else { fail(usage) }
    let interface = findInterface(arguments[0])

    let commands: [String: PlaybackCommand] = [
        "play": .play, "pause": .pause, "stop": .stop, "next": .nextTrack, "prev": .previousTrack,
    ]
    guard let playback = commands[arguments[1]] else { fail("Неизвестная команда \(arguments[1])") }
    guard let position = arguments.count > 2 ? UInt32(arguments[2]) : 0 else {
        fail("Позиция — целое число миллисекунд")
    }

    do {
        let connection = try ShowConnection(interface: interface)
        if let target {
            guard let address = IPv4Address(dottedQuad: target) else { fail("Некорректный адрес \(target)") }
            try connection.send(playback, positionMilliseconds: position, to: address)
            say("\(arguments[1]) @ \(position) мс → \(address):333")
        } else {
            try connection.send(playback, positionMilliseconds: position)
            say("\(arguments[1]) @ \(position) мс → \(connection.broadcastAddress):333")
        }
        say("  \(PlaybackPacket.encode(playback, positionMilliseconds: position).hexString)")
    } catch {
        fail("Не отправлено: \(error)")
    }

case "listen":
    guard let name = arguments.first else { fail(usage) }
    let interface = findInterface(name)
    let duration = seconds(at: 1, default: 60)
    let connection: ShowConnection
    do { connection = try ShowConnection(interface: interface) } catch { fail("Не открыть UDP 333: \(error)") }

    connection.onDeviceResponse = { say("Устройство \($0.address): \($0.rawHex)") }
    connection.onOtherDatagram = { datagram in
        var line = "\(datagram.sender):\(datagram.senderPort) \(datagram.data.hexString)"
        if let playback = PlaybackPacket.command(from: datagram.data),
           let position = PlaybackPacket.position(from: datagram.data) {
            line += "  [\(playback) @ \(position) мс]"
        }
        say(line)
    }
    say("Слушаю UDP 333 на \(interface.name) \(duration) с (свои пакеты скрыты)")
    finish(after: duration) { connection.close() }

case "tcp":
    let legacy = takeFlag("--legacy-address")
    guard let name = arguments.first else { fail(usage) }
    let interface = findInterface(name)
    let duration = seconds(at: 1, default: 15)

    let listener: TCPCallbackListener
    let connection: ShowConnection
    do {
        listener = try TCPCallbackListener(interface: interface)
        connection = try ShowConnection(interface: interface)
    } catch {
        fail("Не открыть сокеты: \(error)")
    }

    var devices: [TCPDeviceConnection] = []
    listener.onConnection = { device in
        devices.append(device)
        say("TCP-подключение: \(device.remoteAddress):\(device.remotePort)")
        device.onData = { say("  ← \(device.remoteAddress): \($0.hexString)") }
        device.onClose = { say("  \(device.remoteAddress) закрыл соединение") }
    }
    connection.onDeviceResponse = { say("UDP-ответ \($0.address): \($0.rawHex)") }

    do {
        try connection.announceTCPCallback(using: legacy ? .broadcastAddress : .interfaceAddress)
    } catch {
        fail("Объявление не отправлено: \(error)")
    }
    let announced = legacy ? connection.broadcastAddress : interface.address
    say("Объявление \(DeviceDiscovery.encodeTCPAnnouncement(localAddress: announced).hexString) → \(connection.broadcastAddress):333")
    say("Адрес в пакете: \(announced)\(legacy ? " (как Windows-версия)" : ""), жду \(duration) с")
    finish(after: duration) {
        say("Готово, подключений: \(devices.count)")
        listener.close()
    }

case "artnet":
    let channel = takeOption("--channel").map { Int($0) ?? -1 } ?? 0
    let universe = takeOption("--universe").map { UInt16($0) }
    guard channel >= 0 else { fail("--channel: индекс с нуля") }
    if case .some(.none) = universe { fail("--universe: число 0–32767") }

    let interface = arguments.first.map(findInterface)
    let receiver: ArtNetReceiver
    do { receiver = try ArtNetReceiver(interface: interface) } catch { fail("Не открыть UDP 6454: \(error)") }

    var trigger = DmxTrigger(settings: DmxTriggerSettings(channel: channel, universe: universe ?? nil))
    var lastShown: UInt8?
    receiver.onDmx = { packet, sender in
        if channel < packet.data.count, packet.data[channel] != lastShown {
            lastShown = packet.data[channel]
            say("\(sender) universe \(packet.universe): канал[\(channel)] = \(packet.data[channel])")
        }
        if let command = trigger.process(packet) {
            say("  → команда \(command)")
        }
    }
    say("Слушаю Art-Net :6454 на \(interface?.name ?? "всех интерфейсах"); play=50 pause=100 stop=150, Ctrl-C — выход")
    dispatchMain()

default:
    fail(usage)
}
