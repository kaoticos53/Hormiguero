namespace AntSim.Core;

/// <summary>
/// Uniform-grid spatial hash for O(n) neighbour queries on a torus. Rebuilt every tick
/// from the ant list; <see cref="QueryNearby"/> returns a reused list (do not hold references to it).
/// </summary>
public sealed class SpatialHash<T> where T : class
{
    private readonly int _cols, _rows;
    private readonly float _cellSize;
    private readonly float _width, _height;
    private readonly List<T>[] _cells;
    private readonly List<T> _results = [];

    public SpatialHash(float width, float height, float cellSize)
    {
        _width = width;
        _height = height;
        _cellSize = MathF.Max(1e-3f, cellSize);
        _cols = Math.Max(1, (int)MathF.Ceiling(width / _cellSize));
        _rows = Math.Max(1, (int)MathF.Ceiling(height / _cellSize));
        _cells = new List<T>[_cols * _rows];
        for (int i = 0; i < _cells.Length; i++)
            _cells[i] = [];
    }

    public void Clear()
    {
        for (int i = 0; i < _cells.Length; i++)
            _cells[i].Clear();
    }

    public void Insert(T item, float x, float y)
    {
        int c = Mod((int)(x / _cellSize), _cols);
        int r = Mod((int)(y / _cellSize), _rows);
        _cells[r * _cols + c].Add(item);
    }

    /// <summary>All inserted items within <paramref name="radius"/> of (x, y) on the torus.</summary>
    public List<T> QueryNearby(float x, float y, float radius)
    {
        _results.Clear();
        int rCells = Math.Max(0, (int)MathF.Ceiling(radius / _cellSize));
        int c0 = Mod((int)(x / _cellSize) - rCells, _cols);
        int r0 = Mod((int)(y / _cellSize) - rCells, _rows);
        int spanC = Math.Min(_cols, 2 * rCells + 1);
        int spanR = Math.Min(_rows, 2 * rCells + 1);

        for (int dr = 0; dr < spanR; dr++)
        {
            int row = Mod(r0 + dr, _rows) * _cols;
            for (int dc = 0; dc < spanC; dc++)
            {
                int col = Mod(c0 + dc, _cols);
                var cell = _cells[row + col];
                for (int i = 0; i < cell.Count; i++)
                    _results.Add(cell[i]);
            }
        }
        return _results;
    }

    private static int Mod(int v, int m)
    {
        v %= m;
        return v < 0 ? v + m : v;
    }
}
