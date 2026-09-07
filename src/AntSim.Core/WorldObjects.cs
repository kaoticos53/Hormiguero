namespace AntSim.Core;

/// <summary>The colony's home: dropping food inside the radius scores a point.</summary>
public sealed class Nest
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Radius { get; set; }
}

/// <summary>A pile of food at a fixed location; ants deplete it by picking up.</summary>
public sealed class FoodSource
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Amount { get; set; }
}
