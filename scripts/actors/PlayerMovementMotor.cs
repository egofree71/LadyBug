using Godot;
using LadyBug.Gameplay;
using LadyBug.Gameplay.Gates;
using LadyBug.Gameplay.Maze;

namespace LadyBug.Actors;

/// <summary>
/// Executes the player's arcade-style movement simulation.
/// </summary>
/// <remarks>
/// This class owns the gameplay movement state:
/// arcade-pixel position, effective movement direction,
/// and the direction used to select the render offset.
///
/// It does not deal with:
/// - raw input collection
/// - sprite facing animation
/// - scene graph rendering
///
/// Its role is to advance the gameplay state by one fixed simulation tick,
/// given the currently intended direction.
/// </remarks>
public sealed class PlayerMovementMotor
{
    // Owning level. Provides coordinate conversion helpers and playfield queries.
    private Level _level = null!;

    // True gameplay position, expressed in original arcade pixels relative to the maze origin.
    private Vector2I _arcadePixelPos = Vector2I.Zero;

    // Direction currently used by effective movement.
    private Vector2I _currentDir = Vector2I.Zero;

    // Direction used to select the current sprite render offset.
    private Vector2I _offsetDir = Vector2I.Up;

    /// <summary>
    /// Gets the current gameplay position in arcade pixels.
    /// </summary>
    public Vector2I ArcadePixelPos => _arcadePixelPos;

    /// <summary>
    /// Gets the current effective movement direction.
    /// </summary>
    public Vector2I CurrentDir => _currentDir;

    /// <summary>
    /// Gets the direction used to select the current sprite render offset.
    /// </summary>
    public Vector2I OffsetDir => _offsetDir;

    /// <summary>
    /// Initializes the movement motor from the owning level.
    /// </summary>
    /// <param name="level">
    /// Owning level that provides playfield access and coordinate conversion.
    /// </param>
    public void Initialize(Level level)
    {
        _level = level;
        _arcadePixelPos = level.LogicalCellToArcadePixel(level.PlayerStartCell);
        _currentDir = Vector2I.Zero;
        _offsetDir = Vector2I.Up;
    }

    /// <summary>
    /// Advances the movement simulation by exactly one tick.
    /// </summary>
    /// <param name="wantedDir">Currently intended movement direction.</param>
    /// <returns>A structured result describing what changed during this tick.</returns>
    public PlayerMovementStepResult Step(Vector2I wantedDir)
    {
        Vector2I previousPixelPos = _arcadePixelPos;
        Vector2I previousDirection = _currentDir;
        Vector2I? snappedArcadePixelPos = null;

        if (wantedDir == Vector2I.Zero)
        {
            _currentDir = Vector2I.Zero;
            return BuildStepResult(previousPixelPos, previousDirection, snappedArcadePixelPos);
        }

        if (_currentDir == Vector2I.Zero)
        {
            Vector2I originalPixelPos = _arcadePixelPos;

            if (!CanStartOrResumeInDirection(wantedDir))
            {
                return BuildStepResult(previousPixelPos, previousDirection, snappedArcadePixelPos);
            }

            if (_arcadePixelPos != originalPixelPos)
            {
                snappedArcadePixelPos = _arcadePixelPos;
            }

            PlayfieldStepResult previewStep = EvaluateOnePixelStep(wantedDir);
            previewStep = ResolveGatePushIfNeeded(previewStep, wantedDir);

            if (!previewStep.Allowed)
            {
                // Important:
                // keep any rail snap that was just applied, but do not advance into
                // the blocked direction. This matches the expected "alignment
                // correction without movement" behavior seen near logical-cell
                // centers and maze corners.
                _currentDir = Vector2I.Zero;
                return BuildStepResult(previousPixelPos, previousDirection, snappedArcadePixelPos);
            }

            _currentDir = wantedDir;
            _offsetDir = _currentDir;
        }
        else
        {
            bool wantsTurn =
                (_currentDir.X != 0 && wantedDir.Y != 0) ||
                (_currentDir.Y != 0 && wantedDir.X != 0);

            if (wantsTurn)
            {
                if (CanAttemptPerpendicularTurnNow(wantedDir))
                {
                    Vector2I originalPixelPos = _arcadePixelPos;
                    TrySnapToRailForDirection(wantedDir);

                    if (_arcadePixelPos != originalPixelPos)
                    {
                        snappedArcadePixelPos = _arcadePixelPos;
                    }

                    PlayfieldStepResult turnPreview = EvaluateOnePixelStep(wantedDir);
                    turnPreview = ResolveGatePushIfNeeded(turnPreview, wantedDir);

                    if (turnPreview.Allowed)
                    {
                        _currentDir = wantedDir;
                        _offsetDir = _currentDir;
                    }
                    else
                    {
                        // Keep the snapped alignment, but do not move into the
                        // blocked perpendicular direction.
                        _currentDir = Vector2I.Zero;
                        return BuildStepResult(previousPixelPos, previousDirection, snappedArcadePixelPos);
                    }
                }
                else
                {
                    // Buffered turn: keep current direction until we enter the turn window.
                }
            }
            else
            {
                _currentDir = wantedDir;
                _offsetDir = _currentDir;
            }
        }

        PlayfieldStepResult step = EvaluateOnePixelStep(_currentDir);
        step = ResolveGatePushIfNeeded(step, _currentDir);

        if (!step.Allowed)
        {
            _currentDir = Vector2I.Zero;
            return BuildStepResult(previousPixelPos, previousDirection, snappedArcadePixelPos);
        }

        _arcadePixelPos += _currentDir;
        RecenterOnCurrentRail();

        return BuildStepResult(previousPixelPos, previousDirection, snappedArcadePixelPos);
    }

