using System;
using System.Collections.Generic;
using UnityEngine;

namespace AntSimViewer
{
    /// <summary>Ant sensor readings for one tick; mirrors AntSim.Core.AntSensors.</summary>
    public sealed class SensorSample
    {
        public float FoodTrailLeft;
        public float FoodTrailCenter;
        public float FoodTrailRight;
        public float HomeTrailLeft;
        public float HomeTrailCenter;
        public float HomeTrailRight;
        public float NestDirSin;
        public float NestDirCos;
        public float FoodDirSin;
        public float FoodDirCos;
        public float FoodVisible;
        public float EnergyNorm;
        public float Carrying;
        public float RandomNoise;
    }

    /// <summary>Motor actions computed by the brain for one tick; mirrors AntSim.Core.AntActions.</summary>
    public sealed class MotorAction
    {
        public float Turn;
        public float Speed;
        public float PheromoneDeposit;
    }

    /// <summary>One ant's mutable simulation state.</summary>
    public sealed class AntRuntime
    {
        public int Id;
        public float X;
        public float Y;
        public float Heading;
        public float FoodCarried;
        public float Age;
        public bool Alive = true;
        public float FoodTrailExposure;
        public float WanderBias; // baseline brain state (smoothed random walk)
        public MotorAction Action = new MotorAction();
    }

    /// <summary>A pile of food; ants deplete it by picking up.</summary>
    public sealed class FoodRuntime
    {
        public float X;
        public float Y;
        public float Amount;
    }

    /// <summary>The colony's home: dropping food inside the radius scores a point.</summary>
    public sealed class NestRuntime
    {
        public float X;
        public float Y;
        public float Radius;
    }

    /// <summary>A per-ant decision module: maps this tick's sensors to motor actions.</summary>
    public interface IAntBrainRuntime
    {
        void Think(SensorSample sensors, MotorAction actions, AntRuntime ant);
    }

    /// <summary>
    /// Port of AntSim.Core.HandcodedForagerBrain: the hand-coded pheromone-following forager
    /// baseline. Outbound ants lay the home trail and follow the food trail / explore outward;
    /// ants carrying food head straight home while laying the food trail.
    /// </summary>
    public sealed class BaselineForagerBrainRuntime : IAntBrainRuntime
    {
        public void Think(SensorSample s, MotorAction a, AntRuntime ant)
        {
            if (s.Carrying > 0.5f)
            {
                // Homing phase: steer to the nest, lay food trail; drop happens on arrival (reflex).
                a.Turn = WorldMathRuntime.Clamp(s.NestDirSin * 4f, -1f, 1f);
                a.Speed = 0.85f;
                a.PheromoneDeposit = 1f;
                return;
            }

            // Foraging phase: lay home trail while searching.
            a.PheromoneDeposit = 0.3f;

            // Visible food: head straight for it.
            if (s.FoodVisible > 0.5f)
            {
                a.Turn = WorldMathRuntime.Clamp(s.FoodDirSin * 4f, -1f, 1f);
                a.Speed = 1f;
                return;
            }

            // Follow the food-trail gradient via the antennae.
            float l = s.FoodTrailLeft, c = s.FoodTrailCenter, r = s.FoodTrailRight;
            if (l + c + r > 0.02f)
            {
                float steer = (l - r) * 8f;
                if (c >= Mathf.Max(l, r)) steer *= 0.35f; // trail straight ahead: keep going
                a.Turn = WorldMathRuntime.Clamp(steer, -1f, 1f);
                a.Speed = 0.75f;
                return;
            }

            // Nothing to follow: explore. Smoothed random walk biased away from the nest.
            ant.WanderBias = 0.9f * ant.WanderBias + 0.1f * s.RandomNoise;
            a.Turn = WorldMathRuntime.Clamp(-s.NestDirSin * 3f + ant.WanderBias * 0.8f, -1f, 1f);
            a.Speed = 0.7f;
        }
    }

    /// <summary>
    /// Adapts a loaded champion.json brain (NeuralNetRuntime) to the ant brain interface,
    /// mirroring AntSim.Evolution.NeatBrain: sensors into inputs[0..13], constant bias 1.0 at
    /// inputs[14], outputs clamped to turn/speed/pheromone-deposit.
    /// </summary>
    public sealed class NeatBrainRuntime : IAntBrainRuntime
    {
        readonly NeuralNetRuntime _net;
        readonly double[] _inputs;
        readonly double[] _outputs;

