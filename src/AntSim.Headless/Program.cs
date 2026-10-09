using System.Globalization;
using System.Text.Json;
using AntSim.Core;
using AntSim.Evolution;

namespace AntSim.Headless;

/// <summary>
/// Headless runner for the ant colony simulation. The simulation core is UI-free; this console
/// app drives it for demos, benchmarks, NEAT evolution runs and champion evaluation.
///
/// Commands (key=value options, all optional):
///   demo    species=black-garden-ant ticks=4000 seed=1 [replay=replay.json] [brain=champion.neat] [food=near|far]
///   bench   ticks=4000 runs=3
///   evolve  gens=100 popsize=128 species=black-garden-ant seed=12345 out=output
///   evaluate path=output/champion.neat lane=val runseed=12345 episodes=3
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
        string? brainPath = opts.GetValueOrDefault("brain");

        // Optional evolved brain: champion.neat plus the companion champion.json that carries the
        // colony's evolved body/plasticity. Loaded through the shared loader so every command runs
        // the champion as it was evolved, never on a default body.
        IAntBrainFactory brainFactory = species.BaselineBrainFactory;
        AntBody? body = null;
        PlasticityVector? plasticity = null;
        string brainLabel = "baseline";
        if (brainPath is not null)
        {
            var champion = ChampionLoader.Load(brainPath);
            brainFactory = champion.BrainFactory;
            body = champion.Body;
            plasticity = champion.Plasticity;
            brainLabel = champion.Label;
        }

        bool nearFood = opts.GetValueOrDefault("food", "far") != "far";
        var config = BuildConfig(ticks, nearFood);
        var world = SimWorld.CreateSeeded(config, species, brainFactory, body, plasticity, seed: seed);

        Console.WriteLine($"Demo: {species.DisplayName}  |  {config.WorkerCount} workers  |  " +
                          $"{config.Width}x{config.Height} world  |  seed {seed}  |  brain: {brainLabel}");
        Console.WriteLine($"{"tick",6}  {"delivered",9}  {"pickedUp",9}  {"alive",6}  {"foodLeft",9}");

        ReplayRecorder? recorder = replayPath is not null ? new ReplayRecorder(sampleInterval: 10) : null;

        recorder?.Record(world);
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
            ValidationEpisodes = opts.GetInt("valepisodes", 3),
            ValidationEvalInterval = opts.GetInt("valinterval", 5),
            NearFoodCurriculum = opts.GetValueOrDefault("food", "near") != "far",
            OutputDir = outputDir,
        };

        Console.WriteLine($"Evolving {species.DisplayName}: gens={opt.Generations} pop={opt.PopulationSize} " +
                          $"episodes/genome={opt.EpisodesPerGenome} ticks={opt.EpisodeTicks} workers={opt.WorkerCount} seed={opt.Seed}");
        // Print both lanes: they make the run reproducible and are exactly what `evaluate` takes.
        Console.WriteLine($"  training seeds   (selection): {string.Join(", ", SeedSpaces.Training(opt.Seed, opt.EpisodesPerGenome))}");
        Console.WriteLine($"  validation seeds (held out): {string.Join(", ", SeedSpaces.Validation(opt.Seed, opt.ValidationEpisodes))}"
                        + $"  [reported every {opt.ValidationEvalInterval} gen, never selected on]");
        Console.WriteLine($"{"gen",5}  {"best",10}  {"mean",10}  {"complex",8}  {"bodyScore",10}  {"validation",10}");

        var runner = new AntEvolutionRunner(opt);
        var csvPath = outputDir is null ? null : Path.Combine(outputDir, "fitness.csv");
        StreamWriter? csv = null;
        if (csvPath is not null)
        {
            Directory.CreateDirectory(outputDir!);
            csv = new StreamWriter(csvPath);
            csv.WriteLine("generation,best,mean,bestComplexity,bodyScore,validationFitness");
        }

        IReadOnlyList<GenerationRecord> records = [];
        try
        {
            records = runner.Run(rec =>
            {
                string validationCell = double.IsNaN(rec.ValidationFitness)
                    ? "-"
                    : rec.ValidationFitness.ToString("F2", CultureInfo.InvariantCulture);
                Console.WriteLine($"{rec.Generation,5}  {rec.BestFitness,10:F2}  {rec.MeanFitness,10:F2}  {rec.BestComplexity,8:F1}  " +
                                  $"{rec.BodyScore.ToString("F2", CultureInfo.InvariantCulture),10}  {validationCell,10}");
                csv?.WriteLine(
                    $"{rec.Generation},{rec.BestFitness.ToString(CultureInfo.InvariantCulture)}," +
                    $"{rec.MeanFitness.ToString(CultureInfo.InvariantCulture)},{rec.BestComplexity.ToString("F1", CultureInfo.InvariantCulture)}," +
                    $"{rec.BodyScore.ToString("F2", CultureInfo.InvariantCulture)}," +
                    $"{rec.ValidationFitness.ToString("F4", CultureInfo.InvariantCulture)}");
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
        int ticks = opts.GetInt("ticks", 4000);
        ulong runSeed = opts.GetUlong("runseed", 12345);
        string lane = opts.GetValueOrDefault("lane", "val").ToLowerInvariant();

        // Default to the held-out lane: a champion measured on its own training seeds reports
        // memory, not skill. `lane=train` reproduces the evolution numbers, `lane=raw seed=N` is
        // the explicit escape hatch for ad-hoc worlds.
        ulong baseSeed = lane switch
        {
            "train" or "training" => SeedSpaces.TrainingBase(runSeed),
            "val" or "validation" => SeedSpaces.ValidationBase(runSeed),
            "raw" => opts.GetUlong("seed", 1000),
            _ => throw new ArgumentException($"Unknown lane '{lane}' (expected train, val or raw)."),
        };

        if (!File.Exists(path))
        {
            Console.WriteLine($"Genome file not found: {path}");
            Environment.ExitCode = 1;
            return;
        }

        var champion = ChampionLoader.Load(path);
        var species = ResolveSpecies(opts.GetValueOrDefault("species", "black-garden-ant"));
        bool nearFood = opts.GetValueOrDefault("food", "near") != "far";
        var config = BuildConfig(ticks, nearFood);
        var runner = new EpisodeRunner();

        Console.WriteLine($"Evaluating {champion.Label} | species {species.Id} | food {(nearFood ? "near" : "far")} | " +
                          $"lane {lane} (runseed={runSeed}) base={baseSeed}, {episodes} episodes of {ticks} ticks.");
        Console.WriteLine($"{"ep",4}  {"seed",13}  {"delivered",10}  {"pickedUp",9}  {"alive",6}");

        double delivered = 0, pickedUp = 0;
        for (int i = 0; i < episodes; i++)
        {
            ulong seed = baseSeed + (ulong)i;
            var result = runner.Run(config, species, champion.BrainFactory, champion.Body, champion.Plasticity, seed);
            delivered += result.FoodDelivered;
            pickedUp += result.FoodPickedUp;
            Console.WriteLine($"{i + 1,4}  {seed,13}  {result.FoodDelivered,10}  {result.FoodPickedUp,9}  {result.AntsAlive,6}");
        }

        double ceiling = (double)config.FoodSourceCount * config.FoodPerSource;
        double meanDelivered = delivered / episodes;
        Console.WriteLine($"Mean delivered: {meanDelivered:F2} of {ceiling:F0} available per episode " +
                          $"({100.0 * meanDelivered / ceiling:F1}% of the food on the map).");
        Console.WriteLine($"Mean fitness (delivered + 0.05*pickedUp): {(delivered + 0.05 * pickedUp) / episodes:F2}");
    }

    // ------------------------------------------------------------------ config

    /// <summary>World config from CLI options (the food curriculum distance follows `food=near|far`).</summary>
    private static WorldConfig BuildConfig(int ticks, bool nearFood)
        => new()
        {
            EpisodeTicks = ticks,
            MinFoodDistanceFromNest = nearFood ? 120f : 250f,
            MaxFoodDistanceFromNest = nearFood ? 220f : 350f,
        };

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
              demo      species=black-garden-ant ticks=4000 seed=1 [replay=replay.json] [brain=champion.neat] [food=near|far]
              bench     ticks=4000 runs=3
              evolve    gens=100 popsize=128 seed=12345 [out=output] [species=...] [episodes=2] [ticks=4000] [workers=150]
                        [valepisodes=3] [valinterval=5]
              evaluate  path=output/champion.neat [lane=val|train|raw] [runseed=12345] [episodes=3] [ticks=4000]
                        [food=near|far] [seed=N (lane=raw only)] [species=...]
            """);
    }
}
