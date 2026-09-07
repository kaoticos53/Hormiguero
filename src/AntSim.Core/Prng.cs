namespace AntSim.Core;

/// <summary>
/// Deterministic SplitMix64 PRNG. Same seed => identical sequence on any platform and runtime,
/// which is what makes simulation replays and determinism tests possible.
/// </summary>
public sealed class Prng
{
    private ulong _state;

    public Prng(ulong seed) => _state = seed;

    public ulong NextULong()
    {
        _state += 0x9E3779B97F4A7C15UL;
        ulong z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>Uniform double in [0, 1).</summary>
    public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

    /// <summary>Uniform float in [0, 1).</summary>
    public float NextFloat() => (float)NextDouble();

    /// <summary>Uniform float in [-1, 1).</summary>
    public float NextSignedFloat() => (float)(NextDouble() * 2.0 - 1.0);

    /// <summary>Uniform int in [minInclusive, maxExclusive).</summary>
    public int NextInt(int minInclusive, int maxExclusive)
        => minInclusive + (int)(NextULong() % (ulong)(maxExclusive - minInclusive));

    /// <summary>Standard normal deviate (Box-Muller transform).</summary>
    public double NextGaussian()
    {
        double u1 = 1.0 - NextDouble();
        double u2 = NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }

    /// <summary>Random heading angle in [0, 2π).</summary>
    public float NextAngle() => (float)(NextDouble() * Math.Tau);
}
