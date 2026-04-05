namespace LadyBug.Gameplay.Gates;

/// <summary>
/// Stable visual orientation of a rotating gate.
/// </summary>
/// <remarks>
/// This enum describes the two resting visual orientations shown by the gate
/// sprite when it is not in its short turning transition.
///
/// For gameplay logic, use <see cref="GateLogicalState"/> instead, since that
/// type describes which movement axis is currently blocked.
/// </remarks>
public enum GateOrientation
{
    /// <summary>
    /// The gate is shown in its horizontal resting orientation.
    /// </summary>
    Horizontal,

    /// <summary>
    /// The gate is shown in its vertical resting orientation.
    /// </summary>
    Vertical
}
