Выпуск новой версии PixelTuner
==============================

Установщик сделан по схеме Windows Installer "major upgrade": новая версия
при установке сама удаляет любую установленную PixelTuner и ставится на её
место (в ту же папку, путь берётся из HKLM\SOFTWARE\e-pic\PixelTuner 2).

Что менять при выпуске
----------------------
1. Номер версии в make.bat / make64.bat (@set Version="X.Y.Z.W").
   SetAssemblyInfoTool проставит его во все AssemblyInfo.cs
   (AssemblyVersion/AssemblyFileVersion) и в ProductVersion в *.wxs.
   Можно поменять и руками, но тогда во всех этих местах.
2. Больше ничего менять не нужно:
   - ProductCode генерируется заново при каждой сборке (Product Id="*").
   - PackageCode тоже "*".
   - Версия бандла (PixelTuner setup exe) берётся из версии MSI
     (Version="!(bind.packageVersion.PixelTunerPackage)" в Bundle.wxs).

Что НЕ менять никогда
---------------------
- UpgradeCode {df1086f7-6803-4697-b66c-74dd2733c092} в Product.wxs и Bundle.wxs.
  По нему новая версия находит и удаляет старую. Сменишь - получишь две
  установки рядом.
- Guid компонентов в Product.wxs.
- InstallScope="perMachine".

Особенности
-----------
- Windows Installer сравнивает только первые три числа версии: 2.0.8.4 и 2.0.8.8
  для него одинаковые. Замена всё равно работает, потому что MajorUpgrade
  стоит с AllowDowngrades="yes" и удаляет установленный продукт любой версии.
  Обратная сторона: установка более старой версии тоже молча заменит новую.
- Если PixelTuner запущен во время обновления, установщик покажет окно
  "файлы используются" и предложит закрыть программу.
- Настройки пользователя (раскладка окон в %AppData%\PixelTuner 2, ключи HKCU)
  при обновлении сохраняются.

Патчи (.msp) не используются: для обычных обновлений нужен только major upgrade.
Справка WiX: https://wixtoolset.org/docs/v3/howtos/updates/major_upgrade/
