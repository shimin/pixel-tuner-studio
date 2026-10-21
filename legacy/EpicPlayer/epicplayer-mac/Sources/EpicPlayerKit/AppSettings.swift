import EpicCore
import Foundation

/// Настройки Art-Net в том виде, как их вводит пользователь.
/// См. plans/app-design.md, раздел 7.
public struct ArtNetSettings: Codable, Equatable, Sendable {
    public var enabled = false
    /// `nil` — любой интерфейс.
    public var interfaceName: String?
    /// `nil` — любой universe, как фактически работает Windows-версия.
    public var universe: Int?
    /// Номер канала DMX, с единицы. В Windows — индекс с нуля
    /// (plans/app-design.md, 9.4).
    public var channel = 1
    public var playValue = 50
    public var pauseValue = 100
    public var stopValue = 150

    public init() {}

    public static let channelRange = 1...512
    public static let valueRange = 0...255
    public static let universeRange = 0...32767

    /// Перевод в настройки `DmxTrigger`: канал → индекс, значения
    /// приводятся к допустимым диапазонам.
    public var triggerSettings: DmxTriggerSettings {
        DmxTriggerSettings(
            channel: Self.clamp(channel, to: Self.channelRange) - 1,
            playValue: UInt8(Self.clamp(playValue, to: Self.valueRange)),
            pauseValue: UInt8(Self.clamp(pauseValue, to: Self.valueRange)),
            stopValue: UInt8(Self.clamp(stopValue, to: Self.valueRange)),
            universe: universe.map { UInt16(Self.clamp($0, to: Self.universeRange)) }
        )
    }

    static func clamp(_ value: Int, to range: ClosedRange<Int>) -> Int {
        min(max(value, range.lowerBound), range.upperBound)
    }
}

/// Всё, что сохраняется между запусками. См. plans/app-design.md, раздел 8.
public struct AppSettings: Codable, Equatable, Sendable {
    public var audioFilePath: String?
    /// Имя BSD (`en0`), а не индекс в списке, как в Windows.
    public var interfaceName: String?
    public var volume = 0.5
    public var artNet = ArtNetSettings()

    public init() {}

    /// Недостающие ключи берут значения по умолчанию: настройки,
    /// сохранённые старой версией, читаются без ошибки.
    public init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        let defaults = AppSettings()
        audioFilePath = try container.decodeIfPresent(String.self, forKey: .audioFilePath)
        interfaceName = try container.decodeIfPresent(String.self, forKey: .interfaceName)
        volume = try container.decodeIfPresent(Double.self, forKey: .volume) ?? defaults.volume
        artNet = try container.decodeIfPresent(ArtNetSettings.self, forKey: .artNet) ?? defaults.artNet
    }
}

extension ArtNetSettings {
    public init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        let defaults = ArtNetSettings()
        enabled = try container.decodeIfPresent(Bool.self, forKey: .enabled) ?? defaults.enabled
        interfaceName = try container.decodeIfPresent(String.self, forKey: .interfaceName)
        universe = try container.decodeIfPresent(Int.self, forKey: .universe)
        channel = try container.decodeIfPresent(Int.self, forKey: .channel) ?? defaults.channel
        playValue = try container.decodeIfPresent(Int.self, forKey: .playValue) ?? defaults.playValue
        pauseValue = try container.decodeIfPresent(Int.self, forKey: .pauseValue) ?? defaults.pauseValue
        stopValue = try container.decodeIfPresent(Int.self, forKey: .stopValue) ?? defaults.stopValue
    }
}

/// Хранение `AppSettings` одним JSON в `UserDefaults`.
///
/// Сохранение при каждом изменении с задержкой: серия правок
/// (движение слайдера громкости) даёт одну запись.
@MainActor
public final class SettingsStore {
    public static let key = "settings.v1"

    private let defaults: UserDefaults
    private let debounce: TimeInterval
    private var pending: AppSettings?
    private var scheduled: DispatchWorkItem?

    /// - Parameter debounce: 0 — писать сразу (для тестов).
    public init(defaults: UserDefaults = .standard, debounce: TimeInterval = 0.5) {
        self.defaults = defaults
        self.debounce = debounce
    }

    /// Битые или отсутствующие данные — значения по умолчанию.
    public func load() -> AppSettings {
        guard let data = defaults.data(forKey: Self.key) else { return AppSettings() }
        return (try? JSONDecoder().decode(AppSettings.self, from: data)) ?? AppSettings()
    }

    public func save(_ settings: AppSettings) {
        guard debounce > 0 else {
            write(settings)
            return
        }

        pending = settings
        scheduled?.cancel()
        let item = DispatchWorkItem { [weak self] in
            MainActor.assumeIsolated { self?.flush() }
        }
        scheduled = item
        DispatchQueue.main.asyncAfter(deadline: .now() + debounce, execute: item)
    }

    /// Записать отложенное немедленно — при выходе из приложения.
    public func flush() {
        scheduled?.cancel()
        scheduled = nil
        if let pending {
            write(pending)
            self.pending = nil
        }
    }

    private func write(_ settings: AppSettings) {
        guard let data = try? JSONEncoder().encode(settings) else { return }
        defaults.set(data, forKey: Self.key)
    }
}
