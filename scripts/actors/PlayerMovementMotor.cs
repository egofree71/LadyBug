using System;
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
/// - turns are resolved through a turn-window lookup instead of a simple
///   "snap to cell center" rule;
/// - assisted turns can combine one orthogonal correction pixel with one pixel
///   in the requested direction;
/// - all committed movement components still pass through the current
///   Level/Maze/Gate collision layer.
/// </remarks>
public sealed class PlayerMovementMotor
{
    private enum TurnPath
    {
        /// <summary>
        /// Use the ordinary request-latch and movement path.
        /// </summary>
        Normal,

        /// <summary>
        /// Enter or continue the assisted turn path.
        /// </summary>
        Assisted,

        /// <summary>
        /// Apply one close-range alignment assist before returning to the normal path.
        /// </summary>
        CloseRangeAssistThenNormal,
    }

    private readonly struct TurnWindowDecision
    {
        public TurnWindowDecision(
            TurnPath path,
            Vector2I laneTarget,
            int assistFlags,
            ushort turnWindowMask,
            int upperLaneCoordinate,
            int lowerLaneCoordinate)
        {
            Path = path;
            LaneTarget = laneTarget;
            AssistFlags = assistFlags;
            TurnWindowMask = turnWindowMask;
            UpperLaneCoordinate = upperLaneCoordinate;
            LowerLaneCoordinate = lowerLaneCoordinate;
        }

        public TurnPath Path { get; }
        public Vector2I LaneTarget { get; }
        public int AssistFlags { get; }
        public ushort TurnWindowMask { get; }
        public int UpperLaneCoordinate { get; }
        public int LowerLaneCoordinate { get; }
    }

    private const int TurnAssistCorrectY = 0x01;
    private const int TurnAssistCorrectX = 0x02;

    // The debug overlay currently displays Y in the same orientation as MAME.
    private const int MameYMirror = 0xDD;

    // Each bit describes a valid arcade turn lane for one row band.
    // These values come from the movement simulator produced during reverse engineering.
    private static readonly ushort[] HorizontalToVerticalTurnMasks =
    {
        0x0777, 0x03DE, 0x05FD, 0x03DE, 0x03FE,
        0x07AF, 0x05FD, 0x07DF, 0x07FF, 0x03DE, 0x07AF,
    };

    // Each bit describes a valid arcade turn lane for one column band.
    // These values come from the movement simulator produced during reverse engineering.
    private static readonly ushort[] VerticalToHorizontalTurnMasks =
    {
        0x05E5, 0x07FF, 0x07FF, 0x07FE, 0x03DF,
        0x0575, 0x03DF, 0x07FE, 0x07FF, 0x07BB, 0x05E5,
    };

    // Set to true temporarily when a console trace is needed.
    private const bool DebugMovementLog = false;

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

    // Bitfield describing which orthogonal axis may be corrected during a turn.
    private int _turnAssistFlags;

    // True while a turn is being resolved by the assisted path.
    private bool _assistedTurnActive;

    // Prevents an immediate same-axis reversal during an assisted turn from
    // applying two correction steps in the same logical transition.
    private Vector2I _deferredSameAxisAssistDir = Vector2I.Zero;

    private int _debugTickIndex;
    private string _debugPath = string.Empty;
    private string _debugNotes = string.Empty;
    private string _debugBlocked = string.Empty;

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
        _arcadePixelPos = level.LogicalCellToArcadePixel(level.PlayerStartCell);
        _currentDir = Vector2I.Zero;
        _offsetDir = Vector2I.Up;
        _latchedRequestedDir = Vector2I.Zero;
        _turnLaneTarget = _arcadePixelPos;
        _turnAssistFlags = 0;
        _assistedTurnActive = false;
        _deferredSameAxisAssistDir = Vector2I.Zero;
        _debugTickIndex = 0;
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

        BeginDebugTick();

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
                NoteDebug("start/resume refused: not close enough to requested rail");
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
    /// Routes a non-zero direction request through either normal movement,
    /// turn-window selection, or the assisted-turn continuation path.
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
                AppendDebugPath("AssistedSameDirection");
                AdvanceAssistedTurnStep(requested, clearAssistFlags: false);
                NoteDebug("same direction, but assisted orthogonal alignment is still active");
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

        _turnAssistFlags = 0;

        TurnWindowDecision decision = ChooseTurnPath(requested, _currentDir);
        _turnLaneTarget = decision.LaneTarget;
        _turnAssistFlags = decision.AssistFlags;

