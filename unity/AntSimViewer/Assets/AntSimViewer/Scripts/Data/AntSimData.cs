using System;

namespace AntSimViewer
{
    // These field names intentionally match System.Text.Json's PascalCase output exactly.
    // JsonUtility only serializes public fields, so these are fields rather than properties.

    [Serializable]
    public sealed class WorldConfigData
    {
        public float Width = 800f;
        public float Height = 800f;
        public float CellSize = 8f;
        public float DeltaTime = 0.1f;
        public int EpisodeTicks = 4000;
        public int WorkerCount = 150;
        public int FoodSourceCount = 6;
        public float FoodPerSource = 60f;
        public float FoodPickupAmount = 1f;
        public float PickupRadius = 3f;
        public float MinFoodDistanceFromNest = 250f;
        public float MaxFoodDistanceFromNest = 350f;
        public float NestRadius = 20f;
        public float TurnRate = 6f;
        public int DiffusionInterval = 2;
    }

    [Serializable]
    public sealed class AntBodyData
    {
        public float Speed = 12f;
        public float SensorRange = 26f;
        public float SensorSpread = 0.55f;
        public float DepositRate = 1f;
        public float LifespanSeconds = 900f;
    }

    [Serializable]
    public sealed class PlasticityData
    {
        public float TrailHabituationRate = 0.02f;
        public float TrailHabituationMax = 0.4f;
    }

    [Serializable]
    public sealed class BrainConnectionData
    {
        public int SourceId;
        public int TargetId;
        public double Weight;
    }

    [Serializable]
    public sealed class BrainData
    {
        public int InputCount;
        public int OutputCount;
        public bool IsAcyclic;
        public string ActivationFnName = "LeakyReLU";
        public int CyclesPerActivation = 1;
        public int[] HiddenNodeIds;
        public BrainConnectionData[] Connections;
        public AntBodyData Body;
        public PlasticityData Plasticity;
    }

    [Serializable]
    public sealed class ReplayFoodData
    {
        public float X;
        public float Y;
        public float Amount;
        public float InitialAmount;
    }

    [Serializable]
    public sealed class ReplayFrameData
    {
        public int Tick;
        public float[] X;
        public float[] Y;
        public float[] Heading;
        // System.Text.Json serializes byte[] as base64, e.g. "AAAA...".
        public string Carrying;
        public float[] FoodRemaining;
        public long Delivered;
        public int Alive;
    }

    [Serializable]
    public sealed class ReplayDataFile
    {
        public string SpeciesId;
        public WorldConfigData Config;
        public int SampleInterval = 10;
        public long FoodDelivered;
        public ReplayFrameData[] Frames;
        // Optional metadata; the player does not depend on it.
        public ulong Seed;
        public ReplayFoodData[] FoodSources;
    }
}