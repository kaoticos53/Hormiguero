namespace AntSim.Core;

/// <summary>Ant castes. The MVP evolves the worker caste; others are staged for later phases.</summary>
public enum Caste
{
    Worker,
    Scout,
    Soldier,
    Queen,
}

/// <summary>
/// One ant: mutable simulation state plus an immutable-per-life body, plasticity vector and brain.
/// All ants of a caste share one brain genome (nestmates are sisters); body/plasticity come from
/// the colony's evolving parameter vector.
/// </summary>
public sealed class Ant
{
    public required int Id { get; init; }
    public required Caste Caste { get; init; }
    public required IAntBrain Brain { get; init; }

    public AntBody Body { get; set; } = new();
    public PlasticityVector Plasticity { get; set; } = new();

    public float X { get; set; }
    public float Y { get; set; }
    public float Heading { get; set; }

    /// <summary>Food amount currently carried (0 when not carrying).</summary>
    public float FoodCarried { get; set; }

    public float Age { get; set; }
    public bool Alive { get; set; } = true;

    /// <summary>Online plasticity state: smoothed exposure to the food trail, drives habituation.</summary>
    public float FoodTrailExposure { get; set; }

    /// <summary>Scratch buffer for this tick's actions (written by the brain, read by the world).</summary>
    public AntActions Actions;
}
