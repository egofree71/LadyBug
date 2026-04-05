using System;
using Godot;

namespace LadyBug.Gameplay.Maze;

/// <summary>
/// Represents the logical maze as a 2D grid of maze cells.
/// </summary>
/// <remarks>
/// This is the main runtime maze representation used by gameplay systems.
/// </remarks>
public sealed class MazeGrid
{
    private readonly MazeCell[,] _cells;

    /// <summary>
    /// Gets the number of logical cells horizontally.
    /// </summary>
    public int Width => _cells.GetLength(0);

    /// <summary>
    /// Gets the number of logical cells vertically.
    /// </summary>
    public int Height => _cells.GetLength(1);

    /// <summary>
    /// Initializes a new instance of the <see cref="MazeGrid"/> class.
    /// </summary>
    /// <param name="width">The maze width, in logical cells.</param>
    /// <param name="height">The maze height, in logical cells.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="width"/> or <paramref name="height"/> is less than or equal to zero.
    /// </exception>
    public MazeGrid(int width, int height)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));

        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));

        _cells = new MazeCell[width, height];
    }

    /// <summary>
    /// Determines whether the specified logical position is inside the maze bounds.
    /// </summary>
    /// <param name="cellPosition">The logical cell position to test.</param>
    /// <returns>
    /// True if the position is inside the maze bounds; otherwise false.
    /// </returns>
    public bool IsInside(Vector2I cellPosition)
    {
        return cellPosition.X >= 0 && cellPosition.X < Width
            && cellPosition.Y >= 0 && cellPosition.Y < Height;
    }

    /// <summary>
    /// Gets the cell at the specified logical position.
    /// </summary>
    /// <param name="cellPosition">The logical position of the cell to retrieve.</param>
    /// <returns>The <see cref="MazeCell"/> stored at the specified position.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="cellPosition"/> is outside the maze bounds.
    /// </exception>
    public MazeCell GetCell(Vector2I cellPosition)
    {
        if (!IsInside(cellPosition))
            throw new ArgumentOutOfRangeException(nameof(cellPosition));

        return _cells[cellPosition.X, cellPosition.Y];
    }

    /// <summary>
    /// Sets the cell at the specified logical position.
    /// </summary>
    /// <param name="cellPosition">The logical position of the cell to update.</param>
    /// <param name="cell">The cell value to store at the specified position.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="cellPosition"/> is outside the maze bounds.
    /// </exception>
    public void SetCell(Vector2I cellPosition, MazeCell cell)
    {
        if (!IsInside(cellPosition))
            throw new ArgumentOutOfRangeException(nameof(cellPosition));

        _cells[cellPosition.X, cellPosition.Y] = cell;
    }

    /// <summary>
    /// Determines whether movement is allowed from the specified cell in the given direction.
    /// </summary>
    /// <param name="cellPosition">The logical position of the source cell.</param>
    /// <param name="direction">The movement direction to test.</param>
    /// <returns>
    /// True if movement is allowed from the specified cell in that direction; otherwise false.
    /// </returns>
    /// <remarks>
    /// This method checks only the wall data of the current cell and assumes that
    /// neighbouring cells are logically consistent with it.
    /// </remarks>
    public bool CanMove(Vector2I cellPosition, Vector2I direction)
    {
        if (!IsInside(cellPosition))
            return false;

        if (direction == Vector2I.Zero)
            return false;

        Vector2I targetCell = cellPosition + direction;
        if (!IsInside(targetCell))
            return false;

        return _cells[cellPosition.X, cellPosition.Y].CanMove(direction);
    }

    /// <summary>
    /// Evaluates whether one arcade-pixel movement step is currently legal.
    /// </summary>
    /// <param name="arcadePixelPos">Current gameplay position in arcade pixels.</param>
    /// <param name="direction">Movement direction to test.</param>
    /// <param name="collisionLead">Forward probe offset used by the caller.</param>
    /// <param name="arcadePixelToLogicalCell">
    /// Function converting an arcade-pixel gameplay position into a logical cell.
    /// </param>
    /// <returns>
    /// A <see cref="MazeStepResult"/> describing whether the step is allowed and
    /// which logical cells are involved.
    /// </returns>
    /// <remarks>
    /// This helper keeps the pixel-to-cell step evaluation close to the maze logic,
    /// while still letting <c>Level</c> remain the source of truth for coordinate conversion.
    /// </remarks>
    public MazeStepResult EvaluateArcadePixelStep(
        Vector2I arcadePixelPos,
        Vector2I direction,
        Vector2I collisionLead,
        Func<Vector2I, Vector2I> arcadePixelToLogicalCell)
    {
        if (direction == Vector2I.Zero || arcadePixelToLogicalCell == null)
            return new MazeStepResult(false, Vector2I.Zero, Vector2I.Zero);

        Vector2I currentCell = arcadePixelToLogicalCell(arcadePixelPos);
        Vector2I nextPixelPos = arcadePixelPos + direction;
        Vector2I probePixel = nextPixelPos + collisionLead;
        Vector2I nextCell = arcadePixelToLogicalCell(probePixel);

        if (!IsInside(currentCell))
            return new MazeStepResult(false, currentCell, nextCell);

        if (nextCell == currentCell)
            return new MazeStepResult(true, currentCell, nextCell);

        bool allowed = CanMove(currentCell, direction);
        return new MazeStepResult(allowed, currentCell, nextCell);
    }

    /// <summary>
    /// Builds a <see cref="MazeGrid"/> from deserialized maze data.
    /// </summary>
    /// <param name="data">The deserialized maze data.</param>
    /// <returns>A new <see cref="MazeGrid"/> instance initialized from the provided data.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the provided maze data is invalid.</exception>
    /// <remarks>
    /// The serialized cell array uses a flat row-major layout:
    /// index = y * width + x.
    /// </remarks>
    public static MazeGrid FromDataFile(MazeDataFile data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        if (data.Width <= 0)
            throw new ArgumentException("Maze width must be greater than zero.", nameof(data));

        if (data.Height <= 0)
            throw new ArgumentException("Maze height must be greater than zero.", nameof(data));

        if (data.Cells == null)
            throw new ArgumentException("Maze cell array cannot be null.", nameof(data));

        int expectedCellCount = data.Width * data.Height;
        if (data.Cells.Length != expectedCellCount)
        {
            throw new ArgumentException(
                $"Maze cell count mismatch. Expected {expectedCellCount}, got {data.Cells.Length}.",
                nameof(data));
        }

        MazeGrid grid = new MazeGrid(data.Width, data.Height);

        for (int y = 0; y < data.Height; y++)
        {
            for (int x = 0; x < data.Width; x++)
            {
                int index = y * data.Width + x;
                WallFlags walls = (WallFlags)data.Cells[index];
                MazeCell cell = new MazeCell(walls);
                grid.SetCell(new Vector2I(x, y), cell);
            }
        }

        return grid;
    }
}
