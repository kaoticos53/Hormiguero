using System.Text.Json;
using AntSim.Core;
using SharpNeat;
using SharpNeat.Evaluation;
using SharpNeat.Neat.Genome;
using SharpNeat.Neat.Genome.Double;
using SharpNeat.Neat.Genome.IO;

namespace AntSim.Evolution;

/// <summary>A champion ready to run: its decoded colony brain plus the body it evolved with.</summary>
public sealed record LoadedChampion(
    NeatBrainFactory BrainFactory,
    AntBody Body,
    PlasticityVector Plasticity,
    int ConnectionCount,
    bool HasEvolvedBody,
    string Label);

/// <summary>
/// Loads an evolved champion: the native SharpNEAT genome (<c>champion.neat</c>) plus the companion
/// <c>champion.json</c> that carries the colony's evolved body/plasticity parameters.
///
/// Every consumer must go through here. Evaluating a champion with the default body instead of the
/// evolved one understates it badly — the evolved body carries speed, sensor range and lifespan
/// genes, and a genome measured on the wrong body is simply a different ant.
/// </summary>
public static class ChampionLoader
{
    public static LoadedChampion Load(string genomePath)
    {
        if (!File.Exists(genomePath))
            throw new FileNotFoundException($"Champion genome not found: {genomePath}", genomePath);

        var meta = AntEvolutionExperiment.CreateMetaNeatGenome();
        var genome = NeatGenomeLoader.Load<double>(genomePath, meta, 0);
        IGenomeDecoder<NeatGenome<double>, IBlackBox<double>> decoder =
            NeatGenomeDecoderFactory.CreateGenomeDecoder(meta.IsAcyclic, false);
        var box = decoder.Decode(genome);

        var body = new AntBody();
        var plasticity = new PlasticityVector();
        bool hasEvolvedBody = false;

        // Companion JSON written next to the genome by AntEvolutionRunner.SaveChampion.
        string jsonPath = Path.ChangeExtension(genomePath, ".json");
        if (File.Exists(jsonPath))
        {
            var export = JsonSerializer.Deserialize<BrainExport>(File.ReadAllText(jsonPath));
            if (export is not null)
            {
                body = export.Body;
                plasticity = export.Plasticity;
                hasEvolvedBody = true;
            }
        }

        int connections = genome.ConnectionGenes.Length;
        string label = Path.GetFileName(genomePath) + $" ({connections} conns)"
                     + (hasEvolvedBody ? " + evolved body" : " (default body: no companion JSON)");

        return new LoadedChampion(new NeatBrainFactory(box), body, plasticity, connections, hasEvolvedBody, label);
    }
}
