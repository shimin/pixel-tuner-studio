# legacy

Код предыдущих продуктов с полной историей. Сюда не добавляются новые функции.

| Папка | Что это | Откуда |
| --- | --- | --- |
| `PixelTuner2/` | PixelTuner 2.x (WPF, .NET Framework 4.0). Собирается как раньше: `PixelTuner2/PixelTuner.sln`, `make.bat` / `make64.bat`; приложение отдельно — `scripts\legacy\build-pixeltuner2.cmd` | `shimin/pixel-tuner2` |
| `PixelTuner2/Common/` | Бывший подмодуль Common | `shimin/Common` |
| `PixelTuner2/Communication/` | Бывший подмодуль Communication | `shimin/Communication` |
| `PixelTuner2/WpfTools/` | Бывший подмодуль WpfTools (NAudio, Workspace, Toolkit) | `shimin/WpfTools` |
| `EpicPlayer/` | EpicPlayer: WPF-версия и нативный порт для macOS на Swift (`epicplayer-mac/`) | `shimin/epicplayer` |

Подмодули заменены обычными папками: относительные пути в `PixelTuner.sln` не
изменились, поэтому PixelTuner 2 собирается из `legacy/PixelTuner2` без правок.

`Directory.Build.props` и `Directory.Packages.props` в этой папке отключают общие
настройки нового кода, чтобы старые проекты собирались так же, как раньше.

## Что отсюда берёт Pixel Tuner Studio

- `EpicPlayer/epicplayer-mac/Tests/Fixtures/*.json` — эталонные байтовые векторы
  протокола устройств и Art-Net. Их читают тесты `tests/PixelTuner.Sync.Tests`,
  Swift-тесты EpicPlayer и `WindowsReference`. Это общий контракт: при изменении
  протокола правится JSON, и все реализации проверяются по нему.
- `EpicPlayer/epicplayer-mac/plans/protocol.md` — спецификация протокола.
- Логика PixelTuner 2 (таймлайны, эффекты, экспорт `.bin`) переносится в `src/`
  по этапу 1 плана.
