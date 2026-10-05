import EpicCore
import AVFoundation
import Darwin
import Foundation

/// Интеграционный зонд сетевого слоя.
///
/// Зачем он нужен. Настоящие сокеты не работают в процессе `swift test`:
/// бинарник, который собирает SwiftPM, не имеет `Info.plist`, а macOS 15+
/// ограничивает такие процессы — `bind` на loopback возвращает
/// `Can't assign requested address`. Поэтому логика проверяется юнит-тестами
/// на подставных сокетах, а реальный сетевой обмен — здесь.
///
/// Зонд собирается как обычный `.app` с `Info.plist`, поэтому работает.
/// Запуск: `./Scripts/build-probe.sh`
///
/// Проверяет на loopback то, что нельзя проверить без сокетов:
/// - интерфейсы виден и фильтруется корректно;
/// - UDP-пакет доходит до приёмника;
/// - TCP-соединение принимается, ответ уходит в порт источника;
/// - Art-Net пакет разбирается.
enum ProbeResult {
    case passed(String)
    case failed(String, String)
    case skipped(String)

    var symbol: String {
        switch self {
        case .passed: return "ok   "
        case .failed: return "FAIL "
        case .skipped: return "skip "
        }
    }

    var message: String {
        switch self {
        case .passed(let text), .skipped(let text):
            return text
        case .failed(let name, let reason):
            return "\(name) — \(reason)"
        }
    }
}

final class SocketProbe {
    private var results: [ProbeResult] = []

    func run() -> Int32 {
        print("=== Интеграционный зонд сетевого слоя ===")
        print("")

        checkInterfaceEnumeration()
        checkUdpRoundTrip()
        checkTcpRoundTrip()
        checkArtNetParsing()
        checkBroadcastBinding()
        checkAudio()

        print("")
        var failures = 0
        for result in results {
            print("\(result.symbol)\(result.message)")
            if case .failed = result { failures += 1 }
        }

        print("")
        let total = results.count
        print("Проверок: \(total), несовпадений: \(failures)")
        if failures == 0 {
            print("Сетевой слой работает на реальных сокетах.")
            print("Проверка связи с устройством шоу — фаза 7, см. plans/porting-plan.md")
        }
        return failures == 0 ? 0 : 1
    }

    private func add(_ result: ProbeResult) {
        results.append(result)
    }

    // MARK: - Интерфейсы

    private func checkInterfaceEnumeration() {
        let all = InterfaceEnumerator.allInterfaces()
        guard !all.isEmpty else {
            add(.failed("интерфейсы", "список пуст"))
            return
        }

        for iface in all where iface.index == 0 {
            add(.failed("индекс интерфейса", "у \(iface.name) индекс 0, "
                       + "привязать сокет не к чему"))
            return
        }

        let usable = InterfaceEnumerator.usableInterfaces()
        let names = usable.map { "\($0.name) \($0.displayName)" }.joined(separator: ", ")

        if usable.isEmpty {
            add(.skipped("интерфейсы: нет пригодных "
                         + "(подключите Mac к сети с устройством)"))
        } else {
            add(.passed("интерфейсы: \(usable.count) пригодных — \(names)"))
        }

        // Виртуальные не должны попасть в рабочий список.
        let virtuals = usable.filter { $0.kind != .ethernet }
        if virtuals.isEmpty {
            add(.passed("фильтрация: виртуальных интерфейсов в списке нет"))
        } else {
            add(.failed("фильтрация",
                        "в списке виртуальные: \(virtuals.map(\.name).joined(separator: ", "))"))
        }
    }

    // MARK: - UDP

