import os

/// Логи сетевого слоя. Смотреть: Console.app, фильтр по подсистеме,
/// или `log stream --predicate 'subsystem == "com.epicplayer.mac"'`.
enum Log {
    static let subsystem = "com.epicplayer.mac"

    static let network = Logger(subsystem: subsystem, category: "network")
    static let artnet = Logger(subsystem: subsystem, category: "artnet")
}
