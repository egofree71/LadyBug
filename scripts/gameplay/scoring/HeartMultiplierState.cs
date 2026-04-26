namespace LadyBug.Gameplay.Scoring;

/// <summary>
/// Tracks the score multiplier unlocked by collecting blue hearts.
/// </summary>
public sealed class HeartMultiplierState
{
    private const int MaxStep = 3;

    public int Step { get; private set; }

    public int CurrentMultiplier => Step switch
    {
        0 => 1,
        1 => 2,
        2 => 3,
        _ => 5,
    };

    public void Reset()
    {
        Step = 0;
    }

    public bool AdvanceOneStep()
    {
        if (Step >= MaxStep)
            return false;

        Step++;
        return true;
    }
}