    /// <summary>
    /// Resolves a gate push immediately when a step is blocked by a pushable gate,
    /// then re-evaluates the same step with the updated gate state.
    /// </summary>
    /// <param name="step">Original evaluated playfield step.</param>
    /// <param name="direction">Attempted gameplay movement direction.</param>
    /// <returns>
    /// The original step if no gate push is possible, otherwise the re-evaluated
    /// step after a successful gate push.
    /// </returns>
    private PlayfieldStepResult ResolveGatePushIfNeeded(
        PlayfieldStepResult step,
        Vector2I direction)
    {
        if (step.Kind == PlayfieldStepKind.BlockedByGate &&
            step.GateId.HasValue &&
            step.ContactHalf.HasValue &&
            _level.TryPushGate(step.GateId.Value, direction, step.ContactHalf.Value))
        {
            return EvaluateOnePixelStep(direction);
        }

        return step;
    }

    /// <summary>
    /// Determines whether a perpendicular turn may be attempted on this tick.
    /// </summary>
    /// <param name="wantedDir">Requested perpendicular direction.</param>
    /// <returns>
    /// True when the player is already close enough to the required rail for that
    /// direction; otherwise false.
    /// </returns>
    /// <remarks>
    /// This is intentionally more permissive than requiring an exact logical anchor.
    /// It allows turns as soon as the player is within the existing rail snap window,
    /// while also preventing premature gate pushes when the player is still clearly
    /// between two turn opportunities.
    /// </remarks>
    private bool CanAttemptPerpendicularTurnNow(Vector2I wantedDir)
    {
        return CanSnapToRailForDirection(wantedDir);
    }

    /// <summary>
    /// Returns whether the player is already close enough to the movement rail
    /// required by the given direction.
    /// </summary>
    /// <param name="direction">Direction whose rail is being tested.</param>
    /// <returns>
    /// True if the player is close enough to the required rail; otherwise false.
    /// </returns>
    private bool CanSnapToRailForDirection(Vector2I direction)
    {
        if (direction == Vector2I.Zero)
            return false;

        Vector2I currentCell = _level.ArcadePixelToLogicalCell(_arcadePixelPos);
        Vector2I anchor = _level.LogicalCellToArcadePixel(currentCell);

        if (direction.X != 0)
        {
            int deltaY = _arcadePixelPos.Y - anchor.Y;
            return Mathf.Abs(deltaY) <= PlayerMovementTuning.HorizontalRailSnapTolerance;
        }

        if (direction.Y != 0)
        {
            int deltaX = _arcadePixelPos.X - anchor.X;
            return Mathf.Abs(deltaX) <= PlayerMovementTuning.VerticalRailSnapTolerance;
        }

        return false;
    }

