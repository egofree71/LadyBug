namespace LadyBug.Actors;

/// <summary>
/// Describes the high-level path selected for one requested movement direction.
/// </summary>
internal enum PlayerTurnPath
{
    /// <summary>
    /// Use the ordinary request-latch and straight movement path.
    /// </summary>
    Normal,

    /// <summary>
    /// Enter or continue the assisted turn path.
    /// </summary>
    Assisted,

    /// <summary>
    /// Apply one close-range alignment assist before returning to the normal path.
    /// </summary>
    CloseRangeAssistThenNormal,
}
