using System;
using System.Collections.Generic;
using Godot;

namespace LadyBug.Gameplay.Enemies;

/// <summary>
/// Owns the round-robin temporary chase timers used by enemies.
/// </summary>
/// <remarks>
/// Activation timing and durations follow the reverse-engineered arcade code
/// (routine 0x46D8) and its ROM tables, validated with MAME memory traces:
/// the activation window opens whenever the life-seconds counter B8 satisfies
/// <c>B8 mod 2^k == V</c>, where V comes from the level pattern tables at
/// 0x4788 / 0x47A6 and k is the bit length of V. Chase durations come from the
/// duration table at 0x47AE indexed by elapsed life time (B8 >> 3).
/// Validated activation examples (MAME, level poked in RAM): level 2 -> B8 = 13,
/// 21, 29 (mod 8 == 5); level 4 -> B8 = 11, 15, 19, 23 (mod 4 == 3);
/// level 15 -> B8 = 5, 7, 9, 11 (mod 2 == 1).
/// </remarks>
public sealed class EnemyChaseSystem
{
    // Number of simulation ticks before the arcade-like B8 chase counter advances once.
    private const int B8TickDivider = 60;

    // Arcade caps the parallel life-seconds counter (RAM 0x61B7) at 0xF0, which
    // also bounds the duration/speed ramp indices.
    private const int LifeSecondsCap = 0xF0;

    // ROM 0x4788: visible level (1-based, capped at 30) -> activation pattern index.
    private static readonly byte[] LevelPatternTable =
    {
        0, 2, 3, 4, 1, 2, 3, 2, 3, 4, 3, 4, 5, 5, 6,
        6, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7
    };

    // ROM 0x47A6: pattern index -> activation match value V. The window opens when
    // B8 mod 2^bitlength(V) == V, so V=5/4 -> mod 8, V=3/2 -> mod 4, V=1 -> mod 2.
    private static readonly byte[] PatternValueTable = { 5, 5, 5, 4, 3, 2, 1, 1 };

    // ROM 0x47AE: chase duration in seconds, indexed by (life seconds >> 3).
    // This is the factory-default table (difficulty DIP bit read as 1 in MAME
    // with default switches; observed durations 3,4,4,5,... match it).
    private static readonly byte[] ChaseDurationEasyTable =
    {
        3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10,
        11, 11, 12, 12, 13, 13, 14, 14, 15, 15, 16, 16, 17, 17, 18
    };

    // ROM 0x47CD: alternative duration table selected by the difficulty DIP
    // (hard side). Kept configurable; the exact UI label <-> table mapping was
    // cross-checked against MAME default-switch observations but the hard side
    // was only exercised through direct DIP-bit manipulation.
    private static readonly byte[] ChaseDurationHardTable =
    {
        10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25,
        26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40
    };

    /// <summary>
    /// Selects the hard-difficulty chase duration table (arcade DIP hard side).
    /// </summary>
    /// <remarks>
    /// Default false = factory-default arcade switches, matching every runtime
    /// trace used for validation.
    /// </remarks>
    public static bool UseHardChaseTable { get; set; }

    // Visible level number used by the activation-window tables.
    private readonly int _levelNumber;

    // Small tick divider that advances B8 once per 60 simulation ticks (one second).
    private int _divider;

    // Arcade-inspired life-seconds counter, equivalent of RAM 0x61B8/0x61B7.
    // It is reset by Reset() on each board attempt, matching the observed arcade
    // per-life reset of both counters.
    private int _b8;

    // Selects which enemy slot receives the next chase activation opportunity.
    // Arcade RAM 0x61D2: advances once per open activation window.
    private int _roundRobinIndex;

    /// <summary>
    /// Creates a chase timer system for the current visible level number.
    /// </summary>
    /// <param name="levelNumber">Current level number, used by the activation tables.</param>
    public EnemyChaseSystem(int levelNumber)
    {
        _levelNumber = Math.Max(1, levelNumber);
    }

    /// <summary>
    /// Gets the elapsed life time in seconds, capped like the arcade counter.
    /// </summary>
    /// <remarks>
    /// This is shared with the enemy speed system, whose arcade ramp uses the
    /// same per-life seconds counter (RAM 0x61B7, capped at 0xF0).
    /// </remarks>
    public int LifeSecondsCapped => Math.Min(_b8, LifeSecondsCap);

