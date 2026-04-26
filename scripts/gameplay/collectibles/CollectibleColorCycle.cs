namespace LadyBug.Gameplay.Collectibles;

/// <summary>
/// Tracks the global color cycle used by hearts and letters during gameplay.
/// </summary>
/// <remarks>
/// The original game classifies collectible color through a 600-tick cycle:
/// red for 31 ticks, yellow for 149 ticks, then blue for 420 ticks.
///
/// This remake starts the visible cycle in the blue range, then keeps the same
/// timing proportions: blue -> red -> yellow -> blue.
/// </remarks>
public sealed class CollectibleColorCycle
{
    private const int TotalTicks = 0x0258;  // 600
    private const int RedEnd = 0x001F;      // 31
    private const int YellowEnd = 0x00B4;   // 180

    // Starting at YellowEnd puts the cycle directly inside the blue range.
    private int _tick = YellowEnd;

    public CollectibleColor CurrentColor => ClassifyTick(_tick);

    public void ResetToBlue()
    {
        _tick = YellowEnd;
    }

    /// <summary>
    /// Advances the cycle by one gameplay tick.
    /// </summary>
    /// <returns><see langword="true"/> if the visible color changed.</returns>
    public bool AdvanceOneTick()
    {
        CollectibleColor previousColor = CurrentColor;

        _tick = (_tick + 1) % TotalTicks;

        return CurrentColor != previousColor;
    }

    private static CollectibleColor ClassifyTick(int tick)
    {
        int normalizedTick = tick % TotalTicks;

        if (normalizedTick < RedEnd)
            return CollectibleColor.Red;

        if (normalizedTick < YellowEnd)
            return CollectibleColor.Yellow;

        return CollectibleColor.Blue;
    }
}
