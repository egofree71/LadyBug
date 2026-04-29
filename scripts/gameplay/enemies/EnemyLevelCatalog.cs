namespace LadyBug.Gameplay.Enemies;

/// <summary>
/// Central lookup for the enemy graphics used by each visible level.
/// </summary>
/// <remarks>
/// The uploaded level 2-8 spritesheets follow the same six-frame layout as the
/// existing level-1 sheet: three right-moving frames followed by three upward-moving
/// frames. Left and down are still produced by mirroring the right/up animations.
/// </remarks>
public static class EnemyLevelCatalog
{
    private static readonly EnemyLevelDefinition[] Levels =
    {
        new(1, "res://assets/sprites/enemies/enemy_level1.png"),
        new(2, "res://assets/sprites/enemies/enemy_level2.png"),
        new(3, "res://assets/sprites/enemies/enemy_level3.png"),
        new(4, "res://assets/sprites/enemies/enemy_level4.png"),
        new(5, "res://assets/sprites/enemies/enemy_level5.png"),
        new(6, "res://assets/sprites/enemies/enemy_level6.png"),
        new(7, "res://assets/sprites/enemies/enemy_level7.png"),
        new(8, "res://assets/sprites/enemies/enemy_level8.png"),
    };

    /// <summary>
    /// Returns the enemy definition for the requested level.
    /// </summary>
    /// <remarks>
    /// Levels beyond the currently extracted set reuse the level-8 graphics for now.
    /// This keeps the game playable while making the fallback explicit and easy to
    /// replace when more arcade data is confirmed.
    /// </remarks>
    public static EnemyLevelDefinition Get(int levelNumber)
    {
        if (levelNumber <= 1)
            return Levels[0];

        if (levelNumber >= Levels.Length)
            return Levels[Levels.Length - 1];

        return Levels[levelNumber - 1];
    }
}
