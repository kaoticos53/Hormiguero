using AntSim.Core;
using AntSim.Evolution;
using SharpNeat;
using SharpNeat.Evaluation;
using SharpNeat.Neat;
using SharpNeat.Neat.Genome;
using SharpNeat.Neat.Genome.Double;

namespace AntSim.Tests;

/// <summary>
/// The hand-coded forager is the ground truth the simulation is calibrated against: if it
/// can't forage, the world or the reflexes are broken. Thresholds are deliberately loose
/// so they hold on any machine.
/// </summary>
public class BaselineThresholdTests
{
    private static readonly WorldConfig NearFoodConfig = new()
    {
        EpisodeTicks = 4000,
        MinFoodDistanceFromNest = 120f,
        MaxFoodDistanceFromNest = 220f,
    };

    [Fact]
    public void Baseline_DeliversFood()
    {
        var runner = new EpisodeRunner();
        var result = runner.Run(
            NearFoodConfig, SpeciesCatalog.BlackGardenAnt,
            SpeciesCatalog.BlackGardenAnt.BaselineBrainFactory, seed: 3);

        Assert.True(result.FoodDelivered > 30,
            $"Baseline delivered only {result.FoodDelivered} food in 4000 ticks — foraging loop is broken.");
        Assert.Equal(150, result.AntsAlive); // 4000 ticks = 400s < 900s lifespan: no natural deaths
        Assert.True(result.FoodPickedUp >= result.FoodDelivered);
    }

    [Fact]
    public void Baseline_BuildsFoodTrail()
    {
        var runner = new EpisodeRunner();
        var result = runner.Run(
            NearFoodConfig, SpeciesCatalog.BlackGardenAnt,
            SpeciesCatalog.BlackGardenAnt.BaselineBrainFactory, seed: 3);

        Assert.True(result.FoodTrailTotal > 1f,
            "Homing ants should lay a food trail; the food-trail field is empty.");
    }
}

/// <summary>
/// Structural smoke tests for the SharpNEAT wiring: schema consistency, decoding, and that the
/// evaluator runs a real episode through the colony brain. No learning-performance assertions
/// here (those are validated interactively via `evolve`, which is too slow/flaky for CI).
/// </summary>
public class NeatWiringTests
{
    [Fact]
    public void MetaGenome_MatchesSensorSchema()
    {
        var meta = AntEvolutionExperiment.CreateMetaNeatGenome();
        Assert.Equal(AntSensors.TotalInputCount, meta.InputNodeCount);
        Assert.Equal(3, meta.OutputNodeCount); // turn, speed, deposit
        Assert.False(meta.IsAcyclic);
        Assert.Equal(1, meta.CyclesPerActivation);
    }

    [Fact]
    public void EvaluationScheme_MatchesMetaGenome()
    {
        var settings = new EvolveTaskSettings { Species = SpeciesCatalog.BlackGardenAnt };
        var scheme = new AntColonyEvaluationScheme(settings);
        var meta = AntEvolutionExperiment.CreateMetaNeatGenome();

        Assert.Equal(meta.InputNodeCount, scheme.InputCount);
        Assert.Equal(meta.OutputNodeCount, scheme.OutputCount);
        Assert.True(scheme.IsDeterministic);
        Assert.False(scheme.EvaluatorsHaveState);
    }

    [Fact]
    public void InitialPopulation_DecodesAndEvaluates()
    {
        var meta = AntEvolutionExperiment.CreateMetaNeatGenome();
        var pop = NeatPopulationFactory<double>.CreatePopulation(meta, 0.25, 8, Redzen.Random.RandomDefaults.CreateRandomSource(5));

        IGenomeDecoder<NeatGenome<double>, IBlackBox<double>> decoder =
            NeatGenomeDecoderFactory.CreateGenomeDecoder(meta.IsAcyclic, false);

        var settings = new EvolveTaskSettings
        {
            Species = SpeciesCatalog.BlackGardenAnt,
            Config = new WorldConfig { EpisodeTicks = 400, MinFoodDistanceFromNest = 120f, MaxFoodDistanceFromNest = 220f },
            EpisodesPerGenome = 1,
            BaseSeed = 500,
        };
        var evaluator = new AntColonyEvaluator(settings);

        foreach (var genome in pop.GenomeList)
        {
            var box = decoder.Decode(genome);
            var fitness = evaluator.Evaluate(box);
            Assert.True(fitness.PrimaryFitness >= 0);
            Assert.True(fitness.AuxFitnessScores[0] >= 0);
        }
    }

    [Fact]
    public void Evaluator_RunsRealEpisodes_DeliversSomething()
    {
        // The colony as a whole: even a poor-but-mobile colony brain should produce *some*
        // pickups over 100 simulated seconds with 150 ants on near-food piles. A frozen
        // (all-zero-speed) colony produces exactly 0 — that's what this test would catch.
        var meta = AntEvolutionExperiment.CreateMetaNeatGenome();
        var pop = NeatPopulationFactory<double>.CreatePopulation(meta, 0.25, 32, Redzen.Random.RandomDefaults.CreateRandomSource(11));

        IGenomeDecoder<NeatGenome<double>, IBlackBox<double>> decoder =
            NeatGenomeDecoderFactory.CreateGenomeDecoder(meta.IsAcyclic, false);

        var settings = new EvolveTaskSettings
        {
            Species = SpeciesCatalog.BlackGardenAnt,
            Config = new WorldConfig { EpisodeTicks = 1000, MinFoodDistanceFromNest = 120f, MaxFoodDistanceFromNest = 220f },
            EpisodesPerGenome = 1,
            BaseSeed = 600,
        };
        var evaluator = new AntColonyEvaluator(settings);

        double bestPickedUp = 0;
        foreach (var genome in pop.GenomeList)
        {
            var box = decoder.Decode(genome);
            bestPickedUp = Math.Max(bestPickedUp, evaluator.Evaluate(box).AuxFitnessScores[0]);
        }

        Assert.True(bestPickedUp > 0,
            "No genome in an initial population picked up any food — the colony brain pathway is likely broken.");
    }
}
