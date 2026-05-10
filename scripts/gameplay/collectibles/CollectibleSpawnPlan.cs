using System;
using System.Collections.Generic;

namespace LadyBug.Gameplay.Collectibles;

/// <summary>
/// Represents the generated start-of-level special collectible plan.
/// </summary>
public sealed class CollectibleSpawnPlan
{
    /// <summary>
    /// Gets the concrete collectible placements applied to the playable maze.
    /// </summary>
    public IReadOnlyList<CollectiblePlacement> Placements { get; }

    /// <summary>
    /// Gets the three letters shown on the arcade-style PART transition screen.
    /// </summary>
    /// <remarks>
    /// The arcade stores the selected letters in RAM-like logical order:
    /// <c>6070 = A/E</c>, <c>6071 = SPECIAL</c>, <c>6072 = EXTRA</c>.
    /// The transition screen displays them visually in reverse logical order:
    /// <c>EXTRA, SPECIAL, A/E</c>.
    ///
    /// This property intentionally describes only the transition-screen preview
    /// order. The actual in-maze placement still uses <see cref="Placements"/> and
    /// can remain independently permuted across the letter anchors.
    /// </remarks>
    public IReadOnlyList<LetterKind> TransitionPreviewLetters { get; }

    public CollectibleSpawnPlan(
        IReadOnlyList<CollectiblePlacement> placements,
        IReadOnlyList<LetterKind>? transitionPreviewLetters = null)
    {
        Placements = placements ?? Array.Empty<CollectiblePlacement>();
        TransitionPreviewLetters = transitionPreviewLetters ?? Array.Empty<LetterKind>();
    }
}
