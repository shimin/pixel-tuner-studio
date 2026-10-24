# Design dictionary

[Русская версия](design-guide.ru.md)

For anyone (a person or a small agent) who changes how Pixel Tuner looks. Each line says: **what you see**,
**what to change**, **where**. Do only what the line says; do not "improve" the neighbours.

The line numbers drift. Every row has a **search** text: find it with `grep -n "<text>" <file>` and change that line.

## Rules

1. The timelines are not XAML. They are drawn by hand in one file: `src/PixelTuner.UI/Timelines/TimelineControl.cs` (called **the control** below).
   Everything else (inspector, library, buttons, start screen) is XAML in `src/PixelTuner.UI/Views/*.axaml` and `src/PixelTuner.UI/Styles/Theme.axaml`.
2. Colours and sizes are written in the code on purpose. Change the value in place. Do not add new classes, settings or files.
3. Sizes of the rows (heights, header width) are **not** in the control. They are constants in
   `src/PixelTuner.UI/Timelines/TimelineEditor.cs` (see "Sizes" below). The mouse uses the same constants, so never type the numbers again in the control.
4. Text positions in the control are numbers like `top + 10`. If you make a font bigger, check that the text still fits its row.
5. Light and dark themes have their own colours (`Theme.axaml`). A colour you type in the control is the same in both.
6. After a change: `dotnet build src/PixelTuner.UI` must pass, then `dotnet test tests/PixelTuner.UI.Tests` must pass (no test checks a colour, they check that nothing broke).
   To look at the result: `dotnet run --project src/Heads/PixelTuner.Studio.Desktop`.

## The colours the control uses

