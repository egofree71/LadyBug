namespace LadyBug.Gameplay.Gates;

/// <summary>
/// Centralizes stable tuning values for rotating gate behavior.
/// </summary>
/// <remarks>
/// These values are intentionally small and easy to adjust later if
/// further reverse engineering refines the exact arcade timing.
/// </remarks>
public static class GateTuning
{
    /// <summary>
    /// Number of simulation ticks during which a gate remains visually
    /// in its turning state after a push has been accepted.
    /// </summary>
    public const int TurningTicks = 2;
}