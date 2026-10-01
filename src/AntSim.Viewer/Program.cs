using System.Text.Json;
using AntSim.Core;
using AntSim.Evolution;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using SharpNeat;
using SharpNeat.Evaluation;
using SharpNeat.Neat.Genome;
using SharpNeat.Neat.Genome.Double;
using SharpNeat.Neat.Genome.IO;

namespace AntSim.Viewer;

/// <summary>
/// Real-time graphical viewer for the ant simulation (OpenTK/OpenGL, cross-platform — the
/// "Unity or similar" viewer; the simulation core itself stays UI-free).
///
/// Options (key=value):
///   species=black-garden-ant   seed=1            brain=output/run/champion.neat
///   paused=1                   smoke=90          size=900
/// `brain` loads an evolved champion (champion.neat) and drives the colony with it instead
/// of the hand-coded baseline. `smoke` runs N frames and exits (integration self-test).
/// </summary>
public static class Program
{
    public static void Main(string[] args)
    {
        var opts = ParseArgs(args);
        var species = ResolveSpecies(opts.GetValueOrDefault("species", "black-garden-ant"));
        ulong seed = GetUlong(opts, "seed", 1);
        bool startPaused = opts.ContainsKey("paused");
        int smokeFrames = opts.TryGetValue("smoke", out var s) && int.TryParse(s, out var n) ? Math.Max(1, n) : 0;
        int size = opts.TryGetValue("size", out var sz) && int.TryParse(sz, out var px) ? Math.Clamp(px, 320, 2400) : 900;

        var config = new WorldConfig(); // full natural food distances (250–350) — the real look
        var (brainFactory, body, plasticity, brainLabel) = MakeBrainFactory(opts.GetValueOrDefault("brain"));

        var world = SimWorld.CreateSeeded(config, species, brainFactory, body, plasticity, seed: seed);

        Console.WriteLine($"""
            AntSim viewer — {species.DisplayName} | {config.WorkerCount} workers | seed {seed} | brain: {brainLabel}
              SPACE pause/reanudar   UP/DOWN velocidad x1..x16   R reiniciar   F rastros   ESC salir
            """);

        var gws = GameWindowSettings.Default;
        var nws = new NativeWindowSettings
        {
            ClientSize = new Vector2i(size, size),
            Title = "AntSim",
            APIVersion = new Version(3, 3),
            Profile = ContextProfile.Core,
            Flags = ContextFlags.ForwardCompatible,
            NumberOfSamples = 4,
        };

        try
        {
            using var window = new ViewerWindow(gws, nws, config, species, brainFactory, brainLabel, seed, world, startPaused, smokeFrames);
            window.Run();
        }
        catch (Exception ex) when (ex.Message.Contains("glfw", StringComparison.OrdinalIgnoreCase)
                                || ex.Message.Contains("OpenGL", StringComparison.OrdinalIgnoreCase)
                                || ex.InnerException is not null && ex.InnerException.Message.Contains("glfw", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"No se pudo inicializar OpenGL/Glfw: {ex.Message}");
            Environment.ExitCode = 1;
        }

        if (smokeFrames > 0)
        {
            Console.WriteLine($"SMOKE frames={smokeFrames} ticks={world.Tick} delivered={world.FoodDelivered} alive={CountAlive(world)}");
        }
    }

    private static (IAntBrainFactory Factory, AntBody? Body, PlasticityVector? Plasticity, string Label) MakeBrainFactory(string? brainPath)
    {
        if (brainPath is null)
            return (SpeciesCatalog.BlackGardenAnt.BaselineBrainFactory, null, null, "baseline (hand-coded)");

        if (!File.Exists(brainPath))
            throw new FileNotFoundException($"Champion genome not found: {brainPath}");

        var meta = AntEvolutionExperiment.CreateMetaNeatGenome();
        var genome = NeatGenomeLoader.Load<double>(brainPath, meta, 0);
        IGenomeDecoder<NeatGenome<double>, IBlackBox<double>> decoder =
            NeatGenomeDecoderFactory.CreateGenomeDecoder(meta.IsAcyclic, false);
        var box = decoder.Decode(genome);

        // Companion champion.json carries the colony's evolved body/plasticity parameters.
        string jsonPath = Path.ChangeExtension(brainPath, ".json");
        if (File.Exists(jsonPath))
        {
            var export = JsonSerializer.Deserialize<BrainExport>(File.ReadAllText(jsonPath));
            if (export is not null)
                return (new NeatBrainFactory(box), export.Body, export.Plasticity,
                        $"{Path.GetFileName(brainPath)} ({genome.ConnectionGenes.Length} conns) + evolved body");
        }

        return (new NeatBrainFactory(box), null, null,
                $"{Path.GetFileName(brainPath)} ({genome.ConnectionGenes.Length} conns)");
    }

    private static int CountAlive(SimWorld world)
    {
        int alive = 0;
        for (int i = 0; i < world.Ants.Count; i++)
            if (world.Ants[i].Alive) alive++;
        return alive;
    }

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
            if (eq > 0) map[arg[..eq]] = arg[(eq + 1)..];
        }
        return map;
    }

    private static ulong GetUlong(Dictionary<string, string> map, string key, ulong fallback)
        => map.TryGetValue(key, out var s) && ulong.TryParse(s, out var v) ? v : fallback;
}
