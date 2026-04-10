using System.Collections.Generic;
using Godot;

namespace LadyBug.Gameplay.Collectibles;

public static class CollectibleAnchorFamilies
{
    public static readonly IReadOnlyList<Vector2I> FamilyA = new[]
    {
        new Vector2I(0, 9),
        new Vector2I(0, 10),
        new Vector2I(3, 10),
        new Vector2I(4, 10),
        new Vector2I(5, 7),
        new Vector2I(5, 8),
        new Vector2I(5, 9),
        new Vector2I(5, 10),
        new Vector2I(6, 10),
        new Vector2I(7, 10)
    };

    public static readonly IReadOnlyList<Vector2I> FamilyB = new[]
    {
        new Vector2I(0, 0),
        new Vector2I(0, 1),
        new Vector2I(0, 2),
        new Vector2I(0, 3),
        new Vector2I(0, 4),
        new Vector2I(0, 5),
        new Vector2I(0, 6),
        new Vector2I(1, 2),
        new Vector2I(1, 5),
        new Vector2I(1, 6),
        new Vector2I(2, 2),
        new Vector2I(2, 6),
        new Vector2I(3, 0),
        new Vector2I(4, 0),
        new Vector2I(4, 5)
    };

    public static readonly IReadOnlyList<Vector2I> FamilyC = new[]
    {
        new Vector2I(6, 0),
        new Vector2I(6, 5),
        new Vector2I(7, 0),
        new Vector2I(8, 2),
        new Vector2I(8, 6),
        new Vector2I(9, 2),
        new Vector2I(9, 5),
        new Vector2I(9, 6),
        new Vector2I(10, 0),
        new Vector2I(10, 1),
        new Vector2I(10, 2),
        new Vector2I(10, 3),
        new Vector2I(10, 4),
        new Vector2I(10, 5),
        new Vector2I(10, 6)
    };
}