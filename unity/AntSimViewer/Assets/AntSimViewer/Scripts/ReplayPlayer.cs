using System;

namespace AntSimViewer
{
    /// <summary>
    /// Plays the sampled frames from replay.json. Positions are interpolated between samples;
    /// movement across a torus seam follows the shortest path instead of jumping across screen.
    /// </summary>
    public sealed class ReplayPlayer
    {
        public readonly ReplayDataFile Data;
        public readonly int FrameCount;
        public readonly int AntCount;

        public bool Paused;
        public float Speed = 1f;
        public float FramePosition;

        public ReplayPlayer(ReplayDataFile data)
        {
            Data = data;
            FrameCount = data != null && data.Frames != null ? data.Frames.Length : 0;
            AntCount = FrameCount > 0 && Data.Frames[0].X != null ? Data.Frames[0].X.Length : 0;
            FramePosition = 0f;
        }

        public int Tick
        {
            get { return FrameCount == 0 ? 0 : Data.Frames[FrameIndex].Tick; }
        }

        public int Delivered
        {
            get { return FrameCount == 0 ? 0 : (int)Data.Frames[FrameIndex].Delivered; }
        }

        public int AliveCount
        {
            get { return FrameCount == 0 ? 0 : Data.Frames[FrameIndex].Alive; }
        }

        public int FrameIndex
        {
            get
            {
                if (FrameCount == 0) return 0;
                int i = (int)Math.Floor(FramePosition);
                return i < 0 ? 0 : (i >= FrameCount ? FrameCount - 1 : i);
            }
        }

        public void Restart()
        {
            FramePosition = 0f;
            Paused = false;
        }

        /// <summary>Advance according to wall-clock seconds and a playback multiplier.</summary>
        public void Advance(float unscaledSeconds, float speedMultiplier)
        {
            if (Paused || FrameCount < 2) return;
            float sampleSeconds = Data.Config != null
                ? Math.Max(0.001f, Data.SampleInterval * Data.Config.DeltaTime)
                : 1f;
            FramePosition += unscaledSeconds * Math.Max(0f, speedMultiplier) / sampleSeconds;
            if (FramePosition >= FrameCount - 1)
                FramePosition = FrameCount - 1;
        }

        public float GetX(int ant)
        {
            return Interpolated(ant, true);
        }

        public float GetY(int ant)
        {
            return Interpolated(ant, false);
        }

        public float GetHeading(int ant)
        {
            if (FrameCount == 0 || ant < 0 || ant >= AntCount) return 0f;
            int a = FrameIndex;
            int b = Math.Min(a + 1, FrameCount - 1);
            float t = FramePosition - a;
            float h0 = Data.Frames[a].Heading[ant];
            float h1 = Data.Frames[b].Heading[ant];
            float delta = WrapAngle(h1 - h0);
            if (delta > (float)Math.PI) delta -= (float)(Math.PI * 2.0);
            return WrapAngle(h0 + delta * t);
        }

        public float GetFoodRemaining(int source)
        {
            if (FrameCount == 0 || Data.Frames[FrameIndex].FoodRemaining == null) return -1f;
            int a = FrameIndex;
            int b = Math.Min(a + 1, FrameCount - 1);
            float t = FramePosition - a;
            float[] first = Data.Frames[a].FoodRemaining;
            float[] second = Data.Frames[b].FoodRemaining;
            if (source < 0 || source >= first.Length) return -1f;
            float end = source < second.Length ? second[source] : first[source];
            return first[source] + (end - first[source]) * t;
        }

        public bool IsCarrying(int ant)
        {
            if (FrameCount == 0 || ant < 0 || ant >= AntCount) return false;
            byte[] carrying = DecodeCarrying(Data.Frames[FrameIndex].Carrying);
            return carrying != null && ant < carrying.Length && carrying[ant] != 0;
        }

        float Interpolated(int ant, bool xAxis)
        {
            if (FrameCount == 0 || ant < 0 || ant >= AntCount) return 0f;
            int a = FrameIndex;
            int b = Math.Min(a + 1, FrameCount - 1);
            float t = FramePosition - a;
            float v0 = xAxis ? Data.Frames[a].X[ant] : Data.Frames[a].Y[ant];
            float v1 = xAxis ? Data.Frames[b].X[ant] : Data.Frames[b].Y[ant];
            float size = Data.Config == null ? 800f : (xAxis ? Data.Config.Width : Data.Config.Height);
            float delta = (v1 - v0) % size;
            if (delta > size * 0.5f) delta -= size;
            else if (delta < -size * 0.5f) delta += size;
            return Wrap(v0 + delta * t, size);
        }

        static float Wrap(float value, float size)
        {
            value %= size;
            return value < 0f ? value + size : value;
        }

        static float WrapAngle(float value)
        {
            float tau = (float)(Math.PI * 2.0);
            value %= tau;
            return value < 0f ? value + tau : value;
        }

        static byte[] DecodeCarrying(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            try { return Convert.FromBase64String(value); }
            catch (FormatException) { return null; }
        }
    }
}