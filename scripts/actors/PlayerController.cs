using Godot;

/// <summary>
/// Controls the player character movement and animation.
///
/// Responsibilities:
/// - reads player input
/// - manages wanted and current directions
/// - updates arcade-style pixel movement on a fixed tick
/// - recenters the player inside maze lanes
/// - queries MazeGrid for movement validation
/// - keeps the player animation running continuously
///
/// This version currently supports movement against the static maze only.
/// Revolving doors and more accurate arcade behavior can be added later.
/// </summary>
public partial class PlayerController : Node2D
{
    // Arcade video frequency.
    private const float ArcadeTickRate = 60.1145f;
    private const float ArcadeTickDuration = 1.0f / ArcadeTickRate;

    // The player spritesheet is a x4 upscale of the original 16x16 sprite.
    private const int RenderScale = 4;

    // Original arcade top offset kept only in the logical position model.
    private const int MazeTopArcade = 0x30;

    // Start position expressed in arcade pixels, not in screen pixels.
    [Export]
    public Vector2I StartArcadePixelPosition = new Vector2I(0x58, 0x86);

    private AnimatedSprite2D _animatedSprite;
    private MazeGrid _mazeGrid;
    private float _accumulator = 0.0f;

    // Logical position in arcade pixels.
    private Vector2I _arcadePixelPos;

    // Last direction that was actually used to move.
    private Vector2I _currentDir = Vector2I.Right;

    // Direction currently requested by player input.
    private Vector2I _wantedDir = Vector2I.Zero;

    // Last direction used for visual orientation.
    private Vector2I _lastVisualDir = Vector2I.Right;

    public override void _Ready()
    {
        _animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");

        // Player and MazeGrid are both children of Level.
        _mazeGrid = GetParent().GetNode<MazeGrid>("MazeGrid");

        _arcadePixelPos = StartArcadePixelPosition;
        _lastVisualDir = _currentDir;

        UpdateRenderPosition();
        PlayAnimation(_lastVisualDir);
    }

    public override void _Process(double delta)
    {
        ReadInput();

        // Advance the simulation using a fixed arcade-style tick.
        _accumulator += (float)delta;
        while (_accumulator >= ArcadeTickDuration)
        {
            _accumulator -= ArcadeTickDuration;
            StepOneTick();
        }

        UpdateRenderPosition();
        UpdateAnimation();
    }

    private void ReadInput()
    {
        // If a direction was just pressed this frame, it becomes the new wanted direction.
        if (Input.IsActionJustPressed("move_left"))
        {
            _wantedDir = Vector2I.Left;
            _lastVisualDir = _wantedDir;
            return;
        }

        if (Input.IsActionJustPressed("move_right"))
        {
            _wantedDir = Vector2I.Right;
            _lastVisualDir = _wantedDir;
            return;
        }

        if (Input.IsActionJustPressed("move_up"))
        {
            _wantedDir = Vector2I.Up;
            _lastVisualDir = _wantedDir;
            return;
        }

        if (Input.IsActionJustPressed("move_down"))
        {
            _wantedDir = Vector2I.Down;
            _lastVisualDir = _wantedDir;
            return;
        }

        // If the currently wanted direction is still being held, keep it.
        if (_wantedDir != Vector2I.Zero && IsDirectionHeld(_wantedDir))
            return;

        // Otherwise, fall back to any currently held direction.
        if (Input.IsActionPressed("move_left"))
        {
            _wantedDir = Vector2I.Left;
            return;
        }

        if (Input.IsActionPressed("move_right"))
        {
            _wantedDir = Vector2I.Right;
            return;
        }

        if (Input.IsActionPressed("move_up"))
        {
            _wantedDir = Vector2I.Up;
            return;
        }

        if (Input.IsActionPressed("move_down"))
        {
            _wantedDir = Vector2I.Down;
            return;
        }

        // No direction is held anymore.
        _wantedDir = Vector2I.Zero;
    }

    /// <summary>
    /// Advances the player by one arcade tick.
    /// </summary>
    private void StepOneTick()
    {
        // In the arcade code, no input means no movement.
        if (_wantedDir == Vector2I.Zero)
            return;

        if (_wantedDir != _currentDir)
        {
            // Opposite direction changes should be immediate.
            if (IsOppositeDirection(_currentDir, _wantedDir) && CanStepForward(_wantedDir))
            {
                _currentDir = _wantedDir;
            }
            // Perpendicular direction changes use arcade turn-window capture.
            else if (TryCaptureTurn(_wantedDir))
            {
                return;
            }
            // If the requested direction cannot be taken immediately,
            // stop instead of continuing in the old direction.
            else
            {
                return;
            }
        }

        // Continue moving in the current direction.
        RecenterStraight(ref _arcadePixelPos, _currentDir);

        if (CanStepForward(_currentDir))
        {
            _arcadePixelPos += _currentDir;
        }
    }

