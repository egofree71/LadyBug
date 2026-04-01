namespace LadyBug.Gameplay.Maze
{
    /// <summary>
    /// Represents the JSON file content used to store the maze.
    /// 
    /// Example JSON:
    /// {
    ///   "width": 11,
    ///   "height": 11,
    ///   "cells": [0, 0, 2, 8, ...]
    /// }
    /// 
    /// Each integer in the "cells" array is a wall bitmask.
    /// </summary>
    public sealed class MazeDataFile
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int[] Cells { get; set; } = [];
    }
}