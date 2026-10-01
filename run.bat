@echo off
rem Build and run AntSim.Headless on Windows (double-clickable, or run from cmd).
rem
rem Usage:
rem   run.bat                                  build + demo episode
rem   run.bat bench runs=3 ticks=4000          build + benchmark
rem   run.bat evolve gens=50 out=output\x      build + evolution run
rem   run.bat evaluate path=output\x\champion.neat
rem   run.bat --release evolve gens=100        optimized build (recommended for long runs)
rem
rem Note: arguments are forwarded verbatim via %%* because cmd would otherwise
rem split key=value options at the '=' sign.
setlocal enabledelayedexpansion

set CONFIG=Debug
set "ARGS=%*"
if not "%ARGS%"=="" (
    echo %ARGS% | find /i "--release" >nul && set CONFIG=Release
    set "ARGS=!ARGS:--release=!"
)
if "%ARGS%"=="" set ARGS=demo

echo ==^> Building AntSim (%CONFIG%)...
dotnet build AntSim.slnx -c %CONFIG% --nologo -v q
if errorlevel 1 (
    echo Build FAILED.
    exit /b 1
)

echo ==^> Running: dotnet run -- %ARGS%
dotnet run --project src/AntSim.Headless -c %CONFIG% --no-build -- %ARGS%
