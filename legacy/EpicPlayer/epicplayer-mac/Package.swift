// swift-tools-version:6.0
import PackageDescription

// Язык Swift 5, не Swift 6: строгая проверка конкурентности включается
// постепенно, начиная с сетевого кода.
let package = Package(
    name: "EpicCore",
    platforms: [.macOS(.v15)],
    products: [
        .library(name: "EpicCore", targets: ["EpicCore"]),
        .executable(name: "EpicPlayerMac", targets: ["EpicPlayerMac"])
    ],
    targets: [
        // Протокол, сокеты, Art-Net. Собирается и тестируется без UI.
        .target(name: "EpicCore"),

        // Приложение. Зависит от EpicCore.
        //
        // Info.plint намеренно не включён в resources: SwiftPM запрещает
        // Info.plist как ресурс бандла. Файл используется как
        // INFOPLIST_FILE в Xcode-проекте (фаза 0).
        .executableTarget(
            name: "EpicPlayerMac",
            dependencies: ["EpicCore"],
            path: "Sources/EpicPlayerMac",
            exclude: ["Resources/Info.plist"],
            swiftSettings: [.swiftLanguageMode(.v5)]
        ),

        .testTarget(name: "EpicCoreTests", dependencies: ["EpicCore"])
    ]
)