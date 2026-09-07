using AntSim.Core;

namespace AntSim.Tests;

/// <summary>Spatial hash correctness: radius queries must return exactly the points in range, including toroidal wrap.</summary>
public class SpatialHashTests
{
    private sealed class Point { public int Id; }

    [Fact]
    public void Query_FindsAllPointsInRadius()
    {
        var hash = new SpatialHash<Point>(100, 100, 20);
        var points = new List<Point>();
        for (int i = 0; i < 50; i++)
        {
            var p = new Point { Id = i };
            float x = (i * 37) % 100;
            float y = (i * 53) % 100;
            points.Add(p);
            hash.Insert(p, x, y);
        }

        var found = hash.QueryNearby(50, 50, 25);

        // Radius queries are cell-granular, so `found` may be a superset of the exact range;
        // the invariant is no false NEGATIVES: every point truly in range must be returned.
        foreach (var p in points)
        {
            float dx = MathF.Abs(WorldMath.TorusDelta(p.Id * 37 % 100, 50, 100));
            float dy = MathF.Abs(WorldMath.TorusDelta(p.Id * 53 % 100, 50, 100));
            bool inRange = dx * dx + dy * dy <= 25 * 25;
            if (inRange)
                Assert.Contains(p, found);
        }
    }

    [Fact]
    public void Query_WrapsAcrossToroidalSeams()
    {
        var hash = new SpatialHash<Point>(100, 100, 20);
        var p = new Point();
        hash.Insert(p, 2, 50); // near the left edge

        var found = hash.QueryNearby(97, 50, 10); // near the right edge, wrapping to the left

        Assert.Contains(p, found);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        var hash = new SpatialHash<Point>(100, 100, 20);
        hash.Insert(new Point(), 50, 50);
        hash.Clear();
        var found = hash.QueryNearby(50, 50, 100);
        Assert.Empty(found);
    }
}
