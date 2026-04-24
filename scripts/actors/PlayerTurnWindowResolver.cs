using System;
using Godot;

namespace LadyBug.Actors;

/// <summary>
/// Resolves the player's arcade turn window from the current arcade-pixel position.
/// </summary>
/// <remarks>
/// Lady Bug does not turn only at the mathematical center of a logical cell.
/// Around many intersections, the original game accepts a requested perpendicular
/// direction slightly before or after the visible lane center and may perform a
/// short assisted correction step.
///
/// This resolver keeps that knowledge outside <see cref="PlayerMovementMotor"/>.
/// The motor asks one question: for the current position and requested direction,
/// should it keep moving normally, start an assisted turn, or apply a close-range
/// correction before returning to normal movement?
///
/// The lane tables below are data, not gameplay policy. They describe which turn
/// lanes exist on each row or column band of the maze. The policy that interprets
/// those lanes is expressed with named constants and helper methods below.
/// </remarks>
internal static class PlayerTurnWindowResolver
{
    /// <summary>
    /// Coordinate-space mirror used by the original arcade runtime for vertical
    /// player positions. Our Godot gameplay Y grows downward from the top of the
    /// maze, while the extracted turn tables use the original mirrored screen Y.
    /// Keeping the conversion here prevents that historical detail from leaking
    /// into the movement motor.
    /// </summary>
    private const int OriginalScreenYMirrorOrigin = 0xDD;

    /// <summary>
    /// All original movement comparisons were effectively performed on 8-bit
    /// coordinates. These helpers preserve the same wrapping behavior around the
    /// maze edges without exposing byte arithmetic to the rest of the movement code.
    /// </summary>
    private const int CoordinateByteMask = 0xFF;

    /// <summary>
    /// Distance before the next valid lane where a full assisted turn is accepted.
    /// </summary>
    private const int AssistedPixelsBeforeNextLane = 4;

    /// <summary>
    /// Distance after the previous valid lane where a full assisted turn is still accepted.
    /// </summary>
    private const int AssistedPixelsAfterPreviousLane = 5;

    /// <summary>
    /// Extra close-range window around X-lane turns. This path applies one stored
    /// correction step and then lets normal movement continue.
    /// </summary>
    private const int CloseRangePaddingForVerticalTurns = 2;

    /// <summary>
    /// Extra close-range window around Y-lane turns. The value is slightly wider
    /// than the X case because the original movement anchor is asymmetric by one
    /// pixel on the vertical axis.
    /// </summary>
    private const int CloseRangePaddingForHorizontalTurns = 3;

    /// <summary>
    /// Turn-lane map used while moving left/right and requesting up/down.
    /// A current row band selects which X lanes can be used for vertical turns.
    /// </summary>
    private static readonly TurnWindowMap VerticalTurnWindowsByRow = new(
        masksByBand: new ushort[]
        {
            0x0777, 0x03DE, 0x05FD, 0x03DE, 0x03FE,
            0x07AF, 0x05FD, 0x07DF, 0x07FF, 0x03DE, 0x07AF,
        },
        bandOrigin: 0x36,
        laneOrigin: 0x08,
        laneSpacing: 0x10);

    /// <summary>
    /// Turn-lane map used while moving up/down and requesting left/right.
    /// A current column band selects which Y lanes can be used for horizontal turns.
    /// The lane coordinates stored in this table are expressed in the original
    /// mirrored screen-Y space and are converted back before returning a target.
    /// </summary>
    private static readonly TurnWindowMap HorizontalTurnWindowsByColumn = new(
        masksByBand: new ushort[]
        {
            0x05E5, 0x07FF, 0x07FF, 0x07FE, 0x03DF,
            0x0575, 0x03DF, 0x07FE, 0x07FF, 0x07BB, 0x05E5,
        },
        bandOrigin: 0x08,
        laneOrigin: 0x36,
        laneSpacing: 0x10);