    /// <summary>
    /// Attempts to snap the player to the movement rail required by the given direction.
    /// </summary>
    /// <param name="direction">Direction the player wants to start or resume.</param>
    /// <returns>
    /// True if the player was close enough to the required rail and was snapped
    /// successfully; otherwise false.
    /// </returns>
    private bool TrySnapToRailForDirection(Vector2I direction)
    {
        if (!CanSnapToRailForDirection(direction))
            return false;

        Vector2I currentCell = _level.ArcadePixelToLogicalCell(_arcadePixelPos);
        Vector2I anchor = _level.LogicalCellToArcadePixel(currentCell);

        if (direction.X != 0)
        {
            _arcadePixelPos = new Vector2I(_arcadePixelPos.X, anchor.Y);
            return true;
        }

        if (direction.Y != 0)
        {
            _arcadePixelPos = new Vector2I(anchor.X, _arcadePixelPos.Y);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Determines whether movement can start or resume in the requested direction.
    /// </summary>
    /// <param name="direction">Requested direction.</param>
    /// <returns>True if movement can start or resume; otherwise false.</returns>
    private bool CanStartOrResumeInDirection(Vector2I direction)
    {
        return TrySnapToRailForDirection(direction);
    }

    /// <summary>
    /// Returns the directional collision probe offset used for step validation.
    /// </summary>
    /// <param name="direction">Direction being tested.</param>
    /// <returns>Arcade-pixel offset applied to the forward collision probe.</returns>
    private Vector2I GetCollisionLead(Vector2I direction)
    {
        if (direction == Vector2I.Left)
            return new Vector2I(-PlayerMovementTuning.CollisionLeadLeft, 0);

        if (direction == Vector2I.Right)
            return new Vector2I(PlayerMovementTuning.CollisionLeadRight, 0);

        if (direction == Vector2I.Up)
            return new Vector2I(0, -PlayerMovementTuning.CollisionLeadUp);

        if (direction == Vector2I.Down)
            return new Vector2I(0, PlayerMovementTuning.CollisionLeadDown);

        return Vector2I.Zero;
    }

    /// <summary>
    /// Tests whether one pixel of movement is currently legal.
    /// </summary>
    /// <param name="direction">Direction to test.</param>
    /// <returns>
    /// A combined playfield result indicating whether movement is allowed.
    /// </returns>
    private PlayfieldStepResult EvaluateOnePixelStep(Vector2I direction)
    {
        if (direction == Vector2I.Zero)
        {
            MazeStepResult blockedMazeStep = new(false, Vector2I.Zero, Vector2I.Zero);
            return PlayfieldStepResult.BlockedByFixedWall(blockedMazeStep);
        }

        return _level.EvaluateArcadePixelStepWithGates(
            _arcadePixelPos,
            direction,
            GetCollisionLead(direction));
    }

    /// <summary>
    /// Re-centers the gameplay position on the current rail after a successful step.
    /// </summary>
    /// <remarks>
    /// This is intentionally conservative.
    /// It only snaps the orthogonal axis when the deviation from the current
    /// rail anchor is already within the existing rail snap tolerance.
    ///
    /// The goal is not to introduce new movement behavior, but to stabilize
    /// the actor on its lane during straight movement.
    /// </remarks>
    private void RecenterOnCurrentRail()
    {
        if (_currentDir == Vector2I.Zero)
            return;

        Vector2I currentCell = _level.ArcadePixelToLogicalCell(_arcadePixelPos);
        Vector2I anchor = _level.LogicalCellToArcadePixel(currentCell);

        if (_currentDir.X != 0)
        {
            int deltaY = _arcadePixelPos.Y - anchor.Y;
            if (Mathf.Abs(deltaY) <= PlayerMovementTuning.HorizontalRailSnapTolerance)
                _arcadePixelPos = new Vector2I(_arcadePixelPos.X, anchor.Y);

            return;
        }

        if (_currentDir.Y != 0)
        {
            int deltaX = _arcadePixelPos.X - anchor.X;
            if (Mathf.Abs(deltaX) <= PlayerMovementTuning.VerticalRailSnapTolerance)
                _arcadePixelPos = new Vector2I(anchor.X, _arcadePixelPos.Y);
        }
    }

    /// <summary>
    /// Builds the structured result for the tick that just completed.
    /// </summary>
    /// <param name="previousPixelPos">Gameplay position before the tick.</param>
    /// <param name="previousDirection">Effective movement direction before the tick.</param>
    /// <param name="snappedArcadePixelPos">
    /// Intermediate gameplay position reached by one rail snap during the tick,
    /// or <see langword="null"/> when no snap occurred.
    /// </param>
    /// <returns>A structured result describing the changes of the tick.</returns>
    private PlayerMovementStepResult BuildStepResult(
        Vector2I previousPixelPos,
        Vector2I previousDirection,
        Vector2I? snappedArcadePixelPos)
    {
        bool moved = _arcadePixelPos != previousPixelPos;
        bool directionChanged = _currentDir != previousDirection;
        bool stopped = previousDirection != Vector2I.Zero && _currentDir == Vector2I.Zero;

        return new PlayerMovementStepResult(
            moved,
            directionChanged,
            stopped,
            previousPixelPos,
            _arcadePixelPos,
            snappedArcadePixelPos,
            previousDirection,
            _currentDir,
            _offsetDir);
    }
}
