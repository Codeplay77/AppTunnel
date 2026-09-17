@echo off
setlocal

cd /d "%~dp0"

dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true
if errorlevel 1 (
    echo.
    echo Build falhou.
    pause
    exit /b 1
)

echo.
echo Build concluido: bin\x64\Release\net8.0-windows\win-x64\publish\
pause
