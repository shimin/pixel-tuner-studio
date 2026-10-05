import Foundation

/// Абстракция над системными сокетами.
///
/// Существует, чтобы логику приёма и отправки можно было проверить
/// юнит-тестами: в тестах подставляется реализация в памяти, а в
/// приложении — реальные BSD-сокеты. Реальный сетевой обмен проверяется
/// интеграционным зондом, см. `Sources/SocketProbe` и фазу 7 плана.
public protocol DatagramSocket: AnyObject {
    /// Привязывает сокет к порту. `address == nil` — ко всем интерфейсам.
    ///
    /// - Parameter port: 0 означает «выбрать системой».
    func bind(port: UInt16, to address: IPv4Address?) throws

    /// Отправляет пакет на широковещательный или конкретный адрес.
    func send(_ data: Data, to destination: IPv4Address, port: UInt16) throws

    /// Блокирующий приём пакета. Возвращает nil при таймауте или закрытии.
    func receive(timeoutMilliseconds: Int32) throws -> Datagram?

    /// Фактический порт после `bind`.
    var localPort: UInt16 { get }

    /// Адрес, к которому привязан сокет.
    var localAddress: IPv4Address? { get }

    func close()
}

/// Принятый датаграмма-пакет.
public struct Datagram {
    public let data: Data
    /// Адрес отправителя.
    public let sender: IPv4Address

    public init(data: Data, sender: IPv4Address) {
        self.data = data
        self.sender = sender
    }
}

/// Соединение, установленное устройством.
public protocol StreamConnection: AnyObject {
    func send(_ data: Data) throws
    func receive(timeoutMilliseconds: Int32) throws -> Data?

    /// Адрес устройства.
    var remoteAddress: IPv4Address { get }

    /// Порт, с которого устройство подключилось: ответ нужно слать туда.
    ///
    /// Windows-версия отвечала в сокет, подключённый к порту источника,
    /// см. `WifiTcpDevicesWindow.xaml.cs:123`.
    var remotePort: UInt16 { get }

    func close()
}

/// Слушатель TCP-соединений.
public protocol StreamListener: AnyObject {
    /// Начинает слушать. Порт 0 — выбрать системой.
    func listen(on port: UInt16) throws

    /// Блокирующее принятие соединения. nil при таймауте или закрытии.
    func accept(timeoutMilliseconds: Int32) throws -> StreamConnection?

    /// Фактический порт после `listen`.
    var localPort: UInt16 { get }

    func close()
}

/// Фабрика сокетов: системная по умолчанию, подменяемая в тестах.
public struct SocketFactory: @unchecked Sendable {
    public var makeDatagram: () -> DatagramSocket
    public var makeListener: () -> StreamListener

    public init(
        makeDatagram: @escaping () -> DatagramSocket,
        makeListener: @escaping () -> StreamListener
    ) {
        self.makeDatagram = makeDatagram
        self.makeListener = makeListener
    }

    /// Настоящие BSD-сокеты.
    public static let system = SocketFactory(
        makeDatagram: { BSDDatagramSocket() },
        makeListener: { BSDStreamListener() })
}