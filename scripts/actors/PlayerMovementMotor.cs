using Godot;
using LadyBug.Gameplay.Maze;

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
    // Owning level. Provides coordinate conversion helpers.
    private Level _level;

    // Runtime logical maze used for movement legality checks.
    private MazeGrid _mazeGrid;

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
    /// Gets the direction used to select the current sprite render offset.
    /// </summary>
    public Vector2I OffsetDir => _offsetDir;

    /// <summary>
    /// Initializes the movement motor from the owning level.
    /// </summary>
    /// <param name="level">Owning level that provides maze access and coordinate conversion.</param>
    public void Initialize(Level level)
    {
        _level = level;
        _mazeGrid = level.MazeGrid;
        _arcadePixelPos = level.LogicalCellToArcadePixel(level.PlayerStartCell);
        _currentDir = Vector2I.Zero;
        _offsetDir = Vector2I.Up;
    }

    /// <summary>
    /// Advances the movement simulation by exactly one tick.
    /// </summary>
    /// <param name="wantedDir">Currently intended movement direction.</param>
    /// <remarks>
    /// This method contains the gameplay-side movement rules:
    /// stop, resume, turn, rail snap, collision probe and one-pixel movement.
    /// </remarks>
    public void Step(Vector2I wantedDir)
    {
        if (wantedDir == Vector2I.Zero)
        {
            _currentDir = Vector2I.Zero;
            return;
        }

        bool isAtLogicalAnchor = IsExactlyOnLogicalCellAnchor();

        if (_currentDir == Vector2I.Zero)
        {
            Vector2I originalPixelPos = _arcadePixelPos;

            if (!CanStartOrResumeInDirection(wantedDir))
                return;

            isAtLogicalAnchor = IsExactlyOnLogicalCellAnchor();

            StepCheckResult previewStep = EvaluateOnePixelStep(wantedDir);
            if (!previewStep.Allowed)
            {
                _arcadePixelPos = originalPixelPos;
                return;
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
                StepCheckResult turnPreview = EvaluateOnePixelStep(wantedDir);

                if (isAtLogicalAnchor && turnPreview.Allowed)
                {
                    _currentDir = wantedDir;
                    _offsetDir = _currentDir;
                }
                else if (!turnPreview.Allowed)
                {
                    _currentDir = Vector2I.Zero;
                    return;
                }
                else
                {
                    // Buffered turn: keep current direction until the anchor.
                }
            }
            else
            {
                _currentDir = wantedDir;
                _offsetDir = _currentDir;
            }
        }

        StepCheckResult step = EvaluateOnePixelStep(_currentDir);

        if (!step.Allowed)
        {
            _currentDir = Vector2I.Zero;
            return;
        }

        _arcadePixelPos += _currentDir;
    }

    /// <summary>
    /// Returns whether the gameplay position exactly matches the logical anchor
    /// of its current cell.
    /// </summary>
    /// <returns>
    /// True when the player is exactly on the current cell anchor; otherwise false.
    /// </returns>
    private bool IsExactlyOnLogicalCellAnchor()
    {
        Vector2I logicalCell = _level.ArcadePixelToLogicalCell(_arcadePixelPos);
        Vector2I anchorPixel = _level.LogicalCellToArcadePixel(logicalCell);
        return _arcadePixelPos == anchorPixel;
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
        if (direction == Vector2I.Zero)
            return false;

        Vector2I currentCell = _level.ArcadePixelToLogicalCell(_arcadePixelPos);
        Vector2I anchor = _level.LogicalCellToArcadePixel(currentCell);

        if (direction.X != 0)
        {
            int deltaY = _arcadePixelPos.Y - anchor.Y;
            if (Mathf.Abs(deltaY) <= PlayerMovementTuning.HorizontalRailSnapTolerance)
            {
                _arcadePixelPos = new Vector2I(_arcadePixelPos.X, anchor.Y);
                return true;
            }

            return false;
        }

        if (direction.Y != 0)
        {
            int deltaX = _arcadePixelPos.X - anchor.X;
            if (Mathf.Abs(deltaX) <= PlayerMovementTuning.VerticalRailSnapTolerance)
            {
                _arcadePixelPos = new Vector2I(anchor.X, _arcadePixelPos.Y);
                return true;
            }

            return false;
        }

        return false;
    }

    /// <summary>
    /// Determines whether movement can start or resume in the requested direction.
    /// </summary>
    /// <param name="direction">Requested direction.</param>
    /// <returns>
    /// True if movement can start or resume; otherwise false.
    /// </returns>
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
    /// A step evaluation result indicating whether movement is allowed.
    /// </returns>
    private StepCheckResult EvaluateOnePixelStep(Vector2I direction)
    {
        StepCheckResult result = new();

        if (direction == Vector2I.Zero)
        {
            result.Allowed = false;
            return result;
        }

        MazeStepResult mazeStep = _mazeGrid.EvaluateArcadePixelStep(
            _arcadePixelPos,
            direction,
            GetCollisionLead(direction),
            _level.ArcadePixelToLogicalCell);

        result.Allowed = mazeStep.Allowed;
        return result;
    }

    // Minimal result used when testing whether one pixel of movement is allowed.
    private struct StepCheckResult
    {
        public bool Allowed;
    }
}
