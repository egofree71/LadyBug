using System;

namespace LadyBug.Actors;

/// <summary>
/// Describes which orthogonal axis may be corrected while resolving a turn.
/// </summary>
[Flags]
internal enum PlayerTurnAssistFlags
{
    None = 0,
    CorrectY = 1 << 0,
    CorrectX = 1 << 1,
}
