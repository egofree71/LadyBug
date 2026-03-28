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
/// - controls visual orientation and animation
///
/// This version matches the current arcade-like behavior:
/// - 1 pixel per tick movement
/// - turn windows
/// - stop if new direction is requested but not possible
/// - visual direction follows input when blocked
/// </summary>
public partial class PlayerController : Node2D
{
    // --- Constants ----------------------------------------------------------

    private const float ArcadeTickRate = 60.1145f;
    private const float ArcadeTickDuration = 1.0f / ArcadeTickRate;

    private const int RenderScale = 4;
    private const int MazeTopArcade = 0x30;

    // --- Export -------------------------------------------------------------

    [Export]
    public Vector2I StartArcadePixelPosition = new Vector2I(0x58, 0x86);

    // --- Nodes & State ------------------------------------------------------

    private AnimatedSprite2D _animatedSprite;
    private MazeGrid _mazeGrid;

    private float _accumulator = 0.0f;

    private Vector2I _arcadePixelPos;

    private Vector2I _currentDir = Vector2I.Right;
    private Vector2I _wantedDir = Vector2I.Zero;

    private Vector2I _lastVisualDir = Vector2I.Right;

    // --- Lifecycle ----------------------------------------------------------

    public override void _Ready()
    {
        _animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        _mazeGrid = GetParent().GetNode<MazeGrid>("MazeGrid");

        _arcadePixelPos = StartArcadePixelPosition;
        _lastVisualDir = _currentDir;

        UpdateRenderPosition();
        PlayAnimation(_lastVisualDir);
    }

    public override void _Process(double delta)
    {
        ReadInput();
        UpdateMovement((float)delta);
        UpdateRenderPosition();
        UpdateAnimation();
    }

    // --- Input --------------------------------------------------------------

    private void ReadInput()
    {
        // Priority to newly pressed input (arcade feel).
        if (Input.IsActionJustPressed("move_left"))
        {
            SetWantedDirection(Vector2I.Left);
            return;
        }

        if (Input.IsActionJustPressed("move_right"))
        {
            SetWantedDirection(Vector2I.Right);
            return;
        }

        if (Input.IsActionJustPressed("move_up"))
        {
            SetWantedDirection(Vector2I.Up);
            return;
        }

        if (Input.IsActionJustPressed("move_down"))
        {
            SetWantedDirection(Vector2I.Down);
            return;
        }

        // Keep current wanted direction if still held.
        if (_wantedDir != Vector2I.Zero && IsDirectionHeld(_wantedDir))
            return;

        // Fallback to currently held keys.
        if (Input.IsActionPressed("move_left"))
        {
            _wantedDir = Vector2I.Left;
        }
        else if (Input.IsActionPressed("move_right"))
        {
            _wantedDir = Vector2I.Right;
        }
        else if (Input.IsActionPressed("move_up"))
        {
            _wantedDir = Vector2I.Up;
        }
        else if (Input.IsActionPressed("move_down"))
        {
            _wantedDir = Vector2I.Down;
        }
        else
        {
            _wantedDir = Vector2I.Zero;
        }
    }

    private void SetWantedDirection(Vector2I dir)
    {
        _wantedDir = dir;
        _lastVisualDir = dir;
    }

    // --- Movement -----------------------------------------------------------

    private void UpdateMovement(float delta)
    {
        _accumulator += delta;

        while (_accumulator >= ArcadeTickDuration)
        {
            _accumulator -= ArcadeTickDuration;
            StepOneTick();
        }
    }

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

    private bool TryCaptureTurn(Vector2I wantedDir)
    {
        if (_mazeGrid == null)
            return false;

        // Horizontal -> Vertical.
        if (_currentDir.X != 0 && wantedDir.Y != 0)
        {
            if (_mazeGrid.TryGetVerticalLaneX(_arcadePixelPos, out int targetX) &&
                _mazeGrid.CanStepToNextPixel(new Vector2I(targetX, _arcadePixelPos.Y), wantedDir))
            {
                return AlignAndTurnX(targetX, wantedDir);
            }
        }

        // Vertical -> Horizontal.
        if (_currentDir.Y != 0 && wantedDir.X != 0)
        {
            if (_mazeGrid.TryGetHorizontalLaneY(_arcadePixelPos, out int laneY) &&
                _mazeGrid.CanStepToNextPixel(new Vector2I(_arcadePixelPos.X, laneY), wantedDir))
            {
                return AlignAndTurnY(laneY, wantedDir);
            }
        }

        return false;
    }

    private bool AlignAndTurnX(int targetX, Vector2I dir)
    {
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

        _currentDir = dir;

        if (CanStepForward(_currentDir))
        {
            _arcadePixelPos += _currentDir;
        }

        return true;
    }

    private bool AlignAndTurnY(int targetY, Vector2I dir)
    {
        if (_arcadePixelPos.Y < targetY)
        {
            _arcadePixelPos.Y += 1;
            return true;
        }

        if (_arcadePixelPos.Y > targetY)
        {
            _arcadePixelPos.Y -= 1;
            return true;
        }

        _currentDir = dir;

        if (CanStepForward(_currentDir))
        {
            _arcadePixelPos += _currentDir;
        }

        return true;
    }

    private bool CanStepForward(Vector2I dir)
    {
        return _mazeGrid != null && _mazeGrid.CanStepToNextPixel(_arcadePixelPos, dir);
    }

    private static void RecenterStraight(ref Vector2I pos, Vector2I dir)
    {
        // Vertical movement: keep X aligned toward lane center X % 16 == 8.
        if (dir.Y != 0)
        {
            int mx = PositiveMod(pos.X, 16);

            if (mx < 8)
                pos.X += 1;
            else if (mx > 8)
                pos.X -= 1;
        }
        // Horizontal movement: keep Y aligned toward lane center Y % 16 == 7.
        else if (dir.X != 0)
        {
            int my = PositiveMod(pos.Y, 16);

            if (my < 7)
                pos.Y += 1;
            else if (my > 7)
                pos.Y -= 1;
        }
    }

    // --- Rendering ----------------------------------------------------------

    private void UpdateRenderPosition()
    {
        Position = new Vector2(
            _arcadePixelPos.X * RenderScale,
            (_arcadePixelPos.Y - MazeTopArcade) * RenderScale
        );
    }

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

    private void PlayAnimation(Vector2I dir)
    {
        ApplyVisualDirection(dir);

        string animationName = dir.X != 0 ? "move_right" : "move_up";

        if (_animatedSprite.Animation != animationName)
        {
            _animatedSprite.Animation = animationName;
        }

        if (!_animatedSprite.IsPlaying())
        {
            _animatedSprite.Play();
        }
    }

    private void ApplyVisualDirection(Vector2I dir)
    {
        _animatedSprite.FlipH = false;
        _animatedSprite.FlipV = false;

        if (dir == Vector2I.Left)
        {
            _animatedSprite.FlipH = true;
        }
        else if (dir == Vector2I.Down)
        {
            _animatedSprite.FlipV = true;
        }
    }

    // --- Utils --------------------------------------------------------------

    private static bool IsDirectionHeld(Vector2I dir)
    {
        if (dir == Vector2I.Left)  return Input.IsActionPressed("move_left");
        if (dir == Vector2I.Right) return Input.IsActionPressed("move_right");
        if (dir == Vector2I.Up)    return Input.IsActionPressed("move_up");
        if (dir == Vector2I.Down)  return Input.IsActionPressed("move_down");
        return false;
    }

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