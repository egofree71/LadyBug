using Godot;

namespace LadyBug.Gameplay.Collectibles;

/// <summary>
/// Represents one collectible visual placement in the logical maze.
/// </summary>
public readonly struct CollectiblePlacement
{
    public CollectibleKind Kind { get; }
    public Vector2I Cell { get; }
    public CollectibleColor Color { get; }
    public LetterKind Letter { get; }

    public CollectiblePlacement(
        CollectibleKind kind,
        Vector2I cell,
        CollectibleColor color = CollectibleColor.None,
        LetterKind letter = LetterKind.None)
    {
        Kind = kind;
        Cell = cell;
        Color = color;
        Letter = letter;
    }
}