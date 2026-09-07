namespace AntSim.Core;

/// <summary>
/// Ant sensor readings for one tick. This is the NEAT input schema (plus a constant bias
/// input appended by the brain wrapper). All pheromone values are normalised to [0, 1]
/// by the channel saturation cap; directional values are sin/cos of the bearing relative
/// to the ant's heading (positive sin = target is to the LEFT).
/// </summary>
public struct AntSensors
{
    // Antenna samples of the food trail (the trail laid by ants returning with food).
    public float FoodTrailLeft;
    public float FoodTrailCenter;
    public float FoodTrailRight;

    // Antenna samples of the home trail (the trail laid by outbound ants).
    public float HomeTrailLeft;
    public float HomeTrailCenter;
    public float HomeTrailRight;

    // Bearing to the nest, relative to heading.
    public float NestDirSin;
    public float NestDirCos;

    // Bearing to the nearest food pile within sensing range (FoodVisible = 0 when none).
    public float FoodDirSin;
    public float FoodDirCos;
    public float FoodVisible;

    // 1 - age / lifespan (1 = newborn, 0 = about to die of old age).
    public float EnergyNorm;

    // 1 when the ant is carrying food.
    public float Carrying;

    // Fresh uniform noise in [-1, 1) every tick; gives the brain exploration drive.
    public float RandomNoise;

    /// <summary>Number of sensor inputs (excludes the constant bias input).</summary>
    public const int Count = 14;

    /// <summary>Total NEAT input count: sensors plus one constant bias input at the end.</summary>
    public const int TotalInputCount = Count + 1;

    /// <summary>Write the sensor values into the first <see cref="Count"/> slots of a black-box input span.</summary>
    public void WriteTo(Span<double> inputs)
    {
        inputs[0] = FoodTrailLeft;
        inputs[1] = FoodTrailCenter;
        inputs[2] = FoodTrailRight;
        inputs[3] = HomeTrailLeft;
        inputs[4] = HomeTrailCenter;
        inputs[5] = HomeTrailRight;
        inputs[6] = NestDirSin;
        inputs[7] = NestDirCos;
        inputs[8] = FoodDirSin;
        inputs[9] = FoodDirCos;
        inputs[10] = FoodVisible;
        inputs[11] = EnergyNorm;
        inputs[12] = Carrying;
        inputs[13] = RandomNoise;
    }
}

/// <summary>Motor actions computed by the brain for one tick. Interpreted and clamped by the world.</summary>
public struct AntActions
{
    /// <summary>Heading change, -1..1 (scaled by WorldConfig.TurnRate). Positive = turn left (CCW).</summary>
    public float Turn;

    /// <summary>Desired speed as a fraction of body max speed, 0..1.</summary>
    public float Speed;

    /// <summary>Pheromone deposit strength, 0..1 (scales AntBody.DepositRate).</summary>
    public float PheromoneDeposit;
}
