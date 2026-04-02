using System;

namespace LadyBug.Gameplay.Maze
{
    /// <summary>
    /// Represents the wall configuration of a logical maze cell as a bitmask.
    /// </summary>
    /// <remarks>
    /// Multiple values can be combined to describe any valid combination of the
    /// four cardinal walls around a cell.
    /// </remarks>
    [Flags]
    public enum WallFlags
    {
        /// <summary>
        /// Indicates that the cell has no walls.
        /// </summary>
        None = 0,

        /// <summary>
        /// Indicates that the cell has a wall on its upper side.
        /// </summary>
        Up = 1 << 0,

        /// <summary>
        /// Indicates that the cell has a wall on its lower side.
        /// </summary>
        Down = 1 << 1,

        /// <summary>
        /// Indicates that the cell has a wall on its left side.
        /// </summary>
        Left = 1 << 2,

        /// <summary>
        /// Indicates that the cell has a wall on its right side.
        /// </summary>
        Right = 1 << 3
    }
}