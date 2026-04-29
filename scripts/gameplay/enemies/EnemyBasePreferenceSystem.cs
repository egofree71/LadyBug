using System.Collections.Generic;
using Godot;

namespace LadyBug.Gameplay.Enemies;

/// <summary>
/// Generates the non-chase preferred directions used by enemy decision logic.
/// </summary>
/// <remarks>
/// This is the arcade-inspired base preference layer that runs before temporary
/// BFS chase override. Reverse engineering showed that the arcade alternates
/// between a deterministic player-direction-derived mode and a pseudo-random
/// mode controlled by a B9-like counter.
/// </remarks>
public sealed class EnemyBasePreferenceSystem
{
    // Level number is kept so later threshold / initial-value tables can be added
    // without changing the public shape of this system.
    private readonly int _levelNumber;

    // Arcade-inspired B9 counter. Level-1 tests observed an initial value around
    // 0xB4 and a deterministic-mode threshold of 0x90.
    private int _b9;

    // Small deterministic PRNG state used as a practical replacement for the Z80
    // R register in the pseudo-random branch. It is intentionally reproducible.
    private uint _randomState;

    /// <summary>
    /// Creates a base preference system for the current visible level.
    /// </summary>
    /// <param name="levelNumber">Visible level number used by timing tables.</param>
    public EnemyBasePreferenceSystem(int levelNumber)
    {
        _levelNumber = levelNumber;
        Reset();
    }

    /// <summary>
    /// Resets the B9-like state used by the base preference generator.
    /// </summary>
    public void Reset()
    {
        _b9 = GetInitialB9ForLevel(_levelNumber);
        _randomState = 0x6D2B79F5u ^ (uint)(_levelNumber * 0x45D9F3B);
    }

    /// <summary>
    /// Recalculates base preferred directions for every active non-chasing enemy.
    /// </summary>
    /// <param name="monsters">Enemy slots to update.</param>
    /// <param name="playerCurrentDirection">Player effective direction, preserved even when input is idle.</param>
    /// <remarks>
    /// Preferred directions are recalculated continuously. Enemies only consume the
    /// current value when they reach decision centers. Temporary BFS chase override
    /// should run after this method and may overwrite these base preferences.
    /// </remarks>
    public void PrepareBasePreferredDirections(
        IReadOnlyList<MonsterEntity> monsters,
        Vector2I playerCurrentDirection)
    {
        if (_b9 >= GetThresholdForLevel(_levelNumber))
            PrepareFromPlayerCurrentDirection(monsters, playerCurrentDirection);
        else
            PreparePseudoRandomDirections(monsters);

        _b9 = (_b9 - 1) & 0xFF;
    }

    /// <summary>
    /// Applies the deterministic player-direction-derived preference pattern.
    /// </summary>
    private static void PrepareFromPlayerCurrentDirection(
        IReadOnlyList<MonsterEntity> monsters,
        Vector2I playerCurrentDirection)
    {
        MonsterDir direction = VectorToMonsterDir(playerCurrentDirection);

        for (int i = 0; i < monsters.Count; i++)
        {
            MonsterEntity monster = monsters[i];

            direction = RotateRight4(direction);

            if (!CanReceiveBasePreference(monster))
                continue;

            monster.PreferredDirection = direction;
        }
    }

    /// <summary>
    /// Applies the pseudo-random preference branch, one generated direction per enemy.
    /// </summary>
    private void PreparePseudoRandomDirections(IReadOnlyList<MonsterEntity> monsters)
    {
        foreach (MonsterEntity monster in monsters)
        {
            MonsterDir direction = RandomPreferredDirection();

            if (!CanReceiveBasePreference(monster))
                continue;

            monster.PreferredDirection = direction;
        }
    }

    /// <summary>
    /// Returns whether one enemy should receive a base preference this tick.
    /// </summary>
    private static bool CanReceiveBasePreference(MonsterEntity monster)
    {
        return monster.MovementActive && monster.ChaseTimer <= 0;
    }

    /// <summary>
    /// Rotates one enemy direction bit through 01, 08, 04, 02, 01.
    /// </summary>
    private static MonsterDir RotateRight4(MonsterDir dir)
    {
        int value = (int)dir;
        int shifted = value >> 1;

        if ((value & 0x01) != 0)
            shifted |= 0x08;

        return (MonsterDir)(shifted & 0x0F);
    }

    /// <summary>
    /// Converts a Godot movement vector into the enemy direction bit encoding.
    /// </summary>
    private static MonsterDir VectorToMonsterDir(Vector2I direction)
    {
        if (direction == Vector2I.Left)
            return MonsterDir.Left;

        if (direction == Vector2I.Up)
            return MonsterDir.Up;

        if (direction == Vector2I.Right)
            return MonsterDir.Right;

        if (direction == Vector2I.Down)
            return MonsterDir.Down;

        return MonsterDir.Up;
    }

    /// <summary>
    /// Generates one pseudo-random preferred direction using the observed branch shape.
    /// </summary>
    private MonsterDir RandomPreferredDirection()
    {
        int value = NextZ80RLikeValue() & 0x0F;
        value >>= 1;
        value += 1;

        if (value < 3)
            return MonsterDir.Left;

        if (value < 5)
            return MonsterDir.Up;

        if (value < 7)
            return MonsterDir.Right;

        return MonsterDir.Down;
    }

    /// <summary>
    /// Returns a deterministic byte used as a practical Z80 R-register substitute.
    /// </summary>
    private int NextZ80RLikeValue()
    {
        _randomState = unchecked(_randomState * 1664525u + 1013904223u);
        return (int)((_randomState >> 16) & 0xFF);
    }

    /// <summary>
    /// Gets the B9 threshold for switching from player-derived to random mode.
    /// </summary>
    private static int GetThresholdForLevel(int levelNumber)
    {
        // Level 1 was validated at 0x90. Keep later levels conservative until
        // additional MAME logs confirm their table values.
        return 0x90;
    }

    /// <summary>
    /// Gets the B9 initial value for the visible level.
    /// </summary>
    private static int GetInitialB9ForLevel(int levelNumber)
    {
        // Level 1 stationary-player tests observed a value around 0xB4.
        // Reuse it for now as a configurable high-level approximation.
        return 0xB4;
    }
}
