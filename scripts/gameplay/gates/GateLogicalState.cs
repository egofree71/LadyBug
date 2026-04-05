namespace LadyBug.Gameplay.Gates;

/// <summary>
/// Logical blocking state of a rotating gate.
/// </summary>
/// <remarks>
/// The names here describe which movement axis is currently blocked,
/// not the visual orientation seen on screen.
///
/// This avoids ambiguity:
/// - a gate that looks vertical blocks left/right movement
/// - a gate that looks horizontal blocks up/down movement
/// </remarks>
public enum GateLogicalState
{
    /// <summary>
    /// The gate currently blocks horizontal movement (left/right).
    /// </summary>
    BlocksHorizontal,

    /// <summary>
    /// The gate currently blocks vertical movement (up/down).
    /// </summary>
    BlocksVertical
}
