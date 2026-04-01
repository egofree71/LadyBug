using Godot;
using LadyBug.Gameplay.Maze;

/// <summary>
/// Controls the player entity.
///
/// Current responsibilities in this intermediate version:
/// - receive Level and MazeGrid references from Level
/// - store the current logical maze cell
/// - move one logical cell at a time
/// - validate movement against the logical maze
/// - update the player scene position from the logical cell
/// - play a matching directional animation
///
/// This is a temporary validation step before restoring the more precise
/// arcade-style pixel movement.
/// </summary>
public partial class PlayerController : Node2D
{
    // --- Nodes --------------------------------------------------------------

    private AnimatedSprite2D _animatedSprite;

    // --- References ---------------------------------------------------------

    private Level _level;
    private MazeGrid _mazeGrid;

    // --- Logical State ------------------------------------------------------

    /// <summary>
    /// Current logical maze cell occupied by the player.
    /// </summary>
    private Vector2I _currentCell = Vector2I.Zero;

    // --- Input State --------------------------------------------------------

    /// <summary>
    /// Prevents repeated moves every frame while a key is held.
    /// One move is triggered per new key press.
    /// </summary>
    private bool _inputLocked = false;

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

        HandleGridMovementInput();
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
        Position = _level.LogicalCellToScenePosition(_currentCell);

        GD.Print($"Player starting logical cell: {_currentCell}");

        Vector2I testCellPosition = _currentCell;
        MazeCell testCell = _mazeGrid.GetCell(testCellPosition);

        GD.Print($"Cell {testCellPosition} -> Walls = {testCell.Walls}");
        GD.Print($"Up: {testCell.HasWallUp}");
        GD.Print($"Down: {testCell.HasWallDown}");
        GD.Print($"Left: {testCell.HasWallLeft}");
        GD.Print($"Right: {testCell.HasWallRight}");
    }

    // --- Input / Movement ---------------------------------------------------

    private void HandleGridMovementInput()
    {
        Vector2I requestedDirection = ReadPressedDirection();

        // Unlock when no movement key is currently pressed.
        if (requestedDirection == Vector2I.Zero)
        {
            _inputLocked = false;
            return;
        }

        // Only move once per new press.
        if (_inputLocked)
            return;

        _inputLocked = true;

        TryMoveToAdjacentCell(requestedDirection);
    }

    /// <summary>
    /// Reads the current pressed direction.
    /// Priority is resolved in a simple fixed order for this temporary version.
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
    /// Tries to move to the adjacent logical cell in the requested direction.
    /// Movement succeeds only if the logical maze allows it.
    /// </summary>
    private void TryMoveToAdjacentCell(Vector2I direction)
    {
        if (!_mazeGrid.CanMove(_currentCell, direction))
        {
            GD.Print($"Blocked from {_currentCell} toward {direction}");
            UpdateAnimation(direction);
            return;
        }

        _currentCell += direction;
        Position = _level.LogicalCellToScenePosition(_currentCell);

        GD.Print($"Moved to logical cell {_currentCell}");

        UpdateAnimation(direction);
    }

    // --- Animation ----------------------------------------------------------

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