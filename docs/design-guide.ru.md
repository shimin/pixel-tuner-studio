# Словарь дизайна

[English version](design-guide.md)

Для всех (человека или небольшого агента), кто меняет внешний вид Pixel Tuner. В каждой строке сказано: **что видно**,
**что менять**, **где**. Делайте только то, что сказано в строке; не «улучшайте» соседние места.

Номера строк уплывают. У каждой строки есть **текст для поиска**: найдите его командой `grep -n "<текст>" <файл>` и меняйте эту строку.

## Правила

1. Таймлайны нарисованы не в XAML, а вручную, в одном файле: `src/PixelTuner.UI/Timelines/TimelineControl.cs` (ниже — **рисовалка**).
   Всё остальное (инспектор, библиотека, кнопки, стартовый экран) — XAML в `src/PixelTuner.UI/Views/*.axaml` и `src/PixelTuner.UI/Styles/Theme.axaml`.
2. Цвета и размеры записаны прямо в коде, так задумано. Меняйте значение на месте. Не добавляйте новые классы, настройки и файлы.
3. Размеры строк (высоты, ширина колонки имён) лежат **не** в рисовалке. Это константы в
   `src/PixelTuner.UI/Timelines/TimelineEditor.cs` (см. «Размеры» ниже). Мышь использует те же константы, поэтому никогда не вписывайте эти числа заново в рисовалку.
4. Положения текста в рисовалке — числа вроде `top + 10`. Если увеличили шрифт, проверьте, что текст по-прежнему помещается в строку.
5. У светлой и тёмной тем свои цвета (`Theme.axaml`). Цвет, вписанный в рисовалку, одинаков в обеих.
6. После правки: `dotnet build src/PixelTuner.UI` должен пройти, затем `dotnet test tests/PixelTuner.UI.Tests` должен пройти (ни один тест не проверяет цвет, они проверяют, что ничего не сломалось).
   Посмотреть результат: `dotnet run --project src/Heads/PixelTuner.Studio.Desktop`.

## Цвета, которые использует рисовалка

