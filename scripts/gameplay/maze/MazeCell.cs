using Godot;

namespace LadyBug.Gameplay.Maze
{
    /// <summary>
    /// Represents one logical cell of the maze.
    /// </summary>
    /// <remarks>
    /// This struct is immutable and stores only the minimal gameplay data 
    /// required to determine valid movements in the four cardinal directions.
    /// </remarks>
    public readonly struct MazeCell
    {
        /// <summary>
        /// Bitmask containing the walls around this cell.
        /// </summary>
        public WallFlags Walls { get; }

        public MazeCell(WallFlags walls)
        {
            Walls = walls;
        }

        /// <summary>
        /// Gets a value indicating whether this cell has a wall on its upper side.
        /// </summary>
        public bool HasWallUp => (Walls & WallFlags.Up) != 0;

        /// <summary>
        /// Gets a value indicating whether this cell has a wall on its lower side.
        /// </summary>
        public bool HasWallDown => (Walls & WallFlags.Down) != 0;

        /// <summary>
        /// Gets a value indicating whether this cell has a wall on its left side.
        /// </summary>
        public bool HasWallLeft => (Walls & WallFlags.Left) != 0;

        /// <summary>
        /// Gets a value indicating whether this cell has a wall on its right side.
        /// </summary>
        public bool HasWallRight => (Walls & WallFlags.Right) != 0;

        /// <summary>
        /// Determines whether movement is allowed from this cell in the specified direction.
        /// </summary>
        /// <param name="direction">The direction to check.</param>
        /// <returns>
        /// <c>true</c> if movement is possible in that direction; otherwise <c>false</c>.
        /// Only the four cardinal directions are supported.
        /// </returns>
        public bool CanMove(Vector2I direction)
        {
            if (direction == Vector2I.Up)    return !HasWallUp;
            if (direction == Vector2I.Down)  return !HasWallDown;
            if (direction == Vector2I.Left)  return !HasWallLeft;
            if (direction == Vector2I.Right) return !HasWallRight;

            return false; // Direction non supportée
        }

        /// <summary>
        /// Returns a string that represents the current cell.
        /// </summary>
        public override string ToString() => $"MazeCell(Walls={Walls})";
    }
}