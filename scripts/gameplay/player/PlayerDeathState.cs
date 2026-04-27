using System;

namespace LadyBug.Gameplay.Player;

/// <summary>
/// Tracks the short gameplay freeze used after the player dies.
/// </summary>
public sealed class PlayerDeathState
{
    private int _ticksRemaining;

    public bool IsActive => _ticksRemaining > 0;

    public int TicksRemaining => _ticksRemaining;

    public void Start(int durationTicks)
    {
        _ticksRemaining = Math.Max(1, durationTicks);
    }

    /// <summary>
    /// Advances the death timer by one gameplay tick.
    /// </summary>
    /// <returns><see langword="true"/> when the death wait has just completed.</returns>
    public bool AdvanceOneTick()
    {
        if (_ticksRemaining <= 0)
            return false;

        _ticksRemaining--;
        return _ticksRemaining <= 0;
    }

    public void Reset()
    {
        _ticksRemaining = 0;
    }
}
