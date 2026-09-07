using AntSim.Core;

namespace AntSim.Tests;

/// <summary>Same config + species + seed => identical trajectory (the determinism contract).</summary>
public class DeterminismTests
{
    private static (SimWorld World, long Delivered) RunEpisode(ulong seed, int ticks = 300)
    {
        var config = new WorldConfig { EpisodeTicks = ticks };
        var world = SimWorld.CreateSeeded(
            config, SpeciesCatalog.BlackGardenAnt, SpeciesCatalog.BlackGardenAnt.BaselineBrainFactory, seed: seed);
        for (int t = 0; t < ticks; t++)
            world.Step();
        return (world, world.FoodDelivered);
    }

    [Fact]
    public void SameSeed_ProducesIdenticalTrajectory()
    {
        var (a, deliveredA) = RunEpisode(42);
        var (b, deliveredB) = RunEpisode(42);

        Assert.Equal(deliveredA, deliveredB);
        Assert.Equal(a.Ants.Count, b.Ants.Count);
        for (int i = 0; i < a.Ants.Count; i++)
        {
            Assert.Equal(a.Ants[i].X, b.Ants[i].X);
            Assert.Equal(a.Ants[i].Y, b.Ants[i].Y);
            Assert.Equal(a.Ants[i].Heading, b.Ants[i].Heading);
            Assert.Equal(a.Ants[i].FoodCarried, b.Ants[i].FoodCarried);
            Assert.Equal(a.Ants[i].Age, b.Ants[i].Age);
        }
        Assert.Equal(a.FoodTrailField.ComputeTotal(), b.FoodTrailField.ComputeTotal());
    }

    [Fact]
    public void DifferentSeeds_Diverge()
    {
        var (a, _) = RunEpisode(1);
        var (b, _) = RunEpisode(2);

        bool anyDifference = false;
        for (int i = 0; i < a.Ants.Count && !anyDifference; i++)
            anyDifference = a.Ants[i].X != b.Ants[i].X || a.Ants[i].Y != b.Ants[i].Y;

        Assert.True(anyDifference, "Different seeds produced identical trajectories.");
    }

    [Fact]
    public void SteppingTickByTick_MatchesBatchRun()
    {
        // The world must be path-independent: stepping the same number of ticks in one call
        // or via manual Step() calls yields the same state.
        var config = new WorldConfig { EpisodeTicks = 120 };
        var a = SimWorld.CreateSeeded(config, SpeciesCatalog.BlackGardenAnt, SpeciesCatalog.BlackGardenAnt.BaselineBrainFactory, seed: 9);
        var b = SimWorld.CreateSeeded(config, SpeciesCatalog.BlackGardenAnt, SpeciesCatalog.BlackGardenAnt.BaselineBrainFactory, seed: 9);

        var runner = new EpisodeRunner();
        var result = runner.Run(config, SpeciesCatalog.BlackGardenAnt, SpeciesCatalog.BlackGardenAnt.BaselineBrainFactory, seed: 9);

        // World A: manual stepping; world B: a second manual run (must be bit-identical to A),
        // and its counters must match the EpisodeRunner's (same seed, same code path).
        for (int i = 0; i < 120; i++)
        {
            a.Step();
            b.Step();
        }

        Assert.Equal(result.TicksRun, a.Tick);
        Assert.Equal(result.FoodDelivered, a.FoodDelivered);
        for (int i = 0; i < a.Ants.Count; i++)
        {
            Assert.Equal(a.Ants[i].X, b.Ants[i].X);
            Assert.Equal(a.Ants[i].Y, b.Ants[i].Y);
        }
    }
}
