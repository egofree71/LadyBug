using System;

namespace LadyBug.Gameplay.Collectibles;

/// <summary>
/// Level-to-vegetable lookup for the central bonus item.
/// </summary>
public static class VegetableBonusCatalog
{
    /// <summary>
    /// Number of 64x64 frames in assets/sprites/props/vegetables.png.
    /// </summary>
    public const int FrameCount = 18;

    /// <summary>
    /// Returns the spritesheet frame used by the vegetable for the given level.
    /// Levels above 18 keep using the final raifort frame.
    /// </summary>
    public static int GetFrame(int levelNumber)
    {
        return Math.Clamp(levelNumber, 1, FrameCount) - 1;
    }

    /// <summary>
    /// Returns the fixed vegetable bonus score for the given level.
    /// The vegetable score is intentionally not multiplied by the blue-heart multiplier.
    /// </summary>
    public static int GetScore(int levelNumber)
    {
        return 1000 + GetFrame(levelNumber) * 500;
    }
}
