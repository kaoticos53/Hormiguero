using AntSim.Core;
using SharpNeat;

namespace AntSim.Evolution;

/// <summary>Current best colony body/plasticity parameters and the score that earned them.</summary>
public sealed record BodyEvolutionResult(AntBody Body, PlasticityVector Plasticity, double Score);

/// <summary>
/// Parallel GA for body parameters (speed, senses, deposit rate, lifespan) and the per-ant
/// plasticity vector, evolving alongside the NEAT brains as a (1+λ) hill-climber: mutants of
/// the current best vector are evaluated with the current champion brain on validation seeds
/// (distinct from the fitness seeds, to reduce overfitting to the evaluation worlds).
/// </summary>
public sealed class BodyParamEvolver
{
    private static readonly ulong[] ExtraValidationSeeds = [910001, 910002];

    private readonly EpisodeRunner _runner = new();

    public BodyEvolutionResult Current { get; private set; } = new(new AntBody(), new PlasticityVector(), double.NaN);

    /// <summary>Evaluate λ mutants; keep the best (current champion brain held fixed).</summary>
    public BodyEvolutionResult Improve(IBlackBox<double> championBrain, EvolveTaskSettings settings, Prng prng, int lambda = 8, double sigma = 0.15)
    {
        double Score(AntBody body, PlasticityVector plasticity)
        {
            double total = 0;
            var factory = new NeatBrainFactory(championBrain);
            // Anchor on ALL of the champion's own fitness seeds: its fitness is positive iff it
            // delivers on their sum, so the incumbent's score is strictly positive whenever the
            // brain demonstrably forages — mutants always have a meaningful gradient to climb.
            // (Anchoring on one seed is not enough: a lucky-direction walker can score 0 there.)
            for (int i = 0; i < settings.EpisodesPerGenome; i++)
                total += _runner.Run(settings.Config, settings.Species, factory, body, plasticity, settings.BaseSeed + (ulong)i).FoodDelivered;
            for (int i = 0; i < ExtraValidationSeeds.Length; i++)
                total += _runner.Run(settings.Config, settings.Species, factory, body, plasticity, ExtraValidationSeeds[i]).FoodDelivered;
            return total;
        }

        var best = Current;
        double bestScore = double.IsNaN(best.Score) ? Score(best.Body, best.Plasticity) : best.Score;
        // Record the incumbent's score so Current/returned result always carry a real value
        // (previously an unimproved round returned Score=NaN, hiding the true body score).
        if (double.IsNaN(best.Score))
            best = new BodyEvolutionResult(best.Body, best.Plasticity, bestScore);

        for (int i = 0; i < lambda; i++)
        {
            var body = best.Body.Mutated(prng, sigma);
            var plasticity = best.Plasticity.Mutated(prng, sigma);
            double score = Score(body, plasticity);
            if (score > bestScore)
            {
                bestScore = score;
                best = new BodyEvolutionResult(body, plasticity, score);
            }
        }

        Current = best;
        return best;
    }
}
