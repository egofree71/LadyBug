using Godot;

namespace LadyBug.Gameplay.Gates;

/// <summary>
/// Runtime state of one rotating gate during gameplay.
/// </summary>
/// <remarks>
/// This class represents the mutable gameplay state of a gate:
/// - its logical blocking axis
/// - its visual state
/// - the diagonal turning visual currently used
/// - whether it is currently locked in rotation
/// - and how many ticks remain before the turning visual ends
///
/// It is intentionally separate from any editor-authored scene instance.
/// </remarks>
public sealed class RotatingGateRuntimeState
{
    /// <summary>
    /// Gets the unique gate identifier.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Gets the logical pivot position of the gate.
    /// </summary>
    public Vector2I Pivot { get; }

    /// <summary>
    /// Gets the current logical blocking state of the gate.
    /// </summary>
    public GateLogicalState LogicalState { get; private set; }

    /// <summary>
    /// Gets the current visual presentation state of the gate.
    /// </summary>
    public GateVisualState VisualState { get; private set; }

    /// <summary>
    /// Gets the diagonal turning visual currently used while the gate rotates.
    /// </summary>
    public GateTurningVisual TurningVisual { get; private set; }

    /// <summary>
    /// Gets whether the gate is currently locked in a short rotation phase.
    /// </summary>
    public bool IsRotating { get; private set; }

    /// <summary>
    /// Gets the number of simulation ticks remaining in the turning phase.
    /// </summary>
    public int RotationTicksRemaining { get; private set; }

    /// <summary>
    /// Initializes a new runtime gate state.
    /// </summary>
    /// <param name="id">Unique gate identifier.</param>
    /// <param name="pivot">Logical pivot position of the gate.</param>
    /// <param name="logicalState">Initial logical blocking state.</param>
    public RotatingGateRuntimeState(int id, Vector2I pivot, GateLogicalState logicalState)
    {
        Id = id;
        Pivot = pivot;
        LogicalState = logicalState;
        VisualState = GateVisualState.Stable;
        TurningVisual = GateTurningVisual.Slash;
        IsRotating = false;
        RotationTicksRemaining = 0;
    }

    /// <summary>
    /// Creates a runtime gate state from one stable initial orientation.
    /// </summary>
    /// <param name="id">Unique gate identifier.</param>
    /// <param name="pivot">Logical pivot position of the gate.</param>
    /// <param name="initialOrientation">Initial stable visual orientation.</param>
    /// <returns>A new runtime gate state initialized from that definition.</returns>
    public static RotatingGateRuntimeState FromInitialOrientation(
        int id,
        Vector2I pivot,
        GateOrientation initialOrientation)
    {
        GateLogicalState logicalState =
            initialOrientation == GateOrientation.Horizontal
                ? GateLogicalState.BlocksVertical
                : GateLogicalState.BlocksHorizontal;

        return new RotatingGateRuntimeState(id, pivot, logicalState);
    }

    /// <summary>
    /// Gets the stable visual orientation corresponding to the current logical state.
    /// </summary>
    /// <returns>The stable orientation that should be displayed when not turning.</returns>
    public GateOrientation GetStableOrientation()
    {
        return LogicalState == GateLogicalState.BlocksHorizontal
            ? GateOrientation.Vertical
            : GateOrientation.Horizontal;
    }

    /// <summary>
    /// Determines whether the gate currently blocks movement in the given direction.
    /// </summary>
    /// <param name="moveDir">Attempted one-pixel gameplay movement direction.</param>
    /// <returns>
    /// True if the gate blocks that movement axis; otherwise false.
    /// </returns>
    public bool BlocksMovement(Vector2I moveDir)
    {
        if (moveDir == Vector2I.Zero)
            return false;

        bool horizontalMove = moveDir.X != 0;
        bool verticalMove = moveDir.Y != 0;

        return LogicalState switch
        {
            GateLogicalState.BlocksHorizontal => horizontalMove,
            GateLogicalState.BlocksVertical => verticalMove,
            _ => false
        };
    }

