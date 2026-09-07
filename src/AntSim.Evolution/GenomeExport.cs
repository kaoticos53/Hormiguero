using AntSim.Core;
using SharpNeat.Neat.Genome;

namespace AntSim.Evolution;

/// <summary>One neural connection in the exported brain (innovation-id endpoints, not indexes).</summary>
public sealed record BrainConnection(int SourceId, int TargetId, double Weight);

/// <summary>
/// Self-contained brain export for the (future) Unity viewer: node ids, connections and the
/// colony's body/plasticity parameters. Deliberately independent of SharpNEAT types.
/// </summary>
public sealed record BrainExport(
    int InputCount,
    int OutputCount,
    bool IsAcyclic,
    string ActivationFnName,
    int CyclesPerActivation,
    int[] HiddenNodeIds,
    BrainConnection[] Connections,
    AntBody Body,
    PlasticityVector Plasticity);

public static class GenomeExport
{
    public static BrainExport ToExport(NeatGenome<double> genome, AntBody body, PlasticityVector plasticity)
    {
        var cg = genome.ConnectionGenes;
        var connArr = cg._connArr;
        var weightArr = cg._weightArr;
        int n = connArr.Length;
        var conns = new BrainConnection[n];
        for (int i = 0; i < n; i++)
            conns[i] = new BrainConnection(connArr[i].SourceId, connArr[i].TargetId, weightArr[i]);

        var meta = genome.MetaNeatGenome;
        return new BrainExport(
            meta.InputNodeCount,
            meta.OutputNodeCount,
            meta.IsAcyclic,
            AntEvolutionExperiment.ActivationFnName,
            meta.CyclesPerActivation,
            genome.HiddenNodeIdArray ?? [],
            conns,
            body,
            plasticity);
    }
}
