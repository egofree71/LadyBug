using Godot;
using LadyBug.Gameplay.Maze;

/// <summary>
/// Controls the player entity.
/// </summary>
/// <remarks>
/// This is an intermediate movement implementation based on smooth cell-to-cell
/// motion. It validates movement against the logical maze, updates the player
/// scene position, and drives the directional animation, but it does not yet
/// reproduce the final arcade-accurate pixel-per-tick behavior.
/// </remarks>
public partial class PlayerController : Node2D
{
    // --- Constants ----------------------------------------------------------

    /// <summary>
    /// Visual movement speed, in scene pixels per second, used by the current
    /// intermediate movement model.
    /// </summary>
    private const float MoveSpeed = 220.0f;

    // --- Nodes --------------------------------------------------------------

    private AnimatedSprite2D _animatedSprite;

    // --- References ---------------------------------------------------------

    private Level _level;
    private MazeGrid _mazeGrid;

    // --- Logical State ------------------------------------------------------

    /// <summary>
    /// The logical maze cell currently occupied by the player.
    /// </summary>
    private Vector2I _currentCell = Vector2I.Zero;

    /// <summary>
    /// The logical maze cell currently targeted by movement.
    /// </summary>
    private Vector2I _targetCell = Vector2I.Zero;

    // --- Movement State -----------------------------------------------------

    /// <summary>
    /// Indicates whether the player is currently moving toward the target cell.
    /// </summary>
    private bool _isMoving = false;

    /// <summary>
    /// The current scene-space position target used for smooth movement.
    /// </summary>
    private Vector2 _targetScenePosition = Vector2.Zero;

    /// <summary>
    /// The last direction used for movement or facing.
    /// </summary>
    private Vector2I _lastDirection = Vector2I.Up;

    // --- Lifecycle ----------------------------------------------------------

    public override void _Ready()
    {
        _animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");

        // Set a valid default visual state before runtime initialization.
        _animatedSprite.FlipH = false;
        _animatedSprite.FlipV = false;
        _animatedSprite.Play("move_up");
    }

    public override void _Process(double delta)
    {
        if (_mazeGrid == null || _level == null)
            return;

        if (_isMoving)
        {
            UpdateSmoothMovement((float)delta);
        }
        else
        {
            HandleGridMovementInput();
        }
    }

    // --- Initialization -----------------------------------------------------

    /// <summary>
    /// Initializes the player from the level runtime data.
    /// </summary>
    /// <param name="level">
    /// The owning level, used to retrieve the logical maze, the player start
    /// cell, and logical-to-scene position conversion.
    /// </param>
    public void Initialize(Level level)
    {
        _level = level;
        _mazeGrid = level.MazeGrid;

        GD.Print($"MazeGrid found: {_mazeGrid != null}");

        _currentCell = level.PlayerStartCell;
        _targetCell = _currentCell;

        Position = _level.LogicalCellToScenePosition(_currentCell);
        _targetScenePosition = Position;

        GD.Print($"Player starting logical cell: {_currentCell}");

        MazeCell testCell = _mazeGrid.GetCell(_currentCell);

        GD.Print($"Cell {_currentCell} -> Walls = {testCell.Walls}");
        GD.Print($"Up: {testCell.HasWallUp}");
        GD.Print($"Down: {testCell.HasWallDown}");
        GD.Print($"Left: {testCell.HasWallLeft}");
        GD.Print($"Right: {testCell.HasWallRight}");
    }

    // --- Input / Movement ---------------------------------------------------

    /// <summary>
    /// Reads the current input direction and attempts to start movement toward
    /// the adjacent logical cell.
    /// </summary>
    private void HandleGridMovementInput()
    {
        Vector2I requestedDirection = ReadPressedDirection();

        if (requestedDirection == Vector2I.Zero)
            return;

        TryStartMoveToAdjacentCell(requestedDirection);
    }

    /// <summary>
    /// Reads the currently pressed movement direction.
    /// </summary>
    /// <returns>
    /// The requested direction, or <see cref="Vector2I.Zero"/> if no movement
    /// input is currently pressed.
    /// </returns>
    /// <remarks>
    /// Direction priority is resolved using a simple fixed order in this
    /// intermediate implementation.
    /// </remarks>
    private static Vector2I ReadPressedDirection()
    {
        if (Input.IsActionPressed("move_left"))
            return Vector2I.Left;

        if (Input.IsActionPressed("move_right"))
            return Vector2I.Right;

        if (Input.IsActionPressed("move_up"))
            return Vector2I.Up;

        if (Input.IsActionPressed("move_down"))
            return Vector2I.Down;

        return Vector2I.Zero;
    }

    /// <summary>
    /// Attempts to start movement toward an adjacent logical cell.
    /// </summary>
    /// <param name="direction">The requested movement direction.</param>
    /// <remarks>
    /// Movement starts only if the logical maze allows movement from the
    /// current cell in the requested direction.
    /// </remarks>
    private void TryStartMoveToAdjacentCell(Vector2I direction)
    {
        _lastDirection = direction;
        UpdateAnimation(direction);

        if (!_mazeGrid.CanMove(_currentCell, direction))
        {
            GD.Print($"Blocked from {_currentCell} toward {direction}");
            return;
        }

        _targetCell = _currentCell + direction;
        _targetScenePosition = _level.LogicalCellToScenePosition(_targetCell);
        _isMoving = true;

        GD.Print($"Started move from {_currentCell} to {_targetCell}");
    }

    /// <summary>
    /// Updates smooth movement toward the current target scene position.
    /// </summary>
    /// <param name="delta">The frame delta time, in seconds.</param>
    /// <remarks>
    /// Once the target position is reached, the target logical cell becomes the
    /// current logical cell.
    /// </remarks>
    private void UpdateSmoothMovement(float delta)
    {
        Position = Position.MoveToward(_targetScenePosition, MoveSpeed * delta);

        // A small tolerance is used because floating-point motion may not land
        // exactly on the target every frame.
        if (Position.DistanceTo(_targetScenePosition) <= 0.5f)
        {
            Position = _targetScenePosition;
            _currentCell = _targetCell;
            _isMoving = false;

            GD.Print($"Reached logical cell {_currentCell}");
        }
    }

    // --- Animation ----------------------------------------------------------

    /// <summary>
    /// Updates the visual animation and sprite orientation for the specified
    /// direction.
    /// </summary>
    /// <param name="direction">The direction to display.</param>
    private void UpdateAnimation(Vector2I direction)
    {
        if (direction == Vector2I.Zero)
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