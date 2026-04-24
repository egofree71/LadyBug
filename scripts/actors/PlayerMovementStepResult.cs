using System;
using System.Collections.Generic;
using Godot;

namespace LadyBug.Actors;

/// <summary>
/// Represents the outcome of one movement-motor simulation tick.
/// </summary>
/// <remarks>
/// Besides the final position and direction, this result exposes the real pixel
/// segments completed during the tick. That is important for assisted turns,
/// because one tick can contain both an alignment correction and a requested
/// movement step.
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
    /// Gets the intermediate snapped gameplay position when one rail snap occurred
    /// before any movement segment.
    /// </summary>
    public Vector2I? SnappedArcadePixelPos { get; }

    /// <summary>
    /// Gets the committed one-pixel movement segments completed during the tick.
    /// </summary>
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
