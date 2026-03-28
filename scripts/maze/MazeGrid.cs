using Godot;
using System.Collections.Generic;

/// <summary>
/// Represents the logical maze used by the game.
///
/// Responsibilities:
/// - stores the static maze layout without revolving doors
/// - exposes movement queries for grid-based actors
/// - builds a pixel-accurate walkable graph for straight movement
/// - exposes turn-window target queries derived from the original game logic
/// - draws a temporary debug visualization of the maze
///
/// This class currently focuses on fixed maze structure only.
/// Revolving doors and other dynamic maze elements will be added later.
/// </summary>
public partial class MazeGrid : Node2D
{
    // --- Constants ----------------------------------------------------------

    private const int RenderScale = 4;

    private const int CellSizeArcade = 16;
    private const int CellSize = CellSizeArcade * RenderScale;

    private const int MazeTopArcade = 0x30;

    private const int VerticalLaneCenterArcade = 8;
    private const int HorizontalLaneCenterArcade = 7;

    // --- Static Maze Data ---------------------------------------------------

    // Direction bits:
    // 0x1 = left
    // 0x2 = up
    // 0x4 = right
    // 0x8 = down
    private static readonly byte[,] OpenMask =
    {
        { 0xC, 0xD, 0xD, 0x5, 0xD, 0xD, 0xD, 0x5, 0xD, 0xD, 0x9 },
        { 0xA, 0x6, 0x7, 0xD, 0xB, 0xA, 0xE, 0xD, 0x7, 0x3, 0xA },
        { 0xE, 0x5, 0xD, 0xF, 0x7, 0xF, 0x7, 0xF, 0xD, 0x5, 0xB },
        { 0xA, 0xC, 0xB, 0xE, 0x9, 0xA, 0xC, 0xB, 0xE, 0x9, 0xA },
        { 0xA, 0xE, 0xF, 0xF, 0xF, 0x7, 0xF, 0xF, 0xF, 0xB, 0xA },
        { 0xE, 0x3, 0xE, 0xB, 0xA, 0x8, 0xA, 0xE, 0xB, 0x6, 0xB },
        { 0xE, 0x5, 0xB, 0xE, 0xF, 0xF, 0xF, 0xB, 0xE, 0x5, 0xB },
        { 0xE, 0x9, 0xE, 0xF, 0x3, 0xA, 0x6, 0xF, 0xB, 0xC, 0xB },
        { 0xE, 0x7, 0xF, 0xF, 0xD, 0xF, 0xD, 0xF, 0xF, 0x7, 0xB },
        { 0xA, 0xC, 0xB, 0xE, 0x3, 0xA, 0x6, 0xB, 0xE, 0x9, 0xA },
        { 0x6, 0x7, 0x7, 0x7, 0x5, 0x7, 0x5, 0x7, 0x7, 0x7, 0x3 }
    };

    // Bit i means that X = 8 + 16 * i is a valid vertical lane center for that row.
    private static readonly ushort[] VerticalCentersByRowMask =
    {
        0x777, 0x3DE, 0x5FD, 0x3DE, 0x3FE,
        0x7AF, 0x5FD, 0x7DF, 0x7FF, 0x3DE, 0x7AF
    };

    // Bit i means that Y = 0x36 + 16 * i is a valid horizontal turn decision center.
    private static readonly ushort[] HorizontalCentersByColumnMask =
    {
        0x5E5, 0x7FF, 0x7FF, 0x7FE, 0x3DF, 0x575,
        0x3DF, 0x7FE, 0x7FF, 0x7BB, 0x5E5, 0x201
    };

    // --- Runtime Data -------------------------------------------------------

    private readonly HashSet<Vector2I> _walkablePixels = new();

    // --- Lifecycle ----------------------------------------------------------

    public override void _Ready()
    {
        BuildWalkablePixels();
        QueueRedraw();
    }

    // --- Movement Queries ---------------------------------------------------

