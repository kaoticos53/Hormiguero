using System;
using UnityEngine;

namespace AntSimViewer
{
    /// <summary>
    /// Port of AntSim.Core.WorldMath: toroidal 2D math. The world is a torus spanning
    /// [0,Width) x [0,Height); angles in radians in [0, 2pi), counter-clockwise, y grows "up".
    /// Positive turn = counter-clockwise = to the ant's left.
    /// </summary>
    public static class WorldMathRuntime
    {
        public static float WrapAngle(float angle)
        {
            float tau = Mathf.PI * 2f;
            angle %= tau;
            return angle < 0f ? angle + tau : angle;
        }

        /// <summary>Shortest signed difference between two angles, in (-pi, pi].</summary>
        public static float AngleDelta(float from, float to)
        {
            float d = WrapAngle(to - from);
            return d > Mathf.PI ? d - Mathf.PI * 2f : d;
        }

        public static float WrapCoord(float v, float size)
        {
            v %= size;
            return v < 0f ? v + size : v;
        }

        /// <summary>Shortest signed delta from `from` to `to` on a torus of length `size`.</summary>
        public static float TorusDelta(float from, float to, float size)
        {
            float d = (to - from) % size;
            if (d > size * 0.5f) d -= size;
            else if (d < -size * 0.5f) d += size;
            return d;
        }

        public static float TorusDistanceSq(float x1, float y1, float x2, float y2, float width, float height)
        {
            float dx = TorusDelta(x1, x2, width);
            float dy = TorusDelta(y1, y2, height);
            return dx * dx + dy * dy;
        }

        public static float Clamp(float v, float min, float max)
        {
            return v < min ? min : (v > max ? max : v);
        }
    }

    /// <summary>
    /// Port of AntSim.Core.Prng: deterministic SplitMix64. Same seed => identical sequence,
    /// which is what makes the live view reproduce the headless simulation's behaviour.
    /// </summary>
    public sealed class PrngRuntime
    {
        ulong _state;

        public PrngRuntime(ulong seed) { _state = seed; }

        public ulong NextULong()
        {
            _state += 0x9E3779B97F4A7C15UL;
            ulong z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>Uniform double in [0, 1).</summary>
        public double NextDouble()
        {
            return (double)(NextULong() >> 11) * (1.0 / (double)(1L << 53));
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public float NextFloat()
        {
            return (float)NextDouble();
        }

        /// <summary>Uniform float in [-1, 1).</summary>
        public float NextSignedFloat()
        {
            return (float)(NextDouble() * 2.0 - 1.0);
        }

        /// <summary>Random heading angle in [0, 2pi).</summary>
        public float NextAngle()
        {
            return (float)(NextDouble() * 2.0 * Math.PI);
        }
    }
}