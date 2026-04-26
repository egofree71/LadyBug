namespace LadyBug.Gameplay.Scoring;

/// <summary>
/// Immutable scoring result for one collected object.
/// </summary>
public readonly struct CollectibleScoreCalculation
{
    public CollectibleScoreCalculation(int baseScore, int multiplier)
    {
        BaseScore = baseScore;
        Multiplier = multiplier;
        ScoreDelta = baseScore * multiplier;
    }

    public int BaseScore { get; }
    public int Multiplier { get; }
    public int ScoreDelta { get; }
    public bool HasScore => ScoreDelta > 0;
}
