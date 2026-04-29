namespace LadyBug.Gameplay.Enemies;

/// <summary>
/// One logical enemy-navigation cell.
/// </summary>
public sealed class EnemyNavigationCell
{
    /// <summary>
    /// Gets or sets the enemy directions currently legal from this logical cell.
    /// </summary>
    /// <remarks>
    /// This is the high-level equivalent of the arcade map's allowed-direction nibble.
    /// Static maze walls and the current rotating-gate states both contribute to it.
    /// </remarks>
    public MonsterDir AllowedDirections { get; set; }

    /// <summary>
    /// Gets or sets the BFS guidance direction that leads from this cell toward Lady Bug.
    /// </summary>
    /// <remarks>
    /// The value is a direction to take from this cell, not a distance. It remains
    /// <see cref="MonsterDir.None"/> when the player cell is unreachable.
    /// </remarks>
    public MonsterDir BfsDirection { get; set; }
}