        public NeatBrainRuntime(BrainData brain)
        {
            _net = new NeuralNetRuntime(brain);
            _inputs = new double[_net.InputCount];
            _outputs = new double[_net.OutputCount];
        }

        public NeuralNetRuntime Net()
        {
            return _net;
        }

        public void Think(SensorSample s, MotorAction a, AntRuntime ant)
        {
            SetInput(0, s.FoodTrailLeft);
            SetInput(1, s.FoodTrailCenter);
            SetInput(2, s.FoodTrailRight);
            SetInput(3, s.HomeTrailLeft);
            SetInput(4, s.HomeTrailCenter);
            SetInput(5, s.HomeTrailRight);
            SetInput(6, s.NestDirSin);
            SetInput(7, s.NestDirCos);
            SetInput(8, s.FoodDirSin);
            SetInput(9, s.FoodDirCos);
            SetInput(10, s.FoodVisible);
            SetInput(11, s.EnergyNorm);
            SetInput(12, s.Carrying);
            SetInput(13, s.RandomNoise);
            SetInput(14, 1.0); // constant bias input

            _net.Activate(_inputs, _outputs);

            a.Turn = _outputs.Length > 0 ? WorldMathRuntime.Clamp((float)_outputs[0], -1f, 1f) : 0f;
            a.Speed = _outputs.Length > 1 ? WorldMathRuntime.Clamp((float)_outputs[1], 0f, 1f) : 0f;
            a.PheromoneDeposit = _outputs.Length > 2 ? WorldMathRuntime.Clamp((float)_outputs[2], 0f, 1f) : 0f;
        }

