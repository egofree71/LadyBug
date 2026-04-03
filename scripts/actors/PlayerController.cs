using Godot;
using LadyBug.Gameplay.Maze;

/// <summary>
/// Arcade-style player controller with fixed-tick movement,
/// buffered input (last pressed wins), rail snapping and
/// direction-specific visual offsets / collision probes.
/// </summary>
public partial class PlayerController : Node2D
{
    // --- Timing -------------------------------------------------------------

    private const double TickRate = 60.1145;
    private const double TickDuration = 1.0 / TickRate;

    // --- Rail snap tolerances ----------------------------------------------

    private const int HorizontalRailSnapTolerance = 1;
    private const int VerticalRailSnapTolerance = 1;

    // --- Visual offsets -----------------------------------------------------

    private static readonly Vector2I SpriteRenderOffsetLeftArcade = new(5, 8);
    private static readonly Vector2I SpriteRenderOffsetRightArcade = new(4, 8);
    private static readonly Vector2I SpriteRenderOffsetVerticalArcade = new(5, 7);

    // --- Debug --------------------------------------------------------------

    [Export]
    private bool _debugDrawAnchor = false;

    // --- Directional collision probe ---------------------------------------

    private const int CollisionLeadLeft = 8;
    private const int CollisionLeadRight = 6;
    private const int CollisionLeadUp = 9;
    private const int CollisionLeadDown = 6;

    // --- Nodes --------------------------------------------------------------

    private AnimatedSprite2D _animatedSprite;

    // --- References ---------------------------------------------------------

    private Level _level;
    private MazeGrid _mazeGrid;

    // --- Gameplay Position --------------------------------------------------

    /// <summary>
    /// Player position in original arcade pixels, relative to the maze origin.
    /// </summary>
    private Vector2I _arcadePixelPos = Vector2I.Zero;

    // --- Movement State -----------------------------------------------------

    /// <summary>
    /// Direction currently used by actual movement.
    /// </summary>
    private Vector2I _currentDir = Vector2I.Zero;

    /// <summary>
    /// Direction currently requested by held input.
    /// </summary>
    private Vector2I _wantedDir = Vector2I.Zero;

    /// <summary>
    /// Direction currently displayed by the sprite.
    /// This changes immediately when the player presses a direction.
    /// </summary>
    private Vector2I _facingDir = Vector2I.Up;

    /// <summary>
    /// Direction used to select the sprite render offset.
    /// This changes only when movement is actually accepted.
    /// </summary>
    private Vector2I _offsetDir = Vector2I.Up;

    /// <summary>
    /// Accumulates frame time until one or more simulation ticks can be run.
    /// </summary>
    private double _accumulator = 0.0;

    // --- Direction input state ---------------------------------------------

    private bool _leftPressed;
    private bool _rightPressed;
    private bool _upPressed;
    private bool _downPressed;

    private long _leftPressOrder;
    private long _rightPressOrder;
    private long _upPressOrder;
    private long _downPressOrder;

    private long _pressOrderCounter;

    // --- Step evaluation ----------------------------------------------------

    private struct StepCheckResult
    {
        public bool Allowed;
    }

    // --- Lifecycle ----------------------------------------------------------

    public override void _Ready()
    {
        _animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        _animatedSprite.Position = Vector2.Zero;

        ApplyVisualFacing(_facingDir);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (_level == null || _mazeGrid == null)
            return;

        _accumulator += delta;

        while (_accumulator >= TickDuration)
        {
            _accumulator -= TickDuration;
            StepOneTick();
        }

        // Node position = gameplay anchor in scene space
        Position = _level.ArcadePixelToScenePosition(_arcadePixelPos);

        // Sprite position = render-only offset relative to gameplay anchor
        _animatedSprite.Position = GetSpriteRenderOffsetScene();

        if (_debugDrawAnchor)
            QueueRedraw();
    }

    public override void _Input(InputEvent @event)
    {
        UpdateDirectionActionState(@event, "move_left", Vector2I.Left);
        UpdateDirectionActionState(@event, "move_right", Vector2I.Right);
        UpdateDirectionActionState(@event, "move_up", Vector2I.Up);
        UpdateDirectionActionState(@event, "move_down", Vector2I.Down);
    }

    // --- Initialization -----------------------------------------------------

    public void Initialize(Level level)
    {
        _level = level;
        _mazeGrid = level.MazeGrid;

        _arcadePixelPos = level.LogicalCellToArcadePixel(level.PlayerStartCell);
        _currentDir = Vector2I.Zero;
        _wantedDir = Vector2I.Zero;
        _accumulator = 0.0;

        _leftPressed = Input.IsActionPressed("move_left");
        _rightPressed = Input.IsActionPressed("move_right");
        _upPressed = Input.IsActionPressed("move_up");
        _downPressed = Input.IsActionPressed("move_down");

        _leftPressOrder = 0;
        _rightPressOrder = 0;
        _upPressOrder = 0;
        _downPressOrder = 0;
        _pressOrderCounter = 0;

        Position = _level.ArcadePixelToScenePosition(_arcadePixelPos);
        _animatedSprite.Position = GetSpriteRenderOffsetScene();
        ApplyVisualFacing(_facingDir);

        QueueRedraw();
    }

    // --- Tick Simulation ----------------------------------------------------

