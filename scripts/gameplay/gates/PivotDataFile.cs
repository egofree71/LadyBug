using System.Text.Json.Serialization;

public sealed class PivotDataFile
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }
}