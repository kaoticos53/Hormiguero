using AntSim.Core;
using Redzen.Random;
using SharpNeat;
using SharpNeat.Evaluation;
using SharpNeat.Neat;
using SharpNeat.Neat.ComplexityRegulation;
using SharpNeat.Neat.DistanceMetrics.Double;
using SharpNeat.Neat.EvolutionAlgorithm;
using SharpNeat.Neat.Genome;
using SharpNeat.Neat.Genome.Double;
using SharpNeat.Neat.Reproduction.Asexual;
using SharpNeat.Neat.Reproduction.Asexual.WeightMutation;
using SharpNeat.Neat.Reproduction.Sexual;
using SharpNeat.Neat.Speciation.GeneticKMeans;
using SharpNeat.NeuralNets;

namespace AntSim.Evolution;

/// <summary>
/// Assembles the SharpNEAT evolution algorithm for the ant colony task (SharpNEAT 4.1.0 wiring).
/// All ants of the worker caste share one genome; population units are colonies, not individuals.
/// </summary>
public static class AntEvolutionExperiment
{
    public const string ActivationFnName = "LeakyReLU";
    public const double ConnectionWeightScale = 5.0;

    public static MetaNeatGenome<double> CreateMetaNeatGenome()
    {
        var actFnFactory = new DefaultActivationFunctionFactory<double>(false);
        // Note. The MetaNeatGenome constructor is internal in SharpNEAT 4.1.0; CreateCyclic is the public factory.
        return MetaNeatGenome<double>.CreateCyclic(
            inputNodeCount: AntSensors.TotalInputCount,
            outputNodeCount: 3, // turn, speed, pheromone deposit (pickup/drop are hardwired reflexes)
            cyclesPerActivation: 1,
            activationFn: actFnFactory.GetActivationFunction(ActivationFnName),
            connectionWeightScale: ConnectionWeightScale);
    }

    public static NeatEvolutionAlgorithm<double> CreateEvolutionAlgorithm(EvolveOptions opt, AntColonyEvaluationScheme scheme)
    {
        var metaNeatGenome = CreateMetaNeatGenome();

        // Seeded population creation and EA random source; simulations themselves are independent of this.
        var popPrng = RandomDefaults.CreateRandomSource(opt.Seed);
        var neatPop = NeatPopulationFactory<double>.CreatePopulation(
            metaNeatGenome,
            0.25, // initial connectivity; denser than NEAT-typical so early nets reliably produce movement
            opt.PopulationSize,
            popPrng);

        // Genome -> black box decoding and (parallel) genome list evaluation.
        IGenomeDecoder<NeatGenome<double>, IBlackBox<double>> genomeDecoder =
            NeatGenomeDecoderFactory.CreateGenomeDecoder(metaNeatGenome.IsAcyclic, false);
        var genomeEvaluator = GenomeListEvaluatorFactory.CreateEvaluator<NeatGenome<double>, IBlackBox<double>>(
            genomeDecoder, scheme, opt.DegreeOfParallelism);

        // K-means speciation runs on a single thread on purpose. It is the *only* nondeterministic
        // stage: the parallel variant reduces floating-point centroid distances in
        // scheduler-dependent order, so identical seeds drift apart run to run (the README used to
        // apologise for this). Genome evaluation stays parallel — per-genome fitness is independent
        // and each episode is bit-reproducible, so evaluating in parallel is deterministic. Peak
        // populations are at most a few thousand genomes, so serial speciation costs nothing
        // measurable next to the thousands of episodes behind each generation.
        var distanceMetric = new ManhattanDistanceMetric(1.0, 0.0, 10.0);
        var speciationStrategy = new GeneticKMeansSpeciationStrategy<double>(distanceMetric, 5);

        var weightMutationScheme = WeightMutationSchemeFactory.CreateDefaultScheme(metaNeatGenome.ConnectionWeightScale);

        var eaSettings = new NeatEvolutionAlgorithmSettings
        {
            SpeciesCount = opt.SpeciesCount,
            ElitismProportion = 0.2,
            SelectionProportion = 0.2,
            OffspringAsexualProportion = 0.5,
            OffspringSexualProportion = 0.5,
            InterspeciesMatingProportion = 0.1,
            StatisticsMovingAverageHistoryLength = 100,
        };

        var asexualSettings = new NeatReproductionAsexualSettings
        {
            ConnectionWeightMutationProbability = 0.94,
            AddConnectionMutationProbability = 0.03,
            AddNodeMutationProbability = 0.02,
            DeleteConnectionMutationProbability = 0.01,
        };

        var sexualSettings = new NeatReproductionSexualSettings
        {
            SecondaryParentGeneProbability = 0.2,
        };

        return new NeatEvolutionAlgorithm<double>(
            eaSettings,
            genomeEvaluator,
            speciationStrategy,
            neatPop,
            new AbsoluteComplexityRegulationStrategy(10, 80),
            asexualSettings,
            sexualSettings,
            weightMutationScheme,
            RandomDefaults.CreateRandomSource(opt.Seed + 1));
    }
}
