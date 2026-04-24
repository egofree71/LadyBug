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
/// Optional debug rendering is delegated to <see cref="PlayerDebugOverlay"/>
/// so debug text can be drawn above gates without changing normal player visuals.
/// </remarks>
public partial class PlayerController : Node2D
{
    // Draw the cyan gameplay anchor in the scene.
    [Export]
    private bool _debugDrawAnchor = false;

    [Export]
    private bool _debugDrawCoordinates = false;

    [Export]
    private Vector2 _debugCoordinatesOffset = new Vector2(18, -22);

    // Animated sprite that visually represents the player.
    private AnimatedSprite2D _animatedSprite = null!;

    // Top-level debug overlay drawn above the playfield when debug flags are enabled.
    private PlayerDebugOverlay _debugOverlay = null!;

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

        EnsureDebugOverlay();
        ApplyVisualFacing(_facingDir);
        UpdateDebugOverlay();
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
        UpdateDebugOverlay();
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
        UpdateDebugOverlay();
    }

    /// <summary>
    /// Executes exactly one controller tick.
    /// </summary>
    /// <remarks>
    /// Collectible consumption follows the actual pixel path reported by the
    /// movement motor.
    ///
    /// This matters for assisted turns: one simulation tick may contain two real
    /// one-pixel movement segments, first an alignment correction and then one
    /// pixel in the requested direction. Checking only the final segment can miss
    /// a flower crossed by the correction segment.
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

        if (!stepResult.Moved)
            return;

        if (stepResult.SnappedArcadePixelPos is Vector2I snappedPos)
        {
            TryConsumeCollectibleAtExactAnchor(snappedPos);
        }

        foreach (PlayerMovementSegment segment in stepResult.MovementSegments)
        {
            TryConsumeCollectibleOnAnchorCrossing(
                segment.StartArcadePixelPos,
                segment.EndArcadePixelPos,
                segment.Direction);
        }
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
    /// Tries to consume one collectible when a one-pixel movement segment crosses
    /// the anchor of its destination logical cell.
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
    /// Ensures the debug overlay exists. It is created at runtime so the player
    /// scene remains unchanged and normal gameplay rendering is unaffected.
    /// </summary>
    private void EnsureDebugOverlay()
    {
        if (_debugOverlay != null)
            return;

        _debugOverlay = new PlayerDebugOverlay
        {
            Name = "DebugOverlay"
        };

        AddChild(_debugOverlay);
    }

    /// <summary>
    /// Updates the optional debug overlay after gameplay and scene transforms have
    /// been synchronized.
    /// </summary>
    private void UpdateDebugOverlay()
    {
        if (_debugOverlay == null || _level == null)
            return;

        _debugOverlay.UpdateState(
            GlobalPosition,
            _movementMotor.ArcadePixelPos,
            _debugDrawAnchor,
            _debugDrawCoordinates,
            _debugCoordinatesOffset);
    }
}
