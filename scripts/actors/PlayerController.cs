using Godot;

public partial class PlayerController : Node2D
{
    // Movement speed in pixels per second.
    // Exposed in the editor to tweak gameplay easily.
    [Export]
    public float Speed = 200.0f;

    private AnimatedSprite2D _animatedSprite;

    // Current movement direction (one of the 4 cardinal directions or zero).
    private Vector2 _moveDirection = Vector2.Zero;

    public override void _Ready()
    {
        _animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");

        // Start with a default animation so the player is not static on launch.
        PlayAnimation(Vector2.Right);
    }

    public override void _Process(double delta)
    {
        ReadInput();
        MovePlayer((float)delta);
        UpdateAnimation();
    }

    /// <summary>
    /// Reads player input and sets a single direction.
    /// Only one direction is allowed at a time to match arcade movement.
    /// </summary>
    private void ReadInput()
    {
        _moveDirection = Vector2.Zero;

        if (Input.IsActionPressed("move_left"))
        {
            _moveDirection = Vector2.Left;
        }
        else if (Input.IsActionPressed("move_right"))
        {
            _moveDirection = Vector2.Right;
        }
        else if (Input.IsActionPressed("move_up"))
        {
            _moveDirection = Vector2.Up;
        }
        else if (Input.IsActionPressed("move_down"))
        {
            _moveDirection = Vector2.Down;
        }
    }

    /// <summary>
    /// Moves the player using simple free movement (no grid yet).
    /// This will later be replaced by grid-based movement logic.
    /// </summary>
    private void MovePlayer(float delta)
    {
        Position += _moveDirection * Speed * delta;
    }

    /// <summary>
    /// Updates the animation depending on movement.
    /// Stops animation if the player is idle.
    /// </summary>
    private void UpdateAnimation()
    {
        if (_moveDirection == Vector2.Zero)
        {
            _animatedSprite.Stop();
            return;
        }

        PlayAnimation(_moveDirection);
    }

    /// <summary>
    /// Plays the correct animation based on direction.
    /// 
    /// We only use two base animations:
    /// - move_right (for horizontal movement)
    /// - move_up (for vertical movement, sprite faces up)
    /// 
    /// We flip the sprite instead of duplicating animations:
    /// - FlipH for left
    /// - FlipV for down
    /// 
    /// This reduces the number of animations and matches the sprite sheet structure.
    /// </summary>
    private void PlayAnimation(Vector2 direction)
    {
        // Reset flips before applying direction-specific ones
        _animatedSprite.FlipH = false;
        _animatedSprite.FlipV = false;

        if (direction == Vector2.Left)
        {
            _animatedSprite.Play("move_right");
            _animatedSprite.FlipH = true;
        }
        else if (direction == Vector2.Right)
        {
            _animatedSprite.Play("move_right");
        }
        else if (direction == Vector2.Up)
        {
            _animatedSprite.Play("move_up");
        }
        else if (direction == Vector2.Down)
        {
            _animatedSprite.Play("move_up");
            _animatedSprite.FlipV = true;
        }
    }
}