    /// <summary>
    /// Returns true if the logical maze cell allows movement in the requested direction.
    /// This is useful for direction checks at intersections.
    /// </summary>
    public bool CanMoveFromCell(Vector2I arcadePixelPos, Vector2I dir)
    {
        int dirMask = ToArcadeDirMask(dir);
        if (dirMask == 0)
            return false;

        int cellX = arcadePixelPos.X >> 4;
        int cellY = (arcadePixelPos.Y >> 4) - 3;

        if (!IsInsideCellBounds(cellX, cellY))
            return false;

        byte cellMask = OpenMask[cellY, cellX];
        return (cellMask & dirMask) != 0;
    }

    /// <summary>
    /// Returns true if the next arcade pixel belongs to the walkable lane graph.
    /// </summary>
    public bool CanStepToNextPixel(Vector2I currentArcadePixelPos, Vector2I dir)
    {
        Vector2I next = currentArcadePixelPos + dir;
        return _walkablePixels.Contains(next);
    }

    /// <summary>
    /// Tries to capture a turn from horizontal motion into a vertical lane.
    /// Returns the target X lane center if successful.
    /// </summary>
    public bool TryGetVerticalLaneX(Vector2I arcadePixelPos, out int targetX)
    {
        targetX = 0;

        int rowIndex = (arcadePixelPos.Y - 0x36) >> 4;
        if (!IsInsideRowIndex(rowIndex))
            return false;

        ushort mask = VerticalCentersByRowMask[rowIndex];
        FindBracketCenters(mask, 8, arcadePixelPos.X, out int lowX, out int highX);

        if (arcadePixelPos.X >= highX - 4)
        {
            targetX = highX;
            return true;
        }

        if (arcadePixelPos.X >= highX - 6)
        {
            targetX = highX;
            return true;
        }

        if (arcadePixelPos.X < lowX + 5)
        {
            targetX = lowX;
            return true;
        }

        if (arcadePixelPos.X < lowX + 7)
        {
            targetX = lowX;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Tries to capture a turn from vertical motion into a horizontal lane.
    /// Returns the actual horizontal lane Y used for movement.
    /// </summary>
    public bool TryGetHorizontalLaneY(Vector2I arcadePixelPos, out int laneY)
    {
        laneY = 0;

        int columnIndex = (arcadePixelPos.X - 0x08) >> 4;
        if (!IsInsideColumnIndex(columnIndex))
            return false;

        ushort mask = HorizontalCentersByColumnMask[columnIndex];
        FindBracketCenters(mask, 0x36, arcadePixelPos.Y, out int lowY, out int highY);

        int targetY;

        if (arcadePixelPos.Y >= highY - 4)
        {
            targetY = highY;
        }
        else if (arcadePixelPos.Y >= highY - 7)
        {
            targetY = highY;
        }
        else if (arcadePixelPos.Y < lowY + 5)
        {
            targetY = lowY;
        }
        else if (arcadePixelPos.Y < lowY + 8)
        {
            targetY = lowY;
        }
        else
        {
            return false;
        }

        // The original decision logic is around Y % 16 == 6,
        // while the actual horizontal lane travel is centered on Y % 16 == 7.
        laneY = targetY + 1;
        return true;
    }

    // --- Debug Rendering ----------------------------------------------------

    public override void _Draw()
    {
        int rows = OpenMask.GetLength(0);
        int cols = OpenMask.GetLength(1);

        Color cellColor = new Color(0.18f, 0.18f, 0.18f);
        Color laneColor = new Color(0.25f, 0.85f, 0.45f);
        Color centerColor = new Color(0.95f, 0.95f, 0.60f);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                DrawCell(row, col, OpenMask[row, col], cellColor, laneColor, centerColor);
            }
        }
    }

