using System.Globalization;
using System.Text.Json;
using AntSim.Core;
using AntSim.Evolution;
using SharpNeat;
using SharpNeat.Evaluation;
using SharpNeat.Neat.EvolutionAlgorithm;
using SharpNeat.Neat.Genome;
using SharpNeat.Neat.Genome.Double;
using SharpNeat.Neat.Genome.IO;

namespace AntSim.Headless;

/// <summary>
/// Headless runner for the ant colony simulation. The simulation core is UI-free; this console
/// app drives it for demos, benchmarks, NEAT evolution runs and champion evaluation.
///
/// Commands (key=value options, all optional):
///   demo    species=black-garden-ant ticks=4000 seed=1 [replay=replay.json]
///   bench   ticks=4000 runs=3
///   evolve  gens=100 popsize=128 species=black-garden-ant seed=12345 out=output
///   evaluate path=output/champion.neat episodes=3 seed=1000
/// </summary>
public static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return;
        }

        var opts = ParseArgs(args.Skip(1));
        switch (args[0].ToLowerInvariant())
        {
            case "demo": RunDemo(opts); break;
            case "bench": RunBench(opts); break;
            case "evolve": RunEvolve(opts); break;
            case "evaluate": RunEvaluate(opts); break;
            default:
                Console.WriteLine($"Unknown command '{args[0]}'.");
                PrintUsage();
                break;
        }
    }

    // ------------------------------------------------------------------ demo

    private static void RunDemo(Dictionary<string, string> opts)
    {
        var species = ResolveSpecies(opts.GetValueOrDefault("species", "black-garden-ant"));
        int ticks = opts.GetInt("ticks", 4000);
        ulong seed = opts.GetUlong("seed", 1);
        string? replayPath = opts.GetValueOrDefault("replay");

        var config = new WorldConfig { EpisodeTicks = ticks };
        var world = SimWorld.CreateSeeded(config, species, species.BaselineBrainFactory, seed: seed);

        Console.WriteLine($"Demo: {species.DisplayName}  |  {config.WorkerCount} workers  |  " +
                          $"{config.Width}x{config.Height} world  |  seed {seed}");
        Console.WriteLine($"{"tick",6}  {"delivered",9}  {"pickedUp",9}  {"alive",6}  {"foodLeft",9}");

        ReplayRecorder? recorder = replayPath is not null ? new ReplayRecorder(sampleInterval: 10) : null;

        for (int t = 0; t < config.EpisodeTicks; t++)
        {
            world.Step();
            recorder?.Record(world);

            if (world.Tick % 500 == 0)
                PrintWorldStats(world);
        }

        if (world.Tick % 500 != 0)
            PrintWorldStats(world);
        Console.WriteLine($"Food remaining in piles: {world.FoodRemaining:F1}");

        if (recorder is not null)
        {
            var replay = recorder.ToReplayData(world, 10);
            File.WriteAllText(replayPath!, JsonSerializer.Serialize(replay));
            Console.WriteLine($"Replay written to {replayPath} ({replay.Frames.Count} frames).");
        }
    }

    private static void PrintWorldStats(SimWorld world)
    {
        int alive = 0;
        for (int i = 0; i < world.Ants.Count; i++)
            if (world.Ants[i].Alive) alive++;

        Console.WriteLine($"{world.Tick,6}  {world.FoodDelivered,9}  {world.FoodPickedUp,9}  {alive,6}  {world.FoodRemaining,9:F1}");
    }

    // ----------------------------------------------------------------- bench

    private static void RunBench(Dictionary<string, string> opts)
    {
        int ticks = opts.GetInt("ticks", 4000);
        int runs = opts.GetInt("runs", 3);
        var species = ResolveSpecies(opts.GetValueOrDefault("species", "black-garden-ant"));
        var config = new WorldConfig { EpisodeTicks = ticks };

        Console.WriteLine($"Benchmark: baseline brain, {config.WorkerCount} ants, {ticks} ticks per run, {runs} runs.");
        var runner = new EpisodeRunner();

        for (int i = 0; i < runs; i++)
        {
            ulong seed = 1000u + (ulong)i;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = runner.Run(config, species, species.BaselineBrainFactory, seed: seed);
            sw.Stop();

            double ticksPerSec = result.TicksRun / sw.Elapsed.TotalSeconds;
            Console.WriteLine($"  run {i + 1}: {result.TicksRun} ticks in {sw.Elapsed.TotalMilliseconds,7:F0} ms " +
                              $"= {ticksPerSec,8:F0} ticks/s   (delivered {result.FoodDelivered})");
        }
    }

    // ---------------------------------------------------------------- evolve

    private static void RunEvolve(Dictionary<string, string> opts)
    {
        var species = ResolveSpecies(opts.GetValueOrDefault("species", "black-garden-ant"));
        string? outputDir = opts.GetValueOrDefault("out");

        var opt = new EvolveOptions
        {
            Species = species,
            Generations = opts.GetInt("gens", 100),
            PopulationSize = opts.GetInt("popsize", 128),
            SpeciesCount = opts.GetInt("speciescount", 16),
            EpisodesPerGenome = opts.GetInt("episodes", 2),
            EpisodeTicks = opts.GetInt("ticks", 4000),
            WorkerCount = opts.GetInt("workers", 150),
            Seed = opts.GetUlong("seed", 12345),
            NearFoodCurriculum = opts.GetValueOrDefault("food", "near") != "far",
            OutputDir = outputDir,
        };

        Console.WriteLine($"Evolving {species.DisplayName}: gens={opt.Generations} pop={opt.PopulationSize} " +
                          $"episodes/genome={opt.EpisodesPerGenome} ticks={opt.EpisodeTicks} workers={opt.WorkerCount} seed={opt.Seed}");
        Console.WriteLine($"{"gen",5}  {"best",10}  {"mean",10}  {"complex",8}  {"bodyScore",10}");

        var runner = new AntEvolutionRunner(opt);
        var csvPath = outputDir is null ? null : Path.Combine(outputDir, "fitness.csv");
        StreamWriter? csv = null;
        if (csvPath is not null)
        {
            Directory.CreateDirectory(outputDir!);
            csv = new StreamWriter(csvPath);
            csv.WriteLine("generation,best,mean,bestComplexity,bodyScore");
        }

        IReadOnlyList<GenerationRecord> records = [];
        try
        {
            records = runner.Run(rec =>
            {
                Console.WriteLine($"{rec.Generation,5}  {rec.BestFitness,10:F2}  {rec.MeanFitness,10:F2}  {rec.BestComplexity,8:F1}  " +
                                  $"{rec.BodyScore.ToString("F2", CultureInfo.InvariantCulture),10}");
                csv?.WriteLine(
                    $"{rec.Generation},{rec.BestFitness.ToString(CultureInfo.InvariantCulture)}," +
                    $"{rec.MeanFitness.ToString(CultureInfo.InvariantCulture)},{rec.BestComplexity.ToString("F1", CultureInfo.InvariantCulture)}," +
                    $"{rec.BodyScore.ToString("F2", CultureInfo.InvariantCulture)}");
                csv?.Flush(); // survive interruptions: every generation is on disk immediately
            });
        }
        finally
        {
            csv?.Dispose();
        }

        if (csvPath is not null)
            Console.WriteLine($"Fitness history written to {csvPath}");

        var best = records.Count == 0 ? 0 : records.Max(r => r.BestFitness);
        Console.WriteLine($"Done. Best fitness over {opt.Generations} generations: {best:F2}");
    }

    // -------------------------------------------------------------- evaluate

    private static void RunEvaluate(Dictionary<string, string> opts)
    {
        string path = opts.GetValueOrDefault("path", "output/champion.neat");
        int episodes = opts.GetInt("episodes", 3);
        ulong seed = opts.GetUlong("seed", 1000);
        int ticks = opts.GetInt("ticks", 4000);

        if (!File.Exists(path))
        {
            Console.WriteLine($"Genome file not found: {path}");
            Environment.ExitCode = 1;
            return;
        }

        var meta = AntEvolutionExperiment.CreateMetaNeatGenome();
        var genome = NeatGenomeLoader.Load<double>(path, meta, 0);
        IGenomeDecoder<NeatGenome<double>, IBlackBox<double>> decoder =
            NeatGenomeDecoderFactory.CreateGenomeDecoder(meta.IsAcyclic, false);
        var box = decoder.Decode(genome);

        var species = ResolveSpecies(opts.GetValueOrDefault("species", "black-garden-ant"));
        bool nearFood = opts.GetValueOrDefault("food", "near") != "far";
        var config = new WorldConfig
        {
            EpisodeTicks = ticks,
            MinFoodDistanceFromNest = nearFood ? 120f : 250f,
            MaxFoodDistanceFromNest = nearFood ? 220f : 350f,
        };
        var runner = new EpisodeRunner();
        var brainFactory = new NeatBrainFactory(box);

        Console.WriteLine($"Evaluating champion ({genome.ConnectionGenes.Length} connections, complexity {genome.Complexity:F1}) " +
                          $"on {episodes} episodes of {ticks} ticks.");

        double total = 0;
        for (int i = 0; i < episodes; i++)
        {
            var result = runner.Run(config, species, brainFactory, seed: seed + (ulong)i);
            total += result.FoodDelivered;
            Console.WriteLine($"  seed {seed + (ulong)i}: delivered {result.FoodDelivered}, " +
                              $"picked up {result.FoodPickedUp}, alive {result.AntsAlive}");
        }

        Console.WriteLine($"Mean delivered: {total / episodes:F2}");
    }

    // ------------------------------------------------------------------ misc

    private static SpeciesDefinition ResolveSpecies(string id) => id.ToLowerInvariant() switch
    {
        "black-garden-ant" or "black" or "lasius" => SpeciesCatalog.BlackGardenAnt,
        "fire-ant" or "fire" => SpeciesCatalog.FireAnt,
        "army-ant" or "army" => SpeciesCatalog.ArmyAnt,
        "leafcutter-ant" or "leafcutter" => SpeciesCatalog.LeafcutterAnt,
        _ => throw new ArgumentException($"Unknown species id '{id}'."),
    };

    private static Dictionary<string, string> ParseArgs(IEnumerable<string> args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var arg in args)
        {
            int eq = arg.IndexOf('=');
            if (eq > 0)
                map[arg[..eq]] = arg[(eq + 1)..];
        }
        return map;
    }

    private static int GetInt(this Dictionary<string, string> map, string key, int fallback)
        => map.TryGetValue(key, out var s) && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private static ulong GetUlong(this Dictionary<string, string> map, string key, ulong fallback)
        => map.TryGetValue(key, out var s) && ulong.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private static void PrintUsage()
    {
        Console.WriteLine("""
            AntSim headless runner
              demo      species=black-garden-ant ticks=4000 seed=1 [replay=replay.json]
              bench     ticks=4000 runs=3
              evolve    gens=100 popsize=128 seed=12345 [out=output] [species=...] [episodes=2] [ticks=4000] [workers=150]
              evaluate  path=output/champion.neat episodes=3 seed=1000
            """);
    }
}
