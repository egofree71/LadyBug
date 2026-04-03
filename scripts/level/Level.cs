using Godot;
using LadyBug.Gameplay.Maze;

/// <summary>
/// Represents one playable level of the game.
/// </summary>
/// <remarks>
/// This class is responsible for:
/// loading the logical maze,
/// exposing it to gameplay actors,
/// and converting between logical cells, arcade-pixel gameplay coordinates,
/// and Godot scene coordinates.
///
/// The level intentionally separates:
/// - logical maze data used for gameplay
/// - visual maze rendering provided by the background sprite
///
/// The player controller uses gameplay anchors in arcade pixels,
/// while sprite-specific visual offsets are handled separately by the actor.
/// </remarks>
[Tool]
public partial class Level : Node2D
{
    // --- Constants ----------------------------------------------------------

    // Size of one logical maze cell in original arcade pixels.
    private const int CellSizeArcade = 16;

    // Scale factor used to render arcade pixels in the Godot scene.
    private const int RenderScale = 4;

    // Gameplay anchor used inside each logical 16x16 cell.
    // This anchor is used for movement and collision, not for visual sprite centering.
    private static readonly Vector2I GameplayAnchorArcade = new(8, 7);

    // --- Exported Properties ------------------------------------------------

    private Vector2I _playerStartCell = Vector2I.Zero;

    /// <summary>
    /// Logical start cell used to place the player.
    /// </summary>
    /// <remarks>
    /// In the editor, changing this property immediately updates the previewed
    /// player instance position.
    /// </remarks>
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

    // Runtime logical maze loaded from JSON.
    private MazeGrid _mazeGrid;

    /// <summary>
    /// Gets the runtime logical maze used by gameplay actors.
    /// </summary>
    public MazeGrid MazeGrid => _mazeGrid;

    // --- Lifecycle ----------------------------------------------------------

    /// <summary>
    /// Initializes the level.
    /// </summary>
    /// <remarks>
    /// In the editor, only the preview position is updated.
    /// At runtime, the logical maze is loaded and then the player controller
    /// is initialized.
    /// </remarks>
    public override void _Ready()
    {
        if (Engine.IsEditorHint())
        {
            UpdatePlayerPositionFromLogicalCell();
            return;
        }

        _mazeGrid = MazeLoader.LoadFromJsonFile("res://data/maze.json");

        PlayerController player = GetNodeOrNull<PlayerController>("Player");
        if (player != null)
            player.Initialize(this);
    }

    // --- Coordinate Conversion ---------------------------------------------

    /// <summary>
    /// Converts a logical maze cell into a gameplay arcade-pixel anchor.
    /// </summary>
    /// <param name="cell">Logical cell coordinates.</param>
    /// <returns>
    /// Arcade-pixel gameplay anchor corresponding to the logical cell.
    /// </returns>
    public Vector2I LogicalCellToArcadePixel(Vector2I cell)
    {
        return cell * CellSizeArcade + GameplayAnchorArcade;
    }

    /// <summary>
    /// Converts a gameplay arcade-pixel position back into a logical maze cell.
    /// </summary>
    /// <param name="arcadePixel">Gameplay position in arcade pixels.</param>
    /// <returns>
    /// Logical cell containing that gameplay position.
    /// </returns>
    /// <remarks>
    /// The cell changes at the midpoint between two anchors, not only once the
    /// next anchor itself is reached.
    /// </remarks>
    public Vector2I ArcadePixelToLogicalCell(Vector2I arcadePixel)
    {
        int halfCell = CellSizeArcade / 2;

        int x = FloorDiv(arcadePixel.X - GameplayAnchorArcade.X + halfCell, CellSizeArcade);
        int y = FloorDiv(arcadePixel.Y - GameplayAnchorArcade.Y + halfCell, CellSizeArcade);

        return new Vector2I(x, y);
    }

    /// <summary>
    /// Converts a gameplay arcade-pixel position into scene coordinates.
    /// </summary>
    /// <param name="arcadePixel">Gameplay position in arcade pixels.</param>
    /// <returns>
    /// Scene position corresponding to that gameplay anchor.
    /// </returns>
    /// <remarks>
    /// This is a pure gameplay-to-scene conversion.
    /// No sprite-specific visual offset is applied here.
    /// </remarks>
    public Vector2 ArcadePixelToScenePosition(Vector2I arcadePixel)
    {
        Sprite2D maze = GetNodeOrNull<Sprite2D>("Maze");
        if (maze == null)
            return Vector2.Zero;

        float x = maze.Position.X + arcadePixel.X * RenderScale;
        float y = maze.Position.Y + arcadePixel.Y * RenderScale;
        return new Vector2(x, y);
    }

    /// <summary>
    /// Converts an arcade-pixel delta into a scene-space delta.
    /// </summary>
    /// <param name="arcadeDelta">Delta expressed in arcade pixels.</param>
    /// <returns>
    /// Delta expressed in Godot scene pixels.
    /// </returns>
    /// <remarks>
    /// This is mainly used by gameplay actors to apply visual sprite offsets
    /// without changing their true gameplay anchor.
    /// </remarks>
    public Vector2 ArcadeDeltaToSceneDelta(Vector2I arcadeDelta)
    {
        return new Vector2(arcadeDelta.X * RenderScale, arcadeDelta.Y * RenderScale);
    }

    /// <summary>
    /// Converts a logical maze cell directly into a scene position.
    /// </summary>
    /// <param name="cell">Logical cell coordinates.</param>
    /// <returns>
    /// Scene position corresponding to the gameplay anchor of that cell.
    /// </returns>
    public Vector2 LogicalCellToScenePosition(Vector2I cell)
    {
        return ArcadePixelToScenePosition(LogicalCellToArcadePixel(cell));
    }

    // --- Helpers ------------------------------------------------------------

    /// <summary>
    /// Computes floor division for integers, including negative values.
    /// </summary>
    /// <param name="value">Dividend.</param>
    /// <param name="divisor">Divisor.</param>
    /// <returns>
    /// Mathematical floor of value / divisor.
    /// </returns>
    private static int FloorDiv(int value, int divisor)
    {
        int quotient = value / divisor;
        int remainder = value % divisor;

        if (remainder != 0 && ((value < 0) != (divisor < 0)))
            quotient--;

        return quotient;
    }

    // --- Editor Preview -----------------------------------------------------

    /// <summary>
    /// Updates the player instance preview position from the configured start cell.
    /// </summary>
    /// <remarks>
    /// This is mainly used in the editor so that changing <see cref="PlayerStartCell"/>
    /// immediately refreshes the visible placement.
    /// </remarks>
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
}