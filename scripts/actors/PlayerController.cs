using Godot;
using LadyBug.Gameplay.Maze;

/// <summary>
/// Controls the player using an arcade-style movement model.
/// </summary>
/// <remarks>
/// This controller currently combines:
/// input interpretation, fixed-tick movement simulation,
/// rail snapping, collision probing, and visual sprite updates.
///
/// The movement model is intentionally tuned to match the current
/// Lady Bug remake prototype:
/// - fixed tick rate
/// - integer arcade-pixel gameplay position
/// - rail-aligned movement inside 16x16 logical cells
/// - immediate facing update on input
/// - visual sprite offset changes only when movement is truly accepted
/// </remarks>
public partial class PlayerController : Node2D
{
    // Draw the cyan gameplay anchor in the scene.
    [Export]
    private bool _debugDrawAnchor = false;

    // Animated sprite that visually represents the player.
    private AnimatedSprite2D _animatedSprite;

    // Owning level. Provides maze access and coordinate conversion helpers.
    private Level _level;

    // Runtime logical maze used for movement legality checks.
    private MazeGrid _mazeGrid;

    // True gameplay position, expressed in original arcade pixels relative to the maze origin.
    private Vector2I _arcadePixelPos = Vector2I.Zero;

    // Direction currently used by effective movement.
    private Vector2I _currentDir = Vector2I.Zero;

    // Direction currently requested by held input.
    private Vector2I _wantedDir = Vector2I.Zero;

    // Direction currently shown by the sprite.
    private Vector2I _facingDir = Vector2I.Up;

    // Direction used to select the current sprite render offset.
    private Vector2I _offsetDir = Vector2I.Up;

    // Accumulates real frame time until one or more simulation ticks can run.
    private double _accumulator = 0.0;

    // Last-pressed directional input state.
    private readonly PlayerInputState _inputState = new();

    // Minimal result used when testing whether one pixel of movement is allowed.
    private struct StepCheckResult
    {
        public bool Allowed;
    }

    /// <summary>
    /// Retrieves the sprite node and applies the initial visual facing.
    /// </summary>
    public override void _Ready()
    {
        _animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        _animatedSprite.Position = Vector2.Zero;

        ApplyVisualFacing(_facingDir);
        QueueRedraw();
    }

    /// <summary>
    /// Advances the fixed-tick simulation and updates the rendered transforms.
    /// </summary>
    /// <param name="delta">Frame delta time in seconds.</param>
    public override void _Process(double delta)
    {
        if (_level == null || _mazeGrid == null)
            return;

        _accumulator += delta;

        while (_accumulator >= PlayerMovementTuning.TickDuration)
        {
            _accumulator -= PlayerMovementTuning.TickDuration;
            StepOneTick();
        }

        Position = _level.ArcadePixelToScenePosition(_arcadePixelPos);
        _animatedSprite.Position = GetSpriteRenderOffsetScene();

        if (_debugDrawAnchor)
            QueueRedraw();
    }

    /// <summary>
    /// Receives input events and forwards them to the player input state.
    /// </summary>
    /// <param name="event">Input event received from Godot.</param>
    public override void _Input(InputEvent @event)
    {
        _inputState.Update(@event);
    }

    /// <summary>
    /// Initializes the controller once the owning level and logical maze exist.
    /// </summary>
    /// <param name="level">Owning level that provides maze access and coordinate conversion.</param>
    public void Initialize(Level level)
    {
        _level = level;
        _mazeGrid = level.MazeGrid;

        _arcadePixelPos = level.LogicalCellToArcadePixel(level.PlayerStartCell);
        _currentDir = Vector2I.Zero;
        _wantedDir = Vector2I.Zero;
        _accumulator = 0.0;

        _inputState.InitializeFromCurrentInput();

        Position = _level.ArcadePixelToScenePosition(_arcadePixelPos);
        _animatedSprite.Position = GetSpriteRenderOffsetScene();
        ApplyVisualFacing(_facingDir);

        QueueRedraw();
    }

