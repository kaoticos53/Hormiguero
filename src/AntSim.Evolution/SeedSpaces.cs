namespace AntSim.Evolution;

/// <summary>
/// The seed lanes that keep selection honest.
///
/// Training seeds drive selection: NEAT fitness and the body-parameter hill-climber both optimise
/// against them. Validation seeds are only ever *read* — never optimised against — so the gap
/// between the two lanes measures generalisation instead of how well a genome memorised two lucky
/// worlds. A champion that scores 756 on training and 300 on validation is not a champion.
///
/// Layout: a run's training lane is <c>runSeed * 1000 + [0, episodeCount)</c>, the historical
/// layout, kept so older runs stay reproducible. Validation sits <see cref="ValidationLaneOffset"/>
/// above that base, far outside any reachable training lane: crossing the two would need a training
/// base of a billion, i.e. a run seed of a million.
/// </summary>
public static class SeedSpaces
{
    /// <summary>Consecutive seeds reserved per run seed (training lane base = runSeed * this).</summary>
    public const ulong LaneStride = 1000UL;

    /// <summary>Distance between a run's training base and its validation base.</summary>
    public const ulong ValidationLaneOffset = 1_000_000_000UL;

    /// <summary>First seed of the training lane for a run seed.</summary>
    public static ulong TrainingBase(ulong runSeed) => runSeed * LaneStride;

    /// <summary>First seed of the held-out validation lane for a run seed.</summary>
    public static ulong ValidationBase(ulong runSeed) => TrainingBase(runSeed) + ValidationLaneOffset;

    /// <summary>The <paramref name="episodeCount"/> training seeds of a run: the lane selection optimises.</summary>
    public static ulong[] Training(ulong runSeed, int episodeCount)
        => Consecutive(TrainingBase(runSeed), episodeCount, nameof(episodeCount));

    /// <summary>The <paramref name="episodeCount"/> held-out seeds of a run: read-only, never selected on.</summary>
    public static ulong[] Validation(ulong runSeed, int episodeCount)
        => Consecutive(ValidationBase(runSeed), episodeCount, nameof(episodeCount));

    /// <summary><paramref name="count"/> consecutive seeds starting at <paramref name="baseSeed"/>.</summary>
    public static ulong[] Consecutive(ulong baseSeed, int count, string parameterName = "count")
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(parameterName, count, "At least one seed is required.");
        var seeds = new ulong[count];
        for (int i = 0; i < count; i++)
            seeds[i] = baseSeed + (ulong)i;
        return seeds;
    }
}