    /// <summary>
    /// Chooses the turn behavior for the requested direction at the current position.
    /// </summary>
    /// <param name="arcadePixelPos">Current gameplay position in arcade pixels.</param>
    /// <param name="requestedDirection">Current non-zero direction requested by input.</param>
    /// <param name="currentDirection">Direction currently used by movement.</param>
    /// <param name="previousLaneTarget">Previous assisted-turn target, reused when no new perpendicular turn is being selected.</param>
    /// <returns>A high-level turn-window decision for the movement motor.</returns>
    public static PlayerTurnWindowDecision Choose(
        Vector2I arcadePixelPos,
        Vector2I requestedDirection,
        Vector2I currentDirection,
        Vector2I previousLaneTarget)
    {
        if (IsVertical(requestedDirection) && IsHorizontal(currentDirection))
            return ChooseVerticalTurnFromHorizontalMovement(arcadePixelPos);

        if (IsHorizontal(requestedDirection) && IsVertical(currentDirection))
            return ChooseHorizontalTurnFromVerticalMovement(arcadePixelPos);

        return new PlayerTurnWindowDecision(
            PlayerTurnPath.Normal,
            previousLaneTarget,
            PlayerTurnAssistFlags.None,
            0,
            0,
            0);
    }

    /// <summary>
    /// Resolves the case where the player is moving horizontally and asks to turn vertically.
    /// The current row band selects valid X lane targets.
    /// </summary>
    private static PlayerTurnWindowDecision ChooseVerticalTurnFromHorizontalMovement(Vector2I arcadePixelPos)
    {
        int actorX = ToWrappedCoordinate(arcadePixelPos.X);
        int originalScreenY = ToOriginalScreenY(arcadePixelPos.Y);

        ushort mask = VerticalTurnWindowsByRow.GetMaskForBand(originalScreenY);
        TurnLanePair lanes = VerticalTurnWindowsByRow.FindSurroundingLanes(mask, actorX);

        return ChooseTurnPathAroundLanePair(
            actorCoordinate: actorX,
            lanes: lanes,
            laneToTarget: laneX => new Vector2I(laneX, arcadePixelPos.Y),
            correctionFlag: PlayerTurnAssistFlags.CorrectX,
            mask: mask,
            closeRangePadding: CloseRangePaddingForVerticalTurns);
    }

    /// <summary>
    /// Resolves the case where the player is moving vertically and asks to turn horizontally.
    /// The current column band selects valid Y lane targets.
    /// </summary>
    private static PlayerTurnWindowDecision ChooseHorizontalTurnFromVerticalMovement(Vector2I arcadePixelPos)
    {
        int actorX = ToWrappedCoordinate(arcadePixelPos.X);
        int originalScreenY = ToOriginalScreenY(arcadePixelPos.Y);

        ushort mask = HorizontalTurnWindowsByColumn.GetMaskForBand(actorX);
        TurnLanePair lanes = HorizontalTurnWindowsByColumn.FindSurroundingLanes(mask, originalScreenY);

        return ChooseTurnPathAroundLanePair(
            actorCoordinate: originalScreenY,
            lanes: lanes,
            laneToTarget: laneY => new Vector2I(arcadePixelPos.X, FromOriginalScreenY(laneY)),
            correctionFlag: PlayerTurnAssistFlags.CorrectY,
            mask: mask,
            closeRangePadding: CloseRangePaddingForHorizontalTurns);
    }

