namespace AntSim.Core;

/// <summary>
/// Definition of one pheromone channel: evaporation, diffusion and saturation behaviour.
/// Channels are species data — different species smell and lay different chemicals.
/// </summary>
public sealed record PheromoneChannelDef
{
    public required string Name { get; init; }

    /// <summary>Fraction of level lost per simulated second (exponential decay).</summary>
    public float EvaporationPerSecond { get; init; } = 0.02f;

    /// <summary>Rate of the 4-neighbour diffusion mix, applied every <see cref="WorldConfig.DiffusionInterval"/> ticks. Range [0, 0.25].</summary>
    public float DiffusionRate { get; init; } = 0.1f;

    /// <summary>Per-cell saturation cap.</summary>
    public float MaxLevel { get; init; } = 30f;
}

/// <summary>
/// Species definition. Species are data, not code: pheromone set, baseline brain and
/// behavioural description all live here. Black garden ant ships first; the other three
/// are staged definitions for later phases (their mechanics are not implemented yet).
/// </summary>
public sealed record SpeciesDefinition
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public string Description { get; init; } = "";

    public required IReadOnlyList<PheromoneChannelDef> PheromoneChannels { get; init; }

    /// <summary>Factory for the hand-coded baseline brains (the level NEAT must beat).</summary>
    public required IAntBrainFactory BaselineBrainFactory { get; init; }

    /// <summary>False for roadmap species whose special mechanics (fungus gardens, swarm raids, stings) are not built yet.</summary>
    public bool Implemented { get; init; } = true;
}

/// <summary>Creates per-ant brain instances for a colony.</summary>
public interface IAntBrainFactory
{
    IAntBrain CreateBrain(Caste caste, int antId, Prng prng);
}

public static class SpeciesCatalog
{
    public static readonly PheromoneChannelDef FoodTrail = new()
    {
        Name = "FoodTrail",
        EvaporationPerSecond = 0.02f,
        DiffusionRate = 0.12f,
        MaxLevel = 30f,
    };

    public static readonly PheromoneChannelDef HomeTrail = new()
    {
        Name = "HomeTrail",
        EvaporationPerSecond = 0.035f,
        DiffusionRate = 0.12f,
        MaxLevel = 15f,
    };

    public static readonly PheromoneChannelDef Alarm = new()
    {
        Name = "Alarm",
        EvaporationPerSecond = 0.25f,
        DiffusionRate = 0.25f,
        MaxLevel = 10f,
    };

    /// <summary>The MVP validation species: generalist trail forager.</summary>
    public static SpeciesDefinition BlackGardenAnt { get; } = new()
    {
        Id = "black-garden-ant",
        DisplayName = "Black garden ant (Lasius niger)",
        Description = "Generalist trail forager. Forms food trails between nest and finds; validation case for the MVP.",
        PheromoneChannels = [FoodTrail, HomeTrail],
        BaselineBrainFactory = new HandcodedForagerFactory(),
    };

    /// <summary>Roadmap species — definition staged, mechanics arrive in later phases.</summary>
    public static SpeciesDefinition FireAnt { get; } = new()
    {
        Id = "fire-ant",
        DisplayName = "Red imported fire ant (Solenopsis invicta)",
        Description = "Aggressive stinging species; recruits via alarm pheromone. Requires combat/sting mechanics (phase 4+).",
        PheromoneChannels = [FoodTrail, HomeTrail, Alarm],
        BaselineBrainFactory = new HandcodedForagerFactory(),
        Implemented = false,
    };

    public static SpeciesDefinition ArmyAnt { get; } = new()
    {
        Id = "army-ant",
        DisplayName = "Army ant (Eciton burchellii)",
        Description = "Nomadic swarm raider with no permanent nest. Requires bivouac/raid mechanics (phase 4+).",
        PheromoneChannels = [FoodTrail, Alarm],
        BaselineBrainFactory = new HandcodedForagerFactory(),
        Implemented = false,
    };

    public static SpeciesDefinition LeafcutterAnt { get; } = new()
    {
        Id = "leafcutter-ant",
        DisplayName = "Leafcutter ant (Atta cephalotes)",
        Description = "Harvests leaves to feed fungus gardens inside the nest. Requires fungus/brood mechanics (phase 4+).",
        PheromoneChannels = [FoodTrail, HomeTrail],
        BaselineBrainFactory = new HandcodedForagerFactory(),
        Implemented = false,
    };
}
