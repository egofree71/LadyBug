using Godot;
using LadyBug.Gameplay.Maze;

/// <summary>
/// Controls the player entity.
///
/// Current responsibilities in this intermediate version:
/// - receive Level and MazeGrid references from Level
/// - store the current logical maze cell
/// - validate movement against the logical maze
/// - move smoothly from one logical cell to the next
/// - update the player scene position from the logical cell target
/// - play a matching directional animation
///
/// This is still an intermediate implementation.
/// It replaces instant cell jumps with smooth movement, but it is not yet the
/// final arcade-accurate pixel-per-tick movement system.
/// </summary>
public partial class PlayerController : Node2D
{
    // --- Constants ----------------------------------------------------------

    /// <summary>
    /// Visual movement speed in scene pixels per second for this intermediate
    /// cell-to-cell movement step.
    /// </summary>
    private const float MoveSpeed = 220.0f;

    // --- Nodes --------------------------------------------------------------

    private AnimatedSprite2D _animatedSprite;

    // --- References ---------------------------------------------------------

    private Level _level;
    private MazeGrid _mazeGrid;

    // --- Logical State ------------------------------------------------------

    /// <summary>
    /// Current logical maze cell fully reached by the player.
    /// </summary>
    private Vector2I _currentCell = Vector2I.Zero;

    /// <summary>
    /// Target logical maze cell currently being approached.
    /// </summary>
    private Vector2I _targetCell = Vector2I.Zero;

    // --- Movement State -----------------------------------------------------

    /// <summary>
    /// True while the player is moving toward the target cell.
    /// </summary>
    private bool _isMoving = false;

    /// <summary>
    /// Current scene position target for smooth motion.
    /// </summary>
    private Vector2 _targetScenePosition = Vector2.Zero;

    /// <summary>
    /// Last direction used for movement or requested facing.
    /// </summary>
    private Vector2I _lastDirection = Vector2I.Up;

    // --- Lifecycle ----------------------------------------------------------

    public override void _Ready()
    {
        _animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");

        // Initial visual state before runtime initialization.
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
    /// Called by Level after the logical maze has been loaded.
    /// </summary>
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
    /// Reads the current pressed direction and tries to start a move toward the
    /// adjacent logical cell.
    /// </summary>
    private void HandleGridMovementInput()
    {
        Vector2I requestedDirection = ReadPressedDirection();

        if (requestedDirection == Vector2I.Zero)
            return;

        TryStartMoveToAdjacentCell(requestedDirection);
    }

    /// <summary>
    /// Reads the currently pressed direction.
    /// Priority is resolved in a simple fixed order for this intermediate
    /// version.
    /// </summary>
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
    /// Tries to start movement toward an adjacent logical cell.
    /// Movement succeeds only if the logical maze allows it.
    /// </summary>
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
    /// Moves the player smoothly toward the current target scene position.
    /// Once the target is reached, the target cell becomes the current cell.
    /// </summary>
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
    /// Updates the visual animation and sprite orientation for the given
    /// direction.
    /// </summary>
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