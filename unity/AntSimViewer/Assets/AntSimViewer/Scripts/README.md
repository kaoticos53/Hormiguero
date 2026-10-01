# AntSim Unity viewer scripts

- `AntSimViewerController` loads `champion.json` / `replay.json`, drives live simulation, input, and HUD.
- `ViewerRenderer` draws the toroidal world with Unity's built-in GL API: pheromone cells, food, nest, and ants.
- `ReplayPlayer` interpolates replay positions and correctly crosses torus seams.
- `Sim/*` is the Unity-side live simulation port; `Neural/*` evaluates the exported cyclic NEAT brain.
- `Data/*` contains `JsonUtility`-compatible DTOs matching the PascalCase .NET export.
- `ViewerBootstrap` makes an empty scene work automatically.

The viewer intentionally has no third-party Unity package dependency. It uses the built-in renderer,
so it is suitable as a starting point for replacing the immediate-mode renderer with prefabs, VFX Graph,
or a DOTS/Burst implementation later.
