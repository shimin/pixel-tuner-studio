# Pixel Tuner Studio: notes for coding agents

- Reply to the owner in Russian. Code, comments, and commit messages in English.
- Two apps on one code base: "Pixel Tuner Studio" (Windows, macOS; `com.pixeltuner.studio`,
  head `src/Heads/PixelTuner.Studio.Desktop`) and "Pixel Tuner Stage" (iOS, Android;
  `com.pixeltuner.stage`, heads `PixelTuner.Stage.iOS` and `PixelTuner.Stage.Android`).
  Shared code speaks as "Pixel Tuner": no version, no Studio or Stage. The app name appears only
  in its head and where the text is about the running app (`PixelTuner.UI.Product.Current`).
  Versions: `StudioVersion` and `StageVersion` in `Directory.Build.props`. Icons come from
  `scripts/icons/make-icons.py`. Repository: shimin/pixel-tuner-studio.
- Builds: the default solution `PixelTunerStudio.slnx` is Studio (Windows, macOS) with the tests and builds with the plain SDK; the phone apps
  are in `src/Heads/PixelTunerStage.slnx` (they need workloads). Windows: `scripts/windows/build.cmd` publishes Studio to `out/win-x64`.
  PixelTuner 2 is not in the default solution: `scripts/legacy/build-pixeltuner2.cmd` (Visual Studio MSBuild, untested here: no Windows).
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
  `tests/PixelTuner.Legacy.OracleTests` compiles PixelTuner 2's own `Communication/Pixelate`, `Gradient`
  and `Binarize` (with the `FileViewModel` glue) and compares on random shows: pixel, fan, video, E-pic poi
  and LightMaster (the last two have no reference shows). The format, field by field: `docs/binary-format.md`;
  a change to the export updates it. `ShowBuilder` (project + device → .bin, with errors and warnings) is the one
  entry for file export and upload; `BinReader` reads a .bin back. `EditorRulesTests` and `ShowBuilderTests` in
  `tests/PixelTuner.Core.Tests` check each editor rule as edit → .bin field → undo.
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
  `TimelineControl` only draws it and forwards the pointer. Do not put model rules into the control. An item dragged over another timeline moves there only if
  `Edits.Accepts` (same kind of item), `Edits.FitsHeight` and `Edits.Fits` say so (else the drag shows a `DropDenial`); item edges and the
  playhead/marker pull points land on a 50 ms grid (`TimelineEditor.OnGrid`); a lane draws the item's `ItemThumbnails` picture repeated by
  `TileLayout` (one pass = the device's time for the picture; a video's pass is a film strip, `DrawFilmStrip`: fixed-size tiles (never stretched by the zoom), each the frame that plays at its left edge, from up to `ItemThumbnails.MaxStripFrames` frames kept per video; no line is drawn between passes) and the pauses between items as black stretches; items are put on timelines from the library (`LibraryViewModel.Place`; see the library below). Rows have no border lines: their backgrounds alternate by place and are tinted by kind as in PixelTuner 2 (`TimelineRows`). The corner left of the ruler holds delete / move up / move down (`HeaderButton`, `TimelineEditor.Press`); zoom is a slider in the toolbar (`EditorViewModel.ZoomSlider`, logarithmic), the wheel over the ruler, or Ctrl/Cmd and the wheel; "fit the whole show" sits beside the slider at the right. Every zoom (slider, wheel, menu) goes through `TimelineEditor.ZoomOnPlayhead`: the playhead ends in the middle of the time area, or stays where it is on screen near the start, and while the sound plays the view keeps it in the middle until the view is panned by hand. `EditorViewModel.AddItemCommand`, `AddColorCommand` and `AddEffectCommand` have no button any more (tests use them). To try the
  window with the mouse in the sandbox: `xvfb-run`, then `python-xlib` (`pip install python-xlib`) with XTest.
