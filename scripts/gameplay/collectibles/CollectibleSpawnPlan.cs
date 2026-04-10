using System;
using System.Collections.Generic;

namespace LadyBug.Gameplay.Collectibles;

/// <summary>
/// Represents the generated start-of-level special collectible plan.
/// </summary>
public sealed class CollectibleSpawnPlan
{
    public IReadOnlyList<CollectiblePlacement> Placements { get; }

    public CollectibleSpawnPlan(IReadOnlyList<CollectiblePlacement> placements)
    {
        Placements = placements ?? Array.Empty<CollectiblePlacement>();
    }
}