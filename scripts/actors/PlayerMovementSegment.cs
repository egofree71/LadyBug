using Godot;

namespace LadyBug.Actors;

/// <summary>
/// Represents one real one-pixel gameplay movement completed during a motor tick.
/// </summary>
/// <remarks>
/// A normal tick usually contains one segment. An assisted turn may contain two:
/// first an orthogonal alignment correction, then one pixel in the requested
/// direction. Reporting the full path lets gameplay systems consume collectibles
/// crossed during special turns.
/// </remarks>
public readonly struct PlayerMovementSegment
{
    public PlayerMovementSegment(
        Vector2I startArcadePixelPos,
        Vector2I endArcadePixelPos,
        Vector2I direction)
    {
        StartArcadePixelPos = startArcadePixelPos;
        EndArcadePixelPos = endArcadePixelPos;
        Direction = direction;
    }

    public Vector2I StartArcadePixelPos { get; }
    public Vector2I EndArcadePixelPos { get; }
    public Vector2I Direction { get; }
}