    /// Пакет должен дойти от отправителя до приёмника на loopback,
    /// а собственный запрос — быть отброшен.
    private func checkUdpRoundTrip() {
        let receiver = UdpReceiver(port: 0)
        var received: Data?

        receiver.onPacket = { data, _ in
            received = data
        }

        do {
            try receiver.start()
            defer { receiver.stop() }

            let listeningPort = receiver.listeningPort
            guard listeningPort > 0 else {
                add(.failed("UDP приём", "порт не назначен"))
                return
            }

            // Отправляем ответ, как будто его прислало устройство: заголовок
            // 49 совпадает с собственным запросом, но длина больше
            // 3 байт — именно этим они различаются.
            let deviceAnswer = Data([0x49, 0x11, 0x22, 0x33, 0x44, 0x55])
            let sender = BSDDatagramSocketForProbe()
            do {
                // Сокет обязательно привязан: без bind отправка
                // не работает, а привязка к адресу интерфейса — это
                // ровно то поведение, ради которого всё затевалось.
                try sender.bind(port: 0, to: IPv4Address([127, 0, 0, 1]))
                try sender.send(deviceAnswer,
                                to: IPv4Address([127, 0, 0, 1]),
                                port: listeningPort)
            } catch {
                add(.failed("UDP отправка", "\(error)"))
                return
            }

            // Ждём доставки.
            let deadline = Date().addingTimeInterval(2)
            while received == nil && Date() < deadline {
                usleep(20_000)
            }

            guard let data = received else {
                add(.failed("UDP приём", "пакет не дошёл за 2 секунды"))
                return
            }

            if data == deviceAnswer {
                add(.passed("UDP: пакет доставлен на порт \(listeningPort)"))
            } else {
                add(.failed("UDP приём", "пришли другие байты: \(data)"))
            }
        } catch {
            add(.failed("UDP приём", "\(error)"))
        }
    }

    // MARK: - TCP

    /// Устройство подключается, приложение отвечает в тот же сокет.
    private func checkTcpRoundTrip() {
        let listener = TcpListener()
        var sentData: Data?
        var callbackPort: UInt16 = 0

        listener.onConnection = { connection in
            callbackPort = connection.callbackPort
            try? connection.send(
                PlaybackPacket.encode(.play, positionMilliseconds: 4321))
        }

        do {
            try listener.start()
            defer { listener.stop() }

            let port = listener.listeningPort
            guard port > 0 else {
                add(.failed("TCP", "порт не назначен"))
                return
            }

            let client = TCPClientForProbe()
            guard client.connect(to: IPv4Address([127, 0, 0, 1]), port: port) else {
                add(.failed("TCP", "клиент не подключился к порту \(port)"))
                return
            }
            defer { client.close() }

            sentData = client.receive(timeoutMilliseconds: 2000)

            guard let data = sentData else {
                add(.failed("TCP", "ответ не получен"))
                return
            }

            if data.count == 8,
               PlaybackPacket.command(from: data) == .play,
               PlaybackPacket.position(from: data) == 4321 {
                add(.passed("TCP: соединение принято, ответ \(data.count) байт "
                            + "на порт источника \(callbackPort)"))
            } else {
                add(.failed("TCP", "ответ неверный: \(data)"))
            }
        } catch {
            add(.failed("TCP", "\(error)"))
        }
    }

    // MARK: - Art-Net

    /// Пакет Art-Net должен дойти и разобраться с правильной вселенной.
    private func checkArtNetParsing() {
        // Отправляем пакет Art-Net через loopback на порт 6454.
        let receiver = ArtNetReceiver()
        var parsed: ArtNetDMXPacket?

        receiver.onDMX = { packet, _ in
            parsed = packet
        }

        do {
            try receiver.start()
            defer { receiver.stop() }

            let sender = BSDDatagramSocketForProbe()
            // Собираем ArtDmx: вселенная 1, три канала.
            var bytes: [UInt8] = [0x41, 0x72, 0x74, 0x2D, 0x4E, 0x65, 0x74, 0x00]
            bytes += [0x00, 0x50]            // OpCode
            bytes += [0x00, 0x0E]            // версия
            bytes += [0x01, 0x00]            // sequence, physical
            bytes += [0x01, 0x00]            // universe 1, little endian
            bytes += [0x00, 0x03]            // длина 3, big endian
            bytes += [0x32, 0x64, 0x00]      // значения 50, 100, 0

            try sender.bind(port: 0, to: IPv4Address([127, 0, 0, 1]))
            try sender.send(Data(bytes), to: IPv4Address([127, 0, 0, 1]), port: 6454)

            let deadline = Date().addingTimeInterval(2)
            while parsed == nil && Date() < deadline {
                usleep(20_000)
            }

            guard let packet = parsed else {
                add(.failed("Art-Net", "пакет не дошёл за 2 секунды"))
                return
            }

            if packet.universe == 1 && packet.dmxData.count == 3 {
                add(.passed("Art-Net: вселенная \(packet.universe), "
                            + "каналов \(packet.dmxData.count)"))
            } else {
                add(.failed("Art-Net",
                            "вселенная \(packet.universe), каналов \(packet.dmxData.count)"))
            }
        } catch {
            add(.failed("Art-Net", "\(error)"))
        }
    }

