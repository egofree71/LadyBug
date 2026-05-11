using System;

namespace LadyBug.Gameplay.Enemies;

/// <summary>
/// Central lookup for arcade enemy graphics and attributes.
/// </summary>
/// <remarks>
/// The original game uses one insect type per level for levels 1..8. From level 9
/// onward, each of the four enemy slots receives a different insect, calculated
/// from both the visible level number and the enemy slot index.
/// </remarks>
public static class EnemyLevelCatalog
{
    private const int MinEnemySlot = 0;
    private const int MaxEnemySlot = 3;

    /// <summary>
    /// Compatibility helper for callers that do not care about slot-specific visuals.
    /// </summary>
    public static EnemyLevelDefinition Get(int levelNumber)
    {
        return Get(levelNumber, MinEnemySlot);
    }

    /// <summary>
    /// Returns the enemy definition for the requested visible level and enemy slot.
    /// </summary>
    /// <remarks>
    /// Levels 1..8 ignore <paramref name="enemySlot"/> and return the same insect for
    /// all four slots. Levels 9+ use the reverse-engineered FUN_ram_3087 formula,
    /// so slots 0..3 form a group of four consecutive insects.
    /// </remarks>
    public static EnemyLevelDefinition Get(int levelNumber, int enemySlot)
    {
        EnemySpriteInfo spriteInfo = GetEnemySpriteInfo(levelNumber, enemySlot);

        return new EnemyLevelDefinition(
            Math.Max(1, levelNumber),
            spriteInfo,
            GetSpritesheetPathForSpriteCode(spriteInfo.SpriteCode));
    }

    /// <summary>
    /// Computes the original arcade sprite code and attribute for one enemy slot.
    /// </summary>
    public static EnemySpriteInfo GetEnemySpriteInfo(int levelNumber, int enemySlot)
    {
        if (levelNumber < 1)
            levelNumber = 1;

        if (enemySlot is < MinEnemySlot or > MaxEnemySlot)
        {
            throw new ArgumentOutOfRangeException(
                nameof(enemySlot),
                $"enemySlot must be between {MinEnemySlot} and {MaxEnemySlot}.");
        }

        // Levels 1..8: all four slots use the same insect. The arcade table has
        // the visible level-3 and level-4 insects crossed relative to natural attr
        // order, so keep both tables explicit instead of deriving attr from level.
        if (levelNumber <= 8)
        {
            ReadOnlySpan<byte> spriteByLevel = new byte[]
            {
                0x18, 0x30, 0x60, 0x48, 0x78, 0x90, 0xA8, 0xC0
            };

            ReadOnlySpan<byte> attrByLevel = new byte[]
            {
                0x01, 0x02, 0x04, 0x03, 0x05, 0x06, 0x07, 0x08
            };

            int index = levelNumber - 1;
            return new EnemySpriteInfo(spriteByLevel[index], attrByLevel[index]);
        }

        // Levels 9+: reverse-engineered FUN_ram_3087 formula.
        // start follows 0,1,2,3,4,0,1,2,0,1,... instead of a simple level % 8.
        int start = (levelNumber - 1) & 0x07;

        if (start >= 5)
            start -= 5;

        int n = start + enemySlot;

        byte spriteCode = (byte)(0x18 + 0x18 * n);
        byte attr = (byte)(n + 1);

        return new EnemySpriteInfo(spriteCode, attr);
    }

    /// <summary>
    /// Maps the original arcade sprite code to the current extracted Godot sheet.
    /// </summary>
    /// <remarks>
    /// The current project stores sheets by first visible level name. Because the
    /// arcade level-3 and level-4 visible insects are crossed, sprite code 0x48 maps
    /// to enemy_level4.png and sprite code 0x60 maps to enemy_level3.png.
    /// </remarks>
    private static string GetSpritesheetPathForSpriteCode(byte spriteCode)
    {
        return spriteCode switch
        {
            0x18 => "res://assets/sprites/enemies/enemy_level1.png",
            0x30 => "res://assets/sprites/enemies/enemy_level2.png",
            0x48 => "res://assets/sprites/enemies/enemy_level4.png",
            0x60 => "res://assets/sprites/enemies/enemy_level3.png",
            0x78 => "res://assets/sprites/enemies/enemy_level5.png",
            0x90 => "res://assets/sprites/enemies/enemy_level6.png",
            0xA8 => "res://assets/sprites/enemies/enemy_level7.png",
            0xC0 => "res://assets/sprites/enemies/enemy_level8.png",
            _ => "res://assets/sprites/enemies/enemy_level1.png"
        };
    }
}
