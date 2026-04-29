using Godot;

namespace LadyBug.Gameplay.Enemies;

/// <summary>
/// Central tuning values for the first enemy implementation.
/// </summary>
public static class EnemyMovementTuning
{
    /// <summary>
    /// Maximum number of enemy slots used by one board.
    /// </summary>
    public const int MaxEnemyCount = 4;

    // Central lair position in the current Godot logical maze: logical cell (5, 5)
    // using the enemy decision-center Y anchor (low nibble 0x06).
    /// <summary>
    /// Arcade-pixel position of the central lair slot currently shown while waiting.
    /// </summary>
    public static readonly Vector2I LairArcadePixelPos = new(5 * 16 + 8, 5 * 16 + 6);

    // Enemies choose directions at the arcade decision center.
    /// <summary>
    /// Required low nibble for the enemy X coordinate at a decision center.
    /// </summary>
    public const int DecisionCenterXLowNibble = 0x08;

    /// <summary>
    /// Required low nibble for the enemy Y coordinate at a decision center.
    /// </summary>
    public const int DecisionCenterYLowNibble = 0x06;

    // Strict arcade collision window: abs(dx) < 9 and abs(dy) < 9.
    /// <summary>
    /// Strict collision window: player and enemy overlap when both deltas are below this value.
    /// </summary>
    public const int PlayerCollisionWindow = 9;

    // Render offset used while the enemy is moving horizontally.
    // Enemy gameplay centers use Y low nibble 0x06, one arcade pixel above the
    // player's collectible/render anchor. A +9 visual offset aligns the sprite with
    // the current maze corridors while preserving the arcade decision-center math.
    /// <summary>
    /// Scene render offset, in arcade pixels, used for left/right enemy sprites.
    /// </summary>
    public static readonly Vector2I SpriteRenderOffsetHorizontalArcade = new(5, 9);

    // Render offset used while the enemy is moving vertically.
    // Keep the same Y compensation as horizontal movement so the enemy does not
    // appear one arcade pixel too high when switching animation rows.
    /// <summary>
    /// Scene render offset, in arcade pixels, used for up/down enemy sprites.
    /// </summary>
    public static readonly Vector2I SpriteRenderOffsetVerticalArcade = new(5, 9);

    /// <summary>
    /// Gets the sprite render offset for the enemy's current facing direction.
    /// </summary>
    public static Vector2I GetSpriteRenderOffsetArcade(MonsterDir dir)
    {
        return dir switch
        {
            MonsterDir.Left => SpriteRenderOffsetHorizontalArcade,
            MonsterDir.Right => SpriteRenderOffsetHorizontalArcade,
            MonsterDir.Up => SpriteRenderOffsetVerticalArcade,
            MonsterDir.Down => SpriteRenderOffsetVerticalArcade,
            _ => SpriteRenderOffsetVerticalArcade
        };
    }

    // Enemy movement uses the anchor pixel itself as the movement probe.
    // Player-style forward probes (8/7 px) made enemies reverse one pixel before
    // their own decision centers, especially near the upper border.
    /// <summary>
    /// Forward probe distance used when an enemy moves left.
    /// </summary>
    public const int CollisionLeadLeft = 0;

    /// <summary>
    /// Forward probe distance used when an enemy moves right.
    /// </summary>
    public const int CollisionLeadRight = 0;

    /// <summary>
    /// Forward probe distance used when an enemy moves up.
    /// </summary>
    public const int CollisionLeadUp = 0;

    /// <summary>
    /// Forward probe distance used when an enemy moves down.
    /// </summary>
    public const int CollisionLeadDown = 0;

    /// <summary>
    /// Returns whether an arcade-pixel position is an enemy decision center.
    /// </summary>
    public static bool IsDecisionCenter(Vector2I arcadePixelPos)
    {
        return (arcadePixelPos.X & 0x0F) == DecisionCenterXLowNibble &&
               (arcadePixelPos.Y & 0x0F) == DecisionCenterYLowNibble;
    }

    /// <summary>
    /// Gets the forward collision probe offset for one enemy direction.
    /// </summary>
    public static Vector2I GetCollisionLead(MonsterDir dir)
    {
        return dir switch
        {
            MonsterDir.Left => new Vector2I(-CollisionLeadLeft, 0),
            MonsterDir.Right => new Vector2I(CollisionLeadRight, 0),
            MonsterDir.Up => new Vector2I(0, -CollisionLeadUp),
            MonsterDir.Down => new Vector2I(0, CollisionLeadDown),
            _ => Vector2I.Zero
        };
    }
}
