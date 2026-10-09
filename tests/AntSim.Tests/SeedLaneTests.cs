using AntSim.Core;
using AntSim.Evolution;
using Redzen.Random;
using SharpNeat;
using SharpNeat.Evaluation;
using SharpNeat.Neat;
using SharpNeat.Neat.Genome;
using SharpNeat.Neat.Genome.Double;

namespace AntSim.Tests;

/// <summary>
/// The seed-lane contract: selection optimises the training lane, validation is held out and only
/// ever read. These tests pin the layout (so old runs stay reproducible) and, more importantly,
/// that the two lanes can never intersect — a validation number that selection has touched is
/// worse than no number at all.
/// </summary>
public class SeedLaneTests
{
    [Fact]
    public void TrainingSeeds_KeepTheHistoricalLayout()
    {
        // Historical layout: training base = run seed * 1000, plus the episode index.
        var settings = new EvolveTaskSettings
        {
            Species = SpeciesCatalog.BlackGardenAnt,
            BaseSeed = 500,
            EpisodesPerGenome = 3,
        };

        Assert.Equal([500UL, 501UL, 502UL], settings.TrainingSeeds);
    }

    [Fact]
    public void Settings_LanesMatchTheSeedSpaceHelpers()
    {
        var settings = new EvolveTaskSettings
        {
            Species = SpeciesCatalog.BlackGardenAnt,
            BaseSeed = SeedSpaces.TrainingBase(424242),
            EpisodesPerGenome = 2,
            ValidationEpisodes = 3,
        };

        Assert.Equal(SeedSpaces.Training(424242, 2), settings.TrainingSeeds);
        Assert.Equal(SeedSpaces.Validation(424242, 3), settings.ValidationSeeds);
        Assert.Equal(424_242_000UL, settings.TrainingSeeds[0]);   // what run6 actually trained on
        Assert.Equal(1_424_242_000UL, settings.ValidationSeeds[0]);
    }

    [Fact]
    public void TrainingAndValidationLanes_NeverIntersect()
    {
        for (ulong runSeed = 1; runSeed <= 2_000; runSeed++)
        {
            foreach (int episodes in new[] { 1, 2, 5, 10 })
            {
                var training = SeedSpaces.Training(runSeed, episodes).ToHashSet();
                var validation = SeedSpaces.Validation(runSeed, episodes);

                Assert.DoesNotContain(validation, training.Contains);
            }
        }

        // The old hard-coded regularisation seeds (910001/910002) sat inside run seed 910's own
        // training lane — the exact collision the offset lane removes.
        var training910 = SeedSpaces.Training(910, 3).ToHashSet();
        Assert.Contains(910001UL, training910);
        Assert.DoesNotContain(910001UL, SeedSpaces.Validation(910, 3));
    }

    [Fact]
    public void ValidationSeeds_HonourEpisodeCount()
    {
        Assert.Equal(3, new EvolveTaskSettings { Species = SpeciesCatalog.BlackGardenAnt }.ValidationSeeds.Length);
        Assert.Single(new EvolveTaskSettings { Species = SpeciesCatalog.BlackGardenAnt, ValidationEpisodes = 1 }.ValidationSeeds);
    }

    [Fact]
    public void AnEmptyLane_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SeedSpaces.Consecutive(10, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EvolveTaskSettings
        {
            Species = SpeciesCatalog.BlackGardenAnt,
            EpisodesPerGenome = 0,
        }.TrainingSeeds);
    }
}

/// <summary>
/// The evaluator's number must be reproducible from the training lane alone, and the evolution
/// runner must publish the held-out score without letting it leak into selection.
/// </summary>
public class HeldOutEvaluationTests
{
    private static readonly WorldConfig TinyConfig = new()
    {
        EpisodeTicks = 240,
        MinFoodDistanceFromNest = 120f,
        MaxFoodDistanceFromNest = 220f,
    };

    private static IReadOnlyList<NeatGenome<double>> Population(int count, ulong seed)
    {
        var meta = AntEvolutionExperiment.CreateMetaNeatGenome();
        return NeatPopulationFactory<double>.CreatePopulation(
            meta, 0.25, count, RandomDefaults.CreateRandomSource(seed)).GenomeList;
    }

