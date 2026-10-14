import SwiftUI

/// Цвета Windows-версии: фон `#202020`, прогресс `#29EE4D`, ошибки `#CC0000`.
enum Theme {
    static let background = Color(red: 0x20 / 255, green: 0x20 / 255, blue: 0x20 / 255)
    static let progress = Color(red: 0x29 / 255, green: 0xEE / 255, blue: 0x4D / 255)
    static let error = Color(red: 0xCC / 255, green: 0, blue: 0)
    static let panel = Color.white.opacity(0.06)
}

/// Крупная кнопка транспорта, высота 42 как в Windows.
struct TransportButton: View {
    let title: String
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            Text(title)
                .frame(maxWidth: .infinity, minHeight: 34)
        }
        .controlSize(.large)
    }
}
