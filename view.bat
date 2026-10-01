@echo off
rem Build and run the AntSim real-time viewer (OpenTK/OpenGL window).
rem
rem Usage:
rem   view.bat                                       baseline brain, seed 1
rem   view.bat seed=42 brain=output\run\champion.neat   watch an evolved brain live
rem   view.bat --release paused=1                    optimized build, start paused
rem
rem Arguments are forwarded verbatim via %%* (cmd would split key=value at '=').
setlocal enabledelayedexpansion

set CONFIG=Debug
set "ARGS=%*"
if not "%ARGS%"=="" (
    echo %ARGS% | find /i "--release" >nul && set CONFIG=Release
    set "ARGS=!ARGS:--release=!"
)
if "%ARGS%"=="" set ARGS=seed=1

echo ==^> Building AntSim.Viewer (%CONFIG%)...
dotnet build AntSim.slnx -c %CONFIG% --nologo -v q
if errorlevel 1 (
    echo Build FAILED.
    exit /b 1
)

echo ==^> Running: dotnet run -- %ARGS%
dotnet run --project src/AntSim.Viewer -c %CONFIG% --no-build -- %ARGS%
