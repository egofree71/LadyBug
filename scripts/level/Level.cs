using Godot;
using LadyBug.Gameplay.Maze;

public partial class Level : Node2D
{
    private MazeGrid _mazeGrid;

    public MazeGrid MazeGrid => _mazeGrid;

    public override void _Ready()
    {
        _mazeGrid = MazeLoader.LoadFromJsonFile("res://data/maze.json");
        GD.Print("Logical maze loaded successfully.");

        PlayerController player = GetNode<PlayerController>("Player");
        player.Initialize(this);
    }
}