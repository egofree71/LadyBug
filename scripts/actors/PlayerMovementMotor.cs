using System;
using System.Collections.Generic;
using Godot;
using LadyBug.Gameplay;
using LadyBug.Gameplay.Gates;
using LadyBug.Gameplay.Maze;

namespace LadyBug.Actors;

/// <summary>
/// Executes the player's arcade-style movement simulation.
/// </summary>
/// <remarks>
/// The motor owns only gameplay movement state: arcade-pixel position,
/// effective movement direction, requested turn direction, and the temporary
/// alignment state used during arcade-style turns.
///
/// Input collection, sprite animation, collectible handling, and scene-space
/// rendering remain handled by <see cref="PlayerController"/>.
///
/// Design notes:
/// - movement advances by integer arcade pixels;
/// - short key taps preserve the last effective direction, like the arcade game;
/// - turns are resolved through reverse-engineered turn windows, isolated in
///   <see cref="PlayerTurnWindowResolver"/>;
/// - assisted turns can combine one orthogonal correction pixel with one pixel
///   in the requested direction;
/// - all committed movement components still pass through the current
///   Level/Maze/Gate collision layer.
/// </remarks>
public sealed class PlayerMovementMotor
{
    private Level _level = null!;

    // True gameplay position, expressed in original arcade pixels relative to the maze origin.
    private Vector2I _arcadePixelPos = Vector2I.Zero;

    // Last effective movement direction. It is intentionally preserved when
    // no input is held, because short taps still need the previous movement context.
    private Vector2I _currentDir = Vector2I.Zero;

    // Direction used by PlayerController to choose the sprite render offset.
    private Vector2I _offsetDir = Vector2I.Up;

    // Last direction accepted by the internal request latch. A changed request
    // usually costs one tick before movement in that direction begins.
    private Vector2I _latchedRequestedDir = Vector2I.Zero;

    // Target lane used while resolving an assisted turn.
    private Vector2I _turnLaneTarget = Vector2I.Zero;

    // Which orthogonal axis may be corrected during a turn.
    private PlayerTurnAssistFlags _turnAssistFlags = PlayerTurnAssistFlags.None;

    // True while a turn is being resolved by the assisted path.
    private bool _assistedTurnActive;

    // Prevents an immediate same-axis reversal during an assisted turn from
    // applying two correction steps in the same logical transition.
    private Vector2I _deferredSameAxisAssistDir = Vector2I.Zero;

    // Ordered list of real one-pixel movement segments completed during the
    // current tick. Assisted turns can produce two segments: one alignment
    // correction and one step in the requested direction.
    private readonly List<PlayerMovementSegment> _movementSegmentsThisTick = new(2);

    private readonly PlayerMovementDebugTrace _debugTrace = new();

    // Turn-window maps generated from the logical maze loaded from data/maze.json.
    private PlayerTurnWindowMaps _turnWindowMaps = null!;

    /// <summary>
    /// Gets the current gameplay position in arcade pixels.
    /// </summary>
    public Vector2I ArcadePixelPos => _arcadePixelPos;

    /// <summary>
    /// Gets the current effective movement direction.
    /// </summary>
    public Vector2I CurrentDir => _currentDir;

    /// <summary>
    /// Gets the direction used to select the current sprite render offset.
    /// </summary>
    public Vector2I OffsetDir => _offsetDir;

    /// <summary>
    /// Initializes the movement motor from the owning level.
    /// </summary>
    public void Initialize(Level level)
    {
        _level = level;
        _turnWindowMaps = PlayerTurnWindowMaps.FromMazeGrid(level.MazeGrid);
        _arcadePixelPos = level.LogicalCellToArcadePixel(level.PlayerStartCell);
        _currentDir = Vector2I.Zero;
        _offsetDir = Vector2I.Up;
        _latchedRequestedDir = Vector2I.Zero;
        _turnLaneTarget = _arcadePixelPos;
        _turnAssistFlags = PlayerTurnAssistFlags.None;
        _assistedTurnActive = false;
        _deferredSameAxisAssistDir = Vector2I.Zero;
        _debugTrace.Reset();
    }

