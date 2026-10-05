import Foundation

/// Точка входа интеграционного зонда.
@main
struct SocketProbeMain {
    static func main() {
        let probe = SocketProbe()
        let status = probe.run()
        exit(status)
    }
}