    /// <summary>
    /// Determines whether the gate could be pushed by a movement step
    /// in the given direction.
    /// </summary>
    /// <param name="moveDir">Attempted one-pixel movement direction.</param>
    /// <returns>
    /// True if the gate currently blocks that movement axis and is not already rotating;
    /// otherwise false.
    /// </returns>
    public bool CanBePushedBy(Vector2I moveDir)
    {
        if (IsRotating)
            return false;

        return BlocksMovement(moveDir);
    }

    /// <summary>
    /// Attempts to begin a gate push from the given movement direction and contacted half.
    /// </summary>
    /// <param name="moveDir">Attempted one-pixel movement direction.</param>
    /// <param name="contactHalf">Half of the gate that is being pushed.</param>
    /// <returns>
    /// True if the push is accepted and the gate state changes immediately;
    /// otherwise false.
    /// </returns>
    /// <remarks>
    /// The logical blocking state toggles immediately when the push is accepted.
    /// The visual state then remains in <see cref="GateVisualState.Turning"/>
    /// for a short number of ticks.
    /// </remarks>
    public bool TryBeginPush(Vector2I moveDir, GateContactHalf contactHalf)
    {
        if (!CanBePushedBy(moveDir))
            return false;

        TurningVisual = ComputeTurningVisual(LogicalState, moveDir, contactHalf);
        LogicalState = Toggle(LogicalState);
        VisualState = GateVisualState.Turning;
        IsRotating = true;
        RotationTicksRemaining = GateTuning.TurningTicks;

        return true;
    }

    /// <summary>
    /// Advances the runtime turning timer by one simulation tick.
    /// </summary>
    public void AdvanceOneTick()
    {
        if (!IsRotating)
            return;

        RotationTicksRemaining--;

        if (RotationTicksRemaining <= 0)
        {
            RotationTicksRemaining = 0;
            IsRotating = false;
            VisualState = GateVisualState.Stable;
        }
    }

    private static GateLogicalState Toggle(GateLogicalState state)
    {
        return state == GateLogicalState.BlocksHorizontal
            ? GateLogicalState.BlocksVertical
            : GateLogicalState.BlocksHorizontal;
    }

    private static GateTurningVisual ComputeTurningVisual(
        GateLogicalState logicalState,
        Vector2I moveDir,
        GateContactHalf contactHalf)
    {
        return logicalState switch
        {
            GateLogicalState.BlocksVertical => ComputeTurningVisualFromHorizontalGate(moveDir, contactHalf),
            GateLogicalState.BlocksHorizontal => ComputeTurningVisualFromVerticalGate(moveDir, contactHalf),
            _ => GateTurningVisual.Slash
        };
    }

    private static GateTurningVisual ComputeTurningVisualFromHorizontalGate(
        Vector2I moveDir,
        GateContactHalf contactHalf)
    {
        if (moveDir.Y < 0)
        {
            return contactHalf == GateContactHalf.Right
                ? GateTurningVisual.Slash
                : GateTurningVisual.Backslash;
        }

        if (moveDir.Y > 0)
        {
            return contactHalf == GateContactHalf.Right
                ? GateTurningVisual.Backslash
                : GateTurningVisual.Slash;
        }

        return GateTurningVisual.Slash;
    }

    private static GateTurningVisual ComputeTurningVisualFromVerticalGate(
        Vector2I moveDir,
        GateContactHalf contactHalf)
    {
        if (moveDir.X > 0)
        {
            return contactHalf == GateContactHalf.Top
                ? GateTurningVisual.Slash
                : GateTurningVisual.Backslash;
        }

        if (moveDir.X < 0)
        {
            return contactHalf == GateContactHalf.Top
                ? GateTurningVisual.Backslash
                : GateTurningVisual.Slash;
        }

        return GateTurningVisual.Slash;
    }
}
