@set Version="2.0.2.1"

call "c:\Program Files\Microsoft Visual Studio 10.0\VC\bin\vcvars32.bat"
msbuild ./SetAssemblyInfoTool/SetAssemblyInfoTool.csproj

SetAssemblyInfoTool.exe SetAssemblyInfoTool\..\ %Version%

msbuild ./BootstrapperNET4/BootstrapperNET4.wixproj
@rem msbuild ./PixelTuner/PixelTuner.sln /t:Clean
@rem msbuild ./PixelTuner/PixelTuner.sln /p:Configuration=Debug
