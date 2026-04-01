using System;

namespace LadyBug.Gameplay.Maze
{
    /// <summary>
    /// Bitmask describing which walls exist around a logical maze cell.
    /// A single cell can have any combination of the four walls.
    /// </summary>
    [Flags]
    public enum WallFlags
    {
        None  = 0,
        Up    = 1 << 0,
        Down  = 1 << 1,
        Left  = 1 << 2,
        Right = 1 << 3
    }
}