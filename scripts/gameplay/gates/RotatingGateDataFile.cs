using System;
using System.Text.Json.Serialization;

namespace LadyBug.Gameplay.Gates;

/// <summary>
/// Serialized rotating gate entry loaded from <c>maze.json</c>.
/// </summary>
/// <remarks>
/// This class only represents static data coming from the JSON file:
/// - identifier
/// - pivot position
/// - initial stable orientation
///
/// It does not represent runtime state changes.
/// </remarks>
public sealed class RotatingGateDataFile
{
    /// <summary>
    /// Unique identifier of the gate inside the level data.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Logical pivot position of the gate.
    /// </summary>
    [JsonPropertyName("pivot")]
    public PivotDataFile Pivot { get; set; } = new();

    /// <summary>
    /// Initial stable orientation read from JSON.
    /// Expected values are <c>"horizontal"</c> or <c>"vertical"</c>.
    /// </summary>
    [JsonPropertyName("initialOrientation")]
    public string InitialOrientation { get; set; } = string.Empty;

    /// <summary>
    /// Converts the serialized orientation string into the runtime enum.
    /// </summary>
    /// <returns>The parsed gate orientation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the JSON value is unknown.
    /// </exception>
    public GateOrientation GetOrientation()
    {
        return InitialOrientation.ToLowerInvariant() switch
        {
            "horizontal" => GateOrientation.Horizontal,
            "vertical" => GateOrientation.Vertical,
            _ => throw new InvalidOperationException(
                $"Unknown gate orientation '{InitialOrientation}'.")
        };
    }
}
