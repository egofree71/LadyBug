using System;
using System.Text.Json.Serialization;

public sealed class RotatingGateDataFile
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("pivot")]
    public PivotDataFile Pivot { get; set; } = new();

    [JsonPropertyName("initialOrientation")]
    public string InitialOrientation { get; set; } = string.Empty;

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