    /// <summary>
    /// Converts a pair of nearby valid lanes into the movement path used by the motor.
    /// </summary>
    /// <remarks>
    /// The same policy applies to both turn orientations:
    /// - just before the next lane, take the assisted path toward that lane;
    /// - very close before the next lane, apply one correction and then continue normally;
    /// - just after the previous lane, take the assisted path toward that lane;
    /// - clearly outside the turn window, continue through the normal request latch.
    ///
    /// The <paramref name="closeRangePadding"/> is orientation-specific because the
    /// original player anchor is not perfectly symmetrical on X and Y.
    /// </remarks>
    private static PlayerTurnWindowDecision ChooseTurnPathAroundLanePair(
        int actorCoordinate,
        TurnLanePair lanes,
        Func<int, Vector2I> laneToTarget,
        PlayerTurnAssistFlags correctionFlag,
        ushort mask,
        int closeRangePadding)
    {
        int assistedStartBeforeNextLane = ToWrappedCoordinate(lanes.NextLane - AssistedPixelsBeforeNextLane);
        if (WrappedGreaterOrEqual(actorCoordinate, assistedStartBeforeNextLane))
        {
            return AssistedTurn(laneToTarget(lanes.NextLane), mask, lanes);
        }

        int closeRangeStartBeforeNextLane = ToWrappedCoordinate(assistedStartBeforeNextLane - closeRangePadding);
        if (WrappedGreaterOrEqual(actorCoordinate, closeRangeStartBeforeNextLane))
        {
            return CloseRangeAssistThenNormal(laneToTarget(lanes.NextLane), correctionFlag, mask, lanes);
        }

        int assistedEndAfterPreviousLane = ToWrappedCoordinate(lanes.PreviousLane + AssistedPixelsAfterPreviousLane);
        if (WrappedLessThan(actorCoordinate, assistedEndAfterPreviousLane))
        {
            return AssistedTurn(laneToTarget(lanes.PreviousLane), mask, lanes);
        }

        int normalStartAfterPreviousLane = ToWrappedCoordinate(assistedEndAfterPreviousLane + closeRangePadding);
        if (WrappedGreaterOrEqual(actorCoordinate, normalStartAfterPreviousLane))
        {
            return NormalMovement(mask, lanes);
        }

        return CloseRangeAssistThenNormal(laneToTarget(lanes.PreviousLane), correctionFlag, mask, lanes);
    }

    /// <summary>
    /// Builds a decision that starts or continues a full assisted turn.
    /// </summary>
    private static PlayerTurnWindowDecision AssistedTurn(Vector2I target, ushort mask, TurnLanePair lanes)
    {
        return new PlayerTurnWindowDecision(
            PlayerTurnPath.Assisted,
            target,
            PlayerTurnAssistFlags.None,
            mask,
            lanes.NextLane,
            lanes.PreviousLane);
    }

    /// <summary>
    /// Builds a decision that applies one close-range correction before normal movement resumes.
    /// </summary>
    private static PlayerTurnWindowDecision CloseRangeAssistThenNormal(
        Vector2I target,
        PlayerTurnAssistFlags correctionFlag,
        ushort mask,
        TurnLanePair lanes)
    {
        return new PlayerTurnWindowDecision(
            PlayerTurnPath.CloseRangeAssistThenNormal,
            target,
            correctionFlag,
            mask,
            lanes.NextLane,
            lanes.PreviousLane);
    }

    /// <summary>
    /// Builds a decision that does not need a new assisted-turn target.
    /// </summary>
    private static PlayerTurnWindowDecision NormalMovement(ushort mask, TurnLanePair lanes)
    {
        return new PlayerTurnWindowDecision(
            PlayerTurnPath.Normal,
            Vector2I.Zero,
            PlayerTurnAssistFlags.None,
            mask,
            lanes.NextLane,
            lanes.PreviousLane);
    }

    /// <summary>
    /// Returns true when a direction is a pure left/right vector.
    /// </summary>
    private static bool IsHorizontal(Vector2I direction)
    {
        return direction.X != 0 && direction.Y == 0;
    }

    /// <summary>
    /// Returns true when a direction is a pure up/down vector.
    /// </summary>
    private static bool IsVertical(Vector2I direction)
    {
        return direction.Y != 0 && direction.X == 0;
    }

    /// <summary>
    /// Keeps coordinate arithmetic in the same 8-bit wrapping range used by the
    /// extracted turn tables.
    /// </summary>
    private static int ToWrappedCoordinate(int value)
    {
        return value & CoordinateByteMask;
    }

    /// <summary>
    /// Compares two wrapped coordinates using the same simple unsigned ordering
    /// used by the extracted turn-window logic.
    /// </summary>
    private static bool WrappedGreaterOrEqual(int a, int b)
    {
        return ToWrappedCoordinate(a) >= ToWrappedCoordinate(b);
    }

    /// <summary>
    /// Compares two wrapped coordinates using the same simple unsigned ordering
    /// used by the extracted turn-window logic.
    /// </summary>
    private static bool WrappedLessThan(int a, int b)
    {
        return ToWrappedCoordinate(a) < ToWrappedCoordinate(b);
    }

