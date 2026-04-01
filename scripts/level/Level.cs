using Godot;
using LadyBug.Gameplay.Maze;

/// <summary>
/// Represents one playable level of the game.
///
/// Responsibilities:
/// - load the logical maze from JSON
/// - expose the runtime MazeGrid to gameplay actors
/// - initialize the player once the logical maze is ready
/// - convert a logical maze cell position into a scene position
/// - keep the player visually aligned with the configured logical start cell
///
/// This class separates:
/// - the visual maze background (Sprite2D "Maze")
/// - the logical maze model loaded from data/maze.json
/// </summary>
[Tool]
public partial class Level : Node2D
{
    // --- Constants ----------------------------------------------------------

    /// <summary>
    /// Size of one logical maze cell in original arcade pixels.
    /// </summary>
    private const int CellSizeArcade = 16;

    /// <summary>
    /// Scale factor used to render arcade pixels in the Godot scene.
    /// </summary>
    private const int RenderScale = 4;

    /// <summary>
    /// Offset inside one logical cell used to place the player visually.
    /// Important: this is a practical visual offset, not necessarily the exact
    /// geometric center of the cell.
    /// </summary>
    private static readonly Vector2I CellCenterOffsetArcade = new Vector2I(13, 15);

    // --- Exported Properties ------------------------------------------------

    private Vector2I _playerStartCell = new Vector2I(0, 0);

    /// <summary>
    /// Logical maze cell used as the player's start position.
    /// Exposed in the Inspector for quick iteration.
    /// </summary>
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

    // --- Runtime State ------------------------------------------------------

    /// <summary>
    /// Runtime logical maze loaded from JSON.
    /// </summary>
    private MazeGrid _mazeGrid;

    /// <summary>
    /// Exposes the runtime logical maze to gameplay objects.
    /// </summary>
    public MazeGrid MazeGrid => _mazeGrid;

    // --- Lifecycle ----------------------------------------------------------

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

    // --- Position Conversion ------------------------------------------------

    /// <summary>
    /// Updates the Player node position from the currently configured
    /// PlayerStartCell. Used for editor preview.
    /// </summary>
    private void UpdatePlayerPositionFromLogicalCell()
    {
        Sprite2D maze = GetNodeOrNull<Sprite2D>("Maze");
        Node2D player = GetNodeOrNull<Node2D>("Player");

        if (maze == null || player == null)
            return;

        player.Position = LogicalCellToScenePosition(_playerStartCell);

        if (Engine.IsEditorHint())
        {
            player.NotifyPropertyListChanged();
            QueueRedraw();
        }
    }

    /// <summary>
    /// Converts one logical maze cell into a 2D scene position.
    /// This is the public conversion used by the player controller.
    /// </summary>
    public Vector2 LogicalCellToScenePosition(Vector2I cell)
    {
        Sprite2D maze = GetNodeOrNull<Sprite2D>("Maze");
        if (maze == null)
            return Vector2.Zero;

        float x = maze.Position.X + (cell.X * CellSizeArcade + CellCenterOffsetArcade.X) * RenderScale;
        float y = maze.Position.Y + (cell.Y * CellSizeArcade + CellCenterOffsetArcade.Y) * RenderScale;

        return new Vector2(x, y);
    }
}