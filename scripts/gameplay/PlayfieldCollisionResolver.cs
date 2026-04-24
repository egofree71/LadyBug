using System;
using Godot;
using LadyBug.Gameplay.Gates;
using LadyBug.Gameplay.Maze;

namespace LadyBug.Gameplay;

/// <summary>
/// Evaluates one arcade-pixel movement step against the active playfield.
/// </summary>
/// <remarks>
/// The active playfield is made of two layers:
/// - the static maze, stored in <see cref="MazeGrid"/>;
/// - the dynamic rotating-gate overlay, stored in <see cref="GateSystem"/>.
///
/// This class keeps collision / blocking logic outside <c>Level</c>. The level
/// remains responsible for owning runtime objects and for coordinate conversion,
/// while this resolver answers one focused question: is this pixel step allowed,
/// blocked by a fixed wall, or blocked by a gate?
/// </remarks>
public sealed class PlayfieldCollisionResolver
{
    private readonly MazeGrid _mazeGrid;
    private readonly GateSystem _gateSystem;
    private readonly Func<Vector2I, Vector2I> _arcadePixelToLogicalCell;
    private readonly Func<Vector2I, Vector2I> _gatePivotToArcadePixel;

    /// <summary>
    /// Creates a playfield collision resolver for one active level runtime.
    /// </summary>
    /// <param name="mazeGrid">Static maze used as the base movement layer.</param>
    /// <param name="gateSystem">Runtime rotating-gate overlay.</param>
    /// <param name="arcadePixelToLogicalCell">Coordinate conversion supplied by the owning level.</param>
    /// <param name="gatePivotToArcadePixel">Gate pivot conversion supplied by the owning level.</param>
    public PlayfieldCollisionResolver(
        MazeGrid mazeGrid,
        GateSystem gateSystem,
        Func<Vector2I, Vector2I> arcadePixelToLogicalCell,
        Func<Vector2I, Vector2I> gatePivotToArcadePixel)
    {
        _mazeGrid = mazeGrid ?? throw new ArgumentNullException(nameof(mazeGrid));
        _gateSystem = gateSystem ?? throw new ArgumentNullException(nameof(gateSystem));
        _arcadePixelToLogicalCell = arcadePixelToLogicalCell ?? throw new ArgumentNullException(nameof(arcadePixelToLogicalCell));
        _gatePivotToArcadePixel = gatePivotToArcadePixel ?? throw new ArgumentNullException(nameof(gatePivotToArcadePixel));
    }

    /// <summary>
    /// Evaluates one attempted arcade-pixel step through the static maze and the
    /// dynamic rotating-gate overlay.
    /// </summary>
    /// <param name="arcadePixelPos">Current gameplay position in arcade pixels.</param>
    /// <param name="direction">Attempted one-pixel movement direction.</param>
    /// <param name="collisionLead">Forward collision probe offset.</param>
    /// <returns>A combined playfield result for the attempted step.</returns>
    public PlayfieldStepResult EvaluateArcadePixelStep(
        Vector2I arcadePixelPos,
        Vector2I direction,
        Vector2I collisionLead)
    {
        MazeStepResult mazeStep = _mazeGrid.EvaluateArcadePixelStep(
            arcadePixelPos,
            direction,
            collisionLead,
            _arcadePixelToLogicalCell);

        if (!mazeStep.Allowed)
            return PlayfieldStepResult.BlockedByFixedWall(mazeStep);

        if (TryGetBlockingGateIdAtProbe(
                arcadePixelPos,
                direction,
                collisionLead,
                out int gateId,
                out GateContactHalf? contactHalf))
        {
            return PlayfieldStepResult.BlockedByGate(mazeStep, gateId, contactHalf);
        }

        if (mazeStep.NextCell == mazeStep.CurrentCell)
            return PlayfieldStepResult.AllowedStep(mazeStep);

        if (TryGetBlockingGateIdForCellBoundaryStep(
                mazeStep,
                direction,
                out gateId,
                out GateContactHalf boundaryContactHalf))
        {
            return PlayfieldStepResult.BlockedByGate(mazeStep, gateId, boundaryContactHalf);
        }

        return PlayfieldStepResult.AllowedStep(mazeStep);
    }