    // MARK: - Привязка broadcast

    /// Сокет отправки должен привязаться к адресу интерфейса.
    /// Если не привяжется — на реальном устройстве пакеты уйдут в utun.
    private func checkBroadcastBinding() {
        guard let iface = InterfaceEnumerator.usableInterfaces().first else {
            add(.skipped("привязка broadcast: нет пригодного интерфейса"))
            return
        }

        let socket = BSDDatagramSocketForProbe()
        do {
            try socket.bind(port: 0, to: iface.primaryAddress)
            defer { socket.close() }

            let bound = socket.localAddress?.description ?? "?"
            if bound == iface.primaryAddress?.description {
                add(.passed("привязка: сокет слушает на \(bound) "
                            + "(интерфейс \(iface.name), индекс \(iface.index))"))
            } else {
                add(.failed("привязка",
                            "ожидалось \(iface.primaryAddress?.description ?? "?"), получено \(bound)"))
            }
        } catch {
            add(.failed("привязка broadcast", "\(error)"))
        }
    }
// MARK: - Аудио

    /// Генерирует короткий WAV-файл, чтобы проверить аудио без
    /// внешних файлов.
    private func makeTestWav(seconds: Double) -> URL? {
        let sampleRate = 22_050.0
        let frameCount = Int(sampleRate * seconds)
        let channels: UInt16 = 1
        let bitsPerSample: UInt16 = 16
        let bytesPerSample = Int(bitsPerSample / 8)
        let dataSize = frameCount * Int(channels) * bytesPerSample

        var data = Data()
        // Заголовок RIFF.
        data.append(contentsOf: Array("RIFF".utf8))
        data.append(contentsOf: littleEndian(UInt32(36 + dataSize)))
        data.append(contentsOf: Array("WAVE".utf8))
        // fmt: PCM, моно.
        data.append(contentsOf: Array("fmt ".utf8))
        data.append(contentsOf: littleEndian(UInt32(16)))
        data.append(contentsOf: littleEndian(UInt16(1)))
        data.append(contentsOf: littleEndian(channels))
        data.append(contentsOf: littleEndian(UInt32(sampleRate)))
        data.append(contentsOf: littleEndian(
                    UInt32(sampleRate) * UInt32(channels) * UInt32(bytesPerSample)))
        data.append(contentsOf: littleEndian(channels * UInt16(bytesPerSample)))
        data.append(contentsOf: littleEndian(bitsPerSample))
        // Тихий сигнал: синус малой амплитуды, чтобы не трещать в колонках.
        data.append(contentsOf: Array("data".utf8))
        data.append(contentsOf: littleEndian(UInt32(dataSize)))
        for frame in 0..<frameCount {
            let t = Double(frame) / sampleRate
            let value = Int(sin(2 * .pi * 220 * t) * 2000)
            data.append(contentsOf: littleEndian(Int16(value)))
        }

        let url = URL(fileURLWithPath: NSTemporaryDirectory())
            .appendingPathComponent("epicprobe-\(UUID().uuidString).wav")
        do {
            try data.write(to: url)
            return url
        } catch {
            return nil
        }
    }

    private func littleEndian<T: FixedWidthInteger>(_ value: T) -> [UInt8] {
        var v = value.littleEndian
        return withUnsafeBytes(of: &v) { Array($0) }
    }

