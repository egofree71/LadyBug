using System;
using System.Collections.Generic;
using Godot;

namespace LadyBug.Gameplay.Collectibles;

/// <summary>
/// Generates the start-of-level placement plan for special collectibles.
/// </summary>
/// <remarks>
/// <para>
/// This planner is responsible only for the initial visual placement of:
/// - 3 letters
/// - 3 hearts
/// - 2 to 6 skulls depending on the level
/// </para>
/// <para>
/// It does not handle scoring, color cycling, or player interaction.
/// </para>
/// </remarks>
public static class CollectibleSpawnPlanner
{
    // Used by the transition overlay: the PART screen must preview the same
    // generated special collectibles that the next rebuilt board will receive.
    // The overlay generates and caches the plan, then the next Generate(...) call
    // for that level consumes the cached plan.
    private static int? _cachedPreviewLevelNumber;
    private static CollectibleSpawnPlan? _cachedPreviewSpawnPlan;

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

        int effectiveLevelNumber = Math.Max(1, levelNumber);

        if (_cachedPreviewLevelNumber == effectiveLevelNumber && _cachedPreviewSpawnPlan != null)
        {
            CollectibleSpawnPlan cachedPlan = _cachedPreviewSpawnPlan;
            _cachedPreviewLevelNumber = null;
            _cachedPreviewSpawnPlan = null;
            return cachedPlan;
        }

        return GenerateFresh(effectiveLevelNumber, rng);
    }

    /// <summary>
    /// Generates and caches the upcoming level plan for the transition screen.
    /// </summary>
    /// <remarks>
    /// The next normal <see cref="Generate"/> call for the same level will return
    /// this exact plan. This lets the intermission screen preview the real letters,
    /// hearts, and skull count that will appear after the transition, without
    /// requiring additional state in <c>Level</c>.
    /// </remarks>
    public static CollectibleSpawnPlan GeneratePreviewForTransition(int levelNumber, RandomNumberGenerator rng)
    {
        if (rng == null)
        {
            throw new ArgumentNullException(nameof(rng));
        }

        int effectiveLevelNumber = Math.Max(1, levelNumber);
        CollectibleSpawnPlan plan = GenerateFresh(effectiveLevelNumber, rng);

        _cachedPreviewLevelNumber = effectiveLevelNumber;
        _cachedPreviewSpawnPlan = plan;

        return plan;
    }

    /// <summary>
    /// Builds a fresh special collectible plan without reading or writing the preview cache.
    /// </summary>
    private static CollectibleSpawnPlan GenerateFresh(int levelNumber, RandomNumberGenerator rng)
    {
        Vector2I[] pickA = DrawFourDistinctAnchors(CollectibleAnchorFamilies.FamilyA, rng);
        Vector2I[] pickB = DrawFourDistinctAnchors(CollectibleAnchorFamilies.FamilyB, rng);
        Vector2I[] pickC = DrawFourDistinctAnchors(CollectibleAnchorFamilies.FamilyC, rng);

        LetterKind[] letters = DrawLevelLetters(rng);
        LetterKind[] transitionPreviewLetters = BuildTransitionPreviewLetters(letters);
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

        return new CollectibleSpawnPlan(placements, transitionPreviewLetters);
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
    /// <returns>An ordered array of four distinct anchors corresponding to draw[0]..draw[3].</returns>
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
    /// Chooses the three letters used at the start of the level.
    /// </summary>
    /// <remarks>
    /// The arcade routine around 0x0485 stores the logical choices as:
    /// <c>6070 = A/E</c>, <c>6071 = SPECIAL</c>, <c>6072 = EXTRA</c>.
    ///
    /// This short-term remake approximation intentionally does not emulate the
    /// Z80 refresh register or every state-dependent adjustment. It preserves the
    /// base distribution produced by the observed bit manipulation:
    ///
    /// - A/E: A 50%, E 50%
    /// - SPECIAL: S 12.5%, P 25%, C 25%, I 25%, L 12.5%
    /// - EXTRA: X 25%, T 50%, R 25%
    /// </remarks>
    private static LetterKind[] DrawLevelLetters(RandomNumberGenerator rng)
    {
        if (rng == null)
        {
            throw new ArgumentNullException(nameof(rng));
        }

        LetterKind aeLetter = DrawCommonLetter(rng);
        LetterKind specialLetter = DrawSpecialLetter(rng);
        LetterKind extraLetter = DrawExtraLetter(rng);

        return new[]
        {
            aeLetter,
            specialLetter,
            extraLetter
        };
    }

    /// <summary>
    /// Builds the letter order used by the arcade-style PART transition screen.
    /// </summary>
    /// <remarks>
    /// MAME checks show examples such as RAM <c>6070..6072 = 0,5,8</c> while the
    /// screen displays <c>TIA</c>. Therefore the preview order is reverse logical
    /// family order: EXTRA, SPECIAL, A/E.
    /// </remarks>
    private static LetterKind[] BuildTransitionPreviewLetters(IReadOnlyList<LetterKind> letters)
    {
        if (letters == null)
        {
            throw new ArgumentNullException(nameof(letters));
        }

        if (letters.Count < 3)
        {
            throw new ArgumentException(
                "The letter list must contain A/E, SPECIAL, and EXTRA letters.",
                nameof(letters));
        }

        return new[]
        {
            letters[2],
            letters[1],
            letters[0]
        };
    }

    /// <summary>
    /// Draws the shared A/E letter with an even split.
    /// </summary>
    private static LetterKind DrawCommonLetter(RandomNumberGenerator rng)
    {
        return rng.RandiRange(0, 1) == 0
            ? LetterKind.A
            : LetterKind.E;
    }

    /// <summary>
    /// Draws the SPECIAL-family letter using the base arcade-weighted distribution.
    /// </summary>
    private static LetterKind DrawSpecialLetter(RandomNumberGenerator rng)
    {
        return rng.RandiRange(0, 7) switch
        {
            0 => LetterKind.S,
            1 or 2 => LetterKind.P,
            3 or 4 => LetterKind.C,
            5 or 6 => LetterKind.I,
            _ => LetterKind.L
        };
    }

    /// <summary>
    /// Draws the EXTRA-family letter using the base arcade-weighted distribution.
    /// </summary>
    private static LetterKind DrawExtraLetter(RandomNumberGenerator rng)
    {
        return rng.RandiRange(0, 3) switch
        {
            0 => LetterKind.X,
            1 or 2 => LetterKind.T,
            _ => LetterKind.R
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
    ///
    /// This permutation is used only for the in-maze letter placement. The PART
    /// transition screen uses <see cref="CollectibleSpawnPlan.TransitionPreviewLetters"/>
    /// instead, because MAME shows the preview order as EXTRA, SPECIAL, A/E.
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
