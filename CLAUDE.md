# Pixel Tuner Studio: notes for coding agents

- Reply to the owner in Russian. Code, comments, and commit messages in English.
- Window titles and product name: "Pixel Tuner Studio". Repository: shimin/pixel-tuner-studio.
- Project package extension is `.pxpkg` (not .pixpkg, not .ptpkg).
- Stack: .NET 10 + Avalonia 12. Shared code in `src/PixelTuner.*`; platform code only in `src/Heads/*`.
- Follow each platform's guidelines: Fluent on Windows, macOS menu bar and Cmd shortcuts,
  iOS safe areas and document picker, Android back navigation and storage access framework.
- No reflection-based serialization or view location: iOS is AOT. Use System.Text.Json source
  generation (`PackageJsonContext`) and compiled bindings (`x:DataType`).
- UI strings go into `src/PixelTuner.UI/Resources/Strings*.resx` (en default, ru, de).
- `legacy/` is history and reference. Do not add features there. PixelTuner 2 must keep building
  from `legacy/PixelTuner2` exactly as before (root props are blocked by `legacy/Directory.Build.props`).
- Device protocol changes must update the JSON vectors in
  `legacy/EpicPlayer/epicplayer-mac/Tests/Fixtures/` and pass `tests/PixelTuner.Sync.Tests`.
- Device .bin export (`src/PixelTuner.Core/Export`) must stay byte for byte what PixelTuner 2 wrote:
  `tests/PixelTuner.Core.Tests` checks it against real shows in `Fixtures/LegacyProjects`, and the
  CI job "Export against the reference set" checks the whole set from the draft release `samples-v1`
  (locally: unpack it and set `PIXELTUNER_SAMPLES`). Only JPEG sources may differ, by one level,
  and video images from the GIFs listed in `FullSampleVideoExportTests.WindowsDecodedDifferently`:
  WPF changed some pixels of small-palette frames, and Studio keeps the exact palette colours
  (Ivan: where Windows and the cleaner result differ, prefer quality over parity).
  E-Pic poi and LightMaster have no reference shows: `tests/PixelTuner.Legacy.OracleTests` compiles
  PixelTuner 2's own `Communication/Gradient` and `Communication/Binarize` and compares on random shows.
- The .bin export runs on the model (`ShowProject`). The `Ptp*Export` classes convert the .ptp with
  `PtpConverter` first, so the reference tests also cover the .ptp import. A field that the export
  needs must survive `PtpConverter` and the package JSON: `tests/PixelTuner.Package.Tests` checks
  that a package exports the same .bin as its .ptp (CI does this for the whole `samples-v1` set too).
- Merge PRs with a normal merge commit only.
- The cloud sandbox can build and test Core, Package, Sync, UI and Desktop on Linux
  (`apt install dotnet-sdk-10.0`, `SSL_CERT_FILE=/root/.ccr/ca-bundle.crt` for NuGet).
  Android needs the Android SDK (download is blocked in the sandbox) and iOS needs a Mac: rely on CI.
