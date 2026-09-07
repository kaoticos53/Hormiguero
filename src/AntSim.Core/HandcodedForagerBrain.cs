namespace AntSim.Core;

/// <summary>
/// Hand-coded pheromone-following forager: the baseline that NEAT evolution must beat.
/// Behaviour model (classic trail-foraging loop):
///  - outbound ants lay the home trail and follow the food trail / explore outward,
///  - ants carrying food head straight home (path integration, like real ants) while laying the food trail,
///  - pick-up is attempted whenever foraging; drop is attempted whenever at the nest.
/// </summary>
public sealed class HandcodedForagerBrain : IAntBrain
{
    private float _wanderBias;

    public void Think(in AntSensors s, ref AntActions a)
    {
        if (s.Carrying > 0.5f)
        {
            // Homing phase: steer to the nest, lay food trail; drop happens on arrival (reflex).
            a.Turn = WorldMath.Clamp(s.NestDirSin * 4f, -1f, 1f);
            a.Speed = 0.85f;
            a.PheromoneDeposit = 1f;
            return;
        }

        // Foraging phase: lay home trail while searching.
        a.PheromoneDeposit = 0.3f;

        // Visible food: head straight for it.
        if (s.FoodVisible > 0.5f)
        {
            a.Turn = WorldMath.Clamp(s.FoodDirSin * 4f, -1f, 1f);
            a.Speed = 1f;
            return;
        }

        // Follow the food-trail gradient via the antennae.
        float l = s.FoodTrailLeft, c = s.FoodTrailCenter, r = s.FoodTrailRight;
        if (l + c + r > 0.02f)
        {
            float steer = (l - r) * 8f;
            if (c >= MathF.Max(l, r)) steer *= 0.35f; // trail straight ahead: keep going
            a.Turn = WorldMath.Clamp(steer, -1f, 1f);
            a.Speed = 0.75f;
            return;
        }

        // Nothing to follow: explore. Smoothed random walk biased away from the nest so
        // that search radiates outward from home (where trails then start).
        _wanderBias = 0.9f * _wanderBias + 0.1f * s.RandomNoise;
        a.Turn = WorldMath.Clamp(-s.NestDirSin * 3f + _wanderBias * 0.8f, -1f, 1f);
        a.Speed = 0.7f;
    }
}

/// <summary>Baseline brain factory: every ant gets its own stateful wander logic.</summary>
public sealed class HandcodedForagerFactory : IAntBrainFactory
{
    public IAntBrain CreateBrain(Caste caste, int antId, Prng prng) => new HandcodedForagerBrain();
}