    /// <summary>
    /// Advances the movement simulation by exactly one fixed tick.
    /// </summary>
    /// <param name="wantedDir">Currently intended movement direction.</param>
    /// <returns>A structured result describing what changed during this tick.</returns>
    public PlayerMovementStepResult Step(Vector2I wantedDir)
    {
        Vector2I previousPixelPos = _arcadePixelPos;
        Vector2I previousDirection = _currentDir;
        Vector2I? snappedArcadePixelPos = null;

        _debugTrace.BeginTick();
        _movementSegmentsThisTick.Clear();

        if (wantedDir == Vector2I.Zero)
        {
            // Do not clear the movement context. This is important for tiny taps:
            // the next input must still be interpreted relative to the previous
            // effective direction.
            return FinishStep(previousPixelPos, previousDirection, snappedArcadePixelPos, wantedDir);
        }

        if (_currentDir == Vector2I.Zero)
        {
            Vector2I originalPixelPos = _arcadePixelPos;

            if (!TrySnapToRailForDirection(wantedDir))
            {
                _debugTrace.Note("start/resume refused: not close enough to requested rail");
                return FinishStep(previousPixelPos, previousDirection, snappedArcadePixelPos, wantedDir);
            }

            if (_arcadePixelPos != originalPixelPos)
                snappedArcadePixelPos = _arcadePixelPos;

            AdvanceStraightStep(wantedDir);
            return FinishStep(previousPixelPos, previousDirection, snappedArcadePixelPos, wantedDir);
        }

        AdvanceWithArcadeTurnRules(wantedDir);
        return FinishStep(previousPixelPos, previousDirection, snappedArcadePixelPos, wantedDir);
    }

    /// <summary>
    /// Routes a non-zero direction request through normal movement, turn-window
    /// selection, or the assisted-turn continuation path.
    /// </summary>
    private void AdvanceWithArcadeTurnRules(Vector2I requested)
    {
        if (requested == _currentDir)
        {
            bool keepAssistedDiagonal =
                _assistedTurnActive &&
                OrthogonalAxisNotAligned(requested);

            if (keepAssistedDiagonal)
            {
                _debugTrace.AppendPath("AssistedSameDirection");
                AdvanceAssistedTurnStep(requested, clearAssistFlags: false);
                _debugTrace.Note("same direction, but assisted orthogonal alignment is still active");
            }
            else
            {
                AdvanceStraightStep(requested);
            }

            return;
        }

        if (_assistedTurnActive)
        {
            ContinueAssistedTurn(requested);
            return;
        }

        _turnAssistFlags = PlayerTurnAssistFlags.None;

        PlayerTurnWindowDecision decision = PlayerTurnWindowResolver.Choose(
            _turnWindowMaps,
            _arcadePixelPos,
            requested,
            _currentDir,
            _turnLaneTarget);

        if (decision.LaneTarget != Vector2I.Zero)
            _turnLaneTarget = decision.LaneTarget;

        _turnAssistFlags = decision.AssistFlags;
        _debugTrace.NoteTurnDecision(decision, _turnLaneTarget);

        switch (decision.Path)
        {
            case PlayerTurnPath.Normal:
                AdvanceViaRequestLatch(requested);
                break;

            case PlayerTurnPath.Assisted:
                ContinueAssistedTurn(requested);
                break;

            case PlayerTurnPath.CloseRangeAssistThenNormal:
                AdvanceCloseRangeAssistThenNormal(requested);
                break;
        }
    }

