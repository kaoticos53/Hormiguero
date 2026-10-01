namespace AntSim.Core;

/// <summary>Outcome of one episode (one colony running for a fixed number of ticks).</summary>
public sealed record EpisodeResult(
    long FoodDelivered,
    long FoodPickedUp,
    long TicksRun,
    int AntsAlive,
    float FoodTrailTotal,
    float HomeTrailTotal)
{
    public double FoodDeliveredPerAnt => AntsAlive == 0 ? 0 : (double)FoodDelivered / AntsAlive;
}

/// <summary>Runs whole episodes of the simulation. Colony fitness = food delivered to the nest.</summary>
public sealed class EpisodeRunner
{
    public EpisodeResult Run(
        WorldConfig config,
        SpeciesDefinition species,
        IAntBrainFactory brainFactory,
        AntBody? body = null,
        PlasticityVector? plasticity = null,
        ulong seed = 1,
        ReplayRecorder? recorder = null)
    {
        var world = SimWorld.CreateSeeded(config, species, brainFactory, body, plasticity, seed);
        for (int t = 0; t < config.EpisodeTicks; t++)
        {
            world.Step();
            recorder?.Record(world);
        }

        int alive = 0;
        for (int i = 0; i < world.Ants.Count; i++)
            if (world.Ants[i].Alive) alive++;

        return new EpisodeResult(
            world.FoodDelivered,
            world.FoodPickedUp,
            world.Tick,
            alive,
            world.FoodTrailField.ComputeTotal(),
            world.HomeTrailField.ComputeTotal());
    }
}

/// <summary>One replay snapshot: positions/headings of all ants at a sampled tick.</summary>
public sealed record ReplayFrame(int Tick, float[] X, float[] Y, float[] Heading, byte[] Carrying, float[] FoodRemaining)
{
    /// <summary>Cumulative food delivered to the nest at this tick (for HUD/replay stats).</summary>
    public long Delivered { get; init; }

    /// <summary>Number of living ants at this tick.</summary>
    public int Alive { get; init; }
}

/// <summary>Food pile snapshot for replay viewers (piles are static; only Amount depletes).</summary>
public sealed record ReplayFood(float X, float Y, float Amount, float InitialAmount);

/// <summary>Complete replay of an episode for later viewing (e.g. in the Unity viewer).</summary>
public sealed record ReplayData(
    string SpeciesId,
    WorldConfig Config,
    int SampleInterval,
    long FoodDelivered,
    IReadOnlyList<ReplayFrame> Frames,
    ulong Seed,
    IReadOnlyList<ReplayFood> FoodSources);

/// <summary>Samples world state every N ticks into a bounded-memory replay.</summary>
public sealed class ReplayRecorder
{
    private readonly int _sampleInterval;
    private readonly List<ReplayFrame> _frames = [];
    private readonly List<ReplayFood> _initialFoodSources = [];

    public ReplayRecorder(int sampleInterval = 10)
    {
        if (sampleInterval < 1) throw new ArgumentOutOfRangeException(nameof(sampleInterval));
        _sampleInterval = sampleInterval;
    }

    public IReadOnlyList<ReplayFrame> Frames => _frames;

    public void Record(SimWorld world)
    {
        if (_initialFoodSources.Count == 0)
        {
            for (int i = 0; i < world.FoodSources.Count; i++)
            {
                var food = world.FoodSources[i];
                _initialFoodSources.Add(new ReplayFood(food.X, food.Y, food.Amount, food.Amount));
            }
        }

        if (world.Tick % _sampleInterval != 0) return;
        var ants = world.Ants;
        int n = ants.Count;
        var x = new float[n];
        var y = new float[n];
        var h = new float[n];
        var c = new byte[n];
        var foodRemaining = new float[world.FoodSources.Count];
        int alive = 0;
        for (int i = 0; i < n; i++)
        {
            var ant = ants[i];
            x[i] = ant.X;
            y[i] = ant.Y;
            h[i] = ant.Heading;
            c[i] = ant.FoodCarried > 0f ? (byte)1 : (byte)0;
            if (ant.Alive) alive++;
        }
        for (int i = 0; i < foodRemaining.Length; i++) foodRemaining[i] = world.FoodSources[i].Amount;
        _frames.Add(new ReplayFrame((int)world.Tick, x, y, h, c, foodRemaining)
        {
            Delivered = world.FoodDelivered,
            Alive = alive,
        });
    }

    public ReplayData ToReplayData(SimWorld world, int sampleInterval)
        => new(
            world.Species.Id,
            world.Config,
            sampleInterval,
            world.FoodDelivered,
            _frames,
            world.Seed,
            world.FoodSources.Select((f, i) =>
                _initialFoodSources.Count == world.FoodSources.Count
                    ? new ReplayFood(f.X, f.Y, f.Amount, _initialFoodSources[i].InitialAmount)
                    : new ReplayFood(f.X, f.Y, f.Amount, f.Amount)).ToArray());
}
