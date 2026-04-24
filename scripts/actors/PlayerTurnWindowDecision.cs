using Godot;

namespace LadyBug.Actors;

/// <summary>
/// Result returned by the player turn-window resolver.
/// </summary>
/// <remarks>
/// The decision tells the movement motor whether the requested turn should be
/// handled normally, through the assisted-turn path, or through a one-pixel
/// close-range correction before returning to the normal path.
///
/// The extra mask/lane fields are diagnostic data only. They are kept so the
/// debug trace can still explain why a given path was selected.
/// </remarks>
internal readonly struct PlayerTurnWindowDecision
{
    public PlayerTurnWindowDecision(
        PlayerTurnPath path,
        Vector2I laneTarget,
        PlayerTurnAssistFlags assistFlags,
        ushort turnWindowMask,
        int upperLaneCoordinate,
        int lowerLaneCoordinate)
    {
        Path = path;
        LaneTarget = laneTarget;
        AssistFlags = assistFlags;
        TurnWindowMask = turnWindowMask;
        UpperLaneCoordinate = upperLaneCoordinate;
        LowerLaneCoordinate = lowerLaneCoordinate;
    }

    public PlayerTurnPath Path { get; }
    public Vector2I LaneTarget { get; }
    public PlayerTurnAssistFlags AssistFlags { get; }
    public ushort TurnWindowMask { get; }
    public int UpperLaneCoordinate { get; }
    public int LowerLaneCoordinate { get; }
}