    /// <summary>
    /// Handles the ordinary movement path. A newly requested direction is first
    /// latched; on a later tick the motor either moves straight or applies a
    /// stored alignment correction before the turn can complete.
    /// </summary>
    private void AdvanceViaRequestLatch(Vector2I requested)
    {
        _debugTrace.AppendPath("RequestLatch");
        _assistedTurnActive = false;
        _deferredSameAxisAssistDir = Vector2I.Zero;

        if (_latchedRequestedDir != requested)
        {
            _latchedRequestedDir = requested;
            _debugTrace.Note("request latched; no movement this tick");
            return;
        }

        bool requestContinuesCurrentAxis =
            (IsHorizontal(requested) && IsHorizontal(_currentDir)) ||
            (IsVertical(requested) && IsVertical(_currentDir));

        if (!requestContinuesCurrentAxis)
        {
            ApplyStoredTurnAlignmentCorrection();
            return;
        }

        AdvanceStraightStep(requested);
    }

    /// <summary>
    /// Applies one orthogonal correction pixel toward the selected turn lane.
    /// The correction is skipped when the requested direction is blocked by a
    /// fixed wall from the target lane, but kept when the block is a pushable gate.
    /// </summary>
    private void ApplyStoredTurnAlignmentCorrection()
    {
        _debugTrace.AppendPath("AlignmentCorrection");

        if (_turnAssistFlags == PlayerTurnAssistFlags.None)
            return;

        if (!CanMoveInRequestedDirectionFromTurnLane(_latchedRequestedDir))
        {
            _debugTrace.Note("correction refused: requested direction is blocked from turn lane");
            return;
        }

        if ((_turnAssistFlags & PlayerTurnAssistFlags.CorrectY) != 0)
        {
            Vector2I correction = StepTowardY(_turnLaneTarget.Y);
            if (correction != Vector2I.Zero)
            {
                TryAdvanceOnePixel(correction, updateCurrentDirection: false);
                _debugTrace.Note("corrected Y toward target lane");
            }

            return;
        }

        if ((_turnAssistFlags & PlayerTurnAssistFlags.CorrectX) != 0)
        {
            Vector2I correction = StepTowardX(_turnLaneTarget.X);
            if (correction != Vector2I.Zero)
            {
                TryAdvanceOnePixel(correction, updateCurrentDirection: false);
                _debugTrace.Note("corrected X toward target lane");
            }
        }
    }

    /// <summary>
    /// Starts with a small assisted correction when the actor is close enough to
    /// a turn lane, otherwise falls back to the ordinary request-latch path.
    /// </summary>
    private void AdvanceCloseRangeAssistThenNormal(Vector2I requested)
    {
        _debugTrace.AppendPath("CloseRangeAssist");

        if (IsVertical(requested))
        {
            int distanceToLane = Math.Abs(_turnLaneTarget.X - _arcadePixelPos.X);
            if (distanceToLane > 4)
            {
                AdvanceViaRequestLatch(requested);
            }
            else if (distanceToLane > 0)
            {
                _assistedTurnActive = true;
                AdvanceAssistedTurnStep(requested, clearAssistFlags: true);
            }
            else
            {
                AdvanceViaRequestLatch(requested);
            }

            return;
        }

        if (IsHorizontal(requested))
        {
            int distanceToLane = Math.Abs(_turnLaneTarget.Y - _arcadePixelPos.Y);
            if (distanceToLane > 4)
            {
                AdvanceViaRequestLatch(requested);
            }
            else if (distanceToLane > 0)
            {
                _assistedTurnActive = true;
                AdvanceAssistedTurnStep(requested, clearAssistFlags: true);
            }
            else
            {
                AdvanceViaRequestLatch(requested);
            }
        }
    }

