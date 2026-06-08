@echo off
setlocal

set "PROJECT_DIR=%~dp0"
set "EXE_PATH=%PROJECT_DIR%bin\Debug\net8.0-windows\WPFToolbarTree.exe"

echo === Killing running WPFToolbarTree ===
taskkill /F /IM WPFToolbarTree.exe >nul 2>&1
if %ERRORLEVEL%==0 (
    echo Killed existing instance.
) else (
    echo No running instance found.
)

echo.
echo === Building ===
dotnet build "%PROJECT_DIR%WPFToolbarTree.csproj" -nologo -v minimal
if errorlevel 1 (
    echo.
    echo Build FAILED.
    exit /b 1
)

echo.
echo === Launching ===
if not exist "%EXE_PATH%" (
    echo Executable not found at "%EXE_PATH%".
    exit /b 1
)
start "" "%EXE_PATH%"
echo Started.

endlocal
