using AntSim.Core;
using SharpNeat;
using SharpNeat.Evaluation;

namespace AntSim.Evolution;

/// <summary>
/// Colony-level evaluation: one NEAT genome is the brain of the whole worker caste, and its
/// fitness is the total food delivered to the nest across several seeded episodes.
/// Colony fitness is both more realistic (nestmates are sisters) and far less noisy than
/// per-ant fitness. Auxiliary score: food picked up (measures progress before delivery).
/// </summary>
public sealed class AntColonyEvaluator : IPhenomeEvaluator<IBlackBox<double>>
{
    private readonly EvolveTaskSettings _settings;
    private readonly EpisodeRunner _runner = new();

    public AntColonyEvaluator(EvolveTaskSettings settings) => _settings = settings;

    public FitnessInfo Evaluate(IBlackBox<double> box)
    {
        box.Reset();
        var factory = new NeatBrainFactory(box);

        double delivered = 0;
        double pickedUp = 0;
        for (int i = 0; i < _settings.EpisodesPerGenome; i++)
        {
            ulong seed = _settings.BaseSeed + (ulong)i;
            var result = _runner.Run(_settings.Config, _settings.Species, factory, _settings.Body, _settings.Plasticity, seed);
            delivered += result.FoodDelivered;
            pickedUp += result.FoodPickedUp;
        }

        // Fitness shaping: pickups count 5% so that genomes that reach food but haven't yet
        // mastered the return trip still get selected (cold-start gradient; NEAT bootstrapping).
        return new FitnessInfo(delivered + 0.05 * pickedUp, [pickedUp]);
    }
}
