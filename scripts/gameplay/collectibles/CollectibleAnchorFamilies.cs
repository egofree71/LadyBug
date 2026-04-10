using System.Collections.Generic;
using Godot;

namespace LadyBug.Gameplay.Collectibles;

public static class CollectibleAnchorFamilies
{
    public static readonly IReadOnlyList<Vector2I> FamilyA = new[]
    {
        new Vector2I(0, 1),
        new Vector2I(0, 0),
        new Vector2I(3, 0),
        new Vector2I(4, 0),
        new Vector2I(5, 3),
        new Vector2I(5, 2),
        new Vector2I(5, 1),
        new Vector2I(5, 0),
        new Vector2I(6, 0),
        new Vector2I(7, 0)
    };

    public static readonly IReadOnlyList<Vector2I> FamilyB = new[]
    {
        new Vector2I(0, 10),
        new Vector2I(0, 9),
        new Vector2I(0, 8),
        new Vector2I(0, 7),
        new Vector2I(0, 6),
        new Vector2I(0, 5),
        new Vector2I(0, 4),
        new Vector2I(1, 8),
        new Vector2I(1, 5),
        new Vector2I(1, 4),
        new Vector2I(2, 8),
        new Vector2I(2, 4),
        new Vector2I(3, 10),
        new Vector2I(4, 10),
        new Vector2I(4, 5)
    };

    public static readonly IReadOnlyList<Vector2I> FamilyC = new[]
    {
        new Vector2I(6, 10),
        new Vector2I(6, 5),
        new Vector2I(7, 10),
        new Vector2I(8, 8),
        new Vector2I(8, 4),
        new Vector2I(9, 8),
        new Vector2I(9, 5),
        new Vector2I(9, 4),
        new Vector2I(10, 10),
        new Vector2I(10, 9),
        new Vector2I(10, 8),
        new Vector2I(10, 7),
        new Vector2I(10, 6),
        new Vector2I(10, 5),
        new Vector2I(10, 4)
    };
}