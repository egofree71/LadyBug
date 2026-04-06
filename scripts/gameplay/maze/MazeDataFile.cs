using System;
using System.Text.Json.Serialization;

namespace LadyBug.Gameplay.Maze;

/// <summary>
/// Serialized root structure loaded from maze.json.
/// </summary>
/// <remarks>
/// This file contains only the static maze definition:
/// - logical maze dimensions
/// - the wall bitmask array for all cells
///
/// Rotating gates are now authored directly in Level.tscn and are no longer
/// serialized inside maze.json.
/// </remarks>
public sealed class MazeDataFile
{
    /// <summary>
    /// Logical maze width in cells.
    /// </summary>
    [JsonPropertyName("width")]
    public int Width { get; set; }

    /// <summary>
    /// Logical maze height in cells.
    /// </summary>
    [JsonPropertyName("height")]
    public int Height { get; set; }

    /// <summary>
    /// Flat array of wall bitmasks, one entry per logical cell.
    /// </summary>
    [JsonPropertyName("cells")]
    public int[] Cells { get; set; } = Array.Empty<int>();
}
