namespace LadyBug.Gameplay.Gates;

/// <summary>
/// Current visual presentation state of a rotating gate.
/// </summary>
/// <remarks>
/// For now, the gate is either:
/// - stable in one of its two resting orientations
/// - briefly shown as turning during a rotation
/// </remarks>
public enum GateVisualState
{
    /// <summary>
    /// The gate is shown in a stable resting orientation.
    /// </summary>
    Stable,

    /// <summary>
    /// The gate is currently shown in its short rotation transition.
    /// </summary>
    Turning
}