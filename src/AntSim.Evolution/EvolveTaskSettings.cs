using AntSim.Core;

namespace AntSim.Evolution;

/// <summary>
/// Task settings shared by the evaluation scheme, evaluator and body-parameter evolver.
/// Mutable so the body-parameter GA can adopt improved body parameters mid-run.
///
/// Seeds come in two lanes (see <see cref="SeedSpaces"/>): <see cref="TrainingSeeds"/> is what
/// selection optimises against, <see cref="ValidationSeeds"/> is held out and only ever read, so
/// the difference between the two is the generalisation gap rather than good luck on two worlds.
/// </summary>
public sealed class EvolveTaskSettings
{
    public required SpeciesDefinition Species { get; set; }
    public WorldConfig Config { get; set; } = new();
    public int EpisodesPerGenome { get; set; } = 2;

    /// <summary>Number of held-out episodes used to report generalisation (never used for selection).</summary>
    public int ValidationEpisodes { get; set; } = 3;

    /// <summary>First seed of the training lane; the lane is this base plus the episode index.</summary>
    public ulong BaseSeed { get; set; } = 1000;

    public AntBody Body { get; set; } = new();
    public PlasticityVector Plasticity { get; set; } = new();

    /// <summary>The seeds selection optimises against (NEAT fitness and body parameters alike).</summary>
    public ulong[] TrainingSeeds => SeedSpaces.Consecutive(BaseSeed, EpisodesPerGenome, nameof(EpisodesPerGenome));

    /// <summary>The held-out seeds used only to report generalisation.</summary>
    public ulong[] ValidationSeeds => SeedSpaces.Consecutive(
        BaseSeed + SeedSpaces.ValidationLaneOffset, ValidationEpisodes, nameof(ValidationEpisodes));
}
