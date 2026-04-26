using LadyBug.Gameplay.Collectibles;

namespace LadyBug.Gameplay.Scoring;

/// <summary>
/// Computes the score awarded by collectibles from their semantic gameplay state.
/// </summary>
public static class CollectibleScoreService
{
    public static CollectibleScoreCalculation Calculate(
        CollectibleKind kind,
        CollectibleColor color,
        int multiplier)
    {
        int safeMultiplier = multiplier <= 0 ? 1 : multiplier;
        int baseScore = GetBaseScore(kind, color);

        return new CollectibleScoreCalculation(baseScore, safeMultiplier);
    }

    private static int GetBaseScore(CollectibleKind kind, CollectibleColor color)
    {
        return kind switch
        {
            CollectibleKind.Flower => 10,
            CollectibleKind.Heart => GetColorScore(color),
            CollectibleKind.Letter => GetColorScore(color),
            _ => 0,
        };
    }

    private static int GetColorScore(CollectibleColor color)
    {
        return color switch
        {
            CollectibleColor.Blue => 100,
            CollectibleColor.Yellow => 300,
            CollectibleColor.Red => 800,
            _ => 0,
        };
    }
}
