using Godot;

namespace LadyBug.Actors;

/// <summary>
/// Optional console trace for hard-to-debug player movement cases.
/// </summary>
/// <remarks>
/// The trace is deliberately isolated from <see cref="PlayerMovementMotor"/> so
/// the gameplay code can stay readable. Leave <see cref="Enabled"/> disabled in
/// committed code; turn it on only while investigating a movement edge case.
/// </remarks>
internal sealed class PlayerMovementDebugTrace
{
    private const bool Enabled = false;
    private const int MameYMirror = 0xDD;

    private int _tickIndex;
    private string _path = string.Empty;
    private string _notes = string.Empty;
    private string _blocked = string.Empty;

    public void Reset()
    {
        _tickIndex = 0;
        _path = string.Empty;
        _notes = string.Empty;
        _blocked = string.Empty;
    }

    public void BeginTick()
    {
        if (!Enabled)
            return;

        _path = string.Empty;
        _notes = string.Empty;
        _blocked = string.Empty;
    }

    public void AppendPath(string path)
    {
        if (!Enabled)
            return;

        if (_path.Length == 0)
            _path = path;
        else
            _path += " -> " + path;
    }

    public void Note(string note)
    {
        if (!Enabled)
            return;

        if (_notes.Length == 0)
            _notes = note;
        else
            _notes += "; " + note;
    }

    public void MarkBlocked(Vector2I direction, string reason)
    {
        if (!Enabled)
            return;

        _blocked = $"blocked {DirectionName(direction)} by {reason}";
    }

    public void EndTick(
        Vector2I previousPixelPos,
        Vector2I currentPixelPos,
        Vector2I previousDirection,
        Vector2I currentDirection,
        Vector2I wantedDirection,
        Vector2I latchedRequestedDirection,
        Vector2I turnLaneTarget,
        PlayerTurnAssistFlags turnAssistFlags,
        bool assistedTurnActive)
    {
        if (!Enabled)
            return;

        GD.Print(
            $"MoveTick {_tickIndex++:0000}: " +
            $"In={DirectionName(wantedDirection),-5} " +
            $"Pre={FormatArcadeAsMamePosition(previousPixelPos)} " +
            $"Post={FormatArcadeAsMamePosition(currentPixelPos)} " +
            $"Dir={DirectionName(previousDirection)}->{DirectionName(currentDirection)} " +
            $"Req={DirectionName(latchedRequestedDirection)} " +
            $"Tgt={FormatArcadeAsMamePosition(turnLaneTarget)} " +
            $"Assist={(int)turnAssistFlags:X2} Assisted={assistedTurnActive} " +
            $"Path={_path} {_blocked} Notes={_notes}");
    }

    public void NoteTurnDecision(PlayerTurnWindowDecision decision, Vector2I laneTarget)
    {
        if (!Enabled)
            return;

        Note(
            $"turnPath={decision.Path} target={FormatArcadeAsMamePosition(laneTarget)} " +
            $"flags={(int)decision.AssistFlags:X2} mask=0x{decision.TurnWindowMask:X4} " +
            $"upper={decision.UpperLaneCoordinate:X2} lower={decision.LowerLaneCoordinate:X2}");
    }

    private static string FormatArcadeAsMamePosition(Vector2I pos)
    {
        return $"({pos.X:X2},{ToByte(ToMameY(pos.Y)):X2})";
    }

    private static string DirectionName(Vector2I direction)
    {
        if (direction == Vector2I.Left)
            return "Left";
        if (direction == Vector2I.Right)
            return "Right";
        if (direction == Vector2I.Up)
            return "Up";
        if (direction == Vector2I.Down)
            return "Down";
        return "None";
    }

    private static int ToByte(int value)
    {
        return value & 0xFF;
    }

    private static int ToMameY(int godotArcadeY)
    {
        return MameYMirror - godotArcadeY;
    }
}
