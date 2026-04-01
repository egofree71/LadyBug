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

        _level = GetParent<Level>();
        _mazeGrid = _level.MazeGrid;

        _animatedSprite.FlipH = false;
        _animatedSprite.FlipV = false;
        _animatedSprite.Play("move_up");
    }
}