namespace AntSim.Core;

/// <summary>
/// Evolvable body parameters. These co-evolve with the NEAT brains in a small parallel GA:
/// one genome per colony, so castes/species diverge naturally (speed, senses, lifespan...).
/// </summary>
public sealed record AntBody
{
    /// <summary>Maximum movement speed, world units per second.</summary>
    public float Speed { get; init; } = 12f;

    /// <summary>Distance of the antenna sensor samples, world units.</summary>
    public float SensorRange { get; init; } = 26f;

    /// <summary>Angle between the centre and side antennae, radians.</summary>
    public float SensorSpread { get; init; } = 0.55f;

    /// <summary>Pheromone units laid per second at deposit action = 1.</summary>
    public float DepositRate { get; init; } = 1f;

    /// <summary>Natural death age, simulated seconds (energy is currently modelled as remaining lifespan).</summary>
    public float LifespanSeconds { get; init; } = 900f;

    /// <summary>Gaussian mutation with per-gene clamping. Used by the body-parameter GA.</summary>
    public AntBody Mutated(Prng prng, double sigma)
    {
        float Gene(float v, double lo, double hi)
            => WorldMath.Clamp((float)(v * Math.Exp(sigma * prng.NextGaussian())), (float)lo, (float)hi);

        return new AntBody
        {
            Speed = Gene(Speed, 3, 40),
            SensorRange = Gene(SensorRange, 8, 80),
            SensorSpread = Gene(SensorSpread, 0.15, 1.4),
            DepositRate = Gene(DepositRate, 0.2, 4),
            LifespanSeconds = Gene(LifespanSeconds, 300, 4000),
        };
    }
}

/// <summary>
/// Per-ant modulatory (plasticity) parameters, inherited through the colony genome.
/// Ants tweak their own responsiveness online during life; successful settings propagate
/// because the whole vector is subject to selection like the body parameters.
/// </summary>
public sealed record PlasticityVector
{
    /// <summary>Per-tick rate at which the food-trail sensitivity tracks current exposure. Range [0.001, 0.2].</summary>
    public float TrailHabituationRate { get; init; } = 0.02f;

    /// <summary>Maximum fraction of sensitivity that habituation can remove. Range [0, 0.9].</summary>
    public float TrailHabituationMax { get; init; } = 0.4f;

    public PlasticityVector Mutated(Prng prng, double sigma)
    {
        return new PlasticityVector
        {
            TrailHabituationRate = WorldMath.Clamp((float)(TrailHabituationRate * Math.Exp(sigma * prng.NextGaussian())), 0.001f, 0.2f),
            TrailHabituationMax = WorldMath.Clamp((float)(TrailHabituationMax * Math.Exp(sigma * prng.NextGaussian())), 0f, 0.9f),
        };
    }
}