- Stretching of an item is a speed, not a position; the item's time span only says how long it plays. Pixel devices
  and hoops (same .bin, PixelTuner 2 has no separate hoop semantics) hold each image column for k x 50 us, so one pass of a
  W-column picture takes W x k x 50 us (`ItemPreview.PassDuration`; Ivan: 100 columns, k=100, 1000 ms gives 2 passes).
  Video module devices take milliseconds per frame (`VideoPassDuration`; 40 is 25 frames a second). E-Pic is copied from
  PixelTuner 2's Repeat window: k / 5 ms per column (`EpicPassDuration`). The units are unchecked on devices: see
  `docs/before-release.md`. PixelTuner 2 defaults: 4 for pictures, 40 for video; its item windows allowed 1-400 and 20-800.
- The library (`ShowProject.Library`, `PixelTuner.Core/Model/Library.cs`): what can be put on timelines (pictures, GIFs, gradients,
  colours, effects). `Library.CanPlace` / `NewItem` are the rules and the defaults (4 for pictures, 40 ms for video); an entry that an
  item plays cannot be removed; old projects get entries from their items on open (`AddMissingEntries`). Pictures come in through
  `ImportViewModel` (the steps of PixelTuner 2's import window: cut, `PerspectiveWarp` (its mesh warp, ported and checked against the legacy
  `WarpGrid` in `WarpOracleTests`), trim, `ImageImport` scale to the device height and colour), the panel's logic is
  `LibraryViewModel` (`EditorViewModel.Gallery`; namespace `PixelTuner.UI.LibraryPanel`, because `Library` is the Core class). An entry (also the LightMaster's fixed ones) is dragged from the list onto a timeline (`LibraryDrag`, source in `EditorView.axaml.cs`, target `TimelineEditor.HoverEntry` / `DropEntry`: the item starts where it was dropped, or at the first free place after; a timeline that cannot play it shows why in red); `HeadlessDragTests` send the drag as the events the platform raises. The panel has a section for each kind of timeline (pixel poi/hoop, video, E-pic poi, LightMaster, as PixelTuner 2 had a pane for each): one Import button sorts a file by what it is (a GIF is a video; a picture is a pixel picture, or an E-pic gradient while an E-pic timeline is selected), shows its section and selects the new entry; the pixel section has a filter by picture height (`HeightFilterIndex`); from there a thing is put on a timeline; the section follows the selected timeline, and what that timeline cannot play is dimmed with the reason. The LightMaster section is the fixed `LibraryViewModel.LightEntries`: PixelTuner 2's effects pane as it was (names, black to red, the T1 and T2 each effect started with, and its own pictures in `Assets/LightEffects`, checked byte for byte against `legacy/PixelTuner2/PixelTuner/Effects`): nothing is added to or removed from it, and it is not in the project.
- The bottom panel of the editor (Library, Preview, Canvas, Devices) is a `TabControl` with its tabs in a column on the left (`Classes="rail"`); each tab keeps its buttons in a column of its own beside the content (`Button.tool`), so nothing takes the height from the content. Its height is the user's: a `GridSplitter` above it moves a fixed-height grid row, and switching tabs does not change it. A bar over it (the splitter, with the "Panels" button on it) folds the panel away (`IsBottomPanelVisible`); `EditorView.axaml.cs` keeps the height the user set and gives it back. A `GridSplitter` must be a direct child of the grid whose rows it resizes.
- The inspector (`EditorViewModel`, partials `.TimelineInspector.cs` and `.BinInfo.cs`): its name at the top is the button that folds it into a narrow strip (the whole strip brings it back, `IsInspectorVisible`); several items can be selected (`TimelineEditor.SelectedItems`;
  Ctrl/Cmd click toggles, Shift click selects a run) and "apply to the others" is one `Edits.ApplySettings` step; the ".bin" line and the
  warnings come from `BinInfo.Of` (it builds the file of the device that plays the timeline with `ShowBuilder` and reads it back with
  `BinReader`, so they are what the device will get); `BuildIssue` codes get their words in `EditorViewModel.IssueText`.
  `InspectorControlsToBinTests` has a test for every control: turn it, build, read the field, undo.
- LED markup (`LedLayout`, the layout of a video timeline; `PixelTuner.Core/Layout/LedMarkup.cs`, UI in `PixelTuner.UI/LedLayouts`): a port of
  PixelTuner 2's canvas element window (`legacy/.../ViewModel/Tools/LedMarkup.cs`, `LedMarkupEditor.cs`, `Controls/LedMarkupCanvas.cs`,
  `CanvasViewModel.cs`, `CanvasElementViewModel.cs`, which stay the reference: read them before changing anything here). The chain is the
  order of the LEDs on the strip (number = place, always 1..N), the model is immutable and every operation is insert/remove with the tail shifting;
  `tests/PixelTuner.Legacy.OracleTests/LedMarkupOracleTests` runs PixelTuner 2's own `LedMarkup` and `LedMarkupIssues` on random edits next to the
  port. `LedMarkupViewModel` is `LedMarkupEditor` (working copy, gestures as cells + keys, window-local undo of 300 steps, OK = one document edit
  through `Edits.AddLedLayout` / `SetLedMarkup`), `LedMarkupCanvas` only draws it and forwards the pointer. The drawing of the props is an SVG in the
  project's media (`ShowProject.CanvasResourceId`, set by `Edits.SetCanvas`; PixelTuner 2's two schemas are embedded in Core): `CanvasDrawing`
  (Core, no drawing platform) reads its outlines (id, path data, transforms), `SvgPath` flattens them for bounds and `CanvasDrawing.HitTest`; an
  outline and its layout are tied by the layout's `Name` = the outline's id. `CanvasPanelViewModel` (`EditorViewModel.Canvas`, the "Canvas" tab)
  imports an SVG or a schema, picks outlines, opens the window, copies and pastes markup (a static clipboard, as PixelTuner 2 kept it) and gives an
  outline's layout to a video timeline: the outline is dragged onto the timeline (`CanvasDrawingControl` starts the drag with `LayoutDrag.Format`,
  `TimelineControl` takes it and `TimelineEditor.HoverLayout` / `DropLayout` decide, one `SetLedLayout` step) or the button gives it to the selected one; the window shows the outline under the grid (cells stretched
  over it), `LedLayout.TilesMargin` (PixelTuner 2's tile margin, 0-100, only for looking at) as the space between tiles, and, for layouts imported
  from a .ptp, `LedLayout.SourceIssues` (gaps and repeats of the stored numbers, `LedNumberIssues`; OK compacts them). The chain of an imported
  layout is still ordered by number then cell, as the export did. A new project starts with the human schema as PixelTuner 2's did (set as the starting state in `MainViewModel.NewProjectAsync`, not as an edit, so it
  stays clean). Not ported: PixelTuner 2's canvas filled the outlines with the playing video (the preview, epic 6). A quarter
  turn also works on grids that are not square; SVG shapes other than paths, rect, circle, ellipse, line, polyline and polygon (text, `use`, clipping) are not read.
