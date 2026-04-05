namespace LadyBug.Gameplay.Gates;

/// <summary>
/// Stable logical orientation of a rotating gate.
/// </summary>
/// <remarks>
/// For now, only the two stable states are represented.
/// Intermediate diagonal rotation frames are visual states and
/// are not needed yet for simple level display.
/// </remarks>
public enum GateOrientation
{
    Horizontal,
    Vertical
}