    /// <summary>
    /// Executes exactly one movement simulation tick.
    /// </summary>
    private void StepOneTick()
    {
        _wantedDir = _inputState.ReadPressedDirection();

        if (_wantedDir != Vector2I.Zero && _facingDir != _wantedDir)
        {
            _facingDir = _wantedDir;
            ApplyVisualFacing(_facingDir);
        }

        if (_wantedDir == Vector2I.Zero)
        {
            _currentDir = Vector2I.Zero;
            return;
        }

        bool isAtLogicalAnchor = IsExactlyOnLogicalCellAnchor();

        if (_currentDir == Vector2I.Zero)
        {
            Vector2I originalPixelPos = _arcadePixelPos;

            if (!CanStartOrResumeInDirection(_wantedDir))
                return;

            isAtLogicalAnchor = IsExactlyOnLogicalCellAnchor();

            StepCheckResult previewStep = EvaluateOnePixelStep(_wantedDir);
            if (!previewStep.Allowed)
            {
                _arcadePixelPos = originalPixelPos;
                return;
            }

            _currentDir = _wantedDir;
            _offsetDir = _currentDir;
        }
        else
        {
            bool wantsTurn =
                (_currentDir.X != 0 && _wantedDir.Y != 0) ||
                (_currentDir.Y != 0 && _wantedDir.X != 0);

            if (wantsTurn)
            {
                StepCheckResult turnPreview = EvaluateOnePixelStep(_wantedDir);

                if (isAtLogicalAnchor && turnPreview.Allowed)
                {
                    _currentDir = _wantedDir;
                    _offsetDir = _currentDir;
                }
                else if (!turnPreview.Allowed)
                {
                    _currentDir = Vector2I.Zero;
                    return;
                }
            }
            else
            {
                _currentDir = _wantedDir;
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

        Vector2I currentCell = _level.ArcadePixelToLogicalCell(_arcadePixelPos);
        Vector2I nextPixelPos = _arcadePixelPos + direction;
        Vector2I probePixel = nextPixelPos + GetCollisionLead(direction);
        Vector2I nextCell = _level.ArcadePixelToLogicalCell(probePixel);

        if (!_mazeGrid.IsInside(currentCell))
        {
            result.Allowed = false;
            return result;
        }

        if (nextCell == currentCell)
        {
            result.Allowed = true;
            return result;
        }

        result.Allowed = _mazeGrid.CanMove(currentCell, direction);
        return result;
    }

    /// <summary>
    /// Selects the sprite render offset according to the currently effective
    /// movement direction.
    /// </summary>
    /// <returns>Arcade-pixel render offset for the current effective direction.</returns>
    private Vector2I GetCurrentSpriteRenderOffsetArcade()
    {
        if (_offsetDir == Vector2I.Left)
            return PlayerMovementTuning.SpriteRenderOffsetLeftArcade;

        if (_offsetDir == Vector2I.Right)
            return PlayerMovementTuning.SpriteRenderOffsetRightArcade;

        return PlayerMovementTuning.SpriteRenderOffsetVerticalArcade;
    }

    /// <summary>
    /// Converts the current render offset from arcade pixels to scene pixels.
    /// </summary>
    /// <returns>
    /// Scene-space sprite offset relative to the gameplay anchor.
    /// Returns Vector2.Zero if the level is not yet initialized.
    /// </returns>
    private Vector2 GetSpriteRenderOffsetScene()
    {
        if (_level == null)
            return Vector2.Zero;

        return _level.ArcadeDeltaToSceneDelta(GetCurrentSpriteRenderOffsetArcade());
    }

    /// <summary>
    /// Applies the current facing to the animated sprite.
    /// </summary>
    /// <param name="direction">Direction to display.</param>
    private void ApplyVisualFacing(Vector2I direction)
    {
        if (direction == Vector2I.Zero || _animatedSprite == null)
            return;

        _animatedSprite.FlipH = false;
        _animatedSprite.FlipV = false;

        if (direction == Vector2I.Left)
        {
            _animatedSprite.Play("move_right");
            _animatedSprite.FlipH = true;
        }
        else if (direction == Vector2I.Right)
        {
            _animatedSprite.Play("move_right");
        }
        else if (direction == Vector2I.Up)
        {
            _animatedSprite.Play("move_up");
        }
        else if (direction == Vector2I.Down)
        {
            _animatedSprite.Play("move_up");
            _animatedSprite.FlipV = true;
        }
    }

    /// <summary>
    /// Draws the gameplay anchor when debug drawing is enabled.
    /// </summary>
    public override void _Draw()
    {
        if (!_debugDrawAnchor)
            return;

        DrawLine(new Vector2(-6, 0), new Vector2(6, 0), Colors.Cyan, 1.5f);
        DrawLine(new Vector2(0, -6), new Vector2(0, 6), Colors.Cyan, 1.5f);
        DrawCircle(Vector2.Zero, 2.0f, Colors.Cyan);
    }
}