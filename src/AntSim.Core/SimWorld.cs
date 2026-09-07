namespace AntSim.Core;

/// <summary>
/// One simulation world: a toroidal 2D arena with a nest, food piles, pheromone fields and a
/// colony of ants. Fully deterministic: same config + species + seed => identical trajectory,
/// on any machine, because all randomness flows from the seeded <see cref="Prng"/> in fixed order.
/// </summary>
public sealed class SimWorld
{
    private readonly WorldConfig _config;
    private readonly SpeciesDefinition _species;
    private readonly Prng _rng;
    private readonly PheromoneField[] _fields;
    private readonly int _foodChannel;
    private readonly int _homeChannel;
    private readonly SpatialHash<Ant> _antHash;
    private readonly float _pickupRadiusSq;

    public WorldConfig Config => _config;
    public SpeciesDefinition Species => _species;
    public Nest Nest { get; }
    public List<Ant> Ants { get; } = [];
    public List<FoodSource> FoodSources { get; } = [];
    public PheromoneField FoodTrailField => _fields[_foodChannel];
    public PheromoneField HomeTrailField => _fields[_homeChannel];
    public SpatialHash<Ant> AntHash => _antHash;

    public long Tick { get; private set; }
    public long FoodDelivered { get; private set; }
    public long FoodPickedUp { get; private set; }

    /// <summary>Total food units still remaining in the world's piles.</summary>
    public float FoodRemaining
    {
        get
        {
            float sum = 0;
            for (int i = 0; i < FoodSources.Count; i++)
                sum += FoodSources[i].Amount;
            return sum;
        }
    }

    private SimWorld(WorldConfig config, SpeciesDefinition species, ulong seed)
    {
        if (!species.Implemented)
            throw new NotSupportedException($"Species '{species.Id}' is defined but its mechanics are not implemented yet (see roadmap).");

        _config = config;
        _species = species;
        _rng = new Prng(seed);
        _pickupRadiusSq = config.PickupRadius * config.PickupRadius;
        _antHash = new SpatialHash<Ant>(config.Width, config.Height, MathF.Max(16f, config.PickupRadius * 2f));

        _fields = new PheromoneField[species.PheromoneChannels.Count];
        _foodChannel = _homeChannel = -1;
        for (int i = 0; i < _fields.Length; i++)
        {
            var def = species.PheromoneChannels[i];
            _fields[i] = new PheromoneField(def, config.Width, config.Height, config.CellSize);
            if (def.Name == "FoodTrail") _foodChannel = i;
            if (def.Name == "HomeTrail") _homeChannel = i;
        }
        if (_foodChannel < 0 || _homeChannel < 0)
            throw new ArgumentException($"Species '{species.Id}' must define 'FoodTrail' and 'HomeTrail' pheromone channels for the current sensor schema.");

        Nest = new Nest { X = config.Width * 0.5f, Y = config.Height * 0.5f, Radius = config.NestRadius };
    }

    /// <summary>Build a fully seeded world with food piles and a colony of workers.</summary>
    public static SimWorld CreateSeeded(
        WorldConfig config,
        SpeciesDefinition species,
        IAntBrainFactory brainFactory,
        AntBody? body = null,
        PlasticityVector? plasticity = null,
        ulong seed = 1)
    {
        var world = new SimWorld(config, species, seed);
        world.SpawnFood();
        world.SpawnAnts(brainFactory, body ?? new AntBody(), plasticity ?? new PlasticityVector());
        return world;
    }

    private void SpawnFood()
    {
        float minDistSq = _config.MinFoodDistanceFromNest * _config.MinFoodDistanceFromNest;
        float maxDistSq = _config.MaxFoodDistanceFromNest * _config.MaxFoodDistanceFromNest;

        for (int i = 0; i < _config.FoodSourceCount; i++)
        {
            for (int attempt = 0; attempt < 200; attempt++)
            {
                float x = _rng.NextFloat() * _config.Width;
                float y = _rng.NextFloat() * _config.Height;
                float dSq = WorldMath.TorusDistanceSq(x, y, Nest.X, Nest.Y, _config.Width, _config.Height);
                if (dSq >= minDistSq && dSq <= maxDistSq)
                {
                    FoodSources.Add(new FoodSource { X = x, Y = y, Amount = _config.FoodPerSource });
                    break;
                }
            }
        }
    }

