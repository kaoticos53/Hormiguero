using AntSim.Core;
using SharpNeat;
using SharpNeat.Evaluation;

namespace AntSim.Evolution;

/// <summary>
/// Colony-level evaluation: one NEAT genome is the brain of the whole worker caste, and its
/// fitness is the total food delivered to the nest across several seeded episodes.
/// Colony fitness is both more realistic (nestmates are sisters) and far less noisy than
/// per-ant fitness. Auxiliary score: food picked up (measures progress before delivery).
///
/// <see cref="Evaluate"/> scores on the training lane only: that is the number selection sees.
/// <see cref="EvaluateOnSeeds"/> exists so the same code can also score the held-out validation
/// lane for reporting, without ever feeding it back into selection.
/// </summary>
public sealed class AntColonyEvaluator : IPhenomeEvaluator<IBlackBox<double>>
{
    private readonly EvolveTaskSettings _settings;
    private readonly EpisodeRunner _runner = new();

    public AntColonyEvaluator(EvolveTaskSettings settings) => _settings = settings;

    /// <summary>Selection fitness: training lane only.</summary>
    public FitnessInfo Evaluate(IBlackBox<double> box) => EvaluateOnSeeds(box, _settings.TrainingSeeds);

    /// <summary>
    /// Score a genome on an explicit seed set. Callers pass the training lane for selection and the
    /// validation lane for honest reporting; the two must never be mixed in one number.
    /// </summary>
    public FitnessInfo EvaluateOnSeeds(IBlackBox<double> box, IReadOnlyList<ulong> seeds)
    {
        box.Reset();
        var factory = new NeatBrainFactory(box);

        double delivered = 0;
        double pickedUp = 0;
        for (int i = 0; i < seeds.Count; i++)
        {
            var result = _runner.Run(_settings.Config, _settings.Species, factory, _settings.Body, _settings.Plasticity, seeds[i]);
            delivered += result.FoodDelivered;
            pickedUp += result.FoodPickedUp;
        }

        // Fitness shaping: pickups count 5% so that genomes that reach food but haven't yet
        // mastered the return trip still get selected (cold-start gradient; NEAT bootstrapping).
        return new FitnessInfo(delivered + 0.05 * pickedUp, [pickedUp]);
    }
}