    /// <summary>
    /// Continues an assisted turn. Until the turn lane is reached, the motor may
    /// combine an orthogonal correction with one pixel in the requested direction.
    /// </summary>
    private void ContinueAssistedTurn(Vector2I requested)
    {
        _debugTrace.AppendPath("AssistedTurn");
        _assistedTurnActive = true;

        if (_latchedRequestedDir != requested)
        {
            bool sameAxisReversalDuringAssistedTurn =
                IsSameAxis(requested, _currentDir) &&
                OrthogonalAxisNotAligned(requested);

            if (sameAxisReversalDuringAssistedTurn && _deferredSameAxisAssistDir != requested)
            {
                _latchedRequestedDir = requested;
                _deferredSameAxisAssistDir = requested;
                _debugTrace.Note("same-axis reversal latched during assisted turn; movement deferred");
                return;
            }

            AdvanceViaRequestLatch(requested);
            return;
        }

        _deferredSameAxisAssistDir = Vector2I.Zero;

        if (_arcadePixelPos == _turnLaneTarget)
        {
            AdvanceStraightStep(requested);
            return;
        }

        if (IsVertical(requested) && _arcadePixelPos.X != _turnLaneTarget.X)
        {
            AdvanceAssistedTurnStep(requested, clearAssistFlags: false);
            return;
        }

        if (IsHorizontal(requested) && _arcadePixelPos.Y != _turnLaneTarget.Y)
        {
            AdvanceAssistedTurnStep(requested, clearAssistFlags: false);
            return;
        }

        FinishRemainingLaneCorrection();
    }

    /// <summary>
    /// Completes any remaining one-axis correction when an assisted turn is almost
    /// aligned but has not reached the exact target lane yet.
    /// </summary>
    private void FinishRemainingLaneCorrection()
    {
        if (_arcadePixelPos.X != _turnLaneTarget.X)
        {
            Vector2I correction = StepTowardX(_turnLaneTarget.X);
            if (correction != Vector2I.Zero)
                TryAdvanceOnePixel(correction, updateCurrentDirection: false);

            return;
        }

        if (_arcadePixelPos.Y != _turnLaneTarget.Y)
        {
            Vector2I correction = StepTowardY(_turnLaneTarget.Y);
            if (correction != Vector2I.Zero)
                TryAdvanceOnePixel(correction, updateCurrentDirection: false);
        }
    }

    /// <summary>
    /// Performs one assisted turn tick: first one correction pixel toward the
    /// target lane, then one pixel in the requested direction.
    /// </summary>
    private void AdvanceAssistedTurnStep(Vector2I requested, bool clearAssistFlags)
    {
        _debugTrace.AppendPath("AssistedStep");

        if (!CanMoveInRequestedDirectionFromTurnLane(requested))
        {
            _debugTrace.Note("assisted step refused: requested direction is blocked from turn lane");
            return;
        }

        Vector2I correction = GetCorrectionTowardTurnLane(requested);
        if (correction != Vector2I.Zero)
            TryAdvanceOnePixel(correction, updateCurrentDirection: false);

        if (TryAdvanceOnePixel(requested, updateCurrentDirection: true))
        {
            _latchedRequestedDir = requested;
            _offsetDir = requested;
        }

        if (clearAssistFlags)
            _turnAssistFlags = PlayerTurnAssistFlags.None;
    }

    /// <summary>
    /// Attempts a single straight pixel step in the requested direction.
    /// </summary>
    private void AdvanceStraightStep(Vector2I requested)
    {
        _debugTrace.AppendPath("StraightStep");

        if (TryAdvanceOnePixel(requested, updateCurrentDirection: true))
        {
            _latchedRequestedDir = requested;
            _offsetDir = requested;
        }
        else
        {
            // A blocked step must not erase the previous direction; otherwise
            // short tap-turns lose their context and become too strict.
            _debugTrace.Note("straight step blocked; current direction preserved");
        }
    }

    private Vector2I GetCorrectionTowardTurnLane(Vector2I requested)
    {
        if (IsVertical(requested))
            return StepTowardX(_turnLaneTarget.X);

        if (IsHorizontal(requested))
            return StepTowardY(_turnLaneTarget.Y);

        return Vector2I.Zero;
    }

    private PlayfieldStepResult ResolveGatePushIfNeeded(
        PlayfieldStepResult step,
        Vector2I direction)
    {
        if (step.Kind == PlayfieldStepKind.BlockedByGate &&
            step.GateId.HasValue &&
            step.ContactHalf.HasValue &&
            _level.TryPushGate(step.GateId.Value, direction, step.ContactHalf.Value))
        {
            return EvaluateOnePixelStep(direction);
        }

        return step;
    }