    private void SpawnAnts(IAntBrainFactory brainFactory, AntBody body, PlasticityVector plasticity)
    {
        for (int i = 0; i < _config.WorkerCount; i++)
        {
            float ang = _rng.NextAngle();
            float dist = _rng.NextFloat() * Nest.Radius * 0.5f;
            Ants.Add(new Ant
            {
                Id = i,
                Caste = Caste.Worker,
                Brain = brainFactory.CreateBrain(Caste.Worker, i, _rng),
                Body = body,
                Plasticity = plasticity,
                X = WorldMath.WrapCoord(Nest.X + MathF.Cos(ang) * dist, _config.Width),
                Y = WorldMath.WrapCoord(Nest.Y + MathF.Sin(ang) * dist, _config.Height),
                Heading = _rng.NextAngle(),
            });
        }
    }

    /// <summary>Advance the world by one tick.</summary>
    public void Step()
    {
        Tick++;
        float dt = _config.DeltaTime;
        bool diffuse = Tick % _config.DiffusionInterval == 0;
        for (int i = 0; i < _fields.Length; i++)
            _fields[i].Update(dt, diffuse);

        // Rebuild the neighbour index, then sense -> think -> act for every ant in fixed order.
        _antHash.Clear();
        var ants = Ants;
        for (int i = 0; i < ants.Count; i++)
        {
            if (ants[i].Alive) _antHash.Insert(ants[i], ants[i].X, ants[i].Y);
        }

        for (int i = 0; i < ants.Count; i++)
        {
            var ant = ants[i];
            if (!ant.Alive) continue;
            SenseAndThink(ant);
            Act(ant, dt);
        }
    }

    private void SenseAndThink(Ant ant)
    {
        var body = ant.Body;
        float spread = body.SensorSpread;
        float range = body.SensorRange;
        float foodMax = FoodTrailField.MaxLevel;
        float homeMax = HomeTrailField.MaxLevel;

        float foodL = SampleChannel(_foodChannel, ant, +spread, range) / foodMax;
        float foodC = SampleChannel(_foodChannel, ant, 0f, range) / foodMax;
        float foodR = SampleChannel(_foodChannel, ant, -spread, range) / foodMax;
        float homeL = SampleChannel(_homeChannel, ant, +spread, range) / homeMax;
        float homeC = SampleChannel(_homeChannel, ant, 0f, range) / homeMax;
        float homeR = SampleChannel(_homeChannel, ant, -spread, range) / homeMax;

        // Online plasticity: repeated strong food-trail exposure habituates the ant's sensitivity.
        float exposure = ant.FoodTrailExposure;
        exposure += (MathF.Min(1f, foodC) - exposure) * ant.Plasticity.TrailHabituationRate;
        ant.FoodTrailExposure = exposure;
        float foodSensitivity = 1f - ant.Plasticity.TrailHabituationMax * exposure;
        foodL *= foodSensitivity;
        foodC *= foodSensitivity;
        foodR *= foodSensitivity;

        // Nest bearing relative to heading.
        float nx = WorldMath.TorusDelta(ant.X, Nest.X, _config.Width);
        float ny = WorldMath.TorusDelta(ant.Y, Nest.Y, _config.Height);
        float nestRel = WorldMath.AngleDelta(ant.Heading, MathF.Atan2(ny, nx));

        // Nearest food pile within detection range (2x the antenna range).
        float detectRange = range * 2f;
        float bestDistSq = detectRange * detectRange;
        float foodRel = 0f;
        float foodVisible = 0f;
        var sources = FoodSources;
        for (int i = 0; i < sources.Count; i++)
        {
            var f = sources[i];
            if (f.Amount < _config.FoodPickupAmount) continue;
            float dx = WorldMath.TorusDelta(ant.X, f.X, _config.Width);
            float dy = WorldMath.TorusDelta(ant.Y, f.Y, _config.Height);
            float dSq = dx * dx + dy * dy;
            if (dSq < bestDistSq)
            {
                bestDistSq = dSq;
                foodRel = WorldMath.AngleDelta(ant.Heading, MathF.Atan2(dy, dx));
                foodVisible = 1f;
            }
        }

        var sensors = new AntSensors
        {
            FoodTrailLeft = foodL,
            FoodTrailCenter = foodC,
            FoodTrailRight = foodR,
            HomeTrailLeft = homeL,
            HomeTrailCenter = homeC,
            HomeTrailRight = homeR,
            NestDirSin = MathF.Sin(nestRel),
            NestDirCos = MathF.Cos(nestRel),
            FoodDirSin = MathF.Sin(foodRel),
            FoodDirCos = MathF.Cos(foodRel),
            FoodVisible = foodVisible,
            EnergyNorm = 1f - MathF.Min(1f, ant.Age / MathF.Max(1f, body.LifespanSeconds)),
            Carrying = ant.FoodCarried > 0f ? 1f : 0f,
            RandomNoise = _rng.NextSignedFloat(),
        };

        ant.Actions = default;
        ant.Brain.Think(sensors, ref ant.Actions);
    }

