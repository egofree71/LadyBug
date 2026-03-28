using Godot;

/// <summary>
/// Controls the player character movement and animation.
/// 
/// Responsibilities:
/// - reads player input
/// - manages wanted and current directions
/// - updates arcade-style pixel movement on a updates arcade-style pixel movement on a fixed tick
/// - recenters the player inside maze lanes
/// - updates the visual animation state
/// 
/// This version currently supports movement against the static maze only.
/// Revolving doors and more accurate arcade turn windows will be added later.
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

    public override void _Ready()
    {
        _animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");

        // Player and MazeGrid are both children of Level.
        _mazeGrid = GetParent().GetNode<MazeGrid>("MazeGrid");

        _arcadePixelPos = StartArcadePixelPosition;
        UpdateRenderPosition();

        PlayAnimation(_currentDir);
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
        _wantedDir = Vector2I.Zero;

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
    }

    private void StepOneTick()
    {
        // In the arcade code, no input means no movement.
        if (_wantedDir == Vector2I.Zero)
            return;

        // If the player asks for a different direction,
        // try to switch only when the maze and alignment allow it.
        if (_wantedDir != _currentDir && CanTurnInto(_wantedDir))
        {
            _currentDir = _wantedDir;
        }

        // Recenter the actor inside the lane before advancing.
        // This is one of the main reasons the movement feels arcade-like.
        RecenterStraight(ref _arcadePixelPos, _currentDir);

        // Actual forward motion is validated pixel by pixel.
        if (CanStepForward(_currentDir))
        {
            _arcadePixelPos += _currentDir;
        }
    }

    private bool CanTurnInto(Vector2I wantedDir)
    {
        if (_mazeGrid == null)
            return false;

        // Turning into a vertical lane requires X alignment on 8 mod 16.
        if (wantedDir.Y != 0)
        {
            return PositiveMod(_arcadePixelPos.X, 16) == 8
                   && _mazeGrid.CanMove(_arcadePixelPos, wantedDir);
        }

        // Turning into a horizontal lane is accepted here with a simplified rule:
        // Y must be aligned on 6 or 7 mod 16.
        // Later we can replace this with the more accurate arcade capture windows.
        if (wantedDir.X != 0)
        {
            int my = PositiveMod(_arcadePixelPos.Y, 16);
            return (my == 6 || my == 7)
                   && _mazeGrid.CanMove(_arcadePixelPos, wantedDir);
        }

        return false;
    }

    private bool CanStepForward(Vector2I dir)
    {
        return _mazeGrid != null && _mazeGrid.CanStep(_arcadePixelPos, dir);
    }

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

    private void UpdateRenderPosition()
    {
        // Remove the original arcade top offset only for rendering.
        Position = new Vector2(
            _arcadePixelPos.X * RenderScale,
            (_arcadePixelPos.Y - MazeTopArcade) * RenderScale
        );
    }

    private void UpdateAnimation()
    {
        if (_wantedDir == Vector2I.Zero)
        {
            _animatedSprite.Stop();
            return;
        }

        PlayAnimation(_currentDir);
    }

    private void PlayAnimation(Vector2I direction)
    {
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

    private static int PositiveMod(int value, int mod)
    {
        int r = value % mod;
        return r < 0 ? r + mod : r;
    }
}