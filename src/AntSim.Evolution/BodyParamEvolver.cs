using AntSim.Core;
using SharpNeat;

namespace AntSim.Evolution;

/// <summary>
/// Current best colony body/plasticity parameters and the scores that earned them:
/// <paramref name="Score"/> on the training lane (what selection optimises) and
/// <paramref name="ValidationScore"/> on the held-out lane (reported only).
/// </summary>
public sealed record BodyEvolutionResult(AntBody Body, PlasticityVector Plasticity, double Score, double ValidationScore);

/// <summary>
/// Parallel GA for body parameters (speed, senses, deposit rate, lifespan) and the per-ant
/// plasticity vector, evolving alongside the NEAT brains as a (1+λ) hill-climber: mutants of the
/// current best vector are evaluated with the current champion brain on the same training lane the
/// brains are scored on.
///
/// Selection deliberately touches the training lane only. An earlier version folded two extra
/// worlds into the objective "to reduce overfitting", which is the wrong tool: any world used to
/// choose parameters is training data by definition, so the number it protected was not a
/// held-out one. The validation lane is now read here strictly for reporting.
/// </summary>
public sealed class BodyParamEvolver
{
    private readonly EpisodeRunner _runner = new();

    public BodyEvolutionResult Current { get; private set; } = new(new AntBody(), new PlasticityVector(), double.NaN, double.NaN);

    /// <summary>Evaluate λ mutants; keep the best (current champion brain held fixed).</summary>
    public BodyEvolutionResult Improve(IBlackBox<double> championBrain, EvolveTaskSettings settings, Prng prng, int lambda = 8, double sigma = 0.15)
    {
        double Score(AntBody body, PlasticityVector plasticity, IReadOnlyList<ulong> seeds)
        {
            double total = 0;
            var factory = new NeatBrainFactory(championBrain);
            // Anchor on ALL of the champion's own training seeds: its fitness is positive iff it
            // delivers on their sum, so the incumbent's score is strictly positive whenever the
            // brain demonstrably forages — mutants always have a meaningful gradient to climb.
            // (Anchoring on one seed is not enough: a lucky-direction walker can score 0 there.)
            for (int i = 0; i < seeds.Count; i++)
                total += _runner.Run(settings.Config, settings.Species, factory, body, plasticity, seeds[i]).FoodDelivered;
            return total;
        }

        var trainingSeeds = settings.TrainingSeeds;
        var best = Current;
        double bestScore = double.IsNaN(best.Score) ? Score(best.Body, best.Plasticity, trainingSeeds) : best.Score;
        // Record the incumbent's score so Current/returned result always carry a real value
        // (previously an unimproved round returned Score=NaN, hiding the true body score).
        if (double.IsNaN(best.Score))
            best = new BodyEvolutionResult(best.Body, best.Plasticity, bestScore, best.ValidationScore);

        for (int i = 0; i < lambda; i++)
        {
            var body = best.Body.Mutated(prng, sigma);
            var plasticity = best.Plasticity.Mutated(prng, sigma);
            double score = Score(body, plasticity, trainingSeeds);
            if (score > bestScore)
            {
                bestScore = score;
                best = new BodyEvolutionResult(body, plasticity, score, double.NaN);
            }
        }

        // Report how the winning colony parameters do on worlds they were never selected on.
        double validation = Score(best.Body, best.Plasticity, settings.ValidationSeeds);
        Current = best with { ValidationScore = validation };
        return Current;
    }
}