    private float SampleChannel(int channel, Ant ant, float angleOffset, float range)
    {
        float a = ant.Heading + angleOffset;
        float x = ant.X + MathF.Cos(a) * range;
        float y = ant.Y + MathF.Sin(a) * range;
        return _fields[channel].Sample(x, y);
    }

    private void Act(Ant ant, float dt)
    {
        var a = ant.Actions;

        // Move.
        ant.Heading = WorldMath.WrapAngle(ant.Heading + WorldMath.Clamp(a.Turn, -1f, 1f) * _config.TurnRate * dt);
        float speed = WorldMath.Clamp(a.Speed, 0f, 1f) * ant.Body.Speed * dt;
        ant.X = WorldMath.WrapCoord(ant.X + MathF.Cos(ant.Heading) * speed, _config.Width);
        ant.Y = WorldMath.WrapCoord(ant.Y + MathF.Sin(ant.Heading) * speed, _config.Height);

        // Age / energy.
        ant.Age += dt;
        if (ant.Age > ant.Body.LifespanSeconds)
        {
            ant.Alive = false;
            return;
        }

        // Pheromone deposit: homing ants lay the food trail, outbound ants lay the home trail.
        float deposit = WorldMath.Clamp(a.PheromoneDeposit, 0f, 1f) * ant.Body.DepositRate * dt;
        if (deposit > 0f)
            _fields[ant.FoodCarried > 0f ? _foodChannel : _homeChannel].Deposit(ant.X, ant.Y, deposit);

        // Reflexes, hardwired rather than learned (real ants grab food on contact and
        // regurgitate at the nest on arrival): pickup on touching a pile, drop at the nest.
        if (ant.FoodCarried <= 0f)
        {
            var sources = FoodSources;
            for (int i = 0; i < sources.Count; i++)
            {
                var f = sources[i];
                if (f.Amount < _config.FoodPickupAmount) continue;
                float dx = WorldMath.TorusDelta(ant.X, f.X, _config.Width);
                float dy = WorldMath.TorusDelta(ant.Y, f.Y, _config.Height);
                if (dx * dx + dy * dy <= _pickupRadiusSq)
                {
                    f.Amount -= _config.FoodPickupAmount;
                    ant.FoodCarried = _config.FoodPickupAmount;
                    FoodPickedUp++;
                    break;
                }
            }
        }
        else
        {
            float dx = WorldMath.TorusDelta(ant.X, Nest.X, _config.Width);
            float dy = WorldMath.TorusDelta(ant.Y, Nest.Y, _config.Height);
            float r = Nest.Radius;
            if (dx * dx + dy * dy <= r * r)
            {
                ant.FoodCarried = 0f;
                FoodDelivered++;
            }
        }
    }
}
