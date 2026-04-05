using System.Text.Json.Serialization;

namespace LadyBug.Gameplay.Gates;

/// <summary>
/// Serialized pivot coordinates for one rotating gate in <c>maze.json</c>.
/// </summary>
/// <remarks>
/// These coordinates identify the logical pivot position of the gate in the
/// internal gate grid. They are not scene-space coordinates.
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