    [Fact]
    public void Evaluator_ScoreIsExactlyTheTrainingLane()
    {
        var settings = new EvolveTaskSettings
        {
            Species = SpeciesCatalog.BlackGardenAnt,
            Config = TinyConfig,
            EpisodesPerGenome = 2,
            BaseSeed = 4242,
        };
        var evaluator = new AntColonyEvaluator(settings);
        var runner = new EpisodeRunner();

        IGenomeDecoder<NeatGenome<double>, IBlackBox<double>> decoder =
            NeatGenomeDecoderFactory.CreateGenomeDecoder(AntEvolutionExperiment.CreateMetaNeatGenome().IsAcyclic, false);

        foreach (var genome in Population(2, 17))
        {
            var fitness = evaluator.Evaluate(decoder.Decode(genome));

            // Independent replication over the training lane only.
            double delivered = 0, pickedUp = 0;
            var factory = new NeatBrainFactory(decoder.Decode(genome));
            foreach (ulong seed in settings.TrainingSeeds)
            {
                var result = runner.Run(settings.Config, settings.Species, factory, seed: seed);
                delivered += result.FoodDelivered;
                pickedUp += result.FoodPickedUp;
            }

            Assert.Equal(delivered + 0.05 * pickedUp, fitness.PrimaryFitness, 6);
            Assert.Equal(pickedUp, fitness.AuxFitnessScores[0], 6);
        }
    }

    [Fact]
    public void Evaluator_ScoresWhateverLaneItIsGiven()
    {
        var settings = new EvolveTaskSettings
        {
            Species = SpeciesCatalog.BlackGardenAnt,
            Config = TinyConfig,
            EpisodesPerGenome = 1,
            ValidationEpisodes = 2,
            BaseSeed = 4242,
        };
        var evaluator = new AntColonyEvaluator(settings);
        var box = NeatGenomeDecoderFactory
            .CreateGenomeDecoder(AntEvolutionExperiment.CreateMetaNeatGenome().IsAcyclic, false)
            .Decode(Population(2, 23)[0]);

        var training = evaluator.EvaluateOnSeeds(box, settings.TrainingSeeds);
        var validation = evaluator.EvaluateOnSeeds(box, settings.ValidationSeeds);

        Assert.True(training.PrimaryFitness >= 0);
        Assert.True(validation.PrimaryFitness >= 0);
    }

    [Fact]
    public void EvolutionRunner_ReportsHeldOutValidationFitness()
    {
        var opt = new EvolveOptions
        {
            Generations = 2,
            PopulationSize = 8,
            SpeciesCount = 2,
            EpisodesPerGenome = 1,
            ValidationEpisodes = 2,
            ValidationEvalInterval = 5, // only the final generation is due
            EpisodeTicks = 120,
            WorkerCount = 20,
            DegreeOfParallelism = 1, // also covers the single-threaded speciation path
            Seed = 4242,
            OutputDir = null,
        };

        var records = new AntEvolutionRunner(opt).Run();

        Assert.Equal(2, records.Count);
        Assert.True(double.IsNaN(records[0].ValidationFitness),
            "validation is throttled, so generation 1 should not carry a held-out score");
        Assert.False(double.IsNaN(records[^1].ValidationFitness),
            "the last generation must always report the held-out score");
        Assert.True(records[^1].ValidationFitness >= 0);
        Assert.True(records[^1].BestFitness >= 0);
    }

    [Fact]
    public void Evolution_IsBitReproducible()
    {
        // The parallel k-means speciation reduced floating-point centroid distances in
        // scheduler-dependent order, so the same seed drifted apart run to run and no A/B could be
        // paired. Speciation is now single-threaded and genome evaluation is per-genome (each
        // episode is bit-reproducible), so two runs of one seed must agree exactly even with the
        // evaluator running in parallel.
        EvolveOptions Options() => new()
        {
            Generations = 3,
            PopulationSize = 16,
            SpeciesCount = 2,
            EpisodesPerGenome = 1,
            ValidationEpisodes = 1,
            EpisodeTicks = 120,
            WorkerCount = 20,
            Seed = 424242,
            OutputDir = null,
        };

        var first = new AntEvolutionRunner(Options()).Run();
        var second = new AntEvolutionRunner(Options()).Run();

        Assert.Equal(first.Count, second.Count);
        for (int i = 0; i < first.Count; i++)
        {
            Assert.Equal(first[i].BestFitness, second[i].BestFitness);
            Assert.Equal(first[i].MeanFitness, second[i].MeanFitness);
            Assert.Equal(first[i].BestComplexity, second[i].BestComplexity);
        }
    }
}
