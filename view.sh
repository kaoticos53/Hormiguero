#!/usr/bin/env bash
# Build and run the AntSim real-time viewer (OpenTK/OpenGL window).
#
# Usage:
#   ./view.sh                                  # baseline brain, seed 1
#   ./view.sh seed=42 brain=output/run/champion.neat   # watch an evolved brain live
#   ./view.sh --release paused=1               # optimized build, start paused
set -euo pipefail
cd "$(dirname "$0")"

CONFIG="Debug"
ARGS=()
for arg in "$@"; do
    if [ "$arg" = "--release" ]; then
        CONFIG="Release"
    else
        ARGS+=("$arg")
    fi
done
if [ ${#ARGS[@]} -eq 0 ]; then
    ARGS=(seed=1)
fi

echo "==> Building AntSim.Viewer (${CONFIG})..."
dotnet build AntSim.slnx -c "$CONFIG" --nologo -v q

echo "==> Running: dotnet run -- ${ARGS[*]}"
exec dotnet run --project src/AntSim.Viewer -c "$CONFIG" --no-build -- "${ARGS[@]}"