    /// Проверяет, что аудиофайл открывается и позиция идёт вперёд.
    private func checkAudio() {
        guard let url = makeTestWav(seconds: 3) else {
            add(.failed("аудио", "не удалось создать тестовый WAV"))
            return
        }
        defer { try? FileManager.default.removeItem(at: url) }

        let audio = AudioPlayer()
        do {
            try audio.open(url: url)
        } catch {
            add(.failed("аудио", "файл не открылся: \(error)"))
            return
        }

        guard let duration = audio.duration else {
            add(.failed("аудио", "длительность не определилась"))
            return
        }

        guard duration > 2.5 else {
            add(.failed("аудио", "длительность \(duration) вместо ~3 с"))
            return
        }

        audio.volume = 0.25
        audio.play()
        Thread.sleep(forTimeInterval: 0.6)
        let afterPlay = audio.position

        audio.pause()
        let atPause = audio.position
        Thread.sleep(forTimeInterval: 0.3)
        let afterPause = audio.position

        guard afterPlay > 0 else {
            add(.failed("аудио", "позиция не двигается при воспроизведении"))
            return
        }

        // После паузы позиция не должна расти.
        guard afterPause - atPause < 0.05 else {
            add(.failed("аудио", "позиция двигается после паузы"))
            return
        }

        // Перемотка.
        audio.position = 1.5
        guard abs(audio.position - 1.5) < 0.2 else {
            add(.failed("аудио", "перемотка не сработала"))
            return
        }

        audio.close()

        add(.passed("аудио: длительность \(String(format: "%.1f", duration)) с, "
                    + "позиция идёт, пауза и перемотка работают"))
    }
}

/// Отправляющий сокет для зонда.
final class BSDDatagramSocketForProbe {
    private let socket: DatagramSocket

    init() {
        socket = BSDDatagramSocket()
    }

    func bind(port: UInt16, to address: IPv4Address?) throws {
        try socket.bind(port: port, to: address)
    }

    func send(_ data: Data, to address: IPv4Address, port: UInt16) throws {
        try socket.send(data, to: address, port: port)
    }

    var localAddress: IPv4Address? { socket.localAddress }
    func close() { socket.close() }
}

/// TCP-клиент для зонда: подключается и держит соединение.
final class TCPClientForProbe {
    private var connection: BSDStreamConnection?

    func connect(to address: IPv4Address, port: UInt16) -> Bool {
        let fd = socket(AF_INET, SOCK_STREAM, 0)
        guard fd >= 0 else { return false }

        var target = sockaddr_in()
        target.sin_len = UInt8(MemoryLayout<sockaddr_in>.size)
        target.sin_family = sa_family_t(AF_INET)
        target.sin_port = port.bigEndian
        target.sin_addr = in_addr(s_addr: address.inAddrT)

        let result = withUnsafePointer(to: &target) { pointer in
            pointer.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                Darwin.connect(fd, $0, socklen_t(MemoryLayout<sockaddr_in>.size))
            }
        }
        guard result == 0 else {
            Darwin.close(fd)
            return false
        }

        // Владение дескриптором переходит соединению: закрывать здесь
        // нельзя, иначе ответ будет отправлен в закрытый сокет.
        var local = sockaddr_in()
        var localLength = socklen_t(MemoryLayout<sockaddr_in>.size)
        _ = withUnsafeMutablePointer(to: &local) { pointer in
            pointer.withMemoryRebound(to: sockaddr.self, capacity: 1) { sockPointer in
                getsockname(fd, sockPointer, &localLength)
            }
        }

        connection = BSDStreamConnection(
            descriptor: fd,
            remoteAddress: address,
            remotePort: UInt16(bigEndian: local.sin_port))
        return true
    }

    func receive(timeoutMilliseconds: Int32) -> Data? {
        try? connection?.receive(timeoutMilliseconds: timeoutMilliseconds)
    }

    func close() {
        connection?.close()
        connection = nil
    }
}