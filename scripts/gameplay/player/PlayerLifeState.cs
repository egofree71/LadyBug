using System;

namespace LadyBug.Gameplay.Player;

/// <summary>
/// Tracks the current player's remaining lives.
/// </summary>
/// <remarks>
/// This is intentionally small and session-like, but it still lives outside
/// Level so EXTRA can later grant a life without inventing another temporary
/// state variable.
/// </remarks>
public sealed class PlayerLifeState
{
    public const int DefaultInitialLives = 3;

    public int Lives { get; private set; } = DefaultInitialLives;

    public bool IsGameOver => Lives <= 0;

    public void Reset(int initialLives = DefaultInitialLives)
    {
        Lives = Math.Max(0, initialLives);
    }

    /// <summary>
    /// Removes one life if possible.
    /// </summary>
    /// <returns><see langword="true"/> if a life was actually removed.</returns>
    public bool LoseLife()
    {
        if (Lives <= 0)
            return false;

        Lives--;
        return true;
    }

    public void AddLife(int count = 1)
    {
        if (count <= 0)
            return;

        Lives += count;
    }
}
