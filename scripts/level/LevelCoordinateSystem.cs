using Godot;

/// <summary>
/// Converts between the coordinate spaces used by one Lady Bug level.
/// </summary>
/// <remarks>
/// Gameplay logic uses integer arcade-pixel coordinates relative to the maze
/// origin. Godot rendering uses scene-space floating-point positions. Logical
/// maze cells and gate pivots are higher-level board coordinates on top of the
/// arcade-pixel space.
///
/// Keeping these conversions in one small value object makes the level runtime
/// easier to read and avoids scattering cell-size / render-scale assumptions
/// through unrelated systems.
/// </remarks>
internal sealed class LevelCoordinateSystem
{
    private readonly int _cellSizeArcade;
    private readonly int _renderScale;
    private readonly Vector2I _gameplayAnchorArcade;

    /// <summary>
    /// Creates a coordinate system for one level layout.
    /// </summary>
    /// <param name="cellSizeArcade">Logical cell size in original arcade pixels.</param>
    /// <param name="renderScale">Scale factor from arcade pixels to Godot scene pixels.</param>
    /// <param name="gameplayAnchorArcade">Gameplay anchor inside one logical cell.</param>
    public LevelCoordinateSystem(
        int cellSizeArcade,
        int renderScale,
        Vector2I gameplayAnchorArcade)
    {
        _cellSizeArcade = cellSizeArcade;
        _renderScale = renderScale;
        _gameplayAnchorArcade = gameplayAnchorArcade;
    }

    /// <summary>
    /// Converts a logical maze cell into its gameplay arcade-pixel anchor.
    /// </summary>
    public Vector2I LogicalCellToArcadePixel(Vector2I cell)
    {
        return cell * _cellSizeArcade + _gameplayAnchorArcade;
    }

    /// <summary>
    /// Converts a gameplay arcade-pixel position back into the logical cell that
    /// owns that position.
    /// </summary>
    public Vector2I ArcadePixelToLogicalCell(Vector2I arcadePixel)
    {
        int halfCell = _cellSizeArcade / 2;

        int x = FloorDiv(
            arcadePixel.X - _gameplayAnchorArcade.X + halfCell,
            _cellSizeArcade);

        int y = FloorDiv(
            arcadePixel.Y - _gameplayAnchorArcade.Y + halfCell,
            _cellSizeArcade);

        return new Vector2I(x, y);
    }

    /// <summary>
    /// Converts one logical gate pivot into an arcade-pixel pivot position.
    /// </summary>
    public Vector2I GatePivotToArcadePixel(Vector2I pivot)
    {
        return new Vector2I(
            pivot.X * _cellSizeArcade,
            pivot.Y * _cellSizeArcade);
    }

    /// <summary>
    /// Converts a gameplay arcade-pixel position into Godot scene coordinates.
    /// </summary>
    /// <param name="arcadePixel">Gameplay position in arcade pixels.</param>
    /// <param name="mazeSceneOrigin">Scene-space origin of the visual maze node.</param>
    public Vector2 ArcadePixelToScenePosition(Vector2I arcadePixel, Vector2 mazeSceneOrigin)
    {
        float x = mazeSceneOrigin.X + arcadePixel.X * _renderScale;
        float y = mazeSceneOrigin.Y + arcadePixel.Y * _renderScale;
        return new Vector2(x, y);
    }

    /// <summary>
    /// Converts an arcade-pixel delta into a Godot scene-space delta.
    /// </summary>
    public Vector2 ArcadeDeltaToSceneDelta(Vector2I arcadeDelta)
    {
        return new Vector2(
            arcadeDelta.X * _renderScale,
            arcadeDelta.Y * _renderScale);
    }

    /// <summary>
    /// Converts a logical cell directly into a Godot scene-space position.
    /// </summary>
    public Vector2 LogicalCellToScenePosition(Vector2I cell, Vector2 mazeSceneOrigin)
    {
        return ArcadePixelToScenePosition(
            LogicalCellToArcadePixel(cell),
            mazeSceneOrigin);
    }

    /// <summary>
    /// Converts one logical gate pivot directly into a Godot scene-space position.
    /// </summary>
    public Vector2 GatePivotToScenePosition(Vector2I pivot, Vector2 mazeSceneOrigin)
    {
        return ArcadePixelToScenePosition(
            GatePivotToArcadePixel(pivot),
            mazeSceneOrigin);
    }

    /// <summary>
    /// Computes floor division for integers, including negative values.
    /// </summary>
    private static int FloorDiv(int value, int divisor)
    {
        int quotient = value / divisor;
        int remainder = value % divisor;

        if (remainder != 0 && ((value < 0) != (divisor < 0)))
            quotient--;

        return quotient;
    }
}