    /// <summary>
    /// Tries to capture an arcade-style turn window.
    /// If successful, this method handles the tick and returns true.
    /// </summary>
    private bool TryCaptureTurn(Vector2I wantedDir)
    {
        if (_mazeGrid == null)
            return false;

        // Horizontal -> Vertical turn
        if (_currentDir.X != 0 && wantedDir.Y != 0)
        {
            if (_mazeGrid.TryGetVerticalTurnTarget(_arcadePixelPos, out int targetX) &&
                _mazeGrid.CanStep(new Vector2I(targetX, _arcadePixelPos.Y), wantedDir))
            {
                // Move horizontally toward the captured turn target.
                if (_arcadePixelPos.X < targetX)
                {
                    _arcadePixelPos.X += 1;
                    return true;
                }

                if (_arcadePixelPos.X > targetX)
                {
                    _arcadePixelPos.X -= 1;
                    return true;
                }

                // Once aligned, switch direction and move into the new lane.
                _currentDir = wantedDir;

                if (CanStepForward(_currentDir))
                {
                    _arcadePixelPos += _currentDir;
                }

                return true;
            }
        }

        // Vertical -> Horizontal turn
        if (_currentDir.Y != 0 && wantedDir.X != 0)
        {
            if (_mazeGrid.TryGetHorizontalTurnTarget(_arcadePixelPos, out int targetY))
            {
                // The original logic makes the turn decision around Y % 16 == 6,
                // but horizontal lane travel is visually centered on Y % 16 == 7.
                int horizontalLaneY = targetY + 1;

                if (_mazeGrid.CanStep(new Vector2I(_arcadePixelPos.X, horizontalLaneY), wantedDir))
                {
                    // Move vertically toward the actual horizontal lane center.
                    if (_arcadePixelPos.Y < horizontalLaneY)
                    {
                        _arcadePixelPos.Y += 1;
                        return true;
                    }

                    if (_arcadePixelPos.Y > horizontalLaneY)
                    {
                        _arcadePixelPos.Y -= 1;
                        return true;
                    }

                    // Once aligned, switch direction and move into the new lane.
                    _currentDir = wantedDir;

                    if (CanStepForward(_currentDir))
                    {
                        _arcadePixelPos += _currentDir;
                    }

                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Returns true if the next arcade pixel belongs to the walkable graph.
    /// </summary>
    private bool CanStepForward(Vector2I dir)
    {
        return _mazeGrid != null && _mazeGrid.CanStep(_arcadePixelPos, dir);
    }

    /// <summary>
    /// Recenter the player on the lane axis before advancing.
    /// </summary>
    private static void RecenterStraight(ref Vector2I pixelPos, Vector2I dir)
    {
        // Vertical movement:
        // keep X aligned toward lane center X % 16 == 8
        if (dir.Y != 0)
        {
            int mx = PositiveMod(pixelPos.X, 16);

            if (mx < 8)
                pixelPos.X += 1;
            else if (mx > 8)
                pixelPos.X -= 1;
        }
        // Horizontal movement:
        // keep Y aligned toward lane center Y % 16 == 7
        else if (dir.X != 0)
        {
            int my = PositiveMod(pixelPos.Y, 16);

            if (my < 7)
                pixelPos.Y += 1;
            else if (my > 7)
                pixelPos.Y -= 1;
        }
    }

    /// <summary>
    /// Converts logical arcade coordinates to rendered screen coordinates.
    /// </summary>
    private void UpdateRenderPosition()
    {
        // Remove the original arcade top offset only for rendering.
        Position = new Vector2(
            _arcadePixelPos.X * RenderScale,
            (_arcadePixelPos.Y - MazeTopArcade) * RenderScale
        );
    }

    /// <summary>
    /// Updates the visual direction while keeping the animation continuously active.
    /// </summary>
    private void UpdateAnimation()
    {
        Vector2I visualDir;

        // If the player is currently requesting a different direction,
        // display that requested direction even if movement is blocked.
        if (_wantedDir != Vector2I.Zero && _wantedDir != _currentDir)
        {
            visualDir = _wantedDir;
        }
        // If an input is held and it matches the current movement direction,
        // display the actual movement direction.
        else if (_wantedDir != Vector2I.Zero)
        {
            visualDir = _currentDir;
        }
        // Otherwise keep the last visual direction.
        else
        {
            visualDir = _lastVisualDir;
        }

        _lastVisualDir = visualDir;
        PlayAnimation(visualDir);
    }

    /// <summary>
    /// Starts the correct animation for the current direction.
    /// </summary>
    private void PlayAnimation(Vector2I direction)
    {
        ApplyVisualDirection(direction);

        string animationName = direction.X != 0 ? "move_right" : "move_up";

        if (_animatedSprite.Animation != animationName)
        {
            _animatedSprite.Animation = animationName;
        }

        if (!_animatedSprite.IsPlaying())
        {
            _animatedSprite.Play();
        }
    }

    /// <summary>
    /// Applies sprite flips without changing the animation identity.
    /// </summary>
    private void ApplyVisualDirection(Vector2I direction)
    {
        _animatedSprite.FlipH = false;
        _animatedSprite.FlipV = false;

        if (direction == Vector2I.Left)
        {
            _animatedSprite.FlipH = true;
        }
        else if (direction == Vector2I.Down)
        {
            _animatedSprite.FlipV = true;
        }
    }

    /// <summary>
    /// Returns true if the given direction is still held on the keyboard.
    /// </summary>
    private static bool IsDirectionHeld(Vector2I dir)
    {
        if (dir == Vector2I.Left)
            return Input.IsActionPressed("move_left");

        if (dir == Vector2I.Right)
            return Input.IsActionPressed("move_right");

        if (dir == Vector2I.Up)
            return Input.IsActionPressed("move_up");

        if (dir == Vector2I.Down)
            return Input.IsActionPressed("move_down");

        return false;
    }

    /// <summary>
    /// Returns true if the two directions are exact opposites.
    /// </summary>
    private static bool IsOppositeDirection(Vector2I a, Vector2I b)
    {
        return a != Vector2I.Zero && b != Vector2I.Zero && a + b == Vector2I.Zero;
    }

    private static int PositiveMod(int value, int mod)
    {
        int r = value % mod;
        return r < 0 ? r + mod : r;
    }
}