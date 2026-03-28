using Godot;
using System.Collections.Generic;

/// <summary>
/// Represents the logical maze used by the game.
/// 
/// Responsibilities:
/// - stores the static maze layout without revolving doors
/// - exposes movement queries for grid-based actors
/// - builds a pixel-accurate walkable graph for straight movement
/// - draws a temporary debug visualization of the maze
/// 
/// This class currently focuses on fixed maze structure only.
/// Revolving doors and other dynamic maze elements will be added later.
/// </summary>
public partial class MazeGrid : Node2D
{
    // 1 arcade pixel = 4 rendered pixels.
    private const int RenderScale = 4;

    // Original arcade maze cell size.
    private const int CellSizeArcade = 16;
    private const int CellSize = CellSizeArcade * RenderScale;

    // The original maze logic starts at arcade Y = 0x30.
    // We keep this value in the logical coordinate system.
    private const int MazeTopArcade = 0x30;

    // Lane centers used by the movement logic.
    private const int VerticalLaneCenterArcade = 8;
    private const int HorizontalLaneCenterArcade = 7;

    // Static maze layout without revolving doors.
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

    // Pixel-accurate walkable graph used for straight movement.
    private readonly HashSet<Vector2I> _walkablePixels = new();

    public override void _Ready()
    {
        BuildWalkablePixels();
        QueueRedraw();
    }

    /// <summary>
    /// Returns true if the logical maze cell allows movement in the requested direction.
    /// This is useful for turn decisions at intersections.
    /// </summary>
    public bool CanMove(Vector2I arcadePixelPos, Vector2I dir)
    {
        int dirMask = ToArcadeDirMask(dir);
        if (dirMask == 0)
            return false;

        int cellX = arcadePixelPos.X >> 4;
        int cellY = (arcadePixelPos.Y >> 4) - 3;

        if (cellX < 0 || cellX >= OpenMask.GetLength(1) || cellY < 0 || cellY >= OpenMask.GetLength(0))
            return false;

        byte cellMask = OpenMask[cellY, cellX];
        return (cellMask & dirMask) != 0;
    }

    /// <summary>
    /// Returns true if the next arcade pixel belongs to the walkable lane graph.
    /// This is used for actual pixel-by-pixel forward motion.
    /// </summary>
    public bool CanStep(Vector2I currentArcadePixelPos, Vector2I dir)
    {
        Vector2I next = currentArcadePixelPos + dir;
        return _walkablePixels.Contains(next);
    }

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
                byte mask = OpenMask[row, col];

                // The debug rendering starts at the top of the screen.
                // We only keep the arcade offset in the logical model, not in screen placement.
                Vector2 cellTopLeft = new Vector2(
                    col * CellSize,
                    row * CellSize
                );

                Rect2 cellRect = new Rect2(cellTopLeft, new Vector2(CellSize, CellSize));

                // Draw the debug cell outline.
                DrawRect(cellRect, cellColor, false, 2.0f);

                // Draw a simplified lane center for visualization.
                Vector2 center = new Vector2(
                    (col * CellSizeArcade + VerticalLaneCenterArcade) * RenderScale,
                    (row * CellSizeArcade + HorizontalLaneCenterArcade) * RenderScale
                );

                if ((mask & 0x1) != 0) // left
                {
                    DrawLine(center, center + Vector2.Left * (CellSize / 2.0f), laneColor, 8.0f);
                }

                if ((mask & 0x2) != 0) // up
                {
                    DrawLine(center, center + Vector2.Up * (CellSize / 2.0f), laneColor, 8.0f);
                }

                if ((mask & 0x4) != 0) // right
                {
                    DrawLine(center, center + Vector2.Right * (CellSize / 2.0f), laneColor, 8.0f);
                }

                if ((mask & 0x8) != 0) // down
                {
                    DrawLine(center, center + Vector2.Down * (CellSize / 2.0f), laneColor, 8.0f);
                }

                DrawCircle(center, 4.0f, centerColor);
            }
        }
    }

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
                byte mask = OpenMask[row, col];

                int centerX = col * CellSizeArcade + VerticalLaneCenterArcade;
                int centerY = MazeTopArcade + row * CellSizeArcade + HorizontalLaneCenterArcade;

                // Always register the lane center itself.
                _walkablePixels.Add(new Vector2I(centerX, centerY));

                if ((mask & 0x1) != 0) // left
                {
                    AddHorizontalSegment(centerY, centerX - 8, centerX);
                }

                if ((mask & 0x2) != 0) // up
                {
                    AddVerticalSegment(centerX, centerY - 8, centerY);
                }

                if ((mask & 0x4) != 0) // right
                {
                    AddHorizontalSegment(centerY, centerX, centerX + 8);
                }

                if ((mask & 0x8) != 0) // down
                {
                    AddVerticalSegment(centerX, centerY, centerY + 8);
                }
            }
        }
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

    private static int ToArcadeDirMask(Vector2I dir)
    {
        if (dir == Vector2I.Left)  return 0x1;
        if (dir == Vector2I.Up)    return 0x2;
        if (dir == Vector2I.Right) return 0x4;
        if (dir == Vector2I.Down)  return 0x8;
        return 0;
    }
}