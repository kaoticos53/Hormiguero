namespace AntSim.Core;

/// <summary>
/// Toroidal 2D math helpers. The world is a torus spanning [0,Width) × [0,Height).
/// Convention: angles in radians in [0, 2π), counter-clockwise, y grows "up".
/// Positive turn = counter-clockwise = to the ant's left.
/// </summary>
public static class WorldMath
{
    public static float WrapAngle(float angle)
    {
        angle %= MathF.Tau;
        return angle < 0f ? angle + MathF.Tau : angle;
    }

    /// <summary>Shortest signed difference between two angles, in (-π, π].</summary>
    public static float AngleDelta(float from, float to)
    {
        float d = WrapAngle(to - from);
        return d > MathF.PI ? d - MathF.Tau : d;
    }

    public static float WrapCoord(float v, float size)
    {
        v %= size;
        return v < 0f ? v + size : v;
    }

    /// <summary>Shortest signed delta from <paramref name="from"/> to <paramref name="to"/> on a torus of length <paramref name="size"/>.</summary>
    public static float TorusDelta(float from, float to, float size)
    {
        float d = (to - from) % size;
        if (d > size * 0.5f) d -= size;
        else if (d < -size * 0.5f) d += size;
        return d;
    }

    /// <summary>Heading (in [0, 2π)) from (x1,y1) to (x2,y2), taking the shortest toroidal path.</summary>
    public static float HeadingTo(float x1, float y1, float x2, float y2, float width, float height)
        => MathF.Atan2(TorusDelta(y1, y2, height), TorusDelta(x1, x2, width));

    public static float TorusDistanceSq(float x1, float y1, float x2, float y2, float width, float height)
    {
        float dx = TorusDelta(x1, x2, width);
        float dy = TorusDelta(y1, y2, height);
        return dx * dx + dy * dy;
    }

    public static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
}