    /// <summary>
    /// Resets the timing state used to select temporary chase windows.
    /// </summary>
    /// <remarks>
    /// This is used after the player death sequence. The arcade restarts the
    /// enemy pressure from the beginning of the board attempt while preserving
    /// the already-consumed collectibles and the current gate orientations.
    /// </remarks>
    public void Reset()
    {
        _divider = 0;
        _b8 = 0;
        _roundRobinIndex = 0;
    }

    /// <summary>
    /// Advances chase countdowns and possibly activates one round-robin enemy.
    /// </summary>
    /// <param name="monsters">The four enemy slots owned by the enemy runtime.</param>
    /// <remarks>
    /// Timer decrement and activation both happen on the one-second B8 boundary,
    /// matching the arcade evaluation point (0x61B6 == 0x3B).
    /// A waiting lair enemy can be armed too: MAME traces showed activations on
    /// slots whose active bit was still clear, so no activity filter is applied.
    /// </remarks>
    public void AdvanceOneTick(IReadOnlyList<MonsterEntity> monsters)
    {
        _divider++;

        if (_divider < B8TickDivider)
            return;

        _divider = 0;
        _b8++;

        foreach (MonsterEntity monster in monsters)
        {
            if (monster.ChaseTimer > 0)
                monster.ChaseTimer--;
        }

        if (!IsActivationWindowOpen())
            return;

        int selectedIndex = _roundRobinIndex & 0x03;
        _roundRobinIndex = (_roundRobinIndex + 1) & 0x03;

        MonsterEntity selected = monsters[selectedIndex];

        // Arcade skip condition (0x472E): only an already-running chase timer
        // blocks arming. Enemies still waiting in the lair are armed normally;
        // their timer keeps counting down while they wait.
        if (selected.ChaseTimer > 0)
            return;

        selected.ChaseTimer = GetCurrentChaseDuration();
    }

    /// <summary>
    /// Applies BFS guidance as the preferred direction for enemies with active chase timers.
    /// </summary>
    /// <param name="monsters">Enemy slots to update.</param>
    /// <param name="navigationGrid">Current BFS guidance map.</param>
    /// <param name="arcadePixelToLogicalCell">Coordinate conversion supplied by the level.</param>
    /// <remarks>
    /// Only enemies already moving in the maze consume BFS guidance here. An armed
    /// lair enemy receives its first BFS override on the tick after its release,
    /// which matches the arcade within one frame because the release path rewrites
    /// the preferred direction anyway.
    /// </remarks>
    public void ApplyBfsOverride(
        IReadOnlyList<MonsterEntity> monsters,
        EnemyNavigationGrid navigationGrid,
        System.Func<Vector2I, Vector2I> arcadePixelToLogicalCell)
    {
        foreach (MonsterEntity monster in monsters)
        {
            if (!monster.MovementActive || monster.ChaseTimer <= 0)
                continue;

            Vector2I cell = arcadePixelToLogicalCell(monster.ArcadePixelPos);
            MonsterDir bfsDir = navigationGrid.GetBfsDirection(cell);

            if (bfsDir != MonsterDir.None)
                monster.PreferredDirection = bfsDir;
        }
    }

    /// <summary>
    /// Returns whether the current B8 second opens a chase activation window.
    /// </summary>
    /// <remarks>
    /// Arcade routine 0x46D8: V = 0x47A6[0x4788[min(level, 30) - 1]] and the
    /// window is open when <c>B8 mod 2^bitlength(V) == V</c>.
    /// </remarks>
    private bool IsActivationWindowOpen()
    {
        int levelIndex = Math.Min(_levelNumber, LevelPatternTable.Length) - 1;
        int matchValue = PatternValueTable[LevelPatternTable[levelIndex]];
        int modulusMask = GetActivationModulus(matchValue) - 1;

        return (_b8 & modulusMask) == matchValue;
    }

    /// <summary>
    /// Gets the activation window period (2^bitlength) for one match value.
    /// </summary>
    private static int GetActivationModulus(int matchValue)
    {
        int modulus = 1;

        while (modulus <= matchValue)
            modulus <<= 1;

        return modulus;
    }

    /// <summary>
    /// Gets the chase duration loaded when an activation succeeds.
    /// </summary>
    /// <remarks>
    /// Arcade: duration table indexed by (life seconds >> 3), capped at the last
    /// table entry (index 30, reached after four minutes of one board attempt).
    /// </remarks>
    private int GetCurrentChaseDuration()
    {
        byte[] table = UseHardChaseTable ? ChaseDurationHardTable : ChaseDurationEasyTable;
        int index = Math.Min(LifeSecondsCapped >> 3, table.Length - 1);
        return table[index];
    }
}
