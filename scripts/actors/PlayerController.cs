using Godot;
using LadyBug.Gameplay.Player;

namespace LadyBug.Actors;

/// <summary>
/// Controls the player node and orchestrates input, movement and rendering.
/// </summary>
/// <remarks>
/// This class is intentionally light.
///
/// It mainly does three things:
/// - read the current intended direction from the input state
/// - advance the movement motor when the level simulation asks for one tick
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

    // Runtime-loaded death spritesheets. The PNGs are seven-frame horizontal strips.
    private const string DeathRedTexturePath = "res://assets/sprites/player/player_dead_red.png";
    private const string DeathGhostTexturePath = "res://assets/sprites/player/player_dead_ghost.png";

    // Animated sprite that visually represents the living player.
    private AnimatedSprite2D _animatedSprite = null!;

    // Separate runtime sprite used only for the red shrink / ghost death sequence.
    private Sprite2D? _deathSprite;

    // Death-sequence textures loaded from the project assets folder.
    private Texture2D? _deathRedTexture;
    private Texture2D? _deathGhostTexture;
    private bool _deathTextureWarningShown;

    // Top-level debug overlay drawn above the playfield when debug flags are enabled.
    private PlayerDebugOverlay _debugOverlay = null!;

    // Owning level. Provides coordinate conversion helpers and level runtime services.
    private Level _level = null!;

    // Last-pressed directional input state.
    private readonly PlayerInputState _inputState = new();

    // Gameplay movement motor.
    private readonly PlayerMovementMotor _movementMotor = new();

    // Tick-accurate state for the player death sequence.
    private readonly PlayerDeathSequenceState _deathSequenceState = new();

    // Player gameplay position and render offset captured at the start of death.
    private Vector2I _deathBaseArcadePixelPos = Vector2I.Zero;
    private Vector2 _deathBaseSpriteOffsetScene = Vector2.Zero;

    // Direction currently shown by the sprite.
    private Vector2I _facingDir = Vector2I.Up;

    /// <summary>
    /// Gets the current player gameplay position in arcade pixels.
    /// </summary>
    /// <remarks>
    /// Enemy collision and BFS guidance use the same gameplay coordinate space as
    /// the movement motor. Exposing this read-only value keeps those systems from
    /// reaching into the motor directly.
    /// </remarks>
    public Vector2I ArcadePixelPos => _movementMotor.ArcadePixelPos;

    /// <summary>
    /// Gets the player direction used by enemy base-preference generation.
    /// </summary>
    /// <remarks>
    /// The arcade keeps an effective current direction even when no input is held.
    /// For enemies, use the movement motor direction when available and fall back
    /// to the visual facing direction at level start or immediately after respawn.
    /// </remarks>
    public Vector2I CurrentDirectionForEnemies =>
        _movementMotor.CurrentDir != Vector2I.Zero
            ? _movementMotor.CurrentDir
            : _facingDir;

    /// <summary>
    /// Retrieves the sprite node and applies the initial visual facing.
    /// </summary>
    public override void _Ready()
    {
        _animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        _animatedSprite.Position = Vector2.Zero;

        EnsureDeathSprite();
        EnsureDebugOverlay();
        ApplyVisualFacing(_facingDir);
        UpdateDebugOverlay();
    }

    /// <summary>
    /// Keeps the rendered node synchronized with the latest gameplay position.
    /// </summary>
    /// <param name="delta">Frame delta time in seconds.</param>
    public override void _Process(double delta)
    {
        SynchronizeSceneFromGameplay();
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

        _inputState.InitializeFromCurrentInput();
        _movementMotor.Initialize(level);
        ResetDeathVisualState();

        ApplyVisualFacing(_facingDir);
        SynchronizeSceneFromGameplay();
    }

    /// <summary>
    /// Advances only the player-specific gameplay state by one simulation tick.
    /// </summary>
    /// <remarks>
    /// The level owns the global tick and advances board-level systems before
    /// calling this method. The player controller only handles player input,
    /// movement, facing, and collectible pickup along the player's actual path.
    ///
    /// Collectible consumption follows the actual pixel path reported by the
    /// movement motor. This matters for assisted turns: one simulation tick may
    /// contain two real one-pixel movement segments, first an alignment correction
    /// and then one pixel in the requested direction. Checking only the final
    /// segment can miss a flower crossed by the correction segment.
    /// </remarks>
    public void AdvanceOneSimulationTick()
    {
        if (_level == null || _deathSequenceState.IsActive)
            return;

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
    /// Synchronizes the Godot node transforms and debug overlay from gameplay state.
    /// </summary>
    public void SynchronizeSceneFromGameplay()
    {
        if (_level == null || _animatedSprite == null)
            return;

        if (_deathSequenceState.IsActive)
        {
            SynchronizeDeathSpriteFromSequence();
            UpdateDebugOverlay();
            return;
        }

        Position = _level.ArcadePixelToScenePosition(_movementMotor.ArcadePixelPos);
        _animatedSprite.Position = GetSpriteRenderOffsetScene();
        UpdateDebugOverlay();
    }

    /// <summary>
    /// Shows or hides only the rendered player sprite while preserving gameplay state.
    /// </summary>
    public void SetGameplaySpriteVisible(bool visible)
    {
        if (_animatedSprite != null)
            _animatedSprite.Visible = visible && !_deathSequenceState.IsActive;
    }

    /// <summary>
    /// Starts the red-shrink and ghost zigzag player death animation.
    /// </summary>
    /// <remarks>
    /// The gameplay position is captured once at the moment of death. The normal
    /// movement motor is then left untouched while the death sprite receives only
    /// visual offsets from <see cref="PlayerDeathSequenceState"/>.
    /// </remarks>
    public void StartDeathSequence()
    {
        if (_level == null)
            return;

        EnsureDeathSprite();

        _deathBaseArcadePixelPos = _movementMotor.ArcadePixelPos;
        _deathBaseSpriteOffsetScene = GetSpriteRenderOffsetScene();

        _deathSequenceState.Start();

        if (_animatedSprite != null)
            _animatedSprite.Visible = false;

        ApplyDeathFrameVisual();
        SynchronizeDeathSpriteFromSequence();
    }

    /// <summary>
    /// Advances the active death animation by one fixed arcade tick.
    /// </summary>
    /// <returns><see langword="true"/> when the sequence has just completed.</returns>
    public bool AdvanceDeathSequenceOneTick()
    {
        if (!_deathSequenceState.IsActive)
            return true;

        bool completed = _deathSequenceState.AdvanceOneTick();

        ApplyDeathFrameVisual();
        SynchronizeDeathSpriteFromSequence();

        if (completed)
            HideDeathSprite();

        return completed;
    }

    /// <summary>
    /// Hides all player visuals after the death sequence, used by the game-over placeholder.
    /// </summary>
    public void HideAfterDeathSequence()
    {
        _deathSequenceState.Reset();
        HideDeathSprite();

        if (_animatedSprite != null)
            _animatedSprite.Visible = false;
    }

    /// <summary>
    /// Resets the player gameplay state and scene transform to the level start cell.
    /// </summary>
    public void RespawnAtStartCell()
    {
        if (_level == null)
            return;

        ResetDeathVisualState();

        _movementMotor.ResetToStartCell();
        _inputState.InitializeFromCurrentInput();
        _facingDir = Vector2I.Up;

        ApplyVisualFacing(_facingDir);
        SetGameplaySpriteVisible(true);
        SynchronizeSceneFromGameplay();
    }

    /// <summary>
    /// Clears death animation state and restores the normal gameplay sprite.
    /// </summary>
    private void ResetDeathVisualState()
    {
        _deathSequenceState.Reset();
        HideDeathSprite();

        if (_animatedSprite != null)
            _animatedSprite.Visible = true;
    }

    /// <summary>
    /// Creates the runtime death sprite and loads both death spritesheets.
    /// </summary>
    private void EnsureDeathSprite()
    {
        _deathRedTexture ??= ResourceLoader.Load<Texture2D>(DeathRedTexturePath);
        _deathGhostTexture ??= ResourceLoader.Load<Texture2D>(DeathGhostTexturePath);

        if (_deathSprite != null)
            return;

        _deathSprite = new Sprite2D
        {
            Name = "DeathSprite",
            Visible = false,
            Centered = true,
            Hframes = PlayerDeathSequenceState.SheetFrameCount,
            Vframes = 1,
            Frame = 0
        };

        AddChild(_deathSprite);
    }

    /// <summary>
    /// Applies the current death frame to the runtime death sprite.
    /// </summary>
    private void ApplyDeathFrameVisual()
    {
        if (_deathSprite == null)
            return;

        Texture2D? texture = _deathSequenceState.CurrentSheet switch
        {
            PlayerDeathVisualSheet.Red => _deathRedTexture,
            PlayerDeathVisualSheet.Ghost => _deathGhostTexture,
            _ => null
        };

        if (texture == null)
        {
            if (!_deathTextureWarningShown)
            {
                _deathTextureWarningShown = true;
                GD.PushWarning("[PlayerController] Death spritesheets are missing. Expected player_dead_red.png and player_dead_ghost.png in assets/sprites/player.");
            }

            _deathSprite.Visible = false;
            return;
        }

        _deathSprite.Texture = texture;
        _deathSprite.Hframes = PlayerDeathSequenceState.SheetFrameCount;
        _deathSprite.Vframes = 1;
        _deathSprite.Frame = _deathSequenceState.CurrentFrame;
        _deathSprite.Visible = true;
    }

    /// <summary>
    /// Synchronizes the death sprite from the captured death origin and current visual offset.
    /// </summary>
    private void SynchronizeDeathSpriteFromSequence()
    {
        if (_level == null || _deathSprite == null)
            return;

        Position = _level.ArcadePixelToScenePosition(_deathBaseArcadePixelPos);
        _deathSprite.Position =
            _deathBaseSpriteOffsetScene +
            _level.ArcadeDeltaToSceneDelta(_deathSequenceState.CurrentVisualOffsetArcade);
    }

    /// <summary>
    /// Hides the runtime death sprite without changing normal movement state.
    /// </summary>
    private void HideDeathSprite()
    {
        if (_deathSprite == null)
            return;

        _deathSprite.Visible = false;
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
