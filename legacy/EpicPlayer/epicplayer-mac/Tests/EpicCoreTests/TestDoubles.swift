import Foundation
@testable import EpicCore

/// Потокобезопасный счётчик: фоновый поток приёмника пишет в него,
/// тест читает.
final class PacketCounter: @unchecked Sendable {
    private let lock = NSLock()
    private var count = 0

    var value: Int {
        lock.lock()
        defer { lock.unlock() }
        return count
    }

    func increment() {
        lock.lock()
        count += 1
        lock.unlock()
    }
}

/// Потокобезопасная очередь пакетов.
    ///
    /// Фоновое чтение забирает пакеты с интервалом, тест наполняет очередь.
    /// При пустой очередее `receive` ждёт сигнала, иначе цикл читателя
    /// вращается вхолостую и забирает пакеты до того, как тест их положит.
    final class PacketQueue: @unchecked Sendable {
        private let condition = NSCondition()
        private var items: [Datagram] = []
        private var isClosed = false

        var count: Int {
            condition.lock()
            defer { condition.unlock() }
            return items.count
        }

        func snapshot() -> [Datagram] {
            condition.lock()
            defer { condition.unlock() }
            return items
        }

        func append(_ datagram: Datagram) {
            condition.lock()
            items.append(datagram)
            condition.signal()
            condition.unlock()
        }

        func set(_ newItems: [Datagram]) {
            condition.lock()
            items = newItems
            condition.signal()
            condition.unlock()
        }

        /// Ожидает появления пакета. nil — очередь закрыта.
        func next(timeoutMilliseconds: Int32) -> Datagram? {
            condition.lock()
            defer { condition.unlock() }

            if isClosed { return nil }
            if items.isEmpty {
                let deadline = Date().addingTimeInterval(
                    TimeInterval(timeoutMilliseconds) / 1000.0)
                while items.isEmpty && !isClosed && Date() < deadline {
                    condition.wait(until: deadline)
                }
            }

            guard !items.isEmpty else { return nil }
            return items.removeFirst()
        }

        func close() {
            condition.lock()
            isClosed = true
            condition.broadcast()
            condition.unlock()
        }
    }

/// Сокеты в памяти: позволяют проверить логику приёма и отправки
/// без настоящих сокетов.
///
/// Настоящие сокеты в процессе `swift test` не работают: бинарник,
/// который собирает SwiftPM, не имеет `Info.plist`, а macOS 15+
/// ограничивает такие процессы. Реальный сетевой обмен проверяется
/// интеграционным зондом, см. `Sources/SocketProbe`.
    final class FakeDatagramSocket: DatagramSocket {
    private let lock = NSLock()
    private let queue = PacketQueue()

    /// Что было отправлено.
    private(set) var sent: [(data: Data, destination: IPv4Address, port: UInt16)] = []

    private(set) var boundPort: UInt16 = 0
    private(set) var boundAddress: IPv4Address?
    private(set) var isClosed = false

    var localPort: UInt16 { boundPort }
    var localAddress: IPv4Address? { boundAddress }

    /// Очередь входящих пакетов.
    ///
    /// Чтение возвращает снимок оставшегося: тесты только наполняют
    /// очередь и проверяют счётчик доставки.
    var incoming: [Datagram] {
        get { queue.snapshot() }
        set { queue.set(newValue) }
    }

    func bind(port: UInt16, to address: IPv4Address?) throws {
        // Порт 0 в тестах заменяем на фиксированный: так адрес предсказуем.
        boundPort = port == 0 ? 45_000 : port
        boundAddress = address
        isClosed = false
    }

    func send(_ data: Data, to destination: IPv4Address, port: UInt16) throws {
        guard !isClosed else { throw NetworkError.notConfigured }
        lock.lock()
        sent.append((data, destination, port))
        lock.unlock()
    }

    func receive(timeoutMilliseconds: Int32) throws -> Datagram? {
        guard !isClosed else { return nil }
        return queue.next(timeoutMilliseconds: max(timeoutMilliseconds, 1))
    }

    func close() {
        isClosed = true
        queue.close()
    }
}

final class FakeStreamConnection: StreamConnection {
    private(set) var sent: [Data] = []
    private(set) var isClosed = false

    let remoteAddress: IPv4Address
    let remotePort: UInt16

    init(remoteAddress: IPv4Address, remotePort: UInt16) {
        self.remoteAddress = remoteAddress
        self.remotePort = remotePort
    }

    func send(_ data: Data) throws {
        guard !isClosed else { throw NetworkError.notConfigured }
        sent.append(data)
    }

    func receive(timeoutMilliseconds: Int32) throws -> Data? { nil }

    func close() { isClosed = true }
}

/// Соединения, которые вернёт `accept`. Потокобезопасно: фоновый поток
/// и тест могут обращаться одновременно.
final class FakeStreamListener: StreamListener {
    private let lock = NSLock()
    private var queued: [StreamConnection] = []

    private(set) var boundPort: UInt16 = 0
    private(set) var isClosed = false

    var localPort: UInt16 { boundPort }

    var pending: [StreamConnection] {
        get { lock.lock(); defer { lock.unlock() }; return queued }
        set { lock.lock(); queued = newValue; lock.unlock() }
    }

    func listen(on port: UInt16) throws {
        boundPort = 45_001
        isClosed = false
    }

    func accept(timeoutMilliseconds: Int32) throws -> StreamConnection? {
        lock.lock()
        defer { lock.unlock() }
        guard !isClosed, !queued.isEmpty else { return nil }
        return queued.removeFirst()
    }

    func close() {
        lock.lock()
        isClosed = true
        lock.unlock()
    }
}

/// Фабрика, отдающая заранее заданные сокеты.
func makeFactory(
    datagram: DatagramSocket,
    listener: StreamListener
) -> SocketFactory {
    SocketFactory(
        makeDatagram: { datagram },
        makeListener: { listener })
}