    private bool TryAdvanceOnePixel(Vector2I direction, bool updateCurrentDirection)
    {
        if (direction == Vector2I.Zero)
            return false;

        PlayfieldStepResult step = EvaluateOnePixelStep(direction);
        step = ResolveGatePushIfNeeded(step, direction);

        if (!step.Allowed)
        {
            _debugTrace.MarkBlocked(direction, step.Kind.ToString());
            return false;
        }

        Vector2I segmentStart = _arcadePixelPos;
        _arcadePixelPos += direction;
        _movementSegmentsThisTick.Add(new PlayerMovementSegment(
            segmentStart,
            _arcadePixelPos,
            direction));

        if (updateCurrentDirection)
            _currentDir = direction;

        return true;
    }

    /// <summary>
    /// Checks whether the requested direction can be used from the target turn
    /// lane before committing an orthogonal correction.
    /// </summary>
    /// <remarks>
    /// This prevents wall-only corrections from sliding the player sideways.
    /// Pushable gates are allowed, because the committed movement step will push
    /// the gate through <see cref="ResolveGatePushIfNeeded"/>.
    /// </remarks>
    private bool CanMoveInRequestedDirectionFromTurnLane(Vector2I requested)
    {
        if (requested == Vector2I.Zero)
            return false;

        Vector2I testPos = GetTurnLaneProbePosition(requested);
        PlayfieldStepResult step = EvaluateOnePixelStepAt(testPos, requested);

        if (step.Allowed)
            return true;

        return IsPushableGateBlock(step, requested);
    }

    private bool IsPushableGateBlock(PlayfieldStepResult step, Vector2I requested)
    {
        if (step.Kind != PlayfieldStepKind.BlockedByGate ||
            !step.GateId.HasValue ||
            !step.ContactHalf.HasValue)
        {
            return false;
        }

        if (!_level.GateSystem.TryGetGateById(step.GateId.Value, out RotatingGateRuntimeState gate))
            return false;

        return gate.CanBePushedBy(requested);
    }

    private Vector2I GetTurnLaneProbePosition(Vector2I requested)
    {
        if (IsVertical(requested))
            return new Vector2I(_turnLaneTarget.X, _arcadePixelPos.Y);

        if (IsHorizontal(requested))
            return new Vector2I(_arcadePixelPos.X, _turnLaneTarget.Y);

        return _arcadePixelPos;
    }

    private bool CanSnapToRailForDirection(Vector2I direction)
    {
        if (direction == Vector2I.Zero)
            return false;

        Vector2I currentCell = _level.ArcadePixelToLogicalCell(_arcadePixelPos);
        Vector2I anchor = _level.LogicalCellToArcadePixel(currentCell);

        if (direction.X != 0)
        {
            int deltaY = _arcadePixelPos.Y - anchor.Y;
            return Math.Abs(deltaY) <= PlayerMovementTuning.HorizontalRailSnapTolerance;
        }

        if (direction.Y != 0)
        {
            int deltaX = _arcadePixelPos.X - anchor.X;
            return Math.Abs(deltaX) <= PlayerMovementTuning.VerticalRailSnapTolerance;
        }

        return false;
    }

    private bool TrySnapToRailForDirection(Vector2I direction)
    {
        if (!CanSnapToRailForDirection(direction))
            return false;

        Vector2I currentCell = _level.ArcadePixelToLogicalCell(_arcadePixelPos);
        Vector2I anchor = _level.LogicalCellToArcadePixel(currentCell);

        if (direction.X != 0)
        {
            _arcadePixelPos = new Vector2I(_arcadePixelPos.X, anchor.Y);
            return true;
        }

        if (direction.Y != 0)
        {
            _arcadePixelPos = new Vector2I(anchor.X, _arcadePixelPos.Y);
            return true;
        }

        return false;
    }

