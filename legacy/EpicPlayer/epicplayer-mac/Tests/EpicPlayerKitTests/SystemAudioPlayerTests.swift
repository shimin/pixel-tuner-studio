import XCTest
@testable import EpicPlayerKit

/// Настоящий `AVAudioPlayer` без воспроизведения: только загрузка файла.
@MainActor
final class SystemAudioPlayerTests: XCTestCase {
    private var directory: URL!

    override func setUpWithError() throws {
        directory = FileManager.default.temporaryDirectory
            .appendingPathComponent("SystemAudioPlayerTests-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: directory)
    }

    /// C11 в final-session.md: вместо «OSStatus error 1685348671» — понятный текст.
    func testDamagedFileHasReadableError() throws {
        let url = directory.appendingPathComponent("broken.mp3")
        try Data("this is not audio".utf8).write(to: url)

        XCTAssertThrowsError(try SystemAudioPlayer().load(url)) { error in
            XCTAssertTrue(error is AudioFileError, "\(error)")
            XCTAssertEqual(error.localizedDescription, "unsupported or damaged audio file")
        }
    }

    func testWavLoadsWithDuration() throws {
        let url = directory.appendingPathComponent("tone.wav")
        try Self.silentWav(seconds: 1).write(to: url)

        let player = SystemAudioPlayer()
        try player.load(url)
        XCTAssertTrue(player.isLoaded)
        XCTAssertEqual(player.duration, 1, accuracy: 0.05)
        XCTAssertEqual(player.currentTime, 0)
    }

    /// 16-битный моно PCM 8 кГц, тишина.
    private static func silentWav(seconds: Int) -> Data {
        let sampleRate: UInt32 = 8000
        let dataSize = sampleRate * 2 * UInt32(seconds)
        var bytes = Data()
        func append<T: FixedWidthInteger>(_ value: T) {
            withUnsafeBytes(of: value.littleEndian) { bytes.append(contentsOf: $0) }
        }
        bytes.append(contentsOf: Array("RIFF".utf8)); append(UInt32(36 + dataSize))
        bytes.append(contentsOf: Array("WAVEfmt ".utf8)); append(UInt32(16))
        append(UInt16(1)); append(UInt16(1)); append(sampleRate); append(sampleRate * 2)
        append(UInt16(2)); append(UInt16(16))
        bytes.append(contentsOf: Array("data".utf8)); append(dataSize)
        bytes.append(Data(count: Int(dataSize)))
        return bytes
    }
}
