using AntSim.Core;

namespace AntSim.Evolution;

/// <summary>
/// Task settings shared by the evaluation scheme, evaluator and body-parameter evolver.
/// Mutable so the body-parameter GA can adopt improved body parameters mid-run.
/// </summary>
public sealed class EvolveTaskSettings
{
    public required SpeciesDefinition Species { get; set; }
    public WorldConfig Config { get; set; } = new();
    public int EpisodesPerGenome { get; set; } = 2;
    public ulong BaseSeed { get; set; } = 1000;
    public AntBody Body { get; set; } = new();
    public PlasticityVector Plasticity { get; set; } = new();
}
