#!/usr/bin/env bash
# Build and run AntSim.Headless (works in Git Bash / Linux / macOS).
#
# Usage:
#   ./run.sh                              # build + demo episode
#   ./run.sh bench runs=3 ticks=4000      # build + benchmark
#   ./run.sh evolve gens=50 out=output/x  # build + evolution run
#   ./run.sh evaluate path=output/x/champion.neat
#   ./run.sh --release evolve gens=100    # optimized build (recommended for long runs)
#
# Any arguments are passed straight to the CLI; with no arguments it runs a demo.
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
    ARGS=(demo)
fi

echo "==> Building AntSim (${CONFIG})..."
dotnet build AntSim.slnx -c "$CONFIG" --nologo -v q

echo "==> Running: dotnet run -- ${ARGS[*]}"
exec dotnet run --project src/AntSim.Headless -c "$CONFIG" --no-build -- "${ARGS[@]}"
