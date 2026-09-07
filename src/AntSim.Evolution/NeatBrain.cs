using AntSim.Core;
using SharpNeat;

namespace AntSim.Evolution;

/// <summary>
/// Adapts a decoded SharpNEAT black box to the ant brain interface.
/// The black box is shared by every worker of the colony (nestmates are sisters — one genome per caste),
/// and is reset after each activation so ants don't leak state into each other.
/// </summary>
public sealed class NeatBrain : IAntBrain
{
    private readonly IBlackBox<double> _blackBox;

    public NeatBrain(IBlackBox<double> blackBox) => _blackBox = blackBox;

    public void Think(in AntSensors s, ref AntActions a)
    {
        var inputs = _blackBox.Inputs.Span;
        s.WriteTo(inputs);
        inputs[AntSensors.Count] = 1.0; // constant bias input

        _blackBox.Activate();

        var outputs = _blackBox.Outputs.Span;
        a.Turn = Math.Clamp((float)outputs[0], -1f, 1f);            // heading change
        a.Speed = Math.Clamp((float)outputs[1], 0f, 1f);            // move intensity
        a.PheromoneDeposit = Math.Clamp((float)outputs[2], 0f, 1f); // trail laying strength

        _blackBox.Reset();
    }
}

/// <summary>Factory handing every ant of the caste the same shared black box brain.</summary>
public sealed class NeatBrainFactory : IAntBrainFactory
{
    private readonly IBlackBox<double> _blackBox;

    public NeatBrainFactory(IBlackBox<double> blackBox) => _blackBox = blackBox;

    public IAntBrain CreateBrain(Caste caste, int antId, Prng prng) => new NeatBrain(_blackBox);
}