        void SetInput(int index, double value)
        {
            if (index < _inputs.Length) _inputs[index] = value;
        }
        }
    }

    /// <summary>
    /// Port of AntSim.Core.SimWorld: one deterministic simulation world — toroidal 2D arena,
    /// nest, food piles, two pheromone channels (FoodTrail / HomeTrail) and a colony of ants.
    /// All randomness flows from the seeded SplitMix64 PRNG in fixed order, so the live view
    /// reproduces the headless simulation's behaviour for the same config + seed + brain.
    /// </summary>
    public sealed class SimWorldRuntime
    {
        // Black-garden-ant pheromone channels (AntSim.Core.SpeciesCatalog).
        static readonly float FoodTrailMax = 30f;
        static readonly float FoodTrailEvap = 0.02f;
        static readonly float FoodTrailDiffusion = 0.12f;
        static readonly float HomeTrailMax = 15f;
        static readonly float HomeTrailEvap = 0.035f;
        static readonly float HomeTrailDiffusion = 0.12f;

        public readonly WorldConfigData Config;
        public readonly AntBodyData Body;
        public readonly PlasticityData Plasticity;
        public readonly ulong Seed;
        public readonly IAntBrainRuntime Brain;

        public readonly NestRuntime Nest = new NestRuntime();
        public readonly List<FoodRuntime> Food = new List<FoodRuntime>();
        public readonly List<AntRuntime> Ants = new List<AntRuntime>();

        public readonly PheromoneFieldRuntime FoodTrailField;
        public readonly PheromoneFieldRuntime HomeTrailField;

        public long Tick;
        public long FoodDelivered;
        public long FoodPickedUp;

        readonly PrngRuntime _rng;
        readonly float _pickupRadiusSq;

        public SimWorldRuntime(
            WorldConfigData config,
            AntBodyData body,
            PlasticityData plasticity,
            IAntBrainRuntime brain,
            ulong seed)
        {
            Config = config;
            Body = body;
            Plasticity = plasticity;
            Seed = seed;
            Brain = brain;
            _rng = new PrngRuntime(seed);
            _pickupRadiusSq = config.PickupRadius * config.PickupRadius;

            int cols = Math.Max(1, (int)Mathf.Ceil(config.Width / config.CellSize));
            int rows = Math.Max(1, (int)Mathf.Ceil(config.Height / config.CellSize));
            FoodTrailField = new PheromoneFieldRuntime(config.CellSize, cols, rows, FoodTrailMax, FoodTrailEvap, FoodTrailDiffusion);
            HomeTrailField = new PheromoneFieldRuntime(config.CellSize, cols, rows, HomeTrailMax, HomeTrailEvap, HomeTrailDiffusion);

            Nest.X = config.Width * 0.5f;
            Nest.Y = config.Height * 0.5f;
            Nest.Radius = config.NestRadius;

            SpawnFood();
            SpawnAnts();
        }

        void SpawnFood()
        {
            float minDistSq = Config.MinFoodDistanceFromNest * Config.MinFoodDistanceFromNest;
            float maxDistSq = Config.MaxFoodDistanceFromNest * Config.MaxFoodDistanceFromNest;

            for (int i = 0; i < Config.FoodSourceCount; i++)
            {
                for (int attempt = 0; attempt < 200; attempt++)
                {
                    float x = _rng.NextFloat() * Config.Width;
                    float y = _rng.NextFloat() * Config.Height;
                    float dSq = WorldMathRuntime.TorusDistanceSq(x, y, Nest.X, Nest.Y, Config.Width, Config.Height);
                    if (dSq >= minDistSq && dSq <= maxDistSq)
                    {
                        FoodRuntime f = new FoodRuntime();
                        f.X = x;
                        f.Y = y;
                        f.Amount = Config.FoodPerSource;
                        Food.Add(f);
                        break;
                    }
                }
            }
        }

        void SpawnAnts()
        {
            for (int i = 0; i < Config.WorkerCount; i++)
            {
                float ang = _rng.NextAngle();
                float dist = _rng.NextFloat() * Nest.Radius * 0.5f;
                AntRuntime ant = new AntRuntime();
                ant.Id = i;
                ant.X = WorldMathRuntime.WrapCoord(Nest.X + Mathf.Cos(ang) * dist, Config.Width);
                ant.Y = WorldMathRuntime.WrapCoord(Nest.Y + Mathf.Sin(ang) * dist, Config.Height);
                ant.Heading = _rng.NextAngle();
                Ants.Add(ant);
            }
        }

        /// <summary>Advance the world by one tick.</summary>
        public void Step()
        {
            Tick++;
            float dt = Config.DeltaTime;
            bool diffuse = Tick % Config.DiffusionInterval == 0;
            FoodTrailField.Update(dt, diffuse);
            HomeTrailField.Update(dt, diffuse);

            // Sense -> think -> act for every ant in fixed order.
            for (int i = 0; i < Ants.Count; i++)
            {
                AntRuntime ant = Ants[i];
                if (!ant.Alive) continue;
                SenseAndThink(ant);
                Act(ant, dt);
            }
        }

        void SenseAndThink(AntRuntime ant)
        {
            float spread = Body.SensorSpread;
            float range = Body.SensorRange;

            float foodL = SampleChannel(FoodTrailField, ant, +spread, range) / FoodTrailMax;
            float foodC = SampleChannel(FoodTrailField, ant, 0f, range) / FoodTrailMax;
            float foodR = SampleChannel(FoodTrailField, ant, -spread, range) / FoodTrailMax;
            float homeL = SampleChannel(HomeTrailField, ant, +spread, range) / HomeTrailMax;
            float homeC = SampleChannel(HomeTrailField, ant, 0f, range) / HomeTrailMax;
            float homeR = SampleChannel(HomeTrailField, ant, -spread, range) / HomeTrailMax;

            // Online plasticity: repeated strong food-trail exposure habituates sensitivity.
            float exposure = ant.FoodTrailExposure;
            exposure += (Mathf.Min(1f, foodC) - exposure) * Plasticity.TrailHabituationRate;
            ant.FoodTrailExposure = exposure;
            float foodSensitivity = 1f - Plasticity.TrailHabituationMax * exposure;
            foodL *= foodSensitivity;
            foodC *= foodSensitivity;
            foodR *= foodSensitivity;

            // Nest bearing relative to heading.
            float nx = WorldMathRuntime.TorusDelta(ant.X, Nest.X, Config.Width);
            float ny = WorldMathRuntime.TorusDelta(ant.Y, Nest.Y, Config.Height);
            float nestRel = WorldMathRuntime.AngleDelta(ant.Heading, Mathf.Atan2(ny, nx));

            // Nearest food pile within detection range (2x the antenna range).
            float detectRange = range * 2f;
            float bestDistSq = detectRange * detectRange;
            float foodRel = 0f;
            float foodVisible = 0f;
            for (int i = 0; i < Food.Count; i++)
            {
                FoodRuntime f = Food[i];
                if (f.Amount < Config.FoodPickupAmount) continue;
                float dx = WorldMathRuntime.TorusDelta(ant.X, f.X, Config.Width);
                float dy = WorldMathRuntime.TorusDelta(ant.Y, f.Y, Config.Height);
                float dSq = dx * dx + dy * dy;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    foodRel = WorldMathRuntime.AngleDelta(ant.Heading, Mathf.Atan2(dy, dx));
                    foodVisible = 1f;
                }
            }

            SensorSample s = new SensorSample();
            s.FoodTrailLeft = foodL;
            s.FoodTrailCenter = foodC;
            s.FoodTrailRight = foodR;
            s.HomeTrailLeft = homeL;
            s.HomeTrailCenter = homeC;
            s.HomeTrailRight = homeR;
            s.NestDirSin = Mathf.Sin(nestRel);
            s.NestDirCos = Mathf.Cos(nestRel);
            s.FoodDirSin = Mathf.Sin(foodRel);
            s.FoodDirCos = Mathf.Cos(foodRel);
            s.FoodVisible = foodVisible;
            s.EnergyNorm = 1f - Mathf.Min(1f, ant.Age / Mathf.Max(1f, Body.LifespanSeconds));
            s.Carrying = ant.FoodCarried > 0f ? 1f : 0f;
            s.RandomNoise = _rng.NextSignedFloat();

            MotorAction a = ant.Action;
            a.Turn = 0f;
            a.Speed = 0f;
            a.PheromoneDeposit = 0f;
            Brain.Think(s, a, ant);
        }

        float SampleChannel(PheromoneFieldRuntime field, AntRuntime ant, float angleOffset, float range)
        {
            float a = ant.Heading + angleOffset;
            float x = ant.X + Mathf.Cos(a) * range;
            float y = ant.Y + Mathf.Sin(a) * range;
            return field.Sample(x, y);
        }

        void Act(AntRuntime ant, float dt)
        {
            MotorAction a = ant.Action;

            // Move.
            ant.Heading = WorldMathRuntime.WrapAngle(ant.Heading + WorldMathRuntime.Clamp(a.Turn, -1f, 1f) * Config.TurnRate * dt);
            float speed = WorldMathRuntime.Clamp(a.Speed, 0f, 1f) * Body.Speed * dt;
            ant.X = WorldMathRuntime.WrapCoord(ant.X + Mathf.Cos(ant.Heading) * speed, Config.Width);
            ant.Y = WorldMathRuntime.WrapCoord(ant.Y + Mathf.Sin(ant.Heading) * speed, Config.Height);

            // Age / energy.
            ant.Age += dt;
            if (ant.Age > Body.LifespanSeconds)
            {
                ant.Alive = false;
                return;
            }

            // Pheromone deposit: homing ants lay the food trail, outbound ants lay the home trail.
            float deposit = WorldMathRuntime.Clamp(a.PheromoneDeposit, 0f, 1f) * Body.DepositRate * dt;
            if (deposit > 0f)
            {
                if (ant.FoodCarried > 0f)
                    FoodTrailField.Deposit(ant.X, ant.Y, deposit);
                else
                    HomeTrailField.Deposit(ant.X, ant.Y, deposit);
            }

            // Reflexes: pickup on touching a pile, drop at the nest.
            if (ant.FoodCarried <= 0f)
            {
                for (int i = 0; i < Food.Count; i++)
                {
                    FoodRuntime f = Food[i];
                    if (f.Amount < Config.FoodPickupAmount) continue;
                    float dx = WorldMathRuntime.TorusDelta(ant.X, f.X, Config.Width);
                    float dy = WorldMathRuntime.TorusDelta(ant.Y, f.Y, Config.Height);
                    if (dx * dx + dy * dy <= _pickupRadiusSq)
                    {
                        f.Amount -= Config.FoodPickupAmount;
                        ant.FoodCarried = Config.FoodPickupAmount;
                        FoodPickedUp++;
                        break;
                    }
                }
            }
            else
            {
                float dx = WorldMathRuntime.TorusDelta(ant.X, Nest.X, Config.Width);
                float dy = WorldMathRuntime.TorusDelta(ant.Y, Nest.Y, Config.Height);
                float r = Nest.Radius;
                if (dx * dx + dy * dy <= r * r)
                {
                    ant.FoodCarried = 0f;
                    FoodDelivered++;
                }
            }
        }
    }
}