        NoteDebug(
            $"turnPath={decision.Path} target={FormatMamePos(_turnLaneTarget)} " +
            $"flags={_turnAssistFlags:X2} mask=0x{decision.TurnWindowMask:X4} " +
            $"upper={decision.UpperLaneCoordinate:X2} lower={decision.LowerLaneCoordinate:X2}");

        switch (decision.Path)
        {
            case TurnPath.Normal:
                AdvanceViaRequestLatch(requested);
                break;

            case TurnPath.Assisted:
                ContinueAssistedTurn(requested);
                break;

            case TurnPath.CloseRangeAssistThenNormal:
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
        AppendDebugPath("RequestLatch");
        _assistedTurnActive = false;
        _deferredSameAxisAssistDir = Vector2I.Zero;

        if (_latchedRequestedDir != requested)
        {
            _latchedRequestedDir = requested;
            NoteDebug("request latched; no movement this tick");
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
        AppendDebugPath("AlignmentCorrection");

        if (_turnAssistFlags == 0)
            return;

        if (!CanMoveInRequestedDirectionFromTurnLane(_latchedRequestedDir))
        {
            NoteDebug("correction refused: requested direction is blocked from turn lane");
            return;
        }

        if ((_turnAssistFlags & TurnAssistCorrectY) != 0)
        {
            Vector2I correction = StepTowardY(_turnLaneTarget.Y);
            if (correction != Vector2I.Zero)
            {
                TryAdvanceOnePixel(correction, updateCurrentDirection: false);
                NoteDebug("corrected Y toward target lane");
            }

            return;
        }

        if ((_turnAssistFlags & TurnAssistCorrectX) != 0)
        {
            Vector2I correction = StepTowardX(_turnLaneTarget.X);
            if (correction != Vector2I.Zero)
            {
                TryAdvanceOnePixel(correction, updateCurrentDirection: false);
                NoteDebug("corrected X toward target lane");
            }
        }
    }

    /// <summary>
    /// Starts with a small assisted correction when the actor is close enough to
    /// a turn lane, otherwise falls back to the ordinary request-latch path.
    /// </summary>
    private void AdvanceCloseRangeAssistThenNormal(Vector2I requested)
    {
        AppendDebugPath("CloseRangeAssist");

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
        AppendDebugPath("AssistedTurn");
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
                NoteDebug("same-axis reversal latched during assisted turn; movement deferred");
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

        // Fallback: finish any remaining one-axis correction before moving normally.
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
        AppendDebugPath("AssistedStep");

        if (!CanMoveInRequestedDirectionFromTurnLane(requested))
        {
            NoteDebug("assisted step refused: requested direction is blocked from turn lane");
            return;
        }

        Vector2I correction = Vector2I.Zero;

        if (IsVertical(requested))
            correction = StepTowardX(_turnLaneTarget.X);
        else if (IsHorizontal(requested))
            correction = StepTowardY(_turnLaneTarget.Y);

        if (correction != Vector2I.Zero)
            TryAdvanceOnePixel(correction, updateCurrentDirection: false);

        if (TryAdvanceOnePixel(requested, updateCurrentDirection: true))
        {
            _latchedRequestedDir = requested;
            _offsetDir = requested;
        }

        if (clearAssistFlags)
            _turnAssistFlags = 0;
    }

    /// <summary>
    /// Attempts a single straight pixel step in the requested direction.
    /// </summary>
    private void AdvanceStraightStep(Vector2I requested)
    {
        AppendDebugPath("StraightStep");

        if (TryAdvanceOnePixel(requested, updateCurrentDirection: true))
        {
            _latchedRequestedDir = requested;
            _offsetDir = requested;
        }
        else
        {
            // A blocked step must not erase the previous direction; otherwise
            // short tap-turns lose their context and become too strict.
            NoteDebug("straight step blocked; current direction preserved");
        }
    }

    /// <summary>
    /// Chooses which turn path to use based on the current pixel position, the
    /// requested direction, and the reverse-engineered turn-window tables.
    /// </summary>
    private TurnWindowDecision ChooseTurnPath(Vector2I requested, Vector2I current)
    {
        if (IsVertical(requested) && IsHorizontal(current))
            return ChooseHorizontalToVerticalTurnPath(requested);

        if (IsHorizontal(requested) && IsVertical(current))
            return ChooseVerticalToHorizontalTurnPath(requested);

        return new TurnWindowDecision(TurnPath.Normal, _turnLaneTarget, 0, 0, 0, 0);
    }

    private TurnWindowDecision ChooseHorizontalToVerticalTurnPath(Vector2I requested)
    {
        int x = ToByte(_arcadePixelPos.X);
        int mameY = ToByte(ToMameY(_arcadePixelPos.Y));
        ushort mask = GetHorizontalToVerticalTurnMask(mameY);
        (int upperLaneX, int lowerLaneX) = ScanTurnMask(mask, start: 0x08, step: 0x10, compare: x);

        int earlyAssistedStart = ToByte(upperLaneX - 4);
        if (ByteGreaterOrEqual(x, earlyAssistedStart))
        {
            return new TurnWindowDecision(
                TurnPath.Assisted,
                new Vector2I(upperLaneX, _arcadePixelPos.Y),
                0,
                mask,
                upperLaneX,
                lowerLaneX);
        }

        int closeAssistStart = ToByte(earlyAssistedStart - 2);
        if (ByteGreaterOrEqual(x, closeAssistStart))
        {
            return new TurnWindowDecision(
                TurnPath.CloseRangeAssistThenNormal,
                new Vector2I(upperLaneX, _arcadePixelPos.Y),
                TurnAssistCorrectX,
                mask,
                upperLaneX,
                lowerLaneX);
        }

        int lateAssistedEnd = ToByte(lowerLaneX + 5);
        if (ByteLessThan(x, lateAssistedEnd))
        {
            return new TurnWindowDecision(
                TurnPath.Assisted,
                new Vector2I(lowerLaneX, _arcadePixelPos.Y),
                0,
                mask,
                upperLaneX,
                lowerLaneX);
        }

        int normalStart = ToByte(lateAssistedEnd + 2);
        if (ByteGreaterOrEqual(x, normalStart))
        {
            return new TurnWindowDecision(
                TurnPath.Normal,
                _turnLaneTarget,
                0,
                mask,
                upperLaneX,
                lowerLaneX);
        }

        return new TurnWindowDecision(
            TurnPath.CloseRangeAssistThenNormal,
            new Vector2I(lowerLaneX, _arcadePixelPos.Y),
            TurnAssistCorrectX,
            mask,
            upperLaneX,
            lowerLaneX);
    }

    private TurnWindowDecision ChooseVerticalToHorizontalTurnPath(Vector2I requested)
    {
        int x = ToByte(_arcadePixelPos.X);
        int mameY = ToByte(ToMameY(_arcadePixelPos.Y));
        ushort mask = GetVerticalToHorizontalTurnMask(x);
        (int upperLaneMameY, int lowerLaneMameY) = ScanTurnMask(mask, start: 0x36, step: 0x10, compare: mameY);

        int earlyAssistedStart = ToByte(upperLaneMameY - 4);
        if (ByteGreaterOrEqual(mameY, earlyAssistedStart))
        {
            return new TurnWindowDecision(
                TurnPath.Assisted,
                new Vector2I(_arcadePixelPos.X, FromMameY(upperLaneMameY)),
                0,
                mask,
                upperLaneMameY,
                lowerLaneMameY);
        }

        int closeAssistStart = ToByte(earlyAssistedStart - 3);
        if (ByteGreaterOrEqual(mameY, closeAssistStart))
        {
            return new TurnWindowDecision(
                TurnPath.CloseRangeAssistThenNormal,
                new Vector2I(_arcadePixelPos.X, FromMameY(upperLaneMameY)),
                TurnAssistCorrectY,
                mask,
                upperLaneMameY,
                lowerLaneMameY);
        }

        int lateAssistedEnd = ToByte(lowerLaneMameY + 5);
        if (ByteLessThan(mameY, lateAssistedEnd))
        {
            return new TurnWindowDecision(
                TurnPath.Assisted,
                new Vector2I(_arcadePixelPos.X, FromMameY(lowerLaneMameY)),
                0,
                mask,
                upperLaneMameY,
                lowerLaneMameY);
        }

        int normalStart = ToByte(lateAssistedEnd + 3);
        if (ByteGreaterOrEqual(mameY, normalStart))
        {
            return new TurnWindowDecision(
                TurnPath.Normal,
                _turnLaneTarget,
                0,
                mask,
                upperLaneMameY,
                lowerLaneMameY);
        }

        return new TurnWindowDecision(
            TurnPath.CloseRangeAssistThenNormal,
            new Vector2I(_arcadePixelPos.X, FromMameY(lowerLaneMameY)),
            TurnAssistCorrectY,
            mask,
            upperLaneMameY,
            lowerLaneMameY);
    }

    /// <summary>
    /// Extracts the two nearest valid turn lanes from a 16-bit turn-window mask.
    /// </summary>
    private static (int UpperLane, int LowerLane) ScanTurnMask(
        ushort mask,
        int start,
        int step,
        int compare)
    {
        int? lower = null;
        int? upper = null;
        int last = start;
        bool foundAny = false;

        for (int bit = 0; bit < 16; bit++)
        {
            if (((mask >> bit) & 1) == 0)
                continue;

            int laneCoordinate = ToByte(start + bit * step);
            last = laneCoordinate;
            foundAny = true;

            if (laneCoordinate < compare)
            {
                lower = laneCoordinate;
            }
            else if (upper == null)
            {
                upper = laneCoordinate;
            }
        }

        if (!foundAny)
            throw new InvalidOperationException($"Turn mask is empty: 0x{mask:X4}");

        lower ??= last;
        upper ??= ToByte(start);

        return (upper.Value, lower.Value);
    }

    private static ushort GetHorizontalToVerticalTurnMask(int mameY)
    {
        int index = ToByte(mameY - 0x36) >> 4;
        index = Math.Clamp(index, 0, HorizontalToVerticalTurnMasks.Length - 1);
        return HorizontalToVerticalTurnMasks[index];
    }

    private static ushort GetVerticalToHorizontalTurnMask(int x)
    {
        int index = ToByte(x - 0x08) >> 4;
        index = Math.Clamp(index, 0, VerticalToHorizontalTurnMasks.Length - 1);
        return VerticalToHorizontalTurnMasks[index];
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
            _debugBlocked = $"blocked {DirName(direction)} by {step.Kind}";
            return false;
        }

        _arcadePixelPos += direction;

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

        EndDebugTick(previousPixelPos, previousDirection, wantedDir);
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

        return new PlayerMovementStepResult(
            moved,
            directionChanged,
            stopped,
            previousPixelPos,
            _arcadePixelPos,
            snappedArcadePixelPos,
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

    private static int ToByte(int value)
    {
        return value & 0xFF;
    }

    private static bool ByteGreaterOrEqual(int a, int b)
    {
        return ToByte(a) >= ToByte(b);
    }

    private static bool ByteLessThan(int a, int b)
    {
        return ToByte(a) < ToByte(b);
    }

    private static int ToMameY(int godotArcadeY)
    {
        return MameYMirror - godotArcadeY;
    }

    private static int FromMameY(int mameY)
    {
        return MameYMirror - ToByte(mameY);
    }

    private void BeginDebugTick()
    {
        if (!DebugMovementLog)
            return;

        _debugPath = string.Empty;
        _debugNotes = string.Empty;
        _debugBlocked = string.Empty;
    }

    private void AppendDebugPath(string path)
    {
        if (!DebugMovementLog)
            return;

        if (_debugPath.Length == 0)
            _debugPath = path;
        else
            _debugPath += " -> " + path;
    }

    private void NoteDebug(string note)
    {
        if (!DebugMovementLog)
            return;

        if (_debugNotes.Length == 0)
            _debugNotes = note;
        else
            _debugNotes += "; " + note;
    }

    private void EndDebugTick(Vector2I previousPixelPos, Vector2I previousDirection, Vector2I wantedDir)
    {
        if (!DebugMovementLog)
            return;

        GD.Print(
            $"MoveTick {_debugTickIndex++:0000}: " +
            $"In={DirName(wantedDir),-5} Pre={FormatMamePos(previousPixelPos)} Post={FormatMamePos(_arcadePixelPos)} " +
            $"Dir={DirName(previousDirection)}->{DirName(_currentDir)} Req={DirName(_latchedRequestedDir)} " +
            $"Tgt={FormatMamePos(_turnLaneTarget)} Assist={_turnAssistFlags:X2} Assisted={_assistedTurnActive} " +
            $"Path={_debugPath} {_debugBlocked} Notes={_debugNotes}");
    }

    private static string FormatMamePos(Vector2I pos)
    {
        return $"({pos.X:X2},{ToByte(ToMameY(pos.Y)):X2})";
    }

    private static string DirName(Vector2I direction)
    {
        if (direction == Vector2I.Left)
            return "Left";
        if (direction == Vector2I.Right)
            return "Right";
        if (direction == Vector2I.Up)
            return "Up";
        if (direction == Vector2I.Down)
            return "Down";
        return "None";
    }
}
