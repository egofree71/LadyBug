using Godot;
using LadyBug.Gameplay;

namespace LadyBug.Gameplay.Enemies;

/// <summary>
/// Implements one-pixel enemy movement, center decisions, fallback, and reversal.
/// </summary>
public sealed class EnemyMovementAi
{
    // Candidate order used when the preferred direction is rejected at a decision center.
    private static readonly MonsterDir[] FallbackOrder =
    {
        MonsterDir.Left,
        MonsterDir.Up,
        MonsterDir.Right,
        MonsterDir.Down
    };

    // Owning level used for arcade-pixel collision checks through the shared playfield resolver.
    private readonly Level _level;

    /// <summary>
    /// Creates the movement AI using the owning level for coordinate and collision checks.
    /// </summary>
    /// <param name="level">Active level that owns maze/gate movement evaluation.</param>
    public EnemyMovementAi(Level level)
    {
        _level = level;
    }

    /// <summary>
    /// Advances one active enemy by one arcade pixel when possible.
    /// </summary>
    /// <param name="monster">Enemy slot to update.</param>
    /// <param name="navigationGrid">Current enemy navigation map.</param>
    /// <remarks>
    /// Direction changes normally occur only at enemy decision centers. Outside a
    /// decision center, the enemy continues straight unless a gate-related block
    /// forces an immediate reversal.
    /// </remarks>
    public void UpdateMonsterOnePixel(MonsterEntity monster, EnemyNavigationGrid navigationGrid)
    {
        if (!monster.MovementActive)
            return;

        MonsterDir dir = monster.Direction;
        bool atDecisionCenter = EnemyMovementTuning.IsDecisionCenter(monster.ArcadePixelPos);

        if (atDecisionCenter)
        {
            dir = ChooseDirectionAtDecisionCenter(monster, navigationGrid);
        }
        else if (ShouldForceReverseBecauseOfDoor(monster, dir))
        {
            dir = dir.Opposite();
        }

        if (dir == MonsterDir.None)
            return;

        PlayfieldStepResult step = EvaluateStep(monster, dir);

        if (!step.Allowed)
        {
            MonsterDir opposite = dir.Opposite();

            if (opposite != MonsterDir.None && EvaluateStep(monster, opposite).Allowed)
                dir = opposite;
            else
                return;
        }

        monster.Direction = dir;
        monster.ArcadePixelPos += dir.ToVector();
    }

    /// <summary>
    /// Chooses the direction to use when an enemy reaches an arcade decision center.
    /// </summary>
    private MonsterDir ChooseDirectionAtDecisionCenter(
        MonsterEntity monster,
        EnemyNavigationGrid navigationGrid)
    {
        if (CanUseDirection(monster, monster.PreferredDirection, navigationGrid))
            return monster.PreferredDirection;

        MonsterDir rejectedMask = monster.PreferredDirection;

        foreach (MonsterDir candidate in FallbackOrder)
        {
            if ((rejectedMask & candidate) != 0)
                continue;

            if (CanUseDirection(monster, candidate, navigationGrid))
                return candidate;
        }

        if (CanUseDirection(monster, monster.Direction, navigationGrid))
            return monster.Direction;

        MonsterDir opposite = monster.Direction.Opposite();
        if (CanUseDirection(monster, opposite, navigationGrid))
            return opposite;

        return MonsterDir.None;
    }

    /// <summary>
    /// Validates a candidate direction against both navigation and pixel-step collision.
    /// </summary>
    private bool CanUseDirection(
        MonsterEntity monster,
        MonsterDir dir,
        EnemyNavigationGrid navigationGrid)
    {
        if (dir == MonsterDir.None)
            return false;

        Vector2I cell = _level.ArcadePixelToLogicalCell(monster.ArcadePixelPos);
        if (!navigationGrid.IsDirectionAllowed(cell, dir))
            return false;

        return EvaluateStep(monster, dir).Allowed;
    }

    /// <summary>
    /// Returns whether a gate block should cause an immediate reversal between centers.
    /// </summary>
    private bool ShouldForceReverseBecauseOfDoor(MonsterEntity monster, MonsterDir dir)
    {
        if (dir == MonsterDir.None)
            return false;

        PlayfieldStepResult step = EvaluateStep(monster, dir);

        return step.Kind == PlayfieldStepKind.BlockedByGate;
    }

    /// <summary>
    /// Evaluates a one-pixel enemy step through the current level collision resolver.
    /// </summary>
    private PlayfieldStepResult EvaluateStep(MonsterEntity monster, MonsterDir dir)
    {
        return _level.EvaluateArcadePixelStepWithGates(
            monster.ArcadePixelPos,
            dir.ToVector(),
            EnemyMovementTuning.GetCollisionLead(dir));
    }
}