    private void DrawCell(int row, int col, byte mask, Color cellColor, Color laneColor, Color centerColor)
    {
        Vector2 cellTopLeft = new Vector2(
            col * CellSize,
            row * CellSize
        );

        Rect2 cellRect = new Rect2(cellTopLeft, new Vector2(CellSize, CellSize));
        DrawRect(cellRect, cellColor, false, 2.0f);

        Vector2 center = new Vector2(
            (col * CellSizeArcade + VerticalLaneCenterArcade) * RenderScale,
            (row * CellSizeArcade + HorizontalLaneCenterArcade) * RenderScale
        );

        if ((mask & 0x1) != 0)
            DrawLine(center, center + Vector2.Left * (CellSize / 2.0f), laneColor, 8.0f);

        if ((mask & 0x2) != 0)
            DrawLine(center, center + Vector2.Up * (CellSize / 2.0f), laneColor, 8.0f);

        if ((mask & 0x4) != 0)
            DrawLine(center, center + Vector2.Right * (CellSize / 2.0f), laneColor, 8.0f);

        if ((mask & 0x8) != 0)
            DrawLine(center, center + Vector2.Down * (CellSize / 2.0f), laneColor, 8.0f);

        DrawCircle(center, 4.0f, centerColor);
    }

    // --- Walkable Graph Construction ---------------------------------------

    /// <summary>
    /// Builds a pixel-accurate walkable graph from the static maze layout.
    /// This version still ignores revolving doors on purpose.
    /// </summary>
    private void BuildWalkablePixels()
    {
        _walkablePixels.Clear();

        int rows = OpenMask.GetLength(0);
        int cols = OpenMask.GetLength(1);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                AddWalkableCellSegments(row, col, OpenMask[row, col]);
            }
        }
    }

    private void AddWalkableCellSegments(int row, int col, byte mask)
    {
        int centerX = col * CellSizeArcade + VerticalLaneCenterArcade;
        int centerY = MazeTopArcade + row * CellSizeArcade + HorizontalLaneCenterArcade;

        _walkablePixels.Add(new Vector2I(centerX, centerY));

        if ((mask & 0x1) != 0)
            AddHorizontalSegment(centerY, centerX - 8, centerX);

        if ((mask & 0x2) != 0)
            AddVerticalSegment(centerX, centerY - 8, centerY);

        if ((mask & 0x4) != 0)
            AddHorizontalSegment(centerY, centerX, centerX + 8);

        if ((mask & 0x8) != 0)
            AddVerticalSegment(centerX, centerY, centerY + 8);
    }

    private void AddHorizontalSegment(int y, int xStart, int xEnd)
    {
        int from = Mathf.Min(xStart, xEnd);
        int to = Mathf.Max(xStart, xEnd);

        for (int x = from; x <= to; x++)
        {
            _walkablePixels.Add(new Vector2I(x, y));
        }
    }

    private void AddVerticalSegment(int x, int yStart, int yEnd)
    {
        int from = Mathf.Min(yStart, yEnd);
        int to = Mathf.Max(yStart, yEnd);

        for (int y = from; y <= to; y++)
        {
            _walkablePixels.Add(new Vector2I(x, y));
        }
    }

    // --- Helpers ------------------------------------------------------------

    private static void FindBracketCenters(ushort mask, int baseCoord, int pos, out int low, out int high)
    {
        low = int.MinValue;
        high = int.MaxValue;

        for (int bit = 0; bit < 16; bit++)
        {
            if ((mask & (1 << bit)) == 0)
                continue;

            int center = baseCoord + 16 * bit;

            if (center <= pos)
                low = center;

            if (center >= pos && high == int.MaxValue)
                high = center;
        }

        if (low == int.MinValue && high != int.MaxValue)
            low = high;

        if (high == int.MaxValue && low != int.MinValue)
            high = low;
    }

    private static bool IsInsideCellBounds(int cellX, int cellY)
    {
        return cellX >= 0
            && cellX < OpenMask.GetLength(1)
            && cellY >= 0
            && cellY < OpenMask.GetLength(0);
    }

    private static bool IsInsideRowIndex(int rowIndex)
    {
        return rowIndex >= 0 && rowIndex < VerticalCentersByRowMask.Length;
    }

    private static bool IsInsideColumnIndex(int columnIndex)
    {
        return columnIndex >= 0 && columnIndex < HorizontalCentersByColumnMask.Length;
    }

    private static int ToArcadeDirMask(Vector2I dir)
    {
        if (dir == Vector2I.Left)  return 0x1;
        if (dir == Vector2I.Up)    return 0x2;
        if (dir == Vector2I.Right) return 0x4;
        if (dir == Vector2I.Down)  return 0x8;
        return 0;
    }
}