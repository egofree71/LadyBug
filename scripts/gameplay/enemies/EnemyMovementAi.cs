using Godot;
using LadyBug.Gameplay;

namespace LadyBug.Gameplay.Enemies;

/// <summary>
/// Implements one-pixel enemy movement, center decisions, fallback, and reversal.
/// </summary>
public sealed class EnemyMovementAi
{
    // Candidate order used when the preferred/current directions are rejected at a decision center.
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
    /// decision center, the enemy keeps its current direction and advances one pixel.
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
        else
        {
            // Simulator comparison refinement: outside a decision center, the
            // arcade-like default is to preserve the current direction and keep
            // moving one pixel. Do not reverse merely because the high-level
            // playfield resolver reports a nearby gate or gate boundary.
            //
            // A future arcade-accurate door reversal should be reintroduced only
            // from a precise local tile/gate probe, not from the broad resolver.
            decisionReason = "outside-center-keep-current";
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

        // At a decision center, the selected direction has to pass the normal
        // candidate validation. Outside a center, the simulator comparison says
        // the enemy should not ask the broad gate/boundary resolver whether it
        // may keep moving; it simply keeps its current direction and advances.
        if (atDecisionCenter)
        {
            PlayfieldStepResult step = EvaluateStep(monster, chosenDir);
            if (!step.Allowed)
            {
                stepBlocked = true;
                blockKind = step.Kind.ToString();

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

        MonsterDir preferred = monster.PreferredDirection;
        MonsterDir current = monster.Direction;

        bool preferredIsImmediateReverse = preferred != MonsterDir.None
            && current != MonsterDir.None
            && preferred == current.Opposite();

        // Normal case: the preferred direction is accepted first, as long as it
        // does not immediately reverse the direction committed on the previous
        // ticks. Immediate preferred reversals are deferred below to avoid a
        // two-cell ping-pong in cul-de-sacs.
        if (!preferredIsImmediateReverse && CanUseDirection(monster, preferred, navigationGrid))
        {
            decisionReason = "preferred-accepted";
            return preferred;
        }

        // A preferred direction that immediately reverses the current direction
        // remains valid, but it is not accepted as the first choice. This lets an
        // enemy keep leaving a cul-de-sac instead of turning back into it at the
        // next decision center. If no non-reversing direction works, the reverse
        // is still accepted as a last resort so real dead ends remain escapable.
        bool deferredPreferredReverse = preferredIsImmediateReverse
            && CanUseDirection(monster, preferred, navigationGrid);

        if (preferred != MonsterDir.None && !deferredPreferredReverse)
            rejectedMask |= preferred;

        // Simulator refinement: a rejected preferred direction does not send the
        // enemy directly to the fixed-order fallback. The arcade-like behavior is
        // to try preserving the direction that was already committed on the enemy.
        if (current != MonsterDir.None && (rejectedMask & current) == 0)
        {
            if (CanUseDirection(monster, current, navigationGrid))
            {
                decisionReason = deferredPreferredReverse
                    ? "preferred-reverse-deferred-current-kept"
                    : "preferred-rejected-current-kept";
                return current;
            }

            rejectedMask |= current;
        }
        else if (current != MonsterDir.None)
        {
            // Preferred and current are the same blocked direction.
            rejectedMask |= current;
        }

        // Only preferred/current rejections are carried into fallback. The fallback
        // search then owns a local scan mask so failed fallback candidates do not
        // become persistent enemy state.
        MonsterDir fallbackRejectedMask = rejectedMask;
        if (deferredPreferredReverse)
        {
            // First scan for a non-reversing fallback. The deferred reverse is
            // allowed afterwards only if this center has no other usable exit.
            fallbackRejectedMask |= preferred;
        }

        MonsterDir fallback = FindFallbackDirection(
            monster,
            navigationGrid,
            fallbackRejectedMask,
            out MonsterDir debugRejectedMask);

        // Keep the final local scan mask only for the debug snapshot returned by
        // UpdateMonsterOnePixel; it is not written back to MonsterEntity.
        rejectedMask = debugRejectedMask;

        if (fallback != MonsterDir.None)
        {
            fallbackUsed = true;
            decisionReason = deferredPreferredReverse
                ? "preferred-reverse-deferred-fallback-accepted"
                : "fallback-accepted";
            return fallback;
        }

        if (deferredPreferredReverse)
        {
            decisionReason = "preferred-reverse-last-resort";
            return preferred;
        }

        // Safety path only: a valid maze cell should almost always have at least
        // one usable direction. Keeping the current direction here preserves the
        // old robustness behavior, but the debug reason makes it visible.
        decisionReason = "no-fallback-keep-current";
        return current;
    }

    /// <summary>
    /// Scans the arcade fallback order with a local rejected-direction mask.
    /// </summary>
    private MonsterDir FindFallbackDirection(
        MonsterEntity monster,
        EnemyNavigationGrid navigationGrid,
        MonsterDir alreadyRejected,
        out MonsterDir debugRejectedMask)
    {
        // Local mask for this single decision. It starts with preferred/current
        // rejections, then grows only while scanning the fallback order.
        MonsterDir scanRejected = alreadyRejected;

        foreach (MonsterDir candidate in FallbackOrder)
        {
            if ((scanRejected & candidate) != 0)
                continue;

            if (CanUseDirection(monster, candidate, navigationGrid))
            {
                debugRejectedMask = scanRejected;
                return candidate;
            }

            // Local to this decision only. This is not stored on MonsterEntity.
            scanRejected |= candidate;
        }

        debugRejectedMask = scanRejected;
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

    // Intentionally no broad outside-center gate reversal here. Earlier versions
    // used EvaluateStep(...).Kind == BlockedByGate as a reversal trigger, but the
    // simulator comparison showed that this is too coarse: gate proximity and
    // boundary probes can look blocked even when the arcade keeps moving.

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
    /// Whether a fallback candidate was used instead of the preferred/current direction.
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