    /// <summary>
    /// Detects a gate blocked directly by the forward pixel probe, even when the
    /// probe has not yet crossed into another logical cell.
    /// </summary>
    /// <remarks>
    /// This is needed because a rotating gate may sit exactly on a local boundary
    /// before <see cref="MazeGrid"/> reports a logical-cell transition.
    /// </remarks>
    private bool TryGetBlockingGateIdAtProbe(
        Vector2I arcadePixelPos,
        Vector2I direction,
        Vector2I collisionLead,
        out int gateId,
        out GateContactHalf? contactHalf)
    {
        gateId = -1;
        contactHalf = null;

        if (direction == Vector2I.Zero)
            return false;

        Vector2I currentCell = _arcadePixelToLogicalCell(arcadePixelPos);
        Vector2I probeStart = arcadePixelPos + collisionLead;
        Vector2I probeEnd = arcadePixelPos + direction + collisionLead;

        Vector2I[] candidatePivots =
        {
            currentCell,
            new Vector2I(currentCell.X + 1, currentCell.Y),
            new Vector2I(currentCell.X, currentCell.Y + 1),
            new Vector2I(currentCell.X + 1, currentCell.Y + 1),
        };

        foreach (Vector2I pivot in candidatePivots)
        {
            if (!_gateSystem.TryGetGateByPivot(pivot, out RotatingGateRuntimeState gate))
                continue;

            if (!gate.BlocksMovement(direction))
                continue;

            if (TryGetGateBlockAtProbe(
                    gate,
                    probeStart,
                    probeEnd,
                    direction,
                    out GateContactHalf? candidateHalf))
            {
                gateId = gate.Id;
                contactHalf = candidateHalf;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Tests whether one probe segment intersects one gate's current blocking axis.
    /// </summary>
    private bool TryGetGateBlockAtProbe(
        RotatingGateRuntimeState gate,
        Vector2I probeStart,
        Vector2I probeEnd,
        Vector2I direction,
        out GateContactHalf? contactHalf)
    {
        contactHalf = null;

        Vector2I pivotArcade = _gatePivotToArcadePixel(gate.Pivot);

        if (gate.LogicalState == GateLogicalState.BlocksVertical)
        {
            if (direction.Y == 0)
                return false;

            if (!CrossesCoordinate(probeStart.Y, probeEnd.Y, pivotArcade.Y))
                return false;

            int localX = probeEnd.X - pivotArcade.X;
            if (Math.Abs(localX) > 8)
                return false;

            if (localX < 0)
                contactHalf = GateContactHalf.Left;
            else if (localX > 0)
                contactHalf = GateContactHalf.Right;
            else
                contactHalf = null;

            return true;
        }

        if (gate.LogicalState == GateLogicalState.BlocksHorizontal)
        {
            if (direction.X == 0)
                return false;

            if (!CrossesCoordinate(probeStart.X, probeEnd.X, pivotArcade.X))
                return false;

            int localY = probeEnd.Y - pivotArcade.Y;
            if (Math.Abs(localY) > 8)
                return false;

            if (localY < 0)
                contactHalf = GateContactHalf.Top;
            else if (localY > 0)
                contactHalf = GateContactHalf.Bottom;
            else
                contactHalf = null;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Detects a blocking gate when the maze probe crosses from one logical cell
    /// to another.
    /// </summary>
    private bool TryGetBlockingGateIdForCellBoundaryStep(
        MazeStepResult mazeStep,
        Vector2I direction,
        out int gateId,
        out GateContactHalf contactHalf)
    {
        gateId = -1;
        contactHalf = GateContactHalf.Left;

        if (direction == Vector2I.Zero)
            return false;

        if (mazeStep.NextCell == mazeStep.CurrentCell)
            return false;

        if (direction.X != 0)
        {
            return TryGetBlockingGateIdAcrossVerticalBoundary(
                mazeStep.CurrentCell,
                mazeStep.NextCell,
                direction,
                out gateId,
                out contactHalf);
        }

        if (direction.Y != 0)
        {
            return TryGetBlockingGateIdAcrossHorizontalBoundary(
                mazeStep.CurrentCell,
                mazeStep.NextCell,
                direction,
                out gateId,
                out contactHalf);
        }

        return false;
    }

    /// <summary>
    /// Checks gates sitting on a vertical cell boundary crossed by left/right movement.
    /// </summary>
    private bool TryGetBlockingGateIdAcrossVerticalBoundary(
        Vector2I currentCell,
        Vector2I nextCell,
        Vector2I direction,
        out int gateId,
        out GateContactHalf contactHalf)
    {
        gateId = -1;
        contactHalf = GateContactHalf.Left;

        int boundaryX = Math.Max(currentCell.X, nextCell.X);

        Vector2I pivotTop = new(boundaryX, currentCell.Y);
        if (TryGetBlockingGateAtPivot(direction, pivotTop, GateContactHalf.Bottom, out gateId, out contactHalf))
            return true;

        Vector2I pivotBottom = new(boundaryX, currentCell.Y + 1);
        if (TryGetBlockingGateAtPivot(direction, pivotBottom, GateContactHalf.Top, out gateId, out contactHalf))
            return true;

        return false;
    }

    /// <summary>
    /// Checks gates sitting on a horizontal cell boundary crossed by up/down movement.
    /// </summary>
    private bool TryGetBlockingGateIdAcrossHorizontalBoundary(
        Vector2I currentCell,
        Vector2I nextCell,
        Vector2I direction,
        out int gateId,
        out GateContactHalf contactHalf)
    {
        gateId = -1;
        contactHalf = GateContactHalf.Left;

        int boundaryY = Math.Max(currentCell.Y, nextCell.Y);

        Vector2I pivotLeft = new(currentCell.X, boundaryY);
        if (TryGetBlockingGateAtPivot(direction, pivotLeft, GateContactHalf.Right, out gateId, out contactHalf))
            return true;

        Vector2I pivotRight = new(currentCell.X + 1, boundaryY);
        if (TryGetBlockingGateAtPivot(direction, pivotRight, GateContactHalf.Left, out gateId, out contactHalf))
            return true;

        return false;
    }

    /// <summary>
    /// Tests whether one candidate gate pivot contains a gate blocking the attempted direction.
    /// </summary>
    private bool TryGetBlockingGateAtPivot(
        Vector2I direction,
        Vector2I pivot,
        GateContactHalf candidateHalf,
        out int gateId,
        out GateContactHalf contactHalf)
    {
        gateId = -1;
        contactHalf = GateContactHalf.Left;

        if (_gateSystem.TryGetGateByPivot(pivot, out RotatingGateRuntimeState gate) &&
            gate.BlocksMovement(direction))
        {
            gateId = gate.Id;
            contactHalf = candidateHalf;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns whether a one-dimensional segment crosses or touches a target coordinate.
    /// </summary>
    private static bool CrossesCoordinate(int start, int end, int target)
    {
        return (start <= target && end >= target) || (start >= target && end <= target);
    }
}
