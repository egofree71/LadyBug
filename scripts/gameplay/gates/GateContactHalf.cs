namespace LadyBug.Gameplay.Gates;

/// <summary>
/// Half of the gate that is contacted by the actor during a push.
/// </summary>
/// <remarks>
/// This is used to choose the correct diagonal turning visual while the gate
/// rotates. The contacted half depends on the attempted movement direction and
/// on which side of the pivot the actor reaches first.
/// </remarks>
public enum GateContactHalf
{
    /// <summary>
    /// The left half of the gate is contacted.
    /// </summary>
    Left,

    /// <summary>
    /// The right half of the gate is contacted.
    /// </summary>
    Right,

    /// <summary>
    /// The top half of the gate is contacted.
    /// </summary>
    Top,

    /// <summary>
    /// The bottom half of the gate is contacted.
    /// </summary>
    Bottom
}
