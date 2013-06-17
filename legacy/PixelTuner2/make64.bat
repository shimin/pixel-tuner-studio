@set Version="2.0.4.11"

call "c:\Program Files (x86)\Microsoft Visual Studio 10.0\VC\bin\vcvars32.bat"
msbuild ./SetAssemblyInfoTool/SetAssemblyInfoTool.csproj

SetAssemblyInfoTool.exe SetAssemblyInfoTool\..\ %Version%

SetAssemblyInfoTool.exe SetAssemblyInfoTool\..\ %Version% autoupdaterconfig

msbuild ./BootstrapperNET4/BootstrapperNET4.wixproj
@rem msbuild ./PixelTuner/PixelTuner.sln /t:Clean
@rem msbuild ./PixelTuner/PixelTuner.sln /p:Configuration=Debug
