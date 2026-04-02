namespace LadyBug.Gameplay.Maze
{
    /// <summary>
    /// Represents the serialized JSON data used to load a maze definition.
    /// </summary>
    /// <remarks>
    /// Each entry in <see cref="Cells"/> stores the wall bitmask for one logical maze cell.
    /// </remarks>
    public sealed class MazeDataFile
    {
        /// <summary>
        /// Gets or sets the maze width, in logical cells.
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Gets or sets the maze height, in logical cells.
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Gets or sets the flattened cell data array.
        /// Each value represents the wall bitmask of one maze cell.
        /// </summary>
        public int[] Cells { get; set; } = [];
    }
}