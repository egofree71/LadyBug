using System.Text.Json.Serialization;

namespace LadyBug.Gameplay.Gates;
    
/// <summary>
/// Serialized pivot coordinates for one rotating gate in maze.json.
/// </summary>
/// <remarks>
/// These coordinates identify the logical pivot position of the gate
/// in the gate grid extracted from the maze.
/// They are not scene coordinates.
/// </remarks>
public sealed class PivotDataFile
{
    /// <summary>
    /// Pivot X coordinate in gate-grid space.
    /// </summary>
    [JsonPropertyName("x")]
    public int X { get; set; }

    /// <summary>
    /// Pivot Y coordinate in gate-grid space.
    /// </summary>
    [JsonPropertyName("y")]
    public int Y { get; set; }
}