    private void StepOneTick()
    {
        _wantedDir = ReadPressedDirection();

        // The direction displayed by the sprite follows the player's input immediately.
        if (_wantedDir != Vector2I.Zero && _facingDir != _wantedDir)
        {
            _facingDir = _wantedDir;
            ApplyVisualFacing(_facingDir);
        }

        // Releasing input stops movement immediately.
        if (_wantedDir == Vector2I.Zero)
        {
            _currentDir = Vector2I.Zero;
            return;
        }

        bool isAtLogicalAnchor = IsExactlyOnLogicalCellAnchor();

        // Case 1: starting or resuming from rest.
        if (_currentDir == Vector2I.Zero)
        {
            if (!CanStartOrResumeInDirection(_wantedDir))
                return;

            // Recalculate after potential snap.
            isAtLogicalAnchor = IsExactlyOnLogicalCellAnchor();

            StepCheckResult previewStep = EvaluateOnePixelStep(_wantedDir);
            if (!previewStep.Allowed)
                return;

            _currentDir = _wantedDir;
            _offsetDir = _currentDir;
        }
        else
        {
            // Case 2: direction change while already moving.
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
                    // If the newly requested perpendicular direction is blocked,
                    // the player stops instead of continuing in the old direction.
                    _currentDir = Vector2I.Zero;
                    return;
                }
                else
                {
                    // Turn buffered: keep moving until the logical anchor.
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

    // --- Movement Rules -----------------------------------------------------

    private bool IsExactlyOnLogicalCellAnchor()
    {
        Vector2I logicalCell = _level.ArcadePixelToLogicalCell(_arcadePixelPos);
        Vector2I anchorPixel = _level.LogicalCellToArcadePixel(logicalCell);
        return _arcadePixelPos == anchorPixel;
    }

    private bool TrySnapToRailForDirection(Vector2I direction)
    {
        if (direction == Vector2I.Zero)
            return false;

        Vector2I currentCell = _level.ArcadePixelToLogicalCell(_arcadePixelPos);
        Vector2I anchor = _level.LogicalCellToArcadePixel(currentCell);

        if (direction.X != 0)
        {
            int deltaY = _arcadePixelPos.Y - anchor.Y;
            if (Mathf.Abs(deltaY) <= HorizontalRailSnapTolerance)
            {
                _arcadePixelPos = new Vector2I(_arcadePixelPos.X, anchor.Y);
                return true;
            }

            return false;
        }

        if (direction.Y != 0)
        {
            int deltaX = _arcadePixelPos.X - anchor.X;
            if (Mathf.Abs(deltaX) <= VerticalRailSnapTolerance)
            {
                _arcadePixelPos = new Vector2I(anchor.X, _arcadePixelPos.Y);
                return true;
            }

            return false;
        }

        return false;
    }

    private bool CanStartOrResumeInDirection(Vector2I direction)
    {
        return TrySnapToRailForDirection(direction);
    }

    private Vector2I GetCollisionLead(Vector2I direction)
    {
        if (direction == Vector2I.Left)
            return new Vector2I(-CollisionLeadLeft, 0);

        if (direction == Vector2I.Right)
            return new Vector2I(CollisionLeadRight, 0);

        if (direction == Vector2I.Up)
            return new Vector2I(0, -CollisionLeadUp);

        if (direction == Vector2I.Down)
            return new Vector2I(0, CollisionLeadDown);

        return Vector2I.Zero;
    }

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

    // --- Input --------------------------------------------------------------

    private void UpdateDirectionActionState(InputEvent @event, string actionName, Vector2I direction)
    {
        if (@event.IsActionPressed(actionName))
        {
            _pressOrderCounter++;

            if (direction == Vector2I.Left)
            {
                _leftPressed = true;
                _leftPressOrder = _pressOrderCounter;
            }
            else if (direction == Vector2I.Right)
            {
                _rightPressed = true;
                _rightPressOrder = _pressOrderCounter;
            }
            else if (direction == Vector2I.Up)
            {
                _upPressed = true;
                _upPressOrder = _pressOrderCounter;
            }
            else if (direction == Vector2I.Down)
            {
                _downPressed = true;
                _downPressOrder = _pressOrderCounter;
            }
        }

        if (@event.IsActionReleased(actionName))
        {
            if (direction == Vector2I.Left)
                _leftPressed = false;
            else if (direction == Vector2I.Right)
                _rightPressed = false;
            else if (direction == Vector2I.Up)
                _upPressed = false;
            else if (direction == Vector2I.Down)
                _downPressed = false;
        }
    }

    private Vector2I ReadPressedDirection()
    {
        Vector2I bestDirection = Vector2I.Zero;
        long bestOrder = long.MinValue;

        if (_leftPressed && _leftPressOrder > bestOrder)
        {
            bestOrder = _leftPressOrder;
            bestDirection = Vector2I.Left;
        }

        if (_rightPressed && _rightPressOrder > bestOrder)
        {
            bestOrder = _rightPressOrder;
            bestDirection = Vector2I.Right;
        }

        if (_upPressed && _upPressOrder > bestOrder)
        {
            bestOrder = _upPressOrder;
            bestDirection = Vector2I.Up;
        }

        if (_downPressed && _downPressOrder > bestOrder)
        {
            bestOrder = _downPressOrder;
            bestDirection = Vector2I.Down;
        }

        return bestDirection;
    }

    // --- Rendering ----------------------------------------------------------

    private Vector2I GetCurrentSpriteRenderOffsetArcade()
    {
        if (_offsetDir == Vector2I.Left)
            return SpriteRenderOffsetLeftArcade;

        if (_offsetDir == Vector2I.Right)
            return SpriteRenderOffsetRightArcade;

        return SpriteRenderOffsetVerticalArcade;
    }

    private Vector2 GetSpriteRenderOffsetScene()
    {
        if (_level == null)
            return Vector2.Zero;

        return _level.ArcadeDeltaToSceneDelta(GetCurrentSpriteRenderOffsetArcade());
    }

    // --- Animation ----------------------------------------------------------

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
}
