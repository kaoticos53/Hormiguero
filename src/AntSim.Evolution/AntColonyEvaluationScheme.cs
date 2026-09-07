using AntSim.Core;
using SharpNeat;
using SharpNeat.Evaluation;

namespace AntSim.Evolution;

/// <summary>Black-box evaluation scheme for the ant colony foraging task.</summary>
public sealed class AntColonyEvaluationScheme : IBlackBoxEvaluationScheme<double>
{
    private readonly EvolveTaskSettings _settings;

    public AntColonyEvaluationScheme(EvolveTaskSettings settings) => _settings = settings;

    public int InputCount => AntSensors.TotalInputCount;
    public int OutputCount => 3; // turn, speed, pheromone deposit
    public bool IsDeterministic => true;
    public IComparer<FitnessInfo> FitnessComparer => PrimaryFitnessInfoComparer.Singleton;
    public FitnessInfo NullFitness => FitnessInfo.DefaultFitnessInfo;
    public bool EvaluatorsHaveState => false;
    public IPhenomeEvaluator<IBlackBox<double>> CreateEvaluator() => new AntColonyEvaluator(_settings);
    public bool TestForStopCondition(FitnessInfo fitnessInfo) => false;
}