Все в `Render`, [TimelineControl.cs#L242-L248](../src/PixelTuner.UI/Timelines/TimelineControl.cs#L242).

| Имя в коде | Поиск | Где используется | По умолчанию |
| --- | --- | --- | --- |
| `surface` | `ThemeBrush("PtSurfaceBrush"` | фон страницы, нечётные строки | `PtSurfaceBrush` в `Theme.axaml` |
| `card` | `ThemeBrush("PtCardBrush", Colors.White);` | чётные строки, линейка, дорожка звука | `PtCardBrush` в `Theme.axaml` |
| `muted` | `ThemeBrush("PtMutedTextBrush"` | серый текст, линия линейки, риски | `PtMutedTextBrush` в `Theme.axaml` |
| `text` | `SystemControlForegroundBaseHighBrush` | основной текст (имена, подписи) | системный (чёрный или белый) |
| `accent` | `SystemControlHighlightAccentBrush` | рамки выделения, курсор, волна, выбранный маркер, наведение | **акцентный цвет системы пользователя** |
| `faint` | `var faint = new Pen` | тонкие разделительные линии | `muted` с 25 % непрозрачности |

**Сделать цвет фиксированным (не системным):** изменение второго аргумента `ThemeBrush(...)` ничего не даёт, он используется только если ресурс не найден.
Замените всю строку на кисть: `var accent = new SolidColorBrush(Color.Parse("#FF8A3D"));`. Так же для `text`.

**Сделать цвет зависящим от светлой и тёмной темы:** добавьте кисть с новым именем в **оба** словаря в `Theme.axaml`
(`<SolidColorBrush x:Key="PtSomethingBrush" Color="#..." />`) и читайте её в рисовалке через `ThemeBrush("PtSomethingBrush", Colors.Gray)`.

## Словарь: что менять

### Выделение

| Хочу изменить | Поиск в `TimelineControl.cs` | Что это |
| --- | --- | --- |
| Отметка **выбранного таймлайна**: та же серая затухающая рамка, что у элемента, вдоль всей строки (колонка имён и дорожка) | `ReferenceEquals(editor.Selected, editor.Project.Timelines[i])` в `Render`, рисует `DrawGlow` (общий с `DrawSelectionGlow`) | `GlowWidth`, `SelectionGrey`, прозрачность `0.9` в `DrawGlow` |
| Отметка **выбранного элемента** (картинки на дорожке): серая рамка, уходящая в прозрачность | `private void DrawSelectionGlow`; `GlowWidth` (4 px) и `SelectionGrey` над ним | цвет (светлый в тёмной теме, тёмный в светлой), начальная прозрачность `0.9`, ширина. Акцентного цвета нет. Место, куда ляжет перетаскиваемый элемент, обведено простой рамкой 2 px того же серого (`outline` в `DrawItem`) |
| Цвет **курсора воспроизведения** и его «шапки» | `private void DrawPlayhead`, строка `var pen = new Pen(accent, 2);` и `DrawGeometry(accent` | толщина `2`; размер шапки — числа `6` и `12` ниже |
| Цвета **маркеров** | `Colors.OrangeRed` в `DrawMarkers` | обычный маркер; выбранный рисуется цветом `accent` |
| Красная / зелёная подсказка при перетаскивании | `Color.FromRgb(209, 73, 91)` (нельзя, красный) и `Color.FromRgb(51, 170, 119)` (можно, зелёный) | красный есть в `DrawDenial` и в `DrawLayoutDrop`, зелёный только в `DrawLayoutDrop` |

### Заголовки (колонка имён слева)

| Хочу изменить | Поиск | Что это |
| --- | --- | --- |
| **Имя** таймлайна: размер, насыщенность, цвет | `timeline.Name, 14` | `Text(текст, размер, кисть, насыщенность)`: `14` — размер, `text` — цвет |
| **Строка типа** таймлайна («Pixel: poi, staff, hoop») | `KindLabel(timeline.Kind), 12` | `12` — размер, `muted` — цвет |
| Цветная **полоска** слева от заголовка | `new Rect(0, top, 4,` | `4` — её ширина; цвет — собственный цвет таймлайна (его выбирает пользователь в инспекторе) |
| **Фон строк** (два чередующихся оттенка, с оттенком по типу таймлайна, как в PixelTuner 2) | `class TimelineRows` в конце файла | пары RGB в `Dark(...)` — оригинальные значения PixelTuner 2 (чётное место, нечётное место), светлая тема считается из них в `Background(...)`: те же оттенки и три четверти расстояния от страницы (`LightWeight`). Линий между строками нет; оттенок зависит от места, поэтому меняется при перестановке таймлайнов |
| **Граница** под линейкой и разделитель колонки имён (он заканчивается там, где начинаются таймлайны) | строки `DrawLine(new Pen(muted, 1), new Point(0, TimelineEditor.RulerHeight)` и `DrawLine(faint, new Point(TimelineEditor.HeaderWidth, 0)` (последние строки `Render`) | цвет и толщина пера |
| **Угловые кнопки** (удалить, выше, ниже; слева от линейки) | `private void DrawCornerButtons` | `fill`, `border`, скругление `6`, размер значка `14`. Их размер и положение — `CornerButton*` в `TimelineEditor.cs` |
| Кнопка **Import sound** (заголовок дорожки звука) | `private void DrawWaveHeader` | тот же вид, что у кнопки «Add timeline» на тулбаре: собственные ресурсы кнопки Fluent (`ButtonBackground`, `ButtonBackgroundPointerOver`, `ButtonForeground`, `ButtonBorderBrush`), скругление `4` в `DrawRectangle(fill, border, button, 4, 4)`, размер текста `14` в `FormattedText`. Её размер и положение — `SoundtrackButton*` в `TimelineEditor.cs` |

### Дорожка звука (волна)

Два контура самого громкого отсчёта каждого канала: **левый** поднимается над средней линией (синий), **правый** опускается под неё (красный); каждый залит лёгким градиентом
от средней линии (слабее) к пикам (сильнее). Все числа и цвета — константы в
[WaveStyle.cs](../src/PixelTuner.UI/Timelines/WaveStyle.cs); рисуют `DrawWave` и `DrawChannel` в
[TimelineControl.cs#L448](../src/PixelTuner.UI/Timelines/TimelineControl.cs#L448).

| Хочу изменить | Поиск | Что это |
| --- | --- | --- |
| **Цвета каналов** (левый синий, правый красный; своя пара для каждой темы) | [WaveStyle.cs#L15](../src/PixelTuner.UI/Timelines/WaveStyle.cs#L15): `LeftDark`, `RightDark`, `LeftLight`, `RightLight` | значения RGB; тему `DrawWave` читает сам (`dark`) |
| **Толщина и прозрачность контура** | [WaveStyle.cs#L21](../src/PixelTuner.UI/Timelines/WaveStyle.cs#L21): `LineWidth`, `LineOpacity`; перо — `WaveStyle.Outline` | пиксели; от 0 до 1 |
| **Градиент** заливки | [WaveStyle.cs#L27](../src/PixelTuner.UI/Timelines/WaveStyle.cs#L27): `FillOpacityAtCentre`, `FillOpacityAtFullScale`; кисть — `WaveStyle.Fill` | непрозрачность на средней линии и на пике полной громкости; градиент идёт по всей половине дорожки, поэтому более высокий пик залит сильнее. Чтобы изменить направление или добавить остановку, правьте `Fill` |
| **Высота** волны и поля сверху и снизу | `WaveHeight` в `TimelineEditor.cs`; поле — `WaveStyle.Margin` | самый громкий отсчёт стоит на `WaveHeight / 2 - Margin` от средней линии |
| **Фон** дорожки и средняя линия | `FillRectangle(card, lane)` и `DrawLine(faint` под ней в `DrawWave` | цвет карточки и тонкое серое перо остальных линий |
| Где контур **ломается** | две ветки в `DrawWave` | каждый пиксельный столбец (самый громкий отсчёт под ним), а при увеличении больше двух пикселей на точку — каждая точка волны |

Каналы приходят из декодера: `src/PixelTuner.Audio.Vlc/VlcAudioDecoder.cs` делает стерео-WAV (моно удваивается, поэтому контуры одинаковы),
`WaveformBuilder` хранит самый громкий отсчёт первого (левого) и второго (правого) канала на точку (`Waveform.Left`, `Waveform.Right`, `Waveform.Peaks`).

### Элементы на дорожке (картинки, GIF, цвета, эффекты)

| Хочу изменить | Поиск | Что это |
| --- | --- | --- |
| **Заливка** элемента и его тонкая **рамка** | `new SolidColorBrush(fill, opacity * 0.85)` в `DrawItem` | заливка — цвет таймлайна с 85 % непрозрачности, на пиксель короче края; рамка — полупрозрачный серый 1 px `new Pen(new SolidColorBrush(EdgeGrey, 0.4), 1)` (светлый в тёмной теме, тёмный в светлой; без цвета) |
| **Скругление углов** элемента | `4, 4` в конце этого `DrawRectangle` | |
| **Подпись** элемента (имя файла): размер, цвет | `ItemLabel(item), 12` | `12` — размер; цвет чёрный или белый по `Contrast(fill)` |
| Тёмная **плашка** под подписью | `Colors.Black, 0.55` | чёрный с 55 % непрозрачности |
| **Паузы** между элементами (чёрные) | `Color.FromRgb(8, 8, 10)` в `DrawPauses` | |

### Линейка и сетка

| Хочу изменить | Поиск | Что это |
| --- | --- | --- |
| **Числа времени** на линейке: размер, цвет | `step), 11, muted` | `11` — размер |
| Риски и линии сетки | `major ? line : faint` в `DrawRulerAndGrid` | `line` — `muted` толщиной 1 px, `faint` — 25 % |
| Размер **имени маркера** | `marker.Name, 11` | |

### Всё, что не таймлайны (XAML)

| Хочу изменить | Файл и поиск | Что это |
| --- | --- | --- |
| Цвета **страницы**, **карточек**, **серого текста** (светлая и тёмная темы) | `src/PixelTuner.UI/Styles/Theme.axaml`, `PtSurfaceBrush`, `PtCardBrush`, `PtMutedTextBrush` | по одному значению на тему. **Они же меняют таймлайны**, потому что те читают те же кисти |
| Вид **карточки** (инспектор, библиотека, стартовый экран) | `Theme.axaml`, `Border.card`; радиус `PtCardRadius` | фон, радиус, отступы |
| Большой **заголовок** («Pixel Tuner Studio») | `Theme.axaml`, `TextBlock.h1` | `FontSize 28`, насыщенность |
| Класс серого текста | `Theme.axaml`, `TextBlock.muted` | цвет |
| Поля страницы и промежутки | `Theme.axaml`, `PtPagePadding`, `PtSpacing` | |
| **Заголовок** инспектора | `src/PixelTuner.UI/Views/EditorView.axaml`, `FontSize="16"` (`InspectorTitle`) | |
| **Вкладки** нижней панели (Library, Preview, Canvas, Devices: колонка слева) | `Theme.axaml`, `TabControl.rail TabItem`; сам контрол — `<TabControl Classes="rail" TabStripPlacement="Left"` в `EditorView.axaml` | размер шрифта, высота, отступы вкладки |
| **Кнопки инструментов** в колонке слева от содержимого вкладки (Import, Put on timeline, ...) | `Theme.axaml`, `Button.tool`; ширина колонки — `ColumnDefinitions="190,*"` в `EditorView.axaml`, `CanvasPanelView.axaml`, `PreviewPanelView.axaml` | отступы, высота, размер шрифта |
| **Высота нижней панели** (пользователь тянет разделитель над ней) | `EditorView.axaml`, `RowDefinitions="Auto,Auto,*,Auto,320"`: `320` — стартовая высота | высота не меняется при смене вкладок |
| Мелкий серый текст в библиотеке | `EditorView.axaml`, `Classes="muted" FontSize="11"` | |
| **Слайдеры** (зум, превью, импорт картинки): таблетка с круглым бегунком внутри | `Theme.axaml`: контрол-тема `PtSliderTheme` (размеры: таблетка высотой 20, радиус скругления 9 = радиусу бегунка, бегунок 18 с отступом 1 px, в коробке 20, правая половина которой цвета заливки: заливка идёт под кружок и кончается его правым краем) и кисти `Pt*Slider*` в обоих словарях тем (дорожка, дорожка при наведении, заливка левее бегунка, бегунок, бегунок при наведении, бегунок зажатый); цвета при наведении и нажатии — стили `Slider:pointerover` и `Thumb#thumb:pressed` ниже | монохромный: меняйте серые цвета по темам. Стиль не может заменить шаблон контрола Fluent: шаблон заменяется через `ControlTheme` и `Theme=` |
| **Кнопки** (форма, акцентная кнопка) | `src/PixelTuner.UI/App.axaml`, `<FluentTheme />` | вид берётся из Fluent. Акцентная кнопка (`Classes="accent"`) использует системный акцентный цвет. Чтобы поменять цвет всех стандартных контролов Fluent, дайте `FluentTheme` палитру с `Accent` (документация Avalonia: `FluentTheme.Palettes`). Здесь не проверялось: соберите и посмотрите |
| Панель **Preview** (точки светодиодов, фон) | `src/PixelTuner.UI/PreviewPanel/TimelinePreviewControl.cs`, `Dark`, `Hairline`, `Marker` в начале файла | |
| Контуры на панели **Canvas** | `src/PixelTuner.UI/LedLayouts/CanvasDrawingControl.cs`, `Page`, `Unmarked`, `Marked`, `Stroke`, `SelectedStroke` в начале файла | |

## Размеры (одно место для всех строк)

`src/PixelTuner.UI/Timelines/TimelineEditor.cs`, начало класса:

| Константа | По умолчанию | Что означает |
| --- | --- | --- |
| `HeaderWidth` | 170 | ширина колонки имён |
| `RulerHeight` | 34 | высота линейки |
| `WaveHeight` | 84 | высота дорожки звука |
| `TrackHeight` | 64 | высота одной строки таймлайна |
| `SoundtrackButtonInset`, `SoundtrackButtonHeight` | 6 (= `CornerButtonGap`), 40 | кнопка Import sound |
| `CornerButtonGap` | 6 | кнопки удаления и перестановки заполняют ячейку слева от линейки; это отступ вокруг них и между ними (ширина и высота следуют из него) |

Если текст заголовка стал больше, чем позволяет строка, увеличьте здесь `TrackHeight` и сдвиньте вторую строку текста (`top + 32`) в рисовалке.

## Нельзя

- Не переносите код рисования в `TimelineEditor.cs` (он ничего не знает о рисовании, и тесты на это опираются).
- Не меняйте `HitTest` и константы выше «просто чтобы выглядело иначе»: мышь и картинка должны оставаться согласованными.
- Не трогайте имена кистей `Pt*`: рисовалка ищет их по имени.
