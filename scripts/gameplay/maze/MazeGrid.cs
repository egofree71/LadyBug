using System;
using Godot;

namespace LadyBug.Gameplay.Maze
{
    /// <summary>
    /// Stores the logical maze as a 2D array of MazeCell.
    /// This class is the main runtime representation used by gameplay code.
    /// </summary>
    public sealed class MazeGrid
    {
        private readonly MazeCell[,] _cells;

        /// <summary>
        /// Number of logical cells horizontally.
        /// </summary>
        public int Width => _cells.GetLength(0);

        /// <summary>
        /// Number of logical cells vertically.
        /// </summary>
        public int Height => _cells.GetLength(1);

        public MazeGrid(int width, int height)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width));

            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height));

            _cells = new MazeCell[width, height];
        }

        /// <summary>
        /// Returns true if the given logical position is inside the maze bounds.
        /// </summary>
        public bool IsInside(Vector2I cellPosition)
        {
            return cellPosition.X >= 0
                && cellPosition.X < Width
                && cellPosition.Y >= 0
                && cellPosition.Y < Height;
        }

        /// <summary>
        /// Returns the cell at the given logical position.
        /// Throws if the position is outside the maze.
        /// </summary>
        public MazeCell GetCell(Vector2I cellPosition)
        {
            if (!IsInside(cellPosition))
                throw new ArgumentOutOfRangeException(nameof(cellPosition));

            return _cells[cellPosition.X, cellPosition.Y];
        }

        /// <summary>
        /// Writes a cell at the given logical position.
        /// Throws if the position is outside the maze.
        /// </summary>
        public void SetCell(Vector2I cellPosition, MazeCell cell)
        {
            if (!IsInside(cellPosition))
                throw new ArgumentOutOfRangeException(nameof(cellPosition));

            _cells[cellPosition.X, cellPosition.Y] = cell;
        }

        /// <summary>
        /// Returns true if movement is allowed from the given cell
        /// in the given direction.
        /// 
        /// This checks only the current cell's wall data.
        /// It assumes the maze data is internally consistent.
        /// For example, if a cell can move right, the neighbour cell
        /// should also allow movement from its left side.
        /// </summary>
        public bool CanMove(Vector2I cellPosition, Vector2I direction)
        {
            if (!IsInside(cellPosition))
                return false;

            return _cells[cellPosition.X, cellPosition.Y].CanMove(direction);
        }

        /// <summary>
        /// Builds a MazeGrid from the deserialized JSON data.
        /// 
        /// The JSON stores cells as a flat 1D array in row-major order:
        /// index = y * width + x
        /// </summary>
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
                    // The JSON uses a flat array, so we convert (x, y)
                    // into a linear index using row-major layout.
                    int index = y * data.Width + x;

                    WallFlags walls = (WallFlags)data.Cells[index];
                    MazeCell cell = new MazeCell(walls);

                    grid.SetCell(new Vector2I(x, y), cell);
                }
            }

            return grid;
        }
    }
}