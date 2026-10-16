import Foundation

/// Настройки Art-Net.
///
/// Значения по умолчанию взяты из `Settings.settings` Windows-версии:
/// start 50, pause 100, stop 150.
///
/// - Important: в `MainWindow.xaml` указано наоборот — stop 100,
///   pause 150. Эти значения перезаписываются в конструкторе из
///   `Settings.Default`, поэтому пользователь никогда их не видит.
///   Реальные значения заданы настройками. Ошибка в XAML вводит
///   в заблуждение при переносе: устройство отреагировало бы наоборот.
public struct ArtNetSettings: Equatable, Sendable {
    /// Локальный IP компьютера.
    public var localIP: String
    /// Маска подсети.
    public var subnetMask: String
    /// Порт.
    public var port: String
    /// Вселенная.
    public var universe: String
    /// Номер канала DMX, отсчитывается с нуля.
    public var channel: Int
    /// Значение канала, запускающее воспроизведение.
    public var start: UInt8
    /// Значение канала, ставящее паузу.
    public var pause: UInt8
    /// Значение канала, останавливающее трек.
    public var stop: UInt8
    /// Включён ли режим Art-Net.
    public var isEnabled: Bool

    public init(localIP: String = "192.168.88.253",
                subnetMask: String = "255.255.255.0",
                port: String = "3333",
                universe: String = "1",
                channel: Int = 0,
                start: UInt8 = 50,
                pause: UInt8 = 100,
                stop: UInt8 = 150,
                isEnabled: Bool = false) {
        self.localIP = localIP
        self.subnetMask = subnetMask
        self.port = port
        self.universe = universe
        self.channel = channel
        self.start = start
        self.pause = pause
        self.stop = stop
        self.isEnabled = isEnabled
    }

    /// Пороги для трекера DMX.
    public var thresholds: ArtNetThresholds {
        ArtNetThresholds(start: start, pause: pause, stop: stop)
    }
}

/// Настройки приложения, сохраняемые между запусками.
///
/// Windows-версия использовала `Settings.Default`
/// (`ApplicationSettingsBase`); здесь `UserDefaults`.
public final class AppSettings {
    private enum Key {
        static let filePath = "FilePath"
        static let interfaceIndex = "InterfaceIdx"
        static let artNetIP = "IP"
        static let artNetMask = "Mask"
        static let artNetPort = "Port"
        static let artNetUniverse = "Universe"
        static let artNetChannel = "Channel"
        static let artNetStart = "StartCmd"
        static let artNetPause = "PauseCmd"
        static let artNetStop = "StopCmd"
        static let artNetEnabled = "ArtNetEnabled"
        static let wifiPort = "WifiPort"
    }

    private let defaults: UserDefaults

    public init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
        registerDefaults()
    }

    /// Регистрирует значения по умолчанию.
    ///
    /// Без этого `UserDefaults.integer(forKey:)` для отсутствующего ключа
    /// вернул бы 0, а не заданное значение: пороги DMX стали бы 0, и
    /// приложение реагировало бы на любой канал. В Windows-версии те же
    /// значения задавались генератором `Settings.settings`.
    private func registerDefaults() {
        defaults.register(defaults: [
            Key.filePath: "",
            Key.interfaceIndex: 0,
            Key.wifiPort: 333,
            Key.artNetIP: "192.168.88.253",
            Key.artNetMask: "255.255.255.0",
            Key.artNetPort: "3333",
            Key.artNetUniverse: "1",
            Key.artNetChannel: 0,
            Key.artNetStart: 50,
            Key.artNetPause: 100,
            Key.artNetStop: 150,
            Key.artNetEnabled: false,
        ])
    }

    // MARK: - Общие

    /// Путь к последнему открытому файлу.
    public var filePath: String {
        get { defaults.string(forKey: Key.filePath) ?? "" }
        set { defaults.set(newValue, forKey: Key.filePath) }
    }

    /// Индекс выбранного сетевого интерфейса.
    public var interfaceIndex: Int {
        get { defaults.integer(forKey: Key.interfaceIndex) }
        set { defaults.set(newValue, forKey: Key.interfaceIndex) }
    }

    /// Порт назначения для собственного протокола.
    public var wifiPort: UInt16 {
        get {
            let value = defaults.integer(forKey: Key.wifiPort)
            return value > 0 && value <= 65535 ? UInt16(value) : 333
        }
        set { defaults.set(Int(newValue), forKey: Key.wifiPort) }
    }

    /// Путь к файлу из настроек, если файл ещё существует.
    ///
    /// `nil`, если путь пуст или файл удалён: иначе при запуске
    /// приложение пыталось бы открыть несуществующий трек.
    public var existingFileURL: URL? {
        let path = filePath
        guard !path.isEmpty, FileManager.default.fileExists(atPath: path) else {
            return nil
        }
        return URL(fileURLWithPath: path)
    }

    // MARK: - Art-Net

    public var artNet: ArtNetSettings {
        get {
            ArtNetSettings(
                localIP: defaults.string(forKey: Key.artNetIP) ?? "192.168.88.253",
                subnetMask: defaults.string(forKey: Key.artNetMask) ?? "255.255.255.0",
                port: defaults.string(forKey: Key.artNetPort) ?? "3333",
                universe: defaults.string(forKey: Key.artNetUniverse) ?? "1",
                channel: defaults.integer(forKey: Key.artNetChannel),
                start: UInt8(defaults.integer(forKey: Key.artNetStart)),
                pause: UInt8(defaults.integer(forKey: Key.artNetPause)),
                stop: UInt8(defaults.integer(forKey: Key.artNetStop)),
                isEnabled: defaults.bool(forKey: Key.artNetEnabled))
        }
        set {
            defaults.set(newValue.localIP, forKey: Key.artNetIP)
            defaults.set(newValue.subnetMask, forKey: Key.artNetMask)
            defaults.set(newValue.port, forKey: Key.artNetPort)
            defaults.set(newValue.universe, forKey: Key.artNetUniverse)
            defaults.set(newValue.channel, forKey: Key.artNetChannel)
            defaults.set(Int(newValue.start), forKey: Key.artNetStart)
            defaults.set(Int(newValue.pause), forKey: Key.artNetPause)
            defaults.set(Int(newValue.stop), forKey: Key.artNetStop)
            defaults.set(newValue.isEnabled, forKey: Key.artNetEnabled)
        }
    }
}