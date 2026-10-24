@echo off
rem Builds PixelTuner 2 (legacy\PixelTuner2: WPF, .NET Framework 4) the way it always was built: with MSBuild of Visual Studio.
rem It is not a part of the default solution and nothing of the new code depends on this build.
rem   scripts\legacy\build-pixeltuner2.cmd            the application, Release, into legacy\PixelTuner2\PixelTuner\bin\Release
rem The installer (WiX) is made by legacy\PixelTuner2\make.bat / make64.bat, as before.
setlocal
set "ROOT=%~dp0..\.."
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"

set "MSBUILD="
if exist "%VSWHERE%" (
  for /f "usebackq delims=" %%i in (`"%VSWHERE%" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do set "MSBUILD=%%i"
)
if not defined MSBUILD (
  echo MSBuild of Visual Studio was not found. PixelTuner 2 needs Visual Studio with the .NET desktop workload and the .NET Framework 4 targeting pack.
  exit /b 1
)

"%MSBUILD%" "%ROOT%\legacy\PixelTuner2\PixelTuner\PixelTuner.csproj" /restore /p:Configuration=Release /p:Platform=AnyCPU /m
