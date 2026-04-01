using Godot;

namespace LadyBug.Gameplay.Maze
{
    /// <summary>
    /// Represents one logical cell of the maze.
    /// This class stores only the gameplay data needed to decide
    /// whether movement is allowed in each direction.
    /// </summary>
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
        /// Returns true if the cell has a wall on its upper side.
        /// </summary>
        public bool HasWallUp => (Walls & WallFlags.Up) != 0;

        /// <summary>
        /// Returns true if the cell has a wall on its lower side.
        /// </summary>
        public bool HasWallDown => (Walls & WallFlags.Down) != 0;

        /// <summary>
        /// Returns true if the cell has a wall on its left side.
        /// </summary>
        public bool HasWallLeft => (Walls & WallFlags.Left) != 0;

        /// <summary>
        /// Returns true if the cell has a wall on its right side.
        /// </summary>
        public bool HasWallRight => (Walls & WallFlags.Right) != 0;

        /// <summary>
        /// Returns true if movement is allowed in the given logical direction.
        /// Only the four cardinal directions are supported.
        /// </summary>
        public bool CanMove(Vector2I direction)
        {
            if (direction == Vector2I.Up)
                return !HasWallUp;

            if (direction == Vector2I.Down)
                return !HasWallDown;

            if (direction == Vector2I.Left)
                return !HasWallLeft;

            if (direction == Vector2I.Right)
                return !HasWallRight;

            return false;
        }

        public override string ToString()
        {
            return $"MazeCell(Walls={Walls})";
        }
    }
}