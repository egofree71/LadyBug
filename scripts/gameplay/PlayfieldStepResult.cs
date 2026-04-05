using Godot;
using LadyBug.Gameplay.Gates;
using LadyBug.Gameplay.Maze;

namespace LadyBug.Gameplay;

/// <summary>
/// Represents the combined result of one attempted arcade-pixel step
/// through the active level playfield.
/// </summary>
/// <remarks>
/// This wraps the existing static <see cref="MazeStepResult"/> and adds the
/// possibility that a step is blocked by a dynamic rotating gate.
///
/// When a gate is blocked at its pivot dead zone, <see cref="ContactHalf"/>
/// is null: the step is blocked, but the gate cannot be pushed from that exact
/// contact point.
/// </remarks>
public readonly struct PlayfieldStepResult
{
    /// <summary>
    /// Gets the high-level outcome kind of the attempted step.
    /// </summary>
    public PlayfieldStepKind Kind { get; }

    /// <summary>
    /// Gets the underlying static maze evaluation for the attempted step.
    /// </summary>
    public MazeStepResult MazeStep { get; }

    /// <summary>
    /// Gets the blocking gate identifier when the step is blocked by a gate.
    /// Otherwise null.
    /// </summary>
    public int? GateId { get; }

    /// <summary>
    /// Gets the contacted half of the blocking gate when relevant.
    /// Otherwise null.
    /// </summary>
    public GateContactHalf? ContactHalf { get; }

    /// <summary>
    /// Gets whether the attempted step is allowed.
    /// </summary>
    public bool Allowed => Kind == PlayfieldStepKind.Allowed;

    /// <summary>
    /// Gets the logical cell containing the current gameplay position.
    /// </summary>
    public Vector2I CurrentCell => MazeStep.CurrentCell;

    /// <summary>
    /// Gets the logical cell reached by the forward probe.
    /// </summary>
    public Vector2I NextCell => MazeStep.NextCell;

    /// <summary>
    /// Initializes a new combined playfield step result.
    /// </summary>
    /// <param name="kind">High-level outcome kind.</param>
    /// <param name="mazeStep">Underlying static maze evaluation.</param>
    /// <param name="gateId">Blocking gate identifier if relevant.</param>
    /// <param name="contactHalf">
    /// Contacted gate half if relevant; otherwise null.
    /// </param>
    public PlayfieldStepResult(
        PlayfieldStepKind kind,
        MazeStepResult mazeStep,
        int? gateId = null,
        GateContactHalf? contactHalf = null)
    {
        Kind = kind;
        MazeStep = mazeStep;
        GateId = gateId;
        ContactHalf = contactHalf;
    }

    /// <summary>
    /// Creates an allowed playfield step result.
    /// </summary>
    public static PlayfieldStepResult AllowedStep(MazeStepResult mazeStep)
    {
        return new PlayfieldStepResult(PlayfieldStepKind.Allowed, mazeStep);
    }

    /// <summary>
    /// Creates a fixed-wall blocked playfield step result.
    /// </summary>
    public static PlayfieldStepResult BlockedByFixedWall(MazeStepResult mazeStep)
    {
        return new PlayfieldStepResult(PlayfieldStepKind.BlockedByFixedWall, mazeStep);
    }

    /// <summary>
    /// Creates a gate-blocked playfield step result.
    /// </summary>
    public static PlayfieldStepResult BlockedByGate(
        MazeStepResult mazeStep,
        int gateId,
        GateContactHalf? contactHalf)
    {
        return new PlayfieldStepResult(
            PlayfieldStepKind.BlockedByGate,
            mazeStep,
            gateId,
            contactHalf);
    }

    /// <summary>
    /// Converts a plain static maze result into a playfield result,
    /// assuming no gate is involved.
    /// </summary>
    public static PlayfieldStepResult FromMazeStep(MazeStepResult mazeStep)
    {
        return mazeStep.Allowed
            ? AllowedStep(mazeStep)
            : BlockedByFixedWall(mazeStep);
    }
}
