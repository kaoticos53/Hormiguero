using AntSim.Core;
using SharpNeat;
using SharpNeat.Evaluation;
using SharpNeat.Neat.EvolutionAlgorithm;
using SharpNeat.Neat.Genome;
using SharpNeat.Neat.Genome.Double;
using SharpNeat.Neat.Genome.IO;
using System.Text.Json;

namespace AntSim.Evolution;

/// <summary>One row of the evolution log.</summary>
public sealed record GenerationRecord(
    int Generation,
    double BestFitness,
    double MeanFitness,
    double BestComplexity,
    double BodyScore);

/// <summary>Options for one evolution run (overridable from the CLI).</summary>
public sealed class EvolveOptions
{
    public SpeciesDefinition Species { get; set; } = SpeciesCatalog.BlackGardenAnt;
    public int Generations { get; set; } = 100;
    public int PopulationSize { get; set; } = 128;
    public int SpeciesCount { get; set; } = 16;
    public int EpisodesPerGenome { get; set; } = 2;
    public int EpisodeTicks { get; set; } = 4000;
    public int WorkerCount { get; set; } = 150;
    public int DegreeOfParallelism { get; set; } = Environment.ProcessorCount;
    public int BodyEvolveInterval { get; set; } = 10;
    public ulong Seed { get; set; } = 12345;

    /// <summary>
    /// Curriculum: when true (default) evolution episodes start with food piles closer to the
    /// nest so that early random-policy colonies can stumble onto food and selection has a
    /// gradient to climb. Set false to evaluate at the full natural distances.
    /// </summary>
    public bool NearFoodCurriculum { get; set; } = true;

    public string? OutputDir { get; set; }
}

/// <summary>
/// Runs NEAT evolution end to end: colony fitness via SharpNEAT, periodic body-parameter
/// hill-climbing rounds, CSV-friendly generation records and champion checkpoints.
/// </summary>
public sealed class AntEvolutionRunner
{
    private readonly EvolveOptions _opt;

    public AntEvolutionRunner(EvolveOptions opt) => _opt = opt;

    public IReadOnlyList<GenerationRecord> Run(Action<GenerationRecord>? onGeneration = null)
    {
        var settings = new EvolveTaskSettings
        {
            Species = _opt.Species,
            Config = new WorldConfig
            {
                EpisodeTicks = _opt.EpisodeTicks,
                WorkerCount = _opt.WorkerCount,
                MinFoodDistanceFromNest = _opt.NearFoodCurriculum ? 120f : 250f,
                MaxFoodDistanceFromNest = _opt.NearFoodCurriculum ? 220f : 350f,
            },
            EpisodesPerGenome = _opt.EpisodesPerGenome,
            BaseSeed = _opt.Seed * 1000,
        };

        var scheme = new AntColonyEvaluationScheme(settings);
        var ea = AntEvolutionExperiment.CreateEvolutionAlgorithm(_opt, scheme);
        ea.Initialise();

        var bodyEvolver = new BodyParamEvolver();
        var bodyPrng = new Prng(_opt.Seed ^ 0x9E3779B97F4A7C15UL);
        IGenomeDecoder<NeatGenome<double>, IBlackBox<double>> decoder =
            NeatGenomeDecoderFactory.CreateGenomeDecoder(false, false);

        var records = new List<GenerationRecord>(_opt.Generations);
        for (int gen = 1; gen <= _opt.Generations; gen++)
        {
            ea.PerformOneGeneration();

            var genomes = ea.Population.GenomeList;
            double best = double.NegativeInfinity, sum = 0, bestComplexity = 0;
            for (int i = 0; i < genomes.Count; i++)
            {
                var g = genomes[i];
                double f = g.FitnessInfo.PrimaryFitness;
                if (f > best) best = f;
                sum += f;
                double cx = g.Complexity;
                if (cx > bestComplexity) bestComplexity = cx;
            }

            double bodyScore = double.NaN;
            if (gen % _opt.BodyEvolveInterval == 0)
            {
                var champ = ea.Population.BestGenome;
                if (champ is not null)
                {
                    var box = decoder.Decode(champ);
                    var result = bodyEvolver.Improve(box, settings, bodyPrng);
                    bodyScore = result.Score;
                    // Adopt improved colony parameters; later evaluations and the final export use them.
                    settings.Body = result.Body;
                    settings.Plasticity = result.Plasticity;
                }
            }

            var rec = new GenerationRecord(gen, best, sum / Math.Max(1, genomes.Count), bestComplexity, bodyScore);
            records.Add(rec);
            onGeneration?.Invoke(rec);

            if (_opt.OutputDir is not null && (gen % 10 == 0 || gen == _opt.Generations))
                SaveChampion(ea, bodyEvolver, settings, _opt.OutputDir);
        }

        return records;
    }

    private static void SaveChampion(NeatEvolutionAlgorithm<double> ea, BodyParamEvolver bodyEvolver, EvolveTaskSettings settings, string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        var champ = ea.Population.BestGenome;
        if (champ is null) return;

        // Native SharpNEAT genome (round-trip fidelity; consumed by `evaluate`).
        NeatGenomeSaver.Save(champ, Path.Combine(outputDir, "champion.neat"));

        // Self-contained brain + colony parameters JSON (consumed later by the Unity viewer).
        var export = GenomeExport.ToExport(champ, settings.Body, settings.Plasticity);
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(Path.Combine(outputDir, "champion.json"), JsonSerializer.Serialize(export, jsonOptions));
    }
}
