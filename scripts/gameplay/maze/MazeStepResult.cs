using Godot;

namespace LadyBug.Gameplay.Maze;

/// <summary>
/// Represents the result of evaluating one arcade-pixel movement step
/// against the logical maze.
/// </summary>
/// <remarks>
/// This helper is intentionally small.
/// It exposes whether the step is allowed and which logical cells are
/// involved in the evaluation.
/// </remarks>
public readonly struct MazeStepResult
{
    /// <summary>
    /// Gets whether the evaluated step is allowed.
    /// </summary>
    public bool Allowed { get; }

    /// <summary>
    /// Gets the logical cell containing the current gameplay position.
    /// </summary>
    public Vector2I CurrentCell { get; }

    /// <summary>
    /// Gets the logical cell reached by the forward probe.
    /// </summary>
    public Vector2I NextCell { get; }

    /// <summary>
    /// Initializes a new maze step result.
    /// </summary>
    /// <param name="allowed">Whether the evaluated step is allowed.</param>
    /// <param name="currentCell">Logical cell containing the current gameplay position.</param>
    /// <param name="nextCell">Logical cell reached by the forward probe.</param>
    public MazeStepResult(bool allowed, Vector2I currentCell, Vector2I nextCell)
    {
        Allowed = allowed;
        CurrentCell = currentCell;
        NextCell = nextCell;
    }
}

