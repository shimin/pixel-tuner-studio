// swift-tools-version:5.7
import PackageDescription

// Временно Swift 5, не Swift 6: строгая проверка конкурентности
// включается постепенно, начиная с фазы сетевого кода.
// После обновления Xcode таргет поднимается до .macOS(.v15).
let package = Package(
    name: "EpicCore",
    platforms: [.macOS(.v13)],
    products: [
        .library(name: "EpicCore", targets: ["EpicCore"])
    ],
    targets: [
        .target(name: "EpicCore"),
        .testTarget(name: "EpicCoreTests", dependencies: ["EpicCore"])
    ]
)