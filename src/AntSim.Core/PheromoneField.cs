namespace AntSim.Core;

/// <summary>
/// A scalar pheromone field on a toroidal grid: evaporation every tick, optional diffusion
/// every N ticks, point deposits, bilinear sampling. Deterministic and allocation-free in the hot path.
/// </summary>
public sealed class PheromoneField
{
    private readonly PheromoneChannelDef _def;
    private readonly int _cols, _rows;
    private readonly float _cellSize;
    private float[] _values;
    private float[] _buffer;

    public PheromoneField(PheromoneChannelDef def, float width, float height, float cellSize)
    {
        _def = def;
        _cellSize = cellSize;
        _cols = Math.Max(1, (int)MathF.Ceiling(width / cellSize));
        _rows = Math.Max(1, (int)MathF.Ceiling(height / cellSize));
        _values = new float[_cols * _rows];
        _buffer = new float[_cols * _rows];
    }

    public int Cols => _cols;
    public int Rows => _rows;
    public float MaxLevel => _def.MaxLevel;
    public float[] Values => _values; // read-only use (telemetry/tests)

    /// <summary>Bilinear sample at world position (toroidal).</summary>
    public float Sample(float x, float y)
    {
        float gx = x / _cellSize - 0.5f;
        float gy = y / _cellSize - 0.5f;
        int c0 = (int)MathF.Floor(gx);
        int r0 = (int)MathF.Floor(gy);
        float fx = gx - c0;
        float fy = gy - r0;
        int c1 = Mod(c0 + 1, _cols);
        int r1 = Mod(r0 + 1, _rows);
        c0 = Mod(c0, _cols);
        r0 = Mod(r0, _rows);

        int i00 = r0 * _cols + c0;
        int i10 = r0 * _cols + c1;
        int i01 = r1 * _cols + c0;
        int i11 = r1 * _cols + c1;

        float v = (_values[i00] * (1f - fx) + _values[i10] * fx) * (1f - fy)
                + (_values[i01] * (1f - fx) + _values[i11] * fx) * fy;
        return v;
    }

    /// <summary>Add <paramref name="amount"/> into the cell containing (x, y), clamped to the saturation cap.</summary>
    public void Deposit(float x, float y, float amount)
    {
        if (amount <= 0f) return;
        int c = Mod((int)(x / _cellSize), _cols);
        int r = Mod((int)(y / _cellSize), _rows);
        int i = r * _cols + c;
        float v = _values[i] + amount;
        _values[i] = v > _def.MaxLevel ? _def.MaxLevel : v;
    }

    /// <summary>
    /// Advance one tick: exponential evaporation, plus a 4-neighbour diffusion pass when <paramref name="diffuse"/> is set.
    /// </summary>
    public void Update(float dt, bool diffuse)
    {
        float evapFactor = 1f - _def.EvaporationPerSecond * dt;
        if (evapFactor < 0f) evapFactor = 0f;

        var v = _values;
        for (int i = 0; i < v.Length; i++)
            v[i] *= evapFactor;

        if (!diffuse || _def.DiffusionRate <= 0f)
            return;

        float rate = _def.DiffusionRate;
        var b = _buffer;
        int cols = _cols, rows = _rows;
        for (int r = 0; r < rows; r++)
        {
            int rUp = r == 0 ? rows - 1 : r - 1;
            int rDown = r == rows - 1 ? 0 : r + 1;
            int rowOff = r * cols;
            int upOff = rUp * cols;
            int downOff = rDown * cols;
            for (int c = 0; c < cols; c++)
            {
                int cLeft = c == 0 ? cols - 1 : c - 1;
                int cRight = c == cols - 1 ? 0 : c + 1;
                float avg = 0.25f * (v[rowOff + cLeft] + v[rowOff + cRight] + v[upOff + c] + v[downOff + c]);
                b[rowOff + c] = v[rowOff + c] + rate * (avg - v[rowOff + c]);
            }
        }
        (_values, _buffer) = (_buffer, _values);
    }

    /// <summary>Sum of all cell levels (tests/telemetry; O(cells)).</summary>
    public float ComputeTotal()
    {
        double sum = 0;
        for (int i = 0; i < _values.Length; i++)
            sum += _values[i];
        return (float)sum;
    }

    private static int Mod(int v, int m)
    {
        v %= m;
        return v < 0 ? v + m : v;
    }
}
