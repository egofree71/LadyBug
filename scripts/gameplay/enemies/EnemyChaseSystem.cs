using System.Collections.Generic;
using Godot;

namespace LadyBug.Gameplay.Enemies;

/// <summary>
/// Owns the round-robin temporary chase timers used by enemies.
/// </summary>
/// <remarks>
/// The current implementation follows the validated early-level activation
/// pattern: level-dependent first activation, then +0x08 B8 units, with one
/// selected enemy by round-robin. Timer values stay configurable and deliberately
/// conservative because the full high-level table is still open.
/// </remarks>
public sealed class EnemyChaseSystem
{
    // Number of simulation ticks before the arcade-like B8 chase counter advances once.
    private const int B8TickDivider = 60;

    // Visible level number used to choose the first chase activation point and durations.
    private readonly int _levelNumber;

    // Small tick divider that slows chase activation down from every frame to B8-like ticks.
    private int _divider;

    // Arcade-inspired chase activation counter. Observed logs call this RAM value B8.
    private int _b8;

    // Selects which enemy slot receives the next chase activation opportunity.
    private int _roundRobinIndex;

    // Counts successful or skipped activation opportunities for duration table lookup.
    private int _activationIndex;

    /// <summary>
    /// Creates a chase timer system for the current visible level number.
    /// </summary>
    /// <param name="levelNumber">Current level number, used to choose early activation timing.</param>
    public EnemyChaseSystem(int levelNumber)
    {
        _levelNumber = levelNumber;
    }

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
        _activationIndex = 0;
    }

    /// <summary>
    /// Advances chase countdowns and possibly activates one round-robin enemy.
    /// </summary>
    /// <param name="monsters">The four enemy slots owned by the enemy runtime.</param>
    /// <remarks>
    /// This method intentionally advances activation on a slower B8-like divider,
    /// matching the observed temporary chase cadence rather than chasing every frame.
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

        if (!ShouldActivateAtCurrentB8())
            return;

        int selectedIndex = _roundRobinIndex & 0x03;
        _roundRobinIndex = (_roundRobinIndex + 1) & 0x03;

        MonsterEntity selected = monsters[selectedIndex];

        if (!selected.MovementActive || selected.ChaseTimer > 0)
        {
            _activationIndex++;
            return;
        }

        selected.ChaseTimer = GetDurationForActivation(_activationIndex);
        _activationIndex++;
    }

    /// <summary>
    /// Applies BFS guidance as the preferred direction for enemies with active chase timers.
    /// </summary>
    /// <param name="monsters">Enemy slots to update.</param>
    /// <param name="navigationGrid">Current BFS guidance map.</param>
    /// <param name="arcadePixelToLogicalCell">Coordinate conversion supplied by the level.</param>
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
    /// Returns whether the current B8-like counter should trigger a chase activation.
    /// </summary>
    private bool ShouldActivateAtCurrentB8()
    {
        int firstActivation = _levelNumber == 1
            ? 0x15
            : _levelNumber < 5
                ? 0x0D
                : 0x05;

        return _b8 >= firstActivation && ((_b8 - firstActivation) % 0x08) == 0;
    }

    /// <summary>
    /// Gets the timer value loaded for one activation opportunity.
    /// </summary>
    private int GetDurationForActivation(int activationIndex)
    {
        if (_levelNumber == 1)
            return 4 + activationIndex / 2;

        if (_levelNumber < 5)
            return 3 + (activationIndex + 1) / 2;

        return 3 + activationIndex / 2;
    }
}
