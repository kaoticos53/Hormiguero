namespace AntSim.Core;

/// <summary>
/// All tuning knobs for one simulation run. Immutable value object; part of the
/// deterministic seed: same config + species + seed => identical trajectory.
/// </summary>
public sealed record WorldConfig
{
    // --- Arena ---
    public float Width { get; init; } = 800f;
    public float Height { get; init; } = 800f;
    public float CellSize { get; init; } = 8f;   // pheromone grid cell edge length

    // --- Time ---
    public float DeltaTime { get; init; } = 0.1f;   // simulated seconds per tick
    public int EpisodeTicks { get; init; } = 4000;  // 400 simulated seconds per episode

    // --- Colony ---
    public int WorkerCount { get; init; } = 150;

    // --- Food ---
    public int FoodSourceCount { get; init; } = 6;
    public float FoodPerSource { get; init; } = 60f;
    public float FoodPickupAmount { get; init; } = 1f;
    public float PickupRadius { get; init; } = 3f;
    public float MinFoodDistanceFromNest { get; init; } = 250f;
    public float MaxFoodDistanceFromNest { get; init; } = 350f;

    // --- Nest ---
    public float NestRadius { get; init; } = 20f;

    // --- Movement ---
    public float TurnRate { get; init; } = 6f;   // max radians per second (at Turn action = ±1)

    // --- Pheromones ---
    public int DiffusionInterval { get; init; } = 2;  // run diffusion every N ticks
}