All in `Render`, [TimelineControl.cs#L242-L248](../src/PixelTuner.UI/Timelines/TimelineControl.cs#L242).

| Name in code | Search | Where it is used | Default |
| --- | --- | --- | --- |
| `surface` | `ThemeBrush("PtSurfaceBrush"` | page background, odd rows | `PtSurfaceBrush` in `Theme.axaml` |
| `card` | `ThemeBrush("PtCardBrush", Colors.White);` | even rows, ruler, sound lane | `PtCardBrush` in `Theme.axaml` |
| `muted` | `ThemeBrush("PtMutedTextBrush"` | grey text, ruler line, tick marks | `PtMutedTextBrush` in `Theme.axaml` |
| `text` | `SystemControlForegroundBaseHighBrush` | main text (names, labels) | system (black or white) |
| `accent` | `SystemControlHighlightAccentBrush` | selection frames, playhead, waveform, selected marker, hover | **the accent colour of the user's system** |
| `faint` | `var faint = new Pen` | thin separator lines | `muted` at 25 % |

**To make a colour fixed (not the system one):** changing the second argument of `ThemeBrush(...)` does nothing, it is only used when the resource is missing.
Replace the whole line with a brush: `var accent = new SolidColorBrush(Color.Parse("#FF8A3D"));`. The same for `text`.

**To make a colour follow light/dark:** add a brush with a new name to **both** dictionaries in `Theme.axaml`
(`<SolidColorBrush x:Key="PtSomethingBrush" Color="#..." />`) and read it with `ThemeBrush("PtSomethingBrush", Colors.Gray)` in the control.

## Dictionary: what to change

### Selection

| I want to change | Search in `TimelineControl.cs` | What it is |
| --- | --- | --- |
| Frame around a **selected timeline** (header) | `ReferenceEquals(editor.Selected, timeline)` | `new Pen(accent, 2)`: colour and thickness (the `2`) |
| Frame around a **selected item** (picture on a lane) | `new Pen(accent, 2) : new Pen(new SolidColorBrush(fill), 1)` in `DrawItem` | same: colour and thickness |
| Colour of the **playhead** (the cursor) and its cap | `private void DrawPlayhead`, line `var pen = new Pen(accent, 2);` and `DrawGeometry(accent` | thickness `2`; cap size is the `6` and `12` below it |
| **Marker** colours | `Colors.OrangeRed` in `DrawMarkers` | normal marker; the selected one uses `accent` |
| Red / green hint while dragging | `Color.FromRgb(209, 73, 91)` (denied, red) and `Color.FromRgb(51, 170, 119)` (allowed, green) | red is in `DrawDenial` and in `DrawLayoutDrop`, green only in `DrawLayoutDrop` |

### Headers (the names column on the left)

| I want to change | Search | What it is |
| --- | --- | --- |
| Timeline **name**: size, weight, colour | `timeline.Name, 14` | `Text(text, size, brush, weight)`: `14` is the size, `text` the colour |
| Timeline **kind line** ("Pixel: poi, staff, hoop") | `KindLabel(timeline.Kind), 12` | `12` is the size, `muted` the colour |
| Coloured **strip** on the left of a header | `new Rect(0, top, 4,` | the `4` is its width; its colour is the timeline's own colour (the user picks it in the inspector) |
| **Row background** (stripes) | `i % 2 == 0 ? card : surface` | swap the two names, or use one brush for all rows |
| **Line between rows** | `DrawLine(faint, new Point(0, top + TimelineEditor.TrackHeight)` | pen: `faint` |
| **Border** under the ruler and between header and lanes | lines `DrawLine(new Pen(muted, 1), new Point(0, TimelineEditor.RulerHeight)` and `DrawLine(faint, new Point(TimelineEditor.HeaderWidth, 0)` (last lines of `Render`) | pen colour and thickness |
| **Import sound** button (sound lane header) | `private void DrawWaveHeader` | `fill`, `border`, corner `8` in `DrawRectangle(fill, border, button, 8, 8)`, text size `13` in the `FormattedText`. Its size and place are `SoundtrackButton*` in `TimelineEditor.cs` |

### Sound lane (waveform)

| I want to change | Search | What it is |
| --- | --- | --- |
| **Waveform colour** | `var pen = new Pen(new SolidColorBrush(((ISolidColorBrush)accent)` in `DrawWave` | `accent` at 75 %: change the brush or the `0.75` |
| Waveform **height** | `amplitude = height / 2 - 6` | the `6` is the margin; the lane height is `WaveHeight` in `TimelineEditor.cs` |
| Lane **background** and centre line | `FillRectangle(card, new Rect(timeArea.X, top, timeArea.Width, height))` and the `DrawLine(faint` under it | |

**There is no left and right channel.** The waveform is one outline of both channels mixed together (`src/PixelTuner.Audio/Waveform.cs`,
"all channels mixed"), so there is one colour only. Two colours for L and R is not a styling task: the decoder
(`src/PixelTuner.Audio.Vlc/VlcAudioDecoder.cs`), `Waveform` and its cache format must carry two outlines first. Do not fake it in the control.

### Items on a lane (pictures, GIFs, colours, effects)

| I want to change | Search | What it is |
| --- | --- | --- |
| Item **fill** and its thin **border** | `new SolidColorBrush(fill, opacity * 0.85)` in `DrawItem` | fill = timeline colour at 85 %; border = `new Pen(new SolidColorBrush(fill), 1)` |
| Item **corner radius** | the `4, 4` at the end of that `DrawRectangle` | |
| Item **label** (file name): size, colour | `ItemLabel(item), 12` | `12` is the size; colour is black or white by `Contrast(fill)` |
| Dark **plate** behind the label | `Colors.Black, 0.55` | 55 % black |
| **Pause** stretches between items (black) | `Color.FromRgb(8, 8, 10)` in `DrawPauses` | |

### Ruler and grid

| I want to change | Search | What it is |
| --- | --- | --- |
| **Time numbers** on the ruler: size, colour | `step), 11, muted` | `11` is the size |
| Tick marks and grid lines | `major ? line : faint` in `DrawRulerAndGrid` | `line` = `muted` 1 px, `faint` = 25 % |
| Marker **name** size | `marker.Name, 11` | |

### Everything that is not the timelines (XAML)

| I want to change | File and search | What it is |
| --- | --- | --- |
| Colours of the **page**, **cards**, **grey text** (light and dark) | `src/PixelTuner.UI/Styles/Theme.axaml`, `PtSurfaceBrush`, `PtCardBrush`, `PtMutedTextBrush` | one value per theme. **These also change the timelines**, they read the same brushes |
| **Card** look (inspector, library, start screen) | `Theme.axaml`, `Border.card`; radius `PtCardRadius` | background, radius, padding |
| Big **heading** ("Pixel Tuner Studio") | `Theme.axaml`, `TextBlock.h1` | `FontSize 28`, weight |
| Grey text class | `Theme.axaml`, `TextBlock.muted` | colour |
| Page margins and spacing | `Theme.axaml`, `PtPagePadding`, `PtSpacing` | |
| Inspector / library **titles** | `src/PixelTuner.UI/Views/EditorView.axaml`, `FontSize="16"` (two places: `InspectorTitle` and `Library_Title`) | |
| Small grey text in the library | `EditorView.axaml`, `Classes="muted" FontSize="11"` | |
| **Buttons** (shape, accent button) | `src/PixelTuner.UI/App.axaml`, `<FluentTheme />` | the look comes from Fluent. The accent button (`Classes="accent"`) takes the system accent colour. Changing the colour of all Fluent controls: give `FluentTheme` a palette with `Accent` (Avalonia docs: `FluentTheme.Palettes`). Not tried here: build and look |
| **Preview** panel (LED dots, background) | `src/PixelTuner.UI/PreviewPanel/TimelinePreviewControl.cs`, `Dark`, `Hairline`, `Marker` at the top | |
| **Canvas** panel outlines | `src/PixelTuner.UI/LedLayouts/CanvasDrawingControl.cs`, `Page`, `Unmarked`, `Marked`, `Stroke`, `SelectedStroke` at the top | |

## Sizes (one place for all rows)

`src/PixelTuner.UI/Timelines/TimelineEditor.cs`, top of the class:

| Constant | Default | Meaning |
| --- | --- | --- |
| `HeaderWidth` | 170 | width of the names column |
| `RulerHeight` | 34 | height of the ruler |
| `WaveHeight` | 84 | height of the sound lane |
| `TrackHeight` | 64 | height of one timeline row |
| `SoundtrackButtonInset`, `SoundtrackButtonHeight` | 12, 40 | the Import sound button |

If you make a header text bigger than the row allows, raise `TrackHeight` here, and move the second text line (`top + 32`) in the control.

## Do not

- Do not move drawing code into `TimelineEditor.cs` (it knows nothing about drawing, and tests rely on that).
- Do not change `HitTest` or the constants above "just to look different": the mouse and the picture must stay in step.
- Do not touch the `Pt*` brush names: the control looks them up by name.