- The preview (`PixelTuner.Core/Preview/TimelinePreview`, UI in `PixelTuner.UI/PreviewPanel`, the "Preview" tab of `EditorViewModel.Preview`):
  it plays the .bin the build makes for the device of the selected timeline (`BinInfo.DeviceFor` + `ShowBuilder` + `BinReader`), so a pixel of
  the preview is the byte of the block (`TimelinePreviewTests`), at the speed of each kind of device (a column is k x 50 us on a poi or hoop, k ms on
  a video or fan module, k / 5 ms on E-Pic; a picture repeats from its first column until the next entry; pauses are dark entries) and at the playhead
  (the sound's position). `TimelinePreview.Trail` gives the afterglow snapshots. `PreviewViewModel` rebuilds per timeline when the project changes
  and gives `ColoursOn(layout)`, with which the canvas fills the outline a video plays on; `TimelinePreviewControl` only draws (poi tiled
  as swung, hoop ring on an orbit, video on the markup over its outline, colours). How a swing looks is a guess: `docs/before-release.md`.
- The devices tab (`PixelTuner.UI/DevicesPanel`, `EditorViewModel.Devices`; Studio only talks to hardware: `DeviceHub.Current` is set by the head and the search for devices is started by `App` when the app starts and stopped when it exits (`desktop.Exit`): there is no button, and a transfer is not in its way (the monitor skips a busy device, `DeviceManagerTests`); a Wi-Fi port that is taken is told by `IDeviceHub.Problem`,
  `DeviceManagerHub`, and is null on phones) works with physical devices only: there are no "devices of the project" in the UI (the model keeps `ShowProject.Devices` and the `Edits` for them for the
  .ptp import, the export and the tests, and the inspector's ".bin" line reads one if the project has it). What is sent to a connected device is set up in its `UploadPlanViewModel`
  (`DevicesPanelViewModel.SelectedPlan`, one per connected device, kept while it is connected, not part of the project): the timelines with ticks and file order (a video module takes a video's
  file or a fan's), built by `ShowBuilder` for the device as it reports itself (its LEDs and memory), with the size against the memory and what the build says; the build settings (colour depth, pause
  stretching, light step) are the project's, in the panel. Nothing is sent until the user presses Upload (`UploadCommand`), which sends block by block with progress and cancel and refuses before
  sending what the device cannot take (bootloader, `BuildIssue`s, memory); the line afterwards names the timelines sent. The panel has no way to save the file of a show: Studio does not hand out
  device binaries (`DevicesPanelViewModelTests` checks that no export command or file-picker method exists).
  The connected devices are `ConnectedDeviceViewModel`s that live as long as the device does: the hub's state (battery, busy) is read into the same object
  (`Refresh`, once a second from `Poll`; only a changed line raises anything), so the list keeps its selection and focus. Device settings (`DeviceSettingsViewModel`, the "Device settings" tab of the bottom panel, Studio only, apart from the project): PixelTuner 2's `DefaultsWindow` for the connected device that is
  chosen in the tab: the name on the network, Wi-Fi (network, password, this computer's address and port), the LED strip (LEDs on each channel, chip, colour order) and the menu and leader flag
  of a baton; `DeviceSettingsCatalog` (`PixelTuner.Devices`) holds its lists, limits and which device takes which group, `DeviceSetting` the bytes, `DeviceSettingsStore` what was written last
  (devices report only their name). Firmware: `PixelTuner.Devices/FirmwareCatalog` (PixelTuner 2's `Flash_*.bin` in `Firmware/`, embedded, checked
  byte for byte in `FirmwareCatalogTests`); `UpdateFirmwareCommand` confirms (`IDialogService.ConfirmAsync`), switches a device in its firmware to the bootloader and
  writes when the same family comes up there (`_afterBoot`), a device already in its bootloader is written at once, `UpdateFirmwareFromFileCommand` takes a .bin of the
  user's. Firmware cannot be cancelled. See `docs/before-release.md`. Memory units and the cancel behaviour: `docs/before-release.md`. `tests/PixelTuner.UI.Tests/FakeDevices.cs` is a firmware in memory.
- `src/PixelTuner.Audio` holds the player and decoder contracts only; implementations go per
  platform. `src/PixelTuner.Audio.Vlc` (LibVLC, LGPL, dynamic) serves Windows and macOS: the VideoLAN NuGet
  packages carry LibVLC under `libvlc/<rid>`, Linux uses the system one (`apt install libvlc5 vlc-plugin-base`).
  The shared UI sees only `IAudioEngine` (`AudioEngine.Current`, set by the head); phones get system players later.
  LibVLC rules learned the hard way: never call it from its own event threads (deadlock or crash), raise our
  events from a worker, and release a `Media` that failed to parse late (releasing it at once crashes the process).
  `PIXELTUNER_VLC_ARGS="--aout=dummy"` runs the app without a sound card; `PIXELTUNER_REQUIRE_VLC=1` makes the
  LibVLC tests fail instead of passing quietly when LibVLC is missing (CI sets it).
- `docs/before-release.md` lists what works today but must be redone before a release (start there when preparing
  one). Add to it whenever a stopgap goes in, and mark the code with `TODO(release)`.
- Merge PRs with a normal merge commit only.
- The cloud sandbox can build and test Core, Package, Sync, UI and Desktop on Linux
  (`apt install dotnet-sdk-10.0`, `SSL_CERT_FILE=/root/.ccr/ca-bundle.crt` for NuGet).
  Android needs the Android SDK (download is blocked in the sandbox) and iOS needs a Mac: rely on CI.
