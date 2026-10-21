# Pixel Tuner Studio: notes for coding agents

- Reply to the owner in Russian. Code, comments, and commit messages in English.
- Two apps on one code base: "Pixel Tuner Studio" (Windows, macOS; `com.pixeltuner.studio`,
  head `src/Heads/PixelTuner.Studio.Desktop`) and "Pixel Tuner Stage" (iOS, Android;
  `com.pixeltuner.stage`, heads `PixelTuner.Stage.iOS` and `PixelTuner.Stage.Android`).
  Shared code speaks as "Pixel Tuner": no version, no Studio or Stage. The app name appears only
  in its head and where the text is about the running app (`PixelTuner.UI.Product.Current`).
  Versions: `StudioVersion` and `StageVersion` in `Directory.Build.props`. Icons come from
  `scripts/icons/make-icons.py`. Repository: shimin/pixel-tuner-studio.
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
- Cable and Wi-Fi device protocol (`src/PixelTuner.Devices`, PixelTuner 2's `Communication/Service`):
  packets `[command, length, data, 0x0D]`, block sizes and info layouts as PixelTuner 2 had them.
  `tests/PixelTuner.Devices.Tests` drives it with an in-memory device and a real TCP connection.
- Studio (not Stage; `Product.CanEdit`) creates, opens, saves and imports projects. The open project is a
  `ProjectDocument` (`PixelTuner.Package`: path, dirty flag, atomic Save/SaveAs that keeps the media);
  every change of the model goes through `ProjectDocument.Execute(Edits.X(...))` (`PixelTuner.Core/Editing`:
  an `Edit` applies and reverts itself, `UndoHistory` keeps the stacks and decides "unsaved": undoing back to
  the saved state is clean again). New kinds of change are new factories in `Edits` with a test and an
  `Edit_<Key>` string in the resx files; items on a timeline stay sorted and never overlap. Desktop menus are one `NativeMenu` in `MainWindow.axaml`
  (menu bar on macOS, `NativeMenuBar` on Windows); shortcuts come from `Views/Shortcuts` (Ctrl or Cmd).
  Unsaved changes are asked about before open, new, import, close and window close (`MainViewModel.ConfirmDiscardAsync`).
  `tests/PixelTuner.UI.Tests` drives all of it with fake dialogs.
- The timelines (`src/PixelTuner.UI/Timelines`): `TimelineEditor` holds everything that is not drawing (coordinates,
  zoom, hit testing, selection, drags that end in one `Edit`, snapping) and is tested without a window;
  `TimelineControl` only draws it and forwards the pointer. Do not put model rules into the control. To try the
  window with the mouse in the sandbox: `xvfb-run`, then `python-xlib` (`pip install python-xlib`) with XTest.
- `src/PixelTuner.Audio` holds the player and decoder contracts only; implementations go per
  platform. `src/PixelTuner.Audio.Vlc` (LibVLC, LGPL, dynamic) serves Windows and macOS: the VideoLAN NuGet
  packages carry LibVLC under `libvlc/<rid>`, Linux uses the system one (`apt install libvlc5 vlc-plugin-base`).
  The shared UI sees only `IAudioEngine` (`AudioEngine.Current`, set by the head); phones get system players later.
  LibVLC rules learned the hard way: never call it from its own event threads (deadlock or crash), raise our
  events from a worker, and release a `Media` that failed to parse late (releasing it at once crashes the process).
  `PIXELTUNER_VLC_ARGS="--aout=dummy"` runs the app without a sound card; `PIXELTUNER_REQUIRE_VLC=1` makes the
  LibVLC tests fail instead of passing quietly when LibVLC is missing (CI sets it).
- Merge PRs with a normal merge commit only.
- The cloud sandbox can build and test Core, Package, Sync, UI and Desktop on Linux
  (`apt install dotnet-sdk-10.0`, `SSL_CERT_FILE=/root/.ccr/ca-bundle.crt` for NuGet).
  Android needs the Android SDK (download is blocked in the sandbox) and iOS needs a Mac: rely on CI.
