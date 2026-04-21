using Godot;

namespace LadyBug.Actors;

/// <summary>
/// Controls the player node and orchestrates input, movement and rendering.
/// </summary>
/// <remarks>
/// This class is intentionally light.
///
/// It mainly does three things:
/// - read the current intended direction from the input state
/// - advance the movement motor by fixed ticks
/// - apply sprite facing and rendering based on the resulting gameplay state
///
/// The gameplay movement rules themselves live in <c>PlayerMovementMotor</c>.
/// </remarks>
public partial class PlayerController : Node2D
{
    // Draw the cyan gameplay anchor in the scene.
    [Export]
    private bool _debugDrawAnchor = false;

    // Animated sprite that visually represents the player.
    private AnimatedSprite2D _animatedSprite = null!;

    // Owning level. Provides coordinate conversion helpers.
    private Level _level = null!;

    // Last-pressed directional input state.
    private readonly PlayerInputState _inputState = new();

    // Gameplay movement motor.
    private readonly PlayerMovementMotor _movementMotor = new();

    // Direction currently shown by the sprite.
    private Vector2I _facingDir = Vector2I.Up;

    // Accumulates real frame time until one or more simulation ticks can run.
    private double _accumulator = 0.0;

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
        if (_level == null)
            return;

        _accumulator += delta;

        while (_accumulator >= PlayerMovementTuning.TickDuration)
        {
            _accumulator -= PlayerMovementTuning.TickDuration;
            RunOneTick();
        }

        Position = _level.ArcadePixelToScenePosition(_movementMotor.ArcadePixelPos);
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
        _accumulator = 0.0;

        _inputState.InitializeFromCurrentInput();
        _movementMotor.Initialize(level);

        Position = _level.ArcadePixelToScenePosition(_movementMotor.ArcadePixelPos);
        _animatedSprite.Position = GetSpriteRenderOffsetScene();
        ApplyVisualFacing(_facingDir);

        QueueRedraw();
    }

    /// <summary>
    /// Executes exactly one controller tick.
    /// </summary>
    /// <remarks>
    /// Collectible consumption is evaluated in two stages:
    /// - first at the exact snapped anchor reached during a perpendicular turn
    /// - then across the final movement segment of the tick
    ///
    /// This allows flowers to be consumed reliably in turns without making them
    /// disappear too early when entering the next logical cell.
    /// </remarks>
    private void RunOneTick()
    {
        _level.AdvanceGateSimulationOneTick();

        Vector2I wantedDir = _inputState.ReadPressedDirection();

        if (wantedDir != Vector2I.Zero && _facingDir != wantedDir)
        {
            _facingDir = wantedDir;
            ApplyVisualFacing(_facingDir);
        }

        PlayerMovementStepResult stepResult = _movementMotor.Step(wantedDir);

        if (!stepResult.Moved || stepResult.CurrentDirection == Vector2I.Zero)
        {
            return;
        }

        if (stepResult.SnappedArcadePixelPos is Vector2I snappedPos)
        {
            TryConsumeCollectibleAtExactAnchor(snappedPos);
        }

        Vector2I segmentStart = stepResult.SnappedArcadePixelPos ?? stepResult.PreviousArcadePixelPos;

        TryConsumeCollectibleOnAnchorCrossing(
            segmentStart,
            stepResult.CurrentArcadePixelPos,
            stepResult.CurrentDirection);
    }

    /// <summary>
    /// Tries to consume one collectible if the given gameplay position matches
    /// exactly the anchor of one logical cell.
    /// </summary>
    /// <remarks>
    /// This is mainly used for the intermediate snapped position reached during
    /// some perpendicular turns.
    /// </remarks>
    private void TryConsumeCollectibleAtExactAnchor(Vector2I arcadePixelPos)
    {
        Vector2I cell = _level.ArcadePixelToLogicalCell(arcadePixelPos);
        Vector2I anchor = _level.LogicalCellToArcadePixel(cell);

        if (arcadePixelPos == anchor)
        {
            _level.TryConsumeCollectible(cell);
        }
    }

    /// <summary>
    /// Tries to consume one collectible when the final movement segment of the
    /// tick crosses the anchor of the destination logical cell.
    /// </summary>
    private void TryConsumeCollectibleOnAnchorCrossing(
        Vector2I startArcadePixelPos,
        Vector2I endArcadePixelPos,
        Vector2I moveDir)
    {
        Vector2I currentCell = _level.ArcadePixelToLogicalCell(endArcadePixelPos);
        Vector2I currentAnchor = _level.LogicalCellToArcadePixel(currentCell);

        bool crossedAnchor = false;

        if (moveDir.X > 0)
        {
            crossedAnchor =
                startArcadePixelPos.X < currentAnchor.X &&
                endArcadePixelPos.X >= currentAnchor.X;
        }
        else if (moveDir.X < 0)
        {
            crossedAnchor =
                startArcadePixelPos.X > currentAnchor.X &&
                endArcadePixelPos.X <= currentAnchor.X;
        }
        else if (moveDir.Y > 0)
        {
            crossedAnchor =
                startArcadePixelPos.Y < currentAnchor.Y &&
                endArcadePixelPos.Y >= currentAnchor.Y;
        }
        else if (moveDir.Y < 0)
        {
            crossedAnchor =
                startArcadePixelPos.Y > currentAnchor.Y &&
                endArcadePixelPos.Y <= currentAnchor.Y;
        }

        if (crossedAnchor)
        {
            _level.TryConsumeCollectible(currentCell);
        }
    }

    /// <summary>
    /// Selects the sprite render offset according to the currently effective
    /// movement direction used by the motor.
    /// </summary>
    private Vector2I GetCurrentSpriteRenderOffsetArcade()
    {
        if (_movementMotor.OffsetDir == Vector2I.Left)
            return PlayerMovementTuning.SpriteRenderOffsetLeftArcade;

        if (_movementMotor.OffsetDir == Vector2I.Right)
            return PlayerMovementTuning.SpriteRenderOffsetRightArcade;

        return PlayerMovementTuning.SpriteRenderOffsetVerticalArcade;
    }

    /// <summary>
    /// Converts the current render offset from arcade pixels to scene pixels.
    /// </summary>
    private Vector2 GetSpriteRenderOffsetScene()
    {
        if (_level == null)
            return Vector2.Zero;

        return _level.ArcadeDeltaToSceneDelta(GetCurrentSpriteRenderOffsetArcade());
    }

    /// <summary>
    /// Applies the current facing to the animated sprite.
    /// </summary>
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
