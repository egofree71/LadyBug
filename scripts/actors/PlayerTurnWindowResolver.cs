using System;
using Godot;

namespace LadyBug.Actors;

/// <summary>
/// Resolves the player's arcade turn window from the current arcade-pixel position.
/// </summary>
/// <remarks>
/// Lady Bug does not turn only at the mathematical center of a logical cell.
/// Around intersections, the game accepts a requested perpendicular direction
/// slightly before or after the visible lane center and may perform a short
/// assisted correction step.
///
/// This resolver keeps that policy outside <see cref="PlayerMovementMotor"/>.
/// The available turn lanes are now supplied by <see cref="PlayerTurnWindowMaps"/>,
/// which can be generated from the logical maze instead of hardcoded from ROM
/// tables. The resolver still owns the pixel-window policy around those lanes.
/// </remarks>
internal static class PlayerTurnWindowResolver
{
    /// <summary>
    /// Coordinate-space mirror used by the original arcade runtime for vertical
    /// player positions. Our Godot gameplay Y grows downward from the top of the
    /// maze, while turn-window Y lanes use the original mirrored screen Y.
    /// </summary>
    private const int OriginalScreenYMirrorOrigin = 0xDD;

    /// <summary>
    /// All original movement comparisons were effectively performed on 8-bit
    /// coordinates. These helpers preserve the same wrapping behavior around the
    /// maze edges without exposing byte arithmetic to the movement motor.
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
    /// Chooses the turn behavior for the requested direction at the current position.
    /// </summary>
    /// <param name="turnWindows">Turn-lane maps generated for the current maze.</param>
    /// <param name="arcadePixelPos">Current gameplay position in arcade pixels.</param>
    /// <param name="requestedDirection">Current non-zero direction requested by input.</param>
    /// <param name="currentDirection">Direction currently used by movement.</param>
    /// <param name="previousLaneTarget">Previous assisted-turn target, reused when no new perpendicular turn is being selected.</param>
    /// <returns>A high-level turn-window decision for the movement motor.</returns>
    public static PlayerTurnWindowDecision Choose(
        PlayerTurnWindowMaps turnWindows,
        Vector2I arcadePixelPos,
        Vector2I requestedDirection,
        Vector2I currentDirection,
        Vector2I previousLaneTarget)
    {
        if (turnWindows == null)
            throw new ArgumentNullException(nameof(turnWindows));

        if (IsVertical(requestedDirection) && IsHorizontal(currentDirection))
            return ChooseVerticalTurnFromHorizontalMovement(turnWindows, arcadePixelPos);

        if (IsHorizontal(requestedDirection) && IsVertical(currentDirection))
            return ChooseHorizontalTurnFromVerticalMovement(turnWindows, arcadePixelPos);

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
    private static PlayerTurnWindowDecision ChooseVerticalTurnFromHorizontalMovement(
        PlayerTurnWindowMaps turnWindows,
        Vector2I arcadePixelPos)
    {
        int actorX = ToWrappedCoordinate(arcadePixelPos.X);
        int originalScreenY = ToOriginalScreenY(arcadePixelPos.Y);

        ushort mask = turnWindows.VerticalTurnWindowsByRow.GetMaskForBand(originalScreenY);
        PlayerTurnWindowMaps.TurnLanePair lanes =
            turnWindows.VerticalTurnWindowsByRow.FindSurroundingLanes(mask, actorX);

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
    private static PlayerTurnWindowDecision ChooseHorizontalTurnFromVerticalMovement(
        PlayerTurnWindowMaps turnWindows,
        Vector2I arcadePixelPos)
    {
        int actorX = ToWrappedCoordinate(arcadePixelPos.X);
        int originalScreenY = ToOriginalScreenY(arcadePixelPos.Y);

        ushort mask = turnWindows.HorizontalTurnWindowsByColumn.GetMaskForBand(actorX);
        PlayerTurnWindowMaps.TurnLanePair lanes =
            turnWindows.HorizontalTurnWindowsByColumn.FindSurroundingLanes(mask, originalScreenY);

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
        PlayerTurnWindowMaps.TurnLanePair lanes,
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
    private static PlayerTurnWindowDecision AssistedTurn(
        Vector2I target,
        ushort mask,
        PlayerTurnWindowMaps.TurnLanePair lanes)
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
        PlayerTurnWindowMaps.TurnLanePair lanes)
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
    private static PlayerTurnWindowDecision NormalMovement(
        ushort mask,
        PlayerTurnWindowMaps.TurnLanePair lanes)
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
    /// turn-window logic.
    /// </summary>
    private static int ToWrappedCoordinate(int value)
    {
        return value & CoordinateByteMask;
    }

    /// <summary>
    /// Compares two wrapped coordinates using simple unsigned ordering.
    /// </summary>
    private static bool WrappedGreaterOrEqual(int a, int b)
    {
        return ToWrappedCoordinate(a) >= ToWrappedCoordinate(b);
    }

    /// <summary>
    /// Compares two wrapped coordinates using simple unsigned ordering.
    /// </summary>
    private static bool WrappedLessThan(int a, int b)
    {
        return ToWrappedCoordinate(a) < ToWrappedCoordinate(b);
    }

    /// <summary>
    /// Converts Godot arcade-space Y to the original mirrored vertical coordinate
    /// used by the lane tables.
    /// </summary>
    private static int ToOriginalScreenY(int godotArcadeY)
    {
        return ToWrappedCoordinate(OriginalScreenYMirrorOrigin - godotArcadeY);
    }

    /// <summary>
    /// Converts a mirrored vertical lane coordinate back to Godot arcade-space Y.
    /// </summary>
    private static int FromOriginalScreenY(int originalScreenY)
    {
        return OriginalScreenYMirrorOrigin - ToWrappedCoordinate(originalScreenY);
    }
}
