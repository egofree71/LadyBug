namespace LadyBug.Gameplay.Gates;

/// <summary>
/// Diagonal visual used while a gate is briefly turning.
/// </summary>
/// <remarks>
/// These values correspond to the two diagonal transition sprites stored in
/// the rotating-gate spritesheet.
/// </remarks>
public enum GateTurningVisual
{
    /// <summary>
    /// The turning gate displays the slash diagonal: '/'.
    /// </summary>
    Slash,

    /// <summary>
    /// The turning gate displays the backslash diagonal: '\\'.
    /// </summary>
    Backslash
}
