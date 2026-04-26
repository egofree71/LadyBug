using Godot;

namespace LadyBug.Gameplay.Collectibles;

/// <summary>
/// Tracks the short pickup popup / freeze window used after collecting hearts and letters.
/// </summary>
public sealed class CollectiblePickupPopupState
{
    public const int DurationTicks = 0x1E;

    public bool IsActive { get; private set; }
    public Vector2I Cell { get; private set; }
    public int BaseScore { get; private set; }
    public int Multiplier { get; private set; }
    public int ScoreDelta { get; private set; }
    public int TicksRemaining { get; private set; }

    public void Start(Vector2I cell, int baseScore, int multiplier, int scoreDelta)
    {
        Cell = cell;
        BaseScore = baseScore;
        Multiplier = multiplier;
        ScoreDelta = scoreDelta;
        TicksRemaining = DurationTicks;
        IsActive = true;
    }

    /// <summary>
    /// Advances the popup timer by one gameplay tick.
    /// </summary>
    /// <returns><see langword="true"/> when the popup has just completed.</returns>
    public bool AdvanceOneTick()
    {
        if (!IsActive)
            return false;

        TicksRemaining--;

        if (TicksRemaining > 0)
            return false;

        Clear();
        return true;
    }

    public void Clear()
    {
        IsActive = false;
        Cell = Vector2I.Zero;
        BaseScore = 0;
        Multiplier = 1;
        ScoreDelta = 0;
        TicksRemaining = 0;
    }
}
