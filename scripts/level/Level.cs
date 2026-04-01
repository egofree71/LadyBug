using Godot;
using LadyBug.Gameplay.Maze;

[Tool]
public partial class Level : Node2D
{
    private const int CellSizeArcade = 16;
    private const int RenderScale = 4;
    private static readonly Vector2I CellCenterOffsetArcade = new Vector2I(13, 15);

    private Vector2I _playerStartCell = new Vector2I(0, 0);

    [Export]
    public Vector2I PlayerStartCell
    {
        get => _playerStartCell;
        set
        {
            if (_playerStartCell == value)
                return;

            _playerStartCell = value;
            UpdatePlayerPositionFromLogicalCell();
        }
    }

    private MazeGrid _mazeGrid;

    public MazeGrid MazeGrid => _mazeGrid;

    public override void _Ready()
    {
        UpdatePlayerPositionFromLogicalCell();

        if (Engine.IsEditorHint())
            return;

        _mazeGrid = MazeLoader.LoadFromJsonFile("res://data/maze.json");
        GD.Print("Logical maze loaded successfully.");

        PlayerController player = GetNodeOrNull<PlayerController>("Player");
        if (player != null)
        {
            player.Initialize(this);
        }
    }

    private void UpdatePlayerPositionFromLogicalCell()
    {
        Sprite2D maze = GetNodeOrNull<Sprite2D>("Maze");
        Node2D player = GetNodeOrNull<Node2D>("Player");

        if (maze == null || player == null)
            return;

        player.Position = LogicalCellToScenePosition(maze, _playerStartCell);

        if (Engine.IsEditorHint())
        {
            player.NotifyPropertyListChanged();
            QueueRedraw();
        }
    }
    
    private Vector2 LogicalCellToScenePosition(Sprite2D maze, Vector2I cell)
    {
        float x = maze.Position.X + (cell.X * CellSizeArcade + CellCenterOffsetArcade.X) * RenderScale;
        float y = maze.Position.Y + (cell.Y * CellSizeArcade + CellCenterOffsetArcade.Y) * RenderScale;

        return new Vector2(x, y);
    }
}