    private Vector2I GetCollisionLead(Vector2I direction)
    {
        if (direction == Vector2I.Left)
            return new Vector2I(-PlayerMovementTuning.CollisionLeadLeft, 0);

        if (direction == Vector2I.Right)
            return new Vector2I(PlayerMovementTuning.CollisionLeadRight, 0);

        if (direction == Vector2I.Up)
            return new Vector2I(0, -PlayerMovementTuning.CollisionLeadUp);

        if (direction == Vector2I.Down)
            return new Vector2I(0, PlayerMovementTuning.CollisionLeadDown);

        return Vector2I.Zero;
    }

    private PlayfieldStepResult EvaluateOnePixelStep(Vector2I direction)
    {
        return EvaluateOnePixelStepAt(_arcadePixelPos, direction);
    }

    private PlayfieldStepResult EvaluateOnePixelStepAt(Vector2I arcadePixelPos, Vector2I direction)
    {
        if (direction == Vector2I.Zero)
        {
            MazeStepResult blockedMazeStep = new(false, Vector2I.Zero, Vector2I.Zero);
            return PlayfieldStepResult.BlockedByFixedWall(blockedMazeStep);
        }

        return _level.EvaluateArcadePixelStepWithGates(
            arcadePixelPos,
            direction,
            GetCollisionLead(direction));
    }

    private PlayerMovementStepResult FinishStep(
        Vector2I previousPixelPos,
        Vector2I previousDirection,
        Vector2I? snappedArcadePixelPos,
        Vector2I wantedDir)
    {
        PlayerMovementStepResult result = BuildStepResult(
            previousPixelPos,
            previousDirection,
            snappedArcadePixelPos);

        _debugTrace.EndTick(
            previousPixelPos,
            _arcadePixelPos,
            previousDirection,
            _currentDir,
            wantedDir,
            _latchedRequestedDir,
            _turnLaneTarget,
            _turnAssistFlags,
            _assistedTurnActive);

        return result;
    }

    private PlayerMovementStepResult BuildStepResult(
        Vector2I previousPixelPos,
        Vector2I previousDirection,
        Vector2I? snappedArcadePixelPos)
    {
        bool moved = _arcadePixelPos != previousPixelPos;
        bool directionChanged = _currentDir != previousDirection;
        bool stopped = previousDirection != Vector2I.Zero && _currentDir == Vector2I.Zero;

        PlayerMovementSegment[] movementSegments =
            _movementSegmentsThisTick.Count == 0
                ? Array.Empty<PlayerMovementSegment>()
                : _movementSegmentsThisTick.ToArray();

        return new PlayerMovementStepResult(
            moved,
            directionChanged,
            stopped,
            previousPixelPos,
            _arcadePixelPos,
            snappedArcadePixelPos,
            movementSegments,
            previousDirection,
            _currentDir,
            _offsetDir);
    }

    private bool OrthogonalAxisNotAligned(Vector2I requested)
    {
        if (IsVertical(requested))
            return _arcadePixelPos.X != _turnLaneTarget.X;

        if (IsHorizontal(requested))
            return _arcadePixelPos.Y != _turnLaneTarget.Y;

        return false;
    }

    private Vector2I StepTowardX(int targetX)
    {
        if (_arcadePixelPos.X < targetX)
            return Vector2I.Right;

        if (_arcadePixelPos.X > targetX)
            return Vector2I.Left;

        return Vector2I.Zero;
    }

    private Vector2I StepTowardY(int targetY)
    {
        if (_arcadePixelPos.Y < targetY)
            return Vector2I.Down;

        if (_arcadePixelPos.Y > targetY)
            return Vector2I.Up;

        return Vector2I.Zero;
    }

    private static bool IsHorizontal(Vector2I direction)
    {
        return direction.X != 0 && direction.Y == 0;
    }

    private static bool IsVertical(Vector2I direction)
    {
        return direction.Y != 0 && direction.X == 0;
    }

    private static bool IsSameAxis(Vector2I a, Vector2I b)
    {
        return (IsHorizontal(a) && IsHorizontal(b)) ||
               (IsVertical(a) && IsVertical(b));
    }
}
