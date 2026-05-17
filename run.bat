@echo off
setlocal

REM Run WPFToolbarTree. Pass "release" as the first arg to build/run Release.
set CONFIG=Debug
if /I "%~1"=="release" set CONFIG=Release

pushd "%~dp0"

dotnet build -c %CONFIG% -nologo --verbosity minimal
if errorlevel 1 (
    popd
    echo Build failed.
    exit /b 1
)

start "" "bin\%CONFIG%\net8.0-windows\WPFToolbarTree.exe"

popd
endlocal
