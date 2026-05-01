using Godot;
using LadyBug.Gameplay;

namespace LadyBug.Gameplay.Enemies;

/// <summary>
/// Implements one-pixel enemy movement, center decisions, fallback, and reversal.
/// </summary>
public sealed class EnemyMovementAi
{
    // Arcade fallback order used by routine 0x4241-like behavior.
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
    /// Direction changes normally occur only at enemy decision centers. At centers,
    /// the preferred direction is validated through the logical navigation grid and
    /// then through the local playfield/gate layer. A local rejection feeds the same
    /// rejected-direction mask as a maze rejection, then fallback scans the arcade
    /// order 01, 02, 04, 08. Outside a decision center, only the gate-related forced
    /// reversal path is allowed to reverse the enemy immediately.
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
                out decisionReason,
                out blockKind);
        }
        else
        {
            PlayfieldStepResult currentStep = EvaluateStep(monster, chosenDir);
            if (currentStep.Kind == PlayfieldStepKind.BlockedByGate)
            {
                MonsterDir opposite = chosenDir.Opposite();

                if (opposite != MonsterDir.None)
                {
                    chosenDir = opposite;
                    forcedReverse = true;
                    decisionReason = "forced-reverse-by-gate";
                    blockKind = currentStep.Kind.ToString();
                }
            }
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
            // This should be rare after center validation. Do not silently turn it
            // into an additional opposite-direction heuristic; log and stop instead.
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
                $"{decisionReason}+validated-step-blocked",
                blockKind);
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
        out string decisionReason,
        out string blockKind)
    {
        rejectedMask = MonsterDir.None;
        fallbackUsed = false;
        blockKind = "none";

        MonsterDirectionValidation preferredValidation = ValidateCandidateDirection(
            monster,
            monster.PreferredDirection,
            navigationGrid);

        if (preferredValidation.Accepted)
        {
            decisionReason = "preferred-accepted";
            return monster.PreferredDirection;
        }

        if (IsSingleDirection(monster.PreferredDirection))
            rejectedMask |= monster.PreferredDirection;

        decisionReason = $"preferred-rejected-{FormatRejectReason(preferredValidation.RejectReason)}";
        blockKind = preferredValidation.BlockKind;

        foreach (MonsterDir candidate in FallbackOrder)
        {
            if ((rejectedMask & candidate) != 0)
                continue;

            MonsterDirectionValidation candidateValidation = ValidateCandidateDirection(
                monster,
                candidate,
                navigationGrid);

            if (candidateValidation.Accepted)
            {
                fallbackUsed = true;
                decisionReason = $"fallback-accepted-after-{FormatRejectReason(preferredValidation.RejectReason)}";
                return candidate;
            }

            rejectedMask |= candidate;

            // Keep the first meaningful block kind for debug output. This usually
            // corresponds to the rejected preferred direction, which is the most
            // useful comparison point against MAME traces.
            if (blockKind == "none" && candidateValidation.BlockKind != "none")
                blockKind = candidateValidation.BlockKind;
        }

        decisionReason = "no-valid-direction-after-fallback";
        return MonsterDir.None;
    }

    /// <summary>
    /// Validates a candidate direction against both navigation and local playfield geometry.
    /// </summary>
    private MonsterDirectionValidation ValidateCandidateDirection(
        MonsterEntity monster,
        MonsterDir dir,
        EnemyNavigationGrid navigationGrid)
    {
        if (!IsSingleDirection(dir))
        {
            return new MonsterDirectionValidation(
                false,
                MonsterDirectionRejectReason.InvalidDirection,
                PlayfieldStepKind.Allowed,
                "none");
        }

        Vector2I cell = _level.ArcadePixelToLogicalCell(monster.ArcadePixelPos);
        if (!navigationGrid.IsDirectionAllowed(cell, dir))
        {
            return new MonsterDirectionValidation(
                false,
                MonsterDirectionRejectReason.StaticMazeBlocked,
                PlayfieldStepKind.BlockedByFixedWall,
                "navigation-grid");
        }

        PlayfieldStepResult step = EvaluateStep(monster, dir);
        if (!step.Allowed)
        {
            MonsterDirectionRejectReason reason = step.Kind == PlayfieldStepKind.BlockedByGate
                ? MonsterDirectionRejectReason.LocalDoorBlocked
                : MonsterDirectionRejectReason.LocalPlayfieldBlocked;

            return new MonsterDirectionValidation(
                false,
                reason,
                step.Kind,
                step.Kind.ToString());
        }

        return new MonsterDirectionValidation(
            true,
            MonsterDirectionRejectReason.None,
            PlayfieldStepKind.Allowed,
            "none");
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

    /// <summary>
    /// Returns whether the value is exactly one arcade enemy direction bit.
    /// </summary>
    private static bool IsSingleDirection(MonsterDir dir)
    {
        return dir == MonsterDir.Left ||
               dir == MonsterDir.Up ||
               dir == MonsterDir.Right ||
               dir == MonsterDir.Down;
    }

    /// <summary>
    /// Formats reject reasons as compact debug labels.
    /// </summary>
    private static string FormatRejectReason(MonsterDirectionRejectReason reason)
    {
        return reason switch
        {
            MonsterDirectionRejectReason.None => "none",
            MonsterDirectionRejectReason.InvalidDirection => "invalid-dir",
            MonsterDirectionRejectReason.StaticMazeBlocked => "static-maze",
            MonsterDirectionRejectReason.LocalDoorBlocked => "local-door",
            MonsterDirectionRejectReason.LocalPlayfieldBlocked => "local-playfield",
            _ => "unknown"
        };
    }
}

/// <summary>
/// Reason why one enemy movement candidate was rejected.
/// </summary>
public enum MonsterDirectionRejectReason
{
    /// <summary>
    /// The candidate was accepted.
    /// </summary>
    None,

    /// <summary>
    /// The candidate was not exactly one arcade enemy direction bit.
    /// </summary>
    InvalidDirection,

    /// <summary>
    /// The logical enemy navigation cell does not allow this direction.
    /// </summary>
    StaticMazeBlocked,

    /// <summary>
    /// The logical cell allows the direction, but a local gate/door blocks the step.
    /// </summary>
    LocalDoorBlocked,

    /// <summary>
    /// The logical cell allows the direction, but the final local playfield probe is blocked.
    /// </summary>
    LocalPlayfieldBlocked
}

/// <summary>
/// Result of validating one enemy movement candidate.
/// </summary>
public readonly struct MonsterDirectionValidation
{
    /// <summary>
    /// Creates one validation result.
    /// </summary>
    public MonsterDirectionValidation(
        bool accepted,
        MonsterDirectionRejectReason rejectReason,
        PlayfieldStepKind stepKind,
        string blockKind)
    {
        Accepted = accepted;
        RejectReason = rejectReason;
        StepKind = stepKind;
        BlockKind = blockKind;
    }

    /// <summary>
    /// Whether the movement candidate is usable.
    /// </summary>
    public bool Accepted { get; }

    /// <summary>
    /// High-level rejection reason.
    /// </summary>
    public MonsterDirectionRejectReason RejectReason { get; }

    /// <summary>
    /// Underlying playfield step kind when available.
    /// </summary>
    public PlayfieldStepKind StepKind { get; }

    /// <summary>
    /// Compact debug string for the playfield block kind.
    /// </summary>
    public string BlockKind { get; }
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
