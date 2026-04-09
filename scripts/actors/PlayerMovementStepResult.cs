using Godot;

namespace LadyBug.Actors;

/// <summary>
/// Represents the outcome of one movement-motor simulation tick.
/// </summary>
/// <remarks>
/// This result is intentionally small but expressive enough for callers that
/// want to react to movement state changes without re-deriving them manually.
///
/// It answers questions such as:
/// - did the actor move?
/// - did the effective movement direction change?
/// - did the actor stop during this tick?
/// - did the motor snap to a different rail before the final step?
/// </remarks>
public readonly struct PlayerMovementStepResult
{
    /// <summary>
    /// Gets whether the gameplay position changed during the tick.
    /// </summary>
    public bool Moved { get; }

    /// <summary>
    /// Gets whether the effective movement direction changed during the tick.
    /// </summary>
    public bool DirectionChanged { get; }

    /// <summary>
    /// Gets whether the actor stopped during this tick.
    /// </summary>
    public bool Stopped { get; }

    /// <summary>
    /// Gets the gameplay position before the tick.
    /// </summary>
    public Vector2I PreviousArcadePixelPos { get; }

    /// <summary>
    /// Gets the gameplay position after the tick.
    /// </summary>
    public Vector2I CurrentArcadePixelPos { get; }

    /// <summary>
    /// Gets the intermediate snapped gameplay position when one rail snap
    /// occurred during the tick.
    /// </summary>
    /// <remarks>
    /// This is <see langword="null"/> when the tick did not perform any
    /// intermediate snap before the final movement step.
    /// </remarks>
    public Vector2I? SnappedArcadePixelPos { get; }

    /// <summary>
    /// Gets the effective movement direction before the tick.
    /// </summary>
    public Vector2I PreviousDirection { get; }

    /// <summary>
    /// Gets the effective movement direction after the tick.
    /// </summary>
    public Vector2I CurrentDirection { get; }

    /// <summary>
    /// Gets the render-offset direction after the tick.
    /// </summary>
    public Vector2I OffsetDirection { get; }

    /// <summary>
    /// Initializes a new movement step result.
    /// </summary>
    /// <param name="moved">Whether the gameplay position changed during the tick.</param>
    /// <param name="directionChanged">Whether the effective movement direction changed during the tick.</param>
    /// <param name="stopped">Whether the actor stopped during this tick.</param>
    /// <param name="previousArcadePixelPos">Gameplay position before the tick.</param>
    /// <param name="currentArcadePixelPos">Gameplay position after the tick.</param>
    /// <param name="snappedArcadePixelPos">
    /// Intermediate snapped gameplay position reached during the tick,
    /// or <see langword="null"/> when no snap occurred.
    /// </param>
    /// <param name="previousDirection">Effective movement direction before the tick.</param>
    /// <param name="currentDirection">Effective movement direction after the tick.</param>
    /// <param name="offsetDirection">Render-offset direction after the tick.</param>
    public PlayerMovementStepResult(
        bool moved,
        bool directionChanged,
        bool stopped,
        Vector2I previousArcadePixelPos,
        Vector2I currentArcadePixelPos,
        Vector2I? snappedArcadePixelPos,
        Vector2I previousDirection,
        Vector2I currentDirection,
        Vector2I offsetDirection)
    {
        Moved = moved;
        DirectionChanged = directionChanged;
        Stopped = stopped;
        PreviousArcadePixelPos = previousArcadePixelPos;
        CurrentArcadePixelPos = currentArcadePixelPos;
        SnappedArcadePixelPos = snappedArcadePixelPos;
        PreviousDirection = previousDirection;
        CurrentDirection = currentDirection;
        OffsetDirection = offsetDirection;
    }
}
