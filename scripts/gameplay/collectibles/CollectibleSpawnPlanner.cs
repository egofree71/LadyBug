using System;
using System.Collections.Generic;
using Godot;

namespace LadyBug.Gameplay.Collectibles;

/// <summary>
/// Generates the start-of-level placement plan for special collectibles.
/// </summary>
/// <remarks>
/// This planner is responsible only for the initial visual placement of:
/// - 3 letters
/// - 3 hearts
/// - 2 to 6 skulls depending on the level
///
/// It does not handle scoring, color cycling, or player interaction.
/// </remarks>
public static class CollectibleSpawnPlanner
{
    /// <summary>
    /// Generates the special collectible placement plan for one level start.
    /// </summary>
    /// <param name="levelNumber">Current gameplay level number.</param>
    /// <param name="rng">Random number generator used for reproducible picks.</param>
    /// <returns>
    /// A generated spawn plan containing the letters, hearts, and skulls
    /// that should replace some of the base flowers at level start.
    /// </returns>
    public static CollectibleSpawnPlan Generate(int levelNumber, RandomNumberGenerator rng)
    {
        if (rng == null)
        {
            throw new ArgumentNullException(nameof(rng));
        }

        Vector2I[] pickA = DrawFourDistinctAnchors(CollectibleAnchorFamilies.FamilyA, rng);
        Vector2I[] pickB = DrawFourDistinctAnchors(CollectibleAnchorFamilies.FamilyB, rng);
        Vector2I[] pickC = DrawFourDistinctAnchors(CollectibleAnchorFamilies.FamilyC, rng);

        LetterKind[] letters = DrawLevelLetters(rng);
        int[] permutation = DrawLetterPermutation(rng);
        int skullCount = ComputeSkullCount(levelNumber);

        List<CollectiblePlacement> placements = new(3 + 3 + skullCount);

        // Letters:
        // The three letters are first chosen by logical family, then permuted
        // across the three family draw[0] anchors A / B / C.
        placements.Add(new CollectiblePlacement(
            CollectibleKind.Letter,
            pickA[0],
            CollectibleColor.Red,
            letters[permutation[0]]));

        placements.Add(new CollectiblePlacement(
            CollectibleKind.Letter,
            pickB[0],
            CollectibleColor.Red,
            letters[permutation[1]]));

        placements.Add(new CollectiblePlacement(
            CollectibleKind.Letter,
            pickC[0],
            CollectibleColor.Red,
            letters[permutation[2]]));

        // Hearts:
        // Always exactly three hearts, placed on draw[1] for A / B / C.
        placements.Add(new CollectiblePlacement(
            CollectibleKind.Heart,
            pickA[1],
            CollectibleColor.Red));

        placements.Add(new CollectiblePlacement(
            CollectibleKind.Heart,
            pickB[1],
            CollectibleColor.Red));

        placements.Add(new CollectiblePlacement(
            CollectibleKind.Heart,
            pickC[1],
            CollectibleColor.Red));

        // Skulls:
        // Use draw[2] from A / B / C first, then draw[3] from A / B / C if needed.
        Vector2I[] skullCells =
        {
            pickA[2],
            pickB[2],
            pickC[2],
            pickA[3],
            pickB[3],
            pickC[3]
        };

        for (int i = 0; i < skullCount; i++)
        {
            placements.Add(new CollectiblePlacement(
                CollectibleKind.Skull,
                skullCells[i],
                CollectibleColor.White));
        }

        return new CollectibleSpawnPlan(placements);
    }

    /// <summary>
    /// Returns the number of skulls to place for the given level.
    /// </summary>
    public static int ComputeSkullCount(int levelNumber)
    {
        if (levelNumber <= 1)
        {
            return 2;
        }

        if (levelNumber <= 4)
        {
            return 3;
        }

        if (levelNumber <= 9)
        {
            return 4;
        }

        if (levelNumber <= 16)
        {
            return 5;
        }

        return 6;
    }

    /// <summary>
    /// Draws exactly four distinct anchors from one candidate family,
    /// without replacement.
    /// </summary>
    /// <param name="candidates">Candidate anchors for one family.</param>
    /// <param name="rng">Random number generator.</param>
    /// <returns>
    /// An ordered array of four distinct anchors corresponding to draw[0]..draw[3].
    /// </returns>
    private static Vector2I[] DrawFourDistinctAnchors(
        IReadOnlyList<Vector2I> candidates,
        RandomNumberGenerator rng)
    {
        if (candidates == null)
        {
            throw new ArgumentNullException(nameof(candidates));
        }

        if (rng == null)
        {
            throw new ArgumentNullException(nameof(rng));
        }

        if (candidates.Count < 4)
        {
            throw new InvalidOperationException(
                "At least four candidate anchors are required.");
        }

        List<Vector2I> pool = new(candidates);
        Vector2I[] draws = new Vector2I[4];

        for (int i = 0; i < 4; i++)
        {
            int pickedIndex = rng.RandiRange(0, pool.Count - 1);
            draws[i] = pool[pickedIndex];
            pool.RemoveAt(pickedIndex);
        }

        return draws;
    }

    /// <summary>
    /// Chooses the three letters used at the start of the level:
    /// one from A/E, one from SPECIAL, and one from EXTRA.
    /// </summary>
    private static LetterKind[] DrawLevelLetters(RandomNumberGenerator rng)
    {
        if (rng == null)
        {
            throw new ArgumentNullException(nameof(rng));
        }

        LetterKind aeLetter = rng.RandiRange(0, 1) == 0
            ? LetterKind.A
            : LetterKind.E;

        LetterKind specialLetter = rng.RandiRange(0, 4) switch
        {
            0 => LetterKind.S,
            1 => LetterKind.P,
            2 => LetterKind.C,
            3 => LetterKind.I,
            _ => LetterKind.L
        };

        LetterKind extraLetter = rng.RandiRange(0, 2) switch
        {
            0 => LetterKind.X,
            1 => LetterKind.T,
            _ => LetterKind.R
        };

        return new[]
        {
            aeLetter,
            specialLetter,
            extraLetter
        };
    }

    /// <summary>
    /// Generates one random permutation of the three letter-family indices.
    /// </summary>
    /// <remarks>
    /// The returned array contains the indices 0, 1, 2 in shuffled order.
    /// Index meaning:
    /// - 0 = A/E
    /// - 1 = SPECIAL
    /// - 2 = EXTRA
    /// </remarks>
    private static int[] DrawLetterPermutation(RandomNumberGenerator rng)
    {
        if (rng == null)
        {
            throw new ArgumentNullException(nameof(rng));
        }

        int[] permutation = { 0, 1, 2 };

        for (int i = permutation.Length - 1; i > 0; i--)
        {
            int j = rng.RandiRange(0, i);
            (permutation[i], permutation[j]) = (permutation[j], permutation[i]);
        }

        return permutation;
    }
}