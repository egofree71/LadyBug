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
    /// <returns>A compact debug result describing the movement decision.</returns>
    /// <remarks>
    /// Direction changes normally occur only at enemy decision centers. Outside a
    /// decision center, the enemy continues straight unless a gate-related block
    /// forces an immediate reversal.
    /// </remarks>
    public EnemyMovementDebugResult UpdateMonsterOnePixel(
        MonsterEntity monster,
        EnemyNavigationGrid navigationGrid)
    {
        Vector2I beforePos = monster.ArcadePixelPos;
        Vector2I cell = _level.ArcadePixelToLogicalCell(beforePos);
        MonsterDir currentDirBefore = monster.Direction;
        MonsterDir preferredDir = monster.PreferredDirection;
        MonsterDir bfsDir = navigationGrid.GetBfsDirection(cell);
        MonsterDir allowedDirections = navigationGrid.IsInside(cell)
            ? navigationGrid.GetCell(cell).AllowedDirections
            : MonsterDir.None;

        bool atDecisionCenter = EnemyMovementTuning.IsDecisionCenter(beforePos);
        bool forcedReverse = false;
        bool fallbackUsed = false;
        bool stepBlocked = false;
        bool moved = false;
        MonsterDir rejectedMask = MonsterDir.None;
        MonsterDir chosenDir = currentDirBefore;
        string decisionReason = atDecisionCenter ? "decision-center" : "straight";
        string blockKind = "none";

        if (!monster.MovementActive)
        {
            return new EnemyMovementDebugResult(
                false,
                monster.Id,
                beforePos,
                monster.ArcadePixelPos,
                cell,
                currentDirBefore,
                preferredDir,
                bfsDir,
                allowedDirections,
                rejectedMask,
                MonsterDir.None,
                atDecisionCenter,
                false,
                false,
                false,
                false,
                "inactive",
                "none");
        }

        if (atDecisionCenter)
        {
            chosenDir = ChooseDirectionAtDecisionCenter(
                monster,
                navigationGrid,
                out rejectedMask,
                out fallbackUsed,
                out decisionReason);
        }
        else if (ShouldForceReverseBecauseOfDoor(monster, chosenDir))
        {
            chosenDir = chosenDir.Opposite();
            forcedReverse = true;
            decisionReason = "forced-reverse-by-gate";
        }

        if (chosenDir == MonsterDir.None)
        {
            return new EnemyMovementDebugResult(
                atDecisionCenter || forcedReverse,
                monster.Id,
                beforePos,
                monster.ArcadePixelPos,
                cell,
                currentDirBefore,
                preferredDir,
                bfsDir,
                allowedDirections,
                rejectedMask,
                chosenDir,
                atDecisionCenter,
                fallbackUsed,
                forcedReverse,
                false,
                false,
                decisionReason,
                blockKind);
        }

        PlayfieldStepResult step = EvaluateStep(monster, chosenDir);

        if (!step.Allowed)
        {
            stepBlocked = true;
            blockKind = step.Kind.ToString();
            MonsterDir opposite = chosenDir.Opposite();

            if (opposite != MonsterDir.None && EvaluateStep(monster, opposite).Allowed)
            {
                chosenDir = opposite;
                forcedReverse = true;
                decisionReason = atDecisionCenter
                    ? $"{decisionReason}+blocked-opposite"
                    : "blocked-opposite";
            }
            else
            {
                return new EnemyMovementDebugResult(
                    true,
                    monster.Id,
                    beforePos,
                    monster.ArcadePixelPos,
                    cell,
                    currentDirBefore,
                    preferredDir,
                    bfsDir,
                    allowedDirections,
                    rejectedMask,
                    chosenDir,
                    atDecisionCenter,
                    fallbackUsed,
                    forcedReverse,
                    true,
                    false,
                    decisionReason,
                    blockKind);
            }
        }

        monster.Direction = chosenDir;
        monster.ArcadePixelPos += chosenDir.ToVector();
        moved = monster.ArcadePixelPos != beforePos;

        return new EnemyMovementDebugResult(
            atDecisionCenter || forcedReverse || stepBlocked,
            monster.Id,
            beforePos,
            monster.ArcadePixelPos,
            cell,
            currentDirBefore,
            preferredDir,
            bfsDir,
            allowedDirections,
            rejectedMask,
            chosenDir,
            atDecisionCenter,
            fallbackUsed,
            forcedReverse,
            stepBlocked,
            moved,
            decisionReason,
            blockKind);
    }

    /// <summary>
    /// Chooses the direction to use when an enemy reaches an arcade decision center.
    /// </summary>
    private MonsterDir ChooseDirectionAtDecisionCenter(
        MonsterEntity monster,
        EnemyNavigationGrid navigationGrid,
        out MonsterDir rejectedMask,
        out bool fallbackUsed,
        out string decisionReason)
    {
        rejectedMask = MonsterDir.None;
        fallbackUsed = false;
        decisionReason = "preferred-accepted";

        if (CanUseDirection(monster, monster.PreferredDirection, navigationGrid))
            return monster.PreferredDirection;

        rejectedMask = monster.PreferredDirection;
        decisionReason = "preferred-rejected";

        foreach (MonsterDir candidate in FallbackOrder)
        {
            if ((rejectedMask & candidate) != 0)
                continue;

            if (!CanUseDirection(monster, candidate, navigationGrid))
            {
                rejectedMask |= candidate;
                continue;
            }

            fallbackUsed = true;
            decisionReason = "fallback-accepted";
            return candidate;
        }

        if (CanUseDirection(monster, monster.Direction, navigationGrid))
        {
            decisionReason = "continue-current-after-fallback";
            return monster.Direction;
        }

        MonsterDir opposite = monster.Direction.Opposite();
        if (CanUseDirection(monster, opposite, navigationGrid))
        {
            decisionReason = "opposite-after-fallback";
            return opposite;
        }

        decisionReason = "no-valid-direction";
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

/// <summary>
/// Debug snapshot for one enemy movement update.
/// </summary>
public readonly struct EnemyMovementDebugResult
{
    /// <summary>
    /// Creates one debug snapshot for a movement AI step.
    /// </summary>
    public EnemyMovementDebugResult(
        bool shouldLog,
        int enemyId,
        Vector2I beforeArcadePixelPos,
        Vector2I afterArcadePixelPos,
        Vector2I logicalCell,
        MonsterDir currentDirBefore,
        MonsterDir preferredDir,
        MonsterDir bfsDir,
        MonsterDir allowedDirections,
        MonsterDir rejectedMask,
        MonsterDir chosenDir,
        bool atDecisionCenter,
        bool fallbackUsed,
        bool forcedReverse,
        bool stepBlocked,
        bool moved,
        string decisionReason,
        string blockKind)
    {
        ShouldLog = shouldLog;
        EnemyId = enemyId;
        BeforeArcadePixelPos = beforeArcadePixelPos;
        AfterArcadePixelPos = afterArcadePixelPos;
        LogicalCell = logicalCell;
        CurrentDirBefore = currentDirBefore;
        PreferredDir = preferredDir;
        BfsDir = bfsDir;
        AllowedDirections = allowedDirections;
        RejectedMask = rejectedMask;
        ChosenDir = chosenDir;
        AtDecisionCenter = atDecisionCenter;
        FallbackUsed = fallbackUsed;
        ForcedReverse = forcedReverse;
        StepBlocked = stepBlocked;
        Moved = moved;
        DecisionReason = decisionReason;
        BlockKind = blockKind;
    }

    /// <summary>
    /// Whether this result is useful enough to print in the debug log.
    /// </summary>
    public bool ShouldLog { get; }

    /// <summary>
    /// Enemy slot id.
    /// </summary>
    public int EnemyId { get; }

    /// <summary>
    /// Enemy position before the movement update.
    /// </summary>
    public Vector2I BeforeArcadePixelPos { get; }

    /// <summary>
    /// Enemy position after the movement update.
    /// </summary>
    public Vector2I AfterArcadePixelPos { get; }

    /// <summary>
    /// Logical cell occupied before the movement update.
    /// </summary>
    public Vector2I LogicalCell { get; }

    /// <summary>
    /// Direction committed before this update.
    /// </summary>
    public MonsterDir CurrentDirBefore { get; }

    /// <summary>
    /// Preferred direction seen by the movement AI.
    /// </summary>
    public MonsterDir PreferredDir { get; }

    /// <summary>
    /// BFS direction stored in the current logical cell.
    /// </summary>
    public MonsterDir BfsDir { get; }

    /// <summary>
    /// Directions allowed by the enemy navigation grid in the current cell.
    /// </summary>
    public MonsterDir AllowedDirections { get; }

    /// <summary>
    /// Directions rejected while trying to choose a direction.
    /// </summary>
    public MonsterDir RejectedMask { get; }

    /// <summary>
    /// Final direction selected for this update.
    /// </summary>
    public MonsterDir ChosenDir { get; }

    /// <summary>
    /// Whether the enemy was at a decision center before the update.
    /// </summary>
    public bool AtDecisionCenter { get; }

    /// <summary>
    /// Whether a fallback candidate was used instead of the preferred direction.
    /// </summary>
    public bool FallbackUsed { get; }

    /// <summary>
    /// Whether this update reversed the enemy direction.
    /// </summary>
    public bool ForcedReverse { get; }

    /// <summary>
    /// Whether the initially selected step was blocked by the playfield resolver.
    /// </summary>
    public bool StepBlocked { get; }

    /// <summary>
    /// Whether the enemy position changed this update.
    /// </summary>
    public bool Moved { get; }

    /// <summary>
    /// Compact reason label for the chosen direction.
    /// </summary>
    public string DecisionReason { get; }

    /// <summary>
    /// Playfield block kind for a blocked step, or "none".
    /// </summary>
    public string BlockKind { get; }
}
