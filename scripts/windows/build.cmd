@echo off
rem Builds Pixel Tuner Studio for Windows: a self-contained folder (no .NET needed on the machine) in out\<rid>.
rem   scripts\windows\build.cmd              win-x64, Release
rem   scripts\windows\build.cmd win-arm64    another runtime
rem   scripts\windows\build.cmd win-x64 test run the tests first
rem Needs the .NET SDK from global.json. PixelTuner 2 is not built here: see scripts\legacy.
setlocal
cd /d "%~dp0..\.."

set "RID=%~1"
if "%RID%"=="" set "RID=win-x64"

if /i "%~2"=="test" (
  dotnet test PixelTunerStudio.slnx -c Release
  if errorlevel 1 exit /b 1
)

dotnet publish src\Heads\PixelTuner.Studio.Desktop -c Release -r %RID% --self-contained -o out\%RID%
if errorlevel 1 exit /b 1

echo.
echo Created %CD%\out\%RID%\PixelTunerStudio.exe
