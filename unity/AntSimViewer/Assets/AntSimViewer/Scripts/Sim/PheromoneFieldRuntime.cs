using UnityEngine;

namespace AntSimViewer
{
    /// <summary>
    /// Port of AntSim.Core.PheromoneField: a scalar pheromone field on a toroidal grid with
    /// evaporation every tick, optional diffusion every N ticks, point deposits and bilinear
    /// sampling. Black-garden-ant channel constants are hardcoded in the constructor callers.
    /// </summary>
    public sealed class PheromoneFieldRuntime
    {
        public readonly int Cols;
        public readonly int Rows;
        public readonly float MaxLevel;

        readonly float _cellSize;
        readonly float _evapPerSecond;
        readonly float _diffusionRate;
        float[] _values;
        float[] _buffer;

        public PheromoneFieldRuntime(float cellSize, int cols, int rows, float maxLevel, float evapPerSecond, float diffusionRate)
        {
            _cellSize = cellSize;
            Cols = cols;
            Rows = rows;
            MaxLevel = maxLevel;
            _evapPerSecond = evapPerSecond;
            _diffusionRate = diffusionRate;
            _values = new float[cols * rows];
            _buffer = new float[cols * rows];
        }

        public float[] Values()
        {
            return _values;
        }

        /// <summary>Bilinear sample at world position (toroidal).</summary>
        public float Sample(float x, float y)
        {
            float gx = x / _cellSize - 0.5f;
            float gy = y / _cellSize - 0.5f;
            int c0 = (int)Mathf.Floor(gx);
            int r0 = (int)Mathf.Floor(gy);
            float fx = gx - c0;
            float fy = gy - r0;
            int c1 = Mod(c0 + 1, Cols);
            int r1 = Mod(r0 + 1, Rows);
            c0 = Mod(c0, Cols);
            r0 = Mod(r0, Rows);

            int i00 = r0 * Cols + c0;
            int i10 = r0 * Cols + c1;
            int i01 = r1 * Cols + c0;
            int i11 = r1 * Cols + c1;

            return (_values[i00] * (1f - fx) + _values[i10] * fx) * (1f - fy)
                 + (_values[i01] * (1f - fx) + _values[i11] * fx) * fy;
        }

        /// <summary>Add `amount` into the cell containing (x, y), clamped to the saturation cap.</summary>
        public void Deposit(float x, float y, float amount)
        {
            if (amount <= 0f) return;
            int c = Mod((int)(x / _cellSize), Cols);
            int r = Mod((int)(y / _cellSize), Rows);
            int i = r * Cols + c;
            float v = _values[i] + amount;
            _values[i] = v > MaxLevel ? MaxLevel : v;
        }

        /// <summary>
        /// Advance one tick: exponential evaporation, plus a 4-neighbour diffusion pass when `diffuse`.
        /// </summary>
        public void Update(float dt, bool diffuse)
        {
            float evapFactor = 1f - _evapPerSecond * dt;
            if (evapFactor < 0f) evapFactor = 0f;

            float[] v = _values;
            for (int i = 0; i < v.Length; i++)
                v[i] *= evapFactor;

            if (!diffuse || _diffusionRate <= 0f)
                return;

            float rate = _diffusionRate;
            float[] b = _buffer;
            for (int r = 0; r < Rows; r++)
            {
                int rUp = r == 0 ? Rows - 1 : r - 1;
                int rDown = r == Rows - 1 ? 0 : r + 1;
                int rowOff = r * Cols;
                int upOff = rUp * Cols;
                int downOff = rDown * Cols;
                for (int c = 0; c < Cols; c++)
                {
                    int cLeft = c == 0 ? Cols - 1 : c - 1;
                    int cRight = c == Cols - 1 ? 0 : c + 1;
                    float avg = 0.25f * (v[rowOff + cLeft] + v[rowOff + cRight] + v[upOff + c] + v[downOff + c]);
                    b[rowOff + c] = v[rowOff + c] + rate * (avg - v[rowOff + c]);
                }
            }
            float[] tmp = _values;
            _values = _buffer;
            _buffer = tmp;
        }

        static int Mod(int v, int m)
        {
            v %= m;
            return v < 0 ? v + m : v;
        }
    }
}