namespace LadyBug.Gameplay;

/// <summary>
/// High-level result kind for one attempted playfield movement step.
/// </summary>
/// <remarks>
/// Unlike MazeStepResult, this describes the combined runtime playfield outcome:
/// static maze + dynamic gate overlay.
/// </remarks>
public enum PlayfieldStepKind
{
    /// <summary>
    /// The attempted step is allowed.
    /// </summary>
    Allowed,

    /// <summary>
    /// The attempted step is blocked by the static maze.
    /// </summary>
    BlockedByFixedWall,

    /// <summary>
    /// The attempted step is blocked by a rotating gate.
    /// </summary>
    BlockedByGate
}