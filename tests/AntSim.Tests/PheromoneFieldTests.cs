using AntSim.Core;

namespace AntSim.Tests;

/// <summary>Pheromone field invariants: deposit, evaporation, diffusion mass conservation, toroidal sampling.</summary>
public class PheromoneFieldTests
{
    private static PheromoneChannelDef Def() => new()
    {
        Name = "Test",
        EvaporationPerSecond = 0.0f, // no evaporation: mass must be conserved exactly by diffusion
        DiffusionRate = 0.2f,
        MaxLevel = 100f,
    };

    [Fact]
    public void Deposit_IncreasesLocalLevel()
    {
        var field = new PheromoneField(Def(), width: 100, height: 100, cellSize: 10);
        // Deposit lands in the single cell containing the point; sampling at a cell centre
        // (55,55) avoids bilinear smoothing across neighbours.
        float before = field.Sample(55, 55);
        field.Deposit(55, 55, 5f);
        Assert.True(field.Sample(55, 55) > before + 4f);
    }

    [Fact]
    public void Deposit_RespectsSaturationCap()
    {
        var def = Def() with { MaxLevel = 10f };
        var field = new PheromoneField(def, 100, 100, 10);
        for (int i = 0; i < 50; i++)
            field.Deposit(50, 50, 5f);
        Assert.True(field.Sample(50, 50) <= def.MaxLevel + 1e-4f);
    }

    [Fact]
    public void Diffusion_ConservesTotalMass_WithoutEvaporation()
    {
        var field = new PheromoneField(Def(), 100, 100, 10);
        field.Deposit(25, 25, 40f);
        field.Deposit(75, 75, 40f);
        float total0 = field.ComputeTotal();

        for (int t = 0; t < 20; t++)
            field.Update(dt: 1f, diffuse: true);

        Assert.Equal(total0, field.ComputeTotal(), 2); // toroidal diffusion must not leak
    }

    [Fact]
    public void Evaporation_DecaysLevels()
    {
        var def = Def() with { DiffusionRate = 0f, EvaporationPerSecond = 0.5f };
        var field = new PheromoneField(def, 100, 100, 10);
        field.Deposit(50, 50, 50f);
        float l0 = field.Sample(50, 50);

        field.Update(dt: 1f, diffuse: false);
        float l1 = field.Sample(50, 50);
        Assert.True(l1 < l0 * 0.55f, $"Expected ~half after 1s at 0.5/s decay; got {l1} vs {l0}");
    }

    [Fact]
    public void Sampling_WrapsAroundToroidalEdges()
    {
        var field = new PheromoneField(Def(), 100, 100, 10);
        field.Deposit(1, 50, 40f); // near the left edge

        // Sampling past the left edge must see the deposit (wrap to the right side).
        float sampled = field.Sample(99, 50);
        Assert.True(sampled > 0.1f, $"Expected wrapped sample > 0.1, got {sampled}");
    }

    [Fact]
    public void Deposit_OutsideGrid_IsSafe()
    {
        var field = new PheromoneField(Def(), 100, 100, 10);
        field.Deposit(-5, 105, 10f); // wraps or clamps; must not throw
        field.Sample(250, -30);
    }
}