    /// <summary>
    /// Converts Godot arcade-space Y to the original mirrored vertical coordinate
    /// used by the extracted lane tables.
    /// </summary>
    private static int ToOriginalScreenY(int godotArcadeY)
    {
        return ToWrappedCoordinate(OriginalScreenYMirrorOrigin - godotArcadeY);
    }

    /// <summary>
    /// Converts an extracted mirrored vertical lane coordinate back to Godot
    /// arcade-space Y.
    /// </summary>
    private static int FromOriginalScreenY(int originalScreenY)
    {
        return OriginalScreenYMirrorOrigin - ToWrappedCoordinate(originalScreenY);
    }

    /// <summary>
    /// Pair of valid turn-lane coordinates surrounding the actor coordinate.
    /// </summary>
    /// <param name="NextLane">First valid lane at or after the actor coordinate, wrapping to the first lane when needed.</param>
    /// <param name="PreviousLane">Last valid lane before the actor coordinate, wrapping to the last lane when needed.</param>
    private readonly record struct TurnLanePair(int NextLane, int PreviousLane);

    /// <summary>
    /// Compact representation of one family of turn windows.
    /// </summary>
    /// <remarks>
    /// A turn-window map is selected in two stages:
    /// - the current row or column chooses one 16-bit lane mask;
    /// - each set bit in that mask enables one lane coordinate.
    /// </remarks>
    private readonly struct TurnWindowMap
    {
        private readonly ushort[] _masksByBand;
        private readonly int _bandOrigin;
        private readonly int _laneOrigin;
        private readonly int _laneSpacing;

        /// <summary>
        /// Creates one map of turn lanes.
        /// </summary>
        /// <param name="masksByBand">One lane mask per row or column band.</param>
        /// <param name="bandOrigin">Coordinate of the first band represented by the first mask.</param>
        /// <param name="laneOrigin">Coordinate of the first lane represented by bit 0.</param>
        /// <param name="laneSpacing">Distance in arcade pixels between two lane bits.</param>
        public TurnWindowMap(ushort[] masksByBand, int bandOrigin, int laneOrigin, int laneSpacing)
        {
            _masksByBand = masksByBand ?? throw new ArgumentNullException(nameof(masksByBand));
            _bandOrigin = bandOrigin;
            _laneOrigin = laneOrigin;
            _laneSpacing = laneSpacing;
        }

        /// <summary>
        /// Gets the lane mask for a given row or column coordinate.
        /// </summary>
        public ushort GetMaskForBand(int coordinate)
        {
            int index = ToWrappedCoordinate(coordinate - _bandOrigin) >> 4;
            index = Math.Clamp(index, 0, _masksByBand.Length - 1);
            return _masksByBand[index];
        }

        /// <summary>
        /// Finds the valid enabled lanes immediately around the actor coordinate.
        /// </summary>
        /// <param name="mask">Lane mask selected by <see cref="GetMaskForBand"/>.</param>
        /// <param name="actorCoordinate">Current actor coordinate on the axis being corrected.</param>
        /// <returns>The next and previous valid lane coordinates.</returns>
        public TurnLanePair FindSurroundingLanes(ushort mask, int actorCoordinate)
        {
            int? previousLane = null;
            int? nextLane = null;
            int lastEnabledLane = _laneOrigin;
            bool foundAnyLane = false;

            for (int bit = 0; bit < 16; bit++)
            {
                if (((mask >> bit) & 1) == 0)
                    continue;

                int laneCoordinate = ToWrappedCoordinate(_laneOrigin + bit * _laneSpacing);
                lastEnabledLane = laneCoordinate;
                foundAnyLane = true;

                if (laneCoordinate < actorCoordinate)
                {
                    previousLane = laneCoordinate;
                }
                else if (nextLane == null)
                {
                    nextLane = laneCoordinate;
                }
            }

            if (!foundAnyLane)
                throw new InvalidOperationException($"Turn-window mask is empty: 0x{mask:X4}");

            previousLane ??= lastEnabledLane;
            nextLane ??= ToWrappedCoordinate(_laneOrigin);

            return new TurnLanePair(nextLane.Value, previousLane.Value);
        }
    }
}
