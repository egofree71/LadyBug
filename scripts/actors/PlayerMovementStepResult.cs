using System;
using System.Collections.Generic;
using Godot;

namespace LadyBug.Actors;

/// <summary>
/// Represents one real one-pixel gameplay movement completed during a motor tick.
/// </summary>
/// <remarks>
/// A normal tick usually contains one segment. An assisted turn may contain two:
/// first an orthogonal alignment correction, then one pixel in the requested
/// direction. Reporting the full path lets gameplay systems consume collectibles
/// crossed during special turns instead of seeing only the final position.
/// </remarks>
public readonly struct PlayerMovementSegment
{
    /// <summary>
    /// Gets the gameplay position before this one-pixel segment.
    /// </summary>
    public Vector2I StartArcadePixelPos { get; }

    /// <summary>
    /// Gets the gameplay position after this one-pixel segment.
    /// </summary>
    public Vector2I EndArcadePixelPos { get; }

    /// <summary>
    /// Gets the direction used by this segment.
    /// </summary>
    public Vector2I Direction { get; }

    /// <summary>
    /// Initializes a new movement segment.
    /// </summary>
    public PlayerMovementSegment(
        Vector2I startArcadePixelPos,
        Vector2I endArcadePixelPos,
        Vector2I direction)
    {
        StartArcadePixelPos = startArcadePixelPos;
        EndArcadePixelPos = endArcadePixelPos;
        Direction = direction;
    }
}

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
/// - which one-pixel movement segments were actually completed?
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
    /// Gets the real one-pixel movement segments completed during this tick.
    /// </summary>
    /// <remarks>
    /// Normal movement has one segment. Assisted turns may have two segments in
    /// one tick. The list is empty when no committed pixel step occurred, even if
    /// the motor only snapped to a rail.
    /// </remarks>
    public IReadOnlyList<PlayerMovementSegment> MovementSegments { get; }

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
    public PlayerMovementStepResult(
        bool moved,
        bool directionChanged,
        bool stopped,
        Vector2I previousArcadePixelPos,
        Vector2I currentArcadePixelPos,
        Vector2I? snappedArcadePixelPos,
        IReadOnlyList<PlayerMovementSegment>? movementSegments,
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
        MovementSegments = movementSegments ?? Array.Empty<PlayerMovementSegment>();
        PreviousDirection = previousDirection;
        CurrentDirection = currentDirection;
        OffsetDirection = offsetDirection;
    }
}
