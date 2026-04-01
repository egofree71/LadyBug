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
    ///
    /// Important:
    /// This is not necessarily the geometric center of the cell.
    /// It is a practical visual offset chosen to match the original game
    /// appearance in the current setup.
    /// </summary>
    private static readonly Vector2I CellCenterOffsetArcade = new Vector2I(13, 15);

    // --- Exported Properties ------------------------------------------------

    private Vector2I _playerStartCell = new Vector2I(0, 0);

    /// <summary>
    /// Logical maze cell used as the player's start position.
    ///
    /// This property is exposed in the Godot Inspector so the player spawn
    /// can be adjusted directly from the editor.
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
    /// Exposes the runtime logical maze to other gameplay objects.
    /// </summary>
    public MazeGrid MazeGrid => _mazeGrid;

    // --- Lifecycle ----------------------------------------------------------

    public override void _Ready()
    {
        // Always keep the player visually synchronized with the configured
        // logical start cell, including when the scene is opened in the editor.
        UpdatePlayerPositionFromLogicalCell();

        // In editor mode, stop here.
        // The runtime logical maze should only be loaded when the game runs.
        if (Engine.IsEditorHint())
            return;

        _mazeGrid = MazeLoader.LoadFromJsonFile("res://data/maze.json");
        GD.Print("Logical maze loaded successfully.");

        PlayerController player = GetNodeOrNull<PlayerController>("Player");
        if (player != null)
        {
            // The player is initialized only after the maze has been loaded,
            // so it can safely access the runtime logical model.
            player.Initialize(this);
        }
    }

    // --- Position Conversion ------------------------------------------------

    /// <summary>
    /// Updates the player node position from the currently configured logical
    /// start cell.
    ///
    /// This is used both:
    /// - in the editor, for immediate visual feedback
    /// - at runtime, before gameplay initialization
    /// </summary>
    private void UpdatePlayerPositionFromLogicalCell()
    {
        Sprite2D maze = GetNodeOrNull<Sprite2D>("Maze");
        Node2D player = GetNodeOrNull<Node2D>("Player");

        if (maze == null || player == null)
            return;

        player.Position = LogicalCellToScenePosition(maze, _playerStartCell);

        if (Engine.IsEditorHint())
        {
            // Request a visual refresh in the editor after changing the player
            // position from an exported property.
            player.NotifyPropertyListChanged();
            QueueRedraw();
        }
    }

    /// <summary>
    /// Converts one logical maze cell into a 2D scene position.
    ///
    /// The conversion uses:
    /// - the top-left position of the maze background sprite
    /// - the logical cell size in arcade pixels
    /// - the render scale applied in the scene
    /// - the configured cell offset used for the player anchor
    /// </summary>
    private Vector2 LogicalCellToScenePosition(Sprite2D maze, Vector2I cell)
    {
        float x = maze.Position.X + (cell.X * CellSizeArcade + CellCenterOffsetArcade.X) * RenderScale;
        float y = maze.Position.Y + (cell.Y * CellSizeArcade + CellCenterOffsetArcade.Y) * RenderScale;

        return new Vector2(x, y);
    }
}