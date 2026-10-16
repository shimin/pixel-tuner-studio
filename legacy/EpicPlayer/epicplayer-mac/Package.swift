// swift-tools-version:6.0
import PackageDescription

// Tools 6.0 нужен для .macOS(.v15). Язык — Swift 5: строгая проверка
// конкурентности Swift 6 включается позже и постепенно.
// Раскладка таргетов — plans/app-design.md, раздел 2.
let package = Package(
    name: "EpicPlayer",
    platforms: [.macOS(.v15)],
    products: [
        .library(name: "EpicCore", targets: ["EpicCore"]),
        .library(name: "EpicPlayerKit", targets: ["EpicPlayerKit"]),
        // Приложение. Бандл .app собирает scripts/build-app.sh.
        .executable(name: "EpicPlayerMac", targets: ["EpicPlayerMac"]),
        // Проверка сетевого слоя на устройстве без UI: swift run epic-probe
        .executable(name: "epic-probe", targets: ["EpicProbe"])
    ],
    targets: [
        .target(name: "EpicCore"),
        .target(name: "EpicPlayerKit", dependencies: ["EpicCore"]),
        .executableTarget(name: "EpicPlayerMac", dependencies: ["EpicPlayerKit", "EpicCore"]),
        .executableTarget(name: "EpicProbe", dependencies: ["EpicCore"]),
        .testTarget(name: "EpicCoreTests", dependencies: ["EpicCore"]),
        .testTarget(name: "EpicPlayerKitTests", dependencies: ["EpicPlayerKit", "EpicCore"])
    ],
    swiftLanguageModes: [.v5]
)
