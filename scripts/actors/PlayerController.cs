using Godot;
using LadyBug.Gameplay.Maze;

public partial class PlayerController : Node2D
{
    private AnimatedSprite2D _animatedSprite;
    private Level _level;
    private MazeGrid _mazeGrid;

    public override void _Ready()
    {
        _animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");

        _animatedSprite.FlipH = false;
        _animatedSprite.FlipV = false;
        _animatedSprite.Play("move_up");
    }

    public void Initialize(Level level)
    {
        _level = level;
        _mazeGrid = level.MazeGrid;

        GD.Print($"MazeGrid found: {_mazeGrid != null}");

        Vector2I testCellPosition = new Vector2I(1, 1);
        MazeCell testCell = _mazeGrid.GetCell(testCellPosition);

        GD.Print($"Cell {testCellPosition} -> Walls = {testCell.Walls}");
        GD.Print($"Up: {testCell.HasWallUp}");
        GD.Print($"Down: {testCell.HasWallDown}");
        GD.Print($"Left: {testCell.HasWallLeft}");
        GD.Print($"Right: {testCell.HasWallRight}");
    }
}