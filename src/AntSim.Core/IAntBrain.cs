namespace AntSim.Core;

/// <summary>
/// A per-ant decision module: maps this tick's sensors to motor actions.
/// Implementations must be side-effect free with respect to the world (state kept on `this` is fine).
/// </summary>
public interface IAntBrain
{
    void Think(in AntSensors sensors, ref AntActions actions);
}
